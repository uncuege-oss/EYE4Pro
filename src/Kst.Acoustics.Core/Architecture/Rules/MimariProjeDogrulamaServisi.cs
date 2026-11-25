using System.Collections.Generic;
using Kst.Acoustics.Core.Architecture.Models;

namespace Kst.Acoustics.Core.Architecture.Rules;

/// <summary>
/// Mimari BIM domain modelinin temel veri gecerliligini kontrol eder.
/// </summary>
public static class MimariProjeDogrulamaServisi
{
    public static IReadOnlyList<MimariKontrolSonucu> Dogrula(MimariProje? proje)
    {
        var sonuclar = new List<MimariKontrolSonucu>();

        if (proje is null)
        {
            sonuclar.Add(Kritik(
                "MIMARI_PROJE_BOS",
                "Mimari proje verisi boş.",
                "Yeni bir mimari proje oluşturun veya geçerli bir .e4mimari.json dosyası açın."));
            return sonuclar;
        }

        if (string.IsNullOrWhiteSpace(proje.ProjeAdi))
        {
            sonuclar.Add(Uyari(
                "PROJE_ADI_EKSIK",
                "Proje adı girilmemiş.",
                "Proje adı alanını doldurun."));
        }

        if (string.IsNullOrWhiteSpace(proje.Id))
        {
            sonuclar.Add(Uyari(
                "PROJE_ID_EKSIK",
                "Proje kimliği boş.",
                "Projeyi yeniden kaydederek kalıcı kimlik oluşturun."));
        }

        DogrulaIhtiyacProgrami(proje.IhtiyacProgrami, sonuclar);
        DogrulaSablonlar(proje.MahalSablonlari, sonuclar);

        return sonuclar;
    }

    public static bool GecerliMi(MimariProje? proje)
        => !Dogrula(proje).AnyCritical();

    private static void DogrulaIhtiyacProgrami(IhtiyacProgrami? program, List<MimariKontrolSonucu> sonuclar)
    {
        if (program is null)
        {
            sonuclar.Add(Kritik(
                "IHTIYAC_PROGRAMI_BOS",
                "İhtiyaç programı bulunmuyor.",
                "Bina işlevi ve mahal ihtiyaçlarını içeren bir ihtiyaç programı oluşturun."));
            return;
        }

        if (program.BinaIslevi == BinaIslevi.Tanimsiz)
        {
            sonuclar.Add(Kritik(
                "BINA_ISLEVI_EKSIK",
                "Bina işlevi seçilmemiş.",
                "Konut, ofis, eğitim, sağlık, kültür veya benzeri bir bina işlevi seçin."));
        }

        if (program.KatSayisi <= 0)
        {
            sonuclar.Add(Kritik(
                "KAT_SAYISI_GECERSIZ",
                "Kat sayısı pozitif olmalıdır.",
                "Kat sayısını 1 veya daha büyük bir değer yapın."));
        }

        if (program.KullaniciKapasitesi < 0)
        {
            sonuclar.Add(Kritik(
                "KAPASITE_NEGATIF",
                "Kullanıcı kapasitesi negatif olamaz.",
                "Kullanıcı kapasitesini 0 veya pozitif bir değer yapın."));
        }

        if (!double.IsFinite(program.HedefToplamAlanM2) || program.HedefToplamAlanM2 < 0)
        {
            sonuclar.Add(Kritik(
                "HEDEF_TOPLAM_ALAN_NEGATIF",
                "Hedef toplam alan sonlu olmalı ve negatif olmamalıdır.",
                "Hedef toplam alanı 0 veya pozitif bir değer yapın."));
        }

        if (!double.IsFinite(program.VarsayilanKatYuksekligiM) || program.VarsayilanKatYuksekligiM <= 0)
        {
            sonuclar.Add(Kritik(
                "KAT_YUKSEKLIGI_GECERSIZ",
                "Varsayılan kat yüksekliği pozitif ve sonlu olmalıdır.",
                "Varsayılan kat yüksekliğini metre cinsinden pozitif bir değer yapın."));
        }

        if (!double.IsFinite(program.SirkulasyonOrani) || program.SirkulasyonOrani < 0)
        {
            sonuclar.Add(Kritik(
                "SIRKULASYON_ORANI_NEGATIF",
                "Sirkülasyon oranı sonlu olmalı ve negatif olmamalıdır.",
                "Sirkülasyon oranını 0 veya pozitif bir değer yapın."));
        }

        if (!double.IsFinite(program.TeknikAlanOrani) || program.TeknikAlanOrani < 0)
        {
            sonuclar.Add(Kritik(
                "TEKNIK_ALAN_ORANI_NEGATIF",
                "Teknik alan oranı sonlu olmalı ve negatif olmamalıdır.",
                "Teknik alan oranını 0 veya pozitif bir değer yapın."));
        }

        if (program.Mahaller.Count == 0)
        {
            sonuclar.Add(Uyari(
                "MAHAL_LISTESI_BOS",
                "İhtiyaç programında mahal bulunmuyor.",
                "Bina işlevine göre mahal gereksinimlerini ekleyin."));
            return;
        }

        foreach (var mahal in program.Mahaller)
        {
            DogrulaMahal(mahal, sonuclar);
        }
    }

