using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia.Controls;

namespace Kst.Acoustics.App.Electric.KesifHazirla;

/// <summary>
/// M14 DataGrid sütun genişliklerini kullanıcı bazında (AppData) hatırlar.
/// Pencere kapanırken kaydeder, açılırken uygular. Her grid bir anahtarla saklanır;
/// sütunlar sıra (index) ile eşlenir.
/// </summary>
public static class KesifKolonGenislikDeposu
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string DosyaYolu = HesaplaYol();

    // grid anahtarı -> sütun genişlikleri (px)
    private static Dictionary<string, List<double>> _veri = Yukle();

    private static string HesaplaYol()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Eye4Pro", "Electric", "KesifHazirla");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "KolonGenislikleri.json");
    }

    private static Dictionary<string, List<double>> Yukle()
    {
        try
        {
            if (File.Exists(DosyaYolu))
            {
                var json = File.ReadAllText(DosyaYolu);
                var veri = JsonSerializer.Deserialize<Dictionary<string, List<double>>>(json, JsonOptions);
                if (veri is not null)
                    return veri;
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // bozuk dosya → boş başla
        }
        return new();
    }

    /// <summary>Kayıtlı genişlikleri grid sütunlarına uygular (varsa).</summary>
    public static void Uygula(string gridAnahtari, DataGrid grid)
    {
        if (!_veri.TryGetValue(gridAnahtari, out var genislikler))
            return;

        var kolonlar = grid.Columns;
        for (var i = 0; i < kolonlar.Count && i < genislikler.Count; i++)
        {
            if (genislikler[i] > 10)
                kolonlar[i].Width = new DataGridLength(genislikler[i]);
        }
    }

    /// <summary>Grid sütunlarının güncel genişliklerini saklar ve diske yazar.</summary>
    public static void Kaydet(string gridAnahtari, DataGrid grid)
    {
        var genislikler = grid.Columns
            .Select(c => c.ActualWidth > 0 ? c.ActualWidth : c.Width.Value)
            .ToList();

        if (genislikler.Count == 0 || genislikler.All(g => double.IsNaN(g) || g <= 0))
            return;

        _veri[gridAnahtari] = genislikler.Select(g => double.IsNaN(g) ? 0 : g).ToList();
        try
        {
            File.WriteAllText(DosyaYolu, JsonSerializer.Serialize(_veri, JsonOptions));
        }
        catch (IOException)
        {
            // yazılamadıysa sessiz geç (kalıcılık kritik değil)
        }
    }
}
