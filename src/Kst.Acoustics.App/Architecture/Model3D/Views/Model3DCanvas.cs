using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Kst.Acoustics.App.Architecture.Model3D.ViewModels;
using Kst.Acoustics.Core.Architecture.Model3D;

namespace Kst.Acoustics.App.Architecture.Model3D.Views;

public sealed class Model3DCanvas : Control
{
    public static readonly StyledProperty<Bim3DModel?> ModelProperty =
        AvaloniaProperty.Register<Model3DCanvas, Bim3DModel?>(nameof(Model));

    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly IBrush ArkaPlan = Brush("#071018");
    private static readonly IBrush CizimAlani = Brush("#0B141D");
    private static readonly Pen IzgaraKalemi = new(Brush("#162838"), 0.7);
    private static readonly Pen KenarKalemi = new(Brush("#DCE9EF", 160), 0.8);
    private static readonly Pen AksKalemi = new(Brush("#4A6A7B", 210), 1.1);

    private double _donusAcisiDerece = -35;
    private double _zoom = 1;
    private bool _surukleniyor;
    private Point _sonPozisyon;

    static Model3DCanvas()
    {
        AffectsRender<Model3DCanvas>(ModelProperty);
    }

    public Model3DCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    public Bim3DModel? Model
    {
        get => GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    public void KamerayiSifirla()
    {
        _donusAcisiDerece = -35;
        _zoom = 1;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = Bounds;
        context.DrawRectangle(ArkaPlan, null, bounds);

        if (bounds.Width < 20 || bounds.Height < 20)
            return;

        var cizimRect = bounds.Deflate(new Thickness(14));
        context.DrawRectangle(CizimAlani, null, cizimRect, 8, 8);

        var kutular = KutulariOlustur(Model);
        if (kutular.Count == 0)
        {
            DrawCenteredText(context, "3D model yok", bounds.Center, 15, Brush("#7890A2"));
            return;
        }

        var kamera = KameraOlustur(kutular, cizimRect);
        IzgaraCiz(context, kamera);
        AkslariCiz(context, kamera);

        foreach (var kutu in kutular.OrderBy(k => Derinlik(k, kamera)))
        {
            KutuCiz(context, kutu, kamera);
        }

        var bilgi = $"Yakınlık %{Math.Round(_zoom * 100).ToString(Tr)}";
        DrawText(context, bilgi, new Point(cizimRect.X + 14, cizimRect.Bottom - 28), 11, Brush("#7890A2"));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        _surukleniyor = true;
        _sonPozisyon = e.GetPosition(this);
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_surukleniyor)
            return;

        var pozisyon = e.GetPosition(this);
        var dx = pozisyon.X - _sonPozisyon.X;
        if (Math.Abs(dx) > 0.1)
        {
            _donusAcisiDerece += dx * 0.35;
            _sonPozisyon = pozisyon;
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _surukleniyor = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var carpan = e.Delta.Y > 0 ? 1.12 : 0.88;
        _zoom = Math.Clamp(_zoom * carpan, 0.45, 2.8);
        InvalidateVisual();
        e.Handled = true;
    }

    private static List<Kutu3D> KutulariOlustur(Bim3DModel? model)
    {
        var kutular = new List<Kutu3D>();
        if (model is null)
            return kutular;

        foreach (var kat in model.Katlar.OrderBy(k => k.KatNo))
        {
            foreach (var doseme in kat.Dosemeler.Where(d => d.YerlesmisMi))
            {
                kutular.Add(new Kutu3D(
                    doseme.X,
                    doseme.Y,
                    doseme.Z,
                    doseme.GenislikM,
                    doseme.DerinlikM,
                    Math.Max(0.06, doseme.KalinlikM),
                    $"{kat.Ad} döşeme",
                    "#6E7F8B",
                    82,
                    0));
            }

            foreach (var mahal in kat.Mahaller.Where(m => m.YerlesmisMi))
            {
                kutular.Add(new Kutu3D(
                    mahal.X,
                    mahal.Y,
                    mahal.Z,
                    mahal.GenislikM,
                    mahal.DerinlikM,
                    mahal.YukseklikM,
                    mahal.Ad,
                    MahalRengi(mahal.Tip),
                    176,
                    2));
            }

            foreach (var cekirdek in kat.CekirdekHacimleri.Where(c => c.YerlesmisMi))
            {
                kutular.Add(new Kutu3D(
                    cekirdek.X,
                    cekirdek.Y,
                    cekirdek.Z,
                    cekirdek.GenislikM,
                    cekirdek.DerinlikM,
                    cekirdek.YukseklikM,
                    string.IsNullOrWhiteSpace(cekirdek.Ad) ? "Çekirdek" : cekirdek.Ad,
                    "#E07955",
                    205,
                    3));
            }

            foreach (var saft in kat.SaftHacimleri.Where(s => s.YerlesmisMi))
            {
                kutular.Add(new Kutu3D(
                    saft.X,
                    saft.Y,
                    saft.Z,
                    saft.GenislikM,
                    saft.DerinlikM,
                    saft.YukseklikM,
                    string.IsNullOrWhiteSpace(saft.Ad) ? "Şaft" : saft.Ad,
                    "#6CC7BE",
                    205,
                    4));
            }

            foreach (var tavan in kat.Tavanlar.Where(t => t.YerlesmisMi))
            {
                kutular.Add(new Kutu3D(
                    tavan.X,
                    tavan.Y,
                    tavan.Z,
                    tavan.GenislikM,
                    tavan.DerinlikM,
                    Math.Max(0.04, tavan.KalinlikM),
                    $"{kat.Ad} tavan",
                    "#9FB0BB",
                    48,
                    1));
            }
        }

        return kutular;
    }

    private Kamera3D KameraOlustur(IReadOnlyList<Kutu3D> kutular, Rect viewport)
    {
        var minX = kutular.Min(k => k.X);
        var minY = kutular.Min(k => k.Y);
        var maxX = kutular.Max(k => k.X + k.Genislik);
        var maxY = kutular.Max(k => k.Y + k.Derinlik);
        var merkezX = (minX + maxX) / 2.0;
        var merkezY = (minY + maxY) / 2.0;
        var aci = _donusAcisiDerece * Math.PI / 180.0;
        var cos = Math.Cos(aci);
        var sin = Math.Sin(aci);

        var hamNoktalar = kutular
            .SelectMany(k => KoseNoktalari(k))
            .Select(p => HamProjeksiyon(p, merkezX, merkezY, cos, sin))
            .ToList();

        var minPx = hamNoktalar.Min(p => p.X);
        var maxPx = hamNoktalar.Max(p => p.X);
        var minPy = hamNoktalar.Min(p => p.Y);
        var maxPy = hamNoktalar.Max(p => p.Y);
        var hamGenislik = Math.Max(1, maxPx - minPx);
        var hamYukseklik = Math.Max(1, maxPy - minPy);
        var olcek = Math.Min(viewport.Width / hamGenislik, viewport.Height / hamYukseklik) * 0.82 * _zoom;
        var offsetX = viewport.Center.X - (minPx + maxPx) / 2.0 * olcek;
        var offsetY = viewport.Center.Y - (minPy + maxPy) / 2.0 * olcek;

        return new Kamera3D(merkezX, merkezY, cos, sin, olcek, offsetX, offsetY, viewport);
    }

    private static IEnumerable<Nokta3D> KoseNoktalari(Kutu3D k)
    {
        yield return new Nokta3D(k.X, k.Y, k.Z);
        yield return new Nokta3D(k.X + k.Genislik, k.Y, k.Z);
        yield return new Nokta3D(k.X, k.Y + k.Derinlik, k.Z);
        yield return new Nokta3D(k.X + k.Genislik, k.Y + k.Derinlik, k.Z);
        yield return new Nokta3D(k.X, k.Y, k.Z + k.Yukseklik);
        yield return new Nokta3D(k.X + k.Genislik, k.Y, k.Z + k.Yukseklik);
        yield return new Nokta3D(k.X, k.Y + k.Derinlik, k.Z + k.Yukseklik);
        yield return new Nokta3D(k.X + k.Genislik, k.Y + k.Derinlik, k.Z + k.Yukseklik);
    }

    private static Point HamProjeksiyon(Nokta3D p, double merkezX, double merkezY, double cos, double sin)
    {
        var x = p.X - merkezX;
        var y = p.Y - merkezY;
        var rx = x * cos - y * sin;
        var ry = x * sin + y * cos;
        return new Point((rx - ry) * 0.86, (rx + ry) * 0.46 - p.Z * 0.78);
    }

    private static Point Projeksiyon(Nokta3D p, Kamera3D kamera)
    {
        var ham = HamProjeksiyon(p, kamera.MerkezX, kamera.MerkezY, kamera.Cos, kamera.Sin);
        return new Point(kamera.OffsetX + ham.X * kamera.Olcek, kamera.OffsetY + ham.Y * kamera.Olcek);
    }

    private static double Derinlik(Kutu3D k, Kamera3D kamera)
    {
        var x = k.X + k.Genislik / 2.0 - kamera.MerkezX;
        var y = k.Y + k.Derinlik / 2.0 - kamera.MerkezY;
        var ry = x * kamera.Sin + y * kamera.Cos;
        return ry + k.Z * 0.08 + k.CizimOnceligi * 0.01;
    }

    private static void KutuCiz(DrawingContext context, Kutu3D kutu, Kamera3D kamera)
    {
        var p000 = Projeksiyon(new Nokta3D(kutu.X, kutu.Y, kutu.Z), kamera);
        var p100 = Projeksiyon(new Nokta3D(kutu.X + kutu.Genislik, kutu.Y, kutu.Z), kamera);
        var p010 = Projeksiyon(new Nokta3D(kutu.X, kutu.Y + kutu.Derinlik, kutu.Z), kamera);
        var p110 = Projeksiyon(new Nokta3D(kutu.X + kutu.Genislik, kutu.Y + kutu.Derinlik, kutu.Z), kamera);
        var p001 = Projeksiyon(new Nokta3D(kutu.X, kutu.Y, kutu.Z + kutu.Yukseklik), kamera);
        var p101 = Projeksiyon(new Nokta3D(kutu.X + kutu.Genislik, kutu.Y, kutu.Z + kutu.Yukseklik), kamera);
        var p011 = Projeksiyon(new Nokta3D(kutu.X, kutu.Y + kutu.Derinlik, kutu.Z + kutu.Yukseklik), kamera);
        var p111 = Projeksiyon(new Nokta3D(kutu.X + kutu.Genislik, kutu.Y + kutu.Derinlik, kutu.Z + kutu.Yukseklik), kamera);

        var ana = Color.Parse(kutu.Renk);
        var yanFirca = new SolidColorBrush(Color.FromArgb((byte)Math.Max(24, kutu.Alfa - 46), ana.R, ana.G, ana.B));
        var onFirca = new SolidColorBrush(Color.FromArgb((byte)Math.Max(30, kutu.Alfa - 24), ana.R, ana.G, ana.B));
        var ustFirca = new SolidColorBrush(Color.FromArgb(kutu.Alfa, ana.R, ana.G, ana.B));

        CokgenCiz(context, [p000, p100, p101, p001], onFirca, KenarKalemi);
        CokgenCiz(context, [p100, p110, p111, p101], yanFirca, KenarKalemi);
        CokgenCiz(context, [p010, p110, p111, p011], yanFirca, KenarKalemi);
        CokgenCiz(context, [p001, p101, p111, p011], ustFirca, KenarKalemi);

        if (kutu.CizimOnceligi >= 2 && kutu.Genislik * kamera.Olcek > 22 && kutu.Derinlik * kamera.Olcek > 18)
        {
            var merkez = Projeksiyon(new Nokta3D(kutu.X + kutu.Genislik / 2.0, kutu.Y + kutu.Derinlik / 2.0, kutu.Z + kutu.Yukseklik), kamera);
            DrawCenteredText(context, Kisalt(kutu.Etiket, 18), merkez, 10.5, Brush("#F3FAFC", 230));
        }
    }

    private static void IzgaraCiz(DrawingContext context, Kamera3D kamera)
    {
        var rect = kamera.Viewport;
        var adim = 34;
        for (var x = rect.X + adim; x < rect.Right; x += adim)
            context.DrawLine(IzgaraKalemi, new Point(x, rect.Y), new Point(x, rect.Bottom));

        for (var y = rect.Y + adim; y < rect.Bottom; y += adim)
            context.DrawLine(IzgaraKalemi, new Point(rect.X, y), new Point(rect.Right, y));
    }

    private static void AkslariCiz(DrawingContext context, Kamera3D kamera)
    {
        var merkez = new Nokta3D(kamera.MerkezX, kamera.MerkezY, 0);
        var p0 = Projeksiyon(merkez, kamera);
        var px = Projeksiyon(new Nokta3D(kamera.MerkezX + 3, kamera.MerkezY, 0), kamera);
        var py = Projeksiyon(new Nokta3D(kamera.MerkezX, kamera.MerkezY + 3, 0), kamera);
        var pz = Projeksiyon(new Nokta3D(kamera.MerkezX, kamera.MerkezY, 3), kamera);

        context.DrawLine(AksKalemi, p0, px);
        context.DrawLine(AksKalemi, p0, py);
        context.DrawLine(AksKalemi, p0, pz);
    }

    private static void CokgenCiz(DrawingContext context, Point[] noktalar, IBrush fill, Pen pen)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(noktalar[0], true);
            for (var i = 1; i < noktalar.Length; i++)
                ctx.LineTo(noktalar[i]);
            ctx.EndFigure(true);
        }

