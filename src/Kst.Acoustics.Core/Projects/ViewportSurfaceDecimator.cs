namespace Kst.Acoustics.Core.Projects;

/// <summary>
/// Büyük modellerde yalnız viewport için kararlı ve kategoriler arası dengeli
/// bir yüzey alt kümesi seçer. Domain modeli değişmez; kayıt ve simülasyon her
/// zaman tam geometriyi kullanır.
/// </summary>
public static class ViewportSurfaceDecimator
{
    public static ViewportSurfaceReduction Reduce(
        IReadOnlyList<ProjectSurface> surfaces,
        int limit,
        IEnumerable<int>? pinnedSurfaceIds = null)
    {
        ArgumentNullException.ThrowIfNull(surfaces);
        if (limit < 1) throw new ArgumentOutOfRangeException(nameof(limit));

        if (surfaces.Count <= limit)
            return new ViewportSurfaceReduction(surfaces.ToArray(), surfaces.Count, false, 0);

        var pinned = (pinnedSurfaceIds ?? [])
            .ToHashSet();
        var selectedIds = new HashSet<int>();

        // Kullanıcının seçtiği/vurguladığı yüzeyler daima önceliklidir.
        foreach (var surface in surfaces
                     .Where(surface => pinned.Contains(surface.Id))
                     .OrderBy(surface => surface.Id))
        {
            if (selectedIds.Count >= limit) break;
            selectedIds.Add(surface.Id);
        }
        var pinnedIncluded = selectedIds.Count;

        // Odanın görsel kapsamı daralmasın: altı eksendeki uç yüzeyleri koru.
        var extrema = FindExtrema(surfaces);
        AddUntilFull(extrema, selectedIds, limit);

        // Her kategoriden en az bir temsilci almaya çalış.
        var categoryQueues = surfaces
            .GroupBy(surface => surface.Category ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new Queue<ProjectSurface>(group
                .OrderBy(surface => surface.Id)
                .ThenBy(surface => surface.Name, StringComparer.OrdinalIgnoreCase)))
            .ToArray();

        foreach (var queue in categoryQueues)
        {
            while (queue.Count > 0 && selectedIds.Contains(queue.Peek().Id)) queue.Dequeue();
            if (queue.Count > 0 && selectedIds.Count < limit)
                selectedIds.Add(queue.Dequeue().Id);
        }

        // Kalan kotayı kategoriler arası round-robin dağıt. Bu, tek bir büyük
        // katmanın küçük ama önemli kategorileri viewport'tan silmesini önler.
        while (selectedIds.Count < limit)
        {
            var progressed = false;
            foreach (var queue in categoryQueues)
            {
                while (queue.Count > 0 && selectedIds.Contains(queue.Peek().Id)) queue.Dequeue();
                if (queue.Count == 0) continue;
                selectedIds.Add(queue.Dequeue().Id);
                progressed = true;
                if (selectedIds.Count >= limit) break;
            }

            if (!progressed) break;
        }

        // Orijinal çizim sırasını koru; aynı girdi her zaman aynı çıktıyı verir.
        var reduced = surfaces.Where(surface => selectedIds.Contains(surface.Id)).ToArray();
        return new ViewportSurfaceReduction(reduced, surfaces.Count, true, pinnedIncluded);
    }

    private static IReadOnlyList<ProjectSurface> FindExtrema(IReadOnlyList<ProjectSurface> surfaces)
    {
        var valid = new List<SurfaceBounds>();
        foreach (var surface in surfaces)
        {
            var points = surface.Vertices.Where(point =>
                double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z)).ToArray();
            if (points.Length == 0) continue;
            valid.Add(new SurfaceBounds(
                surface,
                points.Min(point => point.X), points.Max(point => point.X),
                points.Min(point => point.Y), points.Max(point => point.Y),
                points.Min(point => point.Z), points.Max(point => point.Z)));
        }
        if (valid.Count == 0) return [];

        // ThenBy(Id) eşit koordinatlarda sonucu kararlı yapar.
        return
        [
            valid.OrderBy(item => item.MinX).ThenBy(item => item.Surface.Id).First().Surface,
            valid.OrderByDescending(item => item.MaxX).ThenBy(item => item.Surface.Id).First().Surface,
            valid.OrderBy(item => item.MinY).ThenBy(item => item.Surface.Id).First().Surface,
            valid.OrderByDescending(item => item.MaxY).ThenBy(item => item.Surface.Id).First().Surface,
            valid.OrderBy(item => item.MinZ).ThenBy(item => item.Surface.Id).First().Surface,
            valid.OrderByDescending(item => item.MaxZ).ThenBy(item => item.Surface.Id).First().Surface
        ];
    }

    private static void AddUntilFull(
        IEnumerable<ProjectSurface> candidates,
        HashSet<int> selectedIds,
        int limit)
    {
        foreach (var surface in candidates)
        {
            if (selectedIds.Count >= limit) return;
            selectedIds.Add(surface.Id);
        }
    }

    private sealed record SurfaceBounds(
        ProjectSurface Surface,
        double MinX,
        double MaxX,
        double MinY,
        double MaxY,
        double MinZ,
        double MaxZ);
}

public sealed record ViewportSurfaceReduction(
    IReadOnlyList<ProjectSurface> Surfaces,
    int OriginalCount,
    bool IsReduced,
    int PinnedSurfaceCount)
{
    public int HiddenCount => Math.Max(0, OriginalCount - Surfaces.Count);
}
