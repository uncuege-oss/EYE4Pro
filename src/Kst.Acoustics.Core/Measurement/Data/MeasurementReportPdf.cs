using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Kst.Acoustics.Core.Measurement.Comparison;

namespace Kst.Acoustics.Core.Measurement.Data;

/// <summary>
/// Harici PDF kütüphanesi kullanmadan çok sayfalı ölçüm raporu üretir.
/// </summary>
public static class MeasurementReportPdf
{
    public static byte[] Build(MeasurementSession session, ComparisonReport? comparison = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        // Sayısal biçimlendirme kültürden bağımsız olmalı (tr-TR'de ondalık virgül,
        // sabit genişlikli tablo hizalamasını ve rapor tutarlılığını bozardı).
        var previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            var lines = BuildLines(session, comparison);
            return CreateSimplePdf(lines.Select(Sanitize).ToList());
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    private static List<string> BuildLines(MeasurementSession session, ComparisonReport? cmp)
    {
        var lines = new List<string>
        {
            "EYE4PRO ACOUSTICS - YERINDE OLCUM RAPORU",
            $"Olusturma: {DateTime.Now:yyyy-MM-dd HH:mm}",
            $"Oturum: {session.Name}  (Olusturulma: {session.CreatedAt:yyyy-MM-dd HH:mm})",
            "",
            "DONANIM",
            $"Giris: {session.InputDeviceName ?? "-"}",
            $"Cikis: {session.OutputDeviceName ?? "-"}",
            $"Mikrofon kalibrasyon: {session.MicCalibrationFilePath ?? "-"}",
            $"Fs: {session.SampleRate} Hz   Bit: {session.BitDepth}",
            "",
            "ODA",
            $"Boyut: {session.RoomDimensions.L:F2} x {session.RoomDimensions.W:F2} x {session.RoomDimensions.H:F2} m" +
                $"   Hacim: {(session.RoomDimensions.L * session.RoomDimensions.W * session.RoomDimensions.H):F1} m3",
            $"DXF: {session.DxfFilePath ?? "-"}",
        };

        if (session.SetupPoints.Count > 0)
        {
            lines.Add("");
            lines.Add("KURULUM NOKTALARI");
            foreach (var p in session.SetupPoints.Take(6))
                lines.Add($"  {p.Type,-12} {p.Label,-14} X={p.X:F2}  Y={p.Y:F2}  Z={p.Z:F2}");
        }

        lines.Add("");
        lines.Add($"OLCUM SAYISI: {session.Measurements.Count}");

        foreach (var meas in session.Measurements)
        {
            lines.Add("");
            lines.Add($"--- {meas.Label}  ({meas.Timestamp:yyyy-MM-dd HH:mm}) ---");
            lines.Add($"Sinyal: {meas.SignalType}  Sure: {meas.SignalDurationMs:F0} ms");

            if (meas.Parameters != null)
            {
                int[] bands = { 63, 125, 250, 500, 1000, 2000, 4000, 8000 };
                string Hdr() => "                " + string.Join(" ", bands.Select(b => b >= 1000 ? $"{b / 1000,5}k" : $"{b,6}"));
                lines.Add(Hdr());
                foreach (var key in new[] { "RT30", "EDT", "C50", "C80", "D50", "Ts" })
                {
                    if (!meas.Parameters.TryGetValue(key, out var arr)) continue;
                    var row = string.Join(" ", arr.Take(bands.Length).Select(v => double.IsNaN(v) ? "    --" : $"{v,6:F2}"));
                    lines.Add($"  {key,-12} {row}");
                }
                if (meas.Parameters.TryGetValue("STI", out var stiArr) && stiArr.Length > 0 && !double.IsNaN(stiArr[0]))
                    lines.Add($"  STI (genel): {stiArr[0]:F2}");
            }

            if (meas.Distortion is { } thd)
                lines.Add($"  THD: %{thd.THD:F2}");
            if (meas.Intermodulation is { } imd)
                lines.Add($"  IMD: %{imd.ImdPercent:F2}   Gurultu tabani: {imd.NoiseFloorDbr:F1} dBr");
        }

        if (cmp != null)
        {
            lines.Add("");
            lines.Add($"M01 KARSILASTIRMASI: {cmp.Simulation.ProjectName}  ({cmp.MeasurementLabel})");
            int[] bands = { 63, 125, 250, 500, 1000, 2000, 4000, 8000 };
            foreach (var key in new[] { "RT30", "EDT", "C50", "C80" })
            {
                var row = cmp.Rows.Where(r => r.Parameter == key).ToList();
                if (row.Count == 0) continue;
                var vals = string.Join(" ", bands.Select(b =>
                {
                    var r = row.FirstOrDefault(x => x.FrequencyHz == b);
                    if (r == null || double.IsNaN(r.Delta)) return "    --";
                    return $"{r.Delta,+6:F2}";
                }));
                lines.Add($"  delta {key,-6} {vals}");
            }
        }

        return lines;
    }

    private static string Sanitize(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            sb.Append(ch switch
            {
                'ç' or 'Ç' => 'c',
                'ğ' or 'Ğ' => 'g',
                'ı' or 'İ' => 'i',
                'ö' or 'Ö' => 'o',
                'ş' or 'Ş' => 's',
                'ü' or 'Ü' => 'u',
                'Δ' => 'd',
                >= ' ' and <= '~' => ch,
                _ => ' '
            });
        }
        return sb.ToString();
    }

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

