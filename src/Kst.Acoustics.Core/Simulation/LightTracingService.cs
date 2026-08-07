using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.Core.Simulation;

public static class LightTracingService
{
    private const double Eps = 1e-8;

    private readonly record struct LTri(Point3D V0, Point3D V1, Point3D V2, int SurfaceId);
    private readonly record struct Vec3(double X, double Y, double Z);

    // ── Dış API ───────────────────────────────────────────────────────────────

    public static LightTracingResult Run(
        Point3D                       source,
        IReadOnlySet<int>             mirrorIds,
        IReadOnlySet<int>             receiverIds,
        IReadOnlyList<ProjectSurface> surfaces,
        IReadOnlyDictionary<string, string>? categoryMaterials = null,
        int                           rayCount        = 2000,
        int                           maxBounces      = 5,
        int                           maxDisplayPaths = 300)
    {
        var tris = Triangulate(surfaces);
        if (tris.Length == 0)
            return new LightTracingResult(
                new Dictionary<int, double>(), [], [], mirrorIds, 0, 0);

        var receiverTris    = receiverIds.Count > 0
            ? tris.Where(t => receiverIds.Contains(t.SurfaceId)).ToArray()
            : mirrorIds.Count > 0
                ? tris.Where(t => !mirrorIds.Contains(t.SurfaceId)).ToArray()
                : tris;
        var receiverAreas   = receiverTris.Select(TriArea).ToArray();
        var totalReceiverArea = receiverAreas.Sum();

        var mirrorTris      = tris.Where(t => mirrorIds.Contains(t.SurfaceId)).ToArray();
        var mirrorAreas     = mirrorTris.Select(TriArea).ToArray();
        var totalMirrorArea = mirrorAreas.Sum();
        var reflectanceBySurface = BuildReflectanceMap(surfaces, categoryMaterials ?? new Dictionary<string, string>());

        var surfHitMap = new Dictionary<int, double>();
        var hitPoints  = new List<LightHitPoint>(rayCount);
        var hitLock    = new object();

        var pathBuf  = new List<RaySegment>(maxDisplayPaths * (maxBounces + 1));
        var pathLock = new object();
        int saved    = 0;
        int seedCtr  = Environment.TickCount;

        Parallel.For(0, rayCount,
            () => new Random(Interlocked.Increment(ref seedCtr)),
            (_, _, rng) =>
            {
                var (segs, hitId, hitPt, hitWeight, mirrorBounceCount) =
                    Trace(source, rng, tris, mirrorTris, mirrorAreas, totalMirrorArea,
                          receiverTris, receiverAreas, totalReceiverArea, mirrorIds,
                          reflectanceBySurface, maxBounces);

                bool validReceiver = hitId.HasValue &&
                                      (receiverIds.Count > 0
                                          ? receiverIds.Contains(hitId.Value)
                                          : mirrorIds.Count == 0 || !mirrorIds.Contains(hitId.Value));
                bool valid = hitId.HasValue
                             && validReceiver
                             && (mirrorIds.Count == 0 || mirrorBounceCount > 0);

                if (valid && hitId.HasValue)
                    lock (hitLock)
                    {
                        surfHitMap.TryGetValue(hitId.Value, out var weight);
                        surfHitMap[hitId.Value] = weight + hitWeight;
                        hitPoints.Add(new LightHitPoint(hitPt, hitWeight, hitId.Value));
                    }

                if (valid && segs is { Count: > 0 })
                    lock (pathLock)
                        if (saved < maxDisplayPaths)
                        { pathBuf.AddRange(segs); saved++; }

                return rng;
            },
            _ => { });

        var maxSurf       = surfHitMap.Count > 0 ? surfHitMap.Values.Max() : 1;
        var surfIntensity = surfHitMap.ToDictionary(kv => kv.Key, kv => kv.Value / maxSurf);
        var rayPaths      = NormalizePathWeights(pathBuf);

        return new LightTracingResult(surfIntensity, hitPoints, rayPaths, mirrorIds,
            rayCount, hitPoints.Count);
    }

    // ── Tek ışın ─────────────────────────────────────────────────────────────

