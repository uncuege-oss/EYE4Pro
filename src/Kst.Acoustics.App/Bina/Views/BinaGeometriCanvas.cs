using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.App.Bina.Views;

/// M50 bina geometrisi 2D plan viewport'u.
/// BinaKat doğrudan alır; DXF arka plan + oda blokları + açıklıklar + etiketler çizer.
/// Oda düzenleme: çizim modu, köşe sürükleme, kenara çift tık = köşe ekle, köşeye sağ tık = köşe sil.
public sealed class BinaGeometriCanvas : Control
{
    // ── Stiller ──────────────────────────────────────────────────────────────

    private static readonly SolidColorBrush BgBrush     = new(Color.Parse("#060E14"));
    private static readonly SolidColorBrush GridBrush   = new(Color.Parse("#0A1A24"), 0.8);
    private static readonly SolidColorBrush DxfBrush    = new(Color.Parse("#1A3A50"), 0.75);
    private static readonly SolidColorBrush OdaFill     = new(Color.Parse("#0E3A52"), 0.28);
    private static readonly SolidColorBrush OdaStroke   = new(Color.Parse("#1E6A90"), 0.90);
    private static readonly SolidColorBrush OdaSeçFill  = new(Color.Parse("#0E6090"), 0.50);
    private static readonly SolidColorBrush OdaSeçBorder= new(Color.Parse("#40C8F0"), 1.0);
    private static readonly SolidColorBrush KapiBrush   = new(Color.Parse("#F04060"));
    private static readonly SolidColorBrush PencereBrush= new(Color.Parse("#50B8F0"));
    private static readonly SolidColorBrush AciklikIptal= new(Color.Parse("#404040"), 0.60);
    private static readonly SolidColorBrush LabelBrush  = new(Color.Parse("#A0D8F4"));
    private static readonly SolidColorBrush EmptyBrush  = new(Color.Parse("#1A3A50"), 0.40);
    private static readonly SolidColorBrush RulerBrush  = new(Color.Parse("#284858"));
    private static readonly SolidColorBrush RulerText   = new(Color.Parse("#3A7898"));
    private static readonly SolidColorBrush DimensionBrush = new(Color.Parse("#E6C54A"), 0.95);
    private static readonly SolidColorBrush DimensionBack  = new(Color.Parse("#061018"), 0.86);
    private static readonly SolidColorBrush VertexFill  = new(Color.Parse("#40C8F0"));
    private static readonly SolidColorBrush VertexBorder= new(Color.Parse("#0A2030"));
    private static readonly SolidColorBrush CizimBrush  = new(Color.Parse("#2AE08A"));
    private static readonly SolidColorBrush CizimFill   = new(Color.Parse("#2AE08A"), 0.15);

    private static readonly Pen DxfPen      = new(DxfBrush, 0.8);
    private static readonly Pen OdaPen      = new(OdaStroke, 1.4);
    private static readonly Pen OdaSeçPen   = new(OdaSeçBorder, 2.0);
    private static readonly Pen KapiPen     = new(KapiBrush, 2.5);
    private static readonly Pen PencerePen  = new(PencereBrush, 2.0);
    private static readonly Pen IptalPen    = new(AciklikIptal, 1.5);
    private static readonly Pen GridPen     = new(GridBrush, 0.5);
    private static readonly Pen DimensionPen= new(DimensionBrush, 1.2);
    private static readonly Pen VertexPen   = new(VertexBorder, 1.0);
    private static readonly Pen CizimPen    = new(CizimBrush, 1.8);
    private static readonly Pen CizimOnizPen= new(CizimBrush, 1.2) { DashStyle = new DashStyle([4, 3], 0) };

    // Referans (hayalet) kat — aktif katın altında, hizalama karşılaştırması için
    private static readonly SolidColorBrush RefDxfBrush  = new(Color.Parse("#7A5418"), 0.65);
    private static readonly SolidColorBrush RefOdaStroke = new(Color.Parse("#D89030"), 0.85);
    private static readonly Pen RefDxfPen = new(RefDxfBrush, 0.8);
    private static readonly Pen RefOdaPen = new(RefOdaStroke, 1.3) { DashStyle = new DashStyle([4, 3], 0) };

    private const double Pad     = 30;
    private const double MinZoom = 0.08;
    private const double MaxZoom = 80.0;

    private const double VertexHitPx   = 8.0;   // köşe yakalama yarıçapı (ekran px)
    private const double EdgeHitPx     = 6.0;   // kenar yakalama mesafesi (ekran px)
    private const double SnapToleransM    = 0.5;  // oda köşeleri / çizim noktaları (m)
    private const double DxfSnapToleransM = 0.25; // DXF uçları — dar tolerans (m)

    // ── Avalonia properties ───────────────────────────────────────────────────

    public static readonly StyledProperty<BinaKat?> KatProperty =
        AvaloniaProperty.Register<BinaGeometriCanvas, BinaKat?>(nameof(Kat));

    public static readonly StyledProperty<string?> SeciliOdaIdProperty =
        AvaloniaProperty.Register<BinaGeometriCanvas, string?>(nameof(SeciliOdaId));

    public static readonly StyledProperty<string?> SeciliAciklikIdProperty =
        AvaloniaProperty.Register<BinaGeometriCanvas, string?>(nameof(SeciliAciklikId));

    public static readonly StyledProperty<bool> CetvelGosterProperty =
        AvaloniaProperty.Register<BinaGeometriCanvas, bool>(nameof(CetvelGoster), true);

    /// Aktif katın altına hayalet olarak çizilen referans kat (hizalama karşılaştırması için).
    /// İki katın OriginOffset farkı uygulanır; null = hayalet yok.
    public static readonly StyledProperty<BinaKat?> ReferansKatProperty =
        AvaloniaProperty.Register<BinaGeometriCanvas, BinaKat?>(nameof(ReferansKat));

    public BinaKat? Kat
    {
        get => GetValue(KatProperty);
        set => SetValue(KatProperty, value);
    }

    public string? SeciliOdaId
    {
        get => GetValue(SeciliOdaIdProperty);
        set => SetValue(SeciliOdaIdProperty, value);
    }

    public string? SeciliAciklikId
    {
        get => GetValue(SeciliAciklikIdProperty);
        set => SetValue(SeciliAciklikIdProperty, value);
    }

    public bool CetvelGoster
    {
        get => GetValue(CetvelGosterProperty);
        set => SetValue(CetvelGosterProperty, value);
    }

    public BinaKat? ReferansKat
    {
        get => GetValue(ReferansKatProperty);
        set => SetValue(ReferansKatProperty, value);
    }

    static BinaGeometriCanvas()
    {
        AffectsRender<BinaGeometriCanvas>(KatProperty, SeciliOdaIdProperty, SeciliAciklikIdProperty,
            ReferansKatProperty, CetvelGosterProperty);
        KatProperty.Changed.AddClassHandler<BinaGeometriCanvas>((c, _) => c.KatDegisti());
        ReferansKatProperty.Changed.AddClassHandler<BinaGeometriCanvas>((c, _) => c.FitAll());
    }

    // ── Viewport durumu ───────────────────────────────────────────────────────

    private double _zoom = 1.0;
    private Vector _pan;
    private bool _isPanning;
    private Point _panStart;
    private Vector _panOrigin;
    private bool _fitPending = true;

