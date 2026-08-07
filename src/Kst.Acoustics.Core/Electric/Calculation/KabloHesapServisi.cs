using System;
using System.Linq;
using System.Text.RegularExpressions;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.Core.Electric.Calculation;

/// <summary>Bir yüksek akım beslemesi için otomatik paralel kablo hesabının sonucu.</summary>
public sealed record ParalelKabloSonucu(
    int ParalelKolSayisi,
    double KesitMm2,
    string KesitAd,
    string KabloEtiketi,
    double GercekDusumYuzde,
    bool YeterliMi);

/// <summary>
/// Kablo hesap servisi — termal kapasite ve gerilim düşümü kriteri ile kesit tayini.
/// Varsayılan kablo tipi: NHXMH.
/// TS HD 60364-5-52 uyumlu.
/// </summary>
public static class KabloHesapServisi
{
    /// <summary>M11 şemasında otomatik atanabilecek en küçük bakır kablo kesiti (mm²).</summary>
    public const double MinimumKabloKesitiMm2 = 2.5;

    /// <summary>M11 güç kablolarında tek 240 mm² kol için kullanılan proje akım sınırı.</summary>
    public const double Maksimum240Mm2AkimKapasitesiA = 564;

    /// <summary>Otomatik boyutlandırılan beslemelerde (pano besleme, linye, busbar çıkışı) izin verilen
    /// azami paralel kol sayısı. Bunun ötesinde gereken besleme pratikte busbar ile çözülmelidir; bu
    /// yüzden kol sayısı bu tavanda durur ve sonuç "kesit yetersiz" (YeterliMi=false) olarak işaretlenir.
    /// OG/jeneratör gibi açıkça boyutlandırılan yollar kendi kol sınırlarını geçirir.</summary>
    public const int VarsayilanAzamiParalelKol = 12;

    /// <summary>
    /// Standart bakır kablo kesitleri (mm²).
    /// </summary>
    private static readonly double[] StandartKesitler =
        { 1.5, 2.5, 4, 6, 10, 16, 25, 35, 50, 70, 95, 120, 150, 185, 240 };

    /// <summary>
    /// NHXMH bakır kablo akım taşıma kapasiteleri (A).
    /// Montaj yöntemi B2 (boru içi), ortam 30°C.
    /// </summary>
    private static readonly double[] AkimKapasitesiB2 =
        { 15.5, 21, 28, 36, 50, 68, 89, 110, 134, 171, 207, 239, 272, 310, 364 };

    /// <summary>
    /// Bakır iletken özdirenç: ρ = 0.0175 Ω·mm²/m @ 70°C.
    /// </summary>
    public const double BakirOzdirenc = 0.0175;

    /// <summary>
    /// AG kablo birim reaktansı (Ω/m). Tipik çok-damarlı AG kablo X ≈ 0.08 mΩ/m.
    /// Küçük kesitlerde R baskındır; büyük kesit/yüksek akımda reaktans katkısı belirginleşir.
    /// </summary>
    public const double KabloReaktansOhmPerM = 0.00008;

    /// <summary>cosφ'den sinφ (≥0).</summary>
    private static double SinF(double cosF)
    {
        cosF = NumberHelper.NormalizeCosF(cosF);
        return Math.Sqrt(Math.Max(0, 1 - cosF * cosF));
    }

    /// <summary>
    /// Bir kablo segmentindeki gerilim düşümünü (V) verir: ΔU = k·I·(R·cosφ + X·sinφ),
    /// k = 2 (monofaze) / √3 (trifaze). R = ρ·L/S, X = X_birim·L.
    /// </summary>
    public static double GerilimDususV(double akimA, double mesafeM, double kesitMm2,
        FazTipi fazTipi, double cosF)
    {
        if (akimA <= 0 || mesafeM <= 0 || kesitMm2 <= 0) return 0;
        cosF = NumberHelper.NormalizeCosF(cosF);
        double k = fazTipi == FazTipi.Monofaze ? 2.0 : Math.Sqrt(3);
        double r = BakirOzdirenc * mesafeM / kesitMm2;
        double x = KabloReaktansOhmPerM * mesafeM;
        return k * akimA * (r * cosF + x * SinF(cosF));
    }

