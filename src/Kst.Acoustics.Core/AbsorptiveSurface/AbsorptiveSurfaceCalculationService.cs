using System;
using System.Collections.Generic;
using System.Linq;

namespace Kst.Acoustics.Core.AbsorptiveSurface;

public static class AbsorptiveSurfaceCalculationService
{
    public static AbsorptiveSurfaceResult Calculate(IEnumerable<AbsorptiveLayer> layers, IEnumerable<AbsorptiveMaterial>? materials = null)
    {
        var materialList = (materials ?? AbsorptiveMaterialDatabase.GetAll()).ToList();
        var activeLayers = layers.Where(l => l.IsEnabled).ToList();
        var result = new AbsorptiveSurfaceResult
        {
            TotalThicknessMm = activeLayers.Sum(l => Math.Max(0, l.ThicknessMm))
        };

        if (activeLayers.Count == 0)
        {
            result.Warnings.Add("Aktif katman bulunmuyor.");
            result.PerformanceSummary = "Hesap yapılamadı.";
            return result;
        }

        double airGapMm = activeLayers
            .Where(l => l.LayerType == AbsorptiveLayerType.AirGap)
            .Sum(l => Math.Max(0, l.ThicknessMm));

        double porousThicknessMm = activeLayers
            .Where(l => l.LayerType == AbsorptiveLayerType.PorousAbsorber)
            .Sum(l => Math.Max(0, l.ThicknessMm));

        // Yığında perfore/slotlu panel varsa arka hava boşluğu panelin Helmholtz
        // kavitesidir (EstimateMaaPanel'de zaten sayılır). Bu durumda poröz λ/4
        // bonusunu uygulama → aynı boşluğun çift sayılmasını önle (D1).
        bool hasResonantPanel = activeLayers.Any(l =>
            l.LayerType is AbsorptiveLayerType.PerforatedPanel or AbsorptiveLayerType.SlottedPanel);

        var bandAlphas = new double[activeLayers.Count];

        foreach (var band in AbsorptiveMaterialDatabase.OctaveBands)
        {
            for (int i = 0; i < activeLayers.Count; i++)
            {
                var layer = activeLayers[i];
                var material = layer.Material
                               ?? materialList.FirstOrDefault(m => m.Code == layer.MaterialCode)
                               ?? AbsorptiveMaterialDatabase.GetByCode(layer.MaterialCode);

                bandAlphas[i] = Math.Clamp(
                    EstimateLayerAbsorption(layer, material, band, airGapMm, porousThicknessMm, hasResonantPanel),
                    0.0, 0.98);
            }

            result.AbsorptionBands[band] = CombineLayers(bandAlphas);
            result.BandRows.Add(new AbsorptiveBandResult { FrequencyHz = band, Absorption = result.AbsorptionBands[band] });
        }

        result.Nrc = RoundToNearest005(AverageBands(result, 250, 500, 1000, 2000));
        result.Saa = Math.Round(AverageBands(result, 250, 500, 1000, 2000, 4000), 2);
        result.AlphaW = RoundToNearest005(AverageBands(result, 500, 1000, 2000, 4000));
        result.PerformanceSummary = BuildSummary(result);
        result.RecommendedUse = BuildRecommendedUse(result);

        AddWarnings(result, activeLayers, airGapMm);
        return result;
    }

    // Oda-yüzeyine bakan baskın emici α'yı yönetir; arkadaki katmanlar yalnız ARTIK
    // enerjiye azalan oranla katkı verir (öndeki emici arkadakini gölgeler). Eski
    // union (1−Π(1−α)) çok katmanlı yığında α'yı yapay biçimde 1.0'a doyuruyordu (D1).
    private const double BackingContribution = 0.5;

    private static double CombineLayers(double[] alphas)
    {
        if (alphas.Length == 0) return 0.0;

        // Azalan sıra: baskın (en yüksek α) önce.
        var sorted = (double[])alphas.Clone();
        Array.Sort(sorted);

        double combined = sorted[^1];                       // baskın katman
        for (int k = sorted.Length - 2; k >= 0; k--)        // zayıftan değil, baskından sonrakiler
            combined += BackingContribution * sorted[k] * (1.0 - combined);

        return Math.Clamp(combined, 0.0, 1.0);
    }

