using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Projects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Kst.Acoustics.Core.Validation;

public sealed class LeakTestService
{
    private const double IntersectionEpsilon = 1e-6;
    private const double EscapeOffset = 1e-5;
    private const int    LeakRayCount = 12;

    // Merkezden çıkan 12 omnidirectional ışın — Fibonacci küresi ile küre yüzeyine
    // düzgün dağıtılır. Sabit birkaç yön yerine geniş açısal kapsama, küçük boşlukların
    // kaçırılma ihtimalini ciddi şekilde azaltır.
    private static readonly Vector3D[] TestDirections = BuildOmniDirections(LeakRayCount);

    private static Vector3D[] BuildOmniDirections(int count)
    {
        var dirs = new Vector3D[count];
        double goldenAngle = Math.PI * (3.0 - Math.Sqrt(5.0)); // ~2.39996 rad
        for (int i = 0; i < count; i++)
        {
            // y'yi (+1 → -1) aralığında, kutuplara tam oturmayacak şekilde örnekle
            double y      = 1.0 - (i + 0.5) / count * 2.0;
            double radius = Math.Sqrt(Math.Max(0.0, 1.0 - y * y));
            // theta'ya faz kayması: hiçbir yön tam eksen düzlemine (z=0) oturmasın.
            // Aksi halde kutu odada z=0 olan bir ışın taban/tavana hiç ulaşmaz, düz kalır.
            double theta  = goldenAngle * i + 0.5;
            double x      = Math.Cos(theta) * radius;
            double z      = Math.Sin(theta) * radius;
            dirs[i] = new Vector3D(x, y, z).Normalize();
        }
        return dirs;
    }

    public async Task<LeakTestResult> RunAsync(
        AcousticsProject project,
        int maxBounces = 10_000,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (project.Surfaces.Count == 0)
        {
            return new LeakTestResult(
                false,
                "Projede test edilecek yüzey bulunamadı.",
                0,
                new Point3D(0, 0, 0),
                new Vector3D(0, 0, 0),
                null,
                []);
        }

        return await Task.Run(() =>
        {
            var surfaces = project.Surfaces.Where(s => s.Vertices.Count >= 3).ToArray();
            var indices = Enumerable.Range(0, surfaces.Length).ToArray();
            var nodes = new List<BvhNode>();
            var centers = new Point3D[surfaces.Length];
            var boundsList = new (Point3D Min, Point3D Max)[surfaces.Length];

            for (int i = 0; i < surfaces.Length; i++)
            {
                var pts = surfaces[i].Vertices.Where(p => !double.IsNaN(p.X)).ToArray();
                var minX = pts.Min(p => p.X); var maxX = pts.Max(p => p.X);
                var minY = pts.Min(p => p.Y); var maxY = pts.Max(p => p.Y);
                var minZ = pts.Min(p => p.Z); var maxZ = pts.Max(p => p.Z);
                boundsList[i] = (new Point3D(minX, minY, minZ), new Point3D(maxX, maxY, maxZ));
                centers[i] = new Point3D((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);
            }

            BuildBvhRecursive(indices, 0, surfaces.Length, nodes, boundsList, centers);

            var sortedSurfaces = new ProjectSurface[surfaces.Length];
            for (int i = 0; i < surfaces.Length; i++) sortedSurfaces[i] = surfaces[indices[i]];
            var bvh = nodes.ToArray();

            var startPoint = GetBoundingBoxCenter(surfaces);
            if (!IsInsideRoom(startPoint, sortedSurfaces, bvh))
            {
                var firstSource = project.Sources.FirstOrDefault();
                if (firstSource is not null)
                {
                    startPoint = firstSource.Position;
                }
            }

            for (int i = 0; i < TestDirections.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var direction = TestDirections[i];
                var result = RunSingleRay(sortedSurfaces, bvh, startPoint, direction, maxBounces, cancellationToken);
                if (!result.Passed)
                {
                    return result;
                }

                progress?.Report((double)(i + 1) / TestDirections.Length);
            }

            const int displayBouncesPerRay = 20;
            var combinedPath = new List<LeakTestPathPoint>();
            foreach (var dir in TestDirections)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var dray = RunSingleRay(sortedSurfaces, bvh, startPoint, dir, displayBouncesPerRay, cancellationToken);
                if (combinedPath.Count > 0)
                    combinedPath.Add(new LeakTestPathPoint(-1, new Point3D(double.NaN, double.NaN, double.NaN), null));
                combinedPath.AddRange(dray.Path);
            }

            return new LeakTestResult(
                true,
                $"{TestDirections.Length} omnidirectional ışın, her biri {maxBounces:N0} yansıma boyunca geometri dışına çıkmadı. Proje sızdırmaz görünüyor.",
                maxBounces,
                startPoint,
                TestDirections[0],
                null,
                combinedPath);
        }, cancellationToken);
    }