    private static void DogrulaMahal(MahalGereksinimi mahal, List<MimariKontrolSonucu> sonuclar)
    {
        var kaynak = string.IsNullOrWhiteSpace(mahal.Ad) ? mahal.MahalTipi.ToString() : mahal.Ad;

        if (mahal.MahalTipi == MahalTipi.Tanimsiz)
        {
            sonuclar.Add(Uyari(
                "MAHAL_TIPI_EKSIK",
                $"'{kaynak}' için mahal tipi seçilmemiş.",
                "Mahali salon, ofis, koridor, wc, merdiven gibi bir tipe bağlayın.",
                mahal.Id,
                kaynak));
        }

        if (mahal.KullanimSinifi == KullanimSinifi.Tanimsiz)
        {
            sonuclar.Add(Uyari(
                "KULLANIM_SINIFI_EKSIK",
                $"'{kaynak}' için kullanım sınıfı seçilmemiş.",
                "Mahali ana kullanım, sirkülasyon, ıslak hacim veya teknik hacim gibi bir sınıfa bağlayın.",
                mahal.Id,
                kaynak));
        }

        if (string.IsNullOrWhiteSpace(mahal.Ad))
        {
            sonuclar.Add(Uyari(
                "MAHAL_ADI_EKSIK",
                "Mahal adı girilmemiş.",
                "Mahal adını doldurun.",
                mahal.Id,
                kaynak));
        }

        if (mahal.Adet <= 0)
        {
            sonuclar.Add(Kritik(
                "MAHAL_ADEDI_GECERSIZ",
                $"'{kaynak}' için adet pozitif olmalıdır.",
                "Mahal adedini 1 veya daha büyük bir değer yapın.",
                mahal.Id,
                kaynak));
        }

        if (!double.IsFinite(mahal.MinimumAlanM2) || !double.IsFinite(mahal.IdealAlanM2) ||
            !double.IsFinite(mahal.HedefAlanM2) || mahal.MinimumAlanM2 < 0 || mahal.IdealAlanM2 < 0 || mahal.HedefAlanM2 < 0)
        {
            sonuclar.Add(Kritik(
                "MAHAL_ALANI_NEGATIF",
                $"'{kaynak}' için alan değerleri sonlu olmalı ve negatif olmamalıdır.",
                "Minimum, ideal ve hedef alanları 0 veya pozitif değerler yapın.",
                mahal.Id,
                kaynak));
        }

        if (mahal.HedefAlanM2 > 0 && mahal.MinimumAlanM2 > 0 && mahal.HedefAlanM2 < mahal.MinimumAlanM2)
        {
            sonuclar.Add(Uyari(
                "HEDEF_ALAN_MINIMUMDAN_KUCUK",
                $"'{kaynak}' hedef alanı minimum alandan küçük.",
                "Hedef alanı minimum alanın üzerine çıkarın veya minimum alanı revize edin.",
                mahal.Id,
                kaynak));
        }

        if (!double.IsFinite(mahal.MinimumNetYukseklikM) || !double.IsFinite(mahal.HedefNetYukseklikM) ||
            mahal.MinimumNetYukseklikM < 0 || mahal.HedefNetYukseklikM <= 0)
        {
            sonuclar.Add(Kritik(
                "MAHAL_YUKSEKLIGI_GECERSIZ",
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
                "HEDEF_YUKSEKLIK_MINIMUMDAN_KUCUK",
                $"'{kaynak}' hedef net yüksekliği minimum değerden küçük.",
                "Hedef net yüksekliği minimum net yüksekliğin üzerine çıkarın.",
                mahal.Id,
                kaynak));
        }

        if (mahal.KisiKapasitesi < 0)
        {
            sonuclar.Add(Kritik(
                "MAHAL_KAPASITESI_NEGATIF",
                $"'{kaynak}' için kişi kapasitesi negatif olamaz.",
                "Kişi kapasitesini 0 veya pozitif bir değer yapın.",
                mahal.Id,
                kaynak));
        }
    }

