using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Importing;

namespace Kst.Acoustics.Core.Projects;

/// <summary>Oda hacmi tahmininin nereden geldiğini belirtir; UI'da kullanıcıya gösterilir.</summary>
public enum RoomVolumeOrigin
{
    /// <summary>Kabuk kapalı (watertight) — imzalı hacim (divergence teoremi) güvenilir.</summary>
    ClosedMesh,

    /// <summary>Kabuk kapalı değil (delik/boşluk var) — eksen hizalı bounding box tahmini.</summary>
    BoundingBoxEstimate,

    /// <summary>Hacim kullanıcı tarafından elle girildi veya override edildi.</summary>
    UserProvided,

    /// <summary>Hacim M51 gibi yetkili bir kaynak geometrinin kendi hesabından geldi.</summary>
    SourceModel
}

public readonly record struct RoomVolumeEstimate(double VolumeM3, RoomVolumeOrigin Origin);

/// <summary>
/// DXF'den içe aktarılan yüzey kümesinden oda hacmini hesaplar. Kabuk kapalıysa (her kenar
/// tam iki yüzey tarafından paylaşılıyorsa) yüzeylerin winding yönünü topolojik olarak
/// tutarlı hale getirip divergence teoremiyle gerçek hacim döner; açık/non-manifold veya
/// yönlendirilemeyen kabukta eksen hizalı bounding box'a düşer.
/// </summary>
public static class RoomVolumeCalculator
{
    private const double EdgeToleranceMeters = 1e-4;

    public static RoomVolumeEstimate Estimate(IReadOnlyList<ProjectSurface> surfaces, SurfaceImportUnit unit)
    {
        ArgumentNullException.ThrowIfNull(surfaces);

        var metersPerUnit = ProjectScalingService.GetMetersPerUnit(unit);

        if (TryBuildOrientedComponents(surfaces, metersPerUnit, out var components))
        {
            // Bağlantısız kapalı kabuklar birbirinin ters global yönünde olabilir. Her
            // bağlı bileşenin mutlak hacmini ayrı toplamak, bu bileşenlerin birbirini
            // sayısal olarak iptal etmesini engeller.
            var meshVolume = components.Sum(component =>
                Math.Abs(SignedVolume(component, metersPerUnit)));
            if (double.IsFinite(meshVolume) && meshVolume > 1e-6)
                return new RoomVolumeEstimate(meshVolume, RoomVolumeOrigin.ClosedMesh);
        }

        return new RoomVolumeEstimate(BoundingBoxVolume(surfaces, metersPerUnit), RoomVolumeOrigin.BoundingBoxEstimate);
    }

    private static double SignedVolume(IReadOnlyList<ProjectSurface> surfaces, double metersPerUnit)
    {
        var signedVolume = 0.0;
        foreach (var surface in surfaces)
        {
            var verts = surface.Vertices;
            if (verts.Count < 3) continue;

            var origin = ScaleVector(verts[0], metersPerUnit);
            for (var i = 1; i < verts.Count - 1; i++)
            {
                var b = ScaleVector(verts[i], metersPerUnit);
                var c = ScaleVector(verts[i + 1], metersPerUnit);
                signedVolume += GeometryMath.Dot(origin, GeometryMath.Cross(b, c)) / 6.0;
            }
        }
        return signedVolume;
    }

    private static Vector3D ScaleVector(Point3D p, double factor) =>
        new(p.X * factor, p.Y * factor, p.Z * factor);

    private static double BoundingBoxVolume(IReadOnlyList<ProjectSurface> surfaces, double metersPerUnit)
    {
        var allVertices = surfaces.SelectMany(s => s.Vertices).Where(IsFinite).ToList();
        if (allVertices.Count == 0) return 0;

        var dx = (allVertices.Max(v => v.X) - allVertices.Min(v => v.X)) * metersPerUnit;
        var dy = (allVertices.Max(v => v.Y) - allVertices.Min(v => v.Y)) * metersPerUnit;
        var dz = (allVertices.Max(v => v.Z) - allVertices.Min(v => v.Z)) * metersPerUnit;
        return dx * dy * dz;
    }

