using System.Collections.ObjectModel;
using Kst.Acoustics.App.Controls;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.App.ViewModels;

public sealed partial class ProjectWindowViewModel
{
    public ObservableCollection<AcousticPointViewModel> Sources { get; } = [];
    public ObservableCollection<AcousticPointViewModel> Receivers { get; } = [];

    public AcousticPointViewModel? SelectedSource
    {
        get => _selectedSource;
        set => SetProperty(ref _selectedSource, value);
    }
    private AcousticPointViewModel? _selectedSource;

    public AcousticPointViewModel? SelectedReceiver
    {
        get => _selectedReceiver;
        set => SetProperty(ref _selectedReceiver, value);
    }
    private AcousticPointViewModel? _selectedReceiver;

    public string NewSourceLabel { get => _newSourceLabel; set => SetProperty(ref _newSourceLabel, value); }
    private string _newSourceLabel = string.Empty;
    public string NewSourceX { get => _newSourceX; set => SetProperty(ref _newSourceX, value); }
    private string _newSourceX = "0";
    public string NewSourceY { get => _newSourceY; set => SetProperty(ref _newSourceY, value); }
    private string _newSourceY = "0";
    public string NewSourceZ { get => _newSourceZ; set => SetProperty(ref _newSourceZ, value); }
    private string _newSourceZ = "0";

    public string NewReceiverLabel { get => _newReceiverLabel; set => SetProperty(ref _newReceiverLabel, value); }
    private string _newReceiverLabel = string.Empty;
    public string NewReceiverX { get => _newReceiverX; set => SetProperty(ref _newReceiverX, value); }
    private string _newReceiverX = "0";
    public string NewReceiverY { get => _newReceiverY; set => SetProperty(ref _newReceiverY, value); }
    private string _newReceiverY = "0";
    public string NewReceiverZ { get => _newReceiverZ; set => SetProperty(ref _newReceiverZ, value); }
    private string _newReceiverZ = "0";

    public PointPlacementMode PlacementMode
    {
        get => _placementMode;
        private set
        {
            if (SetProperty(ref _placementMode, value))
            {
                OnPropertyChanged(nameof(IsPlacementActive));
                OnPropertyChanged(nameof(PlacementStatusText));
            }
        }
    }
    private PointPlacementMode _placementMode = PointPlacementMode.None;

    public bool IsPlacementActive => PlacementMode != PointPlacementMode.None;

    public string PlacementStatusText => PlacementMode switch
    {
        PointPlacementMode.PlacingSource => "Kaynak yerleştirme aktif: Top2D veya Ön 2B görünümde tıklayın.",
        PointPlacementMode.PlacingReceiver => "Alıcı yerleştirme aktif: Top2D veya Ön 2B görünümde tıklayın.",
        _ => "Viewport yerleştirme kapalı."
    };

    internal void LoadPoints(IReadOnlyList<AcousticPoint> sources, IReadOnlyList<AcousticPoint> receivers)
    {
        Sources.Clear();
        foreach (var source in sources)
            Sources.Add(AcousticPointViewModel.FromModel(source));

        Receivers.Clear();
        foreach (var receiver in receivers)
            Receivers.Add(AcousticPointViewModel.FromModel(receiver));

        RefreshPointSnapshots(markDirty: false);
    }

    public void AddSource()
    {
        if (!TryParseCoords(NewSourceX, NewSourceY, NewSourceZ, out var pos))
        {
            StatusMessage = "Kaynak koordinatları geçerli sayı değil.";
            return;
        }

        var id = Sources.Count > 0 ? Sources.Max(s => s.Id) + 1 : 1;
        var label = string.IsNullOrWhiteSpace(NewSourceLabel) ? $"S{id}" : NewSourceLabel.Trim();
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        Sources.Add(new AcousticPointViewModel
        {
            Id = id,
            Label = label,
            Type = AcousticPointType.Source,
            XText = pos.X.ToString("R", culture),
            YText = pos.Y.ToString("R", culture),
            ZText = pos.Z.ToString("R", culture)
        });

        NewSourceLabel = string.Empty;
        RefreshPointSnapshots();
        StatusMessage = $"Kaynak '{label}' eklendi.";
    }

