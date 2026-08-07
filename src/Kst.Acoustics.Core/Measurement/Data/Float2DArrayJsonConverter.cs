using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kst.Acoustics.Core.Measurement.Data;

/// <summary>
/// System.Text.Json çok boyutlu diziyi (float[,]) desteklemez; satır listesi (jagged) olarak yazar/okur.
/// </summary>
public sealed class Float2DArrayJsonConverter : JsonConverter<float[,]>
{
    public override float[,] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var rows = JsonSerializer.Deserialize<float[][]>(ref reader, options) ?? [];
        if (rows.Length == 0) return new float[0, 0];

        int cols = rows[0].Length;
        var result = new float[rows.Length, cols];
        for (int r = 0; r < rows.Length; r++)
        {
            var row = rows[r];
            int c = Math.Min(cols, row.Length);
            for (int i = 0; i < c; i++)
                result[r, i] = row[i];
        }
        return result;
    }

    public override void Write(Utf8JsonWriter writer, float[,] value, JsonSerializerOptions options)
    {
        int rows = value.GetLength(0);
        int cols = value.GetLength(1);
        writer.WriteStartArray();
        for (int r = 0; r < rows; r++)
        {
            writer.WriteStartArray();
            for (int c = 0; c < cols; c++)
                writer.WriteNumberValue(value[r, c]);
            writer.WriteEndArray();
        }
        writer.WriteEndArray();
    }
}
