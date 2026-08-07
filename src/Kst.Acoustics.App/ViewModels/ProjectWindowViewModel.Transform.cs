using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.App.ViewModels;

public sealed partial class ProjectWindowViewModel
{
    private sealed record TransformTarget(int SurfaceId, bool MirrorOfSelected);

    // ── Undo için önceki durum ────────────────────────────────────────────────
    private IReadOnlyList<(int SurfaceId, IReadOnlyList<Point3D> Vertices)>? _lastTransformSnapshot;
    private IReadOnlyList<(Guid PanelId, double AciDerece, double SarkmaCikmaM)>? _lastPanelTransformSnapshot;

    // ── Seçili yüzey bilgisi ──────────────────────────────────────────────────

    public string SelectedSurfaceName
    {
        get => _selectedSurfaceName;
        private set => SetProperty(ref _selectedSurfaceName, value);
    }
    private string _selectedSurfaceName = string.Empty;

    public string SelectedSurfaceCentroid
    {
        get => _selectedSurfaceCentroid;
        private set => SetProperty(ref _selectedSurfaceCentroid, value);
    }
    private string _selectedSurfaceCentroid = string.Empty;

    public bool HasSelectedSurface
    {
        get => _hasSelectedSurface;
        private set
        {
            if (SetProperty(ref _hasSelectedSurface, value))
                OnPropertyChanged(nameof(CanApplyTransform));
        }
    }
    private bool _hasSelectedSurface;

    public bool CanApplyTransform => HasSelectedSurface || SelectedSurfaceIds.Count > 0;

    public bool CanDeleteSelectedSurface => SelectedSurfaceIds.Count > 0 || HighlightedSurfaceId is not null;

    public bool CanUndo
    {
        get => _canUndo;
        private set => SetProperty(ref _canUndo, value);
    }
    private bool _canUndo;

    // ── Öteleme parametreleri ─────────────────────────────────────────────────

    public double? TranslateDX
    {
        get => _translateDX;
        set => SetProperty(ref _translateDX, value);
    }
    private double? _translateDX = 0.0;

    public double? TranslateDY
    {
        get => _translateDY;
        set => SetProperty(ref _translateDY, value);
    }
    private double? _translateDY = 0.0;

    public double? TranslateDZ
    {
        get => _translateDZ;
        set => SetProperty(ref _translateDZ, value);
    }
    private double? _translateDZ = 0.0;

    // ── Rotasyon parametreleri ────────────────────────────────────────────────

    public IReadOnlyList<string> RotationAxisOptions { get; } = ["X ekseni", "Y ekseni", "Z ekseni"];

    public int RotationAxisIndex
    {
        get => _rotationAxisIndex;
        set => SetProperty(ref _rotationAxisIndex, value);
    }
    private int _rotationAxisIndex = 2; // Z varsayılan

    public double? RotationAngleDeg
    {
        get => _rotationAngleDeg;
        set => SetProperty(ref _rotationAngleDeg, value);
    }
    private double? _rotationAngleDeg = 5.0;

    // ── HighlightedSurfaceId değiştiğinde çağrılır ────────────────────────────

    internal void RefreshSelectedSurface(int? id)
    {
        if (id is null)
        {
            RefreshSelectedSurfaces();
            return;
        }

        var surf = _workingSurfaces.FirstOrDefault(s => s.Id == id.Value);
        if (surf is null)
        {
            RefreshSelectedSurfaces();
            return;
        }

        var c = SurfaceTransformService.ComputeCentroid(surf.Vertices);
        HasSelectedSurface      = true;
        SelectedSurfaceName     = $"{surf.Name}  [{surf.Category}]";
        SelectedSurfaceCentroid = $"({c.X:F2}, {c.Y:F2}, {c.Z:F2})";
    }

    internal void RefreshSelectedSurfaces()
    {
        var ids = SelectedSurfaceIds.Where(id => _workingSurfaces.Any(s => s.Id == id)).Distinct().ToList();
        if (ids.Count == 0)
        {
            HasSelectedSurface      = HighlightedSurfaceId is not null;
            if (!HasSelectedSurface)
            {
                SelectedSurfaceName     = string.Empty;
                SelectedSurfaceCentroid = string.Empty;
            }
            return;
        }

        HasSelectedSurface = true;
        if (ids.Count == 1)
        {
            RefreshSelectedSurface(ids[0]);
            return;
        }

        SelectedSurfaceName = $"{ids.Count} yüzey seçili";
        SelectedSurfaceCentroid = "Çoklu seçim";
    }

