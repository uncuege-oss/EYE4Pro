using System;
using System.Collections.Generic;
using System.Linq;

namespace Kst.Acoustics.Core.SoundInsulation;

public static class SoundInsulationCalculator
{
    private const double C0 = 343.0; // Speed of sound in air (m/s)
    private const double Rho0 = 1.21; // Density of air (kg/m3)

    // Fiziksel üst sınır: sonlu eleman boyutu, kenar/sızıntı yolları ve laboratuvar ölçüm
    // tabanı nedeniyle gerçek bir bina elemanı bandı bu değeri pratikte aşmaz. Ampirik
    // modelin yüksek frekansta sınırsız büyümesine karşı güvenlik ağı.
    private const double MaxRealisticTl = 85.0; // dB

    // Coincidence çukurundan kütle kanununa toparlanmanın tamamlandığı bant genişliği (oktav).
    private const double CoincidenceRecoveryOctaves = 0.75;

    // Coincidence çukurunun pratik üst sınırı (en derin). Sonsuz panelde çok düşük sönümlü
    // malzemeler (cam η≈0.002, çelik η≈0.0001) için formül -20..-35 dB verir; gerçek 1/3
    // oktav ölçümlerinde çukur 10-15 dB ile sınırlıdır (sonlu boyut, kenar sönümü).
    private const double MaxCoincidenceDipDb = -15.0;

    public static CompositeResult CalculateComposite(List<CompositeElement> elements)
    {
        var totalArea = elements.Sum(e => e.Area);
        var compositeTl = new Dictionary<int, double>();
        
        // Calculate each part separately
        var partResults = elements.Select(e => new { e.Area, Result = Calculate(e.Assembly) }).ToList();

        foreach (var f in SoundInsulationConstants.ThirdOctaveBands)
        {
            double totalTauS = 0;
            foreach (var part in partResults)
            {
                if (part.Result.TransmissionLossValues.TryGetValue(f, out double tl))
                {
                    double tau = Math.Pow(10, -tl / 10.0);
                    totalTauS += tau * part.Area;
                }
            }
            
            double avgTau = totalArea > 0 ? totalTauS / totalArea : 1.0;
            compositeTl[f] = -10 * Math.Log10(Math.Max(avgTau, 1e-12));
        }

        var rating = RwRatingCalculator.Calculate(compositeTl);
        return new CompositeResult(elements, compositeTl, rating, totalArea);
    }

