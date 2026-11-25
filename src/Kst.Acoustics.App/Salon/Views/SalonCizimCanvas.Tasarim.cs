using Avalonia;
using Avalonia.Media;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Salon.Models;
using Kst.Acoustics.Core.Salon.Services;

namespace Kst.Acoustics.App.Salon.Views;

public sealed partial class SalonCizimCanvas
{
    public Action<SalonCizimOgesi>? OgeHazirla { get; set; }
    public Action<Point2D, Point2D>? KesitSecildi { get; set; }
    public Dictionary<string, List<Point2D>> Koltuklar { get; set; } = new();
    public bool NesneYakalama => _snapAcik;
    public bool IzgaraYakalama => _gridSnap;
    public bool AciYakalama => _polar;
    public Action? YakalamaDegisti { get; set; }
    public bool SinirKavisModu { get; set; }
    private readonly List<double> _cizimKavisleri = new();
    private Point2D? _yayBitisi;
    private SalonCizimOgesi? _dragOge;
    private int _dragKenar = -1;
    private Point2D _dragBas;
    private bool _dragSnapshot;

    public void Sec(SalonCizimOgesi? o)
    { CizimiIptal(); _secim.Clear(); if (o != null) _secim.Add(o); _sonVertex = -1; OnSecimDegisti?.Invoke(_secim); InvalidateVisual(); }
    public void Odaklan(SalonCizimOgesi? o)
    {
        if (o == null) { Sigdir(); return; }
        var pts = o.Segmentler().SelectMany(e => new[] { e.A, e.B }).ToList(); if (pts.Count == 0) return;
        double minX = pts.Min(p => p.X), maxX = pts.Max(p => p.X), minY = pts.Min(p => p.Y), maxY = pts.Max(p => p.Y);
        _zoom = Math.Clamp(Math.Min((Bounds.Width - 100) / Math.Max(2, maxX - minX), (Bounds.Height - 100) / Math.Max(2, maxY - minY)) / _basePxPerM, MinZoom, MaxZoom);
        _pan = new(Bounds.Width / 2 - (minX + maxX) / 2 * Scale, Bounds.Height / 2 + (minY + maxY) / 2 * Scale); _panInit = true; InvalidateVisual();
    }
    public void SiniriKapat()
    {
        if (_cizimNoktalar.Count < 3) { BildirDurum("Sınır için en az üç nokta gerekli."); return; }
        OgeEkle(new() { Tip = SalonCizimTipi.Polyline, Kapali = true, Noktalar = _cizimNoktalar.ToList(), KenarKavisleri = _cizimKavisleri.ToList() }); CizimiIptal();
    }
    public void SecimiDegistir(Action<SalonCizimOgesi> change)
    { if (_secim.Count == 0) return; OnDegisecek?.Invoke(); foreach (var o in _secim) change(o); OnDegisti?.Invoke(); OnSecimDegisti?.Invoke(_secim); InvalidateVisual(); }
    public void SecimiTasi(double dx, double dy, bool copy = false) => TasiUygula(dx, dy, copy);
    public void SecimiDondur(double angle)
    { if (Birincil == null) return; _cizimNoktalar.Clear(); _cizimNoktalar.Add(Merkez(Birincil)); DondurUygula(angle, false); _cizimNoktalar.Clear(); InvalidateVisual(); }
    public static Point2D Merkez(SalonCizimOgesi o) => o.Noktalar.Count == 0 ? default : new(o.Noktalar.Average(p => p.X), o.Noktalar.Average(p => p.Y));
    private List<SalonCizimOgesi> DonusumSecimi()
    {
        var list = _secim.ToList(); if (_salon == null) return list;
        for (int i = 0; i < list.Count; i++) foreach (var child in _salon.CizimOgeleri.Where(o => o.UstNesneId == list[i].Id)) if (!list.Contains(child)) list.Add(child);
        return list;
    }
    private List<SalonCizimOgesi> KopyaSecimi()
    {
        var source = DonusumSecimi(); var copies = source.Select(SalonDonusumServisi.Kopyala).ToList();
        for (int i = 0; i < source.Count; i++)
        {
            if (_salon != null) foreach (var ac in _salon.Acikliklar.Where(a => a.DuvarId == source[i].Id).ToList())
            {
                int edge = source[i].KenarKimlikleri.IndexOf(ac.KenarId ?? ""); if (edge < 0) continue;
                var copy = System.Text.Json.JsonSerializer.Deserialize<DuvarAcikligi>(System.Text.Json.JsonSerializer.Serialize(ac))!;
                copy.Id = Guid.NewGuid().ToString("N"); copy.DuvarId = copies[i].Id; copy.KenarId = copies[i].KenarKimlikleri[edge]; _salon.Acikliklar.Add(copy);
            }
            int parent = source.FindIndex(o => o.Id == source[i].UstNesneId); if (parent >= 0) copies[i].UstNesneId = copies[parent].Id;
        }
        return copies;
    }
    private bool KenarSurukleBasla(SalonCizimOgesi o, Point pos)
    {
        if (o.Tip is SalonCizimTipi.Daire or SalonCizimTipi.Yay) return false;
        for (int i = 0; i < (o.Kapali ? o.Noktalar.Count : o.Noktalar.Count - 1); i++)
            if (NoktaKenarMesafe(pos, W2S(o.Noktalar[i]), W2S(o.Noktalar[(i + 1) % o.Noktalar.Count])) < HitPx)
            { _dragOge = o; _dragKenar = i; _dragBas = S2W(pos); _dragSnapshot = false; return true; }
        return false;
    }
    private bool NesneSurukle(Point pos)
    {
        if (_dragOge == null) return false; var p = S2W(pos); double dx = p.X - _dragBas.X, dy = p.Y - _dragBas.Y;
        if (Math.Sqrt(dx * dx + dy * dy) * Scale < 1) return true;
        if (!_dragSnapshot) { OnDegisecek?.Invoke(); _dragSnapshot = true; }
        if (_dragKenar >= 0)
        {
            var a = _dragOge.Noktalar[_dragKenar]; int next = (_dragKenar + 1) % _dragOge.Noktalar.Count; var b = _dragOge.Noktalar[next];
            double nx = -(b.Y - a.Y), ny = b.X - a.X, len = nx * nx + ny * ny; if (len > 1e-9) { double d = (dx * nx + dy * ny) / len; _dragOge.Noktalar[_dragKenar] = new(a.X + d * nx, a.Y + d * ny); _dragOge.Noktalar[next] = new(b.X + d * nx, b.Y + d * ny); }
        }
        else foreach (var o in DonusumSecimi()) SalonDonusumServisi.Tasi(o, dx, dy);
        _dragBas = p; OnDegisti?.Invoke(); InvalidateVisual(); return true;
    }
    private void DrawSeyirciNesnesi(DrawingContext ctx, SalonCizimOgesi o)
    {
        if (Koltuklar.TryGetValue(o.Id, out var seats)) foreach (var seat in seats)
        { var pt = W2S(seat); double radius = Math.Clamp(o.KoltukGenisligiM * Scale * .35, 1, 9); if (pt.X >= 0 && pt.Y >= 0 && pt.X <= Bounds.Width && pt.Y <= Bounds.Height) ctx.DrawEllipse(SeyirciFill, SeyirciPen, pt, radius, radius); }
        var c = Merkez(o); double angle = o.YonDerece * Math.PI / 180;
        var a = W2S(c); var b = new Point(a.X - Math.Cos(angle) * 28, a.Y + Math.Sin(angle) * 28);
        ctx.DrawLine(SeyirciPen, a, b); ctx.DrawEllipse(SeyirciFill, SeyirciPen, b, 3, 3);
    }
}
