using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Kst.Acoustics.Core.Measurement.Data;

public class CalibrationPoint
{
    public double Frequency { get; set; }
    public double Gain { get; set; }
}

public class CalibrationProfile
{
    public string MicrophoneName { get; set; } = "Bilinmeyen Mikrofon";
    public List<CalibrationPoint> Points { get; set; } = new();

    public double GetCorrection(double frequency)
    {
        if (Points.Count == 0) return 0;
        if (frequency <= Points[0].Frequency) return Points[0].Gain;
        if (frequency >= Points.Last().Frequency) return Points.Last().Gain;

        // Lineer interpolasyon
        for (int i = 0; i < Points.Count - 1; i++)
        {
            if (frequency >= Points[i].Frequency && frequency <= Points[i+1].Frequency)
            {
                double t = (frequency - Points[i].Frequency) / (Points[i+1].Frequency - Points[i].Frequency);
                return Points[i].Gain + t * (Points[i+1].Gain - Points[i].Gain);
            }
        }
        return 0;
    }

    public static CalibrationProfile LoadFromFile(string filePath)
    {
        var profile = new CalibrationProfile
        {
            MicrophoneName = Path.GetFileNameWithoutExtension(filePath)
        };

        foreach (var line in File.ReadAllLines(filePath))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("*") || line.StartsWith("#")) continue;
            
            var parts = line.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 &&
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double f) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double g))
            {
                profile.Points.Add(new CalibrationPoint { Frequency = f, Gain = g });
            }
        }

        profile.Points = profile.Points.OrderBy(p => p.Frequency).ToList();
        return profile;
    }
}
