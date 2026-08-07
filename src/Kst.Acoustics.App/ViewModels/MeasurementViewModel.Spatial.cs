using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Measurement.Data;
using Kst.Acoustics.Core.Measurement.Spatial;

namespace Kst.Acoustics.App.ViewModels;

public partial class MeasurementViewModel
{
    [ObservableProperty] private double _roomLength = 6.0;
    [ObservableProperty] private double _roomWidth  = 4.5;
    [ObservableProperty] private double _roomHeight = 3.0;

    [ObservableProperty] private string? _dxfRoomPath;
    [ObservableProperty] private RoomGeometry? _roomGeometry;

    [ObservableProperty] private ObservableCollection<SetupPoint> _setupPoints = [];
    [ObservableProperty] private SetupPoint? _selectedSetupPoint;
    [ObservableProperty] private string? _selectedPointLabel;

    [ObservableProperty] private string _newPointLabel = "Mik 1";
    [ObservableProperty] private PointType _newPointType = PointType.Microphone;
    [ObservableProperty] private int _newPointTypeIndex; // 0=Microphone, 1=Loudspeaker

    partial void OnNewPointTypeIndexChanged(int value)
    {
        NewPointType = value == 1 ? PointType.Loudspeaker : PointType.Microphone;
        if (string.IsNullOrWhiteSpace(NewPointLabel) || NewPointLabel.StartsWith("Mik") || NewPointLabel.StartsWith("Hoparlör"))
            NewPointLabel = AutoLabel(NewPointType);
    }

    partial void OnNewPointTypeChanged(PointType value)
    {
        var idx = value == PointType.Loudspeaker ? 1 : 0;
        if (NewPointTypeIndex != idx) NewPointTypeIndex = idx;
    }
    [ObservableProperty] private double _newPointX;
    [ObservableProperty] private double _newPointY;
    [ObservableProperty] private double _newPointZ = 1.2;
    [ObservableProperty] private bool _placementActive;

    [ObservableProperty] private double _sourceMicDistanceMeters;

    partial void OnSelectedSetupPointChanged(SetupPoint? value)
    {
        SelectedPointLabel = value?.Label;
        if (value != null)
        {
            NewPointLabel = value.Label;
            NewPointType = value.Type;
            NewPointX = value.X;
            NewPointY = value.Y;
            NewPointZ = value.Z;
        }
    }

    public void SetupPointPicked(Point3D world)
    {
        NewPointX = Math.Round(world.X, 3);
        NewPointY = Math.Round(world.Y, 3);
        NewPointZ = Math.Round(world.Z, 3);
        AddSetupPoint();
    }

    public void SetupPointSelectedByLabel(string label)
    {
        var match = SetupPoints.FirstOrDefault(p => p.Label == label);
        if (match != null) SelectedSetupPoint = match;
    }

    [RelayCommand]
    private void BuildBoxRoom()
    {
        try
        {
            RoomGeometry = RoomGeometry.FromBox(RoomLength, RoomWidth, RoomHeight);
            DxfRoomPath = null;
            IsDirty = true;
            StatusMessage = $"Kutu oda: {RoomLength:F2}×{RoomWidth:F2}×{RoomHeight:F2} m";
            StatusColor = "#22C55E";
            UpdateDistance();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Oda oluşturulamadı: {ex.Message}";
            StatusColor = "#EF4444";
        }
    }

    [RelayCommand]
    private async Task LoadDxfRoom()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.StorageProvider == null) return;

        var files = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Oda DXF Dosyası Seç",
            FileTypeFilter = [new FilePickerFileType("DXF") { Patterns = ["*.dxf"] }]
        });
        if (files.Count == 0) return;

        var path = files[0].Path.LocalPath;
        try
        {
            StatusMessage = "DXF yükleniyor...";
            StatusColor = "#EAB308";
            var geom = await DxfRoomLoader.LoadAsync(path);
            RoomGeometry = geom;
            DxfRoomPath = path;
            RoomLength = Math.Round(geom.Length, 2);
            RoomWidth = Math.Round(geom.Width, 2);
            RoomHeight = Math.Round(geom.Height, 2);
            IsDirty = true;
            StatusMessage = $"DXF yüklendi: {geom.Source} ({geom.Triangles.Count} üçgen)";
            StatusColor = "#22C55E";
            UpdateDistance();
        }
        catch (Exception ex)
        {
            StatusMessage = $"DXF yüklenemedi: {ex.Message}";
            StatusColor = "#EF4444";
        }
    }

    [RelayCommand]
    private void AddSetupPoint()
    {
        var label = string.IsNullOrWhiteSpace(NewPointLabel) ? AutoLabel(NewPointType) : NewPointLabel.Trim();
        var existing = SetupPoints.FirstOrDefault(p => p.Label == label);
        var newPoint = new SetupPoint(label, NewPointType, NewPointX, NewPointY, NewPointZ);

        if (existing != null)
        {
            var index = SetupPoints.IndexOf(existing);
            SetupPoints[index] = newPoint;
        }
        else
        {
            SetupPoints.Add(newPoint);
        }
        SelectedSetupPoint = newPoint;
        NewPointLabel = AutoLabel(NewPointType);
        StatusMessage = $"Nokta {(existing != null ? "güncellendi" : "eklendi")}: {label}";
        StatusColor = "#22C55E";
        PlacementActive = false;
        UpdateDistance();
    }

    [RelayCommand]
    private void RemoveSelectedPoint()
    {
        var sel = SelectedSetupPoint;
        if (sel == null) return;
        SetupPoints.Remove(sel);
        SelectedSetupPoint = null;
        UpdateDistance();
    }

    [RelayCommand]
    private void TogglePlacement() => PlacementActive = !PlacementActive;

    [RelayCommand]
    private void ResetRoomView()
    {
        // Viewport ResetView() doğrudan UI tarafında çağrılır; bu metod placeholder.
        // View, RequestResetRoomView ile bilgilendirilir.
        RequestResetRoomView?.Invoke();
    }

    public Action? RequestResetRoomView { get; set; }

    private string AutoLabel(PointType type)
    {
        var prefix = type == PointType.Loudspeaker ? "Hoparlör" : "Mik";
        for (int i = 1; i < 1000; i++)
        {
            var candidate = $"{prefix} {i}";
            if (SetupPoints.All(p => p.Label != candidate)) return candidate;
        }
        return $"{prefix} {Guid.NewGuid().ToString("N")[..4]}";
    }

    private void UpdateDistance()
    {
        var src = SetupPoints.FirstOrDefault(p => p.Type == PointType.Loudspeaker);
        var mic = SetupPoints.FirstOrDefault(p => p.Type == PointType.Microphone);
        if (src == null || mic == null)
        {
            SourceMicDistanceMeters = 0;
            return;
        }
        var dx = src.X - mic.X;
        var dy = src.Y - mic.Y;
        var dz = src.Z - mic.Z;
        SourceMicDistanceMeters = Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    partial void OnSetupPointsChanged(ObservableCollection<SetupPoint> value)
    {
        // Property yeniden atandığında (ör. oturum yükleme) yeni koleksiyon örneği de izlenmeli;
        // eski koleksiyona bağlı dinleyici artık yeni Add/Remove'ları duymaz.
        value.CollectionChanged += (_, _) => IsDirty = true;
        UpdateDistance();
    }
}