    private static LeakTestResult RunSingleRay(
        IReadOnlyList<ProjectSurface> surfaces,
        BvhNode[] bvh,
        Point3D startPoint,
        Vector3D direction,
        int maxBounces,
        CancellationToken cancellationToken)
    {
        var path = new List<LeakTestPathPoint>
        {
            new(0, startPoint, null)
        };

        var currentOrigin = startPoint;
        var currentDirection = direction;
        int? lastSurfaceId = null;

        for (var bounce = 1; bounce <= maxBounces; bounce++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hit = FindClosestIntersection(surfaces, bvh, currentOrigin, currentDirection, lastSurfaceId);
            if (hit is null)
            {
                return new LeakTestResult(
                    false,
                    $"Işın {bounce - 1} yansımadan sonra geometri dışına kaçtı. Proje sızdırmazlık testinden geçemedi.",
                    bounce - 1,
                    startPoint,
                    direction,
                    lastSurfaceId,
                    path);
            }

            var surfaceHit = hit.Value;
            path.Add(new LeakTestPathPoint(bounce, surfaceHit.Point, surfaceHit.Surface.Id));

            var orientedNormal = GeometryMath.Dot(currentDirection, surfaceHit.Normal) < 0
                ? surfaceHit.Normal
                : -surfaceHit.Normal;

            currentDirection = GeometryMath.Reflect(currentDirection, orientedNormal);
            currentOrigin = GeometryMath.Add(surfaceHit.Point, currentDirection * EscapeOffset);
            lastSurfaceId = surfaceHit.Surface.Id;
        }

        return new LeakTestResult(
            true,
            $"Işın {maxBounces} yansıma boyunca geometri dışına çıkmadı. Proje sızdırmaz görünüyor.",
            maxBounces,
            startPoint,
            direction,
            lastSurfaceId,
            path);
    }

    private static Point3D GetBoundingBoxCenter(IReadOnlyList<ProjectSurface> surfaces)
    {
        var points = surfaces.SelectMany(x => x.Vertices).Where(p => !double.IsNaN(p.X)).ToArray();
        var minX = points.Min(x => x.X);
        var maxX = points.Max(x => x.X);
        var minY = points.Min(x => x.Y);
        var maxY = points.Max(x => x.Y);
        var minZ = points.Min(x => x.Z);
        var maxZ = points.Max(x => x.Z);

        return new Point3D(
            (minX + maxX) / 2d,
            (minY + maxY) / 2d,
            (minZ + maxZ) / 2d);
    }

    private static SurfaceHit? FindClosestIntersection(
        IReadOnlyList<ProjectSurface> surfaces,
        BvhNode[] bvh,
        Point3D origin,
        Vector3D direction,
        int? lastSurfaceId)
    {
        SurfaceHit? bestHit = null;
        if (bvh.Length == 0) return null;
        
        IntersectBvhRecursive(0, origin, direction, surfaces, bvh, lastSurfaceId, ref bestHit);
        return bestHit;
    }

    private static void IntersectBvhRecursive(
        int nodeIdx, 
        Point3D origin, 
        Vector3D dir, 
        IReadOnlyList<ProjectSurface> surfaces, 
        BvhNode[] bvh, 
        int? lastSurfaceId, 
        ref SurfaceHit? bestHit)
    {
        ref readonly var node = ref bvh[nodeIdx];
        double currentBestDistance = bestHit?.Distance ?? double.MaxValue;
        
        if (!node.Intersect(origin, dir, out var bTMin) || bTMin > currentBestDistance) return;

        if (node.SurfaceCount > 0)
        {
            for (int i = 0; i < node.SurfaceCount; i++)
            {
                var surface = surfaces[node.LeftIndex + i];
                if (lastSurfaceId == surface.Id) continue;
                
                if (TryIntersectSurface(origin, dir, surface, out var hit))
                {
                    if (bestHit is null || hit.Distance < bestHit.Value.Distance)
                    {
                        bestHit = hit;
                    }
                }
            }
            return;
        }

        int leftIdx = node.LeftIndex;
        int rightIdx = node.RightIndex;
        bool hitL = bvh[leftIdx].Intersect(origin, dir, out var lEntry);
        bool hitR = bvh[rightIdx].Intersect(origin, dir, out var rEntry);

        if (hitL && hitR)
        {
            int near = leftIdx, far = rightIdx;
            if (rEntry < lEntry) { near = rightIdx; far = leftIdx; }
            IntersectBvhRecursive(near, origin, dir, surfaces, bvh, lastSurfaceId, ref bestHit);
            IntersectBvhRecursive(far, origin, dir, surfaces, bvh, lastSurfaceId, ref bestHit);
        }
        else if (hitL) IntersectBvhRecursive(leftIdx, origin, dir, surfaces, bvh, lastSurfaceId, ref bestHit);
        else if (hitR) IntersectBvhRecursive(rightIdx, origin, dir, surfaces, bvh, lastSurfaceId, ref bestHit);
    }

