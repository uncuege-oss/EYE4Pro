using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Kst.Acoustics.ThemeLab;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // Tema, pencere oluşturulmadan ÖNCE uygulanır (Semi kontrol şablonları için şart).
        var env = System.Environment.GetEnvironmentVariable("THEMELAB_THEME");
        int index = int.TryParse(env, out var i) ? i : 0;
        ThemeCatalog.Uygula(this, index);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new ThemeLabWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
