using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kst.Acoustics.App.Bina.Views;
using Kst.Acoustics.Core.Bina.Services;

// Exercises the actual Windows compositor and production theme. A headless
// bitmap render does not exercise native geometry hit testing or GPU drawing.
internal sealed class NativePreviewApp : Kst.Acoustics.App.App
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;
        var owner = new Window { Title = "CAD Önizleme Regresyon Testi", Width = 500, Height = 200 };
        desktop.MainWindow = owner;
        owner.Opened += async (_, _) =>
        {
            var clock = Stopwatch.StartNew();
            long last = clock.ElapsedMilliseconds, maxGap = 0;
            int pulses = 0;
            var heartbeat = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            heartbeat.Tick += (_, _) =>
            {
                var now = clock.ElapsedMilliseconds;
                maxGap = Math.Max(maxGap, now - last); last = now;
                if (++pulses % 20 == 0) Console.WriteLine($"HEARTBEAT {now} ms, max gap {maxGap} ms");
            };
            heartbeat.Start();
            try
            {
                var path = desktop.Args!.First(a => a != "--native");
                var doc = await Task.Run(() => new DxfKatPlaniParser().KatmanliOku(path));
                Console.WriteLine($"PARSED {doc.Katmanlar.Count} layers");
                var dialog = new MimariKatmanSecimWindow(doc);
                var dialogTask = dialog.ShowDialog<List<string>?>(owner);
                Button Find(string text) => dialog.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == text);
                await Until(() => Find("Üret").IsEnabled);
                Console.WriteLine("READY");
                var canvas = dialog.GetVisualDescendants().OfType<MimariKatmanOnizlemeCanvas>().Single();
                var pointer = new Pointer(1, PointerType.Mouse, true);
                for (int i = 0; i < 16; i++)
                {
                    canvas.RaiseEvent(new PointerWheelEventArgs(canvas, pointer, canvas,
                        new Point(canvas.Bounds.Width / 2, canvas.Bounds.Height / 2), 0,
                        new PointerPointProperties(), KeyModifiers.None, new Vector(0, i < 8 ? 1 : -1)));
                    await Task.Delay(50);
                }
                Console.WriteLine("ZOOM 16 frames");
                for (int i = 0; i < 3; i++)
                {
                    Find("Tümünü kapat").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    await Until(() => dialog.GetVisualDescendants().OfType<TextBlock>()
                        .Any(t => t.Text?.StartsWith("Açık katmanlarda üretilecek") == true));
                    Find("Tümünü aç").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    await Until(() => Find("Üret").IsEnabled);
                    dialog.Width -= 10;
                    Find("Ekrana sığdır").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    await Task.Delay(300);
                    Console.WriteLine($"TOGGLE {i + 1}");
                }
                Find("Vazgeç").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await dialogTask;
                if (pulses < 10 || maxGap > 1500) throw new Exception($"UI stalled: {maxGap} ms, {pulses} pulses");
                Console.WriteLine($"PASS: Native preview responsive; {pulses} pulses, max gap {maxGap} ms");
                desktop.Shutdown(0);
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); desktop.Shutdown(1); }
            finally { heartbeat.Stop(); }
        };
    }

    private static async Task Until(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(15)) throw new TimeoutException("Preview did not become ready");
            await Task.Delay(25);
        }
    }
}
