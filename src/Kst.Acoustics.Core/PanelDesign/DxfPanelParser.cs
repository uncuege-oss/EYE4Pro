using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Kst.Acoustics.Core.PanelDesign;

public record DxfPair(string Code, string Value);
public record DxfEntity { public string Type { get; set; } = ""; public List<DxfPair> Pairs { get; set; } = new(); }

public record DxfOpening(
    double Area,
    double Perimeter,
    double HydraulicDiameter,
    double Circularity,
    double AspectRatio,
    string ShapeType,
    double AreaSquareMeters = 0.0,
    double PerimeterMeters = 0.0,
    double HydraulicDiameterMeters = 0.0)
{
    public DxfOpening WithUnitScale(double unitScaleToMeters)
    {
        if (unitScaleToMeters <= 0 || double.IsNaN(unitScaleToMeters)) return this;
        return this with
        {
            AreaSquareMeters = Area * unitScaleToMeters * unitScaleToMeters,
            PerimeterMeters = Perimeter * unitScaleToMeters,
            HydraulicDiameterMeters = HydraulicDiameter * unitScaleToMeters
        };
    }
}

public record DxfQualityMetrics(
    int ConfidenceScore,
    string ConfidenceLabel,
    double MinHydraulicDiameterMeters,
    double MaxHydraulicDiameterMeters,
    double MeanHydraulicDiameterMeters,
    double MinCircularity,
    double MaxAspectRatio,
    int SlotLikeOpeningCount,
    int FreeShapeOpeningCount,
    int OpenGuideEntityCount,
    IReadOnlyList<string> Warnings);

