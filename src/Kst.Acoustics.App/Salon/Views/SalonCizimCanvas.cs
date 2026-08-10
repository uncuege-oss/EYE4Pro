using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Salon.Models;
using Kst.Acoustics.Core.Salon.Services;

namespace Kst.Acoustics.App.Salon.Views;

/// M51 salon 2D eskiz canvas'ı. Sürekli dünya-koordinat sistemi (px/metre + pan/zoom).
/// Araçlar: Seç · Çizgi · Polyline · Dikdörtgen · Daire · Trim · Sil.
public enum SalonAraci
{
    Sec, Cizgi, Polyline, Dikdortgen, Daire, Yay,
    Trim, Uzat, Sil, Tasi, Aynala, Dondur, Offset,
    Olcum, Aciklik
}

/// Aktif snap türü (işaret biçimi için).
public enum SnapTip { Yok, Vertex, Kesisim, Merkez, Dik, Orta, Kenar }

public sealed class SalonCizimCanvas : Control
{
    // ── Stiller ──────────────────────────────────────────────────────────────
    private static readonly SolidColorBrush BgBrush    = new(Color.Parse("#070D13"));
    private static readonly Pen GridPen     = new(new SolidColorBrush(Color.Parse("#0E1B26"), 0.9), 0.5);
    private static readonly Pen GridMajorPen = new(new SolidColorBrush(Color.Parse("#16303F"), 0.9), 0.6);
    private static readonly Pen AxisXPen    = new(new SolidColorBrush(Color.Parse("#7A3030"), 0.8), 1.0);
    private static readonly Pen AxisYPen    = new(new SolidColorBrush(Color.Parse("#307A40"), 0.8), 1.0);
    private static readonly SolidColorBrush SecFill   = new(Color.Parse("#40C8F0"), 0.12);
    private static readonly Pen SecPen      = new(new SolidColorBrush(Color.Parse("#40C8F0")), 2.2);
    private static readonly Pen OnizPen     = new(new SolidColorBrush(Color.Parse("#2AE08A")), 1.4)
        { DashStyle = new DashStyle([4, 3], 0) };
    private static readonly SolidColorBrush SnapFill  = new(Color.Parse("#FFD050"));
    private static readonly Pen SnapPen     = new(new SolidColorBrush(Color.Parse("#FFD050")), 1.4);
    private static readonly SolidColorBrush BandWinFill = new(Color.Parse("#2A6FB0"), 0.14);
    private static readonly Pen BandWinPen  = new(new SolidColorBrush(Color.Parse("#4A9FE0")), 1.0);
    private static readonly SolidColorBrush BandCrossFill = new(Color.Parse("#2AB06F"), 0.12);
    private static readonly Pen BandCrossPen = new(new SolidColorBrush(Color.Parse("#4AE09F")), 1.0)
        { DashStyle = new DashStyle([4, 3], 0) };
    private static readonly SolidColorBrush VertexFill = new(Color.Parse("#40C8F0"));
    private static readonly Pen VertexPen   = new(new SolidColorBrush(Color.Parse("#0A2030")), 1.0);
    private static readonly SolidColorBrush LabelBrush = new(Color.Parse("#7FA8C0"));
    private static readonly SolidColorBrush DefaultEntityBrush = new(Color.Parse("#C0CCD6"));
    private static readonly SolidColorBrush OlcuBack = new(Color.Parse("#0A1622"), 0.92);
    private static readonly SolidColorBrush OlcuText = new(Color.Parse("#FFE08A"));
    private static readonly Pen KaliciOlcuPen = new(new SolidColorBrush(Color.Parse("#FFE08A")), 1.0)
        { DashStyle = new DashStyle([4, 2], 0) };
    private static readonly SolidColorBrush SahneFill = new(Color.Parse("#C08A3E"), 0.18);
    private static readonly SolidColorBrush SahneText = new(Color.Parse("#E6B870"));
    private static readonly Pen SahnePen = new(new SolidColorBrush(Color.Parse("#E0A848")), 1.7)
        { DashStyle = new DashStyle([6, 3], 0) };
    private static readonly SolidColorBrush SeyirciFill = new(Color.Parse("#3FA85A"), 0.13);
    private static readonly SolidColorBrush SeyirciText = new(Color.Parse("#7FD89A"));
    private static readonly Pen SeyirciPen = new(new SolidColorBrush(Color.Parse("#4FB86A")), 1.5);
    private static readonly Pen SeyirciKademePen = new(new SolidColorBrush(Color.Parse("#4FB86A"), 0.65), 0.9);
    private static readonly Pen TavanKirilimPen = new(new SolidColorBrush(Color.Parse("#8678B5"), 0.75), 1.1)
        { DashStyle = new DashStyle([7, 4], 0) };
    private static readonly SolidColorBrush TavanKirilimText = new(Color.Parse("#B0A4D8"));
    private static readonly SolidColorBrush BalkonFill = new(Color.Parse("#B0689A"), 0.14);
    private static readonly SolidColorBrush BalkonText = new(Color.Parse("#D49AC2"));
    private static readonly Pen BalkonPen = new(new SolidColorBrush(Color.Parse("#C078A8")), 1.5)
        { DashStyle = new DashStyle([3, 2], 0) };
    private static readonly Color AltlikRengi = Color.Parse("#6B8FA6");

    private const double MinZoom = 0.1;
    private const double MaxZoom = 40.0;
    private const double HitPx   = 7.0;
    private const double VertexHitPx = 8.0;
    private const double SnapPx  = 12.0;   // manyetik snap yakalama yarıçapı (ekran px)

    // ── Durum ────────────────────────────────────────────────────────────────
    private double _basePxPerM = 40.0;
    private double _zoom = 1.0;
    private Point _pan;
    private bool _panInit;

    private bool _isPanning;
    private Point _panStart;
    private Point _panOrigin;

    private SalonGeometri? _salon;
    public SalonGeometri? Salon
    {
        get => _salon;
        set
        {
            if (ReferenceEquals(_salon, value)) return;
            KomutuSifirla();
            _salon = value;
            _secim.Clear();
            _sonVertex = -1;
            OnSecimDegisti?.Invoke(_secim);
            InvalidateVisual();
        }
    }

    /// Yeni çizilen öğelere atanacak aktif katman.
    public SalonKatman? AktifKatman { get; set; }

    private SalonAraci _arac = SalonAraci.Sec;
    public SalonAraci Arac
    {
        get => _arac;
        set { if (_arac == value) return; _arac = value; CizimiIptal(); GuncelleImlec(); BildirDurum(); }
    }

    /// Orto modu (AutoCAD F8): yeni nokta son noktaya göre yatay/dikey kilitlenir.
    private bool _ortho;
    public bool Ortho
    {
        get => _ortho;
        set { _ortho = value; InvalidateVisual(); }
    }

    /// Orto modunu değiştirir ve yeni durumu döndürür.
    public bool OrthoToggle()
    {
        _ortho = !_ortho;
        BildirDurum(_ortho ? "Orto açık (yatay/dikey kilit)." : "Orto kapalı.");
        InvalidateVisual();
        return _ortho;
    }

    // Snap / grid / polar modları (F3 / F9 / F10)
    private bool _snapAcik = true;
    private bool _gridSnap;
    private bool _polar;
    private double _gridAdimM = 0.5;
    private double _polarAciDerece = 15;

    public void AyarlariUygula(SalonEditorAyarlari ayar)
    {
        ArgumentNullException.ThrowIfNull(ayar);
        _snapAcik = ayar.SnapAcik;
        _gridSnap = ayar.GridAcik;
        _ortho = ayar.OrtoAcik;
        _polar = ayar.PolarAcik && !_ortho;
        _gridAdimM = Math.Clamp(ayar.GridAraligiM, 0.01, 100);
        _polarAciDerece = Math.Clamp(ayar.PolarAciDerece, 1, 90);
        InvalidateVisual();
    }

    public void AyarlariOku(SalonEditorAyarlari ayar)
    {
        ArgumentNullException.ThrowIfNull(ayar);
        ayar.SnapAcik = _snapAcik;
        ayar.GridAcik = _gridSnap;
        ayar.OrtoAcik = _ortho;
        ayar.PolarAcik = _polar;
        ayar.GridAraligiM = _gridAdimM;
        ayar.PolarAciDerece = _polarAciDerece;
    }

    public bool SnapToggle()
    {
        _snapAcik = !_snapAcik;
        BildirDurum(_snapAcik ? "Nesne snap açık (F3)." : "Nesne snap kapalı (F3).");
        InvalidateVisual();
        return _snapAcik;
    }

    public bool GridToggle()
    {
        _gridSnap = !_gridSnap;
        BildirDurum(_gridSnap ? $"Grid snap açık — {_gridAdimM:0.##} m ızgara (F9)." : "Grid snap kapalı (F9).");
        InvalidateVisual();
        return _gridSnap;
    }

    public bool PolarToggle()
    {
        _polar = !_polar;
        BildirDurum(_polar ? $"Polar kilit açık — {_polarAciDerece:0.#}° adımlar (F10)." : "Polar kilit kapalı (F10).");
        InvalidateVisual();
        return _polar;
    }

    // Çizim (in-progress) durumu
    private readonly List<Point2D> _cizimNoktalar = new();
    private Point2D? _fareWorld;     // snaplenmiş güncel fare konumu
    private Point2D? _snapNokta;     // snap işareti
    private SnapTip _snapTipi = SnapTip.Yok;
    private string _sayiGiris = "";  // klavyeyle girilen uzunluk/yarıçap (dynamic input)

    // Seçim (çoklu) + vertex sürükleme + pencere seçimi
    private readonly List<SalonCizimOgesi> _secim = new();
    private SalonCizimOgesi? Birincil => _secim.Count > 0 ? _secim[^1] : null;
    private int _dragVertex = -1;   // sürüklenen vertex (Birincil üzerinde)
    private bool _dragDegisti;      // ilk gerçek harekette tek undo snapshot'ı alınır
    private int _sonVertex = -1;    // son tıklanan/sürüklenen vertex (Delete ile silinebilir)
    private Point? _bandBaslangic;  // pencere seçimi başlangıcı (ekran)
    private Point _bandSon;
    private bool _bandShift;

    // Offset aracı durumu
    private SalonCizimOgesi? _offsetHedef;

    // ── Callbacks ────────────────────────────────────────────────────────────
    public Action<string>? OnDurum { get; set; }
    public Action<string>? OnKoordinat { get; set; }
    public Action<IReadOnlyList<SalonCizimOgesi>>? OnSecimDegisti { get; set; }
    public Action? OnDegisti { get; set; }
    /// Model değişmeden HEMEN ÖNCE çağrılır — pencere undo anlık görüntüsü alır.
    public Action? OnDegisecek { get; set; }
    /// Açıklık aracı: kullanıcı bir duvar çizgisine tıkladı — pencere açıklığı oluşturur.
    public Action<Point2D>? OnAciklikYerlestir { get; set; }

    private readonly Dictionary<string, SolidColorBrush> _fircaCache = new();

    public SalonCizimCanvas()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    // ── Transform ────────────────────────────────────────────────────────────
    private double Scale => _basePxPerM * _zoom;
    private Point W2S(Point2D w) => new(_pan.X + w.X * Scale, _pan.Y - w.Y * Scale);
    private Point2D S2W(Point s) => new((s.X - _pan.X) / Scale, (_pan.Y - s.Y) / Scale);

    private void EnsurePan(Rect b)
    {
        if (_panInit) return;
        _pan = new Point(b.Width * 0.18, b.Height * 0.82);
        _panInit = true;
    }

    public void GorunumuSifirla()
    {
        _zoom = 1.0;
        _panInit = false;
        InvalidateVisual();
    }

