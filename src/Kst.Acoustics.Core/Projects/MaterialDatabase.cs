using System.Text.Json;
using System.Timers;
using System.Diagnostics;

namespace Kst.Acoustics.Core.Projects;

/// <summary>
/// Yerleşik preset'lere ek olarak kullanıcı tanımlı materyalleri
/// kalıcı JSON dosyasında saklar. Singleton.
/// </summary>
public sealed class MaterialDatabase
{
    public static readonly MaterialDatabase Instance = new();
    public event Action? Changed;

    private static readonly string DbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EYE4PRO", "materials.json");

    private static readonly string FavoritesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EYE4PRO", "material_favorites.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly List<AcousticMaterial> _custom = [];
    private readonly HashSet<string> _favorites = new(StringComparer.OrdinalIgnoreCase);
    private readonly FileSystemWatcher? _watcher;
    private readonly System.Timers.Timer _debounceTimer;

    private MaterialDatabase()
    {
        Load();
        LoadFavorites();

        _debounceTimer = new System.Timers.Timer(500) { AutoReset = false };
        _debounceTimer.Elapsed += (_, _) =>
        {
            Debug.WriteLine("MaterialDatabase: Debounce timer elapsed, triggering Reload().");
            // Reload takes place in a background thread now. Event subscribers (UI) should use Dispatcher!
            Reload();
        };

        try
        {
            var dir = Path.GetDirectoryName(DbPath);
            if (dir != null && Directory.Exists(dir))
            {
                _watcher = new FileSystemWatcher(dir, "*.json") { EnableRaisingEvents = true };
                _watcher.Changed += (_, e) => 
                { 
                    if (e.FullPath == DbPath || e.FullPath == FavoritesPath) 
                    {
                        Debug.WriteLine($"MaterialDatabase: File changed detected: {e.FullPath}. Debouncing...");
                        _debounceTimer.Stop();
                        _debounceTimer.Start();
                    }
                };
            }
        }
        catch (Exception ex)
        { 
            Debug.WriteLine($"MaterialDatabase: Watcher init failed: {ex.Message}");
        }
    }

    public void Reload()
    {
        _custom.Clear();
        _favorites.Clear();
        Load();
        LoadFavorites();
        Changed?.Invoke();
    }

    // ── Erişim ────────────────────────────────────────────────────────────────

    public IReadOnlyList<AcousticMaterial> BuiltIn => MaterialLibrary.Presets;
    public IReadOnlyList<AcousticMaterial> Custom  => _custom;

    /// <summary>Yerleşik + özel tüm materyaller.</summary>
    public IReadOnlyList<AcousticMaterial> All =>
        [.. MaterialLibrary.Presets, .. _custom];

    public IReadOnlyList<AcousticMaterial> Favorites
    {
        get
        {
            var items = All.Where(m => _favorites.Contains(m.Id)).ToArray();
            return items.Length > 0 ? items : [MaterialLibrary.Default];
        }
    }

    public bool IsFavorite(string id) => _favorites.Contains(id);

    public void SetFavorite(string id, bool isFavorite)
    {
        if (isFavorite) _favorites.Add(id);
        else _favorites.Remove(id);
        SaveFavorites();
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    public bool Add(AcousticMaterial material)
    {
        if (All.Any(m => string.Equals(m.Id, material.Id, StringComparison.OrdinalIgnoreCase)))
            return false;

        _custom.Add(material);
        Save();
        Changed?.Invoke();
        return true;
    }

    public bool Update(AcousticMaterial updated)
    {
        var idx = _custom.FindIndex(m =>
            string.Equals(m.Id, updated.Id, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return false;

        _custom[idx] = updated;
        Save();
        Changed?.Invoke();
        return true;
    }

    public bool Remove(string id)
    {
        var removed = _custom.RemoveAll(m =>
            string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase)) > 0;
        if (removed) { Save(); Changed?.Invoke(); }
        return removed;
    }

    public AcousticMaterial? FindById(string id) =>
        All.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));