    private static (List<RaySegment>? Segs, int? HitId, Point3D HitPoint, double HitWeight, int MirrorBounceCount)
        Trace(
            Point3D           origin,
            Random            rng,
            LTri[]            allTris,
            LTri[]            mirrorTris,
            double[]          mirrorAreas,
            double            totalMirrorArea,
            LTri[]            receiverTris,
            double[]          receiverAreas,
            double            totalReceiverArea,
            IReadOnlySet<int> mirrorIds,
            IReadOnlyDictionary<int, double> reflectanceBySurface,
            int               maxBounces)
    {
        if (mirrorTris.Length > 0 && receiverTris.Length > 0 && totalMirrorArea > 0 && totalReceiverArea > 0 && maxBounces >= 1 &&
            TryTraceSpecularMirror(origin, rng, allTris,
                mirrorTris, mirrorAreas, totalMirrorArea,
                receiverTris, receiverAreas, totalReceiverArea,
                reflectanceBySurface,
                out var mirrorSegs, out var mirrorHitId, out var mirrorHitPoint, out var mirrorHitWeight))
        {
            return (mirrorSegs, mirrorHitId, mirrorHitPoint, mirrorHitWeight, 1);
        }

        var segs       = new List<RaySegment>(maxBounces + 1);
        var pos        = origin;
        var pathLength  = 0.0;
        var energy      = 1.0;
        var mirrorBounceCount = 0;

        Vec3 dir;
        var aimReceiverDirect = receiverTris.Length > 0
                                && totalReceiverArea > 0
                                && mirrorTris.Length == 0;

        if (aimReceiverDirect)
        {
            var pt = SampleTriPoint(rng, receiverTris, receiverAreas, totalReceiverArea);
            dir = Normalize(new Vec3(pt.X - pos.X, pt.Y - pos.Y, pt.Z - pos.Z));
        }
        else if (mirrorTris.Length > 0 && totalMirrorArea > 0)
        {
            var pt = SampleTriPoint(rng, mirrorTris, mirrorAreas, totalMirrorArea);
            dir = Normalize(new Vec3(pt.X - pos.X, pt.Y - pos.Y, pt.Z - pos.Z));
        }
        else
        {
            dir = RandDir(rng);
        }

        for (int b = 0; b <= maxBounces; b++)
        {
            if (!Nearest(pos, dir, allTris, out var t, out var ti))
                return (segs.Count > 0 ? segs : null, null, default, 0, mirrorBounceCount);

            var tri    = allTris[ti];
            var hitPt  = new Point3D(pos.X + dir.X * t, pos.Y + dir.Y * t, pos.Z + dir.Z * t);
            pathLength += Math.Max(t, 1e-6);
            var distanceWeight = DistanceWeight(pathLength);
            segs.Add(new RaySegment(pos, hitPt, energy * distanceWeight));

            bool isMirror = mirrorIds.Contains(tri.SurfaceId);

            if (!isMirror || b == maxBounces)
                return (segs, tri.SurfaceId, hitPt, energy * distanceWeight, mirrorBounceCount);

            mirrorBounceCount++;

            energy *= reflectanceBySurface.TryGetValue(tri.SurfaceId, out var reflectance)
                ? reflectance
                : 0.95;

            if (energy <= 1e-8)
                return (segs, null, default, 0, mirrorBounceCount);

            var n      = Normal(tri);
            var dot    = dir.X * n.X + dir.Y * n.Y + dir.Z * n.Z;
            var specDir = Normalize(new Vec3(dir.X - 2*dot*n.X, dir.Y - 2*dot*n.Y, dir.Z - 2*dot*n.Z));

            if (receiverTris.Length > 0 && totalReceiverArea > 0 && rng.NextDouble() < 0.65)
            {
                var pt2 = SampleTriPoint(rng, receiverTris, receiverAreas, totalReceiverArea);
                dir = Normalize(new Vec3(pt2.X - hitPt.X, pt2.Y - hitPt.Y, pt2.Z - hitPt.Z));
            }
            // %30 ihtimalle başka bir ayna yüzeyine doğru yeniden biasla →
            // 2. mertebe yansıma yollarının bulunma olasılığını artırır.
            else if (mirrorTris.Length > 0 && totalMirrorArea > 0 && rng.NextDouble() < 0.30)
            {
                var pt2 = SampleTriPoint(rng, mirrorTris, mirrorAreas, totalMirrorArea);
                dir = Normalize(new Vec3(pt2.X - hitPt.X, pt2.Y - hitPt.Y, pt2.Z - hitPt.Z));
            }
            else
            {
                dir = specDir;
            }
            pos = hitPt;
        }

        return (segs.Count > 0 ? segs : null, null, default, 0, mirrorBounceCount);
    }