    /// Çizili içeriği görünüme sığdırır (içerik yoksa varsayılan görünüm).
    /// Sahne/seyirci/balkon footprint'leri de kapsanır ("salon önü" sahne dış hattın
    /// önünde olabilir; şablon sonrası hiçbir öğe görünüm dışı kalmasın).
    public void Sigdir()
    {
        var b = Bounds;
        if (_salon is null || b.Width < 4 || b.Height < 4)
        {
            GorunumuSifirla();
            return;
        }

        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        void Genislet(Point2D p)
        {
            minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
            minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
        }

        foreach (var o in _salon.CizimOgeleri)
            foreach (var (a, bb) in o.Segmentler())
            { Genislet(a); Genislet(bb); }
        if (_salon.Sahne is { } sahne)
            foreach (var p in sahne.TabanPoligonu(24)) Genislet(p);
        foreach (var sek in _salon.SeyirciSekciyonlari)
            foreach (var p in SalonSeyirciServisi.Footprint(sek)) Genislet(p);
        foreach (var bl in _salon.Balkonlar)
            foreach (var p in SalonBalkonServisi.Footprint(bl)) Genislet(p);
        if (_salon.OrkestraCukuru is { } ck)
            foreach (var p in ck.Footprint()) Genislet(p);
        foreach (var altlik in _salon.ReferansAltliklar.Where(x => x.Gorunur))
            foreach (var (a, bb, _) in SalonAltlikServisi.DonusmusSegmentler(altlik))
            { Genislet(a); Genislet(bb); }

        if (!double.IsFinite(minX)) { GorunumuSifirla(); return; }

        double mw = Math.Max(1e-3, maxX - minX), mh = Math.Max(1e-3, maxY - minY);
        double sc = Math.Min((b.Width - 80) / mw, (b.Height - 80) / mh);
        _zoom = Math.Clamp(sc / _basePxPerM, MinZoom, MaxZoom);
        double cx = (minX + maxX) / 2, cy = (minY + maxY) / 2;
        _pan = new Point(b.Width / 2 - cx * Scale, b.Height / 2 + cy * Scale);
        _panInit = true;
        InvalidateVisual();
    }

    // ── Araç API ─────────────────────────────────────────────────────────────
    public void CizimiIptal()
    {
        _cizimNoktalar.Clear();
        _offsetHedef = null;
        _sayiGiris = "";
        _fareWorld = null;
        InvalidateVisual();
    }

    /// Belge değişimi / undo sırasında eski modele ait hiçbir geçici komut durumu kalmasın.
    public void KomutuSifirla()
    {
        CizimiIptal();
        _dragVertex = -1;
        _dragDegisti = false;
        _bandBaslangic = null;
        _isPanning = false;
        _snapNokta = null;
        _snapTipi = SnapTip.Yok;
    }

    public void PolylineBitir()
    {
        if (_arac == SalonAraci.Polyline && _cizimNoktalar.Count >= 2)
        {
            OgeEkle(new SalonCizimOgesi
            {
                Tip = SalonCizimTipi.Polyline,
                Noktalar = _cizimNoktalar.ToList(),
                Kapali = false
            });
        }
        CizimiIptal();
    }

    /// Seçili tüm öğeleri siler. Önce vertex silmeyi dener: son tıklanan vertex varsa
    /// ve öğe küçülmeye uygunsa yalnız o köşe silinir (durum çubuğu bildirir).
    public void SeciliyiSil()
    {
        if (_salon is null || _secim.Count == 0) return;

        if (SeciliVertexSil()) return;

        OnDegisecek?.Invoke();
        int adet = _secim.Count;
        foreach (var o in _secim.ToList())
            _salon.CizimOgeleri.Remove(o);
        _secim.Clear();
        _sonVertex = -1;
        OnSecimDegisti?.Invoke(_secim);
        OnDegisti?.Invoke();
        BildirDurum(adet == 1 ? "Öğe silindi." : $"{adet} öğe silindi.");
        InvalidateVisual();
    }

    /// Son tıklanan vertex'i (Birincil öğede) siler; silinebildiyse true.
    private bool SeciliVertexSil()
    {
        if (Birincil is not { } o || _sonVertex < 0 || _sonVertex >= o.Noktalar.Count) return false;

        int minKose = o.Tip switch
        {
            SalonCizimTipi.Polyline => o.Kapali ? 3 : 2,
            SalonCizimTipi.Dikdortgen => 3,
            _ => int.MaxValue // Cizgi/Daire/Yay'da vertex silinmez
        };
        if (minKose == int.MaxValue || o.Noktalar.Count <= minKose) return false;

        OnDegisecek?.Invoke();
        if (o.Tip == SalonCizimTipi.Dikdortgen) { o.Tip = SalonCizimTipi.Polyline; o.Kapali = true; }
        o.Noktalar.RemoveAt(_sonVertex);
        _sonVertex = -1;
        OnDegisti?.Invoke();
        BildirDurum("Köşe silindi.");
        InvalidateVisual();
        return true;
    }

    public SalonCizimOgesi? SeciliOge => Birincil;
    public IReadOnlyList<SalonCizimOgesi> Secim => _secim;

    /// Seçili çizim öğelerini verilen katmana atar.
    public void SeciliyeKatmanAta(string katmanId)
    {
        if (_secim.Count == 0) { BildirDurum("Önce 🖱 Seç ile öğe seçin."); return; }
        OnDegisecek?.Invoke();
        foreach (var o in _secim) o.KatmanId = katmanId;
        OnDegisti?.Invoke();
        InvalidateVisual();
        BildirDurum(_secim.Count == 1 ? "Öğe katmana atandı." : $"{_secim.Count} öğe katmana atandı.");
    }

    private void GuncelleImlec()
        => Cursor = _arac == SalonAraci.Sec ? Cursor.Default : new Cursor(StandardCursorType.Cross);

    // ── Pointer ──────────────────────────────────────────────────────────────
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var p = e.GetCurrentPoint(this);
        var pos = p.Position;

        if (p.Properties.IsMiddleButtonPressed)
        {
            _isPanning = true;
            _panStart = pos;
            _panOrigin = _pan;
            e.Pointer.Capture(this);
            return;
        }

        if (p.Properties.IsRightButtonPressed)
        {
            // Sağ tık = devam eden çizimi iptal / seçimi kaldır
            if (_cizimNoktalar.Count > 0) { CizimiIptal(); BildirDurum("Çizim iptal edildi."); }
            else SecimiTemizle();
            return;
        }

        if (!p.Properties.IsLeftButtonPressed) return;

        // Çift tık = polyline'ı bitir (ilk tık noktayı eklemiş olur).
        if (_arac == SalonAraci.Polyline && e.ClickCount >= 2)
        {
            PolylineBitir();
            InvalidateVisual();
            return;
        }

        // Çift tık (Seç): seçili öğenin kenarına vertex ekle.
        if (_arac == SalonAraci.Sec && e.ClickCount >= 2)
        {
            VertexEkle(pos);
            InvalidateVisual();
            return;
        }

