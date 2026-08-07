using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Kst.Acoustics.App.ViewModels;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Simulation;
using System.Linq;

namespace Kst.Acoustics.App.Controls;

public sealed partial class GeometryPreviewControl : Control
{
    private const double Padding = 24d;
    private const double MinZoom = 0.2d;
    private const double MaxZoom = 25d;
    private const double ClickThreshold = 4d;
    private const double RulerTargetPixelLength = 140d;
    private const double RotationSensitivity = 0.005d;
    private const double SnapPixelRadius = 11d;

    private Point _pan;
    private double _zoom = 1d;
    private bool _isPanning;
    private bool _isRotating;
    // B5 — kat hizalama drag durumu
    private bool _isAligning;
    private Point _alignDragPressScreen;
    private double _alignDragPxPerUnit;
    private Point _pointerDownPoint;
    private KeyModifiers _pointerDownModifiers;
    private Point _panStart;

    // 3D orbit angles (radians)
    private double _azimuth = 0.8d;   // horizontal rotation
    private double _elevation = 0.5d;  // vertical rotation

    // ── Projeksiyon önbelleği ────────────────────────────────────────────────
    // ProjectPoint (dünya→düzlem) zoom/pan'den bağımsızdır; yalnızca projeksiyon
    // modu + 3D rotasyon + sınırlar değişince yeniden hesaplanır. Zoom/pan sadece
    // ucuz bir affine dönüşümdür. Bu önbellek olmadan büyük DXF'lerde her zoom/pan
    // karesinde on binlerce segment yeniden projekte ediliyordu → donma.
    private IReadOnlyList<ProjectedSurface>? _cProjSurfaces;
    private SceneSetup? _cSetup;
    private StreamGeometry? _cBgGeom;       // arka plan segmentleri, base (zoom/pan öncesi) koordinatta tek geometri
    private StreamGeometry? _cAciklikAktifGeom; // aktif açıklıklar (kırmızı)
    private StreamGeometry? _cAciklikIptalGeom; // iptal açıklıklar (gri)
    private object? _cKeySurfaces, _cKeyBg, _cKeyAciklik;
    private GeometryProjectionMode _cKeyProj = (GeometryProjectionMode)(-1);
    private double _cKeyAz = double.NaN, _cKeyEl = double.NaN;
    private double _cKeyW = -1, _cKeyH = -1;

    public GeometryPreviewControl()
    {
        ClipToBounds = true;
    }

    public static readonly StyledProperty<IReadOnlyList<ProjectSurface>?> SurfacesProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, IReadOnlyList<ProjectSurface>?>(nameof(Surfaces));

    public static readonly StyledProperty<GeometryProjectionMode> ProjectionProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, GeometryProjectionMode>(nameof(Projection), GeometryProjectionMode.Top2D);

    public static readonly StyledProperty<int?> HighlightedSurfaceIdProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, int?>(nameof(HighlightedSurfaceId));

    public static readonly StyledProperty<IReadOnlyList<int>?> SelectedSurfaceIdsProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, IReadOnlyList<int>?>(nameof(SelectedSurfaceIds));

    public static readonly StyledProperty<IReadOnlyList<Point3D>?> LeakTestPathProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, IReadOnlyList<Point3D>?>(nameof(LeakTestPath));

    public static readonly StyledProperty<string> UnitLabelProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, string>(nameof(UnitLabel), "m");

    public static readonly StyledProperty<IReadOnlyList<AcousticPoint>?> SourcePointsProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, IReadOnlyList<AcousticPoint>?>(nameof(SourcePoints));

    public static readonly StyledProperty<IReadOnlyList<AcousticPoint>?> ReceiverPointsProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, IReadOnlyList<AcousticPoint>?>(nameof(ReceiverPoints));

    public static readonly StyledProperty<PointPlacementMode> PlacementModeProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, PointPlacementMode>(nameof(PlacementMode));

    public static readonly StyledProperty<IReadOnlyDictionary<int, double>?> HitIntensityMapProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, IReadOnlyDictionary<int, double>?>(nameof(HitIntensityMap));

    public static readonly StyledProperty<IReadOnlyList<RaySegment>?> RayPathsProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, IReadOnlyList<RaySegment>?>(nameof(RayPaths));

    public static readonly StyledProperty<IReadOnlySet<int>?> MirrorSurfaceIdsProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, IReadOnlySet<int>?>(nameof(MirrorSurfaceIds));

    public static readonly StyledProperty<IReadOnlySet<int>?> ReceiverSurfaceIdsProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, IReadOnlySet<int>?>(nameof(ReceiverSurfaceIds));

    public static readonly StyledProperty<IReadOnlyList<LightHitPoint>?> ReceiverHitPointsProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, IReadOnlyList<LightHitPoint>?>(nameof(ReceiverHitPoints));

    /// DXF ham çizgi segmentleri — yüzeylerin altında soluk gri çizgilerle çizilir.
    /// Liste her elemanı (A, B) çift uç-noktası içerir.
    public static readonly StyledProperty<IReadOnlyList<(Point3D A, Point3D B)>?> BackgroundSegmentsProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, IReadOnlyList<(Point3D A, Point3D B)>?>(nameof(BackgroundSegments));

    public IReadOnlyList<(Point3D A, Point3D B)>? BackgroundSegments
    {
        get => GetValue(BackgroundSegmentsProperty);
        set => SetValue(BackgroundSegmentsProperty, value);
    }

    /// Açıklıklar (kapı + pencere) — kalın kırmızı çizgi; iptal edilmiş olanlar soluk gri.
    /// (Pos, IptalEdildi) çiftleri; lokal kat koordinatında değil, dünya koordinatında olmalı.
    public static readonly StyledProperty<IReadOnlyList<(Point3D Bas, Point3D Bit, bool Iptal, BpcAciklikTipi Tip)>?> AcikliklarProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, IReadOnlyList<(Point3D Bas, Point3D Bit, bool Iptal, BpcAciklikTipi Tip)>?>(nameof(Acikliklar));

