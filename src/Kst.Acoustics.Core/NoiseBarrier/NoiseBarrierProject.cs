using System;
using System.Collections.Generic;

namespace Kst.Acoustics.Core.NoiseBarrier;

public class NoiseBarrierProject
{
    public string Name { get; set; } = "Yeni Ses Bariyeri Projesi";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    public BuildingModel Building { get; set; } = new();
    public List<BarrierModel> Barriers { get; set; } = new() { new BarrierModel { Name = "Bariyer 1" } };
    public List<NoiseSource> Sources { get; set; } = new();
    public List<ReceiverPoint> Receivers { get; set; } = new();
    public List<NoiseBarrierMaterial> EmbeddedMaterials { get; set; } = new();
    public List<string> ValidationWarnings { get; set; } = new();
    
    // Calculation Settings
    public DiffractionMethod Method { get; set; } = DiffractionMethod.KurzeAnderson;
    public double AirTemperature { get; set; } = 20.0;
    public double RelativeHumidity { get; set; } = 50.0;
    public double GroundAbsorption { get; set; } = 0.5; // 0: hard, 1: soft
    public bool EnableFacadeReflections { get; set; } = true;
    public double FacadeReflectionCoefficient { get; set; } = 0.75;
    public double BarrierReflectionCoefficient { get; set; } = 0.45;
    public int MaxReflectionOrder { get; set; } = 2;
}

public enum DiffractionMethod
{
    Maekawa,
    KurzeAnderson,
    Iso9613_2
}