    public void AddReceiver()
    {
        if (!TryParseCoords(NewReceiverX, NewReceiverY, NewReceiverZ, out var pos))
        {
            StatusMessage = "Alıcı koordinatları geçerli sayı değil.";
            return;
        }

        var id = Receivers.Count > 0 ? Receivers.Max(r => r.Id) + 1 : 1;
        var label = string.IsNullOrWhiteSpace(NewReceiverLabel) ? $"R{id}" : NewReceiverLabel.Trim();
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        Receivers.Add(new AcousticPointViewModel
        {
            Id = id,
            Label = label,
            Type = AcousticPointType.Receiver,
            XText = pos.X.ToString("R", culture),
            YText = pos.Y.ToString("R", culture),
            ZText = pos.Z.ToString("R", culture)
        });

        NewReceiverLabel = string.Empty;
        RefreshPointSnapshots();
        StatusMessage = $"Alıcı '{label}' eklendi.";
    }

    public void RemoveSelectedSource()
    {
        if (SelectedSource is null)
        {
            StatusMessage = "Silmek için önce bir kaynak seçin.";
            return;
        }

        var label = SelectedSource.Label;
        Sources.Remove(SelectedSource);
        SelectedSource = null;
        RefreshPointSnapshots();
        StatusMessage = $"Kaynak '{label}' silindi.";
    }

    public void RemoveSelectedReceiver()
    {
        if (SelectedReceiver is null)
        {
            StatusMessage = "Silmek için önce bir alıcı seçin.";
            return;
        }

        var label = SelectedReceiver.Label;
        Receivers.Remove(SelectedReceiver);
        SelectedReceiver = null;
        RefreshPointSnapshots();
        StatusMessage = $"Alıcı '{label}' silindi.";
    }

    public void OnPointEdited()
    {
        RefreshPointSnapshots();
        StatusMessage = "Kaynak/alıcı konumları güncellendi.";
    }

    public void BeginPlaceSource() =>
        PlacementMode = PlacementMode == PointPlacementMode.PlacingSource
            ? PointPlacementMode.None
            : PointPlacementMode.PlacingSource;

    public void BeginPlaceReceiver() =>
        PlacementMode = PlacementMode == PointPlacementMode.PlacingReceiver
            ? PointPlacementMode.None
            : PointPlacementMode.PlacingReceiver;

    public void CancelPlacement() => PlacementMode = PointPlacementMode.None;

    public void OnViewportPlacementPicked(double projX, double projY, GeometryProjectionMode projection)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        string F(double v) => v.ToString("R", culture);