    public static SoundInsulationResult Calculate(WallAssembly assembly)
    {
        var tlValues = new Dictionary<int, double>();
        var warnings = new List<string>();
        var criticalFreqs = new Dictionary<string, double>();
        double mainMamResonance = 0;

        if (assembly.Layers.Count == 0)
        {
            warnings.Add("Sistemde katman bulunamadı.");
            return new SoundInsulationResult(assembly, tlValues, warnings);
        }

        // ── Hesap yöntemi yönlendirmesi (TMM / Hybrid / Empirical) ────────────
        if (assembly.Method == CalculationMethod.TMM)
        {
            return CalculateTmm(assembly);
        }
        if (assembly.Method == CalculationMethod.Hybrid)
        {
            return CalculateHybrid(assembly);
        }

        if (!assembly.Layers.Any(l => !l.IsAirGap))
        {
            warnings.Add("Sistemde masif (kütleli) katman bulunamadı. Yalnızca dolgu/boşluk katmanları ses yalıtımı sağlamaz.");
        }

        // Identify and group layers by gaps
        var leafGroups = new List<List<SoundInsulationLayer>>();
        var gaps = new List<SoundInsulationLayer>();
        
        var currentGroup = new List<SoundInsulationLayer>();
        foreach (var layer in assembly.Layers)
        {
            if (layer.IsAirGap)
            {
                if (currentGroup.Count > 0) leafGroups.Add(currentGroup);
                currentGroup = new List<SoundInsulationLayer>();
                gaps.Add(layer);
            }
            else
            {
                currentGroup.Add(layer);
            }
        }
        if (currentGroup.Count > 0) leafGroups.Add(currentGroup);

        // Analysis of findings
        if (leafGroups.Count == 1)
        {
            // Single Wall
            var layers = leafGroups[0];
            foreach (var f in SoundInsulationConstants.ThirdOctaveBands)
            {
                tlValues[f] = Math.Min(MaxRealisticTl, CalculateLeafGroupTL(layers, f));
            }
            
            // Add critical frequencies for report
            for (int i = 0; i < layers.Count; i++)
            {
                var l = layers[i];
                var fc = l.Material.EstimateCriticalFrequency(l.Thickness);
                if (fc > 0) 
                {
                    string key = layers.Count > 1 ? $"{i + 1}. {l.Material.Name}" : l.Material.Name;
                    criticalFreqs[key] = fc;
                }
            }
        }
        else if (leafGroups.Count == 2)
        {
            // Double Wall (Standard)
            var m1Layers = leafGroups[0];
            var m2Layers = leafGroups[1];
            var gap = gaps[0];
            
            double m1 = m1Layers.Sum(l => l.SurfaceMass);
            double m2 = m2Layers.Sum(l => l.SurfaceMass);
            mainMamResonance = CalculateMAMResonance(m1, m2, gap.Thickness);

            foreach (var f in SoundInsulationConstants.ThirdOctaveBands)
            {
                tlValues[f] = Math.Min(MaxRealisticTl, CalculateDoubleLayerTL(assembly, m1Layers, m2Layers, gap, f, mainMamResonance));
            }
        }
        else
        {
            // Triple Leaf or more (Warning and Approximation)
            warnings.Add("Üç veya daha fazla yapraklı sistemler (Triple Leaf) tespit edildi. Bu sistemlerde rezonans etkileri daha karmaşıktır; sonuçlar kütle kanunu sınırlarına yaklaşabilir.");
            
            // Simplified: calculate as double wall using first and last groups, then apply Triple Leaf penalty
            var first = leafGroups.First();
            var last = leafGroups.Last();
            var totalGap = gaps.Sum(g => g.Thickness);
            var m1 = first.Sum(l => l.SurfaceMass);
            var m2 = last.Sum(l => l.SurfaceMass);
            mainMamResonance = CalculateMAMResonance(m1, m2, totalGap);

            // Tüm boşlukları temsil eden sentetik tek boşluk: kalınlık = toplam boşluk.
            // ESKİ TUTARSIZLIK: f0 totalGap ile, ama CalculateDoubleLayerTL'ye gaps[0] geçiliyordu
            // (kavite d'si, f_l ve fillBonus tek boşluğa göre). Artık ikisi de totalGap kullanır.
            // Poröz dolgu bilgisi varsa dolgulu boşluğu temsilci alır.
            var representativeGap = (gaps.FirstOrDefault(g => g.HasPorousFill) ?? gaps[0])
                with { Thickness = totalGap };

            foreach (var f in SoundInsulationConstants.ThirdOctaveBands)
            {
                double baseTl = CalculateDoubleLayerTL(assembly, first, last, representativeGap, f, mainMamResonance);
                // Triple leaf effect: often 5-10 dB worse at low-mid frequencies than same mass in double leaf
                tlValues[f] = Math.Min(MaxRealisticTl, Math.Max(CalculateMassLaw(assembly.TotalSurfaceMass, f), baseTl - 6.0));
            }
        }

        var rwRating = RwRatingCalculator.Calculate(tlValues);

        // Calculate Reliability Score
        double score = 100;
        if (leafGroups.Count > 2) score -= 20; // Triple leaf is approximate
        if (warnings.Any(w => w.Contains("masif"))) score -= 50;
        foreach (var l in assembly.Layers)
        {
            if (l.Material is not null && string.Equals(l.Material.Source, "Estimated", StringComparison.OrdinalIgnoreCase))
                score -= 5;
        }

        return new SoundInsulationResult(assembly, tlValues, warnings, mainMamResonance, criticalFreqs, rwRating, Math.Max(0, score));
    }

