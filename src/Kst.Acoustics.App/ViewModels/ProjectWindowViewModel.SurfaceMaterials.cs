using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.App.ViewModels;

/// <summary>
/// Materyaller sekmesindeki yüzey listesi panelinin tek satırı. Bir yüzey için
/// halihazırda çözülmüş (override veya kategori varsayılanı) materyali ve override
/// işaretini gösterir; UI'da seçim kutusunu kontrol eder.
/// </summary>
public sealed class SurfaceMaterialRowViewModel : ViewModelBase
{
    public int    Id       { get; }
    public string Name     { get; }
    public string Category { get; }

    public AcousticMaterial AssignedMaterial
    {
        get => _assignedMaterial;
        private set => SetProperty(ref _assignedMaterial, value);
    }
    private AcousticMaterial _assignedMaterial;

    public bool IsOverride
    {
        get => _isOverride;
        private set
        {
            if (SetProperty(ref _isOverride, value))
            {
                OnPropertyChanged(nameof(MaterialDisplay));
                OnPropertyChanged(nameof(OverrideBadge));
            }
        }
    }
    private bool _isOverride;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
    private bool _isSelected;

    public string MaterialDisplay => AssignedMaterial.DisplayName;
    public string OverrideBadge   => IsOverride ? "★" : "";

    public SurfaceMaterialRowViewModel(int id, string name, string category, AcousticMaterial assigned, bool isOverride)
    {
        Id = id;
        Name = name;
        Category = category;
        _assignedMaterial = assigned;
        _isOverride = isOverride;
    }

    internal void Update(AcousticMaterial assigned, bool isOverride)
    {
        AssignedMaterial = assigned;
        IsOverride = isOverride;
        // OverrideBadge & MaterialDisplay direct
        OnPropertyChanged(nameof(MaterialDisplay));
    }
}

public sealed partial class ProjectWindowViewModel
{
    public const string SurfaceMaterialFilterAll = "(Tümü)";

    // Anahtar: surface.Id.ToString() — kalıcı saklama formatıyla aynı (AcousticsProject.SurfaceMaterials)
    private readonly Dictionary<string, string> _surfaceMaterialOverrides = new();

    private readonly List<SurfaceMaterialRowViewModel> _allSurfaceMaterialRows = new();

    public ObservableCollection<SurfaceMaterialRowViewModel> SurfaceMaterialRows { get; } = new();
    public ObservableCollection<string>                       SurfaceMaterialCategoryFilters { get; } = new();
    public ObservableCollection<AcousticMaterial>             SurfaceMaterialChoices { get; } = new();

    public string? SelectedSurfaceMaterialCategoryFilter
    {
        get => _selectedSurfaceMaterialCategoryFilter;
        set
        {
            if (SetProperty(ref _selectedSurfaceMaterialCategoryFilter, value))
                RefreshSurfaceMaterialFilter();
        }
    }
    private string? _selectedSurfaceMaterialCategoryFilter;

    public string SurfaceMaterialSearchText
    {
        get => _surfaceMaterialSearchText;
        set
        {
            if (SetProperty(ref _surfaceMaterialSearchText, value))
                RefreshSurfaceMaterialFilter();
        }
    }
    private string _surfaceMaterialSearchText = "";

    public AcousticMaterial? SelectedSurfaceMaterialChoice
    {
        get => _selectedSurfaceMaterialChoice;
        set => SetProperty(ref _selectedSurfaceMaterialChoice, value);
    }
    private AcousticMaterial? _selectedSurfaceMaterialChoice;

    public string SurfaceMaterialOverrideSummary
    {
        get => _surfaceMaterialOverrideSummary;
        private set => SetProperty(ref _surfaceMaterialOverrideSummary, value);
    }
    private string _surfaceMaterialOverrideSummary = "";

    private void InitSurfaceMaterials()
    {
        // Mevcut overrides'ı projeden al
        _surfaceMaterialOverrides.Clear();
        foreach (var kvp in _project.SurfaceMaterials)
        {
            if (!string.IsNullOrWhiteSpace(kvp.Value))
                _surfaceMaterialOverrides[kvp.Key] = kvp.Value;
        }

        // Kategori filtresi
        SurfaceMaterialCategoryFilters.Clear();
        SurfaceMaterialCategoryFilters.Add(SurfaceMaterialFilterAll);
        foreach (var cat in _workingSurfaces.Select(s => s.Category).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c, StringComparer.OrdinalIgnoreCase))
            SurfaceMaterialCategoryFilters.Add(cat);
        _selectedSurfaceMaterialCategoryFilter = SurfaceMaterialFilterAll;
        OnPropertyChanged(nameof(SelectedSurfaceMaterialCategoryFilter));

