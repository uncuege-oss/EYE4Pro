using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.BuildingAcoustics.Database;

public static class YapiElemaniExcelServisi
{
    private static readonly string[] Headers =
    [
        "Ad", "Tip", "Rw", "C", "Ctr", "Lnw", "DeltaLw", "FlankingDuzeltmesiDb",
        "BaglantiTipi", "YuzeyKutlesiKgM2",
        "R63", "R125", "R250", "R500", "R1k", "R2k", "R4k", "R8k",
        "DinamikRijitlikMNm3",
        "L63", "L125", "L250", "L500", "L1k", "L2k", "L4k",
        "HasRealOktavData",
        "R100_3", "R125_3", "R160_3", "R200_3", "R250_3", "R315_3", "R400_3", "R500_3",
        "R630_3", "R800_3", "R1000_3", "R1250_3", "R1600_3", "R2000_3", "R2500_3", "R3150_3",
        "Hafiflik",
        "YapisalRtSn",
        "Id", "Uretici", "UrunKodu", "Kategori", "PozNo", "MetrajBirimi",
        "BirimFiyat", "ParaBirimi", "FireYuzdesi", "BirimFiyatTarihi",
        "SpektrumKaynagi", "DarbeSpektrumKaynagi"
    ];

    public static void ExportToExcel(string filePath, IReadOnlyList<YapiElemani> elemanlar)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(elemanlar);
        string tamYol = Path.GetFullPath(filePath);
        string klasor = Path.GetDirectoryName(tamYol)
                         ?? throw new IOException($"Excel hedef klasörü belirlenemedi: {filePath}");
        Directory.CreateDirectory(klasor);
        string gecici = Path.Combine(klasor, $".{Path.GetFileNameWithoutExtension(tamYol)}.{Guid.NewGuid():N}.tmp.xlsx");
        try
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("YapiElemanlari");
            WriteHeader(ws);

            for (int i = 0; i < elemanlar.Count; i++)
                WriteElement(ws, i + 2, elemanlar[i]);

