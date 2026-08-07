using System.Collections.ObjectModel;
using Kst.Acoustics.App.Controls;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.App.ViewModels;

public partial class MainWindowViewModel
{
    public IReadOnlyList<DirectivityPattern> SourceDirectivityOptions { get; } =
        Enum.GetValues<DirectivityPattern>();

    public ObservableCollection<LoudspeakerOptionViewModel> LoudspeakerOptions { get; } = [];

    public LoudspeakerOptionViewModel? NewSourceLoudspeaker
    {
        get => _newSourceLoudspeaker;
        set => SetProperty(ref _newSourceLoudspeaker, value);
    }
    private LoudspeakerOptionViewModel? _newSourceLoudspeaker;

    // --- Koleksiyonlar ---
    public ObservableCollection<AcousticPointViewModel> Sources { get; } = [];
    public ObservableCollection<AcousticPointViewModel> Receivers { get; } = [];

    // --- Seçili ---
    public AcousticPointViewModel? SelectedSource
    {
        get => _selectedSource;
        set
        {
            if (SetProperty(ref _selectedSource, value))
            {
                _selectedSourceLoudspeaker = FindLoudspeakerOption(value?.LoudspeakerId);
                OnPropertyChanged(nameof(SelectedSourceLoudspeaker));
            }
        }
    }
    private AcousticPointViewModel? _selectedSource;

    public LoudspeakerOptionViewModel? SelectedSourceLoudspeaker
    {
        get => _selectedSourceLoudspeaker;
        set
        {
            if (SetProperty(ref _selectedSourceLoudspeaker, value) && SelectedSource is not null)
            {
                SelectedSource.LoudspeakerId = value?.Id;
            }
        }
    }
    private LoudspeakerOptionViewModel? _selectedSourceLoudspeaker;

    public AcousticPointViewModel? SelectedReceiver
    {
        get => _selectedReceiver;
        set => SetProperty(ref _selectedReceiver, value);
    }
    private AcousticPointViewModel? _selectedReceiver;

    // --- Yeni nokta formu (Source) ---
    public string NewSourceLabel { get => _newSourceLabel; set => SetProperty(ref _newSourceLabel, value); }
    private string _newSourceLabel = string.Empty;
    public string NewSourceX { get => _newSourceX; set => SetProperty(ref _newSourceX, value); }
    private string _newSourceX = "0";
    public string NewSourceY { get => _newSourceY; set => SetProperty(ref _newSourceY, value); }
    private string _newSourceY = "0";
    public string NewSourceZ { get => _newSourceZ; set => SetProperty(ref _newSourceZ, value); }
    private string _newSourceZ = "0";
    public DirectivityPattern NewSourceDirectivity { get => _newSourceDirectivity; set => SetProperty(ref _newSourceDirectivity, value); }
    private DirectivityPattern _newSourceDirectivity = DirectivityPattern.Omnidirectional;
    public string NewSourceAzimuth { get => _newSourceAzimuth; set => SetProperty(ref _newSourceAzimuth, value); }
    private string _newSourceAzimuth = "0";
    public string NewSourceElevation { get => _newSourceElevation; set => SetProperty(ref _newSourceElevation, value); }
    private string _newSourceElevation = "0";
    public string NewSourceRoll { get => _newSourceRoll; set => SetProperty(ref _newSourceRoll, value); }
    private string _newSourceRoll = "0";

    // --- Yeni nokta formu (Receiver) ---
    public string NewReceiverLabel { get => _newReceiverLabel; set => SetProperty(ref _newReceiverLabel, value); }
    private string _newReceiverLabel = string.Empty;
    public string NewReceiverX { get => _newReceiverX; set => SetProperty(ref _newReceiverX, value); }
    private string _newReceiverX = "0";
    public string NewReceiverY { get => _newReceiverY; set => SetProperty(ref _newReceiverY, value); }
    private string _newReceiverY = "0";
    public string NewReceiverZ { get => _newReceiverZ; set => SetProperty(ref _newReceiverZ, value); }
    private string _newReceiverZ = "0";

