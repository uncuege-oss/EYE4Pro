using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using Kst.Acoustics.Core.Architecture.Models;
using Kst.Acoustics.Core.Architecture.Rules;
using Kst.Acoustics.Core.Architecture.Templates;

namespace Kst.Acoustics.Core.Architecture.Reports;

/// <summary>
/// Mimari rapor uretiminde paylasilan bicim, gorunum adi, HTML-encode ve CSV yardimcilari.
/// Tum rapor servis/builder'lari (M31, M37) tek kaynaktan beslenir.
/// </summary>
internal static class MimariRaporMetin
{
    internal static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    internal static string Encode(string? deger) => WebUtility.HtmlEncode(deger ?? "");

    internal static string Sayisal(double deger) => deger.ToString("N2", Tr);
    internal static string Tamsayi(int deger) => deger.ToString("N0", Tr);
    internal static string Yuzde(double oran) => oran.ToString("P1", Tr);
    internal static string M2(double deger) => $"{Sayisal(deger)} m²";
    internal static string M3(double deger) => $"{Sayisal(deger)} m³";
    internal static string M(double deger) => $"{Sayisal(deger)} m";

    internal static string CsvDegeri(string? deger)
    {
        deger ??= "";
        if (!deger.Contains(';') && !deger.Contains('"') && !deger.Contains('\n') && !deger.Contains('\r'))
            return deger;

        return $"\"{deger.Replace("\"", "\"\"")}\"";
    }

    internal static void CsvSatiri(StringBuilder sb, params string?[] degerler)
        => sb.AppendLine(string.Join(';', degerler.Select(CsvDegeri)));

    internal static string Ad(BinaIslevi deger)
        => BinaIsleviTanimlari.Bul(deger)?.Ad ?? deger.ToString();

    internal static string Ad(MahalTipi deger) => deger switch
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

    internal static string Ad(KullanimSinifi deger) => deger switch
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

    internal static string Ad(KontrolSeviyesi deger) => deger switch
    {
        KontrolSeviyesi.Bilgi => "Bilgi",
        KontrolSeviyesi.Uyari => "Uyarı",
        KontrolSeviyesi.Kritik => "Kritik",
        KontrolSeviyesi.KontrolEdilemedi => "Kontrol Edilemedi",
        _ => deger.ToString()
    };
}