    // ── Oda çizim modu durumu ─────────────────────────────────────────────────

    private bool _odaCizimModu;
    private readonly List<Point2D> _cizimNoktalar = new();
    private Point2D? _cizimFare;

    public bool OdaCizimModu => _odaCizimModu;
    public int CizimNoktaSayisi => _cizimNoktalar.Count;

    // ── Köşe sürükleme durumu ─────────────────────────────────────────────────

    private int _dragVertexIdx = -1;
    private bool _dragSnapshotAlindi;
    private Point _dragPressScreen;

    // ── Kenar (duvar) sürükleme durumu ────────────────────────────────────────

    private int _dragEdgeIdx = -1;
    private Point2D _edgeStartWorld;
    private Point2D _edgeP0Orig;
    private Point2D _edgeP1Orig;

    // ── Manuel açıklık ekleme modu ────────────────────────────────────────────

    private bool _aciklikEkleModu;
    private BinaAciklikTipi _aciklikEkleTipi = BinaAciklikTipi.Kapi;
    private Point2D? _aciklikBas;
    private Point2D? _aciklikFare;

    public bool AciklikEkleModu => _aciklikEkleModu;

    public void AciklikEkleBaslat(BinaAciklikTipi tip)
    {
        OdaCizimIptal();
        var mod = new BinaEditorModDurumu(_odaCizimModu, _aciklikEkleModu)
            .AciklikEklemeyiBaslat();
        _odaCizimModu = mod.OdaCizim;
        _aciklikEkleModu = mod.AciklikEkle;
        _aciklikEkleTipi = tip;
        _aciklikBas = null;
        _aciklikFare = null;
        Cursor = new Cursor(StandardCursorType.Cross);
        OnAciklikEkleModuDegisti?.Invoke(true);
        InvalidateVisual();
    }

    public void AciklikEkleIptal()
    {
        _aciklikEkleModu = false;
        _aciklikBas = null;
        _aciklikFare = null;
        Cursor = Cursor.Default;
        OnAciklikEkleModuDegisti?.Invoke(false);
        InvalidateVisual();
    }

    // ── Callbacks ──────────────────────────────────────────────────────────────

    public Action<BinaOda?>? OnOdaSecildi { get; set; }
    public Action<double, double>? OnViewportClicked { get; set; }

    /// Çizim tamamlandı → poligon noktaları (dünya koordinatı). VM odayı oluşturur.
    public Action<List<Point2D>>? OnOdaCizimTamam { get; set; }
    /// Çizim modu değişti (toolbar buton görünürlüğü için).
    public Action<bool>? OnCizimModuDegisti { get; set; }
    /// Köşe sürükleme başladı (undo snapshot fırsatı).
    public Action? OnVertexTasimaBasladi { get; set; }
    /// Seçili odanın index'inci köşesi (wx, wy)'ye taşındı.
    public Action<int, double, double>? OnVertexTasindi { get; set; }
    /// Seçili odanın index kenarı iki uç noktasıyla atomik taşındı.
    public Action<int, double, double, double, double>? OnKenarTasindi { get; set; }
    /// Köşe sürükleme bitti.
    public Action? OnVertexTasimaBitti { get; set; }
    /// Seçili odanın index'inci kenarına çift tık → araya köşe ekle.
    public Action<int, double, double>? OnKenarCiftTiklandi { get; set; }
    /// Seçili odanın index'inci köşesine sağ tık → köşeyi sil.
    public Action<int>? OnVertexSilIstegi { get; set; }

    /// Açıklık çizgisine tıklama → seçim (null = seçim kalktı).
    public Action<BinaAciklik?>? OnAciklikSecildi { get; set; }
    /// Manuel açıklık tamamlandı: bas, bit (dünya koordinatı) + tip.
    public Action<Point2D, Point2D, BinaAciklikTipi>? OnAciklikEklendi { get; set; }
    /// Açıklık ekleme modu açıldı/kapandı (toolbar görünürlüğü için).
    public Action<bool>? OnAciklikEkleModuDegisti { get; set; }

    /// Oda Id → (tavan açık, döşeme açık). Düşey birleşim işaretleri için; null = işaret yok.
    public Func<string, (bool TavanAcik, bool DosemeAcik)>? OdaBirlesimSorgu { get; set; }

    // ── Çizim modu API ────────────────────────────────────────────────────────

    public void OdaCizimBaslat()
    {
        if (_aciklikEkleModu) AciklikEkleIptal();
        var mod = new BinaEditorModDurumu(_odaCizimModu, _aciklikEkleModu)
            .OdaCiziminiBaslat();
        _odaCizimModu = mod.OdaCizim;
        _aciklikEkleModu = mod.AciklikEkle;
        _cizimNoktalar.Clear();
        _cizimFare = null;
        Cursor = new Cursor(StandardCursorType.Cross);
        OnCizimModuDegisti?.Invoke(true);
        InvalidateVisual();
    }

    public void OdaCizimIptal()
    {
        _cizimNoktalar.Clear();
        _cizimFare = null;
        _odaCizimModu = false;
        Cursor = Cursor.Default;
        OnCizimModuDegisti?.Invoke(false);
        InvalidateVisual();
    }

    public void OdaCizimBitir()
    {
        if (_cizimNoktalar.Count >= 3)
            OnOdaCizimTamam?.Invoke(_cizimNoktalar.ToList());
        OdaCizimIptal();
    }

    // ── FitAll ────────────────────────────────────────────────────────────────

    public void FitAll()
    {
        _layoutGecerli = false;
        _fitPending = true;
        InvalidateVisual();
    }

    private void KatDegisti()
    {
        EtkilesimleriIptalEt();
        FitAll();
    }

    public void EtkilesimleriIptalEt()
    {
        if (_odaCizimModu || _cizimNoktalar.Count > 0) OdaCizimIptal();
        if (_aciklikEkleModu || _aciklikBas is not null) AciklikEkleIptal();
        var mod = new BinaEditorModDurumu(_odaCizimModu, _aciklikEkleModu).TumunuIptalEt();
        _odaCizimModu = mod.OdaCizim;
        _aciklikEkleModu = mod.AciklikEkle;
        _dragVertexIdx = -1;
        _dragEdgeIdx = -1;
        _dragSnapshotAlindi = false;
        _isPanning = false;
        Cursor = Cursor.Default;
        InvalidateVisual();
    }

    public void ResetViewport()
    {
        _zoom = 1.0;
        _pan = default;
        InvalidateVisual();
    }