    // --- Viewport snapshot (render için) ---
    public IReadOnlyList<AcousticPoint> SourcePoints
    {
        get => _sourcePoints;
        private set => SetProperty(ref _sourcePoints, value);
    }
    private IReadOnlyList<AcousticPoint> _sourcePoints = [];

    public IReadOnlyList<AcousticPoint> ReceiverPoints
    {
        get => _receiverPoints;
        private set => SetProperty(ref _receiverPoints, value);
    }
    private IReadOnlyList<AcousticPoint> _receiverPoints = [];

    // --- Model Z aralığı ipucu ---
    public string ModelZRangeHint
    {
        get => _modelZRangeHint;
        private set => SetProperty(ref _modelZRangeHint, value);
    }
    private string _modelZRangeHint = "-";

    // --- Placement modu ---
    public PointPlacementMode PlacementMode
    {
        get => _placementMode;
        private set
        {
            if (SetProperty(ref _placementMode, value))
            {
                OnPropertyChanged(nameof(IsPlacementActive));
                OnPropertyChanged(nameof(PlacementLabel));
            }
        }
    }
    private PointPlacementMode _placementMode = PointPlacementMode.None;

    public bool IsPlacementActive => _placementMode != PointPlacementMode.None;

    public string PlacementLabel => _placementMode switch
    {
        PointPlacementMode.PlacingSource   => "Kaynak yerleştir — görünümde tıklayın  |  ESC: iptal",
        PointPlacementMode.PlacingReceiver => "Alıcı yerleştir — görünümde tıklayın  |  ESC: iptal",
        _ => string.Empty
    };

    // --- Placement komutları ---
    public void BeginPlaceSource()
    {
        PlacementMode = PlacementMode == PointPlacementMode.PlacingSource
            ? PointPlacementMode.None
            : PointPlacementMode.PlacingSource;
    }

    public void BeginPlaceReceiver()
    {
        PlacementMode = PlacementMode == PointPlacementMode.PlacingReceiver
            ? PointPlacementMode.None
            : PointPlacementMode.PlacingReceiver;
    }

    public void CancelPlacement() => PlacementMode = PointPlacementMode.None;

    /// <summary>
    /// Viewport tıklamasından çağrılır. projX/projY, projeksiyona göre:
    ///   Top2D  → (model.X, model.Y)
    ///   Front2D → (model.X, model.Z)
    /// </summary>
    public void OnViewportPlacementPicked(double projX, double projY, GeometryProjectionMode projection)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        string Fmt(double v) => v.ToString("R", culture);

