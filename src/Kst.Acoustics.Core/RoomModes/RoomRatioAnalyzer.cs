using System;

namespace Kst.Acoustics.Core.RoomModes;

/// <summary>
/// Oda oranlarını (L:W:H) analiz eden ve Bolt bölgesini kontrol eden sınıf.
/// </summary>
public static class RoomRatioAnalyzer
{
    /// <summary>
    /// Oda oranlarını analiz eder ve sonucu döner.
    /// </summary>
    public static void Analyze(RoomModeInput input, RoomModeResult result)
    {
        // Her zaman en küçük boyutu yükseklik (1.0) kabul edelim ki standart oranlara uysun.
        double[] dims = { input.LengthX, input.LengthY, input.LengthZ };
        Array.Sort(dims);
        
        double h = dims[0]; // En küçük = yükseklik
        double w = dims[1];
        double l = dims[2];
        
        double ratioW = w / h;
        double ratioL = l / h;

        // Bolt Bölgesi Kontrolü (Basitleştirilmiş Poligon Kontrolü)
        // Bolt Area: 
        // 1.1 < W/H < 1.5
        // 1.3 < L/H < 2.1
        // Ve bazı lineer sınırlar.
        
        bool isInBolt = IsInsideBoltArea(ratioW, ratioL);
        result.IsInBoltRegion = isInBolt;

        result.RatioAnalysis = $"Normalize Oranlar (1 : {ratioW:F2} : {ratioL:F2})\n";
        
        if (isInBolt)
        {
            result.RatioAnalysis += "✓ Oda oranları Bolt bölgesi içerisinde (Akustik olarak tercih edilir).";
        }
        else
        {
            result.RatioAnalysis += "⚠ Oda oranları Bolt bölgesi dışında. Mod çakışmaları yaşanabilir.";
        }
    }

    /// <summary>
    /// Bolt bölgesini kontrol eder (W/H, L/H).
    /// </summary>
    private static bool IsInsideBoltArea(double x, double y)
    {
        // Basitleştirilmiş Bolt Bölgesi (Kaba sınır):
        // 1.1 <= x <= 1.5
        // 1.3 <= y <= 2.1
        // l/h <= (w/h + 1) * 1.5 gibi kısıtlar eklenebilir.
        
        if (x < 1.1 || x > 1.6) return false;
        if (y < 1.3 || y > 2.4) return false;
        
        // Üst sınır eğimi
        if (y > (1.14 * x + 0.6)) return false;
        
        // Alt sınır eğimi
        if (y < (1.4 * x - 0.7)) return false;

        return true;
    }
}
