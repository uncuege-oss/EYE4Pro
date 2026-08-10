using System.Text.Json;
using Kst.Acoustics.Core.Common;
using Kst.Acoustics.Core.Salon.Models;

namespace Kst.Acoustics.Core.Salon.Services;

public sealed class SalonEditorAyarlari
{
    public bool SnapAcik { get; set; } = true;
    public bool GridAcik { get; set; }
    public bool OrtoAcik { get; set; }
    public bool PolarAcik { get; set; }
    public double GridAraligiM { get; set; } = 0.50;
    public double PolarAciDerece { get; set; } = 15;
    public SalonKatmanTipi SonKatmanTipi { get; set; } = SalonKatmanTipi.Zemin;
    public int OtomatikKayitDakika { get; set; } = 3;
    public double SolPanelGenisligi { get; set; } = 320;
    public string SonKlasor { get; set; } = "";
    public bool KoltuklariGoster { get; set; } = true;
    public bool KaliteIsaretleriniGoster { get; set; } = true;
}

/// <summary>Kullanıcının M51 görünüm/snap/otomatik kayıt tercihlerini hatırlar.</summary>
public static class SalonEditorAyarServisi
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string VarsayilanYol => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EYE4Pro", "M51", "editor-settings.json");

    public static SalonEditorAyarlari Yukle(string? yol = null)
    {
        yol ??= VarsayilanYol;
        if (!File.Exists(yol)) return new SalonEditorAyarlari();
        try
        {
            var a = JsonSerializer.Deserialize<SalonEditorAyarlari>(File.ReadAllText(yol), Json) ?? new();
            Normalize(a);
            return a;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new SalonEditorAyarlari();
        }
    }

    public static void Kaydet(SalonEditorAyarlari ayar, string? yol = null)
    {
        ArgumentNullException.ThrowIfNull(ayar);
        yol ??= VarsayilanYol;
        Normalize(ayar);
        GuvenliDosyaYazmaServisi.Yaz(yol, JsonSerializer.Serialize(ayar, Json));
    }

    private static void Normalize(SalonEditorAyarlari a)
    {
        a.GridAraligiM = !double.IsFinite(a.GridAraligiM) ? 0.5 : Math.Clamp(a.GridAraligiM, 0.01, 100);
        a.PolarAciDerece = !double.IsFinite(a.PolarAciDerece) ? 15 : Math.Clamp(a.PolarAciDerece, 1, 90);
        a.OtomatikKayitDakika = Math.Clamp(a.OtomatikKayitDakika, 1, 60);
        a.SolPanelGenisligi = !double.IsFinite(a.SolPanelGenisligi) ? 320 : Math.Clamp(a.SolPanelGenisligi, 240, 700);
        if (!Enum.IsDefined(a.SonKatmanTipi)) a.SonKatmanTipi = SalonKatmanTipi.Zemin;
        a.SonKlasor ??= "";
        if (a.OrtoAcik) a.PolarAcik = false;
    }
}
