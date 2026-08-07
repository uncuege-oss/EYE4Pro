using System.Collections.Generic;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Terrain.Models;

/// Eşyükselti eğrisi (kontur). Plan koordinatları (X,Y) metre cinsindendir;
/// kot `KotM` metre cinsinden ayrı tutulur. `KotKaynagi` kotun güven düzeyini gösterir;
/// `Bilinmiyor` ise kullanıcı kotu elle girmelidir.
public class AraziKonturu : IAraziKontur
{
    public double KotM { get; set; }
    public bool Kapali { get; set; }
    public AraziKotKaynagi KotKaynagi { get; set; } = AraziKotKaynagi.Bilinmiyor;
    public List<Point2D> Noktalar { get; set; } = new();

    IReadOnlyList<Point2D> IAraziKontur.Noktalar => Noktalar;
}
