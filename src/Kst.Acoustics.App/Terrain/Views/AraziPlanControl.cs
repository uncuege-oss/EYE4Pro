using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Terrain.Models;
using Kst.Acoustics.Core.Terrain.Services;

namespace Kst.Acoustics.App.Terrain.Views;

/// <summary>Metre koordinatlarında kot planı, bölge seçimi ve platform sınırı düzenleme.</summary>
public sealed class AraziPlanControl : Control
{
    private AraziYuzeyi? _yuzey;
    private double _minX, _maxX, _minY, _maxY, _minZ, _maxZ, _zoom = 1;
    private Vector _pan;
    private Point? _drag;
    private Point2D? _hoverWorld;
    private AraziKotIndeksi? _kotIndeksi;
    private readonly List<(StreamGeometry Geometry, IBrush Brush)> _geometriler = new();
    private TasarimYuzeyi? _tasarim;
    private Func<double, double, double?>? _hedefKotSorgula;
    private bool _renkHaritasi;
    private bool _tasarimGorunumu;
    private string? _seciliBolgeId;
    private int _seciliKenarIndex = -1;
    private int _dragVertex = -1;
    private string? _dragBolgeId;
    private List<Point2D>? _dragPoligon;
    private Point _dragVertexBaslangic;
    private bool _vertexMoved;
    private static readonly IBrush DikKenarFircasi = Brush.Parse("#F0A35B");
    private static readonly IBrush SevKenarFircasi = Brush.Parse("#61DAB4");

    public TasarimYuzeyi? Tasarim
    {
        get => _tasarim;
        set { _tasarim = value; OnizlemeyiGuncelle(); }
    }
    public bool RenkHaritasi
    {
        get => _renkHaritasi;
        set { if (_renkHaritasi == value) return; _renkHaritasi = value; OnizlemeyiGuncelle(); }
    }
    public bool TasarimGorunumu
    {
        get => _tasarimGorunumu;
        set { if (_tasarimGorunumu == value) return; _tasarimGorunumu = value; OnizlemeyiGuncelle(); }
    }
    /// <summary>Yalnız etkilenen noktaların hedef kotunu döndürür; etkilenmeyen yerde null.</summary>
    public Func<double, double, double?>? HedefKotSorgula
    {
        get => _hedefKotSorgula;
        set { _hedefKotSorgula = value; OnizlemeyiGuncelle(); }
    }
    public string? SeciliBolgeId
    {
        get => _seciliBolgeId;
        set { if (_seciliBolgeId == value) return; _seciliBolgeId = value; _seciliKenarIndex = -1; InvalidateVisual(); }
    }
    public int SeciliKenarIndex
    {
        get => _seciliKenarIndex;
        set { _seciliKenarIndex = value; InvalidateVisual(); }
    }
    public bool CizimModu { get; private set; }
    public bool DikdortgenModu { get; private set; }
    public bool KotSecimModu { get; private set; }
    public List<Point2D> CizimNoktalari { get; } = new();
    public Action<string>? KonumDegisti { get; set; }
    public Action? CizimDegisti { get; set; }
    public Action<string>? BolgeSecildi { get; set; }
    public Action<string, int>? KenarSecildi { get; set; }
    /// <summary>Sürükleme sonunda yeni sınırı bildirir; model yalnız ev sahibi doğrulayıp kabul ederse değişir.</summary>
    public Action<string, List<Point2D>>? SinirDegisti { get; set; }
    public Action<double>? KotSecildi { get; set; }

    public AraziPlanControl() { ClipToBounds = true; Focusable = true; }

    public void Yukle(AraziYuzeyi yuzey)
    {
        ArgumentNullException.ThrowIfNull(yuzey);
        _yuzey = yuzey;
        _kotIndeksi = new AraziKotIndeksi(yuzey);
        _geometriler.Clear();
        if (yuzey.Dugumler.Count == 0)
        {
            _minX = _minY = _minZ = 0; _maxX = _maxY = 1; _maxZ = 0;
        }
        else
        {
            _minX = yuzey.Dugumler.Min(p => p.X); _maxX = yuzey.Dugumler.Max(p => p.X);
            _minY = yuzey.Dugumler.Min(p => p.Y); _maxY = yuzey.Dugumler.Max(p => p.Y);
            _minZ = yuzey.Dugumler.Min(p => p.Z); _maxZ = yuzey.Dugumler.Max(p => p.Z);
        }
        Tasarim = null; HedefKotSorgula = null; RenkHaritasi = false; TasarimGorunumu = false;
        SeciliBolgeId = null; CizimiBitir(); Sigdir();
    }

