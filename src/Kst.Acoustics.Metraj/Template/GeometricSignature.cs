using Kst.Acoustics.Metraj.Models;

namespace Kst.Acoustics.Metraj.Template;

public class GeometricSignature
{
    private readonly List<MetrajPoint> _points;
    public List<(double Length, double TurnAngle)> Elements { get; } = new();
    public double MaxEdgeLength { get; private set; }

    public GeometricSignature(IEnumerable<MetrajPoint> points)
    {
        _points = points.ToList();
        if (_points.Count < 2) return;

        var lengths = new List<double>();
        var angles = new List<double>();

        for (int i = 0; i < _points.Count - 1; i++)
        {
            var p1 = _points[i];
            var p2 = _points[i + 1];
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            lengths.Add(Math.Sqrt(dx * dx + dy * dy));
            angles.Add(Math.Atan2(dy, dx) * 180.0 / Math.PI);
        }

        double maxLength = lengths.Max();
        MaxEdgeLength = maxLength;
        if (maxLength < 1e-9) maxLength = 1.0;

        for (int i = 0; i < lengths.Count; i++)
        {
            double turnAngle = 0;
            if (i < lengths.Count - 1)
            {
                turnAngle = angles[i + 1] - angles[i];
                while (turnAngle > 180) turnAngle -= 360;
                while (turnAngle < -180) turnAngle += 360;
            }
            Elements.Add((lengths[i] / maxLength, turnAngle));
        }
    }

    public static bool IsMatch(GeometricSignature s1, GeometricSignature s2, double lengthTolPercent, double angleTolDeg, bool checkScale = true)
    {
        if (s1.Elements.Count != s2.Elements.Count) return false;

        // Check absolute size compatibility (Bounding box diagonal is more stable)
        if (checkScale)
        {
            var diag1 = GetDiagonal(s1._points);
            var diag2 = GetDiagonal(s2._points);
            double scaleRatio = diag1 / diag2;
            if (scaleRatio < 0.97 || scaleRatio > 1.03) return false;
        }

        if (Compare(s1.Elements, s2.Elements, lengthTolPercent, angleTolDeg)) return true;

        var mirrored = s2.Elements.Select(e => (e.Length, -e.TurnAngle)).ToList();
        if (Compare(s1.Elements, mirrored, lengthTolPercent, angleTolDeg)) return true;

        var reversed = new GeometricSignature(Enumerable.Reverse(s2._points)).Elements;
        if (Compare(s1.Elements, reversed, lengthTolPercent, angleTolDeg)) return true;

        var revMirrored = reversed.Select(e => (e.Length, -e.TurnAngle)).ToList();
        if (Compare(s1.Elements, revMirrored, lengthTolPercent, angleTolDeg)) return true;

        return false;
    }

    private static bool Compare(List<(double Length, double TurnAngle)> e1, List<(double Length, double TurnAngle)> e2, double lengthTolPercent, double angleTolDeg)
    {
        for (int i = 0; i < e1.Count; i++)
        {
            double lenDiff = Math.Abs(e1[i].Length - e2[i].Length);
            if (lenDiff > lengthTolPercent / 100.0) return false;

            double angDiff = Math.Abs(e1[i].TurnAngle - e2[i].TurnAngle);
            while (angDiff > 180) angDiff -= 360;
            while (angDiff < -180) angDiff += 360;
            angDiff = Math.Abs(angDiff);
            
            if (angDiff > angleTolDeg) return false;
        }
        return true;
    }

    private static double GetDiagonal(List<MetrajPoint> pts)
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
}
