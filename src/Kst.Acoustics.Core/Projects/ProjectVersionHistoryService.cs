using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Kst.Acoustics.Core.Projects;

/// <summary>
/// Kullanıcıya gösterilecek otomatik kayıt/sürüm adayı. Snapshot yolu yalnız servis
/// tarafından üretilir ve silme sırasında kök klasör sınırı yeniden doğrulanır.
/// </summary>
public sealed record ProjectVersionInfo(
    string Id,
    string ProjectKey,
    string SnapshotPath,
    DateTime CreatedAtUtc,
    long SizeBytes,
    string Description);

/// <summary>
/// Proje snapshot'larını kullanıcı uygulama verisinde, proje kimliğine ayrılmış bir
/// klasörde saklar. Her snapshot ProjectFileService'in doğrulamalı atomik kayıt
/// zincirinden geçer; bozuk/yarım metadata listelemeyi engellemez.
/// </summary>
public sealed class ProjectVersionHistoryService
{
    public const int DefaultMaximumVersions = 20;

    private const string SnapshotExtension = ".e4p01";
    private const string MetadataExtension = ".meta.json";
    private readonly string _rootDirectory;
    private readonly int _maximumVersions;
    private readonly ProjectFileService _projectFileService;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions MetadataJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ProjectVersionHistoryService(
        string? rootDirectory = null,
        int maximumVersions = DefaultMaximumVersions,
        ProjectFileService? projectFileService = null,
        TimeProvider? timeProvider = null)
    {
        if (maximumVersions < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumVersions), "En az bir sürüm saklanmalıdır.");

