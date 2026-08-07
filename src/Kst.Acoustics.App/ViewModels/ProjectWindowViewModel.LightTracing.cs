using System.Collections.ObjectModel;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Simulation;

namespace Kst.Acoustics.App.ViewModels;

public sealed partial class ProjectWindowViewModel
{
    // ── Kaynak ────────────────────────────────────────────────────────────────

    public AcousticPoint? SelectedLightSource
    {
        get => _selectedLightSource;
        set { SetProperty(ref _selectedLightSource, value); OnPropertyChanged(nameof(CanRunLight)); }
    }
    private AcousticPoint? _selectedLightSource;

    // ── Birleşik yüzey listesi (hem ayna hem alıcı seçimi burada) ─────────────

    public ObservableCollection<MirrorSurfaceViewModel> MirrorSurfaceOptions { get; } = [];

    // ── Parametreler ──────────────────────────────────────────────────────────

    public int LightRayCount
    {
        get => _lightRayCount;
        set => SetProperty(ref _lightRayCount, Math.Max(100, value));
    }
    private int _lightRayCount = 2000;

    public int LightMaxBounces
    {
        get => _lightMaxBounces;
        set => SetProperty(ref _lightMaxBounces, Math.Clamp(value, 1, 30));
    }
    private int _lightMaxBounces = 5;

    // ── Sonuçlar ──────────────────────────────────────────────────────────────

    public IReadOnlyDictionary<int, double>? LightHitMap
    {
        get => _lightHitMap;
        private set => SetProperty(ref _lightHitMap, value);
    }
    private IReadOnlyDictionary<int, double>? _lightHitMap;

    public IReadOnlyList<RaySegment>? LightRayPaths
    {
        get => _lightRayPaths;
        private set => SetProperty(ref _lightRayPaths, value);
    }
    private IReadOnlyList<RaySegment>? _lightRayPaths;

    public IReadOnlyList<LightHitPoint>? LightHitPoints
    {
        get => _lightHitPoints;
        private set => SetProperty(ref _lightHitPoints, value);
    }
    private IReadOnlyList<LightHitPoint>? _lightHitPoints;

    public IReadOnlySet<int>? LightMirrorIds
    {
        get => _lightMirrorIds;
        private set => SetProperty(ref _lightMirrorIds, value);
    }
    private IReadOnlySet<int>? _lightMirrorIds;

    public IReadOnlySet<int>? LightReceiverIds
    {
        get => _lightReceiverIds;
        private set => SetProperty(ref _lightReceiverIds, value);
    }
    private IReadOnlySet<int>? _lightReceiverIds;

    public string LightStatusText
    {
        get => _lightStatusText;
        private set => SetProperty(ref _lightStatusText, value);
    }
    private string _lightStatusText = "Kaynak seç, ayna ve alıcı yüzeyleri işaretle, İzle'ye bas.";

    public bool IsLightRunning
    {
        get => _isLightRunning;
        private set { SetProperty(ref _isLightRunning, value); OnPropertyChanged(nameof(CanRunLight)); }
    }
    private bool _isLightRunning;

    public bool CanRunLight => SelectedLightSource != null && !IsLightRunning;

    // ── Komutlar ──────────────────────────────────────────────────────────────

    public async Task RunLightTracing()
    {
        if (!CanRunLight) return;

        IsLightRunning  = true;
        LightStatusText = "⏳ Hesaplanıyor...";
        LightHitMap     = null;
        LightRayPaths   = null;
        LightMirrorIds  = null;
        LightReceiverIds = null;
        LightHitPoints  = null;

        var source      = SelectedLightSource!.Position;
        var mirrorIds   = MirrorSurfaceOptions.Where(m => m.IsMirror)
                              .Select(m => m.SurfaceId).ToHashSet();
        var receiverIds = MirrorSurfaceOptions.Where(m => m.IsReceiver)
                              .Select(m => m.SurfaceId).ToHashSet();
        var surfaces    = (IReadOnlyList<Kst.Acoustics.Core.Projects.ProjectSurface>)_workingSurfaces;
        var matMap      = BuildCategoryMaterialMap();
        var rays        = LightRayCount;
        var bounces     = LightMaxBounces;

        var result = await Task.Run(() =>
            LightTracingService.Run(source, mirrorIds, receiverIds, surfaces, matMap, rays, bounces));

        LightHitMap    = result.SurfaceIntensity;
        LightRayPaths  = result.RayPaths;
        LightMirrorIds = mirrorIds.Count > 0 ? mirrorIds : null;
        LightReceiverIds = receiverIds.Count > 0 ? receiverIds : null;
        LightHitPoints = result.ReceiverHitPoints.Count > 0 ? result.ReceiverHitPoints : null;

        var rcvCount = receiverIds.Count;
        var rcvLabel = rcvCount == 0 ? "(tüm yüzeyler)" : $"{rcvCount} alıcı yüzey";
        LightStatusText = result.TotalHits == 0
            ? "Uyarı: Alıcıya ulaşan ışın bulunamadı. Ayna ve alıcı yüzey seçimlerini kontrol edin."
            : $"{result.TotalHits} ışın → {rcvLabel}  |  {result.RayPaths.Count} yol gösteriliyor";
        IsLightRunning  = false;
    }

    public void ClearLightTracing()
    {
        LightHitMap     = null;
        LightRayPaths   = null;
        LightMirrorIds  = null;
        LightReceiverIds = null;
        LightHitPoints  = null;
        LightStatusText = "Kaynak seç, ayna ve alıcı yüzeyleri işaretle, İzle'ye bas.";
    }

    // ── Başlatıcı ─────────────────────────────────────────────────────────────

    internal void InitLightTracingOptions() => RefreshLightTracingOptions();

    internal void RefreshLightTracingOptions()
    {
        // Seçim durumlarını ID değil etiket ile sakla —
        // paneller yeniden üretilince ID değişir ama isim sabit kalır.
        var wasMirror   = MirrorSurfaceOptions.Where(m => m.IsMirror)  .Select(m => m.Label).ToHashSet();
        var wasReceiver = MirrorSurfaceOptions.Where(m => m.IsReceiver).Select(m => m.Label).ToHashSet();

        MirrorSurfaceOptions.Clear();

        foreach (var surf in _workingSurfaces)
        {
            var label = $"[{surf.Category}] {surf.Name}";
            var vm    = new MirrorSurfaceViewModel(surf.Id, label);
            vm.IsMirror   = wasMirror.Contains(label);
            vm.IsReceiver = wasReceiver.Contains(label);
            MirrorSurfaceOptions.Add(vm);
        }
    }
}

public sealed class MirrorSurfaceViewModel : ViewModelBase
{
    public int    SurfaceId { get; }
    public string Label     { get; }

    public bool IsMirror
    {
        get => _isMirror;
        set => SetProperty(ref _isMirror, value);
    }
    private bool _isMirror;

    public bool IsReceiver
    {
        get => _isReceiver;
        set => SetProperty(ref _isReceiver, value);
    }
    private bool _isReceiver;

    public MirrorSurfaceViewModel(int surfaceId, string label)
    {
        SurfaceId = surfaceId;
        Label     = label;
    }
}
