using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kst.Acoustics.Core.Projects;

/// <summary>
/// Taşınabilir M01 proje paketi. Paket; projenin kendisini, yalnız kullanılan
/// materyal/hoparlör tanımlarını ve erişilebiliyorsa kaynak geometriyi içerir.
/// Manifestteki SHA-256 değerleri paketin açılmadan önce doğrulanmasını sağlar.
/// </summary>
public sealed class ProjectPackageService
{
    public const int CurrentFormatVersion = 1;
    public const string ManifestEntryPath = "manifest.json";
    public const string ProjectEntryPath = "project/project.e4p01";

    private const int MaxEntryCount = 256;
    private const long MaxExpandedBytes = 2L * 1024 * 1024 * 1024;
    private const long MaxManifestBytes = 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters =
        {
            new JsonStringEnumConverter(),
            new DirectivityBalloonConverter()
        }
    };

    private readonly ProjectFileService _projectFileService;

    public ProjectPackageService(ProjectFileService? projectFileService = null) =>
        _projectFileService = projectFileService ?? new ProjectFileService();

    public async Task<ProjectPackageCreateResult> CreateAsync(
        string packagePath,
        AcousticsProject project,
        IEnumerable<AcousticMaterial>? availableMaterials = null,
        IEnumerable<AcousticLoudspeaker>? availableLoudspeakers = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentNullException.ThrowIfNull(project);

        var fullPath = Path.GetFullPath(packagePath);
        var directory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(directory);

        var token = Guid.NewGuid().ToString("N");
        var temporaryPackagePath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{token}.tmp");
        var temporaryProjectPath = Path.Combine(directory, $".project.{token}.e4p01.tmp");
        var entries = new List<ProjectPackageEntry>();
        var sourceIncluded = false;
        var includedMaterialCount = 0;
        var includedLoudspeakerCount = 0;

        try
        {
            await _projectFileService.SaveAsync(temporaryProjectPath, project).ConfigureAwait(false);

            await using (var packageStream = new FileStream(
                temporaryPackagePath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 128 * 1024,
                options: FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                using (var archive = new ZipArchive(packageStream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    entries.Add(await AddFileEntryAsync(
                        archive, ProjectEntryPath, "project", temporaryProjectPath, cancellationToken)
                        .ConfigureAwait(false));

                    var referencedMaterialIds = project.CategoryMaterials.Values
                        .Concat(project.SurfaceMaterials.Values)
                        .Concat(project.PanelKonfigurasyon?.Paneller?
                            .Select(panel => panel.MaterialId) ?? [])
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    var materials = (availableMaterials ?? [])
                        .Where(material => referencedMaterialIds.Contains(material.Id))
                        .GroupBy(material => material.Id, StringComparer.OrdinalIgnoreCase)
                        .Select(group => group.First())
                        .OrderBy(material => material.Id, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    includedMaterialCount = materials.Length;
                    entries.Add(await AddJsonEntryAsync(
                        archive, "libraries/materials.json", "material-library", materials, cancellationToken)
                        .ConfigureAwait(false));

                    var referencedSpeakerIds = project.Sources
                        .Select(source => source.LoudspeakerId)
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Select(id => id!)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var loudspeakers = (availableLoudspeakers ?? [])
                        .Where(speaker => referencedSpeakerIds.Contains(speaker.Id))
                        .GroupBy(speaker => speaker.Id, StringComparer.OrdinalIgnoreCase)
                        .Select(group => group.First())
                        .OrderBy(speaker => speaker.Id, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    includedLoudspeakerCount = loudspeakers.Length;
                    entries.Add(await AddJsonEntryAsync(
                        archive, "libraries/loudspeakers.json", "loudspeaker-library", loudspeakers, cancellationToken)
                        .ConfigureAwait(false));

                    if (!string.IsNullOrWhiteSpace(project.SourceFilePath) &&
                        File.Exists(project.SourceFilePath) &&
                        !PathsEqual(project.SourceFilePath, fullPath))
                    {
                        var sourceName = MakeSafeEntryFileName(Path.GetFileName(project.SourceFilePath));
                        entries.Add(await AddFileEntryAsync(
                            archive, $"source/{sourceName}", "source-geometry", project.SourceFilePath,
                            cancellationToken).ConfigureAwait(false));
                        sourceIncluded = true;
                    }

                    var readme = BuildReadme(project, materials.Length, loudspeakers.Length, sourceIncluded);
                    entries.Add(await AddBytesEntryAsync(
                        archive, "README.txt", "documentation", Encoding.UTF8.GetBytes(readme), cancellationToken)
                        .ConfigureAwait(false));

                    var manifest = new ProjectPackageManifest(
                        CurrentFormatVersion,
                        "EYE4PRO M01",
                        project.Name,
                        DateTime.UtcNow,
                        ProjectEntryPath,
                        entries.ToArray());
                    _ = await AddJsonEntryAsync(
                        archive, ManifestEntryPath, "manifest", manifest, cancellationToken)
                        .ConfigureAwait(false);
                }

                await packageStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                packageStream.Flush(flushToDisk: true);
            }

            // Hedefe dokunmadan önce yeni paketi tamamen doğrula.
            var validation = await ValidateAsync(temporaryPackagePath, cancellationToken).ConfigureAwait(false);
            if (!validation.IsValid)
                throw new InvalidDataException("Oluşturulan proje paketi doğrulanamadı: " + validation.Message);

            PromoteAtomically(temporaryPackagePath, fullPath);
            return new ProjectPackageCreateResult(
                fullPath,
                entries.Count,
                new FileInfo(fullPath).Length,
                sourceIncluded,
                includedMaterialCount,
                includedLoudspeakerCount);
        }
        finally
        {
            TryDelete(temporaryProjectPath);
            TryDelete(temporaryPackagePath);
        }
    }

    public async Task<ProjectPackageValidationResult> ValidateAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);

        try
        {
            await using var stream = new FileStream(
                packagePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 128 * 1024,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            if (archive.Entries.Count == 0 || archive.Entries.Count > MaxEntryCount)
                return ProjectPackageValidationResult.Invalid("Paket girdi sayısı geçersiz.");

            var names = new HashSet<string>(StringComparer.Ordinal);
            long expandedBytes = 0;
            foreach (var entry in archive.Entries)
            {
                if (!IsSafeEntryPath(entry.FullName) || !names.Add(entry.FullName))
                    return ProjectPackageValidationResult.Invalid("Paket güvenli olmayan veya yinelenen bir yol içeriyor.");
                expandedBytes = checked(expandedBytes + entry.Length);
                if (expandedBytes > MaxExpandedBytes)
                    return ProjectPackageValidationResult.Invalid("Paket açılmış boyut sınırını aşıyor.");
            }

            var manifestEntry = archive.GetEntry(ManifestEntryPath);
            if (manifestEntry is null || manifestEntry.Length <= 0 || manifestEntry.Length > MaxManifestBytes)
                return ProjectPackageValidationResult.Invalid("Paket manifesti eksik veya geçersiz.");

            ProjectPackageManifest? manifest;
            await using (var manifestStream = manifestEntry.Open())
            {
                manifest = await JsonSerializer.DeserializeAsync<ProjectPackageManifest>(
                    manifestStream, JsonOptions, cancellationToken).ConfigureAwait(false);
            }

            if (manifest is null || manifest.FormatVersion != CurrentFormatVersion)
                return ProjectPackageValidationResult.Invalid("Paket sürümü desteklenmiyor.");
            if (!IsSafeEntryPath(manifest.ProjectEntryPath) ||
                !string.Equals(manifest.ProjectEntryPath, ProjectEntryPath, StringComparison.Ordinal))
                return ProjectPackageValidationResult.Invalid("Paket proje yolu geçersiz.");
            if (manifest.Entries is null || manifest.Entries.Count == 0 ||
                manifest.Entries.Count != archive.Entries.Count - 1)
                return ProjectPackageValidationResult.Invalid("Manifest girdi listesi paketle eşleşmiyor.");

            var manifestNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var expected in manifest.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsSafeEntryPath(expected.Path) || !manifestNames.Add(expected.Path) ||
                    string.Equals(expected.Path, ManifestEntryPath, StringComparison.Ordinal))
                    return ProjectPackageValidationResult.Invalid("Manifestte geçersiz veya yinelenen bir yol var.");

                var actual = archive.GetEntry(expected.Path);
                if (actual is null || actual.Length != expected.Size)
                    return ProjectPackageValidationResult.Invalid(
                        $"Paket girdisi bütünlük denetimini geçemedi; girdi eksik veya boyutu değişmiş: {expected.Path}");

                await using var actualStream = actual.Open();
                var actualHash = await ComputeSha256Async(actualStream, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(actualHash, expected.Sha256, StringComparison.OrdinalIgnoreCase))
                    return ProjectPackageValidationResult.Invalid($"Paket girdisi bütünlük denetimini geçemedi: {expected.Path}");
            }

            if (!manifestNames.Contains(ProjectEntryPath))
                return ProjectPackageValidationResult.Invalid("Paket proje dosyasını içermiyor.");

            return ProjectPackageValidationResult.Valid(manifest);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or JsonException or OverflowException)
        {
            return ProjectPackageValidationResult.Invalid($"Paket okunamadı: {ex.Message}");
        }
    }

    public async Task<ProjectPackageLoadResult> LoadProjectAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(packagePath, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid || validation.Manifest is null)
            throw new InvalidDataException(validation.Message);

        var temporaryProjectPath = Path.Combine(
            Path.GetTempPath(), $"eye4pro-package-{Guid.NewGuid():N}.e4p01");
        IReadOnlyList<AcousticMaterial> materials = [];
        IReadOnlyList<AcousticLoudspeaker> loudspeakers = [];
        try
        {
            await using (var stream = new FileStream(
                packagePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 128 * 1024,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false))
            {
                var entry = archive.GetEntry(validation.Manifest.ProjectEntryPath)
                    ?? throw new InvalidDataException("Paket proje dosyasını içermiyor.");
                await using (var input = entry.Open())
                await using (var output = new FileStream(
                    temporaryProjectPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    bufferSize: 128 * 1024, options: FileOptions.Asynchronous))
                {
                    await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                    await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                materials = await ReadOptionalJsonEntryAsync<AcousticMaterial[]>(
                    archive, "libraries/materials.json", cancellationToken).ConfigureAwait(false) ?? [];
                loudspeakers = await ReadOptionalJsonEntryAsync<AcousticLoudspeaker[]>(
                    archive, "libraries/loudspeakers.json", cancellationToken).ConfigureAwait(false) ?? [];
            }

            var project = await _projectFileService.LoadAsync(temporaryProjectPath).ConfigureAwait(false);
            return new ProjectPackageLoadResult(project, validation.Manifest, materials, loudspeakers);
        }
        finally
        {
            TryDelete(temporaryProjectPath);
        }
    }

    private static async Task<ProjectPackageEntry> AddJsonEntryAsync<T>(
        ZipArchive archive,
        string path,
        string kind,
        T value,
        CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        return await AddBytesEntryAsync(archive, path, kind, bytes, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ProjectPackageEntry> AddBytesEntryAsync(
        ZipArchive archive,
        string path,
        string kind,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        await using var output = entry.Open();
        await output.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        return new ProjectPackageEntry(path, kind, bytes.LongLength, Convert.ToHexString(SHA256.HashData(bytes)));
    }

    private static async Task<ProjectPackageEntry> AddFileEntryAsync(
        ZipArchive archive,
        string path,
        string kind,
        string sourcePath,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        await using var input = new FileStream(
            sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 128 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var output = entry.Open();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[128 * 1024];
        long size = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            hash.AppendData(buffer, 0, read);
            size += read;
        }

        return new ProjectPackageEntry(path, kind, size, Convert.ToHexString(hash.GetHashAndReset()));
    }

    private static async Task<string> ComputeSha256Async(Stream input, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[128 * 1024];
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            hash.AppendData(buffer, 0, read);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static async Task<T?> ReadOptionalJsonEntryAsync<T>(
        ZipArchive archive,
        string path,
        CancellationToken cancellationToken)
    {
        var entry = archive.GetEntry(path);
        if (entry is null) return default;
        await using var input = entry.Open();
        return await JsonSerializer.DeserializeAsync<T>(input, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string BuildReadme(
        AcousticsProject project,
        int materialCount,
        int loudspeakerCount,
        bool sourceIncluded) =>
        $"""
        EYE4PRO M01 AKUSTİK PROJE PAKETİ
        Proje: {project.Name}
        Oluşturma (UTC): {DateTime.UtcNow:O}

        İçerik:
        - project/project.e4p01: açılabilir M01 proje dosyası
        - libraries/materials.json: kullanılan {materialCount} materyal tanımı
        - libraries/loudspeakers.json: kullanılan {loudspeakerCount} hoparlör tanımı
        - Kaynak geometri: {(sourceIncluded ? "pakete eklendi" : "mevcut değil veya erişilemedi")}
        - manifest.json: dosya listesi ve SHA-256 bütünlük değerleri

        Bu paket ZIP tabanlı .e4pkg biçimindedir. İçeriği elle değiştirilirse
        EYE4PRO bütünlük denetimi paketi geçersiz sayar.
        """;

    private static string MakeSafeEntryFileName(string fileName)
    {
        var cleaned = new string(fileName
            .Where(character => character is not '/' and not '\\' && !char.IsControl(character))
            .ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "source.bin" : cleaned;
    }

    private static bool IsSafeEntryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('/') || path.Contains('\\') ||
            Path.IsPathRooted(path))
            return false;
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 && parts.All(part => part is not "." and not "..");
    }

    private static void PromoteAtomically(string temporaryPath, string targetPath)
    {
        if (!File.Exists(targetPath))
        {
            File.Move(temporaryPath, targetPath);
            return;
        }

        try
        {
            File.Replace(temporaryPath, targetPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        catch (PlatformNotSupportedException)
        {
            File.Move(temporaryPath, targetPath, overwrite: true);
        }
    }

    private static bool PathsEqual(string left, string right) => string.Equals(
        Path.GetFullPath(left),
        Path.GetFullPath(right),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static void TryDelete(string path)
    {
        if (!File.Exists(path)) return;
        try { File.Delete(path); } catch { /* asıl hatayı gölgeleme */ }
    }
}

public sealed record ProjectPackageEntry(string Path, string Kind, long Size, string Sha256);

public sealed record ProjectPackageManifest(
    int FormatVersion,
    string Application,
    string ProjectName,
    DateTime CreatedAtUtc,
    string ProjectEntryPath,
    IReadOnlyList<ProjectPackageEntry> Entries);

public sealed record ProjectPackageCreateResult(
    string PackagePath,
    int EntryCount,
    long PackageSizeBytes,
    bool SourceIncluded,
    int IncludedMaterialCount,
    int IncludedLoudspeakerCount);

public sealed record ProjectPackageValidationResult(
    bool IsValid,
    string Message,
    ProjectPackageManifest? Manifest)
{
    public static ProjectPackageValidationResult Valid(ProjectPackageManifest manifest) =>
        new(true, "Paket geçerli.", manifest);

    public static ProjectPackageValidationResult Invalid(string message) =>
        new(false, message, null);
}

public sealed record ProjectPackageLoadResult(
    AcousticsProject Project,
    ProjectPackageManifest Manifest,
    IReadOnlyList<AcousticMaterial> Materials,
    IReadOnlyList<AcousticLoudspeaker> Loudspeakers);