    // ── TMM yolu ──────────────────────────────────────────────────────────
    private static SoundInsulationResult CalculateTmm(WallAssembly assembly)
    {
        var warnings = new List<string>();
        var criticalFreqs = new Dictionary<string, double>();

        var tl = TransferMatrixCalculator.Calculate(assembly);

        // Bilgilendirici: kritik frekanslar (TL'i değiştirmez, raporlama için)
        foreach (var layer in assembly.Layers.Where(l => !l.IsAirGap))
        {
            var fc = layer.Material.EstimateCriticalFrequency(layer.Thickness);
            if (fc > 0)
                criticalFreqs[$"{layer.Material.Name} (TMM)"] = fc;
        }

        // Çift cidar için MAM bilgi notu
        double mam = 0;
        var leafGroups = GroupLeaves(assembly, out var gaps);
        if (leafGroups.Count == 2)
        {
            double m1 = leafGroups[0].Sum(l => l.SurfaceMass);
            double m2 = leafGroups[1].Sum(l => l.SurfaceMass);
            mam = CalculateMAMResonance(m1, m2, gaps[0].Thickness);
        }

        if (assembly.StudSpacing > 0 && !string.Equals(assembly.ConnectionType, "None", StringComparison.OrdinalIgnoreCase))
            warnings.Add("TMM modunda mekanik köprüleme yalnız sınırlı yansıtılır; bağlantı etkisi sonradan empirical eklemeyle uygulanır.");

        var rw = RwRatingCalculator.Calculate(tl);

        double score = 90; // TMM doğru parametrelerle daha iyi; default 90
        foreach (var l in assembly.Layers)
        {
            if (l.Material is null) continue;
            if (l.HasPorousFill && l.Material.FlowResistivity <= 0)
            {
                score -= 15;
                warnings.Add($"Poröz katman '{l.Material.Name}' için akış direnci (σ) tanımlanmamış; TMM tahmini doğruluğu düşer.");
            }
            if (string.Equals(l.Material.Source, "Estimated", StringComparison.OrdinalIgnoreCase))
                score -= 5;
        }

        return new SoundInsulationResult(assembly, tl, warnings, mam, criticalFreqs, rw, Math.Max(0, score));
    }

    // ── Hybrid yolu (düşük freq TMM, yüksek freq empirical, geçişte interp) ──
    private static SoundInsulationResult CalculateHybrid(WallAssembly assembly)
    {
        var empirical = Calculate(assembly with { Method = CalculationMethod.Empirical });
        var tmm = TransferMatrixCalculator.Calculate(assembly);

        const int crossoverHz = 500;
        const int bandWidthHz = 200;

        var merged = new Dictionary<int, double>();
        foreach (var f in SoundInsulationConstants.ThirdOctaveBands)
        {
            double tlTmm = tmm.TryGetValue(f, out var v1) ? v1 : 0;
            double tlEmp = empirical.TransmissionLossValues.TryGetValue(f, out var v2) ? v2 : 0;

            // Yumuşak smoothstep ağırlığı: <300 Hz tam TMM, >700 Hz tam empirical
            double low = crossoverHz - bandWidthHz;
            double high = crossoverHz + bandWidthHz;
            double t = Math.Clamp((f - low) / (double)(high - low), 0, 1);
            double w = t * t * (3 - 2 * t); // smoothstep
            merged[f] = (1 - w) * tlTmm + w * tlEmp;
        }

        var warnings = new List<string>(empirical.Warnings)
        {
            "Hybrid mod: ≤300 Hz TMM, ≥700 Hz empirical, ara bantta yumuşak geçiş uygulandı.",
        };
        var rw = RwRatingCalculator.Calculate(merged);
        return new SoundInsulationResult(
            assembly, merged, warnings,
            empirical.MassAirMassResonance,
            empirical.CriticalFrequencies,
            rw,
            (empirical.ReliabilityScore + 90) / 2);
    }

    private static List<List<SoundInsulationLayer>> GroupLeaves(
        WallAssembly assembly, out List<SoundInsulationLayer> gaps)
    {
        var groups = new List<List<SoundInsulationLayer>>();
        gaps = new List<SoundInsulationLayer>();
        var current = new List<SoundInsulationLayer>();
        foreach (var layer in assembly.Layers)
        {
            if (layer.IsAirGap)
            {
                if (current.Count > 0) groups.Add(current);
                current = new List<SoundInsulationLayer>();
                gaps.Add(layer);
            }
            else current.Add(layer);
        }
        if (current.Count > 0) groups.Add(current);
        return groups;
    }