    public IReadOnlyList<(Point3D Bas, Point3D Bit, bool Iptal, BpcAciklikTipi Tip)>? Acikliklar
    {
        get => GetValue(AcikliklarProperty);
        set => SetValue(AcikliklarProperty, value);
    }

    /// Seçili açıklığın vurgu segmenti — turuncu kalın çizgi ile gösterilir.
    public static readonly StyledProperty<(Point3D Bas, Point3D Bit)?> HighlightAciklikSegmentProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, (Point3D Bas, Point3D Bit)?>(nameof(HighlightAciklikSegment));

    public (Point3D Bas, Point3D Bit)? HighlightAciklikSegment
    {
        get => GetValue(HighlightAciklikSegmentProperty);
        set => SetValue(HighlightAciklikSegmentProperty, value);
    }

    /// Salt-görsel hafif 3D modu (M12 gibi 3D'yi yalnız bakmak için kullanan modüller).
    /// Açıkken 3D'de: ham DXF arka plan çizgileri ve eleman yazı etiketleri çizilmez,
    /// döndürme sırasında eleman/kablo katmanları atlanır (bırakınca tam detay döner).
    /// Varsayılan kapalı → akustik modüller (M08 vb.) etkilenmez.
    public static readonly StyledProperty<bool> Lightweight3DProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, bool>(nameof(Lightweight3D));

    public bool Lightweight3D
    {
        get => GetValue(Lightweight3DProperty);
        set => SetValue(Lightweight3DProperty, value);
    }

    // Hafif 3D yardımcıları
    private bool Hafif3D => Lightweight3D && Projection == GeometryProjectionMode.Isometric3D;

    public static readonly StyledProperty<IReadOnlyList<(Point3D A, Point3D B, string Renk)>?> KabloSegmentleriProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, IReadOnlyList<(Point3D A, Point3D B, string Renk)>?>(nameof(KabloSegmentleri));

    public IReadOnlyList<(Point3D A, Point3D B, string Renk)>? KabloSegmentleri
    {
        get => GetValue(KabloSegmentleriProperty);
        set => SetValue(KabloSegmentleriProperty, value);
    }

    /// Seçili hat segmentleri — kalın + parlak çizilir.
    public static readonly StyledProperty<IReadOnlyList<(Point3D A, Point3D B, string Renk)>?> HighlightKabloSegmentleriProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, IReadOnlyList<(Point3D A, Point3D B, string Renk)>?>(nameof(HighlightKabloSegmentleri));

    public IReadOnlyList<(Point3D A, Point3D B, string Renk)>? HighlightKabloSegmentleri
    {
        get => GetValue(HighlightKabloSegmentleriProperty);
        set => SetValue(HighlightKabloSegmentleriProperty, value);
    }

    public static readonly StyledProperty<IReadOnlyList<(Point3D Position, string Text, string Renk)>?> KabloEtiketleriProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, IReadOnlyList<(Point3D Position, string Text, string Renk)>?>(nameof(KabloEtiketleri));

    public IReadOnlyList<(Point3D Position, string Text, string Renk)>? KabloEtiketleri
    {
        get => GetValue(KabloEtiketleriProperty);
        set => SetValue(KabloEtiketleriProperty, value);
    }

    public static readonly StyledProperty<IEnumerable<string>?> CopiedSurfaceIdsProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, IEnumerable<string>?>(nameof(CopiedSurfaceIds));

    public IEnumerable<string>? CopiedSurfaceIds
    {
        get => GetValue(CopiedSurfaceIdsProperty);
        set => SetValue(CopiedSurfaceIdsProperty, value);
    }

    /// Çoklu seçili odaların Id'leri (OwnerId ile eşleşir) — camgöbeği vurgu çizilir.
    public static readonly StyledProperty<IEnumerable<string>?> SelectedRoomIdsProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, IEnumerable<string>?>(nameof(SelectedRoomIds));

    public IEnumerable<string>? SelectedRoomIds
    {
        get => GetValue(SelectedRoomIdsProperty);
        set => SetValue(SelectedRoomIdsProperty, value);
    }

    /// Komşu duvarlar — yüzeylerin üstünde altın çizgilerle vurgulanır. Selected=true
    /// olanlar daha kalın/parlak çizilir ve uzunluk etiketi gösterilir.
    public static readonly StyledProperty<IReadOnlyList<(Point3D A, Point3D B, bool Selected)>?> NeighborSegmentsProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, IReadOnlyList<(Point3D A, Point3D B, bool Selected)>?>(nameof(NeighborSegments));

    public IReadOnlyList<(Point3D A, Point3D B, bool Selected)>? NeighborSegments
    {
        get => GetValue(NeighborSegmentsProperty);
        set => SetValue(NeighborSegmentsProperty, value);
    }

    /// UX — Flanking yolu görselleştirmesi (top-3 yolu renkli okla viewport'ta gösterir).
    public static readonly StyledProperty<IReadOnlyList<(Point3D A, Point3D B, string Renk)>?> FlankingYolSegmentleriProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, IReadOnlyList<(Point3D A, Point3D B, string Renk)>?>(nameof(FlankingYolSegmentleri));

    public IReadOnlyList<(Point3D A, Point3D B, string Renk)>? FlankingYolSegmentleri
    {
        get => GetValue(FlankingYolSegmentleriProperty);
        set => SetValue(FlankingYolSegmentleriProperty, value);
    }

    /// Top2D modunda kullanıcının sürükleyerek düzenleyebileceği oda köşeleri.
    /// Z koordinatı projeksiyonda 0 kabul edilir; Top2D Project() X/Y'yi koruyacak.
    public static readonly StyledProperty<IReadOnlyList<Point3D>?> EditablePolygonProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, IReadOnlyList<Point3D>?>(nameof(EditablePolygon));

    public IReadOnlyList<Point3D>? EditablePolygon
    {
        get => GetValue(EditablePolygonProperty);
        set => SetValue(EditablePolygonProperty, value);
    }

    /// Düzenlenebilir poligondaki bir köşe sürüklendiğinde çağrılır.
    /// Argümanlar: (vertexIndex, newWorldX, newWorldY).
    public Action<int, double, double>? OnVertexDragged { get; set; }

