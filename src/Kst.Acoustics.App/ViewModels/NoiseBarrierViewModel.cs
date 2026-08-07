using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kst.Acoustics.Core.NoiseBarrier;

namespace Kst.Acoustics.App.ViewModels;

public partial class NoiseBarrierViewModel : ViewModelBase
{
    [ObservableProperty]
    private NoiseBarrierProject _project = new();

    [ObservableProperty]
    private NoiseBarrierMaterial? _selectedMaterial;

    public ObservableCollection<NoiseBarrierMaterial> Materials { get; } = new();
    public ObservableCollection<string> ValidationWarnings { get; } = new();
    public SourceType[] SourceTypes { get; } = Enum.GetValues<SourceType>();
    public TrafficEmissionModel[] TrafficModels { get; } = Enum.GetValues<TrafficEmissionModel>();
    public DiffractionMethod[] DiffractionMethods { get; } = Enum.GetValues<DiffractionMethod>();

    [ObservableProperty]
    private string _averageNoBarrierText = "-";

    [ObservableProperty]
    private string _averageWithBarrierText = "-";

    [ObservableProperty]
    private string _averageInsertionLossText = "-";

    [ObservableProperty]
    private string _minimumInsertionLossText = "-";

    [ObservableProperty]
    private string _maximumInsertionLossText = "-";

    [ObservableProperty]
    private string _criticalReceiverText = "-";

    [ObservableProperty]
    private ReceiverPoint? _selectedReceiver;

    [ObservableProperty]
    private int _simulationRevision;

    public ObservableCollection<ReceiverBandRow> SelectedReceiverBands { get; } = new();

    [ObservableProperty] private string _newMaterialName = "Özel Bariyer Paneli";
    [ObservableProperty] private double _newMaterialRw = 30;
    [ObservableProperty] private double _newMaterialNrc = 0.05;
    
    // TL Bands for manual entry
    [ObservableProperty] private double _newMaterialTl63 = 30;
    [ObservableProperty] private double _newMaterialTl125 = 30;
    [ObservableProperty] private double _newMaterialTl250 = 30;
    [ObservableProperty] private double _newMaterialTl500 = 30;
    [ObservableProperty] private double _newMaterialTl1k = 30;
    [ObservableProperty] private double _newMaterialTl2k = 30;
    [ObservableProperty] private double _newMaterialTl4k = 30;
    [ObservableProperty] private double _newMaterialTl8k = 30;

    // Alpha Bands for manual entry
    [ObservableProperty] private double _newMaterialAlpha63 = 0.05;
    [ObservableProperty] private double _newMaterialAlpha125 = 0.05;
    [ObservableProperty] private double _newMaterialAlpha250 = 0.05;
    [ObservableProperty] private double _newMaterialAlpha500 = 0.05;
    [ObservableProperty] private double _newMaterialAlpha1k = 0.05;
    [ObservableProperty] private double _newMaterialAlpha2k = 0.05;
    [ObservableProperty] private double _newMaterialAlpha4k = 0.05;
    [ObservableProperty] private double _newMaterialAlpha8k = 0.05;

    [ObservableProperty]
    private string _manualMaterialCategory = "Özel";

    [ObservableProperty]
    private double _manualMaterialThicknessMm = 100.0;

    [ObservableProperty]
    private double _manualMaterialSurfaceMass = 35.0;

    [ObservableProperty]
    private string _materialStatusText = "Kütüphane hazır.";

    public NoiseBarrierViewModel()
    {
        LoadMaterials();
        InitializeProject();
    }

    private void LoadMaterials()
    {
        var materials = NoiseBarrierLibraryService.LoadLibrary();
        Materials.Clear();
        foreach (var m in materials) Materials.Add(m);
        SelectedMaterial = Materials.FirstOrDefault();
    }

    private void InitializeProject()
    {
        Project = new NoiseBarrierProject();
        // Add a default source
        Project.Sources.Add(new NoiseSource {
            Name = "Yol Gürültüsü (Noktasal)",
            X = 0, Y = -20, Z = 0.5,
            OverallLw = 105
        });

        if (SelectedMaterial != null)
        {
            foreach (var b in Project.Barriers)
                b.MaterialId = SelectedMaterial.Id;
        }

        UpdateSimulation();
    }

