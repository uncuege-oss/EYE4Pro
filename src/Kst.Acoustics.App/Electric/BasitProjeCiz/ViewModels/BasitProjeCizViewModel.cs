using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kst.Acoustics.App.Controls;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.App.Electric.BasitProjeCiz.ViewModels;

public partial class BasitProjeCizViewModel : ObservableObject
{
    private readonly BasitProjeCizKutuphaneServisi _kutuphane = new();
    /// Sembol kütüphanesine salt-okunur erişim (mahal listesi ön-tahmini vb.).
    public BasitProjeCizKutuphaneServisi Kutuphane => _kutuphane;
    private readonly BasitProjeCizGeometriServisi _geometri = new();

    [ObservableProperty] private BasitProjeCizProje _proje = new();
    [ObservableProperty] private BasitProjeKat? _seciliKat;
    [ObservableProperty] private BasitProjeOda? _seciliOda;
    [ObservableProperty] private BasitProjeEleman? _seciliEleman;
    [ObservableProperty] private BpcArmatürTanim? _seciliArmatür;
    [ObservableProperty] private string? _projeDosyaYolu;
    [ObservableProperty] private string _durumMesaji = "M50 bina geometrisini (.e4bina) yükleyerek başlayın.";

    // Sensör modu
    [ObservableProperty] private bool _sensörluMod;

    // Kablo modu
    [ObservableProperty] private bool   _kabloModuAktif;
    [ObservableProperty] private int    _kabloModuDurumu; // 0=anahtar bekleniyor, 1=armatür atama, 2=vavien 2. anahtar, 3=buat-pano için buat
    [ObservableProperty] private string _kabloModuMesaji = "";
    [ObservableProperty] private BpcRotaTipi _kabloRotaTipi = BpcRotaTipi.DaisyChain;
    [ObservableProperty] private int    _komutatorDevreNo = 1;
    public bool KomutatorDevre1Mi => KomutatorDevreNo == 1;
    public bool KomutatorDevre2Mi => KomutatorDevreNo == 2;
    public bool KomutatorDevre3Mi => KomutatorDevreNo == 3;
    partial void OnKomutatorDevreNoChanged(int value)
    {
        OnPropertyChanged(nameof(KomutatorDevre1Mi));
        OnPropertyChanged(nameof(KomutatorDevre2Mi));
        OnPropertyChanged(nameof(KomutatorDevre3Mi));
    }
    [ObservableProperty] private bool   _bostakileriGosterAktif;
    [ObservableProperty] private bool   _kabloTavasiCizimAktif;
    [ObservableProperty] private bool   _kabloTavasiDuzCizimAktif;
    [ObservableProperty] private bool   _kabloTavasiTaliHatModuAktif;
    [ObservableProperty] private int    _kabloTavasiAdet = 1;
    [ObservableProperty] private double _kabloTavasiTaliGenislikMm = 50;
    [ObservableProperty] private double _kabloTavasiGenislikMm = 100;
    [ObservableProperty] private double _kabloTavasiYukseklikMm = 50;
    [ObservableProperty] private double _kabloTavasiEtKalinligiMm = 1.0;
    [ObservableProperty] private bool   _kabloTavasiAgirHizmet;
    [ObservableProperty] private double _kabloTavasiKotM = 2.70;
    [ObservableProperty] private BpcKabloTavasi? _seciliKabloTavasi;
    [ObservableProperty] private bool _topraklamaAlanSecimAktif;
    [ObservableProperty] private bool _topraklamaKazikEkleModuAktif;
    [ObservableProperty] private BpcPanoBeslemeUygulamaTipi _beslemeUygulamaTipi = BpcPanoBeslemeUygulamaTipi.Kablo;
    [ObservableProperty] private BasitProjeEleman? _seciliAnaBeslemePanosu;

    private BasitProjeEleman? _seciliKabloAnahtari;
    private bool              _aydinlatmaMahalModu; // Mahal Modu: odaya tıklayınca odasındaki boş armatürleri otomatik bağla
    private bool              _prizMahalModu;        // Priz Mahal Modu: odaya tıklayınca prizleri priz buatına bağla
    private BpcDevre?         _aktifDevre;
    private BasitProjeEleman? _buatPanoSeciliBuat;
    private BpcPrizDevre?     _aktifPrizDevre;
    private BasitProjeEleman? _seciliKlima;
    private BpcSistemDevre?   _aktifSistemDevre;
    private readonly List<Point2D> _kabloTavasiCizimNoktalar = new();
    public IReadOnlyList<double> KabloTavasiEtKalinligiSecenekleri { get; } = [0.8, 1.0, 1.2, 1.5, 2.0];
    public IReadOnlyList<BpcPanoBeslemeUygulamaTipi> BeslemeUygulamaTipleri { get; } =
        [BpcPanoBeslemeUygulamaTipi.Kablo, BpcPanoBeslemeUygulamaTipi.Busbar];
    public ObservableCollection<BasitProjeEleman> TumKatPanolari { get; } = new();

    [ObservableProperty]
    private IReadOnlyList<(Point3D A, Point3D B, string Renk)>? _renderKabloSegmentleri;
    [ObservableProperty]
    private IReadOnlyList<(Point3D A, Point3D B, string Renk)>? _renderKabloVurgu;
    [ObservableProperty]
    private IReadOnlyList<(Point3D Position, string Text, string Renk)>? _renderKabloEtiketleri;

    // 3D görünüm
    [ObservableProperty] private List<ProjectSurface>? _renderSurfaces;
    [ObservableProperty] private List<(Point3D A, Point3D B)>? _renderBackgroundSegments;
    [ObservableProperty] private IReadOnlyList<AcousticPoint>? _renderArmatürNoktalar;
    [ObservableProperty] private IReadOnlyList<AcousticPoint>? _renderManuelNoktalar;
    [ObservableProperty] private IReadOnlyList<(Point3D Bas, Point3D Bit, bool Iptal, BpcAciklikTipi Tip)>? _renderAcikliklar;
    [ObservableProperty] private GeometryProjectionMode _projection = GeometryProjectionMode.Top2D;

    // Seçili açıklık — seçim/silme için
    private BpcAciklik? _seciliAciklik;
    private BasitProjeKat? _seciliAciklikKat;

    public BpcAciklik? SeciliAciklik
    {
        get => _seciliAciklik;
        private set
        {
            _seciliAciklik = value;
            OnPropertyChanged(nameof(RenderSeciliAciklikSegment));
            OnPropertyChanged(nameof(SeciliAciklikVar));
            OnPropertyChanged(nameof(SeciliAciklikTipi));
        }
    }

    public IReadOnlyList<BpcAciklikTipi> AciklikTipleri { get; } =
        [BpcAciklikTipi.Kapı, BpcAciklikTipi.Pencere];

    public bool SeciliAciklikVar => _seciliAciklik is not null;

    public BpcAciklikTipi SeciliAciklikTipi
    {
        get => _seciliAciklik?.Tip ?? BpcAciklikTipi.Kapı;
        set => SeciliAciklikTipiAta(value);
    }

    /// Seçili açıklığın dünya koordinatındaki segment; highlight için GeometryPreviewControl'e bağlanır.
    public (Point3D Bas, Point3D Bit)? RenderSeciliAciklikSegment
    {
        get
        {
            if (_seciliAciklik is null || _seciliAciklikKat is null) return null;
            double tabanZ = 0;
            foreach (var kat in Katlar)
            {
                if (kat == _seciliAciklikKat)
                {
                    double ox = kat.OriginOffset.X, oy = kat.OriginOffset.Y;
                    return (
                        new Point3D(_seciliAciklik.Bas.X + ox, _seciliAciklik.Bas.Y + oy, tabanZ),
                        new Point3D(_seciliAciklik.Bit.X + ox, _seciliAciklik.Bit.Y + oy, tabanZ));
                }
                tabanZ += kat.KatYuksekligiM;
            }
            return null;
        }
    }

    /// En yakın açıklığı (tüm katlar içinde) seçer. Hit toleransı 0.5 m.
    public bool TrySelectAciklik(double worldX, double worldY)
    {
        const double tol = 0.5;
        BpcAciklik? best = null;
        BasitProjeKat? bestKat = null;
        double bestDist = tol;

        foreach (var kat in Katlar)
        {
            double ox = kat.OriginOffset.X, oy = kat.OriginOffset.Y;
            var p = new Point2D(worldX - ox, worldY - oy);
            foreach (var a in kat.Acikliklar.Where(a => !a.IptalEdildi))
            {
                double d = PoligonYardimci.SegmentMesafesi(p, a.Bas, a.Bit);
                if (d < bestDist) { bestDist = d; best = a; bestKat = kat; }
            }
        }

        if (best is null) return false;
        _seciliAciklikKat = bestKat;
        SeciliAciklik = best;
        DurumMesaji = $"Açıklık seçildi ({best.Tip}, {best.Kaynak}) — tipini değiştirebilir, Delete ile silebilirsin.";
        return true;
    }

    public void SeciliAciklikTemizle()
    {
        _seciliAciklikKat = null;
        SeciliAciklik = null;
    }

    private bool SeciliAciklikTipiAta(BpcAciklikTipi tip)
    {
        if (_seciliAciklik is null) return false;
        if (ProjeKilitli)
        {
            DurumMesaji = "Proje kilitli — açıklık tipini değiştirmek için önce kilidi açın.";
            OnPropertyChanged(nameof(SeciliAciklikTipi));
            return false;
        }
        if (_seciliAciklik.Tip == tip) return true;

        _seciliAciklik.Tip = tip;
        DurumMesaji = $"Açıklık tipi {tip} olarak işaretlendi.";
        OnPropertyChanged(nameof(SeciliAciklikTipi));
        OnPropertyChanged(nameof(AciklikOzeti));
        Guncelle3DGorunum();
        return true;
    }

    [RelayCommand]
    public void SeciliAcikligiKapiYap() => SeciliAciklikTipiAta(BpcAciklikTipi.Kapı);

    [RelayCommand]
    public void SeciliAcikligiPencereYap() => SeciliAciklikTipiAta(BpcAciklikTipi.Pencere);

    [RelayCommand]
    public void SeciliAciklikiSil()
    {
        if (_seciliAciklik is null || _seciliAciklikKat is null) return;
        if (KilitliyseDur("açıklık silmek")) return;
        _seciliAciklikKat.Acikliklar.Remove(_seciliAciklik);
        _seciliAciklikKat = null;
        SeciliAciklik = null;
        DurumMesaji = "Açıklık silindi.";
        OnPropertyChanged(nameof(AciklikOzeti));
        Guncelle3DGorunum();
    }

    /// Seçili açıklığı listede tutar ama IptalEdildi=true yapar.
    /// Yeniden tarama sırasında manuel/iptal kayıtlar korunur → false-positive
    /// bir daha üretilmez.
    [RelayCommand]
    public void SeciliAciklikiIptalEt()
    {
        if (_seciliAciklik is null || _seciliAciklikKat is null) return;
        if (KilitliyseDur("açıklık iptal etmek")) return;
        _seciliAciklik.IptalEdildi = true;
        DurumMesaji = "Açıklık iptal edildi (yeniden taramada gelmez).";
        OnPropertyChanged(nameof(AciklikOzeti));
        OnPropertyChanged(nameof(RenderSeciliAciklikSegment));
        Guncelle3DGorunum();
    }

    /// Tüm katlarda otomatik gap kaynaklı (Kaynak="gap" ve manuel olmayan)
    /// açıklıkları siler. Kullanıcı toplu temizlik için.
    [RelayCommand]
    public void OtomatikGapleriSil()
    {
        if (Katlar.Count == 0) { DurumMesaji = "Önce kat yükleyin."; return; }
        if (KilitliyseDur("açıklıkları temizlemek")) return;
        int silinen = 0;
        foreach (var kat in Katlar)
        {
            silinen += kat.Acikliklar.RemoveAll(a =>
                a.Kaynak == "gap" && !a.ManuelEklendi && !a.IptalEdildi);
        }
        SeciliAciklik = null;
        _seciliAciklikKat = null;
        DurumMesaji = $"{silinen} otomatik gap açıklığı silindi.";
        OnPropertyChanged(nameof(AciklikOzeti));
        Guncelle3DGorunum();
    }

    /// Tüm katlardaki açıklık özetini döner: "Açıklık: A aktif (G gap + R arc) / I iptal / M manuel".
    public string AciklikOzeti
    {
        get
        {
            int aktif = 0, iptal = 0, manuel = 0, gap = 0, arc = 0;
            int kapi = 0, pencere = 0;
            foreach (var kat in Katlar)
            foreach (var a in kat.Acikliklar)
            {
                if (a.IptalEdildi) iptal++;
                else
                {
                    aktif++;
                    if (a.Tip == BpcAciklikTipi.Pencere) pencere++;
                    else kapi++;
                    if (a.ManuelEklendi) manuel++;
                    else if (a.Kaynak == "gap") gap++;
                    else if (a.Kaynak == "arc") arc++;
                }
            }
            return $"Açıklık: {aktif} aktif ({kapi} kapı + {pencere} pencere; {gap} gap + {arc} arc + {manuel} manuel) / {iptal} iptal";
        }
    }

    // Oda polygon düzenleme
    [ObservableProperty] private IReadOnlyList<Point3D>? _editablePolygon;
    private readonly Stack<KatUndoSnapshot> _undoStack = new();
    private readonly Stack<KatUndoSnapshot> _redoStack = new();

    [ObservableProperty] private bool _projeKilitli;

    public string ProjeKilitButonYazisi => ProjeKilitli ? "🔓 Projeyi Çöz" : "🔒 Projeyi Kilitle";
    public string ProjeKilitDurumu      => ProjeKilitli ? "KİLİTLİ" : "";
    public bool ProjeDuzenlenebilir     => !ProjeKilitli;

    partial void OnProjeKilitliChanged(bool value)
    {
        OnPropertyChanged(nameof(ProjeKilitButonYazisi));
        OnPropertyChanged(nameof(ProjeKilitDurumu));
        OnPropertyChanged(nameof(ProjeDuzenlenebilir));
        OnPropertyChanged(nameof(AsamaAraclariEtkin));
        if (value)
        {
            EditablePolygon = null;
        }
    }

    public bool KilitliyseDur(string islem)
    {
        if (!ProjeKilitli) return false;
        DurumMesaji = $"Proje kilitli — {islem} için önce kilidi açın.";
        return true;
    }

    [RelayCommand]
    private void ProjeKilitDegistir() => ProjeKilitli = !ProjeKilitli;

    // ── Kaydedilmemiş değişiklik durumu (IsDirty) — maincodex §4.2 ──────────
    // Projeyi değiştiren yolların büyük kısmı ElemanSnapshotAl'dan geçer; Kirlet
    // orada merkezî çağrılır. Snapshot almayan yollar (mahal listesi hücre/satır
    // düzenlemeleri vb.) kendi noktalarından Kirlet çağırır.

