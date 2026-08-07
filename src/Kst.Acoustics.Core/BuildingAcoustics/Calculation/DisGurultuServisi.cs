using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.BuildingAcoustics.Calculation;

public class DisGurultuServisi
{
    public static readonly int[] ExtendedThirdOctaveFreqler =
        [50, 63, 80, 100, 125, 160, 200, 250, 315, 400, 500, 630, 800, 1000, 1250, 1600, 2000, 2500, 3150];

    public static bool TamDisGurultuSpektrumuVar(BinaProje proje) =>
        proje.DisGurultuThirdOctave.Count >= ExtendedThirdOctaveFreqler.Length &&
        proje.DisGurultuThirdOctave.Take(ExtendedThirdOctaveFreqler.Length)
            .All(v => double.IsFinite(v) && v > 0);

    /// <summary>
    /// EN 12354-3:2000 Annex C, Figure C.2 — cephe form düzeltmesi ΔL_fs (dB).
    /// Standart formül: D_2m,nT = R' + ΔL_fs + 10·log10(V/(6·T₀·S)).  ΔL_fs EKLENİR.
    /// İşaret: pozitif = cephe formu korumayı ARTIRIR (D_2m,nT yükselir).
    /// Değerler Figure C.2'nin (1,5–2,5)m görüş mesafesi satırından alınmıştır (standart
    /// metniyle doğrulandı, 2026-06-10); tam tablo için <see cref="DeltaLfsTam"/> kullanılır.
    /// </summary>
    public static double DeltaLfs(CepheMaruziyet maruziyet) => maruziyet switch
    {
        CepheMaruziyet.SerbestAlan            =>  0.0,   // form 1 (düz cephe) — her koşulda 0
        CepheMaruziyet.TrafikCephesi          => -3.0,   // Figure C.2 dışı: Lgag yansıma-hariç tanımlandığından
                                                         // yansıtıcı karşı cephe/sokak kanyonu maruziyeti artırır
        CepheMaruziyet.BalkonYansiticiTavanli =>  0.0,   // form 7, α≤0,3, (1,5–2,5)m → 0
        CepheMaruziyet.BalkonYutucuTavanli    =>  4.0,   // form 7, α≥0,9, (1,5–2,5)m → +4
        CepheMaruziyet.Loggia                 =>  2.0,   // form 8 (gömme balkon/loggia), α≥0,9 → +2
        _                                     =>  0.0,
    };

    // ── EN 12354-3 Figure C.2 — tam ΔL_fs (form × tavan soğurması × görüş mesafesi) ──

    public enum CepheFormu { DuzCephe, Galeri, Balkon, Teras }
    public enum CatiSogurmasi { Az, Orta, Yuksek }     // α_w: ≤0,3 | 0,6 | ≥0,9
    public enum GorusMesafesi { Yakin, Orta, Uzak }    // cephede görüş hattı: <1,5 | 1,5–2,5 | >2,5 m

    /// <summary>
    /// EN 12354-3:2000 Figure C.2 tam ΔL_fs (dB) — standart metniyle hücre hücre doğrulandı
    /// (2026-06-10). Temsili formlar: düz cephe (form 1), galeri (form 3), balkon (form 7),
    /// teras (form 9, açık çit). Değer = D_2m,nT'ye EKLENİR.
    /// </summary>
    public static double DeltaLfsTam(CepheFormu form, CatiSogurmasi cati, GorusMesafesi gorus)
    {
        // [cati 0=Az,1=Orta,2=Yuksek][gorus 0=Yakin,1=Orta,2=Uzak]
        int c = (int)cati, g = (int)gorus;
        return form switch
        {
            CepheFormu.DuzCephe => 0.0,                              // form 1 — her koşulda 0
            CepheFormu.Galeri => new[,] {                            // form 3
                { -1.0, -1.0,  1.0 },   // α≤0,3
                { -1.0,  0.0,  1.0 },   // α=0,6
                {  0.0,  2.0,  2.0 },   // α≥0,9
            }[c, g],
            CepheFormu.Balkon => new[,] {                           // form 7
                {  0.0,  0.0,  2.0 },   // α≤0,3 (yansıtıcı)
                {  0.0,  2.0,  3.0 },   // α=0,6
                {  1.0,  4.0,  4.0 },   // α≥0,9 (yutucu)
            }[c, g],
            CepheFormu.Teras => new[,] {                            // form 9, açık çit
                {  1.0,  4.0,  4.0 },   // α≤0,3
                {  1.0,  4.0,  4.0 },   // α=0,6
                {  1.0,  5.0,  6.0 },   // α≥0,9
            }[c, g],
            _ => 0.0,
        };
    }

