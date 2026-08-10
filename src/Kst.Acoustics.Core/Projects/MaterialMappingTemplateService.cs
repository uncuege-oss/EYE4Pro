using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Kst.Acoustics.Core.Projects;

[Flags]
public enum MaterialMappingTarget
{
    Category = 1,
    SurfaceName = 2,
    CategoryAndSurfaceName = Category | SurfaceName
}

public sealed record MaterialMappingRule(
    string Pattern,
    string MaterialId,
    MaterialMappingTarget Target = MaterialMappingTarget.Category,
    int Priority = 0);

public sealed record MaterialMappingTemplate(
    Guid Id,
    string Name,
    IReadOnlyList<MaterialMappingRule> Rules,
    DateTime UpdatedAtUtc,
    bool IsBuiltIn = false)
{
    public override string ToString() => IsBuiltIn ? $"{Name} (yerleşik)" : Name;
}

public sealed record MaterialMappingMatch(
    string Category,
    string MaterialId,
    string Pattern,
    int SurfaceCount);

/// <summary>Kategori/yüzey adlarını kullanıcı tarafından saklanabilen wildcard kurallarıyla eşler.</summary>
public sealed class MaterialMappingTemplateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _path;

    public MaterialMappingTemplateService(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EYE4PRO", "m01-material-mapping-templates.json");
    }

    public static MaterialMappingTemplate BuiltInTemplate { get; } = new(
        Guid.Parse("6d112eb5-2f37-45cf-a74a-841d29b1373e"),
        "Temel akustik eşleme",
        [
            new("*AKUSTIK*TAVAN*", "acoustic_ceiling", Priority: 200),
            new("*ALCIPAN*", "gypsum_board", Priority: 190),
            new("*GYPSUM*", "gypsum_board", Priority: 190),
            new("*SEYIRCI*", "audience_seated", Priority: 180),
            new("*AUDIENCE*", "audience_seated", Priority: 180),
            new("*KOLTUK*", "audience_seated", Priority: 180),
            new("*PENCERE*", "glass", Priority: 170),
            new("*CAM*", "glass", Priority: 160),
            new("*GLASS*", "glass", Priority: 160),
            new("*SAHNE*", "wood_stage", Priority: 150),
            new("*STAGE*", "wood_stage", Priority: 150),
            new("*PARKE*", "wood_parquet", Priority: 140),
            new("*HALI*", "carpet_thick", Priority: 130),
            new("*CARPET*", "carpet_thick", Priority: 130),
            new("*PERDE*", "curtain_heavy", Priority: 120),
            new("*CURTAIN*", "curtain_heavy", Priority: 120),
            new("*TUGLA*", "brick", Priority: 110),
            new("*BRICK*", "brick", Priority: 110),
            new("*BETON*", "concrete_bare", Priority: 100),
            new("*CONCRETE*", "concrete_bare", Priority: 100),
            new("*SIVA*", "plaster", Priority: 90),
            new("*PLASTER*", "plaster", Priority: 90)
        ],
        DateTime.UnixEpoch,
        IsBuiltIn: true);

    public async Task<IReadOnlyList<MaterialMappingTemplate>> LoadAsync(CancellationToken ct = default)
    {
        var result = new List<MaterialMappingTemplate> { BuiltInTemplate };
        if (!File.Exists(_path)) return result;

        try
        {
            await using var stream = new FileStream(
                _path, FileMode.Open, FileAccess.Read, FileShare.Read,
                64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var items = await JsonSerializer.DeserializeAsync<List<MaterialMappingTemplate>>(
                stream, JsonOptions, ct).ConfigureAwait(false) ?? [];
            result.AddRange(items.Where(IsValid).Select(Normalize));
        }
        catch (JsonException)
        {
            // Bozuk kullanıcı şablonu yerleşik eşlemeyi kullanmayı engellemez.
        }
        return result
            .GroupBy(x => x.Id)
            .Select(g => g.First())
            .OrderByDescending(x => x.IsBuiltIn)
            .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public async Task SaveUserTemplatesAsync(
        IEnumerable<MaterialMappingTemplate> templates,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(templates);
        var normalized = templates
            .Where(t => !t.IsBuiltIn && IsValid(t))
            .Select(Normalize)
            .GroupBy(t => t.Id)
            .Select(g => g.Last())
            .OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        var directory = Path.GetDirectoryName(Path.GetFullPath(_path))!;
        Directory.CreateDirectory(directory);
        var tmp = Path.Combine(directory, $".{Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                64 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, normalized, JsonOptions, ct).ConfigureAwait(false);
                await stream.FlushAsync(ct).ConfigureAwait(false);
                stream.Flush(true);
            }
            File.Move(tmp, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tmp))
                try { File.Delete(tmp); } catch { }
        }
    }

    public IReadOnlyList<MaterialMappingMatch> Apply(
        MaterialMappingTemplate template,
        IReadOnlyList<ProjectSurface> surfaces,
        IReadOnlySet<string> availableMaterialIds)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(surfaces);
        ArgumentNullException.ThrowIfNull(availableMaterialIds);

        var rules = template.Rules
            .Where(r => !string.IsNullOrWhiteSpace(r.Pattern) &&
                        !string.IsNullOrWhiteSpace(r.MaterialId) &&
                        availableMaterialIds.Contains(r.MaterialId))
            .OrderByDescending(r => r.Priority)
            .ThenByDescending(r => r.Pattern.Length)
            .ToArray();
        var matches = new List<MaterialMappingMatch>();
        foreach (var categoryGroup in surfaces.GroupBy(s => s.Category, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var rule in rules)
            {
                var categoryMatch = rule.Target.HasFlag(MaterialMappingTarget.Category) &&
                    WildcardMatch(categoryGroup.Key, rule.Pattern);
                var surfaceMatch = rule.Target.HasFlag(MaterialMappingTarget.SurfaceName) &&
                    categoryGroup.Any(s => WildcardMatch(s.Name, rule.Pattern));
                if (!categoryMatch && !surfaceMatch) continue;
                matches.Add(new MaterialMappingMatch(
                    categoryGroup.Key, rule.MaterialId, rule.Pattern, categoryGroup.Count()));
                break;
            }
        }
        return matches;
    }

    public static MaterialMappingTemplate FromExactCategoryAssignments(
        string name,
        IReadOnlyDictionary<string, string> assignments,
        Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Şablon adı boş olamaz.", nameof(name));
        ArgumentNullException.ThrowIfNull(assignments);
        var rules = assignments
            .Where(x => !string.IsNullOrWhiteSpace(x.Key) && !string.IsNullOrWhiteSpace(x.Value))
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select((x, i) => new MaterialMappingRule(x.Key.Trim(), x.Value.Trim(), Priority: 10_000 - i))
            .ToArray();
        if (rules.Length == 0) throw new InvalidDataException("Kaydedilecek materyal eşlemesi bulunamadı.");
        return new MaterialMappingTemplate(id ?? Guid.NewGuid(), name.Trim(), rules, DateTime.UtcNow);
    }

    private static bool WildcardMatch(string input, string pattern)
    {
        var normalizedInput = NormalizeSearch(input);
        var normalizedPattern = NormalizeSearch(pattern);
        var regex = "^" + Regex.Escape(normalizedPattern)
            .Replace("\\*", ".*", StringComparison.Ordinal)
            .Replace("\\?", ".", StringComparison.Ordinal) + "$";
        return Regex.IsMatch(normalizedInput, regex, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
    }

    private static string NormalizeSearch(string value)
    {
        var decomposed = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToUpperInvariant(ch));
        return builder.ToString().Normalize(NormalizationForm.FormC)
            .Replace('İ', 'I').Replace('ı', 'I');
    }

    private static bool IsValid(MaterialMappingTemplate template) =>
        template.Id != Guid.Empty && !string.IsNullOrWhiteSpace(template.Name) &&
        template.Rules is { Count: > 0 } && template.Rules.All(r =>
            !string.IsNullOrWhiteSpace(r.Pattern) && !string.IsNullOrWhiteSpace(r.MaterialId) &&
            r.Target != 0 && (r.Target & ~MaterialMappingTarget.CategoryAndSurfaceName) == 0);

    private static MaterialMappingTemplate Normalize(MaterialMappingTemplate template) => template with
    {
        Name = template.Name.Trim(),
        Rules = template.Rules
            .Select(r => r with { Pattern = r.Pattern.Trim(), MaterialId = r.MaterialId.Trim() })
            .ToArray(),
        IsBuiltIn = false
    };
}