    // ── Komutlar ─────────────────────────────────────────────────────────────

    public void ApplyRotation()
    {
        var targets = GetTransformTargets(includeMirrorPanels: true);
        if (targets.Count == 0) return;

        _lastTransformSnapshot = targets
            .Select(t => _workingSurfaces.FirstOrDefault(s => s.Id == t.SurfaceId))
            .Where(s => s is not null)
            .Select(s => (s!.Id, s.Vertices))
            .ToList();
        CanUndo = true;

        var axis    = (RotationAxis)RotationAxisIndex;
        var angle   = RotationAngleDeg ?? 0.0;
        if (TryApplyGeneratedPanelRotation(targets, angle))
            return;

        _lastPanelTransformSnapshot = null;
        foreach (var target in targets)
        {
            var idx = _workingSurfaces.FindIndex(s => s.Id == target.SurfaceId);
            if (idx < 0) continue;
            var signedAngle = target.MirrorOfSelected ? -angle : angle;
            var rotated = SurfaceTransformService.RotateAroundCentroid(_workingSurfaces[idx], axis, signedAngle);
            _workingSurfaces[idx] = ClampGeneratedPanelBelowCeiling(rotated);
        }
        NotifySurfacesChanged();

        RefreshSelectedSurfaces();
        StatusMessage = $"{targets.Count} yüzey {angle:+0.#;-0.#}° döndürüldü ({axis} ekseni).";
    }

    public void ApplyTranslation()
    {
        var targets = GetTransformTargets(includeMirrorPanels: true);
        if (targets.Count == 0) return;

        _lastTransformSnapshot = targets
            .Select(t => _workingSurfaces.FirstOrDefault(s => s.Id == t.SurfaceId))
            .Where(s => s is not null)
            .Select(s => (s!.Id, s.Vertices))
            .ToList();
        _lastPanelTransformSnapshot = null;
        CanUndo = true;

        var dx = TranslateDX ?? 0.0;
        var dy = TranslateDY ?? 0.0;
        var dz = TranslateDZ ?? 0.0;
        foreach (var target in targets)
        {
            var idx = _workingSurfaces.FindIndex(s => s.Id == target.SurfaceId);
            if (idx < 0) continue;
            var signedDx = target.MirrorOfSelected ? -dx : dx;
            if (_workingSurfaces[idx].Category.Equals("DUVAR-PANEL-SOL", StringComparison.OrdinalIgnoreCase))
                signedDx = dx;
            else if (_workingSurfaces[idx].Category.Equals("DUVAR-PANEL-SAG", StringComparison.OrdinalIgnoreCase))
                signedDx = -dx;

            _workingSurfaces[idx] = SurfaceTransformService.Translate(_workingSurfaces[idx], signedDx, dy, dz);
        }
        NotifySurfacesChanged();

        RefreshSelectedSurfaces();
        StatusMessage = $"{targets.Count} yüzey taşındı: ΔX={dx:+0.##;-0.##}, ΔY={dy:+0.##;-0.##}, ΔZ={dz:+0.##;-0.##}.";
    }

    public void UndoLastTransform()
    {
        if (_lastPanelTransformSnapshot is { } panelSnap && PanelKonfigurasyon is not null)
        {
            foreach (var item in panelSnap)
            {
                var panel = PanelKonfigurasyon.Paneller.FirstOrDefault(p => p.Id == item.PanelId);
                if (panel is null)
                    continue;

                panel.AciDerece = item.AciDerece;
                panel.SarkmaCikmaM = item.SarkmaCikmaM;
                PanelListesi.FirstOrDefault(r => r.Panel == panel)?.UpdateDisplay();
            }

            _lastPanelTransformSnapshot = null;
            _lastTransformSnapshot = null;
            CanUndo = false;

            SenkronizeYuzeyler();
            RefreshSelectedSurfaces();
            StatusMessage = "Son panel dönüşümü geri alındı.";
            return;
        }

        if (_lastTransformSnapshot is not { } snap) return;

        foreach (var item in snap)
        {
            var idx = _workingSurfaces.FindIndex(s => s.Id == item.SurfaceId);
            if (idx < 0) continue;
            _workingSurfaces[idx] = _workingSurfaces[idx] with { Vertices = item.Vertices };
        }

        _lastTransformSnapshot = null;
        _lastPanelTransformSnapshot = null;
        CanUndo = false;

        NotifySurfacesChanged();
        RefreshSelectedSurfaces();
        StatusMessage = "Son dönüşüm geri alındı.";
    }

