using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Kst.Acoustics.ThemeLab;

public partial class ThemeLabWindow : Window
{
    public ThemeLabWindow()
    {
        InitializeComponent();

        var grid = this.FindControl<DataGrid>("Grid")!;
        grid.ItemsSource = new[]
        {
            new MetrajSatiri("06.001", "Delikli akustik panel montajı", "120,0", "m²"),
            new MetrajSatiri("06.002", "Mineral yün dolgu 50 mm", "120,0", "m²"),
            new MetrajSatiri("11.014", "Aydınlatma armatürü LED 36W", "48,0", "adet"),
            new MetrajSatiri("30.007", "Bölme duvar alçıpan çift kat", "85,5", "m²"),
            new MetrajSatiri("60.002", "Kazı (yarma) makine ile", "1.250,0", "m³"),
        };

        var panel = this.FindControl<StackPanel>("ThemeButtons")!;
        for (int i = 0; i < ThemeCatalog.Temalar.Count; i++)
        {
            int idx = i;
            var btn = new Button { Content = ThemeCatalog.Temalar[i].Ad };
            // Not: Semi yalnız başlangıçta doğru stillenir; butonlar Fluent/FluentAvalonia
            // arası anlık geçiş için. Tam doğru kıyas: THEMELAB_THEME ile yeniden başlat.
            btn.Click += (_, _) => ThemeCatalog.Uygula(Application.Current!, idx);
            panel.Children.Add(btn);
        }

        var hint = this.FindControl<TextBlock>("ThemeHint");
        if (hint is not null) hint.Text = ThemeCatalog.Temalar[0].Ipucu;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public sealed record MetrajSatiri(string Poz, string Tanim, string Miktar, string Birim);
}
