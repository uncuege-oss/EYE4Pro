using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;

namespace Kst.Acoustics.Core.Architecture.Reports;

/// <summary>
/// Tek bir XLSX sayfasi: ad ve satirlar. Hucre degeri int/long/double ise sayi, aksi halde metin yazilir.
/// </summary>
public sealed record XlsxSayfa(string Ad, IReadOnlyList<IReadOnlyList<object?>> Satirlar);

/// <summary>
/// Bagimlilik olmadan, System.IO.Compression ile minimal OOXML (.xlsx) uretir.
/// Sayilar sayisal hucre, metinler inlineStr olarak yazilir; stil tablosu kullanilmaz.
/// </summary>
internal static class XlsxYazici
{
    public static byte[] Olustur(IReadOnlyList<XlsxSayfa> sayfalar)
    {
        var temizSayfalar = TemizSayfaAdlari(sayfalar);
        using var bellek = new MemoryStream();
        using (var arsiv = new ZipArchive(bellek, ZipArchiveMode.Create, leaveOpen: true))
        {
            Yaz(arsiv, "[Content_Types].xml", ContentTypes(temizSayfalar.Count));
            Yaz(arsiv, "_rels/.rels", KokRels());
            Yaz(arsiv, "xl/workbook.xml", Workbook(temizSayfalar));
            Yaz(arsiv, "xl/_rels/workbook.xml.rels", WorkbookRels(temizSayfalar.Count));

            for (var i = 0; i < temizSayfalar.Count; i++)
                Yaz(arsiv, $"xl/worksheets/sheet{i + 1}.xml", Worksheet(temizSayfalar[i]));
        }

        return bellek.ToArray();
    }

    private static void Yaz(ZipArchive arsiv, string yol, string icerik)
    {
        var giris = arsiv.CreateEntry(yol, CompressionLevel.Optimal);
        using var akis = new StreamWriter(giris.Open(), new UTF8Encoding(false));
        akis.Write(icerik);
    }

    private static List<XlsxSayfa> TemizSayfaAdlari(IReadOnlyList<XlsxSayfa> sayfalar)
    {
        var sonuc = new List<XlsxSayfa>();
        var kullanilan = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        var sira = 0;
        foreach (var sayfa in sayfalar)
        {
            sira++;
            var ad = SayfaAdiTemizle(sayfa.Ad, sira);
            var taban = ad;
            var ek = 1;
            while (!kullanilan.Add(ad))
                ad = $"{Kisalt(taban, 28)}-{++ek}";

            sonuc.Add(sayfa with { Ad = ad });
        }

        return sonuc;
    }

    private static string SayfaAdiTemizle(string? ad, int sira)
    {
        if (string.IsNullOrWhiteSpace(ad))
            return $"Sayfa{sira}";

        var temiz = new string(ad.Where(c => c is not (':' or '\\' or '/' or '?' or '*' or '[' or ']')).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(temiz))
            return $"Sayfa{sira}";

        return Kisalt(temiz, 31);
    }

    private static string Kisalt(string deger, int uzunluk)
        => deger.Length <= uzunluk ? deger : deger[..uzunluk];

    private static string ContentTypes(int sayfaSayisi)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
        sb.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
        sb.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
        sb.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
        for (var i = 0; i < sayfaSayisi; i++)
            sb.Append($"<Override PartName=\"/xl/worksheets/sheet{i + 1}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
        sb.Append("</Types>");
        return sb.ToString();
    }

    private static string KokRels()
        => "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
         + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
         + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>"
         + "</Relationships>";

    private static string Workbook(IReadOnlyList<XlsxSayfa> sayfalar)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" ");
        sb.Append("xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>");
        for (var i = 0; i < sayfalar.Count; i++)
            sb.Append($"<sheet name=\"{XmlOznitelik(sayfalar[i].Ad)}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>");
        sb.Append("</sheets></workbook>");
        return sb.ToString();
    }

    private static string WorkbookRels(int sayfaSayisi)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
        for (var i = 0; i < sayfaSayisi; i++)
            sb.Append($"<Relationship Id=\"rId{i + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i + 1}.xml\"/>");
        sb.Append("</Relationships>");
        return sb.ToString();
    }

    private static string Worksheet(XlsxSayfa sayfa)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");

        for (var r = 0; r < sayfa.Satirlar.Count; r++)
        {
            var satir = sayfa.Satirlar[r];
            sb.Append($"<row r=\"{r + 1}\">");
            for (var c = 0; c < satir.Count; c++)
                sb.Append(Hucre(satir[c], $"{Kolon(c)}{r + 1}"));
            sb.Append("</row>");
        }

        sb.Append("</sheetData></worksheet>");
        return sb.ToString();
    }

    private static string Hucre(object? deger, string referans)
    {
        if (deger is int or long or double or float or decimal)
        {
            var sayi = System.Convert.ToDouble(deger, CultureInfo.InvariantCulture)
                .ToString(CultureInfo.InvariantCulture);
            return $"<c r=\"{referans}\"><v>{sayi}</v></c>";
        }

        var metin = deger?.ToString() ?? "";
        return $"<c r=\"{referans}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{XmlMetin(metin)}</t></is></c>";
    }

    private static string Kolon(int index)
    {
        var s = "";
        index++;
        while (index > 0)
        {
            var kalan = (index - 1) % 26;
            s = (char)('A' + kalan) + s;
            index = (index - 1) / 26;
        }

        return s;
    }

    private static string XmlMetin(string deger) => SecurityElement.Escape(deger) ?? "";

    private static string XmlOznitelik(string deger) => SecurityElement.Escape(deger) ?? "";
}