    private static double CalculateSingleLayerTL(SoundInsulationLayer layer, double f, double fc)
    {
        double massTL = CalculateMassLaw(layer.SurfaceMass, f);

        // Kritik frekans yoksa (elastik veri yok / poröz katman) saf kütle kanunu.
        if (fc <= 0 || f < fc * 0.5)
        {
            return massTL;
        }

        // Coincidence çukur derinliği (Bies & Hansen): R(fc) ≈ R_mass + 10·log10(η) + 5, negatif.
        double eta = Math.Clamp(layer.Material.LossFactor, 1e-4, 0.5);
        double dipDepth = Math.Max(MaxCoincidenceDipDb, 10 * Math.Log10(eta) + 5.0);

        if (f < fc)
        {
            // fc'ye yaklaşırken kütle kanunundan çukura yumuşak geçiş (0.5·fc → fc).
            double t = Math.Clamp((f - fc * 0.5) / (fc * 0.5), 0, 1);
            double w = t * t * (3 - 2 * t); // smoothstep
            return Math.Max(8.0, massTL + w * dipDepth);
        }

        // f ≥ fc : çukurdan kütle-kanunu(f) + sınırlı plateau'ya yumuşak toparlanma.
        // ESKİ HATA: sabit plateauTL(fc) + 10·oktav → kütle kanununu sınırsız aşıyordu
        // (200mm beton 5000 Hz'de 100 dB). Artık massLaw(f)'e çapalı, sınırlı.
        double plateauExcess = Math.Clamp(10 * Math.Log10(eta) + 15.0, 0.0, 6.0);
        double octavesAbove = Math.Log2(f / fc);
        double tu = Math.Clamp(octavesAbove / CoincidenceRecoveryOctaves, 0, 1);
        double wu = tu * tu * (3 - 2 * tu); // smoothstep
        double delta = (1 - wu) * dipDepth + wu * plateauExcess;
        return Math.Max(8.0, massTL + delta);
    }

    private static double CalculateMassLaw(double ms, double f)
    {
        if (ms <= 0 || f <= 0) return 0;
        // Random incidence mass law (Field approximation): R = 20 log10(ms * f) - 47
        return Math.Max(0, 20 * Math.Log10(ms * f) - 47);
    }

    private static double CalculateMAMResonance(double m1, double m2, double d)
    {
        if (m1 <= 0 || m2 <= 0 || d <= 0) return 0;
        // f0 = 60 * sqrt( (m1 + m2) / (d * m1 * m2) ) where d is in meters
        return 60.0 * Math.Sqrt((m1 + m2) / (d * m1 * m2));
    }

    private static double CalculateDoubleLayerTL(
        WallAssembly assembly,
        IReadOnlyList<SoundInsulationLayer> m1Layers,
        IReadOnlyList<SoundInsulationLayer> m2Layers,
        SoundInsulationLayer? gapLayer,
        double f,
        double f0)
    {
        double m1 = m1Layers.Sum(l => l.SurfaceMass);
        double m2 = m2Layers.Sum(l => l.SurfaceMass);
        double d = gapLayer?.Thickness ?? 0.01;
        double r1 = CalculateLeafGroupTL(m1Layers, f);
        double r2 = CalculateLeafGroupTL(m2Layers, f);
        double rSum = CalculateMassLaw(m1 + m2, f);

        // Ayrıştırılmış kavite yükselişi (Sharp, SI sabit -19; codex §9).
        // Sınırlayıcı frekans f_l = c/(2π·d): boşluk derinliği akustik dalga boyu mertebesine
        // ulaşınca kavite katkısı doygunlaşır (§A — eski f_l'siz model 186 dB üretiyordu).
        var fillBonus = CalculatePorousFillBonus(gapLayer, f, f0);
        double fLimit = C0 / (2.0 * Math.PI * Math.Max(d, 1e-3));
        double fEff = Math.Min(f, fLimit);
        double rCavity = r1 + r2 + 20.0 * Math.Log10(Math.Max(fEff * d, 1e-9)) - 19.0 + fillBonus;

        // Taban: f0 altında yapraklar birlikte hareket eder (toplam kütle kanunu); f0 üstünde
        // ayrıştırılmış kavite yükselişi baskındır.
        double baseline = Math.Max(rSum, rCavity);

        // Dar mass-air-mass rezonans çentiği: log-frekansta Gauss, f0'da en derin, ±~1/3 oktavda
        // toparlanır. ESKİ HATA: f0·0.7..f0·1.4 arası DÜZ ceza, rezonans bandının kenarlarını da
        // (örn. f0=120 iken 100 ve 160 Hz) cezalandırıp ayrıştırılmış ideali gerçek-dışı düşük
        // tutuyordu → RC/Clips ile Direct arasındaki fark kapanıyordu. Poröz dolgu çentiği
        // söndürür (sığ); boş hava boşluğunda derin.
        double rIdeal = baseline;
        if (f0 > 0)
        {
            double dipDepth = (gapLayer?.HasPorousFill ?? false) ? 7.0 : 12.0;
            double oct = Math.Log2(f / f0);
            const double sigma = 0.30; // ~1/3 oktav yarı-genişlik
            double notch = dipDepth * Math.Exp(-(oct * oct) / (2.0 * sigma * sigma));
            rIdeal = Math.Max(5, baseline - notch);
        }

        // Apply bridging effect
        if (assembly.StudSpacing > 0 && !string.Equals(assembly.ConnectionType, "None", StringComparison.OrdinalIgnoreCase))
        {
            double rBridge = CalculateBridgeTL(assembly, m1, m2, f);
            // Combine paths: tau_tot = tau_ideal + tau_bridge
            return -10 * Math.Log10(Math.Pow(10, -rIdeal / 10.0) + Math.Pow(10, -rBridge / 10.0));
        }

        return rIdeal;
    }

