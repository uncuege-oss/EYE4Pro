using System.Globalization;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.Core.Validation;

/// <summary>
/// M01 analiz snapshot'ı üzerinde salt-okunur model sağlığı denetimi ve güvenli onarım
/// önizlemesi üretir. Kaynak M50/M51 artifact'larına yazmaz.
/// </summary>
public sealed class ModelHealthService
{
    public ModelHealthReport Analyze(AcousticsProject project, ModelHealthOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        options ??= new ModelHealthOptions();
        ValidateOptions(options);

        var findings = new List<ModelHealthFinding>();
        var unitIsValid = Enum.IsDefined(project.Unit);
        var metersPerUnit = unitIsValid ? ProjectScalingService.GetMetersPerUnit(project.Unit) : 1.0;

        if (!unitIsValid)
        {
            findings.Add(new ModelHealthFinding(
                ModelHealthIssueCode.SuspiciousUnit,
                ModelHealthSeverity.Error,
                "Proje birimi geçersiz",
                $"Birim kodu ({(int)project.Unit}) desteklenmiyor; fiziksel denetimler güvenilir değil."));
        }

        var normalized = new List<ProjectSurface>(project.Surfaces.Count);
        var duplicateVertexSurfaceIds = new List<int>();
        foreach (var surface in project.Surfaces)
        {
            var cleaned = RemoveConsecutiveDuplicateVertices(
                surface.Vertices ?? [], metersPerUnit, options.TopologyToleranceMeters, out var removedCount);
            normalized.Add(surface with { Vertices = cleaned });
            if (removedCount > 0) duplicateVertexSurfaceIds.Add(surface.Id);
        }

        if (duplicateVertexSurfaceIds.Count > 0)
        {
            findings.Add(new ModelHealthFinding(
                ModelHealthIssueCode.ConsecutiveDuplicateVertex,
                ModelHealthSeverity.Warning,
                "Yinelenen ardışık köşeler",
                $"{duplicateVertexSurfaceIds.Count} yüzeyde sıfır uzunluklu kenar oluşturan ardışık köşeler var.",
                duplicateVertexSurfaceIds,
                OccurrenceCount: duplicateVertexSurfaceIds.Count));
        }

        var zeroAreaIds = new List<int>();
        var microSurfaceIds = new List<int>();
        foreach (var surface in normalized)
        {
            var areaM2 = ComputeAreaM2(surface, metersPerUnit);
            if (!double.IsFinite(areaM2) || areaM2 <= options.ZeroAreaToleranceM2)
                zeroAreaIds.Add(surface.Id);
            else if (areaM2 < options.MicroSurfaceAreaM2)
                microSurfaceIds.Add(surface.Id);
        }

        if (zeroAreaIds.Count > 0)
        {
            findings.Add(new ModelHealthFinding(
                ModelHealthIssueCode.ZeroAreaSurface,
                ModelHealthSeverity.Error,
                "Sıfır alanlı yüzeyler",
                $"{zeroAreaIds.Count} yüzey geometrik alan üretmiyor ve ışın izlemeye katılamaz.",
                zeroAreaIds,
                OccurrenceCount: zeroAreaIds.Count));
        }

        if (microSurfaceIds.Count > 0)
        {
            findings.Add(new ModelHealthFinding(
                ModelHealthIssueCode.MicroSurface,
                ModelHealthSeverity.Warning,
                "Mikro yüzeyler",
                $"{microSurfaceIds.Count} yüzey {options.MicroSurfaceAreaM2:G3} m² eşiğinin altında.",
                microSurfaceIds,
                OccurrenceCount: microSurfaceIds.Count));
        }

        var duplicateGroups = FindDuplicateSurfaceGroups(normalized, metersPerUnit, options.TopologyToleranceMeters);
        if (duplicateGroups.Count > 0)
        {
            var ids = duplicateGroups.SelectMany(g => g).Distinct().ToArray();
            findings.Add(new ModelHealthFinding(
                ModelHealthIssueCode.DuplicateSurface,
                ModelHealthSeverity.Warning,
                "Yinelenen yüzeyler",
                $"{duplicateGroups.Count} grupta aynı geometriyi kullanan {ids.Length} yüzey bulundu.",
                ids,
                OccurrenceCount: duplicateGroups.Sum(g => g.Count - 1)));
        }

        var topology = BuildTopology(normalized, metersPerUnit, options.TopologyToleranceMeters);
        var openEdges = topology.Edges.Where(e => e.Value.Count == 1).ToArray();
        var nonManifoldEdges = topology.Edges.Where(e =>
            e.Value.Count > 2 || e.Value.Select(u => u.SurfaceIndex).Distinct().Count() != e.Value.Count).ToArray();

        if (openEdges.Length > 0)
        {
            findings.Add(new ModelHealthFinding(
                ModelHealthIssueCode.OpenEdge,
                ModelHealthSeverity.Error,
                "Açık kabuk kenarları",
                $"{openEdges.Length} kenar yalnız bir yüzey tarafından kullanılıyor; oda kabuğu kapalı değil.",
                SurfaceIds(openEdges, normalized),
                OccurrenceCount: openEdges.Length));
        }

        if (nonManifoldEdges.Length > 0)
        {
            findings.Add(new ModelHealthFinding(
                ModelHealthIssueCode.NonManifoldEdge,
                ModelHealthSeverity.Error,
                "Non-manifold kenarlar",
                $"{nonManifoldEdges.Length} kenar ikiden fazla veya aynı yüzey tarafından birden çok kez kullanılıyor.",
                SurfaceIds(nonManifoldEdges, normalized),
                OccurrenceCount: nonManifoldEdges.Length));
        }

        if (TryOrientSurfaces(normalized, metersPerUnit, options.TopologyToleranceMeters,
                out _, out var windingChangedIds) && windingChangedIds.Count > 0)
        {
            findings.Add(new ModelHealthFinding(
                ModelHealthIssueCode.InconsistentWinding,
                ModelHealthSeverity.Warning,
                "Ters veya karışık yüzey yönleri",
                $"{windingChangedIds.Count} yüzeyin yönü komşu yüzeylerle ya da kapalı kabuğun dış yönüyle tutarsız.",
                windingChangedIds,
                OccurrenceCount: windingChangedIds.Count));
        }

        AddMissingMaterialFindings(project, normalized, findings);
        AddPointLocationFindings(project, normalized, topology.IsClosedManifold, findings);
        AddScaleAndVolumeFindings(project, normalized, metersPerUnit, options, findings);
        AddGridFindings(project, options, findings);

        return new ModelHealthReport(findings
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.Code)
            .ToArray());
    }

    public ModelHealthRepairPreview CreateSafeRepairPreview(
        AcousticsProject project,
        ModelHealthOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        options ??= new ModelHealthOptions();
        ValidateOptions(options);

        var metersPerUnit = Enum.IsDefined(project.Unit)
            ? ProjectScalingService.GetMetersPerUnit(project.Unit)
            : 1.0;
        var changes = new List<ModelHealthRepairChange>();

        // Her yüzey/köşe koleksiyonunu baştan kopyala; önizleme orijinal grafikle
        // değiştirilebilir bir liste paylaşmaz.
        var surfaces = project.Surfaces
            .Select(s => s with { Vertices = (s.Vertices ?? []).ToArray() })
            .ToList();

        var cleanedIds = new List<int>();
        for (var i = 0; i < surfaces.Count; i++)
        {
            var surface = surfaces[i];
            if (options.ProtectedSurfaceIds.Contains(surface.Id)) continue;

            var cleaned = RemoveConsecutiveDuplicateVertices(
                surface.Vertices, metersPerUnit, options.TopologyToleranceMeters, out var removedCount);
            if (removedCount <= 0) continue;
            surfaces[i] = surface with { Vertices = cleaned };
            cleanedIds.Add(surface.Id);
        }
        AddRepairChange(changes, ModelHealthRepairAction.RemoveConsecutiveDuplicateVertex,
            cleanedIds, $"{cleanedIds.Count} yüzeyde ardışık yinelenen köşeler temizlenecek.");

        var duplicateGroups = FindDuplicateSurfaceGroups(surfaces, metersPerUnit, options.TopologyToleranceMeters);
        var duplicateIdsToRemove = new HashSet<int>();
        foreach (var group in duplicateGroups)
        {
            var keeper = group.FirstOrDefault(id => options.ProtectedSurfaceIds.Contains(id));
            if (keeper == 0 && !group.Contains(0)) keeper = group[0];
            foreach (var id in group)
            {
                if (id != keeper && !options.ProtectedSurfaceIds.Contains(id))
                    duplicateIdsToRemove.Add(id);
            }
        }
        if (duplicateIdsToRemove.Count > 0)
            surfaces.RemoveAll(s => duplicateIdsToRemove.Contains(s.Id));
        AddRepairChange(changes, ModelHealthRepairAction.RemoveDuplicateSurface,
            duplicateIdsToRemove, $"{duplicateIdsToRemove.Count} yinelenen yüzey kaldırılacak.");

        var degenerateIds = surfaces
            .Where(s => !options.ProtectedSurfaceIds.Contains(s.Id))
            .Where(s =>
            {
                var area = ComputeAreaM2(s, metersPerUnit);
                return !double.IsFinite(area) || area <= options.ZeroAreaToleranceM2;
            })
            .Select(s => s.Id)
            .ToHashSet();
        if (degenerateIds.Count > 0)
            surfaces.RemoveAll(s => degenerateIds.Contains(s.Id));
        AddRepairChange(changes, ModelHealthRepairAction.RemoveDegenerateSurface,
            degenerateIds, $"{degenerateIds.Count} sıfır alanlı yüzey kaldırılacak.");

        var microIds = surfaces
            .Where(s => !options.ProtectedSurfaceIds.Contains(s.Id))
            .Where(s => ComputeAreaM2(s, metersPerUnit) < options.MicroSurfaceAreaM2)
            .Select(s => s.Id)
            .ToHashSet();
        if (microIds.Count > 0)
            surfaces.RemoveAll(s => microIds.Contains(s.Id));
        AddRepairChange(changes, ModelHealthRepairAction.RemoveMicroSurface,
            microIds, $"{microIds.Count} mikro yüzey kaldırılacak.");

        if (TryOrientSurfaces(surfaces, metersPerUnit, options.TopologyToleranceMeters,
                out var oriented, out var orientedIds) &&
            orientedIds.Count > 0 &&
            !orientedIds.Any(options.ProtectedSurfaceIds.Contains))
        {
            surfaces = oriented.ToList();
            AddRepairChange(changes, ModelHealthRepairAction.OrientSurfaceWinding,
                orientedIds, $"{orientedIds.Count} yüzeyin winding yönü düzeltilecek.");
        }

        var validSurfaceKeys = surfaces
            .Select(s => s.Id.ToString(CultureInfo.InvariantCulture))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var repairedProject = project with
        {
            Surfaces = surfaces.Select(s => s with { Vertices = s.Vertices.ToArray() }).ToArray(),
            SurfaceMaterials = project.SurfaceMaterials
                .Where(pair => validSurfaceKeys.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
        };

        return new ModelHealthRepairPreview(
            repairedProject,
            changes,
            Analyze(repairedProject, options),
            project.Surfaces.Count);
    }

    private static void AddMissingMaterialFindings(
        AcousticsProject project,
        IReadOnlyList<ProjectSurface> surfaces,
        List<ModelHealthFinding> findings)
    {
        var missing = surfaces.Where(surface =>
        {
            var key = surface.Id.ToString(CultureInfo.InvariantCulture);
            var hasOverride = project.SurfaceMaterials.TryGetValue(key, out var surfaceMaterial) &&
                              !string.IsNullOrWhiteSpace(surfaceMaterial);
            var hasCategoryMaterial = project.CategoryMaterials.TryGetValue(surface.Category ?? string.Empty, out var categoryMaterial) &&
                                      !string.IsNullOrWhiteSpace(categoryMaterial);
            return !hasOverride && !hasCategoryMaterial;
        }).Select(s => s.Id).ToArray();

        if (missing.Length == 0) return;
        findings.Add(new ModelHealthFinding(
            ModelHealthIssueCode.MissingMaterial,
            ModelHealthSeverity.Warning,
            "Materyalsiz yüzeyler",
            $"{missing.Length} yüzeyde yüzey veya kategori materyali atanmamış.",
            missing,
            OccurrenceCount: missing.Length));
    }

    private static void AddPointLocationFindings(
        AcousticsProject project,
        IReadOnlyList<ProjectSurface> surfaces,
        bool closedManifold,
        List<ModelHealthFinding> findings)
    {
        var vertices = surfaces.SelectMany(surface => surface.Vertices ?? []).Where(IsFinite).ToArray();
        if (vertices.Length == 0) return;
        var minX = vertices.Min(v => v.X);
        var maxX = vertices.Max(v => v.X);
        var minY = vertices.Min(v => v.Y);
        var maxY = vertices.Max(v => v.Y);
        var minZ = vertices.Min(v => v.Z);
        var maxZ = vertices.Max(v => v.Z);
        var epsilon = Math.Max(1e-9, Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ)) * 1e-9);

        foreach (var point in project.Sources.Concat(project.Receivers))
        {
            var position = point.Position;
            var outsideBounds = !IsFinite(position) ||
                                position.X < minX - epsilon || position.X > maxX + epsilon ||
                                position.Y < minY - epsilon || position.Y > maxY + epsilon ||
                                position.Z < minZ - epsilon || position.Z > maxZ + epsilon;
            var outside = closedManifold
                ? !IsFinite(position) || !RoomPointLocator.IsInside(position, surfaces)
                : outsideBounds;
            if (!outside) continue;

            var role = point.Type == AcousticPointType.Source ? "Kaynak" : "Alıcı";
            findings.Add(new ModelHealthFinding(
                ModelHealthIssueCode.PointOutsideRoom,
                ModelHealthSeverity.Error,
                $"{role} oda dışında",
                closedManifold
                    ? $"{role} '{point.Label}' (ID {point.Id}) kapalı oda kabuğunun içinde değil."
                    : $"{role} '{point.Label}' (ID {point.Id}) model sınır kutusunun dışında; açık kabukta ayrıntılı içeride testi yapılamadı.",
                AffectedPointIds: [point.Id]));
        }
    }

    private static void AddScaleAndVolumeFindings(
        AcousticsProject project,
        IReadOnlyList<ProjectSurface> surfaces,
        double metersPerUnit,
        ModelHealthOptions options,
        List<ModelHealthFinding> findings)
    {
        var vertices = surfaces.SelectMany(s => s.Vertices ?? []).Where(IsFinite).ToArray();
        if (vertices.Length > 0)
        {
            var extents = new[]
            {
                (vertices.Max(v => v.X) - vertices.Min(v => v.X)) * metersPerUnit,
                (vertices.Max(v => v.Y) - vertices.Min(v => v.Y)) * metersPerUnit,
                (vertices.Max(v => v.Z) - vertices.Min(v => v.Z)) * metersPerUnit
            };
            var largestExtent = extents.Max();
            if (largestExtent > 0 && largestExtent < options.SuspiciousMinimumExtentMeters)
            {
                findings.Add(new ModelHealthFinding(
                    ModelHealthIssueCode.SuspiciousUnit,
                    ModelHealthSeverity.Warning,
                    "Model fiziksel olarak çok küçük",
                    $"En büyük boyut {largestExtent:G3} m. İçe aktarma birimini ve ölçeği doğrulayın."));
            }
            else if (largestExtent > options.SuspiciousMaximumExtentMeters)
            {
                findings.Add(new ModelHealthFinding(
                    ModelHealthIssueCode.SuspiciousUnit,
                    ModelHealthSeverity.Warning,
                    "Model fiziksel olarak çok büyük",
                    $"En büyük boyut {largestExtent:G3} m. İçe aktarma birimini ve ölçeği doğrulayın."));
            }
        }

        if (!double.IsFinite(project.RoomVolume) || project.RoomVolume <= 0)
        {
            findings.Add(new ModelHealthFinding(
                ModelHealthIssueCode.SuspiciousVolume,
                ModelHealthSeverity.Error,
                "Oda hacmi geçersiz",
                "Oda hacmi sonlu ve sıfırdan büyük olmalıdır."));
            return;
        }

        if (project.VolumeOrigin == RoomVolumeOrigin.BoundingBoxEstimate)
        {
            findings.Add(new ModelHealthFinding(
                ModelHealthIssueCode.SuspiciousVolume,
                ModelHealthSeverity.Warning,
                "Oda hacmi sınır kutusu tahmini",
                "Kabuk kapalı olmadığı için hacim sınır kutusundan tahmin edilmiş; akustik hesap öncesi doğrulayın."));
        }

        var estimate = RoomVolumeCalculator.Estimate(surfaces, project.Unit);
        if (!double.IsFinite(estimate.VolumeM3) || estimate.VolumeM3 <= 1e-9) return;
        var ratio = project.RoomVolume / estimate.VolumeM3;
        if (ratio is >= 0.1 and <= 10.0) return;

        findings.Add(new ModelHealthFinding(
            ModelHealthIssueCode.SuspiciousVolume,
            ModelHealthSeverity.Warning,
            "Oda hacmi geometriyle uyumsuz görünüyor",
            $"Kayıtlı hacim {project.RoomVolume:G4} m³, geometrik tahmin {estimate.VolumeM3:G4} m³. Hacim kaynağını doğrulayın."));
    }

    private static void AddGridFindings(
        AcousticsProject project,
        ModelHealthOptions options,
        List<ModelHealthFinding> findings)
    {
        var grid = (project.SimulationSettings ?? project.LastResult?.Parameters)?.ReceiverGrid;
        if (grid is null) return;

        if (!TryGetGridCellCount(grid, out var cells))
        {
            findings.Add(new ModelHealthFinding(
                ModelHealthIssueCode.ExcessiveGrid,
                ModelHealthSeverity.Error,
                "Izgara tanımı geçersiz",
                "Izgara sınırları ve hücre boyutu sonlu, pozitif ve hesaplanabilir olmalıdır."));
            return;
        }

        if (cells <= options.MaximumGridCells) return;
        findings.Add(new ModelHealthFinding(
            ModelHealthIssueCode.ExcessiveGrid,
            ModelHealthSeverity.Error,
            "Izgara haritası aşırı büyük",
            $"Izgara {cells:N0} hücre üretiyor; güvenli üst sınır {options.MaximumGridCells:N0} hücre."));
    }

    private static bool TryGetGridCellCount(ReceiverGrid grid, out long cells)
    {
        cells = 0;
        if (!double.IsFinite(grid.XMin) || !double.IsFinite(grid.XMax) ||
            !double.IsFinite(grid.YMin) || !double.IsFinite(grid.YMax) ||
            !double.IsFinite(grid.PlaneZ) || !double.IsFinite(grid.CellSize) ||
            grid.XMax <= grid.XMin || grid.YMax <= grid.YMin || grid.CellSize <= 0)
            return false;

        var columns = Math.Round((grid.XMax - grid.XMin) / grid.CellSize) + 1;
        var rows = Math.Round((grid.YMax - grid.YMin) / grid.CellSize) + 1;
        if (!double.IsFinite(columns) || !double.IsFinite(rows) ||
            columns < 1 || rows < 1 || columns > int.MaxValue || rows > int.MaxValue)
            return false;

        cells = (long)columns * (long)rows;
        return true;
    }

    private static IReadOnlyList<IReadOnlyList<int>> FindDuplicateSurfaceGroups(
        IReadOnlyList<ProjectSurface> surfaces,
        double metersPerUnit,
        double toleranceMeters)
    {
        var groups = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        foreach (var surface in surfaces)
        {
            var signature = CanonicalSurfaceSignature(surface.Vertices ?? [], metersPerUnit, toleranceMeters);
            if (signature is null) continue;
            if (!groups.TryGetValue(signature, out var ids)) groups[signature] = ids = [];
            ids.Add(surface.Id);
        }

        return groups.Values
            .Where(ids => ids.Count > 1)
            .Select(ids => (IReadOnlyList<int>)ids.ToArray())
            .ToArray();
    }

    private static string? CanonicalSurfaceSignature(
        IReadOnlyList<Point3D> vertices,
        double metersPerUnit,
        double toleranceMeters)
    {
        var points = RemoveConsecutiveDuplicateVertices(vertices, metersPerUnit, toleranceMeters, out _)
            .Where(IsFinite)
            .Select(p => Quantize(p, metersPerUnit, toleranceMeters))
            .ToArray();
        if (points.Length < 3) return null;

        string? best = null;
        for (var reverse = 0; reverse < 2; reverse++)
        {
            for (var offset = 0; offset < points.Length; offset++)
            {
                var ordered = new QuantizedPoint[points.Length];
                for (var i = 0; i < points.Length; i++)
                {
                    var index = reverse == 0
                        ? (offset + i) % points.Length
                        : (offset - i + points.Length) % points.Length;
                    ordered[i] = points[index];
                }

                var candidate = string.Join(";", ordered.Select(p => string.Create(
                    CultureInfo.InvariantCulture, $"{p.X:R},{p.Y:R},{p.Z:R}")));
                if (best is null || string.CompareOrdinal(candidate, best) < 0) best = candidate;
            }
        }

        return best;
    }

    private static IReadOnlyList<Point3D> RemoveConsecutiveDuplicateVertices(
        IReadOnlyList<Point3D> vertices,
        double metersPerUnit,
        double toleranceMeters,
        out int removedCount)
    {
        removedCount = 0;
        if (vertices.Count == 0) return [];

        var result = new List<Point3D>(vertices.Count);
        foreach (var point in vertices)
        {
            if (result.Count > 0 && AreClose(result[^1], point, metersPerUnit, toleranceMeters))
            {
                removedCount++;
                continue;
            }
            result.Add(point);
        }

        if (result.Count > 1 && AreClose(result[0], result[^1], metersPerUnit, toleranceMeters))
        {
            result.RemoveAt(result.Count - 1);
            removedCount++;
        }

        return result.ToArray();
    }

    private static bool AreClose(Point3D left, Point3D right, double metersPerUnit, double toleranceMeters)
    {
        if (!IsFinite(left) || !IsFinite(right)) return left.Equals(right);
        var dx = (left.X - right.X) * metersPerUnit;
        var dy = (left.Y - right.Y) * metersPerUnit;
        var dz = (left.Z - right.Z) * metersPerUnit;
        return dx * dx + dy * dy + dz * dz <= toleranceMeters * toleranceMeters;
    }

    private static double ComputeAreaM2(ProjectSurface surface, double metersPerUnit)
    {
        if (surface.Vertices is null || surface.Vertices.Count < 3 || surface.Vertices.Any(v => !IsFinite(v)))
            return 0;
        return SurfaceTransformService.ComputeArea(surface.Vertices) * metersPerUnit * metersPerUnit;
    }

    private static Topology BuildTopology(
        IReadOnlyList<ProjectSurface> surfaces,
        double metersPerUnit,
        double toleranceMeters)
    {
        var edges = new Dictionary<EdgeKey, List<EdgeUse>>();
        for (var surfaceIndex = 0; surfaceIndex < surfaces.Count; surfaceIndex++)
        {
            var vertices = surfaces[surfaceIndex].Vertices ?? [];
            if (vertices.Count < 2 || vertices.Any(v => !IsFinite(v))) continue;
            for (var vertexIndex = 0; vertexIndex < vertices.Count; vertexIndex++)
            {
                var a = Quantize(vertices[vertexIndex], metersPerUnit, toleranceMeters);
                var b = Quantize(vertices[(vertexIndex + 1) % vertices.Count], metersPerUnit, toleranceMeters);
                if (a == b) continue;
                var forward = Compare(a, b) <= 0;
                var key = forward ? new EdgeKey(a, b) : new EdgeKey(b, a);
                if (!edges.TryGetValue(key, out var uses)) edges[key] = uses = [];
                uses.Add(new EdgeUse(surfaceIndex, forward));
            }
        }

        var closed = edges.Count > 0 && edges.Values.All(uses =>
            uses.Count == 2 && uses[0].SurfaceIndex != uses[1].SurfaceIndex);
        return new Topology(edges, closed);
    }

    private static bool TryOrientSurfaces(
        IReadOnlyList<ProjectSurface> surfaces,
        double metersPerUnit,
        double toleranceMeters,
        out IReadOnlyList<ProjectSurface> oriented,
        out IReadOnlyList<int> changedSurfaceIds)
    {
        oriented = surfaces.Select(s => s with { Vertices = (s.Vertices ?? []).ToArray() }).ToArray();
        changedSurfaceIds = [];
        if (surfaces.Count == 0) return true;

        var topology = BuildTopology(surfaces, metersPerUnit, toleranceMeters);
        if (topology.Edges.Values.Any(uses =>
                uses.Count > 2 || uses.Select(u => u.SurfaceIndex).Distinct().Count() != uses.Count))
            return false;

        var adjacency = Enumerable.Range(0, surfaces.Count)
            .Select(_ => new List<OrientationConstraint>())
            .ToArray();
        foreach (var uses in topology.Edges.Values.Where(uses => uses.Count == 2))
        {
            var left = uses[0];
            var right = uses[1];
            var mustDiffer = left.Forward == right.Forward;
            adjacency[left.SurfaceIndex].Add(new OrientationConstraint(right.SurfaceIndex, mustDiffer));
            adjacency[right.SurfaceIndex].Add(new OrientationConstraint(left.SurfaceIndex, mustDiffer));
        }

        var flip = new bool?[surfaces.Count];
        var components = new List<IReadOnlyList<int>>();
        for (var start = 0; start < surfaces.Count; start++)
        {
            if (flip[start].HasValue) continue;
            flip[start] = false;
            var queue = new Queue<int>();
            var component = new List<int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);
                foreach (var constraint in adjacency[current])
                {
                    var expected = flip[current]!.Value ^ constraint.MustDiffer;
                    if (flip[constraint.Neighbor] is { } existing)
                    {
                        if (existing != expected) return false;
                        continue;
                    }
                    flip[constraint.Neighbor] = expected;
                    queue.Enqueue(constraint.Neighbor);
                }
            }
            components.Add(component);
        }

        // Kapalı bileşenlerde global yönü de dışa çevir. Açık levhalarda mutlak dış yön
        // tanımlı değildir; yalnız komşu yüzey tutarlılığı düzeltilir.
        foreach (var component in components)
        {
            var componentSet = component.ToHashSet();
            var componentClosed = topology.Edges
                .Where(pair => pair.Value.Any(use => componentSet.Contains(use.SurfaceIndex)))
                .All(pair => pair.Value.Count == 2 && pair.Value.All(use => componentSet.Contains(use.SurfaceIndex)));
            if (!componentClosed) continue;

            var provisional = component.Select(index => flip[index] == true
                ? surfaces[index] with { Vertices = surfaces[index].Vertices.Reverse().ToArray() }
                : surfaces[index]).ToArray();
            if (SignedVolumeM3(provisional, metersPerUnit) < 0)
            {
                foreach (var index in component) flip[index] = !flip[index]!.Value;
            }
        }

        var changed = new List<int>();
        var result = new ProjectSurface[surfaces.Count];
        for (var i = 0; i < surfaces.Count; i++)
        {
            if (flip[i] == true)
            {
                result[i] = surfaces[i] with { Vertices = surfaces[i].Vertices.Reverse().ToArray() };
                changed.Add(surfaces[i].Id);
            }
            else
            {
                result[i] = surfaces[i] with { Vertices = surfaces[i].Vertices.ToArray() };
            }
        }

        oriented = result;
        changedSurfaceIds = changed;
        return true;
    }

    private static double SignedVolumeM3(IReadOnlyList<ProjectSurface> surfaces, double metersPerUnit)
    {
        var volume = 0.0;
        foreach (var surface in surfaces)
        {
            if (surface.Vertices.Count < 3) continue;
            var origin = surface.Vertices[0];
            for (var i = 1; i < surface.Vertices.Count - 1; i++)
            {
                var b = surface.Vertices[i];
                var c = surface.Vertices[i + 1];
                var triple = origin.X * (b.Y * c.Z - b.Z * c.Y) +
                             origin.Y * (b.Z * c.X - b.X * c.Z) +
                             origin.Z * (b.X * c.Y - b.Y * c.X);
                volume += triple / 6.0;
            }
        }
        return volume * metersPerUnit * metersPerUnit * metersPerUnit;
    }

    private static IReadOnlyList<int> SurfaceIds(
        IEnumerable<KeyValuePair<EdgeKey, List<EdgeUse>>> edges,
        IReadOnlyList<ProjectSurface> surfaces) =>
        edges.SelectMany(pair => pair.Value)
            .Select(use => surfaces[use.SurfaceIndex].Id)
            .Distinct()
            .Order()
            .ToArray();

    private static QuantizedPoint Quantize(Point3D point, double metersPerUnit, double toleranceMeters)
    {
        if (!IsFinite(point)) return new QuantizedPoint(double.NaN, double.NaN, double.NaN);
        return new QuantizedPoint(
            Math.Round(point.X * metersPerUnit / toleranceMeters),
            Math.Round(point.Y * metersPerUnit / toleranceMeters),
            Math.Round(point.Z * metersPerUnit / toleranceMeters));
    }

    private static int Compare(QuantizedPoint left, QuantizedPoint right)
    {
        var x = left.X.CompareTo(right.X);
        if (x != 0) return x;
        var y = left.Y.CompareTo(right.Y);
        return y != 0 ? y : left.Z.CompareTo(right.Z);
    }

    private static bool IsFinite(Point3D point) =>
        double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z);

    private static void AddRepairChange(
        ICollection<ModelHealthRepairChange> changes,
        ModelHealthRepairAction action,
        IEnumerable<int> ids,
        string description)
    {
        var affected = ids.Distinct().Order().ToArray();
        if (affected.Length > 0)
            changes.Add(new ModelHealthRepairChange(action, description, affected));
    }

    private static void ValidateOptions(ModelHealthOptions options)
    {
        if (!double.IsFinite(options.TopologyToleranceMeters) || options.TopologyToleranceMeters <= 0 ||
            !double.IsFinite(options.ZeroAreaToleranceM2) || options.ZeroAreaToleranceM2 < 0 ||
            !double.IsFinite(options.MicroSurfaceAreaM2) || options.MicroSurfaceAreaM2 <= options.ZeroAreaToleranceM2 ||
            options.MaximumGridCells <= 0 ||
            !double.IsFinite(options.SuspiciousMinimumExtentMeters) || options.SuspiciousMinimumExtentMeters <= 0 ||
            !double.IsFinite(options.SuspiciousMaximumExtentMeters) ||
            options.SuspiciousMaximumExtentMeters <= options.SuspiciousMinimumExtentMeters)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Model sağlığı eşikleri geçersiz.");
        }
    }

    private readonly record struct QuantizedPoint(double X, double Y, double Z);
    private readonly record struct EdgeKey(QuantizedPoint A, QuantizedPoint B);
    private readonly record struct EdgeUse(int SurfaceIndex, bool Forward);
    private readonly record struct OrientationConstraint(int Neighbor, bool MustDiffer);
    private sealed record Topology(Dictionary<EdgeKey, List<EdgeUse>> Edges, bool IsClosedManifold);
}
