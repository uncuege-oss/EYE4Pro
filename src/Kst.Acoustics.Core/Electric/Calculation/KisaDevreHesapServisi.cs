using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.Core.Electric.Calculation;

/// <summary>
/// Kısa devre hesap servisi — IEC 60909 basitleştirilmiş üç-faz kısa devre (Ik3).
/// Kaynak empedansı (trafo veya şebeke) ana baradan başlar; her kablo/busbar segmenti
/// empedans ekleyerek alt panolarda Ik'yı düşürür. Zincir boyunca direnç (R) ve reaktans (X)
/// <b>ayrı</b> toplanır; düğüm empedansı Z = √(R² + X²) ile birleşir. Kaynak empedansı
/// (trafo/şebeke) saf reaktans kabul edilir (X/R yüksek olduğundan kaynak direnci ihmal —
/// IEC 60909 basitleştirmesi); kablo/busbar reaktansı gerilim düşümüyle aynı birim değerlerden gelir.
/// </summary>
public static class KisaDevreHesapServisi
{
    private const double VarsayilanBusbarDirencMOhmPerM = 0.18;
    private const double VarsayilanBusbarReaktansMOhmPerM = 0.10;

    /// <summary>
    /// Ana baradaki kaynak empedansını (Ω, faz) hesaplar.
    /// Trafo varsa Z = uk% · V_LL² / S; yoksa şebeke Ik'sından Z = V_LL / (√3 · Ik).
    /// </summary>
    public static double KaynakEmpedansiOhm(ElektrikProje proje) => KaynakEmpedansiOhm(proje, null);

    /// <summary>
    /// Ana baradaki kaynak empedansını (Ω, faz) hesaplar. OG sistemi tanımlıysa ve trafosu
    /// kütüphanede çözülebiliyorsa kaynak, şebeke + trafo empedansından (Zq + Zt) türetilir;
    /// aksi halde proje seviyesindeki TrafoGucKVA / SebekeBaslangicIkKA alanlarına düşülür.
    /// </summary>
    public static double KaynakEmpedansiOhm(ElektrikProje proje, ElektrikKutuphaneServisi? kutuphane)
    {
        double vLL = proje.SistemGerilimV;

        // OG sistemi varsa kaynak empedansını seçili trafodan + şebekeden al
        if (kutuphane != null && proje.OrtaGerilim != null &&
            !string.IsNullOrEmpty(proje.OrtaGerilim.TrafoTanimAd))
        {
            var trafo = kutuphane.Trafolar.FirstOrDefault(t => t.Ad == proje.OrtaGerilim.TrafoTanimAd);
            if (trafo != null)
                return OrtaGerilimHesapServisi.KaynakEmpedansiAgOhm(proje.OrtaGerilim, trafo);
        }

        if (proje.TrafoGucKVA > 0 && proje.TrafoUkYuzde > 0)
        {
            double sVA = proje.TrafoGucKVA * 1000.0;
            return (proje.TrafoUkYuzde / 100.0) * (vLL * vLL) / sVA;
        }
        double ikA = Math.Max(0.1, proje.SebekeBaslangicIkKA) * 1000.0;
        return vLL / (Math.Sqrt(3) * ikA);
    }

    /// <summary>Verilen toplam faz empedansından üç-faz kısa devre akımı (kA).</summary>
    public static double Ik3KA(double vLL, double zToplamOhm)
    {
        if (zToplamOhm <= 0) return 0;
        return vLL / (Math.Sqrt(3) * zToplamOhm) / 1000.0;
    }

    /// <summary>Bir kablonun faz direncini (Ω) verir: R = ρ · L / S.</summary>
    private static double KabloDirenciOhm(double mesafeM, double kesitMm2)
    {
        if (mesafeM <= 0 || kesitMm2 <= 0) return 0;
        return KabloHesapServisi.BakirOzdirenc * mesafeM / kesitMm2;
    }

    /// <summary>Bir kablonun faz reaktansını (Ω) verir: X = X' · L (kesitten bağımsız birim reaktans,
    /// gerilim düşümüyle aynı değer). Paralel kol bölmesi çağıran tarafta yapılır.</summary>
    private static double KabloReaktansOhm(double mesafeM)
    {
        if (mesafeM <= 0) return 0;
        return KabloHesapServisi.KabloReaktansOhmPerM * mesafeM;
    }