    private static double CalculateLeafGroupTL(IReadOnlyList<SoundInsulationLayer> layers, double f)
    {
        if (layers.Count == 0) return 0;
        if (layers.Count == 1)
        {
            var layer = layers[0];
            var fc = layer.Material.EstimateCriticalFrequency(layer.Thickness);
            return CalculateSingleLayerTL(layer, f, fc);
        }

        var totalMass = layers.Sum(l => l.SurfaceMass);
        var tl = CalculateMassLaw(totalMass, f);
        foreach (var layer in layers)
        {
            var fc = layer.Material.EstimateCriticalFrequency(layer.Thickness);
            if (fc > 0 && f >= fc * 0.8 && f <= fc * 1.2)
            {
                var dip = Math.Clamp(10 * Math.Log10(Math.Max(layer.Material.LossFactor, 1e-6)) + 5, -25, 0);
                tl += dip / layers.Count;
            }
        }
        return Math.Max(5, tl);
    }

    private static double CalculatePorousFillBonus(SoundInsulationLayer? gapLayer, double f, double f0)
    {
        if (gapLayer is null || !gapLayer.HasPorousFill || f <= f0) return 0;

        var flow = Math.Max(gapLayer.Material.FlowResistivity, 1000.0);
        var porosity = Math.Clamp(gapLayer.Material.Porosity, 0.1, 1.0);
        var tortuosity = Math.Clamp(gapLayer.Material.Tortuosity, 1.0, 5.0);
        var flowBonus = 1.5 + (2.0 * Math.Log10(flow / 5000.0));
        var materialFactor = Math.Sqrt(porosity / tortuosity);
        var freqFactor = Math.Clamp(Math.Log2(f / f0), 0.0, 2.0) / 2.0;

        return Math.Clamp(flowBonus * materialFactor * freqFactor, 0.0, 6.0);
    }

    private static double CalculateBridgeTL(WallAssembly assembly, double m1, double m2, double f)
    {
        // Yapı-kaynaklı (köprü) iletim platosu: dikmeler üstünden by-pass yolu. Yaprakların
        // kütle kanunu mertebesinde, bağlantı tipi platoyu yukarı kaydırır (ayrıştırma arttıkça).
        // ESKİ KALİBRASYON: Direct=-5 platoyu çok düşük tutuyordu → ince çelik-stud duvar
        // (MAM-sınırlı, ideal ~39) yanlışlıkla Rw 28'e çöküyordu. Çelik stud gerçekte bu tip
        // duvarı yalnız birkaç dB düşürür; köprü esas yüksek-frekans platosunu sınırlar.
        double baseBridge = 20 * Math.Log10(f * (m1 + m2)) - 50;
        var spacingFactor = assembly.StudSpacing > 0
            ? 10.0 * Math.Log10(0.6 / Math.Clamp(assembly.StudSpacing, 0.2, 1.2))
            : 0.0;

        double offset = assembly.ConnectionType switch
        {
            "Direct" => 6.0,             // çelik stud doğrudan vidalı (tipik baz durum)
            "ResilientChannel" => 16.0,  // esnek profil ile yarı ayrıştırma
            "Clips" => 24.0,             // akustik klips / bağımsız karkas
            _ => 0.0
        };
        return baseBridge + offset + spacingFactor;
    }
}
