using System;
using System.Collections.Generic;
using System.Text.Json;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Common.Models;

/// DXF floor plans'ı parse edip Common format'a çeviren + serialization servisi
public static class OrtakKatPlaniServisi
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// .e4kp dosyasına kaydet (OrtakKatPlani → JSON)
    public static void Kaydet(OrtakKatPlani plan, string e4kpYolu)
    {
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        if (string.IsNullOrWhiteSpace(e4kpYolu)) throw new ArgumentException("Path cannot be empty", nameof(e4kpYolu));

        try
        {
            var json = JsonSerializer.Serialize(plan, JsonOptions);
            File.WriteAllText(e4kpYolu, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"OrtakKatPlani kayıt başarısız: {e4kpYolu}", ex);
        }
    }

    /// .e4kp dosyasından yükle (JSON → OrtakKatPlani)
    public static OrtakKatPlani Yukle(string e4kpYolu)
    {
        if (string.IsNullOrWhiteSpace(e4kpYolu)) throw new ArgumentException("Path cannot be empty", nameof(e4kpYolu));
        if (!File.Exists(e4kpYolu)) throw new FileNotFoundException($"Floor plan file not found: {e4kpYolu}");

        try
        {
            var json = File.ReadAllText(e4kpYolu);
            var plan = JsonSerializer.Deserialize<OrtakKatPlani>(json, JsonOptions);
            return plan ?? throw new InvalidOperationException("Deserialization returned null");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"OrtakKatPlani yükleme başarısız (JSON): {e4kpYolu}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"OrtakKatPlani yükleme başarısız: {e4kpYolu}", ex);
        }
    }
}
