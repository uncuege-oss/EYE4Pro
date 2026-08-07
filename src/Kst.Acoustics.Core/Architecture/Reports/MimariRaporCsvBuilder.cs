using System.Text;
using static Kst.Acoustics.Core.Architecture.Reports.MimariRaporMetin;

namespace Kst.Acoustics.Core.Architecture.Reports;

/// <summary>
/// M37 mimari rapor tablolarini CSV (noktali virgul ayracli) metne cevirir. Her tablo ayri uretilir.
/// </summary>
public static class MimariRaporCsvBuilder
{
    public static string MahalListesiCsv(MahalListesiTablosu tablo)
    {
        ArgumentNullException.ThrowIfNull(tablo);
        var sb = new StringBuilder();
        CsvSatiri(sb, "Kat", "Mahal No", "Ad", "Tip", "Kullanım Sınıfı", "Adet", "Alan m²", "Hacim m³");
        foreach (var s in tablo.Satirlar)
        {
            CsvSatiri(sb,
                Tamsayi(s.KatNo), s.MahalNo, s.Ad, Ad(s.MahalTipi), Ad(s.KullanimSinifi),
                Tamsayi(s.Adet), Sayisal(s.AlanM2), Sayisal(s.HacimM3));
        }

        return sb.ToString();
    }

    public static string AlanTablosuCsv(AlanTablosu tablo)
    {
        ArgumentNullException.ThrowIfNull(tablo);
        var sb = new StringBuilder();
        CsvSatiri(sb, "Kullanım Sınıfı", "Mahal", "Adet", "Alan m²", "Hacim m³", "Oran");
        foreach (var k in tablo.KullanimKirilimi)
        {
            CsvSatiri(sb,
                Ad(k.KullanimSinifi), Tamsayi(k.MahalSayisi), Tamsayi(k.ToplamAdet),
                Sayisal(k.AlanM2), Sayisal(k.HacimM3), Yuzde(k.AlanOrani));
        }

        CsvSatiri(sb, "Net Alan", "", "", Sayisal(tablo.NetAlanM2), "", "");
        CsvSatiri(sb, "Sirkülasyon", "", "", Sayisal(tablo.SirkulasyonAlanM2), "", "");
        CsvSatiri(sb, "Teknik Alan", "", "", Sayisal(tablo.TeknikAlanM2), "", "");
        CsvSatiri(sb, "Brüt Alan", "", "", Sayisal(tablo.BrutAlanM2), "", "");
        CsvSatiri(sb, "Hedef Toplam Alan", "", "", Sayisal(tablo.HedefToplamAlanM2), "", "");
        CsvSatiri(sb, "Hedef Farkı", "", "", Sayisal(tablo.HedefFarkM2), "", "");
        return sb.ToString();
    }

    public static string KapiPencereCsv(KapiPencereCizelgesi cizelge)
    {
        ArgumentNullException.ThrowIfNull(cizelge);
        var sb = new StringBuilder();
        CsvSatiri(sb, "Poz", "Tip", "Genişlik cm", "Yükseklik cm", "Adet", "Yapı Elemanı Tipi", "Malzeme", "Toplam Alan m²");
        foreach (var s in cizelge.Satirlar)
        {
            CsvSatiri(sb,
                s.Poz, s.Tip,
                (s.GenislikM * 100).ToString("N0", Tr),
                (s.YukseklikM * 100).ToString("N0", Tr),
                Tamsayi(s.Adet), s.YapiElemaniTipiAdi, string.Join(", ", s.MalzemeAdlari),
                Sayisal(s.ToplamAlanM2));
        }

        return sb.ToString();
    }

    public static string FinisajCsv(FinisajCizelgesi cizelge)
    {
        ArgumentNullException.ThrowIfNull(cizelge);
        var sb = new StringBuilder();
        CsvSatiri(sb, "Kat", "Mahal No", "Mahal", "Zemin", "Duvar", "Tavan", "Islak Hacim", "Not");
        foreach (var s in cizelge.Satirlar)
        {
            CsvSatiri(sb,
                Tamsayi(s.KatNo), s.MahalNo, s.MahalAdi,
                s.ZeminTipiAdi, s.DuvarTipiAdi, s.TavanTipiAdi,
                s.IslakHacim ? "Evet" : "Hayır", s.Not);
        }

        return sb.ToString();
    }

    public static string OnKontrolCsv(OnKontrolRaporu rapor)
    {
        ArgumentNullException.ThrowIfNull(rapor);
        var sb = new StringBuilder();
        CsvSatiri(sb, "Seviye", "Kural Kodu", "Kaynak", "Mesaj", "Çözüm Önerisi", "Etkilenen", "Veri Eksiği");
        foreach (var s in rapor.Satirlar)
        {
            CsvSatiri(sb,
                Ad(s.Seviye), s.KuralKodu, s.KaynakKodu, s.Mesaj, s.CozumOnerisi, s.EtkilenenEleman, s.VeriEksigi);
        }

        return sb.ToString();
    }
}