        if (PlacementMode == PointPlacementMode.PlacingSource)
        {
            if (projection == GeometryProjectionMode.Top2D)
            {
                NewSourceX = Fmt(projX);
                NewSourceY = Fmt(projY);
                // Z mevcut değerde kalır
            }
            else // Front2D
            {
                NewSourceX = Fmt(projX);
                NewSourceZ = Fmt(projY);
                // Y mevcut değerde kalır
            }
            AddSource();
        }
        else if (PlacementMode == PointPlacementMode.PlacingReceiver)
        {
            if (projection == GeometryProjectionMode.Top2D)
            {
                NewReceiverX = Fmt(projX);
                NewReceiverY = Fmt(projY);
            }
            else // Front2D
            {
                NewReceiverX = Fmt(projX);
                NewReceiverZ = Fmt(projY);
            }
            AddReceiver();
        }
    }

    // --- Yüzey snap ---
    public void SnapSourceToSurface()
    {
        var pos = GetSnapPosition("Kaynak");
        if (pos is null) return;
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        string Fmt(double v) => v.ToString("R", culture);
        NewSourceX = Fmt(pos.Value.X);
        NewSourceY = Fmt(pos.Value.Y);
        NewSourceZ = Fmt(pos.Value.Z);
        AddSource();
    }

    public void SnapReceiverToSurface()
    {
        var pos = GetSnapPosition("Alıcı");
        if (pos is null) return;
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        string Fmt(double v) => v.ToString("R", culture);
        NewReceiverX = Fmt(pos.Value.X);
        NewReceiverY = Fmt(pos.Value.Y);
        NewReceiverZ = Fmt(pos.Value.Z);
        AddReceiver();
    }

    private Point3D? GetSnapPosition(string role)
    {
        if (SelectedSurface is null)
        {
            StatusMessage = $"{role} snap: Önce Yüzeyler sekmesinden bir yüzey seçin.";
            return null;
        }

        var surface = ProjectSurfaces.FirstOrDefault(s => s.Id == SelectedSurface.Id);
        if (surface is null || surface.Vertices.Count < 3)
        {
            StatusMessage = $"{role} snap: Seçili yüzey geçerli değil.";
            return null;
        }

        var finiteVertices = surface.Vertices.Where(IsFinite).ToArray();
        if (finiteVertices.Length < 3)
        {
            StatusMessage = $"{role} snap: Seçili yüzey geçerli sonlu köşeler içermiyor.";
            return null;
        }

        var centroid = ComputeCentroid(finiteVertices);
        var normal   = ComputeSurfaceNormal(finiteVertices);
        var unit = _currentProject?.Unit ?? SurfaceImportUnit.Meters;
        var offset = 0.1 / ProjectScalingService.GetMetersPerUnit(unit);

        var plus = new Point3D(
            centroid.X + normal.X * offset,
            centroid.Y + normal.Y * offset,
            centroid.Z + normal.Z * offset);
        var minus = new Point3D(
            centroid.X - normal.X * offset,
            centroid.Y - normal.Y * offset,
            centroid.Z - normal.Z * offset);

        // Yüzey winding'i DXF'te güvenilir olmayabilir. İki normal tarafını da kapalı
        // kabuğa karşı sınayıp içeride kalan adayı seç.
        var plusInside = RoomPointLocator.IsInside(plus, ProjectSurfaces);
        var minusInside = RoomPointLocator.IsInside(minus, ProjectSurfaces);
        if (plusInside != minusInside) return plusInside ? plus : minus;

        // Açık kabukta kesin içeride testi mümkün değildir. Güvenli tahmin olarak oda
        // köşe merkezine yakın tarafı seç; en azından normalin rastgele yönüne bağlı kalma.
        var roomVertices = ProjectSurfaces.SelectMany(s => s.Vertices).Where(IsFinite).ToArray();
        if (roomVertices.Length == 0) return plus;
        var roomCenter = ComputeCentroid(roomVertices);
        return DistanceSquared(plus, roomCenter) <= DistanceSquared(minus, roomCenter)
            ? plus
            : minus;
    }

    private static Point3D ComputeCentroid(IReadOnlyList<Point3D> v) =>
        new(v.Average(p => p.X), v.Average(p => p.Y), v.Average(p => p.Z));

    private static Point3D ComputeSurfaceNormal(IReadOnlyList<Point3D> v)
    {
        double nx = 0, ny = 0, nz = 0;
        for (var i = 0; i < v.Count; i++)
        {
            var current = v[i];
            var next = v[(i + 1) % v.Count];
            nx += (current.Y - next.Y) * (current.Z + next.Z);
            ny += (current.Z - next.Z) * (current.X + next.X);
            nz += (current.X - next.X) * (current.Y + next.Y);
        }

        var len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
        return len < 1e-10 ? new Point3D(0, 0, 1) : new Point3D(nx / len, ny / len, nz / len);
    }

    private static double DistanceSquared(Point3D a, Point3D b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        var dz = a.Z - b.Z;
        return dx * dx + dy * dy + dz * dz;
    }

    private static bool IsFinite(Point3D point) =>
        double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z);

    // --- CRUD ---
    public void AddSource()
    {
        if (!TryParseCoords(NewSourceX, NewSourceY, NewSourceZ, out var pos))
        {
            StatusMessage = "Kaynak koordinatları geçerli sayı değil.";
            return;
        }
        if (!TryParseAngles(NewSourceAzimuth, NewSourceElevation, NewSourceRoll, out var azimuth, out var elevation, out var roll))
        {
            StatusMessage = "Kaynak yön açıları geçerli sayı değil.";
            return;
        }
        var id    = Sources.Count > 0 ? Sources.Max(x => x.Id) + 1 : 1;
        var label = string.IsNullOrWhiteSpace(NewSourceLabel) ? $"S{id}" : NewSourceLabel.Trim();
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        Sources.Add(new AcousticPointViewModel
        {
            Id = id, Label = label, Type = AcousticPointType.Source,
            XText = pos.X.ToString("R", culture),
            YText = pos.Y.ToString("R", culture),
            ZText = pos.Z.ToString("R", culture),
            Directivity = NewSourceDirectivity,
            AzimuthText = azimuth.ToString("R", culture),
            ElevationText = elevation.ToString("R", culture),
            RollText = roll.ToString("R", culture),
            LoudspeakerId = NewSourceLoudspeaker?.Id
        });
        NewSourceLabel = string.Empty;
        RefreshSourcePoints();
        MarkDirty();
        InvalidateStoredSimulationResult("Kaynak noktaları değiştirildi.");
        StatusMessage = $"Kaynak '{label}' eklendi ({pos.X:0.###}, {pos.Y:0.###}, {pos.Z:0.###}).";
    }

    public void RemoveSelectedSource()
    {
        if (SelectedSource is null) { StatusMessage = "Silmek için önce bir kaynak seçin."; return; }
        var label = SelectedSource.Label;
        Sources.Remove(SelectedSource);
        SelectedSource = null;
        RefreshSourcePoints();
        MarkDirty();
        InvalidateStoredSimulationResult("Kaynak noktaları değiştirildi.");
        StatusMessage = $"Kaynak '{label}' silindi.";
    }

    public void ApplySelectedSourceSettings()
    {
        if (SelectedSource is null)
        {
            StatusMessage = "Yön ayarı için önce bir kaynak seçin.";
            return;
        }

        if (SelectedSource.TryBuildPoint() is null)
        {
            StatusMessage = "Seçili kaynağın konum veya yön değerleri geçerli değil.";
            return;
        }

        RefreshSourcePoints();
        MarkDirty();
        InvalidateStoredSimulationResult("Kaynak yönlülüğü değiştirildi.");
        StatusMessage = $"Kaynak '{SelectedSource.Label}' yön ayarları güncellendi.";
    }

    public void ReloadLoudspeakerOptions()
    {
        var previousNewId = NewSourceLoudspeaker?.Id;
        var previousSelectedId = SelectedSource?.LoudspeakerId;

        LoudspeakerOptions.Clear();
        LoudspeakerOptions.Add(new LoudspeakerOptionViewModel(null, "Parametrik kaynak"));

        foreach (var speaker in LoudspeakerDatabase.Instance.All
                     .OrderBy(s => s.Manufacturer, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(s => s.ModelName, StringComparer.OrdinalIgnoreCase))
        {
            LoudspeakerOptions.Add(new LoudspeakerOptionViewModel(speaker.Id, speaker.DisplayName));
        }

        NewSourceLoudspeaker = FindLoudspeakerOption(previousNewId) ?? LoudspeakerOptions.FirstOrDefault();
        _selectedSourceLoudspeaker = FindLoudspeakerOption(previousSelectedId);
        OnPropertyChanged(nameof(SelectedSourceLoudspeaker));
    }

    public void AddReceiver()
    {
        if (!TryParseCoords(NewReceiverX, NewReceiverY, NewReceiverZ, out var pos))
        {
            StatusMessage = "Alıcı koordinatları geçerli sayı değil.";
            return;
        }
        var id    = Receivers.Count > 0 ? Receivers.Max(x => x.Id) + 1 : 1;
        var label = string.IsNullOrWhiteSpace(NewReceiverLabel) ? $"R{id}" : NewReceiverLabel.Trim();
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        Receivers.Add(new AcousticPointViewModel
        {
            Id = id, Label = label, Type = AcousticPointType.Receiver,
            XText = pos.X.ToString("R", culture),
            YText = pos.Y.ToString("R", culture),
            ZText = pos.Z.ToString("R", culture)
        });
        NewReceiverLabel = string.Empty;
        RefreshReceiverPoints();
        MarkDirty();
        InvalidateStoredSimulationResult("Alıcı noktaları değiştirildi.");
        StatusMessage = $"Alıcı '{label}' eklendi ({pos.X:0.###}, {pos.Y:0.###}, {pos.Z:0.###}).";
    }

    public void RemoveSelectedReceiver()
    {
        if (SelectedReceiver is null) { StatusMessage = "Silmek için önce bir alıcı seçin."; return; }
        var label = SelectedReceiver.Label;
        Receivers.Remove(SelectedReceiver);
        SelectedReceiver = null;
        RefreshReceiverPoints();
        MarkDirty();
        InvalidateStoredSimulationResult("Alıcı noktaları değiştirildi.");
        StatusMessage = $"Alıcı '{label}' silindi.";
    }

    // --- İç yardımcılar ---
    public bool HasInvalidPoints
    {
        get => _hasInvalidPoints;
        private set => SetProperty(ref _hasInvalidPoints, value);
    }
    private bool _hasInvalidPoints;

    public string PointValidationMessage
    {
        get => _pointValidationMessage;
        private set => SetProperty(ref _pointValidationMessage, value);
    }
    private string _pointValidationMessage = string.Empty;

    private void RefreshSourcePoints()
    {
        var built = Sources.Select(vm => vm.TryBuildPoint()).ToArray();
        if (built.All(p => p is not null))
            SourcePoints = built.Select(p => p!).ToArray();
        RefreshPointValidationState();
    }

    private void RefreshReceiverPoints()
    {
        var built = Receivers.Select(vm => vm.TryBuildPoint()).ToArray();
        if (built.All(p => p is not null))
            ReceiverPoints = built.Select(p => p!).ToArray();
        RefreshPointValidationState();
    }

    private void RefreshPointValidationState()
    {
        var invalidSources = Sources.Count(vm => vm.TryBuildPoint() is null);
        var invalidReceivers = Receivers.Count(vm => vm.TryBuildPoint() is null);
        HasInvalidPoints = invalidSources + invalidReceivers > 0;
        PointValidationMessage = HasInvalidPoints
            ? $"{invalidSources} kaynak ve {invalidReceivers} alıcı satırı geçersiz. Düzeltmeden proje kaydedilemez."
            : string.Empty;
    }

    internal void LoadPoints(IReadOnlyList<AcousticPoint> sources, IReadOnlyList<AcousticPoint> receivers)
    {
        Sources.Clear();
        foreach (var p in sources) Sources.Add(AcousticPointViewModel.FromModel(p));
        Receivers.Clear();
        foreach (var p in receivers) Receivers.Add(AcousticPointViewModel.FromModel(p));
        RefreshSourcePoints();
        RefreshReceiverPoints();
    }

    internal void ClearPoints()
    {
        Sources.Clear(); Receivers.Clear();
        SourcePoints = []; ReceiverPoints = [];
        SelectedSource = null; SelectedReceiver = null;
        NewSourceLabel = string.Empty; NewSourceX = "0"; NewSourceY = "0"; NewSourceZ = "0";
        NewSourceDirectivity = DirectivityPattern.Omnidirectional;
        NewSourceAzimuth = "0"; NewSourceElevation = "0"; NewSourceRoll = "0";
        NewSourceLoudspeaker = LoudspeakerOptions.FirstOrDefault();
        SelectedSourceLoudspeaker = null;
        NewReceiverLabel = string.Empty; NewReceiverX = "0"; NewReceiverY = "0"; NewReceiverZ = "0";
        ModelZRangeHint = "-";
        PlacementMode = PointPlacementMode.None;
    }

    internal void UpdateModelZRangeHint(IReadOnlyList<ProjectSurface> surfaces, SurfaceImportUnit unit)
    {
        if (surfaces.Count == 0) { ModelZRangeHint = "-"; return; }
        var points = surfaces.SelectMany(s => s.Vertices).ToArray();
        if (points.Length == 0) { ModelZRangeHint = "-"; return; }
        var minZ = points.Min(p => p.Z);
        var maxZ = points.Max(p => p.Z);
        var u = FormatUnitShortName(unit);
        ModelZRangeHint = $"Z aralığı: {FormatDimension(minZ)} — {FormatDimension(maxZ)} {u}";
    }

    /// <summary>Kaynak satırı düzenlendikten sonra çağrılır (CellEditEnded).</summary>
    public void OnSourceEdited()
    {
        RefreshSourcePoints();
        MarkDirty();
        InvalidateStoredSimulationResult("Kaynak noktaları değiştirildi.");
    }

    /// <summary>Alıcı satırı düzenlendikten sonra çağrılır (CellEditEnded).</summary>
    public void OnReceiverEdited()
    {
        RefreshReceiverPoints();
        MarkDirty();
        InvalidateStoredSimulationResult("Alıcı noktaları değiştirildi.");
    }

    private IReadOnlyList<AcousticPoint> BuildSourceSnapshot() => BuildPointSnapshot(Sources, "kaynak");

    private IReadOnlyList<AcousticPoint> BuildReceiverSnapshot() => BuildPointSnapshot(Receivers, "alıcı");

    private static IReadOnlyList<AcousticPoint> BuildPointSnapshot(
        IEnumerable<AcousticPointViewModel> rows,
        string role)
    {
        var result = new List<AcousticPoint>();
        var invalid = new List<string>();
        foreach (var row in rows)
        {
            if (row.TryBuildPoint() is { } point)
                result.Add(point);
            else
                invalid.Add(string.IsNullOrWhiteSpace(row.Label) ? $"ID {row.Id}" : row.Label);
        }

        if (invalid.Count > 0)
            throw new InvalidDataException(
                $"Geçersiz {role} satırları var: {string.Join(", ", invalid)}. Koordinat ve yön alanlarını düzeltin.");

        return result;
    }

    private static bool TryParseCoords(string xText, string yText, string zText, out Point3D result)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        const System.Globalization.NumberStyles Style = System.Globalization.NumberStyles.Float;
        if (double.TryParse(xText, Style, culture, out var x) &&
            double.TryParse(yText, Style, culture, out var y) &&
            double.TryParse(zText, Style, culture, out var z) &&
            double.IsFinite(x) && double.IsFinite(y) && double.IsFinite(z))
        {
            result = new Point3D(x, y, z);
            return true;
        }
        result = default;
        return false;
    }

    private LoudspeakerOptionViewModel? FindLoudspeakerOption(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return LoudspeakerOptions.FirstOrDefault(o => o.Id is null);
        }

        return LoudspeakerOptions.FirstOrDefault(o =>
            string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryParseAngles(
        string azimuthText,
        string elevationText,
        string rollText,
        out double azimuth,
        out double elevation,
        out double roll)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        const System.Globalization.NumberStyles Style = System.Globalization.NumberStyles.Float;
        var azOk = double.TryParse(azimuthText, Style, culture, out azimuth);
        var elOk = double.TryParse(elevationText, Style, culture, out elevation);
        var rollOk = double.TryParse(rollText, Style, culture, out roll);
        return azOk && elOk && rollOk &&
               double.IsFinite(azimuth) &&
               double.IsFinite(elevation) &&
               double.IsFinite(roll);
    }
}