    private static bool TryIntersectSurface(Point3D origin, Vector3D direction, ProjectSurface surface, out SurfaceHit hit)
    {
        hit = default;

        if (!TryGetSurfaceNormal(surface.Vertices, out var normal))
        {
            return false;
        }

        var planePoint = surface.Vertices[0];
        var denominator = GeometryMath.Dot(direction, normal);
        if (Math.Abs(denominator) < IntersectionEpsilon)
        {
            return false;
        }

        var originToPlane = GeometryMath.ToVector(origin, planePoint);
        var distance = GeometryMath.Dot(originToPlane, normal) / denominator;
        if (distance <= IntersectionEpsilon)
        {
            return false;
        }

        var point = GeometryMath.Add(origin, direction * distance);
        if (!IsPointInsidePolygon(point, surface.Vertices, normal))
        {
            return false;
        }

        hit = new SurfaceHit(surface, point, normal.Normalize(), distance);
        return true;
    }

    private static bool TryGetSurfaceNormal(IReadOnlyList<Point3D> vertices, out Vector3D normal)
    {
        normal = default;

        var p0 = vertices.FirstOrDefault(v => !double.IsNaN(v.X));
        if (double.IsNaN(p0.X)) return false;

        for (var i = 1; i < vertices.Count - 1; i++)
        {
            if (double.IsNaN(vertices[i].X) || double.IsNaN(vertices[i + 1].X)) continue;

            var v1 = GeometryMath.ToVector(p0, vertices[i]);
            var v2 = GeometryMath.ToVector(p0, vertices[i + 1]);
            var cross = GeometryMath.Cross(v1, v2);
            if (cross.Length > IntersectionEpsilon)
            {
                normal = cross.Normalize();
                return true;
            }
        }

        return false;
    }

    private static bool IsPointInsidePolygon(Point3D point, IReadOnlyList<Point3D> vertices, Vector3D normal)
    {
        var axis = GetDominantAxis(normal);
        var polygon2D = vertices.Select(vertex => ProjectTo2D(vertex, axis)).ToArray();
        var point2D = ProjectTo2D(point, axis);

        var inside = false;
        int loopStartIndex = 0;

        for (int i = 0; i < polygon2D.Length; i++)
        {
            if (double.IsNaN(polygon2D[i].X))
            {
                // Close the current loop
                if (i > loopStartIndex)
                {
                    var piC = polygon2D[loopStartIndex];
                    var pjC = polygon2D[i - 1];
                    if (!double.IsNaN(piC.X) && !double.IsNaN(pjC.X))
                    {
                        var intersectsC = ((piC.Y > point2D.Y) != (pjC.Y > point2D.Y)) &&
                                          (point2D.X < ((pjC.X - piC.X) * (point2D.Y - piC.Y) / ((pjC.Y - piC.Y) + double.Epsilon)) + piC.X);
                        if (intersectsC) inside = !inside;
                    }
                }
                loopStartIndex = i + 1;
                continue;
            }

            if (i > loopStartIndex)
            {
                var pi = polygon2D[i];
                var pj = polygon2D[i - 1];
                var intersects = ((pi.Y > point2D.Y) != (pj.Y > point2D.Y)) &&
                                 (point2D.X < ((pj.X - pi.X) * (point2D.Y - pi.Y) / ((pj.Y - pi.Y) + double.Epsilon)) + pi.X);
                if (intersects) inside = !inside;
            }
        }

        // Close the final loop
        if (polygon2D.Length > loopStartIndex)
        {
            var piF = polygon2D[loopStartIndex];
            var pjF = polygon2D[polygon2D.Length - 1];
            if (!double.IsNaN(piF.X) && !double.IsNaN(pjF.X))
            {
                var intersectsF = ((piF.Y > point2D.Y) != (pjF.Y > point2D.Y)) &&
                                  (point2D.X < ((pjF.X - piF.X) * (point2D.Y - piF.Y) / ((pjF.Y - piF.Y) + double.Epsilon)) + piF.X);
                if (intersectsF) inside = !inside;
            }
        }

        return inside;
    }

    private static int GetDominantAxis(Vector3D normal)
    {
        var absX = Math.Abs(normal.X);
        var absY = Math.Abs(normal.Y);
        var absZ = Math.Abs(normal.Z);

        if (absX >= absY && absX >= absZ)
        {
            return 0;
        }

        return absY >= absZ ? 1 : 2;
    }

    private static Point2D ProjectTo2D(Point3D point, int dominantAxis)
    {
        return dominantAxis switch
        {
            0 => new Point2D(point.Y, point.Z),
            1 => new Point2D(point.X, point.Z),
            _ => new Point2D(point.X, point.Y)
        };
    }