        var w = SnapliDunya(pos);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control)
                    || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        switch (_arac)
        {
            case SalonAraci.Sec:    TiklaSec(pos, shift, e); break;
            case SalonAraci.Sil:    TiklaSil(pos); break;
            case SalonAraci.Trim:   TiklaTrim(pos); break;
            case SalonAraci.Uzat:   TiklaUzat(pos); break;
            case SalonAraci.Aciklik: TiklaAciklik(pos); break;
            case SalonAraci.Cizgi:  TiklaCizgi(w); break;
            case SalonAraci.Polyline: TiklaPolyline(w); break;
            case SalonAraci.Dikdortgen: TiklaDikdortgen(w); break;
            case SalonAraci.Daire:  TiklaDaire(w); break;
            case SalonAraci.Yay:    TiklaYay(w); break;
            case SalonAraci.Tasi:   TiklaTasi(w, ctrl); break;
            case SalonAraci.Aynala: TiklaAynala(w, ctrl); break;
            case SalonAraci.Dondur: TiklaDondur(w, ctrl); break;
            case SalonAraci.Offset: TiklaOffset(pos, w); break;
            case SalonAraci.Olcum:  TiklaOlcum(pos, w); break;
        }
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetCurrentPoint(this).Position;

        if (_isPanning)
        {
            _pan = _panOrigin + (pos - _panStart);
            InvalidateVisual();
            return;
        }

        if (_bandBaslangic is not null)
        {
            _bandSon = pos;
            InvalidateVisual();
            return;
        }

        var w = SnapliDunya(pos);
        _fareWorld = w;

        if (_dragVertex >= 0 && Birincil is { } bo)
        {
            if (!_dragDegisti)
            {
                OnDegisecek?.Invoke(); // yalnız ilk gerçek harekette tek undo adımı
                _dragDegisti = true;
            }
            bo.Noktalar[_dragVertex] = w;
            OnDegisti?.Invoke();
        }

        BildirKoordinat(w);
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isPanning) { _isPanning = false; e.Pointer.Capture(null); }
        if (_dragVertex >= 0)
        {
            _sonVertex = _dragVertex;
            _dragVertex = -1;
            _dragDegisti = false;
            e.Pointer.Capture(null);
        }
        if (_bandBaslangic is { } bandBas)
        {
            _bandBaslangic = null;
            e.Pointer.Capture(null);
            BandSecimiBitir(bandBas, e.GetCurrentPoint(this).Position);
            InvalidateVisual();
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var pos = e.GetCurrentPoint(this).Position;
        var wBefore = S2W(pos);
        double factor = e.Delta.Y > 0 ? 1.12 : 1.0 / 1.12;
        _zoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);
        // Fare altındaki dünya noktasını sabit tut
        _pan = new Point(pos.X - wBefore.X * Scale, pos.Y + wBefore.Y * Scale);
        InvalidateVisual();
    }

    // ── Klavye sayısal giriş (AutoCAD dynamic input) ──────────────────────────
    /// Aktif araç klavyeden sayı bekliyor mu? (çizgi/polyline uzunluk[+açı], daire
    /// yarıçap, taşı mesafe, döndür derece, offset mesafe)
    private bool CizimReferansiVar =>
        ((_arac == SalonAraci.Cizgi || _arac == SalonAraci.Polyline) && _cizimNoktalar.Count > 0)
        || (_arac == SalonAraci.Daire && _cizimNoktalar.Count == 1)
        || (_arac == SalonAraci.Tasi && _cizimNoktalar.Count == 1)
        || (_arac == SalonAraci.Dondur && _cizimNoktalar.Count >= 1)
        || (_arac == SalonAraci.Offset && _offsetHedef is not null);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (CizimReferansiVar)
        {
            char? c = KeyToChar(e.Key);
            if (c is null && e.KeySymbol == "<") c = '<'; // açı ayracı (5<30)
            if (c is not null)
            {
                // '.' denetimi yalnız geçerli bölümde (açı ayracından sonrası ayrı sayıdır)
                int ayrac = _sayiGiris.IndexOf('<');
                string parca = ayrac >= 0 ? _sayiGiris[(ayrac + 1)..] : _sayiGiris;
                bool tekrar = (c == '.' && parca.Contains('.'))
                              || (c == '<' && (_sayiGiris.Length == 0 || ayrac >= 0))
                              || (c == '-' && (parca.Length > 0
                                  || (_arac != SalonAraci.Dondur && ayrac < 0)));
                if (!tekrar)
                {
                    _sayiGiris += c;
                    InvalidateVisual();
                }
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Back && _sayiGiris.Length > 0)
            {
                _sayiGiris = _sayiGiris[..^1];
                InvalidateVisual();
                e.Handled = true;
                return;
            }
            if ((e.Key == Key.Return || e.Key == Key.Enter) && _sayiGiris.Length > 0)
            {
                bool kopya = e.KeyModifiers.HasFlag(KeyModifiers.Control)
                             || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
                SayiyiUygula(kopya);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape && _sayiGiris.Length > 0)
            {
                _sayiGiris = "";
                InvalidateVisual();
                e.Handled = true;
                return;
            }
        }
        base.OnKeyDown(e);
    }

    private static char? KeyToChar(Key k) => k switch
    {
        >= Key.D0 and <= Key.D9        => (char)('0' + (k - Key.D0)),
        >= Key.NumPad0 and <= Key.NumPad9 => (char)('0' + (k - Key.NumPad0)),
        Key.OemPeriod or Key.Decimal   => '.',
        Key.OemComma                   => '.',
        Key.OemMinus or Key.Subtract   => '-',
        _ => null
    };

    /// Girişi "uzunluk" veya "uzunluk&lt;açı" (ör. 5&lt;30 = 5 m, 30°) olarak çözer.
    private static bool GirisiAyristir(string giris, out double uzunluk, out double? aciDeg)
    {
        uzunluk = 0;
        aciDeg = null;
        var parcalar = giris.Split('<');
        if (!double.TryParse(parcalar[0], NumberStyles.Float, CultureInfo.InvariantCulture, out uzunluk))
            return false;
        if (parcalar.Length == 2)
        {
            if (!double.TryParse(parcalar[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double a))
                return false;
            aciDeg = a;
        }
        return parcalar.Length <= 2;
    }

    /// Girilen uzunluğu/yarıçapı/açıyı aktif araca uygular.
    /// Çizgi/Polyline/Taşı: `L` imleç yönünde, `L&lt;A` mutlak A° yönünde.
    /// Daire: yarıçap. Döndür: derece. Offset: mesafe (taraf = imleç).
    private void SayiyiUygula(bool kopya)
    {
        if (!GirisiAyristir(_sayiGiris, out double L, out double? aciDeg)
            || (L <= 0 && _arac != SalonAraci.Dondur))
        {
            _sayiGiris = "";
            BildirDurum("Geçersiz giriş — uzunluk veya uzunluk<açı yazın (ör. 5 veya 5<30).");
            InvalidateVisual();
            return;
        }

        (double X, double Y)? Yon(Point2D referans)
            => aciDeg is { } a
                ? (Math.Cos(a * Math.PI / 180.0), Math.Sin(a * Math.PI / 180.0))
                : ImlecYonu(referans);

        if (_arac == SalonAraci.Daire && _cizimNoktalar.Count == 1)
        {
            var m = _cizimNoktalar[0];
            OgeEkle(new SalonCizimOgesi
            {
                Tip = SalonCizimTipi.Daire,
                Kapali = true,
                YariCapM = L,
                Noktalar = new() { m }
            });
            _cizimNoktalar.Clear();
        }
        else if ((_arac == SalonAraci.Cizgi || _arac == SalonAraci.Polyline) && _cizimNoktalar.Count > 0)
        {
            var last = _cizimNoktalar[^1];
            var dir = Yon(last);
            if (dir is null)
            {
                _sayiGiris = "";
                BildirDurum("Yön için fareyi hedef tarafa oynatın veya uzunluk<açı yazın.");
                InvalidateVisual();
                return;
            }
            var np = new Point2D(last.X + L * dir.Value.X, last.Y + L * dir.Value.Y);
            if (_arac == SalonAraci.Cizgi) TiklaCizgi(np);
            else TiklaPolyline(np);
        }
        else if (_arac == SalonAraci.Tasi && _cizimNoktalar.Count == 1)
        {
            var dir = Yon(_cizimNoktalar[0]);
            if (dir is null) { _sayiGiris = ""; BildirDurum("Yön için fareyi hedef tarafa oynatın."); InvalidateVisual(); return; }
            TasiUygula(L * dir.Value.X, L * dir.Value.Y, kopya);
            _cizimNoktalar.Clear();
        }
        else if (_arac == SalonAraci.Dondur && _cizimNoktalar.Count >= 1)
        {
            DondurUygula(L, kopya); // L burada derece
            _cizimNoktalar.Clear();
        }
        else if (_arac == SalonAraci.Offset && _offsetHedef is not null && _fareWorld is { } fare)
        {
            OffsetUygula(fare, L);
        }

        _sayiGiris = "";
        InvalidateVisual();
    }

    /// Son noktadan snaplenmiş fare yönüne birim vektör (orto uygulanmış).
    private (double X, double Y)? ImlecYonu(Point2D last)
    {
        if (_fareWorld is null) return null;
        var f = _fareWorld.Value;
        double dx = f.X - last.X, dy = f.Y - last.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-9) return null;
        return (dx / len, dy / len);
    }

    // ── Araç tıklamaları ─────────────────────────────────────────────────────
    private void TiklaCizgi(Point2D w)
    {
        _cizimNoktalar.Add(w);
        if (_cizimNoktalar.Count == 2)
        {
            if (Mesafe(_cizimNoktalar[0], _cizimNoktalar[1]) <= 1e-6)
            {
                _cizimNoktalar.Clear();
                BildirDurum("Çizgi uzunluğu sıfır olamaz; iki farklı nokta seçin.");
                InvalidateVisual();
                return;
            }
            OgeEkle(new SalonCizimOgesi
            {
                Tip = SalonCizimTipi.Cizgi,
                Noktalar = _cizimNoktalar.ToList()
            });
            _cizimNoktalar.Clear();
        }
    }

    private void TiklaPolyline(Point2D w)
    {
        // İlk noktaya tekrar tıklama = kapat + bitir
        if (_cizimNoktalar.Count >= 3 && Mesafe(w, _cizimNoktalar[0]) * Scale < VertexHitPx)
        {
            OgeEkle(new SalonCizimOgesi
            {
                Tip = SalonCizimTipi.Polyline,
                Noktalar = _cizimNoktalar.ToList(),
                Kapali = true
            });
            _cizimNoktalar.Clear();
            return;
        }
        if (_cizimNoktalar.Count > 0 && Mesafe(w, _cizimNoktalar[^1]) <= 1e-6)
        {
            BildirDurum("Polyline'da ardışık noktalar aynı olamaz.");
            return;
        }
        _cizimNoktalar.Add(w);
        BildirDurum("Polyline: tıkla = nokta · ilk noktaya tıkla = kapat · çift tık/Enter = bitir · Esc = iptal");
    }

    private void TiklaDikdortgen(Point2D w)
    {
        _cizimNoktalar.Add(w);
        if (_cizimNoktalar.Count == 2)
        {
            var a = _cizimNoktalar[0];
            var b = _cizimNoktalar[1];
            if (Math.Abs(a.X - b.X) <= 1e-6 || Math.Abs(a.Y - b.Y) <= 1e-6)
            {
                _cizimNoktalar.Clear();
                BildirDurum("Dikdörtgenin genişliği ve yüksekliği sıfırdan büyük olmalı.");
                InvalidateVisual();
                return;
            }
            OgeEkle(new SalonCizimOgesi
            {
                Tip = SalonCizimTipi.Dikdortgen,
                Kapali = true,
                Noktalar = new() { new(a.X, a.Y), new(b.X, a.Y), new(b.X, b.Y), new(a.X, b.Y) }
            });
            _cizimNoktalar.Clear();
        }
    }

    private void TiklaDaire(Point2D w)
    {
        _cizimNoktalar.Add(w);
        if (_cizimNoktalar.Count == 2)
        {
            var m = _cizimNoktalar[0];
            double r = Mesafe(m, _cizimNoktalar[1]);
            if (r > 1e-4)
                OgeEkle(new SalonCizimOgesi
                {
                    Tip = SalonCizimTipi.Daire,
                    Kapali = true,
                    YariCapM = r,
                    Noktalar = new() { m }
                });
            else
                BildirDurum("Daire yarıçapı sıfırdan büyük olmalı.");
            _cizimNoktalar.Clear();
            InvalidateVisual();
        }
    }

    private void TiklaSec(Point pos, bool shift, PointerPressedEventArgs e)
    {
        // Önce birincil öğenin köşesine isabet → vertex sürükle
        if (Birincil is { } birincil)
        {
            for (int i = 0; i < birincil.Noktalar.Count; i++)
            {
                var sp = W2S(birincil.Noktalar[i]);
                if (Math.Abs(sp.X - pos.X) <= VertexHitPx && Math.Abs(sp.Y - pos.Y) <= VertexHitPx)
                {
                    _dragVertex = i;
                    _dragDegisti = false;
                    _sonVertex = i;
                    e.Pointer.Capture(this);
                    return;
                }
            }
        }

        var oge = OgeHitTest(pos);
        if (oge is null)
        {
            // Boş alan: pencere seçimi başlat (bırakınca uygulanır; kısa tık = seçimi kaldır).
            _bandBaslangic = pos;
            _bandSon = pos;
            _bandShift = shift;
            e.Pointer.Capture(this);
            return;
        }

        _sonVertex = -1;
        if (shift)
        {
            if (!_secim.Remove(oge)) _secim.Add(oge); // ekle/çıkar
        }
        else
        {
            _secim.Clear();
            _secim.Add(oge);
        }
        OnSecimDegisti?.Invoke(_secim);
        BildirDurum(SecimOzeti());
    }

    /// Pencere seçimini uygular: soldan sağa = tamamen içindekiler (window),
    /// sağdan sola = değen her şey (crossing). Kısa sürükleme = seçimi kaldır.
    private void BandSecimiBitir(Point bas, Point son)
    {
        if (_salon is null) return;

        if (Math.Abs(son.X - bas.X) < 4 && Math.Abs(son.Y - bas.Y) < 4)
        {
            if (!_bandShift) SecimiTemizle();
            return;
        }

        bool crossing = son.X < bas.X;
        var w1 = S2W(bas);
        var w2 = S2W(son);
        double minX = Math.Min(w1.X, w2.X), maxX = Math.Max(w1.X, w2.X);
        double minY = Math.Min(w1.Y, w2.Y), maxY = Math.Max(w1.Y, w2.Y);

        if (!_bandShift) _secim.Clear();
        foreach (var o in _salon.CizimOgeleri)
        {
            if (!KatmanGorunur(o) || _secim.Contains(o)) continue;
            if (BandaGiriyor(o, minX, minY, maxX, maxY, crossing)) _secim.Add(o);
        }

        _sonVertex = -1;
        OnSecimDegisti?.Invoke(_secim);
        BildirDurum(SecimOzeti());
    }

    private static bool BandaGiriyor(SalonCizimOgesi o,
        double minX, double minY, double maxX, double maxY, bool crossing)
    {
        bool Icinde(Point2D p) => p.X >= minX && p.X <= maxX && p.Y >= minY && p.Y <= maxY;

        var segs = o.Segmentler().ToList();
        if (segs.Count == 0) return false;

        if (!crossing) // window: tüm segment uçları içeride
            return segs.All(s => Icinde(s.A) && Icinde(s.B));

        // crossing: herhangi bir uç içeride veya segment dikdörtgen kenarını kesiyor
        if (segs.Any(s => Icinde(s.A) || Icinde(s.B))) return true;
        var koseler = new[]
        {
            new Point2D(minX, minY), new Point2D(maxX, minY),
            new Point2D(maxX, maxY), new Point2D(minX, maxY)
        };
        foreach (var (a, b) in segs)
            for (int i = 0; i < 4; i++)
                if (SalonTrimServisi.SegmentKesisim(a, b, koseler[i], koseler[(i + 1) % 4], out _, out _))
                    return true;
        return false;
    }

    /// Seçim özeti: adet, tip, toplam uzunluk, kapalıysa alan (m²).
    private string SecimOzeti()
    {
        if (_secim.Count == 0) return "Seçim kaldırıldı.";
        if (_secim.Count > 1) return $"{_secim.Count} öğe seçili (Shift+tık ekle/çıkar).";

        var o = _secim[0];
        double uzunluk = o.Segmentler().Sum(s => Mesafe(s.A, s.B));
        var kapali = o.KapaliPoligon();
        string alan = kapali.Count >= 3
            ? $" · alan {Math.Abs(Kst.Acoustics.Core.Common.Geometry.ClipperAdaptor.IsaretliAlan(kapali)):F1} m²"
            : "";
        return $"Seçili: {AracAd(o.Tip)} · uzunluk {uzunluk:F2} m{alan}";
    }

    /// Çift tık: seçili öğenin kenarına, tıklanan noktada köşe ekler.
    private void VertexEkle(Point pos)
    {
        if (Birincil is not { } o) { BildirDurum("Köşe eklemek için önce öğe seçin, sonra kenara çift tıklayın."); return; }
        if (o.Tip is SalonCizimTipi.Daire or SalonCizimTipi.Yay)
        { BildirDurum("Daire/yaya köşe eklenemez."); return; }

        // Tıklanan kenarı bul (yalnız bu öğede)
        int segIndex = -1;
        double bestD = HitPx + 2;
        Point2D yeni = default;
        int si = 0;
        foreach (var (a, b) in o.Segmentler())
        {
            double d = NoktaKenarMesafe(pos, W2S(a), W2S(b));
            if (d < bestD)
            {
                bestD = d;
                segIndex = si;
                double t = ProjeksiyonT(a, b, S2W(pos));
                yeni = new Point2D(a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y));
            }
            si++;
        }
        if (segIndex < 0) { BildirDurum("Köşe eklemek için öğenin bir kenarına çift tıklayın."); return; }

        OnDegisecek?.Invoke();
        if (o.Tip == SalonCizimTipi.Dikdortgen) { o.Tip = SalonCizimTipi.Polyline; o.Kapali = true; }
        else if (o.Tip == SalonCizimTipi.Cizgi) { o.Tip = SalonCizimTipi.Polyline; }
        o.Noktalar.Insert(segIndex + 1, yeni);
        _sonVertex = segIndex + 1;
        OnDegisti?.Invoke();
        BildirDurum("Köşe eklendi (sürükleyerek taşıyın; Delete ile silin).");
    }

    private void TiklaSil(Point pos)
    {
        var oge = OgeHitTest(pos);
        if (oge is null || _salon is null) return;
        OnDegisecek?.Invoke();
        _salon.CizimOgeleri.Remove(oge);
        if (_secim.Remove(oge)) { _sonVertex = -1; OnSecimDegisti?.Invoke(_secim); }
        OnDegisti?.Invoke();
        BildirDurum("Öğe silindi.");
    }

    private void TiklaTrim(Point pos)
    {
        if (_salon is null) return;

        // Tüm öğeler içinde tıklanan SEGMENTİ bul (çizgi/polyline/dikdörtgen kenarı).
        SalonCizimOgesi? host = null;
        int hostSeg = -1;
        Point2D sa = default, sb = default;
        double bestD = HitPx + 2;
        foreach (var o in _salon.CizimOgeleri)
        {
            if (!KatmanGorunur(o) || o.Tip is SalonCizimTipi.Daire or SalonCizimTipi.Yay) continue;
            int si = 0;
            foreach (var (a, b) in o.Segmentler())
            {
                double d = NoktaKenarMesafe(pos, W2S(a), W2S(b));
                if (d < bestD) { bestD = d; host = o; hostSeg = si; sa = a; sb = b; }
                si++;
            }
        }

        if (host is null)
        {
            BildirDurum("Trim: kesilecek bir çizgi/kenara tıklayın (daire/yay trim edilemez).");
            return;
        }

        // Kesici kenarlar: host segmenti hariç tüm segmentler.
        var kesiciler = new List<(Point2D, Point2D)>();
        foreach (var o in _salon.CizimOgeleri)
        {
            int si = 0;
            foreach (var seg in o.Segmentler())
            {
                if (ReferenceEquals(o, host) && si == hostSeg) { si++; continue; }
                kesiciler.Add(seg);
                si++;
            }
        }

        double t = ProjeksiyonT(sa, sb, S2W(pos));
        if (!SalonTrimServisi.CizgiTrim(sa, sb, kesiciler, t, out var parcalar))
        {
            BildirDurum("Kesişim bulunamadı — çizgiyi kesen başka bir öğe yok.");
            return;
        }

        // Host'u çizgilere ayır: tıklanan segment parçalarla değişir, diğerleri korunur.
        OnDegisecek?.Invoke();
        int idx = _salon.CizimOgeleri.IndexOf(host);
        _salon.CizimOgeleri.RemoveAt(idx);
        int si2 = 0;
        foreach (var (a, b) in host.Segmentler())
        {
            if (si2 == hostSeg)
            {
                foreach (var (pa, pb) in parcalar)
                    _salon.CizimOgeleri.Insert(idx++, YeniCizgi(pa, pb, host.KatmanId));
            }
            else
            {
                _salon.CizimOgeleri.Insert(idx++, YeniCizgi(a, b, host.KatmanId));
            }
            si2++;
        }

        if (_secim.Remove(host)) { _sonVertex = -1; OnSecimDegisti?.Invoke(_secim); }
        OnDegisti?.Invoke();

        bool kapaliydi = host.Tip == SalonCizimTipi.Dikdortgen
                         || (host.Tip == SalonCizimTipi.Polyline && host.Kapali);
        BildirDurum(host.Tip == SalonCizimTipi.Cizgi
            ? "Trim uygulandı."
            : kapaliydi
                ? "⚠ Trim: kapalı şekil çizgilere ayrıldı — kapalılık kayboldu; bu öğe artık " +
                  "zemin+tavan üretmez (geri almak için Ctrl+Z)."
                : "Trim uygulandı (şekil çizgilere ayrıldı).");
    }

    /// Açıklık aracı: tıklanan noktayı en yakın duvar çizgisi üzerine oturtup pencereye bildirir.
    private void TiklaAciklik(Point pos)
    {
        if (_salon is null) return;

        double bestD = HitPx + 4;
        Point2D best = default;
        bool bulundu = false;
        var w = S2W(pos);
        foreach (var o in _salon.CizimOgeleri)
        {
            if (!KatmanGorunur(o)) continue;
            foreach (var (a, b) in o.Segmentler())
            {
                double d = NoktaKenarMesafe(pos, W2S(a), W2S(b));
                if (d < bestD)
                {
                    bestD = d;
                    best = EnYakinNoktaSegment(a, b, w);
                    bulundu = true;
                }
            }
        }

        if (!bulundu)
        {
            BildirDurum("Açıklık: bir duvar çizgisinin üzerine tıklayın (kapı/pencere oraya yerleşir).");
            return;
        }
        OnAciklikYerlestir?.Invoke(best);
    }

    // ── Yay / Taşı / Aynala / Döndür / Offset / Uzat / Ölçüm ─────────────────

    /// Yay: başlangıç → bitiş → kavis noktası (SketchUp 2-nokta + bulge akışı).
    private void TiklaYay(Point2D w)
    {
        _cizimNoktalar.Add(w);
        if (_cizimNoktalar.Count < 3)
        {
            BildirDurum(_cizimNoktalar.Count == 1
                ? "Yay: bitiş noktasını tıklayın."
                : "Yay: kavis (bombeliği) belirleyen noktayı tıklayın.");
            return;
        }

        var yay = SalonCizimOgesi.UcNoktadanYay(_cizimNoktalar[0], _cizimNoktalar[1], _cizimNoktalar[2]);
        if (yay is null)
        {
            OgeEkle(new SalonCizimOgesi
            {
                Tip = SalonCizimTipi.Cizgi,
                Noktalar = new() { _cizimNoktalar[0], _cizimNoktalar[1] }
            });
            BildirDurum("Noktalar doğrusal — çizgi olarak eklendi.");
        }
        else
        {
            OgeEkle(yay);
        }
        _cizimNoktalar.Clear();
    }

    /// Taşı: baz nokta → hedef nokta. Ctrl = kopya olarak taşı.
    private void TiklaTasi(Point2D w, bool ctrl)
    {
        if (_secim.Count == 0)
        {
            BildirDurum("Taşı: önce 🖱 Seç ile öğe seçin (Shift+tık ile çoklu).");
            return;
        }
        if (_cizimNoktalar.Count == 0)
        {
            _cizimNoktalar.Add(w);
            BildirDurum("Taşı: hedef noktayı tıklayın (Ctrl = kopya; uzunluk yazıp Enter olur).");
            return;
        }

        TasiUygula(w.X - _cizimNoktalar[0].X, w.Y - _cizimNoktalar[0].Y, ctrl);
        _cizimNoktalar.Clear();
    }

    private void TasiUygula(double dx, double dy, bool kopya)
    {
        OnDegisecek?.Invoke();
        if (kopya)
        {
            var kopyalar = _secim.Select(SalonDonusumServisi.Kopyala).ToList();
            foreach (var k in kopyalar)
            {
                SalonDonusumServisi.Tasi(k, dx, dy);
                _salon!.CizimOgeleri.Add(k);
            }
            _secim.Clear();
            _secim.AddRange(kopyalar);
        }
        else
        {
            foreach (var o in _secim) SalonDonusumServisi.Tasi(o, dx, dy);
        }
        _sonVertex = -1;
        OnSecimDegisti?.Invoke(_secim);
        OnDegisti?.Invoke();
        BildirDurum(kopya
            ? $"{_secim.Count} öğe kopyalandı (Δ {Math.Sqrt(dx * dx + dy * dy):F2} m)."
            : $"{_secim.Count} öğe taşındı (Δ {Math.Sqrt(dx * dx + dy * dy):F2} m).");
    }

    /// Aynala: 2 nokta ile eksen. Ctrl (2. tıkta) = kopya olarak aynala.
    private void TiklaAynala(Point2D w, bool ctrl)
    {
        if (_secim.Count == 0)
        {
            BildirDurum("Aynala: önce 🖱 Seç ile öğe seçin.");
            return;
        }
        if (_cizimNoktalar.Count == 0)
        {
            _cizimNoktalar.Add(w);
            BildirDurum("Aynala: eksenin ikinci noktasını tıklayın (Ctrl = kopya olarak).");
            return;
        }

        var e1 = _cizimNoktalar[0];
        if (Mesafe(e1, w) < 1e-6) return;

        OnDegisecek?.Invoke();
        if (ctrl)
        {
            var kopyalar = _secim.Select(SalonDonusumServisi.Kopyala).ToList();
            foreach (var k in kopyalar)
            {
                SalonDonusumServisi.Aynala(k, e1, w);
                _salon!.CizimOgeleri.Add(k);
            }
            _secim.Clear();
            _secim.AddRange(kopyalar);
        }
        else
        {
            foreach (var o in _secim) SalonDonusumServisi.Aynala(o, e1, w);
        }
        _cizimNoktalar.Clear();
        _sonVertex = -1;
        OnSecimDegisti?.Invoke(_secim);
        OnDegisti?.Invoke();
        BildirDurum(ctrl ? $"{_secim.Count} öğe kopya olarak aynalandı." : $"{_secim.Count} öğe aynalandı.");
    }

    /// Döndür: merkez → referans yönü → hedef yönü (veya merkez sonrası derece yazıp Enter).
    private void TiklaDondur(Point2D w, bool ctrl)
    {
        if (_secim.Count == 0)
        {
            BildirDurum("Döndür: önce 🖱 Seç ile öğe seçin.");
            return;
        }
        if (_cizimNoktalar.Count == 0)
        {
            _cizimNoktalar.Add(w);
            BildirDurum("Döndür: referans yönünü tıklayın (veya açı yazıp Enter; Ctrl = kopya).");
            return;
        }
        if (_cizimNoktalar.Count == 1)
        {
            if (Mesafe(_cizimNoktalar[0], w) < 1e-6) return;
            _cizimNoktalar.Add(w);
            BildirDurum("Döndür: hedef yönü tıklayın.");
            return;
        }

        var m = _cizimNoktalar[0];
        double aciRef = Math.Atan2(_cizimNoktalar[1].Y - m.Y, _cizimNoktalar[1].X - m.X);
        double aciHedef = Math.Atan2(w.Y - m.Y, w.X - m.X);
        DondurUygula((aciHedef - aciRef) * 180.0 / Math.PI, ctrl);
        _cizimNoktalar.Clear();
    }

    private void DondurUygula(double aciDeg, bool kopya)
    {
        var m = _cizimNoktalar[0];
        OnDegisecek?.Invoke();
        if (kopya)
        {
            var kopyalar = _secim.Select(SalonDonusumServisi.Kopyala).ToList();
            foreach (var k in kopyalar)
            {
                SalonDonusumServisi.Dondur(k, m, aciDeg);
                _salon!.CizimOgeleri.Add(k);
            }
            _secim.Clear();
            _secim.AddRange(kopyalar);
        }
        else
        {
            foreach (var o in _secim) SalonDonusumServisi.Dondur(o, m, aciDeg);
        }
        _sonVertex = -1;
        OnSecimDegisti?.Invoke(_secim);
        OnDegisti?.Invoke();
        BildirDurum($"{_secim.Count} öğe {aciDeg:F1}° döndürüldü{(kopya ? " (kopya)" : "")}.");
    }

    /// Offset: öğeye tıkla → imleç tarafına paralel kopya (mesafe = imleç uzaklığı
    /// veya klavyeden). İkinci tık uygular.
    private void TiklaOffset(Point pos, Point2D w)
    {
        if (_offsetHedef is null)
        {
            var oge = OgeHitTest(pos);
            if (oge is null)
            {
                BildirDurum("Offset: paralel kopyası alınacak öğeye tıklayın.");
                return;
            }
            _offsetHedef = oge;
            BildirDurum("Offset: taraf/mesafe için tıklayın (mesafe yazıp Enter da olur).");
            return;
        }

        OffsetUygula(w);
    }

    private void OffsetUygula(Point2D imlec, double? mesafe = null)
    {
        if (_offsetHedef is not { } hedef || _salon is null) return;

        SalonCizimOgesi? yeni = null;
        switch (hedef.Tip)
        {
            case SalonCizimTipi.Daire:
            case SalonCizimTipi.Yay:
            {
                var m = hedef.Noktalar[0];
                double dImlec = Mesafe(m, imlec);
                double d = mesafe ?? Math.Abs(dImlec - hedef.YariCapM);
                double yeniR = dImlec >= hedef.YariCapM ? hedef.YariCapM + d : hedef.YariCapM - d;
                if (yeniR < 0.01) { BildirDurum("Offset: yarıçap çok küçük olur."); return; }
                yeni = SalonDonusumServisi.Kopyala(hedef);
                yeni.YariCapM = yeniR;
                break;
            }
            case SalonCizimTipi.Dikdortgen:
            case SalonCizimTipi.Polyline when hedef.Kapali:
            {
                var poly = hedef.KapaliPoligon();
                bool iceride = NoktaPoligonIcinde(imlec, poly);
                double d = mesafe ?? PoligonaUzaklik(imlec, hedef);
                var halkalar = Kst.Acoustics.Core.Common.Geometry.ClipperAdaptor
                    .Tampon(poly, iceride ? -d : d);
                if (halkalar.Count == 0) { BildirDurum("Offset: bu mesafede poligon kalmıyor."); return; }
                OnDegisecek?.Invoke();
                foreach (var h in halkalar)
                    _salon.CizimOgeleri.Add(new SalonCizimOgesi
                    {
                        Tip = SalonCizimTipi.Polyline,
                        Kapali = true,
                        KatmanId = AktifKatman is { Gorunur: true } ak ? ak.Id : hedef.KatmanId,
                        Noktalar = h
                    });
                OnDegisti?.Invoke();
                BildirDurum($"Offset uygulandı ({(iceride ? "içe" : "dışa")} {d:F2} m).");
                _offsetHedef = null;
                _sayiGiris = "";
                return;
            }
            default: // Cizgi / açık Polyline
            {
                var pts = hedef.Noktalar;
                if (pts.Count < 2) return;
                // Taraf: imlecin ilk yakın segmente göre işareti (sol = pozitif)
                double d = mesafe ?? PoligonaUzaklik(imlec, hedef);
                var (sa, sb) = EnYakinSegment(hedef, imlec);
                double capraz = (sb.X - sa.X) * (imlec.Y - sa.Y) - (sb.Y - sa.Y) * (imlec.X - sa.X);
                var yeniPts = SalonPoligonServisi.AcikOffset(pts, capraz >= 0 ? d : -d);
                yeni = SalonDonusumServisi.Kopyala(hedef);
                yeni.Noktalar = yeniPts;
                break;
            }
        }

        if (yeni is null) return;
        OnDegisecek?.Invoke();
        yeni.KatmanId = AktifKatman is { Gorunur: true } aktif ? aktif.Id : hedef.KatmanId;
        _salon.CizimOgeleri.Add(yeni);
        OnDegisti?.Invoke();
        BildirDurum("Offset uygulandı.");
        _offsetHedef = null;
        _sayiGiris = "";
    }

    /// Uzat: çizginin ucuna yakın tıkla → uç, diğer öğelerin ilk kesişimine uzatılır.
    private void TiklaUzat(Point pos)
    {
        if (_salon is null) return;

        // Uzatılabilir uç segmenti bul: Cizgi veya açık Polyline'ın ilk/son segmenti.
        SalonCizimOgesi? host = null;
        bool sonUc = true; // true = son nokta uzar, false = ilk nokta
        double bestD = HitPx + 2;
        foreach (var o in _salon.CizimOgeleri)
        {
            if (!KatmanGorunur(o)) continue;
            bool uygun = o.Tip == SalonCizimTipi.Cizgi
                         || (o.Tip == SalonCizimTipi.Polyline && !o.Kapali);
            if (!uygun || o.Noktalar.Count < 2) continue;

            // İlk segment (ilk uç) ve son segment (son uç)
            double dIlk = NoktaKenarMesafe(pos, W2S(o.Noktalar[0]), W2S(o.Noktalar[1]));
            double dSon = NoktaKenarMesafe(pos, W2S(o.Noktalar[^2]), W2S(o.Noktalar[^1]));
            if (dIlk < bestD || dSon < bestD)
            {
                host = o;
                if (dSon <= dIlk) { bestD = dSon; sonUc = true; }
                else { bestD = dIlk; sonUc = false; }
                // Tıklama segmentin hangi yarısındaysa o uca göre karar ver (tek segmentte)
                if (o.Noktalar.Count == 2)
                {
                    double t = ProjeksiyonT(o.Noktalar[0], o.Noktalar[1], S2W(pos));
                    sonUc = t >= 0.5;
                }
            }
        }

        if (host is null)
        {
            BildirDurum("Uzat: bir çizginin/açık polyline'ın ucuna yakın tıklayın.");
            return;
        }

        var sabit = sonUc ? host.Noktalar[^2] : host.Noktalar[1];
        var uc = sonUc ? host.Noktalar[^1] : host.Noktalar[0];

        var kesiciler = new List<(Point2D, Point2D)>();
        foreach (var o in _salon.CizimOgeleri)
        {
            if (ReferenceEquals(o, host)) continue;
            if (!KatmanGorunur(o)) continue;
            foreach (var s in o.Segmentler()) kesiciler.Add(s);
        }

        var yeniUc = SalonTrimServisi.Uzat(sabit, uc, kesiciler);
        if (yeniUc is null)
        {
            BildirDurum("Uzat: bu doğrultuda kesişen bir öğe yok.");
            return;
        }

        OnDegisecek?.Invoke();
        host.Noktalar[sonUc ? host.Noktalar.Count - 1 : 0] = yeniUc.Value;
        OnDegisti?.Invoke();
        BildirDurum($"Uzatıldı → ({yeniUc.Value.X:F2}, {yeniUc.Value.Y:F2}).");
    }

    /// Ölçüm: kapalı öğeye tek tık = alan/çevre; boş alanda 2 tık = mesafe.
    private void TiklaOlcum(Point pos, Point2D w)
    {
        if (_cizimNoktalar.Count == 0)
        {
            var oge = OgeHitTest(pos);
            var kapali = oge?.KapaliPoligon() ?? new List<Point2D>();
            if (oge is not null && kapali.Count >= 3)
            {
                double alan = Math.Abs(Kst.Acoustics.Core.Common.Geometry.ClipperAdaptor.IsaretliAlan(kapali));
                double cevre = oge.Segmentler().Sum(s => Mesafe(s.A, s.B));
                BildirDurum($"📐 {AracAd(oge.Tip)}: alan {alan:F2} m² · çevre {cevre:F2} m");
                return;
            }
            _cizimNoktalar.Add(w);
            BildirDurum("Ölçüm: ikinci noktayı tıklayın.");
            return;
        }

        var a = _cizimNoktalar[0];
        double dx = w.X - a.X, dy = w.Y - a.Y;
        BildirDurum($"📐 Mesafe {Math.Sqrt(dx * dx + dy * dy):F3} m · ΔX {dx:F2} m · ΔY {dy:F2} m " +
                    $"· açı {Math.Atan2(dy, dx) * 180.0 / Math.PI:F1}°");
        _cizimNoktalar.Clear();
    }

    // ── Offset yardımcıları ───────────────────────────────────────────────────

    private static bool NoktaPoligonIcinde(Point2D p, List<Point2D> poly)
    {
        bool icinde = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            if (poly[i].Y > p.Y != poly[j].Y > p.Y &&
                p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
                icinde = !icinde;
        }
        return icinde;
    }

    private static double PoligonaUzaklik(Point2D p, SalonCizimOgesi oge)
    {
        double best = double.MaxValue;
        foreach (var (a, b) in oge.Segmentler())
        {
            var c = EnYakinNoktaSegment(a, b, p);
            best = Math.Min(best, Mesafe(c, p));
        }
        return best == double.MaxValue ? 0 : best;
    }

    private static (Point2D A, Point2D B) EnYakinSegment(SalonCizimOgesi oge, Point2D p)
    {
        (Point2D, Point2D) best = default;
        double bestD = double.MaxValue;
        foreach (var (a, b) in oge.Segmentler())
        {
            double d = Mesafe(EnYakinNoktaSegment(a, b, p), p);
            if (d < bestD) { bestD = d; best = (a, b); }
        }
        return best;
    }

    private static SalonCizimOgesi YeniCizgi(Point2D a, Point2D b, string katmanId) => new()
    {
        Tip = SalonCizimTipi.Cizgi,
        KatmanId = katmanId,
        Noktalar = new() { a, b }
    };

    // ── Yardımcılar ──────────────────────────────────────────────────────────
    private void OgeEkle(SalonCizimOgesi oge)
    {
        if (_salon is null) return;
        if (AktifKatman is not { Gorunur: true } aktifKatman)
        {
            BildirDurum("Çizim için görünür bir aktif katman seçin.");
            return;
        }
        OnDegisecek?.Invoke();
        oge.KatmanId = aktifKatman.Id;
        _salon.CizimOgeleri.Add(oge);
        OnDegisti?.Invoke();
        BildirDurum($"{AracAd(oge.Tip)} eklendi.");
    }

    public void SecimiTemizle()
    {
        if (_secim.Count == 0) return;
        _secim.Clear();
        _sonVertex = -1;
        OnSecimDegisti?.Invoke(_secim);
        InvalidateVisual();
    }

    private SalonCizimOgesi? OgeHitTest(Point pos)
    {
        if (_salon is null) return null;
        SalonCizimOgesi? best = null;
        double bestD = HitPx;
        foreach (var o in _salon.CizimOgeleri)
        {
            if (!KatmanGorunur(o)) continue;
            foreach (var (a, b) in o.Segmentler())
            {
                double d = NoktaKenarMesafe(pos, W2S(a), W2S(b));
                if (d < bestD) { bestD = d; best = o; }
            }
        }
        return best;
    }

    /// Snap önceliği: uç > kesişim > merkez > dik > orta > kenar (manyetik) >
    /// grid > orto/polar/serbest. F3 nesne snap'lerini, F9 grid'i yönetir.
    private Point2D SnapliDunya(Point pos)
    {
        var w = S2W(pos);
        _snapNokta = null;
        _snapTipi = SnapTip.Yok;
        if (_salon is null || !_snapAcik) return SerbestNokta(w);

        double tolW = SnapPx / Scale;

        // Görünür öğelerin segment + uç noktalarını + devam eden çizimi topla.
        // Vertex sürüklenirken öğenin KENDİSİ aday olamaz: aksi halde imleç sürüklenen
        // vertex'in son konumuna snap'lenir ve vertex yerinden oynatılamaz (self-snap).
        bool surukluyor = _dragVertex >= 0 && Birincil is not null;
        var segs = new List<(Point2D A, Point2D B)>();
        var uclar = new List<Point2D>();
        var merkezler = new List<Point2D>();
        foreach (var o in _salon.CizimOgeleri)
        {
            if (!KatmanGorunur(o)) continue;
            if (surukluyor && ReferenceEquals(o, Birincil)) continue;
            foreach (var n in o.Noktalar) uclar.Add(n);
            foreach (var s in o.Segmentler()) segs.Add(s);
            if (o.Tip is SalonCizimTipi.Daire or SalonCizimTipi.Yay && o.Noktalar.Count > 0)
                merkezler.Add(o.Noktalar[0]);
        }
        foreach (var n in _cizimNoktalar) uclar.Add(n);
        for (int i = 0; i + 1 < _cizimNoktalar.Count; i++)
            segs.Add((_cizimNoktalar[i], _cizimNoktalar[i + 1]));

        // 1) Uç nokta
        if (EnYakin(uclar, w, tolW, out var up)) return SnapKaydet(up, SnapTip.Vertex);

        // 2) Kesişim — imlece yakın segment çiftleri
        var yakin = new List<(Point2D A, Point2D B)>();
        foreach (var s in segs)
            if (NoktaKenarMesafe(pos, W2S(s.A), W2S(s.B)) < 30) yakin.Add(s);
        {
            double bestD = tolW; Point2D best = w; bool found = false;
            for (int i = 0; i < yakin.Count; i++)
                for (int j = i + 1; j < yakin.Count; j++)
                    if (SalonTrimServisi.SegmentKesisim(yakin[i].A, yakin[i].B, yakin[j].A, yakin[j].B, out var ip, out _))
                    {
                        double d = Mesafe(ip, w);
                        if (d < bestD) { bestD = d; best = ip; found = true; }
                    }
            if (found) return SnapKaydet(best, SnapTip.Kesisim);
        }

        // 3) Merkez (daire/yay merkezleri)
        if (EnYakin(merkezler, w, tolW, out var cp)) return SnapKaydet(cp, SnapTip.Merkez);

        // 4) Dik (son noktadan segmente dik ayak) — yalnız çizim referansı varken
        if (_cizimNoktalar.Count > 0 &&
            _arac is SalonAraci.Cizgi or SalonAraci.Polyline or SalonAraci.Tasi or SalonAraci.Aynala)
        {
            var son = _cizimNoktalar[^1];
            double bestD = tolW; Point2D best = w; bool found = false;
            foreach (var s in segs)
            {
                var ayak = EnYakinNoktaSegment(s.A, s.B, son);
                if (Mesafe(ayak, son) < 1e-9) continue; // nokta segmentin üzerinde
                double d = Mesafe(ayak, w);
                if (d < bestD) { bestD = d; best = ayak; found = true; }
            }
            if (found) return SnapKaydet(best, SnapTip.Dik);
        }

        // 5) Orta nokta
        {
            var ortalar = segs.Select(s => new Point2D((s.A.X + s.B.X) / 2, (s.A.Y + s.B.Y) / 2)).ToList();
            if (EnYakin(ortalar, w, tolW, out var mp)) return SnapKaydet(mp, SnapTip.Orta);
        }

        // 6) Kenar (manyetik — segment üzerinde en yakın nokta)
        {
            double bestD = tolW; Point2D best = w; bool found = false;
            foreach (var s in segs)
            {
                var c = EnYakinNoktaSegment(s.A, s.B, w);
                double d = Mesafe(c, w);
                if (d < bestD) { bestD = d; best = c; found = true; }
            }
            if (found) return SnapKaydet(best, SnapTip.Kenar);
        }

        return SerbestNokta(w);
    }

    private Point2D SnapKaydet(Point2D p, SnapTip t) { _snapNokta = p; _snapTipi = t; return p; }

    /// Nesne snap'i bulunamadığında: grid kilidi → orto/polar → serbest.
    private Point2D SerbestNokta(Point2D w)
    {
        if (_gridSnap)
            w = new Point2D(
                Math.Round(w.X / _gridAdimM) * _gridAdimM,
                Math.Round(w.Y / _gridAdimM) * _gridAdimM);
        return ApplyOrtho(w);
    }

    private Point2D ApplyOrtho(Point2D w)
    {
        bool yonluArac = _arac is SalonAraci.Cizgi or SalonAraci.Polyline
                         or SalonAraci.Tasi or SalonAraci.Aynala or SalonAraci.Dondur;
        if (_cizimNoktalar.Count == 0 || !yonluArac) return w;

        var r = _cizimNoktalar[^1];
        if (_ortho)
            return Math.Abs(w.X - r.X) >= Math.Abs(w.Y - r.Y)
                ? new Point2D(w.X, r.Y)
                : new Point2D(r.X, w.Y);

        if (_polar)
        {
            double dx = w.X - r.X, dy = w.Y - r.Y;
            double L = Math.Sqrt(dx * dx + dy * dy);
            if (L < 1e-9) return w;
            double aci = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            double hedef = Math.Round(aci / _polarAciDerece) * _polarAciDerece;
            if (Math.Abs(aci - hedef) <= 4.0) // yumuşak kilit: yalnız yakınken
            {
                double rad = hedef * Math.PI / 180.0;
                return new Point2D(r.X + L * Math.Cos(rad), r.Y + L * Math.Sin(rad));
            }
        }
        return w;
    }

    private static bool EnYakin(List<Point2D> noktalar, Point2D w, double tol, out Point2D best)
    {
        best = w; double bestD = tol; bool found = false;
        foreach (var n in noktalar)
        {
            double d = Mesafe(n, w);
            if (d < bestD) { bestD = d; best = n; found = true; }
        }
        return found;
    }

    private static Point2D EnYakinNoktaSegment(Point2D a, Point2D b, Point2D p)
    {
        double abx = b.X - a.X, aby = b.Y - a.Y;
        double len2 = abx * abx + aby * aby;
        if (len2 < 1e-12) return a;
        double t = Math.Clamp(((p.X - a.X) * abx + (p.Y - a.Y) * aby) / len2, 0, 1);
        return new Point2D(a.X + t * abx, a.Y + t * aby);
    }

    private bool KatmanGorunur(SalonCizimOgesi o)
    {
        if (_salon is null) return true;
        var k = _salon.Katmanlar.FirstOrDefault(x => x.Id == o.KatmanId);
        return k?.Gorunur ?? true;
    }

    private SolidColorBrush OgeFircasi(SalonCizimOgesi o)
    {
        var k = _salon?.Katmanlar.FirstOrDefault(x => x.Id == o.KatmanId);
        var hex = k?.RenkHex;
        if (string.IsNullOrWhiteSpace(hex)) return DefaultEntityBrush;
        if (_fircaCache.TryGetValue(hex, out var f)) return f;
        try { f = new SolidColorBrush(Color.Parse(hex)); }
        catch { f = DefaultEntityBrush; }
        _fircaCache[hex] = f;
        return f;
    }

    private static double Mesafe(Point2D a, Point2D b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double ProjeksiyonT(Point2D a, Point2D b, Point2D p)
    {
        double abx = b.X - a.X, aby = b.Y - a.Y;
        double len2 = abx * abx + aby * aby;
        if (len2 < 1e-12) return 0;
        return Math.Clamp(((p.X - a.X) * abx + (p.Y - a.Y) * aby) / len2, 0, 1);
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

    private static string AracAd(SalonCizimTipi t) => t switch
    {
        SalonCizimTipi.Cizgi => "Çizgi",
        SalonCizimTipi.Polyline => "Polyline",
        SalonCizimTipi.Dikdortgen => "Dikdörtgen",
        SalonCizimTipi.Daire => "Daire",
        SalonCizimTipi.Yay => "Yay",
        _ => "Öğe"
    };

    private void BildirDurum(string? mesaj = null)
    {
        if (mesaj is not null) OnDurum?.Invoke(mesaj);
    }

    private void BildirKoordinat(Point2D w)
        => OnKoordinat?.Invoke($"x: {w.X:F2} m   y: {w.Y:F2} m   ·   1m = {Scale:F0}px   ·   {AracEtiket(_arac)}");

    private static string AracEtiket(SalonAraci a) => a switch
    {
        SalonAraci.Sec => "Seç",
        SalonAraci.Cizgi => "Çizgi",
        SalonAraci.Polyline => "Polyline",
        SalonAraci.Dikdortgen => "Dikdörtgen",
        SalonAraci.Daire => "Daire",
        SalonAraci.Yay => "Yay",
        SalonAraci.Trim => "Trim",
        SalonAraci.Uzat => "Uzat",
        SalonAraci.Sil => "Sil",
        SalonAraci.Tasi => "Taşı (Ctrl=kopya)",
        SalonAraci.Aynala => "Aynala (Ctrl=kopya)",
        SalonAraci.Dondur => "Döndür (Ctrl=kopya)",
        SalonAraci.Offset => "Offset",
        SalonAraci.Olcum => "Ölçüm",
        SalonAraci.Aciklik => "Açıklık (kapı/pencere)",
        _ => ""
    };

    // ── Render ───────────────────────────────────────────────────────────────
    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);
        var b = Bounds;
        if (b.Width < 4 || b.Height < 4) return;
        EnsurePan(b);

        ctx.FillRectangle(BgBrush, b);
        DrawGrid(ctx, b);

        if (_salon is not null)
        {
            DrawAltliklar(ctx);
            foreach (var o in _salon.CizimOgeleri)
            {
                if (!KatmanGorunur(o)) continue;
                DrawOge(ctx, o, _secim.Contains(o));
            }
            DrawSahne(ctx);
            DrawCukur(ctx);
            DrawSeyirci(ctx);
            DrawBalkon(ctx);
            DrawAcikliklar(ctx);
            DrawTavanProfili(ctx);
            DrawKaliciOlculer(ctx);
        }

        DrawOnizleme(ctx);
        DrawSnap(ctx);
        DrawVertexHandles(ctx);
        DrawSecimBandi(ctx);
        DrawOlcuEtiketi(ctx);
    }

    private void DrawAltliklar(DrawingContext ctx)
    {
        if (_salon is null) return;
        foreach (var altlik in _salon.ReferansAltliklar.Where(x => x.Gorunur && x.Tip == SalonAltlikTipi.Dxf))
        {
            double alfa = Math.Clamp(1.0 - altlik.Saydamlik, 0.08, 1.0);
            var pen = new Pen(new SolidColorBrush(AltlikRengi, alfa), 1.0)
            {
                DashStyle = new DashStyle([5, 3], 0)
            };
            foreach (var (a, b, _) in SalonAltlikServisi.DonusmusSegmentler(altlik))
                ctx.DrawLine(pen, W2S(a), W2S(b));
        }
    }

    private void DrawKaliciOlculer(DrawingContext ctx)
    {
        if (_salon is null) return;
        foreach (var olcu in _salon.Olculendirmeler.Where(x => x.Gorunur))
        {
            try
            {
                var d = SalonOlculendirmeServisi.Hesapla(_salon, olcu);
                double dx = d.Bitis.X - d.Baslangic.X, dy = d.Bitis.Y - d.Baslangic.Y;
                double l = Math.Sqrt(dx * dx + dy * dy);
                if (l < 1e-9) continue;
                double ofset = double.IsFinite(olcu.OfsetM) ? olcu.OfsetM : 0.35;
                var n = new Point2D(-dy / l * ofset, dx / l * ofset);
                var a = new Point2D(d.Baslangic.X + n.X, d.Baslangic.Y + n.Y);
                var b = new Point2D(d.Bitis.X + n.X, d.Bitis.Y + n.Y);
                ctx.DrawLine(KaliciOlcuPen, W2S(d.Baslangic), W2S(a));
                ctx.DrawLine(KaliciOlcuPen, W2S(d.Bitis), W2S(b));
                ctx.DrawLine(KaliciOlcuPen, W2S(a), W2S(b));
                var m = W2S(new Point2D((a.X + b.X) / 2, (a.Y + b.Y) / 2));
                var ft = new FormattedText(d.Metin, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    Typeface.Default, 11, OlcuText);
                var rect = new Rect(m.X - ft.Width / 2 - 4, m.Y - ft.Height / 2 - 2, ft.Width + 8, ft.Height + 4);
                ctx.FillRectangle(OlcuBack, rect);
                ctx.DrawText(ft, new Point(rect.X + 4, rect.Y + 2));
            }
            catch (InvalidOperationException) { /* bozuk eski ölçü geometriyi çizmeyi engellemesin */ }
        }
    }

    private void DrawSahne(DrawingContext ctx)
    {
        var sahne = _salon?.Sahne;
        if (sahne is null) return;
        var poly = sahne.TabanPoligonu(48);
        if (poly.Count < 3) return;

        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(W2S(poly[0]), true);
            for (int i = 1; i < poly.Count; i++) g.LineTo(W2S(poly[i]));
            g.EndFigure(true);
        }
        ctx.DrawGeometry(SahneFill, SahnePen, geo);

        var c = W2S(new Point2D(sahne.MerkezX, sahne.MerkezY));
        var ft = new FormattedText("SAHNE", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            Typeface.Default, 11, SahneText);
        ctx.DrawText(ft, new Point(c.X - ft.Width / 2, c.Y - ft.Height / 2));
    }

    private void DrawSeyirci(DrawingContext ctx)
    {
        if (_salon is null) return;
        foreach (var sek in _salon.SeyirciSekciyonlari)
        {
            var fp = SalonSeyirciServisi.Footprint(sek);
            if (fp.Count < 4) continue;

            var geo = new StreamGeometry();
            using (var g = geo.Open())
            {
                g.BeginFigure(W2S(fp[0]), true);
                for (int i = 1; i < fp.Count; i++) g.LineTo(W2S(fp[i]));
                g.EndFigure(true);
            }
            ctx.DrawGeometry(SeyirciFill, SeyirciPen, geo);

            foreach (var (a, b) in SalonSeyirciServisi.KademeSinirlari(sek))
                ctx.DrawLine(SeyirciKademePen, W2S(a), W2S(b));

            var c = W2S(new Point2D(sek.MerkezX, sek.MerkezY));
            var ft = new FormattedText("SEYİRCİ", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                Typeface.Default, 11, SeyirciText);
            ctx.DrawText(ft, new Point(c.X - ft.Width / 2, c.Y - ft.Height / 2));
        }
    }

    private void DrawBalkon(DrawingContext ctx)
    {
        if (_salon is null) return;
        foreach (var b in _salon.Balkonlar)
        {
            var fp = SalonBalkonServisi.Footprint(b);
            if (fp.Count < 4) continue;

            var geo = new StreamGeometry();
            using (var g = geo.Open())
            {
                g.BeginFigure(W2S(fp[0]), true);
                for (int i = 1; i < fp.Count; i++) g.LineTo(W2S(fp[i]));
                g.EndFigure(true);
            }
            ctx.DrawGeometry(BalkonFill, BalkonPen, geo);

            var c = W2S(new Point2D(b.MerkezX, b.MerkezY));
            string et = $"{(b.Tip == Kst.Acoustics.Core.Salon.Models.BalkonTipi.Loca ? "LOCA" : "BALKON")} @{b.TabanKotuM:F1}m";
            var ft = new FormattedText(et, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                Typeface.Default, 11, BalkonText);
            ctx.DrawText(ft, new Point(c.X - ft.Width / 2, c.Y - ft.Height / 2));
        }
    }

    /// Tavan profili kırılım çizgileri: her kırılım Y'sinde içerik genişliğince kesikli
    /// yatay çizgi + kot etiketi (plan üzerinde kesit ipucu).
    private static readonly Pen KapiPen = new(new SolidColorBrush(Color.Parse("#C4813C")), 4.5);
    private static readonly Pen PencerePen = new(new SolidColorBrush(Color.Parse("#5FB6D8")), 4.5);
    private static readonly SolidColorBrush AciklikText = new(Color.Parse("#E8C89A"));

    /// Açıklık işaretleri: en yakın duvar segmenti boyunca kalın kısa çizgi + etiket.
    private void DrawAcikliklar(DrawingContext ctx)
    {
        if (_salon is null || _salon.Acikliklar.Count == 0) return;

        foreach (var ac in _salon.Acikliklar)
        {
            // En yakın segment yönünü bul (0,5 m tolerans).
            var merkez = new Point2D(ac.MerkezX, ac.MerkezY);
            (Point2D A, Point2D B)? enYakin = null;
            double bestD = 0.5;
            foreach (var o in _salon.CizimOgeleri)
            {
                if (!KatmanGorunur(o)) continue;
                foreach (var (a, b) in o.Segmentler())
                {
                    var c = EnYakinNoktaSegment(a, b, merkez);
                    double d = Mesafe(c, merkez);
                    if (d < bestD) { bestD = d; enYakin = (a, b); }
                }
            }

            var pen = ac.Tip == Kst.Acoustics.Core.Salon.Models.AciklikTipi.Pencere ? PencerePen : KapiPen;
            if (enYakin is { } seg)
            {
                double dx = seg.B.X - seg.A.X, dy = seg.B.Y - seg.A.Y;
                double L = Math.Sqrt(dx * dx + dy * dy);
                if (L > 1e-9)
                {
                    double ux = dx / L, uy = dy / L, g2 = ac.GenislikM / 2;
                    ctx.DrawLine(pen,
                        W2S(new Point2D(merkez.X - ux * g2, merkez.Y - uy * g2)),
                        W2S(new Point2D(merkez.X + ux * g2, merkez.Y + uy * g2)));
                }
            }
            else
            {
                var s = W2S(merkez); // duvara bağlı değil → çarpı işareti
                ctx.DrawLine(pen, new Point(s.X - 5, s.Y - 5), new Point(s.X + 5, s.Y + 5));
                ctx.DrawLine(pen, new Point(s.X - 5, s.Y + 5), new Point(s.X + 5, s.Y - 5));
            }

            var etiket = ac.Tip == Kst.Acoustics.Core.Salon.Models.AciklikTipi.Pencere ? "P" : "K";
            var ft = new FormattedText($"{etiket} {ac.GenislikM:F1}m", CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Typeface.Default, 10, AciklikText);
            var mp = W2S(merkez);
            ctx.DrawText(ft, new Point(mp.X + 7, mp.Y - ft.Height - 3));
        }
    }

    private static readonly SolidColorBrush CukurFill = new(Color.Parse("#4A9EA8"), 0.13);
    private static readonly SolidColorBrush CukurText = new(Color.Parse("#7FC8D2"));
    private static readonly Pen CukurPen = new(new SolidColorBrush(Color.Parse("#4A9EA8")), 1.5)
        { DashStyle = new DashStyle([4, 3], 0) };

    private void DrawCukur(DrawingContext ctx)
    {
        var c = _salon?.OrkestraCukuru;
        if (c is null) return;
        var fp = c.Footprint();

        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(W2S(fp[0]), true);
            for (int i = 1; i < fp.Count; i++) g.LineTo(W2S(fp[i]));
            g.EndFigure(true);
        }
        ctx.DrawGeometry(CukurFill, CukurPen, geo);

        var m = W2S(new Point2D(c.MerkezX, c.MerkezY));
        var ft = new FormattedText($"ORKESTRA −{c.CukurDerinligiM:F1}m", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 10.5, CukurText);
        ctx.DrawText(ft, new Point(m.X - ft.Width / 2, m.Y - ft.Height / 2));
    }

    private void DrawTavanProfili(DrawingContext ctx)
    {
        if (_salon is null || _salon.TavanProfili.Count < 2) return;

        double minX = double.MaxValue, maxX = double.MinValue;
        foreach (var o in _salon.CizimOgeleri)
            foreach (var (a, b) in o.Segmentler())
            {
                minX = Math.Min(minX, Math.Min(a.X, b.X));
                maxX = Math.Max(maxX, Math.Max(a.X, b.X));
            }
        if (!double.IsFinite(minX)) return;

        foreach (var n in _salon.TavanProfili.OrderBy(t => t.YM))
        {
            var p0 = W2S(new Point2D(minX - 1.5, n.YM));
            var p1 = W2S(new Point2D(maxX + 1.5, n.YM));
            ctx.DrawLine(TavanKirilimPen, p0, p1);

            var ft = new FormattedText($"tavan {n.KotM:F1} m", CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Typeface.Default, 10, TavanKirilimText);
            ctx.DrawText(ft, new Point(p1.X + 6, p1.Y - ft.Height / 2));
        }
    }

    private void DrawOlcuEtiketi(DrawingContext ctx)
    {
        if (_fareWorld is null) return;
        bool olcumAktif = _arac == SalonAraci.Olcum && _cizimNoktalar.Count == 1;
        if (!CizimReferansiVar && !olcumAktif) return;
        var f = _fareWorld.Value;

        string text;
        if (_sayiGiris.Length > 0)
        {
            text = $"{_sayiGiris} ⏎";
        }
        else if (_arac == SalonAraci.Daire)
        {
            text = $"R = {Mesafe(_cizimNoktalar[0], f):F2} m";
        }
        else if (_arac == SalonAraci.Dondur)
        {
            var m = _cizimNoktalar[0];
            double aci = Math.Atan2(f.Y - m.Y, f.X - m.X) * 180.0 / Math.PI;
            if (_cizimNoktalar.Count >= 2)
                aci -= Math.Atan2(_cizimNoktalar[1].Y - m.Y, _cizimNoktalar[1].X - m.X) * 180.0 / Math.PI;
            while (aci <= -180) aci += 360;
            while (aci > 180) aci -= 360;
            text = $"∠ = {aci:F1}°";
        }
        else if (_cizimNoktalar.Count > 0)
        {
            var son = _cizimNoktalar[^1];
            double d = Mesafe(son, f);
            double aci = Math.Atan2(f.Y - son.Y, f.X - son.X) * 180.0 / Math.PI;
            text = $"L = {d:F2} m  <{aci:F1}°";
        }
        else
        {
            return; // Offset: mesafe imleç uzaklığından, etiket gereksiz
        }

        var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            Typeface.Default, 12.5, OlcuText);
        var sp = W2S(f);
        var pos = new Point(sp.X + 14, sp.Y - 28);
        ctx.FillRectangle(OlcuBack, new Rect(pos.X - 5, pos.Y - 3, ft.Width + 10, ft.Height + 6), 4);
        ctx.DrawText(ft, pos);
    }

    private void DrawGrid(DrawingContext ctx, Rect b)
    {
        double step = 1.0; // metre
        double pxStep = step * Scale;
        while (pxStep < 8) { step *= 5; pxStep = step * Scale; }   // çok sık → seyrelt
        while (pxStep > 120) { step /= 5; pxStep = step * Scale; } // çok seyrek → sıklaştır

        var tl = S2W(new Point(0, 0));
        var br = S2W(new Point(b.Width, b.Height));
        double x0 = Math.Floor(tl.X / step) * step;
        double x1 = Math.Ceiling(br.X / step) * step;
        double y0 = Math.Floor(br.Y / step) * step;
        double y1 = Math.Ceiling(tl.Y / step) * step;

        for (double x = x0; x <= x1; x += step)
        {
            var p0 = W2S(new Point2D(x, y0));
            var p1 = W2S(new Point2D(x, y1));
            bool major = Math.Abs(Math.Round(x / (step * 5)) * (step * 5) - x) < step * 0.25;
            ctx.DrawLine(major ? GridMajorPen : GridPen, p0, p1);
        }
        for (double y = y0; y <= y1; y += step)
        {
            var p0 = W2S(new Point2D(x0, y));
            var p1 = W2S(new Point2D(x1, y));
            bool major = Math.Abs(Math.Round(y / (step * 5)) * (step * 5) - y) < step * 0.25;
            ctx.DrawLine(major ? GridMajorPen : GridPen, p0, p1);
        }

        // Eksenler (0,0)
        var ox0 = W2S(new Point2D(x0, 0)); var ox1 = W2S(new Point2D(x1, 0));
        var oy0 = W2S(new Point2D(0, y0)); var oy1 = W2S(new Point2D(0, y1));
        ctx.DrawLine(AxisXPen, ox0, ox1);
        ctx.DrawLine(AxisYPen, oy0, oy1);
    }

    private void DrawOge(DrawingContext ctx, SalonCizimOgesi o, bool secili)
    {
        var pen = secili ? SecPen : new Pen(OgeFircasi(o), 1.8);

        if (o.Tip == SalonCizimTipi.Daire)
        {
            if (o.Noktalar.Count == 0) return;
            var c = W2S(o.Noktalar[0]);
            double r = o.YariCapM * Scale;
            if (secili) ctx.DrawEllipse(SecFill, pen, c, r, r);
            else ctx.DrawEllipse(null, pen, c, r, r);
            return;
        }

        if (o.Tip == SalonCizimTipi.Yay)
        {
            var fp = o.YayiFasetle(96); // ekranda pürüzsüz görünüm
            if (fp.Count < 2) return;
            var yayGeo = new StreamGeometry();
            using (var g = yayGeo.Open())
            {
                g.BeginFigure(W2S(fp[0]), false);
                for (int i = 1; i < fp.Count; i++) g.LineTo(W2S(fp[i]));
                g.EndFigure(false);
            }
            ctx.DrawGeometry(null, pen, yayGeo);
            return;
        }

        var geo = new StreamGeometry();
        bool kapali = o.Tip is SalonCizimTipi.Dikdortgen || (o.Tip == SalonCizimTipi.Polyline && o.Kapali);
        using (var g = geo.Open())
        {
            var pts = o.Noktalar;
            if (pts.Count == 0) return;
            g.BeginFigure(W2S(pts[0]), kapali && secili);
            for (int i = 1; i < pts.Count; i++) g.LineTo(W2S(pts[i]));
            if (kapali) g.LineTo(W2S(pts[0]));
            g.EndFigure(kapali);
        }
        ctx.DrawGeometry(secili && kapali ? SecFill : null, pen, geo);
    }

    private void DrawOnizleme(DrawingContext ctx)
    {
        if (_cizimNoktalar.Count == 0 || _fareWorld is null) return;
        var fare = _fareWorld.Value;

        switch (_arac)
        {
            case SalonAraci.Cizgi:
            case SalonAraci.Polyline:
                for (int i = 0; i + 1 < _cizimNoktalar.Count; i++)
                    ctx.DrawLine(OnizPen, W2S(_cizimNoktalar[i]), W2S(_cizimNoktalar[i + 1]));
                ctx.DrawLine(OnizPen, W2S(_cizimNoktalar[^1]), W2S(fare));
                break;

            case SalonAraci.Dikdortgen:
                var a = _cizimNoktalar[0];
                var geo = new StreamGeometry();
                using (var g = geo.Open())
                {
                    g.BeginFigure(W2S(a), false);
                    g.LineTo(W2S(new Point2D(fare.X, a.Y)));
                    g.LineTo(W2S(fare));
                    g.LineTo(W2S(new Point2D(a.X, fare.Y)));
                    g.LineTo(W2S(a));
                    g.EndFigure(true);
                }
                ctx.DrawGeometry(null, OnizPen, geo);
                break;

            case SalonAraci.Daire:
                var m = _cizimNoktalar[0];
                double r = Mesafe(m, fare) * Scale;
                ctx.DrawEllipse(null, OnizPen, W2S(m), r, r);
                break;

            case SalonAraci.Yay:
                if (_cizimNoktalar.Count == 1)
                {
                    ctx.DrawLine(OnizPen, W2S(_cizimNoktalar[0]), W2S(fare));
                }
                else
                {
                    var yay = SalonCizimOgesi.UcNoktadanYay(_cizimNoktalar[0], _cizimNoktalar[1], fare);
                    if (yay is null)
                    {
                        ctx.DrawLine(OnizPen, W2S(_cizimNoktalar[0]), W2S(_cizimNoktalar[1]));
                    }
                    else
                    {
                        var fp = yay.YayiFasetle(96);
                        for (int i = 0; i + 1 < fp.Count; i++)
                            ctx.DrawLine(OnizPen, W2S(fp[i]), W2S(fp[i + 1]));
                    }
                }
                break;

            case SalonAraci.Tasi:
            {
                var dx = fare.X - _cizimNoktalar[0].X;
                var dy = fare.Y - _cizimNoktalar[0].Y;
                ctx.DrawLine(OnizPen, W2S(_cizimNoktalar[0]), W2S(fare));
                SecimHayaleti(ctx, p => new Point2D(p.X + dx, p.Y + dy));
                break;
            }

            case SalonAraci.Aynala:
            {
                var e1 = _cizimNoktalar[0];
                ctx.DrawLine(OnizPen, W2S(e1), W2S(fare));
                double adx = fare.X - e1.X, ady = fare.Y - e1.Y;
                double len2 = adx * adx + ady * ady;
                if (len2 > 1e-12)
                    SecimHayaleti(ctx, p =>
                    {
                        double t = ((p.X - e1.X) * adx + (p.Y - e1.Y) * ady) / len2;
                        return new Point2D(2 * (e1.X + t * adx) - p.X, 2 * (e1.Y + t * ady) - p.Y);
                    });
                break;
            }

            case SalonAraci.Dondur:
            {
                var mk = _cizimNoktalar[0];
                ctx.DrawLine(OnizPen, W2S(mk), W2S(fare));
                if (_cizimNoktalar.Count >= 2)
                {
                    ctx.DrawLine(OnizPen, W2S(mk), W2S(_cizimNoktalar[1]));
                    double aci = Math.Atan2(fare.Y - mk.Y, fare.X - mk.X)
                                 - Math.Atan2(_cizimNoktalar[1].Y - mk.Y, _cizimNoktalar[1].X - mk.X);
                    double cos = Math.Cos(aci), sin = Math.Sin(aci);
                    SecimHayaleti(ctx, p =>
                    {
                        double px = p.X - mk.X, py = p.Y - mk.Y;
                        return new Point2D(mk.X + px * cos - py * sin, mk.Y + px * sin + py * cos);
                    });
                }
                break;
            }

            case SalonAraci.Olcum:
                ctx.DrawLine(OnizPen, W2S(_cizimNoktalar[0]), W2S(fare));
                break;
        }
    }

    /// Seçili öğelerin dönüştürülmüş hayaletini çizer (taşı/aynala/döndür önizlemesi).
    private void SecimHayaleti(DrawingContext ctx, Func<Point2D, Point2D> donusum)
    {
        foreach (var o in _secim)
            foreach (var (a, b) in o.Segmentler(24))
                ctx.DrawLine(OnizPen, W2S(donusum(a)), W2S(donusum(b)));
    }

    private void DrawSnap(DrawingContext ctx)
    {
        if (_snapNokta is null) return;
        var s = W2S(_snapNokta.Value);
        switch (_snapTipi)
        {
            case SnapTip.Vertex:   // kare
                ctx.DrawRectangle(null, SnapPen, new Rect(s.X - 4.5, s.Y - 4.5, 9, 9));
                break;
            case SnapTip.Kesisim:  // çarpı
                ctx.DrawLine(SnapPen, new Point(s.X - 5, s.Y - 5), new Point(s.X + 5, s.Y + 5));
                ctx.DrawLine(SnapPen, new Point(s.X - 5, s.Y + 5), new Point(s.X + 5, s.Y - 5));
                break;
            case SnapTip.Orta:     // üçgen
                var g = new StreamGeometry();
                using (var gc = g.Open())
                {
                    gc.BeginFigure(new Point(s.X, s.Y - 5.5), false);
                    gc.LineTo(new Point(s.X + 5, s.Y + 4));
                    gc.LineTo(new Point(s.X - 5, s.Y + 4));
                    gc.EndFigure(true);
                }
                ctx.DrawGeometry(null, SnapPen, g);
                break;
            case SnapTip.Merkez:   // daire içinde nokta
                ctx.DrawEllipse(null, SnapPen, s, 5, 5);
                ctx.DrawEllipse(SnapFill, null, s, 1.5, 1.5);
                break;
            case SnapTip.Dik:      // dik açı simgesi (köşe)
                ctx.DrawLine(SnapPen, new Point(s.X - 5, s.Y + 5), new Point(s.X - 5, s.Y - 1));
                ctx.DrawLine(SnapPen, new Point(s.X - 5, s.Y + 5), new Point(s.X + 1, s.Y + 5));
                ctx.DrawRectangle(null, SnapPen, new Rect(s.X - 5, s.Y - 1, 6, 6));
                break;
            case SnapTip.Kenar:    // daire (manyetik)
                ctx.DrawEllipse(null, SnapPen, s, 4.5, 4.5);
                break;
        }
    }

    /// Pencere seçimi bandı: soldan sağa mavi (window), sağdan sola yeşil kesikli (crossing).
    private void DrawSecimBandi(DrawingContext ctx)
    {
        if (_bandBaslangic is not { } bas) return;
        var rect = new Rect(
            Math.Min(bas.X, _bandSon.X), Math.Min(bas.Y, _bandSon.Y),
            Math.Abs(_bandSon.X - bas.X), Math.Abs(_bandSon.Y - bas.Y));
        bool crossing = _bandSon.X < bas.X;
        ctx.FillRectangle(crossing ? BandCrossFill : BandWinFill, rect);
        ctx.DrawRectangle(null, crossing ? BandCrossPen : BandWinPen, rect);
    }

    private void DrawVertexHandles(DrawingContext ctx)
    {
        if (Birincil is not { } o) return;
        foreach (var n in o.Noktalar)
        {
            var s = W2S(n);
            ctx.DrawRectangle(VertexFill, VertexPen, new Rect(s.X - 3.5, s.Y - 3.5, 7, 7));
        }
    }
}
