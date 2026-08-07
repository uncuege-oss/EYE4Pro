using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Kst.Acoustics.App.Architecture.KonseptPlan.ViewModels;
using Kst.Acoustics.Core.Architecture.Circulation;
using Kst.Acoustics.Core.Architecture.Layout;
using Kst.Acoustics.Core.Architecture.Models;

namespace Kst.Acoustics.App.Architecture.KonseptPlan.Views;

public sealed class KonseptPlanCanvas : Control
{
    public static readonly StyledProperty<PlanTaslagi?> PlanProperty =
        AvaloniaProperty.Register<KonseptPlanCanvas, PlanTaslagi?>(nameof(Plan));

    public static readonly StyledProperty<KatTaslagi?> KatProperty =
        AvaloniaProperty.Register<KonseptPlanCanvas, KatTaslagi?>(nameof(Kat));

    public static readonly StyledProperty<MahalBlogu?> SeciliBlokProperty =
        AvaloniaProperty.Register<KonseptPlanCanvas, MahalBlogu?>(nameof(SeciliBlok));

    public static readonly StyledProperty<string?> SeciliDuseyElemanIdProperty =
        AvaloniaProperty.Register<KonseptPlanCanvas, string?>(nameof(SeciliDuseyElemanId));

    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly IBrush ArkaPlan = Brush("#0B1218");
    private static readonly IBrush CizimAlani = Brush("#0E1720");
    private static readonly IBrush IzgaraFirca = Brush("#172735");
    private static readonly Pen SinirKalemi = new(Brush("#3B5668"), 1.2);
    private static readonly Pen KoridorKalemi = new(Brush("#D3A84E"), 1.6);
    private static readonly Pen BlokKalemi = new(Brush("#203746"), 1.0);
    private static readonly Pen SeciliKalem = new(Brush("#F2C66C"), 2.4);
    private static readonly Pen CekirdekKalemi = new(Brush("#F08D6C"), 1.8);
    private static readonly Pen SaftKalemi = new(Brush("#8ACCC5"), 1.6);
    private static readonly Pen SeciliDuseyElemanKalemi = new(Brush("#FFFFFF"), 2.5);

    private MahalBlogu? _suruklenenBlok;
    private Point _sonDunyaNoktasi;

    static KonseptPlanCanvas()
    {
        AffectsRender<KonseptPlanCanvas>(PlanProperty, KatProperty, SeciliBlokProperty, SeciliDuseyElemanIdProperty);
    }

    public KonseptPlanCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    public PlanTaslagi? Plan
    {
        get => GetValue(PlanProperty);
        set => SetValue(PlanProperty, value);
    }

    public KatTaslagi? Kat
    {
        get => GetValue(KatProperty);
        set => SetValue(KatProperty, value);
    }

    public MahalBlogu? SeciliBlok
    {
        get => GetValue(SeciliBlokProperty);
        set => SetValue(SeciliBlokProperty, value);
    }