    private readonly record struct SurfaceHit(ProjectSurface Surface, Point3D Point, Vector3D Normal, double Distance);

    private readonly record struct Point2D(double X, double Y);

    private static bool IsInsideRoom(Point3D point, IReadOnlyList<ProjectSurface> surfaces, BvhNode[] bvh)
    {
        var direction = new Vector3D(1, 0, 0); // +X yönüne ray cast
        int hitCount = 0;
        
        var currentOrigin = point;
        var currentDirection = direction;
        int? lastSurfaceId = null;

        while (true)
        {
            var hit = FindClosestIntersection(surfaces, bvh, currentOrigin, currentDirection, lastSurfaceId);
            if (hit is null) break;
            
            hitCount++;
            currentOrigin = GeometryMath.Add(hit.Value.Point, currentDirection * EscapeOffset);
            lastSurfaceId = hit.Value.Surface.Id;
        }

        return hitCount % 2 != 0;
    }

    private static int BuildBvhRecursive(
        int[] indices, 
        int start, 
        int count, 
        List<BvhNode> nodes, 
        (Point3D Min, Point3D Max)[] boundsList, 
        Point3D[] centers)
    {
        var nodeIdx = nodes.Count;
        nodes.Add(default);

        var boundsMin = boundsList[indices[start]].Min;
        var boundsMax = boundsList[indices[start]].Max;
        
        for (int i = 1; i < count; i++)
        {
            var b = boundsList[indices[start + i]];
            boundsMin = new Point3D(Math.Min(boundsMin.X, b.Min.X), Math.Min(boundsMin.Y, b.Min.Y), Math.Min(boundsMin.Z, b.Min.Z));
            boundsMax = new Point3D(Math.Max(boundsMax.X, b.Max.X), Math.Max(boundsMax.Y, b.Max.Y), Math.Max(boundsMax.Z, b.Max.Z));
        }

        if (count <= 4)
        {
            nodes[nodeIdx] = new BvhNode { Min = boundsMin, Max = boundsMax, SurfaceCount = count, LeftIndex = start };
            return nodeIdx;
        }

        double dx = boundsMax.X - boundsMin.X;
        double dy = boundsMax.Y - boundsMin.Y;
        double dz = boundsMax.Z - boundsMin.Z;
        int axis = dx > dy ? (dx > dz ? 0 : 2) : (dy > dz ? 1 : 2);
        double mid = axis switch { 0 => (boundsMin.X + boundsMax.X) / 2, 1 => (boundsMin.Y + boundsMax.Y) / 2, _ => (boundsMin.Z + boundsMax.Z) / 2 };

        int split = start;
        for (int i = 0; i < count; i++)
        {
            var center = centers[indices[start + i]];
            double c = axis switch { 0 => center.X, 1 => center.Y, _ => center.Z };
            if (c < mid)
            {
                (indices[start + i], indices[split]) = (indices[split], indices[start + i]);
                split++;
            }
        }

        if (split == start || split == start + count) split = start + count / 2;

        int left = BuildBvhRecursive(indices, start, split - start, nodes, boundsList, centers);
        int right = BuildBvhRecursive(indices, split, count - (split - start), nodes, boundsList, centers);

        nodes[nodeIdx] = new BvhNode { Min = boundsMin, Max = boundsMax, LeftIndex = left, RightIndex = right };
        return nodeIdx;
    }

    private struct BvhNode
    {
        public Point3D Min;
        public Point3D Max;
        public int LeftIndex;
        public int RightIndex;
        public int SurfaceCount;

        public readonly bool Intersect(Point3D origin, Vector3D dir, out double tMin)
        {
            tMin = 0; double tMax = double.MaxValue;
            
            double invX = 1.0 / dir.X;
            double t1 = (Min.X - origin.X) * invX;
            double t2 = (Max.X - origin.X) * invX;
            tMin = Math.Max(tMin, Math.Min(t1, t2));
            tMax = Math.Min(tMax, Math.Max(t1, t2));

            double invY = 1.0 / dir.Y;
            double t3 = (Min.Y - origin.Y) * invY;
            double t4 = (Max.Y - origin.Y) * invY;
            tMin = Math.Max(tMin, Math.Min(t3, t4));
            tMax = Math.Min(tMax, Math.Max(t3, t4));

            double invZ = 1.0 / dir.Z;
            double t5 = (Min.Z - origin.Z) * invZ;
            double t6 = (Max.Z - origin.Z) * invZ;
            tMin = Math.Max(tMin, Math.Min(t5, t6));
            tMax = Math.Min(tMax, Math.Max(t5, t6));

            return tMax >= tMin && tMax >= 0;
        }
    }
}