    /// Düzenlenebilir poligonda bir kenar üzerinde çift tıklandığında çağrılır (yeni köşe eklemek için).
    /// Argümanlar: (insertAfterIndex, worldX, worldY).
    public Action<int, double, double>? OnEdgeDoubleClicked { get; set; }

    /// Bir kenar sürüklendiğinde — kenarın iki ucunu birlikte taşımak için.
    /// Argümanlar: (vertexIndexA, newAx, newAy, vertexIndexB, newBx, newBy).
    public Action<int, double, double, int, double, double>? OnEdgeDragged { get; set; }

    /// Köşe/kenar düzenleme sürüklemesi başladığında çağrılır; undo snapshot için.
    public Action? OnEditDragStarted { get; set; }

    /// Bir köşe üzerine sağ tıklandığında — köşeyi silmek için.
    public Action<int>? OnVertexDeleted { get; set; }

    /// Boş bir noktaya (vertex/edge dışı) tek tıklandığında çağrılır — viewmodel
    /// hangi mekan içine düştüğünü hesaplayıp SeciliMekan'ı değiştirebilir.
    /// Üçüncü parametre: Ctrl/Cmd basılıysa true (çoklu seçim isteği).
    public Action<double, double, bool>? OnViewportClicked { get; set; }

    /// Top2D'de boş zemine sağ-tık. Host (Window) açıklığa hit-test edip
    /// context menu açabilir. Parametreler: world X, world Y, control-relative
    /// ekran konumu (MenuFlyout.ShowAt için).
    public Action<double, double, Point>? OnViewportRightClicked { get; set; }

    private int _draggingVertexIndex = -1;
    private int _draggingEdgeStart = -1;                  // kenar sürüklemede başlangıç köşe indeksi (j = i+1)
    private Point _dragPressScreen;                       // sürükleme başındaki ekran konumu
    private double _dragPxPerUnit;                        // sürükleme başındaki sabit ekran→dünya ölçeği
    private double _dragVertexStartX, _dragVertexStartY;  // tek vertex için başlangıç dünya koordinatı
    private double _edgeDragV0X, _edgeDragV0Y, _edgeDragV1X, _edgeDragV1Y; // kenar uç koordinatları
    private double _edgeDragNormalX, _edgeDragNormalY;                    // kenar sürüklemede izin verilen iki yönlü eksen
    private bool _isDraggingEleman;

    // ── Bant (rubber-band) seçim: Shift + sol sürükleme ─────────────────────
    private bool _isBanding;
    private Avalonia.Point _bandStartScreen;
    private Avalonia.Point _bandCurrentScreen;
    /// Bant seçiminin aktifleşip aktifleşmeyeceğini host belirler (ör. kablo
    /// modunda hat açıkken). Null/false ise Shift+sürükleme normal pan yapar.
    public Func<bool>? BandSecimAktifMi { get; set; }
    /// Bant bırakılınca dünya koordinatlarında köşe çifti (x1,y1,x2,y2 — sıralanmamış).
    public Action<double, double, double, double>? OnBandSelected { get; set; }
    private bool _elemanDragMoved;
    private const double VertexHitRadius = 10d; // piksel
    private const double EdgeHitRadius   = 6d;  // piksel

    /// Host, bu callback'i uygularsa pointer press sırasında eleman hit-test yapılır.
    /// true döndürürse eleman sürükleme moduna geçilir (pan engellenir).
    public Func<double, double, bool>? IsElemanAtPoint { get; set; }
    /// Eleman sürükleme başladığında çağrılır — dünya koordinatları (metre).
    public Action<double, double, bool>? OnElemanDragStarted { get; set; }
    /// Eleman sürüklenirken her harekette çağrılır — dünya koordinatları (metre).
    public Action<double, double>? OnElemanDragMoved { get; set; }
    /// Eleman sürükleme bırakıldığında çağrılır — dünya koordinatları (metre).
    public Action<double, double>? OnElemanDragCommitted { get; set; }
    /// Eleman üzerine taşımasız (salt) tıklama bırakıldığında çağrılır. Argüman: multiSelect.
    public Action<bool>? OnElemanClickNoDrag { get; set; }

    /// <summary>Viewport tıklamasında çağrılır. Argümanlar: (projX, projY, projection).</summary>
    public Action<double, double, GeometryProjectionMode>? OnPlacementPicked { get; set; }

    /// <summary>Sağ tıklama olayı (özellikle çizimi bitirmek için kullanılır).</summary>
    public Func<bool>? OnRightClicked { get; set; }

    /// <summary>Viewport üzerinde çift tıklama (herhangi bir oda seçimi vb. için).</summary>
    public Action<double, double>? OnViewportDoubleClicked { get; set; }

    /// Oda çizim modu: 0=kapalı, 1=poligon çiziliyor.
    public static readonly StyledProperty<int> OdaEkleModuProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, int>(nameof(OdaEkleModu));

    public int OdaEkleModu
    {
        get => GetValue(OdaEkleModuProperty);
        set => SetValue(OdaEkleModuProperty, value);
    }

    /// Şimdiye kadar tıklanan poligon noktaları (oda çizim önizlemesi).
    public static readonly StyledProperty<IReadOnlyList<Point3D>?> OdaEkleNoktalarProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, IReadOnlyList<Point3D>?>(nameof(OdaEkleNoktalar));

    public IReadOnlyList<Point3D>? OdaEkleNoktalar
    {
        get => GetValue(OdaEkleNoktalarProperty);
        set => SetValue(OdaEkleNoktalarProperty, value);
    }

    /// Rubber-band çizgisinin ucu (anlık fare konumu).
    public static readonly StyledProperty<Point3D?> OdaEkleFareProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, Point3D?>(nameof(OdaEkleFare));

    public Point3D? OdaEkleFare
    {
        get => GetValue(OdaEkleFareProperty);
        set => SetValue(OdaEkleFareProperty, value);
    }