    private bool _isDirty;
    /// Kaydedilmemiş değişiklik var mı? Pencere başlığında `*` gösterilir; temizken
    /// pencere kapatma / Yeni Proje / Proje Aç sormadan geçer.
    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (_isDirty == value) return;
            _isDirty = value;
            OnPropertyChanged(nameof(IsDirty));
            OnPropertyChanged(nameof(PencereBasligi));
        }
    }

    public string PencereBasligi => (IsDirty ? "* " : "") + ModulAdi;

    /// Projeyi değiştiren her yolun çağırdığı işaret.
    public void Kirlet() => IsDirty = true;

    /// Kaydet / Yeni Proje / Proje Aç sonrası temiz işaretle.
    public void TemizIsaretle() => IsDirty = false;

    /// Eleman taşındı/değişti ama kablo rotaları yenilenmedi — durum çubuğunda
    /// sarı "Rotalar güncel değil" bandı gösterilir. Rota hesabı bandı temizler.
    [ObservableProperty] private bool _rotalarBayat;

    [ObservableProperty] private bool _uyariGosteriliyor;
    [ObservableProperty] private string _uyariMesaji = "";

    [RelayCommand]
    private void KapatUyari()
    {
        UyariGosteriliyor = false;
        UyariMesaji = "";
    }

    private void UyariGoster(string mesaj)
    {
        UyariMesaji = mesaj;
        UyariGosteriliyor = true;
    }

    public bool KabloTavasiCizimde => KabloTavasiCizimAktif;
    public bool KabloTavasiCizimDegil => !KabloTavasiCizimAktif;
    public string KabloTavasiDuzCizimYazisi => KabloTavasiDuzCizimAktif ? "F8 Düz: Açık" : "F8 Düz: Kapalı";
    public string KabloTavasiTaliHatYazisi => KabloTavasiTaliHatModuAktif ? "Tali Hat Kapat" : "Tali Hat Ekle";

    // Konum ofseti paneli
    [ObservableProperty] private bool _ofsetPaneliAcik;
    [ObservableProperty] private bool _katDxfPaneliAcik;
    [ObservableProperty] private bool _katlarPaneliAcik;
    [ObservableProperty] private bool _odalarPaneliAcik;
    [ObservableProperty] private bool _aktarimPaneliAcik;
    [ObservableProperty] private bool _yerlesimPaneliAcik;
    [ObservableProperty] private bool _elemanlarPaneliAcik;
    [ObservableProperty] private double _katOfsetX;
    [ObservableProperty] private double _katOfsetY;
    private bool _katOfsetSyncing;

    // ─── Kütüphane ───────────────────────────────────────────────────────
    [ObservableProperty] private BpcArmatürTanim? _seciliKutuphaneArmatür;
    [ObservableProperty] private BpcArmatürTanim? _seciliKutuphaneAnahtar;
    [ObservableProperty] private BpcArmatürTanim? _seciliKutuphanePano;
    [ObservableProperty] private BpcArmatürTanim? _seciliKutuphanePriz;
    [ObservableProperty] private BpcArmatürTanim? _seciliKutuphaneKlima;
    [ObservableProperty] private BpcArmatürTanim? _aktifKutuphaneEleman;
    [ObservableProperty] private string _kutuphaneMesaji = "";

    public ObservableCollection<BpcArmatürTanim> Armatürler { get; } = new();
    public ObservableCollection<BpcArmatürTanim> Anahtarlar { get; } = new();
    public ObservableCollection<BpcArmatürTanim> Panolar    { get; } = new();
    public ObservableCollection<BpcArmatürTanim> Prizler    { get; } = new();
    public ObservableCollection<BpcArmatürTanim> Klimalar   { get; } = new();
    public ObservableCollection<BpcArmatürTanim> Malzemeler { get; } = new();
    public ObservableCollection<BpcArmatürTanim> AktifPaftaMalzemeleri { get; } = new();

    /// Zayıf akım sabit malzemeleri — sistem başlıkları altında gruplu katalog (salt-okunur).
    public DataGridCollectionView ZayifAkimMalzemeler { get; } = OlusturZayifAkimKatalogu();
    [ObservableProperty] private ZayifAkimMalzemeSatiri? _seciliZayifMalzeme;

    private static DataGridCollectionView OlusturZayifAkimKatalogu()
    {
        var satirlar = new List<ZayifAkimMalzemeSatiri>();
        foreach (var sis in BpcSistemKatalogu.Disipline(BpcDisiplin.ZayifAkim))
        {
            var tipler = sis.CihazTipleri
                .Concat(sis.AraToplayiciTipleri)
                .Append(sis.MerkezTipi)
                .Concat(sis.EkMerkezTipleri)
                .Distinct();
            foreach (var tip in tipler)
            {
                var tanim = BasitProjeCizKutuphaneServisi.ZayifAkimTanim(tip);
                if (tanim != null)
                    satirlar.Add(new ZayifAkimMalzemeSatiri { Grup = sis.Ad, Tanim = tanim });
            }
        }
        var view = new DataGridCollectionView(satirlar);
        view.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(ZayifAkimMalzemeSatiri.Grup)));
        return view;
    }

    // ─── Proje ───────────────────────────────────────────────────────────
    public ObservableCollection<BasitProjeKat> Katlar => Proje.Katlar;
    public IEnumerable<BasitProjeKat> KatlarTersSira => Proje.Katlar;

    private void RefreshKutuphane()
    {
        // Mevcut seçimlerin BlokAdi'sini hatırla — yenileme sonrası referans
        // değişmiş olabilir (Clear+Add), aynı BlokAdi'ye sahip yeni nesneye bağla.
        var eskiSeciliArmaturBlok    = SeciliArmatür?.BlokAdi;
        var eskiKutuphaneSecimiBlok  = SeciliKutuphaneArmatür?.BlokAdi;

        Armatürler.Clear(); foreach (var a in _kutuphane.Armatürler) Armatürler.Add(a);
        Anahtarlar.Clear(); foreach (var a in _kutuphane.Anahtarlar) Anahtarlar.Add(a);
        Panolar.Clear();    foreach (var a in _kutuphane.Panolar)    Panolar.Add(a);
        Prizler.Clear();    foreach (var a in _kutuphane.Prizler)    Prizler.Add(a);
        Klimalar.Clear();   foreach (var a in _kutuphane.Klimalar)   Klimalar.Add(a);
        Malzemeler.Clear(); foreach (var a in _kutuphane.Malzemeler) Malzemeler.Add(a);
        AktifPaftaMalzemeleriniYenile();

        // Seçimi yeni listelerdeki aynı BlokAdi'ne sahip nesneye bağla.
        // Sembol DXF importu SembolCizgiler'i değiştirir; önizlemenin yeniden
        // çizilmesi için SeciliKutuphaneArmatür'e null → yeni atama gerekli
        // (eski referans aynıysa PropertyChanged tetiklenmez → SembolGuncelle çağrılmaz).
        if (eskiSeciliArmaturBlok != null)
            SeciliArmatür = TumKutuphaneSembolleri().FirstOrDefault(a => a.BlokAdi == eskiSeciliArmaturBlok)
                            ?? Armatürler.FirstOrDefault();
        else
            SeciliArmatür ??= Armatürler.FirstOrDefault();

        if (eskiKutuphaneSecimiBlok != null)
        {
            var yeni = TumKutuphaneSembolleri().FirstOrDefault(a => a.BlokAdi == eskiKutuphaneSecimiBlok);
            // Force PropertyChanged tetikle (null → yeni) — UI önizleme yenilensin
            SeciliKutuphaneArmatür = null;
            SeciliKutuphaneArmatür = yeni;
        }
    }

    private IEnumerable<BpcArmatürTanim> TumKutuphaneSembolleri()
    {
        foreach (var a in Armatürler) yield return a;
        foreach (var a in Anahtarlar) yield return a;
        foreach (var a in Panolar)    yield return a;
        foreach (var a in Prizler)    yield return a;
        foreach (var a in Klimalar)   yield return a;
    }

    private void AktifPaftaMalzemeleriniYenile()
    {
        AktifPaftaMalzemeleri.Clear();
        var kategori = AktifPaftaKutuphaneKategorisi(AktifPafta);
        if (kategori is null) return;

        foreach (var malzeme in Malzemeler
                     .Where(m => m.KutuphaneKategorisi == kategori.Value)
                     .OrderBy(m => KutuphaneSira(m))
                     .ThenBy(m => m.Ad))
            AktifPaftaMalzemeleri.Add(malzeme);
    }

    private static int KutuphaneSira(BpcArmatürTanim malzeme)
    {
        var sira = malzeme.KutuphaneKategorisi switch
        {
            BpcKutuphaneKategorisi.Aydinlatma => AydinlatmaSira,
            BpcKutuphaneKategorisi.Priz => PrizSira,
            BpcKutuphaneKategorisi.Anahtar => AnahtarSira,
            BpcKutuphaneKategorisi.Pano => PanoSira,
            BpcKutuphaneKategorisi.Mekanik => KlimaSira,
            BpcKutuphaneKategorisi.Data => DataSira,
            BpcKutuphaneKategorisi.Tv => TvSira,
            BpcKutuphaneKategorisi.Telefon => TelefonSira,
            BpcKutuphaneKategorisi.Cctv => CctvSira,
            BpcKutuphaneKategorisi.AcilAnons => AcilAnonsSira,
            BpcKutuphaneKategorisi.Diafon => DiafonSira,
            BpcKutuphaneKategorisi.YanginAlgilama => YanginSira,
            _ => null
        };
        return sira != null && sira.TryGetValue(malzeme.BlokAdi, out var index) ? index : 1000;
    }

    private static readonly Dictionary<string, int> AydinlatmaSira = Sira(
        "E4P_LED_PANEL_60x60", "E4P_LED_PANEL_30x30", "E4P_LED_PANEL_60x30", "E4P_LED_PANEL_120x30",
        "E4P_YUKSEK_TAVAN_DIK", "E4P_YUKSEK_TAVAN_YUV", "E4P_PROJEKTOR", "E4P_AVIZE",
        "E4P_U_LED", "E4P_LINEAR", "E4P_DOWNLIGHT", "E4P_APLIK", "E4P_SPOT", "E4P_BANT_ARM",
        "E4P_KANOPI", "E4P_ACIL_ARM", "E4P_ACIL_YON", "E4P_EXPROOF", "E4P_OZEL_ARM");
    private static readonly Dictionary<string, int> PrizSira = Sira(
        "E4P_PRIZ", "E4P_KAPAKLI_PRIZ", "E4P_MUSTAKIL_PRIZ", "E4P_SIVAUSTU_PRIZ",
        "E4P_USB_MASA", "E4P_UPS_PRIZ", "E4P_IKILI_PRIZ", "E4P_UCLU_PRIZ",
        "E4P_KOMBIN_PRIZ", "E4P_TRIFAZE_PRIZ");
    private static readonly Dictionary<string, int> AnahtarSira = Sira(
        "E4P_ANAHTAR", "E4P_KOMUTAT", "E4P_VAVIEN", "E4P_3LU_KOMUTAT", "E4P_DARBE", "E4P_SENSOR");
    private static readonly Dictionary<string, int> PanoSira = Sira(
        "E4P_ANA_PANO", "E4P_TALI_PANO", "E4P_ALT_PANO", "E4P_KAT_PANOSU", "E4P_OZEL_PANO", "E4P_BUAT", "E4P_YER_BUATI");
    private static readonly Dictionary<string, int> KlimaSira = Sira("E4P_KLIMA_IC", "E4P_KLIMA_DIS");
    private static readonly Dictionary<string, int> DataSira = Sira("E4P_DATA_PRIZ", "E4P_DATA_PRIZ_2", "E4P_DATA_RACK", "E4P_DATA_MERKEZ");
    private static readonly Dictionary<string, int> TvSira = Sira("E4P_TV_PRIZ", "E4P_FIBER_PRIZ", "E4P_TV_KDP", "E4P_TV_MERKEZ");
    private static readonly Dictionary<string, int> TelefonSira = Sira("E4P_TEL_PRIZ", "E4P_TEL_KDP", "E4P_TEL_DAG");
    private static readonly Dictionary<string, int> CctvSira = Sira("E4P_KAMERA_HARICI", "E4P_KAMERA", "E4P_CCTV_KDK", "E4P_NVR");
    private static readonly Dictionary<string, int> AcilAnonsSira = Sira("E4P_HOPARLOR", "E4P_DUVAR_HOPARLOR", "E4P_ANONS_KDK", "E4P_ANONS_AMFI");
    private static readonly Dictionary<string, int> DiafonSira = Sira("E4P_KAPI_ZILI", "E4P_DIAFON", "E4P_DIAFON_SAN");
    private static readonly Dictionary<string, int> YanginSira = Sira(
        "E4P_ISI_DED", "E4P_KOMBINE_DED", "E4P_DUMAN_DED", "E4P_ASMA_TAV_ARM", "E4P_YANGIN_BTN",
        "E4P_SIREN", "E4P_FLASOR_SIREN", "E4P_YANGIN_SAN");

    private static Dictionary<string, int> Sira(params string[] bloklar)
        => bloklar.Select((blok, index) => (blok, index))
            .ToDictionary(x => x.blok, x => x.index, StringComparer.OrdinalIgnoreCase);

    private static BpcKutuphaneKategorisi? AktifPaftaKutuphaneKategorisi(BpcPaftaTipi pafta) => pafta switch
    {
        BpcPaftaTipi.Aydinlatma => BpcKutuphaneKategorisi.Aydinlatma,
        BpcPaftaTipi.Priz => BpcKutuphaneKategorisi.Priz,
        BpcPaftaTipi.Mekanik => BpcKutuphaneKategorisi.Mekanik,
        BpcPaftaTipi.Data => BpcKutuphaneKategorisi.Data,
        BpcPaftaTipi.Tv => BpcKutuphaneKategorisi.Tv,
        BpcPaftaTipi.Telefon => BpcKutuphaneKategorisi.Telefon,
        BpcPaftaTipi.Cctv => BpcKutuphaneKategorisi.Cctv,
        BpcPaftaTipi.Anons => BpcKutuphaneKategorisi.AcilAnons,
        BpcPaftaTipi.Diafon => BpcKutuphaneKategorisi.Diafon,
        BpcPaftaTipi.Yangin => BpcKutuphaneKategorisi.YanginAlgilama,
        _ => null
    };

    /// <summary>DXF şablonu dışa aktarır (tüm sembolleri block olarak içerir).</summary>
    public void SablonDxfDisariAktar(string path)
    {
        // VM listesindeki değişiklikleri kütüphaneye yansıt, sonra dışa aktar
        _kutuphane.Armatürler.Clear(); _kutuphane.Armatürler.AddRange(Armatürler);
        _kutuphane.Anahtarlar.Clear(); _kutuphane.Anahtarlar.AddRange(Anahtarlar);
        _kutuphane.Panolar.Clear();    _kutuphane.Panolar.AddRange(Panolar);
        _kutuphane.Prizler.Clear();    _kutuphane.Prizler.AddRange(Prizler);
        _kutuphane.Klimalar.Clear();   _kutuphane.Klimalar.AddRange(Klimalar);
        _kutuphane.SablonDxfDisariAktar(path);
        KutuphaneMesaji = $"Şablon DXF kaydedildi: {path}";
    }

    /// <summary>
    /// DXF'ten sembolleri yükleyip eşleşen kütüphane girdilerini günceller.
    /// Yedek otomatik alınır. Sonuçta UI listeleri yenilenir, mesaj döner.
    /// </summary>
    public string SembolleriDxftenYukle(string path)
    {
        var sonuc = _kutuphane.SembolleriDxftenYukle(path);
        RefreshKutuphane();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"✓ {sonuc.Guncellendi} sembol güncellendi  •  DXF'te {sonuc.OkunanBloklar.Count} block okundu");

        if (sonuc.BeklenenAmaBulunamayan.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"⚠ DXF'te bulunamayan {sonuc.BeklenenAmaBulunamayan.Count} block adı (şablonda silinmiş veya farklı adla kaydedilmiş):");
            sb.AppendLine("   " + string.Join(", ", sonuc.BeklenenAmaBulunamayan));
        }
        if (sonuc.Eslesemedi.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"ℹ DXF'te bulunan ama kütüphanenin tanımadığı {sonuc.Eslesemedi.Count} block:");
            sb.AppendLine("   " + string.Join(", ", sonuc.Eslesemedi));
        }
        if (sonuc.BosBloklar.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"⚠ Boş çıkan {sonuc.BosBloklar.Count} block (desteklenmeyen entity — HATCH/SOLID/TEXT atlanır):");
            sb.AppendLine("   " + string.Join(", ", sonuc.BosBloklar));
        }
        if (sonuc.Hatalar.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("✗ Hatalar:");
            foreach (var h in sonuc.Hatalar) sb.AppendLine("   " + h);
        }

        KutuphaneMesaji = sb.ToString().TrimEnd();
        return KutuphaneMesaji;
    }

    public ObservableCollection<BasitProjeOda>? Odalar => SeciliKat?.Odalar;
    public ObservableCollection<BasitProjeEleman>? Elemanlar => SeciliKat?.Elemanlar;



    public BasitProjeCizViewModel()
    {
        RefreshKutuphane();
        SeciliArmatür = Armatürler.FirstOrDefault();
        SeciliKutuphaneArmatür = SeciliArmatür;
        SeciliKutuphaneAnahtar = Anahtarlar.FirstOrDefault();
        SeciliKutuphanePano    = Panolar.FirstOrDefault();
        SeciliKutuphanePriz    = Prizler.FirstOrDefault();
        SeciliKutuphaneKlima   = Klimalar.FirstOrDefault();
        DoldurAktifDisiplinSistemleri();
        DoldurZayifElemanSecenekleri();

        BaglaKatlarAbonelikleri();
    }

    private void BaglaKatlarAbonelikleri()
    {
        foreach (var kat in Katlar)
        {
            kat.PropertyChanged -= OnKatGorunumPropertyChanged;
            kat.PropertyChanged += OnKatGorunumPropertyChanged;
        }
        Katlar.CollectionChanged -= OnKatlarCollectionChanged;
        Katlar.CollectionChanged += OnKatlarCollectionChanged;
    }

    private void CozKatlarAbonelikleri()
    {
        foreach (var kat in Katlar)
            kat.PropertyChanged -= OnKatGorunumPropertyChanged;
        Katlar.CollectionChanged -= OnKatlarCollectionChanged;
    }

    private void OnKatlarCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (BasitProjeKat kat in e.NewItems)
                kat.PropertyChanged += OnKatGorunumPropertyChanged;
        }
        if (e.OldItems != null)
        {
            foreach (BasitProjeKat kat in e.OldItems)
                kat.PropertyChanged -= OnKatGorunumPropertyChanged;
        }
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
        {
            foreach (var kat in Katlar)
            {
                kat.PropertyChanged -= OnKatGorunumPropertyChanged;
                kat.PropertyChanged += OnKatGorunumPropertyChanged;
            }
        }
    }

    partial void OnSeciliKatChanged(BasitProjeKat? value)
    {
        if (AsamaliPaftaAktif) AsamaAraclariniSifirla();
        OnPropertyChanged(nameof(AsamaAraclariEtkin));
        SeciliAciklikTemizle();
        OnPropertyChanged(nameof(Odalar));
        OnPropertyChanged(nameof(Elemanlar));
        OnPropertyChanged(nameof(ElemanFiltreKatSecenekleri));
        GuncelleFiltrelenmisElemanlar();
        OnPropertyChanged(nameof(SeciliKatYuksekligi));
        GuncelleTumKatPanolari();
        GuncellePanoIcerik();
        SyncKatOfset(value);
        Guncelle3DGorunum();
    }

    private void OnKatGorunumPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BasitProjeKat.DxfGoster) or nameof(BasitProjeKat.BloklariGoster))
            Guncelle3DGorunum();
    }

    partial void OnSeciliOdaChanged(BasitProjeOda? value)
    {
        // Çoklu seçim güncellemesi değilse, SeciliOdalar'ı tek seçime indir
        if (!_odaCokluSecimGuncelleniyor)
        {
            SeciliOdalar.Clear();
            if (value != null) SeciliOdalar.Add(value);
            GuncelleOdaVurgu();
        }

        if (value == null)
        {
            EditablePolygon = null;
            return;
        }
        var odaKat = Proje.Katlar.FirstOrDefault(k => k.Odalar.Contains(value));
        double ox = odaKat?.OriginOffset.X ?? 0, oy = odaKat?.OriginOffset.Y ?? 0;
        EditablePolygon = value.Poligon.Select(p => new Point3D(p.X + ox, p.Y + oy, 0)).ToList();
    }

    partial void OnSeciliElemanChanged(BasitProjeEleman? value)
    {
        OnPropertyChanged(nameof(VurgulananElemanPos));
        OnPropertyChanged(nameof(VurgulananElemanPosList));
        if (value != null) ElemanlarPaneliAcik = true;
        SeciliElemanAyarlariniBildir();
    }

    private void SeciliElemanAyarlariniBildir()
    {
        OnPropertyChanged(nameof(SeciliElemanAyarVar));
        OnPropertyChanged(nameof(SeciliElemanAd));
        OnPropertyChanged(nameof(SeciliElemanOzellikler));
        OnPropertyChanged(nameof(SeciliElemanMontajTipi));
        OnPropertyChanged(nameof(SeciliElemanYerlesimTipi));
        OnPropertyChanged(nameof(SeciliElemanGucW));
        OnPropertyChanged(nameof(SeciliElemanGridAraligiM));
        OnPropertyChanged(nameof(SeciliElemanGridAraligiYM));
        OnPropertyChanged(nameof(SeciliElemanAlanBasinaAdetM2));
        OnPropertyChanged(nameof(SeciliElemanDuvarAraligiM));
        OnPropertyChanged(nameof(SeciliElemanAdet));
        OnPropertyChanged(nameof(SeciliElemanMonofazeAdet));
        OnPropertyChanged(nameof(SeciliElemanTrifazeAdet));
        OnPropertyChanged(nameof(SeciliElemanMegapiksel));
        OnPropertyChanged(nameof(GosterSeciliElemanGuc));
        OnPropertyChanged(nameof(GosterSeciliElemanGrid));
        OnPropertyChanged(nameof(GosterSeciliElemanDuvar));
        OnPropertyChanged(nameof(GosterSeciliElemanAdet));
        OnPropertyChanged(nameof(GosterSeciliElemanKombinePriz));
        OnPropertyChanged(nameof(GosterSeciliElemanMegapiksel));
        OnPropertyChanged(nameof(GosterSeciliElemanOzelArmatur));
        OnPropertyChanged(nameof(GosterSeciliElemanBuatTuru));
        OnPropertyChanged(nameof(GosterSeciliElemanYerlesimVeMontaj));
        OnPropertyChanged(nameof(GosterSeciliElemanPanoAdi));
        OnPropertyChanged(nameof(GosterSeciliElemanSaltOkunurAd));
        OnPropertyChanged(nameof(SeciliElemanBuatTuru));
        OnPropertyChanged(nameof(GosterSeciliElemanPanoOdalari));
        OnPropertyChanged(nameof(PanoKatArasiSecim));
        OnPropertyChanged(nameof(GosterSeciliElemanTaliPanoBaglanti));
        OnPropertyChanged(nameof(TaliPanoOdaAdi));
        OnPropertyChanged(nameof(TaliPanoOtomatikCozumMetni));
        PanoOdaSecenekleriDoldur();
        TaliPanoSecenekleriDoldur();
        GuncelleOdaVurgu(); // pano seçilince/bırakılınca bağlı oda vurgusunu güncelle
    }

    private bool ElemanDegisikligineBasla<T>(
        BasitProjeEleman eleman, T eskiDeger, T yeniDeger, string islem = "eleman özelliklerini değiştirmek")
    {
        if (EqualityComparer<T>.Default.Equals(eskiDeger, yeniDeger)) return false;
        if (KilitliyseDur(islem))
        {
            SeciliElemanAyarlariniBildir();
            return false;
        }

        var kat = Proje.Katlar.FirstOrDefault(k => k.Elemanlar.Contains(eleman));
        if (kat == null) return false;
        ElemanSnapshotAl(kat);
        if (AsamaliPaftaAktif &&
            eleman.OtomatikYerlesimKaynak == BasitProjeCizOtomatikYerlestirServisi.MahalListesiKaynak)
        {
            eleman.OtomatikYerlesimKaynak = "";
            eleman.OtomatikYerlesimGrupIds?.Clear();
        }
        return true;
    }

    // ── Kat panosu → bağlı oda seçimi (sağ panel) ─────────────────────────
    public bool GosterSeciliElemanPanoOdalari =>
        SeciliEleman?.Tip == BpcElemanTipi.KatPanosu;

    // ── Tali pano → kat panosu bağlantısı (sağ panel) ──────────────────────
    public bool GosterSeciliElemanTaliPanoBaglanti =>
        SeciliEleman is { Tip: BpcElemanTipi.KatPanosu } el &&
        (BasitProjeCizKabloServisi.PanoTaliMi(el) || BasitProjeCizKabloServisi.PanoAltMi(el));

    public string TaliPanoOdaAdi
    {
        get
        {
            if (SeciliEleman is not { Tip: BpcElemanTipi.KatPanosu } tali) return "";
            if (string.IsNullOrEmpty(tali.OdaId)) return "Oda belirlenmemiş";
            var oda = SeciliKat?.Odalar.FirstOrDefault(o => o.Id == tali.OdaId);
            return string.IsNullOrWhiteSpace(oda?.Ad) ? "Oda" : oda.Ad;
        }
    }

    public string TaliPanoOtomatikCozumMetni
    {
        get
        {
            if (SeciliEleman is not { Tip: BpcElemanTipi.KatPanosu } tali) return "";
            if (!string.IsNullOrEmpty(tali.TaliPanoHedefPanoId)) return "";
            var otomatik = SeciliKat == null ? null : UstPanoSecenekleri(tali, SeciliKat.Elemanlar).FirstOrDefault();
            return otomatik != null
                ? $"Otomatik üst pano: {otomatik.Ad}"
                : "Bu katta uygun üst pano bulunmuyor.";
        }
    }

    public ObservableCollection<BasitProjeEleman> TaliPanoKatPanoSecenekleri { get; } = new();

    private void TaliPanoSecenekleriDoldur()
    {
        _ustPanoSecenekleriGuncelleniyor = true;
        try
        {
            TaliPanoKatPanoSecenekleri.Clear();
            if (SeciliKat != null && GosterSeciliElemanTaliPanoBaglanti)
                foreach (var p in UstPanoSecenekleri(SeciliEleman!, SeciliKat.Elemanlar))
                    TaliPanoKatPanoSecenekleri.Add(p);
            OnPropertyChanged(nameof(SeciliTaliPanoHedefPano));
        }
        finally { _ustPanoSecenekleriGuncelleniyor = false; }
    }

    private bool _ustPanoSecenekleriGuncelleniyor;

    public BasitProjeEleman? SeciliTaliPanoHedefPano
    {
        get
        {
            if (SeciliEleman is not { Tip: BpcElemanTipi.KatPanosu } tali) return null;
            if (string.IsNullOrEmpty(tali.TaliPanoHedefPanoId)) return null;
            return TaliPanoKatPanoSecenekleri.FirstOrDefault(p => p.Id == tali.TaliPanoHedefPanoId);
        }
        set
        {
            if (_ustPanoSecenekleriGuncelleniyor || SeciliEleman is not { Tip: BpcElemanTipi.KatPanosu } tali) return;
            var yeniId = value?.Id;
            if (value != null && !TaliPanoKatPanoSecenekleri.Contains(value)) return;
            if (!ElemanDegisikligineBasla(tali, tali.TaliPanoHedefPanoId, yeniId, "tali pano hedefini değiştirmek")) return;
            tali.TaliPanoHedefPanoId = yeniId;
            // Kullanıcı eski bir kat içi beslemenin üst panosunu açıkça değiştiriyor.
            // 5. adımda rota yeniden kurulabilir; katlar arası tava hatları kendi işleminde düzenlenir.
            if (SeciliKat is { } kat)
                foreach (var hat in kat.PanoBeslemeHatlari.Where(h => h.TaliPanoId == tali.Id &&
                    h.UygulamaTipi == BpcPanoBeslemeUygulamaTipi.Kablo &&
                    h.AnaKatNo == kat.KatNo && h.TaliKatNo == kat.KatNo))
                    hat.OtomatikKatIciBesleme = true;
            if (Proje.Katlar.Any(k => k.PanoBeslemeHatlari.Any(h => h.TaliPanoId == tali.Id)))
                RotalarBayat = true;
            SeciliElemanDegisti();
        }
    }

    /// Kat arası oda seçimi (varsayılan kapalı). Açıkken diğer katların odaları da listelenir.
    public bool PanoKatArasiSecim
    {
        get => SeciliEleman?.KatArasiSecim ?? false;
        set
        {
            if (SeciliEleman is not { Tip: BpcElemanTipi.KatPanosu } el) return;
            if (!ElemanDegisikligineBasla(el, el.KatArasiSecim, value, "pano oda kapsamını değiştirmek")) return;
            el.KatArasiSecim = value;
            SeciliElemanDegisti();
        }
    }

    /// Seçili kat panosunun beslediği oda seçenekleri (checkbox listesi).
    public ObservableCollection<PanoOdaSecenek> SeciliPanoOdaSecenekleri { get; } = new();
    private bool _panoOdaDolduruluyor;

    private void PanoOdaSecenekleriDoldur()
    {
        _panoOdaDolduruluyor = true;
        SeciliPanoOdaSecenekleri.Clear();
        var gecerliOdaIdler = new HashSet<string>();
        var pano = SeciliEleman;
        if (pano is { Tip: BpcElemanTipi.KatPanosu })
        {
            var panoKat = Proje.Katlar.FirstOrDefault(k => k.Elemanlar.Contains(pano));
            var katlar = pano.KatArasiSecim && !AydinlatmaAkisiAktif
                ? Proje.Katlar.ToList()
                : (panoKat != null ? new List<BasitProjeKat> { panoKat } : new());

            // Diğer panoların sahiplendiği odalar: odaId → pano adı
            var alinanOdalar = new Dictionary<string, string>();
            foreach (var k in Katlar)
            {
                foreach (var dp in k.Elemanlar.Where(e => e.Tip == BpcElemanTipi.KatPanosu && e.Id != pano.Id))
                {
                    var dpAd = string.IsNullOrWhiteSpace(dp.Ad) ? "Kat Panosu" : dp.Ad;
                    foreach (var oid in dp.BagliOdaIds)
                        alinanOdalar.TryAdd(oid, dpAd);
                }
            }

            foreach (var kat in katlar)
            {
                bool digerKat = !ReferenceEquals(kat, panoKat);
                foreach (var oda in kat.Odalar)
                {
                    var ad = string.IsNullOrWhiteSpace(oda.Ad) ? "Oda" : oda.Ad;
                    if (digerKat) ad = $"[{kat.Ad}] {ad}";
                    alinanOdalar.TryGetValue(oda.Id, out var baskaPanoAd);
                    var secenek = new PanoOdaSecenek
                    {
                        OdaId = oda.Id,
                        Ad = ad,
                        BaskaPanoAd = baskaPanoAd,
                        Secili = pano.BagliOdaIds.Contains(oda.Id)
                    };
                    secenek.SeciliDegisti = v => PanoOdaSecimDegisti(pano, oda.Id, v);
                    secenek.OdaTiklandi = () => PanoIcinOdaSec(oda.Id);
                    SeciliPanoOdaSecenekleri.Add(secenek);
                    gecerliOdaIdler.Add(oda.Id);
                }
            }
        }
        _panoOdaDolduruluyor = false;
        if (SeciliEleman?.Tip != BpcElemanTipi.KatPanosu || !gecerliOdaIdler.Contains(SeciliOda?.Id ?? string.Empty))
            PanoOdaVurgula(null);
    }

    private void PanoOdaSecimDegisti(BasitProjeEleman pano, string odaId, bool secili)
    {
        if (_panoOdaDolduruluyor) return;
        if (secili != pano.BagliOdaIds.Contains(odaId))
            MahalePanoAta(odaId, secili ? pano.Id : null);
    }

    private void PanoIcinOdaSec(string odaId)
    {
        var odaKat = Proje.Katlar.FirstOrDefault(k => k.Odalar.Any(o => o.Id == odaId));
        var oda = odaKat?.Odalar.FirstOrDefault(o => o.Id == odaId);
        if (odaKat == null || oda == null)
        {
            PanoOdaVurgula(null);
            return;
        }

        if (SeciliKat != odaKat)
            SeciliKat = odaKat;

        SeciliOda = oda;
        PanoOdaVurgula(oda.Id);
    }

    private void SeciliElemanDegisti()
    {
        SeciliElemanAyarlariniBildir();
        GuncelleFiltrelenmisElemanlar();
        GuncellePanoIcerik();
        Guncelle3DGorunum();
    }

    public bool SeciliElemanAyarVar => SeciliEleman != null;
    public string SeciliElemanAd
    {
        get => SeciliEleman?.Ad ?? "";
        set
        {
            if (SeciliEleman is not { } el) return;
            if (!ElemanDegisikligineBasla(el, el.Ad, value)) return;
            el.Ad = value;
            el.EkranAdi = value;
            SeciliElemanDegisti();
        }
    }
    public string SeciliElemanOzellikler
    {
        get => SeciliEleman?.Ozellikler ?? "";
        set
        {
            if (SeciliEleman is not { } el) return;
            if (!ElemanDegisikligineBasla(el, el.Ozellikler, value)) return;
            el.Ozellikler = value;
            SeciliElemanDegisti();
        }
    }
    public IReadOnlyList<string> MontajTipiSecenekleri { get; } = ["Tavan", "Duvar"];
    public IReadOnlyList<string> YerlesimTipiSecenekleri { get; } = ["Grid", "m²/adet", "Duvar aralıklı"];

    public string SeciliElemanMontajTipi
    {
        get => string.IsNullOrWhiteSpace(SeciliEleman?.MontajTipi) ? "Tavan" : SeciliEleman.MontajTipi;
        set
        {
            if (SeciliEleman is not { } el) return;
            var yeniYerlesim = value == "Duvar" ? "Duvar aralıklı" : "Grid";
            if (!ElemanDegisikligineBasla(el, (el.MontajTipi, el.YerlesimTipi), (value, yeniYerlesim))) return;
            el.MontajTipi = value;
            el.YerlesimTipi = yeniYerlesim;
            SeciliElemanDegisti();
        }
    }

    public string SeciliElemanYerlesimTipi
    {
        get => string.IsNullOrWhiteSpace(SeciliEleman?.YerlesimTipi) ? "Grid" : SeciliEleman.YerlesimTipi;
        set { if (SeciliEleman is { } el && ElemanDegisikligineBasla(el, el.YerlesimTipi, value)) { el.YerlesimTipi = value; SeciliElemanDegisti(); } }
    }

    public double SeciliElemanGucW
    {
        get => SeciliEleman?.CustomGucW ?? 0;
        set { if (SeciliEleman is { } el) { var v = Math.Max(0, value); if (ElemanDegisikligineBasla(el, el.CustomGucW ?? 0, v)) { el.CustomGucW = v; SeciliElemanDegisti(); } } }
    }

    public double SeciliElemanGridAraligiM
    {
        get => SeciliEleman?.GridAraligiM ?? 3.0;
        set { if (SeciliEleman is { } el) { var v = Math.Max(0.1, value); if (ElemanDegisikligineBasla(el, el.GridAraligiM ?? 3.0, v)) { el.GridAraligiM = v; SeciliElemanDegisti(); } } }
    }

    /// Grid Y aralığı. null/boş = X ile aynı (kare grid).
    public double? SeciliElemanGridAraligiYM
    {
        get => SeciliEleman?.GridAraligiYM;
        set { if (SeciliEleman is { } el) { double? v = value is { } n ? Math.Max(0.1, n) : null; if (ElemanDegisikligineBasla(el, el.GridAraligiYM, v)) { el.GridAraligiYM = v; SeciliElemanDegisti(); } } }
    }

    public double SeciliElemanAlanBasinaAdetM2
    {
        get => SeciliEleman?.AlanBasinaAdetM2 ?? 12.0;
        set { if (SeciliEleman is { } el) { var v = Math.Max(0.1, value); if (ElemanDegisikligineBasla(el, el.AlanBasinaAdetM2 ?? 12.0, v)) { el.AlanBasinaAdetM2 = v; SeciliElemanDegisti(); } } }
    }

    public double SeciliElemanDuvarAraligiM
    {
        get => SeciliEleman?.DuvarAraligiM ?? 3.0;
        set { if (SeciliEleman is { } el) { var v = Math.Max(0.1, value); if (ElemanDegisikligineBasla(el, el.DuvarAraligiM ?? 3.0, v)) { el.DuvarAraligiM = v; SeciliElemanDegisti(); } } }
    }

    public int SeciliElemanAdet
    {
        get => SeciliEleman?.CustomAdet ?? 1;
        set { if (SeciliEleman is { } el) { var v = Math.Max(1, value); if (ElemanDegisikligineBasla(el, el.CustomAdet ?? 1, v)) { el.CustomAdet = v; SeciliElemanDegisti(); } } }
    }

    public int SeciliElemanMonofazeAdet
    {
        get => SeciliEleman?.MonofazeAdet ?? 2;
        set { if (SeciliEleman is { } el) { var v = Math.Max(0, value); if (ElemanDegisikligineBasla(el, el.MonofazeAdet ?? 2, v)) { el.MonofazeAdet = v; SeciliElemanDegisti(); } } }
    }

    public int SeciliElemanTrifazeAdet
    {
        get => SeciliEleman?.TrifazeAdet ?? 2;
        set { if (SeciliEleman is { } el) { var v = Math.Max(0, value); if (ElemanDegisikligineBasla(el, el.TrifazeAdet ?? 2, v)) { el.TrifazeAdet = v; SeciliElemanDegisti(); } } }
    }

    public double SeciliElemanMegapiksel
    {
        get => SeciliEleman?.Megapiksel ?? 2.0;
        set { if (SeciliEleman is { } el) { var v = Math.Max(0.1, value); if (ElemanDegisikligineBasla(el, el.Megapiksel ?? 2.0, v)) { el.Megapiksel = v; SeciliElemanDegisti(); } } }
    }

    private static bool ElemanMontajYerlesimGoster(BpcElemanTipi? tip) =>
        tip != BpcElemanTipi.Buat && tip != BpcElemanTipi.KatPanosu;

    public bool GosterSeciliElemanGuc => ElemanMontajYerlesimGoster(SeciliEleman?.Tip) && (SeciliEleman?.CustomGucW is not null || GosterSeciliElemanOzelArmatur);
    public bool GosterSeciliElemanGrid => ElemanMontajYerlesimGoster(SeciliEleman?.Tip) && SeciliElemanMontajTipi == "Tavan";
    public bool GosterSeciliElemanDuvar => ElemanMontajYerlesimGoster(SeciliEleman?.Tip) && SeciliElemanMontajTipi == "Duvar";
    public bool GosterSeciliElemanAdet => ElemanMontajYerlesimGoster(SeciliEleman?.Tip) && SeciliEleman?.OzelAlanTuru == "Adet";
    public bool GosterSeciliElemanKombinePriz => ElemanMontajYerlesimGoster(SeciliEleman?.Tip) && SeciliEleman?.OzelAlanTuru == "KombinePrizKutusu";
    public bool GosterSeciliElemanMegapiksel => ElemanMontajYerlesimGoster(SeciliEleman?.Tip) && SeciliEleman?.OzelAlanTuru == "Megapiksel";
    public bool GosterSeciliElemanOzelArmatur => ElemanMontajYerlesimGoster(SeciliEleman?.Tip) && SeciliEleman?.OzelAlanTuru == "OzelArmatur";
    public bool GosterSeciliElemanYerlesimVeMontaj => ElemanMontajYerlesimGoster(SeciliEleman?.Tip);
    public bool GosterSeciliElemanPanoAdi => SeciliEleman?.Tip == BpcElemanTipi.KatPanosu;
    public bool GosterSeciliElemanSaltOkunurAd => !GosterSeciliElemanOzelArmatur && !GosterSeciliElemanPanoAdi;
    
    public bool GosterSeciliElemanBuatTuru => !AsamaliPaftaAktif && SeciliEleman?.Tip == BpcElemanTipi.Buat;
    public string[] BuatTurleri { get; } = [ "Aydınlatma Buatı", "Priz Buatı" ];

    public string? SeciliElemanBuatTuru
    {
        get
        {
            if (SeciliEleman?.Tip != BpcElemanTipi.Buat) return null;
            return SeciliEleman.PaftaTipi == BpcPaftaTipi.Priz ? "Priz Buatı" : "Aydınlatma Buatı";
        }
        set
        {
            if (!AsamaliPaftaAktif && SeciliEleman?.Tip == BpcElemanTipi.Buat && value != null)
            {
                var yeniPafta = value == "Priz Buatı" ? BpcPaftaTipi.Priz : BpcPaftaTipi.Aydinlatma;
                if (!ElemanDegisikligineBasla(SeciliEleman, SeciliEleman.PaftaTipi, yeniPafta)) return;
                SeciliEleman.PaftaTipi = yeniPafta;
                SeciliElemanDegisti();
            }
        }
    }

    public Point3D? VurgulananElemanPos
    {
        get
        {
            var el = SeciliEleman;
            if (el == null) return null;
            var elKat = Proje.Katlar.FirstOrDefault(k => k.Elemanlar.Contains(el));
            double ox = elKat?.OriginOffset.X ?? 0, oy = elKat?.OriginOffset.Y ?? 0;
            return new Point3D(el.X + ox, el.Y + oy, 0);
        }
    }

    // Çoklu seçim pozisyonları — HighlightedPointPositions'a bağlanır.
    public IReadOnlyList<Point3D>? VurgulananElemanPosList
    {
        get
        {
            var result = new List<Point3D>();
            if (SeciliElemanlar.Count > 0)
            {
                foreach (var el in SeciliElemanlar)
                {
                    var elKat = Proje.Katlar.FirstOrDefault(k => k.Elemanlar.Contains(el));
                    double ox = elKat?.OriginOffset.X ?? 0, oy = elKat?.OriginOffset.Y ?? 0;
                    result.Add(new Point3D(el.X + ox, el.Y + oy, 0));
                }
            }

            if (BostakileriGosterAktif && PrizMahalKablosuAdimi && SeciliKat is { } prizKati)
            {
                var bagli = prizKati.PrizDevreleri.Where(d => d.BuatId != null).SelectMany(d => d.PrizElemanIds).ToHashSet();
                foreach (var priz in prizKati.Elemanlar.Where(PrizTasarimServisi.MahaldeBaglananPriz))
                    if (!bagli.Contains(priz.Id))
                        result.Add(new Point3D(priz.X + prizKati.OriginOffset.X, priz.Y + prizKati.OriginOffset.Y, 0));
            }
            else if (BostakileriGosterAktif && !PrizAkisiAktif && SeciliKat != null)
            {
                var kat = SeciliKat;
                double ox = kat.OriginOffset.X, oy = kat.OriginOffset.Y;

                var atananArmaturIds = kat.Devreler.SelectMany(d => d.ArmaturElemanIds).ToHashSet();
                var atananAnahtarIds = kat.Devreler.Select(d => d.AnahtarElemanId).Where(id => !string.IsNullOrEmpty(id)).ToHashSet();
                var atananVavienIds = kat.VavienGruplari.SelectMany(g => new[] { g.Anahtar1Id, g.Anahtar2Id }).Where(id => !string.IsNullOrEmpty(id)).ToHashSet();
                var atananBuatIds = kat.Devreler.Select(d => d.BuatId).Where(id => !string.IsNullOrEmpty(id))
                    .Concat(kat.BuatPanoBaglantilari.Select(b => b.BuatId).Where(id => !string.IsNullOrEmpty(id)))
                    .Concat(kat.PrizDevreleri.Select(d => d.BuatId).Where(id => !string.IsNullOrEmpty(id)))
                    .ToHashSet();

                foreach (var el in kat.Elemanlar.Where(ElemanAktifPaftadaMi))
                {
                    bool isBos = false;
                    if (BasitProjeCizKabloServisi.ElemanArmaturMi(el.Tip) && !atananArmaturIds.Contains(el.Id))
                        isBos = true;
                    else if (BasitProjeCizKabloServisi.ElemanAnahtarMi(el.Tip) && !atananAnahtarIds.Contains(el.Id) && !atananVavienIds.Contains(el.Id))
                        isBos = true;
                    else if (el.Tip == BpcElemanTipi.Buat && !atananBuatIds.Contains(el.Id))
                        isBos = true;

                    if (isBos)
                    {
                        result.Add(new Point3D(el.X + ox, el.Y + oy, 0));
                    }
                }
            }

            return result.Count > 0 ? result : null;
        }
    }

    partial void OnBostakileriGosterAktifChanged(bool value)
    {
        OnPropertyChanged(nameof(VurgulananElemanPosList));
        GuncelleElemanVurgu();
    }

    partial void OnKabloTavasiCizimAktifChanged(bool value)
    {
        OnPropertyChanged(nameof(KabloTavasiCizimde));
        OnPropertyChanged(nameof(KabloTavasiCizimDegil));
        KabloTavasiCizBitirCommand.NotifyCanExecuteChanged();
    }

    partial void OnKabloTavasiDuzCizimAktifChanged(bool value)
        => OnPropertyChanged(nameof(KabloTavasiDuzCizimYazisi));

    partial void OnKabloTavasiTaliHatModuAktifChanged(bool value)
        => OnPropertyChanged(nameof(KabloTavasiTaliHatYazisi));

    partial void OnTopraklamaAlanSecimAktifChanged(bool value)
        => OnPropertyChanged(nameof(TopraklamaSecimBekliyor));

    partial void OnTopraklamaKazikEkleModuAktifChanged(bool value)
    {
        OnPropertyChanged(nameof(TopraklamaKazikEklemede));
        OnPropertyChanged(nameof(TopraklamaKazikModuYazisi));
    }

    partial void OnKatOfsetXChanged(double value)
    {
        if (_katOfsetSyncing || SeciliKat == null) return;
        SeciliKat.OriginOffset = new Point2D(value, SeciliKat.OriginOffset.Y);
        Guncelle3DGorunum();
    }

    partial void OnKatOfsetYChanged(double value)
    {
        if (_katOfsetSyncing || SeciliKat == null) return;
        SeciliKat.OriginOffset = new Point2D(SeciliKat.OriginOffset.X, value);
        Guncelle3DGorunum();
    }

    partial void OnProjectionChanged(GeometryProjectionMode value) => Guncelle3DGorunum();

    /// 2D modda yalnız seçili katın arka planını/yüzeylerini render et — büyük çok-katlı
    /// projelerde 2D'de katların üst üste binmesini önler. Default kapalı (mevcut davranış).
    /// 3D modda etkisiz; her zaman tüm katlar render edilir.
    [ObservableProperty] private bool _sadece2DSeciliKat;
    partial void OnSadece2DSeciliKatChanged(bool value) => Guncelle3DGorunum();

    private void SyncKatOfset(BasitProjeKat? kat)
    {
        _katOfsetSyncing = true;
        KatOfsetX = kat?.OriginOffset.X ?? 0;
        KatOfsetY = kat?.OriginOffset.Y ?? 0;
        _katOfsetSyncing = false;
    }

    public double SeciliKatYuksekligi
    {
        get => SeciliKat?.KatYuksekligiM ?? 3.0;
        set
        {
            var kat = SeciliKat;
            if (kat == null || value <= 0 || double.IsNaN(value) || double.IsInfinity(value)) return;
            if (System.Math.Abs(kat.KatYuksekligiM - value) < 1e-6) return;
            kat.KatYuksekligiM = value;
            OnPropertyChanged();
            Guncelle3DGorunum();
        }
    }

    // ─── Proje komutları ─────────────────────────────────────────────────

    [RelayCommand]
    public void YeniProje()
    {
        UndoRedoYiginlariniTemizle();
        CozKatlarAbonelikleri();
        Proje = new BasitProjeCizProje { ModulDisiplini = ModulModu };
        BaglaKatlarAbonelikleri();
        ProjeAsamasiniAc();
        OnPropertyChanged(nameof(Katlar));
        OnPropertyChanged(nameof(KatlarTersSira));
        SeciliKat = null;
        SeciliOda = null;
        SeciliEleman = null;
        ProjeDosyaYolu = null;
        TemizIsaretle();
        DurumMesaji = "Yeni proje oluşturuldu. M50 bina geometrisini (.e4bina) yükleyin.";
        Guncelle3DGorunum();
    }

    public void KatEkle(BasitProjeKat kat)
    {
        if (kat.KatNo <= 0)
            kat.KatNo = Katlar.Any() ? Katlar.Max(k => k.KatNo) + 1 : 1;
        Proje.Katlar.Add(kat);
        OnPropertyChanged(nameof(KatlarTersSira));
        SeciliKat = kat;
        SeciliOda = kat.Odalar.FirstOrDefault();
        DurumMesaji = $"{kat.Ad}: {kat.Odalar.Count} oda, {kat.DxfSegmentleri.Count} DXF çizgisi yüklendi.";
    }

    /// <summary>
    /// Tüm katlarda kapı (arc) + pencere (duvar boşluk) tespiti çalıştırır.
    /// Manuel eklenen ve "açıklık değil" işaretli kayıtlar korunur.
    /// Filtre parametreleri Aciklik* observable property'leri üzerinden geçer.
    /// </summary>
    [RelayCommand]
    public async System.Threading.Tasks.Task AcikliklariTespitEt()
    {
        if (Katlar.Count == 0) { DurumMesaji = "Önce kat yükleyin."; return; }
        DurumMesaji = "Açıklık tespiti çalışıyor…";

        var ayar = new Kst.Acoustics.Core.Electric.BasitProjeCiz.Services
            .AciklikTespitServisi.Ayarlar
        {
            MinAciklikGenisM = AciklikMinGenisM,
            SoveAramaUcM = AciklikSoveUcM,
            MinSoveBoyuM = AciklikMinSoveBoyuM,
            MaxKarsiDuvarM = AciklikMaxKarsiDuvarM
        };

        var ozet = await System.Threading.Tasks.Task.Run(() =>
        {
            int toplam = 0;
            var satirlar = new List<string>();
            foreach (var kat in Katlar)
            {
                var segs = kat.DxfSegmentleri.Select(s => (s.A, s.B)).ToList();
                var arcs = kat.DxfArclari
                    .Select(a => (a.Merkez, a.R, a.BasAciDeg, a.BitAciDeg))
                    .ToList();
                kat.Acikliklar = Kst.Acoustics.Core.Electric.BasitProjeCiz.Services
                    .AciklikTespitServisi.Bul(segs, arcs, kat.Acikliklar, ayar);
                Kst.Acoustics.Core.Electric.BasitProjeCiz.Services
                    .AciklikYardimci.DisindaKalanlariSil(kat);
                int aktif = kat.Acikliklar.Count(a => !a.IptalEdildi);
                toplam += aktif;
                satirlar.Add($"{kat.Ad}: {aktif}");
            }
            return (toplam, string.Join(", ", satirlar));
        });

        DurumMesaji = $"Açıklık tespiti tamam: {ozet.toplam} aktif (kat bazında — {ozet.Item2}).";
        OnPropertyChanged(nameof(AciklikOzeti));
        Guncelle3DGorunum();
    }

    // ── Açıklık tespiti filtre parametreleri ─────────────────────────────────
    // Default = sıkı (codex12 2026-06-02 ayarları). Kullanıcı UI'dan ayarlar +
    // "Yeniden Tespit Et" ile çalıştırır.

    [ObservableProperty] private double _aciklikMinGenisM    = 0.60;
    [ObservableProperty] private double _aciklikSoveUcM      = 0.30;
    [ObservableProperty] private double _aciklikMinSoveBoyuM = 0.10;
    [ObservableProperty] private double _aciklikMaxKarsiDuvarM = 1.50;

    /// Sıkı preset: false-positive minimize edilir, bazı gerçek kapılar kaçabilir.
    [RelayCommand]
    public void AciklikPresetSiki()
    {
        AciklikMinGenisM = 0.60;
        AciklikSoveUcM = 0.30;
        AciklikMinSoveBoyuM = 0.10;
        AciklikMaxKarsiDuvarM = 1.50;
        DurumMesaji = "Açıklık filtresi: Sıkı (Yeniden Tespit Et ile uygula).";
    }

    /// Dengeli preset: makul orta yol.
    [RelayCommand]
    public void AciklikPresetDengeli()
    {
        AciklikMinGenisM = 0.50;
        AciklikSoveUcM = 0.50;
        AciklikMinSoveBoyuM = 0.08;
        AciklikMaxKarsiDuvarM = 2.50;
        DurumMesaji = "Açıklık filtresi: Dengeli (Yeniden Tespit Et ile uygula).";
    }

    /// Gevşek preset: gerçek kapıların hepsini yakalar ama false-positive artar.
    [RelayCommand]
    public void AciklikPresetGevsek()
    {
        AciklikMinGenisM = 0.40;
        AciklikSoveUcM = 0.80;
        AciklikMinSoveBoyuM = 0.05;
        AciklikMaxKarsiDuvarM = 4.00;
        DurumMesaji = "Açıklık filtresi: Gevşek (Yeniden Tespit Et ile uygula).";
    }

    /// Manuel açıklık ekleme modu — true ise canvas iki-nokta seçim yapacak.
    [ObservableProperty] private bool _manuelAciklikModu;
    private Point2D? _manuelAciklikIlkNokta;

    [RelayCommand]
    public void ManuelAciklikModuAcKapat() => ManuelAciklikModu = !ManuelAciklikModu;

    partial void OnManuelAciklikModuChanged(bool value)
    {
        _manuelAciklikIlkNokta = null;
        DurumMesaji = value
            ? "Manuel açıklık modu: iki nokta seçin (Esc iptal)."
            : "Manuel açıklık modu kapatıldı.";
    }

    /// Canvas dünya koordinatından manuel açıklık tıklamasını işle. İki nokta toplanınca
    /// SeciliKat.Acikliklar'a ekler ve modu kapatır. İlk tıklamada başlangıcı kaydeder.
    public void ManuelAciklikTiklama(double worldX, double worldY)
    {
        if (!ManuelAciklikModu || SeciliKat is null) return;
        if (KilitliyseDur("açıklık eklemek")) return;
        var p = new Point2D(worldX, worldY);
        if (_manuelAciklikIlkNokta is null)
        {
            _manuelAciklikIlkNokta = p;
            DurumMesaji = "İlk nokta alındı — ikinci noktayı seç (Esc iptal).";
            return;
        }
        var bas = _manuelAciklikIlkNokta.Value;
        var aciklik = new Kst.Acoustics.Core.Electric.BasitProjeCiz.Models.BpcAciklik
        {
            Bas = bas, Bit = p, Tip = BpcAciklikTipi.Kapı, ManuelEklendi = true, Kaynak = "manuel"
        };
        SeciliKat.Acikliklar.Add(aciklik);

        // Kapı iki odaya birden hizmet eder: komşu duvara otomatik yansıt.
        var yansima = AciklikYardimci.KomsuDuvaraYansit(SeciliKat, aciklik);

        // Modu kapatmadan devam et: kullanıcı butona tekrar basana (veya Esc) kadar
        // arka arkaya açıklık ekleyebilsin.
        _manuelAciklikIlkNokta = null;
        DurumMesaji = yansima != null
            ? "Manuel açıklık eklendi (komşu duvara da yansıtıldı) — yeni açıklık için iki nokta seçin (Esc/buton ile bitir)."
            : "Manuel açıklık eklendi — yeni açıklık için iki nokta seçin (Esc/buton ile bitir).";
        OnPropertyChanged(nameof(AciklikOzeti));
        Guncelle3DGorunum();
    }

    public void ManuelAciklikIptal()
    {
        ManuelAciklikModu = false;         // OnManuelAciklikModuChanged → _ilkNokta = null
        DurumMesaji = "Manuel açıklık iptal edildi.";
    }

    /// <summary>
    /// Seçili katın DXF segmentlerinden odaları boşluk tarama (flood-fill) ile
    /// yeniden bulur. Mevcut oda isimlerini centroid eşleşmesiyle korur.
    /// </summary>
    [RelayCommand]
    public async System.Threading.Tasks.Task OdalariYenidenTara()
    {
        if (SeciliKat == null) { DurumMesaji = "Önce bir kat seçin."; return; }
        var kat = SeciliKat;
        if (kat.DxfSegmentleri.Count == 0)
        {
            DurumMesaji = "Bu katta DXF çizgisi yok; tarama yapılamaz.";
            return;
        }

        DurumMesaji = "Boşluk tarama çalışıyor…";
        // Ağır flood-fill hesabını UI thread'inde yapma (donmayı önler).
        int sayi = await System.Threading.Tasks.Task.Run(
            () => BasitProjeCizDxfServisi.OdalariYenidenTara(kat));
        if (sayi == 0)
        {
            DurumMesaji = "Boşluk tarama oda bulamadı. Hücre/kapı ayarları veya DXF duvarlarını kontrol edin.";
            return;
        }

        OnPropertyChanged(nameof(Odalar));
        OnPropertyChanged(nameof(ElemanFiltreOdaSecenekleri));
        SeciliOda = kat.Odalar.FirstOrDefault();
        DurumMesaji = $"Boşluk tarama: {sayi} oda bulundu.";
        Guncelle3DGorunum();
    }

    [RelayCommand]
    public void BenzerKatOlustur()
    {
        if (SeciliKat == null) return;
        int yeniNo = Katlar.Max(k => k.KatNo) + 1;
        var kopya = BasitProjeCizDxfServisi.KatiKopyala(SeciliKat, yeniNo, $"Kat {yeniNo}");
        Proje.Katlar.Add(kopya);
        OnPropertyChanged(nameof(KatlarTersSira));
        SeciliKat = kopya;
        SeciliOda = kopya.Odalar.FirstOrDefault();
        DurumMesaji = $"Benzer kat oluşturuldu: {kopya.Ad}";
        Guncelle3DGorunum();
    }

    [RelayCommand]
    public void KatSil()
    {
        if (SeciliKat == null) return;
        var silinen = SeciliKat;
        int idx = Katlar.IndexOf(silinen);
        Katlar.Remove(silinen);
        KatlariYenidenNumaralandir();
        OnPropertyChanged(nameof(KatlarTersSira));
        SeciliKat = Katlar.Count > 0 ? Katlar[System.Math.Max(0, idx - 1)] : null;
        SeciliOda = SeciliKat?.Odalar.FirstOrDefault();
        DurumMesaji = $"{silinen.Ad} silindi.";
        Guncelle3DGorunum();
    }

    [RelayCommand]
    public void KatYukari()
    {
        if (SeciliKat == null) return;
        int idx = Katlar.IndexOf(SeciliKat);
        if (idx <= 0) return;
        Katlar.Move(idx, idx - 1);
        KatlariYenidenNumaralandir();
        OnPropertyChanged(nameof(KatlarTersSira));
        Guncelle3DGorunum();
    }

    [RelayCommand]
    public void KatAsagi()
    {
        if (SeciliKat == null) return;
        int idx = Katlar.IndexOf(SeciliKat);
        if (idx >= Katlar.Count - 1) return;
        Katlar.Move(idx, idx + 1);
        KatlariYenidenNumaralandir();
        OnPropertyChanged(nameof(KatlarTersSira));
        Guncelle3DGorunum();
    }

    [RelayCommand] public void KatSola()           => KatOfsetX -= 0.5;
    [RelayCommand] public void KatSaga()           => KatOfsetX += 0.5;
    [RelayCommand] public void KatYukar()          => KatOfsetY += 0.5;
    [RelayCommand] public void KatAsag()           => KatOfsetY -= 0.5;
    [RelayCommand] public void ToggleOfsetPaneli() => OfsetPaneliAcik = !OfsetPaneliAcik;
    [RelayCommand] public void KatGorunumDegisti() => Guncelle3DGorunum();

    [RelayCommand]
    public void KatlariHizala()
    {
        foreach (var kat in Katlar)
            kat.OriginOffset = new Point2D(0, 0);
        SyncKatOfset(SeciliKat);
        DurumMesaji = "Tüm katlar sıfır noktasına hizalandı.";
        Guncelle3DGorunum();
    }

    [RelayCommand] public void Goster2D() => Projection = GeometryProjectionMode.Top2D;
    [RelayCommand] public void Goster3D() => Projection = GeometryProjectionMode.Isometric3D;

    private void KatlariYenidenNumaralandir()
    {
        var eskiYeniNo = Katlar.ToDictionary(k => k.Id, k => (EskiNo: k.KatNo, YeniNo: k.KatNo));
        for (int i = 0; i < Katlar.Count; i++)
        {
            Katlar[i].KatNo = i + 1;
            eskiYeniNo[Katlar[i].Id] = (eskiYeniNo[Katlar[i].Id].EskiNo, i + 1);
        }

        var noMap = eskiYeniNo.Values
            .Where(x => x.EskiNo != x.YeniNo)
            .GroupBy(x => x.EskiNo)
            .ToDictionary(g => g.Key, g => g.First().YeniNo);
        if (noMap.Count == 0) return;

        foreach (var kat in Katlar)
        foreach (var hat in kat.PanoBeslemeHatlari)
        {
            if (noMap.TryGetValue(hat.AnaKatNo, out var anaYeniNo)) hat.AnaKatNo = anaYeniNo;
            if (noMap.TryGetValue(hat.TaliKatNo, out var taliYeniNo)) hat.TaliKatNo = taliYeniNo;
        }
    }



    // ─── Proje IO ────────────────────────────────────────────────────────

    /// <summary>
    /// Proje kaydet/aç için ortak JSON ayarları. Enum'lar İSİM olarak yazılır
    /// (JsonStringEnumConverter) — böylece enum'a sonradan değer eklemek/sıralamayı
    /// değiştirmek eski kayıtlı projeleri bozmaz.
    /// </summary>
    public static readonly JsonSerializerOptions ProjeJsonAyarlari = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public void ProjeyiYukle(BasitProjeCizProje proje, string dosyaYolu)
    {
        UndoRedoYiginlariniTemizle();
        CozKatlarAbonelikleri();
        Proje = proje;
        BaglaKatlarAbonelikleri();
        ProjeAsamasiniAc();

        // M12/M16 kabuk ayrımı: yüklenen proje bu modülün disiplininden farklı
        // elektrik içeriği taşıyorsa ayıklanır (geometri korunur). M16'nın eski
        // .e4p12'den zayıf akımı içe aktarması da bu yolla olur.
        string? disiplinNotu = DisiplinMutabakati(dosyaYolu);

        // A3 migrasyonu: 2026-05-24'ten önce kaydedilmiş .e4p12 dosyaları ham
        // milyonlarca segment taşıyabilir → render/snap'i ağırlaştırır. Açılışta
        // 50K üzeri katları transparan sadeleştir (yeni DXF yüklemesinin yaptığı
        // aynı işlem). Otomatik kaydetme yok — kullanıcı isterse Kaydet'e basar.
        var migrasyonNotu = SadelestirEskiSegmentler();

        // Mahal-aktiflik migrasyonu: eski (sürümsüz) Grid/Duvar projelerini yeni
        // semantiğe taşı — eski "tüm odalara otomatik" görünümü korunur.
        Kst.Acoustics.Core.Electric.BasitProjeCiz.Services
            .BasitProjeCizOtomatikYerlestirServisi.MahalGridAktiflikMigrasyonu(Proje);

        KabloModuAktif = false;
        KabloModuDurumu = 0;
        _aktifDevre = null;
        _aktifPrizDevre = null;
        ProjeKilitli = false;
        ElemanFiltreKat = "Tüm Katlar";
        ElemanFiltreOda = TumOdalarSecenek;
        ElemanFiltreTip = "Tümü";
        SeciliDevreId = null;

        OnPropertyChanged(nameof(Katlar));
        OnPropertyChanged(nameof(KatlarTersSira));
        ProjeDosyaYolu = dosyaYolu;
        SeciliKat = (Proje.SeciliKatNo > 0
            ? Proje.Katlar.FirstOrDefault(k => k.KatNo == Proje.SeciliKatNo)
            : null) ?? Proje.Katlar.FirstOrDefault();
        SeciliOda = SeciliKat?.Odalar.FirstOrDefault();
        SeciliEleman = null;
        OnPropertyChanged(nameof(AciklikOzeti));
        DurumMesaji = string.IsNullOrEmpty(migrasyonNotu)
            ? $"Proje yüklendi: {System.IO.Path.GetFileName(dosyaYolu)}"
            : $"Proje yüklendi: {System.IO.Path.GetFileName(dosyaYolu)} — {migrasyonNotu}";
        if (!string.IsNullOrEmpty(disiplinNotu))
            DurumMesaji += $"  ⚠ {disiplinNotu}";

        // 3.2 migrasyonu: eski (M50 bağlantısız) projenin gömülü geometrisi
        // otomatik .e4bina'ya dışa aktarılır. Veri kaybı yok; kalıcılaşması Kaydet ile.
        try
        {
            if (Kst.Acoustics.Core.Electric.BasitProjeCiz.Services
                    .BinaGeometriExportServisi.EskiProjeyiMigrateEt(Proje, dosyaYolu) is { } geoMigrasyon)
                DurumMesaji += $"  · {geoMigrasyon}";
        }
        catch (System.Exception ex)
        {
            DurumMesaji += $"  ⚠ Geometri migrasyonu başarısız: {ex.Message}";
        }

        // Bağlı .e4bina revizyon denetimi (3.1): geometri güncellenmişse uyar.
        if (BinaGeometriGuncellikUyarisi() is { } geoUyari)
            DurumMesaji += $"  ⚠ {geoUyari}";

        // Açılış migrasyonları diskteki dosyayı değiştirmez; kapatıp yeniden açmak
        // veri kaybetmez → yükleme sonrası proje temiz sayılır.
        TemizIsaretle();
        Guncelle3DGorunum();
    }

    /// Eski .e4p12 dosyalarında biriken ham milyonlarca DXF segmentini transparan
    /// sadeleştirir. Saf mantık Core'a (<see cref="Kst.Acoustics.Core.Electric.BasitProjeCiz
    /// .Services.BasitProjeCizDxfServisi.EskiSegmentMigrasyonuUygula"/>) taşındı.
    private string SadelestirEskiSegmentler()
    {
        var (etkilenen, once, sonra) = Kst.Acoustics.Core.Electric.BasitProjeCiz.Services
            .BasitProjeCizDxfServisi.EskiSegmentMigrasyonuUygula(Proje);
        if (etkilenen == 0) return "";
        return $"Migrasyon: {etkilenen} kat sadeleştirildi ({once:N0} → {sonra:N0} segment, yeniden Kaydet öneririz).";
    }

    // ─── M50 bina geometrisi bağlantısı ──────────────────────────────────

    /// .e4bina dosyasını açıp projeye uygular (yol + gömülü kopya + revizyon damgası).
    /// Kat/oda Id'leri korunur; elemanlar/devreler/mahal listesi dokunulmaz.
    public void BinaGeometrisiYukle(string yol)
    {
        var geo = Kst.Acoustics.Core.Bina.Services.BinaGeometriServisi.Ac(yol);
        BinaGeometrisiUygula(geo, yol);
    }

    /// 3.4 — Yüklemeden ÖNCE mutabakat: .e4bina okunur, projenin geometriye
    /// bağımlı kayıtları (eleman/devre/mahal) yeni geometriyle karşılaştırılır.
    /// Askıda veri varsa pencere diyalog gösterir; onaylanırsa Uygula çağrılır.
    public (Kst.Acoustics.Core.Bina.Models.BinaGeometri Geo,
            Kst.Acoustics.Core.Bina.Services.GeometriMutabakatRaporu Rapor) BinaGeometriOnizle(string yol)
    {
        var geo = Kst.Acoustics.Core.Bina.Services.BinaGeometriServisi.Ac(yol);
        var kayitlar = new Kst.Acoustics.Core.Electric.BasitProjeCiz.Services
            .BasitProjeGeometriBagimliVeri(Proje).BagimliKayitlar();
        var rapor = Kst.Acoustics.Core.Bina.Services
            .GeometriMutabakatServisi.Karsilastir(geo, kayitlar);
        return (geo, rapor);
    }

    public void BinaGeometrisiUygula(Kst.Acoustics.Core.Bina.Models.BinaGeometri geo, string? yol)
    {
        bool ilkYukleme = Proje.Katlar.Count == 0;
        UndoRedoYiginlariniTemizle();
        CozKatlarAbonelikleri();
        var sonuc = Kst.Acoustics.Core.Electric.BasitProjeCiz.Services
            .BinaGeometriImportServisi.Uygula(Proje, geo, yol);
        BaglaKatlarAbonelikleri();

        if (ilkYukleme && ModulKuvvetliMi)
        {
            Proje.AydinlatmaAsamasi = BpcAydinlatmaAsamasi.Pano;
            Proje.PrizAsamasi = BpcPrizAsamasi.Priz;
            Proje.SonTasarimPaftasi = BpcPaftaTipi.Aydinlatma;
            ProjeAsamasiniAc();
        }
        Kirlet();
        OnPropertyChanged(nameof(Katlar));
        OnPropertyChanged(nameof(KatlarTersSira));
        SeciliKat = Proje.Katlar.FirstOrDefault();
        SeciliOda = SeciliKat?.Odalar.FirstOrDefault();
        SeciliEleman = null;
        OnPropertyChanged(nameof(AciklikOzeti));

        string ozet = $"Bina geometrisi yüklendi (Rev. {geo.GeometriRevizyon}): " +
                      $"{sonuc.YeniKat} yeni, {sonuc.GuncellenenKat} güncellenen kat.";
        if (sonuc.Uyarilar.Count > 0)
            ozet += "  ⚠ " + string.Join(" ", sonuc.Uyarilar);
        DurumMesaji = ozet;
        Guncelle3DGorunum();
    }

    /// Proje açılışında bağlı .e4bina'nın güncelliğini denetler; yeniyse uyarı döner.
    public string? BinaGeometriGuncellikUyarisi()
    {
        if (string.IsNullOrWhiteSpace(Proje.BinaGeometriYolu) || Proje.BinaGeometriRevizyon is null)
            return null;
        try
        {
            if (!System.IO.File.Exists(Proje.BinaGeometriYolu))
                return "Bağlı .e4bina dosyası bulunamadı; gömülü geometri kopyası kullanılıyor.";
            var guncel = Kst.Acoustics.Core.Bina.Services.BinaGeometriServisi.Ac(Proje.BinaGeometriYolu);
            if (guncel.GeometriRevizyon > Proje.BinaGeometriRevizyon)
                return $"Bina geometrisi güncellenmiş (Rev. {Proje.BinaGeometriRevizyon} → {guncel.GeometriRevizyon}). " +
                       "Yeniden yüklemek için 'Bina Geometrisi Yükle'yi kullanın.";
        }
        catch
        {
            // Denetim başarısızsa sessiz geç — proje açılışını engelleme.
        }
        return null;
    }

    /// Projeyi diske yazar. Atomik yazım: önce geçici dosyaya yazılır, sonra
    /// hedefle değiştirilir — yazım ortasında çökme mevcut dosyayı bozmaz.
    /// Hata durumunda çökmek yerine durum çubuğuna mesaj yazıp false döner.
    public bool ProjeyiKaydet(string dosyaYolu)
    {
        // Güncel şema sürümünü damgala (mahal-aktiflik semantiği vb.)
        Proje.SemaSurum = Kst.Acoustics.Core.Electric.BasitProjeCiz.Services
            .BasitProjeCizOtomatikYerlestirServisi.GuncelSemaSurum;
        Proje.ModulDisiplini = ModulModu;  // kabuk kimliğini damgala
        Proje.SeciliKatNo = SeciliKat?.KatNo ?? 0;  // çalışılan katı sakla

        var gecici = dosyaYolu + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(Proje, ProjeJsonAyarlari);
            System.IO.File.WriteAllText(gecici, json);
            // Atomik değiştir: hedef varsa Replace, yoksa Move.
            if (System.IO.File.Exists(dosyaYolu))
                System.IO.File.Replace(gecici, dosyaYolu, null);
            else
                System.IO.File.Move(gecici, dosyaYolu);

            ProjeDosyaYolu = dosyaYolu;
            TemizIsaretle();
            DurumMesaji = $"Proje kaydedildi: {System.IO.Path.GetFileName(dosyaYolu)}";
            return true;
        }
        catch (System.Exception ex)
        {
            // Yarım kalan geçici dosyayı bırakma (Replace/Move başarısızsa arta kalır).
            try { if (System.IO.File.Exists(gecici)) System.IO.File.Delete(gecici); }
            catch (System.Exception temizlikEx)
            {
                System.Diagnostics.Debug.WriteLine($"[M12] Geçici kayıt dosyası silinemedi: {temizlikEx.Message}");
            }
            DurumMesaji = $"Proje kaydedilemedi: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// M12/M16 kabuk ayrımı mutabakatı. Yüklenen projedeki başka-disiplin elektrik
    /// içeriğini (eleman/kablo/devre/mahal) ayıklar; geometri korunur. Döndürülen
    /// not boş değilse durum çubuğunda uyarı olarak gösterilir. ModulDisiplini damgalanır.
    /// </summary>
    private string? DisiplinMutabakati(string dosyaYolu)
    {
        var sayim = Kst.Acoustics.Core.Electric.BasitProjeCiz.Services
            .DisiplinAyiklamaServisi.Say(Proje);
        bool yabanciVar = ModulKuvvetliMi ? sayim.ZayifVar : sayim.KuvvetliVar;

        if (!yabanciVar)
        {
            Proje.ModulDisiplini = ModulModu;
            return null;
        }

        var sonuc = Kst.Acoustics.Core.Electric.BasitProjeCiz.Services
            .DisiplinAyiklamaServisi.Ayikla(Proje, ModulModu);

        bool farkliUzanti = !dosyaYolu.EndsWith($".{ProjeUzanti}", System.StringComparison.OrdinalIgnoreCase);
        string atilan = ModulKuvvetliMi ? "zayıf akım" : "kuvvetli akım";
        string oneri = farkliUzanti
            ? $" İçe aktarıldı; '.{ProjeUzanti}' olarak kaydedin."
            : "";
        return $"{atilan} içeriği bu modüle ait değil, ayıklandı " +
               $"({sonuc.SilinenEleman} eleman, {sonuc.SilinenKablo} kablo, {sonuc.SilinenMahalSatiri} mahal satırı).{oneri}";
    }

    public string KabloModuButonYazisi => KabloModuAktif ? "Kablo Modunu Kapat" : "Kablo Modu";
    public bool GosterKomutatorSecim => KabloModuDurumu == 1 && _seciliKabloAnahtari is { } anahtar &&
                                       BasitProjeCizKabloServisi.MaxDevreNo(anahtar.Tip) > 1;
    public bool GosterUcuncuDevre => _seciliKabloAnahtari?.Tip == BpcElemanTipi.UcluKomutator;

    public void KumandaDevresiniSec(int numara)
    {
        if (!GosterKomutatorSecim || SeciliKat is not { } kat || _seciliKabloAnahtari is not { } anahtar ||
            numara < 1 || numara > BasitProjeCizKabloServisi.MaxDevreNo(anahtar.Tip) || KilitliyseDur("devre seçmek")) return;
        if (!kat.Devreler.Any(d => d.AnahtarElemanId == anahtar.Id && d.DevreNo == numara))
            ElemanSnapshotAl(kat);
        KomutatorDevreNo = numara;
        _aktifDevre = BasitProjeCizKabloServisi.EnsureDevre(kat, anahtar.Id, numara);
        SeciliDevreId = _aktifDevre.Id;
        GuncelleKabloGorunum();
        AdayArmaturleriVurgula(kat, anahtar);
    }

    partial void OnKabloModuAktifChanged(bool value)   => OnPropertyChanged(nameof(KabloModuButonYazisi));
    partial void OnKabloModuDurumuChanged(int value)
    {
        OnPropertyChanged(nameof(GosterKomutatorSecim));
        OnPropertyChanged(nameof(GosterOdadakileriAta));
    }

    private void GuncelleTumKatPanolari()
    {
        var seciliId = SeciliAnaBeslemePanosu?.Id;
        TumKatPanolari.Clear();
        foreach (var pano in Katlar
                     .SelectMany(k => k.Elemanlar.Where(e => e.Tip == BpcElemanTipi.KatPanosu))
                     .OrderBy(p => PanoKatNo(p.Id))
                     .ThenBy(p => PanoEtiket(p)))
            TumKatPanolari.Add(pano);
        SeciliAnaBeslemePanosu = TumKatPanolari.FirstOrDefault(p => p.Id == seciliId) ?? TumKatPanolari.FirstOrDefault();
    }

    private int PanoKatNo(string panoId)
        => Katlar.FirstOrDefault(k => k.Elemanlar.Any(e => e.Id == panoId))?.KatNo ?? 0;

    private BasitProjeKat? PanoKat(string panoId)
        => Katlar.FirstOrDefault(k => k.Elemanlar.Any(e => e.Id == panoId));

    // ─── Kablo tavası çizimi ─────────────────────────────────────────────

    [RelayCommand]
    public void KabloTavasiCizBaslat()
    {
        if (SeciliKat == null) return;
        if (KilitliyseDur("kablo tavası çizmek")) return;
        KabloModuKapat();
        KabloTavasiTaliHatModuAktif = false;
        _kabloTavasiCizimNoktalar.Clear();
        KabloTavasiCizimAktif = true;
        SeciliKabloTavasi = null;
        EditablePolygon = null;
        DurumMesaji = "Kablo tavası için rota noktalarını sırayla tıklayın. F8 düz/serbest, Enter bitir, ESC iptal.";
        RenderKablolar();
    }

    [RelayCommand(CanExecute = nameof(CanKabloTavasiCizBitir))]
    public void KabloTavasiCizBitir()
    {
        var kat = SeciliKat;
        if (kat == null || _kabloTavasiCizimNoktalar.Count < 2) return;
        if (KilitliyseDur("kablo tavası çizmek")) return;

        var ad = $"KT{kat.KabloTavalari.Count + 1}";
        var tava = new BpcKabloTavasi
        {
            Ad = ad,
            Adet = System.Math.Clamp(KabloTavasiAdet, 1, 2),
            GenislikMm = System.Math.Max(50, KabloTavasiGenislikMm),
            YukseklikMm = System.Math.Max(25, KabloTavasiYukseklikMm),
            EtKalinligiMm = KabloTavasiEtKalinligiSecenekleri.Contains(KabloTavasiEtKalinligiMm)
                ? KabloTavasiEtKalinligiMm
                : 1.0,
            AgirHizmet = KabloTavasiAgirHizmet,
            KotM = KabloTavasiKotM,
            Rota = _kabloTavasiCizimNoktalar.ToList()
        };
        kat.KabloTavalari.Add(tava);
        SeciliKabloTavasi = tava;
        _kabloTavasiCizimNoktalar.Clear();
        KabloTavasiCizimAktif = false;
        KabloTavasiTaliHatModuAktif = true;
        DurumMesaji = $"{ad} ana kablo tavası eklendi. Tali genişliği seçip odadaki noktalara tıklayın.";
        Guncelle3DGorunum();
    }

    private bool CanKabloTavasiCizBitir() => _kabloTavasiCizimNoktalar.Count >= 2;

    [RelayCommand]
    public void KabloTavasiCizIptal()
    {
        _kabloTavasiCizimNoktalar.Clear();
        KabloTavasiCizimAktif = false;
        DurumMesaji = "Kablo tavası çizimi iptal edildi.";
        Guncelle3DGorunum();
    }

    [RelayCommand]
    public void KabloTavasiDuzCizimDegistir()
    {
        KabloTavasiDuzCizimAktif = !KabloTavasiDuzCizimAktif;
        DurumMesaji = KabloTavasiDuzCizimAktif
            ? "Kablo tavası düz çizim açık: yeni nokta yatay/dikey kilitlenir."
            : "Kablo tavası serbest çizim açık.";
    }

    [RelayCommand]
    public void KabloTavasiTaliHatModuDegistir()
    {
        if (SeciliKat == null) return;
        if (KilitliyseDur("kablo tavası düzenlemek")) return;
        if (KabloTavasiCizimAktif)
        {
            DurumMesaji = "Tali hat eklemek için önce ana tava çizimini bitirin.";
            return;
        }
        if (!KabloTavasiTaliHatModuAktif && SeciliKabloTavasi is not { Rota.Count: >= 2 })
        {
            DurumMesaji = "Tali hat için önce ana kablo tavasını seçin.";
            return;
        }

        KabloTavasiTaliHatModuAktif = !KabloTavasiTaliHatModuAktif;
        DurumMesaji = KabloTavasiTaliHatModuAktif
            ? $"Tali hat genişliği {KabloTavasiTaliGenislikMm:N0} mm. Odadaki noktaları tıklayın; en yakın ana tavaya düz bağlanır."
            : "Tali hat modu kapatıldı.";
    }

    [RelayCommand]
    public void SeciliKabloTavasiSil()
    {
        var kat = SeciliKat;
        var tava = SeciliKabloTavasi;
        if (kat == null || tava == null) return;
        if (KilitliyseDur("kablo tavası silmek")) return;
        kat.KabloTavalari.Remove(tava);
        SeciliKabloTavasi = null;
        DurumMesaji = "Seçili kablo tavası silindi.";
        Guncelle3DGorunum();
    }

    public void KabloTavasiNoktaEkle(double worldX, double worldY)
    {
        var kat = SeciliKat;
        if (kat == null || !KabloTavasiCizimAktif) return;
        if (KilitliyseDur("kablo tavası çizmek")) return;
        var p = new Point2D(worldX - kat.OriginOffset.X, worldY - kat.OriginOffset.Y);
        if (KabloTavasiDuzCizimAktif && _kabloTavasiCizimNoktalar.Count > 0)
            p = KabloTavasiDuzNoktayaKilitle(_kabloTavasiCizimNoktalar[^1], p);
        if (_kabloTavasiCizimNoktalar.Count == 0 ||
            Mesafe(_kabloTavasiCizimNoktalar[^1], p) > 0.05)
        {
            _kabloTavasiCizimNoktalar.Add(p);
            KabloTavasiCizBitirCommand.NotifyCanExecuteChanged();
            DurumMesaji = $"Kablo tavası noktası eklendi: {_kabloTavasiCizimNoktalar.Count}";
            RenderKablolar();
        }
    }

    public void KabloTavasiTaliHatNoktaEkle(double worldX, double worldY)
    {
        var kat = SeciliKat;
        var tava = SeciliKabloTavasi;
        if (kat == null || tava is not { Rota.Count: >= 2 }) return;
        if (KilitliyseDur("kablo tavası düzenlemek")) return;

        var odaNoktasi = new Point2D(worldX - kat.OriginOffset.X, worldY - kat.OriginOffset.Y);
        var anaHatNoktasi = KabloTavasiEnYakinNokta(odaNoktasi, tava.Rota);
        if (Mesafe(odaNoktasi, anaHatNoktasi) < 0.05)
        {
            DurumMesaji = "Tali hat için ana tavanın dışında bir oda noktası seçin.";
            return;
        }

        var tali = new BpcKabloTavasiTaliHat
        {
            GenislikMm = System.Math.Max(25, KabloTavasiTaliGenislikMm),
            Rota = [odaNoktasi, anaHatNoktasi]
        };
        tava.TaliHatlar.Add(tali);
        DurumMesaji = $"{tava.Ad} için {tali.GenislikMm:N0} mm tali kablo tavası eklendi.";
        Guncelle3DGorunum();
    }

    public bool KabloTavasiTiklamaDetect(double worldX, double worldY)
    {
        var kat = SeciliKat;
        if (kat == null) return false;
        var p = new Point2D(worldX - kat.OriginOffset.X, worldY - kat.OriginOffset.Y);
        const double threshold = 0.22;
        BpcKabloTavasi? best = null;
        double bestDist = double.MaxValue;

        foreach (var tava in kat.KabloTavalari.Where(t => t.Rota.Count >= 2))
        {
            for (int i = 0; i < tava.Rota.Count - 1; i++)
            {
                var d = NoktaSegmentMesafesi(p, tava.Rota[i], tava.Rota[i + 1]);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = tava;
                }
            }
        }

        if (best == null || bestDist > threshold) return false;
        SeciliKabloTavasi = best;
        SeciliDevreId = null;
        DurumMesaji = $"{best.Ad} kablo tavası seçildi. Tali hat için genişliği seçip Tali Hat Ekle'yi açın.";
        RenderKablolar();
        return true;
    }

    // ─── Topraklama paftası ──────────────────────────────────────────────

    [RelayCommand]
    public void TopraklamaAlanSecBaslat()
    {
        var kat = SeciliKat;
        if (kat == null) return;
        if (KilitliyseDur("topraklama düzenlemek")) return;
        KabloModuKapat();
        TopraklamaKazikEkleModuAktif = false;
        kat.Topraklama.SecimNoktalari.Clear();
        TopraklamaAlanSecimAktif = true;
        DurumMesaji = "Topraklama alanı için 4 köşe noktasını sırayla işaretleyin.";
        RenderKablolar();
    }

    public void TopraklamaNoktaEkle(double worldX, double worldY)
    {
        var kat = SeciliKat;
        if (kat == null || !TopraklamaAlanSecimAktif) return;
        if (KilitliyseDur("topraklama düzenlemek")) return;

        var p = new Point2D(worldX - kat.OriginOffset.X, worldY - kat.OriginOffset.Y);
        kat.Topraklama.SecimNoktalari.Add(p);
        if (kat.Topraklama.SecimNoktalari.Count < 4)
        {
            DurumMesaji = $"Topraklama köşesi eklendi: {kat.Topraklama.SecimNoktalari.Count}/4";
            RenderKablolar();
            return;
        }

        TopraklamaSeritleriniOlustur(kat);
        TopraklamaAlanSecimAktif = false;
        DurumMesaji = "30x3.5 mm galvaniz topraklama şeridi ve en fazla 20 m grid oluşturuldu.";
        Guncelle3DGorunum();
    }

    [RelayCommand]
    public void TopraklamaKazikOtomatikAta()
    {
        var kat = SeciliKat;
        if (kat == null || kat.Topraklama.Seritler.Count == 0) return;
        if (KilitliyseDur("topraklama düzenlemek")) return;
        kat.Topraklama.Kaziklar.Clear();
        foreach (var p in TopraklamaBaglantiNoktalari(kat.Topraklama))
            kat.Topraklama.Kaziklar.Add(p);
        DurumMesaji = $"{kat.Topraklama.Kaziklar.Count} adet topraklama kazığı otomatik atandı.";
        Guncelle3DGorunum();
    }

    [RelayCommand]
    public void TopraklamaKazikEkleModuDegistir()
    {
        if (SeciliKat == null) return;
        if (KilitliyseDur("topraklama düzenlemek")) return;
        TopraklamaAlanSecimAktif = false;
        TopraklamaKazikEkleModuAktif = !TopraklamaKazikEkleModuAktif;
        DurumMesaji = TopraklamaKazikEkleModuAktif
            ? "Serbest kazık ekleme açık. Kazık koymak istediğiniz noktaya tıklayın."
            : "Serbest kazık ekleme kapatıldı.";
    }

    public void TopraklamaKazikEkle(double worldX, double worldY)
    {
        var kat = SeciliKat;
        if (kat == null || !TopraklamaKazikEkleModuAktif) return;
        if (KilitliyseDur("topraklama düzenlemek")) return;
        kat.Topraklama.Kaziklar.Add(new Point2D(worldX - kat.OriginOffset.X, worldY - kat.OriginOffset.Y));
        DurumMesaji = "Topraklama kazığı eklendi.";
        Guncelle3DGorunum();
    }

    [RelayCommand]
    public void EkTopraklamaCek()
    {
        var kat = SeciliKat;
        if (kat == null || kat.Topraklama.SecimNoktalari.Count < 4) return;
        if (KilitliyseDur("topraklama düzenlemek")) return;
        TopraklamaBakirCevreOlustur(kat);
        DurumMesaji = "35 mm örgülü bakır ek topraklama dış şerit çevresinden çekildi.";
        Guncelle3DGorunum();
    }

    [RelayCommand]
    public void TopraklamaTemizle()
    {
        var kat = SeciliKat;
        if (kat == null) return;
        if (KilitliyseDur("topraklama temizlemek")) return;
        kat.Topraklama = new BpcTopraklama();
        TopraklamaModlariniKapat();
        DurumMesaji = "Topraklama paftası temizlendi.";
        Guncelle3DGorunum();
    }

    [RelayCommand]
    public void BeslemeHatlariniHesapla()
    {
        if (KilitliyseDur("besleme hattı hesaplamak")) return;
        if (BeslemeUygulamaTipi == BpcPanoBeslemeUygulamaTipi.Busbar)
        {
            DurumMesaji = "Busbar hattı bir sonraki tarifte eklenecek. Şimdilik Kablo uygulama tipini seçin.";
            return;
        }
        var anaPano = SeciliAnaBeslemePanosu;
        var anaKat = anaPano == null ? null : PanoKat(anaPano.Id);
        if (anaPano == null || anaKat == null)
        {
            DurumMesaji = "Kablo hattı için önce ana panoyu seçin.";
            return;
        }
        if (!Katlar.Any(k => k.KabloTavalari.Any(t => t.Rota.Count >= 2)))
        {
            DurumMesaji = "Ana pano besleme kabloları için önce kablo tavası çizilmelidir.";
            return;
        }

        foreach (var kat in Katlar)
            kat.PanoBeslemeHatlari.RemoveAll(h => h.AnaPanoId == anaPano.Id && h.UygulamaTipi == BpcPanoBeslemeUygulamaTipi.Kablo);

        // Kablo tavası beslemesi yalnız KAT/ANA panolarını (riser dağıtım panoları)
        // besler. TALİ panolar doğrudan ana panoya değil, kendi kat panolarına bağlanır
        // (tali → kat → ana); bu bağ TaliPanolariAnaPanoyaBagla ile kurulur. Tali panoyu
        // burada da beslemek çift besleme hattı (belirsiz ebeveyn + çift kablo boyu) üretir.
        int sira = 1;
        foreach (var hedefKat in Katlar)
        foreach (var tali in hedefKat.Elemanlar.Where(e => e.Tip == BpcElemanTipi.KatPanosu
                     && e.Id != anaPano.Id
                     && !BasitProjeCizKabloServisi.PanoTaliMi(e)
                     && !BasitProjeCizKabloServisi.PanoAltMi(e)))
        {
            var parcalar = BeslemeKabloRotaParcalari(anaKat, anaPano, hedefKat, tali);
            if (parcalar.Count == 0) continue;
            var etiket = $"BK{sira++}";
            foreach (var (kat, rota) in parcalar.Where(p => p.Rota.Count >= 2))
            {
                kat.PanoBeslemeHatlari.Add(new BpcPanoBeslemeHatti
                {
                    AnaPanoId = anaPano.Id,
                    TaliPanoId = tali.Id,
                    AnaKatNo = anaKat.KatNo,
                    TaliKatNo = hedefKat.KatNo,
                    UygulamaTipi = BpcPanoBeslemeUygulamaTipi.Kablo,
                    Rota = rota,
                    Etiket = etiket
                });
            }
        }

        DurumMesaji = $"{sira - 1} adet ana pano → tali pano kablo hattı hesaplandı.";
        Guncelle3DGorunum();
    }

    [RelayCommand]
    public void BeslemeHatlariniTemizle()
    {
        if (KilitliyseDur("besleme hatlarını temizlemek")) return;
        foreach (var kat in Katlar)
            kat.PanoBeslemeHatlari.Clear();
        DurumMesaji = "Busbar / kablo besleme hatları temizlendi.";
        Guncelle3DGorunum();
    }

    private void TopraklamaModlariniKapat()
    {
        TopraklamaAlanSecimAktif = false;
        TopraklamaKazikEkleModuAktif = false;
    }

    private static void TopraklamaSeritleriniOlustur(BasitProjeKat kat)
    {
        var t = kat.Topraklama;
        var (minX, minY, maxX, maxY) = TopraklamaDisSinir(kat);
        t.Seritler.RemoveAll(s => s.Tip == BpcTopraklamaSeritTipi.GalvanizSerit);
        t.EkTopraklamaVar = t.Seritler.Any(s => s.Tip == BpcTopraklamaSeritTipi.OrguBakir);

        void Add(double ax, double ay, double bx, double by) => t.Seritler.Add(new BpcTopraklamaSerit
        {
            A = new Point2D(ax, ay),
            B = new Point2D(bx, by),
            Tip = BpcTopraklamaSeritTipi.GalvanizSerit
        });

        Add(minX, minY, maxX, minY);
        Add(maxX, minY, maxX, maxY);
        Add(maxX, maxY, minX, maxY);
        Add(minX, maxY, minX, minY);

        var w = maxX - minX;
        var h = maxY - minY;
        var xBolum = System.Math.Max(1, (int)System.Math.Ceiling(w / 20.0));
        var yBolum = System.Math.Max(1, (int)System.Math.Ceiling(h / 20.0));
        for (int i = 1; i < xBolum; i++)
        {
            var x = minX + w * i / xBolum;
            Add(x, minY, x, maxY);
        }
        for (int i = 1; i < yBolum; i++)
        {
            var y = minY + h * i / yBolum;
            Add(minX, y, maxX, y);
        }
    }

    private static void TopraklamaBakirCevreOlustur(BasitProjeKat kat)
    {
        var t = kat.Topraklama;
        var (minX, minY, maxX, maxY) = TopraklamaDisSinir(kat);
        const double ofset = 0.45;
        minX -= ofset; minY -= ofset; maxX += ofset; maxY += ofset;
        t.Seritler.RemoveAll(s => s.Tip == BpcTopraklamaSeritTipi.OrguBakir);
        t.Seritler.AddRange(new[]
        {
            new BpcTopraklamaSerit { A = new(minX, minY), B = new(maxX, minY), Tip = BpcTopraklamaSeritTipi.OrguBakir },
            new BpcTopraklamaSerit { A = new(maxX, minY), B = new(maxX, maxY), Tip = BpcTopraklamaSeritTipi.OrguBakir },
            new BpcTopraklamaSerit { A = new(maxX, maxY), B = new(minX, maxY), Tip = BpcTopraklamaSeritTipi.OrguBakir },
            new BpcTopraklamaSerit { A = new(minX, maxY), B = new(minX, minY), Tip = BpcTopraklamaSeritTipi.OrguBakir }
        });
        t.EkTopraklamaVar = true;
    }

    private static (double minX, double minY, double maxX, double maxY) TopraklamaDisSinir(BasitProjeKat kat)
    {
        var secim = kat.Topraklama.SecimNoktalari;
        var sx1 = secim.Min(p => p.X); var sy1 = secim.Min(p => p.Y);
        var sx2 = secim.Max(p => p.X); var sy2 = secim.Max(p => p.Y);

        var pts = kat.Odalar
            .Where(o => o.Poligon.Count > 0 && o.Poligon.Any(p => p.X >= sx1 && p.X <= sx2 && p.Y >= sy1 && p.Y <= sy2))
            .SelectMany(o => o.Poligon)
            .Where(p => p.X >= sx1 - 0.05 && p.X <= sx2 + 0.05 && p.Y >= sy1 - 0.05 && p.Y <= sy2 + 0.05)
            .ToList();

        if (pts.Count == 0)
        {
            pts.AddRange(kat.DxfSegmentleri.SelectMany(s => new[] { s.A, s.B })
                .Where(p => p.X >= sx1 && p.X <= sx2 && p.Y >= sy1 && p.Y <= sy2));
        }
        if (pts.Count == 0) pts.AddRange(secim);

        return (pts.Min(p => p.X), pts.Min(p => p.Y), pts.Max(p => p.X), pts.Max(p => p.Y));
    }

    private static IEnumerable<Point2D> TopraklamaBaglantiNoktalari(BpcTopraklama t)
    {
        var pts = new List<Point2D>();
        foreach (var s in t.Seritler.Where(s => s.Tip == BpcTopraklamaSeritTipi.GalvanizSerit))
        {
            pts.Add(s.A);
            pts.Add(s.B);
        }
        return pts
            .GroupBy(p => $"{System.Math.Round(p.X, 3)}:{System.Math.Round(p.Y, 3)}")
            .Select(g => g.First());
    }

    private static List<(BasitProjeKat Kat, List<Point2D> Rota)> BeslemeKabloRotaParcalari(
        BasitProjeKat anaKat,
        BasitProjeEleman anaPano,
        BasitProjeKat hedefKat,
        BasitProjeEleman taliPano)
    {
        var start = new Point2D(anaPano.X, anaPano.Y);
        var end = new Point2D(taliPano.X, taliPano.Y);
        var anaTava = EnYakinTavaRota(anaKat, start);
        var hedefTava = EnYakinTavaRota(hedefKat, end);
        if (anaTava == null || hedefTava == null)
            return [(hedefKat, [start, end])];

        if (anaKat == hedefKat)
        {
            var ara = TavaUzerindenRota(anaTava, start, end);
            return [(hedefKat, [start, .. ara, end])];
        }

        var anaSaft = EnYakinTavaNoktasi(anaTava, start);
        var hedefSaft = EnYakinTavaNoktasi(hedefTava, new Point2D(anaSaft.X, anaSaft.Y));
        var anaAra = TavaUzerindenRota(anaTava, start, anaSaft);
        var hedefAra = TavaUzerindenRota(hedefTava, hedefSaft, end);
        return
        [
            (anaKat, [start, .. anaAra, anaSaft]),
            (hedefKat, [hedefSaft, .. hedefAra, end])
        ];
    }

    private static IReadOnlyList<Point2D>? EnYakinTavaRota(BasitProjeKat kat, Point2D p)
    {
        IReadOnlyList<Point2D>? best = null;
        var bestD = double.MaxValue;
        foreach (var rota in TumTavaRotalari(kat))
        {
            var q = KabloTavasiEnYakinNokta(p, rota);
            var d = Mesafe(p, q);
            if (d < bestD)
            {
                bestD = d;
                best = rota;
            }
        }
        return best;
    }

    private static IEnumerable<IReadOnlyList<Point2D>> TumTavaRotalari(BasitProjeKat kat)
    {
        foreach (var tava in kat.KabloTavalari.Where(t => t.Rota.Count >= 2))
        {
            yield return tava.Rota;
            foreach (var tali in tava.TaliHatlar.Where(t => t.Rota.Count >= 2))
                yield return tali.Rota;
        }
    }

    private static Point2D EnYakinTavaNoktasi(IReadOnlyList<Point2D> rota, Point2D p)
        => KabloTavasiEnYakinNokta(p, rota);

    private static List<Point2D> TavaUzerindenRota(IReadOnlyList<Point2D> tavaRota, Point2D from, Point2D to)
    {
        var a = TavaProjeksiyon(tavaRota, from);
        var b = TavaProjeksiyon(tavaRota, to);
        var forward = TavaParcasi(tavaRota, a, b, ters: false);
        var backward = TavaParcasi(tavaRota, a, b, ters: true);
        return RouteLength(forward) <= RouteLength(backward) ? forward : backward;
    }

    private static (Point2D p, int seg, double t) TavaProjeksiyon(IReadOnlyList<Point2D> rota, Point2D p)
    {
        var best = (p: rota[0], seg: 0, t: 0.0);
        var bestD = double.MaxValue;
        for (int i = 0; i < rota.Count - 1; i++)
        {
            var a = rota[i];
            var b = rota[i + 1];
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var len2 = dx * dx + dy * dy;
            var t = len2 < 1e-9 ? 0 : System.Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2, 0, 1);
            var q = new Point2D(a.X + dx * t, a.Y + dy * t);
            var d = Mesafe(p, q);
            if (d < bestD)
            {
                bestD = d;
                best = (q, i, t);
            }
        }
        return best;
    }

    private static List<Point2D> TavaParcasi(
        IReadOnlyList<Point2D> rota,
        (Point2D p, int seg, double t) a,
        (Point2D p, int seg, double t) b,
        bool ters)
    {
        if (!ters)
        {
            if (a.seg > b.seg || (a.seg == b.seg && a.t > b.t))
                return TavaParcasi(rota, b, a, ters: true);
            var result = new List<Point2D> { a.p };
            for (int i = a.seg + 1; i <= b.seg; i++)
                result.Add(rota[i]);
            result.Add(b.p);
            return NoktaTekrarlariniTemizle(result);
        }
        var duz = TavaParcasi(rota, b, a, ters: false);
        duz.Reverse();
        return duz;
    }

    private static List<Point2D> NoktaTekrarlariniTemizle(IEnumerable<Point2D> pts)
    {
        var result = new List<Point2D>();
        foreach (var p in pts)
            if (result.Count == 0 || Mesafe(result[^1], p) > 0.03)
                result.Add(p);
        return result;
    }

    private static double RouteLength(IReadOnlyList<Point2D> pts)
    {
        double total = 0;
        for (int i = 0; i < pts.Count - 1; i++)
            total += Mesafe(pts[i], pts[i + 1]);
        return total;
    }

    // ─── Kablo modu komutları ─────────────────────────────────────────────

    // ── Kablo modu UX altyapısı (2026-07-19) ─────────────────────────────
    // Aktif alt mod: "Manuel" | "Mahal" | "BuatPano" | "PrizManuel" | "PrizMahal" | "".
    // Panel butonları ToggleButton olarak buna bağlanır (aktif mod vurgulu görünür).
    [ObservableProperty] private string _kabloAltMod = "";
    public bool KabloAltModManuelMi     => KabloAltMod == "Manuel";
    public bool KabloAltModMahalMi      => KabloAltMod == "Mahal";
    public bool KabloAltModBuatPanoMi   => KabloAltMod == "BuatPano";
    public bool KabloAltModPrizManuelMi => KabloAltMod == "PrizManuel";
    public bool KabloAltModPrizMahalMi  => KabloAltMod == "PrizMahal";
    partial void OnKabloAltModChanged(string value)
    {
        OnPropertyChanged(nameof(KabloAltModManuelMi));
        OnPropertyChanged(nameof(KabloAltModMahalMi));
        OnPropertyChanged(nameof(KabloAltModBuatPanoMi));
        OnPropertyChanged(nameof(KabloAltModPrizManuelMi));
        OnPropertyChanged(nameof(KabloAltModPrizMahalMi));
    }

    /// Adım göstergesi: "① Anahtar ✓ → ② Armatür (3) → ③ Buat" biçiminde.
    [ObservableProperty] private string _kabloAdimGosterge = "";
    public bool KabloAdimGostergeVar => !string.IsNullOrEmpty(KabloAdimGosterge);
    partial void OnKabloAdimGostergeChanged(string value)
        => OnPropertyChanged(nameof(KabloAdimGostergeVar));

    /// Kablo modunda imleç altındaki (tıklanacak) elemanın vurgusu.
    [ObservableProperty] private Point3D? _hoverElemanPos;

    /// Anahtar seçiliyken odadaki boş armatürlerin aday vurguları.
    [ObservableProperty] private IReadOnlyList<Point3D>? _adayElemanPosList;

    /// Yarım kalmış bir hat/atama var mı? (Esc'nin iptal mi kapatma mı olduğunu belirler.)
    public bool KabloAktifHatVarMi =>
        _aktifDevre != null || _aktifPrizDevre != null || _aktifSistemDevre != null ||
        _buatPanoSeciliBuat != null ||
        KabloModuDurumu is 1 or 2 or 4;

    private void AdimGostergeGuncelle()
    {
        OnPropertyChanged(nameof(GosterOdadakileriAta));
        if (!KabloModuAktif) { KabloAdimGosterge = ""; return; }

        if (KabloModuDurumu is 3 or 4)
        {
            if (PrizPanoKablosuAdimi)
            {
                KabloAdimGosterge = KabloModuDurumu == 3 ? "Buat veya doğrudan priz → Atanmış pano" : "Kaynak seçildi → Atanmış pano";
                return;
            }
            KabloAdimGosterge = KabloModuDurumu == 3
                ? "① Buat → ② Pano"
                : "① Buat ✓ → ② Pano";
            return;
        }
        if (AktifPafta == BpcPaftaTipi.Priz)
        {
            if (PrizMahalKablosuAdimi)
            {
                KabloAdimGosterge = _aktifPrizDevre == null ? "Priz buatı → Prizler" : $"Buat seçildi · {_aktifPrizDevre.PrizElemanIds.Count} priz";
                return;
            }
            KabloAdimGosterge = _aktifPrizDevre == null
                ? "① Priz → ② Buat"
                : $"① Priz ({_aktifPrizDevre.PrizElemanIds.Count}) → ② Buat {(string.IsNullOrEmpty(_aktifPrizDevre.BuatId) ? "" : "✓")}";
            return;
        }
        if (BpcSistemKatalogu.Get(AktifPafta).Disiplin == BpcDisiplin.ZayifAkim)
        {
            KabloAdimGosterge = _aktifSistemDevre == null
                ? "① Cihazlar → ② Merkez"
                : $"① Cihaz ({_aktifSistemDevre.CihazIds.Count}) → ② Merkez {(string.IsNullOrEmpty(_aktifSistemDevre.MerkezId) ? "" : "✓")}";
            return;
        }
        if (AktifPafta == BpcPaftaTipi.Aydinlatma)
        {
            if (KabloModuDurumu == 2)
            {
                KabloAdimGosterge = "① Vavien ✓ → ② İkinci Vavien → ③ Armatürler";
                return;
            }
            KabloAdimGosterge = _aktifDevre == null
                ? "① Anahtar → ② Armatürler → ③ Buat"
                : $"① Anahtar ✓ → ② Armatür ({_aktifDevre.ArmaturElemanIds.Count}) → ③ Buat {(string.IsNullOrEmpty(_aktifDevre.BuatId) ? "" : "✓")}";
            return;
        }
        KabloAdimGosterge = "";
    }

    /// Aktif aydınlatma devresinin rotasını ANINDA çizer (canlı geri bildirim).
    /// Her atama tıklamasından sonra çağrılır; ayrıca "Hesapla" adımı gerektirmez.
    private void AktifDevreRotasiniCiz(BasitProjeKat kat)
    {
        if (_aktifDevre == null) return;
        var wallSlots = BasitProjeCizKabloServisi.HesaplaWallSlotlari(kat);
        int slot = wallSlots.TryGetValue(_aktifDevre.Id, out var s) ? s : 0;
        BasitProjeCizKabloServisi.RotaHesapla(kat, _aktifDevre, KabloRotaTipi, slot);
        // Bir kumandanın ortak buat beslemesi yalnız birinci devrede çizilir.
        if (_aktifDevre.DevreNo > 1 && kat.Devreler.FirstOrDefault(d =>
                d.AnahtarElemanId == _aktifDevre.AnahtarElemanId && d.DevreNo == 1) is { } ortak)
            BasitProjeCizKabloServisi.RotaHesapla(kat, ortak, KabloRotaTipi, wallSlots.GetValueOrDefault(ortak.Id));
        SeciliDevreId = _aktifDevre.Id;
        GuncelleKabloGorunum();
    }

    /// Aktif priz devresinin rotasını anında çizer (buat atanmışsa).
    private void AktifPrizRotasiniCiz(BasitProjeKat kat)
    {
        if (_aktifPrizDevre == null) return;
        BasitProjeCizKabloServisi.PrizRotaHesapla(kat, _aktifPrizDevre);
        SeciliDevreId = _aktifPrizDevre.Id;
        GuncelleKabloGorunum();
    }

    /// Esc: yarım kalan hattı/atamayı iptal eder; mod açık kalır.
    [RelayCommand]
    public void KabloModuHattiIptal()
    {
        _aktifDevre          = null;
        _aktifPrizDevre      = null;
        _aktifSistemDevre    = null;
        _seciliKabloAnahtari = null;
        _buatPanoSeciliBuat  = null;
        OnPropertyChanged(nameof(GosterKomutatorSecim));
        AdayElemanPosList = null;
        KabloModuDurumu = KabloAltMod == "BuatPano" ? 3 : 0;
        KabloModuMesaji = KabloAltMod == "BuatPano"
            ? "Panoya bağlanacak buata tıklayın."
            : KabloModuBaslangicMesaji(AktifPafta);
        if (PrizMahalKablosuAdimi) KabloModuMesaji = "Mahaldeki priz buatını seçin.";
        if (PrizPanoKablosuAdimi) KabloModuMesaji = "Priz buatını veya doğrudan beslenecek prizi seçin.";
        SeciliDevreId = null;
        AdimGostergeGuncelle();
        GuncelleKabloGorunum();
        DurumMesaji = "Aktif hat iptal edildi.";
    }

    /// Enter / sağ tık: aktif hattı bitirir (rota hesaplanır, sıradaki hatta geçilir).
    [RelayCommand]
    public void KabloModuHattiBitir()
    {
        if (!KabloAktifHatVarMi) return;
        if (MahalKabloAsamasinda || PanoKabloAsamasinda || PrizMahalKablosuAdimi || PrizPanoKablosuAdimi)
        {
            if (KabloModuDurumu == 4) { KabloModuMesaji = "Hattı tamamlamak için mahale atanan panoyu seçin."; return; }
            if (KabloModuDurumu == 2) { KabloModuMesaji = "Vavien grubunu tamamlamak için ikinci anahtarı seçin."; return; }
            // Her tıklama rotayı zaten çizdi. Enter yalnız geçerli düzenlemeyi bitirir.
            var devreId = _aktifDevre?.Id ?? _aktifPrizDevre?.Id;
            KabloModuHattiIptal();
            SeciliDevreId = devreId;
            GuncelleKabloGorunum();
            DurumMesaji = "Hat tamamlandı.";
            return;
        }
        KabloRotaHesapla();
        AdayElemanPosList = null;
        AdimGostergeGuncelle();
    }

    /// Bant (Shift+sürükleme) seçiminin aktif olacağı durum: hat açıkken.
    public bool KabloBandSecimUygunMu =>
        KabloModuAktif && KabloModuDurumu == 1 &&
        (_aktifDevre != null || _aktifPrizDevre != null);

    /// Bant seçimi: dikdörtgene giren boş armatürler aktif devreye (veya prizler
    /// aktif priz devresine) topluca atanır. Koordinatlar dünya çerçevesindedir.
    public void KabloModuBandSecim(double x1, double y1, double x2, double y2)
    {
        if (!KabloBandSecimUygunMu || SeciliKat is not { } kat) return;
        if (KilitliyseDur("bant ile atamak")) return;

        double ox = kat.OriginOffset.X, oy = kat.OriginOffset.Y;
        double minX = Math.Min(x1, x2) - ox, maxX = Math.Max(x1, x2) - ox;
        double minY = Math.Min(y1, y2) - oy, maxY = Math.Max(y1, y2) - oy;
        bool Icinde(BasitProjeEleman e) =>
            e.X >= minX && e.X <= maxX && e.Y >= minY && e.Y <= maxY;

        if (PrizMahalKablosuAdimi)
        {
            PrizleriAktifBuataBagla(kat, kat.Elemanlar.Where(Icinde));
            AdimGostergeGuncelle();
            return;
        }

        int eklenen = 0;
        if (_aktifDevre != null)
        {
            var atanan = kat.Devreler.SelectMany(d => d.ArmaturElemanIds).ToHashSet();
            var hedefler = kat.Elemanlar
                .Where(e => BasitProjeCizKabloServisi.ElemanArmaturMi(e.Tip) &&
                            (!MahalKabloAsamasinda || e.OdaId == _seciliKabloAnahtari?.OdaId) &&
                            !e.SensorKontrollu && !atanan.Contains(e.Id) && Icinde(e))
                .ToList();
            if (hedefler.Count == 0) { KabloModuMesaji = "Bant içinde boş armatür yok."; return; }

            ElemanSnapshotAl(kat);
            foreach (var arm in hedefler)
            {
                BasitProjeCizKabloServisi.ArmaturAtama(kat, _aktifDevre, arm.Id);
                eklenen++;
            }
            AktifDevreRotasiniCiz(kat);
            AdayArmaturleriVurgula(kat, _seciliKabloAnahtari);
            KabloModuMesaji = $"Bant: {eklenen} armatür atandı ({_aktifDevre.ArmaturElemanIds.Count} devrede).";
        }
        else if (_aktifPrizDevre != null)
        {
            var hedefler = kat.Elemanlar
                .Where(e => BasitProjeCizKabloServisi.ElemanPrizMi(e.Tip) &&
                            !BasitProjeCizKabloServisi.ElemanPrizDirektPanoMu(e) &&
                            !_aktifPrizDevre.PrizElemanIds.Contains(e.Id) && Icinde(e))
                .ToList();
            if (hedefler.Count == 0) { KabloModuMesaji = "Bant içinde eklenecek priz yok."; return; }

            ElemanSnapshotAl(kat);
            foreach (var priz in hedefler)
            {
                foreach (var d in kat.PrizDevreleri) d.PrizElemanIds.Remove(priz.Id);
                _aktifPrizDevre.PrizElemanIds.Add(priz.Id);
                eklenen++;
            }
            AktifPrizRotasiniCiz(kat);
            KabloModuMesaji = $"Bant: {eklenen} priz eklendi ({_aktifPrizDevre.PrizElemanIds.Count} devrede).";
        }
        AdimGostergeGuncelle();
        DurumMesaji = $"Bant seçimiyle {eklenen} eleman atandı.";
    }

    /// İmleç hareketinde tıklanacak hedef elemanı vurgular (kablo modunda).
    public void KabloModuHover(double worldX, double worldY)
    {
        if (!KabloModuAktif || SeciliKat is not { } kat)
        {
            if (HoverElemanPos != null) HoverElemanPos = null;
            return;
        }
        double ox = kat.OriginOffset.X, oy = kat.OriginOffset.Y;
        bool anahtarBekle = KabloModuDurumu == 0 || KabloModuDurumu == 2;
        var el = PrizAkisiAktif ? PrizKabloHedefi(kat, worldX - ox, worldY - oy)
            : BulEnYakinEleman(kat, worldX - ox, worldY - oy, tolerans: 1.5, tercihAnahtar: anahtarBekle);
        HoverElemanPos = el == null ? null : new Point3D(el.X + ox, el.Y + oy, 0);
    }

    /// Pafta için varsayılan alt mod.
    private static string VarsayilanAltMod(BpcPaftaTipi pafta) => pafta switch
    {
        BpcPaftaTipi.Aydinlatma => "Manuel",
        BpcPaftaTipi.Priz       => "PrizManuel",
        _                       => ""
    };

    [RelayCommand]
    public void KabloModuToggle()
    {
        if (KabloModuAktif) { KabloModuKapat(); return; }
        if (PrizAkisiAktif && !PrizMahalKablosuAdimi && !PrizPanoKablosuAdimi) return;
        KabloModuAktif    = true;
        KabloModuDurumu   = 0;
        KabloModuMesaji   = KabloModuBaslangicMesaji(AktifPafta);
        _aydinlatmaMahalModu = false;
        _prizMahalModu = false;
        _seciliKabloAnahtari = null;
        OnPropertyChanged(nameof(GosterKomutatorSecim));
        _aktifDevre          = null;
        _aktifPrizDevre      = null;
        _buatPanoSeciliBuat  = null;
        _seciliKlima         = null;
        _aktifSistemDevre    = null;
        EditablePolygon      = null; // Duvara yapışık elemanlar kablo modunda seçilebilsin
        KabloAltMod = VarsayilanAltMod(AktifPafta);
        if (PrizMahalKablosuAdimi) KabloModuMesaji = "Önce priz buatını, sonra aynı mahaldeki prizleri seçin.";
        if (PrizPanoKablosuAdimi)
        {
            KabloAltMod = "BuatPano";
            KabloModuDurumu = 3;
            KabloModuMesaji = "Priz buatını veya doğrudan beslenecek prizi, ardından atanmış panoyu seçin.";
        }
        AdimGostergeGuncelle();
    }

    public void KabloModuKapat()
    {
        KabloModuAktif       = false;
        KabloModuDurumu      = 0;
        KabloModuMesaji      = "";
        _aydinlatmaMahalModu = false;
        _prizMahalModu       = false;
        _seciliKabloAnahtari = null;
        OnPropertyChanged(nameof(GosterKomutatorSecim));
        _aktifDevre          = null;
        _buatPanoSeciliBuat  = null;
        _aktifPrizDevre      = null;
        _seciliKlima         = null;
        _aktifSistemDevre    = null;
        KabloAltMod          = "";
        KabloAdimGosterge    = "";
        HoverElemanPos       = null;
        AdayElemanPosList    = null;
    }

    [ObservableProperty] private string? _seciliDevreId;

    // ─── Modül modu (M12 / M16 kabuk ayrımı) ──────────────────────────────
    /// Pencerenin modül kimliği: M12 = KuvvetliAkim (.e4p12), M16 = ZayifAkim (.e4p16).
    /// Açılışta sabitlenir; çalışma zamanında disiplin değiştirilemez.
    public BpcDisiplin ModulModu { get; private set; } = BpcDisiplin.KuvvetliAkim;
    public bool ModulKuvvetliMi => ModulModu == BpcDisiplin.KuvvetliAkim;
    public bool ModulZayifMi    => ModulModu == BpcDisiplin.ZayifAkim;
    public string ModulAdi => ModulKuvvetliMi
        ? "Eye4Pro Electric - Proje Tasarımı"
        : "Eye4Pro Electric - Zayıf Akım Proje Tasarımı";
    /// Modülün proje dosya uzantısı (noktasız).
    public string ProjeUzanti => ModulKuvvetliMi ? "e4p12" : "e4p16";
    public string ProjeDosyaEtiketi => ModulKuvvetliMi
        ? "EYE4PRO M12 Proje Tasarımı (.e4p12)"
        : "EYE4PRO M16 Zayıf Akım Proje Tasarımı (.e4p16)";

    /// Pencere açılışında bir kez çağrılır; disiplini modüle sabitler.
    public void ModulModuAyarla(BpcDisiplin mod)
    {
        ModulModu = mod;
        Proje.ModulDisiplini = mod;
        AktifDisiplin = mod;
        OnPropertyChanged(nameof(ModulKuvvetliMi));
        OnPropertyChanged(nameof(ModulZayifMi));
        OnPropertyChanged(nameof(ModulAdi));
        OnPropertyChanged(nameof(ProjeUzanti));
        OnPropertyChanged(nameof(ProjeDosyaEtiketi));
    }

    // ─── Disiplin / Pafta (sistem) — registry tabanlı ─────────────────────
    [ObservableProperty] private BpcDisiplin _aktifDisiplin = BpcDisiplin.KuvvetliAkim;
    [ObservableProperty] private BpcPaftaTipi _aktifPafta = BpcPaftaTipi.Aydinlatma;
    [ObservableProperty] private BpcSistemTanim? _aktifSistemTanim = BpcSistemKatalogu.Get(BpcPaftaTipi.Aydinlatma);

    /// Üst bardaki disiplin sekmeleri (sabit) ve aktif disiplinin sistem sekmeleri.
    public IReadOnlyList<BpcSistemTanim> KuvvetliSistemler { get; } =
        BpcSistemKatalogu.Disipline(BpcDisiplin.KuvvetliAkim).ToList();
    public IReadOnlyList<BpcSistemTanim> ZayifSistemler { get; } =
        BpcSistemKatalogu.Disipline(BpcDisiplin.ZayifAkim).ToList();
    public ObservableCollection<BpcSistemTanim> AktifDisiplinSistemleri { get; } = new();

    public bool KuvvetliAkimAktif => AktifDisiplin == BpcDisiplin.KuvvetliAkim;
    public bool ZayifAkimAktif    => AktifDisiplin == BpcDisiplin.ZayifAkim;

    // Sağ panelin doğru bölümü için (geriye uyumlu yardımcılar)
    public bool AydinlatmaAktif => AktifPafta == BpcPaftaTipi.Aydinlatma;
    public bool PrizAktif       => AktifPafta == BpcPaftaTipi.Priz;
    public bool MekanikAktif    => AktifPafta == BpcPaftaTipi.Mekanik;
    public bool KabloTavasiAktif => AktifPafta == BpcPaftaTipi.KabloTavasi;
    public bool TopraklamaAktif => AktifPafta == BpcPaftaTipi.Topraklama;
    public bool BusbarKabloAktif => AktifPafta == BpcPaftaTipi.BusbarKablo;
    public bool TasiyiciPaftaAktif => KabloTavasiAktif || TopraklamaAktif || BusbarKabloAktif;
    public bool YanginAktif     => AktifPafta == BpcPaftaTipi.Yangin;
    public bool TopraklamaSecimBekliyor => TopraklamaAlanSecimAktif;
    public bool TopraklamaKazikEklemede => TopraklamaKazikEkleModuAktif;
    public string TopraklamaKazikModuYazisi => TopraklamaKazikEkleModuAktif ? "Kazık Ekleme Kapat" : "Serbest Kazık Ekle";

    private static readonly HashSet<BpcElemanTipi> _dedektorTipleri = new()
    {
        BpcElemanTipi.DumanDedektor, BpcElemanTipi.IsiDedektor, BpcElemanTipi.KombineDedektor
    };

    public bool DedektorOtomatikMumkun =>
        YanginAktif &&
        SeciliZayifElemanSecenek?.Tip is { } t &&
        _dedektorTipleri.Contains(t);

    // ─── İçerik menüsü başlık/etiketleri (disipline göre uyarlanır) ───────
    public string MerkezBaslik => ZayifAkimAktif ? "📡 Merkez İçeriği" : "⚡ Pano İçeriği";
    public string MerkezAdiEtiket => ZayifAkimAktif ? "Merkez adı" : "Pano adı";
    public string MerkezBosMesaji => ZayifAkimAktif
        ? $"Bu katta {AktifSistemTanim?.Ad} merkezi yok. Bir {ZayifElemanAdi(AktifSistemTanim?.MerkezTipi ?? BpcElemanTipi.DataRack)} ekleyip cihazları bağlayın."
        : "Bu katta kat panosu yok. Bir kat panosu ekleyip buatları panoya bağlayın.";

    private void IcerikMenuBasliklariniBildir()
    {
        OnPropertyChanged(nameof(MerkezBaslik));
        OnPropertyChanged(nameof(MerkezAdiEtiket));
        OnPropertyChanged(nameof(MerkezBosMesaji));
    }

    // Aktif zayıf akım sisteminin yerleştirilebilir eleman seçenekleri (combo)
    public ObservableCollection<ZayifElemanSecenek> ZayifElemanSecenekleri { get; } = new();
    [ObservableProperty] private ZayifElemanSecenek? _seciliZayifElemanSecenek;

    private void DoldurAktifDisiplinSistemleri()
    {
        AktifDisiplinSistemleri.Clear();
        foreach (var s in BpcSistemKatalogu.Disipline(AktifDisiplin))
            AktifDisiplinSistemleri.Add(s);
    }

    private void DoldurZayifElemanSecenekleri()
    {
        ZayifElemanSecenekleri.Clear();
        ZayifElemanSecenekleri.Add(new ZayifElemanSecenek { Tip = null, Ad = "Seç/Taşı" });
        if (AktifSistemTanim is { Disiplin: BpcDisiplin.ZayifAkim } t)
        {
            foreach (var ct in t.CihazTipleri)
                ZayifElemanSecenekleri.Add(new() { Tip = ct, Ad = ZayifElemanAdi(ct) });
            foreach (var at in t.AraToplayiciTipleri)
                ZayifElemanSecenekleri.Add(new() { Tip = at, Ad = ZayifElemanAdi(at) });
            ZayifElemanSecenekleri.Add(new() { Tip = t.MerkezTipi, Ad = ZayifElemanAdi(t.MerkezTipi) });
            foreach (var em in t.EkMerkezTipleri)
                ZayifElemanSecenekleri.Add(new() { Tip = em, Ad = ZayifElemanAdi(em) });
        }
        SeciliZayifElemanSecenek = ZayifElemanSecenekleri.FirstOrDefault();
    }

    private static string ZayifElemanAdi(BpcElemanTipi tip)
        => BasitProjeCizKutuphaneServisi.ZayifAkimTanim(tip)?.Ad ?? tip.ToString();

    /// Bir sistemin tüm merkez seçeneklerinin adları ("Kayıt Cihazı (NVR) / Ağ Switch'i").
    private static string MerkezAdlari(BpcSistemTanim tanim)
        => string.Join(" / ", new[] { tanim.MerkezTipi }
            .Concat(tanim.EkMerkezTipleri)
            .Select(ZayifElemanAdi));

    partial void OnAktifDisiplinChanged(BpcDisiplin value)
    {
        // Kabuk ayrımı: disiplin modülün kimliğine sabitlidir; sapma geri çevrilir.
        if (value != ModulModu) { AktifDisiplin = ModulModu; return; }
        OnPropertyChanged(nameof(KuvvetliAkimAktif));
        OnPropertyChanged(nameof(ZayifAkimAktif));
        IcerikMenuBasliklariniBildir();
        DoldurAktifDisiplinSistemleri();
        // Disiplin değişince o disiplinin ilk sistemine geç
        if (AktifDisiplinSistemleri.Count > 0 &&
            (AktifSistemTanim == null || AktifSistemTanim.Disiplin != value))
            AktifSistemTanim = AktifDisiplinSistemleri[0];
    }

    partial void OnAktifSistemTanimChanged(BpcSistemTanim? value)
    {
        if (value != null) AktifPafta = value.Sistem;
        DoldurZayifElemanSecenekleri();
        IcerikMenuBasliklariniBildir();
    }

    partial void OnSeciliZayifElemanSecenekChanged(ZayifElemanSecenek? value)
    {
        OnPropertyChanged(nameof(DedektorOtomatikMumkun));
    }

    partial void OnAktifPaftaChanged(BpcPaftaTipi oldValue, BpcPaftaTipi newValue)
    {
        if (ElemanEkleModu) ElemanEkleModIptal();
        if (AsamaliPaftaAktif)
        {
            Proje.SonTasarimPaftasi = newValue;
            AsamaAraclariniSifirla();
        }
        AsamaGorunumunuBildir();
        var tanim = BpcSistemKatalogu.Get(newValue);
        // Disiplin ve seçili sistem tanımını senkron tut
        if (AktifDisiplin != tanim.Disiplin) AktifDisiplin = tanim.Disiplin;
        if (AktifSistemTanim?.Sistem != newValue) AktifSistemTanim = tanim;

        OnPropertyChanged(nameof(AydinlatmaAktif));
        OnPropertyChanged(nameof(PrizAktif));
        OnPropertyChanged(nameof(MekanikAktif));
        OnPropertyChanged(nameof(KabloTavasiAktif));
        OnPropertyChanged(nameof(TopraklamaAktif));
        OnPropertyChanged(nameof(BusbarKabloAktif));
        OnPropertyChanged(nameof(TasiyiciPaftaAktif));
        OnPropertyChanged(nameof(YanginAktif));
        OnPropertyChanged(nameof(DedektorOtomatikMumkun));
        AktifPaftaMalzemeleriniYenile();
        // Pafta değişince yarım kalmış tüm kablo modu durumlarını sıfırla
        _aydinlatmaMahalModu = false;
        _prizMahalModu       = false;
        _aktifPrizDevre     = null;
        _aktifDevre         = null;
        _seciliKlima        = null;
        _aktifSistemDevre   = null;
        _buatPanoSeciliBuat = null;
        SeciliDevreId       = null;
        if (newValue != BpcPaftaTipi.KabloTavasi && KabloTavasiCizimAktif)
            KabloTavasiCizIptal();
        if (newValue != BpcPaftaTipi.Topraklama)
            TopraklamaModlariniKapat();
        if ((newValue == BpcPaftaTipi.KabloTavasi || newValue == BpcPaftaTipi.Topraklama || newValue == BpcPaftaTipi.BusbarKablo) && KabloModuAktif)
            KabloModuKapat();
        if (KabloModuAktif)
        {
            KabloModuDurumu = 0;
            KabloModuMesaji = KabloModuBaslangicMesaji(newValue);
            KabloAltMod = VarsayilanAltMod(newValue);
            AdayElemanPosList = null;
            AdimGostergeGuncelle();
        }
        if (AsamaSigortaMi ||
            (oldValue == BpcPaftaTipi.Priz && PrizAsamasi == BpcPrizAsamasi.Sigortalar) ||
            (oldValue == BpcPaftaTipi.Aydinlatma && AydinlatmaAsamasi == BpcAydinlatmaAsamasi.Sigortalar))
            GuncellePanoIcerik();
        Guncelle3DGorunum();
    }

    /// Kablo moduna girince/pafta değişince gösterilecek ilk yönerge.
    private static string KabloModuBaslangicMesaji(BpcPaftaTipi sistem)
    {
        var tanim = BpcSistemKatalogu.Get(sistem);
        if (tanim.Disiplin == BpcDisiplin.ZayifAkim)
            return $"{tanim.Ad} cihazına tıklayın";
        return sistem switch
        {
            BpcPaftaTipi.Priz    => "Prize tıklayın",
            BpcPaftaTipi.Mekanik => "Klima ünitesine tıklayın",
            BpcPaftaTipi.KabloTavasi => "Kablo tavası paftasında Tava Çiz'i kullanın",
            BpcPaftaTipi.Topraklama => "Topraklama paftasında 4 köşe seçin",
            BpcPaftaTipi.BusbarKablo => "Ana pano seçip kablo hatlarını hesaplayın",
            _                    => "Anahtara tıklayın"
        };
    }

    [RelayCommand]
    private void DisiplinDegistir(string disiplin)
    {
        // Kabuk ayrımı: M12/M16 penceresi yalnız kendi disiplininde çalışır.
        var istenen = disiplin == "Zayif" ? BpcDisiplin.ZayifAkim : BpcDisiplin.KuvvetliAkim;
        if (istenen == ModulModu) AktifDisiplin = istenen;
    }

    [RelayCommand]
    private void PaftaSec(BpcSistemTanim? tanim)
    {
        if (tanim != null && tanim.Disiplin == ModulModu) AktifSistemTanim = tanim;
    }

    // ─── Pano içeriği menüsü ──────────────────────────────────────────────
    [ObservableProperty] private bool _panoIcerikAcik;
    [ObservableProperty] private PanoOzet? _seciliPanoOzet;
    [ObservableProperty] private PanoLinyeOzet? _seciliLinye;
    /// Pano içeriği linye tablosunun yüksekliği (alt tutamaçtan sürüklenerek ayarlanır).
    [ObservableProperty] private double _panoTabloYuksekligi = 180;
    private bool _panoIcerikGuncelleniyor;

    /// <summary>Seçili kattaki panoların içerik özetleri.</summary>
    public ObservableCollection<PanoOzet> PanoOzetleri { get; } = new();

    public bool PanoCokluMu    => PanoOzetleri.Count > 1;
    public bool PanoIcerikVarMi => PanoOzetleri.Count > 0;

    /// <summary>Seçili panonun adı — menüden düzenlenebilir.</summary>
    public string SeciliPanoAdi
    {
        get => SeciliPanoOzet?.Pano.Ad ?? "";
        set
        {
            if (SeciliPanoOzet?.Pano is not { } pano || pano.Ad == value) return;
            pano.Ad = value;
            pano.EkranAdi = value;
            OnPropertyChanged();
        }
    }

    [RelayCommand]
    public void TogglePanoIcerik() => PanoIcerikAcik = !PanoIcerikAcik;

    // ─── Oda Araçları paneli (sağ taraf, açılır/kapanır + boyutlandırılabilir) ──
    /// Oda menüsü (ODALAR paneli) açıkken sağda görünen Araçlar panelinin açık/kapalı durumu.
    [ObservableProperty] private bool _araclarPaneliAcik = true;
    /// Sağdaki Araçlar panelinin genişliği — sol tutamaçtan sürüklenerek ayarlanır.
    [ObservableProperty] private double _araclarPaneliGenisligi = 240;

    [RelayCommand]
    public void ToggleAraclarPaneli() => AraclarPaneliAcik = !AraclarPaneliAcik;

    partial void OnSeciliPanoOzetChanged(PanoOzet? value)
        => OnPropertyChanged(nameof(SeciliPanoAdi));

    partial void OnSeciliLinyeChanged(PanoLinyeOzet? value)
    {
        if (_panoIcerikGuncelleniyor) return;
        RenderKablolar();
    }

    /// <summary>Pano içerik listesini seçili kattan yeniden kurar (seçimleri korur).</summary>
    public void GuncellePanoIcerik()
    {
        _panoIcerikGuncelleniyor = true;
        var prevPanoId  = SeciliPanoOzet?.Pano.Id;
        var prevLinyeId = SeciliLinye?.BaglantiId;

        PanoOzetleri.Clear();
        var kat = SeciliKat;
        if (kat != null)
        {
            if (AktifDisiplin == BpcDisiplin.ZayifAkim)
            {
                // Zayıf akım: aktif sistemin merkez içerikleri
                foreach (var o in BasitProjeCizPanoServisi.MerkezOzetleri(kat, AktifPafta))
                    PanoOzetleri.Add(o);
            }
            else if (AsamaSigortaMi)
            {
                var ozetler = BasitProjeCizPanoServisi.TasarimPanoOzetleri(Proje, Armatürler.Concat(Prizler), kayitliSigortalariKullan: true);
                foreach (var o in ozetler.Where(o => kat.Elemanlar.Contains(o.Pano)))
                    PanoOzetleri.Add(o);
            }
            else
            {
                // Kuvvetli akım: kat panosu içerikleri.
                // Pafta filtresi YOK — bir pano fiziksel olarak tüm linyelerini (aydınlatma A,
                // priz P, mekanik M) birlikte taşır. Aktif pafta geçilirse diğer linyelerin
                // sigortaları görünmez/kaybolur (ör. aydınlatma kablo modunda priz sigortaları).
                var gucTanimlari = Armatürler.Concat(Prizler).Concat(Klimalar).ToList();
                var ozetler = Katlar.Any(k => k.PanoBeslemeHatlari.Any(h => h.AnaKatNo != h.TaliKatNo))
                    ? BasitProjeCizPanoServisi.ProjePanoOzetleri(Proje, gucTanimlari).Where(o => kat.Elemanlar.Contains(o.Pano))
                    : BasitProjeCizPanoServisi.PanoOzetleri(kat, gucTanimlari);
                foreach (var o in ozetler)
                    PanoOzetleri.Add(o);
            }
        }

        SeciliPanoOzet = PanoOzetleri.FirstOrDefault(o => o.Pano.Id == prevPanoId)
                         ?? PanoOzetleri.FirstOrDefault();
        SeciliLinye = SeciliPanoOzet?.Linyeler.FirstOrDefault(l => l.BaglantiId == prevLinyeId);

        OnPropertyChanged(nameof(PanoCokluMu));
        OnPropertyChanged(nameof(PanoIcerikVarMi));
        _panoIcerikGuncelleniyor = false;
    }

    /// Manuel eleman taşıma sonrası tüm mevcut kablo rotalarını (atamaları
    /// değiştirmeden) yeniden hesaplar. "Kabloları Yenile" butonu bunu çağırır.
    [RelayCommand]
    public void TumKablolariYenidenHesapla()
    {
        var kat = SeciliKat;
        if (kat == null) return;
        if (ProjeKilitli) { DurumMesaji = "Proje kilitli — kabloları yenilemek için önce kilidi açın."; return; }

        ElemanSnapshotAl(kat);

        if (PrizAkisiAktif)
        {
            // Yenile yalnız mevcut priz rotalarını çizer. Mahale yeni bir devre veya pano atamaz.
            BasitProjeCizKabloServisi.TumPrizRotalariniHesapla(kat);
            var uyarilar = BasitProjeCizKabloServisi.BuatPanoRotalariniHesapla(kat, "P");
            RotaUyarilariniGoster(uyarilar);
            GuncelleKabloGorunum();
            DurumMesaji = uyarilar.FirstOrDefault() ?? "Mevcut priz güzergâhları yenilendi.";
            return;
        }

        // Aydınlatma devreleri (duvar slotuyla daisy chain)
        var wallSlots = BasitProjeCizKabloServisi.HesaplaWallSlotlari(kat);
        foreach (var d in kat.Devreler)
        {
            int slot = wallSlots.TryGetValue(d.Id, out var s) ? s : 0;
            BasitProjeCizKabloServisi.RotaHesapla(kat, d, KabloRotaTipi, slot);
        }

        BasitProjeCizKabloServisi.TumPrizRotalariniHesapla(kat);
        BasitProjeCizKabloServisi.MekanikRotalariniHesapla(kat);
        BasitProjeCizKabloServisi.TumSistemRotalariniHesapla(kat);
        RotaUyarilariniGoster(BasitProjeCizKabloServisi.BuatPanoRotalariniHesapla(kat));

        BasitProjeCizKabloServisi.PanoKablolariniNumaralandir(kat);
        BasitProjeCizKabloServisi.SistemKablolariniNumaralandir(kat);
        BasitProjeCizKabloServisi.MekanikKablolariniNumaralandir(kat);

        GuncelleKabloGorunum();
        GuncellePanoIcerik();
        Guncelle3DGorunum();
        RotalarBayat = false;
        DurumMesaji = $"{kat.Kablolar.Count} kablo rotası yeniden hesaplandı.";
        if (KabloModuAktif) KabloModuMesaji = DurumMesaji;
    }

    [RelayCommand]
    public void KabloRotaHesapla()
    {
        var kat = SeciliKat;
        if (kat == null) return;
        if (KilitliyseDur("kablo rotası hesaplamak")) return;
        if (PrizAkisiAktif) { TumKablolariYenidenHesapla(); return; }
        RotalarBayat = false;

        // Zayıf akım paftaları: tüm sistem rotalarını yeniden hesapla
        if (BpcSistemKatalogu.Get(AktifPafta).Disiplin == BpcDisiplin.ZayifAkim)
        {
            BasitProjeCizKabloServisi.TumSistemRotalariniHesapla(kat);
            GuncelleKabloGorunum();
            DurumMesaji = $"{BpcSistemKatalogu.Get(AktifPafta).Ad} rotaları hesaplandı.";
            return;
        }

        // Mekanik paftası: mekanik devrelerini ve rotalarını hesapla
        if (AktifPafta == BpcPaftaTipi.Mekanik)
        {
            BasitProjeCizKabloServisi.MekanikRotalariniHesapla(kat);
            GuncelleKabloGorunum();
            DurumMesaji = "Mekanik (Klima) rotaları hesaplandı.";
            return;
        }

        // Priz paftası: aktif priz devresini hesapla
        if (AktifPafta == BpcPaftaTipi.Priz && _aktifPrizDevre != null)
        {
            if (string.IsNullOrEmpty(_aktifPrizDevre.BuatId))
            {
                KabloModuMesaji = "Önce bir buata tıklayın.";
                return;
            }
            BasitProjeCizKabloServisi.TumPrizRotalariniHesapla(kat);
            SeciliDevreId = _aktifPrizDevre.Id;
            GuncelleKabloGorunum();
            KabloModuMesaji = $"Priz hattı hesaplandı ({_aktifPrizDevre.PrizElemanIds.Count} priz). Sonraki priz için tıklayın.";
            DurumMesaji = "Priz rotası hesaplandı.";

            // Yeni hatta geçiş yapabilmek için aktif devreyi serbest bırakıyoruz
            _aktifPrizDevre = null;
            KabloModuDurumu = 0;
            return;
        }

        if (_aktifDevre != null)
        {
            // Aktif hattı hesapla — state'i DEĞIŞTIRMEZ, kullanıcı sonraki switche tıklayarak geçer
            var wallSlots = BasitProjeCizKabloServisi.HesaplaWallSlotlari(kat);
            int slot = wallSlots.TryGetValue(_aktifDevre.Id, out var s) ? s : 0;
            BasitProjeCizKabloServisi.RotaHesapla(kat, _aktifDevre, KabloRotaTipi, slot);
            SeciliDevreId = _aktifDevre.Id;
            GuncelleKabloGorunum();
            DurumMesaji = "Hat hesaplandı.";
            KabloModuMesaji = $"Hat tamam. Sonraki anahtar için tıklayın.";
        }
        else
        {
            // Aktif devre yok → tüm devreleri yeniden hesapla
            var wallSlots = BasitProjeCizKabloServisi.HesaplaWallSlotlari(kat);
            foreach (var d in kat.Devreler)
            {
                int slot = wallSlots.TryGetValue(d.Id, out var s) ? s : 0;
                BasitProjeCizKabloServisi.RotaHesapla(kat, d, KabloRotaTipi, slot);
            }
            RotaUyarilariniGoster(BasitProjeCizKabloServisi.BuatPanoRotalariniHesapla(kat));
            GuncelleKabloGorunum();
            DurumMesaji = $"{kat.Kablolar.Count} kablo rotası hesaplandı.";
        }
    }

    [RelayCommand]
    public void AydinlatmaMahaliOtomatikBagla()
    {
        var kat = SeciliKat;
        if (kat == null) return;
        if (KilitliyseDur("aydınlatma kablolamak")) return;
        if (!KabloModuAktif) KabloModuAktif = true;

        // Mahal Modu: canvas'ta bir odaya tıklayınca o odanın tüm boş armatürleri
        // odadaki anahtarlara KatAydinlatmasiniOtomatikKablola mantığıyla bağlanır.
        _aydinlatmaMahalModu = true;
        _aktifDevre = null;
        _seciliKabloAnahtari = null;
        KabloModuDurumu = 0;
        KabloAltMod = "Mahal";
        AdayElemanPosList = null;
        AdimGostergeGuncelle();
        KabloModuMesaji = "Mahal Modu: Atama yapmak istediğiniz odaya (veya bir elemana) tıklayın.";
        DurumMesaji = "Mahal Modu aktif. Bir odaya tıklayın.";
    }

    /// Mahal Modu'nda tıklanan oda içindeki boş armatürleri odanın anahtarlarına bağlar.
    public void MahalModuOdayaBagla(BasitProjeKat kat, string odaId)
    {
        if (MahalKabloAsamasinda) { MahalAydinlatmaKablolariniCekIc(odaId); return; }
        if (KilitliyseDur("aydınlatma kablolamak")) return;
        var oda = kat.Odalar.FirstOrDefault(o => o.Id == odaId);
        if (oda == null) return;

        var odadakiAnahtarlar = kat.Elemanlar
            .Where(e => e.OdaId == odaId && BasitProjeCizKabloServisi.ElemanAnahtarMi(e.Tip))
            .ToList();
        if (odadakiAnahtarlar.Count == 0)
        {
            KabloModuMesaji = $"\"{oda.Ad}\" odasında anahtar yok.";
            return;
        }

        ElemanSnapshotAl(kat);

        // Odanın tüm boş armatürlerini anahtarlara dağıt (Tüm Kat mantığıyla tek oda)
        var atananArmaturIds = kat.Devreler.SelectMany(d => d.ArmaturElemanIds).ToHashSet();
        var bosArmaturler = kat.Elemanlar
            .Where(e => e.OdaId == odaId && BasitProjeCizKabloServisi.ElemanArmaturMi(e.Tip) && !atananArmaturIds.Contains(e.Id))
            .ToList();

        if (bosArmaturler.Count == 0)
        {
            KabloModuMesaji = $"\"{oda.Ad}\" odasında boş armatür yok.";
            return;
        }

        int anahtarIdx = 0;
        var gruplar = bosArmaturler.GroupBy(a => a.BlokAdi).ToList();
        foreach (var grup in gruplar)
        {
            var sirali = BasitProjeCizKabloServisi.GeometrikSiralaPublic(
                new Kst.Acoustics.Core.Common.Geometry.Point2D(odadakiAnahtarlar[0].X, odadakiAnahtarlar[0].Y),
                grup.ToList());
            while (sirali.Count > 0 && anahtarIdx < odadakiAnahtarlar.Count)
            {
                var anahtar = odadakiAnahtarlar[anahtarIdx];
                // Kapasite muhasebesi: anahtarın mevcut devre yükü düşülür.
                int kap = Math.Max(0, BasitProjeCizKabloServisi.KapasiteHesaplaPublic(anahtar.Tip)
                    - BasitProjeCizKabloServisi.AnahtarMevcutYuk(kat, anahtar.Id));
                if (kap == 0) { anahtarIdx++; continue; }
                int devNo = BasitProjeCizKabloServisi.MaxDevreNo(anahtar.Tip);
                int atanacak = Math.Min(sirali.Count, kap);
                var atanacaklar = sirali.Take(atanacak).ToList();
                sirali.RemoveRange(0, atanacak);
                int bolum = (int)Math.Ceiling(atanacak / (double)devNo);
                for (int d = 0; d < devNo; d++)
                {
                    var dilim = atanacaklar.Skip(d * bolum).Take(bolum).ToList();
                    if (dilim.Count == 0) continue;
                    var devre = BasitProjeCizKabloServisi.EnsureDevre(kat, anahtar.Id, d + 1);
                    foreach (var arm in dilim)
                        if (!devre.ArmaturElemanIds.Contains(arm.Id))
                            devre.ArmaturElemanIds.Add(arm.Id);
                }
                anahtarIdx++;
            }
        }

        OtomatikBuatPanoBaglantisiYap(kat);
        KabloRotaHesapla();
        OnPropertyChanged(nameof(VurgulananElemanPosList));
        GuncelleElemanVurgu();
        int atanan = bosArmaturler.Count;
        KabloModuMesaji = $"\"{oda.Ad}\" — {atanan} armatür bağlandı. Başka odaya tıklayın.";
        DurumMesaji = $"Mahal Modu: {oda.Ad} tamamlandı ({atanan} armatür).";
    }

    [RelayCommand]
    public void KatAydinlatmasiniOtomatikKablola()
    {
        if (MahalKabloAsamasinda) { MahalAydinlatmaKablolariniCek(); return; }
        var kat = SeciliKat;
        if (kat == null) return;
        if (KilitliyseDur("aydınlatma kablolamak")) return;

        _aydinlatmaMahalModu = false;
        ElemanSnapshotAl(kat);
        BasitProjeCizKabloServisi.KatAydinlatmasiniOtomatikKablola(kat);
        OtomatikBuatPanoBaglantisiYap(kat);

        // Rotaları yeniden hesapla
        KabloRotaHesapla();
        OnPropertyChanged(nameof(VurgulananElemanPosList));
        GuncelleElemanVurgu();
        DurumMesaji = "Kat genelindeki boş aydınlatma armatürleri otomatik kablolandı.";
    }

    // ─── Priz kablo modu komutları ────────────────────────────────────────────

    [RelayCommand]
    public void PrizManuelModuSec()
    {
        _aktifPrizDevre = null;
        _prizMahalModu = false;
        KabloModuDurumu = 0;
        KabloAltMod = "PrizManuel";
        AdimGostergeGuncelle();
        KabloModuMesaji = "Manuel Priz: Bir priz elemanına tıklayın.";
        DurumMesaji = "Manuel priz kablolama modu.";
    }

    [RelayCommand]
    public void PrizMahaliOtomatikBagla()
    {
        var kat = SeciliKat;
        if (kat == null) return;
        if (KilitliyseDur("priz kablolamak")) return;
        if (!KabloModuAktif) KabloModuAktif = true;
        _aktifPrizDevre = null;
        KabloModuDurumu = 0;
        // Priz mahal modunu işaretlemek için küçük bir flag
        _prizMahalModu = true;
        KabloAltMod = "PrizMahal";
        AdimGostergeGuncelle();
        KabloModuMesaji = "Priz Mahal Modu: Priz bağlamak istediğiniz odaya tıklayın.";
        DurumMesaji = "Priz Mahal Modu aktif.";
    }

    [RelayCommand]
    public void KatPrizleriniOtomatikKablola()
    {
        if (PrizAkisiAktif) { PrizMahalKablolariniCek(); return; }
        var kat = SeciliKat;
        if (kat == null) return;
        if (KilitliyseDur("priz kablolamak")) return;
        _prizMahalModu = false;
        ElemanSnapshotAl(kat);
        BasitProjeCizKabloServisi.KatPrizleriniOtomatikKablola(kat);
        OtomatikBuatPanoBaglantisiYap(kat);
        BasitProjeCizKabloServisi.TumPrizRotalariniHesapla(kat);
        GuncelleKabloGorunum();
        GuncellePanoIcerik();
        RotalarBayat = false;
        DurumMesaji = "Kattaki prizler otomatik kablolandı.";
    }

    /// Tek oda priz bağlantısı (Priz Mahal Modu).
    public void PrizMahalModuOdayaBagla(BasitProjeKat kat, string odaId)
    {
        if (KilitliyseDur("priz kablolamak")) return;
        var oda = kat.Odalar.FirstOrDefault(o => o.Id == odaId);
        if (oda == null) return;
        ElemanSnapshotAl(kat);
        BasitProjeCizKabloServisi.KatPrizleriniOtomatikKablola(kat, odaId);
        BasitProjeCizKabloServisi.TumPrizRotalariniHesapla(kat);
        GuncelleKabloGorunum();
        KabloModuMesaji = $"\"{oda.Ad}\" priz bağlantısı tamamlandı. Başka odaya tıklayın.";
        DurumMesaji = $"Priz Mahal Modu: {oda.Ad} tamamlandı.";
    }

    [RelayCommand]
    public void AydinlatmaManuelModuSec()
    {
        _aydinlatmaMahalModu = false;
        _seciliKabloAnahtari = null;
        _aktifDevre = null;
        KabloModuDurumu = 0;
        KabloAltMod = "Manuel";
        AdayElemanPosList = null;
        AdimGostergeGuncelle();
        KabloModuMesaji = "Manuel Mod: Anahtara veya sensöre tıklayın.";
        DurumMesaji = "Manuel kablolama modu aktif.";
    }

    [RelayCommand]
    public void SeciliBuatPanoBagla()
    {
        if (SeciliKat == null) return;
        if (KilitliyseDur("buat-pano kablolamak")) return;
        if (!KabloModuAktif) KabloModuAktif = true;
        _buatPanoSeciliBuat = null;
        KabloModuDurumu = 3;
        KabloAltMod = "BuatPano";
        AdimGostergeGuncelle();
        KabloModuMesaji = "Panoya bağlanacak buata tıklayın.";
        DurumMesaji = "Buat→Pano: önce buata tıklayın.";
    }

    private void BuatPanosuBagla(BasitProjeKat kat, BasitProjeEleman buat, BasitProjeEleman pano)
    {
        var gecisOdalari = kat.Odalar.Where(o => o.GecisSecili).Select(o => o.Id).ToList();
        var prefix = AktifPafta == BpcPaftaTipi.Priz ? "P" : "A";
        var baglanti = BasitProjeCizKabloServisi.BuatPanoBagla(kat, buat.Id, pano.Id, gecisOdalari, prefix);
        RotaUyarilariniGoster(BasitProjeCizKabloServisi.BuatPanoRotalariniHesapla(kat, prefix));
        bool kabloCizildi = kat.Kablolar.Any(k =>
            k.Tip == BpcKabloTipi.PanoKablosu && k.DevreId == baglanti.Id && k.Rota.Count >= 2);
        SeciliDevreId = null;
        GuncelleKabloGorunum();
        KabloModuMesaji = kabloCizildi
            ? $"\"{buat.Ad}\" → \"{PanoEtiket(pano)}\" bağlandı. Başka buat için tıklayın."
            : $"\"{buat.Ad}\" bağlantısı kaydedildi ancak izinli rota bulunamadı.";
        DurumMesaji = kabloCizildi
            ? "Buat-pano kablosu çizildi ve numaralandırıldı."
            : "Bağlantı kaydedildi; kablo çizilemedi. Geçiş odalarını kontrol edin.";
    }

    /// Alt → tali → (varsa kat) → ana beslemelerini kurar. Kablo tavası
    /// üzerinden veya önceki projede belirlenmiş üst pano bağlantısını korur.
    private void TaliPanolariAnaPanoyaBagla(BasitProjeKat kat, List<BasitProjeEleman> katPanolari)
    {
        var gecisOdalari = kat.Odalar.Where(o => o.GecisSecili).Select(o => o.Id).ToList();
        var mevcutBeslemeler = Katlar.SelectMany(k => k.PanoBeslemeHatlari).Where(h => !h.OtomatikKatIciBesleme)
            .Select(h => h.TaliPanoId).ToHashSet();
        var gecerliHatIds = new HashSet<string>();
        var etiketler = kat.PanoBeslemeHatlari.Select(h => h.Etiket).ToHashSet();
        int sira = 0;
        foreach (var alt in katPanolari)
        {
            if (BasitProjeCizKabloServisi.PanoAnaMi(alt) || mevcutBeslemeler.Contains(alt.Id)) continue;
            var adaylar = UstPanoSecenekleri(alt, katPanolari).ToList();
            var ust = string.IsNullOrEmpty(alt.TaliPanoHedefPanoId)
                ? adaylar.FirstOrDefault()
                : adaylar.FirstOrDefault(p => p.Id == alt.TaliPanoHedefPanoId);
            if (ust == null) continue;
            var rota = BasitProjeCizKabloServisi.PanoBeslemeRota(kat, alt, ust, gecisOdalari);
            if (rota.Count < 2) continue;

            var hat = kat.PanoBeslemeHatlari.FirstOrDefault(h =>
                h.OtomatikKatIciBesleme && h.TaliPanoId == alt.Id && h.AnaPanoId == ust.Id);
            if (hat == null)
            {
                string etiket;
                do { etiket = $"BK{++sira}"; } while (!etiketler.Add(etiket));
                hat = new BpcPanoBeslemeHatti
                {
                    AnaPanoId = ust.Id, TaliPanoId = alt.Id,
                    AnaKatNo = kat.KatNo, TaliKatNo = kat.KatNo,
                    OtomatikKatIciBesleme = true, Etiket = etiket
                };
                kat.PanoBeslemeHatlari.Add(hat);
            }
            hat.Rota = rota;
            gecerliHatIds.Add(hat.Id);
        }
        kat.PanoBeslemeHatlari.RemoveAll(h => h.OtomatikKatIciBesleme && !gecerliHatIds.Contains(h.Id));
    }

    /// Mevcut devrelerin ve priz devrelerinin buatlarını en yakın kat panosuna bağlar;
    /// tali panoları da ana panoya bağlar. Halihazırda bağlı olanlar atlanır.
    /// Buat→pano rota hesabından dönen "rota bulunamadı" uyarılarını kullanıcıya
    /// gösterir. Boş listede hiçbir şey yapmaz.
    private void RotaUyarilariniGoster(System.Collections.Generic.List<string> uyarilar)
    {
        if (uyarilar == null || uyarilar.Count == 0) return;
        UyariGoster("Çizilemeyen kablolar:\n• " + string.Join("\n• ", uyarilar));
    }

    private void OtomatikBuatPanoBaglantisiYap(BasitProjeKat kat)
    {
        var katPanolari = kat.Elemanlar.Where(e => e.Tip == BpcElemanTipi.KatPanosu).ToList();
        if (katPanolari.Count == 0) return;
        var gecisOdalari = kat.Odalar.Where(o => o.GecisSecili).Select(o => o.Id).ToList();

        foreach (var buatId in kat.Devreler
                     .Where(d => !string.IsNullOrWhiteSpace(d.BuatId))
                     .Select(d => d.BuatId!)
                     .Distinct())
        {
            if (kat.BuatPanoBaglantilari.Any(b => b.BuatId == buatId && b.LinyePrefix == "A")) continue;
            var buat = kat.Elemanlar.FirstOrDefault(e => e.Id == buatId && e.Tip == BpcElemanTipi.Buat);
            var pano = buat == null ? null : BasitProjeCizKabloServisi.BuatIcinHedefPano(buat, katPanolari);
            if (buat == null || pano == null) continue;
            BasitProjeCizKabloServisi.BuatPanoBagla(kat, buat.Id, pano.Id, gecisOdalari, "A");
        }

        foreach (var buatId in kat.PrizDevreleri
                     .Where(d => !string.IsNullOrWhiteSpace(d.BuatId))
                     .Select(d => d.BuatId!)
                     .Distinct())
        {
            if (kat.BuatPanoBaglantilari.Any(b => b.BuatId == buatId && b.LinyePrefix == "P")) continue;
            var buat = kat.Elemanlar.FirstOrDefault(e => e.Id == buatId && e.Tip == BpcElemanTipi.Buat);
            var pano = buat == null ? null : BasitProjeCizKabloServisi.BuatIcinHedefPano(buat, katPanolari);
            if (buat == null || pano == null) continue;
            BasitProjeCizKabloServisi.BuatPanoBagla(kat, buat.Id, pano.Id, gecisOdalari, "P");
        }

        TaliPanolariAnaPanoyaBagla(kat, katPanolari);
        RotaUyarilariniGoster(BasitProjeCizKabloServisi.BuatPanoRotalariniHesapla(kat));
    }

    [RelayCommand]
    public void BosUclariEnYakinPanoyaBagla()
    {
        if (PrizAkisiAktif) { PrizPanoKablolariniCek(); return; }
        if (PanoKabloAsamasinda) { AtanmisPanolaraKabloCek(); return; }
        if (MahalKabloAsamasinda) { MahalAydinlatmaKablolariniCek(); return; }
        var kat = SeciliKat;
        if (kat == null) return;
        if (KilitliyseDur("boş uçları bağlamak")) return;
        ElemanSnapshotAl(kat);

        var katPanolari = kat.Elemanlar.Where(e => e.Tip == BpcElemanTipi.KatPanosu).ToList();
        int baglanan = 0;
        int atlanan = 0;

        if (katPanolari.Count > 0)
        {
            var gecisOdalari = kat.Odalar.Where(o => o.GecisSecili).Select(o => o.Id).ToList();

            foreach (var buatId in kat.Devreler
                         .Where(d => !string.IsNullOrWhiteSpace(d.BuatId))
                         .Select(d => d.BuatId!)
                         .Distinct())
            {
                if (kat.BuatPanoBaglantilari.Any(b => b.BuatId == buatId && b.LinyePrefix == "A"))
                    continue;

                var buat = kat.Elemanlar.FirstOrDefault(e => e.Id == buatId && e.Tip == BpcElemanTipi.Buat);
                var pano = buat == null ? null : BasitProjeCizKabloServisi.BuatIcinHedefPano(buat, katPanolari);
                if (buat == null || pano == null) { atlanan++; continue; }

                BasitProjeCizKabloServisi.BuatPanoBagla(kat, buat.Id, pano.Id, gecisOdalari, "A");
                baglanan++;
            }

            // Priz buatı otomatik kablolama (buat-gerektiren prizleri priz buatına bağla)
            BasitProjeCizKabloServisi.KatPrizleriniOtomatikKablola(kat);

            foreach (var buatId in kat.PrizDevreleri
                         .Where(d => !string.IsNullOrWhiteSpace(d.BuatId))
                         .Select(d => d.BuatId!)
                         .Distinct())
            {
                if (kat.BuatPanoBaglantilari.Any(b => b.BuatId == buatId && b.LinyePrefix == "P"))
                    continue;

                var buat = kat.Elemanlar.FirstOrDefault(e => e.Id == buatId && e.Tip == BpcElemanTipi.Buat);
                var pano = buat == null ? null : BasitProjeCizKabloServisi.BuatIcinHedefPano(buat, katPanolari);
                if (buat == null || pano == null) { atlanan++; continue; }

                BasitProjeCizKabloServisi.BuatPanoBagla(kat, buat.Id, pano.Id, gecisOdalari, "P");
                baglanan++;
            }

            // Müstakil/trifaze/kombine prizler → direk panoya (P linyesi, pano bağlantısı)
            foreach (var priz in kat.Elemanlar.Where(e =>
                BasitProjeCizKabloServisi.ElemanPrizMi(e.Tip) &&
                BasitProjeCizKabloServisi.ElemanPrizDirektPanoMu(e)))
            {
                if (kat.PrizDevreleri.Any(d => d.PrizElemanIds.Contains(priz.Id) && !string.IsNullOrEmpty(d.BuatId)))
                    continue; // zaten bağlı
                var pano = BasitProjeCizKabloServisi.BuatIcinHedefPano(priz, katPanolari);
                if (pano == null) { atlanan++; continue; }
                var devre = kat.PrizDevreleri.FirstOrDefault(d => d.PrizElemanIds.Contains(priz.Id))
                    ?? new BpcPrizDevre();
                if (!devre.PrizElemanIds.Contains(priz.Id)) devre.PrizElemanIds.Add(priz.Id);
                // Doğrudan panolu priz, pano özetinde aynı uç kimliğiyle bulunur.
                devre.BuatId = priz.Id;
                if (!kat.PrizDevreleri.Contains(devre)) kat.PrizDevreleri.Add(devre);
                // Direk pano bağlantısı: priz ID'sini geçici buat gibi kullan (priz buatı yoksa)
                BasitProjeCizKabloServisi.BuatPanoBagla(kat, priz.Id, pano.Id, gecisOdalari, "P");
                baglanan++;
            }

            foreach (var klima in kat.Elemanlar.Where(e =>
                         e.Tip is BpcElemanTipi.KlimaIcUnite or BpcElemanTipi.KlimaDisUnite))
            {
                if (kat.MekanikDevreleri.Any(d => d.KlimaElemanId == klima.Id && !string.IsNullOrWhiteSpace(d.PanoId)))
                    continue;

                var pano = EnYakinEleman(klima, katPanolari);
                if (pano == null) { atlanan++; continue; }

                kat.MekanikDevreleri.RemoveAll(d => d.KlimaElemanId == klima.Id);
                kat.MekanikDevreleri.Add(new BpcMekanikDevre
                {
                    KlimaElemanId = klima.Id,
                    PanoId = pano.Id
                });
                baglanan++;
            }

            // Tali panoları (oda içindeki kat panoları) ana panoya bağla
            TaliPanolariAnaPanoyaBagla(kat, katPanolari);

            RotaUyarilariniGoster(BasitProjeCizKabloServisi.BuatPanoRotalariniHesapla(kat));
            BasitProjeCizKabloServisi.MekanikRotalariniHesapla(kat);
        }

        foreach (var sistem in new[] { BpcPaftaTipi.Data, BpcPaftaTipi.Tv, BpcPaftaTipi.Telefon, BpcPaftaTipi.Cctv })
        {
            var tanim = BpcSistemKatalogu.Get(sistem);
            var cihazlar = kat.Elemanlar.Where(e => tanim.CihazTipleri.Contains(e.Tip)).ToList();
            if (cihazlar.Count == 0) continue;

            var hedefler = kat.Elemanlar.Where(e => tanim.MerkezMi(e.Tip)).ToList();
            if (hedefler.Count == 0)
                hedefler = katPanolari;

            if (hedefler.Count == 0)
            {
                atlanan += cihazlar.Count(c => !ZayifCihazBagliMi(kat, sistem, c.Id));
                continue;
            }

            foreach (var cihaz in cihazlar)
            {
                if (ZayifCihazBagliMi(kat, sistem, cihaz.Id))
                    continue;

                foreach (var d in kat.SistemDevreleri.Where(d => d.Sistem == sistem).ToList())
                {
                    if (!d.CihazIds.Remove(cihaz.Id)) continue;
                    kat.Kablolar.RemoveAll(k => k.DevreId == d.Id);
                    if (d.CihazIds.Count == 0)
                        kat.SistemDevreleri.Remove(d);
                }

                var hedef = EnYakinEleman(cihaz, hedefler);
                if (hedef == null) { atlanan++; continue; }

                var devre = new BpcSistemDevre
                {
                    Sistem = sistem,
                    MerkezId = hedef.Id
                };
                devre.CihazIds.Add(cihaz.Id);
                kat.SistemDevreleri.Add(devre);
                baglanan++;
            }
        }

        BasitProjeCizKabloServisi.TumSistemRotalariniHesapla(kat);
        BasitProjeCizKabloServisi.TumPrizRotalariniHesapla(kat);
        GuncelleKabloGorunum();
        GuncellePanoIcerik();
        Guncelle3DGorunum();
        RotalarBayat = false;

        var atlamaNotu = atlanan > 0 ? $" {atlanan} uç için uygun pano/merkez bulunamadı." : "";
        DurumMesaji = $"{baglanan} boş uç en yakın pano/merkeze bağlandı.{atlamaNotu}";
        if (KabloModuAktif)
            KabloModuMesaji = DurumMesaji;
    }

    private static bool ZayifCihazBagliMi(BasitProjeKat kat, BpcPaftaTipi sistem, string cihazId)
        => kat.SistemDevreleri.Any(d =>
            d.Sistem == sistem &&
            d.CihazIds.Contains(cihazId) &&
            !string.IsNullOrWhiteSpace(d.MerkezId));

    private static BasitProjeEleman? EnYakinEleman(BasitProjeEleman kaynak, IReadOnlyList<BasitProjeEleman> adaylar)
    {
        BasitProjeEleman? enYakin = null;
        double enIyi = double.MaxValue;
        foreach (var aday in adaylar)
        {
            var dx = kaynak.X - aday.X;
            var dy = kaynak.Y - aday.Y;
            var d2 = dx * dx + dy * dy;
            if (d2 >= enIyi) continue;
            enIyi = d2;
            enYakin = aday;
        }
        return enYakin;
    }

    /// Aktif hattın kablo rotasını ve armatür atamalarını temizle.
    private static void KullanilmayanBuatPanoBaglantisiniSil(
        BasitProjeKat kat, string? buatId, string linyePrefix)
    {
        if (string.IsNullOrWhiteSpace(buatId)) return;
        bool kullaniliyor = linyePrefix == "P"
            ? kat.PrizDevreleri.Any(d => d.BuatId == buatId)
            : kat.Devreler.Any(d => d.BuatId == buatId);
        if (kullaniliyor) return;

        var baglantiIdleri = kat.BuatPanoBaglantilari
            .Where(b => b.BuatId == buatId && b.LinyePrefix == linyePrefix)
            .Select(b => b.Id)
            .ToHashSet();
        kat.Kablolar.RemoveAll(k => baglantiIdleri.Contains(k.DevreId));
        kat.BuatPanoBaglantilari.RemoveAll(b => baglantiIdleri.Contains(b.Id));
    }

    [RelayCommand]
    public void KabloSil()
    {
        var kat = SeciliKat;
        if (kat == null) return;
        if (KilitliyseDur("kablo silmek")) return;
        if (MahalKabloAsamasinda || PanoKabloAsamasinda || PrizAkisiAktif)
        {
            if (SeciliDevreId == null && MahalKabloAsamasinda) SeciliDevreId = _aktifDevre?.Id;
            if (SeciliDevreId == null) { DurumMesaji = "Önce bu adımdaki bir hattı seçin."; return; }
            KabloSeciliDevreyiSil();
            return;
        }

        // Priz paftası: aktif priz devresini temizle
        if (AktifPafta == BpcPaftaTipi.Priz && _aktifPrizDevre != null)
        {
            ElemanSnapshotAl(kat);
            var eskiBuatId = _aktifPrizDevre.BuatId;
            kat.Kablolar.RemoveAll(k => k.DevreId == _aktifPrizDevre.Id);
            _aktifPrizDevre.PrizElemanIds.Clear();
            _aktifPrizDevre.BuatId = null;
            KullanilmayanBuatPanoBaglantisiniSil(kat, eskiBuatId, "P");
            if (SeciliDevreId == _aktifPrizDevre.Id) SeciliDevreId = null;
            GuncelleKabloGorunum();
            KabloModuMesaji = "Priz hattı temizlendi.";
            DurumMesaji = "Aktif priz hattı temizlendi.";
            return;
        }

        // Zayıf akım paftası: aktif sistem devresini sil
        if (BpcSistemKatalogu.Get(AktifPafta).Disiplin == BpcDisiplin.ZayifAkim && _aktifSistemDevre != null)
        {
            ElemanSnapshotAl(kat);
            kat.Kablolar.RemoveAll(k => k.DevreId == _aktifSistemDevre.Id);
            kat.SistemDevreleri.Remove(_aktifSistemDevre);
            if (SeciliDevreId == _aktifSistemDevre.Id) SeciliDevreId = null;
            _aktifSistemDevre = null;
            KabloModuDurumu = 0;
            GuncelleKabloGorunum();
            KabloModuMesaji = KabloModuBaslangicMesaji(AktifPafta);
            DurumMesaji = "Aktif zayıf akım hattı temizlendi.";
            return;
        }

        if (_aktifDevre != null)
        {
            ElemanSnapshotAl(kat);
            var eskiBuatId = _aktifDevre.BuatId;
            // Yalnızca aktif hattı temizle
            kat.Kablolar.RemoveAll(k => k.DevreId == _aktifDevre.Id);
            _aktifDevre.ArmaturElemanIds.Clear();
            _aktifDevre.BuatId = null;
            KullanilmayanBuatPanoBaglantisiniSil(kat, eskiBuatId, "A");
            if (SeciliDevreId == _aktifDevre.Id) SeciliDevreId = null;
            GuncelleKabloGorunum();
            KabloModuMesaji = "Hat temizlendi. Armatür seçebilirsiniz.";
            DurumMesaji = "Aktif hat temizlendi.";
        }
        else if (!string.IsNullOrEmpty(SeciliDevreId))
        {
            ElemanSnapshotAl(kat);
            var sid = SeciliDevreId!;
            kat.Kablolar.RemoveAll(k => k.DevreId == sid);
            // Priz devresi mi?
            var prizDevre = kat.PrizDevreleri.FirstOrDefault(d => d.Id == sid);
            var mekDevre  = kat.MekanikDevreleri.FirstOrDefault(d => d.Id == sid);
            var sisDevre  = kat.SistemDevreleri.FirstOrDefault(d => d.Id == sid);
            if (prizDevre != null)
            {
                var eskiBuatId = prizDevre.BuatId;
                prizDevre.PrizElemanIds.Clear();
                prizDevre.BuatId = null;
                KullanilmayanBuatPanoBaglantisiniSil(kat, eskiBuatId, "P");
                if (_aktifPrizDevre?.Id == sid) { _aktifPrizDevre = null; KabloModuDurumu = 0; KabloModuMesaji = KabloModuAktif ? "Prize tıklayın" : ""; }
            }
            else if (mekDevre != null)
            {
                kat.MekanikDevreleri.Remove(mekDevre);
            }
            else if (sisDevre != null)
            {
                kat.SistemDevreleri.Remove(sisDevre);
                if (_aktifSistemDevre?.Id == sid) { _aktifSistemDevre = null; KabloModuDurumu = 0; KabloModuMesaji = KabloModuAktif ? KabloModuBaslangicMesaji(AktifPafta) : ""; }
            }
            else
            {
                var seciliDevre = kat.Devreler.FirstOrDefault(d => d.Id == sid);
                if (seciliDevre != null)
                {
                    var eskiBuatId = seciliDevre.BuatId;
                    seciliDevre.ArmaturElemanIds.Clear();
                    seciliDevre.BuatId = null;
                    KullanilmayanBuatPanoBaglantisiniSil(kat, eskiBuatId, "A");
                }
                else
                    kat.BuatPanoBaglantilari.RemoveAll(b => b.Id == sid);
            }
            BasitProjeCizKabloServisi.PanoKablolariniNumaralandir(kat);
            SeciliDevreId = null;
            GuncelleKabloGorunum();
            DurumMesaji = "Seçili hat temizlendi.";
        }
        else
        {
            DurumMesaji = "Temizlemek için önce bir hat seçin. Tüm hatları silmek için Tümünü Sil kullanın.";
            if (KabloModuAktif)
                KabloModuMesaji = "Temizlemek için önce bir hat seçin.";
        }
    }

    /// Yalnız aktif paftanın kablo atamalarını ve devrelerini sıfırla.
    [RelayCommand]
    public void KabloTumunuSil()
    {
        var kat = SeciliKat;
        if (kat == null) return;
        if (KilitliyseDur("kabloları silmek")) return;
        ElemanSnapshotAl(kat);

        var silinecekIdler = new HashSet<string>();
        string paftaAdi;
        if (AktifPafta == BpcPaftaTipi.Aydinlatma)
        {
            silinecekIdler.UnionWith(kat.Devreler.Select(d => d.Id));
            var baglantilar = kat.BuatPanoBaglantilari.Where(b => b.LinyePrefix == "A").ToList();
            silinecekIdler.UnionWith(baglantilar.Select(b => b.Id));
            kat.Devreler.Clear();
            kat.VavienGruplari.Clear();
            foreach (var b in baglantilar) kat.BuatPanoBaglantilari.Remove(b);
            _aktifDevre = null;
            paftaAdi = "aydınlatma";
        }
        else if (AktifPafta == BpcPaftaTipi.Priz)
        {
            silinecekIdler.UnionWith(kat.PrizDevreleri.Select(d => d.Id));
            var baglantilar = kat.BuatPanoBaglantilari.Where(b => b.LinyePrefix == "P").ToList();
            silinecekIdler.UnionWith(baglantilar.Select(b => b.Id));
            kat.PrizDevreleri.Clear();
            foreach (var b in baglantilar) kat.BuatPanoBaglantilari.Remove(b);
            _aktifPrizDevre = null;
            paftaAdi = "priz";
        }
        else if (AktifPafta == BpcPaftaTipi.Mekanik)
        {
            silinecekIdler.UnionWith(kat.MekanikDevreleri.Select(d => d.Id));
            kat.MekanikDevreleri.Clear();
            _seciliKlima = null;
            paftaAdi = "mekanik";
        }
        else
        {
            var sistemDevreleri = kat.SistemDevreleri.Where(d => d.Sistem == AktifPafta).ToList();
            silinecekIdler.UnionWith(sistemDevreleri.Select(d => d.Id));
            foreach (var d in sistemDevreleri) kat.SistemDevreleri.Remove(d);
            if (_aktifSistemDevre?.Sistem == AktifPafta) _aktifSistemDevre = null;
            paftaAdi = BpcSistemKatalogu.Get(AktifPafta).Ad;
        }

        kat.Kablolar.RemoveAll(k => silinecekIdler.Contains(k.DevreId));
        _seciliKabloAnahtari = null;
        KabloModuDurumu = 0;
        KabloModuMesaji = KabloModuAktif ? KabloModuBaslangicMesaji(AktifPafta) : "";
        SeciliDevreId = null;
        GuncelleKabloGorunum();
        DurumMesaji = $"Kattaki tüm {paftaAdi} kablo atamaları temizlendi.";
    }

    /// Seçili hattı (SeciliDevreId) tamamen siler: kablo rotaları + devre ataması + vavien grubu.
    [RelayCommand]
    public void KabloSeciliDevreyiSil()
    {
        var kat = SeciliKat;
        if (kat == null || SeciliDevreId == null) return;
        if (!DevreAsamadaDuzenlenebilir(kat, SeciliDevreId)) return;
        if (KilitliyseDur("kablo silmek")) return;
        ElemanSnapshotAl(kat);
        var sid = SeciliDevreId;
        kat.Kablolar.RemoveAll(k => k.DevreId == sid);

        // Priz devresi mi?
        var prizDevre = kat.PrizDevreleri.FirstOrDefault(d => d.Id == sid);
        var mekDevre  = kat.MekanikDevreleri.FirstOrDefault(d => d.Id == sid);
        var sisDevre  = kat.SistemDevreleri.FirstOrDefault(d => d.Id == sid);
        if (prizDevre != null)
        {
            var eskiBuatId = prizDevre.BuatId;
            kat.PrizDevreleri.Remove(prizDevre);
            KullanilmayanBuatPanoBaglantisiniSil(kat, eskiBuatId, "P");
            if (_aktifPrizDevre?.Id == sid) { _aktifPrizDevre = null; KabloModuDurumu = 0; KabloModuMesaji = KabloModuAktif ? "Prize tıklayın" : ""; }
        }
        else if (mekDevre != null)
        {
            kat.MekanikDevreleri.Remove(mekDevre);
            if (_seciliKlima?.Id == mekDevre.KlimaElemanId) { _seciliKlima = null; KabloModuDurumu = 0; KabloModuMesaji = KabloModuAktif ? "Klima ünitesine tıklayın" : ""; }
        }
        else if (sisDevre != null)
        {
            kat.SistemDevreleri.Remove(sisDevre);
            if (_aktifSistemDevre?.Id == sid) { _aktifSistemDevre = null; KabloModuDurumu = 0; KabloModuMesaji = KabloModuAktif ? KabloModuBaslangicMesaji(AktifPafta) : ""; }
        }
        else
        {
            var devre = kat.Devreler.FirstOrDefault(d => d.Id == sid);
            if (devre != null)
            {
                var eskiBuatId = devre.BuatId;
                kat.VavienGruplari.RemoveAll(g => g.DevreId == sid
                    || g.Anahtar1Id == devre.AnahtarElemanId || g.Anahtar2Id == devre.AnahtarElemanId);
                kat.Devreler.Remove(devre);
                KullanilmayanBuatPanoBaglantisiniSil(kat, eskiBuatId, "A");
            }
            else
                kat.BuatPanoBaglantilari.RemoveAll(b => b.Id == sid);
            if (_aktifDevre?.Id == sid) { _aktifDevre = null; KabloModuDurumu = 0; KabloModuMesaji = KabloModuAktif ? "Anahtara tıklayın" : ""; }
        }

        BasitProjeCizKabloServisi.PanoKablolariniNumaralandir(kat, PrizAkisiAktif ? "P" : null);
        SeciliDevreId = null;
        GuncelleKabloGorunum();
        DurumMesaji = "Seçili hat silindi.";
    }

    /// Dünya koordinatında kablo segmentine yakın tıklama; en yakın devreyi seçer.
    public void KabloTiklamaDetect(double worldX, double worldY)
    {
        var kat = SeciliKat;
        if (kat == null) return;
        // Rotalar lokal koordinatta; tıklamayı lokale çevir
        double lx = worldX - kat.OriginOffset.X, ly = worldY - kat.OriginOffset.Y;
        string? nearestId = null;
        // İnce çizgiyi yakalamak zordu (0.5 m); tolerans 0.8 m'ye çıkarıldı —
        // en yakın hat seçildiği için komşu hatlar arasında yanlış seçim üretmez.
        double  minD      = 0.8;
        foreach (var kablo in kat.Kablolar.Where(k => KabloAktifPaftadaMi(kat, k) && DevreAsamadaDuzenlenebilir(kat, k.DevreId)))
        {
            for (int i = 0; i < kablo.Rota.Count - 1; i++)
            {
                var a = kablo.Rota[i];
                var b = kablo.Rota[i + 1];
                double d = DistPointToSegment(lx, ly, a.X, a.Y, b.X, b.Y);
                if (d < minD) { minD = d; nearestId = kablo.DevreId; }
            }
        }
        if (nearestId != SeciliDevreId)
        {
            SeciliDevreId = nearestId;
            GuncelleKabloGorunum();
            if (nearestId != null)
                DurumMesaji = "Hat seçildi — Delete siler, \"Seçili Hattı Sil\" de kullanılabilir.";
        }
    }

    private static double DistPointToSegment(double px, double py,
        double ax, double ay, double bx, double by)
    {
        double dx = bx - ax, dy = by - ay;
        double lenSq = dx * dx + dy * dy;
        if (lenSq < 1e-12)
            return System.Math.Sqrt((px - ax) * (px - ax) + (py - ay) * (py - ay));
        double t = System.Math.Clamp(((px - ax) * dx + (py - ay) * dy) / lenSq, 0, 1);
        double projX = ax + t * dx, projY = ay + t * dy;
        return System.Math.Sqrt((px - projX) * (px - projX) + (py - projY) * (py - projY));
    }

    /// Viewport tıklaması → kablo modu içinde switch veya armatür seçimi.
    /// Sarmalayıcı: her tıklamadan sonra adım göstergesi güncellenir.
    public bool KabloModuTiklama(double worldX, double worldY)
    {
        var sonuc = KabloModuTiklamaIc(worldX, worldY);
        if (sonuc) AdimGostergeGuncelle();
        return sonuc;
    }

    /// Anahtar seçiliyken odasındaki boş armatürleri aday olarak vurgular.
    private void AdayArmaturleriVurgula(BasitProjeKat kat, BasitProjeEleman? anahtar)
    {
        if (anahtar == null || string.IsNullOrEmpty(anahtar.OdaId))
        {
            AdayElemanPosList = null;
            return;
        }
        double ox = kat.OriginOffset.X, oy = kat.OriginOffset.Y;
        var atanan = kat.Devreler.SelectMany(d => d.ArmaturElemanIds).ToHashSet();
        var adaylar = kat.Elemanlar
            .Where(e => e.OdaId == anahtar.OdaId &&
                        BasitProjeCizKabloServisi.ElemanArmaturMi(e.Tip) &&
                        !e.SensorKontrollu && !atanan.Contains(e.Id))
            .Select(e => new Point3D(e.X + ox, e.Y + oy, 0))
            .ToList();
        AdayElemanPosList = adaylar.Count > 0 ? adaylar : null;
    }

    /// "Odadakileri Ata" butonu görünürlüğü (aydınlatma manuel akışı, hat açıkken).
    public bool GosterOdadakileriAta =>
        KabloModuAktif && AktifPafta == BpcPaftaTipi.Aydinlatma &&
        KabloModuDurumu == 1 && _aktifDevre != null;

    /// Seçili anahtarın odasındaki tüm boş armatürleri tek tıkla atar
    /// (Mahal Modu'nun manuel akışa gömülmüş hâli).
    [RelayCommand]
    public void OdadakiArmaturleriAta()
    {
        var kat = SeciliKat;
        if (kat == null || _aktifDevre == null || _seciliKabloAnahtari == null) return;
        if (KilitliyseDur("odadaki armatürleri atamak")) return;

        ElemanSnapshotAl(kat);
        BasitProjeCizKabloServisi.MahaldekiBosArmaturleriAnahtaraBagla(kat, _aktifDevre, _seciliKabloAnahtari.Id);

        // Atama kopya anahtarlara taşmış olabilir → kat aydınlatma rotalarını topluca çiz.
        var wallSlots = BasitProjeCizKabloServisi.HesaplaWallSlotlari(kat);
        foreach (var d in kat.Devreler)
        {
            int slot = wallSlots.TryGetValue(d.Id, out var s) ? s : 0;
            BasitProjeCizKabloServisi.RotaHesapla(kat, d, KabloRotaTipi, slot);
        }
        SeciliDevreId = _aktifDevre.Id;
        GuncelleKabloGorunum();
        AdayArmaturleriVurgula(kat, _seciliKabloAnahtari);
        AdimGostergeGuncelle();
        KabloModuMesaji = $"Odadaki boş armatürler atandı ({_aktifDevre.ArmaturElemanIds.Count} armatür bu devrede).";
        DurumMesaji = "Odadaki boş armatürler anahtara bağlandı.";
    }

    private bool KabloModuTiklamaIc(double worldX, double worldY)
    {
        if (!KabloModuAktif) return false;
        var kat = SeciliKat;
        if (kat == null) return true;
        if (KilitliyseDur("kablolamak")) return true;

        double ox = kat.OriginOffset.X, oy = kat.OriginOffset.Y;
        double lx = worldX - ox, ly = worldY - oy;

        if (PrizAkisiAktif)
        {
            if (PrizMahalKablosuAdimi || PrizPanoKablosuAdimi) PrizAsamasiNoktaSec(kat, lx, ly);
            return true;
        }

        // Mekanik paftası: direkt Klima -> Pano
        if (AktifPafta == BpcPaftaTipi.Mekanik)
            return MekanikKabloModuTiklama(kat, lx, ly, worldX, worldY);

        // Zayıf akım paftaları
        var sistemTanim = BpcSistemKatalogu.Get(AktifPafta);
        if (sistemTanim.Disiplin == BpcDisiplin.ZayifAkim)
        {
            return sistemTanim.Topoloji switch
            {
                // BuatDaisy ve Loop aynı akış: cihaz topla → merkeze tıkla
                // (rota farkı SistemRotaHesapla'da topolojiye göre yapılır)
                BpcSistemTopoloji.BuatDaisy or BpcSistemTopoloji.Loop
                    => ZayifAkimKabloModuTiklama(kat, sistemTanim, lx, ly, worldX, worldY),
                BpcSistemTopoloji.YildizMerkez
                    => ZayifAkimYildizKabloModuTiklama(kat, sistemTanim, lx, ly, worldX, worldY),
                _ => YangiKabloDesteksiz()
            };
        }

        // Priz paftası: state 3/4 (Buat→Pano) ortak; 0/1 priz özel
        if (AktifPafta == BpcPaftaTipi.Priz && KabloModuDurumu < 3)
            return PrizKabloModuTiklama(kat, lx, ly, worldX, worldY);

        // Duruma göre tip tercihi: anahtar/sensör seçiminde önce switch ara
        bool anahtarBekle = KabloModuDurumu == 0 || KabloModuDurumu == 2;
        var eleman = BulEnYakinEleman(kat, lx, ly, tolerans: 1.5, tercihAnahtar: anahtarBekle);
        if (eleman == null)
        {
            if (_aydinlatmaMahalModu)
            {
                string? odaId = kat.Odalar.FirstOrDefault(o => o.Poligon.Count >= 3 &&
                    Kst.Acoustics.Core.Common.Geometry.PoligonYardimci.IsPointInside(
                        new Kst.Acoustics.Core.Common.Geometry.Point2D(lx, ly), o.Poligon))?.Id;
                if (!string.IsNullOrEmpty(odaId))
                    MahalModuOdayaBagla(kat, odaId);
                else
                    KabloModuMesaji = "Oda tespit edilemedi. Oda içindeki bir elemana tıklayın.";
                return true;
            }
            KabloTiklamaDetect(worldX, worldY); // Eleman yok → kablo çizgisine tıklandıysa seç
            return true;
        }

        // Gerçek mutasyon olduğunda (atama/bağlama) tek sefer undo snapshot al.
        bool snapAlindi = false;
        void Snap() { if (!snapAlindi) { ElemanSnapshotAl(kat); snapAlindi = true; } }

        if (MahalKabloAsamasinda && (eleman.Tip == BpcElemanTipi.KatPanosu ||
            (KabloModuDurumu == 0 && eleman.Tip == BpcElemanTipi.Buat)))
        {
            KabloModuMesaji = "Mahal içi hat için önce anahtarı, ardından armatürleri ve buatı seçin.";
            return true;
        }
        if (PanoKabloAsamasinda && KabloModuDurumu < 3)
        {
            KabloModuDurumu = 3;
            _buatPanoSeciliBuat = null;
        }
        if (MahalKabloAsamasinda && KabloModuDurumu == 1 && _seciliKabloAnahtari != null &&
            (BasitProjeCizKabloServisi.ElemanArmaturMi(eleman.Tip) || eleman.Tip == BpcElemanTipi.Buat) &&
            eleman.OdaId != _seciliKabloAnahtari.OdaId)
        {
            KabloModuMesaji = "Aynı mahaldeki armatürü veya buatı seçin.";
            return true;
        }

        if (KabloModuDurumu == 3) // Buat bekleniyor
        {
            if (eleman.Tip != BpcElemanTipi.Buat || (PanoKabloAsamasinda && eleman.PaftaTipi != BpcPaftaTipi.Aydinlatma))
            {
                KabloModuMesaji = "Lütfen panoya bağlanacak buata tıklayın.";
                return true;
            }
            _buatPanoSeciliBuat = eleman;
            SeciliEleman = eleman;
            KabloModuDurumu = 4;
            KabloModuMesaji = $"\"{eleman.Ad}\" seçildi. Şimdi kat panosuna tıklayın.";
            DurumMesaji = "Buat seçildi. Bağlanacak kat panosuna tıklayın.";
            return true;
        }

        if (KabloModuDurumu == 4) // KatPanosu bekleniyor
        {
            if (eleman.Tip != BpcElemanTipi.KatPanosu)
            {
                KabloModuMesaji = "Lütfen kat panosuna tıklayın.";
                return true;
            }
            if (_buatPanoSeciliBuat == null) { KabloModuDurumu = 3; KabloModuMesaji = "Önce buata tıklayın."; return true; }
            if (PanoKabloAsamasinda &&
                BasitProjeCizKabloServisi.MahaleAtananPano(_buatPanoSeciliBuat.OdaId, kat.Elemanlar)?.Id != eleman.Id)
            {
                KabloModuMesaji = "Mahal için atanmış panoyu seçin. Atamayı değiştirmek için pano adımında mahale sağ tıklayın.";
                return true;
            }
            Snap();
            BuatPanosuBagla(kat, _buatPanoSeciliBuat, eleman);
            _buatPanoSeciliBuat = null;
            KabloModuDurumu = 3; // Zincir: başka buat için hazır
            return true;
        }

        if (KabloModuDurumu == 0) // Anahtar seçimi
        {
            // Mahal Modu: tıklanan elemandan oda tespit et, o odayı otomatik bağla
            if (_aydinlatmaMahalModu)
            {
                string? odaId = eleman.OdaId
                    ?? kat.Odalar.FirstOrDefault(o => o.Poligon.Count >= 3 &&
                        Kst.Acoustics.Core.Common.Geometry.PoligonYardimci.IsPointInside(
                            new Kst.Acoustics.Core.Common.Geometry.Point2D(lx, ly), o.Poligon))?.Id;
                if (!string.IsNullOrEmpty(odaId))
                    MahalModuOdayaBagla(kat, odaId);
                else
                    KabloModuMesaji = "Oda tespit edilemedi. Oda içindeki bir elemana tıklayın.";
                return true;
            }

            // Akıllı tıklama: buata tıklandıysa mod düğmesine gerek kalmadan
            // Buat→Pano akışı başlar (niyeti eleman tipi belirler).
            if (eleman.Tip == BpcElemanTipi.Buat)
            {
                _buatPanoSeciliBuat = eleman;
                SeciliEleman = eleman;
                KabloModuDurumu = 4;
                KabloModuMesaji = $"\"{eleman.Ad}\" seçildi. Şimdi kat panosuna tıklayın (Esc = iptal).";
                return true;
            }
            if (eleman.Tip == BpcElemanTipi.KatPanosu)
            {
                KabloModuMesaji = "Önce buata tıklayın — pano bağlantısı buattan başlar.";
                return true;
            }
            if (!BasitProjeCizKabloServisi.ElemanAnahtarMi(eleman.Tip)) { KabloModuMesaji = "Anahtara veya sensöre tıklayın."; return true; }

            _seciliKabloAnahtari = eleman;
            KomutatorDevreNo = 1;
            OnPropertyChanged(nameof(GosterUcuncuDevre));
            OnPropertyChanged(nameof(GosterKomutatorSecim));

            if (eleman.Tip == BpcElemanTipi.Vavien)
            {
                var grup = BasitProjeCizKabloServisi.BulVavienGrup(kat, eleman.Id);
                if (grup == null)
                {
                    KabloModuDurumu = 2;
                    KabloModuMesaji = "İkinci Vavien anahtara tıklayın (Esc = iptal)";
                    return true;
                }
                // Grup zaten var, devre hazır
                if (!kat.Devreler.Any(d => d.Id == grup.DevreId)) Snap();
                _aktifDevre = kat.Devreler.FirstOrDefault(d => d.Id == grup.DevreId)
                              ?? BasitProjeCizKabloServisi.EnsureDevre(kat, eleman.Id);
            }
            else
            {
                if (!kat.Devreler.Any(d => d.AnahtarElemanId == eleman.Id && d.DevreNo == KomutatorDevreNo)) Snap();
                _aktifDevre = BasitProjeCizKabloServisi.EnsureDevre(kat, eleman.Id, KomutatorDevreNo);
            }

            KabloModuDurumu = 1;
            // Odadaki boş armatürler aday olarak vurgulanır; devrede içerik varsa hat çizilir.
            AdayArmaturleriVurgula(kat, eleman);
            if (_aktifDevre.ArmaturElemanIds.Count > 0) AktifDevreRotasiniCiz(kat);
            KabloModuMesaji = $"\"{eleman.Ad}\" seçildi. Armatür ve buata tıklayın (Enter/sağ tık = hattı bitir).";
        }
        else if (KabloModuDurumu == 2) // İkinci Vavien
        {
            if (eleman.Tip != BpcElemanTipi.Vavien) { KabloModuMesaji = "Lütfen bir Vavien anahtarı seçin."; return true; }
            if (eleman.Id == _seciliKabloAnahtari!.Id) { KabloModuMesaji = "Aynı anahtar seçilemez."; return true; }

            Snap();
            var grup = BasitProjeCizKabloServisi.VavienGrupOlustur(kat, _seciliKabloAnahtari!.Id, eleman.Id);
            _aktifDevre = kat.Devreler.FirstOrDefault(d => d.Id == grup.DevreId)
                          ?? BasitProjeCizKabloServisi.EnsureDevre(kat, _seciliKabloAnahtari.Id);

            KabloModuDurumu = 1;
            AdayArmaturleriVurgula(kat, _seciliKabloAnahtari);
            AktifDevreRotasiniCiz(kat); // seyahat kablosu anında görünür
            KabloModuMesaji = "Vavien grubu oluşturuldu. Armatürlere tıklayın.";
        }
        else if (KabloModuDurumu == 1) // Armatür / buat atama
        {
            if (eleman.Tip == BpcElemanTipi.Buat)
            {
                if (_aktifDevre != null)
                {
                    Snap();
                    BasitProjeCizKabloServisi.ButaAta(kat, _aktifDevre, eleman.Id);
                    AktifDevreRotasiniCiz(kat);
                    KabloModuMesaji = "Buat atandı — hat çizildi. Armatür eklemeye devam edin veya Enter ile bitirin.";
                }
                return true;
            }

            // Akıllı tıklama: hattın buatı atanmışken panoya tıklanırsa buat→pano bağlanır.
            if (eleman.Tip == BpcElemanTipi.KatPanosu)
            {
                if (_aktifDevre != null && !string.IsNullOrEmpty(_aktifDevre.BuatId))
                {
                    var devreBuati = kat.Elemanlar.FirstOrDefault(e => e.Id == _aktifDevre.BuatId);
                    if (devreBuati != null)
                    {
                        Snap();
                        BuatPanosuBagla(kat, devreBuati, eleman);
                        return true;
                    }
                }
                KabloModuMesaji = "Panoya bağlamak için önce devreye buat atayın (veya buata tıklayıp akışı başlatın).";
                return true;
            }

            if (!BasitProjeCizKabloServisi.ElemanArmaturMi(eleman.Tip))
            {
                // Başka bir anahtar seçildi → o anahtara geçiş
                if (BasitProjeCizKabloServisi.ElemanAnahtarMi(eleman.Tip))
                {
                    _seciliKabloAnahtari = eleman;
                    KomutatorDevreNo = 1;
                    OnPropertyChanged(nameof(GosterUcuncuDevre));
                    OnPropertyChanged(nameof(GosterKomutatorSecim));
                    if (!kat.Devreler.Any(d => d.AnahtarElemanId == eleman.Id && d.DevreNo == KomutatorDevreNo)) Snap();
                    _aktifDevre = BasitProjeCizKabloServisi.EnsureDevre(kat, eleman.Id, KomutatorDevreNo);
                    AdayArmaturleriVurgula(kat, eleman);
                    if (_aktifDevre.ArmaturElemanIds.Count > 0) AktifDevreRotasiniCiz(kat);
                    KabloModuMesaji = $"\"{eleman.Ad}\" seçildi. Armatür ve buata tıklayın.";
                }
                return true;
            }
            if (_aktifDevre == null) return true;

            // İkinci tık = devreden çıkar (yanlış tıklama tek tıkla onarılır).
            if (_aktifDevre.ArmaturElemanIds.Contains(eleman.Id))
            {
                Snap();
                _aktifDevre.ArmaturElemanIds.Remove(eleman.Id);
                AktifDevreRotasiniCiz(kat);
                AdayArmaturleriVurgula(kat, _seciliKabloAnahtari);
                KabloModuMesaji = $"Armatür çıkarıldı ({_aktifDevre.ArmaturElemanIds.Count} kaldı). Tekrar tıklamak geri ekler.";
                return true;
            }

            Snap();
            BasitProjeCizKabloServisi.ArmaturAtama(kat, _aktifDevre, eleman.Id);
            AktifDevreRotasiniCiz(kat); // canlı çizim: tık → kablo anında görünür
            AdayArmaturleriVurgula(kat, _seciliKabloAnahtari);
            int sayi = _aktifDevre.ArmaturElemanIds.Count;
            KabloModuMesaji = $"{sayi} armatür atandı. Devam et, buata tıkla veya Enter ile bitir.";
        }
        return true;
    }

    // ─── Priz kablo modu ─────────────────────────────────────────────────

    private bool PrizKabloModuTiklama(BasitProjeKat kat, double lx, double ly,
                                       double worldX, double worldY)
    {
        var eleman = BulEnYakinEleman(kat, lx, ly, tolerans: 1.5, tercihAnahtar: false);
        if (eleman == null)
        {
            if (_prizMahalModu && KabloModuDurumu == 0)
            {
                string? odaId = kat.Odalar.FirstOrDefault(o => o.Poligon.Count >= 3 &&
                    Kst.Acoustics.Core.Common.Geometry.PoligonYardimci.IsPointInside(
                        new Kst.Acoustics.Core.Common.Geometry.Point2D(lx, ly), o.Poligon))?.Id;
                if (!string.IsNullOrEmpty(odaId))
                    PrizMahalModuOdayaBagla(kat, odaId);
                else
                    KabloModuMesaji = "Oda tespit edilemedi. Priz elemanına tıklayın.";
                return true;
            }
            KabloTiklamaDetect(worldX, worldY);
            return true;
        }

        bool snapAlindi = false;
        void Snap() { if (!snapAlindi) { ElemanSnapshotAl(kat); snapAlindi = true; } }

        // Priz Mahal Modu: tıklanan elemandan oda tespit et, o odayı otomatik bağla
        if (_prizMahalModu && KabloModuDurumu == 0)
        {
            string? odaId = eleman.OdaId
                ?? kat.Odalar.FirstOrDefault(o => o.Poligon.Count >= 3 &&
                    Kst.Acoustics.Core.Common.Geometry.PoligonYardimci.IsPointInside(
                        new Kst.Acoustics.Core.Common.Geometry.Point2D(lx, ly), o.Poligon))?.Id;
            if (!string.IsNullOrEmpty(odaId))
            {
                Snap();
                PrizMahalModuOdayaBagla(kat, odaId);
            }
            else KabloModuMesaji = "Oda tespit edilemedi. Priz elemanına tıklayın.";
            return true;
        }

        if (KabloModuDurumu == 0) // Priz seçimi
        {
            if (!BasitProjeCizKabloServisi.ElemanPrizMi(eleman.Tip))
            {
                KabloModuMesaji = "Bir priz elemanına tıklayın.";
                return true;
            }
            Snap();
            _aktifPrizDevre = EnsurePrizDevre(kat, eleman.Id);
            SeciliEleman = eleman;
            KabloModuDurumu = 1;
            KabloModuMesaji = $"\"{eleman.Ad}\" seçildi. Daha fazla priz ekleyin veya buata tıklayın.";
            return true;
        }

        if (KabloModuDurumu == 1) // Priz ekleme veya buat atama
        {
            if (eleman.Tip == BpcElemanTipi.Buat)
            {
                if (_aktifPrizDevre != null)
                {
                    Snap();
                    _aktifPrizDevre.BuatId = eleman.Id;
                    AktifPrizRotasiniCiz(kat); // canlı çizim — ayrıca "Hesapla" gerekmez
                    KabloModuMesaji = "Buat atandı — hat çizildi. Priz eklemeye devam edin veya Enter ile bitirin.";
                }
                return true;
            }
            if (BasitProjeCizKabloServisi.ElemanPrizMi(eleman.Tip))
            {
                if (_aktifPrizDevre == null) return true;

                // İkinci tık = devreden çıkar (yanlış tıklama tek tıkla onarılır).
                if (_aktifPrizDevre.PrizElemanIds.Contains(eleman.Id))
                {
                    Snap();
                    _aktifPrizDevre.PrizElemanIds.Remove(eleman.Id);
                    AktifPrizRotasiniCiz(kat);
                    KabloModuMesaji = $"Priz çıkarıldı ({_aktifPrizDevre.PrizElemanIds.Count} kaldı). Tekrar tıklamak geri ekler.";
                    return true;
                }

                Snap();
                // Priz başka bir devreye aitse önce oradan kopart
                foreach (var d in kat.PrizDevreleri)
                    d.PrizElemanIds.Remove(eleman.Id);

                _aktifPrizDevre.PrizElemanIds.Add(eleman.Id);
                AktifPrizRotasiniCiz(kat); // buat atanmışsa hat anında güncellenir
                KabloModuMesaji = $"{_aktifPrizDevre.PrizElemanIds.Count} priz seçildi. Buata tıkla veya Enter ile bitir.";
                return true;
            }
            KabloModuMesaji = "Priz veya buata tıklayın.";
            return true;
        }

        return true;
    }

    private bool MekanikKabloModuTiklama(BasitProjeKat kat, double lx, double ly,
                                         double worldX, double worldY)
    {
        var eleman = BulEnYakinEleman(kat, lx, ly, tolerans: 1.5, tercihAnahtar: false);
        if (eleman == null)
        {
            KabloTiklamaDetect(worldX, worldY);
            return true;
        }

        if (KabloModuDurumu == 0) // Klima seçimi
        {
            if (eleman.Tip != BpcElemanTipi.KlimaIcUnite && eleman.Tip != BpcElemanTipi.KlimaDisUnite)
            {
                KabloModuMesaji = "Klima iç veya dış ünitesine tıklayın.";
                return true;
            }
            _seciliKlima = eleman;
            SeciliEleman = eleman;
            KabloModuDurumu = 1;
            KabloModuMesaji = $"\"{eleman.Ad}\" seçildi. Kat panosuna tıklayarak doğrudan bağlayın.";
            return true;
        }

        if (KabloModuDurumu == 1) // Pano seçimi
        {
            if (eleman.Tip != BpcElemanTipi.KatPanosu)
            {
                KabloModuMesaji = "Lütfen doğrudan bağlanacak kat panosuna tıklayın.";
                return true;
            }
            if (_seciliKlima == null) { KabloModuDurumu = 0; KabloModuMesaji = "Önce klimaya tıklayın."; return true; }

            ElemanSnapshotAl(kat);
            if (kat.MekanikDevreleri == null) kat.MekanikDevreleri = new();
            kat.MekanikDevreleri.RemoveAll(d => d.KlimaElemanId == _seciliKlima.Id);

            var devre = new BpcMekanikDevre
            {
                KlimaElemanId = _seciliKlima.Id,
                PanoId = eleman.Id
            };
            kat.MekanikDevreleri.Add(devre);

            BasitProjeCizKabloServisi.MekanikRotalariniHesapla(kat);
            GuncelleKabloGorunum();
            Guncelle3DGorunum();

            KabloModuDurumu = 0;
            KabloModuMesaji = "Klima ünitesine tıklayın";
            DurumMesaji = $"{_seciliKlima.Ad} doğrudan {eleman.Ad} panosuna bağlandı.";
            _seciliKlima = null;
            return true;
        }

        return true;
    }

    private static BpcPrizDevre EnsurePrizDevre(BasitProjeKat kat, string prizId)
    {
        var devre = kat.PrizDevreleri.FirstOrDefault(d => d.PrizElemanIds.Contains(prizId));
        if (devre == null)
        {
            devre = new BpcPrizDevre();
            devre.PrizElemanIds.Add(prizId);
            kat.PrizDevreleri.Add(devre);
        }
        return devre;
    }

    // ─── Zayıf akım kablo modu (Data/TV/Telefon = BuatDaisy, Yangın = Loop) ──
    private bool ZayifAkimKabloModuTiklama(BasitProjeKat kat, BpcSistemTanim tanim,
        double lx, double ly, double worldX, double worldY)
    {
        var eleman = BulEnYakinEleman(kat, lx, ly, tolerans: 1.5, tercihAnahtar: false);
        if (eleman == null)
        {
            KabloTiklamaDetect(worldX, worldY);
            return true;
        }

        bool snapAlindi = false;
        void Snap() { if (!snapAlindi) { ElemanSnapshotAl(kat); snapAlindi = true; } }

        bool cihazMi = tanim.CihazTipleri.Contains(eleman.Tip);
        bool araToplayiciMi = tanim.AraToplayiciTipleri.Contains(eleman.Tip);
        bool merkezMi = tanim.MerkezMi(eleman.Tip);
        bool loop = tanim.Topoloji == BpcSistemTopoloji.Loop;
        string merkezAd = ZayifElemanAdi(tanim.MerkezTipi);
        string sonrakiYonerge = tanim.AraToplayiciTipleri.Count > 0
            ? $"Cihaz ekleyin, ara toplayıcıya veya {merkezAd}'na tıklayın."
            : $"Cihaz ekleyin veya {merkezAd}'na tıklayın.";

        if (KabloModuDurumu == 0) // İlk cihaz seçimi
        {
            if (!cihazMi)
            {
                KabloModuMesaji = $"{tanim.Ad} cihazına tıklayın.";
                return true;
            }
            Snap();
            _aktifSistemDevre = EnsureSistemDevre(kat, tanim.Sistem, eleman.Id);
            SeciliEleman = eleman;
            KabloModuDurumu = 1;
            KabloModuMesaji = $"\"{eleman.Ad}\" seçildi. {sonrakiYonerge}";
            return true;
        }

        if (KabloModuDurumu == 1)
        {
            if (_aktifSistemDevre == null) { KabloModuDurumu = 0; return true; }

            if (merkezMi)
            {
                Snap();
                _aktifSistemDevre.MerkezId = eleman.Id;
                BasitProjeCizKabloServisi.SistemRotaHesapla(kat, _aktifSistemDevre);
                BasitProjeCizKabloServisi.SistemKablolariniNumaralandir(kat);
                SeciliDevreId = _aktifSistemDevre.Id;
                GuncelleKabloGorunum();
                // Hat tamam → yeni hat için sıfırla
                _aktifSistemDevre = null;
                KabloModuDurumu = 0;
                KabloModuMesaji = loop
                    ? $"Halka {merkezAd}'nda kapatıldı. Yeni hat için cihaza tıklayın."
                    : $"\"{eleman.Ad}\" merkezine bağlandı. Yeni hat için cihaza tıklayın.";
                DurumMesaji = $"{tanim.Ad} hattı çizildi.";
                return true;
            }
            if (araToplayiciMi)
            {
                Snap();
                _aktifSistemDevre.AraToplayiciId = eleman.Id;
                if (!string.IsNullOrEmpty(_aktifSistemDevre.MerkezId))
                {
                    BasitProjeCizKabloServisi.SistemRotaHesapla(kat, _aktifSistemDevre);
                    GuncelleKabloGorunum();
                }
                KabloModuMesaji = "Ara toplayıcı atandı. Merkeze tıklayın.";
                return true;
            }
            if (cihazMi)
            {
                if (!_aktifSistemDevre.CihazIds.Contains(eleman.Id))
                {
                    Snap();
                    foreach (var d in kat.SistemDevreleri.Where(d => d.Sistem == tanim.Sistem))
                        d.CihazIds.Remove(eleman.Id);
                    _aktifSistemDevre.CihazIds.Add(eleman.Id);
                }
                if (!string.IsNullOrEmpty(_aktifSistemDevre.MerkezId))
                {
                    BasitProjeCizKabloServisi.SistemRotaHesapla(kat, _aktifSistemDevre);
                    GuncelleKabloGorunum();
                }
                KabloModuMesaji = $"{_aktifSistemDevre.CihazIds.Count} cihaz seçildi.";
                return true;
            }
            KabloModuMesaji = sonrakiYonerge;
            return true;
        }

        return true;
    }

    private BpcSistemDevre EnsureSistemDevre(BasitProjeKat kat, BpcPaftaTipi sistem, string cihazId)
    {
        var devre = kat.SistemDevreleri.FirstOrDefault(
            d => d.Sistem == sistem && d.CihazIds.Contains(cihazId));
        if (devre == null)
        {
            devre = new BpcSistemDevre { Sistem = sistem };
            devre.CihazIds.Add(cihazId);
            kat.SistemDevreleri.Add(devre);
        }
        return devre;
    }

    // ─── Zayıf akım kablo modu (CCTV/Anons — YildizMerkez) ───────────────
    // Her cihaz doğrudan merkeze bağlanır; her bağlantı kendi hattıdır (C1, S1…).
    private bool ZayifAkimYildizKabloModuTiklama(BasitProjeKat kat, BpcSistemTanim tanim,
        double lx, double ly, double worldX, double worldY)
    {
        var eleman = BulEnYakinEleman(kat, lx, ly, tolerans: 1.5, tercihAnahtar: false);
        if (eleman == null)
        {
            KabloTiklamaDetect(worldX, worldY);
            return true;
        }

        bool snapAlindi = false;
        void Snap() { if (!snapAlindi) { ElemanSnapshotAl(kat); snapAlindi = true; } }

        bool cihazMi = tanim.CihazTipleri.Contains(eleman.Tip);
        bool merkezMi = tanim.MerkezMi(eleman.Tip);

        if (KabloModuDurumu == 0) // Cihaz seçimi
        {
            if (!cihazMi)
            {
                KabloModuMesaji = $"{tanim.Ad} cihazına tıklayın.";
                return true;
            }
            Snap();
            // Cihaz başka bir hatta varsa önce oradan kopart
            foreach (var d in kat.SistemDevreleri.Where(d => d.Sistem == tanim.Sistem).ToList())
            {
                if (d.CihazIds.Remove(eleman.Id))
                {
                    kat.Kablolar.RemoveAll(k => k.DevreId == d.Id);
                    if (d.CihazIds.Count == 0) kat.SistemDevreleri.Remove(d);
                }
            }
            _aktifSistemDevre = new BpcSistemDevre { Sistem = tanim.Sistem };
            _aktifSistemDevre.CihazIds.Add(eleman.Id);
            kat.SistemDevreleri.Add(_aktifSistemDevre);
            SeciliEleman = eleman;
            KabloModuDurumu = 1;
            KabloModuMesaji = $"\"{eleman.Ad}\" seçildi. {MerkezAdlari(tanim)}'na tıklayın.";
            return true;
        }

        if (KabloModuDurumu == 1) // Merkez seçimi
        {
            if (!merkezMi)
            {
                KabloModuMesaji = $"Lütfen {MerkezAdlari(tanim)}'na tıklayın.";
                return true;
            }
            if (_aktifSistemDevre == null) { KabloModuDurumu = 0; return true; }

            Snap();
            _aktifSistemDevre.MerkezId = eleman.Id;
            BasitProjeCizKabloServisi.SistemRotaHesapla(kat, _aktifSistemDevre);
            BasitProjeCizKabloServisi.SistemKablolariniNumaralandir(kat);
            SeciliDevreId = _aktifSistemDevre.Id;
            GuncelleKabloGorunum();

            KabloModuDurumu = 0;
            _aktifSistemDevre = null;
            KabloModuMesaji = $"Hat çizildi. Yeni hat için {tanim.Ad} cihazına tıklayın.";
            DurumMesaji = $"{tanim.Ad} hattı çizildi.";
            return true;
        }

        return true;
    }

    private bool YangiKabloDesteksiz()
    {
        KabloModuMesaji = "Bu sistem için kablolama henüz desteklenmiyor.";
        return true;
    }

    /// tercihAnahtar=true → tolerans içinde anahtar/sensör tipi varsa önce onu döner.
    private static BasitProjeEleman? BulEnYakinEleman(BasitProjeKat kat,
        double lx, double ly, double tolerans, bool tercihAnahtar = false)
    {
        BasitProjeEleman? enYakin        = null;
        BasitProjeEleman? enYakinAnahtar = null;
        double minD  = tolerans;
        double minDA = tolerans;
        foreach (var e in kat.Elemanlar)
        {
            double d = System.Math.Sqrt((e.X - lx) * (e.X - lx) + (e.Y - ly) * (e.Y - ly));
            if (d < minD)  { minD  = d; enYakin        = e; }
            if (BasitProjeCizKabloServisi.ElemanAnahtarMi(e.Tip) && d < minDA)
                           { minDA = d; enYakinAnahtar = e; }
        }
        return tercihAnahtar ? (enYakinAnahtar ?? enYakin) : enYakin;
    }

    public void GuncelleKabloGorunum()
    {
        if (BostakileriGosterAktif) GuncelleElemanVurgu();
        var kat = SeciliKat;
        if (kat != null)
        {
            BasitProjeCizKabloServisi.PanoKablolariniNumaralandir(kat);
            // Mekanik rotaları yalnızca mekanik paftadayken yeniden hesapla
            if (AktifPafta == BpcPaftaTipi.Mekanik)
                BasitProjeCizKabloServisi.MekanikRotalariniHesapla(kat);
            else
                BasitProjeCizKabloServisi.MekanikKablolariniNumaralandir(kat);
            // Zayıf akım rotaları yalnızca o paftadayken yeniden hesapla
            if (BpcSistemKatalogu.Get(AktifPafta).Disiplin == BpcDisiplin.ZayifAkim)
                BasitProjeCizKabloServisi.TumSistemRotalariniHesapla(kat);
            else
                BasitProjeCizKabloServisi.SistemKablolariniNumaralandir(kat);
        }
        GuncellePanoIcerik(); // önce listeyi kur (seçimi korur, render'ı bastırır)
        RenderKablolar();     // sonra güncel seçimle tek sefer çiz
        GuncelleDevreOzetleri();
    }

    // ── Devre listesi paneli (K11) ───────────────────────────────────────────
    // Kat devrelerinin görünür/yönetilebilir listesi: satıra tıkla → canvas'ta
    // vurgula; ✕ → hattı sil. İnce kablo çizgisi avlamaya alternatif.

    public sealed record DevreOzetSatiri(
        string DevreId, string Ikon, string Baslik, string Detay, bool Secili);

    public System.Collections.ObjectModel.ObservableCollection<DevreOzetSatiri> DevreOzetleri { get; } = new();

    private void GuncelleDevreOzetleri()
    {
        DevreOzetleri.Clear();
        var kat = SeciliKat;
        if (kat == null) return;

        var elemanAd = kat.Elemanlar.ToDictionary(e => e.Id, e => string.IsNullOrWhiteSpace(e.Ad) ? e.Tip.ToString() : e.Ad);
        string Ad(string? id) => id != null && elemanAd.TryGetValue(id, out var a) ? a : "—";

        double DevreUzunluk(string devreId) => kat.Kablolar
            .Where(k => k.DevreId == devreId)
            .Sum(k =>
            {
                double top = 0;
                for (int i = 0; i < k.Rota.Count - 1; i++)
                {
                    double dx = k.Rota[i + 1].X - k.Rota[i].X, dy = k.Rota[i + 1].Y - k.Rota[i].Y;
                    top += Math.Sqrt(dx * dx + dy * dy);
                }
                return top;
            });

        foreach (var d in kat.Devreler)
        {
            string buat = string.IsNullOrEmpty(d.BuatId) ? "buat yok" : $"buat: {Ad(d.BuatId)}";
            if (!DevreAsamadaDuzenlenebilir(kat, d.Id)) continue;
            DevreOzetleri.Add(new DevreOzetSatiri(
                d.Id, "💡",
                $"{Ad(d.AnahtarElemanId)}{(d.DevreNo > 1 ? $" · D{d.DevreNo}" : "")}",
                $"{d.ArmaturElemanIds.Count} armatür · {buat} · {DevreUzunluk(d.Id):0.#} m",
                d.Id == SeciliDevreId));
        }
        foreach (var d in kat.PrizDevreleri)
        {
            string buat = string.IsNullOrEmpty(d.BuatId) ? "buat yok" : $"buat: {Ad(d.BuatId)}";
            if (!DevreAsamadaDuzenlenebilir(kat, d.Id)) continue;
            DevreOzetleri.Add(new DevreOzetSatiri(
                d.Id, "🔌", "Priz hattı",
                $"{d.PrizElemanIds.Count} priz · {buat} · {DevreUzunluk(d.Id):0.#} m",
                d.Id == SeciliDevreId));
        }
        foreach (var b in kat.BuatPanoBaglantilari)
        {
            if (!DevreAsamadaDuzenlenebilir(kat, b.Id)) continue;
            DevreOzetleri.Add(new DevreOzetSatiri(
                b.Id, "▦", $"{b.LinyePrefix} linyesi → {Ad(b.PanoId)}",
                $"{Ad(b.BuatId)} → {Ad(b.PanoId)} · {DevreUzunluk(b.Id):0.#} m",
                b.Id == SeciliDevreId));
        }
        OnPropertyChanged(nameof(DevreOzetiVar));
    }

    public bool DevreOzetiVar => DevreOzetleri.Count > 0;

    [RelayCommand]
    private void DevreSatiriSec(DevreOzetSatiri? satir)
    {
        if (satir == null) return;
        SeciliDevreId = satir.DevreId;
        GuncelleKabloGorunum();
        DurumMesaji = $"Hat vurgulandı: {satir.Baslik}";
    }

    [RelayCommand]
    private void DevreSatiriSil(DevreOzetSatiri? satir)
    {
        if (satir == null) return;
        SeciliDevreId = satir.DevreId;
        KabloSeciliDevreyiSil(); // kilit denetimi + undo snapshot orada
    }

    /// <summary>Kablo segmentlerini ve pano etiketlerini günceller (liste yeniden kurulmaz).</summary>
    private void RenderKablolar()
    {
        var kat = SeciliKat;
        if (kat == null) { RenderKabloSegmentleri = null; RenderKabloVurgu = null; RenderKabloEtiketleri = null; return; }

        // Kablo rotaları lokal koordinattadır; dünya dönüşümü yalnız burada uygulanır.
        double rox = kat.OriginOffset.X, roy = kat.OriginOffset.Y;
        (Point3D, Point3D, string) ToRender((Point2D A, Point2D B, string Renk) s)
            => (new Point3D(s.A.X + rox, s.A.Y + roy, 0), new Point3D(s.B.X + rox, s.B.Y + roy, 0), s.Renk);

        var baglantiById = kat.BuatPanoBaglantilari.ToDictionary(b => b.Id);
        var aktifKablolar = kat.Kablolar.Where(k => KabloAktifPaftadaMi(kat, k)).ToList();

        var vurguSet = VurgulananDevreIdleri(kat);
        var tavaSegs = KabloTavasiRenderSegmentleri(kat);
        var toprakSegs = TopraklamaRenderSegmentleri(kat);
        var beslemeSegs = BeslemeRenderSegmentleri(kat);
        if (vurguSet.Count > 0)
        {
            var normalSegs = BasitProjeCizKabloServisi.KabloSegmentleri(
                aktifKablolar.Where(k => !vurguSet.Contains(k.DevreId)));
            var vurguSegs  = BasitProjeCizKabloServisi.KabloSegmentleri(
                aktifKablolar.Where(k => vurguSet.Contains(k.DevreId)));
            var normalRender = normalSegs.Select(ToRender).Concat(tavaSegs).Concat(toprakSegs).Concat(beslemeSegs).ToList();
            RenderKabloSegmentleri = normalRender.Count > 0 ? normalRender : null;
            RenderKabloVurgu       = vurguSegs.Count  > 0 ? vurguSegs.Select(ToRender).ToList()  : null;
        }
        else
        {
            var segs = BasitProjeCizKabloServisi.KabloSegmentleri(aktifKablolar);
            var normalRender = segs.Select(ToRender).Concat(tavaSegs).Concat(toprakSegs).Concat(beslemeSegs).ToList();
            RenderKabloSegmentleri = normalRender.Count > 0 ? normalRender : null;
            RenderKabloVurgu       = null;
        }

        var etiketler = new List<(Point3D, string, string)>();
        var etiketNoktalari = new List<Point2D>();

        bool EtiketliMi(BpcKablo k) =>
            (k.Tip == BpcKabloTipi.PanoKablosu || k.Tip == BpcKabloTipi.MekanikKablosu ||
             k.Tip == BpcKabloTipi.ZayifAkimKablosu) && k.PanoSira is not null && k.Rota.Count >= 2;

        foreach (var k in aktifKablolar.Where(EtiketliMi))
        {
            string prefix;
            string color;
            if (k.Tip == BpcKabloTipi.ZayifAkimKablosu && k.SistemAnahtar is { } sa)
            {
                var t = BpcSistemKatalogu.Get(sa);
                prefix = t.LinyePrefix;
                color  = t.KabloRengi;
            }
            else if (k.Tip == BpcKabloTipi.MekanikKablosu)
            {
                prefix = "M";
                color  = "#80D8FF";
            }
            else
            {
                prefix = baglantiById.TryGetValue(k.DevreId, out var b) ? b.LinyePrefix : "A";
                color  = "#00D0B8";
            }
            var p = UygunPanoEtiketNoktasi(k, aktifKablolar, kat.Elemanlar, etiketNoktalari);
            etiketNoktalari.Add(p);
            etiketler.Add((new Point3D(p.X + rox, p.Y + roy, 0), $"{prefix}{k.PanoSira}", color));
        }
        foreach (var e in KabloTavasiEtiketleri(kat))
            etiketler.Add(e);
        foreach (var e in TopraklamaEtiketleri(kat))
            etiketler.Add(e);
        foreach (var e in BeslemeEtiketleri(kat))
            etiketler.Add(e);
        RenderKabloEtiketleri = etiketler;
    }

    private List<(Point3D A, Point3D B, string Renk)> KabloTavasiRenderSegmentleri(BasitProjeKat kat)
    {
        var segs = new List<(Point3D A, Point3D B, string Renk)>();
        if (!KabloTavasiAktif) return segs;

        foreach (var tava in kat.KabloTavalari.Where(t => t.Rota.Count >= 2))
        {
            KabloTavasiSegmentleriniEkle(segs, kat, tava.Rota, tava.GenislikMm, tava.Adet,
                tava == SeciliKabloTavasi ? "#F7F7D2" : "#B8C0B0");
            foreach (var tali in tava.TaliHatlar.Where(t => t.Rota.Count >= 2))
                KabloTavasiSegmentleriniEkle(segs, kat, tali.Rota, tali.GenislikMm, tava.Adet,
                    tava == SeciliKabloTavasi ? "#EDEB9A" : "#AEB7A8");
        }

        if (KabloTavasiCizimAktif && _kabloTavasiCizimNoktalar.Count >= 2)
            KabloTavasiSegmentleriniEkle(segs, kat, _kabloTavasiCizimNoktalar,
                KabloTavasiGenislikMm, KabloTavasiAdet, "#F2E56B");

        return segs;
    }

    private static void KabloTavasiSegmentleriniEkle(
        List<(Point3D A, Point3D B, string Renk)> segs,
        BasitProjeKat kat,
        IReadOnlyList<Point2D> rota,
        double genislikMm,
        int adet,
        string renk)
    {
        var tavaAdedi = System.Math.Clamp(adet, 1, 2);
        var genislikM = System.Math.Clamp(genislikMm / 1000.0, 0.07, 0.70);
        var half = genislikM / 2.0;
        var aralik = System.Math.Clamp(genislikM * 0.20, 0.05, 0.15);
        var merkezAraligi = genislikM + aralik;
        for (int i = 0; i < rota.Count - 1; i++)
        {
            var a = rota[i];
            var b = rota[i + 1];
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var len = System.Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-6) continue;
            var ux = -dy / len;
            var uy = dx / len;

            for (int tavaNo = 0; tavaNo < tavaAdedi; tavaNo++)
            {
                var merkezOfset = tavaAdedi == 1 ? 0.0 : (tavaNo == 0 ? -merkezAraligi / 2.0 : merkezAraligi / 2.0);
                var cox = ux * merkezOfset;
                var coy = uy * merkezOfset;
                var nx = ux * half;
                var ny = uy * half;
                var a1 = ToWorld(kat, new Point2D(a.X + cox + nx, a.Y + coy + ny));
                var b1 = ToWorld(kat, new Point2D(b.X + cox + nx, b.Y + coy + ny));
                var a2 = ToWorld(kat, new Point2D(a.X + cox - nx, a.Y + coy - ny));
                var b2 = ToWorld(kat, new Point2D(b.X + cox - nx, b.Y + coy - ny));
                segs.Add((a1, b1, renk));
                segs.Add((a2, b2, renk));

                for (double dist = 0.0; dist <= len + 1e-6; dist += 0.8)
                {
                    var t = len < 1e-6 ? 0 : dist / len;
                    var cx = a.X + dx * t + cox;
                    var cy = a.Y + dy * t + coy;
                    segs.Add((ToWorld(kat, new Point2D(cx + nx, cy + ny)),
                              ToWorld(kat, new Point2D(cx - nx, cy - ny)), renk));
                }
            }
        }
    }

    private IEnumerable<(Point3D Position, string Text, string Renk)> KabloTavasiEtiketleri(BasitProjeKat kat)
    {
        if (!KabloTavasiAktif) yield break;
        foreach (var tava in kat.KabloTavalari.Where(t => t.EtiketGoster && t.Rota.Count >= 2))
        {
            var p = OrtaNokta(tava.Rota);
            yield return (ToWorld(kat, new Point2D(p.X + 0.15, p.Y + 0.15)),
                KabloTavasiEtiketi(tava), tava == SeciliKabloTavasi ? "#F7F7D2" : "#B8C0B0");
        }
    }

    private static string KabloTavasiEtiketi(BpcKabloTavasi tava)
    {
        var adet = System.Math.Clamp(tava.Adet, 1, 2);
        var genislik = $"{tava.GenislikMm:N0}";
        var yukseklik = $"{tava.YukseklikMm:N0}";
        var et = tava.EtKalinligiMm.ToString("0.0", CultureInfo.InvariantCulture);
        var olcu = adet == 1
            ? $"{genislik}x{yukseklik}x{et}"
            : $"{adet}x{genislik}x{yukseklik}x{et}";
        return tava.AgirHizmet ? $"{tava.Ad}-{olcu} Ağır Hizmet" : $"{tava.Ad}-{olcu}";
    }

    private List<(Point3D A, Point3D B, string Renk)> TopraklamaRenderSegmentleri(BasitProjeKat kat)
    {
        var segs = new List<(Point3D A, Point3D B, string Renk)>();
        if (!TopraklamaAktif) return segs;

        foreach (var s in kat.Topraklama.Seritler)
        {
            var renk = s.Tip == BpcTopraklamaSeritTipi.OrguBakir ? "#C98745" : "#7EE070";
            segs.Add((ToWorld(kat, s.A), ToWorld(kat, s.B), renk));
        }

        for (int i = 0; i < kat.Topraklama.SecimNoktalari.Count - 1; i++)
            segs.Add((ToWorld(kat, kat.Topraklama.SecimNoktalari[i]),
                      ToWorld(kat, kat.Topraklama.SecimNoktalari[i + 1]), "#A0F0A0"));

        foreach (var p in kat.Topraklama.Kaziklar)
        {
            const double d = 0.16;
            segs.Add((ToWorld(kat, new Point2D(p.X - d, p.Y)), ToWorld(kat, new Point2D(p.X + d, p.Y)), "#F0F08A"));
            segs.Add((ToWorld(kat, new Point2D(p.X, p.Y - d)), ToWorld(kat, new Point2D(p.X, p.Y + d)), "#F0F08A"));
        }
        return segs;
    }

    private IEnumerable<(Point3D Position, string Text, string Renk)> TopraklamaEtiketleri(BasitProjeKat kat)
    {
        if (!TopraklamaAktif || kat.Topraklama.Seritler.Count == 0) yield break;
        var p = kat.Topraklama.Seritler[0].A;
        yield return (ToWorld(kat, new Point2D(p.X + 0.25, p.Y + 0.25)), "30x3.5 Galv. Topraklama Şeridi", "#7EE070");
        if (kat.Topraklama.EkTopraklamaVar)
            yield return (ToWorld(kat, new Point2D(p.X + 0.25, p.Y + 0.55)), "35 mm Örgülü Bakır Ek Topraklama", "#C98745");
    }

    private List<(Point3D A, Point3D B, string Renk)> BeslemeRenderSegmentleri(BasitProjeKat kat)
    {
        var segs = new List<(Point3D A, Point3D B, string Renk)>();
        if (!BusbarKabloAktif) return segs;
        foreach (var hat in kat.PanoBeslemeHatlari.Where(h => h.Rota.Count >= 2))
            for (int i = 0; i < hat.Rota.Count - 1; i++)
                segs.Add((ToWorld(kat, hat.Rota[i]), ToWorld(kat, hat.Rota[i + 1]), "#FFB860"));
        return segs;
    }

    private IEnumerable<(Point3D Position, string Text, string Renk)> BeslemeEtiketleri(BasitProjeKat kat)
    {
        if (!BusbarKabloAktif) yield break;
        foreach (var hat in kat.PanoBeslemeHatlari.Where(h => h.Rota.Count >= 2 && !string.IsNullOrWhiteSpace(h.Etiket)))
        {
            var p = OrtaNokta(hat.Rota);
            yield return (ToWorld(kat, new Point2D(p.X + 0.15, p.Y + 0.15)), hat.Etiket, "#FFB860");
        }
    }

    private static Point3D ToWorld(BasitProjeKat kat, Point2D p)
        => new(p.X + kat.OriginOffset.X, p.Y + kat.OriginOffset.Y, 0);

    private static Point2D OrtaNokta(IReadOnlyList<Point2D> rota)
    {
        var mid = (rota.Count - 1) / 2;
        return new Point2D((rota[mid].X + rota[mid + 1].X) / 2.0, (rota[mid].Y + rota[mid + 1].Y) / 2.0);
    }

    private static Point2D KabloTavasiDuzNoktayaKilitle(Point2D onceki, Point2D yeni)
    {
        var dx = System.Math.Abs(yeni.X - onceki.X);
        var dy = System.Math.Abs(yeni.Y - onceki.Y);
        return dx >= dy
            ? new Point2D(yeni.X, onceki.Y)
            : new Point2D(onceki.X, yeni.Y);
    }

    private static Point2D KabloTavasiEnYakinNokta(Point2D p, IReadOnlyList<Point2D> rota)
    {
        var best = rota[0];
        var bestDist = double.MaxValue;
        for (int i = 0; i < rota.Count - 1; i++)
        {
            var aday = NoktaSegmentProjeksiyonu(p, rota[i], rota[i + 1]);
            var d = Mesafe(p, aday);
            if (d < bestDist)
            {
                bestDist = d;
                best = aday;
            }
        }
        return best;
    }

    private static double Mesafe(Point2D a, Point2D b)
        => System.Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static double NoktaSegmentMesafesi(Point2D p, Point2D a, Point2D b)
        => Mesafe(p, NoktaSegmentProjeksiyonu(p, a, b));

    private static Point2D NoktaSegmentProjeksiyonu(Point2D p, Point2D a, Point2D b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var len2 = dx * dx + dy * dy;
        if (len2 < 1e-9) return a;
        var t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2;
        t = System.Math.Clamp(t, 0, 1);
        return new Point2D(a.X + dx * t, a.Y + dy * t);
    }

    /// <summary>
    /// Vurgulanacak devre kimlikleri: tek seçili kablo (SeciliDevreId) ve
    /// menüden seçili linye (pano kablosu + o buata bağlı tüm devreler).
    /// </summary>
    private bool KabloAktifPaftadaMi(BasitProjeKat kat, BpcKablo k)
    {
        return k.Tip switch
        {
            BpcKabloTipi.PrizKablosu    => AktifPafta == BpcPaftaTipi.Priz,
            BpcKabloTipi.MekanikKablosu => AktifPafta == BpcPaftaTipi.Mekanik,
            // Zayıf akım kablosu yalnızca kendi sisteminin paftasında görünür
            BpcKabloTipi.ZayifAkimKablosu => k.SistemAnahtar == AktifPafta,
            // Pano kablosu hangi linyeye (A/P) aitse o paftada görünür
            BpcKabloTipi.PanoKablosu =>
                kat.BuatPanoBaglantilari.FirstOrDefault(b => b.Id == k.DevreId) is { } bag
                    ? (AktifPafta == BpcPaftaTipi.Priz ? bag.LinyePrefix == "P"
                       : AktifPafta == BpcPaftaTipi.Aydinlatma && bag.LinyePrefix == "A")
                    : AktifPafta == BpcPaftaTipi.Aydinlatma,
            // Güç/seyahat/kontrol kabloları yalnızca aydınlatma paftasında
            BpcKabloTipi.GucKablosu or BpcKabloTipi.GucKablosu2 or
            BpcKabloTipi.SeyahatKablosu or BpcKabloTipi.KontrolKablosu
                => AktifPafta == BpcPaftaTipi.Aydinlatma,
            _ => AktifPafta == BpcPaftaTipi.Aydinlatma
        };
    }

    private HashSet<string> VurgulananDevreIdleri(BasitProjeKat kat)
    {
        var set = new HashSet<string>();
        if (!string.IsNullOrEmpty(SeciliDevreId)) set.Add(SeciliDevreId!);

        var bid = SeciliLinye?.BaglantiId;
        if (!string.IsNullOrEmpty(bid))
        {
            set.Add(bid!); // pano/mekanik kablosunun DevreId'si = bağlantı kimliği
            var bag = kat.BuatPanoBaglantilari.FirstOrDefault(b => b.Id == bid);
            if (bag != null)
            {
                // O buata bağlı aydınlatma devreleri (A linyesi)
                foreach (var d in kat.Devreler.Where(d => d.BuatId == bag.BuatId))
                    set.Add(d.Id);
                // O buata bağlı priz devreleri (P linyesi)
                foreach (var d in kat.PrizDevreleri.Where(d => d.BuatId == bag.BuatId))
                    set.Add(d.Id);
            }
        }
        return set;
    }

    private static Point2D PanoEtiketNoktasi(BpcKablo kablo)
    {
        var p = kablo.Rota[^1];
        var prev = kablo.Rota[^2];
        var dx = p.X - prev.X;
        var dy = p.Y - prev.Y;
        var len = System.Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-9)
            return p;
        return new Point2D(p.X - dx / len * 0.35 - dy / len * 0.08,
                            p.Y - dy / len * 0.35 + dx / len * 0.08);
    }

    private static Point2D UygunPanoEtiketNoktasi(
        BpcKablo kablo,
        IReadOnlyList<BpcKablo> kablolar,
        IReadOnlyList<BasitProjeEleman> elemanlar,
        IReadOnlyList<Point2D> oncekiEtiketler)
    {
        var baz = PanoEtiketNoktasi(kablo);
        var adaylar = new[]
        {
            baz,
            new Point2D(baz.X, baz.Y + 0.40),
            new Point2D(baz.X, baz.Y - 0.40),
            new Point2D(baz.X + 0.45, baz.Y),
            new Point2D(baz.X - 0.45, baz.Y),
            new Point2D(baz.X + 0.45, baz.Y + 0.35),
            new Point2D(baz.X - 0.45, baz.Y + 0.35),
            new Point2D(baz.X + 0.45, baz.Y - 0.35),
            new Point2D(baz.X - 0.45, baz.Y - 0.35),
            new Point2D(baz.X, baz.Y + 0.80)
        };

        return adaylar
            .OrderBy(p => PanoEtiketCakismaSkoru(p, kablolar, elemanlar, oncekiEtiketler))
            .ThenBy(p => System.Math.Abs(p.X - baz.X) + System.Math.Abs(p.Y - baz.Y))
            .First();
    }

    private static double PanoEtiketCakismaSkoru(
        Point2D p,
        IReadOnlyList<BpcKablo> kablolar,
        IReadOnlyList<BasitProjeEleman> elemanlar,
        IReadOnlyList<Point2D> oncekiEtiketler)
    {
        double skor = 0;
        foreach (var e in elemanlar)
        {
            var d = System.Math.Sqrt((p.X - e.X) * (p.X - e.X) + (p.Y - e.Y) * (p.Y - e.Y));
            if (d < 0.45) skor += (0.45 - d) * 80;
        }

        foreach (var k in kablolar)
        {
            for (int i = 0; i < k.Rota.Count - 1; i++)
            {
                var a = k.Rota[i];
                var b = k.Rota[i + 1];
                var d = DistPointToSegment(p.X, p.Y, a.X, a.Y, b.X, b.Y);
                if (d < 0.22) skor += (0.22 - d) * 55;
            }
        }

        foreach (var onceki in oncekiEtiketler)
        {
            var d = System.Math.Sqrt((p.X - onceki.X) * (p.X - onceki.X) + (p.Y - onceki.Y) * (p.Y - onceki.Y));
            if (d < 0.38) skor += (0.38 - d) * 120;
        }

        return skor;
    }

    // ─── Eleman yerleşimi ────────────────────────────────────────────────

    public void SeciliOdayaArmatürYerleştir()
    {
        if (SeciliKat == null || SeciliOda == null || SeciliArmatür == null) return;
        var yeni = ArmaturYerlesimServisi.YerlesimOlustur(SeciliOda, SeciliArmatür);
        foreach (var eleman in yeni)
        {
            SeciliKat.Elemanlar.Add(eleman);
            if (SensörluMod)
                SeciliKat.Elemanlar.Add(YeniSensör(eleman));
        }
        DurumMesaji = $"{SeciliOda.Ad}: {yeni.Count} adet {SeciliArmatür.Ad} yerleştirildi.";
        OnPropertyChanged(nameof(Elemanlar));
        Guncelle3DGorunum();
    }

    public void SeciliOdayaDedektorYerlestir()
    {
        if (SeciliKat == null || SeciliOda == null) return;
        if (SeciliZayifElemanSecenek?.Tip is not { } tip || !_dedektorTipleri.Contains(tip)) return;

        var yeni = DedektorYerlesimServisi.YerlesimOlustur(SeciliOda, tip);
        foreach (var eleman in yeni)
            SeciliKat.Elemanlar.Add(eleman);

        var tipAdi = BasitProjeCizKutuphaneServisi.ZayifAkimTanim(tip)?.Ad ?? tip.ToString();
        DurumMesaji = $"{SeciliOda.Ad}: {yeni.Count} adet {tipAdi} yerleştirildi.";
        OnPropertyChanged(nameof(Elemanlar));
        Guncelle3DGorunum();
    }

    private static BasitProjeEleman YeniSensör(BasitProjeEleman armatür) => new()
    {
        Tip       = BpcElemanTipi.Sensör,
        Ad        = $"Sns-{armatür.Ad}",
        EkranAdi  = "S",
        OdaId     = armatür.OdaId,
        X         = armatür.X + 0.35,
        Y         = armatür.Y,
        PaftaTipi = armatür.PaftaTipi
    };

    public void ManuelElemanEkle(BpcElemanTipi tip, Point2D p)
    {
        if (SeciliKat == null) return;
        if (KilitliyseDur("eleman eklemek")) return;

        // Duvar snap: anahtar/buat tipleri en yakın duvara yapışır
        if (BasitProjeCizKabloServisi.ElemanDuvarSnapMi(tip) && SeciliKat.Odalar.Count > 0)
            p = BasitProjeCizDxfServisi.SnapToNearestWall(p, SeciliKat.Odalar);

        var oda = SeciliKat.Odalar.FirstOrDefault(o => PoligonYardimci.IsPointInside(p, o.Poligon));
        var tanim = _kutuphane.BulManuelTanim(tip);
        var eleman = new BasitProjeEleman
        {
            Tip       = tip,
            Ad        = tanim?.Ad ?? tip.ToString(),
            EkranAdi  = string.IsNullOrWhiteSpace(tanim?.EkranAdi) ? (tanim?.Ad ?? tip.ToString()) : tanim.EkranAdi,
            OdaId     = oda?.Id,
            X         = p.X,
            Y         = p.Y,
            BlokAdi   = tanim?.BlokAdi ?? tip.ToString(),
            // Kat panosu tüm paftalarda ortak görünür; sabit Aydinlatma olarak saklanır
            PaftaTipi = tip == BpcElemanTipi.KatPanosu ? BpcPaftaTipi.Aydinlatma : AktifPafta
        };
        SeciliKat.Elemanlar.Add(eleman);
        if (SensörluMod && tip == BpcElemanTipi.Armatür)
            SeciliKat.Elemanlar.Add(YeniSensör(eleman));
        SeciliEleman = eleman;
        DurumMesaji = $"{eleman.Ad} yerleştirildi.";
        OnPropertyChanged(nameof(Elemanlar));
        if (tip == BpcElemanTipi.KatPanosu) GuncellePanoIcerik();
        Guncelle3DGorunum();
    }

    private void ElemanSil(BasitProjeEleman? eleman)
    {
        if (SeciliKat == null || eleman == null) return;
        if (KilitliyseDur("silmek")) return;
        var kat = SeciliKat;

        // Devreleri ve kabloları temizle
        var silDevre = kat.Devreler
            .Where(d => d.AnahtarElemanId == eleman.Id)
            .ToList();
        foreach (var d in silDevre)
        {
            kat.Kablolar.RemoveAll(k => k.DevreId == d.Id);
            kat.VavienGruplari.RemoveAll(g => g.DevreId == d.Id
                || g.Anahtar1Id == eleman.Id || g.Anahtar2Id == eleman.Id);
            kat.Devreler.Remove(d);
        }

        // Armatür olarak yer aldığı devrelerden çıkar
        foreach (var d in kat.Devreler)
        {
            if (d.ArmaturElemanIds.Remove(eleman.Id))
                kat.Kablolar.RemoveAll(k => k.DevreId == d.Id);
        }

        // Buat olarak atanmışsa temizle
        foreach (var d in kat.Devreler.Where(d => d.BuatId == eleman.Id))
        {
            d.BuatId = null;
            kat.Kablolar.RemoveAll(k => k.DevreId == d.Id);
        }

        // Priz devrelerinde priz veya priz buatı olarak yer alıyorsa temizle
        foreach (var d in kat.PrizDevreleri.ToList())
        {
            var degisti = d.PrizElemanIds.Remove(eleman.Id);
            if (d.BuatId == eleman.Id)
            {
                d.BuatId = null;
                degisti = true;
            }

            if (!degisti) continue;
            kat.Kablolar.RemoveAll(k => k.DevreId == d.Id);
            if (d.PrizElemanIds.Count == 0 && string.IsNullOrWhiteSpace(d.BuatId))
                kat.PrizDevreleri.Remove(d);
        }

        // Buat-pano bağlantısında buat/direkt priz/pano olarak yer alıyorsa temizle
        foreach (var baglanti in kat.BuatPanoBaglantilari
                     .Where(b => b.BuatId == eleman.Id || b.PanoId == eleman.Id)
                     .ToList())
        {
            kat.Kablolar.RemoveAll(k => k.DevreId == baglanti.Id);
            kat.BuatPanoBaglantilari.Remove(baglanti);
        }

        // Mekanik devresi olarak yer aldığı bağlantıları temizle
        if (kat.MekanikDevreleri != null)
        {
            var silMek = kat.MekanikDevreleri
                .Where(m => m.KlimaElemanId == eleman.Id || m.PanoId == eleman.Id)
                .ToList();
            foreach (var m in silMek)
            {
                kat.Kablolar.RemoveAll(k => k.DevreId == m.Id);
                kat.MekanikDevreleri.Remove(m);
            }
        }

        // Zayıf akım devresi: merkez/ara toplayıcıysa devreyi sil, cihazsa devreden çıkar
        foreach (var d in kat.SistemDevreleri.ToList())
        {
            if (d.MerkezId == eleman.Id || d.AraToplayiciId == eleman.Id)
            {
                kat.Kablolar.RemoveAll(k => k.DevreId == d.Id);
                kat.SistemDevreleri.Remove(d);
            }
            else if (d.CihazIds.Remove(eleman.Id))
            {
                // Cihaz çıktı → rota geçersiz; boşaldıysa devreyi de sil
                kat.Kablolar.RemoveAll(k => k.DevreId == d.Id);
                if (d.CihazIds.Count == 0) kat.SistemDevreleri.Remove(d);
            }
        }

        if (eleman.Tip == BpcElemanTipi.KatPanosu)
        {
            // Pano besleme hatları katlar arası parçalar taşıyabilir; panonun
            // silinmesi bütün projedeki elektriksel referansları temizlemelidir.
            foreach (var projeKati in Proje.Katlar)
            {
                projeKati.PanoBeslemeHatlari ??= new();
                projeKati.BuatPanoBaglantilari ??= new();
                projeKati.MekanikDevreleri ??= new();
                projeKati.PanoBeslemeHatlari.RemoveAll(h =>
                    h.AnaPanoId == eleman.Id || h.TaliPanoId == eleman.Id);

                foreach (var tali in projeKati.Elemanlar.Where(e =>
                             e.TaliPanoHedefPanoId == eleman.Id))
                    tali.TaliPanoHedefPanoId = null;

                foreach (var baglanti in projeKati.BuatPanoBaglantilari
                             .Where(b => b.PanoId == eleman.Id)
                             .ToList())
                {
                    projeKati.Kablolar.RemoveAll(k => k.DevreId == baglanti.Id);
                    projeKati.BuatPanoBaglantilari.Remove(baglanti);
                }

                foreach (var mekanik in projeKati.MekanikDevreleri
                             .Where(m => m.PanoId == eleman.Id)
                             .ToList())
                {
                    projeKati.Kablolar.RemoveAll(k => k.DevreId == mekanik.Id);
                    projeKati.MekanikDevreleri.Remove(mekanik);
                }
            }
        }

        var silTip = eleman.Tip;
        kat.Elemanlar.Remove(eleman);
        if (SeciliEleman == eleman) SeciliEleman = null;
        DurumMesaji = "Eleman silindi.";
        OnPropertyChanged(nameof(Elemanlar));
        if (silTip == BpcElemanTipi.KatPanosu)
        {
            GuncelleTumKatPanolari();
            GuncellePanoIcerik();
        }
        Guncelle3DGorunum();
    }

    public void ElemanTasi(BasitProjeEleman eleman, Point2D p)
    {
        if (KilitliyseDur("eleman taşımak")) return;
        eleman.X = p.X;
        eleman.Y = p.Y;
        OnPropertyChanged(nameof(Elemanlar));
        Guncelle3DGorunum();
    }

    // ─── Kütüphane — Aydınlatma ──────────────────────────────────────────

    [RelayCommand]
    private void KutuphaneArmatürEkle()
    {
        var yeni = new BpcArmatürTanim
        {
            Ad = "Yeni Armatür", BlokAdi = "E4P_LIGHT", SembolAdi = "L",
            MontajTipi = "Tavan", GucW = 18,
            YerlesimMantigi = BpcArmaturlestirmeMantigi.GridDizilim,
            AlanBasinaAdetM2 = 12, GridAraligiM = 3.0
        };
        yeni.SembolCizgiler.AddRange(BasitProjeCizKutuphaneServisi.GenelArmatürSembol());
        Armatürler.Add(yeni);
        SeciliKutuphaneArmatür = yeni;
        KutuphaneMesaji = "Yeni armatür eklendi. Değerleri düzenleyip Kaydet'e basın.";
    }

    [RelayCommand]
    private void KutuphaneArmatürSil()
    {
        if (SeciliKutuphaneArmatür == null) return;
        Armatürler.Remove(SeciliKutuphaneArmatür);
        SeciliKutuphaneArmatür = Armatürler.FirstOrDefault();
        KutuphaneMesaji = "Armatür kaldırıldı. Kalıcı olması için Kaydet'e basın.";
    }

    // ─── Kütüphane — Anahtarlar ──────────────────────────────────────────

    [RelayCommand]
    private void KutuphaneAnahtarEkle()
    {
        var yeni = new BpcArmatürTanim
        {
            Ad = "Yeni Anahtar", BlokAdi = "E4P_ANAHTAR", SembolAdi = "A",
            MontajTipi = "Duvar", GucW = 0
        };
        yeni.SembolCizgiler.AddRange(BasitProjeCizKutuphaneServisi.AnahtarSembol());
        Anahtarlar.Add(yeni);
        SeciliKutuphaneAnahtar = yeni;
        KutuphaneMesaji = "Yeni anahtar tipi eklendi.";
    }

    [RelayCommand]
    private void KutuphaneAnahtarSil()
    {
        if (SeciliKutuphaneAnahtar == null) return;
        Anahtarlar.Remove(SeciliKutuphaneAnahtar);
        SeciliKutuphaneAnahtar = Anahtarlar.FirstOrDefault();
        KutuphaneMesaji = "Anahtar kaldırıldı.";
    }

    // ─── Kütüphane — Panolar ─────────────────────────────────────────────

    [RelayCommand]
    private void KutuphanePanoEkle()
    {
        var yeni = new BpcArmatürTanim
        {
            Ad = "Yeni Pano", BlokAdi = "E4P_PANO", SembolAdi = "P",
            MontajTipi = "Duvar", GucW = 0
        };
        yeni.SembolCizgiler.AddRange(BasitProjeCizKutuphaneServisi.KatPanosuSembol());
        Panolar.Add(yeni);
        SeciliKutuphanePano = yeni;
        KutuphaneMesaji = "Yeni pano tipi eklendi.";
    }

    [RelayCommand]
    private void KutuphanePanoSil()
    {
        if (SeciliKutuphanePano == null) return;
        Panolar.Remove(SeciliKutuphanePano);
        SeciliKutuphanePano = Panolar.FirstOrDefault();
        KutuphaneMesaji = "Pano kaldırıldı.";
    }

    // ─── Kütüphane — Prizler ─────────────────────────────────────────────

    [RelayCommand]
    private void KutuphanePrizEkle()
    {
        var yeni = new BpcArmatürTanim
        {
            Ad = "Yeni Priz", BlokAdi = "E4P_PRIZ_YENI", SembolAdi = "P",
            MontajTipi = "Duvar", GucW = 300
        };
        yeni.SembolCizgiler.AddRange(BasitProjeCizKutuphaneServisi.PrizSembol());
        Prizler.Add(yeni);
        SeciliKutuphanePriz = yeni;
        KutuphaneMesaji = "Yeni priz tipi eklendi.";
    }

    [RelayCommand]
    private void KutuphanePrizSil()
    {
        if (SeciliKutuphanePriz == null) return;
        Prizler.Remove(SeciliKutuphanePriz);
        SeciliKutuphanePriz = Prizler.FirstOrDefault();
        KutuphaneMesaji = "Priz kaldırıldı.";
    }

    // ─── Kütüphane — Klimalar ────────────────────────────────────────────

    [RelayCommand]
    private void KutuphaneKlimaEkle()
    {
        var yeni = new BpcArmatürTanim
        {
            Ad = "Yeni Klima", BlokAdi = "E4P_KLIMA_YENI", SembolAdi = "K",
            MontajTipi = "Duvar", GucW = 1000
        };
        yeni.SembolCizgiler.AddRange(BasitProjeCizKutuphaneServisi.KlimaIcUniteSembol());
        Klimalar.Add(yeni);
        SeciliKutuphaneKlima = yeni;
        KutuphaneMesaji = "Yeni klima tipi eklendi.";
    }

    [RelayCommand]
    private void KutuphaneKlimaSil()
    {
        if (SeciliKutuphaneKlima == null) return;
        Klimalar.Remove(SeciliKutuphaneKlima);
        SeciliKutuphaneKlima = Klimalar.FirstOrDefault();
        KutuphaneMesaji = "Klima kaldırıldı.";
    }

    // ─── Kütüphane — Düzelt ──────────────────────────────────────────────

    partial void OnAktifKutuphaneElemanChanged(BpcArmatürTanim? value)
    {
        OnPropertyChanged(nameof(DuzeltAd));
        OnPropertyChanged(nameof(DuzeltEkranAdi));
        OnPropertyChanged(nameof(DuzeltBlokAdi));
        OnPropertyChanged(nameof(DuzeltSembolAdi));
        OnPropertyChanged(nameof(DuzeltMontajTipi));
        OnPropertyChanged(nameof(DuzeltGucW));
        OnPropertyChanged(nameof(DuzeltGridAraligiM));
        OnPropertyChanged(nameof(DuzeltGridAraligiYM));
    }

    public string DuzeltAd
    {
        get => AktifKutuphaneEleman?.Ad ?? "";
        set { if (AktifKutuphaneEleman is { } e) { e.Ad = value; OnPropertyChanged(); } }
    }

    public string DuzeltEkranAdi
    {
        get => AktifKutuphaneEleman?.EkranAdi ?? "";
        set { if (AktifKutuphaneEleman is { } e) { e.EkranAdi = value; OnPropertyChanged(); } }
    }

    public string DuzeltBlokAdi
    {
        get => AktifKutuphaneEleman?.BlokAdi ?? "";
        set { if (AktifKutuphaneEleman is { } e) { e.BlokAdi = value; OnPropertyChanged(); } }
    }

    public string DuzeltSembolAdi
    {
        get => AktifKutuphaneEleman?.SembolAdi ?? "";
        set { if (AktifKutuphaneEleman is { } e) { e.SembolAdi = value; OnPropertyChanged(); } }
    }

    public string DuzeltMontajTipi
    {
        get => AktifKutuphaneEleman?.MontajTipi ?? "";
        set { if (AktifKutuphaneEleman is { } e) { e.MontajTipi = value; OnPropertyChanged(); } }
    }

    public double DuzeltGucW
    {
        get => AktifKutuphaneEleman?.GucW ?? 0;
        set { if (AktifKutuphaneEleman is { } e) { e.GucW = value; OnPropertyChanged(); } }
    }

    public double DuzeltGridAraligiM
    {
        get => AktifKutuphaneEleman?.GridAraligiM ?? 3;
        set { if (AktifKutuphaneEleman is { } e) { e.GridAraligiM = value; OnPropertyChanged(); } }
    }

    /// Grid Y aralığı. null/boş = X ile aynı (kare grid).
    public double? DuzeltGridAraligiYM
    {
        get => AktifKutuphaneEleman?.GridAraligiYM;
        set { if (AktifKutuphaneEleman is { } e) { e.GridAraligiYM = value; OnPropertyChanged(); } }
    }

    // ─── Kütüphane — Ortak ───────────────────────────────────────────────

    [RelayCommand]
    private void KutuphaneKaydet()
    {
        _kutuphane.Armatürler.Clear(); _kutuphane.Armatürler.AddRange(Armatürler);
        _kutuphane.Anahtarlar.Clear(); _kutuphane.Anahtarlar.AddRange(Anahtarlar);
        _kutuphane.Panolar.Clear();    _kutuphane.Panolar.AddRange(Panolar);
        _kutuphane.Prizler.Clear();    _kutuphane.Prizler.AddRange(Prizler);
        _kutuphane.Klimalar.Clear();   _kutuphane.Klimalar.AddRange(Klimalar);
        _kutuphane.Kaydet();
        RefreshKutuphane();
        KutuphaneMesaji = $"{Malzemeler.Count} global malzeme kaydedildi.";
    }

    // ArmatürleriKaydet: eski çağrı noktaları için tutuldu
    public void ArmatürleriKaydet() => KutuphaneKaydet();
}