        _rootDirectory = Path.GetFullPath(rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EYE4Pro",
            "M01",
            "AutoSave"));
        _maximumVersions = maximumVersions;
        _projectFileService = projectFileService ?? new ProjectFileService();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string RootDirectory => _rootDirectory;
    public int MaximumVersions => _maximumVersions;

    public async Task<ProjectVersionInfo> SaveVersionAsync(
        AcousticsProject project,
        string? projectPath,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        cancellationToken.ThrowIfCancellationRequested();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var projectKey = GetProjectKey(project, projectPath);
            var directory = GetProjectDirectory(projectKey);
            Directory.CreateDirectory(directory);

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var id = $"{now:yyyyMMddTHHmmssfffffffZ}_{Guid.NewGuid():N}";
            var snapshotPath = Path.Combine(directory, id + SnapshotExtension);
            var normalizedDescription = NormalizeDescription(description);

            await _projectFileService.SaveAsync(snapshotPath, project).ConfigureAwait(false);

            var metadata = new VersionMetadata(
                id,
                projectKey,
                now,
                normalizedDescription,
                project.Name,
                NormalizeProjectPath(projectPath));
            await WriteMetadataAtomicallyAsync(
                GetMetadataPath(snapshotPath), metadata, CancellationToken.None).ConfigureAwait(false);

            var info = new ProjectVersionInfo(
                id,
                projectKey,
                snapshotPath,
                now,
                new FileInfo(snapshotPath).Length,
                normalizedDescription);

            // Snapshot yazımı başladıktan sonra metadata+prune işlemini tamamlamak
            // transaction bütünlüğü için iptalden daha önemlidir. İptal yalnız işlem
            // başlamadan önce kabul edilir.
            await PruneCoreAsync(directory, CancellationToken.None).ConfigureAwait(false);
            return info;
        }
        catch
        {
            // Metadata yazımı başarısızsa sahipsiz snapshot da bir sonraki listelemede
            // kurtarılabilir; yalnız gerçekten sıfır baytlık/yarım geçici dosyalar
            // ProjectFileService tarafından zaten temizlenir.
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<ProjectVersionInfo>> ListVersionsAsync(
        AcousticsProject project,
        string? projectPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        var projectKey = GetProjectKey(project, projectPath);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ListCoreAsync(GetProjectDirectory(projectKey), projectKey, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<AcousticsProject> RestoreVersionAsync(
        ProjectVersionInfo version,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(version);
        var safePath = EnsureOwnedSnapshotPath(version.SnapshotPath);
        cancellationToken.ThrowIfCancellationRequested();
        return await _projectFileService.LoadAsync(safePath).ConfigureAwait(false);
    }

    public async Task DeleteVersionAsync(
        ProjectVersionInfo version,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(version);
        var safePath = EnsureOwnedSnapshotPath(version.SnapshotPath);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            TryDelete(safePath);
            TryDelete(GetMetadataPath(safePath));
            TryDelete(safePath + ".bak");
        }
        finally
        {
            _gate.Release();
        }
    }

    public string GetProjectKey(AcousticsProject project, string? projectPath)
    {
        ArgumentNullException.ThrowIfNull(project);
        var normalizedPath = NormalizeProjectPath(projectPath);
        var identity = normalizedPath is not null
            ? $"path|{normalizedPath}"
            : $"project|{project.Name.Trim()}|{project.SourceFilePath}|{project.ImportedAtUtc.Ticks}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..24];
    }

    private async Task PruneCoreAsync(string directory, CancellationToken cancellationToken)
    {
        var projectKey = Path.GetFileName(directory);
        var versions = await ListCoreAsync(directory, projectKey, cancellationToken).ConfigureAwait(false);
        foreach (var version in versions.Skip(_maximumVersions))
        {
            cancellationToken.ThrowIfCancellationRequested();
            TryDelete(version.SnapshotPath);
            TryDelete(GetMetadataPath(version.SnapshotPath));
            TryDelete(version.SnapshotPath + ".bak");
        }
    }

    private static async Task<IReadOnlyList<ProjectVersionInfo>> ListCoreAsync(
        string directory,
        string projectKey,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directory)) return [];

        var result = new List<ProjectVersionInfo>();
        foreach (var snapshotPath in Directory.EnumerateFiles(directory, "*" + SnapshotExtension))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = new FileInfo(snapshotPath);
            if (!file.Exists || file.Length == 0) continue;

            VersionMetadata? metadata = null;
            var metadataPath = GetMetadataPath(snapshotPath);
            if (File.Exists(metadataPath))
            {
                try
                {
                    await using var stream = new FileStream(
                        metadataPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                        16 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
                    metadata = await JsonSerializer.DeserializeAsync<VersionMetadata>(
                        stream, MetadataJsonOptions, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
                {
                    // Snapshot okunabilir olabilir. Metadata bozuksa dosya zamanıyla
                    // güvenli bir aday üret ve kullanıcıya açıklamayı kaybetme olarak yansıt.
                }
            }

            var id = metadata?.Id ?? Path.GetFileNameWithoutExtension(snapshotPath);
            var createdAt = metadata?.CreatedAtUtc ?? file.LastWriteTimeUtc;
            var description = NormalizeDescription(metadata?.Description ?? "Kurtarılabilir otomatik kayıt");
            result.Add(new ProjectVersionInfo(
                id, projectKey, snapshotPath, createdAt, file.Length, description));
        }

        return result
            .OrderByDescending(v => v.CreatedAtUtc)
            .ThenByDescending(v => v.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private async Task WriteMetadataAtomicallyAsync(
        string metadataPath,
        VersionMetadata metadata,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(metadataPath)
            ?? throw new InvalidOperationException("Sürüm metadata klasörü belirlenemedi.");
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(metadataPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                16 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                    stream, metadata, MetadataJsonOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, metadataPath, overwrite: true);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private string GetProjectDirectory(string projectKey) =>
        Path.Combine(_rootDirectory, projectKey);

    private string EnsureOwnedSnapshotPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(_rootDirectory, fullPath);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            Path.IsPathRooted(relative) || !fullPath.EndsWith(SnapshotExtension, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Sürüm dosyası servis kök klasörünün dışında.");
        return fullPath;
    }

    private static string GetMetadataPath(string snapshotPath) =>
        snapshotPath + MetadataExtension;

    private static string NormalizeDescription(string? description)
    {
        var value = string.IsNullOrWhiteSpace(description)
            ? "Otomatik kayıt"
            : description.Trim();
        return value.Length <= 240 ? value : value[..240];
    }

    private static string? NormalizeProjectPath(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath)) return null;
        var path = Path.GetFullPath(projectPath);
        return OperatingSystem.IsWindows() ? path.ToUpperInvariant() : path;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Prune/cleanup bir sonraki işlemde yeniden denenebilir; başarılı ana
            // snapshot işlemini ikincil temizlik hatasıyla başarısız göstermeyiz.
        }
    }

    private sealed record VersionMetadata(
        string Id,
        string ProjectKey,
        DateTime CreatedAtUtc,
        string Description,
        string ProjectName,
        string? ProjectPath);
}