    // ── Kalıcı depolama ───────────────────────────────────────────────────────

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(DbPath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(_custom, JsonOpts);
            File.WriteAllText(DbPath, json);
        }
        catch { /* Sessiz hata — uygulama çalışmaya devam eder */ }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(DbPath)) return;
            var json = File.ReadAllText(DbPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return;

            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var mat = ParseMaterialElement(el);
                if (mat is not null) _custom.Add(mat);
            }
        }
        catch { /* Bozuk dosya — boş başla */ }
    }

    private void LoadFavorites()
    {
        try
        {
            if (!File.Exists(FavoritesPath))
            {
                foreach (var material in MaterialLibrary.Presets.Where(m => !m.Id.StartsWith("master_", StringComparison.OrdinalIgnoreCase)))
                    _favorites.Add(material.Id);
                SaveFavorites();
                return;
            }

            var json = File.ReadAllText(FavoritesPath);
            var ids = JsonSerializer.Deserialize<List<string>>(json, JsonOpts) ?? [];
            foreach (var id in ids.Where(id => !string.IsNullOrWhiteSpace(id)))
                _favorites.Add(id);
        }
        catch
        {
            _favorites.Add(MaterialLibrary.Default.Id);
        }
    }

    private void SaveFavorites()
    {
        try
        {
            var dir = Path.GetDirectoryName(FavoritesPath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(_favorites.Order(StringComparer.OrdinalIgnoreCase).ToArray(), JsonOpts);
            File.WriteAllText(FavoritesPath, json);
        }
        catch { /* Sessiz hata — uygulama çalışmaya devam eder */ }
    }

    /// <summary>
    /// Hem eski (6 bantlı Alpha + sayısal Scattering) hem yeni
    /// (8 bantlı Alpha + dizi Scattering) JSON formatını okur.
    /// </summary>
    private static AcousticMaterial? ParseMaterialElement(JsonElement el)
    {
        if (!el.TryGetProperty("id", out var idEl) ||
            !el.TryGetProperty("displayName", out var nameEl))
            return null;

        var id   = idEl.GetString();
        var name = nameEl.GetString();
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
            return null;

        // Alpha — eski format 6 eleman, yeni format 8 eleman
        double[] alpha;
        if (el.TryGetProperty("alpha", out var alphaEl) &&
            alphaEl.ValueKind == JsonValueKind.Array)
        {
            var vals = alphaEl.EnumerateArray()
                .Select(v => v.TryGetDouble(out var d) ? Math.Clamp(d, 0, 1) : 0.0)
                .ToArray();

            if (vals.Length == 6)
            {
                // Eski format: [125..4k] → 8 banda genişlet [63, 125..4k, 8k]
                // Eski format: [125, 250, 500, 1k, 2k, 4k] -> 8 bant
                // 63 Hz = 125 Hz değerinin %90'ı (muhafazakar tahmin)
                // 8 kHz = 4 kHz değerinin %85'i (muhafazakar tahmin)
                alpha = [
                    Math.Clamp(vals[0] * 0.9, 0, 1),
                    vals[0], vals[1], vals[2], vals[3], vals[4], vals[5],
                    Math.Clamp(vals[5] * 0.85, 0, 1)
                ];
            }
            else if (vals.Length >= 8)
            {
                alpha = vals[..8];
            }
            else return null;
        }
        else return null;

        // Scattering — eski format: sayı, yeni format: dizi
        double[] scattering;
        if (el.TryGetProperty("scattering", out var scEl))
        {
            if (scEl.ValueKind == JsonValueKind.Array)
            {
                var vals = scEl.EnumerateArray()
                    .Select(v => v.TryGetDouble(out var d) ? Math.Clamp(d, 0, 1) : 0.05)
                    .ToArray();
                scattering = vals.Length >= 8 ? vals[..8]
                    : Enumerable.Repeat(vals.Length > 0 ? vals[0] : 0.05, 8).ToArray();
            }
            else if (scEl.TryGetDouble(out var s))
            {
                scattering = Enumerable.Repeat(Math.Clamp(s, 0, 1), 8).ToArray();
            }
            else
            {
                scattering = Enumerable.Repeat(0.05, 8).ToArray();
            }
        }
        else
        {
            scattering = Enumerable.Repeat(0.05, 8).ToArray();
        }

        var category = el.TryGetProperty("category", out var catEl) ? catEl.GetString() ?? "" : "";
        var note = el.TryGetProperty("note", out var noteEl) ? noteEl.GetString() ?? "" : "";
        var reference = el.TryGetProperty("reference", out var refEl) ? refEl.GetString() ?? "" : "";
        var scatteringDataType = el.TryGetProperty("scatteringDataType", out var sdtEl) ? sdtEl.GetString() ?? "" : "";

        return new AcousticMaterial(id, name, alpha, scattering, category, note, reference, scatteringDataType);
    }

    // ── Excel toplu ekleme ────────────────────────────────────────────────────

    /// <summary>Excel'den içe aktarılan materyalleri ekler/günceller. (added, updated) sayısı döner.</summary>
    public (int Added, int Updated) Merge(IEnumerable<AcousticMaterial> materials)
    {
        int added = 0, updated = 0;
        foreach (var m in materials)
        {
            var existing = _custom.FindIndex(x =>
                string.Equals(x.Id, m.Id, StringComparison.OrdinalIgnoreCase));

            if (existing >= 0) { _custom[existing] = m; updated++; }
            else if (!MaterialLibrary.Presets.Any(p =>
                string.Equals(p.Id, m.Id, StringComparison.OrdinalIgnoreCase)))
            { _custom.Add(m); added++; }
        }
        if (added > 0 || updated > 0) { Save(); Changed?.Invoke(); }
        return (added, updated);
    }
}
