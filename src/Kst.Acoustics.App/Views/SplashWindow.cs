using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace Kst.Acoustics.App.Views;

public sealed class SplashWindow : Window
{
    public SplashWindow()
    {
        Width = 360; Height = 420;
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brush.Parse("#292F35");
        var progress = new ProgressBar { Minimum = 0, Maximum = 100, Height = 6, Margin = new Avalonia.Thickness(24, 12) };
        using var stream = AssetLoader.Open(new Uri("avares://Kst.Acoustics.App/Assets/eye4pro-original.jpg"));
        Content = new StackPanel { Children = {
            new Image { Source = new Bitmap(stream), Width = 360, Height = 360 },
            new TextBlock { Text = "Modüller Yükleniyor", Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center },
            progress
        }};
        Opened += (_, _) =>
        {
            var watch = Stopwatch.StartNew();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            timer.Tick += (_, _) =>
            {
                progress.Value = Math.Min(100, watch.Elapsed.TotalSeconds / 3 * 100);
                if (watch.Elapsed.TotalSeconds < 3) return;
                timer.Stop();
                var home = new EngineeringHomeWindow();
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    desktop.MainWindow = home;
                home.Show();
                Close();
            };
            timer.Start();
        };
    }
}