    /// <summary>
    /// Kabuk kapalılık denetimi: her kenar (yönsüz köşe çifti) tam iki kez görünmeli.
    /// Bir kez görünen kenar = delik/açık sınır; ikiden fazla = non-manifold. İkisi de
    /// mesh hacmini güvenilmez kılar.
    /// </summary>
    private static bool TryBuildOrientedComponents(
        IReadOnlyList<ProjectSurface> surfaces,
        double metersPerUnit,
        out IReadOnlyList<IReadOnlyList<ProjectSurface>> components)
    {
        components = [];
        if (surfaces.Count == 0) return false;

        var edgeUses = new Dictionary<(Point3D, Point3D), List<EdgeUse>>();

        for (var surfaceIndex = 0; surfaceIndex < surfaces.Count; surfaceIndex++)
        {
            var surface = surfaces[surfaceIndex];
            var verts = surface.Vertices;
            if (verts.Count < 3 || verts.Any(v => !IsFinite(v))) return false;

            for (var i = 0; i < verts.Count; i++)
            {
                var a = Round(Scale(verts[i], metersPerUnit));
                var b = Round(Scale(verts[(i + 1) % verts.Count], metersPerUnit));
                if (a == b) continue; // sıfır uzunluklu kenar — dejenere köşe, yok say

                var forward = ComparePoints(a, b) <= 0;
                var key = forward ? (a, b) : (b, a);
                if (!edgeUses.TryGetValue(key, out var uses))
                {
                    edgeUses[key] = uses = [];
                }

                uses.Add(new EdgeUse(surfaceIndex, forward));
            }
        }

        if (edgeUses.Count == 0 || edgeUses.Values.Any(uses =>
                uses.Count != 2 || uses[0].SurfaceIndex == uses[1].SurfaceIndex))
        {
            return false;
        }

        var adjacency = Enumerable.Range(0, surfaces.Count)
            .Select(_ => new List<OrientationConstraint>())
            .ToArray();

        foreach (var uses in edgeUses.Values)
        {
            var a = uses[0];
            var b = uses[1];
            // Paylaşılan kenar iki yüzeyde aynı yönde yürünüyorsa yüzeylerden biri
            // çevrilmelidir; ters yöndeyse ikisi aynı flip durumunda kalmalıdır.
            var mustDiffer = a.Forward == b.Forward;
            adjacency[a.SurfaceIndex].Add(new OrientationConstraint(b.SurfaceIndex, mustDiffer));
            adjacency[b.SurfaceIndex].Add(new OrientationConstraint(a.SurfaceIndex, mustDiffer));
        }

        var flip = new bool?[surfaces.Count];
        var orientedComponents = new List<IReadOnlyList<ProjectSurface>>();

        for (var start = 0; start < surfaces.Count; start++)
        {
            if (flip[start].HasValue) continue;

            flip[start] = false;
            var queue = new Queue<int>();
            var indices = new List<int>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                indices.Add(current);

                foreach (var constraint in adjacency[current])
                {
                    var expected = flip[current]!.Value ^ constraint.MustDiffer;
                    if (flip[constraint.Neighbor] is { } existing)
                    {
                        if (existing != expected)
                        {
                            // Möbius-benzeri/non-orientable veya tutarsız topoloji.
                            return false;
                        }

                        continue;
                    }

                    flip[constraint.Neighbor] = expected;
                    queue.Enqueue(constraint.Neighbor);
                }
            }

            orientedComponents.Add(indices.Select(index =>
            {
                var surface = surfaces[index];
                return flip[index] == true
                    ? surface with { Vertices = surface.Vertices.Reverse().ToArray() }
                    : surface;
            }).ToArray());
        }

        components = orientedComponents;
        return true;
    }

    private static int ComparePoints(Point3D a, Point3D b)
    {
        var cx = a.X.CompareTo(b.X);
        if (cx != 0) return cx;
        var cy = a.Y.CompareTo(b.Y);
        if (cy != 0) return cy;
        return a.Z.CompareTo(b.Z);
    }

    private static Point3D Round(Point3D p)
    {
        var scale = 1.0 / EdgeToleranceMeters;
        return new Point3D(
            Math.Round(p.X * scale) / scale,
            Math.Round(p.Y * scale) / scale,
            Math.Round(p.Z * scale) / scale);
    }

    private static Point3D Scale(Point3D p, double factor) =>
        new(p.X * factor, p.Y * factor, p.Z * factor);

    private static bool IsFinite(Point3D p) =>
        double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z);

    private readonly record struct EdgeUse(int SurfaceIndex, bool Forward);
    private readonly record struct OrientationConstraint(int Neighbor, bool MustDiffer);
}
