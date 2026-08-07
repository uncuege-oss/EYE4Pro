using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Kst.Acoustics.Core.Electric.CevreAydinlatma;

namespace Kst.Acoustics.App.Electric.CevreAydinlatma;

/// <summary>
/// Park yüzeylerini (kategori renkli poligon) ve yerleşmiş direkleri (nokta) 2D çizen basit önizleme
/// kontrolü. Dünya koordinatlarını mevcut sınırlara sığdırır (fit-to-view, Y ekseni yukarı). Etkileşim
/// (zoom/pan, çizim) yok — sadece görüntüleme. NOT: bu kontrolün görsel çıktısı headless ortamda
/// doğrulanmadı.
/// </summary>
public class CevreAydinlatmaPreview : Control
{
    public static readonly StyledProperty<IReadOnlyList<CevreYuzey>?> YuzeylerProperty =
        AvaloniaProperty.Register<CevreAydinlatmaPreview, IReadOnlyList<CevreYuzey>?>(nameof(Yuzeyler));

    public static readonly StyledProperty<IReadOnlyList<AydinlatmaDiregi>?> DireklerProperty =
        AvaloniaProperty.Register<CevreAydinlatmaPreview, IReadOnlyList<AydinlatmaDiregi>?>(nameof(Direkler));

    public static readonly StyledProperty<IReadOnlyList<CizgiSegment>?> ArkaPlanProperty =
        AvaloniaProperty.Register<CevreAydinlatmaPreview, IReadOnlyList<CizgiSegment>?>(nameof(ArkaPlan));

    public IReadOnlyList<CevreYuzey>? Yuzeyler
    {
        get => GetValue(YuzeylerProperty);
        set => SetValue(YuzeylerProperty, value);
    }

    public IReadOnlyList<AydinlatmaDiregi>? Direkler
    {
        get => GetValue(DireklerProperty);
        set => SetValue(DireklerProperty, value);
    }

    public IReadOnlyList<CizgiSegment>? ArkaPlan
    {
        get => GetValue(ArkaPlanProperty);
        set => SetValue(ArkaPlanProperty, value);
    }

    static CevreAydinlatmaPreview()
    {
        AffectsRender<CevreAydinlatmaPreview>(YuzeylerProperty, DireklerProperty, ArkaPlanProperty);
    }

    public override void Render(DrawingContext ctx)
    {
        var size = Bounds.Size;
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#0A140D")), new Rect(size));

        var yuzeyler = Yuzeyler;
        var direkler = Direkler;
        var arkaPlan = ArkaPlan;

        // Dünya sınırları (tüm poligon köşeleri + direk + arka plan noktaları)
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        bool varMi = false;

        if (arkaPlan != null)
            foreach (var s in arkaPlan)
                foreach (var p in new[] { s.A, s.B })
                {
                    varMi = true;
                    if (p.X < minX) minX = p.X;
                    if (p.X > maxX) maxX = p.X;
                    if (p.Y < minY) minY = p.Y;
                    if (p.Y > maxY) maxY = p.Y;
                }

        if (yuzeyler != null)
            foreach (var y in yuzeyler)
                foreach (var p in y.Poligon)
                {
                    varMi = true;
                    if (p.X < minX) minX = p.X;
                    if (p.X > maxX) maxX = p.X;
                    if (p.Y < minY) minY = p.Y;
                    if (p.Y > maxY) maxY = p.Y;
                }

        if (direkler != null)
            foreach (var d in direkler)
            {
                varMi = true;
                if (d.X < minX) minX = d.X;
                if (d.X > maxX) maxX = d.X;
                if (d.Y < minY) minY = d.Y;
                if (d.Y > maxY) maxY = d.Y;
            }

        if (!varMi || size.Width < 10 || size.Height < 10)
            return;

        double dunyaW = maxX - minX;
        double dunyaH = maxY - minY;
        if (dunyaW < 1e-6) dunyaW = 1;
        if (dunyaH < 1e-6) dunyaH = 1;

        const double kenar = 20;
        double olcek = System.Math.Min(
            (size.Width - 2 * kenar) / dunyaW,
            (size.Height - 2 * kenar) / dunyaH);

        double ofsetX = (size.Width - dunyaW * olcek) / 2;
        double ofsetY = (size.Height - dunyaH * olcek) / 2;

        Point Donustur(double wx, double wy)
        {
            double sx = (wx - minX) * olcek + ofsetX;
            double sy = size.Height - ((wy - minY) * olcek + ofsetY); // Y yukarı
            return new Point(sx, sy);
        }

        // Arka plan çizgileri (en altta, soluk gri)
        if (arkaPlan != null)
        {
            var arkaPlanKalem = new Pen(new SolidColorBrush(Color.Parse("#33889988")), 0.7);
            foreach (var s in arkaPlan)
                ctx.DrawLine(arkaPlanKalem, Donustur(s.A.X, s.A.Y), Donustur(s.B.X, s.B.Y));
        }

        // Yüzeyler
        if (yuzeyler != null)
            foreach (var y in yuzeyler)
            {
                if (y.Poligon.Count < 3) continue;
                var (dolgu, cizgi) = KategoriRenkleri(y.Kategori);

                var geo = new StreamGeometry();
                using (var g = geo.Open())
                {
                    g.BeginFigure(Donustur(y.Poligon[0].X, y.Poligon[0].Y), true);
                    for (int i = 1; i < y.Poligon.Count; i++)
                        g.LineTo(Donustur(y.Poligon[i].X, y.Poligon[i].Y));
                    g.EndFigure(true);
                }
                ctx.DrawGeometry(new SolidColorBrush(dolgu), new Pen(new SolidColorBrush(cizgi), 1.4), geo);
            }

        // Direkler
        if (direkler != null)
        {
            var direkDolgu = new SolidColorBrush(Color.Parse("#E0C84E"));
            var direkCizgi = new Pen(new SolidColorBrush(Color.Parse("#7A6A1E")), 1);
            foreach (var d in direkler)
            {
                var c = Donustur(d.X, d.Y);
                ctx.DrawEllipse(direkDolgu, direkCizgi, c, 3.5, 3.5);
            }
        }
    }

    private static (Color Dolgu, Color Cizgi) KategoriRenkleri(YuzeyKategori kategori) => kategori switch
    {
        YuzeyKategori.YesilAlan => (Color.Parse("#552E7D32"), Color.Parse("#2E7D32")),
        YuzeyKategori.Yol => (Color.Parse("#55607D8B"), Color.Parse("#607D8B")),
        YuzeyKategori.SporAlani => (Color.Parse("#55EF6C00"), Color.Parse("#EF6C00")),
        YuzeyKategori.Bina => (Color.Parse("#551565C0"), Color.Parse("#1565C0")),
        _ => (Color.Parse("#55555555"), Color.Parse("#888888"))
    };
}