            ws.Columns().AdjustToContents();
            workbook.SaveAs(gecici);
            File.Move(gecici, tamYol, overwrite: true);
        }
        finally
        {
            if (File.Exists(gecici))
                File.Delete(gecici);
        }
    }

    public static List<YapiElemani> ImportFromExcel(string filePath)
    {
        var result = new List<YapiElemani>();
        using var workbook = new XLWorkbook(filePath);
        var ws = workbook.Worksheet(1);
        var range = ws.RangeUsed();
        if (range == null)
            return result;

        foreach (var row in range.RowsUsed().Skip(1))
        {
            var ad = row.Cell(1).GetString().Trim();
            if (string.IsNullOrWhiteSpace(ad))
                continue;

            var tipText = row.Cell(2).GetString().Trim();
            if (!Enum.TryParse<YapiElemaniTipi>(tipText, true, out var tip))
                tip = YapiElemaniTipi.Duvar;

            var baglantiText = row.Cell(9).GetString().Trim();
            if (!Enum.TryParse<BaglantiTipi>(baglantiText, true, out var baglanti))
                baglanti = BaglantiTipi.Rijit;

            var yuzeyKutlesi = GetDouble(row.Cell(10), 0);
            var eleman = new YapiElemani
            {
                Ad = ad,
                Tip = tip,
                Rw = GetDouble(row.Cell(3), 0),
                C = GetDouble(row.Cell(4), 0),
                Ctr = GetDouble(row.Cell(5), 0),
                Lnw = GetDouble(row.Cell(6), 0),
                DeltaLw = GetDouble(row.Cell(7), 0),
                FlankingDuzeltmesiDb = GetDouble(row.Cell(8), 0),
                BaglantiTipi = baglanti,
                R63 = GetDouble(row.Cell(11), 0),
                R125 = GetDouble(row.Cell(12), 0),
                R250 = GetDouble(row.Cell(13), 0),
                R500 = GetDouble(row.Cell(14), 0),
                R1k = GetDouble(row.Cell(15), 0),
                R2k = GetDouble(row.Cell(16), 0),
                R4k = GetDouble(row.Cell(17), 0),
                R8k = GetDouble(row.Cell(18), 0),
                DinamikRijitlikMNm3 = GetDouble(row.Cell(19), 0),
                L63  = GetDouble(row.Cell(20), 0),
                L125 = GetDouble(row.Cell(21), 0),
                L250 = GetDouble(row.Cell(22), 0),
                L500 = GetDouble(row.Cell(23), 0),
                L1k  = GetDouble(row.Cell(24), 0),
                L2k  = GetDouble(row.Cell(25), 0),
                L4k  = GetDouble(row.Cell(26), 0),
                HasRealOktavData = GetBool(row.Cell(27)),
            };
            eleman.RThirdOctave = ReadThirdOctave(row, 28);

            // Hafiflik kolonu (44): boş ise Homogen
            var hafiflikText = row.Cell(44).GetString().Trim();
            if (!string.IsNullOrWhiteSpace(hafiflikText) &&
                Enum.TryParse<YapiElemaniHafifligi>(hafiflikText, true, out var hafiflik))
                eleman.Hafiflik = hafiflik;
            // YapisalRtSn kolonu (45): boş ise 0 (varsayılan 0.5s davranışı korunur)
            eleman.YapisalRtSn = GetDouble(row.Cell(45), 0);

            if (Guid.TryParse(row.Cell(46).GetString().Trim(), out var id))
                eleman.Id = id;
            eleman.Uretici = row.Cell(47).GetString().Trim();
            eleman.UrunKodu = row.Cell(48).GetString().Trim();
            eleman.Kategori = row.Cell(49).GetString().Trim();
            eleman.PozNo = row.Cell(50).GetString().Trim();
            if (Enum.TryParse<AkustikMetrajBirimi>(row.Cell(51).GetString().Trim(), true, out var metrajBirimi))
                eleman.MetrajBirimi = metrajBirimi;
            eleman.BirimFiyat = GetDecimal(row.Cell(52), 0m);
            eleman.ParaBirimi = string.IsNullOrWhiteSpace(row.Cell(53).GetString())
                ? "TRY"
                : row.Cell(53).GetString().Trim().ToUpperInvariant();
            eleman.FireYuzdesi = GetDecimal(row.Cell(54), 0m);
            if (row.Cell(55).TryGetValue<DateTime>(out var fiyatTarihi))
                eleman.BirimFiyatTarihi = fiyatTarihi;
            else if (DateTime.TryParse(row.Cell(55).GetString(), CultureInfo.CurrentCulture,
                         DateTimeStyles.AssumeLocal, out fiyatTarihi))
                eleman.BirimFiyatTarihi = fiyatTarihi;
            if (Enum.TryParse<SpektrumVeriKaynagi>(row.Cell(56).GetString().Trim(), true, out var spektrumKaynagi))
                eleman.SpektrumKaynagi = spektrumKaynagi;
            if (Enum.TryParse<SpektrumVeriKaynagi>(row.Cell(57).GetString().Trim(), true, out var darbeKaynagi))
                eleman.DarbeSpektrumKaynagi = darbeKaynagi;

            if (yuzeyKutlesi > 0)
            {
                eleman.Katmanlar.Add(new YapiKatmani
                {
                    MalzemeAdi = ad,
                    Kalinlik = 100,
                    Yogunluk = yuzeyKutlesi * 10
                });
            }

            YapiElemaniKutuphanesi.TamamlaOktavVerisi(eleman);
            result.Add(eleman);
        }

        return result;
    }

    public static void GenerateTemplate(string filePath)
    {
        ExportToExcel(filePath,
        [
            new YapiElemani
            {
                Ad = "Ornek Betonarme Duvar",
                Tip = YapiElemaniTipi.Duvar,
                Rw = 55,
                C = -1,
                Ctr = -4,
                FlankingDuzeltmesiDb = 2,
                BaglantiTipi = BaglantiTipi.Rijit,
                R63 = 38,
                R125 = 44,
                R250 = 50,
                R500 = 55,
                R1k = 58,
                R2k = 60,
                R4k = 61,
                R8k = 61,
                RThirdOctave = [43, 45, 47, 49, 51, 53, 54, 55, 56, 57, 58, 59, 60, 60, 61, 61],
                Katmanlar =
                [
                    new YapiKatmani { MalzemeAdi = "Betonarme", Kalinlik = 200, Yogunluk = 2400 }
                ]
            },
            new YapiElemani
            {
                Ad = "Ornek Yuzer Doseme",
                Tip = YapiElemaniTipi.Doseme,
                Rw = 58,
                Lnw = 80,
                DeltaLw = 28,
                BaglantiTipi = BaglantiTipi.Rijit,
                R63 = 42,
                R125 = 48,
                R250 = 54,
                R500 = 58,
                R1k = 60,
                R2k = 62,
                R4k = 63,
                R8k = 63,
                RThirdOctave = [47, 49, 51, 53, 55, 56, 57, 58, 59, 60, 60, 61, 62, 62, 63, 63],
                // Ln(f) — çıplak beton döşeme laboratuvar değerleri (DeltaLw uygulanmadan önce)
                L63  = 84,
                L125 = 83,
                L250 = 82,
                L500 = 80,
                L1k  = 79,
                L2k  = 75,
                L4k  = 72,
                HasRealOktavData = true,
                Katmanlar =
                [
                    new YapiKatmani { MalzemeAdi = "Doseme", Kalinlik = 220, Yogunluk = 2200 }
                ]
            }
        ]);
    }

    private static void WriteHeader(IXLWorksheet ws)
    {
        for (int i = 0; i < Headers.Length; i++)
            ws.Cell(1, i + 1).Value = Headers[i];

        var range = ws.Range(1, 1, 1, Headers.Length);
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.LightBlue;
    }

    private static void WriteElement(IXLWorksheet ws, int row, YapiElemani eleman)
    {
        YapiElemaniKutuphanesi.TamamlaOktavVerisi(eleman);
        ws.Cell(row, 1).Value = eleman.Ad;
        ws.Cell(row, 2).Value = eleman.Tip.ToString();
        ws.Cell(row, 3).Value = eleman.Rw;
        ws.Cell(row, 4).Value = eleman.C;
        ws.Cell(row, 5).Value = eleman.Ctr;
        ws.Cell(row, 6).Value = eleman.Lnw;
        ws.Cell(row, 7).Value = eleman.DeltaLw;
        ws.Cell(row, 8).Value = eleman.FlankingDuzeltmesiDb;
        ws.Cell(row, 9).Value = eleman.BaglantiTipi.ToString();
        ws.Cell(row, 10).Value = eleman.ToplamYuzeyKutlesi;
        ws.Cell(row, 11).Value = eleman.R63;
        ws.Cell(row, 12).Value = eleman.R125;
        ws.Cell(row, 13).Value = eleman.R250;
        ws.Cell(row, 14).Value = eleman.R500;
        ws.Cell(row, 15).Value = eleman.R1k;
        ws.Cell(row, 16).Value = eleman.R2k;
        ws.Cell(row, 17).Value = eleman.R4k;
        ws.Cell(row, 18).Value = eleman.R8k;
        ws.Cell(row, 19).Value = eleman.DinamikRijitlikMNm3;
        ws.Cell(row, 20).Value = eleman.L63;
        ws.Cell(row, 21).Value = eleman.L125;
        ws.Cell(row, 22).Value = eleman.L250;
        ws.Cell(row, 23).Value = eleman.L500;
        ws.Cell(row, 24).Value = eleman.L1k;
        ws.Cell(row, 25).Value = eleman.L2k;
        ws.Cell(row, 26).Value = eleman.L4k;
        ws.Cell(row, 27).Value = eleman.HasRealOktavData;
        for (int i = 0; i < 16; i++)
            ws.Cell(row, 28 + i).Value = eleman.RThirdOctave.Count > i ? eleman.RThirdOctave[i] : "";
        ws.Cell(row, 44).Value = eleman.Hafiflik.ToString();
        ws.Cell(row, 45).Value = eleman.YapisalRtSn;
        ws.Cell(row, 46).Value = eleman.Id.ToString("D");
        ws.Cell(row, 47).Value = eleman.Uretici;
        ws.Cell(row, 48).Value = eleman.UrunKodu;
        ws.Cell(row, 49).Value = eleman.Kategori;
        ws.Cell(row, 50).Value = eleman.PozNo;
        ws.Cell(row, 51).Value = eleman.MetrajBirimi.ToString();
        ws.Cell(row, 52).Value = eleman.BirimFiyat;
        ws.Cell(row, 53).Value = eleman.ParaBirimi;
        ws.Cell(row, 54).Value = eleman.FireYuzdesi;
        if (eleman.BirimFiyatTarihi is { } fiyatTarihi)
            ws.Cell(row, 55).Value = fiyatTarihi;
        ws.Cell(row, 56).Value = eleman.SpektrumKaynagi.ToString();
        ws.Cell(row, 57).Value = eleman.DarbeSpektrumKaynagi.ToString();
    }

    private static double GetDouble(IXLCell cell, double fallback)
    {
        if (cell.TryGetValue<double>(out var value))
            return value;
        return double.TryParse(cell.GetString(), out value) ? value : fallback;
    }

    private static bool GetBool(IXLCell cell)
    {
        if (cell.TryGetValue<bool>(out var b))
            return b;
        var s = cell.GetString().Trim().ToLowerInvariant();
        return s is "true" or "evet" or "1" or "yes";
    }

    private static decimal GetDecimal(IXLCell cell, decimal fallback)
    {
        if (cell.TryGetValue<decimal>(out var value))
            return value;
        var metin = cell.GetString();
        return decimal.TryParse(metin, NumberStyles.Number, CultureInfo.CurrentCulture, out value) ||
               decimal.TryParse(metin, NumberStyles.Number, CultureInfo.InvariantCulture, out value)
            ? value
            : fallback;
    }

    private static List<double> ReadThirdOctave(IXLRangeRow row, int startColumn)
    {
        var values = new List<double>(16);
        for (int i = 0; i < 16; i++)
        {
            var value = GetDouble(row.Cell(startColumn + i), 0);
            values.Add(value);
        }

        return values.Any(v => v > 0) ? values : new List<double>();
    }
}