    private static double EstimateLayerAbsorption(AbsorptiveLayer layer, AbsorptiveMaterial? material, double frequency, double airGapMm, double porousThicknessMm, bool hasResonantPanel)
    {
        if (layer.ThicknessMm <= 0)
            return 0;

        material ??= AbsorptiveMaterialDatabase.GetByCode(layer.MaterialCode);
        double baseAlpha = material?.GetAlpha(frequency) ?? 0.05;

        return layer.LayerType switch
        {
            AbsorptiveLayerType.PorousAbsorber => EstimatePorousAbsorber(layer, baseAlpha, frequency, airGapMm, hasResonantPanel),
            AbsorptiveLayerType.Fabric => Math.Min(0.12, baseAlpha + layer.ThicknessMm / 1000.0),
            AbsorptiveLayerType.DecorativeFinish => Math.Min(0.08, baseAlpha),
            AbsorptiveLayerType.RigidBacking => 0.01,
            AbsorptiveLayerType.AirGap => 0.0,
            AbsorptiveLayerType.PerforatedPanel => EstimateMaaPanel(layer, frequency, airGapMm, porousThicknessMm, baseAlpha),
            AbsorptiveLayerType.SlottedPanel => EstimateMaaPanel(layer, frequency, airGapMm, porousThicknessMm, baseAlpha) * 0.9,
            _ => baseAlpha
        };
    }

    /// <summary>
    /// Poröz emici için kalınlık ölçekleme + arka hava boşluğuyla λ/4 rezonansı.
    /// Hava boşluğu λ/4 = (t_poröz + t_air) koşulunda düşük frekansta artış sağlar.
    /// </summary>
    private static double EstimatePorousAbsorber(AbsorptiveLayer layer, double baseAlpha, double frequency, double airGapMm, bool hasResonantPanel)
    {
        double thicknessFactor = 1.0 - Math.Exp(-layer.ThicknessMm / ThicknessReference(AbsorptiveLayerType.PorousAbsorber, frequency));
        double scaled = baseAlpha * (0.45 + thicknessFactor * 0.75);

        // Panel varsa hava boşluğu onun Helmholtz kavitesidir; λ/4 bonusunu burada
        // tekrar ekleme (çift sayım — D1).
        if (airGapMm > 5 && !hasResonantPanel)
        {
            double totalLengthM = (layer.ThicknessMm + airGapMm) / 1000.0;
            double qwFreq = 343.0 / (4.0 * totalLengthM);
            double octaveDist = Math.Abs(Math.Log(frequency / Math.Max(20.0, qwFreq), 2.0));
            // Düşük frekansta belirgin, geniş bantlı (porous damping → broad Q) bir tepe
            double qwBonus = 0.18 * Math.Exp(-octaveDist * octaveDist / 1.6);
            scaled += qwBonus;
        }

        return Math.Clamp(scaled, 0.0, 0.99);
    }

    /// <summary>
    /// Maa formülüne göre perfore/slotlu panel Helmholtz rezonansı.
    ///   f₀ = (c / 2π) · sqrt(σ / (t_eff · D))
    ///   t_eff = t_panel + 2δ,   δ = 0.85 · d/2 = 0.425 d  (yaklaşık uç düzeltme)
    /// Arka tarafa poröz katman konulursa Q faktörü düşer → tepe genişler ve yükselir.
    /// </summary>
    private static double EstimateMaaPanel(AbsorptiveLayer layer, double frequency, double airGapMm, double porousThicknessMm, double baseAlpha)
    {
        double openArea = Math.Clamp(layer.OpenAreaPercent / 100.0, 0.005, 0.50);
        double cavityM = Math.Max(0.02, airGapMm / 1000.0);
        double panelM = Math.Max(0.001, layer.ThicknessMm / 1000.0);
        double holeM = Math.Max(0.001, layer.HoleDiameterMm / 1000.0);

        // Maa uç düzeltmesi: yarıçap başına ~0.85, dolayısıyla iki yüz için ~0.85·d
        double endCorrection = 0.85 * holeM;
        double effectiveNeckM = panelM + endCorrection;

        double resonance = 343.0 / (2.0 * Math.PI) * Math.Sqrt(openArea / (effectiveNeckM * cavityM));

        // Poröz absorber yoksa rezonans dar ve düşük tepeli (Q yüksek, malzeme kayıpsız).
        // Poröz varsa damping ile geniş tepe.
        bool hasBackingAbsorber = porousThicknessMm > 5;
        double bandwidth = hasBackingAbsorber ? 1.2 : 0.45; // oktav cinsinden Gaussian sigma²
        double peakBonus = hasBackingAbsorber ? 0.75 : 0.45;

        double octaveDistance = Math.Abs(Math.Log(frequency / Math.Max(20.0, resonance), 2.0));
        double resonanceBonus = peakBonus * Math.Exp(-octaveDistance * octaveDistance / bandwidth);

        return Math.Clamp(baseAlpha + resonanceBonus, 0.0, 0.97);
    }

