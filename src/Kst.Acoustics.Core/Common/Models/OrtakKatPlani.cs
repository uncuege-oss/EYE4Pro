using System;
using System.Collections.Generic;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Common.Models;

/// M08 ve M12 arasında paylaşılan DXF kat planı. Parsed floor plan intermediate representation.
public class OrtakKatPlani
{
    public string DxfDosyaYolu { get; set; } = "";
    public int KatNo { get; set; }
    public string KatAdi { get; set; } = "";
    public double KatYuksekligiM { get; set; } = 3.0;
    public List<OrtakOda> Odalar { get; set; } = new();
    public List<(Point2D A, Point2D B)> HamSegmentler { get; set; } = new();
    public string? BirimUyarisi { get; set; }
}

public class OrtakOda
{
    public string Ad { get; set; } = "";
    public List<Point2D> Poligon { get; set; } = new();
    public double AlanM2 { get; set; }
}
