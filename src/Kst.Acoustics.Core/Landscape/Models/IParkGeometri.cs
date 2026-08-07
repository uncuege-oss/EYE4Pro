using System.Collections.Generic;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Landscape.Models;

/// Park sert zemin yerleşiminin salt-okunur görünümü. Tüketici modüller
/// (M71 Bitkilendirme, ileride M15 park aydınlatma) parkı yalnız bu arayüzle
/// okur; geometriyi değiştiremez. Üretici M70'tir. `.e4bina`/`.e4arazi`
/// salt-okunurluk deseninin park karşılığı (maincodex §4.8).
public interface IParkGeometri
{
    int SemaSurum { get; }
    int GeometriRevizyon { get; }
    string ParkAdi { get; }

    /// Park dış sınırı (kapalı poligon).
    IReadOnlyList<Point2D> Sinir { get; }

    /// Sert zemin öğeleri (salt-okunur).
    IReadOnlyList<IParkOgesi> Ogeler { get; }
}

/// Tek bir sert zemin öğesinin salt-okunur görünümü.
public interface IParkOgesi
{
    int Id { get; }
    ParkOgeTipi Tip { get; }
    string Ad { get; }
    double? ZeminKotuM { get; }
}
