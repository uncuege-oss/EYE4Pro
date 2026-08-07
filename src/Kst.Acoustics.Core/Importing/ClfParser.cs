using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.Core.Importing;

/// <summary>
/// CLF (Common Loudspeaker Format) - özellikle CF1 (text tabanlı) 
/// formatındaki hoparlör verilerini okur.
/// </summary>
public static class ClfParser
{
    /// <summary>
    /// Verilen dosya yolundaki CF1 dosyasını ayrıştırıp AcousticLoudspeaker döner.
    /// </summary>
    public static AcousticLoudspeaker ParseCf1(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        return ParseCf1Lines(lines, Path.GetFileNameWithoutExtension(filePath));
    }

    /// <summary>
    /// Text satırları halindeki CF1 verisini ayrıştırır.
    /// </summary>
    public static AcousticLoudspeaker ParseCf1Lines(string[] lines, string fallbackId)
    {
        var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        string manufacturer = "Unknown";
        string model = "Unknown";
        int resolution = 10; // CF1 varsayılan olarak genelde 10 derecedir.

        // Basit Ini Parser Mantığı
        string currentSection = "";
        
        // Frekans başına verileri geçici olarak tutalım. 
        // Gerçek formatta freq band indeksleri farklı olabilir, biz 8 oktav (63Hz - 8kHz) varsayacağız.
        var bandsData = new Dictionary<int, List<double>>();
        var sensitivities = new double[8];
        
        int currentBandIndex = -1;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("//"))
                continue;

            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                currentSection = line.Substring(1, line.Length - 2).Trim();
                
                // Örneğin [Band 1] gibi bölümleri yakala
                if (currentSection.StartsWith("Band", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = currentSection.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1 && int.TryParse(parts[1], out int bIdx))
                    {
                        currentBandIndex = bIdx;
                        if (!bandsData.ContainsKey(currentBandIndex))
                            bandsData[currentBandIndex] = new List<double>();
                    }
                }
                continue;
            }

            // Key=Value çiftleri
            var eqIndex = line.IndexOf('=');
            if (eqIndex > 0)
            {
                var key = line.Substring(0, eqIndex).Trim();
                var val = line.Substring(eqIndex + 1).Trim();
                
                if (currentSection.Equals("General", StringComparison.OrdinalIgnoreCase))
                {
                    if (key.Equals("Manufacturer", StringComparison.OrdinalIgnoreCase)) manufacturer = val;
                    else if (key.Equals("Model", StringComparison.OrdinalIgnoreCase)) model = val;
                    else if (key.Equals("Resolution", StringComparison.OrdinalIgnoreCase) && int.TryParse(val, out int r)) resolution = r;
                }
                else if (currentBandIndex >= 0 && key.Equals("Sensitivity", StringComparison.OrdinalIgnoreCase))
                {
                    if (double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out double sens))
                    {
                        // 1. band 63 Hz ise 8'e kadar map et.
                        if (currentBandIndex >= 1 && currentBandIndex <= 8)
                            sensitivities[currentBandIndex - 1] = sens;
                    }
                }
            }
            else if (currentBandIndex >= 0)
            {
                // Değer dizesi ise (Örn: -3.5, -4.0, -4.5 ...) virgül veya boşluk ile ayrılmış olabilir
                var parts = line.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var p in parts)
                {
                    if (double.TryParse(p, NumberStyles.Any, CultureInfo.InvariantCulture, out double v))
                    {
                        bandsData[currentBandIndex].Add(v);
                    }
                }
            }
        }

        // Balonları oluştur
        var balloons = new DirectivityBalloon[8];
        int rows = (180 / resolution) + 1; // 0, 10, ..., 180 = 19
        int cols = (360 / resolution);     // 0, 10, ..., 350 = 36

        for (int i = 0; i < 8; i++)
        {
            var dataArray = new double[rows, cols];
            int bandKey = i + 1; // CF1'de band index genelde 1'den başlar (63Hz)

            if (bandsData.TryGetValue(bandKey, out var flatData) && flatData.Count >= rows * cols)
            {
                int idx = 0;
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        dataArray[r, c] = flatData[idx++];
                    }
                }
            }
            // Veri yoksa veya eksikse varsayılan 0 (omnidirectional) kalır.
            
            balloons[i] = new DirectivityBalloon(resolution, dataArray);
        }

        string id = Guid.NewGuid().ToString("N");
        return new AcousticLoudspeaker(id, manufacturer, model, sensitivities, balloons);
    }
}