    /// Oda çizim modunda fare hareket ettiğinde çağrılır (dünya koordinatı).
    public Action<double, double>? OnRoomDrawMouseMoved { get; set; }

    /// Manuel açıklık çizim modu: iki tıkla kapı/pencere açıklığı ekler.
    public static readonly StyledProperty<bool> ManuelAciklikModuProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, bool>(nameof(ManuelAciklikModu));

    public bool ManuelAciklikModu
    {
        get => GetValue(ManuelAciklikModuProperty);
        set => SetValue(ManuelAciklikModuProperty, value);
    }

    // B5 — Kat hizalama (sürükle-bırak) modu
    public static readonly StyledProperty<bool> KatHizalamaModuProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, bool>(nameof(KatHizalamaModu));

    public bool KatHizalamaModu
    {
        get => GetValue(KatHizalamaModuProperty);
        set => SetValue(KatHizalamaModuProperty, value);
    }

    /// UX — Fare hareketi sırasında dünya koord (Top2D) iletilir; ViewModel bölme hit-test yapar.
    public Action<double, double>? OnHoverWorldPoint { get; set; }
    public Action? OnHoverLeave { get; set; }

    public Action? OnKatHizalamaDragBaslat { get; set; }
    /// dxWorld, dyWorld başlangıçtan beri toplam yer değiştirme (kumulatif).
    public Action<double, double>? OnKatHizalamaDragGuncel { get; set; }
    public Action? OnKatHizalamaDragBitis { get; set; }

    public static readonly StyledProperty<IReadOnlyList<Point3D>?> ManuelAciklikNoktalarProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, IReadOnlyList<Point3D>?>(nameof(ManuelAciklikNoktalar));

    public IReadOnlyList<Point3D>? ManuelAciklikNoktalar
    {
        get => GetValue(ManuelAciklikNoktalarProperty);
        set => SetValue(ManuelAciklikNoktalarProperty, value);
    }

    public static readonly StyledProperty<Point3D?> ManuelAciklikFareProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, Point3D?>(nameof(ManuelAciklikFare));

    public Point3D? ManuelAciklikFare
    {
        get => GetValue(ManuelAciklikFareProperty);
        set => SetValue(ManuelAciklikFareProperty, value);
    }

    public Action<double, double>? OnManualOpeningMouseMoved { get; set; }

    /// Malzeme panelinde seçili duvarın başlangıç noktası (vurgu için).
    public static readonly StyledProperty<Point3D?> SelectedWallAProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, Point3D?>(nameof(SelectedWallA));

    public Point3D? SelectedWallA
    {
        get => GetValue(SelectedWallAProperty);
        set => SetValue(SelectedWallAProperty, value);
    }

    /// Malzeme panelinde seçili duvarın bitiş noktası (vurgu için).
    public static readonly StyledProperty<Point3D?> SelectedWallBProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, Point3D?>(nameof(SelectedWallB));

    public Point3D? SelectedWallB
    {
        get => GetValue(SelectedWallBProperty);
        set => SetValue(SelectedWallBProperty, value);
    }

    /// Eleman listesinden seçilen elemanın viewport'ta vurgulanacağı dünya konumu.
    public static readonly StyledProperty<Point3D?> HighlightedPointPositionProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, Point3D?>(nameof(HighlightedPointPosition));

    public Point3D? HighlightedPointPosition
    {
        get => GetValue(HighlightedPointPositionProperty);
        set => SetValue(HighlightedPointPositionProperty, value);
    }

    public static readonly StyledProperty<IReadOnlyList<Point3D>?> HighlightedPointPositionsProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, IReadOnlyList<Point3D>?>(nameof(HighlightedPointPositions));

    public IReadOnlyList<Point3D>? HighlightedPointPositions
    {
        get => GetValue(HighlightedPointPositionsProperty);
        set => SetValue(HighlightedPointPositionsProperty, value);
    }

    /// İmleç altındaki (tıklanacak) hedef elemanın ön-vurgusu — turkuaz halka.
    /// Kablo modunda hangi elemanın yakalanacağını tıklamadan önce gösterir.
    public static readonly StyledProperty<Point3D?> HoverPointPositionProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, Point3D?>(nameof(HoverPointPosition));

    public Point3D? HoverPointPosition
    {
        get => GetValue(HoverPointPositionProperty);
        set => SetValue(HoverPointPositionProperty, value);
    }

    /// Aday eleman vurguları (ör. anahtar seçilince odadaki boş armatürler) —
    /// soluk yeşil kesikli halkalar; "buraya tıklanabilir" ipucu.
    public static readonly StyledProperty<IReadOnlyList<Point3D>?> CandidatePointPositionsProperty =
        AvaloniaProperty.Register<GeometryPreviewControl, IReadOnlyList<Point3D>?>(nameof(CandidatePointPositions));

    public IReadOnlyList<Point3D>? CandidatePointPositions
    {
        get => GetValue(CandidatePointPositionsProperty);
        set => SetValue(CandidatePointPositionsProperty, value);
    }

    static GeometryPreviewControl()
    {
        AffectsRender<GeometryPreviewControl>(
            SurfacesProperty, ProjectionProperty, HighlightedSurfaceIdProperty,
            SelectedSurfaceIdsProperty, LeakTestPathProperty, UnitLabelProperty,
            SourcePointsProperty, ReceiverPointsProperty, PlacementModeProperty,
            HitIntensityMapProperty, RayPathsProperty, MirrorSurfaceIdsProperty,
            ReceiverSurfaceIdsProperty, ReceiverHitPointsProperty, BackgroundSegmentsProperty, EditablePolygonProperty,
            NeighborSegmentsProperty, FlankingYolSegmentleriProperty, OdaEkleModuProperty, OdaEkleNoktalarProperty,
            OdaEkleFareProperty, ManuelAciklikModuProperty, ManuelAciklikNoktalarProperty,
            ManuelAciklikFareProperty, SelectedWallAProperty, SelectedWallBProperty,
            KabloSegmentleriProperty, KabloEtiketleriProperty, HighlightedPointPositionProperty,
            HighlightedPointPositionsProperty, SelectedRoomIdsProperty,
            HighlightKabloSegmentleriProperty, AcikliklarProperty, HighlightAciklikSegmentProperty,
            HoverPointPositionProperty, CandidatePointPositionsProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ProjectionProperty)
        {
            ResetViewport();
        }
        else if (change.Property == SurfacesProperty)
        {
            // Yalnızca ilk yüklemede (önce null/boş, şimdi dolu) viewport'u sıfırla.
            // Aksi halde sürükleme sırasında Surfaces güncellemesi pan/zoom'u bozar.
            var oldList = change.GetOldValue<IReadOnlyList<ProjectSurface>?>();
            var newList = change.GetNewValue<IReadOnlyList<ProjectSurface>?>();
            bool wasEmpty = oldList is null || oldList.Count == 0;
            bool isFilled = newList is not null && newList.Count > 0;
            if (wasEmpty && isFilled) ResetViewport();
        }
    }