    // ── Render ────────────────────────────────────────────────────────────────

    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);
        var bounds = Bounds;
        using var _ = ctx.PushClip(bounds);
        ctx.FillRectangle(BgBrush, bounds);

        var kat = Kat;
        var refKat = ReferansKat;
        if (refKat is not null && ReferenceEquals(refKat, kat)) refKat = null;
        bool refVar = refKat is not null &&
                      (refKat.DxfSegmentler.Count > 0 || refKat.Odalar.Count > 0);

        bool hasSegments = kat?.DxfSegmentler is { Count: > 0 };
        bool hasOdalar   = kat?.Odalar is { Count: > 0 };
        bool cizimVar    = _odaCizimModu || _cizimNoktalar.Count > 0;
        if (kat is null || (!hasSegments && !hasOdalar && !cizimVar && !refVar))
        {
            DrawEmpty(ctx, bounds);
            return;
        }

        if (bounds.Width < 4 || bounds.Height < 4) return;

        var (sc, ox, oy, minX, maxY) = GetLayout(bounds, kat);
        if (!double.IsFinite(sc) || sc <= 0) return;

        if (_fitPending)
        {
            _zoom = 1.0;
            _pan = default;
            _fitPending = false;
        }

        Point W2S(Point2D w) => W2Screen(w.X, w.Y, sc, ox, oy, minX, maxY, bounds);

        DrawGrid(ctx, bounds, sc, ox, oy, minX, maxY);

        // Referans (hayalet) kat — aktif katın altında. İki katın OriginOffset farkı uygulanır:
        // aktif kat ham yerel çerçevede çizildiği için hayalet de (refLocal + refOfset − aktifOfset).
        if (refVar && refKat is not null)
        {
            double rdx = refKat.OriginOffset.X - kat.OriginOffset.X;
            double rdy = refKat.OriginOffset.Y - kat.OriginOffset.Y;
            DrawReferansKat(ctx, refKat, rdx, rdy, sc, ox, oy, minX, maxY, bounds);
        }

        if (kat.DxfGoster)
            DrawDxfSegments(ctx, kat, sc, ox, oy, minX, maxY, bounds);
        if (kat.BloklariGoster)
        {
            DrawOdalar(ctx, kat, W2S);
            DrawAcikliklar(ctx, kat, W2S);
            if (CetvelGoster)
                DrawSelectedRoomDimensions(ctx, W2S);
            DrawVertexHandles(ctx, kat, W2S);
        }
        DrawCizim(ctx, W2S);
        if (CetvelGoster)
            DrawRuler(ctx, bounds, sc);
    }

    // ── Layout (önbellekli) ─────────────────────────────────────────────────
    //
    // BuildLayout tüm segment/oda/açıklık setini tarar; her fare hareketi + hit-test
    // bunu yeniden çağırınca büyük planlarda (200k'ya kadar segment) sürükleme takılır.
    // Sonucu önbelleğe alıyoruz: geometri YAPISAL olarak değişmediği sürece (imza aynı)
    // yeniden hesaplanmaz. Ek yan fayda: köşe sürüklerken görünüm sabit kalır (imza
    // köşe SAYISINA bağlı; koordinat değişimi imzayı değiştirmez), böylece plan
    // sürükleme sırasında kaymaz/yeniden ölçeklenmez.

    private (double sc, double ox, double oy, double minX, double maxY) _layoutCache;
    private bool _layoutGecerli;
    private Rect _layoutBounds;
    private BinaKat? _layoutKat;
    private BinaKat? _layoutRef;
    private int _layoutSig;

    private (double sc, double ox, double oy, double minX, double maxY) GetLayout(Rect bounds, BinaKat kat)
    {
        int sig = LayoutImzasi(kat);
        if (_layoutGecerli
            && _layoutBounds == bounds
            && ReferenceEquals(_layoutKat, kat)
            && ReferenceEquals(_layoutRef, ReferansKat)
            && _layoutSig == sig)
            return _layoutCache;

        _layoutCache   = BuildLayout(bounds, kat);
        _layoutGecerli = true;
        _layoutBounds  = bounds;
        _layoutKat     = kat;
        _layoutRef     = ReferansKat;
        _layoutSig     = sig;
        return _layoutCache;
    }

    /// Yapısal imza: köşe/segment/açıklık SAYILARI + ofsetler. Koordinat değişimi
    /// (köşe sürükleme) imzayı değiştirmez → sürükleme boyunca görünüm sabittir.
    private int LayoutImzasi(BinaKat kat)
    {
        int h = 17;
        h = h * 397 + kat.DxfSegmentler.Count;
        h = h * 397 + kat.Odalar.Count;
        h = h * 397 + kat.Acikliklar.Count;
        int vtx = 0;
        foreach (var o in kat.Odalar) vtx += o.Poligon.Count;
        h = h * 397 + vtx;
        h = h * 397 + kat.OriginOffset.GetHashCode();

        var r = ReferansKat;
        if (r is not null && !ReferenceEquals(r, kat))
        {
            h = h * 397 + r.DxfSegmentler.Count;
            int rv = 0;
            foreach (var o in r.Odalar) rv += o.Poligon.Count;
            h = h * 397 + rv;
            h = h * 397 + r.OriginOffset.GetHashCode();
        }
        return h;
    }

    // ── Layout ────────────────────────────────────────────────────────────────

    private (double sc, double ox, double oy, double minX, double maxY) BuildLayout(Rect bounds, BinaKat kat)
    {
        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxYv = double.MinValue;

        foreach (var s in kat.DxfSegmentler)
        {
            Expand(s.A.X, s.A.Y, ref minX, ref maxX, ref minY, ref maxYv);
            Expand(s.B.X, s.B.Y, ref minX, ref maxX, ref minY, ref maxYv);
        }
        foreach (var oda in kat.Odalar)
        {
            foreach (var p in oda.Poligon)
                Expand(p.X, p.Y, ref minX, ref maxX, ref minY, ref maxYv);
        }
        foreach (var ac in kat.Acikliklar)
        {
            Expand(ac.Bas.X, ac.Bas.Y, ref minX, ref maxX, ref minY, ref maxYv);
            Expand(ac.Bit.X, ac.Bit.Y, ref minX, ref maxX, ref minY, ref maxYv);
        }

        // Referans (hayalet) kat varsa onu da sığdır — delta ile ötelenmiş
        var refKat = ReferansKat;
        if (refKat is not null && !ReferenceEquals(refKat, kat))
        {
            double rdx = refKat.OriginOffset.X - kat.OriginOffset.X;
            double rdy = refKat.OriginOffset.Y - kat.OriginOffset.Y;
            foreach (var s in refKat.DxfSegmentler)
            {
                Expand(s.A.X + rdx, s.A.Y + rdy, ref minX, ref maxX, ref minY, ref maxYv);
                Expand(s.B.X + rdx, s.B.Y + rdy, ref minX, ref maxX, ref minY, ref maxYv);
            }
            foreach (var oda in refKat.Odalar)
                foreach (var p in oda.Poligon)
                    Expand(p.X + rdx, p.Y + rdy, ref minX, ref maxX, ref minY, ref maxYv);
        }

        if (!double.IsFinite(minX)) return (1, 0, 0, 0, 1);

        double mw = Math.Max(1e-6, maxX - minX);
        double mh = Math.Max(1e-6, maxYv - minY);
        double sc = Math.Min((bounds.Width - Pad * 2) / mw, (bounds.Height - Pad * 2) / mh);
        sc = double.IsFinite(sc) && sc > 0 ? sc : 1.0;

        double ox = Pad + ((bounds.Width  - Pad * 2) - mw * sc) / 2.0;
        double oy = Pad + ((bounds.Height - Pad * 2) - mh * sc) / 2.0;

        return (sc, ox, oy, minX, maxYv);
    }

    private static void Expand(double x, double y,
        ref double minX, ref double maxX, ref double minY, ref double maxY)
    {
        if (x < minX) minX = x; if (x > maxX) maxX = x;
        if (y < minY) minY = y; if (y > maxY) maxY = y;
    }

    // World → screen (applies zoom + pan around viewport center)
    private Point W2Screen(double wx, double wy, double sc, double ox, double oy,
        double minX, double maxY, Rect bounds)
    {
        var c = bounds.Center;
        var bx = ox + (wx - minX) * sc;
        var by = oy + (maxY - wy) * sc;
        return c + _pan + ((new Point(bx, by) - c) * _zoom);
    }

    public bool TryGetWorldPoint(Point screen, out double wx, out double wy)
    {
        wx = wy = 0;
        var kat = Kat;
        if (kat is null) return false;
        var bounds = Bounds;
        if (bounds.Width < 4 || bounds.Height < 4) return false;
        var (sc, ox, oy, minX, maxY) = GetLayout(bounds, kat);
        if (!double.IsFinite(sc) || sc <= 0) return false;

        var c = bounds.Center;
        var bx = c.X + (screen.X - c.X - _pan.X) / _zoom;
        var by = c.Y + (screen.Y - c.Y - _pan.Y) / _zoom;

        wx = minX + (bx - ox) / sc;
        wy = maxY - (by - oy) / sc;
        return true;
    }

    private BinaOda? SeciliOda()
    {
        var kat = Kat;
        var id = SeciliOdaId;
        if (kat is null || id is null) return null;
        return kat.Odalar.FirstOrDefault(o => o.Id == id);
    }

    // ── Hit test (ekran koordinatında) ────────────────────────────────────────

    /// Seçili odanın ekrandaki köşe karelerinden birine isabet → köşe index'i, yoksa -1.
    private int HitTestVertex(Point screen)
    {
        var oda = SeciliOda();
        var kat = Kat;
        if (oda is null || kat is null || !kat.BloklariGoster) return -1;
        var bounds = Bounds;
        var (sc, ox, oy, minX, maxY) = GetLayout(bounds, kat);
        if (!double.IsFinite(sc) || sc <= 0) return -1;

        for (int i = 0; i < oda.Poligon.Count; i++)
        {
            var p = W2Screen(oda.Poligon[i].X, oda.Poligon[i].Y, sc, ox, oy, minX, maxY, bounds);
            double dx = p.X - screen.X, dy = p.Y - screen.Y;
            if (dx * dx + dy * dy <= VertexHitPx * VertexHitPx) return i;
        }
        return -1;
    }

    /// Seçili odanın kenarlarından birine isabet → kenar başlangıç köşe index'i, yoksa -1.
    private int HitTestEdge(Point screen)
    {
        var oda = SeciliOda();
        var kat = Kat;
        if (oda is null || kat is null || !kat.BloklariGoster || oda.Poligon.Count < 3) return -1;
        var bounds = Bounds;
        var (sc, ox, oy, minX, maxY) = GetLayout(bounds, kat);
        if (!double.IsFinite(sc) || sc <= 0) return -1;

        for (int i = 0; i < oda.Poligon.Count; i++)
        {
            var a = W2Screen(oda.Poligon[i].X, oda.Poligon[i].Y, sc, ox, oy, minX, maxY, bounds);
            int j = (i + 1) % oda.Poligon.Count;
            var b = W2Screen(oda.Poligon[j].X, oda.Poligon[j].Y, sc, ox, oy, minX, maxY, bounds);
            if (NoktaKenarMesafe(screen, a, b) <= EdgeHitPx) return i;
        }
        return -1;
    }

    /// Açıklık çizgilerinden birine isabet → açıklık, yoksa null.
    private BinaAciklik? HitTestAciklik(Point screen)
    {
        var kat = Kat;
        if (kat is null || !kat.BloklariGoster || kat.Acikliklar.Count == 0) return null;
        var bounds = Bounds;
        var (sc, ox, oy, minX, maxY) = GetLayout(bounds, kat);
        if (!double.IsFinite(sc) || sc <= 0) return null;

        foreach (var ac in kat.Acikliklar)
        {
            var a = W2Screen(ac.Bas.X, ac.Bas.Y, sc, ox, oy, minX, maxY, bounds);
            var b = W2Screen(ac.Bit.X, ac.Bit.Y, sc, ox, oy, minX, maxY, bounds);
            if (NoktaKenarMesafe(screen, a, b) <= EdgeHitPx + 2) return ac;
        }
        return null;
    }

    private static double NoktaKenarMesafe(Point p, Point a, Point b)
    {
        double abx = b.X - a.X, aby = b.Y - a.Y;
        double len2 = abx * abx + aby * aby;
        if (len2 < 1e-9) return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
        double t = Math.Clamp(((p.X - a.X) * abx + (p.Y - a.Y) * aby) / len2, 0, 1);
        double cx = a.X + t * abx, cy = a.Y + t * aby;
        return Math.Sqrt((p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy));
    }

    private static double Distance(Point2D a, Point2D b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    // ── Magnetik snap (dünya koordinatında, M12 deseni) ──────────────────────

    /// (wx, wy)'ye en yakın snap hedefi: çizim noktaları + oda köşeleri (0.5 m), DXF uçları (0.25 m).
    /// excludeOdaId/excludeVertex: sürüklenen köşenin kendine snap olmasını önler.
    private (double X, double Y) Snap(double wx, double wy,
        string? excludeOdaId = null, int excludeVertex = -1,
        bool excludeWholeSelectedOda = false)
    {
        double bestX = wx, bestY = wy, bestD = SnapToleransM;

        void Try(double cx, double cy, double tolerans)
        {
            double d = Math.Sqrt((cx - wx) * (cx - wx) + (cy - wy) * (cy - wy));
            if (d < tolerans && d < bestD) { bestD = d; bestX = cx; bestY = cy; }
        }

        foreach (var p in _cizimNoktalar)
            Try(p.X, p.Y, SnapToleransM);

        var kat = Kat;
        if (kat is not null)
        {
            foreach (var oda in kat.Odalar)
            {
                if (excludeWholeSelectedOda && oda.Id == SeciliOdaId) continue;
                for (int i = 0; i < oda.Poligon.Count; i++)
                {
                    if (oda.Id == excludeOdaId && i == excludeVertex) continue;
                    Try(oda.Poligon[i].X, oda.Poligon[i].Y, SnapToleransM);
                }
            }
            foreach (var seg in kat.DxfSegmentler)
            {
                Try(seg.A.X, seg.A.Y, DxfSnapToleransM);
                Try(seg.B.X, seg.B.Y, DxfSnapToleransM);
            }
        }

        return (bestX, bestY);
    }

    /// Manuel açıklık noktalarını en yakın oda duvarına projekte eder.
    private (double X, double Y) SnapAciklikNoktasi(double wx, double wy)
    {
        var temel = Snap(wx, wy);
        double bestX = temel.X, bestY = temel.Y, bestD = 0.35;
        var kat = Kat;
        if (kat is null || !kat.BloklariGoster) return temel;

        foreach (var oda in kat.Odalar)
        {
            for (int i = 0; i < oda.Poligon.Count; i++)
            {
                var a = oda.Poligon[i];
                var b = oda.Poligon[(i + 1) % oda.Poligon.Count];
                double dx = b.X - a.X, dy = b.Y - a.Y;
                double len2 = dx * dx + dy * dy;
                if (len2 < 1e-12) continue;
                double t = Math.Clamp(((temel.X - a.X) * dx + (temel.Y - a.Y) * dy) / len2, 0, 1);
                double px = a.X + t * dx, py = a.Y + t * dy;
                double d = Math.Sqrt((px - temel.X) * (px - temel.X) + (py - temel.Y) * (py - temel.Y));
                if (d < bestD) { bestD = d; bestX = px; bestY = py; }
            }
        }
        return (bestX, bestY);
    }

    // ── Draw helpers ──────────────────────────────────────────────────────────

    private void DrawEmpty(DrawingContext ctx, Rect bounds)
    {
        var tf = new FormattedText("Kat seçin veya DXF yükleyin",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default, 14, EmptyBrush);
        ctx.DrawText(tf, new Point(
            (bounds.Width - tf.Width) / 2,
            (bounds.Height - tf.Height) / 2));
    }

    private void DrawGrid(DrawingContext ctx, Rect bounds, double sc,
        double ox, double oy, double minX, double maxY)
    {
        if (_zoom * sc < 10) return; // çok küçük ölçekte ızgara kalabalık yapar

        double gridM = PickGridStep(1.0 / (_zoom * sc));

        double startX = Math.Floor(minX / gridM) * gridM;
        double startY = Math.Floor((maxY - (bounds.Height / sc)) / gridM) * gridM;

        for (double gx = startX; gx < minX + bounds.Width / sc; gx += gridM)
        {
            var p0 = W2Screen(gx, maxY + 50 / sc, sc, ox, oy, minX, maxY, bounds);
            var p1 = W2Screen(gx, maxY - bounds.Height / sc - 50 / sc, sc, ox, oy, minX, maxY, bounds);
            if (p0.X < -50 || p0.X > bounds.Width + 50) continue;
            ctx.DrawLine(GridPen, p0, p1);
        }
        for (double gy = startY; gy < maxY + bounds.Height / sc; gy += gridM)
        {
            var p0 = W2Screen(minX - 50 / sc, gy, sc, ox, oy, minX, maxY, bounds);
            var p1 = W2Screen(minX + bounds.Width / sc + 50 / sc, gy, sc, ox, oy, minX, maxY, bounds);
            if (p0.Y < -50 || p0.Y > bounds.Height + 50) continue;
            ctx.DrawLine(GridPen, p0, p1);
        }
    }

    private static double PickGridStep(double worldPerPixel)
    {
        double raw = worldPerPixel * 80;
        double[] steps = [0.1, 0.25, 0.5, 1, 2, 5, 10, 20, 50];
        return steps.FirstOrDefault(s => s >= raw, 100);
    }

    /// Referans katı hayalet olarak çizer: DXF segmentleri + oda dış hatları (delta ile ötelenmiş).
    /// Etkileşim yok — yalnız hizalama karşılaştırması için görsel altlık.
    private void DrawReferansKat(DrawingContext ctx, BinaKat refKat, double dx, double dy,
        double sc, double ox, double oy, double minX, double maxY, Rect bounds)
    {
        if (refKat.DxfSegmentler.Count > 0)
        {
            var geom = new StreamGeometry();
            using (var gc = geom.Open())
            {
                foreach (var seg in refKat.DxfSegmentler)
                {
                    var a = W2Screen(seg.A.X + dx, seg.A.Y + dy, sc, ox, oy, minX, maxY, bounds);
                    var b = W2Screen(seg.B.X + dx, seg.B.Y + dy, sc, ox, oy, minX, maxY, bounds);
                    if (a.X < -200 && b.X < -200) continue;
                    if (a.X > bounds.Width + 200 && b.X > bounds.Width + 200) continue;
                    gc.BeginFigure(a, false);
                    gc.LineTo(b);
                    gc.EndFigure(false);
                }
            }
            ctx.DrawGeometry(null, RefDxfPen, geom);
        }

        foreach (var oda in refKat.Odalar)
        {
            if (oda.Poligon.Count < 3) continue;
            var geom = new StreamGeometry();
            using (var gc = geom.Open())
            {
                gc.BeginFigure(W2Screen(oda.Poligon[0].X + dx, oda.Poligon[0].Y + dy,
                    sc, ox, oy, minX, maxY, bounds), false);
                for (int i = 1; i < oda.Poligon.Count; i++)
                    gc.LineTo(W2Screen(oda.Poligon[i].X + dx, oda.Poligon[i].Y + dy,
                        sc, ox, oy, minX, maxY, bounds));
                gc.EndFigure(true);
            }
            ctx.DrawGeometry(null, RefOdaPen, geom);
        }
    }

    private void DrawDxfSegments(DrawingContext ctx, BinaKat kat, double sc,
        double ox, double oy, double minX, double maxY, Rect bounds)
    {
        if (kat.DxfSegmentler.Count == 0) return;
        var geom = new StreamGeometry();
        using (var gc = geom.Open())
        {
            foreach (var seg in kat.DxfSegmentler)
            {
                var a = W2Screen(seg.A.X, seg.A.Y, sc, ox, oy, minX, maxY, bounds);
                var b = W2Screen(seg.B.X, seg.B.Y, sc, ox, oy, minX, maxY, bounds);
                if (a.X < -200 && b.X < -200) continue;
                if (a.X > bounds.Width + 200 && b.X > bounds.Width + 200) continue;
                gc.BeginFigure(a, false);
                gc.LineTo(b);
                gc.EndFigure(false);
            }
        }
        ctx.DrawGeometry(null, DxfPen, geom);
    }

    private void DrawOdalar(DrawingContext ctx, BinaKat kat, Func<Point2D, Point> w2s)
    {
        var seciliId = SeciliOdaId;
        foreach (var oda in kat.Odalar)
        {
            if (oda.Poligon.Count < 3) continue;
            bool secili = oda.Id == seciliId;

            var geom = new StreamGeometry();
            using (var gc = geom.Open())
            {
                gc.BeginFigure(w2s(oda.Poligon[0]), true);
                for (int i = 1; i < oda.Poligon.Count; i++)
                    gc.LineTo(w2s(oda.Poligon[i]));
                gc.EndFigure(true);
            }
            ctx.DrawGeometry(secili ? OdaSeçFill : OdaFill, secili ? OdaSeçPen : OdaPen, geom);

            // Oda ismi etiketi
            if (string.IsNullOrWhiteSpace(oda.Ad)) continue;
            var merkez = w2s(oda.Merkez.X > 0 || oda.Merkez.Y > 0
                ? oda.Merkez
                : new Point2D(
                    oda.Poligon.Average(p => p.X),
                    oda.Poligon.Average(p => p.Y)));

            var tf = new FormattedText(oda.Ad,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Typeface.Default, 11, LabelBrush);
            ctx.DrawText(tf, new Point(merkez.X - tf.Width / 2, merkez.Y - tf.Height / 2));

            // Düşey birleşim işareti (⬆ tavan açık · ⬇ döşeme açık)
            if (OdaBirlesimSorgu?.Invoke(oda.Id) is { } b && (b.TavanAcik || b.DosemeAcik))
            {
                string isaret = (b.TavanAcik ? "⬆" : "") + (b.DosemeAcik ? "⬇" : "");
                var tfB = new FormattedText(isaret,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, Typeface.Default, 12,
                    new SolidColorBrush(Color.Parse("#E0C060")));
                ctx.DrawText(tfB, new Point(merkez.X - tfB.Width / 2, merkez.Y + tf.Height / 2 + 2));
            }
        }
    }

    /// Seçili odanın köşelerine sürüklenebilir kare tutamaçlar çizer.
    private void DrawVertexHandles(DrawingContext ctx, BinaKat kat, Func<Point2D, Point> w2s)
    {
        if (_odaCizimModu) return;
        var oda = SeciliOda();
        if (oda is null || oda.Poligon.Count < 3) return;

        const double h = 4.0; // yarı kenar (px)
        for (int i = 0; i < oda.Poligon.Count; i++)
        {
            var p = w2s(oda.Poligon[i]);
            var rect = new Rect(p.X - h, p.Y - h, h * 2, h * 2);
            ctx.FillRectangle(VertexFill, rect);
            ctx.DrawRectangle(VertexPen, rect);
        }
    }

    /// Çizim modundaki poligon taslağını çizer: noktalar + kenarlar + fareye önizleme çizgisi.
    private void DrawCizim(DrawingContext ctx, Func<Point2D, Point> w2s)
    {
        if (_cizimNoktalar.Count == 0) return;

        // Taslak alanı doldur (>= 3 nokta)
        if (_cizimNoktalar.Count >= 3)
        {
            var fillGeom = new StreamGeometry();
            using (var gc = fillGeom.Open())
            {
                gc.BeginFigure(w2s(_cizimNoktalar[0]), true);
                for (int i = 1; i < _cizimNoktalar.Count; i++)
                    gc.LineTo(w2s(_cizimNoktalar[i]));
                gc.EndFigure(true);
            }
            ctx.DrawGeometry(CizimFill, null, fillGeom);
        }

        // Kenarlar
        for (int i = 0; i < _cizimNoktalar.Count - 1; i++)
            ctx.DrawLine(CizimPen, w2s(_cizimNoktalar[i]), w2s(_cizimNoktalar[i + 1]));

        // Fareye önizleme (son nokta → fare; >= 3 nokta varsa fare → ilk nokta da)
        if (_odaCizimModu && _cizimFare is { } fare)
        {
            ctx.DrawLine(CizimOnizPen, w2s(_cizimNoktalar[^1]), w2s(fare));
            if (_cizimNoktalar.Count >= 2)
                ctx.DrawLine(CizimOnizPen, w2s(fare), w2s(_cizimNoktalar[0]));
        }

        // Noktalar (ilk nokta vurgulu — kapatma hedefi)
        for (int i = 0; i < _cizimNoktalar.Count; i++)
        {
            var p = w2s(_cizimNoktalar[i]);
            double r = i == 0 ? 5 : 3;
            ctx.DrawEllipse(CizimBrush, null, p, r, r);
            if (i == 0)
                ctx.DrawEllipse(null, new Pen(CizimBrush, 1.2), p, 8, 8);
        }
    }

    private void DrawAcikliklar(DrawingContext ctx, BinaKat kat, Func<Point2D, Point> w2s)
    {
        var seciliId = SeciliAciklikId;
        foreach (var ac in kat.Acikliklar)
        {
            var a = w2s(ac.Bas);
            var b = w2s(ac.Bit);
            bool secili = ac.Id == seciliId;
            Pen pen = ac.IptalEdildi ? IptalPen
                : ac.Tip == BinaAciklikTipi.Pencere ? PencerePen : KapiPen;
            if (secili)
            {
                // Seçim halkası: kalın yarı saydam arka çizgi + uç noktaları
                var brush = ac.IptalEdildi ? AciklikIptal
                    : ac.Tip == BinaAciklikTipi.Pencere ? PencereBrush : KapiBrush;
                ctx.DrawLine(new Pen(new SolidColorBrush(((SolidColorBrush)brush).Color, 0.30), 8), a, b);
                ctx.DrawEllipse(brush, null, a, 4, 4);
                ctx.DrawEllipse(brush, null, b, 4, 4);
            }
            ctx.DrawLine(pen, a, b);
        }

        // Manuel açıklık ekleme önizlemesi
        if (_aciklikEkleModu && _aciklikBas is { } bas)
        {
            var pBas = w2s(bas);
            var oniz = _aciklikEkleTipi == BinaAciklikTipi.Pencere ? PencereBrush : KapiBrush;
            ctx.DrawEllipse(oniz, null, pBas, 4, 4);
            if (_aciklikFare is { } fare)
                ctx.DrawLine(new Pen(oniz, 2) { DashStyle = new DashStyle([4, 3], 0) }, pBas, w2s(fare));
        }
    }

    private void DrawSelectedRoomDimensions(DrawingContext ctx, Func<Point2D, Point> w2s)
    {
        var oda = SeciliOda();
        if (oda is null || oda.Poligon.Count < 3) return;

        var centerWorld = new Point2D(
            oda.Poligon.Average(p => p.X),
            oda.Poligon.Average(p => p.Y));
        var center = w2s(centerWorld);

        for (int i = 0; i < oda.Poligon.Count; i++)
        {
            var aWorld = oda.Poligon[i];
            var bWorld = oda.Poligon[(i + 1) % oda.Poligon.Count];
            double lengthM = Distance(aWorld, bWorld);
            if (lengthM < 0.10) continue;

            var a = w2s(aWorld);
            var b = w2s(bWorld);
            var edge = b - a;
            double edgeLen = Math.Sqrt(edge.X * edge.X + edge.Y * edge.Y);
            if (edgeLen < 34) continue;

            var normal = new Vector(-edge.Y / edgeLen, edge.X / edgeLen);
            var mid = new Point((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5);
            var away = mid - center;
            if (away.X * normal.X + away.Y * normal.Y < 0)
                normal = -normal;

            var offset = normal * 15;
            var da = a + offset;
            var db = b + offset;
            ctx.DrawLine(DimensionPen, da, db);
            ctx.DrawLine(DimensionPen, da - normal * 4, da + normal * 4);
            ctx.DrawLine(DimensionPen, db - normal * 4, db + normal * 4);

            DrawDimensionLabel(ctx, FormatLength(lengthM), mid + offset + normal * 2);
        }
    }

    private static void DrawDimensionLabel(DrawingContext ctx, string text, Point center)
    {
        var tf = new FormattedText(text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            10,
            DimensionBrush);
        var rect = new Rect(
            center.X - tf.Width / 2 - 4,
            center.Y - tf.Height / 2 - 2,
            tf.Width + 8,
            tf.Height + 4);

        ctx.DrawRectangle(DimensionBack, new Pen(DimensionBrush, 0.6), rect, 3, 3);
        ctx.DrawText(tf, new Point(rect.X + 4, rect.Y + 2));
    }

    private static string FormatLength(double lengthM)
        => lengthM >= 1.0 ? $"{lengthM:0.##} m" : $"{lengthM * 100:0.#} cm";

    private void DrawRuler(DrawingContext ctx, Rect bounds, double sc)
    {
        double rulerLen = 140;
        double worldLen = rulerLen / (sc * _zoom);
        double step = PickGridStep(worldLen / 3);
        double displayLen = step * sc * _zoom;
        if (displayLen < 30) displayLen = Math.Min(step * 5 * sc * _zoom, rulerLen);

        double rx = bounds.Width - rulerLen - 20, ry = bounds.Height - 22;
        ctx.DrawLine(new Pen(RulerBrush, 2), new Point(rx, ry), new Point(rx + displayLen, ry));
        ctx.DrawLine(new Pen(RulerBrush, 2), new Point(rx, ry - 5), new Point(rx, ry + 5));
        ctx.DrawLine(new Pen(RulerBrush, 2), new Point(rx + displayLen, ry - 5), new Point(rx + displayLen, ry + 5));

        string label = step >= 1 ? $"{step:G} m" : $"{step * 100:G} cm";
        var tf = new FormattedText(label, System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 10, RulerText);
        ctx.DrawText(tf, new Point(rx + (displayLen - tf.Width) / 2, ry - 16));
    }

    // ── Input: pan + zoom + click + düzenleme ─────────────────────────────────

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pt = e.GetCurrentPoint(this);
        var pos = e.GetPosition(this);

        // Sağ tık → köşe sil (seçili odada, çizim modu dışında)
        if (pt.Properties.IsRightButtonPressed && !_odaCizimModu && !_aciklikEkleModu)
        {
            int vi = HitTestVertex(pos);
            if (vi >= 0)
            {
                OnVertexSilIstegi?.Invoke(vi);
                InvalidateVisual();
                e.Handled = true;
                return;
            }
        }

        if (pt.Properties.IsLeftButtonPressed)
        {
            // Normal seçim modunda açıklık, aynı duvarın kenar-sürükleme
            // hit-test'inden önce gelir.
            if (!_odaCizimModu && !_aciklikEkleModu && HitTestAciklik(pos) is { } acHit)
            {
                SetCurrentValue(SeciliAciklikIdProperty, acHit.Id);
                OnAciklikSecildi?.Invoke(acHit);
                e.Handled = true;
                return;
            }

            // Çift tık: çizimde → poligonu bitir; seçili oda kenarında → köşe ekle
            if (e.ClickCount == 2)
            {
                if (_odaCizimModu)
                {
                    OdaCizimBitir();
                    e.Handled = true;
                    return;
                }
                int ei = _aciklikEkleModu ? -1 : HitTestEdge(pos);
                if (ei >= 0 && TryGetWorldPoint(pos, out double ewx, out double ewy))
                {
                    OnKenarCiftTiklandi?.Invoke(ei, ewx, ewy);
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }
            }

            // Köşe / kenar sürükleme başlat (çizim modu dışında)
            if (!_odaCizimModu && !_aciklikEkleModu)
            {
                int vi = HitTestVertex(pos);
                if (vi >= 0)
                {
                    _dragVertexIdx = vi;
                    _dragSnapshotAlindi = false;
                    _dragPressScreen = pos;
                    e.Pointer.Capture(this);
                    e.Handled = true;
                    return;
                }

                // Köşe değilse, seçili odanın bir kenarına bastıysa duvarı taşı
                int ei = HitTestEdge(pos);
                if (ei >= 0 && SeciliOda() is { } odaE &&
                    TryGetWorldPoint(pos, out double ewx, out double ewy))
                {
                    _dragEdgeIdx    = ei;
                    _edgeStartWorld = new Point2D(ewx, ewy);
                    _edgeP0Orig     = odaE.Poligon[ei];
                    _edgeP1Orig     = odaE.Poligon[(ei + 1) % odaE.Poligon.Count];
                    _dragSnapshotAlindi = false;
                    _dragPressScreen = pos;
                    e.Pointer.Capture(this);
                    e.Handled = true;
                    return;
                }
            }
        }

        if (pt.Properties.IsMiddleButtonPressed || pt.Properties.IsLeftButtonPressed)
        {
            _isPanning = true;
            _panStart = pos;
            _panOrigin = _pan;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);

        // Köşe sürükleme
        if (_dragVertexIdx >= 0)
        {
            if (!_dragSnapshotAlindi)
            {
                var d = pos - _dragPressScreen;
                if (d.X * d.X + d.Y * d.Y <= 9) { e.Handled = true; return; }
            }
            if (TryGetWorldPoint(pos, out double wx, out double wy))
            {
                var (sx, sy) = Snap(wx, wy, SeciliOdaId, _dragVertexIdx);
                var oda = SeciliOda();
                bool gercekDegisim = oda is not null && _dragVertexIdx < oda.Poligon.Count &&
                    (Math.Abs(oda.Poligon[_dragVertexIdx].X - sx) > 1e-9 ||
                     Math.Abs(oda.Poligon[_dragVertexIdx].Y - sy) > 1e-9);
                if (MutasyonKapisi(ref _dragSnapshotAlindi, gercekDegisim, OnVertexTasimaBasladi))
                {
                    OnVertexTasindi?.Invoke(_dragVertexIdx, sx, sy);
                    InvalidateVisual();
                }
            }
            e.Handled = true;
            return;
        }

        // Kenar (duvar) sürükleme — yalnız kendi normaline (dik yönde) taşınır,
        // böylece duvar orijinal doğrultusuna PARALEL kalır; köşe açıları korunur.
        // Magnetik snap yalnız normal bileşeni olarak uygulanır (paralellik bozulmaz).
        if (_dragEdgeIdx >= 0)
        {
            if (!_dragSnapshotAlindi)
            {
                var d = pos - _dragPressScreen;
                if (d.X * d.X + d.Y * d.Y <= 9) { e.Handled = true; return; }
            }
            if (SeciliOda() is { } odaE && TryGetWorldPoint(pos, out double wx, out double wy))
            {
                int i0 = _dragEdgeIdx, i1 = (i0 + 1) % odaE.Poligon.Count;

                // Kenar normali (birim) — orijinal yönden
                double ex = _edgeP1Orig.X - _edgeP0Orig.X;
                double ey = _edgeP1Orig.Y - _edgeP0Orig.Y;
                double elen = Math.Sqrt(ex * ex + ey * ey);
                if (elen < 1e-9) { e.Handled = true; return; }
                double nx = -ey / elen, ny = ex / elen;

                // Serbest fare deltasını yalnız normale projekte et → paralel kayma
                double dx = wx - _edgeStartWorld.X, dy = wy - _edgeStartWorld.Y;
                double t = dx * nx + dy * ny;

                double c0x = _edgeP0Orig.X + t * nx, c0y = _edgeP0Orig.Y + t * ny;
                double c1x = _edgeP1Orig.X + t * nx, c1y = _edgeP1Orig.Y + t * ny;

                // Snap: aday uçların en yakın hedefe kaymasının yalnız normal bileşeni
                var s0 = Snap(c0x, c0y, excludeWholeSelectedOda: true);
                var s1 = Snap(c1x, c1y, excludeWholeSelectedOda: true);
                bool snap0 = Math.Abs(s0.X - c0x) > 1e-9 || Math.Abs(s0.Y - c0y) > 1e-9;
                bool snap1 = Math.Abs(s1.X - c1x) > 1e-9 || Math.Abs(s1.Y - c1y) > 1e-9;
                double n0 = (s0.X - c0x) * nx + (s0.Y - c0y) * ny;
                double n1 = (s1.X - c1x) * nx + (s1.Y - c1y) * ny;

                double snapN = 0;
                if (snap0 && (!snap1 || Math.Abs(n0) <= Math.Abs(n1))) snapN = n0;
                else if (snap1)                                        snapN = n1;

                double ax = snapN * nx, ay = snapN * ny;
                double y0x = c0x + ax, y0y = c0y + ay;
                double y1x = c1x + ax, y1y = c1y + ay;
                bool gercekDegisim =
                    Math.Abs(odaE.Poligon[i0].X - y0x) > 1e-9 ||
                    Math.Abs(odaE.Poligon[i0].Y - y0y) > 1e-9 ||
                    Math.Abs(odaE.Poligon[i1].X - y1x) > 1e-9 ||
                    Math.Abs(odaE.Poligon[i1].Y - y1y) > 1e-9;
                if (MutasyonKapisi(ref _dragSnapshotAlindi, gercekDegisim, OnVertexTasimaBasladi))
                {
                    if (OnKenarTasindi is not null)
                        OnKenarTasindi(i0, y0x, y0y, y1x, y1y);
                    else
                    {
                        OnVertexTasindi?.Invoke(i0, y0x, y0y);
                        OnVertexTasindi?.Invoke(i1, y1x, y1y);
                    }
                    InvalidateVisual();
                }
            }
            e.Handled = true;
            return;
        }

        // Çizim modunda fare önizlemesi (pan yokken)
        if (_odaCizimModu && !_isPanning && _cizimNoktalar.Count > 0)
        {
            if (TryGetWorldPoint(pos, out double wx, out double wy))
            {
                var (sx, sy) = Snap(wx, wy);
                _cizimFare = new Point2D(sx, sy);
                InvalidateVisual();
            }
        }

        // Açıklık ekleme modunda fare önizlemesi
        if (_aciklikEkleModu && !_isPanning && _aciklikBas is not null)
        {
            if (TryGetWorldPoint(pos, out double wx, out double wy))
            {
                var (sx, sy) = SnapAciklikNoktasi(wx, wy);
                _aciklikFare = new Point2D(sx, sy);
                InvalidateVisual();
            }
        }

        if (!_isPanning) return;
        _pan = _panOrigin + (Vector)(pos - _panStart);
        InvalidateVisual();
        e.Handled = true;
    }

    /// Drag eşiğinin aşılması tek başına history adımı değildir. Snapshot yalnız
    /// snap sonrası hedef canlı koordinattan gerçekten farklıysa ve sürükleme başına
    /// tam bir kez alınır. Ayrı kapı, bu kritik sıralamanın regresyon testini kolaylaştırır.
    private static bool MutasyonKapisi(ref bool snapshotAlindi, bool gercekDegisim, Action? snapshotAl)
    {
        if (!gercekDegisim) return false;
        if (!snapshotAlindi)
        {
            snapshotAl?.Invoke();
            snapshotAlindi = true;
        }
        return true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        bool geometriDegisti = _dragSnapshotAlindi && (_dragVertexIdx >= 0 || _dragEdgeIdx >= 0);
        _dragVertexIdx = -1;
        _dragEdgeIdx = -1;
        _dragSnapshotAlindi = false;
        _isPanning = false;
        Cursor = Cursor.Default;
        if (geometriDegisti) OnVertexTasimaBitti?.Invoke();
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        // Köşe sürükleme bitişi
        if (_dragVertexIdx >= 0)
        {
            bool degisti = _dragSnapshotAlindi;
            _dragVertexIdx = -1;
            _dragSnapshotAlindi = false;
            e.Pointer.Capture(null);
            if (degisti) OnVertexTasimaBitti?.Invoke();
            e.Handled = true;
            return;
        }

        // Kenar (duvar) sürükleme bitişi
        if (_dragEdgeIdx >= 0)
        {
            bool degisti = _dragSnapshotAlindi;
            _dragEdgeIdx = -1;
            _dragSnapshotAlindi = false;
            e.Pointer.Capture(null);
            if (degisti) OnVertexTasimaBitti?.Invoke();
            e.Handled = true;
            return;
        }

        if (!_isPanning) return;

        var now = e.GetPosition(this);
        double dx = now.X - _panStart.X, dy = now.Y - _panStart.Y;
        bool wasDrag = dx * dx + dy * dy > 16;

        _isPanning = false;
        e.Pointer.Capture(null);

        if (!wasDrag && TryGetWorldPoint(now, out double wx, out double wy))
        {
            // Çizim modunda tıklama → nokta ekle / ilk noktaya yakınsa kapat
            if (_odaCizimModu)
            {
                var (sx, sy) = Snap(wx, wy);
                if (_cizimNoktalar.Count >= 3)
                {
                    var f = _cizimNoktalar[0];
                    double d0 = Math.Sqrt((sx - f.X) * (sx - f.X) + (sy - f.Y) * (sy - f.Y));
                    if (d0 < SnapToleransM)
                    {
                        OdaCizimBitir();
                        e.Handled = true;
                        return;
                    }
                }
                _cizimNoktalar.Add(new Point2D(sx, sy));
                InvalidateVisual();
                e.Handled = true;
                return;
            }

            // Manuel açıklık ekleme: 1. tık = başlangıç, 2. tık = bitiş
            if (_aciklikEkleModu)
            {
                var (sx, sy) = SnapAciklikNoktasi(wx, wy);
                if (_aciklikBas is null)
                {
                    _aciklikBas = new Point2D(sx, sy);
                    InvalidateVisual();
                }
                else
                {
                    var bas = _aciklikBas.Value;
                    OnAciklikEklendi?.Invoke(bas, new Point2D(sx, sy), _aciklikEkleTipi);
                    AciklikEkleIptal();
                }
                e.Handled = true;
                return;
            }

            // Önce açıklık çizgisi, sonra oda tıklaması
            var kat = Kat;
            if (kat is not null)
            {
                var acHit = HitTestAciklik(now);
                if (acHit is not null)
                {
                    SetCurrentValue(SeciliAciklikIdProperty, acHit.Id);
                    OnAciklikSecildi?.Invoke(acHit);
                }
                else
                {
                    if (SeciliAciklikId is not null)
                    {
                        SetCurrentValue(SeciliAciklikIdProperty, null);
                        OnAciklikSecildi?.Invoke(null);
                    }
                    var hit = kat.BloklariGoster ? kat.Odalar.FirstOrDefault(o =>
                        o.Poligon.Count >= 3 &&
                        PoligonYardimci.IsPointInside(new Point2D(wx, wy), o.Poligon)) : null;
                    SetCurrentValue(SeciliOdaIdProperty, hit?.Id);
                    OnOdaSecildi?.Invoke(hit);
                }
            }
            OnViewportClicked?.Invoke(wx, wy);
        }
        e.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var mouse = e.GetPosition(this);
        var c = Bounds.Center;
        double factor = e.Delta.Y > 0 ? 1.15 : 1.0 / 1.15;
        double newZoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);
        double ratio = newZoom / _zoom;
        // Zoom around mouse pointer: world point under mouse stays fixed.
        // screen = center + pan + (base - center) * zoom
        // => newPan = (mouse - center) * (1 - ratio) + pan * ratio
        var mx = mouse.X - c.X;
        var my = mouse.Y - c.Y;
        _pan = new Vector(mx * (1 - ratio) + _pan.X * ratio,
                          my * (1 - ratio) + _pan.Y * ratio);
        _zoom = newZoom;
        InvalidateVisual();
        e.Handled = true;
    }
}

/// Basit nokta-içinde-poligon testi (2D, BinaOda poligonları için).
file static class PoligonYardimci
{
    public static bool IsPointInside(Point2D p, IList<Point2D> poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var pi = poly[i]; var pj = poly[j];
            if ((pi.Y > p.Y) != (pj.Y > p.Y) &&
                p.X < (pj.X - pi.X) * (p.Y - pi.Y) / ((pj.Y - pi.Y) + double.Epsilon) + pi.X)
                inside = !inside;
        }
        return inside;
    }
}
