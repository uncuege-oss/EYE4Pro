using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Kst.Acoustics.App.Views;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.App.ViewModels;

public partial class MainWindowViewModel
{
    private bool _isSavingProject;

    public async Task PickAndImportAsync(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var storageProvider = window.StorageProvider;
        if (storageProvider is null)
        {
            StatusMessage = "Bu platformda dosya seçici kullanılamıyor.";
            return;
        }

        IReadOnlyList<IStorageFile> files;
        try
        {
            files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "İçe aktarılacak DXF dosyasını seçin",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("DXF Dosyaları")
                    {
                        Patterns = ["*.dxf"]
                    }
                ]
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Dosya seçici açılamadı: {ex.Message}";
            return;
        }

        var file = files.FirstOrDefault();
        if (file is null)
        {
            StatusMessage = "İçe aktarma iptal edildi.";
            return;
        }

        var path = file.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusMessage = "Seçilen dosya yerel bir yol olarak kullanılamıyor.";
            return;
        }

        await ImportAsync(path, window);
    }

    /// <returns>true: kaydedildi. false: kullanıcı iptal etti veya kaydetme başarısız oldu.</returns>
    public async Task<bool> SaveProjectAsync(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (_currentProject is null && SurfacePreviews.Count == 0)
        {
            StatusMessage = "Kaydetmeden önce içe aktarılmış bir proje oluşturulmalıdır.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            StatusMessage = "Proje adı boş olamaz.";
            return false;
        }

        if (_isSavingProject)
        {
            StatusMessage = "Bir kayıt işlemi zaten sürüyor.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(_savedProjectPath))
            return await SaveToPathAsync(_savedProjectPath);

        var storageProvider = window.StorageProvider;
        if (storageProvider is null)
        {
            StatusMessage = "Bu platformda dosya kaydetme penceresi kullanılamıyor.";
            return false;
        }

        IStorageFile? file;
        try
        {
            var suggestedName = $"{SanitizeFileName(ProjectName)}.e4p01";
            file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Proje dosyasını kaydedin",
                SuggestedFileName = suggestedName,
                FileTypeChoices =
                [
                    new FilePickerFileType("EYE4PRO M01 Proje Dosyası") { Patterns = ["*.e4p01"] },
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
            StatusMessage = "Proje kaydetme iptal edildi.";
            return false;
        }

        return await SaveToPathAsync(path);
    }

    private async Task<bool> SaveToPathAsync(string path)
    {
        if (_isSavingProject) return false;
        _isSavingProject = true;
        var revisionAtSave = _revision;
        StatusMessage = "Kaydediliyor...";
        try
        {
            var project = BuildProjectSnapshot();
            var preserveBackup = PathEquals(path, _backupPreservationPath);
            await _projectFileService.SaveAsync(path, project, preserveBackup);
            _savedProjectPath = path;
            _backupPreservationPath = null;
            SelectedFilePath = path;
            var fullySaved = _revision == revisionAtSave;
            if (fullySaved)
                MarkClean();
            StatusMessage = fullySaved
                ? $"Proje kaydedildi: {path}"
                : "Proje kaydedildi; kayıt sırasında yapılan yeni değişiklikler henüz kaydedilmedi.";
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

    private static string BuildSaveErrorMessage(string path, Exception ex) => ex switch
    {
        UnauthorizedAccessException =>
            $"Kaydedilemedi: '{Path.GetFileName(path)}' için yazma izniniz yok. Farklı bir konum seçin.",
        DirectoryNotFoundException =>
            "Kaydedilemedi: hedef klasör bulunamadı. Geçerli bir konum seçin.",
        IOException =>
            $"Kaydedilemedi: dosya sistemi hatası ({ex.Message}). Disk dolu olabilir ya da dosya başka bir programda açık olabilir.",
        _ => $"Kaydetme hatası: {ex.Message}"
    };

    public async Task OpenProjectAsync(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var storageProvider = window.StorageProvider;
        if (storageProvider is null)
        {
            StatusMessage = "Bu platformda dosya seçici kullanılamıyor.";
            return;
        }

        IReadOnlyList<IStorageFile> files;
        try
        {
            files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Açılacak proje dosyasını seçin",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("EYE4PRO M01 Proje Dosyaları")
                    {
                        Patterns = ["*.e4p01"]
                    },
                    new FilePickerFileType("JSON Dosyaları")
                    {
                        Patterns = ["*.json"]
                    }
                ]
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Dosya seçici açılamadı: {ex.Message}";
            return;
        }

        var file = files.FirstOrDefault();
        if (file is null)
        {
            StatusMessage = "Proje açma iptal edildi.";
            return;
        }

        var path = file.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusMessage = "Seçilen proje dosyası yerel bir yol olarak kullanılamıyor.";
            return;
        }

        try
        {
            StatusMessage = "Proje açılıyor...";
            var loadResult = await _projectFileService.LoadWithStatusAsync(path);
            var project = loadResult.Project;
            ApplyProject(
                project,
                project.ImportFormat,
                markDirty: loadResult.RecoveredFromBackup,
                savedProjectPath: path,
                recoveredFromBackup: loadResult.RecoveredFromBackup);
            ApplySessionWarnings(loadResult.SessionWarnings);
            StatusMessage = loadResult.RecoveredFromBackup
                ? "Proje son sağlam yedekten kurtarıldı. İçeriği doğrulayıp yeniden kaydedin."
                : SalonRevizyonNotu(project) is { } not
                    ? $"Proje açıldı. {not}"
                    : "Proje başarıyla açıldı.";
        }
        catch (Exception ex)
        {
            // Kullanıcıya sade Türkçe mesaj; teknik ayrıntı (tip/stack trace) yalnız log'a gider.
            StatusMessage = BuildLoadErrorMessage(ex);
            System.Console.Error.WriteLine($"[Project Load Error] {ex}");
            System.Diagnostics.Debug.WriteLine($"Project Load Error: {ex}");
        }
    }

    private static string BuildLoadErrorMessage(Exception ex) => ex switch
    {
        UnsupportedProjectSchemaException =>
            $"Proje dosyası bu uygulama sürümünden daha yeni: {ex.Message} EYE4PRO'yu güncelleyin.",
        InvalidDataException => $"Proje dosyası geçersiz ya da bozuk: {ex.Message}",
        UnauthorizedAccessException => "Proje açılamadı: dosya için okuma izniniz yok.",
        FileNotFoundException => "Proje açılamadı: dosya bulunamadı. Taşınmış ya da silinmiş olabilir.",
        IOException => "Proje açılamadı: dosya sistemi hatası. Dosya başka bir programda açık olabilir.",
        _ => "Proje açılırken beklenmeyen bir hata oluştu. Ayrıntılar için günlüğe bakın."
    };

    private static bool PathEquals(string left, string? right) =>
        right is not null && string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// M51 "Salon Tasarla" çıktısını (`.e4salon.json`) yükler ve M01 projesine uygular.
    public async Task SalonYukleAsync(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var storageProvider = window.StorageProvider;
        if (storageProvider is null)
        {
            StatusMessage = "Bu platformda dosya seçici kullanılamıyor.";
            return;
        }

        IReadOnlyList<IStorageFile> files;
        try
        {
            files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Salon geometrisi seçin (.e4salon.json)",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("EYE4PRO Salon Geometrisi (.e4salon.json)")
                    {
                        Patterns = ["*.e4salon.json"]
                    }
                ]
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Dosya seçici açılamadı: {ex.Message}";
            return;
        }

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusMessage = "Salon yükleme iptal edildi.";
            return;
        }

        try
        {
            StatusMessage = "Salon yükleniyor...";
            var (project, uyarilar) = await Task.Run(() =>
            {
                var salon = Kst.Acoustics.Core.Salon.Services.SalonGeometriServisi.Ac(path);
                return (Kst.Acoustics.Core.Salon.Services.SalonImporter.Cevir(salon, path),
                        Kst.Acoustics.Core.Salon.Services.SalonDogrulamaServisi.Dogrula(salon));
            });
            project = project with { ImportWarnings = uyarilar.ToArray() };
            SelectedFilePath = path;
            ApplyProject(project, project.ImportFormat, markDirty: true, savedProjectPath: null);
            SetImportWarnings(uyarilar);
            StatusMessage = $"Salon yüklendi: {project.Surfaces.Count} yüzey · hacim ≈ {project.RoomVolume:N0} m³"
                + (uyarilar.Count > 0
                    ? $" · Uyarı: {uyarilar.Count} geometri sorunu — M51'de 'Kontrol' çalıştırın"
                    : "");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Salon yüklenemedi: {ex.Message}";
        }
    }

    /// Salon kaynaklı projede kaynak `.e4salon.json` dosyası proje kaydedildikten sonra
    /// güncellenmişse kullanıcıya not döner; değilse null.
    private static string? SalonRevizyonNotu(AcousticsProject project)
    {
        if (project.ImportFormat != SurfaceImportFormat.Salon) return null;
        if (project.KaynakGeometriRevizyon is not int rev) return null;
        var yol = project.SourceFilePath;
        if (string.IsNullOrWhiteSpace(yol) || !File.Exists(yol)) return null;
        try
        {
            int guncel = Kst.Acoustics.Core.Salon.Services.SalonGeometriServisi.Ac(yol).GeometriRevizyon;
            return guncel != rev
                ? $"Uyarı: Salon geometrisi güncellenmiş (Rev {rev} → {guncel}) — 'Salon Yükle' ile yeniden alın."
                : null;
        }
        catch
        {
            return null; // kaynak okunamadıysa uyarı üretme
        }
    }

    public Task ImportAsync(string path) => ImportAsync(path, null);

    private async Task ImportAsync(string path, Window? owner)
    {
        StatusMessage = "İçe aktarma devam ediyor...";

        try
        {
            if (!string.Equals(Path.GetExtension(path), ".dxf", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Bu modülde yalnızca DXF dosyası içe aktarılabilir.");
            }

            var result = await Task.Run(() => _importService.ParseFile(path));
            result = await ConfirmDxfUnitIfNeededAsync(result, owner);
            var projectName = Path.GetFileNameWithoutExtension(path);
            var importedProject = ProjectFactory.CreateFromImport(projectName, path, result);
            SelectedFilePath = path;
            ApplyProject(importedProject, result.Format, markDirty: true, savedProjectPath: null);
            SetImportWarnings(importedProject.ImportWarnings);
            StatusMessage = ImportWarnings.Count > 0
                ? $"İçe aktarma tamamlandı — {ImportWarnings.Count} geometri uyarısı var (aşağıdaki panele bakın)."
                : "İçe aktarma önizlemesi başarıyla yüklendi.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "İçe aktarma iptal edildi; açık proje değiştirilmedi.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"İçe aktarma hatası: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Import Error: {ex}");
        }
    }

    private async Task<SurfaceImportResult> ConfirmDxfUnitIfNeededAsync(SurfaceImportResult result, Window? owner)
    {
        if (result.Format != SurfaceImportFormat.Dxf || result.Document.UnitDetected)
        {
            return result;
        }

        if (owner is null)
        {
            StatusMessage = "DXF birimi belirtilmemiş; varsayılan birim milimetre olarak kabul edildi.";
            return result;
        }

        var suggestedScale = ProjectScalingService.GetMetersPerUnit(result.Document.Unit);
        var summary = $"DXF $INSUNITS kodu: {result.Document.InsUnitsCode}. Yüzey sayısı: {result.Document.Surfaces.Count}.";
        var chosenScale = await DxfUnitConfirmDialog.ShowAsync(owner, summary, suggestedScale, detected: false);
        if (chosenScale is null)
        {
            throw new OperationCanceledException("DXF birim seçimi iptal edildi.");
        }

        var chosenUnit = UnitFromMetersPerUnit(chosenScale.Value);
        if (chosenUnit is not null)
        {
            return result with
            {
                Document = result.Document with
                {
                    Unit = chosenUnit.Value,
                    UnitDetected = true
                }
            };
        }

        var scaledSurfaces = result.Document.Surfaces
            .Select(surface => surface with
            {
                Vertices = surface.Vertices
                    .Select(v => new Point3D(v.X * chosenScale.Value, v.Y * chosenScale.Value, v.Z * chosenScale.Value))
                    .ToArray()
            })
            .ToArray();

        return result with
        {
            Document = result.Document with
            {
                Unit = SurfaceImportUnit.Meters,
                Surfaces = scaledSurfaces,
                UnitDetected = true
            }
        };
    }

    private static SurfaceImportUnit? UnitFromMetersPerUnit(double metersPerUnit)
    {
        foreach (var unit in new[] { SurfaceImportUnit.Meters, SurfaceImportUnit.Centimeters, SurfaceImportUnit.Millimeters, SurfaceImportUnit.Inches, SurfaceImportUnit.Feet })
        {
            if (Math.Abs(ProjectScalingService.GetMetersPerUnit(unit) - metersPerUnit) < 1e-9)
            {
                return unit;
            }
        }

        return null;
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "YeniProje" : cleaned;
    }
}
