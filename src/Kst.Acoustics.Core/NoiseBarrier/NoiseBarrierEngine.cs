using System;
using System.Collections.Generic;
using System.Linq;

namespace Kst.Acoustics.Core.NoiseBarrier;

public class NoiseBarrierEngine
{
    private readonly NoiseBarrierProject _project;
    private readonly List<NoiseBarrierMaterial> _materials;

    public NoiseBarrierEngine(NoiseBarrierProject project, List<NoiseBarrierMaterial> materials)
    {
        _project = project;
        _materials = materials;
    }

    public void GenerateReceivers()
    {
        _project.Receivers.Clear();
        var building = _project.Building;

        double xStart = building.CenterX - building.Width / 2;
        double xStep = building.Width / Math.Max(1, building.FacadeGridColumns - 1);

        double zStep = building.Height / Math.Max(1, building.FacadeGridRows + 1);

        int id = 1;
        for (int r = 0; r < building.FacadeGridRows; r++)
        {
            double z;
            if (building.FacadeGridRows > 1)
                z = building.BaseZ + ((r + 1) * zStep);
            else
                z = building.BaseZ + (building.Height / 2.0);

            int floorIndex = Math.Clamp((int)Math.Floor((z - building.BaseZ) / Math.Max(0.1, building.FloorHeight)), 0, 999);

            for (int c = 0; c < building.FacadeGridColumns; c++)
            {
                double x;
                if (building.FacadeGridColumns > 1)
                    x = xStart + (c * xStep);
                else
                    x = building.CenterX;

                _project.Receivers.Add(new ReceiverPoint
                {
                    Id = id++,
                    X = x,
                    Y = -building.FacadeReceiverOffset,
                    Z = z,
                    FloorIndex = floorIndex
                });
            }
        }
    }

    public void RunSimulation()
    {
        var octaves = new double[] { 63, 125, 250, 500, 1000, 2000, 4000, 8000 };
        var barrierPairs = _project.Barriers
            .Where(b => b.IsEnabled)
            .Select(b => (Barrier: b, Material: _materials.FirstOrDefault(m => m.Id == b.MaterialId)))
            .ToList();

        ValidateProject(barrierPairs);

        foreach (var receiver in _project.Receivers.Where(r => r.IsEnabled))
        {
            receiver.LevelNoBarrier.Clear();
            receiver.LevelWithBarrier.Clear();

            foreach (var freq in octaves)
            {
                double totalEnergyNoBarrier = 0;
                double totalEnergyWithBarrier = 0;

                foreach (var source in _project.Sources.Where(s => s.IsEnabled))
                {
                    var segments = GetSourceSegments(source);
                    foreach (var segment in segments)
                    {
                        double Lw = segment.GetLw(freq);

                        double dist = CalculateDistance(segment.X, segment.Y, segment.Z, receiver.X, receiver.Y, receiver.Z);
                        double Adiv = 20 * Math.Log10(Math.Max(0.1, dist)) + 11;
                        double Aatm = CalculateAtmosphericAbs(freq, dist);
                        double Agr = CalculateGroundAttenuation(dist, segment.Z, receiver.Z);

                        var primary = SelectPrimaryBarrier(segment, receiver, freq, barrierPairs);
                        double Abar = primary.Abar;

                        double Lp_no_barrier = Lw - Adiv - Aatm - Agr;
                        totalEnergyNoBarrier += Math.Pow(10, Lp_no_barrier / 10) *
                                                CalculateReflectionEnergyFactor(primary.Material, primary.Barrier, freq, hasBarrierReflectionPath: false);

                        double Lp_with_barrier = Lw - Adiv - Aatm - Agr - Abar;
                        totalEnergyWithBarrier += Math.Pow(10, Lp_with_barrier / 10) *
                                                  CalculateReflectionEnergyFactor(primary.Material, primary.Barrier, freq, hasBarrierReflectionPath: Abar > 0.05);
                    }
                }

                receiver.LevelNoBarrier[freq] = 10 * Math.Log10(Math.Max(1e-12, totalEnergyNoBarrier));
                receiver.LevelWithBarrier[freq] = 10 * Math.Log10(Math.Max(1e-12, totalEnergyWithBarrier));
            }

            receiver.TotalDbANoBarrier = CalculateDbA(receiver.LevelNoBarrier);
            receiver.TotalDbAWithBarrier = CalculateDbA(receiver.LevelWithBarrier);
            receiver.InsertionLoss = receiver.TotalDbANoBarrier - receiver.TotalDbAWithBarrier;
        }
    }

