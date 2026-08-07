using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.BuildingAcoustics.Database;

public static class AciklikOnayarKutuphanesi
{
    private static List<AciklikOnayar>? _cache;

    private static readonly string _dbPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "BuildingAcoustics", "Database", "aciklik_onayarlar.json");

    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static List<AciklikOnayar> TumOnayarlar => _cache ??= Yukle();

    private static List<AciklikOnayar> Yukle()
    {
        try
        {
            if (!File.Exists(_dbPath))
                return VarsayilanOnayarlar();

            string json = File.ReadAllText(_dbPath);
            var liste = JsonSerializer.Deserialize<List<AciklikOnayar>>(json, _options);
            if (liste == null || liste.Count == 0)
                return VarsayilanOnayarlar();
            return liste;
        }
        catch (Exception)
        {
            return VarsayilanOnayarlar();
        }
    }

    public static void Kaydet(List<AciklikOnayar> onayarlar)
    {
        try
        {
            string? dir = Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(_dbPath, JsonSerializer.Serialize(onayarlar, _options));
            _cache = onayarlar;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Açıklık önayarları kaydedilirken hata: {ex.Message}");
        }
    }

    public static List<AciklikOnayar> VarsayilanOnayarlar() =>
    [
        // Pencereler
        new AciklikOnayar { Ad = "Banyo Penceresi",       Tip = BolmeAcikligiTipi.Pencere, GenislikCm =  60, YukseklikCm =  60 },
        new AciklikOnayar { Ad = "Mutfak Penceresi",      Tip = BolmeAcikligiTipi.Pencere, GenislikCm =  80, YukseklikCm =  60 },
        new AciklikOnayar { Ad = "Küçük Pencere",         Tip = BolmeAcikligiTipi.Pencere, GenislikCm =  80, YukseklikCm = 100 },
        new AciklikOnayar { Ad = "Standart Pencere",      Tip = BolmeAcikligiTipi.Pencere, GenislikCm = 100, YukseklikCm = 120 },
        new AciklikOnayar { Ad = "Geniş Pencere",         Tip = BolmeAcikligiTipi.Pencere, GenislikCm = 120, YukseklikCm = 120 },
        new AciklikOnayar { Ad = "Büyük Pencere",         Tip = BolmeAcikligiTipi.Pencere, GenislikCm = 150, YukseklikCm = 120 },
        new AciklikOnayar { Ad = "Salon Penceresi",       Tip = BolmeAcikligiTipi.Pencere, GenislikCm = 180, YukseklikCm = 150 },
        new AciklikOnayar { Ad = "Balkon Kapısı",         Tip = BolmeAcikligiTipi.Pencere, GenislikCm = 100, YukseklikCm = 220 },
        // Kapılar
        new AciklikOnayar { Ad = "Küçük Kapı",            Tip = BolmeAcikligiTipi.Kapi,    GenislikCm =  70, YukseklikCm = 200 },
        new AciklikOnayar { Ad = "Standart Kapı",         Tip = BolmeAcikligiTipi.Kapi,    GenislikCm =  80, YukseklikCm = 200 },
        new AciklikOnayar { Ad = "Standart Kapı (90)",    Tip = BolmeAcikligiTipi.Kapi,    GenislikCm =  90, YukseklikCm = 200 },
        new AciklikOnayar { Ad = "Yüksek Kapı (205)",     Tip = BolmeAcikligiTipi.Kapi,    GenislikCm =  90, YukseklikCm = 205 },
        new AciklikOnayar { Ad = "Yüksek Kapı (210)",     Tip = BolmeAcikligiTipi.Kapi,    GenislikCm =  90, YukseklikCm = 210 },
        new AciklikOnayar { Ad = "Geniş Kapı",            Tip = BolmeAcikligiTipi.Kapi,    GenislikCm = 100, YukseklikCm = 210 },
        new AciklikOnayar { Ad = "Çift Kanat Kapı",       Tip = BolmeAcikligiTipi.Kapi,    GenislikCm = 120, YukseklikCm = 210 },
        new AciklikOnayar { Ad = "Çift Kanat Geniş Kapı", Tip = BolmeAcikligiTipi.Kapi,    GenislikCm = 140, YukseklikCm = 210 },
    ];
}
