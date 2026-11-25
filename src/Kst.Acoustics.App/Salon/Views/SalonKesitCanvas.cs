using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Salon.Models;
using Kst.Acoustics.Core.Salon.Services;

namespace Kst.Acoustics.App.Salon.Views;

public sealed class SalonKesitCanvas : Control
{
    public SalonGeometri? Salon { get; set; }
    public Point2D A { get; set; }
    public Point2D B { get; set; } = new(0, 20);
    public Action? Degisecek { get; set; }
    public Action? Degisti { get; set; }
    public Action<int>? NoktaSecildi { get; set; }
    public IReadOnlyList<Point2D> Profil => _profil;
    private string Grup => FormattableString.Invariant($"{A.X:0.####},{A.Y:0.####}>{B.X:0.####},{B.Y:0.####}");
    private readonly List<Point2D> _profil = new();
    private int _drag = -1;
    private double _maxZ = 10;
    private double Length => Math.Max(.01, Math.Sqrt(Math.Pow(B.X - A.X, 2) + Math.Pow(B.Y - A.Y, 2)));
    private double Scale => Math.Max(.1, Math.Min((Bounds.Width - 90) / Length, (Bounds.Height - 90) / (_maxZ + 3)));
    private Point P(double x, double z) => new(45 + x * Scale, Bounds.Height - 40 - (z + 2) * Scale);
    public void ProfiliSifirla()
    {
        _profil.Clear(); if (Salon == null) return;
        _profil.Add(new(0, SalonTasarimServisi.TavanKotu(Salon, A))); _profil.Add(new(Length, SalonTasarimServisi.TavanKotu(Salon, B)));
        foreach (var o in Salon.CizimOgeleri.Where(o => o.KesitGrubu == Grup))
        {
            var p = o.KotReferansi; double x = ((p.X - A.X) * (B.X - A.X) + (p.Y - A.Y) * (B.Y - A.Y)) / Length;
            if (x > .001 && x < Length - .001 && !_profil.Any(n => Math.Abs(n.X - x) < .001)) _profil.Add(new(x, o.KotM));
        }
        _profil.Sort((a, b) => a.X.CompareTo(b.X)); InvalidateVisual();
    }
    public void NoktaDuzenle(int index, double x, double z)
    {
        if (index < 0 || index >= _profil.Count) return;
        double lo = index == 0 ? 0 : _profil[index - 1].X + .01, hi = index == _profil.Count - 1 ? Length : _profil[index + 1].X - .01;
        if (index == 0) x = 0; if (index == _profil.Count - 1) x = Length;
        if (x < lo || x > hi || z <= 0) throw new InvalidOperationException("Nokta komşu kırılımların arasında, kot ise sıfırdan büyük olmalı.");
        _profil[index] = new(x, z); Uygula();
    }
    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(Brush.Parse("#0B1420"), Bounds);
        if (Salon == null) return;
        if (_drag < 0) _maxZ = Math.Max(5, Math.Max(Salon.SalonYuksekligiM, _profil.Select(p => p.Y).DefaultIfEmpty(0).Max()) + 2);
        ctx.DrawText(new FormattedText("Kesit · tavana çift tık: kırılım ekle · noktayı sürükle: kot değiştir", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 13, Brushes.LightBlue), new Point(12, 12));
        var floor = new Pen(Brushes.SlateGray, 1); ctx.DrawLine(floor, P(0, 0), P(Length, 0));
        double ux = (B.X - A.X) / Length, uy = (B.Y - A.Y) / Length;
        var shell = SalonTasarimServisi.Kabuk(Salon); var panels = SalonTasarimServisi.Paneller(Salon, shell);
        for (int i = 0; i < 300; i++)
        {
            double x = Length * i / 300, x2 = Length * (i + 1) / 300; var p = new Point2D(A.X + ux * x, A.Y + uy * x); var q = new Point2D(A.X + ux * x2, A.Y + uy * x2);
            if (shell.Count(r => SalonTasarimServisi.Icinde(p, r)) % 2 == 0) continue;
            ctx.DrawLine(new Pen(Brushes.MediumPurple, 2), P(x, SalonTasarimServisi.TavanKotu(Salon, p)), P(x2, SalonTasarimServisi.TavanKotu(Salon, q)));
            foreach (var panel in panels.Where(r => SalonTasarimServisi.Icinde(p, r.Plan)))
            {
                var top = P(x, panel.Kot(p)); var bottom = P(x, panel.Alt); double height = Math.Abs(top.Y - bottom.Y);
                if (height > .1) ctx.FillRectangle(new SolidColorBrush(Colors.MediumSeaGreen, .18), new Rect(top.X, Math.Min(top.Y, bottom.Y), Math.Max(1, (x2 - x) * Scale), height));
                ctx.DrawLine(new Pen(Brushes.LightGreen, 2), top, P(x2, panel.Kot(q)));
                if (panel.Alt > 0) ctx.DrawLine(floor, bottom, P(x2, panel.Alt));
            }
        }
        foreach (var p in _profil) { var pt = P(p.X, p.Y); ctx.DrawEllipse(Brushes.Orange, null, pt, 6, 6); ctx.DrawText(new FormattedText($"{p.Y:0.##} m", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 12, Brushes.Orange), pt + new Vector(8, -18)); }
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (Salon == null) return; var pos = e.GetPosition(this);
        for (int i = 0; i < _profil.Count; i++) if (Math.Sqrt(Math.Pow(P(_profil[i].X, _profil[i].Y).X - pos.X, 2) + Math.Pow(P(_profil[i].X, _profil[i].Y).Y - pos.Y, 2)) < 12) { Degisecek?.Invoke(); _drag = i; e.Pointer.Capture(this); NoktaSecildi?.Invoke(i); return; }
        if (e.ClickCount == 2) { double x = Math.Clamp((pos.X - 45) / Scale, 0, Length), z = Math.Max(.1, (Bounds.Height - 40 - pos.Y) / Scale - 2); if (_profil.Any(p => Math.Abs(p.X - x) < .05)) return; Degisecek?.Invoke(); _profil.Add(new(x, z)); _profil.Sort((a, b) => a.X.CompareTo(b.X)); Uygula(); }
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    { if (_drag < 0) return; var pos = e.GetPosition(this); double z = Math.Max(.1, (Bounds.Height - 40 - pos.Y) / Scale - 2); _profil[_drag] = new(_profil[_drag].X, Math.Round(z, 2)); Uygula(); }
    protected override void OnPointerReleased(PointerReleasedEventArgs e) { int i = _drag; _drag = -1; e.Pointer.Capture(null); if (i >= 0) NoktaSecildi?.Invoke(i); }
    private void Uygula()
    {
        if (Salon == null || _profil.Count < 2) return;
        double ux = (B.X - A.X) / Length, uy = (B.Y - A.Y) / Length;
        var points = Salon.CizimOgeleri.Where(o => o.Islev == SalonIslevi.Sinir).SelectMany(o => o.KapaliPoligon()).ToList(); if (points.Count == 0) return;
        double lo = points.Min(p => (p.X - A.X) * (-uy) + (p.Y - A.Y) * ux) - 1, hi = points.Max(p => (p.X - A.X) * (-uy) + (p.Y - A.Y) * ux) + 1;
        foreach (var old in Salon.CizimOgeleri.Where(o => o.Islev == SalonIslevi.Tavan && o.KesitGrubu == Grup).ToList()) Salon.CizimOgeleri.Remove(old);
        Point2D W(double x, double y) => new(A.X + ux * x - uy * y, A.Y + uy * x + ux * y);
        for (int i = 0; i + 1 < _profil.Count; i++)
        { var a = _profil[i]; var b = _profil[i + 1]; double slope = (b.Y - a.Y) / (b.X - a.X); var o = new SalonCizimOgesi { Ad = $"Kesit tavanı {i + 1}", KesitGrubu = Grup, Islev = SalonIslevi.Tavan, Tip = SalonCizimTipi.Polyline, Kapali = true, Noktalar = new() { W(a.X, lo), W(b.X, lo), W(b.X, hi), W(a.X, hi) }, KotM = a.Y, KotReferansi = W(a.X, 0), EgimX = ux * slope, EgimY = uy * slope, KatmanId = Salon.Katmanlar.FirstOrDefault(k => k.Tip == SalonKatmanTipi.Tavan)?.Id ?? "" }; o.KenarlariHazirla(); Salon.CizimOgeleri.Add(o); }
        Degisti?.Invoke(); InvalidateVisual();
    }
}
