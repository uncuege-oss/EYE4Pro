using System.Globalization;
using System.Net;
using System.Text;
using Kst.Acoustics.Core.Architecture.Models;
using Kst.Acoustics.Core.Architecture.Rules;
using Kst.Acoustics.Core.Architecture.Services;
using Kst.Acoustics.Core.Architecture.Templates;

namespace Kst.Acoustics.Core.Architecture.Reports;

/// <summary>
/// Ihtiyac programi icin ilk HTML rapor ve CSV mahal listesi ciktisini uretir.
/// </summary>
public static class IhtiyacProgramiRaporServisi
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static string HtmlOlustur(MimariProje proje)
    {
        ArgumentNullException.ThrowIfNull(proje);
        ArgumentNullException.ThrowIfNull(proje.IhtiyacProgrami);

        var program = proje.IhtiyacProgrami;
        var ozet = AlanOzetiServisi.Hesapla(program);
        var tanim = BinaIsleviTanimlari.Bul(program.BinaIslevi);
        var sonuclar = MimariProjeDogrulamaServisi.Dogrula(proje);
        var sb = new StringBuilder();

        sb.AppendLine("<!doctype html>");
        sb.AppendLine("<html lang=\"tr\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\">");
        sb.AppendLine("  <title>İhtiyaç Programı Raporu</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    body { font-family: Arial, sans-serif; color: #1f2933; margin: 32px; }");
        sb.AppendLine("    h1, h2 { color: #0f3f4a; }");
        sb.AppendLine("    table { border-collapse: collapse; width: 100%; margin: 16px 0 28px; }");
        sb.AppendLine("    th, td { border: 1px solid #d7dee5; padding: 8px 10px; font-size: 13px; text-align: left; }");
        sb.AppendLine("    th { background: #edf5f7; }");
        sb.AppendLine("    .summary { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 10px; margin: 18px 0 26px; }");
        sb.AppendLine("    .metric { border: 1px solid #d7dee5; padding: 12px; }");
        sb.AppendLine("    .metric span { display: block; color: #5f7080; font-size: 12px; }");
        sb.AppendLine("    .metric strong { display: block; margin-top: 4px; font-size: 18px; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <h1>İhtiyaç Programı Raporu</h1>");
        sb.AppendLine("  <table>");
        BilgiSatiri(sb, "Proje", proje.ProjeAdi);
        BilgiSatiri(sb, "Bina İşlevi", tanim?.Ad ?? Gorunum(program.BinaIslevi));
        BilgiSatiri(sb, "Proje Tipi", program.ProjeTipi);
        BilgiSatiri(sb, "Kapasite", program.KullaniciKapasitesi.ToString("N0", Tr));
        BilgiSatiri(sb, "Kat Sayısı", program.KatSayisi.ToString("N0", Tr));
        BilgiSatiri(sb, "Varsayılan Kat Yüksekliği", M(program.VarsayilanKatYuksekligiM));
        sb.AppendLine("  </table>");

        sb.AppendLine("  <h2>Alan Özeti</h2>");
        sb.AppendLine("  <div class=\"summary\">");
        Metrik(sb, "Net Alan", M2(ozet.NetAlanM2));
        Metrik(sb, "Sirkülasyon", M2(ozet.SirkulasyonAlanM2));
        Metrik(sb, "Teknik Alan", M2(ozet.TeknikAlanM2));
        Metrik(sb, "Brüt Alan", M2(ozet.BrutAlanM2));
        Metrik(sb, "Net Hacim", M3(ozet.NetHacimM3));
        Metrik(sb, "Mahal Satırı", ozet.MahalSayisi.ToString("N0", Tr));
        Metrik(sb, "Toplam Mahal Adedi", ozet.ToplamMahalAdedi.ToString("N0", Tr));
        Metrik(sb, "Hedef Farkı", M2(ozet.HedefFarkM2));
        sb.AppendLine("  </div>");

        sb.AppendLine("  <h2>Kullanım Sınıfı Kırılımı</h2>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>Kullanım Sınıfı</th><th>Mahal</th><th>Adet</th><th>Alan</th><th>Hacim</th><th>Oran</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        foreach (var satir in ozet.KullanimSinifiOzetleri)
        {
            sb.AppendLine("      <tr>");
            Hucre(sb, Gorunum(satir.KullanimSinifi));
            Hucre(sb, satir.MahalSayisi.ToString("N0", Tr));
            Hucre(sb, satir.ToplamAdet.ToString("N0", Tr));
            Hucre(sb, M2(satir.AlanM2));
            Hucre(sb, M3(satir.HacimM3));
            Hucre(sb, satir.AlanOrani.ToString("P1", Tr));
            sb.AppendLine("      </tr>");
        }

        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");

        sb.AppendLine("  <h2>Mahal Listesi</h2>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>Ad</th><th>Tip</th><th>Sınıf</th><th>Adet</th><th>m²/Mahal</th><th>Toplam Alan</th><th>Yükseklik</th><th>Hacim</th><th>Zorunlu</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        foreach (var mahal in program.Mahaller)
        {
            sb.AppendLine("      <tr>");
            Hucre(sb, mahal.Ad);
            Hucre(sb, Gorunum(mahal.MahalTipi));
            Hucre(sb, Gorunum(mahal.KullanimSinifi));
            Hucre(sb, mahal.Adet.ToString("N0", Tr));
            Hucre(sb, M2(mahal.HedefAlanM2));
            Hucre(sb, M2(mahal.ToplamHedefAlanM2));
            Hucre(sb, M(mahal.HedefNetYukseklikM));
            Hucre(sb, M3(mahal.ToplamHedefHacimM3));
            Hucre(sb, mahal.Zorunlu ? "Evet" : "Hayır");
            sb.AppendLine("      </tr>");
        }

        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");

        sb.AppendLine("  <h2>Ön Kontrol</h2>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>Seviye</th><th>Kod</th><th>Mesaj</th><th>Çözüm Önerisi</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        foreach (var sonuc in sonuclar)
        {
            sb.AppendLine("      <tr>");
            Hucre(sb, Gorunum(sonuc.Seviye));
            Hucre(sb, sonuc.Kod);
            Hucre(sb, sonuc.Mesaj);
            Hucre(sb, sonuc.CozumOnerisi);
            sb.AppendLine("      </tr>");
        }

        if (sonuclar.Count == 0)
        {
            sb.AppendLine("      <tr><td colspan=\"4\">Kritik veya uyarı düzeyinde sonuç bulunmuyor.</td></tr>");
        }

        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    public static string CsvOlustur(MimariProje proje)
    {
        ArgumentNullException.ThrowIfNull(proje);
        ArgumentNullException.ThrowIfNull(proje.IhtiyacProgrami);

        var sb = new StringBuilder();
        CsvSatiri(sb,
            "Mahal Id",
            "Ad",
            "Mahal Tipi",
            "Kullanım Sınıfı",
            "Adet",
            "Kişi Kapasitesi",
            "Minimum Alan m²",
            "İdeal Alan m²",
            "Hedef Alan m²",
            "Toplam Hedef Alan m²",
            "Hedef Net Yükseklik m",
            "Toplam Hedef Hacim m³",
            "Zorunlu",
            "Notlar");

        foreach (var mahal in proje.IhtiyacProgrami.Mahaller)
        {
            CsvSatiri(sb,
                mahal.Id,
                mahal.Ad,
                Gorunum(mahal.MahalTipi),
                Gorunum(mahal.KullanimSinifi),
                mahal.Adet.ToString("N0", Tr),
                mahal.KisiKapasitesi.ToString("N0", Tr),
                Sayisal(mahal.MinimumAlanM2),
                Sayisal(mahal.IdealAlanM2),
                Sayisal(mahal.HedefAlanM2),
                Sayisal(mahal.ToplamHedefAlanM2),
                Sayisal(mahal.HedefNetYukseklikM),
                Sayisal(mahal.ToplamHedefHacimM3),
                mahal.Zorunlu ? "Evet" : "Hayır",
                mahal.Notlar);
        }

        return sb.ToString();
    }

    private static void BilgiSatiri(StringBuilder sb, string etiket, string? deger)
    {
        sb.AppendLine("    <tr>");
        Hucre(sb, etiket, "th");
        Hucre(sb, string.IsNullOrWhiteSpace(deger) ? "-" : deger);
        sb.AppendLine("    </tr>");
    }

    private static void Metrik(StringBuilder sb, string etiket, string deger)
    {
        sb.Append("    <div class=\"metric\"><span>");
        sb.Append(Encode(etiket));
        sb.Append("</span><strong>");
        sb.Append(Encode(deger));
        sb.AppendLine("</strong></div>");
    }

    private static void Hucre(StringBuilder sb, string? deger, string tag = "td")
    {
        sb.Append("        <");
        sb.Append(tag);
        sb.Append('>');
        sb.Append(Encode(deger));
        sb.Append("</");
        sb.Append(tag);
        sb.AppendLine(">");
    }

    private static void CsvSatiri(StringBuilder sb, params string?[] degerler)
        => sb.AppendLine(string.Join(';', degerler.Select(CsvDegeri)));

    private static string CsvDegeri(string? deger)
    {
        deger ??= "";
        if (!deger.Contains(';') && !deger.Contains('"') && !deger.Contains('\n') && !deger.Contains('\r'))
            return deger;

        return $"\"{deger.Replace("\"", "\"\"")}\"";
    }

    private static string Encode(string? deger) => WebUtility.HtmlEncode(deger ?? "");

    private static string Sayisal(double deger) => deger.ToString("N2", Tr);

    private static string M2(double deger) => $"{Sayisal(deger)} m²";

    private static string M3(double deger) => $"{Sayisal(deger)} m³";

    private static string M(double deger) => $"{Sayisal(deger)} m";

    private static string Gorunum(BinaIslevi deger)
        => BinaIsleviTanimlari.Bul(deger)?.Ad ?? deger.ToString();

    private static string Gorunum(MahalTipi deger) => deger switch
    {
        MahalTipi.Tanimsiz => "Tanımsız",
        MahalTipi.Salon => "Salon",
        MahalTipi.OturmaOdasi => "Oturma Odası",
        MahalTipi.YatakOdasi => "Yatak Odası",
        MahalTipi.CocukOdasi => "Çocuk Odası",
        MahalTipi.Mutfak => "Mutfak",
        MahalTipi.Banyo => "Banyo",
        MahalTipi.Wc => "WC",
        MahalTipi.Antre => "Antre",
        MahalTipi.Hol => "Hol",
        MahalTipi.Balkon => "Balkon",
        MahalTipi.Teras => "Teras",
        MahalTipi.Koridor => "Koridor",
        MahalTipi.Depo => "Depo",
        MahalTipi.TeknikOda => "Teknik Oda",
        MahalTipi.ElektrikOdasi => "Elektrik Odası",
        MahalTipi.MekanikOda => "Mekanik Oda",
        MahalTipi.JeneratorOdasi => "Jeneratör Odası",
        MahalTipi.KazanDairesi => "Kazan Dairesi",
        MahalTipi.Siginak => "Sığınak",
        MahalTipi.Otopark => "Otopark",
        MahalTipi.Merdiven => "Merdiven",
        MahalTipi.YanginMerdiveni => "Yangın Merdiveni",
        MahalTipi.Asansor => "Asansör",
        MahalTipi.Rampa => "Rampa",
        MahalTipi.Saft => "Şaft",
        MahalTipi.Cekirdek => "Çekirdek",
        MahalTipi.Ofis => "Ofis",
        MahalTipi.AcikOfis => "Açık Ofis",
        MahalTipi.ToplantiOdasi => "Toplantı Odası",
        MahalTipi.Resepsiyon => "Resepsiyon",
        MahalTipi.Arsiv => "Arşiv",
        MahalTipi.ServerOdasi => "Server Odası",
        MahalTipi.Magaza => "Mağaza",
        MahalTipi.TicariAlan => "Ticari Alan",
        MahalTipi.Restoran => "Restoran",
        MahalTipi.Kafe => "Kafe",
        MahalTipi.Derslik => "Derslik",
        MahalTipi.Laboratuvar => "Laboratuvar",
        MahalTipi.Atolye => "Atölye",
        MahalTipi.OgretmenOdasi => "Öğretmen Odası",
        MahalTipi.Kutuphane => "Kütüphane",
        MahalTipi.KonferansSalonu => "Konferans Salonu",
        MahalTipi.Fuaye => "Fuaye",
        MahalTipi.Sahne => "Sahne",
        MahalTipi.Kulis => "Kulis",
        MahalTipi.MuayeneOdasi => "Muayene Odası",
        MahalTipi.BeklemeAlani => "Bekleme Alanı",
        MahalTipi.HastaOdasi => "Hasta Odası",
        MahalTipi.PersonelOdasi => "Personel Odası",
        _ => deger.ToString()
    };

    private static string Gorunum(KullanimSinifi deger) => deger switch
    {
        KullanimSinifi.Tanimsiz => "Tanımsız",
        KullanimSinifi.AnaKullanim => "Ana Kullanım",
        KullanimSinifi.YardimciKullanim => "Yardımcı Kullanım",
        KullanimSinifi.Sirkulasyon => "Sirkülasyon",
        KullanimSinifi.IslakHacim => "Islak Hacim",
        KullanimSinifi.TeknikHacim => "Teknik Hacim",
        KullanimSinifi.Servis => "Servis",
        KullanimSinifi.Depolama => "Depolama",
        KullanimSinifi.DuseySirkulasyon => "Düşey Sirkülasyon",
        KullanimSinifi.AcikAlan => "Açık Alan",
        KullanimSinifi.Otopark => "Otopark",
        KullanimSinifi.GuvenlikVeYangin => "Güvenlik ve Yangın",
        KullanimSinifi.HesapDisi => "Hesap Dışı",
        _ => deger.ToString()
    };

    private static string Gorunum(KontrolSeviyesi deger) => deger switch
    {
        KontrolSeviyesi.Bilgi => "Bilgi",
        KontrolSeviyesi.Uyari => "Uyarı",
        KontrolSeviyesi.Kritik => "Kritik",
        KontrolSeviyesi.KontrolEdilemedi => "Kontrol Edilemedi",
        _ => deger.ToString()
    };
}