    [RelayCommand]
    public void AddBarrier()
    {
        var nextIndex = Project.Barriers.Count + 1;
        Project.Barriers.Add(new BarrierModel
        {
            Name = $"Bariyer {nextIndex}",
            DistanceFromBuilding = 5.0 + (nextIndex - 1) * 8.0,
            MaterialId = SelectedMaterial?.Id ?? string.Empty
        });
        UpdateSimulation();
    }

    [RelayCommand]
    public void RemoveBarrier(BarrierModel barrier)
    {
        if (Project.Barriers.Count <= 1) return;
        Project.Barriers.Remove(barrier);
        UpdateSimulation();
    }

    [RelayCommand]
    public void UpdateSimulation()
    {
        var selectedReceiverId = SelectedReceiver?.Id;

        if (SelectedMaterial != null)
        {
            foreach (var b in Project.Barriers.Where(b => string.IsNullOrEmpty(b.MaterialId)))
                b.MaterialId = SelectedMaterial.Id;
        }

        NormalizeLinearSourceGeometry();

        var engine = new NoiseBarrierEngine(Project, Materials.ToList());
        engine.GenerateReceivers();
        engine.RunSimulation();

        SelectedReceiver = Project.Receivers.FirstOrDefault(r => r.Id == selectedReceiverId)
                           ?? Project.Receivers.FirstOrDefault(r => r.IsEnabled);

        UpdateSummary();
        UpdateSelectedReceiverBands();
        UpdateWarnings();

        SimulationRevision++;
        OnPropertyChanged(nameof(Project));
    }

    [RelayCommand]
    public void AddSource()
    {
        Project.Sources.Add(new NoiseSource
        {
            Name = "Yeni Kaynak",
            X = 0,
            Y = -25,
            Z = 0.5,
            XEnd = 20,
            YEnd = -25
        });
        UpdateSimulation();
    }

    [RelayCommand]
    public void AddManualMaterial()
    {
        var safeName = string.IsNullOrWhiteSpace(NewMaterialName) ? "Özel Bariyer Paneli" : NewMaterialName.Trim();
        var id = CreateMaterialId(safeName);
        var existing = Materials.FirstOrDefault(m => m.Id == id);
        if (existing != null)
            Materials.Remove(existing);

        var material = new NoiseBarrierMaterial
        {
            Id = id,
            Name = safeName,
            Category = string.IsNullOrWhiteSpace(ManualMaterialCategory) ? "Özel" : ManualMaterialCategory.Trim(),
            ThicknessMm = Math.Clamp(ManualMaterialThicknessMm, 1.0, 1000.0),
            SurfaceMass = Math.Clamp(ManualMaterialSurfaceMass, 0.1, 2000.0),
            Rw = NewMaterialRw,
            Nrc = NewMaterialNrc,
            Tl63 = NewMaterialTl63,
            Tl125 = NewMaterialTl125,
            Tl250 = NewMaterialTl250,
            Tl500 = NewMaterialTl500,
            Tl1k = NewMaterialTl1k,
            Tl2k = NewMaterialTl2k,
            Tl4k = NewMaterialTl4k,
            Tl8k = NewMaterialTl8k,
            Alpha63 = NewMaterialAlpha63,
            Alpha125 = NewMaterialAlpha125,
            Alpha250 = NewMaterialAlpha250,
            Alpha500 = NewMaterialAlpha500,
            Alpha1k = NewMaterialAlpha1k,
            Alpha2k = NewMaterialAlpha2k,
            Alpha4k = NewMaterialAlpha4k,
            Alpha8k = NewMaterialAlpha8k,
            Source = "Manuel",
            Confidence = 80
        };

        Materials.Add(material);
        SelectedMaterial = material;
        SaveCurrentLibrary();
        MaterialStatusText = $"{material.Name} eklendi.";
        UpdateSimulation();
    }

