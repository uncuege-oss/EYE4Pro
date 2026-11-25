using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kst.Acoustics.App.Bina.Views;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.Common.Geometry;

internal static class Program
{
    private static int _passed;
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            if (args.Contains("--native"))
                return AppBuilder.Configure<NativePreviewApp>().UsePlatformDetect().WithInterFont()
                    .StartWithClassicDesktopLifetime(args);
            AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                .UseSkia().WithInterFont().SetupWithoutStarting();
            var empty = new MimariIceriAktarimRaporuWindow("Not yok", null);
            empty.Show(); Pump();
            Check(!Button(empty, "Çizimi incele").IsEnabled, "No drawing disables inspect with an explanation");
            empty.Close(); Pump();
            var snapshot = new BinaKat {
                DxfFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".dxf"),
                HamSegmentler = new() { (new Point2D(0, 0), new Point2D(8, 0)),
                    (new Point2D(8, 0), new Point2D(8, 6)) },
                HamMetinler = new() { (new Point2D(2, 2), "KAYNAK ÇİZİM") },
                KaynakKatmanlari = new() { "DUVAR", "YAZI" }
            };
            Check(!File.Exists(snapshot.DxfFilePath) && snapshot.Odalar.Count == 0, "Fixture has no source file or generated rooms");
            Review(snapshot, "synthetic");
            var savedFloor = new BinaKat { Ad = "Kaydedilmiş kat", DxfFilePath = "missing-source.dwg",
                DxfSegmentler = new() { new BinaDxfSegment { A = new Point2D(0, 0), B = new Point2D(4, 4) } } };
            Review(savedFloor, "saved-floor");
            Check(savedFloor.HamSegmentler.Count == 0, "Saved floor preview does not mutate floor data");
            if (args.Length > 0)
            {
                var drawing = new DxfKatPlaniParser().KatmanliOku(args[0]);
                var real = drawing.HamKatOlustur(drawing.Katmanlar.Where(k => k.VarsayilanAcik).Select(k => k.Ad));
                real.DxfFilePath = "berecik.dwg";
                Review(real, "berecik");
                Console.WriteLine($"REAL: {real.HamSegmentler.Count} segments, {real.HamMetinler.Count} texts");
            }
            Console.WriteLine($"{_passed}/{_passed} passed");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void Review(BinaKat drawing, string name)
    {
        var report = new MimariIceriAktarimRaporuWindow("Güvenilir kat planı ayrılamadı.", drawing);
        report.Show(); Pump();
        for (int i = 0; i < 2; i++)
        {
            Button(report, "Çizimi incele").RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Pump();
            var viewer = report.OwnedWindows.OfType<MimariCizimIncelemeWindow>().Single();
            Check(viewer.IsVisible && report.IsVisible, name + ": inspect opens a drawing window and keeps the report");
            var canvas = viewer.GetVisualDescendants().OfType<MimariKatmanOnizlemeCanvas>().Single();
            Check(canvas.Bounds.Width > 100 && canvas.Bounds.Height > 100, name + ": visible drawing viewport");
            if (i == 0)
            {
                using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(viewer.Bounds.Width),
                    (int)Math.Ceiling(viewer.Bounds.Height)));
                bitmap.Render(viewer);
                var output = Path.GetFullPath("outputs/drawing-review-tests");
                Directory.CreateDirectory(output);
                bitmap.Save(Path.Combine(output, name + ".png"));
                Button(viewer, "Ekrana sığdır").RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                Pump();
            }
            Button(viewer, "Rapora dön").RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Pump();
            Check(!viewer.IsVisible && report.IsVisible && Button(report, "Çizimi incele").IsEnabled,
                name + ": return restores report and supports repeated inspection");
        }
        Button(report, "Kapat").RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Pump();
        Check(!report.IsVisible, name + ": close has a separate action");
    }

    private static Button Button(Window window, string text) => window.GetVisualDescendants().OfType<Button>()
        .Single(b => b.Content as string == text);
    private static void Pump() => Dispatcher.UIThread.RunJobs();
    private static void Check(bool condition, string title)
    {
        if (!condition) throw new Exception(title);
        _passed++; Console.WriteLine("PASS " + title);
    }
    private sealed class TestApp : Application
    {
        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
            RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
        }
    }
}
