using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using System.Text;
using CommunityToolkit.Mvvm.Input;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Simulation;

namespace Kst.Acoustics.App.ViewModels;

public sealed partial class ProjectWindowViewModel
{
    public string ReportFullText
    {
        get => _reportFullText;
        private set => SetProperty(ref _reportFullText, value);
    }
    private string _reportFullText = "Simülasyon sonuçları üretildikten sonra rapor oluşturulur.";

    [RelayCommand]
    public void GenerateReport()
    {
        if (_lastResult == null)
        {
            ReportFullText = "Lütfen önce bir simülasyon çalıştırın.";
            return;
        }

        var sb = new StringBuilder();
        var criteria = RoomTypeRegistry.All.FirstOrDefault(c => c.Type == _project.SelectedRoomType) ?? RoomTypeRegistry.All[0];
        var (minRt, maxRt) = criteria.GetTargetRtMid(RoomVolume);

        sb.AppendLine("================================================================");
        sb.AppendLine("                EYE4PRO ACOUSTICS - ANALİZ RAPORU               ");
        sb.AppendLine("================================================================");
        sb.AppendLine($"Proje Adı:  {_project.Name}");
        sb.AppendLine($"Tarih:      {DateTime.Now:dd.MM.yyyy HH:mm}");
        sb.AppendLine($"Salon Tipi: {criteria.DisplayName}");
        sb.AppendLine($"Hacim:      {RoomVolume:F1} m³");
        sb.AppendLine($"Hedef RT60: {minRt:F2} - {maxRt:F2} s");
        sb.AppendLine($"Sonuç İzi:  {(_lastResult.InputFingerprint ?? "Eski sürümde kaydedilmemiş")}");
        sb.AppendLine($"Sonuç Durumu: {(IsSimulationResultStale ? "ESKİ — girdiler değişti" : "Güncel")}");
        if (IsSimulationResultStale && !string.IsNullOrWhiteSpace(_lastResult.StaleReason))
            sb.AppendLine($"Eskime Nedeni: {_lastResult.StaleReason}");
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine();

        if (_lastResult.Reliability is { } reliability)
        {
            sb.AppendLine("================================================================");
            sb.AppendLine("                    GÜVENİLİRLİK VE KALİTE KONTROLÜ             ");
            sb.AppendLine("================================================================");
            sb.AppendLine($"Güven Puanı: {reliability.ConfidenceScore:F0}/100 ({reliability.ConfidenceLabel})");
            sb.AppendLine($"Özet:        {reliability.Summary}");
            sb.AppendLine();
            sb.AppendLine("Uyarılar:");
            foreach (var warning in reliability.Warnings)
                sb.AppendLine($"- {warning}");
            sb.AppendLine();
            sb.AppendLine("Öneriler:");
            foreach (var recommendation in reliability.Recommendations)
                sb.AppendLine($"- {recommendation}");
            sb.AppendLine();
            sb.AppendLine("Yazılım Kontrolleri:");
            sb.AppendLine($"- {MaterialAuditText}");
            sb.AppendLine($"- {ConvergenceSummaryText}");
            sb.AppendLine($"- {ResultComparisonText}");
            sb.AppendLine("- Ölçüm kalibrasyonu: Yerinde Ölçüm modülünden alınan RT/C80/STI verileriyle model sapması karşılaştırılmalıdır.");
            sb.AppendLine("----------------------------------------------------------------");
            sb.AppendLine();
        }

        var allBandParams = new List<BandParameters>();

        for (var echogramIndex = 0; echogramIndex < _lastResult.Echograms.Count; echogramIndex++)
        {
            var echogram = _lastResult.Echograms[echogramIndex];
            var ap = _lastResult.ComputedParameters is { Count: > 0 } computed && echogramIndex < computed.Count
                ? computed[echogramIndex]
                : AcousticParameterCalculator.Compute(echogram, _lastResult.Parameters);
            sb.AppendLine($">> KAYNAK: {echogram.SourceLabel} → ALICI: {echogram.ReceiverLabel}");
            sb.AppendLine("   Frekans (Hz) | RT60 (s) | C80 (dB) | Ts (ms) | LF (%) ");
            sb.AppendLine("   -------------|----------|----------|---------|--------");

            foreach (var b in ap.Bands)
            {
                sb.AppendLine($"   {b.FrequencyHz,12} | {b.RT60,8:F2} | {b.C80,8:F1} | {b.Ts,7:F1} | {b.LF,6:P0}");
                allBandParams.Add(b);
            }
            sb.AppendLine();
        }

        // Ortalamalar
        sb.AppendLine("================================================================");
        sb.AppendLine("                       GENEL ORTALAMALAR                        ");
        sb.AppendLine("================================================================");
        
        var freqs = new[] { 63, 125, 250, 500, 1000, 2000, 4000, 8000 };
        var avgRtMid = 0.0;
        var midCount = 0;

        foreach (var f in freqs)
        {
            var bandValues = allBandParams.Where(b => b.FrequencyHz == f).ToList();
            if (bandValues.Count > 0)
            {
                var avgRt = AverageFinite(bandValues.Select(b => b.RT60));
                var avgC80 = AverageFinite(bandValues.Select(b => b.C80));
                sb.AppendLine($"{f,7} Hz Ortalama RT60: {FormatFinite(avgRt, "F2")} s, C80: {FormatFinite(avgC80, "F1")} dB");

                if ((f == 500 || f == 1000) && double.IsFinite(avgRt))
                {
                    avgRtMid += avgRt;
                    midCount++;
                }
            }
        }
        
        avgRtMid = midCount > 0 ? avgRtMid / midCount : double.NaN;
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine($"ORTALAMA RT60 (500-1k Hz): {FormatFinite(avgRtMid, "F2")} s");
        sb.AppendLine();

        // Uygunluk Kontrolü
        sb.AppendLine("================================================================");
        sb.AppendLine("                      UYGUNLUK DEĞERLENDİRMESİ                  ");
        sb.AppendLine("================================================================");
        
        if (!double.IsFinite(avgRtMid))
        {
            sb.AppendLine("DURUM: VERİ EKSİK");
            sb.AppendLine("Sonuç: 500 Hz ve 1 kHz RT60 ortalaması hesaplanamadı.");
        }
        else
        {
            bool isOk = avgRtMid >= minRt && avgRtMid <= maxRt;
        if (isOk)
        {
            sb.AppendLine("DURUM: UYGUN");
            sb.AppendLine($"Sonuç: Ortalama RT60 ({avgRtMid:F2}s), hedef aralığında ({minRt:F2}-{maxRt:F2}s).");
        }
        else
        {
            sb.AppendLine("DURUM: UYGUN DEĞİL");
            if (avgRtMid < minRt)
                sb.AppendLine($"Sonuç: Oda çok 'Kuru' (RT düşük). Hedef en az {minRt:F2}s olmalı.");
            else
                sb.AppendLine($"Sonuç: Oda çok 'Çınlamalı' (RT yüksek). Hedef en fazla {maxRt:F2}s olmalı.");
        }
        }
        sb.AppendLine("================================================================");
        sb.AppendLine();


        ReportFullText = sb.ToString();
    }

