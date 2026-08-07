using Kst.Acoustics.Metraj.Models;

namespace Kst.Acoustics.Metraj.Template;

public class MetrajTemplate
{
    public string Name { get; set; } = "";
    public GeometricSignature Signature { get; set; } = null!;
    public int SegmentCount { get; set; }
    public double LengthTolPercent { get; set; } = 2.0;
    public double AngleTolDeg { get; set; } = 5.0;
    public double BoundingDiagonal { get; set; }
    // BoundingDiagonal in template coordinate space divided by the max edge length —
    // used as a scale-invariant upper bound on match spatial extent.
    public double NormalizedBoundingDiagonal { get; set; }
    public double TotalLength { get; set; }

    public MetrajTemplate() { }

    public MetrajTemplate(string name, IEnumerable<MetrajPoint> points)
    {
        Name = name;
        Signature = new GeometricSignature(points);
        SegmentCount = Signature.Elements.Count;
        BoundingDiagonal = ComputeBoundingDiagonal(points);
        TotalLength = Signature.Elements.Sum(e => e.Length);
        NormalizedBoundingDiagonal = Signature.MaxEdgeLength > 1e-9 ? BoundingDiagonal / Signature.MaxEdgeLength : 0;
    }

    private static double ComputeBoundingDiagonal(IEnumerable<MetrajPoint> points)
    {
        var list = points.ToList();
        if (list.Count < 2) return 0;
        double maxSq = 0;
        for (int i = 0; i < list.Count; i++)
        {
            for (int j = i + 1; j < list.Count; j++)
            {
                double dx = list[i].X - list[j].X;
                double dy = list[i].Y - list[j].Y;
                double dSq = dx * dx + dy * dy;
                if (dSq > maxSq) maxSq = dSq;
            }
        }
        return Math.Sqrt(maxSq);
    }
}

public class TemplateLibrary
{
    private readonly List<MetrajTemplate> _templates = new();

    public void AddTemplate(string name, IEnumerable<MetrajPoint> points, double lengthTol = 2.0, double angleTol = 5.0)
    {
        var ptsList = points.ToList();
        var sig = new GeometricSignature(ptsList);
        double bd = ComputeBoundingDiagonal(ptsList);
        var template = new MetrajTemplate
        {
            Name = name,
            Signature = sig,
            SegmentCount = ptsList.Count - 1,
            LengthTolPercent = lengthTol,
            AngleTolDeg = angleTol,
            BoundingDiagonal = bd,
            TotalLength = sig.Elements.Sum(e => e.Length),
            NormalizedBoundingDiagonal = sig.MaxEdgeLength > 1e-9 ? bd / sig.MaxEdgeLength : 0
        };
        _templates.Add(template);
    }

    public double GetMaxBoundingDiagonal(string baseName)
    {
        return _templates.Where(t => t.Name.StartsWith(baseName)).Select(t => t.BoundingDiagonal).DefaultIfEmpty(100).Max();
    }

    private static double ComputeBoundingDiagonal(List<MetrajPoint> pts)
    {
        if (pts.Count < 2) return 0;
        double maxSq = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            for (int j = i + 1; j < pts.Count; j++)
            {
                double dx = pts[i].X - pts[j].X;
                double dy = pts[i].Y - pts[j].Y;
                double dSq = dx * dx + dy * dy;
                if (dSq > maxSq) maxSq = dSq;
            }
        }
        return Math.Sqrt(maxSq);
    }

    public IEnumerable<MetrajTemplate> GetSortedTemplates()
    {
        // Büyükten küçüğe sırala (segment sayısı en çok olandan başlayarak)
        // Bu, alt-geometrilerin yanlışlıkla üst-geometriler tarafından tüketilmesini engellemek için önemlidir.
        return _templates.OrderByDescending(t => t.SegmentCount);
    }

    public void Clear() => _templates.Clear();
}
