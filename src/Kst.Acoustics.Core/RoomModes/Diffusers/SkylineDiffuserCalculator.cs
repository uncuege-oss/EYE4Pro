using System;
using System.Collections.Generic;
using System.Linq;

namespace Kst.Acoustics.Core.RoomModes.Diffusers;

public static class SkylineDiffuserCalculator
{
    public static SkylineDiffuserResult Calculate(DiffuserDesignInput input)
    {
        var result = new SkylineDiffuserResult
        {
            TargetFrequencyHz = input.TargetFrequencyHz,
            PrimeOrder = input.PrimeOrder,
            CellSizeMm = input.WellWidthMm
        };

        if (!ValidateInput(input, result))
            return result;

        var wavelengthMm = input.SoundSpeed * 1000 / input.TargetFrequencyHz;
        var cells = new List<SkylineCell>();
        var maxDepth = 0.0;

        for (var row = 0; row < input.PrimeOrder; row++)
        {
            for (var column = 0; column < input.PrimeOrder; column++)
            {
                var residue = (row * row + column * column) % input.PrimeOrder;
                var depth = residue * wavelengthMm / (2 * input.PrimeOrder);
                depth = Math.Min(depth, input.MaxDepthLimitMm);
                depth = Math.Round(depth, 1);
                maxDepth = Math.Max(maxDepth, depth);
                cells.Add(new SkylineCell(row + 1, column + 1, residue, Math.Round(input.WellWidthMm, 1), depth));
            }
        }

        result.Cells = cells;
        result.PanelWidthMm = Math.Round(input.WellWidthMm * input.PrimeOrder, 1);
        result.PanelHeightMm = Math.Round(input.WellWidthMm * input.PrimeOrder, 1);
        result.MaxDepthMm = Math.Round(maxDepth, 1);
        result.LowerFrequencyHz = Math.Round(input.LowerFrequencyHz ?? input.SoundSpeed * 1000 / (2 * Math.Max(maxDepth, 1)), 1);
        result.UpperFrequencyHz = Math.Round(input.UpperFrequencyHz ?? input.SoundSpeed * 1000 / (2 * input.WellWidthMm), 1);

        if (result.MaxDepthMm >= input.MaxDepthLimitMm - 0.05)
            result.Warnings.Add("Bazı hücre derinlikleri maksimum derinlik sınırında kırpıldı; daha derin panel veya daha yüksek hedef frekans seçilebilir.");

        if (cells.Count > 1 && cells.Where(c => c.Residue > 0).All(c => Math.Abs(c.DepthMm - input.MaxDepthLimitMm) < 0.05))
            result.Warnings.Add("Hedef frekans bu panel için çok düşük olduğu için sıfır dışındaki tüm hücreler aynı maksimum derinliğe kırpıldı; gerçek Skyline dizilimi oluşmuyor.");

        var proxy = new DiffuserDesignResult { Warnings = result.Warnings };
        DiffuserSuitabilityAnalyzer.Analyze(input, proxy);
        result.Suitability = proxy.Suitability;
        result.Recommendation = proxy.Recommendation;
        result.Warnings = proxy.Warnings;

        return result;
    }

    private static bool ValidateInput(DiffuserDesignInput input, SkylineDiffuserResult result)
    {
        if (!double.IsFinite(input.TargetFrequencyHz) || input.TargetFrequencyHz <= 0)
        {
            result.Warnings.Add("Hedef frekans pozitif ve sonlu olmalıdır.");
            result.Suitability = DiffuserSuitability.NotRecommended;
            return false;
        }

        if (!QrdDiffuserCalculator.IsSupportedPrime(input.PrimeOrder))
        {
            result.Warnings.Add("Skyline sıra değeri desteklenen asal sayılardan biri olmalıdır: 7, 11, 13, 17.");
            result.Suitability = DiffuserSuitability.NotRecommended;
            return false;
        }

        if (!double.IsFinite(input.WellWidthMm) || input.WellWidthMm <= 0 ||
            !double.IsFinite(input.MaxDepthLimitMm) || input.MaxDepthLimitMm <= 0 ||
            !double.IsFinite(input.SoundSpeed) || input.SoundSpeed <= 0)
        {
            result.Warnings.Add("Hücre ölçüsü, maksimum derinlik ve ses hızı pozitif ve sonlu değerler olmalıdır.");
            result.Suitability = DiffuserSuitability.NotRecommended;
            return false;
        }

        return true;
    }
}
