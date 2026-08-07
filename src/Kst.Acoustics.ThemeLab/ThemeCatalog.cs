using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using FluentAvalonia.Styling;

namespace Kst.Acoustics.ThemeLab;

/// <summary>Kıyaslanacak temaların tek kaynağı. Tema kontrol şablonlarının doğru
/// yüklenmesi için tema, pencere oluşturulmadan ÖNCE uygulanır (Semi bunu gerektirir).</summary>
public static class ThemeCatalog
{
    private static readonly Uri BaseUri = new("avares://Kst.Acoustics.ThemeLab/");

    public sealed record ThemeOption(string Ad, Func<IStyle> StilUret, ThemeVariant Variant, string Ipucu);

    public static readonly IReadOnlyList<ThemeOption> Temalar = new List<ThemeOption>
    {
        new("Semi (Koyu)",           SemiStili,          ThemeVariant.Dark,  "Önerilen — modern, temiz. Avalonia 12 uyumlu."),
        new("Semi (Açık)",           SemiStili,          ThemeVariant.Light, "Önerilen — modern, temiz. Avalonia 12 uyumlu."),
        new("Fluent (Koyu)",         () => new FluentTheme(),        ThemeVariant.Dark,  "Yerleşik Microsoft Fluent — ekstra paket yok."),
        new("Fluent (Açık)",         () => new FluentTheme(),        ThemeVariant.Light, "Yerleşik Microsoft Fluent — ekstra paket yok."),
        new("FluentAvalonia (Koyu)", () => new FluentAvaloniaTheme(), ThemeVariant.Dark,  "WinUI 3 / Windows 11 görünümü."),
        new("FluentAvalonia (Açık)", () => new FluentAvaloniaTheme(), ThemeVariant.Light, "WinUI 3 / Windows 11 görünümü."),
    };

    public static void Uygula(Application app, int index)
    {
        if (index < 0 || index >= Temalar.Count) index = 0;
        var secim = Temalar[index];
        app.Styles.Clear();
        app.Styles.Add(secim.StilUret());
        app.Styles.Add(DataGridStili(secim.Ad));
        app.RequestedThemeVariant = secim.Variant;
    }

    private static IStyle SemiStili()
        => new StyleInclude(BaseUri) { Source = new Uri("avares://Semi.Avalonia/SemiTheme.axaml") };

    private static IStyle DataGridStili(string temaAd)
    {
        var kaynak = temaAd.StartsWith("Semi", StringComparison.Ordinal)
            ? "avares://Semi.Avalonia.DataGrid/Index.axaml"
            : "avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml";
        return new StyleInclude(BaseUri) { Source = new Uri(kaynak) };
    }
}