    // (800-40)/12 satır sığar; taşan içerik eskiden sayfa dışına sessizce yazılıyordu
    private const int LinesPerPage = 60;

    private static byte[] CreateSimplePdf(IReadOnlyList<string> lines)
    {
        var pages = lines.Chunk(LinesPerPage).ToList();
        if (pages.Count == 0) pages.Add([]);

        // Nesne numaraları: 1=Catalog, 2=Pages, ardından her sayfa için (Page, Contents) çifti, son=Font
        int fontObjNum = 2 + pages.Count * 2 + 1;
        var kids = string.Join(" ", Enumerable.Range(0, pages.Count).Select(i => $"{3 + i * 2} 0 R"));

        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{kids}] /Count {pages.Count} >>",
        };

        foreach (var page in pages)
        {
            int pageObjNum = objects.Count + 1;
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] " +
                $"/Resources << /Font << /F1 {fontObjNum} 0 R >> >> /Contents {pageObjNum + 1} 0 R >>");

            var content = new StringBuilder();
            content.AppendLine("BT");
            content.AppendLine("/F1 9 Tf");
            content.AppendLine("40 800 Td");
            foreach (var line in page)
            {
                content.Append('(').Append(Escape(line)).AppendLine(") Tj");
                content.AppendLine("0 -12 Td");
            }
            content.AppendLine("ET");
            objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(content.ToString())} >>\nstream\n{content}endstream");
        }

        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>");

        var pdf = new StringBuilder();
        var offsets = new List<int> { 0 };
        pdf.AppendLine("%PDF-1.4");
        for (int i = 0; i < objects.Count; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.AppendLine($"{i + 1} 0 obj");
            pdf.AppendLine(objects[i]);
            pdf.AppendLine("endobj");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.AppendLine("xref");
        pdf.AppendLine($"0 {objects.Count + 1}");
        pdf.AppendLine("0000000000 65535 f ");
        for (int i = 1; i < offsets.Count; i++)
            pdf.AppendLine($"{offsets[i]:D10} 00000 n ");

        pdf.AppendLine("trailer");
        pdf.AppendLine($"<< /Size {objects.Count + 1} /Root 1 0 R >>");
        pdf.AppendLine("startxref");
        pdf.AppendLine(xrefOffset.ToString());
        pdf.AppendLine("%%EOF");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