    [RelayCommand]
    public async Task SaveProjectAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow is null)
        {
            return;
        }

        var file = await desktop.MainWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Projeyi Kaydet",
            DefaultExtension = "kstnb",
            SuggestedFileName = "Ses_Bariyeri_Projesi.kstnb",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Ses Bariyeri Projesi") { Patterns = new[] { "*.kstnb" } }
            }
        });

        if (file is null)
            return;

        try
        {
            // Embed materials into the project before saving
            Project.EmbeddedMaterials.Clear();
            foreach (var m in Materials) Project.EmbeddedMaterials.Add(m);

            var json = System.Text.Json.JsonSerializer.Serialize(Project, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(file.Path.LocalPath, json);
            MaterialStatusText = "Proje kaydedildi.";
            UpdateWarnings();
        }
        catch (Exception ex)
        {
            MaterialStatusText = $"Kaydetme hatası: {ex.Message}";
            UpdateWarnings();
        }
    }

    [RelayCommand]
    public async Task LoadProjectAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow is null)
        {
            return;
        }

        var files = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Proje Aç",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Ses Bariyeri Projesi") { Patterns = new[] { "*.kstnb" } }
            }
        });

        if (files.Count == 0)
            return;

        try
        {
            var json = await File.ReadAllTextAsync(files[0].Path.LocalPath);
            var project = System.Text.Json.JsonSerializer.Deserialize<NoiseBarrierProject>(json);
            if (project != null)
            {
                Project = project;
                
                // Restore materials from embedded list
                if (Project.EmbeddedMaterials.Count > 0)
                {
                    Materials.Clear();
                    foreach (var m in Project.EmbeddedMaterials) Materials.Add(m);
                }

                SelectedMaterial = Materials.FirstOrDefault(m => m.Id == Project.Barriers.FirstOrDefault()?.MaterialId)
                                   ?? Materials.FirstOrDefault();
                
                UpdateSimulation();
                MaterialStatusText = "Proje yüklendi.";
            }
        }
        catch (Exception ex)
        {
            MaterialStatusText = $"Yükleme hatası: {ex.Message}";
            UpdateWarnings();
        }
    }

    [RelayCommand]
    public async Task ExportPdfAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow is null)
        {
            return;
        }

        var file = await desktop.MainWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "PDF Raporu Al",
            DefaultExtension = "pdf",
            SuggestedFileName = "Ses_Bariyeri_Raporu.pdf",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("PDF Belgesi") { Patterns = new[] { "*.pdf" } }
            }
        });

        if (file is null)
            return;

        try
        {
            await File.WriteAllBytesAsync(file.Path.LocalPath, BuildPdfReport());
            MaterialStatusText = "PDF raporu kaydedildi.";
            UpdateWarnings();
        }
        catch (Exception ex)
        {
            MaterialStatusText = $"PDF hatası: {ex.Message}";
            UpdateWarnings();
        }
    }

    [RelayCommand]
    public async Task ImportBarrierMaterialsFromExcelAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow is null)
        {
            return;
        }

        var files = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Ses Bariyeri Malzemelerini Excelden Aktar",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Excel Dosyası") { Patterns = new[] { "*.xlsx" } }
            }
        });

        if (files.Count == 0)
            return;

        try
        {
            var imported = NoiseBarrierLibraryService.ImportFromExcel(files[0].Path.LocalPath);
            foreach (var material in imported)
            {
                var existing = Materials.FirstOrDefault(m => m.Id == material.Id);
                if (existing != null)
                    Materials.Remove(existing);
                Materials.Add(material);
            }

            SaveCurrentLibrary();
            SelectedMaterial = imported.FirstOrDefault() ?? SelectedMaterial;
            MaterialStatusText = $"{imported.Count} malzeme aktarıldı.";
            UpdateSimulation();
        }
        catch (Exception ex)
        {
            MaterialStatusText = $"Excel aktarım hatası: {ex.Message}";
            UpdateWarnings();
        }
    }

    [RelayCommand]
    public async Task ExportBarrierMaterialTemplateAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow is null)
        {
            return;
        }

        var file = await desktop.MainWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Ses Bariyeri Excel Şablonunu Kaydet",
            DefaultExtension = "xlsx",
            SuggestedFileName = "Ses_Bariyeri_Malzeme_Sablonu.xlsx",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Excel Dosyası") { Patterns = new[] { "*.xlsx" } }
            }
        });

        if (file is null)
            return;

        try
        {
            NoiseBarrierLibraryService.GenerateExcelTemplate(file.Path.LocalPath);
            MaterialStatusText = "Excel şablonu kaydedildi.";
        }
        catch (Exception ex)
        {
            MaterialStatusText = $"Şablon kaydetme hatası: {ex.Message}";
            UpdateWarnings();
        }
    }

    [RelayCommand]
    public void RemoveSource(NoiseSource source)
    {
        Project.Sources.Remove(source);
        UpdateSimulation();
    }

    partial void OnSelectedReceiverChanged(ReceiverPoint? value)
    {
        UpdateSelectedReceiverBands();
    }

    partial void OnSelectedMaterialChanged(NoiseBarrierMaterial? value)
    {
        if (Project != null && value != null)
            UpdateSimulation();
    }

    private void UpdateSummary()
    {
        var activeReceivers = Project.Receivers.Where(r => r.IsEnabled).ToList();
        if (activeReceivers.Count == 0)
        {
            AverageNoBarrierText = "-";
            AverageWithBarrierText = "-";
            AverageInsertionLossText = "-";
            MinimumInsertionLossText = "-";
            MaximumInsertionLossText = "-";
            CriticalReceiverText = "-";
            return;
        }

        double noBarrier = EnergyAverage(activeReceivers.Select(r => r.TotalDbANoBarrier));
        double withBarrier = EnergyAverage(activeReceivers.Select(r => r.TotalDbAWithBarrier));
        double insertionLoss = noBarrier - withBarrier;
        var critical = activeReceivers.OrderBy(r => r.InsertionLoss).First();
        var best = activeReceivers.OrderByDescending(r => r.InsertionLoss).First();

        AverageNoBarrierText = $"{noBarrier:F1} dB(A)";
        AverageWithBarrierText = $"{withBarrier:F1} dB(A)";
        AverageInsertionLossText = $"{insertionLoss:F1} dB";
        MinimumInsertionLossText = $"{critical.InsertionLoss:F1} dB";
        MaximumInsertionLossText = $"{best.InsertionLoss:F1} dB";
        CriticalReceiverText = $"R{critical.Id} / Kat {critical.FloorIndex + 1} / {critical.TotalDbAWithBarrier:F1} dB(A)";
    }

    private void UpdateSelectedReceiverBands()
    {
        SelectedReceiverBands.Clear();
        if (SelectedReceiver is null)
            return;

        foreach (var freq in new double[] { 63, 125, 250, 500, 1000, 2000, 4000, 8000 })
        {
            SelectedReceiver.LevelNoBarrier.TryGetValue(freq, out var noBarrier);
            SelectedReceiver.LevelWithBarrier.TryGetValue(freq, out var withBarrier);
            SelectedReceiverBands.Add(new ReceiverBandRow(freq, noBarrier, withBarrier, noBarrier - withBarrier));
        }
    }

    private void UpdateWarnings()
    {
        ValidationWarnings.Clear();
        if (!string.IsNullOrWhiteSpace(NoiseBarrierLibraryService.LastError))
            ValidationWarnings.Add($"Malzeme kütüphanesi uyarısı: {NoiseBarrierLibraryService.LastError}");

        foreach (var warning in Project.ValidationWarnings.Distinct())
            ValidationWarnings.Add(warning);
    }

    private static double EnergyAverage(IEnumerable<double> levels)
    {
        var values = levels.ToList();
        if (values.Count == 0) return 0;

        double energy = values.Sum(level => Math.Pow(10, level / 10.0));
        return 10 * Math.Log10(Math.Max(1e-12, energy / values.Count));
    }

    private void NormalizeLinearSourceGeometry()
    {
        foreach (var source in Project.Sources.Where(s => s.Type == SourceType.Line || s.Type == SourceType.Traffic))
        {
            var dx = source.XEnd - source.X;
            var dy = source.YEnd - source.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            var pointsTowardBuilding = Math.Abs(dx) < Math.Max(0.2, Math.Abs(dy) * 0.35) && source.YEnd > source.Y;
            if (length >= 0.1 && !pointsTowardBuilding)
                continue;

            var centerX = (source.X + source.XEnd) / 2.0;
            if (length < 0.1)
                centerX = source.X;

            var roadLength = Math.Max(50.0, Project.Building.Width + 20.0);
            source.X = centerX - roadLength / 2.0;
            source.XEnd = centerX + roadLength / 2.0;
            source.YEnd = source.Y;
        }
    }

    private void SaveCurrentLibrary()
    {
        NoiseBarrierLibraryService.SaveLibrary(Materials.ToList());
        if (!string.IsNullOrWhiteSpace(NoiseBarrierLibraryService.LastError))
            MaterialStatusText = $"Kütüphane kayıt uyarısı: {NoiseBarrierLibraryService.LastError}";
    }

    private static string CreateMaterialId(string name)
    {
        var chars = name
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray();
        var slug = new string(chars).Trim('_');
        while (slug.Contains("__", StringComparison.Ordinal))
            slug = slug.Replace("__", "_", StringComparison.Ordinal);

        return $"manual_{(string.IsNullOrWhiteSpace(slug) ? "barrier" : slug)}";
    }

    private byte[] BuildPdfReport()
    {
        var lines = new List<string>
        {
            "EYE4PRO ACOUSTICS - SES BARIYERI RAPORU",
            $"Tarih: {DateTime.Now:yyyy-MM-dd HH:mm}",
            $"Proje: {Project.Name}",
            $"Malzeme: {SelectedMaterial?.Name ?? "-"}",
            $"Bina: {Project.Building.Width:F1} x {Project.Building.Depth:F1} x {Project.Building.Height:F1} m",
            $"Bariyer Sayisi: {Project.Barriers.Count(b => b.IsEnabled)} (toplam {Project.Barriers.Count})",
            $"Bariyersiz Ortalama: {AverageNoBarrierText}",
            $"Bariyerli Ortalama: {AverageWithBarrierText}",
            $"Iyilestirme: {AverageInsertionLossText}",
            $"Minimum Iyilestirme: {MinimumInsertionLossText}",
            $"Maksimum Iyilestirme: {MaximumInsertionLossText}",
            $"Kritik Alici: {CriticalReceiverText}",
            "",
            "Bariyerler"
        };

        foreach (var barrier in Project.Barriers)
        {
            var mat = Materials.FirstOrDefault(m => m.Id == barrier.MaterialId);
            lines.Add($"- {barrier.Name} (L={barrier.Length:F1} m, H={barrier.Height:F1} m, Uzaklik={barrier.DistanceFromBuilding:F1} m, Malzeme={mat?.Name ?? "-"}, {(barrier.IsEnabled ? "aktif" : "pasif")})");
        }

        lines.Add("");
        lines.Add("Alici Sonuclari");

        foreach (var receiver in Project.Receivers.Where(r => r.IsEnabled).Take(18))
        {
            lines.Add($"R{receiver.Id}: Z={receiver.Z:F1} m, bariyersiz={receiver.TotalDbANoBarrier:F1} dB(A), bariyerli={receiver.TotalDbAWithBarrier:F1} dB(A), IL={receiver.InsertionLoss:F1} dB");
        }

        if (Project.ValidationWarnings.Count > 0)
        {
            lines.Add("");
            lines.Add("Uyarilar");
            lines.AddRange(Project.ValidationWarnings.Take(8).Select(SanitizePdfText));
        }

        return CreateSimplePdf(lines.Select(SanitizePdfText).ToList());
    }

    private static byte[] CreateSimplePdf(IReadOnlyList<string> lines)
    {
        var content = new StringBuilder();
        content.AppendLine("BT");
        content.AppendLine("/F1 10 Tf");
        content.AppendLine("50 790 Td");
        foreach (var line in lines)
        {
            content.Append('(').Append(EscapePdfText(line)).AppendLine(") Tj");
            content.AppendLine("0 -16 Td");
        }
        content.AppendLine("ET");

        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content.ToString())} >>\nstream\n{content}endstream"
        };

        var pdf = new StringBuilder();
        var offsets = new List<int> { 0 };
        pdf.AppendLine("%PDF-1.4");
        for (int i = 0; i < objects.Count; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.AppendLine($"{i + 1} 0 obj");
            pdf.AppendLine(objects[i]);
            pdf.AppendLine("endobj");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.AppendLine("xref");
        pdf.AppendLine($"0 {objects.Count + 1}");
        pdf.AppendLine("0000000000 65535 f ");
        for (int i = 1; i < offsets.Count; i++)
            pdf.AppendLine($"{offsets[i]:D10} 00000 n ");

        pdf.AppendLine("trailer");
        pdf.AppendLine($"<< /Size {objects.Count + 1} /Root 1 0 R >>");
        pdf.AppendLine("startxref");
        pdf.AppendLine(xrefOffset.ToString());
        pdf.AppendLine("%%EOF");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    private static string SanitizePdfText(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(ch switch
            {
                'ç' or 'Ç' => 'c',
                'ğ' or 'Ğ' => 'g',
                'ı' or 'İ' => 'i',
                'ö' or 'Ö' => 'o',
                'ş' or 'Ş' => 's',
                'ü' or 'Ü' => 'u',
                >= ' ' and <= '~' => ch,
                _ => ' '
            });
        }

        return builder.ToString();
    }

    private static string EscapePdfText(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }
}

public sealed record ReceiverBandRow(double FrequencyHz, double NoBarrierDb, double WithBarrierDb, double InsertionLossDb);