    /// <summary>
    /// TS EN 12354-3 — Dış cephe bileşik ses azaltma indisi ve standartize D_2m,nT.
    /// R_composite = -10·log10(Σ(Si·τi) / Stotal)
    /// D_2m,nT = R_composite + ΔL_fs + 10·log10(V/(6·T₀·S))   [ΔL_fs EKLENİR — ISO 12354-3]
    /// </summary>
    public double HesaplaDisCepheYalitimi(Mekan mekan, List<Bolme> cepheElemanlari,
        CepheMaruziyet maruziyet = CepheMaruziyet.SerbestAlan)
    {
        if (mekan.Hacim <= 0 || cepheElemanlari == null || !cepheElemanlari.Any() ||
            !CepheGirdileriTam(cepheElemanlari))
            return 0;

        double totalArea = cepheElemanlari.Sum(e => e.Alan);
        if (totalArea <= 0) return 0;

        double weightedTau = 0;
        foreach (var eleman in cepheElemanlari)
        {
            // Bileşik Rw (pencere/kapı açıklıkları dahil)
            double rw = HavaSesiHesapServisi.CalculateCompositeRw(eleman);
            if (rw <= 0) return 0;

            weightedTau += eleman.Alan * Math.Pow(10, -rw / 10.0);
        }

        if (weightedTau <= 0) return 0;

        // Bileşik cephe ses azaltma indisi
        double rComposite = -10 * Math.Log10(weightedTau / totalArea);

        // Cephe şekli düzeltmesi ΔL_fs (TS EN 12354-3 Tablo 1) — D_2m,nT'ye EKLENİR.
        double deltaLfs = DeltaLfs(maruziyet);

        // EN 12354-3: D_2m,nT,w = R'_composite + ΔL_fs + 10·log10(V/(6·T₀·S))   [T₀ = 0.5 s]
        double volumeNorm = FacadeVolumeNormalization(mekan, totalArea);

        return rComposite + deltaLfs + volumeNorm;
    }

    /// EN 12354-3 cephe hacim normalizasyonu: 10·log10(V / (6·T₀·S)), T₀ = 0.5 s.
    /// Eksik girdi varsa 0 döner (eski davranışla uyumlu fallback).
    public static double FacadeVolumeNormalization(Mekan? mekan, double facadeArea)
    {
        if (mekan == null || mekan.Hacim <= 0 || facadeArea <= 0) return 0;
        const double t0 = 0.5;
        return 10.0 * Math.Log10(mekan.Hacim / (6.0 * t0 * facadeArea));
    }

    public void UygulaDisCepheYalitimi(Mekan mekan, List<Bolme> cepheElemanlari,
        CepheMaruziyet maruziyet = CepheMaruziyet.SerbestAlan)
    {
        var dntw = HesaplaDisCepheYalitimi(mekan, cepheElemanlari, maruziyet);
        foreach (var eleman in cepheElemanlari)
        {
            eleman.Dntw = dntw;
            if (dntw <= 0)
                eleman.DisCepheSpektralSonuc = null;
        }
    }

    /// İç gürültü seviyesi: L_int = L_ext − Dnt,w
    public double HesaplaIcGurultu(double disGurultuLden, double dntw) =>
        disGurultuLden - dntw;

    public double[] GetDisGurultuSpektrumu(BinaProje proje)
    {
        if (TamDisGurultuSpektrumuVar(proje))
            return proje.DisGurultuThirdOctave.Take(ExtendedThirdOctaveFreqler.Length).ToArray();

        // Trafik kaynaklı dış gürültü için normalize 1/3 oktav şekli.
        // Spektrumun enerjetik toplamı proje Lgag/Lden değerine eşitlenir.
        double[] trafikOffset =
            [-5, -4, -4, -5, -5, -6, -7, -8, -9, -10, -11, -12, -13, -15, -17, -19, -22, -25, -28];
        var toplamOffset = EnerjiToplam(trafikOffset);
        var shift = proje.DisGurultuDb - toplamOffset;
        return trafikOffset.Select(v => v + shift).ToArray();
    }