    private static double ThicknessReference(AbsorptiveLayerType type, double frequency)
    {
        double wavelengthMm = 343000.0 / Math.Max(63.0, frequency);
        return type == AbsorptiveLayerType.PorousAbsorber
            ? Math.Clamp(wavelengthMm / 10.0, 25.0, 450.0)
            : 50.0;
    }

    private static double AverageBands(AbsorptiveSurfaceResult result, params double[] bands)
    {
        var values = bands
            .Where(result.AbsorptionBands.ContainsKey)
            .Select(b => result.AbsorptionBands[b])
            .ToArray();

        return values.Length == 0 ? 0 : values.Average();
    }

    private static double RoundToNearest005(double value) => Math.Round(value / 0.05, MidpointRounding.AwayFromZero) * 0.05;

    private static string BuildSummary(AbsorptiveSurfaceResult result)
    {
        return result.AlphaW switch
        {
            >= 0.80 => "Çok yüksek ses yutuculuk",
            >= 0.60 => "Yüksek ses yutuculuk",
            >= 0.40 => "Orta seviye ses yutuculuk",
            >= 0.20 => "Sınırlı ses yutuculuk",
            _ => "Zayıf ses yutuculuk"
        };
    }

    private static string BuildRecommendedUse(AbsorptiveSurfaceResult result)
    {
        if (result.AlphaW >= 0.75 && result.AbsorptionBands.GetValueOrDefault(500) >= 0.65)
            return "ofis, sınıf, toplantı odası, restoran ve konuşma hacimleri";
        if (result.AbsorptionBands.GetValueOrDefault(125) >= 0.35 || result.AbsorptionBands.GetValueOrDefault(250) >= 0.45)
            return "düşük frekans desteği isteyen müzik/stüdyo ve çok amaçlı hacimler";
        if (result.AlphaW >= 0.45)
            return "genel tavan/duvar kaplaması ve yankı kontrolü";

        return "dekoratif kaplama; ek poröz katman veya hava boşluğu önerilir";
    }

    private static void AddWarnings(AbsorptiveSurfaceResult result, List<AbsorptiveLayer> layers, double airGapMm)
    {
        if (!layers.Any(l => l.LayerType == AbsorptiveLayerType.PorousAbsorber))
            result.Warnings.Add("Poröz emici katman yok; geniş bant performans sınırlı kalabilir.");

        if (result.AbsorptionBands.GetValueOrDefault(125) < 0.25)
            result.Warnings.Add("125 Hz düşük frekans yutuculuğu zayıf; kalınlık veya hava boşluğu artırılabilir.");

        if (layers.Any(l => l.LayerType is AbsorptiveLayerType.PerforatedPanel or AbsorptiveLayerType.SlottedPanel) && airGapMm < 20)
            result.Warnings.Add("Perfore/slotlu yüzey için arka hava boşluğu çok az; rezonans etkisi sınırlı olur.");

        if (layers.Any(l => l.LayerType == AbsorptiveLayerType.DecorativeFinish))
            result.Warnings.Add("Dekoratif kaplama hava geçirgen değilse yüksek frekans emilimini düşürebilir.");
    }
}