    public IReadOnlyList<ProjectSurface>? Surfaces
    {
        get => GetValue(SurfacesProperty);
        set => SetValue(SurfacesProperty, value);
    }

    public GeometryProjectionMode Projection
    {
        get => GetValue(ProjectionProperty);
        set => SetValue(ProjectionProperty, value);
    }

    public int? HighlightedSurfaceId
    {
        get => GetValue(HighlightedSurfaceIdProperty);
        set => SetValue(HighlightedSurfaceIdProperty, value);
    }

    public IReadOnlyList<int>? SelectedSurfaceIds
    {
        get => GetValue(SelectedSurfaceIdsProperty);
        set => SetValue(SelectedSurfaceIdsProperty, value);
    }

    public IReadOnlyList<Point3D>? LeakTestPath
    {
        get => GetValue(LeakTestPathProperty);
        set => SetValue(LeakTestPathProperty, value);
    }

    public string UnitLabel
    {
        get => GetValue(UnitLabelProperty);
        set => SetValue(UnitLabelProperty, value);
    }

    public IReadOnlyList<AcousticPoint>? SourcePoints
    {
        get => GetValue(SourcePointsProperty);
        set => SetValue(SourcePointsProperty, value);
    }

    public IReadOnlyList<AcousticPoint>? ReceiverPoints
    {
        get => GetValue(ReceiverPointsProperty);
        set => SetValue(ReceiverPointsProperty, value);
    }

    public PointPlacementMode PlacementMode
    {
        get => GetValue(PlacementModeProperty);
        set => SetValue(PlacementModeProperty, value);
    }

    public IReadOnlyDictionary<int, double>? HitIntensityMap
    {
        get => GetValue(HitIntensityMapProperty);
        set => SetValue(HitIntensityMapProperty, value);
    }

    public IReadOnlyList<RaySegment>? RayPaths
    {
        get => GetValue(RayPathsProperty);
        set => SetValue(RayPathsProperty, value);
    }

    public IReadOnlySet<int>? MirrorSurfaceIds
    {
        get => GetValue(MirrorSurfaceIdsProperty);
        set => SetValue(MirrorSurfaceIdsProperty, value);
    }

    public IReadOnlySet<int>? ReceiverSurfaceIds
    {
        get => GetValue(ReceiverSurfaceIdsProperty);
        set => SetValue(ReceiverSurfaceIdsProperty, value);
    }

    public IReadOnlyList<LightHitPoint>? ReceiverHitPoints
    {
        get => GetValue(ReceiverHitPointsProperty);
        set => SetValue(ReceiverHitPointsProperty, value);
    }







    private double SnapToleranceWorld => _dragPxPerUnit > 1e-9
        ? Math.Clamp(SnapPixelRadius / _dragPxPerUnit, 0.025, 0.18)
        : 0.10;



    private static Point ClosestPointOnSegment(Point p, Point a, Point b)
    {
        double vx = b.X - a.X;
        double vy = b.Y - a.Y;
        double len2 = vx * vx + vy * vy;
        if (len2 <= 1e-12)
            return a;

        double t = ((p.X - a.X) * vx + (p.Y - a.Y) * vy) / len2;
        t = Math.Clamp(t, 0, 1);
        return new Point(a.X + vx * t, a.Y + vy * t);
    }