    public void OnizlemeyiGuncelle() { _geometriler.Clear(); InvalidateVisual(); }
    public void Sigdir() { _zoom = 1; _pan = default; InvalidateVisual(); }
    public void CizimeBasla()
    {
        CizimiBitir(); CizimModu = true; DikdortgenModu = false;
        Focus(); InvalidateVisual();
    }
    public void DikdortgenCizimeBasla()
    {
        CizimeBasla(); DikdortgenModu = true;
        KonumDegisti?.Invoke("Dikdörtgenin karşılıklı iki köşesini seçin, ardından Tamamla'ya basın.");
    }
    public void KotSecimineBasla()
    {
        CizimiBitir(); KotSecimModu = true; Focus();
        KonumDegisti?.Invoke("Hedef kotu almak için mevcut arazi üzerinde bir noktaya tıklayın. Esc: iptal.");
        InvalidateVisual();
    }
    public void CizimiBitir()
    {
        CizimModu = false; DikdortgenModu = false; KotSecimModu = false;
        CizimNoktalari.Clear(); _hoverWorld = null; SuruklemeyiIptalEt(); InvalidateVisual();
    }
    public void SonNoktayiSil()
    {
        if (DikdortgenModu && CizimNoktalari.Count == 4)
            CizimNoktalari.RemoveRange(1, 3);
        else if (CizimNoktalari.Count > 0)
            CizimNoktalari.RemoveAt(CizimNoktalari.Count - 1);
        CizimDegisti?.Invoke(); InvalidateVisual();
    }
    private double Scale => Math.Max(1e-9, Math.Min(Math.Max(1, Bounds.Width - 90) / Math.Max(.01, _maxX - _minX),
        Math.Max(1, Bounds.Height - 110) / Math.Max(.01, _maxY - _minY))) * _zoom;
    private Point P(double x, double y) => new(Bounds.Width / 2 + (x - (_minX + _maxX) / 2) * Scale + _pan.X,
        Bounds.Height / 2 - (y - (_minY + _maxY) / 2) * Scale + _pan.Y);
    private Point2D W(Point p) => new((p.X - Bounds.Width / 2 - _pan.X) / Scale + (_minX + _maxX) / 2,
        -(p.Y - Bounds.Height / 2 - _pan.Y) / Scale + (_minY + _maxY) / 2);

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(Brush.Parse("#091811"), new Rect(Bounds.Size));
        if (_yuzey == null || _yuzey.Dugumler.Count == 0)
        {
            Text(ctx, "DWG / DXF yükleyin; kotlar ve arazi yüzeyi burada görünür.", new(24, 38), Brushes.LightGray);
            return;
        }
        GeometrileriHazirla();
        using (ctx.PushTransform(new Matrix(Scale, 0, 0, Scale, Bounds.Width / 2 + _pan.X, Bounds.Height / 2 + _pan.Y)))
            foreach (var (geometry, brush) in _geometriler)
                ctx.DrawGeometry(brush, _zoom > 3 ? new Pen(Brush.Parse("#385647"), .5 / Scale) : null, geometry);

        KotEtiketleriniCiz(ctx);
        if (Tasarim != null)
            for (int i = 0; i < Tasarim.Bolgeler.Count; i++) BolgeyiCiz(ctx, Tasarim.Bolgeler[i], i + 1);

