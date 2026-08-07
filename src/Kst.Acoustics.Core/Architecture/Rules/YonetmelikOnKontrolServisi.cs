using System.Collections.Generic;
using Kst.Acoustics.Core.Architecture.Circulation;
using Kst.Acoustics.Core.Architecture.Layout;
using Kst.Acoustics.Core.Architecture.Materials;
using Kst.Acoustics.Core.Architecture.Models;
using Kst.Acoustics.Core.Architecture.Services;
using Kst.Acoustics.Core.Architecture.Templates;

namespace Kst.Acoustics.Core.Architecture.Rules;

/// <summary>
/// M34 kural setlerini calistirir ve eksik veri durumlarini on kontrol sonucuna cevirir.
/// </summary>
public static class YonetmelikOnKontrolServisi
{
    public const string VarsayilanVeriKaynakKodu = "M34-VERI";

    public static IReadOnlyList<string> IlkVeriKuralKodlari { get; } =
    [
        "M34-PROJE-BOS",
        "M34-PROJE-ADI-EKSIK",
        "M34-IHTIYAC-PROGRAMI-BOS",
        "M34-BINA-ISLEVI-EKSIK",
        "M34-PROJE-TIPI-EKSIK",
        "M34-KAPASITE-NEGATIF",
        "M34-KAPASITE-EKSIK",
        "M34-KAT-SAYISI-GECERSIZ",
        "M34-KAT-YUKSEKLIGI-GECERSIZ",
        "M34-MAHAL-LISTESI-BOS",
        "M34-MAHAL-ADI-EKSIK",
        "M34-MAHAL-TIPI-EKSIK",
        "M34-KULLANIM-SINIFI-EKSIK",
        "M34-MAHAL-ADEDI-GECERSIZ",
        "M34-MAHAL-ALANI-NEGATIF",
        "M34-HEDEF-ALAN-MINIMUMDAN-KUCUK",
        "M34-MAHAL-YUKSEKLIGI-GECERSIZ",
        "M34-HEDEF-YUKSEKLIK-MINIMUMDAN-KUCUK",
        "M34-MAHAL-KAPASITESI-NEGATIF",
        "M34-ZORUNLU-MAHAL-EKSIK",
        "M34-HEDEF-TOPLAM-ALAN-NEGATIF",
        "M34-SIRKULASYON-ORANI-NEGATIF",
        "M34-SIRKULASYON-ORANI-YUKSEK",
        "M34-TEKNIK-ALAN-ORANI-NEGATIF",
        "M34-TEKNIK-ALAN-ORANI-YUKSEK",
        "M34-NET-ALAN-BOS",
        "M34-ANA-KULLANIM-YOK",
        "M34-SIRKULASYON-MAHALI-YOK",
        "M34-TEKNIK-HACIM-YOK",
        "M34-HEDEF-TOPLAM-ALAN-FARKI",
        "M34-PLAN-TASLAGI-YOK",
        "M34-PLAN-KAT-YOK",
        "M34-PLAN-KAT-SINIRI-YOK",
        "M34-PLAN-KAT-BLOK-YOK",
        "M34-PLAN-KAT-KORIDOR-YOK",
        "M34-PLAN-BLOK-YERLESMEMIS",
        "M34-PLAN-BAGLANTISIZ-BLOK",
        "M34-PLAN-BLOK-CAKISMASI",
        "M34-PLAN-ALAN-TOLERANSI",
        "M34-CEKIRDEK-YOK",
        "M34-CEKIRDEK-AKS-KODU-EKSIK",
        "M34-CEKIRDEK-KAT-NO-UYUMSUZ",
        "M34-CEKIRDEK-KAT-ARALIGI-PLAN-DISI",
        "M34-CEKIRDEK-SUREKLILIK-KOPUK",
        "M34-CEKIRDEK-AKS-KAYMASI",
        "M34-SAFT-AKS-KODU-EKSIK",
        "M34-SAFT-KAT-NO-UYUMSUZ",
        "M34-SAFT-KAT-ARALIGI-PLAN-DISI",
        "M34-SAFT-SUREKLILIK-KOPUK",
        "M34-SAFT-AKS-KAYMASI",
        "M34-ISLAK-HACIM-SAFT-YOK",
        "M34-ISLAK-HACIM-SAFT-UZAK",
        "M34-KACIS-GRAFI-MERDIVENE-ULASAMAYAN-MAHAL"
    ];

