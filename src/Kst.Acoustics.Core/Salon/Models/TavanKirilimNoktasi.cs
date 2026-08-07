namespace Kst.Acoustics.Core.Salon.Models;

/// Tavan kesit profili kontrol noktası: derinlik (Y, m) → tavan kotu (m).
/// Profil X yönünde ekstrüde edilir (kesit boyunca parçalı düz tavan panelleri) —
/// Terrain `TasarimYuzeyi` deseninin tavan karşılığı. En az 2 nokta profil sayılır;
/// profil boşsa tavan düzdür (`SalonGeometri.SalonYuksekligiM`).
public class TavanKirilimNoktasi : ITavanKirilimNoktasi
{
    /// Derinlik konumu (dünya Y, m).
    public double YM { get; set; }

    /// Bu derinlikteki tavan kotu (m).
    public double KotM { get; set; }

    public override string ToString() => $"y={YM:F1} m → kot {KotM:F2} m";
}
