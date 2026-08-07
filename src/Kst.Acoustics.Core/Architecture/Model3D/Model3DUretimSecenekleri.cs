namespace Kst.Acoustics.Core.Architecture.Model3D;

/// <summary>
/// M32/M33 taslagindan M35 3D model uretirken kullanilan varsayilanlar.
/// </summary>
public class Model3DUretimSecenekleri
{
    public double VarsayilanKatYuksekligiM { get; set; } = 3.2;
    public double VarsayilanNetYukseklikM { get; set; } = 3.0;
    public double VarsayilanDuvarKalinligiM { get; set; } = 0.2;
    public double VarsayilanDosemeKalinligiM { get; set; } = 0.2;
    public double VarsayilanTavanKalinligiM { get; set; } = 0.1;
    public double PlanSiniriBoslukM { get; set; }
    public string VarsayilanDuvarTipiId { get; set; } = "varsayilan-duvar";
    public string VarsayilanDosemeTipiId { get; set; } = "varsayilan-doseme";
    public string VarsayilanTavanTipiId { get; set; } = "varsayilan-tavan";
    public bool PlanaYaz { get; set; } = true;
    public bool DuvarlariUret { get; set; } = true;
    public bool DosemeVeTavanUret { get; set; } = true;
    public bool CekirdekVeSaftHacimleriniUret { get; set; } = true;
}
