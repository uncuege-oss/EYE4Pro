namespace Kst.Acoustics.App.ViewModels;

public partial class MainWindowViewModel
{
    public void ApplyCategoryToSelectedSurface()
    {
        var targets = SelectedSurfaces.Count > 0
            ? SelectedSurfaces.ToArray()
            : (SelectedSurface is not null ? [SelectedSurface] : []);

        if (targets.Length == 0)
        {
            StatusMessage = "Lütfen önce en az bir yüzey seçin.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewCategoryName))
        {
            StatusMessage = "Yeni kategori adı boş olamaz.";
            return;
        }

        var targetCategory = NewCategoryName.Trim();
        foreach (var surface in targets)
        {
            surface.Category = targetCategory;
        }

        MarkDirty();
        InvalidateStoredSimulationResult("Yüzey kategorisi değiştirildi.");
        RebuildCategorySummaries();
        RefreshCategoryFilters();
        ApplyVisibilityFilter();
        StatusMessage = $"{targets.Length} yüzey yeni kategoriye atandı.";
    }

    public void ApplyCategoryToSelectedGroup()
    {
        if (SelectedCategory is null)
        {
            StatusMessage = "Lütfen önce bir kategori seçin.";
            return;
        }

        if (string.IsNullOrWhiteSpace(BatchTargetCategoryName))
        {
            StatusMessage = "Hedef kategori adı boş olamaz.";
            return;
        }

        var sourceCategory = SelectedCategory.Name;
        var targetCategory = BatchTargetCategoryName.Trim();
        var changedCount = 0;

        foreach (var surface in SurfacePreviews.Where(x => string.Equals(x.Category, sourceCategory, StringComparison.OrdinalIgnoreCase)))
        {
            surface.Category = targetCategory;
            changedCount++;
        }

        MarkDirty();
        InvalidateStoredSimulationResult("Yüzey kategorileri değiştirildi.");
        RebuildCategorySummaries();
        RefreshCategoryFilters();
        ApplyVisibilityFilter();
        SelectedCategory = CategorySummaries.FirstOrDefault(x => string.Equals(x.Name, targetCategory, StringComparison.OrdinalIgnoreCase));
        StatusMessage = $"{changedCount} yüzey '{sourceCategory}' kategorisinden '{targetCategory}' kategorisine taşındı.";
    }

    public void HideSelectedSurfaces()
    {
        var targets = SelectedSurfaces.Count > 0
            ? SelectedSurfaces.ToArray()
            : (SelectedSurface is not null ? [SelectedSurface] : []);

        if (targets.Length == 0)
        {
            StatusMessage = "Gizlemek için en az bir yüzey seçin.";
            return;
        }

        foreach (var surface in targets)
        {
            surface.IsVisible = false;
        }

        ApplyVisibilityFilter();
        StatusMessage = $"{targets.Length} yüzey gizlendi.";
    }

    public void IsolateSelectedSurfaces()
    {
        var targetIds = (SelectedSurfaces.Count > 0
            ? SelectedSurfaces.Select(x => x.Id)
            : (SelectedSurface is not null ? [SelectedSurface.Id] : []))
            .ToHashSet();

        if (targetIds.Count == 0)
        {
            StatusMessage = "İzole etmek için en az bir yüzey seçin.";
            return;
        }

        foreach (var surface in SurfacePreviews)
        {
            surface.IsVisible = targetIds.Contains(surface.Id);
        }

        ApplyVisibilityFilter();
        StatusMessage = $"{targetIds.Count} yüzey izole edildi.";
    }

    public void ShowAllSurfaces()
    {
        foreach (var surface in SurfacePreviews)
        {
            surface.IsVisible = true;
        }

        ApplyVisibilityFilter();
        StatusMessage = "Tüm yüzeyler tekrar görünür yapıldı.";
    }
}
