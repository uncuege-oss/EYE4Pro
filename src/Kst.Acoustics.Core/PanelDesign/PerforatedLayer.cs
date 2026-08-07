using System;
using System.Numerics;

namespace Kst.Acoustics.Core.PanelDesign;

public enum HolePattern
{
    Square,
    Staggered
}

public record PerforationOpening(
    double AreaRatio,
    double HydraulicDiameter,
    double Circularity,
    double AspectRatio,
    string ShapeType);

/// <summary>
/// Represents a perforated panel layer.
/// </summary>
public class PerforatedLayer : AcousticLayer
{
    public const double MinOpenAreaRatio = 0.0001;
    public const double MaxOpenAreaRatio = 0.99;

    /// <summary>
    /// Open area ratio (0 to 1). e.g., 0.15 for 15%.
    /// </summary>
    public double OpenAreaRatio { get; set; }

    /// <summary>
    /// Diameter of the holes in meters.
    /// </summary>
    public double HoleDiameter { get; set; }

    /// <summary>
    /// Centre-to-centre hole spacing in meters.
    /// </summary>
    public double HoleSpacing { get; set; }

    public HolePattern HolePattern { get; set; } = HolePattern.Square;

    public IReadOnlyList<PerforationOpening>? CustomOpenings { get; set; }

    public override TransferMatrix GetTransferMatrix(double frequency)
    {
        if (frequency <= 0 || Thickness < 0)
        {
            return RigidSheet();
        }

        if (CustomOpenings is { Count: > 0 })
        {
            return GetCustomOpeningTransferMatrix(frequency);
        }

        if (HoleDiameter <= 0 || OpenAreaRatio <= 0)
        {
            return RigidSheet();
        }

        var zp = CalculateOpeningImpedance(frequency, OpenAreaRatio, OpenAreaRatio, HoleDiameter, circularity: 1.0, aspectRatio: 1.0);
        return IsFinite(zp) ? new TransferMatrix(1, zp, 0, 1) : RigidSheet();
    }

    public double EstimateHelmholtzResonance(double airGapThickness)
    {
        if (airGapThickness <= 0 || Thickness < 0)
        {
            return 0.0;
        }

        double frequency = 500.0;
        for (int i = 0; i < 12; i++)
        {
            var neck = EstimateEquivalentNeck(frequency);
            if (neck is null || neck.Value.OpenAreaRatio <= 0 || neck.Value.EffectiveLength <= 0)
            {
                return 0.0;
            }

            double next = 343.0 / (2.0 * Math.PI) *
                Math.Sqrt(neck.Value.OpenAreaRatio / (neck.Value.EffectiveLength * airGapThickness));
            if (!double.IsFinite(next) || next <= 0)
            {
                return 0.0;
            }

            if (Math.Abs(next - frequency) < 0.01)
            {
                return next;
            }

            frequency = next;
        }

        return frequency;
    }

    private TransferMatrix GetCustomOpeningTransferMatrix(double frequency)
    {
        var openings = CustomOpenings?
            .Where(o => o.AreaRatio > 0 && o.HydraulicDiameter > 0)
            .ToArray() ?? [];
        if (openings.Length == 0) return RigidSheet();

        Complex admittance = Complex.Zero;
        double totalSigma = ClampOpenAreaRatio(openings.Sum(o => o.AreaRatio));
        foreach (var opening in openings)
        {
            double sigma = ClampOpenAreaRatio(opening.AreaRatio);
            double diameter = Math.Max(opening.HydraulicDiameter, 1e-6);
            var zi = CalculateOpeningImpedance(
                frequency,
                sigma,
                totalSigma,
                diameter,
                opening.Circularity,
                opening.AspectRatio);
            if (IsFinite(zi) && zi.Magnitude > 1e-12)
            {
                admittance += Complex.One / zi;
            }
        }

        if (!IsFinite(admittance) || admittance.Magnitude <= 1e-12)
            return RigidSheet();

        var zp = Complex.One / admittance;
        return IsFinite(zp) ? new TransferMatrix(1, zp, 0, 1) : RigidSheet();
    }