    public DisCepheSpektralSonuc? HesaplaSpektralDisCephe(BinaProje proje, Bolme bolme)
    {
        if (!bolme.IsDisCephe)
            return null;

        var cepheler = bolme.KaynakMekan?.Bolmeler
            .Where(b => b.IsDisCephe)
            .ToList();
        if (cepheler is null || cepheler.Count == 0)
            cepheler = [bolme];

        // Bir odanın tek bir malzemesiz cephe parçası bile birleşik sonucu tanımsız
        // yapar. Eksik parçayı toplam alana katıp enerji toplamından atlamak, onu
        // sonsuz yalıtımlı kabul ederek iyimser ve tehlikeli sonuç üretiyordu.
        if (bolme.KaynakMekan?.Hacim <= 0 || !CepheGirdileriTam(cepheler))
        {
            GecersizKil(cepheler);
            return null;
        }

        double totalArea = cepheler.Sum(b => b.Alan);
        if (totalArea <= 0)
            return null;

        var compositeR = new double[OktavBantElemanServisi.UcuncuOktavFreqler.Length];
        for (int i = 0; i < compositeR.Length; i++)
        {
            double weightedTau = 0;
            foreach (var cephe in cepheler)
            {
                var solidR = OktavBantElemanServisi.GetRUcuncuOktavDizisi(cephe.YapiElemani!);
                var cepheR = HavaSesiHesapServisi.CalculateCompositeRThirdOctave(cephe, solidR);
                weightedTau += cephe.Alan * Math.Pow(10, -cepheR[i] / 10.0);
            }
            compositeR[i] = weightedTau > 0 ? -10.0 * Math.Log10(weightedTau / totalArea) : 0;
        }

        var deltaLfs = DeltaLfs(proje.DisGurultuCepheMaruziyeti);

        // EN 12354-3: D_2m,nT(f) = R'(f) + ΔL_fs + 10·log10(V/(6·T₀·S)) — Vnorm sabit per band offset.
        var volumeNorm = FacadeVolumeNormalization(bolme.KaynakMekan, totalArea);

        var dntSpectrum = new double[ExtendedThirdOctaveFreqler.Length];
        dntSpectrum[0] = Math.Max(0, compositeR[0] - 6.0 + deltaLfs + volumeNorm);
        dntSpectrum[1] = Math.Max(0, compositeR[0] - 4.0 + deltaLfs + volumeNorm);
        dntSpectrum[2] = Math.Max(0, compositeR[0] - 2.0 + deltaLfs + volumeNorm);

        for (int i = 0; i < compositeR.Length; i++)
            dntSpectrum[i + 3] = Math.Max(0, compositeR[i] + deltaLfs + volumeNorm);

        var dis = GetDisGurultuSpektrumu(proje);
        var ic = new double[dis.Length];
        for (int i = 0; i < dis.Length; i++)
            ic[i] = dis[i] - dntSpectrum[i];

        // DnT,A,tr için Ctr'yi opak duvarın tek sayısından değil, açıklıklar dahil
        // BİLEŞİK cephe spektrumundan türet (ISO 717-1, doğrulanmış rutin).
        var (rwComposite, _, ctrComposite, yetersiz) = new HavaSesiHesapServisi().CalculateSingleValue(compositeR);
        var ctr = yetersiz ? bolme.YapiElemani!.Ctr : ctrComposite;
        
        double dntw = (yetersiz ? CompositeRw(cepheler, totalArea) : rwComposite) + deltaLfs + volumeNorm;
        var dntAtr = dntw + ctr;
        var lowExt = EnerjiToplam(dis.Take(3));
        var lowInt = EnerjiToplam(ic.Take(3));
        var d2mNt50 = lowExt - lowInt;

        var sonuc = new DisCepheSpektralSonuc(
            ExtendedThirdOctaveFreqler.Select(f => (double)f).ToArray(),
            dis,
            dntSpectrum,
            ic,
            dntAtr,
            d2mNt50,
            EnerjiToplam(ic),
            DisSpektrumKaynagiEtiketi(proje));

        foreach (var cephe in cepheler)
        {
            cephe.Dntw = dntw;
            cephe.DisCepheSpektralSonuc = sonuc;
        }
        return sonuc;
    }

    private static double CompositeRw(IReadOnlyList<Bolme> cepheler, double totalArea)
    {
        double weightedTau = 0;
        foreach (var cephe in cepheler)
        {
            var rw = HavaSesiHesapServisi.CalculateCompositeRw(cephe);
            if (rw > 0)
                weightedTau += cephe.Alan * Math.Pow(10, -rw / 10.0);
        }
        return weightedTau > 0 ? -10.0 * Math.Log10(weightedTau / totalArea) : 0;
    }

    private static bool CepheGirdileriTam(IEnumerable<Bolme> cepheler)
    {
        foreach (var cephe in cepheler)
        {
            if (cephe.Alan <= 0 || cephe.YapiElemani == null || cephe.YapiElemani.EffectiveRw <= 0)
                return false;

            // Açıklık için varsayılan 28/32 dB bir tasarım varsayımı olabilir; resmî
            // cephe uygunluğunda ise atanmış eleman olmadan sonuç üretilemez.
            if (cephe.Acikliklar.Any(a => a.Alan > 0 &&
                    (a.YapiElemani == null || a.YapiElemani.EffectiveRw <= 0)))
                return false;

            if (cephe.Menfezler.Any(m => !m.HasSpectrum && m.DneDb <= 0))
                return false;
        }
        return true;
    }

    private static void GecersizKil(IEnumerable<Bolme> cepheler)
    {
        foreach (var cephe in cepheler)
        {
            cephe.Dntw = 0;
            cephe.DisCepheSpektralSonuc = null;
        }
    }

    private static string DisSpektrumKaynagiEtiketi(BinaProje proje)
    {
        if (!TamDisGurultuSpektrumuVar(proje))
            return "Varsayilan-NormalizeTrafik";

        return proje.DisGurultuSpektrumKaynagi switch
        {
            SpektrumVeriKaynagi.Olcum => "Olcum",
            SpektrumVeriKaynagi.Uretici => "Uretici",
            SpektrumVeriKaynagi.Model => "Model",
            SpektrumVeriKaynagi.Varsayilan => "Varsayilan",
            _ => "Belirsiz-KullaniciSpektrumu",
        };
    }

    private static double EnerjiToplam(IEnumerable<double> levels)
    {
        var sum = levels.Sum(l => Math.Pow(10.0, l / 10.0));
        return sum > 0 ? 10.0 * Math.Log10(sum) : 0.0;
    }
}
