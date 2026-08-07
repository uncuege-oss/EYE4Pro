using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.App.ViewModels;

public sealed partial class ProjectWindowViewModel
{
    private readonly ProjectFileService _projectFileService = new();
    private string? _savedFilePath;
    private string? _backupPreservationPath;
    private bool _isSavingProject;
    private long _revision;
    private long _savedRevision;

    /// <summary>
    /// Diske gerçekten yazılmış snapshot'ı pencere yaşam döngüsüne bildirir. Ana pencere
    /// bu kopyayı son sağlam baseline olarak tutar; böylece kayıttan sonraki düzenlemeler
    /// "Kaydetmeden Çık" ile atıldığında daha eski bir belgeye dönülmez.
    /// </summary>
    internal event Action<AcousticsProject, string>? SavedSnapshotAvailable;

    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
    }
    private bool _isDirty;

    public void MarkDirty()
    {
        _revision++;
        IsDirty = true;
    }

    private void MarkClean()
    {
        _savedRevision = _revision;
        IsDirty = false;
    }

    internal string? SavedFilePath => _savedFilePath;
    internal bool RecoveredFromBackup => _backupPreservationPath is not null;
    internal void SetDocumentState(string? path, bool isDirty, bool recoveredFromBackup = false)
    {
        _savedFilePath = path;
        _backupPreservationPath = recoveredFromBackup && !string.IsNullOrWhiteSpace(path)
            ? Path.GetFullPath(path)
            : null;
        if (isDirty) MarkDirty();
        else MarkClean();
    }

    internal void SetSavedFilePath(string path)
    {
        _savedFilePath = path;
        _backupPreservationPath = null;
        MarkClean();
    }
    internal void ReportError(string message) => StatusMessage = message;

    /// <returns>true: kaydedildi. false: kullanıcı iptal etti veya kaydetme başarısız oldu.</returns>
    public async Task<bool> SaveProjectAsync(Window window)
    {
        if (_isSavingProject)
        {
            StatusMessage = "Bir kayıt işlemi zaten sürüyor.";
            return false;
        }
        if (!string.IsNullOrWhiteSpace(_savedFilePath))
        {
            return await DoSaveAsync(_savedFilePath);
        }
        return await SaveAsProjectAsync(window);
    }

    /// <returns>true: kaydedildi. false: kullanıcı iptal etti veya kaydetme başarısız oldu.</returns>
    public async Task<bool> SaveAsProjectAsync(Window window)
    {
        var storageProvider = window.StorageProvider;
        if (storageProvider is null)
        {
            StatusMessage = "Kaydetme desteklenmiyor (StorageProvider eksik).";
            return false;
        }

        var suggestedName = $"{SanitizeFileName(ProjectName)}.e4p01";

        IStorageFile? file;
        try
        {
            file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Proje dosyasını kaydedin",
                SuggestedFileName = suggestedName,
                FileTypeChoices =
                [
                    new FilePickerFileType("EYE4PRO Akustik Proje") { Patterns = ["*.e4p01"] },
                    new FilePickerFileType("JSON Dosyası") { Patterns = ["*.json"] }
                ]
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Dosya seçici açılamadı: {ex.Message}";
            return false;
        }

        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusMessage = "Kaydetme iptal edildi.";
            return false;
        }

        return await DoSaveAsync(path);
    }

    private async Task<bool> DoSaveAsync(string path)
    {
        if (_isSavingProject) return false;
        _isSavingProject = true;
        var revisionAtSave = _revision;
        StatusMessage = "Kaydediliyor...";
        try
        {
            if (HasInvalidPoints)
                throw new InvalidDataException(PointValidationMessage);

            var project = BuildCurrentProject();
            var preserveBackup = PathEquals(path, _backupPreservationPath);
            await _projectFileService.SaveAsync(path, project, preserveBackup);
            _savedFilePath = path;
            _backupPreservationPath = null;
            PublishSavedSnapshot(project, path);
            var fullySaved = _revision == revisionAtSave;
            if (fullySaved) MarkClean();
            StatusMessage = fullySaved
                ? $"Kaydedildi: {Path.GetFileName(path)}"
                : "Dosya kaydedildi; kayıt sırasında yapılan yeni değişiklikler henüz kaydedilmedi.";
            return fullySaved;
        }
        catch (Exception ex)
        {
            // IsDirty bilerek dokunulmadı — kayıt başarısız oldu, değişiklikler hâlâ kaydedilmemiş.
            StatusMessage = BuildSaveErrorMessage(path, ex);
            return false;
        }
        finally
        {
            _isSavingProject = false;
        }
    }

    private void PublishSavedSnapshot(AcousticsProject project, string path)
    {
        if (SavedSnapshotAvailable is null) return;

        try
        {
            SavedSnapshotAvailable(project, path);
        }
        catch (Exception ex)
        {
            // Dosya bu noktada başarıyla yazılmıştır. UI eşitleme hatası kaydı başarısız
            // gibi göstermemeli; ayrıntı tanılama kanalında tutulur.
            System.Diagnostics.Debug.WriteLine($"Saved snapshot propagation failed: {ex}");
        }
    }

    private static string BuildSaveErrorMessage(string path, Exception ex) => ex switch
    {
        UnauthorizedAccessException =>
            $"Kaydedilemedi: '{Path.GetFileName(path)}' için yazma izniniz yok. 'Farklı Kaydet' ile başka bir konum seçin.",
        DirectoryNotFoundException =>
            "Kaydedilemedi: hedef klasör bulunamadı. 'Farklı Kaydet' ile geçerli bir konum seçin.",
        IOException =>
            $"Kaydedilemedi: dosya sistemi hatası ({ex.Message}). Disk dolu olabilir ya da dosya başka bir programda açık olabilir.",
        _ => $"Kaydetme hatası: {ex.Message}"
    };

    public AcousticsProject BuildCurrentProject()
    {
        var sources = BuildValidatedPointSnapshot(Sources, "kaynak");
        var receivers = BuildValidatedPointSnapshot(Receivers, "alıcı");
        return _project with
        {
            Surfaces          = _workingSurfaces.Select(s => s with { Vertices = s.Vertices.ToArray() }).ToArray(),
            Sources           = sources,
            Receivers         = receivers,
            CategoryMaterials = new Dictionary<string, string>(BuildCategoryMaterialMap(), StringComparer.OrdinalIgnoreCase),
            SurfaceMaterials  = new Dictionary<string, string>(BuildSurfaceMaterialMap(), StringComparer.OrdinalIgnoreCase),
            RoomVolume        = RoomVolume,
            VolumeOrigin      = VolumeOrigin,
            LastResult        = LastResult,
            SimulationSettings = BuildStoredSimulationSettings(),
            PanelKonfigurasyon = ClonePanelConfiguration(PanelKonfigurasyon),
            ImportWarnings    = _project.ImportWarnings.ToArray()
        };
    }

    private static IReadOnlyList<AcousticPoint> BuildValidatedPointSnapshot(
        IEnumerable<AcousticPointViewModel> rows,
        string role)
    {
        var points = new List<AcousticPoint>();
        var invalid = new List<string>();
        foreach (var row in rows)
        {
            if (row.TryBuildPoint() is { } point) points.Add(point);
            else invalid.Add(string.IsNullOrWhiteSpace(row.Label) ? $"ID {row.Id}" : row.Label);
        }

        if (invalid.Count > 0)
            throw new InvalidDataException(
                $"Geçersiz {role} satırları var: {string.Join(", ", invalid)}. Alanları düzeltmeden kayıt yapılamaz.");

        return points;
    }

    private static string SanitizeFileName(string value)
    {
        var invalid  = Path.GetInvalidFileNameChars();
        var cleaned  = new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "YeniProje" : cleaned;
    }

    private static bool PathEquals(string left, string? right) =>
        right is not null && string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