internal static class ListExtensions
{
    public static void AddRange<T>(this System.Collections.Generic.ICollection<T> target,
        System.Collections.Generic.IEnumerable<T> items)
    {
        foreach (var item in items) target.Add(item);
    }
}

/// <summary>Zayıf akım sağ panel combosundaki bir yerleştirme seçeneği.</summary>
public sealed class ZayifElemanSecenek
{
    public BpcElemanTipi? Tip { get; init; }
    public string Ad { get; init; } = "";
    public override string ToString() => Ad;
}

/// <summary>Kütüphane penceresindeki zayıf akım kataloğunun bir satırı (sistem başlığı + tanım).</summary>
public sealed class ZayifAkimMalzemeSatiri
{
    public required string Grup { get; init; }          // sistem adı = grup başlığı
    public required BpcArmatürTanim Tanim { get; init; }
    public string Ad => Tanim.Ad;
    public string SembolAdi => Tanim.SembolAdi;
    public string BlokAdi => Tanim.BlokAdi;
    public string MontajTipi => Tanim.MontajTipi;
}

/// Kat panosuna oda atama checkbox listesi öğesi.
public sealed partial class PanoOdaSecenek : ObservableObject
{
    public string OdaId { get; init; } = "";
    public string Ad { get; init; } = "";
    /// Oda başka bir kat panosuna atanmışsa o panonun adı; yoksa null.
    public string? BaskaPanoAd { get; init; }
    public bool Engelli => BaskaPanoAd != null;
    public bool SerbestMi => BaskaPanoAd == null;
    /// Atanmış odalar farklı renkle gösterilir.
    public string OdaRengi => BaskaPanoAd != null ? "#C08030" : "#DDEED2";
    public string? ToolTipMetni => BaskaPanoAd != null ? $"'{BaskaPanoAd}' panosuna bağlı" : null;

    public Action<bool>? SeciliDegisti { get; set; }
    public Action? OdaTiklandi { get; set; }

    [ObservableProperty] private bool _secili;
    partial void OnSeciliChanged(bool value)
    {
        if (Engelli) return;
        SeciliDegisti?.Invoke(value);
    }

    [RelayCommand]
    private void Tikla() => OdaTiklandi?.Invoke();
}
