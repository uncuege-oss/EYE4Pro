using System.Collections.Generic;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Terrain.Models;

/// Arazi geometrisinin salt-okunur görünümü. Tüketici modüller (M61 temel kota
/// oturtma, mimari, M15 çevre aydınlatma) araziyi yalnız bu arayüz üzerinden okur;
/// geometriyi değiştiremez. Üretici M60'tır. M50 `.e4bina` salt-okunurluk deseninin
/// arazi karşılığı.
public interface IAraziGeometri
{
    int SemaSurum { get; }
    int GeometriRevizyon { get; }
    string AraziAdi { get; }
    IReadOnlyList<IAraziNokta> Noktalar { get; }
    IReadOnlyList<IAraziKontur> Konturlar { get; }
}

/// Tekil arazi kotu (spot height) — salt-okunur.
public interface IAraziNokta
{
    double X { get; }
    double Y { get; }
    double Z { get; }
    AraziVeriKaynagi Kaynak { get; }
}

/// Eşyükselti eğrisi — salt-okunur.
public interface IAraziKontur
{
    double KotM { get; }
    bool Kapali { get; }
    AraziKotKaynagi KotKaynagi { get; }
    IReadOnlyList<Point2D> Noktalar { get; }
}
