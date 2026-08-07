using System.Collections.Generic;
using static Kst.Acoustics.Core.Architecture.Reports.MimariRaporMetin;

namespace Kst.Acoustics.Core.Architecture.Reports;

/// <summary>
/// M37 mimari rapor modelini cok sayfali bir .xlsx dosyasina cevirir (her tablo bir sayfa).
/// Olcum degerleri sayisal hucre, adlar metin olarak yazilir.
/// </summary>
public static class MimariRaporXlsxBuilder
{
    public static byte[] Olustur(MimariRaporu rapor)
    {
        ArgumentNullException.ThrowIfNull(rapor);

        var sayfalar = new List<XlsxSayfa>
        {
            new("Mahal Listesi", MahalListesiSatirlari(rapor.MahalListesi)),
            new("Alan Tablosu", AlanTablosuSatirlari(rapor.AlanTablosu)),
            new("Kapı-Pencere", KapiPencereSatirlari(rapor.KapiPencereCizelgesi)),
            new("Finisaj", FinisajSatirlari(rapor.FinisajCizelgesi)),
            new("Ön Kontrol", OnKontrolSatirlari(rapor.OnKontrol))
        };

        return XlsxYazici.Olustur(sayfalar);
    }

    private static List<IReadOnlyList<object?>> MahalListesiSatirlari(MahalListesiTablosu tablo)
    {
        var satirlar = new List<IReadOnlyList<object?>>
        {
            new object?[] { "Kat", "Mahal No", "Ad", "Tip", "Kullanım Sınıfı", "Adet", "Alan m²", "Hacim m³" }
        };

        foreach (var s in tablo.Satirlar)
            satirlar.Add(new object?[] { s.KatNo, s.MahalNo, s.Ad, Ad(s.MahalTipi), Ad(s.KullanimSinifi), s.Adet, s.AlanM2, s.HacimM3 });

        return satirlar;
    }

    private static List<IReadOnlyList<object?>> AlanTablosuSatirlari(AlanTablosu tablo)
    {
        var satirlar = new List<IReadOnlyList<object?>>
        {
            new object?[] { "Kullanım Sınıfı", "Mahal", "Adet", "Alan m²", "Hacim m³", "Oran %" }
        };

        foreach (var k in tablo.KullanimKirilimi)
            satirlar.Add(new object?[] { Ad(k.KullanimSinifi), k.MahalSayisi, k.ToplamAdet, k.AlanM2, k.HacimM3, k.AlanOrani * 100 });

        satirlar.Add(new object?[] { "", "", "", "", "", "" });
        satirlar.Add(new object?[] { "Net Alan m²", tablo.NetAlanM2, "", "", "", "" });
        satirlar.Add(new object?[] { "Sirkülasyon m²", tablo.SirkulasyonAlanM2, "", "", "", "" });
        satirlar.Add(new object?[] { "Teknik Alan m²", tablo.TeknikAlanM2, "", "", "", "" });
        satirlar.Add(new object?[] { "Brüt Alan m²", tablo.BrutAlanM2, "", "", "", "" });
        satirlar.Add(new object?[] { "Net Hacim m³", tablo.NetHacimM3, "", "", "", "" });
        satirlar.Add(new object?[] { "Hedef Toplam Alan m²", tablo.HedefToplamAlanM2, "", "", "", "" });
        satirlar.Add(new object?[] { "Hedef Farkı m²", tablo.HedefFarkM2, "", "", "", "" });
        return satirlar;
    }

    private static List<IReadOnlyList<object?>> KapiPencereSatirlari(KapiPencereCizelgesi cizelge)
    {
        var satirlar = new List<IReadOnlyList<object?>>
        {
            new object?[] { "Poz", "Tip", "Genişlik cm", "Yükseklik cm", "Adet", "Yapı Elemanı Tipi", "Malzeme", "Toplam Alan m²" }
        };

        foreach (var s in cizelge.Satirlar)
            satirlar.Add(new object?[] { s.Poz, s.Tip, s.GenislikM * 100, s.YukseklikM * 100, s.Adet, s.YapiElemaniTipiAdi, string.Join(", ", s.MalzemeAdlari), s.ToplamAlanM2 });

        return satirlar;
    }

    private static List<IReadOnlyList<object?>> FinisajSatirlari(FinisajCizelgesi cizelge)
    {
        var satirlar = new List<IReadOnlyList<object?>>
        {
            new object?[] { "Kat", "Mahal No", "Mahal", "Zemin", "Duvar", "Tavan", "Islak Hacim", "Not" }
        };

        foreach (var s in cizelge.Satirlar)
            satirlar.Add(new object?[] { s.KatNo, s.MahalNo, s.MahalAdi, s.ZeminTipiAdi, s.DuvarTipiAdi, s.TavanTipiAdi, s.IslakHacim ? "Evet" : "Hayır", s.Not });

        return satirlar;
    }

    private static List<IReadOnlyList<object?>> OnKontrolSatirlari(OnKontrolRaporu rapor)
    {
        var satirlar = new List<IReadOnlyList<object?>>
        {
            new object?[] { "Seviye", "Kural Kodu", "Kaynak", "Mesaj", "Çözüm Önerisi", "Etkilenen", "Veri Eksiği" }
        };

        foreach (var s in rapor.Satirlar)
            satirlar.Add(new object?[] { Ad(s.Seviye), s.KuralKodu, s.KaynakKodu, s.Mesaj, s.CozumOnerisi, s.EtkilenenEleman, s.VeriEksigi });

        return satirlar;
    }
}