    /// <summary>
    /// Projedeki tüm panolar için Ik3'ü kaynaktan aşağı doğru hesaplar ve
    /// her linyenin kesme kapasitesini bulunduğu panonun Ik'sına göre günceller.
    /// PanoHesapla + PanoKabloHesapla çağrıldıktan sonra çalıştırılmalıdır (kesitler hazır olmalı).
    /// </summary>
    public static void Hesapla(ElektrikProje proje, ElektrikKutuphaneServisi? kutuphane = null)
    {
        double vLL = proje.SistemGerilimV;
        double zKaynak = KaynakEmpedansiOhm(proje, kutuphane);

        // Pano başına kaynaktan o panoya kadar toplam (R, X) empedansı (Ω). Üstten alta çözülür.
        // Kaynak saf reaktans (X = |Zkaynak|, R = 0); kablo/busbar segmentleri R ve X ekler.
        var rxToplam = new Dictionary<Pano, (double R, double X)>();

        // Kök panolar: kaynak reaktansı + varsa trafo AG çıkışı → ana pano bağlantısı (R + X).
        foreach (var kok in proje.TumPanolar.Where(p => p.UstPano == null))
            rxToplam[kok] = (
                TrafoAnaPanoBaglantiDirenciOhm(proje, kok, kutuphane),
                zKaynak + TrafoAnaPanoBaglantiReaktansOhm(proje, kok, kutuphane));

        // Derinlik sırasına göre (üst → alt) çöz; üst zaten hesaplanmış olur.
        foreach (var pano in proje.TumPanolar.OrderBy(DerinlikBul))
        {
            if (!rxToplam.TryGetValue(pano, out var rx))
            {
                // Üst panonun (R, X)'i + bu panoya gelen segmentin direnci ve reaktansı.
                var ust = pano.UstPano != null && rxToplam.TryGetValue(pano.UstPano, out var u)
                    ? u : (R: 0.0, X: zKaynak);
                double kesit = KabloHesapServisi.KesitDegeri(pano.KabloKesiti);
                int kol = Math.Max(1, pano.KabloParalelKolSayisi);
                double r = ust.R
                    + BusbarSegmentDirenciOhm(pano, proje, kutuphane)
                    + KabloDirenciOhm(pano.KabloMesafeM, kesit) / kol;
                double x = ust.X
                    + BusbarSegmentReaktansOhm(pano, proje, kutuphane)
                    + KabloReaktansOhm(pano.KabloMesafeM) / kol;
                rx = (r, x);
                rxToplam[pano] = rx;
            }

            pano.KisaDevreAkimiKA = Ik3KA(vLL, Math.Sqrt(rx.R * rx.R + rx.X * rx.X));

            // Linye kesme kapasiteleri — anma akımına göre cihaz tipi (≤63A sigorta / >63A TMŞ; kullanıcı
            // ≤63A'i TMŞ'ye zorlayabilir) ve bara Ik'sına göre kesme kapasitesi (linye kablosu ihmal: en
            // kötü durum baradadır). Kullanıcı kesme kapasitesini elle seçtiyse korunur; ancak cihaz tipiyle
            // uyumsuz (ör. sigortaya geçişte 50kA) kalmışsa motor otomatik değere döner.
            foreach (var linye in pano.Linyeler)
            {
                bool tms = KorumaCihaziServisi.TmsAktif(linye.SigortaAmper, linye.TmsZorla);
                if (!linye.KesmeKapasitesiManuel || !KorumaCihaziServisi.GecerliKesme(linye.KesmeKapasitesi, tms, kutuphane))
                    linye.KesmeKapasitesi = KorumaCihaziServisi.KesmeKapasitesiSec(
                        linye.SigortaAmper, pano.KisaDevreAkimiKA, linye.TmsZorla, kutuphane);
            }

            // Çıkış (alt panolara giden) koruma cihazları bu panonun barasına oturur → kesme kapasitesi
            // bu panonun Ik'sına göre. Anma akımı PanoHesapla'da hesaplandı (UstPanoCikisSigortaA / busbar).
            foreach (var alt in pano.AltPanolar)
            {
                bool tms = KorumaCihaziServisi.TmsAktif(alt.CikisKorumaAkimA, alt.CikisTmsZorla);
                if (!alt.CikisKesmeKapasitesiManuel || !KorumaCihaziServisi.GecerliKesme(alt.CikisKesmeKapasitesi, tms, kutuphane))
                    alt.CikisKesmeKapasitesi = KorumaCihaziServisi.KesmeKapasitesiSec(
                        alt.CikisKorumaAkimA, pano.KisaDevreAkimiKA, alt.CikisTmsZorla, kutuphane);
            }
        }
    }