    public string? SeciliDuseyElemanId
    {
        get => GetValue(SeciliDuseyElemanIdProperty);
        set => SetValue(SeciliDuseyElemanIdProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = Bounds;
        context.DrawRectangle(ArkaPlan, null, bounds);

        if (Kat is null || bounds.Width < 10 || bounds.Height < 10)
        {
            DrawCenteredText(context, "Plan taslağı yok", bounds.Center, 14, Brush("#7890A2"));
            return;
        }

        var transform = TransformOlustur(Kat, bounds);
        var cizimRect = new Rect(transform.Padding, transform.Padding,
            Math.Max(1, bounds.Width - transform.Padding * 2),
            Math.Max(1, bounds.Height - transform.Padding * 2));

        context.DrawRectangle(CizimAlani, null, cizimRect, 8, 8);
        IzgaraCiz(context, transform);
        KatSiniriCiz(context, transform);
        KoridorlariCiz(context, transform);
        BloklariCiz(context, transform);
        DuseyElemanlariCiz(context, transform);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (Kat is null)
            return;

        Focus();
        var pozisyon = e.GetPosition(this);
        var duseyElemanId = DuseyElemanBul(pozisyon);
        if (duseyElemanId is not null)
        {
            if (DataContext is KonseptPlanViewModel duseyVm)
                duseyVm.DuseyElemanSec(duseyElemanId);

            SeciliDuseyElemanId = duseyElemanId;
            InvalidateVisual();
            return;
        }

        var blok = BlokBul(pozisyon);
        if (DataContext is KonseptPlanViewModel vm)
            vm.BlokSec(blok);

        SeciliBlok = blok;
        if (blok is not null && !blok.Kilitli)
        {
            _suruklenenBlok = blok;
            _sonDunyaNoktasi = EkrandanDunyaya(pozisyon, TransformOlustur(Kat, Bounds));
            e.Pointer.Capture(this);
        }

        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_suruklenenBlok is null || Kat is null || DataContext is not KonseptPlanViewModel vm)
            return;

        var dunya = EkrandanDunyaya(e.GetPosition(this), TransformOlustur(Kat, Bounds));
        var dx = dunya.X - _sonDunyaNoktasi.X;
        var dy = dunya.Y - _sonDunyaNoktasi.Y;
        if (Math.Abs(dx) < 0.01 && Math.Abs(dy) < 0.01)
            return;

        vm.BlokTasindi(_suruklenenBlok, _suruklenenBlok.X + dx, _suruklenenBlok.Y + dy);
        _sonDunyaNoktasi = dunya;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _suruklenenBlok = null;
        e.Pointer.Capture(null);
        InvalidateVisual();
    }

    private MahalBlogu? BlokBul(Point ekranNoktasi)
    {
        if (Kat is null)
            return null;

        var transform = TransformOlustur(Kat, Bounds);
        var dunya = EkrandanDunyaya(ekranNoktasi, transform);
        return Kat.Bloklar
            .Where(b => b.YerlesmisMi)
            .Where(b => dunya.X >= b.X && dunya.X <= b.X + b.GenislikM)
            .Where(b => dunya.Y >= b.Y && dunya.Y <= b.Y + b.DerinlikM)
            .OrderBy(b => b.AlanM2)
            .FirstOrDefault();
    }

    private string? DuseyElemanBul(Point ekranNoktasi)
    {
        if (Kat is null)
            return null;

        var transform = TransformOlustur(Kat, Bounds);
        var dunya = EkrandanDunyaya(ekranNoktasi, transform);
        var cekirdek = Kat.Cekirdekler
            .Where(c => c.YerlesmisMi)
            .Where(c => dunya.X >= c.X && dunya.X <= c.X + c.GenislikM)
            .Where(c => dunya.Y >= c.Y && dunya.Y <= c.Y + c.DerinlikM)
            .OrderBy(c => c.AlanM2)
            .FirstOrDefault();
        if (cekirdek is not null)
            return cekirdek.Id;

        var saft = Kat.Saftlar
            .Where(s => s.YerlesmisMi)
            .Where(s => dunya.X >= s.X && dunya.X <= s.X + s.GenislikM)
            .Where(s => dunya.Y >= s.Y && dunya.Y <= s.Y + s.DerinlikM)
            .OrderBy(s => s.AlanM2)
            .FirstOrDefault();
        return saft?.Id;
    }

    private void IzgaraCiz(DrawingContext context, TransformInfo transform)
    {
        var pen = new Pen(IzgaraFirca, 0.6);
        var adim = IzgaraAdimi(transform.Scale);
        var minX = Math.Floor(transform.MinX / adim) * adim;
        var maxX = transform.MinX + transform.WorldWidth;
        var minY = Math.Floor(transform.MinY / adim) * adim;
        var maxY = transform.MinY + transform.WorldHeight;

        for (var x = minX; x <= maxX + 0.001; x += adim)
        {
            var p1 = DunyadanEkrana(new Point(x, transform.MinY), transform);
            var p2 = DunyadanEkrana(new Point(x, maxY), transform);
            context.DrawLine(pen, p1, p2);
        }

        for (var y = minY; y <= maxY + 0.001; y += adim)
        {
            var p1 = DunyadanEkrana(new Point(transform.MinX, y), transform);
            var p2 = DunyadanEkrana(new Point(maxX, y), transform);
            context.DrawLine(pen, p1, p2);
        }
    }