    private (double Abar, BarrierModel? Barrier, NoiseBarrierMaterial? Material) SelectPrimaryBarrier(
        SourceSegment segment,
        ReceiverPoint receiver,
        double freq,
        List<(BarrierModel Barrier, NoiseBarrierMaterial? Material)> barrierPairs)
    {
        double maxAbar = 0;
        BarrierModel? maxBarrier = null;
        NoiseBarrierMaterial? maxMaterial = null;

        foreach (var (barrier, material) in barrierPairs)
        {
            double abar = CalculateBarrierAttenuation(segment, receiver, freq, barrier, material);
            if (abar > maxAbar)
            {
                maxAbar = abar;
                maxBarrier = barrier;
                maxMaterial = material;
            }
        }

        return (maxAbar, maxBarrier, maxMaterial);
    }

    private List<SourceSegment> GetSourceSegments(NoiseSource source)
    {
        var segments = new List<SourceSegment>();
        var sourceSpectrum = source.Type == SourceType.Traffic
            ? GenerateTrafficSpectrum(source)
            : new Dictionary<double, double>(source.Lw);
        double sourceOverall = source.Type == SourceType.Traffic
            ? CalculateOverallLevel(sourceSpectrum)
            : source.OverallLw;

        if (source.Type == SourceType.Point)
        {
            var normalizedSpectrum = NormalizeSpectrum(sourceSpectrum, sourceOverall);
            segments.Add(new SourceSegment
            {
                X = source.X,
                Y = source.Y,
                Z = source.Z,
                OverallLw = sourceOverall,
                Lw = normalizedSpectrum
            });
        }
        else
        {
            double dx = source.XEnd - source.X;
            double dy = source.YEnd - source.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);

            int numSegments = (int)Math.Max(1, Math.Ceiling(length / 2.0));
            double segmentCorrection = 10 * Math.Log10(numSegments);
            double lengthCorrection = 10 * Math.Log10(Math.Max(0.1, length));
            double totalPower = sourceOverall;

            if (source.Type == SourceType.Traffic)
            {
                totalPower += lengthCorrection;
            }

            double lwPerSegment = totalPower - segmentCorrection;
            var normalizedSpectrum = NormalizeSpectrum(sourceSpectrum, sourceOverall);
            var spectrumPerSegment = normalizedSpectrum.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value + (totalPower - sourceOverall) - segmentCorrection);