    private static int DerinlikBul(Pano pano)
    {
        int d = 0;
        var p = pano;
        while (p.UstPano != null) { d++; p = p.UstPano; }
        return d;
    }

    private static double BusbarSegmentDirenciOhm(Pano pano, ElektrikProje proje,
        ElektrikKutuphaneServisi? kutuphane)
    {
        if (pano.BaglantiTipi != BaglantiTipi.Busbar || string.IsNullOrEmpty(pano.BaglandigiBusbarId))
            return 0;

        var busbar = proje.TumBusbarlar.FirstOrDefault(b => b.Id == pano.BaglandigiBusbarId);
        if (busbar == null) return 0;

        var kutu = busbar.CikisKutulari.FirstOrDefault(k => k.BagliPanoAd == pano.Ad);
        double pozisyonM = kutu?.PozisyonM ?? 0;
        if (pozisyonM <= 0) return 0;

        double direncMOhmPerM = kutuphane?.BusbarTipleri
            .FirstOrDefault(b => b.Ad == busbar.BusbarTipiAd)
            ?.DirencMOhmPerM ?? VarsayilanBusbarDirencMOhmPerM;

        return (direncMOhmPerM / 1000.0) * pozisyonM;
    }

    /// <summary>Busbar beslemeli panoda, busbar başlangıcından çıkış kutusuna kadarki segmentin
    /// faz reaktansı (Ω). <see cref="BusbarSegmentDirenciOhm"/> ile aynı yol, reaktans birim değeriyle.</summary>
    private static double BusbarSegmentReaktansOhm(Pano pano, ElektrikProje proje,
        ElektrikKutuphaneServisi? kutuphane)
    {
        if (pano.BaglantiTipi != BaglantiTipi.Busbar || string.IsNullOrEmpty(pano.BaglandigiBusbarId))
            return 0;

        var busbar = proje.TumBusbarlar.FirstOrDefault(b => b.Id == pano.BaglandigiBusbarId);
        if (busbar == null) return 0;

        var kutu = busbar.CikisKutulari.FirstOrDefault(k => k.BagliPanoAd == pano.Ad);
        double pozisyonM = kutu?.PozisyonM ?? 0;
        if (pozisyonM <= 0) return 0;

        double reaktansMOhmPerM = kutuphane?.BusbarTipleri
            .FirstOrDefault(b => b.Ad == busbar.BusbarTipiAd)
            ?.ReaktansMOhmPerM ?? VarsayilanBusbarReaktansMOhmPerM;

        return (reaktansMOhmPerM / 1000.0) * pozisyonM;
    }

    private static double TrafoAnaPanoBaglantiDirenciOhm(ElektrikProje proje, Pano pano,
        ElektrikKutuphaneServisi? kutuphane)
    {
        var og = proje.OrtaGerilim;
        if (og == null) return 0;

        bool buPanoBesleniyor = string.IsNullOrWhiteSpace(og.AnaPanoAd)
            ? (ReferenceEquals(proje.KokPano, pano) || string.Equals(proje.KokPano?.Ad, pano.Ad, StringComparison.OrdinalIgnoreCase))
            : string.Equals(og.AnaPanoAd, pano.Ad, StringComparison.OrdinalIgnoreCase);

        return buPanoBesleniyor
            ? OrtaGerilimHesapServisi.BaglantiFazDirenciOhm(og.CikisBaglanti, kutuphane)
            : 0;
    }

    private static double TrafoAnaPanoBaglantiReaktansOhm(ElektrikProje proje, Pano pano,
        ElektrikKutuphaneServisi? kutuphane)
    {
        var og = proje.OrtaGerilim;
        if (og == null) return 0;

        bool buPanoBesleniyor = string.IsNullOrWhiteSpace(og.AnaPanoAd)
            ? (ReferenceEquals(proje.KokPano, pano) || string.Equals(proje.KokPano?.Ad, pano.Ad, StringComparison.OrdinalIgnoreCase))
            : string.Equals(og.AnaPanoAd, pano.Ad, StringComparison.OrdinalIgnoreCase);

        return buPanoBesleniyor
            ? OrtaGerilimHesapServisi.BaglantiFazReaktansOhm(og.CikisBaglanti, kutuphane)
            : 0;
    }
}