        // Materyal seçenekleri (favoriler)
        RefreshSurfaceMaterialChoices();

        // Satırları oluştur
        RebuildSurfaceMaterialRows();
    }

    private void RefreshSurfaceMaterialChoices()
    {
        SurfaceMaterialChoices.Clear();
        foreach (var m in MaterialDatabase.Instance.Favorites)
            SurfaceMaterialChoices.Add(m);
        if (!SurfaceMaterialChoices.Contains(MaterialLibrary.Default))
            SurfaceMaterialChoices.Insert(0, MaterialLibrary.Default);
        SelectedSurfaceMaterialChoice = SurfaceMaterialChoices.FirstOrDefault();
    }

    private void RebuildSurfaceMaterialRows()
    {
        _allSurfaceMaterialRows.Clear();
        foreach (var s in _workingSurfaces.OrderBy(s => s.Category).ThenBy(s => s.Id))
        {
            var (mat, isOverride) = ResolveSurfaceMaterial(s);
            _allSurfaceMaterialRows.Add(new SurfaceMaterialRowViewModel(s.Id, s.Name, s.Category, mat, isOverride));
        }
        RefreshSurfaceMaterialFilter();
        RefreshSurfaceMaterialOverrideSummary();
        RefreshCategoryOverrideBadges();
    }

    private void RefreshSurfaceMaterialFilter()
    {
        var category = SelectedSurfaceMaterialCategoryFilter;
        var search = SurfaceMaterialSearchText?.Trim() ?? "";
        bool categoryAll = string.IsNullOrEmpty(category) || category == SurfaceMaterialFilterAll;

        SurfaceMaterialRows.Clear();
        foreach (var row in _allSurfaceMaterialRows)
        {
            if (!categoryAll && !row.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.IsNullOrEmpty(search))
            {
                bool match = row.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                          || row.Category.Contains(search, StringComparison.OrdinalIgnoreCase)
                          || row.Id.ToString(CultureInfo.InvariantCulture).Contains(search);
                if (!match) continue;
            }

            SurfaceMaterialRows.Add(row);
        }
    }

    private void RefreshSurfaceMaterialOverrideSummary()
    {
        var n = _surfaceMaterialOverrides.Count;
        SurfaceMaterialOverrideSummary = n == 0
            ? "Tüm yüzeyler kategori varsayılanını kullanıyor."
            : $"{n} yüzey kendi malzemesini kullanıyor.";
    }

    /// <summary>Seçili tüm satırlar için override materyali atar.</summary>
    public void AssignMaterialToSelectedSurfaces()
    {
        var mat = SelectedSurfaceMaterialChoice;
        if (mat is null)
        {
            StatusMessage = "Önce atanacak malzemeyi seçin.";
            return;
        }

        var selected = SurfaceMaterialRows.Where(r => r.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "Önce listede yüzey(ler) seçin.";
            return;
        }

        foreach (var row in selected)
        {
            _surfaceMaterialOverrides[row.Id.ToString(CultureInfo.InvariantCulture)] = mat.Id;
            row.Update(mat, isOverride: true);
        }

        InvalidateSimulationResult("Yüzey materyali değiştirildi.");
        MarkDirty($"{selected.Count} yüzeye materyal atandı");
        RefreshSurfaceMaterialOverrideSummary();
        RefreshCategoryOverrideBadges();
        StatusMessage = $"{selected.Count} yüzeye {mat.DisplayName} atandı.";
    }

    /// <summary>Seçili satırların override'ını kaldırır; kategori varsayılanına döner.</summary>
    public void ResetSelectedSurfacesToDefault()
    {
        var selected = SurfaceMaterialRows.Where(r => r.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "Önce listede yüzey(ler) seçin.";
            return;
        }

        foreach (var row in selected)
        {
            _surfaceMaterialOverrides.Remove(row.Id.ToString(CultureInfo.InvariantCulture));
            var surface = _workingSurfaces.FirstOrDefault(s => s.Id == row.Id);
            if (surface is null) continue;
            var (mat, isOverride) = ResolveSurfaceMaterial(surface);
            row.Update(mat, isOverride);
        }

        InvalidateSimulationResult("Yüzey materyal override'ı kaldırıldı.");
        MarkDirty($"{selected.Count} yüzey materyali varsayılana döndürüldü");
        RefreshSurfaceMaterialOverrideSummary();
        RefreshCategoryOverrideBadges();
        StatusMessage = $"{selected.Count} yüzey kategori varsayılanına döndürüldü.";
    }

    private (AcousticMaterial Material, bool IsOverride) ResolveSurfaceMaterial(ProjectSurface surface)
    {
        // Override kontrolü
        if (_surfaceMaterialOverrides.TryGetValue(surface.Id.ToString(CultureInfo.InvariantCulture), out var overrideId)
            && !string.IsNullOrEmpty(overrideId))
        {
            var m = MaterialDatabase.Instance.FindById(overrideId) ?? MaterialLibrary.FindById(overrideId);
            if (m is not null) return (m, true);
        }

        // Kategori varsayılanı (CategoryMaterialRows üzerinden, çünkü kullanıcı UI'da değiştirmiş olabilir)
        var catRow = CategoryMaterialRows.FirstOrDefault(r =>
            r.Category.Equals(surface.Category, StringComparison.OrdinalIgnoreCase));
        if (catRow?.SelectedMaterial is { } catMat) return (catMat, false);

        return (MaterialLibrary.Default, false);
    }

    /// <summary>Kategori satırı materyali değiştiğinde veya yüzey override'ı değiştiğinde
    /// kategori satırının override sayacını güncelle.</summary>
    private void RefreshCategoryOverrideBadges()
    {
        // Her kategori için: bu kategoride kaç yüzey override'a sahip
        var overrideCountsByCategory = _workingSurfaces
            .Where(s => _surfaceMaterialOverrides.ContainsKey(s.Id.ToString(CultureInfo.InvariantCulture)))
            .GroupBy(s => s.Category, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        foreach (var row in CategoryMaterialRows)
        {
            overrideCountsByCategory.TryGetValue(row.Category, out var count);
            row.OverrideCount = count;
        }
    }

    /// <summary>Kategori atama UI'ında bir materyal değişirse, override'ı olmayan satırların
    /// gösterimini güncelle.</summary>
    internal void OnCategoryMaterialChanged(string category)
    {
        InvalidateSimulationResult($"'{category}' kategori materyali değiştirildi.");
        MarkDirty($"'{category}' kategori materyali değiştirildi");
        foreach (var row in _allSurfaceMaterialRows)
        {
            if (!row.Category.Equals(category, StringComparison.OrdinalIgnoreCase)) continue;
            if (row.IsOverride) continue;
            var surface = _workingSurfaces.FirstOrDefault(s => s.Id == row.Id);
            if (surface is null) continue;
            var (mat, isOverride) = ResolveSurfaceMaterial(surface);
            row.Update(mat, isOverride);
        }
    }

    /// <summary>
    /// DataGrid'in mevcut seçimi değiştiğinde çağrılır: tüm satırların IsSelected'ı
    /// senkron edilir, 3D viewport'taki HighlightedSurfaceId + SelectedSurfaceIds güncellenir.
    /// </summary>
    public void UpdateSurfaceMaterialSelection(IReadOnlyList<SurfaceMaterialRowViewModel> selectedRows)
    {
        var selectedSet = selectedRows.Select(r => r.Id).ToHashSet();
        foreach (var row in _allSurfaceMaterialRows)
            row.IsSelected = selectedSet.Contains(row.Id);

        SelectedSurfaceIds   = selectedSet.ToArray();
        HighlightedSurfaceId = selectedRows.Count > 0 ? selectedRows[^1].Id : null;
    }

    internal IReadOnlyDictionary<string, string> BuildSurfaceMaterialMap()
    {
        // Sadece geçerli yüzey ID'leri için tut
        var validIds = _workingSurfaces.Select(s => s.Id.ToString(CultureInfo.InvariantCulture)).ToHashSet();
        return _surfaceMaterialOverrides
            .Where(kvp => validIds.Contains(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
    }
}