    private (double OpenAreaRatio, double EffectiveLength)? EstimateEquivalentNeck(double frequency)
    {
        if (CustomOpenings is { Count: > 0 })
        {
            var openings = CustomOpenings
                .Where(o => o.AreaRatio > 0 && o.HydraulicDiameter > 0)
                .ToArray();
            if (openings.Length == 0) return null;

            double totalSigma = ClampOpenAreaRatio(openings.Sum(o => o.AreaRatio));
            double admittanceMassTerm = 0.0;
            foreach (var opening in openings)
            {
                double openingSigma = ClampOpenAreaRatio(opening.AreaRatio);
                double effectiveLength = EstimateOpeningEffectiveLength(
                    frequency,
                    totalSigma,
                    Math.Max(opening.HydraulicDiameter, 1e-6),
                    opening.Circularity,
                    opening.AspectRatio);
                if (effectiveLength > 0 && double.IsFinite(effectiveLength))
                {
                    admittanceMassTerm += openingSigma / effectiveLength;
                }
            }

            if (admittanceMassTerm <= 0) return null;
            return (totalSigma, totalSigma / admittanceMassTerm);
        }

        if (HoleDiameter <= 0 || OpenAreaRatio <= 0)
        {
            return null;
        }

        double sigma = ClampOpenAreaRatio(OpenAreaRatio);
        return (sigma, EstimateOpeningEffectiveLength(frequency, sigma, HoleDiameter, 1.0, 1.0));
    }

    private Complex CalculateOpeningImpedance(
        double frequency,
        double openAreaRatio,
        double fokOpenAreaRatio,
        double hydraulicDiameter,
        double circularity,
        double aspectRatio)
    {
        double omega = 2 * Math.PI * frequency;
        double rho0 = 1.21;
        double mu = 1.81e-5; // Dynamic viscosity of air
        double sigma = ClampOpenAreaRatio(openAreaRatio);
        double diameter = Math.Max(hydraulicDiameter, 1e-6);

        double perforateConstant = diameter / 2.0 * Math.Sqrt(omega * rho0 / mu);
        double shapeResistanceFactor = CalculateShapeResistanceFactor(circularity, aspectRatio);
        double tubeResistance = 32.0 * mu * Thickness / (sigma * diameter * diameter)
            * Math.Sqrt(1.0 + perforateConstant * perforateConstant / 32.0)
            * shapeResistanceFactor;
        double edgeResistance = Math.Sqrt(2.0) * mu * perforateConstant / (sigma * diameter)
            * Math.Sqrt(shapeResistanceFactor);
        double resist = tubeResistance + edgeResistance;

        double innerMassCorrection = 1.0 + 1.0 / Math.Sqrt(9.0 + perforateConstant * perforateConstant / 2.0);
        double endCorrection = 0.85 * diameter * CalculateFokCorrection(fokOpenAreaRatio, HolePattern) * CalculateEndCorrectionShapeFactor(circularity, aspectRatio);
        double effectiveLength = Thickness * innerMassCorrection + endCorrection;
        Complex react = Complex.ImaginaryOne * omega * rho0 * effectiveLength / sigma;

        return resist + react;
    }

    private double EstimateOpeningEffectiveLength(
        double frequency,
        double fokOpenAreaRatio,
        double hydraulicDiameter,
        double circularity,
        double aspectRatio)
    {
        double omega = 2.0 * Math.PI * Math.Max(frequency, 1e-6);
        double rho0 = 1.21;
        double mu = 1.81e-5;
        double diameter = Math.Max(hydraulicDiameter, 1e-6);
        double perforateConstant = diameter / 2.0 * Math.Sqrt(omega * rho0 / mu);
        double innerMassCorrection = 1.0 + 1.0 / Math.Sqrt(9.0 + perforateConstant * perforateConstant / 2.0);
        double endCorrection = 0.85 * diameter * CalculateFokCorrection(fokOpenAreaRatio, HolePattern) * CalculateEndCorrectionShapeFactor(circularity, aspectRatio);

        return Math.Max(1e-9, Thickness * innerMassCorrection + endCorrection);
    }

    private static TransferMatrix RigidSheet()
    {
        return new TransferMatrix(1, new Complex(1e30, 0), 0, 1);
    }