    private static double Distance(Point a, Point b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Ekran koordinatını model koordinatına dönüştürür (Top2D ve Front2D için).
    /// Top2D:   projX = model.X,  projY = model.Y
    /// Front2D: projX = model.X,  projY = model.Z
    /// Isometric3D için false döner.
    /// </summary>
    public bool TryGetWorldPoint(Point screenPoint, out double projX, out double projY)
    {
        projX = projY = 0;
        if (Projection == GeometryProjectionMode.Isometric3D) return false;

        var scene = BuildScene(Bounds);
        if (scene is null) return false;

        var center = Bounds.Center;
        var setup  = scene.Setup;

        // Ekran → temel koordinat
        var baseX = center.X + (screenPoint.X - center.X - _pan.X) / _zoom;
        var baseY = center.Y + (screenPoint.Y - center.Y - _pan.Y) / _zoom;

        // Temel → projeksiyon koordinatı
        projX = setup.MinX + (baseX - setup.OffsetX) / setup.BaseScale;
        projY = setup.MaxY - (baseY - setup.OffsetY) / setup.BaseScale;
        return true;
    }






    public void ResetViewport()
    {
        _zoom = 1d;
        _pan = default;
        _azimuth = 0.8d;
        _elevation = 0.5d;
        InvalidateVisual();
    }

    public void FocusSurface(int? surfaceId)
    {
        if (surfaceId is null)
        {
            ResetViewport();
            return;
        }

        var bounds = Bounds;
        if (bounds.Width <= 1 || bounds.Height <= 1)
        {
            return;
        }

        var projectedSurfaces = BuildProjectedSurfaces();
        if (projectedSurfaces is null || projectedSurfaces.Length == 0)
        {
            return;
        }

        var target = projectedSurfaces.FirstOrDefault(s => s.Surface.Id == surfaceId.Value);
        if (target is null)
        {
            return;
        }

        var sceneSetup = BuildSceneSetup(bounds, projectedSurfaces);

        var basePoints = target.ProjectedPoints
            .Select(point => new Point(
                sceneSetup.OffsetX + ((point.X - sceneSetup.MinX) * sceneSetup.BaseScale),
                sceneSetup.OffsetY + ((sceneSetup.MaxY - point.Y) * sceneSetup.BaseScale)))
            .ToArray();

        var minX = basePoints.Min(p => p.X);
        var maxX = basePoints.Max(p => p.X);
        var minY = basePoints.Min(p => p.Y);
        var maxY = basePoints.Max(p => p.Y);
        var width = Math.Max(1d, maxX - minX);
        var height = Math.Max(1d, maxY - minY);

        var targetZoom = Math.Min((bounds.Width * 0.7d) / width, (bounds.Height * 0.7d) / height);
        _zoom = Math.Clamp(targetZoom, MinZoom, MaxZoom);

        var targetCenter = new Point((minX + maxX) / 2d, (minY + maxY) / 2d);
        var viewCenter = bounds.Center;
        _pan = new Point(
            -((targetCenter.X - viewCenter.X) * _zoom),
            -((targetCenter.Y - viewCenter.Y) * _zoom));

        InvalidateVisual();
    }



    private Point ProjectPoint(Point3D point)
    {
        return Projection switch
        {
            GeometryProjectionMode.Top2D => new Point(point.X, point.Y),
            GeometryProjectionMode.Front2D => new Point(point.X, point.Z),
            GeometryProjectionMode.Isometric3D => ProjectIsometric3D(point),
            _ => new Point(point.X, point.Y)
        };
    }

    private Point ProjectIsometric3D(Point3D p)
    {
        // Orbit camera: rotate around Y then X using azimuth/elevation
        var cosA = Math.Cos(_azimuth);
        var sinA = Math.Sin(_azimuth);
        var cosE = Math.Cos(_elevation);
        var sinE = Math.Sin(_elevation);

        // Rotate around Y axis (azimuth)
        var x1 = p.X * cosA - p.Y * sinA;
        var y1 = p.X * sinA + p.Y * cosA;
        var z1 = p.Z;

        // Rotate around X axis (elevation)
        var y2 = y1 * cosE - z1 * sinE;
        var z2 = y1 * sinE + z1 * cosE;

        // Orthographic projection: drop depth (y2), use x1 as screen X and z2 as screen Y
        return new Point(x1, z2);
    }

    private SceneModel? BuildScene(Rect bounds)
    {
        if (bounds.Width <= 1 || bounds.Height <= 1)
        {
            return null;
        }

        EnsureProjCache(bounds);
        var projectedSurfaces = _cProjSurfaces ?? Array.Empty<ProjectedSurface>();
        var sceneSetup = _cSetup!;
        var center = bounds.Center;

        // Zoom/pan yalnızca ucuz affine; trig projeksiyon önbellekten gelir.
        var screenSurfaces = projectedSurfaces
            .Select(item =>
            {
                var points = item.ProjectedPoints
                    .Select(point =>
                    {
                        var basePoint = new Point(
                            sceneSetup.OffsetX + ((point.X - sceneSetup.MinX) * sceneSetup.BaseScale),
                            sceneSetup.OffsetY + ((sceneSetup.MaxY - point.Y) * sceneSetup.BaseScale));

                        return center + _pan + ((basePoint - center) * _zoom);
                    })
                    .ToArray();

                return new ScreenSurface(item.Surface, points);
            })
            .ToArray();

        return new SceneModel(center, screenSurfaces, sceneSetup.BaseScale * _zoom, sceneSetup);
    }

    // Projeksiyon önbelleğini gerekiyorsa yeniden kurar (referans/mod/rotasyon/sınır değişince).
    private void EnsureProjCache(Rect bounds)
    {
        bool same =
            ReferenceEquals(_cKeySurfaces, Surfaces) &&
            ReferenceEquals(_cKeyBg, BackgroundSegments) &&
            ReferenceEquals(_cKeyAciklik, Acikliklar) &&
            _cKeyProj == Projection &&
            _cKeyAz == _azimuth && _cKeyEl == _elevation &&
            _cKeyW == bounds.Width && _cKeyH == bounds.Height;
        if (same && _cSetup is not null) return;

        // Yüzeyleri projekte et (trig burada, kare başı değil)
        var surfaces = Surfaces;
        var projSurfaces = (surfaces is null || surfaces.Count == 0)
            ? Array.Empty<ProjectedSurface>()
            : surfaces
                .Select(s => new ProjectedSurface(s, s.Vertices.Select(ProjectPoint).ToArray()))
                .Where(item => item.ProjectedPoints.Count >= 3)
                .ToArray();

        // Arka plan segment uçlarını projekte et.
        // Hafif 3D modunda ham DXF arka planı çizilmez → projeksiyonu/geometriyi hiç
        // kurma (en büyük döndürme maliyeti budur; sahne sınırları yüzeylerden gelir).
        var bg = Hafif3D ? null : BackgroundSegments;
        Point[]? bgProj = null;
        if (bg is { Count: > 0 })
        {
            bgProj = new Point[bg.Count * 2];
            for (int i = 0; i < bg.Count; i++)
            {
                bgProj[2 * i]     = ProjectPoint(bg[i].A);
                bgProj[2 * i + 1] = ProjectPoint(bg[i].B);
            }
        }

        var setup = BuildSceneSetup(bounds, projSurfaces, bgProj);

        // Arka plan segmentlerini base (zoom/pan öncesi) koordinatta tek geometriye topla
        StreamGeometry? geom = null;
        if (bgProj is not null)
        {
            geom = new StreamGeometry();
            using var gc = geom.Open();
            for (int i = 0; i < bgProj.Length; i += 2)
            {
                var a = bgProj[i];
                var b = bgProj[i + 1];
                if (double.IsNaN(a.X) || double.IsNaN(b.X)) continue;
                var ba = new Point(
                    setup.OffsetX + ((a.X - setup.MinX) * setup.BaseScale),
                    setup.OffsetY + ((setup.MaxY - a.Y) * setup.BaseScale));
                var bb = new Point(
                    setup.OffsetX + ((b.X - setup.MinX) * setup.BaseScale),
                    setup.OffsetY + ((setup.MaxY - b.Y) * setup.BaseScale));
                gc.BeginFigure(ba, false);
                gc.LineTo(bb);
                gc.EndFigure(false);
            }
        }

        // Açıklık geometrilerini hazırla (aktif kırmızı + iptal gri)
        StreamGeometry? aciklikAktif = null, aciklikIptal = null;
        var acikliklar = Acikliklar;
        if (acikliklar is { Count: > 0 })
        {
            aciklikAktif = new StreamGeometry();
            aciklikIptal = new StreamGeometry();
            using var gcA = aciklikAktif.Open();
            using var gcI = aciklikIptal.Open();
            foreach (var (bas3, bit3, iptal, _) in acikliklar)
            {
                var pA = ProjectPoint(bas3);
                var pB = ProjectPoint(bit3);
                if (double.IsNaN(pA.X) || double.IsNaN(pB.X)) continue;
                var ba = new Point(
                    setup.OffsetX + ((pA.X - setup.MinX) * setup.BaseScale),
                    setup.OffsetY + ((setup.MaxY - pA.Y) * setup.BaseScale));
                var bb = new Point(
                    setup.OffsetX + ((pB.X - setup.MinX) * setup.BaseScale),
                    setup.OffsetY + ((setup.MaxY - pB.Y) * setup.BaseScale));
                var target = iptal ? gcI : gcA;
                target.BeginFigure(ba, false);
                target.LineTo(bb);
                target.EndFigure(false);
            }
        }

        _cProjSurfaces = projSurfaces;
        _cSetup = setup;
        _cBgGeom = geom;
        _cAciklikAktifGeom = aciklikAktif;
        _cAciklikIptalGeom = aciklikIptal;
        _cKeySurfaces = Surfaces;
        _cKeyBg = BackgroundSegments;
        _cKeyAciklik = Acikliklar;
        _cKeyProj = Projection;
        _cKeyAz = _azimuth;
        _cKeyEl = _elevation;
        _cKeyW = bounds.Width;
        _cKeyH = bounds.Height;
    }

    // Base (zoom/pan öncesi) koordinattan ekran koordinatına affine matris.
    private Matrix ZoomPanMatrix(Rect bounds)
    {
        var c = bounds.Center;
        double tx = _pan.X + c.X * (1 - _zoom);
        double ty = _pan.Y + c.Y * (1 - _zoom);
        return new Matrix(_zoom, 0, 0, _zoom, tx, ty);
    }

    private int? HitTestSurfaceId(Point position)
    {
        var scene = BuildScene(Bounds);
        if (scene is null)
        {
            return null;
        }

        for (var i = scene.Surfaces.Count - 1; i >= 0; i--)
        {
            var surface = scene.Surfaces[i];
            if (IsPointInPolygon(position, surface.ScreenPoints))
            {
                return surface.Surface.Id;
            }
        }

        return null;
    }

    private ProjectedSurface[]? BuildProjectedSurfaces()
    {
        var surfaces = Surfaces;
        if (surfaces is null || surfaces.Count == 0)
        {
            return Array.Empty<ProjectedSurface>();
        }

        return surfaces
            .Select(surface => new ProjectedSurface(surface, surface.Vertices.Select(ProjectPoint).ToArray()))
            .Where(item => item.ProjectedPoints.Count >= 3)
            .ToArray();
    }

    private static SceneSetup BuildSceneSetup(Rect bounds, IReadOnlyList<ProjectedSurface> projectedSurfaces, IReadOnlyList<Point>? extraPoints = null)
    {
        var validPoints = projectedSurfaces.SelectMany(x => x.ProjectedPoints).Where(p => !double.IsNaN(p.X)).ToList();
        if (extraPoints is not null)
            validPoints.AddRange(extraPoints.Where(p => !double.IsNaN(p.X)));
        if (validPoints.Count == 0) return new SceneSetup(0, 0, 1, 0, 0);

        var minX = validPoints.Min(x => x.X);
        var maxX = validPoints.Max(x => x.X);
        var minY = validPoints.Min(x => x.Y);
        var maxY = validPoints.Max(x => x.Y);

        var modelWidth = Math.Max(1e-6, maxX - minX);
        var modelHeight = Math.Max(1e-6, maxY - minY);
        var baseScale = Math.Min((bounds.Width - (Padding * 2d)) / modelWidth, (bounds.Height - (Padding * 2d)) / modelHeight);
        baseScale = double.IsFinite(baseScale) && baseScale > 0 ? baseScale : 1d;

        var offsetX = Padding + ((bounds.Width - (Padding * 2d)) - (modelWidth * baseScale)) / 2d;
        var offsetY = Padding + ((bounds.Height - (Padding * 2d)) - (modelHeight * baseScale)) / 2d;

        return new SceneSetup(minX, maxY, baseScale, offsetX, offsetY);
    }

    private static Rect GetScreenBounds(IReadOnlyList<Point> points)
    {
        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxY = double.MinValue;
        foreach (var p in points)
        {
            if (double.IsNaN(p.X)) continue;
            if (p.X < minX) minX = p.X; if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y; if (p.Y > maxY) maxY = p.Y;
        }
        return new Rect(new Point(minX, minY), new Point(maxX, maxY));
    }

    private static bool IsPointInPolygon(Point point, IReadOnlyList<Point> polygon)
    {
        var inside = false;

        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];

            if (double.IsNaN(pi.X) || double.IsNaN(pj.X)) continue;

            var intersects = ((pi.Y > point.Y) != (pj.Y > point.Y)) &&
                             (point.X < ((pj.X - pi.X) * (point.Y - pi.Y) / ((pj.Y - pi.Y) + double.Epsilon)) + pi.X);

            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }






