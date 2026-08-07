using System.Text.Json;
using System.Text.Json.Serialization;
using System.Runtime.ExceptionServices;

namespace Kst.Acoustics.Core.Projects;

/// <summary>
/// Proje içeriği ile yalnız bu açma oturumuna ait kurtarma bilgisini ayırır. Oturum
/// uyarıları tekrar kaydedilmez; aksi halde onarılmış bir dosya sonraki açılışlarda da
/// yanlışlıkla ".bak'tan açıldı" uyarısı taşır.
/// </summary>
public sealed record ProjectLoadResult(
    AcousticsProject Project,
    bool RecoveredFromBackup,
    IReadOnlyList<string> SessionWarnings);

public sealed class ProjectFileService
{
    private const string BackupRecoveryWarning =
        "Ana proje dosyası okunamadı; son sağlam .bak yedeği açıldı. " +
        "İçeriği doğrulayıp projeyi yeniden kaydedin.";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters =
        {
            new NonFiniteDoubleJsonConverter(),
            new JsonStringEnumConverter()
        }
    };

    public void Save(
        string path,
        AcousticsProject project,
        bool preserveExistingBackup = false)
    {
        // Geriye dönük uyumluluk için senkron
        SaveAsync(path, project, preserveExistingBackup).GetAwaiter().GetResult();
    }

    public async Task SaveAsync(
        string path,
        AcousticsProject project,
        bool preserveExistingBackup = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(project);

        var issues = AcousticsProjectValidator.Validate(project);
        if (AcousticsProjectValidator.HasErrors(issues))
            throw new InvalidDataException(
                "Proje kaydedilemedi:\n" + string.Join("\n", issues
                    .Where(i => i.Severity == ProjectValidationSeverity.Error)
                    .Select(i => i.Message)));

        var snapshot = CreateSaveSnapshot(project) with
        {
            SemaSurum = AcousticsProjectSchema.CurrentVersion
        };

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(directory);
        var fileName = Path.GetFileName(fullPath);
        var tmpPath = Path.Combine(directory, $".{fileName}.{Guid.NewGuid():N}.tmp");
        var bakPath = fullPath + ".bak";

        try
        {
            await using (var stream = new FileStream(
                tmpPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 128 * 1024,
                options: FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            // Diske yazılan geçici snapshot, hedefe dokunulmadan önce aynı yükleme +
            // migrasyon + doğrulama zincirinden geçsin. Serileştirme/model uyumsuzluğu
            // olursa sağlam primary ve backup yerinde kalır.
            _ = await LoadSingleAsync(tmpPath).ConfigureAwait(false);

            if (File.Exists(fullPath))
            {
                // Yalnız LoadWithStatusAsync tarafından bildirilen recovery akışı bu
                // seçeneği verir. Normal Save/Save As her tür mevcut hedefi (gelecek
                // şema veya elle düzenlenmiş dosya dahil) .bak'a alır; recovery save
                // ise bozuk primary'yi sağlam yedeğin üzerine yazmaz.
                var keepBackup = preserveExistingBackup && File.Exists(bakPath);
                PromoteSnapshot(tmpPath, fullPath, bakPath, keepBackup);
            }
            else
            {
                File.Move(tmpPath, fullPath);
            }
        }
        finally
        {
            if (File.Exists(tmpPath))
            {
                try { File.Delete(tmpPath); } catch { /* asıl hatayı gölgeleme */ }
            }
        }
    }

    public AcousticsProject Load(string path)
    {
        // İmza geriye uyumludur; sessiz backup recovery yapmaz. Kurtarma bilgisinin
        // kullanıcıya gösterilebilmesi gereken akışlar LoadWithStatusAsync kullanır.
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return LoadSingleAsync(path).GetAwaiter().GetResult();
    }

    public async Task<AcousticsProject> LoadAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return await LoadSingleAsync(path).ConfigureAwait(false);
    }

    public async Task<ProjectLoadResult> LoadWithStatusAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            return new ProjectLoadResult(
                await LoadSingleAsync(path).ConfigureAwait(false), false, []);
        }
        catch (Exception primaryError) when (
            primaryError is not UnauthorizedAccessException &&
            primaryError is not UnsupportedProjectSchemaException &&
            File.Exists(path + ".bak"))
        {
            try
            {
                var recovered = await LoadSingleAsync(path + ".bak").ConfigureAwait(false);
                return new ProjectLoadResult(recovered, true, [BackupRecoveryWarning]);
            }
            catch
            {
                ExceptionDispatchInfo.Capture(primaryError).Throw();
                throw; // erişilemez; derleyicinin akış analizi için
            }
        }
    }

    private static async Task<AcousticsProject> LoadSingleAsync(string path)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 128 * 1024, options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        var project = await JsonSerializer.DeserializeAsync<AcousticsProject>(stream, JsonOptions)
            .ConfigureAwait(false);
        if (project is null) throw new InvalidDataException("Proje dosyası okunamadı.");
        return AcousticsProjectMigrator.MigrateAndValidate(project);
    }

    private static void PromoteSnapshot(
        string tmpPath,
        string fullPath,
        string bakPath,
        bool preserveExistingBackup)
    {
        try
        {
            // Normal kayıtta önceki hedef .bak'a gider. Açıkça recovery kaydı
            // istendiğinde backup parametresi verilmez ve sağlam .bak korunur.
            File.Replace(
                tmpPath,
                fullPath,
                preserveExistingBackup ? null : bakPath,
                ignoreMetadataErrors: true);
        }
        catch (PlatformNotSupportedException)
        {
            // Geçici dosya hedefle aynı dizinde olduğundan overwrite move aynı
            // dosya sisteminde atomik rename'dir. Platform Replace'i desteklemiyorsa
            // dosya sisteminde atomik rename'dir. Recovery değilse önceki hedefi
            // backup'a kopyala; recovery ise var olan sağlam yedeğe dokunma.
            if (!preserveExistingBackup)
                File.Copy(fullPath, bakPath, overwrite: true);
            File.Move(tmpPath, fullPath, overwrite: true);
        }
    }

    private static AcousticsProject CreateSaveSnapshot(AcousticsProject project) =>
        project with
        {
            Surfaces = project.Surfaces
                .Select(s => s with { Vertices = s.Vertices.ToArray() })
                .ToArray(),
            Sources = project.Sources.ToArray(),
            Receivers = project.Receivers.ToArray(),
            CategoryMaterials = new Dictionary<string, string>(
                project.CategoryMaterials, StringComparer.OrdinalIgnoreCase),
            SurfaceMaterials = new Dictionary<string, string>(
                project.SurfaceMaterials, StringComparer.OrdinalIgnoreCase),
            ImportWarnings = project.ImportWarnings.ToArray(),
            PanelKonfigurasyon = ClonePanelConfiguration(project.PanelKonfigurasyon)
        };

    private static AcousticPanelKonfigurasyon? ClonePanelConfiguration(AcousticPanelKonfigurasyon? source)
    {
        if (source is null) return null;
        return new AcousticPanelKonfigurasyon
        {
            OturumId = source.OturumId,
            SahneOnuYuzeyId = source.SahneOnuYuzeyId,
            PanelEniM = source.PanelEniM,
            PanelEniModu = source.PanelEniModu,
            PanelEnleriM = source.PanelEnleriM?.ToList() ?? [],
            SarkmaCikmaM = source.SarkmaCikmaM,
            AktifTipler = source.AktifTipler?.ToArray() ?? [],
            TavanYuzeyId = source.TavanYuzeyId,
            SolDuvarYuzeyId = source.SolDuvarYuzeyId,
            SagDuvarYuzeyId = source.SagDuvarYuzeyId,
            Paneller = source.Paneller?.Select(panel => new AcousticPanel
            {
                Id = panel.Id,
                Tip = panel.Tip,
                DilimNo = panel.DilimNo,
                PanelEniM = panel.PanelEniM,
                SarkmaCikmaM = panel.SarkmaCikmaM,
                AciDerece = panel.AciDerece,
                MaterialId = panel.MaterialId,
                Vertices = panel.Vertices?.ToList() ?? []
            }).ToList() ?? []
        };
    }
}
