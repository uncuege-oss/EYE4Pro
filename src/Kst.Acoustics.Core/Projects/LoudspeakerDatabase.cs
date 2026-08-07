using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kst.Acoustics.Core.Projects;

/// <summary>
/// Kullanıcı tanımlı veya içe aktarılmış hoparlör modellerini (CLF)
/// kalıcı JSON dosyasında saklar. Singleton yapısındadır.
/// </summary>
public sealed class LoudspeakerDatabase
{
    public static readonly LoudspeakerDatabase Instance = new();

    private static readonly string DbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EYE4PRO", "loudspeakers.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new DirectivityBalloonConverter() }
    };

    private readonly List<AcousticLoudspeaker> _items = [];

    private LoudspeakerDatabase() => Load();

    // ── Erişim ────────────────────────────────────────────────────────────────

    public IReadOnlyList<AcousticLoudspeaker> All => _items;

    // ── CRUD ──────────────────────────────────────────────────────────────────

    public bool Add(AcousticLoudspeaker speaker)
    {
        if (_items.Any(s => string.Equals(s.Id, speaker.Id, StringComparison.OrdinalIgnoreCase)))
            return false;

        _items.Add(speaker);
        Save();
        return true;
    }

    public bool Update(AcousticLoudspeaker updated)
    {
        var idx = _items.FindIndex(s =>
            string.Equals(s.Id, updated.Id, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return false;

        _items[idx] = updated;
        Save();
        return true;
    }

    public bool Remove(string id)
    {
        var removed = _items.RemoveAll(s =>
            string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase)) > 0;
        if (removed) Save();
        return removed;
    }

    public AcousticLoudspeaker? FindById(string id) =>
        _items.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

    // ── Kalıcı depolama ───────────────────────────────────────────────────────

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(DbPath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(_items, JsonOpts);
            File.WriteAllText(DbPath, json);
        }
        catch { /* Sessiz hata — uygulama çalışmaya devam eder */ }
    }

    private void Load()
    {
        try
        {
            if (File.Exists(DbPath))
            {
                var json = File.ReadAllText(DbPath);
                var items = JsonSerializer.Deserialize<List<AcousticLoudspeaker>>(json, JsonOpts);
                if (items is not null && items.Count > 0)
                {
                    _items.AddRange(items);
                    return; // Dosyadan başarıyla yüklendi
                }
            }
            
            // Dosya yoksa veya boşsa varsayılanları yükle
            _items.AddRange(LoudspeakerDefaults.GetDefaults());
            Save();
        }
        catch 
        { 
            // Bozuk dosya veya hata durumunda varsayılanlarla başla
            _items.Clear();
            _items.AddRange(LoudspeakerDefaults.GetDefaults());
            Save();
        }
    }
}

/// <summary>
/// DirectivityBalloon sınıfını JSON'a 2 boyutlu yerine 1 boyutlu (jagged) dizi olarak serileştirir.
/// </summary>
public class DirectivityBalloonConverter : JsonConverter<DirectivityBalloon>
{
    private class BalloonDto
    {
        public int Resolution { get; set; }
        public double[][] Data { get; set; } = [];
    }

    public override DirectivityBalloon Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dto = JsonSerializer.Deserialize<BalloonDto>(ref reader, options);
        if (dto == null || dto.Data.Length == 0) return new DirectivityBalloon(10, new double[0,0]);

        int rows = dto.Data.Length;
        int cols = dto.Data[0].Length;
        var array2D = new double[rows, cols];

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                if (j < dto.Data[i].Length)
                    array2D[i, j] = dto.Data[i][j];
            }
        }

        return new DirectivityBalloon(dto.Resolution, array2D);
    }

    public override void Write(Utf8JsonWriter writer, DirectivityBalloon value, JsonSerializerOptions options)
    {
        int rows = value.AttenuationData.GetLength(0);
        int cols = value.AttenuationData.GetLength(1);
        var jagged = new double[rows][];

        for (int i = 0; i < rows; i++)
        {
            jagged[i] = new double[cols];
            for (int j = 0; j < cols; j++)
            {
                jagged[i][j] = value.AttenuationData[i, j];
            }
        }

        var dto = new BalloonDto { Resolution = value.Resolution, Data = jagged };
        JsonSerializer.Serialize(writer, dto, options);
    }
}
