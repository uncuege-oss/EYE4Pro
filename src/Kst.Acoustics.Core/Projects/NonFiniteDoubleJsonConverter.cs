using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kst.Acoustics.Core.Projects;

/// <summary>
/// Akustik sonuçlarda "ölçülemedi" anlamındaki NaN/Infinity değerlerini geçerli
/// JSON <c>null</c> olarak saklar. Okumada null tekrar NaN'e döner. Önceki bir
/// sürümün yazmış olabileceği adlandırılmış kayan nokta dizgileri de kabul edilir.
/// </summary>
internal sealed class NonFiniteDoubleJsonConverter : JsonConverter<double>
{
    public override bool HandleNull => true;

    public override double Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return double.NaN;
        if (reader.TokenType == JsonTokenType.Number)
            return reader.GetDouble();
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString() switch
            {
                "NaN" => double.NaN,
                "Infinity" => double.PositiveInfinity,
                "-Infinity" => double.NegativeInfinity,
                { } text when double.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var value) => value,
                _ => throw new JsonException("Geçersiz kayan nokta değeri.")
            };
        }

        throw new JsonException($"Kayan nokta değeri bekleniyordu; {reader.TokenType} bulundu.");
    }

    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
    {
        if (double.IsFinite(value))
            writer.WriteNumberValue(value);
        else
            writer.WriteNullValue();
    }
}
