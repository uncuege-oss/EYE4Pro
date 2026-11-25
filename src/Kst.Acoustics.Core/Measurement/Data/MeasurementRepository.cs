using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Kst.Acoustics.Core.Common;

namespace Kst.Acoustics.Core.Measurement.Data;

/// <summary>
/// Ölçüm oturumlarını JSON olarak yöneten depo.
/// </summary>
public static class MeasurementRepository
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        // RoomDimensions (ValueTuple) alanları property değil field'dır; STJ varsayılanı yalnız
        // property serileştirir, IncludeFields olmadan oda boyutları sessizce (0,0,0)'a düşer.
        IncludeFields = true,
        Converters = { new Float2DArrayJsonConverter() }
    };

    public static async Task SaveSessionAsync(MeasurementSession session, string filePath)
    {
        string json = JsonSerializer.Serialize(session, _options);
        await GuvenliDosyaYazmaServisi.YazAsync(filePath, json);
    }

    public static async Task<MeasurementSession?> LoadSessionAsync(string filePath)
    {
        if (!File.Exists(filePath)) return null;
        string json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<MeasurementSession>(json, _options);
    }
}
