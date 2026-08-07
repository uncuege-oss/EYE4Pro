using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Bina.Models;

/// JSON-uyumlu DXF arka plan segmenti (sadeleştirilmiş — görüntü/snap için).
public class BinaDxfSegment
{
    public Point2D A { get; set; }
    public Point2D B { get; set; }
    public string Layer { get; set; } = "";
}

/// JSON-uyumlu DXF ARC entity (kapı yayı tespiti için).
public class BinaDxfArc
{
    public Point2D Merkez { get; set; }
    public double R { get; set; }
    public double BasAciDeg { get; set; }
    public double BitAciDeg { get; set; }
}