    private static void DogrulaSablonlar(List<MahalSablonu>? sablonlar, List<MimariKontrolSonucu> sonuclar)
    {
        if (sablonlar is null)
            return;

        foreach (var sablon in sablonlar)
        {
            var kaynak = string.IsNullOrWhiteSpace(sablon.Kod)
                ? sablon.VarsayilanAd
                : sablon.Kod;

            if (string.IsNullOrWhiteSpace(kaynak))
                kaynak = sablon.MahalTipi.ToString();

            if (sablon.MahalTipi == MahalTipi.Tanimsiz)
            {
                sonuclar.Add(Uyari(
                    "SABLON_MAHAL_TIPI_EKSIK",
                    $"'{kaynak}' şablonu için mahal tipi seçilmemiş.",
                    "Şablonu bir mahal tipine bağlayın.",
                    kaynak: kaynak));
            }

            if (sablon.VarsayilanAdet <= 0)
            {
                sonuclar.Add(Kritik(
                    "SABLON_ADEDI_GECERSIZ",
                    $"'{kaynak}' şablonu için varsayılan adet pozitif olmalıdır.",
                    "Varsayılan adedi 1 veya daha büyük bir değer yapın.",
                    kaynak: kaynak));
            }

            if (!double.IsFinite(sablon.MinimumAlanM2) || !double.IsFinite(sablon.IdealAlanM2) ||
                !double.IsFinite(sablon.KisiBasinaAlanM2) || sablon.MinimumAlanM2 < 0 || sablon.IdealAlanM2 < 0 || sablon.KisiBasinaAlanM2 < 0)
            {
                sonuclar.Add(Kritik(
                    "SABLON_ALANI_NEGATIF",
                    $"'{kaynak}' şablonu için alan değerleri sonlu olmalı ve negatif olmamalıdır.",
                    "Minimum, ideal ve kişi başına alan değerlerini 0 veya pozitif yapın.",
                    kaynak: kaynak));
            }

            if (!double.IsFinite(sablon.MinimumNetYukseklikM) || sablon.MinimumNetYukseklikM <= 0)
            {
                sonuclar.Add(Kritik(
                    "SABLON_YUKSEKLIGI_GECERSIZ",
                    $"'{kaynak}' şablonu için minimum net yükseklik pozitif ve sonlu olmalıdır.",
                    "Minimum net yüksekliği pozitif bir değer yapın.",
                    kaynak: kaynak));
            }
        }
    }

    private static MimariKontrolSonucu Kritik(
        string kod,
        string mesaj,
        string cozumOnerisi,
        string? mahalId = null,
        string? kaynak = null)
        => Sonuc(KontrolSeviyesi.Kritik, kod, mesaj, cozumOnerisi, mahalId, kaynak);

    private static MimariKontrolSonucu Uyari(
        string kod,
        string mesaj,
        string cozumOnerisi,
        string? mahalId = null,
        string? kaynak = null)
        => Sonuc(KontrolSeviyesi.Uyari, kod, mesaj, cozumOnerisi, mahalId, kaynak);

    private static MimariKontrolSonucu Sonuc(
        KontrolSeviyesi seviye,
        string kod,
        string mesaj,
        string cozumOnerisi,
        string? mahalId,
        string? kaynak)
        => new()
        {
            Seviye = seviye,
            Kod = kod,
            Mesaj = mesaj,
            CozumOnerisi = cozumOnerisi,
            MahalId = mahalId,
            Kaynak = kaynak
        };

    private static bool AnyCritical(this IReadOnlyList<MimariKontrolSonucu> sonuclar)
    {
        foreach (var sonuc in sonuclar)
        {
            if (sonuc.KritikMi)
                return true;
        }

        return false;
    }
}
