using System;
using System.Collections.Generic;
using System.Linq;

namespace Kst.Acoustics.Core.RoomModes;

/// <summary>
/// Oda modlarını hesaplayan ana motor.
/// </summary>
public static class RoomModeCalculator
{
    /// <summary>
    /// Verilen giriş parametrelerine göre oda modlarını hesaplar.
    /// </summary>
    public static RoomModeResult Calculate(RoomModeInput input)
    {
        var result = new RoomModeResult();
        if (!ValidateInput(input, result))
            return result;

        double c = CalculateSpeedOfSound(input.Temperature);
        // Bound work before integer conversion/allocation. Large finite inputs can
        // otherwise overflow indices or keep the UI busy for billions of iterations.
        double nxLimit = Math.Ceiling(input.MaxFrequency * 2 * input.LengthX / c);
        double nyLimit = Math.Ceiling(input.MaxFrequency * 2 * input.LengthY / c);
        double nzLimit = Math.Ceiling(input.MaxFrequency * 2 * input.LengthZ / c);
        double candidates = (nxLimit + 1) * (nyLimit + 1) * (nzLimit + 1);
        if (!double.IsFinite(input.Volume) || input.Volume <= 0 || !double.IsFinite(candidates) ||
            c <= 0 || candidates > 2_000_000)
        {
            result.Warnings.Add("Hesap boyutu güvenli sınırı aşıyor; oda boyutlarını veya üst frekansı azaltın, sıcaklığı kontrol edin.");
            return result;
        }
        result.SpeedOfSound = c;

        var modes = new List<RoomMode>();

        // Üst sınır tayini: n = f * 2 * L / c
        // Her eksen için maksimum n değerini frekans sınırına göre hesapla
        int maxNx = (int)nxLimit;
        int maxNy = (int)nyLimit;
        int maxNz = (int)nzLimit;

        for (int nx = 0; nx <= maxNx; nx++)
        {
            for (int ny = 0; ny <= maxNy; ny++)
            {
                for (int nz = 0; nz <= maxNz; nz++)
                {
                    if (nx == 0 && ny == 0 && nz == 0) continue;

                    double freq = (c / 2) * Math.Sqrt(
                        Math.Pow(nx / input.LengthX, 2) +
                        Math.Pow(ny / input.LengthY, 2) +
                        Math.Pow(nz / input.LengthZ, 2)
                    );

                    if (freq <= input.MaxFrequency)
                    {
                        modes.Add(new RoomMode(
                            Math.Round(freq, 2),
                            nx,
                            ny,
                            nz,
                            DetermineModeType(nx, ny, nz)
                        ));
                    }
                }
            }
        }

        result.Modes = modes.OrderBy(m => m.Frequency).ToList();
        
        // Oda Oran Analizi
        RoomRatioAnalyzer.Analyze(input, result);

        var effectiveRt60 = input.RT60.HasValue && input.RT60.Value > 0
            ? input.RT60.Value
            : EstimateDefaultRT60(input.Volume);
        result.EffectiveRT60 = Math.Round(effectiveRt60, 2);
        result.IsRT60Estimated = !input.RT60.HasValue || input.RT60.Value <= 0;
        if (result.IsRT60Estimated)
            result.Warnings.Add("RT60 girilmedi; Schroeder frekansı hacme bağlı tahmini RT60 ile hesaplandı.");

        result.SchroederFrequency = Math.Round(2000 * Math.Sqrt(effectiveRt60 / input.Volume), 1);

        return result;
    }

    /// <summary>
    /// Sıcaklığa bağlı ses hızını hesaplar: c = 331.3 * sqrt(1 + T/273.15)
    /// </summary>
    public static double CalculateSpeedOfSound(double temperatureCelsius)
    {
        return Math.Round(331.3 * Math.Sqrt(1 + temperatureCelsius / 273.15), 2);
    }

    private static bool ValidateInput(RoomModeInput input, RoomModeResult result)
    {
        if (input.LengthX <= 0 || input.LengthY <= 0 || input.LengthZ <= 0 ||
            !double.IsFinite(input.LengthX) || !double.IsFinite(input.LengthY) || !double.IsFinite(input.LengthZ))
        {
            result.Warnings.Add("Oda boyutları pozitif ve sonlu değerler olmalıdır.");
            return false;
        }

        if (input.Temperature <= -273.15 || !double.IsFinite(input.Temperature))
        {
            result.Warnings.Add("Sıcaklık mutlak sıfırın üzerinde ve sonlu olmalıdır.");
            return false;
        }

        if (input.MaxFrequency <= 0 || input.MaxFrequency > 2000 || !double.IsFinite(input.MaxFrequency))
        {
            result.Warnings.Add("Maksimum frekans 0-2000 Hz aralığında olmalıdır.");
            return false;
        }

        if (input.RT60.HasValue && (!double.IsFinite(input.RT60.Value) || input.RT60.Value < 0))
        {
            result.Warnings.Add("RT60 değeri negatif olamaz.");
            return false;
        }

        return true;
    }

    private static double EstimateDefaultRT60(double volume)
    {
        if (volume < 40) return 0.35;
        if (volume < 120) return 0.45;
        if (volume < 300) return 0.60;
        return 0.80;
    }

    /// <summary>
    /// nx, ny, nz değerlerine göre modun tipini belirler.
    /// </summary>
    public static RoomModeType DetermineModeType(int nx, int ny, int nz)
    {
        int nonZeroCount = 0;
        if (nx > 0) nonZeroCount++;
        if (ny > 0) nonZeroCount++;
        if (nz > 0) nonZeroCount++;

        return nonZeroCount switch
        {
            1 => RoomModeType.Axial,
            2 => RoomModeType.Tangential,
            _ => RoomModeType.Oblique
        };
    }
}