        if (PlacementMode == PointPlacementMode.PlacingSource)
        {
            if (projection == GeometryProjectionMode.Top2D)
            {
                NewSourceX = F(projX);
                NewSourceY = F(projY);
            }
            else
            {
                NewSourceX = F(projX);
                NewSourceZ = F(projY);
            }
            AddSource();
        }
        else if (PlacementMode == PointPlacementMode.PlacingReceiver)
        {
            if (projection == GeometryProjectionMode.Top2D)
            {
                NewReceiverX = F(projX);
                NewReceiverY = F(projY);
            }
            else
            {
                NewReceiverX = F(projX);
                NewReceiverZ = F(projY);
            }
            AddReceiver();
        }
    }

    public void SnapSourceToSelectedSurface()
    {
        if (GetSelectedSurfaceSnapPoint("Kaynak") is not { } p) return;
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        NewSourceX = p.X.ToString("R", culture);
        NewSourceY = p.Y.ToString("R", culture);
        NewSourceZ = p.Z.ToString("R", culture);
        AddSource();
    }

    public void SnapReceiverToSelectedSurface()
    {
        if (GetSelectedSurfaceSnapPoint("Alıcı") is not { } p) return;
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        NewReceiverX = p.X.ToString("R", culture);
        NewReceiverY = p.Y.ToString("R", culture);
        NewReceiverZ = p.Z.ToString("R", culture);
        AddReceiver();
    }

    private Point3D? GetSelectedSurfaceSnapPoint(string role)
    {
        if (HighlightedSurfaceId is not { } id)
        {
            StatusMessage = $"{role} snap: Önce viewport veya geometri ağacından bir yüzey seçin.";
            return null;
        }

        var surface = _workingSurfaces.FirstOrDefault(s => s.Id == id);
        if (surface is null || surface.Vertices.Count < 3)
        {
            StatusMessage = $"{role} snap: Seçili yüzey geçerli değil.";
            return null;
        }

        var centroid = SurfaceTransformService.ComputeCentroid(surface.Vertices);
        var n = ComputeSurfaceNormal(surface.Vertices);
        // Hedef fiziksel offset her zaman ~10cm (metre projesinde 0.1 birim); proje
        // birimine göre çevrilir. Sabit 0.1 "birim" mm projesinde 0,1mm, feet projesinde
        // ~3cm olurdu — fiziksel olarak tutarsız (maincodex M01 incelemesi P2).
        const double OffsetMeters = 0.1;
        var offset = OffsetMeters / ProjectScalingService.GetMetersPerUnit(_project.Unit);
        var positive = new Point3D(
            centroid.X + n.X * offset,
            centroid.Y + n.Y * offset,
            centroid.Z + n.Z * offset);
        var negative = new Point3D(
            centroid.X - n.X * offset,
            centroid.Y - n.Y * offset,
            centroid.Z - n.Z * offset);

        if (RoomPointLocator.IsInside(positive, _workingSurfaces)) return positive;
        if (RoomPointLocator.IsInside(negative, _workingSurfaces)) return negative;

        // Açık kabuklarda içeride testi sonuç vermez. Bu durumda adaylardan oda vertex
        // merkezine yakın olanı seç ve kullanıcının sonucu kontrol etmesini açıkça söyle.
        var finite = _workingSurfaces.SelectMany(s => s.Vertices)
            .Where(v => double.IsFinite(v.X) && double.IsFinite(v.Y) && double.IsFinite(v.Z))
            .ToArray();
        if (finite.Length > 0)
        {
            var roomCenter = new Point3D(
                finite.Average(v => v.X), finite.Average(v => v.Y), finite.Average(v => v.Z));
            static double D2(Point3D a, Point3D b) =>
                (a.X - b.X) * (a.X - b.X) +
                (a.Y - b.Y) * (a.Y - b.Y) +
                (a.Z - b.Z) * (a.Z - b.Z);
            StatusMessage = $"{role} yakalama: Kabuk kapalı olmadığı için oda merkezine bakan taraf seçildi; konumu doğrulayın.";
            return D2(positive, roomCenter) <= D2(negative, roomCenter) ? positive : negative;
        }

        return positive;
    }

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

    private void RefreshPointSnapshots(bool markDirty = true)
    {
        var sourcePoints = Sources.Select(s => s.TryBuildPoint()).ToArray();
        var receiverPoints = Receivers.Select(r => r.TryBuildPoint()).ToArray();
        var invalidSourceCount = sourcePoints.Count(p => p is null);
        var invalidReceiverCount = receiverPoints.Count(p => p is null);

        HasInvalidPoints = invalidSourceCount + invalidReceiverCount > 0;
        PointValidationMessage = HasInvalidPoints
            ? $"{invalidSourceCount} kaynak ve {invalidReceiverCount} alıcı satırında geçersiz/sonlu olmayan koordinat var. Düzeltmeden kaydetme ve simülasyon yapılamaz."
            : string.Empty;

        // Geçersiz bir satır varken önceki geçerli snapshot'ı koru. Böylece hatalı
        // düzenleme bir noktayı sessizce projeden veya simülasyon girdisinden düşürmez.
        if (!HasInvalidPoints)
        {
            SourcePoints = sourcePoints.Select(p => p!).ToArray();
            ReceiverPoints = receiverPoints.Select(p => p!).ToArray();
        }

        if (SelectedLightSource is not null && SourcePoints.All(s => s.Id != SelectedLightSource.Id))
            SelectedLightSource = SourcePoints.FirstOrDefault();

        OnPropertyChanged(nameof(CanRunSimulation));
        OnPropertyChanged(nameof(CanRunLight));

        if (markDirty)
        {
            MarkDirty();
            InvalidateSimulationResult("Kaynak veya alıcı noktaları değiştirildi.");
        }
    }

    private static Point3D ComputeSurfaceNormal(IReadOnlyList<Point3D> v)
    {
        var ax = v[1].X - v[0].X; var ay = v[1].Y - v[0].Y; var az = v[1].Z - v[0].Z;
        var bx = v[2].X - v[0].X; var by = v[2].Y - v[0].Y; var bz = v[2].Z - v[0].Z;
        var nx = ay * bz - az * by;
        var ny = az * bx - ax * bz;
        var nz = ax * by - ay * bx;
        var len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
        return len < 1e-10 ? new Point3D(0, 0, 1) : new Point3D(nx / len, ny / len, nz / len);
    }

    private static bool TryParseCoords(string xText, string yText, string zText, out Point3D result)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        const System.Globalization.NumberStyles style = System.Globalization.NumberStyles.Float;
        if (double.TryParse(xText, style, culture, out var x) &&
            double.TryParse(yText, style, culture, out var y) &&
            double.TryParse(zText, style, culture, out var z) &&
            double.IsFinite(x) && double.IsFinite(y) && double.IsFinite(z))
        {
            result = new Point3D(x, y, z);
            return true;
        }

        result = default;
        return false;
    }
}
