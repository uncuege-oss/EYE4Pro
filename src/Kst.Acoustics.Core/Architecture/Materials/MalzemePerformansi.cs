namespace Kst.Acoustics.Core.Architecture.Materials;

/// <summary>
/// M36 malzeme kararlarinin yangin, akustik, isi ve islak hacim performans verisi.
/// </summary>
public class MalzemePerformansi
{
    public string YanginTepkiSinifi { get; set; } = "";
    public int YanginDayanimDakika { get; set; }
    public string AkustikNot { get; set; } = "";
    public double SesAzaltimRw { get; set; }
    public double YutuculukAw { get; set; }
    public double IsiIletkenlikWmK { get; set; }
    public double UDegeriWm2K { get; set; }
    public bool SuyaDayanikli { get; set; }
    public bool IslakHacimUygun { get; set; }
    public string KaymazlikSinifi { get; set; } = "";

    public bool YanginBilgisiVar
        => !string.IsNullOrWhiteSpace(YanginTepkiSinifi) || YanginDayanimDakika > 0;

    public bool AkustikBilgisiVar
        => !string.IsNullOrWhiteSpace(AkustikNot) || SesAzaltimRw > 0 || YutuculukAw > 0;

    public bool IsiBilgisiVar
        => IsiIletkenlikWmK > 0 || UDegeriWm2K > 0;

    public bool KaymazlikBilgisiVar
        => !string.IsNullOrWhiteSpace(KaymazlikSinifi);
}