    public static IReadOnlyList<KuralSonucu> Calistir(
        KontrolBaglami? baglam,
        IEnumerable<KuralSeti>? kuralSetleri)
    {
        baglam ??= new KontrolBaglami();
        if (kuralSetleri is null)
            return [];

        var sonuclar = new List<KuralSonucu>();
        foreach (var set in kuralSetleri)
        {
            if (set is null || !set.Aktif)
                continue;

            foreach (var kural in set.Kurallar)
            {
                if (kural is null || !kural.Aktif)
                    continue;

                var kaynakKodu = string.IsNullOrWhiteSpace(kural.KaynakKodu)
                    ? set.KaynakKodu
                    : kural.KaynakKodu;

                foreach (var veri in EksikVerileriBul(baglam, kural))
                {
                    sonuclar.Add(VeriEksigiSonucu(kural, veri, kaynakKodu));
                }
            }
        }

        return Sirala(sonuclar);
    }

    public static IReadOnlyList<KuralSonucu> Calistir(
        MimariProje? proje,
        IEnumerable<KuralSeti>? kuralSetleri)
        => Calistir(proje is null ? new KontrolBaglami() : KontrolBaglami.Projeden(proje), kuralSetleri);

    public static IReadOnlyList<KuralSonucu> IlkVeriKontrolleriniCalistir(MimariProje? proje)
        => IlkVeriKontrolleriniCalistir(proje is null ? new KontrolBaglami() : KontrolBaglami.Projeden(proje));

    public static IReadOnlyList<KuralSonucu> IlkVeriKontrolleriniCalistir(KontrolBaglami? baglam)
    {
        baglam ??= new KontrolBaglami();

        var sonuclar = new List<KuralSonucu>();
        if (baglam.MimariProje is null)
        {
            sonuclar.Add(Kritik(
                "M34-PROJE-BOS",
                "Mimari proje verisi bulunmuyor.",
                "Geçerli bir mimari proje oluşturun veya açın."));
            return Sirala(sonuclar);
        }

        var proje = baglam.MimariProje;
        if (string.IsNullOrWhiteSpace(proje.ProjeAdi))
        {
            sonuclar.Add(Uyari(
                "M34-PROJE-ADI-EKSIK",
                "Proje adı girilmemiş.",
                "Proje adı alanını doldurun."));
        }

        var program = baglam.IhtiyacProgrami ?? proje.IhtiyacProgrami;
        if (program is null)
        {
            sonuclar.Add(Kritik(
                "M34-IHTIYAC-PROGRAMI-BOS",
                "İhtiyaç programı bulunmuyor.",
                "Bina işlevi ve mahal ihtiyaçlarını içeren bir ihtiyaç programı oluşturun."));
            return Sirala(sonuclar);
        }

        BinaKapsaminiKontrolEt(program, sonuclar);
        MahalVerileriniKontrolEt(program, sonuclar);
        ZorunluMahalleriKontrolEt(program, sonuclar);
        AlanOranlariniKontrolEt(program, sonuclar);
        foreach (var uyari in MahalFinisajOneriServisi.FinisajUyarilari(proje))
            sonuclar.Add(FinisajUyarisi(uyari));
        if (baglam.PlanTaslagi is not null)
            PlanTaslaginiKontrolEt(baglam.PlanTaslagi, sonuclar);

        return Sirala(sonuclar);
    }

    /// <summary>
    /// M36 mahal finisaj on kontrol uyarilarini bagimsiz olarak calistirir.
    /// </summary>
    public static IReadOnlyList<KuralSonucu> FinisajKontrolleriniCalistir(MimariProje? proje)
        => Sirala(MahalFinisajOneriServisi.FinisajUyarilari(proje).Select(FinisajUyarisi));

    public static IReadOnlyList<KuralSonucu> PlanTaslagiKontrolleriniCalistir(PlanTaslagi? plan)
        => PlanTaslagiKontrolleriniCalistir(new KontrolBaglami { PlanTaslagi = plan });