    private void KatSiniriCiz(DrawingContext context, TransformInfo transform)
    {
        if (Kat is null)
            return;

        var rect = DunyaRectToScreen(new Rect(Kat.Sinir.X, Kat.Sinir.Y, Kat.Sinir.GenislikM, Kat.Sinir.DerinlikM), transform);
        context.DrawRectangle(null, SinirKalemi, rect, 6, 6);
        DrawText(context, Kat.Ad, new Point(rect.X + 10, rect.Y + 8), 12, Brush("#9FB3C1"));
    }

    private void KoridorlariCiz(DrawingContext context, TransformInfo transform)
    {
        if (Kat is null)
            return;

        foreach (var koridor in Kat.Koridorlar)
        {
            if (koridor.YatayMi)
            {
                var x = Math.Min(koridor.BaslangicX, koridor.BitisX);
                var rect = new Rect(
                    x,
                    koridor.BaslangicY - koridor.GenislikM / 2.0,
                    koridor.UzunlukM,
                    koridor.GenislikM);
                var ekran = DunyaRectToScreen(rect, transform);
                context.DrawRectangle(Brush("#3A321F"), KoridorKalemi, ekran, 4, 4);
                DrawText(context, "Koridor", new Point(ekran.X + 8, ekran.Y + 4), 11, Brush("#F0D58D"));
                continue;
            }

            var p1 = DunyadanEkrana(new Point(koridor.BaslangicX, koridor.BaslangicY), transform);
            var p2 = DunyadanEkrana(new Point(koridor.BitisX, koridor.BitisY), transform);
            context.DrawLine(new Pen(Brush("#D3A84E"), Math.Max(2, koridor.GenislikM * transform.Scale)), p1, p2);
        }
    }

    private void BloklariCiz(DrawingContext context, TransformInfo transform)
    {
        if (Kat is null)
            return;

        foreach (var blok in Kat.Bloklar.OrderBy(b => b.AlanM2))
        {
            if (!blok.YerlesmisMi)
                continue;

            var rect = DunyaRectToScreen(new Rect(blok.X, blok.Y, blok.GenislikM, blok.DerinlikM), transform);
            var secili = string.Equals(blok.Id, SeciliBlok?.Id, StringComparison.OrdinalIgnoreCase);
            var fill = BlokFirca(blok.KullanimSinifi);
            var pen = secili ? SeciliKalem : BlokKalemi;

            context.DrawRectangle(fill, pen, rect, 5, 5);
            if (blok.Kilitli)
                context.DrawRectangle(null, new Pen(Brush("#FFFFFF"), 0.8), rect.Deflate(3), 4, 4);

            if (rect.Width > 44 && rect.Height > 28)
            {
                using var _ = context.PushClip(rect.Deflate(4));
                var ad = Kisalt(blok.Ad, Math.Max(6, (int)(rect.Width / 7)));
                DrawText(context, ad, new Point(rect.X + 6, rect.Y + 5), 11, Brush("#F4FAFC"));

                if (rect.Height > 44)
                    DrawText(context, KonseptPlanViewModel.AlanYaz(blok.AlanM2), new Point(rect.X + 6, rect.Y + 22), 10, Brush("#D4E2E9"));
            }

            if (secili)
            {
                var tutamak = new Rect(rect.Right - 7, rect.Bottom - 7, 7, 7);
                context.DrawRectangle(Brush("#F2C66C"), null, tutamak, 2, 2);
            }
        }
    }