        var cizim = CizimOnizlemesi();
        DrawPolygon(ctx, cizim, DikdortgenModu && cizim.Count == 4, Brushes.Cyan);
        foreach (var p in CizimNoktalari) ctx.DrawEllipse(Brushes.Cyan, null, P(p.X, p.Y), 4, 4);
        if (CizimModu && !DikdortgenModu && CizimNoktalari.Count > 0 && _hoverWorld is { } hw)
            ctx.DrawLine(new Pen(Brushes.Cyan, 1) { DashStyle = DashStyle.Dash }, P(CizimNoktalari[^1].X, CizimNoktalari[^1].Y), P(hw.X, hw.Y));
        if (KotSecimModu && _hoverWorld is { } pick)
        {
            var p = P(pick.X, pick.Y);
            ctx.DrawLine(new Pen(Brushes.Cyan, 1.5), p + new Vector(-10, 0), p + new Vector(10, 0));
            ctx.DrawLine(new Pen(Brushes.Cyan, 1.5), p + new Vector(0, -10), p + new Vector(0, 10));
        }

        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#091811"), .94), new Rect(0, 0, Bounds.Width, 38));
        string header = RenkHaritasi ? "Yarma: kırmızı · Dolgu: mavi · Etiketler: mevcut kot"
            : TasarimGorunumu ? "Tasarım renk önizlemesi · T: hedef kot · Kenarlar: turuncu dik / yeşil şev"
            : $"Kot: {_minZ:0.00} – {_maxZ:0.00} m · X → / Y ↑";
        Text(ctx, header, new(14, 12), Brushes.LightGray);
        string hint = KotSecimModu ? "Araziye tıkla: kotu al · Esc: iptal"
            : CizimModu ? (DikdortgenModu ? "Karşılıklı iki köşeyi seç · Tamamla: bölgeyi oluştur · Esc: iptal" : "Sol tık: köşe ekle · sağ tık: geri al · Tamamla: kapat · Esc: iptal")
            : "Bölge/kenara tıkla: seç · köşeyi sürükle: düzenle · boş alanda veya Shift ile sürükle: kaydır";
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#091811"), .94), new Rect(0, Math.Max(0, Bounds.Height - 34), Bounds.Width, 34));
        Text(ctx, hint, new(14, Bounds.Height - 25), Brushes.LightGray, 10);
    }

    private void KotEtiketleriniCiz(DrawingContext ctx)
    {
        if (_yuzey == null) return;
        var occupied = new HashSet<(int, int)>();
        foreach (var n in _yuzey.Dugumler)
        {
            var p = P(n.X, n.Y);
            if (p.X < 5 || p.Y < 50 || p.X > Bounds.Width - 65 || p.Y > Bounds.Height - 40) continue;
            int cellX = (int)(p.X / 65), cellY = (int)(p.Y / 22);
            bool yakin = false;
            for (int x = cellX - 1; x <= cellX + 1; x++)
            for (int y = cellY - 1; y <= cellY + 1; y++)
                if (occupied.Contains((x, y))) yakin = true;
            if (yakin) continue;
            occupied.Add((cellX, cellY));
            ctx.DrawEllipse(Brushes.White, null, p, 2, 2);
            double? target = TasarimGorunumu && !RenkHaritasi
                ? (HedefKotSorgula != null ? HedefKotSorgula(n.X, n.Y) : Tasarim?.HedefKot(n.X, n.Y)) : null;
            Text(ctx, target.HasValue ? $"T {target.Value:0.00}" : $"{n.Z:0.00}", p + new Vector(5, -16), Brushes.White, 10);
        }
    }

    private void BolgeyiCiz(DrawingContext ctx, TasarimBolgesi bolge, int number)
    {
        var points = _dragBolgeId == bolge.Id && _dragPoligon != null ? _dragPoligon : bolge.Poligon;
        if (points.Count < 2) return;
        bool selected = SeciliBolgeId == bolge.Id;
        var g = new StreamGeometry();
        using (var gc = g.Open())
        {
            gc.BeginFigure(P(points[0].X, points[0].Y), true);
            for (int i = 1; i < points.Count; i++) gc.LineTo(P(points[i].X, points[i].Y));
            gc.EndFigure(true);
        }
        ctx.DrawGeometry(new SolidColorBrush(selected ? Colors.Gold : Colors.SkyBlue, selected ? .16 : .08), null, g);
        for (int i = 0; i < points.Count; i++)
        {
            var a = P(points[i].X, points[i].Y); var b = P(points[(i + 1) % points.Count].X, points[(i + 1) % points.Count].Y);
            bool edgeSelected = selected && SeciliKenarIndex == i;
            bool sev = bolge.KenarTipi(i) == TasarimKenarTipi.Sev;
            if (edgeSelected) ctx.DrawLine(new Pen(Brushes.White, 6), a, b);
            var pen = new Pen(sev ? SevKenarFircasi : DikKenarFircasi, selected ? 3 : 2);
            if (sev) pen.DashStyle = DashStyle.Dash;
            ctx.DrawLine(pen, a, b);
            if (selected)
            {
                var middle = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                var label = $"{i + 1}";
                ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#10251B")), new Pen(edgeSelected ? Brushes.White : pen.Brush, 1), middle, 9, 9);
                Text(ctx, label, middle + new Vector(label.Length > 1 ? -6 : -3, -6), Brushes.White, 10);
            }
        }
        if (selected)
            foreach (var p in points)
                ctx.DrawRectangle(Brushes.Gold, new Pen(Brushes.Black, 1), new Rect(P(p.X, p.Y) - new Vector(4, 4), new Size(8, 8)));
        // Ortalama poligon dışında kalırsa etiket ilk kenarın ortasına taşınır.
        var labelWorld = new Point2D(points.Average(p => p.X), points.Average(p => p.Y));
        if (!PoligonYardimci.IsPointInside(labelWorld, points))
            labelWorld = new Point2D((points[0].X + points[1].X) / 2, (points[0].Y + points[1].Y) / 2);
        var pos = P(labelWorld.X, labelWorld.Y);
        string title = $"{number}. {(string.IsNullOrWhiteSpace(bolge.Ad) ? "Bölge" : bolge.Ad)} · {bolge.HedefKotM:0.00} m";
        var ft = new FormattedText(title, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 11, selected ? Brushes.Gold : Brushes.White);
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#07150F"), .94), new Rect(pos.X - ft.Width / 2 - 5, pos.Y - 10, ft.Width + 10, 21));
        ctx.DrawText(ft, new Point(pos.X - ft.Width / 2, pos.Y - 7));
    }

    private IReadOnlyList<Point2D> CizimOnizlemesi()
    {
        if (DikdortgenModu && CizimNoktalari.Count == 1 && _hoverWorld is { } hover)
            return Dikdortgen(CizimNoktalari[0], hover);
        return CizimNoktalari;
    }
    private static List<Point2D> Dikdortgen(Point2D a, Point2D b) => [a, new(b.X, a.Y), b, new(a.X, b.Y)];
    private double Seviye(double z) => Math.Clamp((z - _minZ) / Math.Max(.001, _maxZ - _minZ), 0, 1);
    private void GeometrileriHazirla()
    {
        if (_yuzey == null || _geometriler.Count > 0) return;
        var gruplar = new Dictionary<int, List<AraziUcgen>>();
        foreach (var t in _yuzey.Ucgenler)
        {
            var a = _yuzey.Dugumler[t.A]; var b = _yuzey.Dugumler[t.B]; var c = _yuzey.Dugumler[t.C];
            double z = (a.Z + b.Z + c.Z) / 3;
            double cx = (a.X + b.X + c.X) / 3, cy = (a.Y + b.Y + c.Y) / 3;
            double? hedef = RenkHaritasi || TasarimGorunumu ? (HedefKotSorgula != null ? HedefKotSorgula(cx, cy) : Tasarim?.HedefKot(cx, cy)) : null;
            int grup = RenkHaritasi && hedef.HasValue ? (z - hedef.Value > .001 ? 100 : z - hedef.Value < -.001 ? 101 : 102)
                : (int)(Seviye(TasarimGorunumu ? hedef ?? z : z) * 31);
            if (!gruplar.TryGetValue(grup, out var list)) gruplar[grup] = list = new();
            list.Add(t);
        }
        Point Local(AraziDugum n) => new(n.X - (_minX + _maxX) / 2, -(n.Y - (_minY + _maxY) / 2));
        foreach (var (grup, triangles) in gruplar)
        {
            double seviye = grup / 31.0;
            var color = grup switch
            {
                100 => Color.Parse("#B85842"), 101 => Color.Parse("#387CB6"), 102 => Color.Parse("#65806A"),
                _ => Color.FromRgb((byte)(45 + 105 * seviye), (byte)(95 + 60 * seviye), (byte)(76 - 30 * seviye))
            };
            var g = new StreamGeometry();
            using (var gc = g.Open())
                foreach (var t in triangles)
                {
                    gc.BeginFigure(Local(_yuzey.Dugumler[t.A]), true);
                    gc.LineTo(Local(_yuzey.Dugumler[t.B])); gc.LineTo(Local(_yuzey.Dugumler[t.C])); gc.EndFigure(true);
                }
            _geometriler.Add((g, new SolidColorBrush(color)));
        }
    }
    private void DrawPolygon(DrawingContext ctx, IReadOnlyList<Point2D> points, bool close, IBrush brush)
    {
        var pen = new Pen(brush, 2.5);
        for (int i = 1; i < points.Count; i++) ctx.DrawLine(pen, P(points[i - 1].X, points[i - 1].Y), P(points[i].X, points[i].Y));
        if (close && points.Count > 2) ctx.DrawLine(pen, P(points[^1].X, points[^1].Y), P(points[0].X, points[0].Y));
    }
    private static void Text(DrawingContext ctx, string s, Point p, IBrush b, double size = 12) =>
        ctx.DrawText(new FormattedText(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, size, b), p);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_yuzey == null) return;
        Focus();
        var prop = e.GetCurrentPoint(this).Properties;
        var pos = e.GetPosition(this);
        if (prop.IsMiddleButtonPressed || (prop.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Shift)))
        { _drag = pos; e.Pointer.Capture(this); e.Handled = true; return; }
        if (CizimModu && prop.IsRightButtonPressed) { SonNoktayiSil(); e.Handled = true; return; }
        if (KotSecimModu && prop.IsRightButtonPressed) { CizimiBitir(); e.Handled = true; return; }
        if (!prop.IsLeftButtonPressed) return;
        if (KotSecimModu)
        {
            var p = W(pos); var z = _kotIndeksi?.KotSorgula(p.X, p.Y);
            if (z.HasValue) { KotSecimModu = false; KotSecildi?.Invoke(z.Value); InvalidateVisual(); }
            else KonumDegisti?.Invoke("Kot seçmek için ölçülmüş arazi yüzeyi içine tıklayın.");
            e.Handled = true; return;
        }
        if (CizimModu)
        {
            CizimNoktasiEkle(W(pos)); e.Handled = true; return;
        }
        var selected = Tasarim?.Bolgeler.FirstOrDefault(b => b.Id == SeciliBolgeId);
        if (selected != null)
            for (int i = 0; i < selected.Poligon.Count; i++)
                if (Mesafe(P(selected.Poligon[i].X, selected.Poligon[i].Y), pos) <= 9)
                {
                    _dragBolgeId = selected.Id; _dragVertex = i; _dragPoligon = selected.Poligon.ToList();
                    _dragVertexBaslangic = pos; _vertexMoved = false; e.Pointer.Capture(this); e.Handled = true; return;
                }
        if (KenarBul(pos) is { } edge)
        {
            SeciliBolgeId = edge.Bolge.Id; SeciliKenarIndex = edge.Kenar;
            KenarSecildi?.Invoke(edge.Bolge.Id, edge.Kenar);
            InvalidateVisual(); e.Handled = true; return;
        }
        var world = W(pos);
        var region = Tasarim?.Bolgeler.FirstOrDefault(b => b.Poligon.Count >= 3 && PoligonYardimci.IsPointInside(world, b.Poligon));
        if (region != null)
        {
            SeciliBolgeId = region.Id; SeciliKenarIndex = -1; BolgeSecildi?.Invoke(region.Id);
            InvalidateVisual(); e.Handled = true; return;
        }
        _drag = pos; e.Pointer.Capture(this); e.Handled = true;
    }

    private void CizimNoktasiEkle(Point2D p)
    {
        if (_kotIndeksi?.KotSorgula(p.X, p.Y) == null)
        { KonumDegisti?.Invoke("Platform köşesi ölçülmüş arazi yüzeyi içinde olmalı."); return; }
        if (DikdortgenModu)
        {
            if (CizimNoktalari.Count == 0) CizimNoktalari.Add(p);
            else if (CizimNoktalari.Count == 1)
            {
                var corners = Dikdortgen(CizimNoktalari[0], p);
                if (Math.Abs(corners[0].X - p.X) < 1e-6 || Math.Abs(corners[0].Y - p.Y) < 1e-6)
                { KonumDegisti?.Invoke("Dikdörtgenin eni ve boyu sıfırdan büyük olmalı."); return; }
                if (corners.Any(c => _kotIndeksi?.KotSorgula(c.X, c.Y) == null))
                { KonumDegisti?.Invoke("Dikdörtgenin dört köşesi de ölçülmüş arazi içinde olmalı."); return; }
                CizimNoktalari.Clear(); CizimNoktalari.AddRange(corners);
            }
            else { KonumDegisti?.Invoke("Dikdörtgen hazır. Tamamla ile oluşturun veya sağ tıkla ikinci köşeyi geri alın."); return; }
        }
        else if (CizimNoktalari.Count == 0 || Math.Abs(CizimNoktalari[^1].X - p.X) + Math.Abs(CizimNoktalari[^1].Y - p.Y) > .001)
            CizimNoktalari.Add(p);
        CizimDegisti?.Invoke(); InvalidateVisual();
    }

    private (TasarimBolgesi Bolge, int Kenar)? KenarBul(Point pos)
    {
        if (Tasarim == null) return null;
        (TasarimBolgesi, int)? result = null;
        double best = 9;
        foreach (var b in Tasarim.Bolgeler.OrderByDescending(b => b.Id == SeciliBolgeId))
            for (int i = 0; i < b.Poligon.Count; i++)
            {
                var a = b.Poligon[i]; var c = b.Poligon[(i + 1) % b.Poligon.Count];
                double d = SegmentMesafesi(pos, P(a.X, a.Y), P(c.X, c.Y));
                if (d < best) { best = d; result = (b, i); }
            }
        return result;
    }
    private static double SegmentMesafesi(Point p, Point a, Point b)
    {
        var ab = b - a;
        double length2 = ab.X * ab.X + ab.Y * ab.Y;
        if (length2 < 1e-12) return Mesafe(p, a);
        double t = Math.Clamp(((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y) / length2, 0, 1);
        return Mesafe(p, a + ab * t);
    }
    private static double Mesafe(Point a, Point b) => double.Hypot(a.X - b.X, a.Y - b.Y);

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        if (_drag.HasValue) { _pan += p - _drag.Value; _drag = p; InvalidateVisual(); }
        if (_yuzey == null) return;
        var w = W(p); var z = _kotIndeksi?.KotSorgula(w.X, w.Y);
        _hoverWorld = w;
        if (_dragVertex >= 0 && _dragPoligon != null)
        {
            _dragPoligon[_dragVertex] = w;
            _vertexMoved |= Mesafe(p, _dragVertexBaslangic) > 2;
            InvalidateVisual();
        }
        else if (CizimModu || KotSecimModu) InvalidateVisual();
        string kot = z.HasValue ? $"Mevcut {z.Value:0.00} m" : "Yüzey dışında";
        if (z.HasValue && !CizimModu && !KotSecimModu && HedefKotSorgula?.Invoke(w.X, w.Y) is double hedef)
            kot += $" · Hedef {hedef:0.00} m · Fark {z.Value - hedef:+0.00;-0.00;0.00} m";
        KonumDegisti?.Invoke($"X {w.X:0.00}   Y {w.Y:0.00}   {kot}");
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        string? id = _dragBolgeId;
        var polygon = _dragPoligon;
        bool commit = _dragVertex >= 0 && _vertexMoved && id != null && polygon != null;
        SuruklemeyiIptalEt(); e.Pointer.Capture(null);
        if (commit)
        {
            try { SinirDegisti?.Invoke(id!, polygon!); }
            catch (Exception ex) { KonumDegisti?.Invoke("Sınır uygulanamadı: " + ex.Message); }
        }
        InvalidateVisual();
    }
    private void SuruklemeyiIptalEt()
    {
        _drag = null; _dragVertex = -1; _dragBolgeId = null; _dragPoligon = null; _vertexMoved = false;
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    { base.OnPointerCaptureLost(e); SuruklemeyiIptalEt(); InvalidateVisual(); }
    protected override void OnPointerExited(PointerEventArgs e)
    { base.OnPointerExited(e); if (_dragVertex < 0) _hoverWorld = null; if (CizimModu || KotSecimModu) InvalidateVisual(); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            bool aktif = CizimModu || KotSecimModu || _dragVertex >= 0;
            CizimiBitir();
            if (aktif) { CizimDegisti?.Invoke(); KonumDegisti?.Invoke("Çizim / kot seçimi iptal edildi."); e.Handled = true; }
        }
        else if (CizimModu && (e.Key == Key.Back || e.Key == Key.Delete))
        { SonNoktayiSil(); e.Handled = true; }
    }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (_yuzey == null) return;
        var p = e.GetPosition(this); var w = W(p);
        _zoom = Math.Clamp(_zoom * Math.Pow(1.2, e.Delta.Y), .25, 40);
        _pan += p - P(w.X, w.Y); InvalidateVisual(); e.Handled = true;
    }
}