    [RelayCommand]
    private async Task ExportReport()
    {
        try
        {
            var path = await PickSavePathAsync(
                "Akustik raporu kaydedin",
                $"Acoustic_Report_{SanitizeFileName(_project.Name)}.txt",
                new FilePickerFileType("Metin Raporu") { Patterns = ["*.txt"] });

            if (string.IsNullOrWhiteSpace(path))
            {
                StatusMessage = "Rapor kaydetme iptal edildi.";
                return;
            }

            await File.WriteAllTextAsync(path, ReportFullText);
            StatusMessage = $"Rapor kaydedildi: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Rapor kaydedilemedi: {ex.Message}";
        }
    }
    
    [RelayCommand]
    private async Task ExportGeometryDxf()
    {
        try
        {
            var path = await PickSavePathAsync(
                "DXF modeli kaydedin",
                $"Geometry_Export_{SanitizeFileName(_project.Name)}.dxf",
                new FilePickerFileType("DXF Dosyası") { Patterns = ["*.dxf"] });

            if (string.IsNullOrWhiteSpace(path))
            {
                StatusMessage = "DXF kaydetme iptal edildi.";
                return;
            }

            var exporter = new Kst.Acoustics.Core.Exporting.DxfExportService();
            exporter.Export(path, _workingSurfaces);
            StatusMessage = $"DXF modeli kaydedildi: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"DXF kaydedilemedi: {ex.Message}";
        }
    }

    private static double AverageFinite(IEnumerable<double> values)
    {
        var valid = values.Where(double.IsFinite).ToArray();
        return valid.Length == 0 ? double.NaN : valid.Average();
    }

    private static string FormatFinite(double value, string format) =>
        double.IsFinite(value) ? value.ToString(format) : "—";

    private async Task<string?> PickSavePathAsync(string title, string suggestedFileName, FilePickerFileType fileType)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow is null)
            return null;

        var file = await desktop.MainWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            FileTypeChoices = [fileType]
        });

        return file?.TryGetLocalPath();
    }
}