    private static double CalculateShapeResistanceFactor(double circularity, double aspectRatio)
    {
        double c = Math.Clamp(circularity, 0.05, 1.0);
        double aspectPenalty = Math.Sqrt(Math.Clamp(aspectRatio, 1.0, 20.0));
        return Math.Clamp((1.0 / c) * Math.Min(aspectPenalty, 3.0), 1.0, 8.0);
    }

    private static double CalculateEndCorrectionShapeFactor(double circularity, double aspectRatio)
    {
        double c = Math.Clamp(circularity, 0.10, 1.0);
        double aspectPenalty = Math.Pow(Math.Clamp(aspectRatio, 1.0, 20.0), 0.15);
        return Math.Clamp(Math.Sqrt(c) * aspectPenalty, 0.45, 1.6);
    }

    public static double CalculateOpenAreaRatio(double holeDiameter, double holeSpacing, HolePattern pattern)
    {
        if (holeDiameter <= 0 || holeSpacing <= 0)
        {
            return 0.0;
        }

        double holeArea = Math.PI * holeDiameter * holeDiameter / 4.0;
        double cellArea = pattern == HolePattern.Staggered
            ? holeSpacing * holeSpacing * Math.Sqrt(3.0) / 2.0
            : holeSpacing * holeSpacing;

        return ClampOpenAreaRatio(holeArea / cellArea);
    }

    public static double CalculateSpacingForOpenAreaRatio(double holeDiameter, double openAreaRatio, HolePattern pattern)
    {
        if (holeDiameter <= 0 || openAreaRatio <= 0)
        {
            return 0.0;
        }

        double holeArea = Math.PI * holeDiameter * holeDiameter / 4.0;
        double cellFactor = pattern == HolePattern.Staggered ? Math.Sqrt(3.0) / 2.0 : 1.0;
        return Math.Sqrt(holeArea / (ClampOpenAreaRatio(openAreaRatio) * cellFactor));
    }

    public static double ClampOpenAreaRatio(double openAreaRatio)
    {
        return Math.Clamp(openAreaRatio, MinOpenAreaRatio, MaxOpenAreaRatio);
    }

    private static double CalculateFokCorrection(double openAreaRatio, HolePattern pattern)
    {
        return pattern == HolePattern.Staggered
            ? FokHexagonal(openAreaRatio)
            : FokSquare(openAreaRatio);
    }

    // Kare (square) dizilim: orijinal Fok serisi (Ingard 1953 / Mechel "Formulas of Acoustics")
    private static double FokSquare(double openAreaRatio)
    {
        double epsilon = Math.Sqrt(Math.Clamp(openAreaRatio, 0.0, 1.0));
        double epsilon2 = epsilon * epsilon;
        double epsilon3 = epsilon2 * epsilon;
        double epsilon5 = epsilon3 * epsilon2;
        double epsilon6 = epsilon5 * epsilon;
        double epsilon7 = epsilon6 * epsilon;
        double epsilon8 = epsilon7 * epsilon;

        double correction = 1.0
            - 1.4092 * epsilon
            + 0.33818 * epsilon3
            + 0.06793 * epsilon5
            - 0.02287 * epsilon6
            + 0.03015 * epsilon7
            - 0.01641 * epsilon8;

        return Math.Clamp(correction, 0.0, 1.0);
    }

    // Şaşırtmalı/üçgen (hexagonal close-packed) dizilim için Fok serisi.
    // Ref: Mechel, "Formulas of Acoustics" 2nd ed., bölüm K (delikli levhalar);
    // Allard & Atalla, "Propagation of Sound in Porous Media" 2009, eşitlik (9.32).
    // Karen dizilimden farklı olarak doğrusal terim katsayısı 1.4527, üs serisi
    // farklı bir biçimdedir.
    private static double FokHexagonal(double openAreaRatio)
    {
        double epsilon = Math.Sqrt(Math.Clamp(openAreaRatio, 0.0, 1.0));
        double epsilon2 = epsilon * epsilon;
        double epsilon3 = epsilon2 * epsilon;
        double epsilon5 = epsilon3 * epsilon2;

        double correction = 1.0
            - 1.4527 * epsilon
            + 0.47293 * epsilon3
            - 0.06104 * epsilon5;

        return Math.Clamp(correction, 0.0, 1.0);
    }

    private static bool IsFinite(Complex value)
    {
        return double.IsFinite(value.Real) && double.IsFinite(value.Imaginary);
    }
}
