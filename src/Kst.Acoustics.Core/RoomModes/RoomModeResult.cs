using System.Collections.Generic;
using System.Linq;

namespace Kst.Acoustics.Core.RoomModes;

/// <summary>
/// Oda modu hesaplama sonuçlarını içeren kapsayıcı sınıf.
/// </summary>
public class RoomModeResult
{
    /// <summary>
    /// Hesaplanan tüm modlar.
    /// </summary>
    public List<RoomMode> Modes { get; set; } = new();

    /// <summary>
    /// Schroeder geçiş frekansı [Hz].
    /// </summary>
    public double? SchroederFrequency { get; set; }

    /// <summary>
    /// Oda oran analizi sonucu.
    /// </summary>
    public string RatioAnalysis { get; set; } = string.Empty;

    /// <summary>
    /// Bolt bölgesi içinde mi?
    /// </summary>
    public bool IsInBoltRegion { get; set; }

    /// <summary>
    /// Ses hızı [m/s].
    /// </summary>
    public double SpeedOfSound { get; set; }

    /// <summary>
    /// Hesap sırasında üretilen kullanıcı uyarıları.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Kullanılan RT60 değeri [s]. Kullanıcı girmediyse tahmini değerdir.
    /// </summary>
    public double? EffectiveRT60 { get; set; }

    /// <summary>
    /// RT60 değerinin kullanıcı girdisi yerine tahminle üretildiğini belirtir.
    /// </summary>
    public bool IsRT60Estimated { get; set; }

    /// <summary>
    /// Sadece eksenel modları döner.
    /// </summary>
    public IEnumerable<RoomMode> AxialModes => Modes.Where(m => m.Type == RoomModeType.Axial);

    /// <summary>
    /// Sadece teğetsel modları döner.
    /// </summary>
    public IEnumerable<RoomMode> TangentialModes => Modes.Where(m => m.Type == RoomModeType.Tangential);

    /// <summary>
    /// Sadece eğik modları döner.
    /// </summary>
    public IEnumerable<RoomMode> ObliqueModes => Modes.Where(m => m.Type == RoomModeType.Oblique);
}