    public void ResetAllTransforms()
    {
        for (var i = 0; i < _workingSurfaces.Count; i++)
        {
            var orig = _originalSurfaces.FirstOrDefault(s => s.Id == _workingSurfaces[i].Id);
            if (orig is not null) _workingSurfaces[i] = orig;
        }

        _lastTransformSnapshot = null;
        _lastPanelTransformSnapshot = null;
        CanUndo = false;

        if (PanelKonfigurasyon is not null)
        {
            foreach (var panel in PanelKonfigurasyon.Paneller)
            {
                panel.AciDerece = 0;
                ApplyPanelSarkmaDelta(panel, PanelKonfigurasyon.SarkmaCikmaM - panel.SarkmaCikmaM);
                panel.SarkmaCikmaM = PanelKonfigurasyon.SarkmaCikmaM;
                PanelListesi.FirstOrDefault(r => r.Panel == panel)?.UpdateDisplay();
            }

            SelectedSurfaceIds = [];
            HighlightedSurfaceId = null;
            SenkronizeYuzeyler();
            StatusMessage = "Tüm dönüşümler sıfırlandı.";
            return;
        }

        NotifySurfacesChanged();
        RefreshSelectedSurface(HighlightedSurfaceId);
        StatusMessage = "Tüm dönüşümler sıfırlandı.";
    }

    public void DeleteSelectedSurfaces()
    {
        var ids = SelectedSurfaceIds.Count > 0
            ? SelectedSurfaceIds.Distinct().ToList()
            : HighlightedSurfaceId is { } id ? [id] : new List<int>();

        if (ids.Count == 0)
            return;

        var panelIdsToRemove = ids
            .Select(GetPanelForSurfaceId)
            .Where(p => p is not null)
            .Select(p => p!.Id)
            .Distinct()
            .ToHashSet();

        var removedPanelCount = 0;
        if (PanelKonfigurasyon is not null && panelIdsToRemove.Count > 0)
        {
            removedPanelCount = PanelKonfigurasyon.Paneller.RemoveAll(p => panelIdsToRemove.Contains(p.Id));

            foreach (var row in PanelListesi.Where(r => panelIdsToRemove.Contains(r.Panel.Id)).ToList())
                PanelListesi.Remove(row);
        }

        var nonPanelIds = ids
            .Where(id => GetPanelForSurfaceId(id) is null)
            .ToHashSet();
        var removedSurfaceCount = nonPanelIds.Count > 0
            ? _workingSurfaces.RemoveAll(s => nonPanelIds.Contains(s.Id))
            : 0;

        SelectedSurfaceIds = [];
        HighlightedSurfaceId = null;
        _lastTransformSnapshot = null;
        _lastPanelTransformSnapshot = null;
        CanUndo = false;

        if (removedPanelCount > 0)
            SenkronizeYuzeyler();
        else
        {
            RebuildCategoryNodes();
            MarkDirty();
            NotifySurfacesChanged();
        }

        RefreshSelectedSurfaces();
        StatusMessage = $"{removedPanelCount + removedSurfaceCount} yüzey silindi.";
    }

    private bool TryApplyGeneratedPanelRotation(IReadOnlyList<TransformTarget> targets, double angle)
    {
        if (PanelKonfigurasyon is null || targets.Count == 0)
            return false;

        var panelTargets = targets
            .Select(t => (Target: t, Panel: GetPanelForSurfaceId(t.SurfaceId)))
            .ToList();
        if (panelTargets.Any(t => t.Panel is null))
            return false;

        _lastPanelTransformSnapshot = PanelKonfigurasyon.Paneller
            .Select(p => (p.Id, p.AciDerece, p.SarkmaCikmaM))
            .ToList();
        _lastTransformSnapshot = null;
        CanUndo = true;

        foreach (var item in panelTargets)
        {
            var panel = item.Panel!;
            var delta = panel.Tip switch
            {
                PanelTipi.DuvarSol => -angle,
                PanelTipi.DuvarSag => angle,
                _ => item.Target.MirrorOfSelected ? -angle : angle
            };

            panel.AciDerece += delta;
            PanelListesi.FirstOrDefault(r => r.Panel == panel)?.UpdateDisplay();
        }

        SenkronizeYuzeyler();
        RefreshSelectedSurfaces();
        StatusMessage = $"{panelTargets.Count} panel modülü {angle:+0.#;-0.#}° güncellendi.";
        return true;
    }

