using System;
using System.Collections.Generic;

namespace Kst.Acoustics.Core.RoomModes.Diffusers;

public static class QrdDiffuserCalculator
{
    public static DiffuserDesignResult Calculate(DiffuserDesignInput input)
    {
        var result = new DiffuserDesignResult
        {
            Type = DiffuserType.Qrd1D,
            TargetFrequencyHz = input.TargetFrequencyHz,
            PrimeOrder = input.PrimeOrder,
            PanelHeightMm = input.PanelHeightMm
        };

        if (!ValidateInput(input, result))
            return result;

        var wavelengthMm = input.SoundSpeed * 1000 / input.TargetFrequencyHz;
        var wells = new List<DiffuserWell>();
        var maxDepth = 0.0;

        for (int n = 0; n < input.PrimeOrder; n++)
        {
            var residue = n * n % input.PrimeOrder;
            var depth = residue * wavelengthMm / (2 * input.PrimeOrder);
            depth = Math.Min(depth, input.MaxDepthLimitMm);
            depth = Math.Round(depth, 1);
            maxDepth = Math.Max(maxDepth, depth);
            wells.Add(new DiffuserWell(n + 1, residue, Math.Round(input.WellWidthMm, 1), depth));
        }

        result.Wells = wells;
        result.PanelWidthMm = Math.Round(input.WellWidthMm * input.PrimeOrder, 1);
        result.MaxDepthMm = Math.Round(maxDepth, 1);
        result.LowerFrequencyHz = Math.Round(input.LowerFrequencyHz ?? input.SoundSpeed * 1000 / (2 * Math.Max(maxDepth, 1)), 1);
        result.UpperFrequencyHz = Math.Round(input.UpperFrequencyHz ?? input.SoundSpeed * 1000 / (2 * input.WellWidthMm), 1);

        if (result.MaxDepthMm >= input.MaxDepthLimitMm - 0.05)
            result.Warnings.Add("Bazı kuyu derinlikleri maksimum derinlik sınırında kırpıldı; daha derin panel veya daha yüksek hedef frekans seçilebilir.");

        if (wells.Count > 1 && wells.Skip(1).All(w => Math.Abs(w.DepthMm - input.MaxDepthLimitMm) < 0.05))
            result.Warnings.Add("Hedef frekans bu panel için çok düşük olduğu için sıfır dışındaki tüm kuyular aynı maksimum derinliğe kırpıldı; gerçek QRD dizilimi oluşmuyor.");

        if (input.WellWidthMm > wavelengthMm / 2)
            result.Warnings.Add("Kuyu genişliği hedef dalga boyunun yarısından büyük; üst frekans difüzyonu zayıflayabilir.");

        DiffuserSuitabilityAnalyzer.Analyze(input, result);
        return result;
    }

    private static bool ValidateInput(DiffuserDesignInput input, DiffuserDesignResult result)
    {
        if (input.Type != DiffuserType.Qrd1D)
        {
            result.Warnings.Add("Bu hesaplayıcı yalnızca 1D QRD panel için kullanılabilir.");
            result.Suitability = DiffuserSuitability.NotRecommended;
            return false;
        }

        if (!double.IsFinite(input.TargetFrequencyHz) || input.TargetFrequencyHz <= 0)
        {
            result.Warnings.Add("Hedef frekans pozitif ve sonlu olmalıdır.");
            result.Suitability = DiffuserSuitability.NotRecommended;
            return false;
        }

        if (!IsSupportedPrime(input.PrimeOrder))
        {
            result.Warnings.Add("QRD sıra değeri desteklenen asal sayılardan biri olmalıdır: 7, 11, 13, 17.");
            result.Suitability = DiffuserSuitability.NotRecommended;
            return false;
        }

        if (!double.IsFinite(input.WellWidthMm) || input.WellWidthMm <= 0 ||
            !double.IsFinite(input.PanelHeightMm) || input.PanelHeightMm <= 0 ||
            !double.IsFinite(input.MaxDepthLimitMm) || input.MaxDepthLimitMm <= 0 ||
            !double.IsFinite(input.SoundSpeed) || input.SoundSpeed <= 0)
        {
            result.Warnings.Add("Panel ölçüleri ve ses hızı pozitif ve sonlu değerler olmalıdır.");
            result.Suitability = DiffuserSuitability.NotRecommended;
            return false;
        }

        return true;
    }

    public static bool IsSupportedPrime(int value)
    {
        return value is 7 or 11 or 13 or 17;
    }
}