    private void DuseyElemanlariCiz(DrawingContext context, TransformInfo transform)
    {
        if (Kat is null)
            return;

        foreach (var cekirdek in Kat.Cekirdekler.Where(c => c.YerlesmisMi).OrderBy(c => c.AlanM2))
        {
            var rect = DunyaRectToScreen(new Rect(cekirdek.X, cekirdek.Y, cekirdek.GenislikM, cekirdek.DerinlikM), transform);
            var secili = string.Equals(cekirdek.Id, SeciliDuseyElemanId, StringComparison.OrdinalIgnoreCase);
            context.DrawRectangle(Brush("#5B2D28"), secili ? SeciliDuseyElemanKalemi : CekirdekKalemi, rect, 4, 4);
            context.DrawLine(new Pen(Brush("#F7B08F"), 1.2), rect.TopLeft, rect.BottomRight);
            context.DrawLine(new Pen(Brush("#F7B08F"), 1.2), rect.TopRight, rect.BottomLeft);

            if (rect.Width > 34 && rect.Height > 24)
            {
                using var _ = context.PushClip(rect.Deflate(3));
                DrawText(context, KisaCekirdekEtiketi(cekirdek.Tip), new Point(rect.X + 5, rect.Y + 4), 10, Brush("#FFE1D4"));
            }
        }

        foreach (var saft in Kat.Saftlar.Where(s => s.YerlesmisMi).OrderBy(s => s.AlanM2))
        {
            var rect = DunyaRectToScreen(new Rect(saft.X, saft.Y, saft.GenislikM, saft.DerinlikM), transform);
            var secili = string.Equals(saft.Id, SeciliDuseyElemanId, StringComparison.OrdinalIgnoreCase);
            context.DrawRectangle(Brush("#214B4C"), secili ? SeciliDuseyElemanKalemi : SaftKalemi, rect, 3, 3);

            if (rect.Width > 28 && rect.Height > 20)
            {
                using var _ = context.PushClip(rect.Deflate(3));
                DrawText(context, "Şaft", new Point(rect.X + 4, rect.Y + 3), 10, Brush("#D2FFFA"));
            }
        }
    }

    private TransformInfo TransformOlustur(KatTaslagi kat, Rect bounds)
    {
        var minX = kat.Sinir.X;
        var minY = kat.Sinir.Y;
        var maxX = kat.Sinir.X + Math.Max(1, kat.Sinir.GenislikM);
        var maxY = kat.Sinir.Y + Math.Max(1, kat.Sinir.DerinlikM);

        foreach (var blok in kat.Bloklar.Where(b => b.YerlesmisMi))
        {
            minX = Math.Min(minX, blok.X);
            minY = Math.Min(minY, blok.Y);
            maxX = Math.Max(maxX, blok.X + blok.GenislikM);
            maxY = Math.Max(maxY, blok.Y + blok.DerinlikM);
        }

        foreach (var koridor in kat.Koridorlar)
        {
            minX = Math.Min(minX, Math.Min(koridor.BaslangicX, koridor.BitisX) - koridor.GenislikM);
            minY = Math.Min(minY, Math.Min(koridor.BaslangicY, koridor.BitisY) - koridor.GenislikM);
            maxX = Math.Max(maxX, Math.Max(koridor.BaslangicX, koridor.BitisX) + koridor.GenislikM);
            maxY = Math.Max(maxY, Math.Max(koridor.BaslangicY, koridor.BitisY) + koridor.GenislikM);
        }

        foreach (var cekirdek in kat.Cekirdekler.Where(c => c.YerlesmisMi))
        {
            minX = Math.Min(minX, cekirdek.X);
            minY = Math.Min(minY, cekirdek.Y);
            maxX = Math.Max(maxX, cekirdek.X + cekirdek.GenislikM);
            maxY = Math.Max(maxY, cekirdek.Y + cekirdek.DerinlikM);
        }

        foreach (var saft in kat.Saftlar.Where(s => s.YerlesmisMi))
        {
            minX = Math.Min(minX, saft.X);
            minY = Math.Min(minY, saft.Y);
            maxX = Math.Max(maxX, saft.X + saft.GenislikM);
            maxY = Math.Max(maxY, saft.Y + saft.DerinlikM);
        }

        var worldWidth = Math.Max(1, maxX - minX);
        var worldHeight = Math.Max(1, maxY - minY);
        var padding = 24.0;
        var availableWidth = Math.Max(1, bounds.Width - padding * 2);
        var availableHeight = Math.Max(1, bounds.Height - padding * 2);
        var scale = Math.Max(1, Math.Min(availableWidth / worldWidth, availableHeight / worldHeight));

        return new TransformInfo(minX, minY, worldWidth, worldHeight, scale, padding);
    }

