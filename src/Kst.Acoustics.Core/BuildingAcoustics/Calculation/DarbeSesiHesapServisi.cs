using System;
using System.Linq;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.BuildingAcoustics.Calculation;

public class DarbeSesiHesapServisi
{
    /// <summary>
    /// TS EN 12354-2 standardına göre döşemeler arası darbe sesi seviyesini hesaplar.
    /// L'n,w = Ln,w,eq − ΔLw + K_flanking
    /// </summary>
    public double HesaplaApparentLnw(Bolme dosemeBolmesi)
    {
        dosemeBolmesi.ApparentLnw = 0;
        dosemeBolmesi.ApparentLnTw = 0;

        if (dosemeBolmesi.YapiElemani == null || dosemeBolmesi.AliciMekan == null ||
            dosemeBolmesi.Alan <= 0 || dosemeBolmesi.AliciMekan.Sinif == MekanSinifi.HesapDisi ||
            dosemeBolmesi.KaynakMekan?.Sinif == MekanSinifi.HesapDisi)
            return 0;

        var eleman = dosemeBolmesi.YapiElemani;

        // 1. Çıplak Döşeme Darbe Sesi — laboratuvar eşdeğer değeri (Ln,w,eq)
        double lnw0 = eleman.Lnw;
        if (lnw0 <= 0)
            return 0;

        // 2. Kaplama azaltması ΔLw (yüzer döşeme, halı vb.)
        double deltaLw = eleman.DeltaLw;

        // 3. Normalize darbe sesi: Ln = Ln,w,eq − ΔLw
        double ln = lnw0 - deltaLw;

        // 4. Flanking: TS EN 12354-2 Df yolları (döşeme → alıcı oda duvarları) enerji toplamı
        double lnFlankToplam = GetImpactFlankingPaths(dosemeBolmesi, ln);

        double apparentLnw = double.IsNegativeInfinity(lnFlankToplam)
            ? ln
            : 10.0 * Math.Log10(Math.Pow(10.0, ln / 10.0) + Math.Pow(10.0, lnFlankToplam / 10.0));

        dosemeBolmesi.ApparentLnw = apparentLnw;
        return apparentLnw;
    }

    /// <summary>
    /// TS EN 12354-2 Df yolları (döşeme → alıcı odanın duvarları) için flanking darbe sesi
    /// seviyelerinin logaritmik enerji toplamını döndürür.
    /// L_n,Df = L_n + (R_i − R_j)/2 − ΔR_j − D̄_v,ij + 10·log10(S_i/√(S_i·S_j))
    /// D̄_v,ij titreşim azaltımıdır ve flanking seviyesini DÜŞÜRÜR (EN 12354-2).
    /// Alıcı odanın tavanı ayırıcı döşemenin kendisi olduğundan flank olarak kullanılmaz;
    /// flanklar alıcı odanın düşey duvarlarıdır. Flank yoksa NegativeInfinity döner.
    /// </summary>
    private double GetImpactFlankingPaths(Bolme dosemeBolmesi, double ln)
    {
        var eleman = dosemeBolmesi.YapiElemani!;
        var alici = dosemeBolmesi.AliciMekan!;
        double si = Math.Max(0.01, dosemeBolmesi.Alan);
        double sumTauFlank = 0;

        foreach (var duvar in alici.Bolmeler)
        {
            var flank = duvar.YapiElemani;
            if (flank == null || flank.Rw <= 0 ||
                flank.Tip is YapiElemaniTipi.Doseme or YapiElemaniTipi.Tavan)
                continue;
            double sj = duvar.Alan;
            if (sj <= 0)
                continue;

            // M = lg(m'⊥,i / m'i): yol elemanı i = döşeme, dik eleman = duvar (EN 12354-1 s.43).
            double m = AnnexEKijServisi.KutleOraniM(flank.ToplamYuzeyKutlesi, eleman.ToplamYuzeyKutlesi);

            // Döşeme-duvar birleşimi: T varsayılır; Df/Fd yolları branch (K12) formülünden okunur.
            // A2 — Sandviç sınıfında BaglantiEklemesi bastırılır (E.5 zaten içerir).
            var sinif = AnnexEKijServisi.SinifSec(flank.Hafiflik, eleman.Hafiflik, flank.Hafiflik);
            double kAnnex = AnnexEKijServisi.BaselineKij(
                                JunctionGeometriTipi.T, AnnexEYolTipi.Branch, m,
                                flank.Hafiflik, eleman.Hafiflik, flank.Hafiflik, 500)
                          + (sinif == YapiElemaniHafifligi.Sandvic
                                ? 0
                                : AnnexEKijServisi.BaglantiEklemesi(flank.BaglantiTipi));

            double lij = Math.Max(0.01, DuvarUzunlugu(duvar));
            double aI = AnnexEKijServisi.EquivalentAbsorptionLength(si, HavaSesiHesapServisi.ElemanTs(eleman), 500);
            double aJ = AnnexEKijServisi.EquivalentAbsorptionLength(sj, HavaSesiHesapServisi.ElemanTs(flank), 500);

            // A4 — EN 12354-1 E.3 Limiting values: D̄_v,ij,situ ≥ 0 dB.
            double dvij = Math.Max(0, kAnnex + AnnexEKijServisi.AbsorptionLengthCorrection(lij, aI, aJ));

            double areaTerm = 10.0 * Math.Log10(si / Math.Sqrt(si * Math.Max(0.01, sj)));

            double lnDf = ln
                        + (eleman.Rw - flank.Rw) / 2.0
                        - flank.DeltaRDb
                        - dvij
                        + areaTerm;

            sumTauFlank += Math.Pow(10, lnDf / 10.0);
        }

        if (sumTauFlank <= 0) return double.NegativeInfinity;
        return 10.0 * Math.Log10(sumTauFlank);
    }