    /// <summary>"16 mm²" / "2.5" gibi bir kesit etiketinden sayısal kesiti (mm²) çözer.</summary>
    public static double KesitDegeri(string? kesitAd)
    {
        if (string.IsNullOrWhiteSpace(kesitAd)) return 0;
        var sayilar = Regex.Matches(kesitAd, @"[0-9]+(?:[\.,][0-9]+)?");
        if (sayilar.Count == 0) return 0;

        // "4x240 mm²" veya "2x(4x240)" gibi çok damarlı etiketlerde gerçek faz kesiti
        // son sayıdır; düz "16 mm²" etiketlerinde de aynı kural doğru sonucu verir.
        var s = sayilar[^1].Value
            .Replace(',', '.');
        return double.TryParse(s, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    /// <summary>
    /// Kablo kesiti hesapla — max(termal kriter, gerilim düşümü kriteri).
    /// </summary>
    /// <param name="akimA">Linye veya pano giriş akımı (A)</param>
    /// <param name="mesafeM">Kablo uzunluğu (m)</param>
    /// <param name="fazTipi">Monofaze veya Trifaze</param>
    /// <param name="gerilimV">Faz gerilimi (monofaze: 230V, trifaze: 400V)</param>
    /// <param name="izinVerilenDusumYuzde">İzin verilen gerilim düşümü % (son devre: 3, toplam: 5)</param>
    /// <param name="cosF">Güç faktörü</param>
    public static (double KesitMm2, string KesitAd, double GercekDusumYuzde) KesitHesapla(
        double akimA, double mesafeM, FazTipi fazTipi,
        double gerilimV = 230, double izinVerilenDusumYuzde = 3.0, double cosF = 0.85)
    {
        if (akimA <= 0 || mesafeM <= 0)
            return (MinimumKabloKesitiMm2, "2.5", 0);
        cosF = NumberHelper.NormalizeCosF(cosF, 0.85);
        if (izinVerilenDusumYuzde <= 0) izinVerilenDusumYuzde = 3.0;
        if (gerilimV <= 0)
            gerilimV = fazTipi == FazTipi.Monofaze ? 230 : 400;

        // Kriter 1: Termal — akımı taşıyabilecek minimum kesit
        double termalKesit = 1.5;
        for (int i = 0; i < StandartKesitler.Length; i++)
        {
            if (AkimKapasitesiB2[i] >= akimA)
            {
                termalKesit = StandartKesitler[i];
                break;
            }
            if (i == StandartKesitler.Length - 1)
                termalKesit = StandartKesitler[i]; // Max
        }

        // Kriter 2: Gerilim düşümü (reaktans dahil)
        // ΔU = k × I × L × (ρ/S · cosφ + X' · sinφ)  ;  k = 2 (mono) / √3 (tri)
        // İzin sınırı için S'yi çöz: ρ·cosφ/S ≤ ΔU_izin/(k·I·L) − X'·sinφ
        double carpan = fazTipi == FazTipi.Monofaze ? 2.0 : Math.Sqrt(3);
        double izinDusumV = gerilimV * izinVerilenDusumYuzde / 100.0;
        double sinF = SinF(cosF);
        double reaktansDususBirim = KabloReaktansOhmPerM * sinF; // S'den bağımsız birim uzunluk terimi
        double sagTaraf = izinDusumV / (carpan * akimA * mesafeM) - reaktansDususBirim;

        // sagTaraf ≤ 0 ise reaktans tek başına sınırı aşıyor → en büyük kesit gerekir
        double gerilimKesit = sagTaraf <= 0
            ? StandartKesitler[^1]
            : (BakirOzdirenc * cosF) / sagTaraf;

        // En büyük kriteri karşılayan standart kesiti bul
        double gerekliKesit = Math.Max(MinimumKabloKesitiMm2, Math.Max(termalKesit, gerilimKesit));

        double secilen = MinimumKabloKesitiMm2;
        for (int i = 0; i < StandartKesitler.Length; i++)
        {
            if (StandartKesitler[i] >= gerekliKesit)
            {
                secilen = StandartKesitler[i];
                break;
            }
            if (i == StandartKesitler.Length - 1)
                secilen = StandartKesitler[i];
        }

        // Gerçek gerilim düşümü hesapla (reaktans dahil)
        double gercekDusum = GerilimDususV(akimA, mesafeM, secilen, fazTipi, cosF);
        double gercekDusumYuzde = (gercekDusum / gerilimV) * 100.0;

        string kesitAd = secilen switch
        {
            < 10 => $"{secilen}",
            _ => $"{secilen:0}"
        };

        return (secilen, kesitAd, gercekDusumYuzde);
    }

    /// <summary>Standart bir kesitin B2 montaj yöntemindeki akım taşıma kapasitesini verir.</summary>
    public static double AkimKapasitesiA(double kesitMm2)
    {
        for (int i = 0; i < StandartKesitler.Length; i++)
        {
            if (Math.Abs(StandartKesitler[i] - kesitMm2) < 0.001)
                return AkimKapasitesiB2[i];
        }

        return 0;
    }

    /// <summary>M11 kablo etiketini ortak formata çevirir: 2xS, 4xS veya Nx(4xS).</summary>
    public static string KabloEtiketiOlustur(string kesitAd, FazTipi fazTipi, int paralelKolSayisi = 1)
    {
        if (string.IsNullOrWhiteSpace(kesitAd)) return "";
        int kol = Math.Max(1, paralelKolSayisi);
        string damar = fazTipi == FazTipi.Monofaze ? "2x" : "4x";
        return kol > 1
            ? $"{kol}x({damar}{kesitAd.Trim()}) mm²"
            : $"{damar}{kesitAd.Trim()} mm²";
    }

    /// <summary>
    /// Akımı ve gerilim düşümünü karşılayan ilk paralel kablo düzenini hesaplar. Tek kablonun
    /// en büyük standart kesitte yetersiz kaldığı durumda paralel kol sayısı otomatik artırılır.
    /// </summary>
    /// <param name="maksimumKesitAkimKapasitesiA">Özel montaj koşulunda 240 mm² için kullanılacak
    /// akım kapasitesi. Sıfırsa B2 tablosundaki değer kullanılır.</param>
    public static ParalelKabloSonucu ParalelKabloHesapla(
        double toplamAkimA, double mesafeM, FazTipi fazTipi,
        double gerilimV = 230, double izinVerilenDusumYuzde = 3.0, double cosF = 0.85,
        double maksimumKesitAkimKapasitesiA = 0, int minimumParalelKolSayisi = 1,
        int maksimumParalelKolSayisi = 64)
    {
        if (toplamAkimA <= 0 || mesafeM <= 0)
            return new ParalelKabloSonucu(
                Math.Max(1, minimumParalelKolSayisi), MinimumKabloKesitiMm2, "2.5", "", 0, true);

        int sonKol = Math.Max(1, maksimumParalelKolSayisi);
        int ilkKol = Math.Clamp(minimumParalelKolSayisi, 1, sonKol);
        ParalelKabloSonucu? sonSonuc = null;

        for (int kol = ilkKol; kol <= sonKol; kol++)
        {
            double kolAkimiA = toplamAkimA / kol;
            var (kesit, kesitAd, dusumYuzde) = KesitHesapla(
                kolAkimiA, mesafeM, fazTipi, gerilimV, izinVerilenDusumYuzde, cosF);

            double kapasiteA = AkimKapasitesiA(kesit);
            if (Math.Abs(kesit - StandartKesitler[^1]) < 0.001 && maksimumKesitAkimKapasitesiA > 0)
                kapasiteA = Math.Max(kapasiteA, maksimumKesitAkimKapasitesiA);

            bool termalYeterli = kapasiteA + 0.001 >= kolAkimiA;
            bool dusumYeterli = dusumYuzde <= izinVerilenDusumYuzde + 0.0001;
            string etiket = KabloEtiketiOlustur(kesitAd, fazTipi, kol);

            sonSonuc = new ParalelKabloSonucu(
                kol, kesit, kesitAd, etiket, dusumYuzde, termalYeterli && dusumYeterli);
            if (sonSonuc.YeterliMi)
                return sonSonuc;
        }

        return sonSonuc!;
    }

    /// <summary>
    /// Pano kablosunu hesaplar ve atar.
    /// Boyutlandırma: üst panodaki çıkış sigortası amperajına göre.
    /// Gerilim düşümü: gerçek akım üzerinden.
    /// </summary>
    public static void PanoKabloHesapla(Pano pano, ElektrikProje proje)
    {
        // Kök pano (UstPano == null) — kablo yok
        if (pano.UstPano == null || pano.KabloMesafeM <= 0)
        {
            pano.KabloKesitiYeterli = true;
            return;
        }

        // Busbar üzerinden bağlıysa: kablo busbar çıkış kutusundan tali panoya gider.
        // Bu durumda UstPanoCikisSigortaA yerine CikisKutusuSigortaA kullanılır.
        int boyutlandirmaSigortaA = pano.BaglantiTipi == BaglantiTipi.Busbar
            ? (pano.CikisKutusuSigortaA > 0 ? pano.CikisKutusuSigortaA : pano.GirisSigortaA)
            : (pano.UstPanoCikisSigortaA > 0 ? pano.UstPanoCikisSigortaA : pano.GirisSigortaA);

        var gerilim = proje.SistemGerilimV; // Hat gerilimi (trifaze)
        var izinDusum = proje.IzinVerilenGerilimDususYuzde > 0
            ? proje.IzinVerilenGerilimDususYuzde
            : 3.0;

        var kablo = ParalelKabloHesapla(
            (double)boyutlandirmaSigortaA,
            pano.KabloMesafeM,
            FazTipi.Trifaze,
            gerilim,
            izinVerilenDusumYuzde: izinDusum,
            cosF: proje.CosF,
            maksimumKesitAkimKapasitesiA: Maksimum240Mm2AkimKapasitesiA,
            maksimumParalelKolSayisi: VarsayilanAzamiParalelKol);

        pano.KabloParalelKolSayisi = kablo.ParalelKolSayisi;
        pano.KabloKesiti = kablo.KabloEtiketi;
        pano.KabloTipi = proje.VarsayilanKabloTipi;
        pano.KabloKesitiYeterli = kablo.YeterliMi;

        // Gerçek gerilim düşümü ve ısı kaybı — besleme talep akımına göre (reaktans dahil).
        // Talep akımı (ks uygulanmış) besleme kablosunda akan akımdır.
        double akim = pano.TalepAkimA > 0 ? pano.TalepAkimA : pano.GirisAkimA;
        double kolAkimi = akim / kablo.ParalelKolSayisi;
        double dusumV = GerilimDususV(
            kolAkimi, pano.KabloMesafeM, kablo.KesitMm2, FazTipi.Trifaze, proje.CosF);
        pano.KabloGerilimDususYuzde = (dusumV / gerilim) * 100.0;

        // Isı kaybı: P_loss = 3 × kol × I_kol² × R (trifaze 3 faz iletkeni)
        // R = ρ × L / S (Ohm)
        double direnc = BakirOzdirenc * pano.KabloMesafeM / kablo.KesitMm2;
        pano.KabloIsiKaybiW = 3.0 * kablo.ParalelKolSayisi * kolAkimi * kolAkimi * direnc;
    }

    /// <summary>
    /// Bir linye (son devre) kablosunu boyutlandırır ve kesit/gerilim düşümü/ısı kaybını atar.
    /// Boyutlandırma linyenin kendi yük akımına göre yapılır.
    /// </summary>
    public static void LinyeKabloHesapla(Linye linye, ElektrikProje proje)
    {
        if (linye.KabloMesafeM <= 0 || linye.AkimA <= 0)
        {
            linye.KabloKesiti = "";
            linye.KabloParalelKolSayisi = 1;
            linye.KabloGerilimDususYuzde = 0;
            linye.KabloIsiKaybiW = 0;
            linye.KabloKesitiYeterli = true;
            return;
        }

        double gerilim = linye.FazTipi == FazTipi.Monofaze ? proje.FazGerilimV : proje.SistemGerilimV;
        double izinDusum = proje.IzinVerilenGerilimDususYuzde > 0 ? proje.IzinVerilenGerilimDususYuzde : 3.0;

        var kablo = ParalelKabloHesapla(
            linye.AkimA, linye.KabloMesafeM, linye.FazTipi,
            gerilim, izinVerilenDusumYuzde: izinDusum, cosF: proje.CosF,
            maksimumKesitAkimKapasitesiA: Maksimum240Mm2AkimKapasitesiA,
            maksimumParalelKolSayisi: VarsayilanAzamiParalelKol);

        linye.KabloParalelKolSayisi = kablo.ParalelKolSayisi;
        linye.KabloKesiti = kablo.KabloEtiketi;
        linye.KabloKesitiYeterli = kablo.YeterliMi;

        double kolAkimi = linye.AkimA / kablo.ParalelKolSayisi;
        double dusumV = GerilimDususV(
            kolAkimi, linye.KabloMesafeM, kablo.KesitMm2, linye.FazTipi, proje.CosF);
        linye.KabloGerilimDususYuzde = (dusumV / gerilim) * 100.0;

        double direnc = BakirOzdirenc * linye.KabloMesafeM / kablo.KesitMm2;
        int iletken = linye.FazTipi == FazTipi.Monofaze ? 2 : 3;
        linye.KabloIsiKaybiW = iletken * kablo.ParalelKolSayisi * kolAkimi * kolAkimi * direnc;
    }

    /// <summary>
    /// Busbar üzerindeki bir çıkış kutusundan tali panoya giden kabloyu hesaplar.
    /// </summary>
    public static (string KesitAd, double GerilimDususYuzde, double IsiKaybiW) BusbarCikisKablosuHesapla(
        BusbarCikisKutusu kutu, double akimA, FazTipi fazTipi, ElektrikProje proje)
    {
        if (kutu.KabloMesafeM <= 0 || akimA <= 0) return ("—", 0, 0);

        double gerilim = fazTipi == FazTipi.Monofaze ? proje.FazGerilimV : proje.SistemGerilimV;
        double izinDusum = proje.IzinVerilenGerilimDususYuzde > 0 ? proje.IzinVerilenGerilimDususYuzde : 3.0;

        var kablo = ParalelKabloHesapla(
            (double)kutu.SigortaA,
            kutu.KabloMesafeM,
            fazTipi,
            gerilim,
            izinVerilenDusumYuzde: izinDusum,
            cosF: proje.CosF,
            maksimumKesitAkimKapasitesiA: Maksimum240Mm2AkimKapasitesiA,
            maksimumParalelKolSayisi: VarsayilanAzamiParalelKol);

        double kolAkimi = akimA / kablo.ParalelKolSayisi;
        double dusumV = GerilimDususV(
            kolAkimi, kutu.KabloMesafeM, kablo.KesitMm2, fazTipi, proje.CosF);
        double dusumYuzde = (dusumV / gerilim) * 100.0;
        double direnc = BakirOzdirenc * kutu.KabloMesafeM / kablo.KesitMm2;
        int iletken = fazTipi == FazTipi.Monofaze ? 2 : 3;
        double isi = iletken * kablo.ParalelKolSayisi * kolAkimi * kolAkimi * direnc;

        return (kablo.KabloEtiketi, dusumYuzde, isi);
    }
}