    private static bool TryTraceSpecularMirror(
        Point3D source,
        Random rng,
        LTri[] allTris,
        LTri[] mirrorTris,
        double[] mirrorAreas,
        double totalMirrorArea,
        LTri[] receiverTris,
        double[] receiverAreas,
        double totalReceiverArea,
        IReadOnlyDictionary<int, double> reflectanceBySurface,
        out List<RaySegment>? segs,
        out int? hitId,
        out Point3D hitPoint,
        out double hitWeight)
    {
        const int Attempts = 14;

        segs = null;
        hitId = null;
        hitPoint = default;
        hitWeight = 0;

        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var mirror = SampleTriWithPoint(rng, mirrorTris, mirrorAreas, totalMirrorArea).Tri;
            var receiverSample = SampleTriWithPoint(rng, receiverTris, receiverAreas, totalReceiverArea).Point;
            var mirrorNormal = Normal(mirror);
            var imageSource = ReflectPointAcrossPlane(source, mirror, mirrorNormal);

            if (!TryLinePlaneIntersection(imageSource, receiverSample, mirror, mirrorNormal, out var mirrorPoint))
                continue;

            if (!PointInTri(mirrorPoint, mirror, 1e-6))
                continue;

            var toMirror = Normalize(new Vec3(
                mirrorPoint.X - source.X,
                mirrorPoint.Y - source.Y,
                mirrorPoint.Z - source.Z));

            if (!Nearest(source, toMirror, allTris, out var tMirror, out var mirrorHitIndex))
                continue;

            var mirrorHitTri = allTris[mirrorHitIndex];
            if (mirrorHitTri.SurfaceId != mirror.SurfaceId)
                continue;

            var actualMirrorPoint = Add(source, toMirror, tMirror);
            if (Distance(actualMirrorPoint, mirrorPoint) > Math.Max(1e-5, tMirror * 1e-5))
                continue;

            var incidenceDot = Dot(toMirror, mirrorNormal);
            var reflected = Normalize(new Vec3(
                toMirror.X - 2 * incidenceDot * mirrorNormal.X,
                toMirror.Y - 2 * incidenceDot * mirrorNormal.Y,
                toMirror.Z - 2 * incidenceDot * mirrorNormal.Z));

            var towardReceiver = Normalize(new Vec3(
                receiverSample.X - mirrorPoint.X,
                receiverSample.Y - mirrorPoint.Y,
                receiverSample.Z - mirrorPoint.Z));

            if (Dot(reflected, towardReceiver) < 0.999)
                continue;

            var offsetMirrorPoint = Add(mirrorPoint, reflected, 1e-5);
            if (!Nearest(offsetMirrorPoint, reflected, allTris, out var tReceiver, out var receiverHitIndex))
                continue;

            var receiverHitTri = allTris[receiverHitIndex];
            if (!IsReceiverSurface(receiverHitTri.SurfaceId, receiverTris))
                continue;

            var actualReceiverPoint = Add(offsetMirrorPoint, reflected, tReceiver);
            var firstLen = Distance(source, mirrorPoint);
            var totalLen = firstLen + Distance(mirrorPoint, actualReceiverPoint);
            var reflectance = reflectanceBySurface.TryGetValue(mirror.SurfaceId, out var r)
                ? r
                : 0.95;
            hitWeight = reflectance * DistanceWeight(totalLen);

            segs =
            [
                new RaySegment(source, mirrorPoint, DistanceWeight(firstLen)),
                new RaySegment(mirrorPoint, actualReceiverPoint, hitWeight)
            ];
            hitId = receiverHitTri.SurfaceId;
            hitPoint = actualReceiverPoint;
            return true;
        }