    public static IReadOnlyList<KuralSonucu> PlanTaslagiKontrolleriniCalistir(KontrolBaglami? baglam)
    {
        baglam ??= new KontrolBaglami();
        var sonuclar = new List<KuralSonucu>();
        PlanTaslaginiKontrolEt(baglam.PlanTaslagi, sonuclar);
        return Sirala(sonuclar);
    }

    public static IReadOnlyList<KuralSonucu> Sirala(IEnumerable<KuralSonucu>? sonuclar)
        => (sonuclar ?? [])
            .OrderBy(s => SeviyeSirasi(s.Seviye))
            .ThenBy(s => s.KuralKodu, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.VeriEksigi, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static KuralSonucu VeriEksigiSonucu(Kural kural, string veriEksigi, string? kaynakKodu = null)
    {
        ArgumentNullException.ThrowIfNull(kural);

        var baslik = string.IsNullOrWhiteSpace(kural.Baslik)
            ? string.IsNullOrWhiteSpace(kural.Kod) ? "Ön kontrol" : kural.Kod
            : kural.Baslik;
        var kaynak = string.IsNullOrWhiteSpace(kaynakKodu) ? kural.KaynakKodu : kaynakKodu;

        return new KuralSonucu
        {
            Seviye = KontrolSeviyesi.KontrolEdilemedi,
            KuralKodu = kural.Kod,
            KaynakKodu = kaynak ?? "",
            Baslik = baslik,
            Mesaj = $"{baslik} için gerekli veri bulunamadı: {veriEksigi}.",
            CozumOnerisi = CozumOnerisiUret(veriEksigi),
            VeriEksigi = veriEksigi
        };
    }

    public static IReadOnlyList<string> EksikVerileriBul(KontrolBaglami? baglam, Kural? kural)
    {
        if (baglam is null)
            baglam = new KontrolBaglami();

        if (kural is null || kural.GerekliVeriler.Count == 0)
            return [];

        var eksikler = new List<string>();
        foreach (var veri in kural.GerekliVeriler)
        {
            if (string.IsNullOrWhiteSpace(veri))
                continue;

            if (!VeriVarMi(baglam, veri))
                eksikler.Add(veri);
        }

        return eksikler;
    }

    public static bool VeriVarMi(KontrolBaglami? baglam, string veri)
    {
        if (baglam is null || string.IsNullOrWhiteSpace(veri))
            return false;

        return veri.Trim() switch
        {
            "MimariProje" => baglam.MimariProje is not null,
            "MimariProje.ProjeAdi" => !string.IsNullOrWhiteSpace(baglam.MimariProje?.ProjeAdi),
            "IhtiyacProgrami" => baglam.IhtiyacProgrami is not null,
            "IhtiyacProgrami.BinaIslevi" => baglam.IhtiyacProgrami?.BinaIslevi is not null &&
                baglam.IhtiyacProgrami.BinaIslevi != BinaIslevi.Tanimsiz,
            "IhtiyacProgrami.ProjeTipi" => !string.IsNullOrWhiteSpace(baglam.IhtiyacProgrami?.ProjeTipi),
            "IhtiyacProgrami.KullaniciKapasitesi" => baglam.IhtiyacProgrami?.KullaniciKapasitesi > 0,
            "IhtiyacProgrami.KatSayisi" => baglam.IhtiyacProgrami?.KatSayisi > 0,
            "IhtiyacProgrami.Mahaller" => baglam.IhtiyacProgrami?.Mahaller.Count > 0,
            "PlanTaslagi" => baglam.PlanTaslagi is not null,
            "CekirdekTaslagi" => baglam.CekirdekTaslagi is not null,
            "Geometri" or "GeometriVerisi" => baglam.GeometriVerisiVar,
            _ => baglam.Parametreler.ContainsKey(veri)
        };
    }

    private static int SeviyeSirasi(KontrolSeviyesi seviye)
        => seviye switch
        {
            KontrolSeviyesi.Kritik => 0,
            KontrolSeviyesi.Uyari => 1,
            KontrolSeviyesi.KontrolEdilemedi => 2,
            KontrolSeviyesi.Bilgi => 3,
            _ => 4
        };

    private static string CozumOnerisiUret(string veriEksigi)
        => veriEksigi switch
        {
            "MimariProje" => "Geçerli bir mimari proje oluşturun veya açın.",
            "MimariProje.ProjeAdi" => "Proje adı alanını doldurun.",
            "IhtiyacProgrami" => "Bina işlevi ve mahal ihtiyaçlarını içeren bir ihtiyaç programı oluşturun.",
            "IhtiyacProgrami.BinaIslevi" => "Bina işlevini seçin.",
            "IhtiyacProgrami.ProjeTipi" => "Proje tipi alanını doldurun.",
            "IhtiyacProgrami.KullaniciKapasitesi" => "Kullanıcı kapasitesini girin.",
            "IhtiyacProgrami.KatSayisi" => "Kat sayısını 1 veya daha büyük bir değer yapın.",
            "IhtiyacProgrami.Mahaller" => "Bina işlevine göre mahal gereksinimlerini oluşturun.",
            "PlanTaslagi" => "Konsept plan taslağı oluşturulduktan sonra bu kontrolü yeniden çalıştırın.",
            "CekirdekTaslagi" => "Düşey çekirdek ve şaft taslağı oluşturulduktan sonra bu kontrolü yeniden çalıştırın.",
            "Geometri" or "GeometriVerisi" => "Plan veya çekirdek geometrisi oluşturulduktan sonra bu kontrolü yeniden çalıştırın.",
            _ => "Eksik veriyi tamamlayıp ön kontrolü yeniden çalıştırın."
        };

    private static void BinaKapsaminiKontrolEt(IhtiyacProgrami program, List<KuralSonucu> sonuclar)
    {
        if (program.BinaIslevi == BinaIslevi.Tanimsiz)
        {
            sonuclar.Add(Kritik(
                "M34-BINA-ISLEVI-EKSIK",
                "Bina işlevi seçilmemiş.",
                "Konut, ofis, eğitim, sağlık, kültür veya benzeri bir bina işlevi seçin."));
        }

        if (string.IsNullOrWhiteSpace(program.ProjeTipi))
        {
            sonuclar.Add(Uyari(
                "M34-PROJE-TIPI-EKSIK",
                "Proje tipi girilmemiş.",
                "Bina işlevine uygun proje tipi bilgisini doldurun."));
        }

        if (program.KullaniciKapasitesi < 0)
        {
            sonuclar.Add(Kritik(
                "M34-KAPASITE-NEGATIF",
                "Kullanıcı kapasitesi negatif olamaz.",
                "Kullanıcı kapasitesini 0 veya pozitif bir değer yapın."));
        }
        else if (program.KullaniciKapasitesi == 0)
        {
            sonuclar.Add(Uyari(
                "M34-KAPASITE-EKSIK",
                "Kullanıcı kapasitesi girilmemiş.",
                "İhtiyaç programı üretimi ve ön kontrol için kapasite bilgisini girin."));
        }

        if (program.KatSayisi <= 0)
        {
            sonuclar.Add(Kritik(
                "M34-KAT-SAYISI-GECERSIZ",
                "Kat sayısı pozitif olmalıdır.",
                "Kat sayısını 1 veya daha büyük bir değer yapın."));
        }

        if (program.VarsayilanKatYuksekligiM <= 0)
        {
            sonuclar.Add(Kritik(
                "M34-KAT-YUKSEKLIGI-GECERSIZ",
                "Varsayılan kat yüksekliği pozitif olmalıdır.",
                "Varsayılan kat yüksekliğini metre cinsinden pozitif bir değer yapın."));
        }
    }

    private static void MahalVerileriniKontrolEt(IhtiyacProgrami program, List<KuralSonucu> sonuclar)
    {
        if (program.Mahaller.Count == 0)
        {
            sonuclar.Add(Uyari(
                "M34-MAHAL-LISTESI-BOS",
                "İhtiyaç programında mahal bulunmuyor.",
                "Bina işlevine göre mahal gereksinimlerini oluşturun."));
            return;
        }

        foreach (var mahal in program.Mahaller)
        {
            var kaynak = string.IsNullOrWhiteSpace(mahal.Ad) ? mahal.MahalTipi.ToString() : mahal.Ad;

            if (string.IsNullOrWhiteSpace(mahal.Ad))
            {
                sonuclar.Add(Uyari(
                    "M34-MAHAL-ADI-EKSIK",
                    "Mahal adı girilmemiş.",
                    "Mahal adını doldurun.",
                    mahal.Id,
                    kaynak));
            }

            if (mahal.MahalTipi == MahalTipi.Tanimsiz)
            {
                sonuclar.Add(Uyari(
                    "M34-MAHAL-TIPI-EKSIK",
                    $"'{kaynak}' için mahal tipi seçilmemiş.",
                    "Mahali salon, ofis, koridor, wc, merdiven gibi bir tipe bağlayın.",
                    mahal.Id,
                    kaynak));
            }

            if (mahal.KullanimSinifi == KullanimSinifi.Tanimsiz)
            {
                sonuclar.Add(Uyari(
                    "M34-KULLANIM-SINIFI-EKSIK",
                    $"'{kaynak}' için kullanım sınıfı seçilmemiş.",
                    "Mahali ana kullanım, sirkülasyon, ıslak hacim veya teknik hacim gibi bir sınıfa bağlayın.",
                    mahal.Id,
                    kaynak));
            }

            if (mahal.Adet <= 0)
            {
                sonuclar.Add(Kritik(
                    "M34-MAHAL-ADEDI-GECERSIZ",
                    $"'{kaynak}' için adet pozitif olmalıdır.",
                    "Mahal adedini 1 veya daha büyük bir değer yapın.",
                    mahal.Id,
                    kaynak));
            }

            if (mahal.MinimumAlanM2 < 0 || mahal.IdealAlanM2 < 0 || mahal.HedefAlanM2 < 0)
            {
                sonuclar.Add(Kritik(
                    "M34-MAHAL-ALANI-NEGATIF",
                    $"'{kaynak}' için alan değerleri negatif olamaz.",
                    "Minimum, ideal ve hedef alanları 0 veya pozitif değerler yapın.",
                    mahal.Id,
                    kaynak));
            }

            if (mahal.HedefAlanM2 > 0 && mahal.MinimumAlanM2 > 0 && mahal.HedefAlanM2 < mahal.MinimumAlanM2)
            {
                sonuclar.Add(Uyari(
                    "M34-HEDEF-ALAN-MINIMUMDAN-KUCUK",
                    $"'{kaynak}' hedef alanı minimum alandan küçük.",
                    "Hedef alanı minimum alanın üzerine çıkarın veya minimum alanı revize edin.",
                    mahal.Id,
                    kaynak));
            }

            if (mahal.MinimumNetYukseklikM < 0 || mahal.HedefNetYukseklikM <= 0)
            {
                sonuclar.Add(Kritik(
                    "M34-MAHAL-YUKSEKLIGI-GECERSIZ",
                    $"'{kaynak}' için yükseklik değerleri geçersiz.",
                    "Minimum yüksekliği 0 veya pozitif, hedef net yüksekliği pozitif yapın.",
                    mahal.Id,
                    kaynak));
            }

            if (mahal.HedefNetYukseklikM > 0 &&
                mahal.MinimumNetYukseklikM > 0 &&
                mahal.HedefNetYukseklikM < mahal.MinimumNetYukseklikM)
            {
                sonuclar.Add(Uyari(
                    "M34-HEDEF-YUKSEKLIK-MINIMUMDAN-KUCUK",
                    $"'{kaynak}' hedef net yüksekliği minimum değerden küçük.",
                    "Hedef net yüksekliği minimum net yüksekliğin üzerine çıkarın.",
                    mahal.Id,
                    kaynak));
            }

            if (mahal.KisiKapasitesi < 0)
            {
                sonuclar.Add(Kritik(
                    "M34-MAHAL-KAPASITESI-NEGATIF",
                    $"'{kaynak}' için kişi kapasitesi negatif olamaz.",
                    "Kişi kapasitesini 0 veya pozitif bir değer yapın.",
                    mahal.Id,
                    kaynak));
            }
        }
    }

    private static void ZorunluMahalleriKontrolEt(IhtiyacProgrami program, List<KuralSonucu> sonuclar)
    {
        if (program.BinaIslevi == BinaIslevi.Tanimsiz || program.Mahaller.Count == 0)
            return;

        foreach (var sablon in VarsayilanMahalSablonlari.BinaIslevineGore(program.BinaIslevi).Where(s => s.Zorunlu))
        {
            if (program.Mahaller.Any(m => m.MahalTipi == sablon.MahalTipi))
                continue;

            sonuclar.Add(Uyari(
                "M34-ZORUNLU-MAHAL-EKSIK",
                $"'{sablon.VarsayilanAd}' mahal ailesi ihtiyaç programında bulunmuyor.",
                "Bina işlevine göre önerilen zorunlu mahal ailesini ekleyin veya neden gerekli olmadığını notlarda açıklayın.",
                kaynak: sablon.Kod));
        }
    }

    private static void AlanOranlariniKontrolEt(IhtiyacProgrami program, List<KuralSonucu> sonuclar)
    {
        if (program.HedefToplamAlanM2 < 0)
        {
            sonuclar.Add(Kritik(
                "M34-HEDEF-TOPLAM-ALAN-NEGATIF",
                "Hedef toplam alan negatif olamaz.",
                "Hedef toplam alanı 0 veya pozitif bir değer yapın."));
        }

        if (program.SirkulasyonOrani < 0)
        {
            sonuclar.Add(Kritik(
                "M34-SIRKULASYON-ORANI-NEGATIF",
                "Sirkülasyon oranı negatif olamaz.",
                "Sirkülasyon oranını 0 veya pozitif bir değer yapın."));
        }
        else if (program.SirkulasyonOrani > 0.50)
        {
            sonuclar.Add(Uyari(
                "M34-SIRKULASYON-ORANI-YUKSEK",
                "Sirkülasyon oranı ilk ihtiyaç programı için yüksek görünüyor.",
                "Koridor, çekirdek ve dolaşım kararları netleşince oranı yeniden kontrol edin."));
        }

        if (program.TeknikAlanOrani < 0)
        {
            sonuclar.Add(Kritik(
                "M34-TEKNIK-ALAN-ORANI-NEGATIF",
                "Teknik alan oranı negatif olamaz.",
                "Teknik alan oranını 0 veya pozitif bir değer yapın."));
        }
        else if (program.TeknikAlanOrani > 0.30)
        {
            sonuclar.Add(Uyari(
                "M34-TEKNIK-ALAN-ORANI-YUKSEK",
                "Teknik alan oranı ilk ihtiyaç programı için yüksek görünüyor.",
                "Mekanik, elektrik ve yangın senaryosu netleşince teknik alan oranını yeniden kontrol edin."));
        }

        if (program.Mahaller.Count == 0)
            return;

        var ozet = AlanOzetiServisi.Hesapla(program);
        if (ozet.NetAlanM2 <= 0)
        {
            sonuclar.Add(Uyari(
                "M34-NET-ALAN-BOS",
                "İhtiyaç programında net alan toplamı oluşmuyor.",
                "Mahallerin adet ve hedef alanlarını doldurun."));
        }

        if (!program.Mahaller.Any(m => m.KullanimSinifi == KullanimSinifi.AnaKullanim))
        {
            sonuclar.Add(Uyari(
                "M34-ANA-KULLANIM-YOK",
                "İhtiyaç programında ana kullanım sınıfı bulunmuyor.",
                "En az bir mahali ana kullanım sınıfına bağlayın."));
        }

        if (!program.Mahaller.Any(m =>
            m.KullanimSinifi == KullanimSinifi.Sirkulasyon ||
            m.KullanimSinifi == KullanimSinifi.DuseySirkulasyon))
        {
            sonuclar.Add(Uyari(
                "M34-SIRKULASYON-MAHALI-YOK",
                "İhtiyaç programında sirkülasyon veya düşey sirkülasyon sınıfı bulunmuyor.",
                "Koridor, merdiven, asansör veya benzeri sirkülasyon mahallerini ekleyin."));
        }

        if (!program.Mahaller.Any(m => m.KullanimSinifi == KullanimSinifi.TeknikHacim))
        {
            sonuclar.Add(Uyari(
                "M34-TEKNIK-HACIM-YOK",
                "İhtiyaç programında teknik hacim bulunmuyor.",
                "Elektrik, mekanik, şaft veya teknik oda ihtiyaçlarını değerlendirin."));
        }

        if (program.HedefToplamAlanM2 > 0 && Math.Abs(ozet.HedefFarkOrani) > 0.10)
        {
            sonuclar.Add(Uyari(
                "M34-HEDEF-TOPLAM-ALAN-FARKI",
                "Brüt alan hesabı ile hedef toplam alan arasında yüzde 10'dan fazla fark var.",
                "Net alan, sirkülasyon oranı, teknik alan oranı veya hedef toplam alanı yeniden dengeleyin."));
        }
    }

    private static void PlanTaslaginiKontrolEt(PlanTaslagi? plan, List<KuralSonucu> sonuclar)
    {
        if (plan is null)
        {
            sonuclar.Add(KontrolEdilemedi(
                "M34-PLAN-TASLAGI-YOK",
                "Konsept plan taslağı bulunmuyor.",
                "M32 konsept plan üretimini çalıştırın.",
                veriEksigi: "PlanTaslagi"));
            return;
        }

        if (plan.Katlar.Count == 0)
        {
            sonuclar.Add(Uyari(
                "M34-PLAN-KAT-YOK",
                "Plan taslağında kat bulunmuyor.",
                "İhtiyaç programından en az bir katlı konsept plan üretin."));
            return;
        }

        var tolerans = Math.Max(0, plan.Parametreler.AlanToleransiOrani);
        foreach (var kat in plan.Katlar.OrderBy(k => k.KatNo))
        {
            if (kat.Sinir.GenislikM <= 0 || kat.Sinir.DerinlikM <= 0)
            {
                sonuclar.Add(Uyari(
                    "M34-PLAN-KAT-SINIRI-YOK",
                    $"{kat.Ad} için plan sınırı oluşmamış.",
                    "Kat yerleşimini yeniden üretin veya blokları geçerli sınır içine taşıyın.",
                    katId: kat.Id));
            }

            if (kat.Bloklar.Count == 0)
            {
                sonuclar.Add(Uyari(
                    "M34-PLAN-KAT-BLOK-YOK",
                    $"{kat.Ad} içinde mahal bloğu bulunmuyor.",
                    "Kat dağıtımını yeniden üretin veya ihtiyaç programındaki mahalleri kontrol edin.",
                    katId: kat.Id));
                continue;
            }

            var baglanacakBloklar = KoridorYerlesimServisi.BaglanmasiGerekenBloklariBul(kat);
            if (baglanacakBloklar.Count > 0 && kat.Koridorlar.Count == 0)
            {
                sonuclar.Add(Uyari(
                    "M34-PLAN-KAT-KORIDOR-YOK",
                    $"{kat.Ad} içinde ana koridor taslağı bulunmuyor.",
                    "Koridor üretimini yeniden çalıştırın veya kat dolaşım aksını tanımlayın.",
                    katId: kat.Id));
            }

            foreach (var blok in kat.Bloklar.Where(b => b.HedefAlanM2 > 0 && !b.YerlesmisMi))
            {
                sonuclar.Add(Uyari(
                    "M34-PLAN-BLOK-YERLESMEMIS",
                    $"'{blok.Ad}' plan üzerinde yerleşmiş görünmüyor.",
                    "Bloğu geçerli boyut ve konuma taşıyın veya yerleşimi yeniden üretin.",
                    blok.Id,
                    katId: kat.Id));
            }

            foreach (var blok in KoridorYerlesimServisi.BaglantisizBloklariBul(kat))
            {
                sonuclar.Add(Uyari(
                    "M34-PLAN-BAGLANTISIZ-BLOK",
                    $"'{blok.Ad}' koridor ağına bağlı görünmüyor.",
                    "Bloku ana koridor ağına bağlayın veya koridor üretimini yeniden çalıştırın.",
                    blok.Id,
                    katId: kat.Id));
            }

            foreach (var (a, b) in BasitYerlesimServisi.CakisanBlokCiftleri(kat))
            {
                sonuclar.Add(Uyari(
                    "M34-PLAN-BLOK-CAKISMASI",
                    $"'{a.Ad}' ile '{b.Ad}' plan üzerinde çakışıyor.",
                    "Blokları ayırın veya yerleşimi yeniden üretin.",
                    a.Id,
                    katId: kat.Id));
            }

            foreach (var blok in kat.Bloklar.Where(b => !BasitYerlesimServisi.AlanToleransiIcindeMi(b, tolerans)))
            {
                sonuclar.Add(Uyari(
                    "M34-PLAN-ALAN-TOLERANSI",
                    $"'{blok.Ad}' yerleşim alanı hedef alandan tolerans dışında sapıyor.",
                    "Blok boyutlarını hedef alana yaklaştırın veya ihtiyaç programı alanını revize edin.",
                    blok.Id,
                    katId: kat.Id));
            }
        }

        foreach (var uyari in SirkulasyonKontrolServisi.KatlarArasiSureklilikUyarilari(plan))
        {
            sonuclar.Add(SirkulasyonUyarisi(uyari));
        }

        foreach (var uyari in SirkulasyonKontrolServisi.IslakHacimSaftUyarilari(plan))
        {
            sonuclar.Add(SirkulasyonUyarisi(uyari));
        }

        foreach (var uyari in SirkulasyonKontrolServisi.KacisGrafiUyarilari(plan))
        {
            sonuclar.Add(SirkulasyonUyarisi(uyari));
        }
    }

    private static KuralSonucu FinisajUyarisi(MahalFinisajUyarisi uyari)
        => new()
        {
            Seviye = uyari.EksikVeriMi ? KontrolSeviyesi.KontrolEdilemedi : KontrolSeviyesi.Uyari,
            KuralKodu = uyari.KuralKodu,
            KaynakKodu = VarsayilanVeriKaynakKodu,
            Baslik = uyari.KuralKodu,
            Mesaj = uyari.Mesaj,
            CozumOnerisi = uyari.CozumOnerisi,
            EtkilenenMahalId = uyari.EtkilenenMahalId,
            VeriEksigi = uyari.VeriEksigi
        };

    private static KuralSonucu SirkulasyonUyarisi(SirkulasyonKontrolUyarisi uyari)
        => new()
        {
            Seviye = KontrolSeviyesi.Uyari,
            KuralKodu = uyari.KuralKodu,
            KaynakKodu = VarsayilanVeriKaynakKodu,
            Baslik = uyari.KuralKodu,
            Mesaj = uyari.Mesaj,
            CozumOnerisi = uyari.CozumOnerisi,
            EtkilenenKatId = uyari.EtkilenenKatId,
            EtkilenenMahalId = uyari.EtkilenenMahalId,
            EtkilenenElemanId = uyari.EtkilenenElemanId
        };

    private static KuralSonucu Kritik(
        string kod,
        string mesaj,
        string cozumOnerisi,
        string? mahalId = null,
        string? kaynak = null,
        string? katId = null)
        => Sonuc(KontrolSeviyesi.Kritik, kod, mesaj, cozumOnerisi, mahalId, kaynak, katId);

    private static KuralSonucu Uyari(
        string kod,
        string mesaj,
        string cozumOnerisi,
        string? mahalId = null,
        string? kaynak = null,
        string? katId = null)
        => Sonuc(KontrolSeviyesi.Uyari, kod, mesaj, cozumOnerisi, mahalId, kaynak, katId);

    private static KuralSonucu KontrolEdilemedi(
        string kod,
        string mesaj,
        string cozumOnerisi,
        string? veriEksigi = null,
        string? mahalId = null,
        string? kaynak = null,
        string? katId = null)
        => Sonuc(KontrolSeviyesi.KontrolEdilemedi, kod, mesaj, cozumOnerisi, mahalId, kaynak, katId, veriEksigi);

    private static KuralSonucu Sonuc(
        KontrolSeviyesi seviye,
        string kod,
        string mesaj,
        string cozumOnerisi,
        string? mahalId,
        string? kaynak,
        string? katId = null,
        string? veriEksigi = null)
        => new()
        {
            Seviye = seviye,
            KuralKodu = kod,
            KaynakKodu = VarsayilanVeriKaynakKodu,
            Baslik = kod,
            Mesaj = mesaj,
            CozumOnerisi = cozumOnerisi,
            EtkilenenMahalId = mahalId,
            EtkilenenKatId = katId,
            VeriEksigi = veriEksigi
        };
}
