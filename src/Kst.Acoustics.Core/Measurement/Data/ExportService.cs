using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Kst.Acoustics.Core.Measurement.Data;

public static class ExportService
{
    public static async Task ExportToCsvAsync(MeasurementData data, string filePath)
    {
        if (data.FrequencyResponse == null) return;

        var sb = new StringBuilder();
        sb.AppendLine("Frequency (Hz),Magnitude (dB),Phase (rad)");

        var fr = data.FrequencyResponse;
        for (int i = 0; i < fr.Frequencies.Length; i++)
        {
            // Ondalık ayracı her zaman nokta olmalı (tr-TR kültüründe virgül CSV alanlarını bozar)
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{fr.Frequencies[i]:F2},{fr.Magnitudes[i]:F2},{fr.Phases[i]:F4}"));
        }

        await File.WriteAllTextAsync(filePath, sb.ToString());
    }

    public static async Task ExportParametersToCsvAsync(MeasurementData data, string filePath)
    {
        if (data.Parameters == null) return;

        var sb = new StringBuilder();
        sb.AppendLine("Parameter,63,125,250,500,1000,2000,4000,8000");

        foreach (var kvp in data.Parameters)
        {
            sb.Append(kvp.Key);
            foreach (var v in kvp.Value)
            {
                sb.Append(string.Create(CultureInfo.InvariantCulture, $",{v:F2}"));
            }
            sb.AppendLine();
        }

        await File.WriteAllTextAsync(filePath, sb.ToString());
    }
}