public record DxfAnalysisResult(
    double TotalArea,
    double OpenArea,
    double OpenAreaRatio,
    int HoleCount,
    double AverageHoleArea,
    double EquivalentRadius,
    IReadOnlyList<DxfEntity> Entities,
    double UnitScaleToMeters = 1.0,
    double TotalAreaSquareMeters = 0.0,
    double OpenAreaSquareMeters = 0.0,
    double EquivalentRadiusMeters = 0.0,
    bool UnitDetected = false,
    int DetectedInsUnitsCode = 0,
    IReadOnlyList<DxfOpening>? Openings = null,
    DxfQualityMetrics? Quality = null,
    int InvalidNumericValueCount = 0)
{
    /// <summary>
    /// Aynı parse sonucundan birim ölçeğini override edip yeniden hesaplanmış metrik
    /// alanları üretir. Geometri (oran, sayı, raw alan) değişmez.
    /// </summary>
    public DxfAnalysisResult WithUnitScale(double newUnitScaleToMeters)
    {
        if (newUnitScaleToMeters <= 0 || double.IsNaN(newUnitScaleToMeters)) return this;
        double areaScale = newUnitScaleToMeters * newUnitScaleToMeters;
        return this with
        {
            UnitScaleToMeters       = newUnitScaleToMeters,
            TotalAreaSquareMeters   = TotalArea * areaScale,
            OpenAreaSquareMeters    = OpenArea * areaScale,
            EquivalentRadiusMeters  = EquivalentRadius * newUnitScaleToMeters,
            Openings                = (Openings ?? []).Select(o => o.WithUnitScale(newUnitScaleToMeters)).ToArray(),
            Quality                 = BuildQualityMetrics(
                Openings?.Select(o => o.WithUnitScale(newUnitScaleToMeters)).ToArray() ?? [],
                OpenAreaRatio,
                Entities,
                newUnitScaleToMeters,
                UnitDetected,
                InvalidNumericValueCount)
        };
    }

    internal static DxfQualityMetrics BuildQualityMetrics(
        IReadOnlyList<DxfOpening> openings,
        double openAreaRatio,
        IReadOnlyList<DxfEntity> entities,
        double unitScaleToMeters,
        bool unitDetected,
        int invalidNumericValueCount = 0)
    {
        var warnings = new List<string>();
        int score = 100;

        int openGuideCount = entities.Count(e => e.Type is "LINE" or "ARC" || !IsEntityClosedForQuality(e));
        int slotLikeCount = openings.Count(o => o.AspectRatio >= 4.0 || o.Circularity < 0.35);
        int freeShapeCount = openings.Count(o => o.ShapeType.Contains("Serbest", StringComparison.OrdinalIgnoreCase));

        if (!unitDetected)
        {
            score -= 10;
            warnings.Add("DXF birimi dosyadan kesin okunamadı; kullanıcı seçimiyle hesaplandı.");
        }
        if (invalidNumericValueCount > 0)
        {
            score -= 10;
            warnings.Add("Bazı sayısal DXF değerleri okunamadı; ilgili geometri atlandı veya 0 kabul edildi.");
        }
        if (openAreaRatio > 0.45)
        {
            score -= 20;
            warnings.Add("Açık alan oranı %45 üzerinde; delikli levha yaklaşımının doğruluğu azalabilir.");
        }
        if (openAreaRatio < 0.005 && openings.Count > 0)
        {
            score -= 10;
            warnings.Add("Açık alan oranı çok düşük; üretim toleransı ve tıkanma etkileri sonucu belirgin etkileyebilir.");
        }
        if (slotLikeCount > 0)
        {
            score -= Math.Min(20, 5 + slotLikeCount * 2);
            warnings.Add($"{slotLikeCount} açıklık slot/serbest forma yakın; hidrolik çap yaklaşımı kullanıldı.");
        }
        if (freeShapeCount > 0)
        {
            score -= Math.Min(15, freeShapeCount * 2);
            warnings.Add($"{freeShapeCount} serbest biçimli açıklık var; sonuç mühendislik yaklaşımıdır.");
        }
        if (openGuideCount > 0)
        {
            score -= 5;
            warnings.Add($"{openGuideCount} açık rehber çizgi/arc hesap alanına katılmadı.");
        }

        double minDh = openings.Count > 0 ? openings.Min(o => o.HydraulicDiameterMeters) : 0.0;
        double maxDh = openings.Count > 0 ? openings.Max(o => o.HydraulicDiameterMeters) : 0.0;
        double meanDh = openings.Count > 0 ? openings.Average(o => o.HydraulicDiameterMeters) : 0.0;
        double minCircularity = openings.Count > 0 ? openings.Min(o => o.Circularity) : 0.0;
        double maxAspectRatio = openings.Count > 0 ? openings.Max(o => o.AspectRatio) : 0.0;

        if (openings.Count > 0 && minDh < 0.001)
        {
            score -= 15;
            warnings.Add("Minimum hidrolik çap 1 mm altında; viskoz kayıplar ve üretim toleransı çok baskın olabilir.");
        }
        if (openings.Count > 1 && maxDh > 0 && minDh > 0 && maxDh / minDh > 6.0)
        {
            score -= 10;
            warnings.Add("Açıklık boyutları arasında 6x üzeri dağılım var; tek panel davranışı daha heterojen olabilir.");
        }
        if (openings.Count > 0 && minCircularity < 0.20)
        {
            score -= 10;
            warnings.Add("Çok düşük dairesellikli açıklık var; tam dalga/FEA doğrulaması önerilir.");
        }
        if (unitScaleToMeters <= 0)
        {
            score = 0;
            warnings.Add("Geçersiz birim ölçeği.");
        }

        score = Math.Clamp(score, 0, 100);
        string label = score >= 85 ? "Yüksek" : score >= 65 ? "Orta-Yüksek" : score >= 45 ? "Orta" : "Düşük";
        return new DxfQualityMetrics(
            score,
            label,
            minDh,
            maxDh,
            meanDh,
            minCircularity,
            maxAspectRatio,
            slotLikeCount,
            freeShapeCount,
            openGuideCount,
            warnings);
    }

    private static bool IsEntityClosedForQuality(DxfEntity entity)
    {
        if (entity.Type is "CIRCLE" or "3DFACE") return true;
        if (entity.Type is "LINE" or "ARC") return false;

        var flagsPair = entity.Pairs.FirstOrDefault(p => p.Code == "70");
        if (flagsPair != null && int.TryParse(flagsPair.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var flags))
            return (flags & 1) == 1;

        if (entity.Type == "ELLIPSE")
        {
            double start = GetPairDouble(entity.Pairs, "41");
            double end = GetPairDouble(entity.Pairs, "42");
            return Math.Abs((end - start) - Math.PI * 2.0) < 0.01 || end <= start;
        }

        return false;
    }

    private static double GetPairDouble(List<DxfPair> pairs, string code)
    {
        var pair = pairs.FirstOrDefault(p => p.Code == code);
        return pair != null && double.TryParse(pair.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0.0;
    }
}

/// <summary>
/// Specialized parser for analyzing perforated panel patterns from DXF files.
/// Calculates total area and open area ratio.
/// </summary>
public class DxfPanelParser
{
    private int _invalidNumericValueCount;

    public DxfAnalysisResult Parse(string dxfContent)
    {
        _invalidNumericValueCount = 0;
        var pairs = ParsePairs(dxfContent);
        var (unitScale, detected, code) = GetUnitScaleToMetersDetailed(pairs);

        var entities = new List<DxfEntity>();
        var polygonNodes = new List<PolyNode>();
        var allExtents = new List<(double MinX, double MaxX, double MinY, double MaxY)>();

        for (int i = 0; i < pairs.Count; i++)
        {
            if (pairs[i].Code == "0")
            {
                string type = pairs[i].Value.ToUpper();
                if (type == "CIRCLE" || type == "ARC" || type == "ELLIPSE" || type == "SPLINE" ||
                    type == "LWPOLYLINE" || type == "POLYLINE" || type == "LINE" || type == "3DFACE")
                {
                    var entityPairs = new List<DxfPair>();
                    int j = i + 1;
                    while (j < pairs.Count && pairs[j].Code != "0")
                    {
                        entityPairs.Add(pairs[j]);
                        j++;
                    }
                    
                    if (type == "POLYLINE")
                    {
                        while (j < pairs.Count && pairs[j].Value.ToUpper() != "SEQEND")
                        {
                            entityPairs.Add(pairs[j]);
                            j++;
                        }
                    }

                    var entity = new DxfEntity { Type = type, Pairs = entityPairs };
                    entities.Add(entity);

                    // Immediate analysis
                    double area = CalculateArea(entity);
                    var ext = GetExtents(entity);
                    if (ext.HasValue) allExtents.Add(ext.Value);

                    if (area > 0 && IsClosedAreaEntity(entity))
                    {
                        var pts = GetPoints(entity);
                        if (pts.Count >= 3)
                        {
                            polygonNodes.Add(new PolyNode(pts, area, CalculatePerimeter(entity, pts), ClassifyShape(entity, pts)));
                        }
                    }

                    i = j - 1;
                }
            }
        }

        if (!entities.Any()) return EmptyResult(new List<DxfEntity>(), unitScale, detected, code, _invalidNumericValueCount);

        if (!polygonNodes.Any())
        {
            if (!allExtents.Any()) return EmptyResult(entities, unitScale, detected, code, _invalidNumericValueCount);
            double minX = allExtents.Min(e => e.MinX);
            double maxX = allExtents.Max(e => e.MaxX);
            double minY = allExtents.Min(e => e.MinY);
            double maxY = allExtents.Max(e => e.MaxY);
            double totalArea = (maxX - minX) * (maxY - minY);
            return new DxfAnalysisResult(
                totalArea, 0, 0, 0, 0, 0, entities, unitScale,
                totalArea * unitScale * unitScale, 0, 0, detected, code, [],
                DxfAnalysisResult.BuildQualityMetrics([], 0, entities, unitScale, detected, _invalidNumericValueCount),
                _invalidNumericValueCount);
        }

        // Build hierarchy tree
        var nodes = polygonNodes.OrderByDescending(x => x.Area).ToList();
        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = 0; j < nodes.Count; j++)
            {
                if (i == j) continue;
                if (IsPolygonInside(nodes[i].Points, nodes[j].Points))
                {
                    // Update to smaller container if found
                    if (nodes[i].Parent == null || nodes[j].Area < nodes[i].Parent!.Area)
                    {
                        nodes[i].Parent = nodes[j];
                    }
                }
            }
        }

        // Add children to parents
        foreach (var node in nodes.Where(n => n.Parent != null))
        {
            node.Parent!.Children.Add(node);
        }

        // Codex rule: the largest outer closed shape is the panel boundary.
        // Other closed shapes outside that boundary are treated as auxiliary geometry.
        var panelBoundary = nodes
            .Where(n => n.Parent == null)
            .OrderByDescending(n => n.Area)
            .FirstOrDefault() ?? nodes[0];
        double totalPanelArea = panelBoundary.Area;
        var openings = new List<DxfOpening>();

        foreach (var node in nodes)
        {
            if (node == panelBoundary || !node.IsDescendantOf(panelBoundary))
            {
                continue;
            }

            int relativeDepth = node.GetDepth() - panelBoundary.GetDepth();
            if (relativeDepth % 2 == 1) // Hole
            {
                double netArea = Math.Max(0, node.Area - node.Children.Sum(c => c.Area));
                double wetPerimeter = node.Perimeter + node.Children.Sum(c => c.Perimeter);
                if (netArea > 0 && wetPerimeter > 0)
                {
                    double hydraulicDiameter = 4.0 * netArea / wetPerimeter;
                    double circularity = 4.0 * Math.PI * netArea / (wetPerimeter * wetPerimeter);
                    openings.Add(new DxfOpening(
                        netArea,
                        wetPerimeter,
                        hydraulicDiameter,
                        Math.Clamp(circularity, 0.0, 1.0),
                        node.AspectRatio,
                        node.ShapeType).WithUnitScale(unitScale));
                }
            }
        }

        double openArea = openings.Sum(o => o.Area);
        int holeCount = openings.Count;
        double avgHoleArea = holeCount > 0 ? openArea / holeCount : 0;
        
        double denominator = openings.Where(o => o.Area > 0).Sum(o => Math.Sqrt(Math.PI * o.Area));
        double eqRadius = denominator > 0 ? openArea / denominator : 0;

        double ratio = totalPanelArea > 0
            ? Math.Clamp(openArea / totalPanelArea, 0.0, PerforatedLayer.MaxOpenAreaRatio)
            : 0;
        double areaScale = unitScale * unitScale;

        return new DxfAnalysisResult(
            totalPanelArea,
            openArea,
            ratio,
            holeCount,
            avgHoleArea,
            eqRadius,
            entities,
            unitScale,
            totalPanelArea * areaScale,
            openArea * areaScale,
            eqRadius * unitScale,
            detected,
            code,
            openings,
            DxfAnalysisResult.BuildQualityMetrics(openings, ratio, entities, unitScale, detected, _invalidNumericValueCount),
            _invalidNumericValueCount);
    }

    private class PolyNode
    {
        public List<(double X, double Y)> Points { get; }
        public double Area { get; }
        public double Perimeter { get; }
        public string ShapeType { get; }
        public double AspectRatio { get; }
        public PolyNode? Parent { get; set; }
        public List<PolyNode> Children { get; } = new();

        public PolyNode(List<(double X, double Y)> points, double area, double perimeter, string shapeType)
        {
            Points = points;
            Area = area;
            Perimeter = perimeter;
            ShapeType = shapeType;
            AspectRatio = CalculateAspectRatio(points);
        }

        public int GetDepth()
        {
            int d = 0;
            var p = Parent;
            while (p != null) { d++; p = p.Parent; }
            return d;
        }

        public bool IsDescendantOf(PolyNode ancestor)
        {
            var p = Parent;
            while (p != null)
            {
                if (ReferenceEquals(p, ancestor)) return true;
                p = p.Parent;
            }

            return false;
        }

        private static double CalculateAspectRatio(IReadOnlyList<(double X, double Y)> points)
        {
            if (points.Count == 0) return 1.0;
            double width = points.Max(p => p.X) - points.Min(p => p.X);
            double height = points.Max(p => p.Y) - points.Min(p => p.Y);
            double min = Math.Max(Math.Min(width, height), 1e-9);
            return Math.Max(width, height) / min;
        }
    }

    private bool IsPolygonInside(List<(double X, double Y)> inner, List<(double X, double Y)> outer)
    {
        // Check if all points of inner are inside outer (simple check)
        foreach (var p in inner)
        {
            if (!IsPointInPolygon(p, outer)) return false;
        }
        return true;
    }

    private bool IsPointInPolygon((double X, double Y) p, List<(double X, double Y)> poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            if (((poly[i].Y > p.Y) != (poly[j].Y > p.Y)) &&
                (p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X))
            {
                inside = !inside;
            }
        }
        return inside;
    }


    private double CalculateArea(DxfEntity entity)
    {
        switch (entity.Type)
        {
            case "CIRCLE":
                double r = GetDouble(entity.Pairs, "40");
                return Math.PI * r * r;

            case "ELLIPSE":
            case "SPLINE":
            case "LWPOLYLINE":
            case "POLYLINE":
            case "3DFACE":
                var points = GetPoints(entity);
                return Math.Abs(CalculatePolygonArea(points));

            case "ARC":
            case "LINE":
                // Open entities have no acoustic opening area; they are kept only for preview extents.
                return 0.0;

            default:
                return 0;
        }
    }

    private double CalculatePolygonArea(List<(double X, double Y)> points)
    {
        if (points.Count < 3) return 0;
        double area = 0;
        for (int i = 0; i < points.Count; i++)
        {
            int j = (i + 1) % points.Count;
            area += points[i].X * points[j].Y;
            area -= points[j].X * points[i].Y;
        }
        // Return signed area (positive for CCW, negative for CW)
        return area / 2.0;
    }

    private double CalculatePerimeter(DxfEntity entity, IReadOnlyList<(double X, double Y)> points)
    {
        if (entity.Type == "CIRCLE")
        {
            double r = GetDouble(entity.Pairs, "40");
            return r > 0 ? 2.0 * Math.PI * r : CalculatePolylineLength(points, true);
        }

        if (entity.Type == "ELLIPSE" || IsClosedAreaEntity(entity))
        {
            return CalculatePolylineLength(points, true);
        }

        return CalculatePolylineLength(points, false);
    }

    private static double CalculatePolylineLength(IReadOnlyList<(double X, double Y)> points, bool closed)
    {
        if (points.Count < 2) return 0.0;
        double length = 0.0;
        for (int i = 0; i < points.Count - 1; i++)
            length += Distance(points[i], points[i + 1]);
        if (closed)
            length += Distance(points[^1], points[0]);
        return length;
    }

    private string ClassifyShape(DxfEntity entity, IReadOnlyList<(double X, double Y)> points)
    {
        if (entity.Type == "CIRCLE") return "Dairesel";
        if (entity.Type == "ELLIPSE") return "Eliptik";

        if (points.Count >= 3)
        {
            double area = Math.Abs(CalculatePolygonArea(points.ToList()));
            double perimeter = CalculatePolylineLength(points, true);
            double circularity = perimeter > 0 ? 4.0 * Math.PI * area / (perimeter * perimeter) : 0.0;
            double width = points.Max(p => p.X) - points.Min(p => p.X);
            double height = points.Max(p => p.Y) - points.Min(p => p.Y);
            double aspect = Math.Max(width, height) / Math.Max(Math.Min(width, height), 1e-9);

            if (aspect >= 4.0 || circularity < 0.35) return "Slot/Serbest";
            if (circularity > 0.85) return "Daireye Yakın";
        }

        return "Poligon/Serbest";
    }

    private List<(double X, double Y)> GetPoints(DxfEntity entity)
    {
        if (entity.Type == "CIRCLE")
        {
            return GetCirclePoints(entity.Pairs);
        }
        if (entity.Type == "ARC")
        {
            return GetArcPoints(entity.Pairs);
        }
        if (entity.Type == "ELLIPSE")
        {
            return GetEllipsePoints(entity.Pairs);
        }
        if (entity.Type == "SPLINE")
        {
            return GetSplinePoints(entity.Pairs);
        }
        if (entity.Type == "3DFACE")
        {
            return Get3DFacePoints(entity.Pairs);
        }

        return GetPoints(entity.Pairs);
    }

    private List<(double X, double Y)> GetCirclePoints(List<DxfPair> pairs)
    {
        double x = GetDouble(pairs, "10");
        double y = GetDouble(pairs, "20");
        double r = GetDouble(pairs, "40");
        var points = new List<(double X, double Y)>();
        if (r <= 0) return points;

        const int segments = 48;
        for (var i = 0; i < segments; i++)
        {
            double angle = 2.0 * Math.PI * i / segments;
            points.Add((x + r * Math.Cos(angle), y + r * Math.Sin(angle)));
        }

        return points;
    }

    private List<(double X, double Y)> GetArcPoints(List<DxfPair> pairs)
    {
        double x = GetDouble(pairs, "10");
        double y = GetDouble(pairs, "20");
        double r = GetDouble(pairs, "40");
        double start = DegreesToRadians(GetDouble(pairs, "50"));
        double end = DegreesToRadians(GetDouble(pairs, "51"));
        var points = new List<(double X, double Y)>();
        if (r <= 0) return points;

        if (end < start) end += Math.PI * 2.0;
        int segments = Math.Clamp((int)Math.Ceiling((end - start) / (Math.PI / 24.0)), 6, 96);
        for (var i = 0; i <= segments; i++)
        {
            double angle = start + (end - start) * i / segments;
            points.Add((x + r * Math.Cos(angle), y + r * Math.Sin(angle)));
        }

        return points;
    }

    private List<(double X, double Y)> GetEllipsePoints(List<DxfPair> pairs)
    {
        double cx = GetDouble(pairs, "10");
        double cy = GetDouble(pairs, "20");
        double majorX = GetDouble(pairs, "11");
        double majorY = GetDouble(pairs, "21");
        double ratio = GetDouble(pairs, "40");
        double start = GetDouble(pairs, "41");
        double end = GetDouble(pairs, "42");
        if (ratio <= 0) ratio = 1.0;
        if (end <= start) end += Math.PI * 2.0;

        double minorX = -majorY * ratio;
        double minorY = majorX * ratio;
        int segments = Math.Clamp((int)Math.Ceiling((end - start) / (Math.PI / 48.0)), 24, 144);
        var points = new List<(double X, double Y)>();
        for (int i = 0; i <= segments; i++)
        {
            double t = start + (end - start) * i / segments;
            points.Add((
                cx + majorX * Math.Cos(t) + minorX * Math.Sin(t),
                cy + majorY * Math.Cos(t) + minorY * Math.Sin(t)));
        }

        return points;
    }

    private List<(double X, double Y)> GetSplinePoints(List<DxfPair> pairs)
    {
        var fitPoints = GetPairedPoints(pairs, "11", "21");
        if (fitPoints.Count >= 2) return fitPoints;
        return GetPairedPoints(pairs, "10", "20");
    }

    private List<(double X, double Y)> Get3DFacePoints(List<DxfPair> pairs)
    {
        var points = new List<(double X, double Y)>();
        for (int i = 0; i < 4; i++)
        {
            string xCode = (10 + i).ToString(CultureInfo.InvariantCulture);
            string yCode = (20 + i).ToString(CultureInfo.InvariantCulture);
            if (TryGetPairDouble(pairs, xCode, out var x) &&
                TryGetPairDouble(pairs, yCode, out var y))
            {
                AddDistinctPoint(points, (x, y));
            }
        }

        if (points.Count > 2 && AreSamePoint(points[0], points[^1]))
            points.RemoveAt(points.Count - 1);

        return points;
    }

    private static void AddDistinctPoint(List<(double X, double Y)> points, (double X, double Y) point)
    {
        if (points.Count == 0 || !AreSamePoint(points[^1], point))
            points.Add(point);
    }

    private static bool AreSamePoint((double X, double Y) a, (double X, double Y) b)
    {
        return Math.Abs(a.X - b.X) < 1e-9 && Math.Abs(a.Y - b.Y) < 1e-9;
    }

    private bool TryGetPairDouble(List<DxfPair> pairs, string code, out double value)
    {
        var pair = pairs.FirstOrDefault(p => p.Code == code);
        if (pair == null)
        {
            value = 0;
            return false;
        }

        return TryParseDouble(pair.Value, out value);
    }

    private List<(double X, double Y)> GetPairedPoints(List<DxfPair> pairs, string xCode, string yCode)
    {
        var points = new List<(double X, double Y)>();
        double? x = null;
        foreach (var pair in pairs)
        {
            if (pair.Code == xCode)
            {
                x = TryParseDouble(pair.Value, out var parsedX) ? parsedX : null;
            }
            else if (pair.Code == yCode && x.HasValue &&
                     TryParseDouble(pair.Value, out var parsedY))
            {
                points.Add((x.Value, parsedY));
                x = null;
            }
        }
        return points;
    }

    private List<(double X, double Y)> GetPoints(List<DxfPair> pairs)
    {
        var points = new List<(double X, double Y)>();
        double? currentX = null;
        double? currentY = null;
        double? currentBulge = null;

        var vertexData = new List<(double X, double Y, double Bulge)>();

        for (int i = 0; i < pairs.Count; i++)
        {
            var p = pairs[i];
            if (p.Code == "10") currentX = TryParseDouble(p.Value, out var parsedX) ? parsedX : null;
            if (p.Code == "20" && currentX.HasValue)
            {
                if (!TryParseDouble(p.Value, out var parsedY))
                {
                    currentX = null;
                    continue;
                }

                currentY = parsedY;
                // In LWPOLYLINE, 42 (bulge) comes after 10/20 for each vertex
                currentBulge = 0;
                for (int k = i + 1; k < Math.Min(i + 5, pairs.Count); k++)
                {
                    if (pairs[k].Code == "42")
                    {
                        currentBulge = TryParseDouble(pairs[k].Value, out var parsedBulge) ? parsedBulge : 0;
                        break;
                    }
                    if (pairs[k].Code == "10" || pairs[k].Code == "0") break;
                }

                vertexData.Add((currentX.Value, currentY.Value, currentBulge.Value));
                currentX = null;
                currentY = null;
            }
        }

        if (vertexData.Count == 0) return points;

        for (int i = 0; i < vertexData.Count; i++)
        {
            var v1 = vertexData[i];
            points.Add((v1.X, v1.Y));

            if (v1.Bulge != 0)
            {
                var v2 = vertexData[(i + 1) % vertexData.Count];
                TessellateBulge(points, v1.X, v1.Y, v2.X, v2.Y, v1.Bulge);
            }
        }

        return points;
    }

    private void TessellateBulge(List<(double X, double Y)> points, double x1, double y1, double x2, double y2, double bulge)
    {
        double dist = Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
        if (dist < 1e-9) return;

        // Bulge to angle: bulge = tan(theta/4)
        double theta = 4 * Math.Atan(bulge);
        double sagitta = dist / 2.0 * bulge;
        double radius = (dist / 2.0) * (1 + bulge * bulge) / (2 * bulge);

        double midX = (x1 + x2) / 2.0;
        double midY = (y1 + y2) / 2.0;
        double dx = x2 - x1;
        double dy = y2 - y1;

        // Center of arc
        double dCenter = radius - sagitta;
        double cx = midX - dy * (dCenter / dist);
        double cy = midY + dx * (dCenter / dist);

        double startAngle = Math.Atan2(y1 - cy, x1 - cx);
        int segments = 8; // Number of segments for the arc
        for (int i = 1; i < segments; i++)
        {
            double angle = startAngle + (theta * i / segments);
            points.Add((cx + radius * Math.Cos(angle), cy + radius * Math.Sin(angle)));
        }
    }

    private double GetDouble(List<DxfPair> pairs, string code)
    {
        var pair = pairs.FirstOrDefault(p => p.Code == code);
        return pair != null && TryParseDouble(pair.Value, out var value) ? value : 0;
    }

    private bool TryParseDouble(string value, out double parsed)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) &&
            double.IsFinite(parsed))
        {
            return true;
        }

        _invalidNumericValueCount++;
        parsed = 0;
        return false;
    }

    private List<DxfPair> ParsePairs(string text)
    {
        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
                        .Select(l => l.Trim())
                        .Where(l => !string.IsNullOrEmpty(l))
                        .ToList();
        var pairs = new List<DxfPair>();
        for (int i = 0; i < lines.Count - 1; i += 2)
        {
            pairs.Add(new DxfPair(lines[i], lines[i+1]));
        }
        return pairs;
    }

    private static DxfAnalysisResult EmptyResult(
        IReadOnlyList<DxfEntity> entities,
        double unitScale,
        bool detected = false,
        int code = 0,
        int invalidNumericValueCount = 0)
    {
        return new DxfAnalysisResult(
            0, 0, 0, 0, 0, 0, entities, unitScale, 0, 0, 0, detected, code, [],
            DxfAnalysisResult.BuildQualityMetrics([], 0, entities, unitScale, detected, invalidNumericValueCount),
            invalidNumericValueCount);
    }

    private bool IsBoundaryCandidate(DxfEntity entity)
    {
        return entity.Type is "LWPOLYLINE" or "POLYLINE" or "3DFACE";
    }

    private bool IsHoleCandidate(DxfEntity entity, double area, double totalArea)
    {
        if (area <= 0 || area >= totalArea * 0.98)
        {
            return false;
        }

        return entity.Type is "CIRCLE" or "ELLIPSE" or "SPLINE" or "LWPOLYLINE" or "POLYLINE" or "3DFACE";
    }

    private bool IsClosedAreaEntity(DxfEntity entity)
    {
        if (entity.Type is "CIRCLE" or "3DFACE") return true;
        if (entity.Type is "ELLIPSE")
        {
            double start = GetDouble(entity.Pairs, "41");
            double end = GetDouble(entity.Pairs, "42");
            return Math.Abs((end - start) - Math.PI * 2.0) < 0.01 || end <= start;
        }
        if (entity.Type is "SPLINE")
        {
            int flags = (int)GetDouble(entity.Pairs, "70");
            return (flags & 1) == 1 || IsNearlyClosed(GetPoints(entity));
        }
        if (entity.Type is "LWPOLYLINE" or "POLYLINE")
        {
            int flags = (int)GetDouble(entity.Pairs, "70");
            return (flags & 1) == 1 || IsNearlyClosed(GetPoints(entity));
        }

        return false;
    }

    private static bool IsNearlyClosed(IReadOnlyList<(double X, double Y)> points)
    {
        if (points.Count < 3) return false;
        var first = points[0];
        var last = points[^1];
        double dx = first.X - last.X;
        double dy = first.Y - last.Y;
        return Math.Sqrt(dx * dx + dy * dy) < 1e-6;
    }

    private (double MinX, double MaxX, double MinY, double MaxY)? GetExtents(DxfEntity entity)
    {
        if (entity.Type == "CIRCLE")
        {
            double x = GetDouble(entity.Pairs, "10");
            double y = GetDouble(entity.Pairs, "20");
            double r = GetDouble(entity.Pairs, "40");
            return (x - r, x + r, y - r, y + r);
        }

        var points = GetPoints(entity);
        if (points.Count == 0)
        {
            return null;
        }

        return (
            points.Min(p => p.X),
            points.Max(p => p.X),
            points.Min(p => p.Y),
            points.Max(p => p.Y));
    }

    private double GetUnitScaleToMeters(List<DxfPair> pairs)
        => GetUnitScaleToMetersDetailed(pairs).Scale;

    /// <summary>
    /// Birim ölçeğini, gerçekten DXF'ten okunup okunmadığını ve $INSUNITS kodunu döner.
    /// UI, INSUNITS yok veya 0 (Unitless) ise kullanıcıya onay diyaloğu gösterir.
    /// </summary>
    private (double Scale, bool Detected, int Code) GetUnitScaleToMetersDetailed(List<DxfPair> pairs)
    {
        for (int i = 0; i < pairs.Count - 1; i++)
        {
            if (pairs[i].Code == "9" && pairs[i].Value.Equals("$INSUNITS", StringComparison.OrdinalIgnoreCase))
            {
                for (int j = i + 1; j < Math.Min(i + 8, pairs.Count); j++)
                {
                    if (pairs[j].Code == "70" && int.TryParse(pairs[j].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int unit))
                    {
                        if (unit == 0) return (0.001, false, 0); // Unitless → bilinmiyor
                        return TryUnitCodeToMeters(unit, out var scale)
                            ? (scale, true, unit)
                            : (0.001, false, unit);
                    }
                }
            }
        }

        // INSUNITS yok: panel şablonlarında mm yaygın ama yine de kullanıcı onayı şart.
        return (0.001, false, 0);
    }

    private static bool TryUnitCodeToMeters(int unit, out double scale)
    {
        scale = unit switch
        {
            1 => 0.0254,       // inches
            2 => 0.3048,       // feet
            3 => 1609.344,     // miles
            4 => 0.001,        // millimeters
            5 => 0.01,         // centimeters
            6 => 1.0,          // metres
            7 => 1000.0,       // kilometers
            8 => 2.54e-8,      // microinches
            9 => 0.0000254,    // mils
            10 => 0.9144,      // yards
            11 => 1e-10,       // angstroms
            12 => 1e-9,        // nanometers
            13 => 1e-6,        // microns
            14 => 0.1,         // decimetres
            15 => 10.0,        // decameters
            16 => 100.0,       // hectometers
            17 => 1e9,         // gigameters
            18 => 149_597_870_700.0,       // astronomical units
            19 => 9_460_730_472_580_800.0, // light years
            20 => 30_856_775_814_913_672.0, // parsecs
            _ => double.NaN
        };
        return double.IsFinite(scale);
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static double Distance((double X, double Y) a, (double X, double Y) b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

}
