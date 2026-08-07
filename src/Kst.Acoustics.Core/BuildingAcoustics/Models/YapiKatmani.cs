namespace Kst.Acoustics.Core.BuildingAcoustics.Models;

public class YapiKatmani
{
    public string MalzemeAdi { get; set; } = "";
    public double Kalinlik { get; set; } // mm
    public double Yogunluk { get; set; } // kg/m3
    public double YuzeyKutlesi => (Kalinlik / 1000.0) * Yogunluk; // kg/m2
}