        return false;
    }

    private static double DistanceWeight(double pathLength) =>
        1.0 / Math.Max(pathLength * pathLength, 1e-6);

    private static bool IsReceiverSurface(int surfaceId, LTri[] receiverTris) =>
        receiverTris.Any(t => t.SurfaceId == surfaceId);

    private static Point3D Add(Point3D origin, Vec3 direction, double length) =>
        new(
            origin.X + direction.X * length,
            origin.Y + direction.Y * length,
            origin.Z + direction.Z * length);

    private static double Distance(Point3D a, Point3D b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        var dz = a.Z - b.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static Point3D ReflectPointAcrossPlane(Point3D p, LTri planeTri, Vec3 n)
    {
        var v = new Vec3(p.X - planeTri.V0.X, p.Y - planeTri.V0.Y, p.Z - planeTri.V0.Z);
        var d = Dot(v, n);
        return new Point3D(p.X - 2 * d * n.X, p.Y - 2 * d * n.Y, p.Z - 2 * d * n.Z);
    }

    private static bool TryLinePlaneIntersection(Point3D a, Point3D b, LTri planeTri, Vec3 n, out Point3D p)
    {
        p = default;
        var ab = new Vec3(b.X - a.X, b.Y - a.Y, b.Z - a.Z);
        var denom = Dot(ab, n);
        if (Math.Abs(denom) < Eps) return false;

        var av0 = new Vec3(planeTri.V0.X - a.X, planeTri.V0.Y - a.Y, planeTri.V0.Z - a.Z);
        var t = Dot(av0, n) / denom;
        if (t <= Eps || t >= 1.0 - Eps) return false;

        p = Add(a, ab, t);
        return true;
    }

    private static bool PointInTri(Point3D p, LTri tri, double tolerance)
    {
        var v0 = new Vec3(tri.V2.X - tri.V0.X, tri.V2.Y - tri.V0.Y, tri.V2.Z - tri.V0.Z);
        var v1 = new Vec3(tri.V1.X - tri.V0.X, tri.V1.Y - tri.V0.Y, tri.V1.Z - tri.V0.Z);
        var v2 = new Vec3(p.X - tri.V0.X, p.Y - tri.V0.Y, p.Z - tri.V0.Z);

        var dot00 = Dot(v0, v0);
        var dot01 = Dot(v0, v1);
        var dot02 = Dot(v0, v2);
        var dot11 = Dot(v1, v1);
        var dot12 = Dot(v1, v2);
        var denom = dot00 * dot11 - dot01 * dot01;
        if (Math.Abs(denom) < Eps) return false;

        var invDenom = 1.0 / denom;
        var u = (dot11 * dot02 - dot01 * dot12) * invDenom;
        var v = (dot00 * dot12 - dot01 * dot02) * invDenom;

        return u >= -tolerance && v >= -tolerance && u + v <= 1.0 + tolerance;
    }

    private static IReadOnlyList<RaySegment> NormalizePathWeights(List<RaySegment> paths)
    {
        if (paths.Count == 0) return paths;

        var maxWeight = paths.Max(p => p.Weight);
        if (!double.IsFinite(maxWeight) || maxWeight <= 0) return paths;

        return paths
            .Select(p => p with { Weight = Math.Clamp(p.Weight / maxWeight, 0.02, 1.0) })
            .ToArray();
    }

    private static IReadOnlyDictionary<int, double> BuildReflectanceMap(
        IReadOnlyList<ProjectSurface> surfaces,
        IReadOnlyDictionary<string, string> categoryMaterials)
    {
        var result = new Dictionary<int, double>();
        foreach (var surface in surfaces)
        {
            var material = ResolveMaterial(surface.Category, categoryMaterials);
            var alpha = MidBandAlpha(material);
            result[surface.Id] = Math.Clamp(1.0 - alpha, 0.02, 1.0);
        }

        return result;
    }

    private static AcousticMaterial ResolveMaterial(
        string category,
        IReadOnlyDictionary<string, string> categoryMaterials)
    {
        if (categoryMaterials.TryGetValue(category, out var id))
            return MaterialDatabase.Instance.FindById(id)
                   ?? MaterialLibrary.FindById(id)
                   ?? MaterialLibrary.Default;

        return MaterialLibrary.Default;
    }

    private static double MidBandAlpha(AcousticMaterial material)
    {
        var alpha = material.Alpha;
        if (alpha.Length >= 5)
            return Math.Clamp((alpha[3] + alpha[4]) * 0.5, 0.0, 0.98);

        return alpha.Length > 0 ? Math.Clamp(alpha.Average(), 0.0, 0.98) : 0.05;
    }

    // ── Alan ağırlıklı üçgen nokta örneklemesi ────────────────────────────────

    private static (LTri Tri, Point3D Point) SampleTriWithPoint(Random rng, LTri[] tris, double[] areas, double total)
    {
        var r = rng.NextDouble() * total;
        var sum = 0d;
        int idx = 0;
        for (; idx < tris.Length - 1; idx++)
        { sum += areas[idx]; if (sum >= r) break; }

        var tri = tris[idx];
        return (tri, SamplePointInTri(rng, tri));
    }

    private static Point3D SampleTriPoint(Random rng, LTri[] tris, double[] areas, double total)
    {
        var r = rng.NextDouble() * total;
        var sum = 0d;
        int idx = 0;
        for (; idx < tris.Length - 1; idx++)
        { sum += areas[idx]; if (sum >= r) break; }

        return SamplePointInTri(rng, tris[idx]);
    }

    private static Point3D SamplePointInTri(Random rng, LTri tri)
    {
        var s   = rng.NextDouble();
        var tv  = rng.NextDouble();
        if (s + tv > 1d) { s = 1d - s; tv = 1d - tv; }
        return new Point3D(
            tri.V0.X + s*(tri.V1.X-tri.V0.X) + tv*(tri.V2.X-tri.V0.X),
            tri.V0.Y + s*(tri.V1.Y-tri.V0.Y) + tv*(tri.V2.Y-tri.V0.Y),
            tri.V0.Z + s*(tri.V1.Z-tri.V0.Z) + tv*(tri.V2.Z-tri.V0.Z));
    }

    // ── Kesişim ───────────────────────────────────────────────────────────────

    private static bool Nearest(Point3D o, Vec3 d, LTri[] tris, out double tMin, out int idx)
    {
        tMin = double.MaxValue; idx = -1;
        for (int i = 0; i < tris.Length; i++)
            if (MT(o, d, tris[i], out var t) && t > 1e-5 && t < tMin)
            { tMin = t; idx = i; }
        return idx >= 0;
    }

    private static bool MT(Point3D o, Vec3 d, LTri tri, out double t)
    {
        t = 0;
        var e1 = new Vec3(tri.V1.X-tri.V0.X, tri.V1.Y-tri.V0.Y, tri.V1.Z-tri.V0.Z);
        var e2 = new Vec3(tri.V2.X-tri.V0.X, tri.V2.Y-tri.V0.Y, tri.V2.Z-tri.V0.Z);
        var h  = Cross(d, e2);
        var a  = Dot(e1, h);
        if (Math.Abs(a) < Eps) return false;
        var f  = 1.0 / a;
        var s  = new Vec3(o.X-tri.V0.X, o.Y-tri.V0.Y, o.Z-tri.V0.Z);
        var u  = f * Dot(s, h);
        if (u < 0 || u > 1) return false;
        var q  = Cross(s, e1);
        var v  = f * Dot(d, q);
        if (v < 0 || u+v > 1) return false;
        t = f * Dot(e2, q);
        return true;
    }

    private static Vec3 Normal(LTri tri)
    {
        var e1 = new Vec3(tri.V1.X-tri.V0.X, tri.V1.Y-tri.V0.Y, tri.V1.Z-tri.V0.Z);
        var e2 = new Vec3(tri.V2.X-tri.V0.X, tri.V2.Y-tri.V0.Y, tri.V2.Z-tri.V0.Z);
        return Normalize(Cross(e1, e2));
    }

    private static double TriArea(LTri tri)
    {
        var e1 = new Vec3(tri.V1.X-tri.V0.X, tri.V1.Y-tri.V0.Y, tri.V1.Z-tri.V0.Z);
        var e2 = new Vec3(tri.V2.X-tri.V0.X, tri.V2.Y-tri.V0.Y, tri.V2.Z-tri.V0.Z);
        var c  = Cross(e1, e2);
        return 0.5 * Math.Sqrt(c.X*c.X + c.Y*c.Y + c.Z*c.Z);
    }

    private static Vec3 RandDir(Random rng)
    {
        double x, y, s;
        do { x = rng.NextDouble()*2-1; y = rng.NextDouble()*2-1; s = x*x+y*y; }
        while (s >= 1.0);
        var f = 2 * Math.Sqrt(1-s);
        return new Vec3(x*f, y*f, 1-2*s);
    }

    private static Vec3 Normalize(Vec3 v)
    {
        var l = Math.Sqrt(v.X*v.X + v.Y*v.Y + v.Z*v.Z);
        return l < Eps ? new Vec3(0, 0, 1) : new Vec3(v.X/l, v.Y/l, v.Z/l);
    }

    private static LTri[] Triangulate(IReadOnlyList<ProjectSurface> surfaces)
    {
        var buf = new List<LTri>();
        foreach (var surf in surfaces)
        {
            var v = surf.Vertices;
            for (int i = 1; i < v.Count - 1; i++)
                buf.Add(new LTri(v[0], v[i], v[i+1], surf.Id));
        }
        return buf.ToArray();
    }

    private static Vec3 Cross(Vec3 a, Vec3 b) =>
        new(a.Y*b.Z - a.Z*b.Y, a.Z*b.X - a.X*b.Z, a.X*b.Y - a.Y*b.X);

    private static double Dot(Vec3 a, Vec3 b) => a.X*b.X + a.Y*b.Y + a.Z*b.Z;
}
