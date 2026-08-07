using Kst.Acoustics.Metraj.Models;
using Kst.Acoustics.Metraj.Filters;

namespace Kst.Acoustics.Metraj.Metrics;

public class LengthCalculator
{
    public Dictionary<string, double> CalculateByLayer(IEnumerable<MetrajEntity> entities, IMetrajFilter? filter = null)
    {
        var results = new Dictionary<string, double>();
        foreach (var entity in entities)
        {
            if (filter != null && !filter.IsMatch(entity)) continue;

            double len = CalculateLength(entity);
            if (len <= 0) continue;

            if (results.ContainsKey(entity.Layer)) results[entity.Layer] += len;
            else results[entity.Layer] = len;
        }
        return results;
    }

    public Dictionary<string, double> CalculateByColor(IEnumerable<MetrajEntity> entities, IMetrajFilter? filter = null)
    {
        var results = new Dictionary<string, double>();
        foreach (var entity in entities)
        {
            if (filter != null && !filter.IsMatch(entity)) continue;

            double len = CalculateLength(entity);
            if (len <= 0) continue;

            string colorKey = entity.EffectiveColor.Aci.ToString();
            if (entity.EffectiveColor.R.HasValue) 
                colorKey = $"#{entity.EffectiveColor.R:X2}{entity.EffectiveColor.G:X2}{entity.EffectiveColor.B:X2}";

            if (results.ContainsKey(colorKey)) results[colorKey] += len;
            else results[colorKey] = len;
        }
        return results;
    }

    public double CalculateLength(MetrajEntity entity)
    {
        return entity switch
        {
            MetrajLine line => Distance(line.Start, line.End),
            MetrajPolyline poly => CalculatePolylineLength(poly),
            MetrajCircle circle => 2 * Math.PI * circle.Radius,
            MetrajArc arc => CalculateArcLength(arc),
            MetrajEllipse ell => CalculateEllipseLength(ell),
            MetrajSpline spline => CalculateSplineLength(spline),
            _ => 0
        };
    }

    private double Distance(MetrajPoint p1, MetrajPoint p2)
    {
        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        double dz = p2.Z - p1.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private double CalculatePolylineLength(MetrajPolyline poly)
    {
        double total = 0;
        for (int i = 0; i < poly.Vertices.Count - 1; i++)
        {
            total += Distance(poly.Vertices[i], poly.Vertices[i + 1]);
        }
        if (poly.IsClosed && poly.Vertices.Count > 2)
        {
            total += Distance(poly.Vertices.Last(), poly.Vertices.First());
        }
        return total;
    }

    private double CalculateArcLength(MetrajArc arc)
    {
        double start = arc.StartAngle;
        double end = arc.EndAngle;
        double sweep = end - start;
        if (sweep < 0) sweep += 360;
        return (sweep * Math.PI / 180.0) * arc.Radius;
    }

    private double CalculateEllipseLength(MetrajEllipse ell)
    {
        // Ramanujan approximation for full ellipse: PI * (3(a+b) - sqrt((3a+b)(a+3b)))
        // But DXF Ellipse can be an arc.
        // For simplicity, if it's a full ellipse (sweep 360), we use Ramanujan.
        // Otherwise, we approximate with segments (not ideal but common).
        
        double a = Distance(new MetrajPoint(0,0,0), new MetrajPoint(ell.MajorAxisX, ell.MajorAxisY, ell.MajorAxisZ));
        double b = a * ell.Ratio;
        
        double sweep = ell.EndAngle - ell.StartAngle;
        if (sweep <= 0) sweep += 360;

        if (Math.Abs(sweep - 360) < 1e-3)
        {
            return Math.PI * (3 * (a + b) - Math.Sqrt((3 * a + b) * (a + 3 * b)));
        }

        // Partial ellipse: simple segment approximation (8 segments per 90 deg)
        int segments = (int)Math.Max(4, (sweep / 360.0) * 32);
        double total = 0;
        MetrajPoint? prev = null;
        for (int i = 0; i <= segments; i++)
        {
            double ang = (ell.StartAngle + (sweep * i / segments)) * Math.PI / 180.0;
            // Ellipse parametric: x = a*cos(t), y = b*sin(t)
            // Note: This assumes major axis is along X. For rotated ellipse, we should rotate the points.
            // But Distance is invariant to rotation, so we can calculate in local space.
            var p = new MetrajPoint(a * Math.Cos(ang), b * Math.Sin(ang), 0);
            if (prev != null) total += Distance(prev, p);
            prev = p;
        }
        return total;
    }

    private double CalculateSplineLength(MetrajSpline spline)
    {
        // Simple approximation using control points.
        double total = 0;
        for (int i = 0; i < spline.ControlPoints.Count - 1; i++)
        {
            total += Distance(spline.ControlPoints[i], spline.ControlPoints[i + 1]);
        }
        return total;
    }
}