    /// EditablePolygon köşelerinden screenPoint'e en yakın olanın indeksini döner (toleransta), yoksa -1.
    private int HitTestVertex(Point screenPoint)
    {
        var poly = EditablePolygon;
        if (poly is null || poly.Count == 0) return -1;
        var scene = BuildScene(Bounds);
        if (scene is null) return -1;

        var center = Bounds.Center;
        var setup  = scene.Setup;
        int bestIdx = -1; double bestD = VertexHitRadius * VertexHitRadius;
        for (int i = 0; i < poly.Count; i++)
        {
            var proj = ProjectPoint(poly[i]);
            var basePt = new Point(
                setup.OffsetX + ((proj.X - setup.MinX) * setup.BaseScale),
                setup.OffsetY + ((setup.MaxY - proj.Y) * setup.BaseScale));
            var sp = center + _pan + ((basePt - center) * _zoom);
            double dx = sp.X - screenPoint.X, dy = sp.Y - screenPoint.Y;
            double d2 = dx * dx + dy * dy;
            if (d2 < bestD) { bestD = d2; bestIdx = i; }
        }
        return bestIdx;
    }

    /// EditablePolygon kenarlarından birine yakın bir piksele tıklandı mı? (vertex hariç)
    private bool HitTestEdge(Point screenPoint, out int edgeStartIdx)
    {
        edgeStartIdx = -1;
        var poly = EditablePolygon;
        if (poly is null || poly.Count < 2) return false;
        // Köşe hit'i önceliklidir; köşeye düşen tıklamaları kenara saymayalım
        if (HitTestVertex(screenPoint) >= 0) return false;
        var scene = BuildScene(Bounds);
        if (scene is null) return false;
        var center = Bounds.Center;
        var setup = scene.Setup;
        Point ToScreen(Point3D p)
        {
            var proj = ProjectPoint(p);
            var basePt = new Point(
                setup.OffsetX + ((proj.X - setup.MinX) * setup.BaseScale),
                setup.OffsetY + ((setup.MaxY - proj.Y) * setup.BaseScale));
            return center + _pan + ((basePt - center) * _zoom);
        }
        double bestD = EdgeHitRadius * EdgeHitRadius;
        for (int i = 0; i < poly.Count; i++)
        {
            int j = (i + 1) % poly.Count;
            var a = ToScreen(poly[i]);
            var b = ToScreen(poly[j]);
            double abx = b.X - a.X, aby = b.Y - a.Y;
            double len2 = abx * abx + aby * aby;
            if (len2 < 1e-6) continue;
            double t = ((screenPoint.X - a.X) * abx + (screenPoint.Y - a.Y) * aby) / len2;
            if (t < 0 || t > 1) continue;
            double px = a.X + t * abx, py = a.Y + t * aby;
            double dx = px - screenPoint.X, dy = py - screenPoint.Y;
            double d2 = dx * dx + dy * dy;
            if (d2 < bestD) { bestD = d2; edgeStartIdx = i; }
        }
        return edgeStartIdx >= 0;
    }