    private static double DuvarUzunlugu(Bolme duvar)
    {
        var dx = duvar.Baslangic.X - duvar.Bitis.X;
        var dy = duvar.Baslangic.Y - duvar.Bitis.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// TS EN 12354-2 ve Binaların Gürültüye Karşı Korunması Yönetmeliği (2017) uyarınca
    /// Alıcı Hacmine göre Normalize Darbe Sesi (L'nT,w).
    /// L'nT,w = L'n,w - 10 * log10(0.032 * V)
    /// </summary>
    public double HesaplaApparentLnTw(Bolme dosemeBolmesi)
    {
        double lnw = dosemeBolmesi.ApparentLnw > 0 ? dosemeBolmesi.ApparentLnw : HesaplaApparentLnw(dosemeBolmesi);
        if (lnw <= 0 || dosemeBolmesi.AliciMekan == null || dosemeBolmesi.AliciMekan.Hacim <= 0)
        {
            // L'nT,w hacim normalizasyonu olmadan tanımlı değildir. Ham L'n,w'yi
            // L'nT,w alanına taşımak yanlış bir geçti/kaldı sonucu üretebilir.
            dosemeBolmesi.ApparentLnTw = 0;
            return 0;
        }

        double v = dosemeBolmesi.AliciMekan.Hacim;
        double lntw = lnw - 10.0 * Math.Log10(0.032 * v);
        
        dosemeBolmesi.ApparentLnTw = lntw;
        return lntw;
    }

    /// <summary>
    /// TS EN ISO 717-2 — Tek sayı değeri L'n,w.
    /// Giriş: 16 üçüncü-oktav bant değeri, 100–3150 Hz.
    /// Algoritma: referans eğri ölçülen eğriye doğru 1 dB adımlarla kaydırılır;
    /// toplam olumsuz sapma ≤ 32 dB koşulunu sağlayan son geçerli konum alınır.
    /// </summary>
    /// A3 — ISO 717-2 Madde 5: en az 100–3150 Hz (16 bant) zorunlu; eksikse NaN döner.
    public double CalculateSingleValue(double[] thirdOctaveLn)
    {
        if (thirdOctaveLn.Length < 16)
            return double.NaN;

        var values = thirdOctaveLn.Take(16).ToArray();
        // ISO 717-2 referans eğrisi, 100–3150 Hz (16 bant)
        var reference = new[] { 62, 62, 62, 62, 62, 62, 61, 60, 59, 58, 57, 54, 51, 48, 45, 42 };

        // Olumsuz sapma: ölçülen Ln(i) > referans(i) + kaydırma olduğunda
        // En küçük kaydırma: referansın ölçümün altında kalmasını sağlayan en az düzeltme
        int bestShift = 50;
        for (int shift = 50; shift >= -50; shift--)
        {
            double unfavorable = 0;
            for (int i = 0; i < values.Length && i < reference.Length; i++)
                unfavorable += Math.Max(0, values[i] - (reference[i] + shift));

            if (unfavorable <= 32)
                bestShift = shift;
        }

        // L'n,w = referans_500Hz (60 dB, index 7) + kaydırma
        // H10: ISO 717 yuvarlaması (0.1 dB) — referans eğri kaydırması tam sayı çıksa da, standardın istediği hassasiyet.
        return Math.Round(reference[7] + (double)bestShift, 1);
    }

    // ─── Oktav bant hesabı ────────────────────────────────────────────────────

    /// Bölme için oktav bantlı L'n(f) dizisi ve tek sayı türetimi.
    public OktavDarbeSonucu HesaplaOktavApparentLn(Bolme bolme)
    {
        if (bolme.YapiElemani == null || bolme.AliciMekan == null)
        {
            var empty = new double[7];
            return new OktavDarbeSonucu(empty, 0, "TekSayi");
        }

        double[] lnDizi = OktavBantElemanServisi.GetLnDizisi(bolme.YapiElemani);
        string kaynak = OktavBantElemanServisi.DarbeSpektrumKaynagiEtiketi(bolme.YapiElemani);

        // Flanking düzeltmesi: ApparentLnw - (Lnw - DeltaLw) farkını uygula
        double lnBase     = bolme.YapiElemani.Lnw - bolme.YapiElemani.DeltaLw;
        double apparentLn = bolme.ApparentLnw > 0 ? bolme.ApparentLnw : HesaplaApparentLnw(bolme);
        double delta = apparentLn - lnBase;
        for (int i = 0; i < lnDizi.Length; i++)
            lnDizi[i] = Math.Max(0, lnDizi[i] + delta);

        // Fix #6 — Görsel oktav tek-sayısı, otoriter ApparentLnw değeridir (12354-2 basit model);
        // standart-dışı 7-bant ISO 717-2 uyarlaması raporlanan değer olarak kullanılmaz.
        double lnw = apparentLn;
        var sonuc = new OktavDarbeSonucu(lnDizi, lnw, kaynak);
        bolme.OktavDarbeSesiSonucu = sonuc;
        return sonuc;
    }

    /// ISO 717-2 uyarlaması — 7 oktav bant (63–4000 Hz) için L'n,w türetimi.
    public double CalculateSingleValueOktav(double[] oktavLn)
    {
        if (oktavLn.Length < 7)
            return double.NaN;

        var values = oktavLn[..7];
        // ISO 717-2 referans eğrisinin 63–4000 Hz oktav seçimi
        // (63=62, 125=62, 250=62, 500=60, 1k=59, 2k=54, 4k=51)
        var reference = new[] { 62, 62, 62, 60, 59, 54, 51 };

        int bestShift = 50;
        for (int shift = 50; shift >= -50; shift--)
        {
            double unfavorable = 0;
            for (int i = 0; i < values.Length; i++)
                unfavorable += Math.Max(0, values[i] - (reference[i] + shift));

            // 7 bantlı eşik ≈ 32 × (7/16) ≈ 14 dB
            if (unfavorable <= 14)
                bestShift = shift;
        }

        // H10: ISO 717 yuvarlaması (0.1 dB)
        return Math.Round(reference[3] + (double)bestShift, 1); // 500 Hz referans = 60
    }

    // TS EN 12354-2 Table B.1: σ ≈ 2 dB
    public const double HesapBelirsizligi = 2.0;
}