    private static Rect DunyaRectToScreen(Rect rect, TransformInfo transform)
    {
        var p = DunyadanEkrana(new Point(rect.X, rect.Y), transform);
        return new Rect(p.X, p.Y, rect.Width * transform.Scale, rect.Height * transform.Scale);
    }

    private static Point DunyadanEkrana(Point dunya, TransformInfo transform)
        => new(
            transform.Padding + (dunya.X - transform.MinX) * transform.Scale,
            transform.Padding + (dunya.Y - transform.MinY) * transform.Scale);

    private static Point EkrandanDunyaya(Point ekran, TransformInfo transform)
        => new(
            transform.MinX + (ekran.X - transform.Padding) / transform.Scale,
            transform.MinY + (ekran.Y - transform.Padding) / transform.Scale);

    private static double IzgaraAdimi(double scale)
    {
        if (scale >= 36)
            return 1;
        if (scale >= 18)
            return 2;
        if (scale >= 8)
            return 5;

        return 10;
    }

    private static IBrush BlokFirca(KullanimSinifi sinif)
        => sinif switch
        {
            KullanimSinifi.AnaKullanim => Brush("#256D7B"),
            KullanimSinifi.YardimciKullanim => Brush("#395E7B"),
            KullanimSinifi.Sirkulasyon => Brush("#6D5B2C"),
            KullanimSinifi.IslakHacim => Brush("#4D5E89"),
            KullanimSinifi.TeknikHacim => Brush("#664E72"),
            KullanimSinifi.Servis => Brush("#74603E"),
            KullanimSinifi.Depolama => Brush("#536148"),
            KullanimSinifi.DuseySirkulasyon => Brush("#7B4E4E"),
            KullanimSinifi.Otopark => Brush("#4F5A62"),
            KullanimSinifi.GuvenlikVeYangin => Brush("#7A4A38"),
            _ => Brush("#435260")
        };

    private static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));

    private static void DrawCenteredText(DrawingContext context, string text, Point center, double size, IBrush brush)
    {
        var formatted = new FormattedText(text, Tr, FlowDirection.LeftToRight, Typeface.Default, size, brush);
        context.DrawText(formatted, new Point(center.X - formatted.Width / 2, center.Y - formatted.Height / 2));
    }

    private static void DrawText(DrawingContext context, string text, Point point, double size, IBrush brush)
    {
        var formatted = new FormattedText(text, Tr, FlowDirection.LeftToRight, Typeface.Default, size, brush);
        context.DrawText(formatted, point);
    }

    private static string Kisalt(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= maxLength)
            return text;

        return maxLength <= 3 ? text[..maxLength] : $"{text[..(maxLength - 1)]}…";
    }

    private static string KisaCekirdekEtiketi(CekirdekTipi tip)
        => tip switch
        {
            CekirdekTipi.YanginMerdiveni => "Y.M.",
            CekirdekTipi.Merdiven => "Mer.",
            CekirdekTipi.Asansor => "As.",
            CekirdekTipi.Rampa => "R.",
            _ => "Çek."
        };

    private readonly record struct TransformInfo(
        double MinX,
        double MinY,
        double WorldWidth,
        double WorldHeight,
        double Scale,
        double Padding);
}
