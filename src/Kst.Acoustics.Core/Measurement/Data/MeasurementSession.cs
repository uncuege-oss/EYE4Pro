using System;
using System.Collections.Generic;
using Kst.Acoustics.Core.Measurement.Analysis;

namespace Kst.Acoustics.Core.Measurement.Data;

public enum PointType
{
    Loudspeaker,
    Microphone
}

public record SetupPoint(string Label, PointType Type, double X, double Y, double Z);

public record FrequencyResponseData(float[] Frequencies, float[] Magnitudes, float[] Phases);

public record MeasurementData
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Label { get; set; } = "Ölçüm";
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string? LinkedPositionId { get; set; }
    public string SignalType { get; set; } = "LogSweep";
    public double SignalDurationMs { get; set; }
    public float[]? ImpulseResponse { get; set; }
    public float[]? StepResponse { get; set; }
        public WaterfallData? Waterfall { get; set; }
        public SpectrogramData? Spectrogram { get; set; }
        public FrequencyResponseData? FrequencyResponse { get; set; }
        public DistortionResult? Distortion { get; set; }
        public ImdResult? Intermodulation { get; set; }
    // Akustik parametreler (Faz 3'te detaylandırılacak)
    public Dictionary<string, double[]>? Parameters { get; set; }
}

public class MeasurementSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime LastModified { get; set; } = DateTime.Now;
    public string Name { get; set; } = "Yeni Oturum";

    public string? DxfFilePath { get; set; }
    public (double L, double W, double H) RoomDimensions { get; set; }
    public List<SetupPoint> SetupPoints { get; set; } = [];

    public string? InputDeviceName { get; set; }
    public string? OutputDeviceName { get; set; }
    public int SampleRate { get; set; } = 48000;
    public int BitDepth { get; set; } = 24;
    public string? MicCalibrationFilePath { get; set; }
    public double LatencyMs { get; set; }
    public double SplOffsetDb { get; set; }
    public bool IsSplCalibrated { get; set; }

    public List<MeasurementData> Measurements { get; set; } = [];
    public string? SimulationResultPath { get; set; }
}
