using System;
using System.IO;
using System.Text.Json;

namespace Kst.Acoustics.Core.Electric.CevreAydinlatma;

/// <summary>
/// Çevre aydınlatma projesinin (M15) JSON kaydet/aç servisi. Dosya uzantısı <c>.e4p15</c>.
/// </summary>
public static class CevreAydinlatmaJsonServisi
{
    private static readonly JsonSerializerOptions Ayarlar = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static void Kaydet(CevreAydinlatmaProje proje, string dosyaYolu)
        => File.WriteAllText(dosyaYolu, Serialize(proje));

    public static CevreAydinlatmaProje Ac(string dosyaYolu)
        => Deserialize(File.ReadAllText(dosyaYolu));

    public static string Serialize(CevreAydinlatmaProje proje)
        => JsonSerializer.Serialize(proje, Ayarlar);

    public static CevreAydinlatmaProje Deserialize(string json)
        => JsonSerializer.Deserialize<CevreAydinlatmaProje>(json, Ayarlar)
           ?? throw new InvalidOperationException("Proje dosyası okunamadı.");
}