            for (int i = 0; i < numSegments; i++)
            {
                double t = (i + 0.5) / numSegments;
                segments.Add(new SourceSegment
                {
                    X = source.X + t * dx,
                    Y = source.Y + t * dy,
                    Z = source.Z,
                    OverallLw = lwPerSegment,
                    Lw = new Dictionary<double, double>(spectrumPerSegment)
                });
            }
        }
        return segments;
    }

    private static Dictionary<double, double> GenerateTrafficSpectrum(NoiseSource source)
    {
        double flow = Math.Clamp(source.TrafficFlowVehiclesPerHour, 1.0, 20000.0);
        double speed = Math.Clamp(source.AverageSpeedKmh, 10.0, 160.0);
        double heavy = Math.Clamp(source.HeavyVehiclePercent, 0.0, 100.0) / 100.0;

        var profile = GetTrafficProfile(source.TrafficModel);
        double overall = profile.BaseLevel
            + 10.0 * Math.Log10(flow / 100.0)
            + profile.SpeedSlope * Math.Log10(speed / profile.ReferenceSpeedKmh)
            + 10.0 * Math.Log10(1.0 + profile.HeavyVehicleWeight * heavy)
            + 23.5;

        double shapeOverall = CalculateOverallLevel(profile.OctaveShape);
        double correction = overall - shapeOverall;
        return profile.OctaveShape.ToDictionary(kvp => kvp.Key, kvp => kvp.Value + correction);
    }

    private static TrafficProfile GetTrafficProfile(TrafficEmissionModel model)
    {
        return model switch
        {
            TrafficEmissionModel.CnossosEuPreview => new TrafficProfile(
                BaseLevel: 59.5,
                ReferenceSpeedKmh: 70.0,
                SpeedSlope: 27.0,
                HeavyVehicleWeight: 5.5,
                OctaveShape: new Dictionary<double, double>
                {
                    { 63, -15.0 }, { 125, -10.0 }, { 250, -6.0 }, { 500, -3.5 },
                    { 1000, -3.0 }, { 2000, -6.5 }, { 4000, -13.0 }, { 8000, -24.0 }
                }),
            TrafficEmissionModel.FhwaTnmPreview => new TrafficProfile(
                BaseLevel: 60.5,
                ReferenceSpeedKmh: 88.0,
                SpeedSlope: 34.0,
                HeavyVehicleWeight: 7.0,
                OctaveShape: new Dictionary<double, double>
                {
                    { 63, -12.0 }, { 125, -8.0 }, { 250, -5.0 }, { 500, -4.0 },
                    { 1000, -4.5 }, { 2000, -7.5 }, { 4000, -14.0 }, { 8000, -25.0 }
                }),
            TrafficEmissionModel.NmpbRoutesPreview => new TrafficProfile(
                BaseLevel: 58.7,
                ReferenceSpeedKmh: 70.0,
                SpeedSlope: 30.0,
                HeavyVehicleWeight: 6.0,
                OctaveShape: new Dictionary<double, double>
                {
                    { 63, -14.0 }, { 125, -9.0 }, { 250, -5.5 }, { 500, -3.0 },
                    { 1000, -3.5 }, { 2000, -7.0 }, { 4000, -14.5 }, { 8000, -26.0 }
                }),
            _ => new TrafficProfile(
                BaseLevel: 58.0,
                ReferenceSpeedKmh: 70.0,
                SpeedSlope: 30.0,
                HeavyVehicleWeight: 4.0,
                OctaveShape: new Dictionary<double, double>
                {
                    { 63, -18.0 }, { 125, -12.0 }, { 250, -7.0 }, { 500, -4.0 },
                    { 1000, -3.0 }, { 2000, -6.0 }, { 4000, -12.0 }, { 8000, -22.0 }
                })
        };
    }

    private static double CalculateOverallLevel(Dictionary<double, double> spectrum)
    {
        double energy = spectrum.Values.Sum(level => Math.Pow(10, level / 10.0));
        return 10.0 * Math.Log10(Math.Max(1e-12, energy));
    }

    private static Dictionary<double, double> NormalizeSpectrum(Dictionary<double, double> spectrum, double targetOverallLw)
    {
        if (spectrum.Count == 0)
            return new Dictionary<double, double> { { 1000, targetOverallLw } };

        double currentEnergy = spectrum.Values.Sum(level => Math.Pow(10, level / 10.0));
        double currentOverall = 10 * Math.Log10(Math.Max(1e-12, currentEnergy));
        double correction = targetOverallLw - currentOverall;

        return spectrum.ToDictionary(kvp => kvp.Key, kvp => kvp.Value + correction);
    }

    internal class SourceSegment
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double OverallLw { get; set; }
        public Dictionary<double, double> Lw { get; set; } = new();

        public double GetLw(double frequency)
        {
            return Lw.TryGetValue(frequency, out var value) ? value : OverallLw;
        }
    }

    private double CalculateDistance(double x1, double y1, double z1, double x2, double y2, double z2)
    {
        return Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2) + Math.Pow(z2 - z1, 2));
    }

    private double CalculateAtmosphericAbs(double freq, double distance)
    {
        double alpha = freq switch
        {
            63 => 0.1,
            125 => 0.3,
            250 => 1.0,
            500 => 2.5,
            1000 => 5.0,
            2000 => 10.0,
            4000 => 30.0,
            8000 => 80.0,
            _ => 2.0
        };
        double humidityFactor = 1.0 + Math.Clamp((50.0 - _project.RelativeHumidity) / 100.0, -0.3, 0.5);
        double temperatureFactor = 1.0 + Math.Clamp((20.0 - _project.AirTemperature) / 100.0, -0.2, 0.3);
        return alpha * humidityFactor * temperatureFactor * distance / 1000.0;
    }

    private double CalculateGroundAttenuation(double distance, double sourceHeight, double receiverHeight)
    {
        if (distance < 3.0) return 0;

        double heightFactor = Math.Clamp((sourceHeight + receiverHeight) / 8.0, 0.0, 1.0);
        double distanceFactor = Math.Clamp(distance / 100.0, 0.0, 1.0);
        return Math.Clamp(_project.GroundAbsorption, 0.0, 1.0) * (1.0 - heightFactor) * distanceFactor * 3.0;
    }

    private double CalculateBarrierAttenuation(
        SourceSegment source,
        ReceiverPoint receiver,
        double freq,
        BarrierModel barrier,
        NoiseBarrierMaterial? material)
    {
        double lambda = 343.0 / freq;
        double d0 = CalculateDistance(source.X, source.Y, source.Z, receiver.X, receiver.Y, receiver.Z);
        double denominator = receiver.Y - source.Y;
        if (Math.Abs(denominator) < 1e-9) return 0;

        double t = (barrier.PositionY - source.Y) / denominator;
        bool isLineOfSightBlocked = false;
        if (t > 0 && t < 1)
        {
            double xInt = source.X + t * (receiver.X - source.X);
            double zInt = source.Z + t * (receiver.Z - source.Z);
            isLineOfSightBlocked = xInt >= (barrier.CenterX - barrier.Length / 2) &&
                                   xInt <= (barrier.CenterX + barrier.Length / 2) &&
                                   zInt >= barrier.BaseZ &&
                                   zInt <= (barrier.BaseZ + barrier.Height);
        }

        if (!isLineOfSightBlocked) return 0;

        double topZ = barrier.BaseZ + barrier.Height;
        double xTop = source.X + t * (receiver.X - source.X);
        xTop = Math.Clamp(xTop, barrier.CenterX - barrier.Length / 2, barrier.CenterX + barrier.Length / 2);
        double d1Top = CalculateDistance(source.X, source.Y, source.Z, xTop, barrier.PositionY, topZ);
        double d2Top = CalculateDistance(xTop, barrier.PositionY, topZ, receiver.X, receiver.Y, receiver.Z);
        double A_top = CalculateEdgeAttenuation(d1Top + d2Top - d0, lambda);

        double leftX = barrier.CenterX - barrier.Length / 2;
        double zLeft = source.Z + t * (receiver.Z - source.Z);
        zLeft = Math.Clamp(zLeft, barrier.BaseZ, barrier.BaseZ + barrier.Height);
        double d1Left = CalculateDistance(source.X, source.Y, source.Z, leftX, barrier.PositionY, zLeft);
        double d2Left = CalculateDistance(leftX, barrier.PositionY, zLeft, receiver.X, receiver.Y, receiver.Z);
        double A_left = CalculateEdgeAttenuation(d1Left + d2Left - d0, lambda);

        double rightX = barrier.CenterX + barrier.Length / 2;
        double zRight = source.Z + t * (receiver.Z - source.Z);
        zRight = Math.Clamp(zRight, barrier.BaseZ, barrier.BaseZ + barrier.Height);
        double d1Right = CalculateDistance(source.X, source.Y, source.Z, rightX, barrier.PositionY, zRight);
        double d2Right = CalculateDistance(rightX, barrier.PositionY, zRight, receiver.X, receiver.Y, receiver.Z);
        double A_right = CalculateEdgeAttenuation(d1Right + d2Right - d0, lambda);

        double energyDiff = Math.Pow(10, -A_top / 10) + Math.Pow(10, -A_left / 10) + Math.Pow(10, -A_right / 10);
        double Abar = -10 * Math.Log10(Math.Min(1.0, energyDiff));
        Abar = Math.Min(25, Abar);

        Abar += CalculateAbsorptiveBarrierBonus(barrier, material, freq);

        if (material != null && material.TransmissionLoss.TryGetValue(freq, out double tl))
        {
            double energyTrans = Math.Pow(10, -tl / 10);
            Abar = -10 * Math.Log10(Math.Pow(10, -Abar / 10) + energyTrans);
        }

        return Math.Clamp(Abar, 0, 25);
    }

    private static double CalculateAbsorptiveBarrierBonus(BarrierModel barrier, NoiseBarrierMaterial? material, double freq)
    {
        if (material == null || !material.AbsorptionCoefficients.TryGetValue(freq, out var alpha))
            return 0;

        alpha = Math.Clamp(alpha, 0.0, 1.0);
        double sourceSideWeight = barrier.HasAbsorptiveSourceSide ? 1.0 : 0.0;
        double receiverSideWeight = barrier.HasAbsorptiveReceiverSide ? 0.45 : 0.0;
        return Math.Clamp(alpha * (sourceSideWeight + receiverSideWeight) * 2.5, 0.0, 3.0);
    }

    private double CalculateReflectionEnergyFactor(NoiseBarrierMaterial? material, BarrierModel? barrier, double freq, bool hasBarrierReflectionPath)
    {
        if (!_project.EnableFacadeReflections)
            return 1.0;

        double facade = Math.Clamp(_project.FacadeReflectionCoefficient, 0.0, 1.0);
        double factor = 1.0 + facade;

        if (!hasBarrierReflectionPath || barrier == null)
            return factor;

        double alpha = 0.0;
        if (material != null && material.AbsorptionCoefficients.TryGetValue(freq, out var materialAlpha))
            alpha = Math.Clamp(materialAlpha, 0.0, 1.0);

        double barrierRefl = Math.Clamp(_project.BarrierReflectionCoefficient, 0.0, 1.0);
        if (barrier.HasAbsorptiveSourceSide)
            barrierRefl *= 1.0 - 0.75 * alpha;
        if (barrier.HasAbsorptiveReceiverSide)
            barrierRefl *= 1.0 - 0.35 * alpha;

        double pair = Math.Clamp(facade * barrierRefl, 0.0, 0.95);
        int order = Math.Clamp(_project.MaxReflectionOrder, 0, 6);
        for (int i = 1; i <= order; i++)
        {
            factor += Math.Pow(pair, i) / (1.0 + i * 0.45);
        }

        return Math.Clamp(factor, 1.0, 3.5);
    }

    private double CalculateEdgeAttenuation(double delta, double lambda)
    {
        if (lambda <= 0) return 0;

        double frequency = NoiseBarrierDiffraction.DefaultSpeedOfSound / lambda;
        return _project.Method switch
        {
            DiffractionMethod.Maekawa => NoiseBarrierDiffraction.CalculateMaekawaAttenuation(delta, frequency),
            DiffractionMethod.KurzeAnderson => NoiseBarrierDiffraction.CalculateKurzeAndersonAttenuation(delta, frequency),
            DiffractionMethod.Iso9613_2 => NoiseBarrierDiffraction.CalculateIso9613_2Attenuation(delta, frequency),
            _ => NoiseBarrierDiffraction.CalculateEngineeringPreviewAttenuation(delta, frequency)
        };
    }

    private double CalculateDbA(Dictionary<double, double> levels)
    {
        var aWeights = new Dictionary<double, double>
        {
            { 63, -26.2 }, { 125, -16.1 }, { 250, -8.6 }, { 500, -3.2 },
            { 1000, 0.0 }, { 2000, 1.2 }, { 4000, 1.0 }, { 8000, -1.1 }
        };

        double energySum = 0;
        foreach (var kvp in levels)
        {
            if (aWeights.TryGetValue(kvp.Key, out double weight))
            {
                energySum += Math.Pow(10, (kvp.Value + weight) / 10);
            }
        }
        return 10 * Math.Log10(Math.Max(1e-12, energySum));
    }

    private void ValidateProject(List<(BarrierModel Barrier, NoiseBarrierMaterial? Material)> barrierPairs)
    {
        _project.ValidationWarnings.Clear();

        if (!_project.Sources.Any(source => source.IsEnabled))
            _project.ValidationWarnings.Add("Etkin kaynak bulunmuyor.");

        if (barrierPairs.Count == 0)
            _project.ValidationWarnings.Add("Etkin bariyer bulunmuyor; hesap bariyersizle özdeş olacaktır.");

        double nearestBarrierY = barrierPairs.Count > 0
            ? barrierPairs.Min(p => p.Barrier.PositionY)
            : 0;

        foreach (var source in _project.Sources.Where(s => s.IsEnabled))
        {
            if (barrierPairs.Count > 0 && source.Y >= nearestBarrierY)
                _project.ValidationWarnings.Add($"'{source.Name}' tüm bariyerlerin alıcı tarafında; kırınım hesap dışında kalır.");

            if ((source.Type == SourceType.Line || source.Type == SourceType.Traffic) &&
                CalculateDistance(source.X, source.Y, source.Z, source.XEnd, source.YEnd, source.Z) < 0.1)
            {
                _project.ValidationWarnings.Add($"'{source.Name}' çizgisel kaynak uzunluğu çok kısa.");
            }

            if (source.Type == SourceType.Traffic)
            {
                if (source.TrafficFlowVehiclesPerHour <= 0)
                    _project.ValidationWarnings.Add($"'{source.Name}' trafik debisi pozitif olmalıdır.");
                if (source.AverageSpeedKmh <= 0)
                    _project.ValidationWarnings.Add($"'{source.Name}' trafik hızı pozitif olmalıdır.");
                if (!Enum.IsDefined(source.TrafficModel))
                    _project.ValidationWarnings.Add($"'{source.Name}' için trafik modeli geçersiz.");
            }

            var missingBands = new double[] { 63, 125, 250, 500, 1000, 2000, 4000, 8000 }
                .Where(band => !source.Lw.ContainsKey(band))
                .ToList();
            if (missingBands.Count > 0)
            {
                var bandList = string.Join(", ", missingBands.Select(b => $"{b:0} Hz"));
                _project.ValidationWarnings.Add(
                    $"'{source.Name}' için eksik kaynak bantları: {bandList}; genel Lw kullanılacak.");
            }
        }

        foreach (var (barrier, material) in barrierPairs)
        {
            if (material == null)
            {
                _project.ValidationWarnings.Add($"'{barrier.Name}' için malzeme seçili değil; yalnız kırınım hesabı kullanılacak.");
                continue;
            }

            var missingTlBands = new double[] { 63, 125, 250, 500, 1000, 2000, 4000, 8000 }
                .Where(band => !material.TransmissionLoss.ContainsKey(band))
                .ToList();
            if (missingTlBands.Count > 0)
            {
                var bandList = string.Join(", ", missingTlBands.Select(b => $"{b:0} Hz"));
                _project.ValidationWarnings.Add($"'{barrier.Name}' / {material.Name} için eksik TL bantları: {bandList}.");
            }
        }
    }

    private sealed record TrafficProfile(
        double BaseLevel,
        double ReferenceSpeedKmh,
        double SpeedSlope,
        double HeavyVehicleWeight,
        Dictionary<double, double> OctaveShape);
}