        context.DrawGeometry(fill, pen, geometry);
    }

    private static string MahalRengi(Kst.Acoustics.Core.Architecture.Models.MahalTipi tip)
        => tip switch
        {
            Kst.Acoustics.Core.Architecture.Models.MahalTipi.ToplantiOdasi or
            Kst.Acoustics.Core.Architecture.Models.MahalTipi.KonferansSalonu => "#B9854E",
            Kst.Acoustics.Core.Architecture.Models.MahalTipi.Wc or
            Kst.Acoustics.Core.Architecture.Models.MahalTipi.Banyo or
            Kst.Acoustics.Core.Architecture.Models.MahalTipi.Mutfak => "#577CB8",
            Kst.Acoustics.Core.Architecture.Models.MahalTipi.TeknikOda or
            Kst.Acoustics.Core.Architecture.Models.MahalTipi.ElektrikOdasi or
            Kst.Acoustics.Core.Architecture.Models.MahalTipi.MekanikOda => "#836A96",
            Kst.Acoustics.Core.Architecture.Models.MahalTipi.Koridor or
            Kst.Acoustics.Core.Architecture.Models.MahalTipi.Hol => "#9A7A39",
            _ => "#2F8EA0"
        };

    private static IBrush Brush(string hex, byte alpha = 255)
    {
        var c = Color.Parse(hex);
        return new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
    }

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

    private readonly record struct Nokta3D(double X, double Y, double Z);

    private readonly record struct Kutu3D(
        double X,
        double Y,
        double Z,
        double Genislik,
        double Derinlik,
        double Yukseklik,
        string Etiket,
        string Renk,
        byte Alfa,
        int CizimOnceligi);

    private readonly record struct Kamera3D(
        double MerkezX,
        double MerkezY,
        double Cos,
        double Sin,
        double Olcek,
        double OffsetX,
        double OffsetY,
        Rect Viewport);
}
