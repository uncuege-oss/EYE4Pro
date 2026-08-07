using System;
using Kst.Acoustics.Core.Geometry;

namespace Kst.Acoustics.Core.BuildingAcoustics.Models;

public enum AkustikNoktaTipi
{
    Kaynak,
    Alici
}

public class AkustikNokta
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Ad { get; set; } = "";
    public AkustikNoktaTipi Tip { get; set; }
    
    // 3D Konum
    public Point3D Konum { get; set; }
    
    // İlişkili Mekan
    public Mekan? Mekan { get; set; }
    
    // Kaynak Parametreleri (Sadece Kaynak tipindeyse)
    public double Lw { get; set; } // Ses Gücü Seviyesi (dB)
    public double[] OctaveLevels { get; set; } = []; // Frekans spektrumu
    
    // Sonuçlar (Sadece Alıcı tipindeyse)
    public double ToplamGurultu { get; set; } // Lp (dB)
}
