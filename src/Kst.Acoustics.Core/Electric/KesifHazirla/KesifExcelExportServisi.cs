using ClosedXML.Excel;

namespace Kst.Acoustics.Core.Electric.KesifHazirla;

/// <summary>M14 keşif projesini genel icmal, tablo özeti ve tablo ayrıntılarıyla Excel'e aktarır.</summary>
public static class KesifExcelExportServisi
{
    private const string ParaFormati = "#,##0.00 \"₺\"";
    private const string MiktarFormati = "#,##0.###";
    private const int BaslikSatiri = 4;
    private const int IlkVeriSatiri = 5;

    /// <summary>Excel'in oluşturduğu geçici ~$ kilit dosyasından çalışma kitabının açık olup olmadığını belirler.</summary>
    public static bool DosyaAcikMi(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName))
            return false;
        return File.Exists(Path.Combine(directory, $"~${fileName}"));
    }

    public static void RaporOlustur(KesifProje proje, string filePath)
    {
        ArgumentNullException.ThrowIfNull(proje);
        if (DosyaAcikMi(filePath))
            throw new IOException("Excel dosyası açık. Güncel toplamları görebilmek için dosyayı Excel'de kapatın veya farklı bir ad seçin.");
        if (proje.Tablolar.Count == 0)
            throw new InvalidOperationException("Excel çıktısı için en az bir keşif tablosu bulunmalıdır.");

        using var workbook = new XLWorkbook();
        var kullanilanAdlar = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Genel İcmal", "Tablo Özeti"
        };

        var genel = workbook.Worksheets.Add("Genel İcmal");
        var ozet = workbook.Worksheets.Add("Tablo Özeti");

        // Detay sayfaları yalnız satır taşıyan (yaprak) tablolar için üretilir; üst tablolar yalnız başlıktır.
        var satirliTablolar = KesifTabloAgaci.TumTablolar(proje).Where(t => t.Satirlar.Count > 0).ToList();
        var detaylar = satirliTablolar.Select((tablo, index) =>
        {
            var sayfaAdi = BenzersizSayfaAdi(tablo.Ad, index + 1, kullanilanAdlar);
            return new DetaySayfa(tablo, workbook.Worksheets.Add(sayfaAdi));
        }).ToList();
        var sayfaHaritasi = detaylar.ToDictionary(d => d.Tablo);

        GenelIcmalYaz(genel, proje);
        foreach (var detay in detaylar)
            detay.ToplamSatiri = TabloDetayiYaz(detay.Sayfa, proje, detay.Tablo);
        TabloOzetiYaz(ozet, proje, sayfaHaritasi);

        workbook.Properties.Title = $"{proje.Ad} - Keşif Raporu";
        workbook.Properties.Subject = "M14 Keşif Hazırla genel icmal ve tablo ayrıntıları";
        workbook.Properties.Author = "EYE4PRO Electric";
        workbook.CalculateMode = XLCalculateMode.Auto;
        workbook.SaveAs(filePath);
    }

    private static void GenelIcmalYaz(IXLWorksheet ws, KesifProje proje)
    {
        var satirlar = KesifHesapServisi.IcmalOlustur(proje);
        const int sutunSayisi = 9;
        SayfaBasligiYaz(ws, $"{proje.Ad} - Genel İcmal", KunyeAltBaslik(proje), sutunSayisi);

        var headers = new[]
        {
            "Sıra", "Poz No", "İmalat Adı", "Birim", "Toplam Miktar",
            "Ortalama Birim Fiyat", "Toplam Tutar", "Tablo Dağılımı", "Fiyat Uyarısı"
        };
        BasliklariYaz(ws, headers);

        var row = IlkVeriSatiri;
        for (var i = 0; i < satirlar.Count; i++, row++)
        {
            var satir = satirlar[i];
            ws.Cell(row, 1).Value = i + 1;
            ws.Cell(row, 2).Value = satir.PozNo;
            ws.Cell(row, 3).Value = satir.BirimAdi;
            ws.Cell(row, 4).Value = satir.Birim;
            ws.Cell(row, 5).Value = satir.ToplamMiktar;
            ws.Cell(row, 6).Value = satir.OrtalamaBirimFiyat;
            ws.Cell(row, 7).Value = satir.ToplamTutar;
            ws.Cell(row, 8).Value = satir.TabloOzeti;
            if (!string.IsNullOrWhiteSpace(satir.BirimFiyatFarkiMetni))
                ws.Cell(row, 9).Value = satir.BirimFiyatFarkiMetni;
        }

        var sonVeriSatiri = Math.Max(IlkVeriSatiri, row - 1);
        if (satirlar.Count > 0)
            ExcelTablosuOlustur(ws, BaslikSatiri, sonVeriSatiri, sutunSayisi, "GenelIcmalTablosu");

        var toplamSatiri = row;
        ws.Range(toplamSatiri, 1, toplamSatiri, 6).Merge();
        ws.Cell(toplamSatiri, 1).Value = "GENEL TOPLAM";
        ws.Cell(toplamSatiri, 7).FormulaA1 = satirlar.Count > 0
            ? $"=SUM(G{IlkVeriSatiri}:G{sonVeriSatiri})"
            : "=0";
        ToplamSatiriStili(ws.Range(toplamSatiri, 1, toplamSatiri, sutunSayisi));

        ws.Range(IlkVeriSatiri, 5, Math.Max(IlkVeriSatiri, toplamSatiri), 5).Style.NumberFormat.Format = MiktarFormati;
        ws.Range(IlkVeriSatiri, 6, Math.Max(IlkVeriSatiri, toplamSatiri), 7).Style.NumberFormat.Format = ParaFormati;
        ws.Range(IlkVeriSatiri, 8, Math.Max(IlkVeriSatiri, sonVeriSatiri), 9).Style.Alignment.WrapText = true;
        SonDuzen(ws, sutunSayisi, new Dictionary<int, double> { [2] = 14, [3] = 48, [8] = 38, [9] = 18 });
    }

    private static void TabloOzetiYaz(IXLWorksheet ws, KesifProje proje, IReadOnlyDictionary<KesifTablosu, DetaySayfa> sayfaHaritasi)
    {
        const int sutunSayisi = 5;
        SayfaBasligiYaz(ws, $"{proje.Ad} - Tablo Özeti", "Keşif tablo ağacı: alt tablolar girintili, üst tablolar alt toplamlarıyla", sutunSayisi);
        BasliklariYaz(ws, ["Sıra", "Tablo Adı", "Satır Sayısı", "Tablo Toplamı", "Not"]);

        // Üst seviye tablolar ön-sıralı; alt tablolar derinliğe göre girintili yazılır.
        var dugumler = new List<(KesifTablosu Tablo, int Derinlik)>();
        void Ekle(KesifTablosu tablo, int derinlik)
        {
            dugumler.Add((tablo, derinlik));
            foreach (var alt in tablo.AltTablolar)
                Ekle(alt, derinlik + 1);
        }
        foreach (var tablo in proje.Tablolar)
            Ekle(tablo, 0);

        var row = IlkVeriSatiri;
        for (var i = 0; i < dugumler.Count; i++, row++)
        {
            var (tablo, derinlik) = dugumler[i];
            ws.Cell(row, 1).Value = i + 1;
            ws.Cell(row, 2).Value = new string(' ', derinlik * 4) + tablo.Ad;
            // Yaprak tablo: kendi satır sayısı; üst (başlık) tablo: satırı yok.
            if (tablo.YaprakMi)
                ws.Cell(row, 3).Value = tablo.Satirlar.Count;
            // Yaprak tablonun toplamı detay sayfasına bağlanır; üst tablo özyinelemeli toplamı (hesaplanmış değer).
            if (tablo.YaprakMi && sayfaHaritasi.TryGetValue(tablo, out var detay))
                ws.Cell(row, 4).FormulaA1 = $"='{ExcelSayfaAdi(detay.Sayfa.Name)}'!G{detay.ToplamSatiri}";
            else
                ws.Cell(row, 4).Value = tablo.Toplam;
            if (tablo.AltTabloVarMi)
                ws.Cell(row, 2).Style.Font.Bold = true;
            if (!string.IsNullOrWhiteSpace(tablo.Not))
                ws.Cell(row, 5).Value = tablo.Not;
        }

        var sonVeriSatiri = Math.Max(IlkVeriSatiri, row - 1);
        if (dugumler.Count > 0)
            ExcelTablosuOlustur(ws, BaslikSatiri, sonVeriSatiri, sutunSayisi, "TabloOzetTablosu");

        // Genel toplam yalnız en üst seviye tabloların toplamıdır (girintili liste alt toplamları tekrarladığından SUM kullanılmaz).
        ws.Range(row, 1, row, 3).Merge();
        ws.Cell(row, 1).Value = "GENEL TOPLAM";
        ws.Cell(row, 4).Value = proje.GenelToplam;
        ToplamSatiriStili(ws.Range(row, 1, row, sutunSayisi));
        ws.Range(IlkVeriSatiri, 4, row, 4).Style.NumberFormat.Format = ParaFormati;
        ws.Range(IlkVeriSatiri, 5, Math.Max(IlkVeriSatiri, sonVeriSatiri), 5).Style.Alignment.WrapText = true;
        SonDuzen(ws, sutunSayisi, new Dictionary<int, double> { [2] = 40, [5] = 46 });
    }

    /// <summary>Genel İcmal alt başlığına proje künyesini (işveren/yüklenici/yer/tarih) ekler.</summary>
    private static string KunyeAltBaslik(KesifProje proje)
    {
        var b = proje.Bilgiler;
        var parcalar = new List<string>();
        if (!string.IsNullOrWhiteSpace(b.Isveren)) parcalar.Add($"İşveren: {b.Isveren}");
        if (!string.IsNullOrWhiteSpace(b.Yuklenici)) parcalar.Add($"Yüklenici: {b.Yuklenici}");
        if (!string.IsNullOrWhiteSpace(b.IlIlce)) parcalar.Add($"Yer: {b.IlIlce}");
        if (!string.IsNullOrWhiteSpace(b.AdaParsel)) parcalar.Add($"Ada/Parsel: {b.AdaParsel}");
        if (!string.IsNullOrWhiteSpace(b.SozlesmeNo)) parcalar.Add($"Sözleşme: {b.SozlesmeNo}");
        if (!string.IsNullOrWhiteSpace(b.Tarih)) parcalar.Add($"Tarih: {b.Tarih}");
        return parcalar.Count > 0
            ? string.Join("  •  ", parcalar)
            : "Tüm keşif tablolarındaki pozların konsolide toplamı";
    }

    private static int TabloDetayiYaz(IXLWorksheet ws, KesifProje proje, KesifTablosu tablo)
    {
        const int sutunSayisi = 9;
        SayfaBasligiYaz(ws, $"{proje.Ad} - {tablo.Ad}", "Keşif tablosu satır ayrıntıları", sutunSayisi);
        BasliklariYaz(ws,
        [
            "Sıra", "Poz No", "İmalat Adı", "Birim", "Miktar", "Birim Fiyat",
            "Tutar", "Kaynak", "Açıklama"
        ]);

        var row = IlkVeriSatiri;
        for (var i = 0; i < tablo.Satirlar.Count; i++, row++)
        {
            var satir = tablo.Satirlar[i];
            ws.Cell(row, 1).Value = i + 1;
            if (!string.IsNullOrWhiteSpace(satir.PozNo))
                ws.Cell(row, 2).Value = satir.PozNo;
            ws.Cell(row, 3).Value = satir.BirimAdi;
            ws.Cell(row, 4).Value = satir.Birim;
            ws.Cell(row, 5).Value = satir.Miktar;
            ws.Cell(row, 6).Value = satir.BirimFiyat;
            ws.Cell(row, 7).FormulaA1 = $"=E{row}*F{row}";
            ws.Cell(row, 8).Value = satir.KaynakTipi switch
            {
                KesifKaynakTipi.Otomatik => "Otomatik",
                KesifKaynakTipi.Karma => "Karma",
                _ => "Manuel"
            };
            if (!string.IsNullOrWhiteSpace(satir.Aciklama))
                ws.Cell(row, 9).Value = satir.Aciklama;
        }

        var sonVeriSatiri = Math.Max(IlkVeriSatiri, row - 1);
        if (tablo.Satirlar.Count > 0)
            ExcelTablosuOlustur(ws, BaslikSatiri, sonVeriSatiri, sutunSayisi, $"Detay_{ws.Position}");

        ws.Range(row, 1, row, 6).Merge();
        ws.Cell(row, 1).Value = "TABLO TOPLAMI";
        ws.Cell(row, 7).FormulaA1 = tablo.Satirlar.Count > 0
            ? $"=SUM(G{IlkVeriSatiri}:G{sonVeriSatiri})"
            : "=0";
        ToplamSatiriStili(ws.Range(row, 1, row, sutunSayisi));

        ws.Range(IlkVeriSatiri, 5, Math.Max(IlkVeriSatiri, row), 5).Style.NumberFormat.Format = MiktarFormati;
        ws.Range(IlkVeriSatiri, 6, Math.Max(IlkVeriSatiri, row), 7).Style.NumberFormat.Format = ParaFormati;
        ws.Range(IlkVeriSatiri, 9, Math.Max(IlkVeriSatiri, sonVeriSatiri), 9).Style.Alignment.WrapText = true;
        SonDuzen(ws, sutunSayisi, new Dictionary<int, double> { [2] = 14, [3] = 48, [9] = 42 });
        return row;
    }

    private static void SayfaBasligiYaz(IXLWorksheet ws, string baslik, string altBaslik, int sutunSayisi)
    {
        ws.ShowGridLines = false;
        ws.Range(1, 1, 1, sutunSayisi).Merge();
        ws.Cell(1, 1).Value = baslik;
        var baslikStili = ws.Range(1, 1, 1, sutunSayisi).Style;
        baslikStili.Fill.BackgroundColor = XLColor.FromHtml("#DCE9DF");
        baslikStili.Font.FontColor = XLColor.FromHtml("#173F2A");
        baslikStili.Font.Bold = true;
        baslikStili.Font.FontSize = 16;
        ws.Row(1).Height = 30;

        ws.Range(2, 1, 2, sutunSayisi).Merge();
        ws.Cell(2, 1).Value = $"{altBaslik} • Oluşturma: {DateTime.Now:dd.MM.yyyy HH:mm}";
        var altBaslikStili = ws.Range(2, 1, 2, sutunSayisi).Style;
        altBaslikStili.Fill.BackgroundColor = XLColor.FromHtml("#E8F0EA");
        altBaslikStili.Font.FontColor = XLColor.FromHtml("#254B34");
        altBaslikStili.Font.Italic = true;
        ws.Row(2).Height = 22;
    }

    private static void BasliklariYaz(IXLWorksheet ws, IReadOnlyList<string> headers)
    {
        for (var col = 0; col < headers.Count; col++)
            ws.Cell(BaslikSatiri, col + 1).Value = headers[col];

        var range = ws.Range(BaslikSatiri, 1, BaslikSatiri, headers.Count);
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#6F9E42");
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Font.Bold = true;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Row(BaslikSatiri).Height = 26;
    }

    private static void ExcelTablosuOlustur(IXLWorksheet ws, int ilkSatir, int sonSatir, int sonSutun, string ad)
    {
        var table = ws.Range(ilkSatir, 1, sonSatir, sonSutun).CreateTable(ad);
        table.Theme = XLTableTheme.None;
        table.ShowAutoFilter = true;
        table.ShowRowStripes = true;
    }

    private static void ToplamSatiriStili(IXLRange range)
    {
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#DCE9DF");
        range.Style.Font.FontColor = XLColor.FromHtml("#173F2A");
        range.Style.Font.Bold = true;
        range.Style.Border.TopBorder = XLBorderStyleValues.Medium;
        range.Style.Border.TopBorderColor = XLColor.FromHtml("#6F9E42");
    }

    private static void SonDuzen(IXLWorksheet ws, int sutunSayisi, IReadOnlyDictionary<int, double> sabitGenislikler)
    {
        ws.SheetView.FreezeRows(BaslikSatiri);
        ws.Columns(1, sutunSayisi).AdjustToContents(1, Math.Max(IlkVeriSatiri, ws.LastRowUsed()?.RowNumber() ?? IlkVeriSatiri));
        foreach (var (sutun, genislik) in sabitGenislikler)
            ws.Column(sutun).Width = genislik;
        for (var col = 1; col <= sutunSayisi; col++)
            ws.Column(col).Width = Math.Min(ws.Column(col).Width, 48);
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.PagesWide = 1;
        ws.PageSetup.PagesTall = 0;
        ws.PageSetup.Margins.SetLeft(0.25).SetRight(0.25).SetTop(0.4).SetBottom(0.4);
    }

    private static string BenzersizSayfaAdi(string? ad, int index, ISet<string> kullanilan)
    {
        var temiz = string.IsNullOrWhiteSpace(ad) ? $"Tablo {index}" : ad.Trim();
        foreach (var invalid in new[] { ':', '\\', '/', '?', '*', '[', ']' })
            temiz = temiz.Replace(invalid, '-');
        temiz = temiz.Length > 31 ? temiz[..31] : temiz;
        if (string.IsNullOrWhiteSpace(temiz)) temiz = $"Tablo {index}";

        var aday = temiz;
        var sira = 2;
        while (!kullanilan.Add(aday))
        {
            var ek = $" ({sira++})";
            aday = temiz[..Math.Min(temiz.Length, 31 - ek.Length)] + ek;
        }
        return aday;
    }

    private static string ExcelSayfaAdi(string ad) => ad.Replace("'", "''", StringComparison.Ordinal);

    private sealed class DetaySayfa(KesifTablosu tablo, IXLWorksheet sayfa)
    {
        public KesifTablosu Tablo { get; } = tablo;
        public IXLWorksheet Sayfa { get; } = sayfa;
        public int ToplamSatiri { get; set; }
    }
}