    private IReadOnlyList<TransformTarget> GetTransformTargets(bool includeMirrorPanels)
    {
        var selectedIds = SelectedSurfaceIds.Count > 0
            ? SelectedSurfaceIds.ToList()
            : HighlightedSurfaceId is { } id ? [id] : new List<int>();

        var targets = selectedIds
            .Distinct()
            .Select(id => new TransformTarget(id, MirrorOfSelected: false))
            .ToList();

        if (includeMirrorPanels)
        {
            foreach (var selectedId in selectedIds.ToArray())
            {
                var mirror = FindMirrorPanelSurfaceId(selectedId);
                if (mirror is { } mirrorId && targets.All(t => t.SurfaceId != mirrorId))
                    targets.Add(new TransformTarget(mirrorId, MirrorOfSelected: true));
            }
        }

        return targets;
    }

    private int? FindMirrorPanelSurfaceId(int surfaceId)
    {
        var surface = _workingSurfaces.FirstOrDefault(s => s.Id == surfaceId);
        if (surface is null)
            return null;

        const string left = "DUVAR-PANEL-SOL";
        const string right = "DUVAR-PANEL-SAG";
        string? mirrorCategory = surface.Category.Equals(left, StringComparison.OrdinalIgnoreCase)
            ? right
            : surface.Category.Equals(right, StringComparison.OrdinalIgnoreCase)
                ? left
                : null;
        if (mirrorCategory is null)
            return null;

        var suffix = surface.Name.Split('-').LastOrDefault();
        if (string.IsNullOrWhiteSpace(suffix))
            return null;

        return _workingSurfaces.FirstOrDefault(s =>
            s.Category.Equals(mirrorCategory, StringComparison.OrdinalIgnoreCase) &&
            s.Name.EndsWith("-" + suffix, StringComparison.OrdinalIgnoreCase))?.Id;
    }

    private AcousticPanel? GetPanelForSurfaceId(int surfaceId)
    {
        var surface = _workingSurfaces.FirstOrDefault(s => s.Id == surfaceId);
        if (surface is null || !IsGeneratedPanelSurface(surface))
            return null;

        var tip = surface.Category.ToUpperInvariant() switch
        {
            "TAVAN-PANEL" => PanelTipi.Tavan,
            "DUVAR-PANEL-SOL" => PanelTipi.DuvarSol,
            "DUVAR-PANEL-SAG" => PanelTipi.DuvarSag,
            _ => (PanelTipi?)null
        };
        if (tip is null)
            return null;

        var suffix = surface.Name.Split('-').LastOrDefault();
        if (!int.TryParse(suffix, out var dilimNo))
            return null;

        return PanelKonfigurasyon?.Paneller.FirstOrDefault(p => p.Tip == tip.Value && p.DilimNo == dilimNo);
    }

    private ProjectSurface ClampGeneratedPanelBelowCeiling(ProjectSurface surface)
    {
        if (!IsGeneratedPanelSurface(surface))
            return surface;

        var ceilingZ = GetPanelHostSurfaces()
            .SelectMany(s => s.Vertices)
            .Select(v => v.Z)
            .DefaultIfEmpty(0)
            .Max();

        var maxZ = surface.Vertices.Select(v => v.Z).DefaultIfEmpty(ceilingZ).Max();
        var overflow = maxZ - ceilingZ;
        if (overflow <= 1e-6)
            return surface;

        return SurfaceTransformService.Translate(surface, 0, 0, -overflow);
    }

    private static bool IsGeneratedPanelSurface(ProjectSurface surface)
    {
        return surface.Category.Equals("TAVAN-PANEL", StringComparison.OrdinalIgnoreCase) ||
               surface.Category.Equals("DUVAR-PANEL-SOL", StringComparison.OrdinalIgnoreCase) ||
               surface.Category.Equals("DUVAR-PANEL-SAG", StringComparison.OrdinalIgnoreCase);
    }
}