    /// EditablePolygon kenarlarından birine yakın çift-tıklamaysa, hangi indeksin
    /// SONRASINA köşe ekleneceğini ve eklenen noktanın dünya koordinatını döner.
    private bool TryHitEdge(Point screenPoint, out int insertAfterIdx, out double worldX, out double worldY)
    {
        insertAfterIdx = -1; worldX = worldY = 0;
        var poly = EditablePolygon;
        if (poly is null || poly.Count < 2) return false;
        var scene = BuildScene(Bounds);
        if (scene is null) return false;

        var center = Bounds.Center;
        var setup  = scene.Setup;
        Point ToScreen(Point3D p)
        {
            var proj = ProjectPoint(p);
            var basePt = new Point(
                setup.OffsetX + ((proj.X - setup.MinX) * setup.BaseScale),
                setup.OffsetY + ((setup.MaxY - proj.Y) * setup.BaseScale));
            return center + _pan + ((basePt - center) * _zoom);
        }

        int bestIdx = -1; double bestD = VertexHitRadius * VertexHitRadius;
        for (int i = 0; i < poly.Count; i++)
        {
            int j = (i + 1) % poly.Count;
            var a = ToScreen(poly[i]);
            var b = ToScreen(poly[j]);
            double abx = b.X - a.X, aby = b.Y - a.Y;
            double len2 = abx * abx + aby * aby;
            if (len2 < 1e-6) continue;
            double t = ((screenPoint.X - a.X) * abx + (screenPoint.Y - a.Y) * aby) / len2;
            if (t < 0 || t > 1) continue;
            double px = a.X + t * abx, py = a.Y + t * aby;
            double dx = px - screenPoint.X, dy = py - screenPoint.Y;
            double d2 = dx * dx + dy * dy;
            if (d2 < bestD) { bestD = d2; bestIdx = i; }
        }
        if (bestIdx < 0) return false;
        if (!TryGetWorldPoint(screenPoint, out worldX, out worldY)) return false;
        insertAfterIdx = bestIdx;
        return true;
    }














    private static Point3D Add(Point3D origin, Kst.Acoustics.Core.Geometry.Vector3D direction, double length) =>
        new(
            origin.X + direction.X * length,
            origin.Y + direction.Y * length,
            origin.Z + direction.Z * length);

    private static Kst.Acoustics.Core.Geometry.Vector3D DirectionFromAngles(double azDeg, double elDeg)
    {
        var az = azDeg * Math.PI / 180.0;
        var el = elDeg * Math.PI / 180.0;
        return new Kst.Acoustics.Core.Geometry.Vector3D(
            Math.Cos(el) * Math.Cos(az),
            Math.Cos(el) * Math.Sin(az),
            Math.Sin(el)).Normalize();
    }







    private sealed record ProjectedSurface(ProjectSurface Surface, IReadOnlyList<Point> ProjectedPoints);

    private sealed record ScreenSurface(ProjectSurface Surface, IReadOnlyList<Point> ScreenPoints);

    private sealed record SceneModel(Point ViewCenter, IReadOnlyList<ScreenSurface> Surfaces, double PixelsPerUnit, SceneSetup Setup);

    private sealed record SceneSetup(double MinX, double MaxY, double BaseScale, double OffsetX, double OffsetY);
}
