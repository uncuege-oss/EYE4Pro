using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.BuildingAcoustics.Parsers;
using Kst.Acoustics.Core.BuildingAcoustics.Calculation;
using Kst.Acoustics.Core.BuildingAcoustics.Database;
using Kst.Acoustics.Core.BuildingAcoustics.Geometry;
using Kst.Acoustics.Core.BuildingAcoustics.Reports;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Projects;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using System;
using Kst.Acoustics.App.BuildingAcoustics.Views;
using AciklikOnayar = Kst.Acoustics.Core.BuildingAcoustics.Models.AciklikOnayar;

namespace Kst.Acoustics.App.BuildingAcoustics.ViewModels;

/// Ribbon (üst toolbar) kategori sekmeleri — sol panelde açılan içerikleri belirler.
/// Kütüphane ayrı pencere olduğu için bu enum'da yok.
public enum KategoriTipi { Proje, Katlar, Mekanlar }

public partial class BinaProjesiViewModel : ObservableObject, IDisposable
{
    // EK-4 entegrasyonu: resmî iç gürültü hedefi mekan işlevine göre Tablo 4.1'den
    // gelir; eşleşme yoksa uygunluk kararı üretilmez ve sonuç VeriEksik gösterilir.

    [ObservableProperty]
    private BinaProje _proje = new();

    [ObservableProperty]
    private Kat? _seciliKat;

    [ObservableProperty]
    private Mekan? _seciliMekan;

    [ObservableProperty]
    private List<ProjectSurface>? _renderSurfaces;

    /// Seçili katın ham DXF çizgileri (referans katmanı olarak viewport'a çizilir).
    [ObservableProperty]
    private List<(Kst.Acoustics.Core.Geometry.Point3D A, Kst.Acoustics.Core.Geometry.Point3D B)>? _renderBackgroundSegments;

    /// Kapı/pencere açıklıkları — viewport'ta kalın kırmızı çizgilerle çizilir.
    [ObservableProperty]
    private IReadOnlyList<(Kst.Acoustics.Core.Geometry.Point3D Bas, Kst.Acoustics.Core.Geometry.Point3D Bit, bool Iptal)>? _renderAcikliklar;

    [ObservableProperty]
    private BolmeAcikligi? _seciliAciklikPaneli;

    /// Seçili mekanın düzenlenebilir köşeleri (viewport'ta sürüklenir).
    [ObservableProperty]
    private List<Kst.Acoustics.Core.Geometry.Point3D>? _editablePolygon;

    /// Viewport'ta vurgulanan komşu duvar segmentleri. (A, B, isSelected) → altın çizgi.
    [ObservableProperty]
    private List<(Kst.Acoustics.Core.Geometry.Point3D A, Kst.Acoustics.Core.Geometry.Point3D B, bool Selected)>? _neighborSegments;

    /// Viewport projeksiyon modu (Top2D / Isometric3D).
    [ObservableProperty]
    private Kst.Acoustics.App.Controls.GeometryProjectionMode _projection =
        Kst.Acoustics.App.Controls.GeometryProjectionMode.Top2D;

    private bool IsTop2D => Projection == Kst.Acoustics.App.Controls.GeometryProjectionMode.Top2D;

    // ── Ribbon kategori sistemi (AutoCAD Classic tarzı) ────────────────────
    // Üst ribbon'da 3 toggle kategori (Proje/Katlar/Mekanlar); Kütüphane ayrı pencere.
    // Aynı kategoriye 2. tıklama paneli kapatır; başka kategoriye geçişte panel açık kalır.
    [ObservableProperty]
    private KategoriTipi? _seciliKategori = KategoriTipi.Proje;

    public bool KategoriProjeAcik    => SeciliKategori == KategoriTipi.Proje;
    public bool KategoriKatlarAcik   => SeciliKategori == KategoriTipi.Katlar;
    public bool KategoriMekanlarAcik => SeciliKategori == KategoriTipi.Mekanlar;
    public bool KategoriPaneliAcik   => SeciliKategori is not null;

    partial void OnSeciliKategoriChanged(KategoriTipi? value)
    {
        OnPropertyChanged(nameof(KategoriProjeAcik));
        OnPropertyChanged(nameof(KategoriKatlarAcik));
        OnPropertyChanged(nameof(KategoriMekanlarAcik));
        OnPropertyChanged(nameof(KategoriPaneliAcik));
    }

    [RelayCommand]
    private void KategoriSec(string ad)
    {
        if (!Enum.TryParse<KategoriTipi>(ad, ignoreCase: true, out var yeni)) return;
        SeciliKategori = SeciliKategori == yeni ? null : yeni;
    }

    [RelayCommand]
    private void KutuphanesiAc()
    {
        var win = new Kst.Acoustics.App.BuildingAcoustics.Views.YapiElemaniKutuphaneWindow
        {
            DataContext = this
        };
        win.Show();
    }

    [RelayCommand]
    private void Goster2D() => Projection = Kst.Acoustics.App.Controls.GeometryProjectionMode.Top2D;

    [RelayCommand]
    private void Goster3D() => Projection = Kst.Acoustics.App.Controls.GeometryProjectionMode.Isometric3D;

    partial void OnProjectionChanged(Kst.Acoustics.App.Controls.GeometryProjectionMode value)
        => Guncelle3DGorunum();

    /// M08'de geometri M50'nin salt-okunur çıktısıdır. Ölçek değiştirme geçmişte yalnız
    /// bazı koordinatları dönüştürerek açıklık/geometri ayrışmasına yol açtığı için devre dışıdır.
    [ObservableProperty]
    private double _projeOlcek = 1.0;

    partial void OnProjeOlcekChanged(double value)
    {
        if (Math.Abs(value - 1.0) < 1e-9) return;
        ProjeOlcek = 1.0; // ikinci callback value=1 ile hemen çıkar
        DxfUyariMesaji = "Geometri ölçeği M50 tarafından yönetilir. Ölçeği M50'de düzeltip bina geometrisini yeniden yükleyin.";
    }

    [ObservableProperty]
    private bool _sonuclariGoster;

    // UX (Faz 5) — sonuç tablosu varsayılan olarak dar/karar-odaklı sütun setiyle açılır;
    // teknik/ayrıntı sütunları (Komşuluk, Bağımsız Bölüm, dış gürültü alanları, Baseline/Δ,
    // Birim, Rezonans, Yönetmelik) bu bayrak açılınca görünür. Zaten seçili satırın tüm
    // ayrıntısı sağ panelde (oktav/flanking/öneriler) gösteriliyor; tablo yalnız karar için
    // gereken sütunlara odaklanır.
    [ObservableProperty]
    private bool _sonucTablosuGenisGorunum;

    public string SonucTablosuGorunumEtiketi => SonucTablosuGenisGorunum ? "Tüm Sütunlar" : "Sade Görünüm";

    partial void OnSonucTablosuGenisGorunumChanged(bool value) =>
        OnPropertyChanged(nameof(SonucTablosuGorunumEtiketi));

    // UX — Isı haritası overlay (oda Doseme/Tavan yüzeyini yarı saydam renkte boyar)
    [ObservableProperty]
    private bool _isiHaritasiAcik;

    partial void OnIsiHaritasiAcikChanged(bool value) => Guncelle3DGorunum();

    [ObservableProperty]
    private string? _dxfUyariMesaji;

    [ObservableProperty]
    private string? _dogrulamaUyarisi;

    [ObservableProperty]
    private string? _duseyBirlesimMesaji;

    public ObservableCollection<Kat> Katlar { get; } = new();

    /// Kat listesi yukarıdan aşağıya (en yüksek kat önce) — sadece görüntüleme için.
    public IEnumerable<Kat> KatlarTersSira => Katlar;
    public ObservableCollection<SonucSatiriViewModel> SonucSatirlari { get; } = new();
    public ObservableCollection<SonucSatiriViewModel> EnKotuBolmeler { get; } = new();

    // UX — Rapora dahil edilecek odalar ve komşuluklar
    public ObservableCollection<RaporSatiriViewModel> RaporDahilSatirlar { get; } = new();
    public int RaporDahilSonucSayisi => RaporDahilSatirlar.Count(r => r.DahilMi);

    /// P0 düzeltmesi — "Rapora Dahil" işareti kaldırılmış bölmelerin Id kümesi.
    /// Metin/Excel/DXF dışa aktarımlarına geçilir; artık gerçek bir filtredir
    /// (eskiden hiçbir rapor yolu bu seçimi okumuyordu).
    public System.Collections.Generic.HashSet<System.Guid> RaporHaricTutulanBolmeIdleri =>
        RaporDahilSatirlar
            .Where(r => !r.DahilMi && r.SonucId != System.Guid.Empty)
            .Select(r => r.SonucId)
            .ToHashSet();

    [ObservableProperty]
    private bool _raporTamaminiDahilEt = true;

    partial void OnRaporTamaminiDahilEtChanged(bool value)
    {
        foreach (var r in RaporDahilSatirlar) r.DahilMi = value;
        OnPropertyChanged(nameof(RaporDahilSonucSayisi));
    }

    [RelayCommand]
    private void RaporSeciliSatiriToggle()
    {
        if (SeciliSonucSatiri == null) return;
        // Her sonuç türünde kararlı kimlik kullanılır; tesisat/RT satırlarında Bolme yoktur.
        var sonucId = SeciliSonucSatiri.SonucId;
        var eslesen = sonucId != System.Guid.Empty
            ? RaporDahilSatirlar.FirstOrDefault(r => r.SonucId == sonucId)
            : null;
        eslesen ??= RaporDahilSatirlar.FirstOrDefault(r =>
            r.Mekan == SeciliSonucSatiri.Mekan &&
            r.Bolme == SeciliSonucSatiri.Bolme &&
            r.HesapTuru == SeciliSonucSatiri.HesapTuru);
        eslesen ??= SeciliSonucSatiri.HesapTuru == "Cephe"
            ? RaporDahilSatirlar.FirstOrDefault(r =>
                r.Mekan == SeciliSonucSatiri.Mekan && r.HesapTuru == "Cephe")
            : null;
        if (eslesen != null)
        {
            eslesen.DahilMi = !eslesen.DahilMi;
            OnPropertyChanged(nameof(RaporDahilSonucSayisi));
        }
    }

    [RelayCommand]
    private void RaporSecimSifirla()
    {
        foreach (var r in RaporDahilSatirlar) r.DahilMi = true;
        RaporTamaminiDahilEt = true;
        OnPropertyChanged(nameof(RaporDahilSonucSayisi));
    }

    public void RefreshRaporDahilSatirlar()
    {
        var oncekiSecimler = RaporDahilSatirlar
            .Where(r => r.SonucId != Guid.Empty)
            .GroupBy(r => r.SonucId)
            .ToDictionary(g => g.Key, g => g.Last().DahilMi);
        RaporDahilSatirlar.Clear();
        // Seçim kapsamı, dışa aktarımların kullandığı aynı kanonik satırlardan gelir.
        // Böylece oda özetinde görünmeyen fiziksel duvarlar ile VeriEksik/HVAC/RT
        // satırları da kullanıcı tarafından tek tek dahil/haric edilebilir.
        foreach (var s in new BinaAkustikRaporServisi().TabloVerisiOlustur(Proje))
        {
            var satir = RaporSatiriViewModel.FromRapor(s, Proje);
            if (oncekiSecimler.TryGetValue(satir.SonucId, out var dahilMi)) satir.DahilMi = dahilMi;
            satir.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(RaporSatiriViewModel.DahilMi))
                    OnPropertyChanged(nameof(RaporDahilSonucSayisi));
            };
            RaporDahilSatirlar.Add(satir);
        }
        OnPropertyChanged(nameof(RaporDahilSonucSayisi));
    }

    // UX — Senaryo karşılaştırma: mevcut Deger'leri anlık görüntü olarak sakla,
    // sonra hesap tekrarlanınca her satırda Δ (mevcut − baseline) görüntülenir.
    private readonly Dictionary<string, (double Deger, double Marj)> _baselineDegerleri = new();
    [ObservableProperty] private string? _baselineEtiketi;
    public bool BaselineAktif => _baselineDegerleri.Count > 0;
    partial void OnBaselineEtiketiChanged(string? value) => OnPropertyChanged(nameof(BaselineAktif));

    /// Anlık görüntü al: SonucSatirlari'nı bir baseline olarak kaydet.
    [RelayCommand]
    private void SenaryoAnlikGoruntuAl()
    {
        _baselineDegerleri.Clear();
        foreach (var s in SonucSatirlari)
        {
            var key = SenaryoKey(s);
            _baselineDegerleri[key] = (s.Deger, s.MarjDb);
        }
        BaselineEtiketi = _baselineDegerleri.Count > 0
            ? $"Anlık görüntü ({_baselineDegerleri.Count} bölme) — sonraki hesapta Δ gösterilir."
            : null;
        UygulaBaselineDegerleri();
        OnPropertyChanged(nameof(BaselineAktif));
    }

    /// Baseline'ı temizle.
    [RelayCommand]
    private void SenaryoTemizle()
    {
        _baselineDegerleri.Clear();
        BaselineEtiketi = null;
        UygulaBaselineDegerleri();
        OnPropertyChanged(nameof(BaselineAktif));
    }

    private static string SenaryoKey(SonucSatiriViewModel s)
        => $"{s.Kat}|{s.Mekan}|{s.Bolme}|{s.Birim}";

    /// Her hesap sonrası çağrılır: baseline değer set edilmişse her satıra Baseline + Delta yansıtılır.
    public void UygulaBaselineDegerleri()
    {
        if (_baselineDegerleri.Count == 0)
        {
            foreach (var s in SonucSatirlari) { s.BaselineDeger = null; s.BaselineMarj = null; }
            return;
        }
        foreach (var s in SonucSatirlari)
        {
            if (_baselineDegerleri.TryGetValue(SenaryoKey(s), out var b))
            {
                s.BaselineDeger = b.Deger;
                s.BaselineMarj = b.Marj;
            }
            else { s.BaselineDeger = null; s.BaselineMarj = null; }
        }
    }

    [ObservableProperty]
    private SonucSatiriViewModel? _seciliSonucSatiri;

    public ObservableCollection<OktavBantGorunumu> OktavBantGorunumleri { get; } = new();
    public bool OktavDetayGorunur => SeciliSonucSatiri?.OktavBantlari != null;

    // UX — En Büyük 3 Flanking Yolu paneli
    public ObservableCollection<EtkiliYolGorunumu> EnBuyukUcYol { get; } = new();
    public bool EnBuyukUcYolGorunur => EnBuyukUcYol.Count > 0;

    // UX — Çözüm önerisi paneli
    public ObservableCollection<OneriRowViewModel> OnerilerListesi { get; } = new();
    public bool OnerilerGorunur => OnerilerListesi.Count > 0;

    // UX — Flanking yolu görselleştirme: seçili bölmenin top-3 yolu viewport'ta renkli ok ile çizilir
    [ObservableProperty] private List<(Kst.Acoustics.Core.Geometry.Point3D Bas, Kst.Acoustics.Core.Geometry.Point3D Bit, string Renk)>? _flankingYolSegmentleri;
    [ObservableProperty] private bool _flankingYolGoster = true;
    partial void OnFlankingYolGosterChanged(bool value) => GuncelleFlankingYolSegmentleri(SeciliSonucSatiri);

    private void GuncelleFlankingYolSegmentleri(SonucSatiriViewModel? satir)
    {
        if (!FlankingYolGoster || satir?.IlgiliBolme is not { } bolme || bolme.KaynakMekan == null)
        {
            FlankingYolSegmentleri = null;
            return;
        }
        // Top-3 etkili yolun renklerini al, kaynak mekan centroid ↔ alıcı mekan centroid arası ok çiz
        var katBolme = KatForBolme(bolme);
        var off = katBolme?.OriginOffset ?? new Point2D(0, 0);
        double z = KatTabanZ(katBolme) + 0.5;
        var kaynak = bolme.KaynakMekan;
        var alici = bolme.AliciMekan;
        if (kaynak == null || alici == null || kaynak.Poligon.Count < 3 || alici.Poligon.Count < 3)
        {
            FlankingYolSegmentleri = null;
            return;
        }
        double kx = kaynak.Poligon.Average(p => p.X) + off.X;
        double ky = kaynak.Poligon.Average(p => p.Y) + off.Y;
        double ax = alici.Poligon.Average(p => p.X) + off.X;
        double ay = alici.Poligon.Average(p => p.Y) + off.Y;
        var bas = new Kst.Acoustics.Core.Geometry.Point3D(kx, ky, z);
        var bit = new Kst.Acoustics.Core.Geometry.Point3D(ax, ay, z);

        // Her yolda hafif Y eksenini kaydır (Df, Fd, Ff iç içe çizilmesin)
        var segs = new List<(Kst.Acoustics.Core.Geometry.Point3D, Kst.Acoustics.Core.Geometry.Point3D, string)>();
        int i = 0;
        foreach (var yol in EnBuyukUcYol)
        {
            double shift = (i - 1) * 0.30; // -0.30, 0, +0.30
            var bas2 = new Kst.Acoustics.Core.Geometry.Point3D(bas.X, bas.Y + shift, z);
            var bit2 = new Kst.Acoustics.Core.Geometry.Point3D(bit.X, bit.Y + shift, z);
            segs.Add((bas2, bit2, yol.KatkiRengi));
            i++;
        }
        FlankingYolSegmentleri = segs.Count > 0 ? segs : null;
    }

    partial void OnSeciliSonucSatiriChanged(SonucSatiriViewModel? value)
    {
        if (value != null && RaporTablosuGozukuyor)
        {
            RaporTablosuGozukuyor = false;
            OnPropertyChanged(nameof(RaporTablosuToggleIkon));
        }
        OktavBantGorunumleri.Clear();
        if (value?.OktavBantlari is { } bantlar)
        {
            foreach (var b in bantlar)
                OktavBantGorunumleri.Add(b);
        }
        OnPropertyChanged(nameof(OktavDetayGorunur));

        // En Büyük 3 Yol hesabı (yalnızca hava sesi bölmelerinde)
        EnBuyukUcYol.Clear();
        if (value?.IlgiliBolme is { } bolme && bolme.YapiElemani != null)
        {
            foreach (var yol in EtkiliYolGorunumu.HesaplaUcEnZayifYol(bolme))
                EnBuyukUcYol.Add(yol);
        }
        OnPropertyChanged(nameof(EnBuyukUcYolGorunur));

        // UX — Çözüm önerileri (limit aşan / sınır bölmelerde anlamlı)
        OnerilerListesi.Clear();
        if (value?.IlgiliBolme is { } b2 && b2.YapiElemani != null)
        {
            double gereken = Math.Max(0, -value.MarjDb);
            var oneriler = new CozumOnerisiServisi().OnerileriGetir(b2, gereken);
            foreach (var o in oneriler)
                OnerilerListesi.Add(new OneriRowViewModel(o, value, this));
        }
        OnPropertyChanged(nameof(OnerilerGorunur));

        // UX — Flanking yolu görselleştirmesi
        GuncelleFlankingYolSegmentleri(value);
    }

    /// UX — Bir öneri "Uygula" butonuna tıklandığında çağrılır.
    /// Bolme'nin YapiElemani'ni klonlar, DeltaRDb'sini önerinin ArtisDb kadar artırır,
    /// klonu sadece bu bölmeye atar ve hesabı tetikler.
    public void OneriUygula(SonucSatiriViewModel satir, CozumOnerisi oneri)
    {
        if (ProjeKilitli || oneri.Uyari || satir.IlgiliBolme is not { } bolme || bolme.YapiElemani == null) return;
        // Snapshot al — Cmd+Z ile geri alınabilir
        PushUndoSnapshot();
        // YapiElemani'ni klonla (catalog'u kirletmeden)
        var orjinal = bolme.YapiElemani;
        var klon = new YapiElemani
        {
            Ad                 = $"{orjinal.Ad} + {oneri.OneriAdi}",
            Tip                = orjinal.Tip,
            Rw                 = orjinal.Rw,
            C                  = orjinal.C,
            Ctr                = orjinal.Ctr,
            Lnw                = orjinal.Lnw,
            DeltaLw            = orjinal.DeltaLw + oneri.ArtisDb,
            DeltaRDb           = orjinal.DeltaRDb + oneri.ArtisDb,
            BaglantiTipi       = orjinal.BaglantiTipi,
            FlankingDuzeltmesiDb = orjinal.FlankingDuzeltmesiDb,
            Hafiflik           = orjinal.Hafiflik,
            YapisalRtSn        = orjinal.YapisalRtSn,
            DinamikRijitlikMNm3 = orjinal.DinamikRijitlikMNm3,
            R63 = orjinal.R63, R125 = orjinal.R125, R250 = orjinal.R250, R500 = orjinal.R500,
            R1k = orjinal.R1k, R2k = orjinal.R2k, R4k = orjinal.R4k, R8k = orjinal.R8k,
            RThirdOctave = new List<double>(orjinal.RThirdOctave),
            DeltaRThirdOctave = orjinal.DeltaRThirdOctave.Select(d => d + oneri.ArtisDb).ToList(),
            L63 = orjinal.L63, L125 = orjinal.L125, L250 = orjinal.L250, L500 = orjinal.L500,
            L1k = orjinal.L1k, L2k = orjinal.L2k, L4k = orjinal.L4k,
            HasRealOktavData = orjinal.HasRealOktavData,
            SpektrumKaynagi = orjinal.SpektrumKaynagi,
            DarbeSpektrumKaynagi = orjinal.DarbeSpektrumKaynagi,
            Katmanlar = orjinal.Katmanlar.ToList()
        };

        // Eğer Darbe Sesi sonucu ise (veya döşeme/tavan yolu ise), IlgiliBolme geçici bir nesnedir
        // (DarbeSesiSonucuEkle'de senteziklenir). Asıl mekanların Zemin/Tavan özelliklerini
        // güncellemeliyiz.
        bool applied = false;
        if (satir.HesapTuru == "Darbe" || bolme.Ad.StartsWith("Döşeme") || bolme.Ad.StartsWith("Tavan"))
        {
            // P1 düzeltmesi (Faz 3) — eskiden 4 rol (KaynakMekan.Zemin/Tavan, AliciMekan.Zemin/Tavan)
            // nesne-referans eşitliğiyle taranıyordu. Eğer aynı katalog döşeme elemanı birden
            // fazla odanın zemin/tavanında kullanılıyorsa (kütüphaneden paylaşılan tip — yaygın),
            // bu döşemeyi iyileştirme önerisi KaynakMekan'ın KENDİ tavanını (bir üst kattan gelen,
            // tamamen alakasız bir darbe yolu) veya AliciMekan'ın KENDİ zeminini (bir alt kata giden,
            // yine alakasız bir yol) sessizce değiştirebiliyordu. Bu darbe yolu yalnız
            // KaynakMekan'ın zemini ile AliciMekan'ın tavanını kapsar — fiziksel olarak AYNI
            // döşeme plağıdır; yalnız bu ikisi güncellenir.
            if (bolme.KaynakMekan != null && bolme.KaynakMekan.ZeminYapiElemani == orjinal)
            {
                bolme.KaynakMekan.ZeminYapiElemani = klon;
                applied = true;
            }
            if (bolme.AliciMekan != null && bolme.AliciMekan.TavanYapiElemani == orjinal)
            {
                bolme.AliciMekan.TavanYapiElemani = klon;
                applied = true;
            }
        }
        else
        {
            bolme.YapiElemani = klon;
            applied = true;

            // Eğer ortak duvarsa, alıcı mekandaki simetrik bölmeyi de güncellemeliyiz.
            if (bolme.AliciMekan != null)
            {
                var karsiBolme = bolme.AliciMekan.Bolmeler.FirstOrDefault(b => b.AliciMekan == bolme.KaynakMekan && b.YapiElemani == orjinal);
                if (karsiBolme != null)
                    karsiBolme.YapiElemani = klon;
            }
        }
        if (!applied) return;

        DxfUyariMesaji = $"'{oneri.OneriAdi}' uygulandı (+{oneri.ArtisDb:F0} dB ek kaplama). Cmd+Z ile geri alabilirsiniz.";
        // Hesabı yenile
        HesaplaCommand.Execute(null);
    }
    public ObservableCollection<YapiElemani> KutuphaneElemanlari { get; } = new();
    public IEnumerable<YapiElemani> DuvarKutuphaneElemanlari =>
        KutuphaneElemanlari.Where(e => e.Tip == YapiElemaniTipi.Duvar);
    public void KutuphaneDuvarListesiniYenile() => OnPropertyChanged(nameof(DuvarKutuphaneElemanlari));
    public ObservableCollection<AciklikOnayar> OnayarElemanlari { get; } = new();
    public ObservableCollection<BagimsizBolum> BagimsizBolumler { get; } = new();
    public IReadOnlyList<BagimsizBolumTipi> BagimsizBolumTipleri { get; } =
        Enum.GetValues<BagimsizBolumTipi>();

    [ObservableProperty]
    private AciklikOnayar? _seciliOnayarElemani;

    /// Tüm katlardaki komşu duvarlar (AliciMekan != null olan Bolme'ler).
    public ObservableCollection<KomsuDuvarSatiri> KomsuDuvarlar { get; } = new();

    /// UX — Atama bekleyen duvar/oda iş listesi (atanmamış duvar, döşeme/tavan, bağımsız bölüm, sınıf).
    public ObservableCollection<AtamaBekleyenSatiri> AtamaBekleyenler { get; } = new();
    public bool AtamaBekleyenVarMi => AtamaBekleyenler.Count > 0;

    private AtamaBekleyenSatiri? _seciliAtamaBekleyen;
    public AtamaBekleyenSatiri? SeciliAtamaBekleyen
    {
        get => _seciliAtamaBekleyen;
        set
        {
            _seciliAtamaBekleyen = value;
            OnPropertyChanged();
            if (value != null) AtamaBekleyeneGit(value);
        }
    }

    /// Bekleyen satıra tıklanınca ilgili kata/odaya (ve mümkünse duvara) git.
    private void AtamaBekleyeneGit(AtamaBekleyenSatiri satir)
    {
        var kat = Katlar.FirstOrDefault(k => k.KatNo == satir.KatNo);
        if (kat != null) SeciliKat = kat;
        if (satir.Mekan != null) SeciliMekan = satir.Mekan;
        SeciliEksikBolme = satir.Bolme; // panel açılınca bu duvarı seçmek için
    }

    [ObservableProperty]
    private bool _raporTablosuGozukuyor = false;

    public string RaporTablosuToggleIkon => RaporTablosuGozukuyor ? "◀" : "▶";

    [RelayCommand]
    private void RaporTablosuToggle()
    {
        RaporTablosuGozukuyor = !RaporTablosuGozukuyor;
        if (RaporTablosuGozukuyor)
            SeciliSonucSatiri = null;
        OnPropertyChanged(nameof(RaporTablosuToggleIkon));
    }

    /// Panelin seçmesi için işaretlenen eksik bölme (atama bekleyenlerden navigasyon).
    [ObservableProperty]
    private Bolme? _seciliEksikBolme;

    /// UX (Issue 3) — Mekan listesinde gösterilecek odalar. Düşey birleşime dahil
    /// odalar yalnızca birleşimin EN ALT katındaki üyede tek kez gösterilir; diğer
    /// kat üyeleri gizlenir. RefreshGoruntulenenMekanlar ile tazelenir.
    public ObservableCollection<Mekan> GoruntulenenMekanlar { get; } = new();

    /// UX (Issue 3) — Seçili kat altında gösterilecek düşey birleşim açıklamaları.
    public ObservableCollection<string> SeciliKatBirlesimAciklamalari { get; } = new();
    public bool BirlesimAciklamasiVarMi => SeciliKatBirlesimAciklamalari.Count > 0;
    // UX — 20 seviye Undo + Redo yığını
    private const int MaxUndoLevel = 20;
    private readonly LinkedList<ProjeUndoSnapshot> _undoStack = new();
    private readonly LinkedList<ProjeUndoSnapshot> _redoStack = new();
    private Guid _belgeOturumId = Guid.NewGuid();
    private bool _degisiklikTakibiAskida;

    [ObservableProperty]
    private KomsuDuvarSatiri? _seciliKomsuDuvar;

    // ── Filtreli Atama Sistemi ──
    [ObservableProperty]
    private bool _filterDisCephe;

    [ObservableProperty]
    private bool _filterAyniIciDuvar;

    [ObservableProperty]
    private bool _filterKomsuBagimsizBolum;

    // Kaynak derece
    [ObservableProperty] private bool _filterDG;
    [ObservableProperty] private bool _filterOG;
    [ObservableProperty] private bool _filterYG;

    // Atama durumu
    [ObservableProperty] private bool _filterAtanmamis;
    [ObservableProperty] private bool _filterAtanmis;

    // Alıcı sınıfı
    [ObservableProperty] private bool _filterAliciI;
    [ObservableProperty] private bool _filterAliciII;
    [ObservableProperty] private bool _filterAliciIII;

    // Açıklık
    [ObservableProperty] private bool _filterAciklikVar;
    [ObservableProperty] private bool _filterAciklikYok;

    // Komşu duvarlar (AliciMekan != null)
    [ObservableProperty] private bool _filterSadeceKomsu;

    // Kat filtresi (seçili kat)
    [ObservableProperty] private bool _filterSadeceSeciliKat;

    // Uzunluk aralığı (m)
    [ObservableProperty] private string _filterUzunlukMin = "";
    [ObservableProperty] private string _filterUzunlukMax = "";

    // Alan aralığı (m²)
    [ObservableProperty] private string _filterAlanMin = "";
    [ObservableProperty] private string _filterAlanMax = "";

    // Mekan adı arama
    [ObservableProperty] private string _filterMekanAdi = "";

    // Bağımsız bölüm filtresi
    [ObservableProperty] private BagimsizBolum? _filterBagimsizBolum;

    // Menfez varlığı
    [ObservableProperty] private bool _filterMenfezVar;
    [ObservableProperty] private bool _filterMenfezYok;

    [ObservableProperty]
    private YapiElemani? _seciliAlamaMalzemesi;

    // Tüm bölmeler (komşu + dış cephe + iç) — Filtre ile Duvar Ata sekmesi için
    public ObservableCollection<KomsuDuvarSatiri> AtamaBolmeleri { get; } = new();
    public ObservableCollection<KomsuDuvarSatiri> FiltreLiKomsuDuvarlar { get; } = new();
    public ObservableCollection<KomsuDuvarSatiri> SeciliKomsuDuvarlarAtamaya { get; } = new();

    [ObservableProperty]
    private YapiElemani? _seciliKutuphaneElemani;

    [ObservableProperty]
    private string? _kutuphaneMesaji;

    [ObservableProperty]
    private bool _projeButonlariAcik = true;

    [ObservableProperty]
    private bool _katlarAcik = true;

    [ObservableProperty]
    private bool _katYuksekligiAcik = true;

    [ObservableProperty]
    private bool _mekanlarAcik = true;

    [ObservableProperty]
    private bool _mekanDetayAcik = true;

    [ObservableProperty]
    private bool _projeKilitli;

    public string ProjeKilitButonYazisi => ProjeKilitli ? "🔓 Projeyi Çöz" : "🔒 Projeyi Kilitle";
    public string ProjeKilitDurumu      => ProjeKilitli ? "KİLİTLİ" : "";
    public bool DuzenlemeEtkin          => !ProjeKilitli;

    partial void OnProjeKilitliChanged(bool value)
    {
        Proje.ProjeKilitli = value; // kalıcı: projeyle birlikte kaydedilir
        Kirlet();
        OnPropertyChanged(nameof(ProjeKilitButonYazisi));
        OnPropertyChanged(nameof(ProjeKilitDurumu));
        OnPropertyChanged(nameof(DuzenlemeEtkin));
        if (value)
        {
            EditablePolygon = null;
        }
    }

    [RelayCommand]
    private void ProjeKilitDegistir()
    {
        // Patlatma varken kilit manuel çözülemez; önce patlatma geri alınmalı.
        if (ProjeKilitli && Proje.PatlatilmisDosemeVar)
        {
            DuseyBirlesimMesaji = "Proje patlatma nedeniyle kilitli. Çözmek için önce döşeme/tavan patlatmasını geri alın.";
            return;
        }
        ProjeKilitli = !ProjeKilitli;
    }

    [RelayCommand]
    private async Task YeniProje(Window? parentWindow)
    {
        if (!await OnayKaydedilmemisDegisiklik(parentWindow, "Yeni Proje")) return;

        Proje = new BinaProje();
        Katlar.Clear();
        BagimsizBolumler.Clear();
        SonucSatirlari.Clear();
        EnKotuBolmeler.Clear();
        KomsuDuvarlar.Clear();
        TumMekanSiniflari.Clear();

        SeciliKat = null;
        SeciliMekan = null;
        SeciliKomsuDuvar = null;
        SeciliBolmePaneli = null;
        RenderSurfaces = null;
        RenderBackgroundSegments = null;
        EditablePolygon = null;
        NeighborSegments = null;
        DxfUyariMesaji = "Yeni proje oluşturuldu. Geometriyi M50'de hazırlayıp 'Bina Geometrisi Yükle' ile aktarın.";
        DogrulamaUyarisi = null;
        DuseyBirlesimMesaji = null;
        SonuclariGoster = false;
        ProjeOlcek = 1.0;
        _sonKaydedilenYol = null;
        ProjeKilitli = false;
        GecmisiTemizle(yeniBelgeOturumu: true);
        OtoKayitMesaji = null;

        Proje.BinaSinifi = BinaSinifi.S1;
        _seciliBinaSinifi = BinaSinifiHelper.BilgiAl(BinaSinifi.S1);
        OnPropertyChanged(nameof(SeciliBinaSinifi));
        SeciliBinaIslevi = BinaIsleviHelper.Varsayilan;
        SeciliAkustikPerformansSinifi = AkustikPerformansSinifiHelper.BilgiAl(AkustikPerformansSinifi.C);
        OnPropertyChanged(nameof(DisGurultuSpektrumMetni));
        OnPropertyChanged(nameof(SeciliDisGurultuSpektrumKaynagi));
        GuncelleDogrulamaUyarisi();
        TemizIsaretle(); // yeni proje temiz başlar
        _modelRevizyonu = 0;
        _hesapRevizyonu = -1;
        BildirRaporKimlikAlanlari();
    }

    public BagimsizBolum? SeciliMekanBagimsizBolum
    {
        get
        {
            var id = SeciliMekan?.BagimsizBolumId;
            return id is { } value
                ? BagimsizBolumler.FirstOrDefault(b => b.Id == value)
                : null;
        }
        set
        {
            if (ProjeKilitli || SeciliMekan == null) return;
            var yeniId = value?.Id;
            if (SeciliMekan.BagimsizBolumId == yeniId) return;

            SeciliMekan.BagimsizBolumId = yeniId;
            Kirlet();
            OnPropertyChanged();
            OnPropertyChanged(nameof(SeciliMekanBagimsizBolumAdi));
            GuncelleDogrulamaUyarisi();
            Guncelle3DGorunum();
        }
    }

    public string SeciliMekanBagimsizBolumAdi =>
        SeciliMekanBagimsizBolum?.Ad ?? "Atanmamış";

    [RelayCommand]
    private void BagimsizBolumEkle()
    {
        if (ProjeKilitli) return;
        var index = BagimsizBolumler.Count + 1;
        var bolum = new BagimsizBolum
        {
            Ad = $"Bağımsız Bölüm {index}",
            Tip = BagimsizBolumTipi.Konut,
            Renk = BagimsizBolumRengi(index)
        };
        BagimsizBolumler.Add(bolum);
        Proje.BagimsizBolumler.Add(bolum);
        SeciliMekanBagimsizBolum = bolum;
        OnPropertyChanged(nameof(BagimsizBolumler));
    }

    [RelayCommand]
    private void BagimsizBolumAtamasiniTemizle()
    {
        if (ProjeKilitli) return;
        SeciliMekanBagimsizBolum = null;
    }

    private static string BagimsizBolumRengi(int index)
    {
        string[] renkler =
        [
            "#2D6CDF", "#2A9D8F", "#E76F51", "#9B5DE5",
            "#F4A261", "#00B4D8", "#7CB342", "#C44569"
        ];
        return renkler[(Math.Max(1, index) - 1) % renkler.Length];
    }

    public void BeginUndoSnapshot()
    {
        if (!ProjeKilitli) PushUndoSnapshot();
    }

    public bool GeriAlVarMi => _undoStack.Count > 0;
    public bool YineleVarMi => _redoStack.Count > 0;
    public string GeriAlSayisi => _undoStack.Count > 0 ? $"({_undoStack.Count})" : "";
    public string YineleSayisi => _redoStack.Count > 0 ? $"({_redoStack.Count})" : "";

    [RelayCommand]
    private void GeriAl()
    {
        if (ProjeKilitli || _undoStack.Count == 0) return;
        var snapshot = _undoStack.Last!.Value;
        _undoStack.RemoveLast();
        var mevcut = AlGuncelSnapshot();
        _redoStack.AddLast(mevcut);
        _gecmisUygulaniyor = true;
        try
        {
            UygulaSnapshot(snapshot);
            Kirlet();
        }
        finally
        {
            _gecmisUygulaniyor = false;
        }
        BildirGeriAlYineleSayilari();
    }

    [RelayCommand]
    private void Yinele()
    {
        if (ProjeKilitli || _redoStack.Count == 0) return;
        var snapshot = _redoStack.Last!.Value;
        _redoStack.RemoveLast();
        var mevcut = AlGuncelSnapshot();
        PushSnapshotInternal(_undoStack, mevcut);
        _gecmisUygulaniyor = true;
        try
        {
            UygulaSnapshot(snapshot);
            Kirlet();
        }
        finally
        {
            _gecmisUygulaniyor = false;
        }
        BildirGeriAlYineleSayilari();
    }

    private ProjeUndoSnapshot AlGuncelSnapshot()
    {
        var katlar = Proje.Katlar.Select(k => new KatUndoData(
            k.KatNo,
            MekanKlonServisi.KlonlaListe(k.Mekanlar).ToList())).ToList();
        return new ProjeUndoSnapshot(
            _belgeOturumId,
            SeciliKat?.KatNo,
            SeciliMekan?.Id,
            katlar,
            DuseyBirlesimleriKlonla(Proje.DuseyMekanBirlesimleri),
            ProjeKilitli);
    }

    private void UygulaSnapshot(ProjeUndoSnapshot snapshot)
    {
        // Her snapshot belge oturumuna bağlıdır; eski bir projenin geçmişi yeni projeye
        // hiçbir koşulda uygulanamaz.
        if (snapshot.BelgeOturumId != _belgeOturumId) return;

        _degisiklikTakibiAskida = true;
        try
        {
            foreach (var katData in snapshot.Katlar)
            {
                var kat = Proje.Katlar.FirstOrDefault(k => k.KatNo == katData.KatNo);
                if (kat == null) continue;
                var mekanKlonlari = MekanKlonServisi.KlonlaListe(katData.Mekanlar);
                // Koleksiyonu değiştirmeden doldur: mevcut CollectionChanged/dirty abonelikleri
                // yaşamaya devam eder ve yeni mekanlara otomatik bağlanır.
                kat.Mekanlar.Clear();
                foreach (var mekan in mekanKlonlari) kat.Mekanlar.Add(mekan);
            }

            Proje.DuseyMekanBirlesimleri = DuseyBirlesimleriKlonla(snapshot.DuseyBirlesimleri);
            Proje.ProjeKilitli = snapshot.ProjeKilitli;
            ProjeKilitli = snapshot.ProjeKilitli;

            SeciliKat = snapshot.SeciliKatNo is { } katNo
                ? Katlar.FirstOrDefault(k => k.KatNo == katNo)
                : Katlar.FirstOrDefault();
            SeciliMekan = snapshot.SeciliMekanId is { } id
                ? SeciliKat?.Mekanlar.FirstOrDefault(m => m.Id == id)
                : SeciliKat?.Mekanlar.FirstOrDefault();

            SonucSatirlari.Clear();
            EnKotuBolmeler.Clear();
            RaporDahilSatirlar.Clear();
            SonuclariGoster = false;
            _hesapRevizyonu = -1;
            EditablePolygon = PolygonAsWorld(SeciliMekan);
            DuseyMekanBirlesimServisi.HacimleriGuncelle(Proje);
            GuncelleDogrulamaUyarisi();
            Guncelle3DGorunum();
            GuncelleDuseyBirlesimMesaji();
        }
        finally
        {
            _degisiklikTakibiAskida = false;
        }
    }

    private void BildirGeriAlYineleSayilari()
    {
        OnPropertyChanged(nameof(GeriAlVarMi));
        OnPropertyChanged(nameof(YineleVarMi));
        OnPropertyChanged(nameof(GeriAlSayisi));
        OnPropertyChanged(nameof(YineleSayisi));
    }

    private static void PushSnapshotInternal(LinkedList<ProjeUndoSnapshot> stack, ProjeUndoSnapshot snap)
    {
        stack.AddLast(snap);
        // Cap: en eski 20+ snapshot'ı at
        while (stack.Count > MaxUndoLevel) stack.RemoveFirst();
    }

    private void GecmisiTemizle(bool yeniBelgeOturumu)
    {
        _undoStack.Clear();
        _redoStack.Clear();
        if (yeniBelgeOturumu) _belgeOturumId = Guid.NewGuid();
        BildirGeriAlYineleSayilari();
    }

    private static List<DuseyMekanBirlesimi> DuseyBirlesimleriKlonla(IEnumerable<DuseyMekanBirlesimi> kaynak) =>
        kaynak.Select(b => new DuseyMekanBirlesimi
        {
            Id = b.Id,
            AktifMi = b.AktifMi,
            UstKatNo = b.UstKatNo,
            AltKatNo = b.AltKatNo,
            Uyeler = b.Uyeler.Select(u => new DuseyMekanBirlesimUyesi
            {
                KatNo = u.KatNo,
                MekanId = u.MekanId,
                TavanAcik = u.TavanAcik,
                DosemeAcik = u.DosemeAcik,
            }).ToList(),
            GeriAlmaUstKatMekanlari = b.GeriAlmaUstKatMekanlari == null
                ? null
                : MekanKlonServisi.KlonlaListe(b.GeriAlmaUstKatMekanlari).ToList(),
            GeriAlmaAltKatMekanlari = b.GeriAlmaAltKatMekanlari == null
                ? null
                : MekanKlonServisi.KlonlaListe(b.GeriAlmaAltKatMekanlari).ToList(),
        }).ToList();

    partial void OnSeciliKomsuDuvarChanged(KomsuDuvarSatiri? value)
    {
        if (value != null)
        {
            // İlgili katı/mekanı seç
            if (value.Kat != null) SeciliKat = value.Kat;
            SeciliMekan = value.KaynakMekan;
        }
        Guncelle3DGorunum();
    }

    public double DisGurultuDb
    {
        get => Proje.DisGurultuDb;
        set
        {
            if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value))
                return;

            if (Math.Abs(Proje.DisGurultuDb - value) < 1e-6)
                return;

            Proje.DisGurultuDb = value;
            Proje.DisGurultuKullaniciTarafindanDogrulandi = true;
            if (Proje.DisGurultuThirdOctave.Count != DisGurultuServisi.ExtendedThirdOctaveFreqler.Length)
                Proje.DisGurultuSpektrumKaynagi = SpektrumVeriKaynagi.Varsayilan;
            Kirlet();
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisGurultuDogrulandi));
            GuncelleDogrulamaUyarisi();
        }
    }

    /// Varsayılan dış gürültü değerinin proje girdisi olarak bilinçli doğrulandığı checkbox/API.
    public bool DisGurultuDogrulandi
    {
        get => Proje.DisGurultuKullaniciTarafindanDogrulandi;
        set
        {
            if (Proje.DisGurultuKullaniciTarafindanDogrulandi == value) return;
            Proje.DisGurultuKullaniciTarafindanDogrulandi = value;
            if (value && Proje.DisGurultuThirdOctave.Count != DisGurultuServisi.ExtendedThirdOctaveFreqler.Length)
                Proje.DisGurultuSpektrumKaynagi = SpektrumVeriKaynagi.Varsayilan;
            Kirlet();
            OnPropertyChanged();
            GuncelleDogrulamaUyarisi();
        }
    }

    // ── Rapor kimlik bilgileri (EK-10 10.2 kapak) — Proje modeline proxy ─────

    public string ProjeAdiMetni
    {
        get => Proje.ProjeAdi;
        set { if (Proje.ProjeAdi == value) return; Proje.ProjeAdi = value; Kirlet(); OnPropertyChanged(); }
    }

    public string ProjeAdresMetni
    {
        get => Proje.Adres;
        set { if (Proje.Adres == value) return; Proje.Adres = value; Kirlet(); OnPropertyChanged(); }
    }

    public string MusteriAdiMetni
    {
        get => Proje.MusteriAdi;
        set { if (Proje.MusteriAdi == value) return; Proje.MusteriAdi = value; Kirlet(); OnPropertyChanged(); }
    }

    public string SorumluKisiKurulusMetni
    {
        get => Proje.SorumluKisiKurulus;
        set { if (Proje.SorumluKisiKurulus == value) return; Proje.SorumluKisiKurulus = value; Kirlet(); OnPropertyChanged(); }
    }

    /// Proje Aç / Yeni Proje sonrası kapak alanı bildirimlerini tazeler.
    private void BildirRaporKimlikAlanlari()
    {
        OnPropertyChanged(nameof(ProjeAdiMetni));
        OnPropertyChanged(nameof(ProjeAdresMetni));
        OnPropertyChanged(nameof(MusteriAdiMetni));
        OnPropertyChanged(nameof(SorumluKisiKurulusMetni));
    }

    public IReadOnlyList<CepheMaruziyet> CepheMaruziyetleri { get; } = Enum.GetValues<CepheMaruziyet>();

    public IReadOnlyList<SpektrumKaynagiInfo> DisGurultuSpektrumKaynaklari { get; } =
    [
        new(SpektrumVeriKaynagi.Belirsiz, "Belirsiz", "Kaynak henüz belgelenmedi"),
        new(SpektrumVeriKaynagi.Olcum, "Ölçüm", "Saha/laboratuvar ölçümü"),
        new(SpektrumVeriKaynagi.Uretici, "Üretici", "Üretici veya akredite katalog verisi"),
        new(SpektrumVeriKaynagi.Model, "Model", "Hesap/model çıktısı"),
        new(SpektrumVeriKaynagi.Varsayilan, "Varsayılan", "Programın trafik spektrumu"),
    ];

    public SpektrumKaynagiInfo SeciliDisGurultuSpektrumKaynagi
    {
        get => DisGurultuSpektrumKaynaklari.FirstOrDefault(x => x.Deger == Proje.DisGurultuSpektrumKaynagi)
               ?? DisGurultuSpektrumKaynaklari[0];
        set
        {
            if (value == null || Proje.DisGurultuSpektrumKaynagi == value.Deger) return;
            Proje.DisGurultuSpektrumKaynagi = value.Deger;
            Kirlet();
            OnPropertyChanged();
        }
    }

    public CepheMaruziyet DisGurultuCepheMaruziyeti
    {
        get => Proje.DisGurultuCepheMaruziyeti;
        set
        {
            if (Proje.DisGurultuCepheMaruziyeti == value)
                return;

            Proje.DisGurultuCepheMaruziyeti = value;
            Kirlet();
            OnPropertyChanged();
        }
    }

    public string DisGurultuSpektrumMetni
    {
        get => Proje.DisGurultuThirdOctave.Count == 0
            ? ""
            : string.Join("; ", Proje.DisGurultuThirdOctave.Select(v =>
                v.ToString("0.#", System.Globalization.CultureInfo.CurrentCulture)));
        set
        {
            // Virgül Türkçe ondalık ayırıcıdır; bant ayırıcı olarak yalnız noktalı virgül
            // ve beyaz alan kullanılır. Böylece "65,5; 64,2" iki doğru değer olarak kalır.
            var tokens = (value ?? "").Split([';', ' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries);
            var parsed = new List<double>();
            var tumDegerlerGecerli = true;
            foreach (var token in tokens)
            {
                var ok = double.TryParse(token,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.CurrentCulture,
                    out var d);
                if (!ok)
                    ok = double.TryParse(token.Replace(',', '.'),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out d);
                if (ok && !double.IsNaN(d) && !double.IsInfinity(d))
                    parsed.Add(d);
                else
                    tumDegerlerGecerli = false;
            }

            var spektrumGecerli = tumDegerlerGecerli &&
                parsed.Count == DisGurultuServisi.ExtendedThirdOctaveFreqler.Length;
            Proje.DisGurultuThirdOctave = spektrumGecerli
                ? parsed
                : new List<double>();
            if (spektrumGecerli)
            {
                Proje.DisGurultuKullaniciTarafindanDogrulandi = true;
                // Yapıştırılan sayılar tek başına ölçüm olduklarını kanıtlamaz. Kullanıcı
                // kökeni ayrı alandan seçer; önceki varsayılan kaynak özel veri için artık
                // geçerli olmadığından Belirsiz'e düşürülür.
                if (Proje.DisGurultuSpektrumKaynagi is SpektrumVeriKaynagi.Varsayilan)
                    Proje.DisGurultuSpektrumKaynagi = SpektrumVeriKaynagi.Belirsiz;
            }
            else
            {
                Proje.DisGurultuSpektrumKaynagi = SpektrumVeriKaynagi.Varsayilan;
                if (!string.IsNullOrWhiteSpace(value))
                    Proje.DisGurultuKullaniciTarafindanDogrulandi = false;
            }
            if (!string.IsNullOrWhiteSpace(value) && !spektrumGecerli)
                DxfUyariMesaji = $"Dış gürültü spektrumu {DisGurultuServisi.ExtendedThirdOctaveFreqler.Length} bant içermelidir; değerler noktalı virgülle ayrılmalıdır.";
            Kirlet();
            OnPropertyChanged();
            OnPropertyChanged(nameof(SeciliDisGurultuSpektrumKaynagi));
            OnPropertyChanged(nameof(DisGurultuDogrulandi));
            GuncelleDogrulamaUyarisi();
        }
    }

    public IReadOnlyList<AkustikPerformansSinifiInfo> AkustikPerformansSiniflari { get; } =
        AkustikPerformansSinifiHelper.Tumü;

    public IReadOnlyList<BinaSinifiInfo> BinaSiniflari { get; } = BinaSinifiHelper.Tumü;

    private BinaSinifiInfo _seciliBinaSinifi = BinaSinifiHelper.BilgiAl(BinaSinifi.S1);
    public BinaSinifiInfo SeciliBinaSinifi
    {
        get => _seciliBinaSinifi;
        set
        {
            if (value == null || !SetProperty(ref _seciliBinaSinifi, value)) return;
            Proje.BinaSinifi = value.Sinif;
            Kirlet();
        }
    }

    private AkustikPerformansSinifiInfo _seciliAkustikPerformansSinifi =
        AkustikPerformansSinifiHelper.BilgiAl(AkustikPerformansSinifi.C);

    public AkustikPerformansSinifiInfo SeciliAkustikPerformansSinifi
    {
        get => _seciliAkustikPerformansSinifi;
        set
        {
            if (SetProperty(ref _seciliAkustikPerformansSinifi, value))
            {
                Proje.AkustikPerformansSinifi = value.Sinif;
                Kirlet();
            }
        }
    }

    // ── EK-2 Oda Sınıflandırma ──────────────────────────────────────────────

    /// Tüm kat+mekan satırları (SiniflandirmaPanel DataGrid kaynağı).
    public ObservableCollection<MekanSinifSatiri> TumMekanSiniflari { get; } = new();

    /// Bina işlevi listesi (ComboBox için).
    public IReadOnlyList<BinaIsleviInfo> BinaIslevleri { get; } = BinaIsleviHelper.Tumü;

    private BinaIsleviInfo _seciliBinaIslevi = BinaIsleviHelper.Varsayilan;
    public BinaIsleviInfo SeciliBinaIslevi
    {
        get => _seciliBinaIslevi;
        set
        {
            if (SetProperty(ref _seciliBinaIslevi, value))
            {
                Proje.BinaIslevi = value.Islevi;
                // İşlev değişince yönetmelik S1–S5 önerisini açıkça uygula; kullanıcı
                // SeciliBinaSinifi listesinden daha sonra bilinçli override yapabilir.
                var onerilen = BinaSinifiHelper.BilgiAl(BinaIslevindenSinif(value.Islevi));
                _seciliBinaSinifi = onerilen;
                Proje.BinaSinifi = onerilen.Sinif;
                OnPropertyChanged(nameof(SeciliBinaSinifi));
                Kirlet();
            }
        }
    }

    private static BinaSinifi BinaIslevindenSinif(BinaIslevi islevi) => islevi switch
    {
        BinaIslevi.Konut => BinaSinifi.S1,
        BinaIslevi.Konaklama or BinaIslevi.Yurt => BinaSinifi.S2,
        BinaIslevi.BuroIdari or BinaIslevi.TicariTesisler => BinaSinifi.S3,
        BinaIslevi.SanayiTesisleri => BinaSinifi.S5,
        BinaIslevi.EgitimTesisleri or BinaIslevi.SaglikVeYasliEvleri or
        BinaIslevi.KulturelTesisler or BinaIslevi.Terminaller or
        BinaIslevi.DiniTesisler or BinaIslevi.EglenceSporTesisleri => BinaSinifi.S4,
        _ => BinaSinifi.S1,
    };

    /// Tüm mekan satırlarını EK-2 servisine göre otomatik sınıflandırır.
    [RelayCommand]
    private void OtomatikTahmin()
    {
        if (ProjeKilitli) return;
        PushUndoSnapshot();
        var islevi = _seciliBinaIslevi.Islevi;
        foreach (var satir in TumMekanSiniflari)
        {
            var kategori = EK2SiniflandirmaServisi.TahminEt(islevi, satir.Ad, satir.KullanimTipi);
            if (kategori != null)
            {
                satir.KaynakDerecesi = kategori.KaynakDerecesi;
                satir.AliciSinifi    = kategori.AliciSinifi;
            }
        }

        GuncelleDogrulamaUyarisi();
        Guncelle3DGorunum();
    }

    private void RefreshTumMekanSiniflari()
    {
        TumMekanSiniflari.Clear();
        foreach (var kat in Proje.Katlar)
            foreach (var m in kat.Mekanlar)
                TumMekanSiniflari.Add(new MekanSinifSatiri(m, kat.KatNo, kat.Ad));
    }

    /// #3 — Sınıflandırma satırına tıklanınca ilgili kata/odaya gidip 2D'de seçer.
    [RelayCommand]
    private void MekanSinifSatiriSec(MekanSinifSatiri? satir)
    {
        if (satir?.Mekan == null) return;
        var kat = Katlar.FirstOrDefault(k => k.KatNo == satir.KatNo);
        if (kat != null) SeciliKat = kat;
        SeciliMekan = satir.Mekan;
    }

    public Action? RequestClose { get; set; }

    // ── Kaydedilmemiş değişiklik durumu (IsDirty) — codex8.md §10, maincodex §4.2 ──────

    private bool _isDirty;
    private bool _gecmisUygulaniyor;
    /// Kaydedilmemiş değişiklik var mı? Pencere başlığında `*` gösterilir; temizken
    /// Yeni Proje / Proje Aç / Ana Menü / pencere kapatma soru sormadan geçer.
    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (_isDirty == value) return;
            _isDirty = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PencereBasligi));
        }
    }

    public string PencereBasligi => (IsDirty ? "* " : "") + "EYE4PRO Acoustics - Bina Akustiği";

    /// Projeyi değiştiren her yolun çağırdığı işaret.
    public void Kirlet()
    {
        if (_degisiklikTakibiAskida) return;
        // Geri almadan sonra yapılan herhangi bir yeni düzenleme farklı bir geçmiş
        // dalı başlatır. Eski redo dalını tutmak, "Yinele" ile yeni veriyi ezebilirdi.
        if (!_gecmisUygulaniyor && _redoStack.Count > 0)
        {
            _redoStack.Clear();
            BildirGeriAlYineleSayilari();
        }
        IsDirty = true;
        _modelRevizyonu++;
        BildirSonucDurumu();
    }

    /// Kaydet / Yeni Proje / Proje Aç sonrası temiz işaretle.
    public void TemizIsaretle() => IsDirty = false;

    // ── Sonuç bayatlama takibi (codex8.md §10) ─────────────────────────────────
    // Kirlet() tetikleyen her veri değişikliği _modelRevizyonu'nu artırır; Hesapla()
    // başarıyla bittiğinde o anki değeri _hesapRevizyonu'na kopyalar. İkisi eşit değilse
    // ekrandaki sonuçlar artık girdileri yansıtmıyordur (malzeme/sınıflandırma/dış gürültü
    // değişmiş ama yeniden hesaplanmamış olabilir).
    private int _modelRevizyonu;
    private int _hesapRevizyonu = -1;

    public bool SonuclarGuncelMi => SonucSatirlari.Count > 0 && _hesapRevizyonu == _modelRevizyonu;

    /// UX — sonuç var ama girdiler değişti, yeniden hesap gerekiyor (bayat sonuç bandı).
    public bool SonuclarBayatMi => SonucSatirlari.Count > 0 && _hesapRevizyonu != _modelRevizyonu;

    /// UX — boş projede veya bayat sonuçlarla dışa aktarım düğmeleri etkin kalmamalı.
    public bool DisaAktarmaEtkin => SonuclarGuncelMi;

    private void BildirSonucDurumu()
    {
        OnPropertyChanged(nameof(SonuclarGuncelMi));
        OnPropertyChanged(nameof(SonuclarBayatMi));
        OnPropertyChanged(nameof(DisaAktarmaEtkin));
    }

    public void BagliGeometriDegisikliginiBildir(int yeniRevizyon)
    {
        // Dış .e4bina henüz projeye içe aktarılmadı; belge içeriği kirli değildir,
        // fakat mevcut sonuçların eski geometriye ait olduğu açıkça işaretlenmelidir.
        _modelRevizyonu++;
        BildirSonucDurumu();
        DxfUyariMesaji = $"Bağlı M50 geometrisi Rev. {yeniRevizyon} olarak değişti. " +
                         "Sonuçlar bayat; 'Bina Geometrisi Yükle' ile güncelleyin ve yeniden hesaplayın.";
    }

    /// IsDirty ise kullanıcıya Kaydet / Kaydetmeden Devam Et / İptal sorar.
    /// Devam edilebilirse (kaydedildi ya da kullanıcı kaydetmeden devamı seçti) true döner.
    /// Pencere yoksa (parentWindow null) sormadan devam eder — savunma amaçlı varsayılan.
    public async Task<bool> OnayKaydedilmemisDegisiklik(Window? parentWindow, string eylemAdi)
    {
        if (!IsDirty || parentWindow == null) return true;

        var dialog = new OnayDiyaloguWindow(
            "Kaydedilmemiş Değişiklikler",
            $"Projede kaydedilmemiş değişiklikler var. '{eylemAdi}' devam ederse bu değişiklikler kaybolur.\n\n" +
            "Önce kaydetmek ister misiniz?",
            tamamYazi: "Kaydet ve Devam Et", tamamSonuc: OnaySonucu.Tamam,
            iptalYazi: "İptal",
            ekstraYazi: "Kaydetmeden Devam Et", ekstraSonuc: OnaySonucu.KaydetmedenDevamEt);
        var sonuc = await dialog.ShowDialog<OnaySonucu>(parentWindow);
        switch (sonuc)
        {
            case OnaySonucu.Tamam:
                await Kaydet(parentWindow!);
                return !IsDirty; // kaydetme hata verirse IsDirty hâlâ true kalır → devam etme
            case OnaySonucu.KaydetmedenDevamEt:
                return true;
            default:
                return false;
        }
    }

    /// Yeni eklenen Kat/Mekan/Bolme'nin değişiklik bildirimlerini IsDirty'e bağlar.
    /// Menfez/Junction/Açıklık listeleri INotifyPropertyChanged uygulamadığından (List<T>,
    /// ObservableCollection değil) buradan yakalanamaz; onlar BolmeMalzemePanel'in
    /// OnAciklikDegisti/OnJunctionDegisti kancalarıyla ayrıca işaretlenir (bkz. BinaProjesiWindow.axaml.cs).
    private void KirletTakibiKur()
    {
        Katlar.CollectionChanged += (_, e) =>
        {
            Kirlet();
            if (e.NewItems != null) foreach (Kat k in e.NewItems) KatKirletAboneOl(k);
        };
        foreach (var k in Katlar) KatKirletAboneOl(k);

        BagimsizBolumler.CollectionChanged += (_, e) =>
        {
            Kirlet();
            if (e.NewItems != null)
                foreach (BagimsizBolum bolum in e.NewItems)
                    BagimsizBolumKirletAboneOl(bolum);
        };
        foreach (var bolum in BagimsizBolumler) BagimsizBolumKirletAboneOl(bolum);
    }

    private readonly HashSet<BagimsizBolum> _bagimsizBolumAbonelikleri = new();

    private void BagimsizBolumKirletAboneOl(BagimsizBolum bolum)
    {
        if (!_bagimsizBolumAbonelikleri.Add(bolum)) return;
        bolum.PropertyChanged += (_, _) =>
        {
            Kirlet();
            OnPropertyChanged(nameof(SeciliMekanBagimsizBolumAdi));
        };
    }

    private void KatKirletAboneOl(Kat kat)
    {
        kat.PropertyChanged += (_, _) => Kirlet();
        kat.Mekanlar.CollectionChanged += (_, e) =>
        {
            Kirlet();
            if (e.NewItems != null) foreach (Mekan m in e.NewItems) MekanKirletAboneOl(m);
        };
        foreach (var m in kat.Mekanlar) MekanKirletAboneOl(m);
    }

    private void MekanKirletAboneOl(Mekan mekan)
    {
        mekan.PropertyChanged += (_, _) => Kirlet();
        mekan.Bolmeler.CollectionChanged += (_, e) =>
        {
            Kirlet();
            if (e.NewItems != null) foreach (Bolme b in e.NewItems) b.PropertyChanged += (_, _) => Kirlet();
        };
        foreach (var b in mekan.Bolmeler) b.PropertyChanged += (_, _) => Kirlet();
    }

    public BinaProjesiViewModel()
    {
        Proje.ProjeAdi = "Yeni Bina Projesi";
        RefreshKutuphane();
        Katlar.CollectionChanged += (_, _) => OnPropertyChanged(nameof(KatlarTersSira));
        KirletTakibiKur();
        // Sonuç listesi boşalınca (ör. Yeni Proje) dışa aktarım düğmeleri de kapanmalı.
        SonucSatirlari.CollectionChanged += (_, _) => BildirSonucDurumu();
        BaslatOtoKayit(); // UX — Otomatik kaydetme timer'ı (5 dk)
    }

    [RelayCommand]
    private void Geri() => RequestClose?.Invoke();

    [RelayCommand]
    private void KutuphaneElemaniEkle()
    {
        if (ProjeKilitli) { KutuphaneMesaji = "Kilitli projede kütüphane değiştirilemez."; return; }
        var eleman = new YapiElemani
        {
            Ad = "Yeni Yapı Elemanı",
            Tip = YapiElemaniTipi.Duvar,
            Rw = 50,
            C = -1,
            Ctr = -4,
            FlankingDuzeltmesiDb = 2,
            Katmanlar =
            [
                new YapiKatmani { MalzemeAdi = "Yeni Yapı Elemanı", Kalinlik = 100, Yogunluk = 1000 }
            ]
        };
        YapiElemaniKutuphanesi.TamamlaOktavVerisi(eleman);
        KutuphaneElemanlari.Add(eleman);
        KutuphaneDuvarListesiniYenile();
        SeciliKutuphaneElemani = eleman;
        KutuphaneMesaji = "Yeni eleman eklendi. Değerleri düzenleyip Kaydet'e basın.";
        Kirlet();
    }

    [RelayCommand]
    private void KutuphaneElemaniSil()
    {
        if (ProjeKilitli) { KutuphaneMesaji = "Kilitli projede kütüphane değiştirilemez."; return; }
        if (SeciliKutuphaneElemani == null) return;
        KutuphaneElemanlari.Remove(SeciliKutuphaneElemani);
        KutuphaneDuvarListesiniYenile();
        SeciliKutuphaneElemani = KutuphaneElemanlari.FirstOrDefault();
        KutuphaneMesaji = "Eleman kaldırıldı. Kalıcı olması için Kaydet'e basın.";
        Kirlet();
    }

    [RelayCommand]
    private void KutuphaneKaydet() => KutuphaneKaydetCore();

    private bool KutuphaneKaydetCore()
    {
        if (ProjeKilitli) { KutuphaneMesaji = "Kilitli projede kütüphane değiştirilemez."; return false; }
        bool projedeCanliReferansVar = Proje.Katlar.SelectMany(k => k.Mekanlar).Any(m =>
            (m.ZeminYapiElemani != null && KutuphaneElemanlari.Contains(m.ZeminYapiElemani)) ||
            (m.TavanYapiElemani != null && KutuphaneElemanlari.Contains(m.TavanYapiElemani)) ||
            m.Bolmeler.Any(b =>
                (b.YapiElemani != null && KutuphaneElemanlari.Contains(b.YapiElemani)) ||
                b.Acikliklar.Any(a => a.YapiElemani != null && KutuphaneElemanlari.Contains(a.YapiElemani))));
        try
        {
            foreach (var eleman in KutuphaneElemanlari)
                YapiElemaniKutuphanesi.TamamlaOktavVerisi(eleman);
            YapiElemaniKutuphanesi.Degistir(KutuphaneElemanlari.ToList());
            KutuphaneMesaji = $"{KutuphaneElemanlari.Count} eleman kaydedildi.";
            KutuphaneDuvarListesiniYenile();
        }
        catch (Exception ex)
        {
            KutuphaneMesaji = $"Kütüphane kaydedilemedi: {ex.Message}";
            return false;
        }
        // Kütüphane nesneleri projede canlı referans olarak kullanılıyor. Rw/oktav vb.
        // düzenlenmişse hesap girdileri değişmiştir; sonuçları bayat ve belgeyi kirli işaretle.
        if (projedeCanliReferansVar) Kirlet();
        return true;
    }

    [RelayCommand]
    private async Task KutuphaneIceriAktar(Window parentWindow)
    {
        if (ProjeKilitli) { KutuphaneMesaji = "Kilitli projede kütüphane değiştirilemez."; return; }
        if (parentWindow == null) return;
        var files = await parentWindow.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Yapı Elemanı Kütüphanesini Excelden Aktar",
            AllowMultiple = false,
            FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType("Excel Dosyası") { Patterns = ["*.xlsx"] }]
        });
        var file = files.FirstOrDefault();
        if (file == null) return;

        try
        {
            var imported = YapiElemaniExcelServisi.ImportFromExcel(file.Path.LocalPath);
            foreach (var eleman in imported)
            {
                var existing = KutuphaneElemanlari.FirstOrDefault(e =>
                    e.Ad.Equals(eleman.Ad, StringComparison.OrdinalIgnoreCase) && e.Tip == eleman.Tip);
                if (existing != null)
                    KutuphaneElemanlari.Remove(existing);
                KutuphaneElemanlari.Add(eleman);
            }
            if (!KutuphaneKaydetCore()) return;
            KutuphaneDuvarListesiniYenile();
            SeciliKutuphaneElemani = imported.FirstOrDefault() ?? SeciliKutuphaneElemani;
            KutuphaneMesaji = $"{imported.Count} eleman içe aktarıldı.";
        }
        catch (Exception ex)
        {
            KutuphaneMesaji = $"İçe aktarma hatası: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task KutuphaneDisaAktar(Window parentWindow)
    {
        if (parentWindow == null) return;
        var file = await parentWindow.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Yapı Elemanı Kütüphanesini Excel'e Aktar",
            DefaultExtension = "xlsx",
            SuggestedFileName = "Yapi_Elemani_Kutuphanesi.xlsx",
            FileTypeChoices = [new Avalonia.Platform.Storage.FilePickerFileType("Excel Dosyası") { Patterns = ["*.xlsx"] }]
        });
        if (file == null) return;

        try
        {
            YapiElemaniExcelServisi.ExportToExcel(file.Path.LocalPath, KutuphaneElemanlari.ToList());
            KutuphaneMesaji = "Kütüphane Excel'e aktarıldı.";
        }
        catch (Exception ex)
        {
            KutuphaneMesaji = $"Dışa aktarma hatası: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task KutuphaneSablonKaydet(Window parentWindow)
    {
        if (parentWindow == null) return;
        var file = await parentWindow.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Yapı Elemanı Excel Şablonunu Kaydet",
            DefaultExtension = "xlsx",
            SuggestedFileName = "Yapi_Elemani_Kutuphanesi_Sablon.xlsx",
            FileTypeChoices = [new Avalonia.Platform.Storage.FilePickerFileType("Excel Dosyası") { Patterns = ["*.xlsx"] }]
        });
        if (file == null) return;

        try
        {
            YapiElemaniExcelServisi.GenerateTemplate(file.Path.LocalPath);
            KutuphaneMesaji = "Excel şablonu kaydedildi.";
        }
        catch (Exception ex)
        {
            KutuphaneMesaji = $"Şablon hatası: {ex.Message}";
        }
    }

    private void RefreshKutuphane()
    {
        KutuphaneElemanlari.Clear();
        foreach (var eleman in YapiElemaniKutuphanesi.TumElemanlar)
            KutuphaneElemanlari.Add(eleman);
        KutuphaneDuvarListesiniYenile();
        SeciliKutuphaneElemani = KutuphaneElemanlari.FirstOrDefault();

        OnayarElemanlari.Clear();
        foreach (var onayar in AciklikOnayarKutuphanesi.TumOnayarlar)
            OnayarElemanlari.Add(onayar);
        SeciliOnayarElemani = OnayarElemanlari.FirstOrDefault();
    }

    [RelayCommand]
    private void OnayarYeniPencere()
    {
        if (ProjeKilitli) { KutuphaneMesaji = "Kilitli projede kütüphane değiştirilemez."; return; }
        var onayar = new AciklikOnayar
        {
            Ad = "Yeni Pencere",
            Tip = BolmeAcikligiTipi.Pencere,
            GenislikCm = 100,
            YukseklikCm = 120
        };
        OnayarElemanlari.Add(onayar);
        SeciliOnayarElemani = onayar;
        Kirlet();
    }

    [RelayCommand]
    private void OnayarYeniKapi()
    {
        if (ProjeKilitli) { KutuphaneMesaji = "Kilitli projede kütüphane değiştirilemez."; return; }
        var onayar = new AciklikOnayar
        {
            Ad = "Yeni Kapı",
            Tip = BolmeAcikligiTipi.Kapi,
            GenislikCm = 90,
            YukseklikCm = 210
        };
        OnayarElemanlari.Add(onayar);
        SeciliOnayarElemani = onayar;
        Kirlet();
    }

    [RelayCommand]
    private void OnayarSil()
    {
        if (ProjeKilitli) { KutuphaneMesaji = "Kilitli projede kütüphane değiştirilemez."; return; }
        if (SeciliOnayarElemani == null) return;
        OnayarElemanlari.Remove(SeciliOnayarElemani);
        SeciliOnayarElemani = OnayarElemanlari.FirstOrDefault();
        Kirlet();
    }

    [RelayCommand]
    private void OnayarKaydet()
    {
        if (ProjeKilitli) { KutuphaneMesaji = "Kilitli projede kütüphane değiştirilemez."; return; }
        AciklikOnayarKutuphanesi.Kaydet(OnayarElemanlari.ToList());
        KutuphaneMesaji = $"{OnayarElemanlari.Count} açıklık önayarı kaydedildi.";
    }

    private static double SegmentMesafesi(Point2D p, Point2D a, Point2D b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var len2 = dx * dx + dy * dy;
        if (len2 < 1e-12) return Distance(p, a);
        var t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2;
        t = Math.Clamp(t, 0, 1);
        return Distance(p, new Point2D(a.X + t * dx, a.Y + t * dy));
    }

    private static double Distance(Point2D a, Point2D b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    // ── UX — Hover tooltip (viewport'ta bölmeye gelince Rw/limit/marj) ─────
    [ObservableProperty] private string? _hoverBolgeMetni;
    [ObservableProperty] private string? _hoverBolgeBaslik;
    [ObservableProperty] private string? _hoverBolgeRenk;
    public bool HoverGorunur => !string.IsNullOrEmpty(HoverBolgeMetni);

    partial void OnHoverBolgeMetniChanged(string? value) => OnPropertyChanged(nameof(HoverGorunur));

    /// Viewport'ta fare hareketi: tıklanabilir bir bölme bulunursa Rw/limit/marj metni hesaplanır.
    public void GuncelleHover(double worldX, double worldY)
    {
        if (SeciliKat == null) { TemizleHover(); return; }
        var off = SeciliKat.OriginOffset;
        double localX = worldX - off.X;
        double localY = worldY - off.Y;

        Bolme? enYakin = null;
        double enYakinMesafe = 0.40; // 40 cm eşik
        foreach (var m in SeciliKat.Mekanlar)
        {
            foreach (var b in m.Bolmeler)
            {
                if (!b.HasGeometry) continue;
                double d = SegmenteUzaklik(localX, localY, b.Baslangic, b.Bitis);
                if (d < enYakinMesafe) { enYakinMesafe = d; enYakin = b; }
            }
        }
        if (enYakin == null) { TemizleHover(); return; }

        var uyumServisi = new YonetmelikUyumServisi();
        var marj = uyumServisi.HesaplaMarjDb(Proje.BinaSinifi, enYakin, Proje.DisGurultuDb);
        var (_, limit) = uyumServisi.KontrolEtDetayli(Proje.BinaSinifi, enYakin, Proje.DisGurultuDb);

        string deger = enYakin.YapiElemani?.Tip == YapiElemaniTipi.Doseme
            ? $"L'nT,w {enYakin.ApparentLnTw:F1} dB"
            : enYakin.IsDisCephe
                ? $"DnT,A,tr {enYakin.Dntw:F1} dB"
                : $"R'w {enYakin.ApparentRw:F1} dB";

        var malzeme = enYakin.YapiElemani?.Ad ?? "(malzeme yok)";
        var marjStr = double.IsNaN(marj) ? "—" : $"{marj:+0.0;-0.0;0.0} dB";
        var limStr  = limit.HedefDeger > 0 ? $"Limit: {limit.HedefDeger:F0} {limit.Birim}" : "Limit: —";

        HoverBolgeBaslik = $"{enYakin.Ad}  ·  {malzeme}";
        HoverBolgeMetni  = $"{deger}   ·   {limStr}   ·   Marj: {marjStr}";
        HoverBolgeRenk   = double.IsNaN(marj) ? "#F1C40F"
                         : marj >=  3 ? "#27AE60"
                         : marj >=  1 ? "#2ECC71"
                         : marj >= -1 ? "#F39C12"
                         : marj >= -3 ? "#E67E22"
                                       : "#E74C3C";
    }

    public void TemizleHover()
    {
        HoverBolgeMetni = null;
        HoverBolgeBaslik = null;
        HoverBolgeRenk = null;
    }

    /// UX — Drag-drop ile malzeme atama: drop noktasına en yakın bölmeyi/elemanı bul ve uygula.
    /// Bolme tipine göre uygun hedef: Duvar/Tavan → bolme.YapiElemani; Doseme → mekan.ZeminYapiElemani.
    public void AtaMalzemeyeDragDrop(YapiElemani malzeme, double worldX, double worldY)
    {
        if (ProjeKilitli || SeciliKat == null) return;
        var off = SeciliKat.OriginOffset;
        double localX = worldX - off.X;
        double localY = worldY - off.Y;

        if (malzeme.Tip == YapiElemaniTipi.Doseme || malzeme.Tip == YapiElemaniTipi.Tavan)
        {
            // Drop noktasındaki mekana zemin/tavan ata
            var mekan = SeciliKat.Mekanlar.FirstOrDefault(m => PointInPolygon(localX, localY, m.Poligon));
            if (mekan == null) { DxfUyariMesaji = "Mekan içine bırakın."; return; }
            PushUndoSnapshot();
            if (malzeme.Tip == YapiElemaniTipi.Doseme) mekan.ZeminYapiElemani = malzeme;
            else mekan.TavanYapiElemani = malzeme;
            DxfUyariMesaji = $"'{malzeme.Ad}' {mekan.Ad}'a atandı.";
            Kirlet();
            Guncelle3DGorunum();
            GuncelleDogrulamaUyarisi();
            return;
        }

        if (malzeme.Tip is not (YapiElemaniTipi.Duvar or YapiElemaniTipi.Pencere or YapiElemaniTipi.Kapi))
        {
            DxfUyariMesaji = "Bu yapı elemanı duvar veya açıklık hedefinde kullanılamaz.";
            return;
        }

        // Diğer tipler (Duvar/Pencere/Kapı): en yakın bölmeye uygula
        Bolme? enYakin = null;
        double enYakinMesafe = 0.50;
        foreach (var m in SeciliKat.Mekanlar)
        {
            foreach (var b in m.Bolmeler)
            {
                if (!b.HasGeometry) continue;
                double d = SegmenteUzaklik(localX, localY, b.Baslangic, b.Bitis);
                if (d < enYakinMesafe) { enYakinMesafe = d; enYakin = b; }
            }
        }
        if (enYakin == null) { DxfUyariMesaji = "Yakında uygun bölme bulunamadı."; return; }
        if (malzeme.Tip == YapiElemaniTipi.Duvar)
        {
            PushUndoSnapshot();
            enYakin.YapiElemani = malzeme;
            if (enYakin.AliciMekan != null)
            {
                var karsiBolme = enYakin.AliciMekan.Bolmeler.FirstOrDefault(b =>
                    b.AliciMekan == enYakin.KaynakMekan);
                if (karsiBolme != null) karsiBolme.YapiElemani = malzeme;
            }
            DxfUyariMesaji = $"'{malzeme.Ad}' '{enYakin.Ad}' bölmesine atandı.";
        }
        else
        {
            // Pencere/kapı: en yakın açıklığa uygula (varsa)
            BolmeAcikligi? enYakinAciklik = null;
            double enYakinAcikMesafe = 0.50;
            foreach (var a in enYakin.Acikliklar)
            {
                double d = SegmenteUzaklik(localX, localY, a.Baslangic, a.Bitis);
                if (d < enYakinAcikMesafe) { enYakinAcikMesafe = d; enYakinAciklik = a; }
            }
            if (enYakinAciklik == null) { DxfUyariMesaji = "Yakında uygun açıklık bulunamadı."; return; }
            var beklenenTip = enYakinAciklik.Tip == BolmeAcikligiTipi.Pencere
                ? YapiElemaniTipi.Pencere
                : YapiElemaniTipi.Kapi;
            if (malzeme.Tip != beklenenTip)
            {
                DxfUyariMesaji = $"{enYakinAciklik.Tip} açıklığına yalnız {beklenenTip} tipinde eleman atanabilir.";
                return;
            }
            PushUndoSnapshot();
            enYakinAciklik.YapiElemani = malzeme;
            DxfUyariMesaji = $"'{malzeme.Ad}' açıklığa atandı.";
        }
        Kirlet();
        Guncelle3DGorunum();
        GuncelleDogrulamaUyarisi();
    }

    private static double SegmenteUzaklik(double px, double py, Point2D a, Point2D b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double len2 = dx * dx + dy * dy;
        if (len2 < 1e-12) return Math.Sqrt((px - a.X) * (px - a.X) + (py - a.Y) * (py - a.Y));
        double t = ((px - a.X) * dx + (py - a.Y) * dy) / len2;
        t = Math.Clamp(t, 0, 1);
        double cx = a.X + dx * t, cy = a.Y + dy * t;
        return Math.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
    }

    [RelayCommand] private void ToggleProjeButonlari()  => ProjeButonlariAcik = !ProjeButonlariAcik;
    [RelayCommand] private void ToggleKatlar()          => KatlarAcik = !KatlarAcik;
    [RelayCommand] private void ToggleKatYuksekligi()   => KatYuksekligiAcik = !KatYuksekligiAcik;
    [RelayCommand] private void ToggleMekanlar()        => MekanlarAcik = !MekanlarAcik;
    [RelayCommand] private void ToggleMekanDetay()      => MekanDetayAcik = !MekanDetayAcik;
    [RelayCommand] private void KatGorunumDegisti()  => Guncelle3DGorunum();

    private void KatlariYenidenNumaralandir()
    {
        for (int i = 0; i < Katlar.Count; i++)
            Katlar[i].KatNo = i + 1;
        Proje.Katlar.Clear();
        foreach (var kat in Katlar)
            Proje.Katlar.Add(kat);
    }

    /// Açıklık (kapı/pencere) ekleme/silme sonrası 3D görünümü günceller.
    public void ZorlaCizimYenile() => Guncelle3DGorunum();

    public bool TrySelectAciklik(double worldX, double worldY)
    {
        const double tol = 0.50;
        Kat? bestKat = null;
        Mekan? bestMekan = null;
        Bolme? bestBolme = null;
        BolmeAcikligi? bestAciklik = null;
        double bestDist = tol;

        foreach (var kat in Katlar)
        {
            var p = new Point2D(worldX - kat.OriginOffset.X, worldY - kat.OriginOffset.Y);
            foreach (var mekan in kat.Mekanlar)
            foreach (var bolme in mekan.Bolmeler)
            foreach (var aciklik in bolme.Acikliklar)
            {
                var d = SegmentMesafesi(p, aciklik.Baslangic, aciklik.Bitis);
                if (d >= bestDist) continue;
                bestDist = d;
                bestKat = kat;
                bestMekan = mekan;
                bestBolme = bolme;
                bestAciklik = aciklik;
            }
        }

        if (bestAciklik == null || bestBolme == null || bestMekan == null)
            return false;

        SeciliKat = bestKat;
        SeciliMekan = bestMekan;
        SeciliBolmePaneli = bestBolme;
        SeciliAciklikPaneli = bestAciklik;
        OnPropertyChanged(nameof(SeciliAciklikSegment));
        DxfUyariMesaji = "Açıklık seçildi. Bölme & Malzeme panelinden düzenleyebilir veya silebilirsiniz.";
        return true;
    }

    public void SeciliAciklikSil()
    {
        if (ProjeKilitli || SeciliBolmePaneli == null || SeciliAciklikPaneli == null) return;
        PushUndoSnapshot();
        SeciliBolmePaneli.Acikliklar.Remove(SeciliAciklikPaneli);
        SeciliAciklikPaneli = null;
        OnPropertyChanged(nameof(SeciliAciklikSegment));
        Guncelle3DGorunum();
    }

    private string? _sonKaydedilenYol; // son "Kaydet" yolu (Kaydet tekrar kullanır)

    // UX — Otomatik kaydetme (5 dk'da bir; son 5 anlık görüntü tutulur)
    private Avalonia.Threading.DispatcherTimer? _otoKayitTimer;
    private bool _otoKayitSuruyor;
    private bool _disposed;
    [ObservableProperty] private bool _otoKayitAcik = true;
    [ObservableProperty] private string? _otoKayitMesaji;

    partial void OnOtoKayitAcikChanged(bool value)
    {
        if (_disposed) return;
        if (value) BaslatOtoKayit();
        else DurdurOtoKayit();
    }

    public void BaslatOtoKayit()
    {
        if (_disposed || !OtoKayitAcik) return;
        DurdurOtoKayit();
        _otoKayitTimer = new Avalonia.Threading.DispatcherTimer
        {
            Interval = System.TimeSpan.FromMinutes(5)
        };
        _otoKayitTimer.Tick += OtoKayitTimerTick;
        _otoKayitTimer.Start();
    }

    private async void OtoKayitTimerTick(object? sender, EventArgs e) => await OtoKayitTick();

    public void DurdurOtoKayit()
    {
        if (_otoKayitTimer != null)
        {
            _otoKayitTimer.Stop();
            _otoKayitTimer.Tick -= OtoKayitTimerTick;
        }
        _otoKayitTimer = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DurdurOtoKayit();
        RequestClose = null;
        GC.SuppressFinalize(this);
    }

    private async Task OtoKayitTick()
    {
        if (_disposed || _otoKayitSuruyor || !IsDirty) return;
        _otoKayitSuruyor = true;
        try
        {
            string dir;
            string ad;
            if (!string.IsNullOrWhiteSpace(_sonKaydedilenYol))
            {
                dir = System.IO.Path.GetDirectoryName(_sonKaydedilenYol) ?? "";
                ad = System.IO.Path.GetFileNameWithoutExtension(_sonKaydedilenYol);
            }
            else
            {
                // İlk kez kaydedilmemiş projeler de kurtarılabilir olmalı.
                dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "EYE4PRO", "Autosave");
                var guvenliAd = string.Concat((string.IsNullOrWhiteSpace(Proje.ProjeAdi) ? "Yeni-Proje" : Proje.ProjeAdi)
                    .Select(c => System.IO.Path.GetInvalidFileNameChars().Contains(c) ? '-' : c));
                ad = $"{guvenliAd}-{Proje.Id:N}";
            }
            System.IO.Directory.CreateDirectory(dir);
            var damga = System.DateTime.Now.ToString("yyyyMMdd-HHmm");
            var autoPath = System.IO.Path.Combine(dir, $"{ad}.autosave-{damga}.e4p08");
            if (!await KaydetYola(autoPath, anaKayit: false, sessiz: true))
            {
                OtoKayitMesaji = "Otomatik kayıt başarısız; proje kaydedilmemiş durumda korunuyor.";
                return;
            }
            OtoKayitMesaji = $"Otomatik kayıt: {damga}";
            // Rotation: son 5 anlık görüntü dışındakileri sil
            try
            {
                var pattern = $"{ad}.autosave-*.e4p08";
                var files = System.IO.Directory.GetFiles(dir, pattern)
                    .OrderByDescending(f => System.IO.File.GetLastWriteTime(f))
                    .Skip(5).ToList();
                foreach (var f in files)
                    try { System.IO.File.Delete(f); } catch { /* yok say */ }
            }
            catch { /* yok say */ }
        }
        catch (System.Exception ex)
        {
            OtoKayitMesaji = $"Otomatik kayıt hatası: {ex.Message}";
        }
        finally
        {
            _otoKayitSuruyor = false;
        }
    }

    /// Projeyi son kaydedilen yola yazar; yol yoksa "Farklı Kaydet" gibi davranır.
    [RelayCommand]
    private async Task Kaydet(Window parentWindow)
    {
        if (!string.IsNullOrEmpty(_sonKaydedilenYol))
        {
            await KaydetYola(_sonKaydedilenYol, anaKayit: true);
            return;
        }
        await FarkliKaydet(parentWindow);
    }

    /// Her zaman dosya seçici açar.
    [RelayCommand]
    private async Task FarkliKaydet(Window parentWindow)
    {
        if (parentWindow == null) return;
        var sp = parentWindow.StorageProvider;
        if (sp == null) return;

        var file = await sp.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title             = "Projeyi Farklı Kaydet",
            SuggestedFileName = string.IsNullOrWhiteSpace(Proje.ProjeAdi) ? "bina_projesi" : Proje.ProjeAdi,
            DefaultExtension  = "e4p08",
            FileTypeChoices   = [ new Avalonia.Platform.Storage.FilePickerFileType("EYE4PRO M08 Bina Akustik") { Patterns = ["*.e4p08"] } ]
        });
        if (file == null) return;
        var yeniYol = file.Path.LocalPath;
        if (await KaydetYola(yeniYol, anaKayit: true))
            _sonKaydedilenYol = yeniYol;
    }

    private async Task<bool> KaydetYola(string yol, bool anaKayit, bool sessiz = false)
    {
        string? tmpYol = null;
        try
        {
            Proje.BagimsizBolumler = BagimsizBolumler.ToList();
            Proje.ModuleId = BinaProje.CurrentModuleId;
            Proje.SchemaVersion = BinaProje.CurrentSchemaVersion;
            // Kaydetme salt-okunur bir belge işlemidir; topoloji/komşuluk yeniden üretilmez.
            int komsuToplam = Proje.Katlar.SelectMany(k => k.Mekanlar)
                .SelectMany(m => m.Bolmeler).Count(b => b.AliciMekan != null);
            var opts = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented    = true,
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve,
                MaxDepth         = 256,
            };
            var json = System.Text.Json.JsonSerializer.Serialize(Proje, opts);

            // Atomik yazım: önce .tmp'ye yaz, sonra hedefe taşı. Yazım sırasında kesinti/çökme
            // olursa mevcut dosya bozulmaz — yarım yazılmış .tmp devreye hiç girmez.
            tmpYol = yol + ".tmp";
            await System.IO.File.WriteAllTextAsync(tmpYol, json);
            System.IO.File.Move(tmpYol, yol, overwrite: true);

            int dxfSegmentToplam = Proje.Katlar.Sum(k => k.RawDxfSegments.Count);
            if (!sessiz)
                DxfUyariMesaji = $"Kaydedildi: {System.IO.Path.GetFileName(yol)} — {komsuToplam} komşu duvar, {dxfSegmentToplam} DXF çizgisi saklandı.";
            // Otomatik yedek ayrı bir dosyadır; kullanıcının kaydedilmemiş durumunu değiştirmez.
            if (anaKayit) TemizIsaretle();
            return true;
        }
        catch (Exception ex)
        {
            if (tmpYol != null)
                try { System.IO.File.Delete(tmpYol); } catch { /* en iyi çaba */ }
            if (!sessiz) DxfUyariMesaji = $"Kaydetme hatası: {ex.Message}";
            return false;
        }
    }

    /// Kaydedilmiş JSON projeyi açar ve mevcut projeyi sıfırlar.
    [RelayCommand]
    private async Task ProjeAc(Window parentWindow)
    {
        if (parentWindow == null) return;
        if (!await OnayKaydedilmemisDegisiklik(parentWindow, "Proje Aç")) return;

        var sp = parentWindow.StorageProvider;
        if (sp == null) return;

        var files = await sp.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title            = "Proje Aç",
            AllowMultiple    = false,
            FileTypeFilter   = [ new Avalonia.Platform.Storage.FilePickerFileType("EYE4PRO M08 Bina Akustik") { Patterns = ["*.e4p08"] } ]
        });
        var file = files.FirstOrDefault();
        if (file == null) return;

        try
        {
            var json = await System.IO.File.ReadAllTextAsync(file.Path.LocalPath);
            if (!BinaProjeMigrator.DosyaKimliginiDogrula(json, file.Path.LocalPath, out var kimlikHatasi))
            {
                DxfUyariMesaji = $"Proje açılmadı: {kimlikHatasi}";
                return;
            }
            var opts = new System.Text.Json.JsonSerializerOptions
            {
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve,
                MaxDepth         = 256,
            };
            var yuklenen = System.Text.Json.JsonSerializer.Deserialize<BinaProje>(json, opts);
            if (yuklenen == null) { DxfUyariMesaji = "Proje dosyası okunamadı."; return; }

            // Eski/eksik JSON alanlarını (açık null listeler dahil) güvenli varsayılanlarla
            // tamamla — aşağıdaki Proje = yuklenen ataması sonrası NullReferenceException'ı önler.
            BinaProjeMigrator.Normalize(yuklenen);

            // Açış kapısı: patlatılmış döşeme/tavan içeren proje KİLİTLİ olarak açılır.
            // İstenirse patlatma geri alınıp düzenlenebilir halde açılabilir.
            if (yuklenen.PatlatilmisDosemeVar)
            {
                var diyalog = new OnayDiyaloguWindow(
                    "Patlatılmış Döşeme Var",
                    "Bu projede geri alınmamış döşeme/tavan patlatması var.\n\n" +
                    "Proje KİLİTLİ olarak açılır (görüntülenebilir, düzenlenemez). Düzenlemek için " +
                    "patlatmayı geri alıp açabilir ya da uygulamada 'Patlatmayı Geri Al' ile sonradan çözebilirsiniz.",
                    tamamYazi: "Kilitli Aç", tamamSonuc: OnaySonucu.Tamam,
                    iptalYazi: "İptal",
                    ekstraYazi: "Patlatmayı geri al ve aç", ekstraSonuc: OnaySonucu.GeriAlVeAc);
                var karar = await diyalog.ShowDialog<OnaySonucu>(parentWindow);
                if (karar == OnaySonucu.Iptal)
                {
                    DxfUyariMesaji = "Proje açılmadı.";
                    return;
                }
                if (karar == OnaySonucu.GeriAlVeAc)
                {
                    DuseyMekanBirlesimServisi.TumPatlatmalariGeriAl(yuklenen);
                    yuklenen.ProjeKilitli = false;
                }
                else
                {
                    // Kilitli Aç: patlatma korunur, kilit garanti altına alınır.
                    yuklenen.ProjeKilitli = true;
                }
            }

            // Tüm dosya/legacy DXF hazırlığını mevcut belgeye dokunmadan tamamla. Bu bölümde
            // hata olursa açık proje aynen kalır.
            int dxfSegmentToplam = 0;
            var eksikDxfKatlari = new List<int>();
            foreach (var kat in yuklenen.Katlar)
            {
                if (kat.RawDxfSegments.Count == 0 &&
                    !string.IsNullOrWhiteSpace(kat.DxfFilePath) &&
                    System.IO.File.Exists(kat.DxfFilePath))
                {
                    try
                    {
                        var parser = new DxfKatPlaniParser();
                        kat.RawDxfSegments = parser.ExtractRawSegmentsFromFile(kat.DxfFilePath);
                    }
                    catch
                    {
                        eksikDxfKatlari.Add(kat.KatNo);
                    }
                }

                if (kat.RawDxfSegments.Count == 0) eksikDxfKatlari.Add(kat.KatNo);
                dxfSegmentToplam += kat.RawDxfSegments.Count;
                DxfKatPlaniParser.NormalizeOrigin(kat);
            }

            // Mevcut projeyi temizle
            _degisiklikTakibiAskida = true;
            try
            {
                Katlar.Clear();
                BagimsizBolumler.Clear();
                SeciliMekan = null;
                SeciliKat = null;
                SonucSatirlari.Clear();
                EnKotuBolmeler.Clear();
                RaporDahilSatirlar.Clear();

                // Deserialize edilen proje doğrudan atanır — hiçbir alan kaybolmaz.
                Proje = yuklenen;
                foreach (var bolum in Proje.BagimsizBolumler) BagimsizBolumler.Add(bolum);
                foreach (var kat in Proje.Katlar) Katlar.Add(kat);

                _seciliBinaIslevi = BinaIsleviHelper.BilgiAl(Proje.BinaIslevi);
                OnPropertyChanged(nameof(SeciliBinaIslevi));
                _seciliBinaSinifi = BinaSinifiHelper.BilgiAl(Proje.BinaSinifi);
                OnPropertyChanged(nameof(SeciliBinaSinifi));
                _seciliAkustikPerformansSinifi =
                    AkustikPerformansSinifiHelper.BilgiAl(Proje.AkustikPerformansSinifi);
                OnPropertyChanged(nameof(SeciliAkustikPerformansSinifi));
                ProjeKilitli = Proje.ProjeKilitli;
                ProjeOlcek = 1.0;
            }
            finally
            {
                _degisiklikTakibiAskida = false;
            }

            _sonKaydedilenYol = file.Path.LocalPath;
            GecmisiTemizle(yeniBelgeOturumu: true);
            OnPropertyChanged(nameof(DisGurultuDb));
            OnPropertyChanged(nameof(DisGurultuCepheMaruziyeti));
            OnPropertyChanged(nameof(DisGurultuSpektrumMetni));
            OnPropertyChanged(nameof(SeciliDisGurultuSpektrumKaynagi));
            OnPropertyChanged(nameof(DisGurultuDogrulandi));
            BildirRaporKimlikAlanlari();
            // UX — Mekanların BirlesikHacimM3 alanlarını birleşimlerden tazele
            DuseyMekanBirlesimServisi.HacimleriGuncelle(Proje);
            SeciliKat   = Katlar.FirstOrDefault();
            SeciliMekan = SeciliKat?.Mekanlar.FirstOrDefault();
            OnPropertyChanged(nameof(SeciliMekanBagimsizBolum));
            OnPropertyChanged(nameof(SeciliMekanBagimsizBolumAdi));
            DxfUyariMesaji = eksikDxfKatlari.Count == 0
                ? $"Proje açıldı: {file.Name} — {dxfSegmentToplam} DXF çizgisi yüklendi."
                : $"Proje açıldı: {file.Name}; DXF arka planı eksik katlar: {string.Join(", ", eksikDxfKatlari.Distinct())}. Projeyi yeniden kaydederseniz yeni format DXF çizgilerini saklar.";

            // 4.3 — Eski (M50 bağlantısız) projenin geometrisi otomatik .e4bina'ya aktarılır.
            var geometriMigrasyonuYapildi = false;
            try
            {
                if (Kst.Acoustics.Core.BuildingAcoustics.Geometry
                        .BinaGeometriM08ExportServisi.EskiProjeyiMigrateEt(Proje, _sonKaydedilenYol) is { } geoNot)
                {
                    geometriMigrasyonuYapildi = true;
                    DxfUyariMesaji += $"  · {geoNot}";
                }
            }
            catch (Exception migEx)
            {
                DxfUyariMesaji += $"  ⚠ Geometri migrasyonu başarısız: {migEx.Message}";
            }

            // Bağlı .e4bina revizyon denetimi: geometri güncellenmişse uyar.
            if (Kst.Acoustics.Core.BuildingAcoustics.Geometry
                    .BinaGeometriM08ExportServisi.GuncellikUyarisi(Proje) is { } geoUyari)
                DxfUyariMesaji += $"  ⚠ {geoUyari}";

            Guncelle3DGorunum();
            // Açılan projede henüz hesap yapılmadı; SonucSatirlari zaten boşaltıldı (satır ~1725).
            _modelRevizyonu = 0;
            _hesapRevizyonu = -1;
            if (geometriMigrasyonuYapildi)
            {
                // Yeni M50 bağlantısı/kopyası yalnız bellekte kalmamalı; kapanışta kaydet
                // uyarısı vererek migrasyonu M08 belgesine kalıcılaştır.
                Kirlet();
                DxfUyariMesaji += "  · M50 bağlantısını kalıcılaştırmak için projeyi kaydedin.";
            }
            else
            {
                TemizIsaretle(); // değişmeyen açılmış proje temiz başlar
            }
        }
        catch (Exception ex)
        {
            DxfUyariMesaji = $"Proje açma hatası: {ex.Message}";
        }
    }

    /// 4.3 — M50 .e4bina geometrisini projeye yükler: mekanlar/bölmeler güncellenir
    /// (Id kararlı), açıklıklar yeniden yerleştirilir; malzeme atamaları, akustik
    /// sınıflar ve bağımsız bölüm bağları korunur.
    [RelayCommand]
    private async Task BinaGeometrisiYukle(Window parentWindow)
    {
        if (parentWindow?.StorageProvider is not { } sp) return;
        if (ProjeKilitli)
        {
            DxfUyariMesaji = "Kilitli projede bina geometrisi değiştirilemez.";
            return;
        }

        var files = await sp.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Bina Geometrisi Yükle (.e4bina)",
            AllowMultiple = false,
            FileTypeFilter = [ new Avalonia.Platform.Storage.FilePickerFileType("EYE4PRO Bina Geometrisi (.e4bina.json)")
                { Patterns = ["*.e4bina.json"] } ]
        });
        var path = files.FirstOrDefault()?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            var geo = Kst.Acoustics.Core.Bina.Services.BinaGeometriServisi.Ac(path);
            // Adapter mevcut projeyi yerinde değiştirir. İşlemi derin bir aday kopyada
            // tamamlayıp yalnız başarıdan sonra atomik olarak UI/proje modeline geçir.
            Proje.BagimsizBolumler = BagimsizBolumler.ToList();
            var adayProje = ProjeyiDerinKlonla(Proje);
            var sonuc = Kst.Acoustics.Core.BuildingAcoustics.Geometry
                .BinaGeometriMekanAdapteri.Uygula(adayProje, geo, path);

            var adayMekanIdleri = adayProje.Katlar.SelectMany(k => k.Mekanlar).Select(m => m.Id).ToHashSet();
            var akustikVeriyleSilinecek = Proje.Katlar
                .SelectMany(k => k.Mekanlar.Select(m => (Kat: k, Mekan: m)))
                .Where(x => !adayMekanIdleri.Contains(x.Mekan.Id) && MekandaM08AkustikVerisiVar(x.Mekan))
                .ToList();
            if (akustikVeriyleSilinecek.Count > 0)
            {
                var ornekler = string.Join("\n", akustikVeriyleSilinecek.Take(8)
                    .Select(x => $"• {x.Kat.Ad} / {x.Mekan.Ad}"));
                if (akustikVeriyleSilinecek.Count > 8)
                    ornekler += $"\n• … ve {akustikVeriyleSilinecek.Count - 8} mekan daha";
                var onay = new OnayDiyaloguWindow(
                    "M50 Mekanları Silmiş",
                    $"Yeni geometri, M08 akustik ataması taşıyan {akustikVeriyleSilinecek.Count} mekanı artık içermiyor:\n\n" +
                    $"{ornekler}\n\nDevam edilirse bu mekanların malzeme, sınıf, tesisat ve emilim verileri projeden kaldırılır.",
                    tamamYazi: "Silerek Yükle", tamamSonuc: OnaySonucu.Tamam,
                    iptalYazi: "İptal");
                if (await onay.ShowDialog<OnaySonucu>(parentWindow) != OnaySonucu.Tamam)
                {
                    DxfUyariMesaji = "Bina geometrisi yüklenmedi; mevcut M08 akustik verileri korundu.";
                    return;
                }
            }

            _degisiklikTakibiAskida = true;
            try
            {
                Katlar.Clear();
                BagimsizBolumler.Clear();
                SonucSatirlari.Clear();
                EnKotuBolmeler.Clear();
                RaporDahilSatirlar.Clear();
                Proje = adayProje;
                foreach (var bolum in Proje.BagimsizBolumler) BagimsizBolumler.Add(bolum);
                foreach (var kat in Proje.Katlar) Katlar.Add(kat);
                SeciliKat = Katlar.FirstOrDefault();
                SeciliMekan = SeciliKat?.Mekanlar.FirstOrDefault();
                SonuclariGoster = false;
                _hesapRevizyonu = -1;
                ProjeOlcek = 1.0;
                DuseyMekanBirlesimServisi.HacimleriGuncelle(Proje);
            }
            finally
            {
                _degisiklikTakibiAskida = false;
            }
            GecmisiTemizle(yeniBelgeOturumu: true);
            Kirlet();

            string ozet = $"Bina geometrisi yüklendi (Rev. {geo.GeometriRevizyon}): " +
                          $"{sonuc.YeniKat} yeni, {sonuc.GuncellenenKat} güncellenen kat.";
            if (sonuc.Uyarilar.Count > 0)
                ozet += "  ⚠ " + string.Join(" ", sonuc.Uyarilar);
            DxfUyariMesaji = ozet;
            GuncelleDogrulamaUyarisi();
            Guncelle3DGorunum();
        }
        catch (Exception ex)
        {
            DxfUyariMesaji = $"Bina geometrisi yüklenemedi: {ex.Message}";
        }
    }

    private static bool MekandaM08AkustikVerisiVar(Mekan mekan) =>
        mekan.BagimsizBolumId != null ||
        !string.IsNullOrWhiteSpace(mekan.KaynakDerecesi) ||
        !string.IsNullOrWhiteSpace(mekan.AliciSinifi) ||
        mekan.EmilimAlani > 0 ||
        mekan.TesisatSurekliDb > 0 ||
        mekan.TesisatKesikliDb > 0 ||
        mekan.ZeminYapiElemani != null ||
        mekan.TavanYapiElemani != null ||
        mekan.Bolmeler.Any(b => b.YapiElemani != null ||
                                b.Acikliklar.Any(a => a.YapiElemani != null) ||
                                b.Menfezler.Count > 0 ||
                                b.Junctions.Count > 0);

    private static BinaProje ProjeyiDerinKlonla(BinaProje proje)
    {
        var opts = new System.Text.Json.JsonSerializerOptions
        {
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve,
            MaxDepth = 256,
        };
        var json = System.Text.Json.JsonSerializer.Serialize(proje, opts);
        var klon = System.Text.Json.JsonSerializer.Deserialize<BinaProje>(json, opts)
            ?? throw new InvalidOperationException("Proje işlem kopyası oluşturulamadı.");
        BinaProjeMigrator.Normalize(klon);
        return klon;
    }

    /// Dışa aktarım arka planda sürerken UI düzenlenebilse bile rapor tek, tutarlı
    /// model revizyonunu okusun diye UI iş parçacığında derin anlık görüntü alır.
    public BinaProje DisaAktarmaAnlikGoruntusuAl()
    {
        var klon = ProjeyiDerinKlonla(Proje);
        // Hesap DTO'ları belge JSON'una bilinçli olarak yazılmaz; rapor anlık görüntüsü
        // ise mevcut hesap revizyonunun spektral/oktav çıktısını da korumalıdır.
        var kaynakBolmeler = Proje.Katlar.SelectMany(k => k.Mekanlar)
            .SelectMany(m => m.Bolmeler).GroupBy(b => b.Id).ToDictionary(g => g.Key, g => g.First());
        foreach (var bolme in klon.Katlar.SelectMany(k => k.Mekanlar).SelectMany(m => m.Bolmeler))
        {
            if (!kaynakBolmeler.TryGetValue(bolme.Id, out var kaynak)) continue;
            bolme.OktavHavaSesiSonucu = kaynak.OktavHavaSesiSonucu;
            bolme.OktavDarbeSesiSonucu = kaynak.OktavDarbeSesiSonucu;
            bolme.UcuncuOktavHavaSesiSonucu = kaynak.UcuncuOktavHavaSesiSonucu;
            bolme.DisCepheSpektralSonuc = kaynak.DisCepheSpektralSonuc;
        }
        return klon;
    }

    [RelayCommand]
    private void Hesapla()
    {
        GuncelleDogrulamaUyarisi();
        if (OnKontrolKritikHataVarMi)
        {
            SonucSatirlari.Clear();
            EnKotuBolmeler.Clear();
            RaporDahilSatirlar.Clear();
            SonuclariGoster = false;
            _hesapRevizyonu = -1;
            BildirSonucDurumu();
            DxfUyariMesaji = "Hesaplama başlatılmadı: ön kontroldeki kritik eksikleri tamamlayın.";
            return;
        }

        var havaServisi = new HavaSesiHesapServisi();
        var darbeServisi = new DarbeSesiHesapServisi();
        var darbeYoluServisi = new KatlarArasiDarbeYoluServisi();
        var disGurultuServisi = new DisGurultuServisi();
        var uyumServisi = new YonetmelikUyumServisi();
        var guvenServisi = new HesapGuvenilirlikServisi();

        SonucSatirlari.Clear();

        // Önbellekteki eski hesap sonuçlarını temizle, aksi halde malzeme değişse bile eski sonuç kullanılır
        foreach (var k in Proje.Katlar)
        {
            foreach (var m in k.Mekanlar)
            {
                foreach (var b in m.Bolmeler)
                {
                    b.ApparentRw = 0;
                    b.ApparentLnw = 0;
                    b.ApparentLnTw = 0;
                    b.Dntw = 0;
                    b.OktavHavaSesiSonucu = null;
                    b.OktavDarbeSesiSonucu = null;
                    b.UcuncuOktavHavaSesiSonucu = null;
                    b.DisCepheSpektralSonuc = null;
                }
            }
        }

        // Önce tüm bölmeleri tek tek hesapla, sonra oda başına en zayıf arayüzü seç
        var perBolme = new System.Collections.Generic.List<SonucSatiriViewModel>();

        foreach (var kat in Proje.Katlar)
        {
            foreach (var mekan in kat.Mekanlar)
            {
                if (HesapDisiMekan(mekan))
                    continue;

                var disCepheler = mekan.Bolmeler.Where(b => b.IsDisCephe).ToList();
                if (disCepheler.Count > 0)
                    disGurultuServisi.UygulaDisCepheYalitimi(mekan, disCepheler, Proje.DisGurultuCepheMaruziyeti);

                foreach (var bolme in mekan.Bolmeler)
                {
                    if (HesapDisiBolme(bolme))
                        continue;

                    double deger;
                    string birim;

                    OktavBantSonucu? oktavHava = null;
                    OktavDarbeSonucu? oktavDarbe = null;

                    if (bolme.YapiElemani?.Tip == YapiElemaniTipi.Doseme)
                    {
                        darbeServisi.HesaplaApparentLnw(bolme);
                        // Yönetmelik Madde 10(3): döşeme değerlendirmesi L'nT,w ile yapılır.
                        deger = darbeServisi.HesaplaApparentLnTw(bolme);
                        birim = "dB L'nT,w";
                        oktavDarbe = darbeServisi.HesaplaOktavApparentLn(bolme);
                    }
                    else
                    {
                        havaServisi.HesaplaUcuncuOktavApparentR(bolme);
                        if (!bolme.IsDisCephe)
                            havaServisi.HesaplaDntw(bolme);

                        birim = bolme.IsDisCephe ? "dB DnT,A,tr" : "dB DnT,A";
                        oktavHava = havaServisi.HesaplaOktavApparentR(bolme);
                        deger = 0;
                    }

                    var (uyum, limit) = uyumServisi.KontrolEtDetayli(
                        Proje.BinaSinifi,
                        Proje.AkustikPerformansSinifi,
                        bolme,
                        Proje.DisGurultuDb);
                    DisCepheSpektralSonuc? disSpektral = null;
                    // Resmî cephe değerlendirmesinde eşleşmeyen kullanım tipi sabit 35 dBA'ya
                    // düşmez; açıkça VeriEksik olur.
                    double? icHedefDb = bolme.IsDisCephe
                        ? EK4IcGurultuServisi.IcGurultuHedefiBul(Proje, bolme.KaynakMekan ?? mekan)
                        : null;
                    if (bolme.IsDisCephe && Proje.DisGurultuDb > 0)
                    {
                        disSpektral = disGurultuServisi.HesaplaSpektralDisCephe(Proje, bolme);
                        if (disSpektral != null)
                            deger = disSpektral.DnTATr;
                        if (icHedefDb is not { } icHedef)
                        {
                            uyum = UyumDurumu.VeriEksik;
                            limit.HedefDeger = 0;
                            limit.Aciklama = "EK-4 Tablo 4.1 için mekan kullanım tipi eşleşmedi; iç gürültü hedefi belirlenemedi.";
                        }
                        else
                        {
                            var hedef = Math.Max(limit.HedefDeger, Proje.DisGurultuDb - icHedef);
                            limit.HedefDeger = hedef;
                            limit.Aciklama = $"Dış gürültü Lgag {Proje.DisGurultuDb:F0} dBA; iç hedef {icHedef:F0} dBA (EK-4 Tablo 4.1); DnT,A,tr ≥ {hedef:F0} dB; D2m,nT,50 raporlanır";
                            if (disSpektral == null)
                            {
                                uyum = UyumDurumu.VeriEksik;
                                limit.Aciklama += "; cephe bileşenlerinin geçerli spektral verisi eksik";
                            }
                            else
                            {
                                uyum = deger > 0 && deger >= hedef && disSpektral.IcToplam <= icHedef
                                    ? UyumDurumu.Gecti
                                    : UyumDurumu.Kaldi;
                            }
                        }
                    }
                    bool isDoseme = bolme.YapiElemani?.Tip == YapiElemaniTipi.Doseme;
                    if (!isDoseme && !bolme.IsDisCephe && deger <= 0)
                        deger = uyumServisi.HesaplaOlcumDegeri(bolme, limit);

                    // Margin: pozitif → geçti, negatif → kaldı
                    double marjDb = uyum == UyumDurumu.VeriEksik
                        ? double.NaN
                        : isDoseme
                            ? limit.HedefDeger - deger   // L'n,w: düşük = iyi
                            : deger - limit.HedefDeger;  // R'w / DnT,w: yüksek = iyi
                    if (disSpektral != null && icHedefDb is { } hedefIc)
                        marjDb = Math.Min(marjDb, hedefIc - disSpektral.IcToplam);

                    var (durumText, durumColor) = uyum switch
                    {
                        UyumDurumu.VeriEksik => ("VERİ EKSİK", "#F1C40F"),
                        _ => (marjDb >= 3   ? ("GEÇTİ (+{0:F0})", "#27AE60") :
                              marjDb >= 1   ? ("GEÇTİ (+{0:F0})", "#2ECC71") :
                              marjDb >= -1  ? ("SINIRDA",         "#F39C12") :
                              marjDb >= -3  ? ("KALDI ({0:F0})",  "#E67E22") :
                                             ("KALDI ({0:F0})",  "#E74C3C"))
                    };

                    if (uyum != UyumDurumu.VeriEksik && durumText.Contains("{0"))
                        durumText = string.Format(durumText, Math.Abs(marjDb));

                    const double sigma = HavaSesiHesapServisi.HesapBelirsizligi;
                    var guven = guvenServisi.Degerlendir(Proje, bolme);
                    var spektrumKaynak = bolme.IsDisCephe
                        ? disSpektral?.Kaynak ?? BinaAkustikRaporServisi.KaynakEtiketi(Proje.DisGurultuSpektrumKaynagi.ToString())
                        : isDoseme
                            ? BinaAkustikRaporServisi.KaynakEtiketi(
                                oktavDarbe?.Kaynak ?? bolme.YapiElemani?.DarbeSpektrumKaynagi.ToString())
                            : BinaAkustikRaporServisi.KaynakEtiketi(
                                bolme.UcuncuOktavHavaSesiSonucu?.Kaynak ?? oktavHava?.Kaynak ??
                                bolme.YapiElemani?.SpektrumKaynagi.ToString());

                    perBolme.Add(new SonucSatiriViewModel
                    {
                        SonucId           = bolme.Id,
                        Kat               = kat.KatNo.ToString(),
                        KatAdi            = string.IsNullOrWhiteSpace(kat.Ad) ? $"Kat {kat.KatNo}" : kat.Ad,
                        Mekan             = mekan.Ad,
                        Bolme             = bolme.Ad,
                        IlgiliBolme       = bolme,
                        BagimsizBolum     = BolmeBagimsizBolumEtiketi(bolme),
                        KomsulukTipi      = bolme.KomsulukTipiEtiketi,
                        DisGurultuDb      = bolme.IsDisCephe ? Proje.DisGurultuDb : null,
                        IcGurultuDb       = disSpektral?.IcToplam ?? (bolme.IsDisCephe && deger > 0 ? disGurultuServisi.HesaplaIcGurultu(Proje.DisGurultuDb, deger) : null),
                        IcHedefDb         = bolme.IsDisCephe ? icHedefDb : null,
                        D2mNt50Db         = disSpektral?.D2mNt50,
                        SpektrumKaynak    = spektrumKaynak,
                        GuvenPuani        = guven.Puan,
                        GuvenSeviyesi     = guven.Seviye,
                        GuvenRengi        = guven.Renk,
                        GuvenOzeti        = guven.Ozet,
                        Deger             = deger,
                        Birim             = birim,
                        HesapTuru         = birim.Contains("L'n") ? "Darbe" : bolme.IsDisCephe ? "Cephe" : "Hava",
                        Limit             = limit.HedefDeger,
                        Belirsizlik       = sigma,
                        MarjDb            = marjDb,
                        Durum             = durumText,
                        DurumColor        = durumColor,
                        OktavBantlari     = OktavBantGorunumu.OlusturListesi(oktavHava, oktavDarbe),
                        VeriEksik         = uyum == UyumDurumu.VeriEksik,
                        Uyum              = uyum,
                        RezonansAciklama  = bolme.YapiElemani?.RezonansAciklama ?? "",
                        YonetmelikRef     = limit.Aciklama ?? ""
                    });
                }

                // P1 düzeltmesi (Faz 3) — bir üst oda birden fazla alt odaya taşıyorsa (gerçek
                // poligon kesişimiyle tespit edilir) TÜMÜ işlenir; eskiden yalnız en fazla
                // örtüşen tek alt mekan alınıyordu, ikinci/üçüncü alıcı atlanıyordu.
                foreach (var darbeYolu in darbeYoluServisi.BulAltMekanlar(Proje.Katlar, kat, mekan))
                {
                    if (!DuseyMekanBirlesimServisi.AyniBirlesimdeAcik(
                            Proje,
                            darbeYolu.AliciKat.KatNo,
                            darbeYolu.AliciMekan.Id,
                            darbeYolu.KaynakKat.KatNo,
                            darbeYolu.KaynakMekan.Id))
                    {
                        DarbeSesiSonucuEkle(darbeYolu, darbeServisi, uyumServisi, guvenServisi, perBolme);
                    }
                }
            }
        }

        // Oda başına en zayıf arayüzü seç: hava sesi için minimum marj, darbe sesi için minimum marj
        // Tüm bölmeler arasında en kötüsü yönetmelikle kıyaslanan tek değer.
        var katsiraDict = Proje.Katlar.ToDictionary(k => k.KatNo.ToString(), k => k.KatNo);
        var gruplar = perBolme.GroupBy(r => (r.Kat, r.Mekan));
        foreach (var grup in gruplar.OrderBy(g => katsiraDict.TryGetValue(g.Key.Kat, out var n) ? n : 0)
                                    .ThenBy(g => g.Key.Mekan))
        {
            // İç hava ve dış cephe farklı yönetmelik yollarıdır; aynı "Hava" havuzunda
            // birleştirmek birinin oda özetinden kaybolmasına yol açıyordu.
            var havaIcin = grup.Where(r => r.HesapTuru == "Hava" && !r.VeriEksik).ToList();
            var cepheIcin = grup.Where(r => r.HesapTuru == "Cephe" && !r.VeriEksik).ToList();
            OzetSonucEkle(havaIcin, "Hava");
            OzetSonucEkle(cepheIcin, "Cephe");

            // Darbe sesi (L'n,w) — en zayıf = minimum marj
            var darbeIcin = grup.Where(r => r.Birim.Contains("L'n") && !r.VeriEksik).ToList();
            if (darbeIcin.Count > 0)
            {
                var enZayif = darbeIcin.OrderBy(r => r.MarjDb).First();
                SonucSatirlari.Add(new SonucSatiriViewModel
                {
                    SonucId          = enZayif.SonucId,
                    Kat              = enZayif.Kat,
                    KatAdi           = enZayif.KatAdi,
                    HesapTuru        = "Darbe",
                    Mekan            = enZayif.Mekan,
                    Bolme            = darbeIcin.Count == 1
                        ? enZayif.Bolme
                        : $"{enZayif.Bolme}  (en zayıf / {darbeIcin.Count})",
                    // P1 düzeltmesi (Faz 3) — eskiden burada set edilmiyordu; darbe sonuç
                    // satırlarında "Uygula" ve flanking-yol gösterimi IlgiliBolme'ye ihtiyaç
                    // duyduğundan (bkz. codex8.md §12) tamamen sessizce çalışmıyordu.
                    IlgiliBolme      = enZayif.IlgiliBolme,
                    Deger            = enZayif.Deger,
                    Birim            = enZayif.Birim,
                    Limit            = enZayif.Limit,
                    Belirsizlik      = enZayif.Belirsizlik,
                    MarjDb           = enZayif.MarjDb,
                    Durum            = enZayif.Durum,
                    DurumColor       = enZayif.DurumColor,
                    VeriEksik        = false,
                    Uyum             = enZayif.Uyum,
                    RezonansAciklama = enZayif.RezonansAciklama,
                    YonetmelikRef    = enZayif.YonetmelikRef,
                    OktavBantlari    = enZayif.OktavBantlari,
                    BagimsizBolum    = enZayif.BagimsizBolum,
                    KomsulukTipi     = enZayif.KomsulukTipi,
                    DisGurultuDb     = enZayif.DisGurultuDb,
                    IcGurultuDb      = enZayif.IcGurultuDb,
                    IcHedefDb        = enZayif.IcHedefDb,
                    D2mNt50Db        = enZayif.D2mNt50Db,
                    SpektrumKaynak   = enZayif.SpektrumKaynak,
                    GuvenPuani       = enZayif.GuvenPuani,
                    GuvenSeviyesi    = enZayif.GuvenSeviyesi,
                    GuvenRengi       = enZayif.GuvenRengi,
                    GuvenOzeti       = enZayif.GuvenOzeti
                });
            }

            // Geçerli sonucu olmayan her yönetmelik yolunu ayrı VeriEksik satırıyla
            // görünür tut; iç hava eksikliği cepheyi (veya tersi) saklamasın.
            if (havaIcin.Count == 0 && grup.FirstOrDefault(r => r.HesapTuru == "Hava") is { } eksikHava)
                SonucSatirlari.Add(eksikHava);
            if (cepheIcin.Count == 0 && grup.FirstOrDefault(r => r.HesapTuru == "Cephe") is { } eksikCephe)
                SonucSatirlari.Add(eksikCephe);
            if (darbeIcin.Count == 0 && grup.FirstOrDefault(r => r.HesapTuru == "Darbe") is { } eksikDarbe)
                SonucSatirlari.Add(eksikDarbe);
        }

        // ── EK-5 tesisat + EK-6 reverberasyon satırları (mekan bazlı, gruplama dışı) ──
        foreach (var kat in Proje.Katlar)
        {
            foreach (var mekan in kat.Mekanlar)
            {
                if (HesapDisiMekan(mekan)) continue;
                var katAdi = string.IsNullOrWhiteSpace(kat.Ad) ? $"Kat {kat.KatNo}" : kat.Ad;

                var (surekli, kesikli) = EK5TesisatServisi.Degerlendir(Proje, mekan);
                foreach (var (tur, ts) in new[] { ("sürekli", surekli), ("kesikli", kesikli) })
                {
                    if (ts == null) continue;
                    var (dText, dColor) = ts.Durum switch
                    {
                        UyumDurumu.VeriEksik => ("VERİ EKSİK", "#F1C40F"),
                        UyumDurumu.Gecti => ($"GEÇTİ (+{Math.Abs(ts.MarjDb):F0})", "#2ECC71"),
                        _ => ($"KALDI ({Math.Abs(ts.MarjDb):F0})", "#E74C3C"),
                    };
                    SonucSatirlari.Add(new SonucSatiriViewModel
                    {
                        SonucId = RaporSonucIdleri.Tesisat(mekan.Id, kesikli: tur == "kesikli"),
                        Kat = kat.KatNo.ToString(), KatAdi = katAdi, Mekan = mekan.Ad,
                        HesapTuru = "Tesisat",
                        Bolme = $"Servis ekipmanı ({tur})",
                        Deger = ts.GirilenDb, Birim = ts.Birim,
                        Limit = ts.LimitDb, MarjDb = ts.MarjDb,
                        Durum = dText, DurumColor = dColor,
                        VeriEksik = ts.Durum == UyumDurumu.VeriEksik,
                        Uyum = ts.Durum,
                        YonetmelikRef = ts.SatirAdi,
                        SpektrumKaynak = "Kullanıcı beyanı",
                        GuvenPuani = 85,
                        GuvenSeviyesi = "Beyan",
                        GuvenRengi = "#2E8B57",
                        GuvenOzeti = "Tesisat düzeyi kullanıcı tarafından beyan edilmiştir.",
                    });
                }

                var rt = EK6ReverberasyonServisi.Degerlendir(Proje, mekan);
                if (rt != null)
                {
                    var marj = rt.MarjSn ?? 0;
                    var varsayilanRt = mekan.EmilimAlani <= 0;
                    var (dText, dColor) = rt.Durum switch
                    {
                        UyumDurumu.VeriEksik => ("VERİ EKSİK", "#F1C40F"),
                        UyumDurumu.Gecti => ($"GEÇTİ (+{Math.Abs(marj):F1}s)", "#2ECC71"),
                        _ => ($"KALDI ({Math.Abs(marj):F1}s)", "#E74C3C"),
                    };
                    SonucSatirlari.Add(new SonucSatiriViewModel
                    {
                        SonucId = RaporSonucIdleri.Reverberasyon(mekan.Id),
                        Kat = kat.KatNo.ToString(), KatAdi = katAdi, Mekan = mekan.Ad,
                        HesapTuru = "Çınlama",
                        Bolme = rt.OzelTasarim
                            ? "Reverberasyon — özel tasarım / uzman kararı"
                            : "Reverberasyon (Sabine ön tahmin)",
                        Deger = rt.TahminiTSn, Birim = "s",
                        Limit = rt.TMaxSn ?? 0, MarjDb = marj,
                        Durum = dText, DurumColor = dColor,
                        VeriEksik = rt.Durum == UyumDurumu.VeriEksik,
                        Uyum = rt.Durum,
                        YonetmelikRef = $"Tablo 6.1: {rt.SatirAdi}" +
                                        (rt.OzelTasarim
                                            ? " — özel tasarım: uzman kararı gerekli"
                                            : rt.UzmanKarariGerekir ? " — C-D dışı sınıf: uzman kararı" : ""),
                        SpektrumKaynak = mekan.EmilimAlani > 0 ? "Kullanıcı beyanı" : "Varsayılan/tahmin",
                        GuvenPuani = varsayilanRt ? 30 : 75,
                        GuvenSeviyesi = varsayilanRt ? "Düşük" : "Orta",
                        GuvenRengi = varsayilanRt ? "#A66B00" : "#28708A",
                        GuvenOzeti = varsayilanRt
                            ? "Emilim alanı beyan edilmedi; bu satır yalnız ön tahmindir."
                            : "Sabine ön tahmini, kullanıcı emilim alanına dayanır.",
                    });
                }
            }
        }

        // Top-3 en kötü oda (en düşük marj) — Çınlama satırları hariç: marjı saniye
        // cinsinden olduğundan dB marjlarıyla sıralamada karışır (0.1 s ≪ 3 dB görünür).
        EnKotuBolmeler.Clear();
        foreach (var row in SonucSatirlari
            .Where(r => !r.VeriEksik && r.HesapTuru != "Çınlama")
            .OrderBy(r => r.MarjDb)
            .Take(3))
        {
            EnKotuBolmeler.Add(row);
        }

        GuncelleDogrulamaUyarisi();
        UygulaBaselineDegerleri();
        SonuclariGoster = true;
        RefreshRaporDahilSatirlar();
        Guncelle3DGorunum();

        // P0 — sonuçlar artık bu model revizyonuna kadar günceldir; sonraki her Kirlet()
        // çağrısı (malzeme/sınıflandırma/dış gürültü/geometri değişikliği) bunu bayatlatır.
        _hesapRevizyonu = _modelRevizyonu;
        BildirSonucDurumu();
    }

    private void OzetSonucEkle(IReadOnlyList<SonucSatiriViewModel> adaylar, string hesapTuru)
    {
        if (adaylar.Count == 0) return;
        var enZayif = adaylar.OrderBy(r => r.MarjDb).First();
        SonucSatirlari.Add(new SonucSatiriViewModel
        {
            SonucId = enZayif.SonucId,
            Kat = enZayif.Kat,
            KatAdi = enZayif.KatAdi,
            HesapTuru = hesapTuru,
            Mekan = enZayif.Mekan,
            Bolme = adaylar.Count == 1 ? enZayif.Bolme : $"{enZayif.Bolme}  (en zayıf / {adaylar.Count})",
            IlgiliBolme = enZayif.IlgiliBolme,
            Deger = enZayif.Deger,
            Birim = enZayif.Birim,
            Limit = enZayif.Limit,
            Belirsizlik = enZayif.Belirsizlik,
            MarjDb = enZayif.MarjDb,
            Durum = enZayif.Durum,
            DurumColor = enZayif.DurumColor,
            VeriEksik = enZayif.VeriEksik,
            RezonansAciklama = enZayif.RezonansAciklama,
            YonetmelikRef = enZayif.YonetmelikRef,
            OktavBantlari = enZayif.OktavBantlari,
            BagimsizBolum = enZayif.BagimsizBolum,
            KomsulukTipi = enZayif.KomsulukTipi,
            DisGurultuDb = enZayif.DisGurultuDb,
            IcGurultuDb = enZayif.IcGurultuDb,
            IcHedefDb = enZayif.IcHedefDb,
            D2mNt50Db = enZayif.D2mNt50Db,
            SpektrumKaynak = enZayif.SpektrumKaynak,
            GuvenPuani = enZayif.GuvenPuani,
            GuvenSeviyesi = enZayif.GuvenSeviyesi,
            GuvenRengi = enZayif.GuvenRengi,
            GuvenOzeti = enZayif.GuvenOzeti,
            Uyum = enZayif.Uyum,
        });
    }

    private void DarbeSesiSonucuEkle(
        KatlarArasiDarbeYolu yol,
        DarbeSesiHesapServisi darbeServisi,
        YonetmelikUyumServisi uyumServisi,
        HesapGuvenilirlikServisi guvenServisi,
        System.Collections.Generic.List<SonucSatiriViewModel> hedefListe)
    {
        var dosemeBolmesi = new Bolme
        {
            // Deterministik Id — "Rapora Dahil" filtresi ve HTML rapor yeniden üretimi
            // arasında aynı darbe yolunun aynı kimliği taşıması için (rastgele değil).
            Id = KatlarArasiDarbeYoluServisi.DarbeYoluId(yol.KaynakMekan.Id, yol.AliciMekan.Id),
            Ad = $"Döşeme ({yol.KaynakMekan.Ad} → {yol.AliciMekan.Ad})",
            Alan = Math.Max(yol.OrtusenAlan, 0.01),
            YapiElemani = yol.Doseme,
            KaynakMekan = yol.KaynakMekan,
            AliciMekan = yol.AliciMekan
        };

        dosemeBolmesi.ApparentLnw = darbeServisi.HesaplaApparentLnw(dosemeBolmesi);
        // Yönetmelik Madde 10(3): döşeme değerlendirmesi L'nT,w ile yapılır.
        var deger = darbeServisi.HesaplaApparentLnTw(dosemeBolmesi);
        var oktavDarbe = darbeServisi.HesaplaOktavApparentLn(dosemeBolmesi);
        var (uyum, limit) = uyumServisi.KontrolEtDetayli(
            Proje.BinaSinifi,
            Proje.AkustikPerformansSinifi,
            dosemeBolmesi);

        var marjDb = limit.HedefDeger - deger;
        var (durumText, durumColor) = uyum switch
        {
            UyumDurumu.VeriEksik => ("VERİ EKSİK", "#F1C40F"),
            _ => (marjDb >= 3   ? ("GEÇTİ (+{0:F0})", "#27AE60") :
                  marjDb >= 1   ? ("GEÇTİ (+{0:F0})", "#2ECC71") :
                  marjDb >= -1  ? ("SINIRDA",         "#F39C12") :
                  marjDb >= -3  ? ("KALDI ({0:F0})",  "#E67E22") :
                                 ("KALDI ({0:F0})",  "#E74C3C"))
        };

        if (uyum != UyumDurumu.VeriEksik && durumText.Contains("{0"))
            durumText = string.Format(durumText, Math.Abs(marjDb));

        var guven = guvenServisi.Degerlendir(Proje, dosemeBolmesi);
        hedefListe.Add(new SonucSatiriViewModel
        {
            SonucId = dosemeBolmesi.Id,
            Kat = yol.AliciKat.KatNo.ToString(),
            KatAdi = string.IsNullOrWhiteSpace(yol.AliciKat.Ad) ? $"Kat {yol.AliciKat.KatNo}" : yol.AliciKat.Ad,
            Mekan = yol.AliciMekan.Ad,
            Bolme = $"Döşeme üstü: {yol.KaynakKat.KatNo}. kat / {yol.KaynakMekan.Ad}",
            BagimsizBolum = BolmeBagimsizBolumEtiketi(dosemeBolmesi),
            KomsulukTipi = dosemeBolmesi.KomsulukTipiEtiketi,
            Deger = deger,
            IlgiliBolme = dosemeBolmesi,
            Birim = "dB L'nT,w",
            HesapTuru = "Darbe",
            Limit = limit.HedefDeger,
            Belirsizlik = DarbeSesiHesapServisi.HesapBelirsizligi,
            MarjDb = marjDb,
            Durum = durumText,
            DurumColor = durumColor,
            VeriEksik = uyum == UyumDurumu.VeriEksik,
            Uyum = uyum,
            RezonansAciklama = yol.Doseme?.RezonansAciklama ?? "",
            GuvenPuani = guven.Puan,
            GuvenSeviyesi = guven.Seviye,
            GuvenRengi = guven.Renk,
            GuvenOzeti = guven.Ozet,
            SpektrumKaynak = BinaAkustikRaporServisi.KaynakEtiketi(
                oktavDarbe?.Kaynak ?? yol.Doseme?.DarbeSpektrumKaynagi.ToString()),
            OktavBantlari = OktavBantGorunumu.OlusturListesi(null, oktavDarbe)
        });
    }

    public ObservableCollection<DogrulamaSatiri> DogrulamaSatirlari { get; } = new();
    public bool DogrulamaSatirlariVarMi => DogrulamaSatirlari.Count > 0;
    /// Rapor/UI tarafından bağlanabilir tek ön-kontrol API'si.
    public bool OnKontrolKritikHataVarMi => DogrulamaSatirlari.Any(s => s.KritikMi);
    public bool OnKontrolBasarili => !OnKontrolKritikHataVarMi;
    public bool HesaplamaEtkin => Proje.Katlar.SelectMany(k => k.Mekanlar).Any() && !OnKontrolKritikHataVarMi;
    public string OnKontrolOzeti { get; private set; } = "Ön kontrol henüz çalıştırılmadı.";

    /// Hesap öncesi kontrol panelini (atanmamış bölme/döşeme/BB sayıları) yeniden hesaplar.
    /// Public — UI panellerinden de tetiklenebilir (örn. BolmeMalzemePanel "tümüne ata" sonrası).
    public void GuncelleDogrulamaUyarisi()
    {
        var aktifMekanlar = Proje.Katlar.SelectMany(k => k.Mekanlar).Where(m => !HesapDisiMekan(m)).ToList();
        var aktifBolmeler = aktifMekanlar
            .SelectMany(m => m.Bolmeler)
            .Where(b => !HesapDisiBolme(b))
            .ToList();
        int atanmamis = aktifBolmeler.Count(b => b.YapiElemani == null);
        int yanlisBolmeTipi = aktifBolmeler.Count(b => b.YapiElemani is { Tip: not YapiElemaniTipi.Duvar });
        int aciklikMalzemesiz = aktifBolmeler.Sum(b => b.Acikliklar.Count(a => a.YapiElemani == null));
        int yanlisAciklikTipi = aktifBolmeler.Sum(b => b.Acikliklar.Count(a =>
            a.YapiElemani != null &&
            ((a.Tip == BolmeAcikligiTipi.Pencere && a.YapiElemani.Tip != YapiElemaniTipi.Pencere) ||
             (a.Tip == BolmeAcikligiTipi.Kapi && a.YapiElemani.Tip != YapiElemaniTipi.Kapi))));
        int bolmesiz = aktifMekanlar.Count(m => !m.Bolmeler.Any(b => !HesapDisiBolme(b)));
        int dosemesiz = aktifMekanlar.Count(m => m.ZeminYapiElemani?.Tip != YapiElemaniTipi.Doseme);
        int tavansiz = aktifMekanlar.Count(m => m.TavanYapiElemani?.Tip != YapiElemaniTipi.Tavan);
        int bagimsizBolumsuz = aktifMekanlar.Count(m => m.BagimsizBolumId == null);
        int kullanimsiz = aktifMekanlar.Count(m => string.IsNullOrWhiteSpace(m.KullanimTipi));
        int hacimsiz = aktifMekanlar.Count(m => m.Alan <= 0 || m.Yukseklik <= 0 || m.Hacim <= 0);
        int siniflandirmasiz = aktifBolmeler.Count(b => b.IsDisCephe
            ? string.IsNullOrWhiteSpace(b.KaynakMekan?.AliciSinifi)
            : string.IsNullOrWhiteSpace(b.KaynakMekan?.KaynakDerecesi) ||
              string.IsNullOrWhiteSpace(b.AliciMekan?.AliciSinifi));

        DogrulamaSatirlari.Clear();
        if (Proje.Katlar.Count == 0)
            DogrulamaSatirlari.Add(new DogrulamaSatiri("🏢", "#E74C3C", "Kat/geometri bulunmuyor", 1, kritikMi: true));
        else if (aktifMekanlar.Count == 0)
            DogrulamaSatirlari.Add(new DogrulamaSatiri("▱", "#E74C3C", "Hesaba dahil mekan bulunmuyor", 1, kritikMi: true));
        if (bolmesiz > 0)
            DogrulamaSatirlari.Add(new DogrulamaSatiri("▱", "#E74C3C", "Duvar topolojisi bulunmayan mekan", bolmesiz, kritikMi: true));
        if (atanmamis > 0)
            DogrulamaSatirlari.Add(new DogrulamaSatiri("🧱", "#E74C3C", "Yapı elemanı atanmamış bölme", atanmamis, kritikMi: true));
        if (yanlisBolmeTipi > 0)
            DogrulamaSatirlari.Add(new DogrulamaSatiri("🧱", "#E74C3C", "Duvara uyumsuz yapı elemanı atanmış", yanlisBolmeTipi, kritikMi: true));
        if (aciklikMalzemesiz > 0)
            DogrulamaSatirlari.Add(new DogrulamaSatiri("▯", "#E74C3C", "Malzemesi atanmamış kapı/pencere", aciklikMalzemesiz, kritikMi: true));
        if (yanlisAciklikTipi > 0)
            DogrulamaSatirlari.Add(new DogrulamaSatiri("▯", "#E74C3C", "Kapı/pencere tipine uyumsuz eleman atanmış", yanlisAciklikTipi, kritikMi: true));
        if (dosemesiz > 0)
            DogrulamaSatirlari.Add(new DogrulamaSatiri("🔻", "#E74C3C", "Döşeme atanmamış mekan", dosemesiz, kritikMi: true));
        if (tavansiz > 0)
            DogrulamaSatirlari.Add(new DogrulamaSatiri("🔺", "#E74C3C", "Tavan atanmamış mekan", tavansiz, kritikMi: true));
        if (bagimsizBolumsuz > 0)
            DogrulamaSatirlari.Add(new DogrulamaSatiri("🏷", "#E74C3C", "Bağımsız bölüme atanmamış mekan", bagimsizBolumsuz, kritikMi: true));
        if (kullanimsiz > 0)
            DogrulamaSatirlari.Add(new DogrulamaSatiri("❔", "#E74C3C", "Kullanım tipi belirsiz mekan", kullanimsiz, kritikMi: true));
        if (hacimsiz > 0)
            DogrulamaSatirlari.Add(new DogrulamaSatiri("◫", "#E74C3C", "Alan/yükseklik/hacim bilgisi geçersiz mekan", hacimsiz, kritikMi: true));
        if (siniflandirmasiz > 0)
            DogrulamaSatirlari.Add(new DogrulamaSatiri("⚖", "#E74C3C", "Kaynak/Alıcı sınıfı atanmamış", siniflandirmasiz, kritikMi: true));
        if (Proje.BinaIslevi == BinaIslevi.Tanimsiz)
            DogrulamaSatirlari.Add(new DogrulamaSatiri("🏛", "#E74C3C", "Bina işlevi seçilmemiş", 1, kritikMi: true));

        // Varsayımlar sonucu bloklamaz, ancak resmi değerlendirmede görünür kalır.
        if (aktifBolmeler.Any(b => b.IsDisCephe) && !Proje.DisGurultuKullaniciTarafindanDogrulandi)
            DogrulamaSatirlari.Add(new DogrulamaSatiri("🔊", "#E74C3C", "Dış gürültü varsayılan 65 dBA; kullanıcı doğrulaması yok", 1, kritikMi: true));
        int emilimVarsayimli = aktifMekanlar.Count(m => m.EmilimAlani <= 0);
        if (emilimVarsayimli > 0)
            DogrulamaSatirlari.Add(new DogrulamaSatiri("≈", "#F1C40F", "Reverberasyon emilim verisi yerine varsayım kullanılan mekan", emilimVarsayimli));

        OnPropertyChanged(nameof(DogrulamaSatirlariVarMi));
        OnPropertyChanged(nameof(OnKontrolKritikHataVarMi));
        OnPropertyChanged(nameof(OnKontrolBasarili));
        OnPropertyChanged(nameof(HesaplamaEtkin));

        var uyarilar = DogrulamaSatirlari.Select(s => $"{s.Aciklama}: {s.Sayi}").ToList();
        int kritikSayisi = DogrulamaSatirlari.Where(s => s.KritikMi).Sum(s => s.Sayi);
        int uyariSayisi = DogrulamaSatirlari.Where(s => !s.KritikMi).Sum(s => s.Sayi);
        OnKontrolOzeti = kritikSayisi > 0
            ? $"Hesap engellendi: {kritikSayisi} kritik eksik, {uyariSayisi} uyarı."
            : uyariSayisi > 0
                ? $"Ön kontrol tamamlandı: kritik eksik yok, {uyariSayisi} varsayım/uyarı var."
                : "Ön kontrol tamamlandı: hesap için zorunlu girdiler hazır.";
        OnPropertyChanged(nameof(OnKontrolOzeti));
        DogrulamaUyarisi = uyarilar.Count > 0
            ? $"{OnKontrolOzeti} {string.Join(" · ", uyarilar)}"
            : null;

        RefreshAtamaBekleyenler(aktifMekanlar);
    }

    /// UX — "Atama Bekleyenler" sekmesi için iş listesini doldurur.
    private void RefreshAtamaBekleyenler(List<Mekan> aktifMekanlar)
    {
        AtamaBekleyenler.Clear();

        // 1) Atanmamış duvarlar (her kat / oda)
        foreach (var kat in Proje.Katlar)
            foreach (var m in kat.Mekanlar)
                foreach (var b in m.Bolmeler.Where(b => b.YapiElemani == null))
                    AtamaBekleyenler.Add(new AtamaBekleyenSatiri(
                        "🧱", "#E67E22", "Duvar", kat.KatNo, m, b,
                        $"{m.Ad} — {(string.IsNullOrWhiteSpace(b.Ad) ? "Duvar" : b.Ad)} · {b.Alan:F1} m²",
                        $"Kat {kat.KatNo} · {(b.IsDisCephe ? "dış cephe" : "iç/komşu")}"));

        // 2) Döşeme / tavan atanmamış mekanlar
        foreach (var kat in Proje.Katlar)
            foreach (var m in kat.Mekanlar.Where(aktifMekanlar.Contains))
            {
                if (m.ZeminYapiElemani?.Tip != YapiElemaniTipi.Doseme)
                    AtamaBekleyenler.Add(new AtamaBekleyenSatiri(
                        "🔻", "#E67E22", "Döşeme", kat.KatNo, m, null,
                        $"{m.Ad} — döşeme atanmamış", $"Kat {kat.KatNo}"));
                if (m.TavanYapiElemani == null)
                    AtamaBekleyenler.Add(new AtamaBekleyenSatiri(
                        "🔺", "#E67E22", "Tavan", kat.KatNo, m, null,
                        $"{m.Ad} — tavan atanmamış", $"Kat {kat.KatNo}"));
            }

        // 3) Bağımsız bölüme atanmamış mekanlar
        foreach (var kat in Proje.Katlar)
            foreach (var m in kat.Mekanlar.Where(x => aktifMekanlar.Contains(x) && x.BagimsizBolumId == null))
                AtamaBekleyenler.Add(new AtamaBekleyenSatiri(
                    "🏷", "#F39C12", "Bağımsız Bölüm", kat.KatNo, m, null,
                    $"{m.Ad} — bağımsız bölüm yok", $"Kat {kat.KatNo}"));

        // 4) Kaynak/Alıcı sınıfı atanmamış mekanlar
        foreach (var kat in Proje.Katlar)
            foreach (var m in kat.Mekanlar.Where(x => aktifMekanlar.Contains(x) &&
                         string.IsNullOrWhiteSpace(x.KaynakDerecesi) && string.IsNullOrWhiteSpace(x.AliciSinifi)))
                AtamaBekleyenler.Add(new AtamaBekleyenSatiri(
                    "⚖", "#F1C40F", "Sınıf", kat.KatNo, m, null,
                    $"{m.Ad} — kaynak/alıcı sınıfı yok", $"Kat {kat.KatNo}"));

        OnPropertyChanged(nameof(AtamaBekleyenVarMi));
    }

    [RelayCommand]
    private Task TavanPatlat(Window? parentWindow) => PatlatOrtak(parentWindow, asagiBak: false);

    [RelayCommand]
    private Task DosemePatlat(Window? parentWindow) => PatlatOrtak(parentWindow, asagiBak: true);

    private async Task PatlatOrtak(Window? parentWindow, bool asagiBak)
    {
        var yonStr = asagiBak ? "Döşeme" : "Tavan";
        if (SeciliKat == null || SeciliMekan == null)
        {
            DuseyBirlesimMesaji = $"{yonStr} patlatmak için önce bir oda seçin.";
            return;
        }

        // Uyarı: işlem geri-dönüşü zor; proje kilitlenecek.
        if (parentWindow != null)
        {
            var diyalog = new OnayDiyaloguWindow(
                $"{yonStr} Patlat — Dikkat",
                $"'{SeciliMekan.Ad}' odasının {yonStr.ToLower()}si patlatılacak. Üst kat odası kaldırılıp duvarları " +
                "bu hacme taşınacak.\n\nBu işlemden sonra PROJE OTOMATİK KİLİTLENİR ve patlatma geri alınmadan " +
                "yeniden açılamaz/düzenlenemez.\n\nDevam edilsin mi?",
                tamamYazi: $"{yonStr} Patlat", tamamSonuc: OnaySonucu.Tamam);
            var sonucDiyalog = await diyalog.ShowDialog<OnaySonucu>(parentWindow);
            if (sonucDiyalog != OnaySonucu.Tamam) { DuseyBirlesimMesaji = "Patlatma iptal edildi."; return; }
        }

        PushUndoSnapshot();
        var servis = new DuseyMekanBirlesimServisi();
        var sonuc = asagiBak
            ? servis.DosemePatlat(Proje, SeciliKat, SeciliMekan)
            : servis.TavanPatlat(Proje, SeciliKat, SeciliMekan);
        DuseyBirlesimMesaji = sonuc.Mesaj;

        if (sonuc.Basarili)
        {
            // Görünür (en alt kat) birleşik odaya geç → panelde tüm birleşik duvarlar görünür
            var altUye = sonuc.Birlesim?.Uyeler.OrderBy(u => u.KatNo).FirstOrDefault();
            if (altUye != null)
            {
                var kat = Katlar.FirstOrDefault(k => k.KatNo == altUye.KatNo);
                SeciliKat = kat ?? SeciliKat;
                SeciliMekan = kat?.Mekanlar.FirstOrDefault(m => m.Id == altUye.MekanId) ?? SeciliMekan;
            }
            ProjeKilitli = true; // otomatik kalıcı kilit
        }

        Kirlet();
        DuseyMekanBirlesimServisi.HacimleriGuncelle(Proje);
        GuncelleDogrulamaUyarisi();
        Guncelle3DGorunum();
    }

    [RelayCommand]
    private void DuseyBirlesimiKaldir()
    {
        if (SeciliMekan == null)
        {
            DuseyBirlesimMesaji = "Patlatmayı geri almak için önce bir oda seçin.";
            return;
        }

        if (new DuseyMekanBirlesimServisi().BirlesimdenCikar(Proje, SeciliMekan, out var mesaj))
        {
            DuseyBirlesimMesaji = mesaj;
            // Patlatma kalmadıysa kilidi otomatik çöz
            if (!Proje.PatlatilmisDosemeVar) ProjeKilitli = false;
            Kirlet();
            DuseyMekanBirlesimServisi.HacimleriGuncelle(Proje);
            GuncelleDogrulamaUyarisi();
            Guncelle3DGorunum();
        }
        else
        {
            DuseyBirlesimMesaji = mesaj;
        }
    }

    private void GuncelleDuseyBirlesimMesaji()
    {
        if (SeciliMekan == null)
        {
            DuseyBirlesimMesaji = null;
            return;
        }

        var birlesim = DuseyMekanBirlesimServisi.BirlesimBul(Proje, SeciliMekan.Id);
        DuseyBirlesimMesaji = birlesim == null
            ? null
            : $"Düşey birleşik hacim: {birlesim.Uyeler.Count} kat.";
    }

    private IEnumerable<Bolme> TumBolmeler() =>
        Proje.Katlar.SelectMany(k => k.Mekanlar).SelectMany(m => m.Bolmeler);

    /// UX — Düşey birleşimde, atama panelinde hayatta kalan odanın altında gösterilecek
    /// partner oda(lar)ın duvarları. Böylece birleşen üst odanın duvarları da atanabilir.
    public IEnumerable<Bolme> BirlesikPartnerDuvarlari(Mekan mekan) =>
        DuseyMekanBirlesimServisi.BirlesikPartnerMekanlar(Proje, mekan)
            .SelectMany(m => m.Bolmeler);

    private static bool HesapDisiMekan(Mekan? mekan) =>
        mekan != null &&
        (
        mekan.Sinif == MekanSinifi.HesapDisi ||
        EK2SiniflandirmaServisi.HesapDisiMi(mekan.KullanimTipi) ||
        MekanKullanimiHelper.Parse(mekan.KullanimTipi) == MekanKullanimiTipi.HesapDisi
        );

    private static bool HesapDisiBolme(Bolme bolme) =>
        HesapDisiMekan(bolme.KaynakMekan) || HesapDisiMekan(bolme.AliciMekan);

    private string BagimsizBolumAdi(Mekan? mekan)
    {
        if (mekan?.BagimsizBolumId is not { } id)
            return "Atanmamış";

        return BagimsizBolumler.FirstOrDefault(b => b.Id == id)?.Ad ?? "Atanmamış";
    }

    private string BolmeBagimsizBolumEtiketi(Bolme bolme)
    {
        if (bolme.IsDisCephe)
            return BagimsizBolumAdi(bolme.KaynakMekan);

        return $"{BagimsizBolumAdi(bolme.KaynakMekan)} → {BagimsizBolumAdi(bolme.AliciMekan)}";
    }

    private void RefreshKomsuDuvarlar()
    {
        // Salt-okunur görünüm yenilemesi: mevcut kanonik topolojiyi yalnız listelere yansıtır.
        // Komşuluk yeniden üretimi yalnız açık bir geometri içe aktarma/kontrol işleminde yapılır.
        KomsuDuvarlar.Clear();
        AtamaBolmeleri.Clear();
        var segs = new List<(Kst.Acoustics.Core.Geometry.Point3D, Kst.Acoustics.Core.Geometry.Point3D, bool)>();
        // Komşu duvarlar için tekrar kontrolü (A↔B sadece bir kez)
        var komsuEklendi = new HashSet<Guid>();
        foreach (var k in Proje.Katlar)
        {
            if (!k.BloklariGoster) continue;
            double z = KatTabanZ(k);
            var off = k.OriginOffset;
            foreach (var m in k.Mekanlar)
            {
                foreach (var b in m.Bolmeler)
                {
                    double uz = Math.Sqrt(
                        (b.Bitis.X - b.Baslangic.X) * (b.Bitis.X - b.Baslangic.X) +
                        (b.Bitis.Y - b.Baslangic.Y) * (b.Bitis.Y - b.Baslangic.Y));

                    // TumBolmeler — her bölme (dış cephe dahil), tekrar yok
                    if (!komsuEklendi.Contains(b.Id))
                    {
                        komsuEklendi.Add(b.Id);
                        string baslik = b.IsDisCephe
                            ? $"Kat {k.KatNo}: {m.Ad} (Dış Cephe)"
                            : $"Kat {k.KatNo}: {m.Ad} ↔ {b.AliciMekan!.Ad}";
                        AtamaBolmeleri.Add(new KomsuDuvarSatiri
                        {
                            Kat           = k,
                            KaynakMekan   = m,
                            AliciMekan    = b.AliciMekan,
                            Bolme         = b,
                            Uzunluk       = uz,
                            Baslik        = baslik,
                            Ozet          = $"{uz:0.00} m  ·  {b.Alan:0.00} m²  ·  {b.KomsulukTipiEtiketi}",
                            BagimsizBolum = BolmeBagimsizBolumEtiketi(b),
                            KomsulukTipi  = b.KomsulukTipiEtiketi,
                        });
                    }

                    // KomsuDuvarlar — yalnızca komşu (AliciMekan != null), A↔B teke indir
                    if (b.AliciMekan == null) continue;
                    if (string.CompareOrdinal(m.Ad, b.AliciMekan.Ad) > 0) continue;
                    var komsuSatir = new KomsuDuvarSatiri
                    {
                        Kat          = k,
                        KaynakMekan  = m,
                        AliciMekan   = b.AliciMekan,
                        Bolme        = b,
                        Uzunluk      = uz,
                        Baslik       = $"Kat {k.KatNo}: {m.Ad} ↔ {b.AliciMekan.Ad}",
                        Ozet         = $"{uz:0.00} m  ·  {b.Alan:0.00} m²  ·  {b.KomsulukTipiEtiketi}",
                        BagimsizBolum = BolmeBagimsizBolumEtiketi(b),
                        KomsulukTipi = b.KomsulukTipiEtiketi,
                    };
                    KomsuDuvarlar.Add(komsuSatir);
                    bool sel = SeciliKomsuDuvar?.Bolme == b;
                    segs.Add((
                        new Kst.Acoustics.Core.Geometry.Point3D(b.Baslangic.X + off.X, b.Baslangic.Y + off.Y, z),
                        new Kst.Acoustics.Core.Geometry.Point3D(b.Bitis.X + off.X, b.Bitis.Y + off.Y, z),
                        sel));
                }
            }
        }
        NeighborSegments = segs;
        GuncelleFiltreLiKomsuDuvarlar();
    }

    private void GuncelleFiltreLiKomsuDuvarlar()
    {
        FiltreLiKomsuDuvarlar.Clear();

        // Uzunluk / Alan text → double ayrıştırma (bir kere)
        bool uzMinOk = TryParseFilterDouble(FilterUzunlukMin, out double uzMin);
        bool uzMaxOk = TryParseFilterDouble(FilterUzunlukMax, out double uzMax);
        bool alMinOk = TryParseFilterDouble(FilterAlanMin, out double alMin);
        bool alMaxOk = TryParseFilterDouble(FilterAlanMax, out double alMax);
        var mekanAdi = (FilterMekanAdi ?? "").Trim();

        bool herhangiFilterAktif =
            FilterDisCephe || FilterAyniIciDuvar || FilterKomsuBagimsizBolum ||
            FilterDG || FilterOG || FilterYG ||
            FilterAtanmamis || FilterAtanmis ||
            FilterAliciI || FilterAliciII || FilterAliciIII ||
            FilterAciklikVar || FilterAciklikYok ||
            FilterSadeceKomsu || FilterSadeceSeciliKat ||
            uzMinOk || uzMaxOk || alMinOk || alMaxOk ||
            mekanAdi.Length > 0 ||
            FilterBagimsizBolum != null ||
            FilterMenfezVar || FilterMenfezYok;

        if (!herhangiFilterAktif)
        {
            foreach (var duvar in AtamaBolmeleri)
                FiltreLiKomsuDuvarlar.Add(duvar);
            return;
        }

        foreach (var duvar in AtamaBolmeleri)
        {
            bool gecti = true;

            // Komşuluk tipi
            if (FilterDisCephe || FilterAyniIciDuvar || FilterKomsuBagimsizBolum)
            {
                bool ok = false;
                if (FilterDisCephe         && duvar.KomsulukTipi == "Dış cephe")                    ok = true;
                if (FilterAyniIciDuvar     && duvar.KomsulukTipi == "Aynı bağımsız bölüm içi")      ok = true;
                if (FilterKomsuBagimsizBolum && duvar.KomsulukTipi == "Bağımsız bölümler arası")    ok = true;
                gecti = gecti && ok;
            }

            // Kaynak derece
            if (FilterDG || FilterOG || FilterYG)
            {
                bool ok = false;
                if (FilterDG && duvar.KaynakMekan?.KaynakDerecesi == "DG") ok = true;
                if (FilterOG && duvar.KaynakMekan?.KaynakDerecesi == "OG") ok = true;
                if (FilterYG && duvar.KaynakMekan?.KaynakDerecesi == "YG") ok = true;
                gecti = gecti && ok;
            }

            // Atama durumu
            if (FilterAtanmamis || FilterAtanmis)
            {
                bool atanmis = duvar.Bolme?.YapiElemani != null;
                bool ok = (FilterAtanmamis && !atanmis) || (FilterAtanmis && atanmis);
                gecti = gecti && ok;
            }

            // Alıcı sınıfı (AliciMekan üzerinden)
            if (FilterAliciI || FilterAliciII || FilterAliciIII)
            {
                bool ok = false;
                var sinif = duvar.AliciMekan?.AliciSinifi ?? "";
                if (FilterAliciI   && sinif == "I")   ok = true;
                if (FilterAliciII  && sinif == "II")  ok = true;
                if (FilterAliciIII && sinif == "III") ok = true;
                gecti = gecti && ok;
            }

            // Açıklık varlığı
            if (FilterAciklikVar || FilterAciklikYok)
            {
                bool aciklikVar = (duvar.Bolme?.Acikliklar?.Count ?? 0) > 0;
                bool ok = (FilterAciklikVar && aciklikVar) || (FilterAciklikYok && !aciklikVar);
                gecti = gecti && ok;
            }

            // Sadece komşu duvarlar (AliciMekan != null)
            if (FilterSadeceKomsu)
                gecti = gecti && duvar.AliciMekan != null;

            // Sadece seçili kat
            if (FilterSadeceSeciliKat && SeciliKat != null)
                gecti = gecti && duvar.Kat == SeciliKat;

            // Uzunluk aralığı (m)
            if (uzMinOk && duvar.Uzunluk < uzMin) gecti = false;
            if (uzMaxOk && duvar.Uzunluk > uzMax) gecti = false;

            // Alan aralığı (m²)
            if (alMinOk || alMaxOk)
            {
                double alan = duvar.Bolme?.Alan ?? 0;
                if (alMinOk && alan < alMin) gecti = false;
                if (alMaxOk && alan > alMax) gecti = false;
            }

            // Mekan adı arama
            if (mekanAdi.Length > 0)
            {
                bool ok = (duvar.KaynakMekan?.Ad?.Contains(mekanAdi, StringComparison.OrdinalIgnoreCase) ?? false) ||
                          (duvar.AliciMekan?.Ad?.Contains(mekanAdi, StringComparison.OrdinalIgnoreCase) ?? false) ||
                          (duvar.Baslik?.Contains(mekanAdi, StringComparison.OrdinalIgnoreCase) ?? false);
                gecti = gecti && ok;
            }

            // Bağımsız bölüm filtresi
            if (FilterBagimsizBolum != null)
            {
                var bbId = FilterBagimsizBolum.Id;
                bool ok = duvar.KaynakMekan?.BagimsizBolumId == bbId ||
                          duvar.AliciMekan?.BagimsizBolumId == bbId;
                gecti = gecti && ok;
            }

            // Menfez varlığı
            if (FilterMenfezVar || FilterMenfezYok)
            {
                bool menfezVar = (duvar.Bolme?.Menfezler?.Count ?? 0) > 0;
                bool ok = (FilterMenfezVar && menfezVar) || (FilterMenfezYok && !menfezVar);
                gecti = gecti && ok;
            }

            if (gecti) FiltreLiKomsuDuvarlar.Add(duvar);
        }
    }

    private static bool TryParseFilterDouble(string? text, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var normalized = text.Replace(',', '.');
        return double.TryParse(normalized, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out result) && result >= 0;
    }

    partial void OnFilterDisCepheChanged(bool value)          => GuncelleFiltreLiKomsuDuvarlar();
    partial void OnFilterAyniIciDuvarChanged(bool value)      => GuncelleFiltreLiKomsuDuvarlar();
    partial void OnFilterKomsuBagimsizBolumChanged(bool value) => GuncelleFiltreLiKomsuDuvarlar();
    partial void OnFilterDGChanged(bool value)                 => GuncelleFiltreLiKomsuDuvarlar();
    partial void OnFilterOGChanged(bool value)                 => GuncelleFiltreLiKomsuDuvarlar();
    partial void OnFilterYGChanged(bool value)                 => GuncelleFiltreLiKomsuDuvarlar();
    partial void OnFilterAtanmamisChanged(bool value)          => GuncelleFiltreLiKomsuDuvarlar();
    partial void OnFilterAtanmisChanged(bool value)            => GuncelleFiltreLiKomsuDuvarlar();
    partial void OnFilterAliciIChanged(bool value)             => GuncelleFiltreLiKomsuDuvarlar();
    partial void OnFilterAliciIIChanged(bool value)            => GuncelleFiltreLiKomsuDuvarlar();
    partial void OnFilterAliciIIIChanged(bool value)           => GuncelleFiltreLiKomsuDuvarlar();
    partial void OnFilterAciklikVarChanged(bool value)         => GuncelleFiltreLiKomsuDuvarlar();
    partial void OnFilterAciklikYokChanged(bool value)         => GuncelleFiltreLiKomsuDuvarlar();
    partial void OnFilterSadeceKomsuChanged(bool value)        => GuncelleFiltreLiKomsuDuvarlar();
    partial void OnFilterSadeceSeciliKatChanged(bool value)    => GuncelleFiltreLiKomsuDuvarlar();
    partial void OnFilterUzunlukMinChanged(string value)       => GuncelleFiltreLiKomsuDuvarlar();
    partial void OnFilterUzunlukMaxChanged(string value)       => GuncelleFiltreLiKomsuDuvarlar();
    partial void OnFilterAlanMinChanged(string value)          => GuncelleFiltreLiKomsuDuvarlar();
    partial void OnFilterAlanMaxChanged(string value)          => GuncelleFiltreLiKomsuDuvarlar();
    partial void OnFilterMekanAdiChanged(string value)         => GuncelleFiltreLiKomsuDuvarlar();
    partial void OnFilterBagimsizBolumChanged(BagimsizBolum? value) => GuncelleFiltreLiKomsuDuvarlar();
    partial void OnFilterMenfezVarChanged(bool value)          => GuncelleFiltreLiKomsuDuvarlar();
    partial void OnFilterMenfezYokChanged(bool value)          => GuncelleFiltreLiKomsuDuvarlar();

    [RelayCommand]
    private void AlamaDuvarToggle(KomsuDuvarSatiri duvar)
    {
        if (SeciliKomsuDuvarlarAtamaya.Contains(duvar))
            SeciliKomsuDuvarlarAtamaya.Remove(duvar);
        else
            SeciliKomsuDuvarlarAtamaya.Add(duvar);
    }

    [RelayCommand]
    private void AlamaTumDuvarlarSec()
    {
        SeciliKomsuDuvarlarAtamaya.Clear();
        foreach (var d in FiltreLiKomsuDuvarlar)
            SeciliKomsuDuvarlarAtamaya.Add(d);
    }

    [RelayCommand]
    private void AlamaTumDuvarlarKaldır()
    {
        SeciliKomsuDuvarlarAtamaya.Clear();
    }

    [RelayCommand]
    private void AlamaYap()
    {
        if (ProjeKilitli)
        {
            KutuphaneMesaji = "Kilitli projede malzeme atanamaz.";
            return;
        }
        if (SeciliAlamaMalzemesi == null)
        {
            KutuphaneMesaji = "❌ Lütfen sağ taraftan bir yapı elemanı seçin.";
            return;
        }
        if (SeciliAlamaMalzemesi.Tip != YapiElemaniTipi.Duvar)
        {
            KutuphaneMesaji = "❌ Toplu duvar atamasında yalnız Duvar tipindeki yapı elemanları kullanılabilir.";
            return;
        }

        if (SeciliKomsuDuvarlarAtamaya.Count == 0)
        {
            KutuphaneMesaji = "❌ Lütfen sol taraftan en az bir duvar seçin.";
            return;
        }

        PushUndoSnapshot();
        int atananSayi = 0;
        foreach (var duvar in SeciliKomsuDuvarlarAtamaya)
        {
            if (duvar.Bolme != null)
            {
                duvar.Bolme.YapiElemani = SeciliAlamaMalzemesi;

                // Çift yönlü atama: eğer komşu mekan varsa onun karşıt duvarına da ata
                if (duvar.AliciMekan != null)
                {
                    var komsuBolme = duvar.AliciMekan.Bolmeler.FirstOrDefault(b =>
                        b.AliciMekan == duvar.KaynakMekan);
                    if (komsuBolme != null)
                        komsuBolme.YapiElemani = SeciliAlamaMalzemesi;
                }
                atananSayi++;
            }
        }

        KutuphaneMesaji = $"✓ {atananSayi} duvar/bölmeye '{SeciliAlamaMalzemesi.Ad}' atandı.";
        Hesapla();
    }

    /// Issue 3 — Düşey birleşime dahil odaları yalnızca birleşimin EN ALT katındaki
    /// üyede tek kez göster; üst kat üyelerini gizle. Açıklama bloğunu da yeniler.
    /// Liste, Clear yerine artımlı senkronlanır → SeciliMekan/ListBox seçimi düşmez.
    private void RefreshGoruntulenenMekanlar()
    {
        var kat = SeciliKat;
        var hedef = new List<Mekan>();
        if (kat != null)
            foreach (var m in kat.Mekanlar)
                if (!BirlesimdeGizliMi(m, kat))
                    hedef.Add(m);

        // 1. Artık gösterilmeyenleri çıkar
        for (int i = GoruntulenenMekanlar.Count - 1; i >= 0; i--)
            if (!hedef.Contains(GoruntulenenMekanlar[i]))
                GoruntulenenMekanlar.RemoveAt(i);
        // 2. Eksikleri doğru sıraya yerleştir
        for (int i = 0; i < hedef.Count; i++)
        {
            if (i < GoruntulenenMekanlar.Count && ReferenceEquals(GoruntulenenMekanlar[i], hedef[i]))
                continue;
            int mevcut = GoruntulenenMekanlar.IndexOf(hedef[i]);
            if (mevcut >= 0) GoruntulenenMekanlar.Move(mevcut, i);
            else GoruntulenenMekanlar.Insert(i, hedef[i]);
        }

        // Açıklamalar: birleşimi en alt katında (görünür üye katında) listele
        SeciliKatBirlesimAciklamalari.Clear();
        if (kat != null)
        {
            foreach (var b in Proje.DuseyMekanBirlesimleri.Where(x => x.AktifMi))
            {
                if (b.Uyeler.Count == 0) continue;
                int minKat = b.Uyeler.Min(u => u.KatNo);
                if (minKat != kat.KatNo) continue;

                var parcalar = b.Uyeler.OrderBy(u => u.KatNo).Select(u =>
                {
                    var mk = Proje.Katlar.FirstOrDefault(kk => kk.KatNo == u.KatNo)?
                                  .Mekanlar.FirstOrDefault(mm => mm.Id == u.MekanId);
                    var yuzeyler = new List<string>();
                    if (u.DosemeAcik) yuzeyler.Add("döşeme");
                    if (u.TavanAcik) yuzeyler.Add("tavan");
                    var yuzeyStr = yuzeyler.Count > 0 ? $" [{string.Join("+", yuzeyler)} açık]" : "";
                    return mk != null ? $"K{u.KatNo}: {mk.Ad}{yuzeyStr}" : $"K{u.KatNo}: (silinmiş)";
                });
                double toplam = b.Uyeler.Sum(u =>
                    Proje.Katlar.FirstOrDefault(kk => kk.KatNo == u.KatNo)?
                         .Mekanlar.FirstOrDefault(mm => mm.Id == u.MekanId)?.Hacim ?? 0);
                SeciliKatBirlesimAciklamalari.Add(
                    $"⬍ Düşey birleşim: {string.Join("  +  ", parcalar)}  =  Σ {toplam:F0} m³");
            }
        }
        OnPropertyChanged(nameof(BirlesimAciklamasiVarMi));
    }

    /// Mekan, aktif bir düşey birleşimin üyesi ve bulunduğu kat birleşimin EN ALT
    /// katı DEĞİLSE listede gizlenir (üst kat tekrarı engellenir).
    private bool BirlesimdeGizliMi(Mekan m, Kat kat)
    {
        var b = DuseyMekanBirlesimServisi.BirlesimBul(Proje, m.Id);
        if (b == null || b.Uyeler.Count == 0) return false;
        return kat.KatNo != b.Uyeler.Min(u => u.KatNo);
    }

    private void Guncelle3DGorunum()
    {
        var geometriServisi = new BinaGeometriServisi();
        var seciliKat = SeciliKat;

        // BloklariGoster işaretli tüm katları her iki görünümde de göster.
        // 2D'de tıklama sadece seciliKat.Mekanlar'a bakar (PickRoomAt), diğer katlar görünür ama seçilemez.
        {
            var tumSurfaces = new System.Collections.Generic.List<Kst.Acoustics.Core.Projects.ProjectSurface>();
            foreach (var kat in Katlar.Where(k => k.BloklariGoster))
                tumSurfaces.AddRange(geometriServisi.BinaModelineCevir(Proje, SonuclariGoster, kat.KatNo, IsiHaritasiAcik));
            RenderSurfaces = tumSurfaces;
        }

        // Komşu duvar listesini yenile
        RefreshKomsuDuvarlar();

        // Issue 3 — Mekan listesi (düşey birleşim filtreli) ve açıklamaları yenile
        RefreshGoruntulenenMekanlar();

        // Sınıflandırma tablosunu yenile
        RefreshTumMekanSiniflari();

        // DXF arka plan segmentleri — DxfGoster işaretli tüm katlar
        var bgSegs = new System.Collections.Generic.List<(Kst.Acoustics.Core.Geometry.Point3D, Kst.Acoustics.Core.Geometry.Point3D)>();
        foreach (var kat in Katlar.Where(k => k.DxfGoster))
        {
            if (!kat.DxfGoster || kat.RawDxfSegments.Count == 0) continue;
            double z   = KatTabanZ(kat);
            var    off = kat.OriginOffset;
            foreach (var s in kat.RawDxfSegments)
                bgSegs.Add((
                    new Kst.Acoustics.Core.Geometry.Point3D(s.A.X + off.X, s.A.Y + off.Y, z),
                    new Kst.Acoustics.Core.Geometry.Point3D(s.B.X + off.X, s.B.Y + off.Y, z)));
        }
        RenderBackgroundSegments = bgSegs.Count > 0 ? bgSegs : null;

        var aciklikSegs = new System.Collections.Generic.List<(Kst.Acoustics.Core.Geometry.Point3D, Kst.Acoustics.Core.Geometry.Point3D, bool)>();
        foreach (var kat in Katlar.Where(k => k.BloklariGoster))
        {
            double z = KatTabanZ(kat);
            var off = kat.OriginOffset;
            foreach (var aciklik in kat.Mekanlar
                         .SelectMany(m => m.Bolmeler)
                         .SelectMany(b => b.Acikliklar))
            {
                aciklikSegs.Add((
                    new Kst.Acoustics.Core.Geometry.Point3D(aciklik.Baslangic.X + off.X, aciklik.Baslangic.Y + off.Y, z),
                    new Kst.Acoustics.Core.Geometry.Point3D(aciklik.Bitis.X + off.X, aciklik.Bitis.Y + off.Y, z),
                    false));
            }
        }
        RenderAcikliklar = aciklikSegs.Count > 0 ? aciklikSegs : null;
    }

    /// 4.5 — salt-okunur: kat yüksekliği yalnız M50'de düzenlenir, buraya
    /// `BinaGeometriMekanAdapteri` üzerinden salt-okunur yansır.
    public double SeciliKatYuksekligi => SeciliKat?.KatYuksekligi ?? 2.8;

    partial void OnSeciliKatChanged(Kat? value)
    {
        OnPropertyChanged(nameof(SeciliKatYuksekligi));
        Guncelle3DGorunum();
    }

    public void SeciliKatDosemeUygula(YapiElemani? doseme)
    {
        var kat = SeciliKat;
        if (ProjeKilitli || kat == null || doseme?.Tip != YapiElemaniTipi.Doseme) return;
        PushUndoSnapshot();

        foreach (var mekan in kat.Mekanlar)
        {
            mekan.ZeminYapiElemani = doseme;
            mekan.TavanYapiElemani = doseme;
        }

        GuncelleDogrulamaUyarisi();
        Guncelle3DGorunum();
    }

    partial void OnSeciliMekanChanged(Mekan? value)
    {
        EditablePolygon = PolygonAsWorld(value);
        SeciliBolmePaneli = null;
        OnPropertyChanged(nameof(SeciliMekanBagimsizBolum));
        OnPropertyChanged(nameof(SeciliMekanBagimsizBolumAdi));
        GuncelleDuseyBirlesimMesaji();
    }

    [ObservableProperty]
    private Bolme? _seciliBolmePaneli;

    partial void OnSeciliBolmePaneliChanged(Bolme? value)
    {
        OnPropertyChanged(nameof(SeciliBolmeWallA));
        OnPropertyChanged(nameof(SeciliBolmeWallB));
        if (value == null)
        {
            SeciliAciklikPaneli = null;
            OnPropertyChanged(nameof(SeciliAciklikSegment));
        }
    }

    partial void OnSeciliAciklikPaneliChanged(BolmeAcikligi? value)
    {
        OnPropertyChanged(nameof(SeciliAciklikSegment));
    }

    public Kst.Acoustics.Core.Geometry.Point3D? SeciliBolmeWallA =>
        SeciliBolmePaneli?.HasGeometry == true
            ? ToWorldPoint(SeciliBolmePaneli.Baslangic, SeciliBolmePaneli)
            : null;

    public Kst.Acoustics.Core.Geometry.Point3D? SeciliBolmeWallB =>
        SeciliBolmePaneli?.HasGeometry == true
            ? ToWorldPoint(SeciliBolmePaneli.Bitis, SeciliBolmePaneli)
            : null;

    public (Kst.Acoustics.Core.Geometry.Point3D Bas, Kst.Acoustics.Core.Geometry.Point3D Bit)? SeciliAciklikSegment =>
        SeciliAciklikPaneli != null && SeciliBolmePaneli != null
            ? (ToWorldPoint(SeciliAciklikPaneli.Baslangic, SeciliBolmePaneli),
               ToWorldPoint(SeciliAciklikPaneli.Bitis, SeciliBolmePaneli))
            : null;

    private Kst.Acoustics.Core.Geometry.Point3D ToWorldPoint(Point2D p, Bolme bolme)
    {
        var kat = KatForBolme(bolme);
        var off = kat?.OriginOffset ?? new Point2D(0, 0);
        var z = KatTabanZ(kat);
        return new Kst.Acoustics.Core.Geometry.Point3D(p.X + off.X, p.Y + off.Y, z);
    }

    private double KatTabanZ(Kat? hedef)
    {
        if (hedef == null) return 0;

        double z = 0;
        foreach (var kat in Katlar.AsEnumerable().Reverse())
        {
            if (ReferenceEquals(kat, hedef) || kat.KatNo == hedef.KatNo)
                return z;

            z += kat.KatYuksekligi;
        }

        return 0;
    }

    private Kat? KatForBolme(Bolme bolme) =>
        Katlar.FirstOrDefault(k => k.Mekanlar.Any(m => m.Bolmeler.Contains(bolme)));

    /// B5 — Bir Mekan'ın hangi katta olduğunu bulur (poligon offset dönüşümleri için).
    private Kat? KatForMekan(Mekan mekan) =>
        Katlar.FirstOrDefault(k => k.Mekanlar.Contains(mekan));

    /// B5 — Mekan poligonunu dünya koordinatına çevirir (kat OriginOffset uygulanır).
    /// EditablePolygon viewport ile aynı koordinat sisteminde olmalı.
    private List<Kst.Acoustics.Core.Geometry.Point3D>? PolygonAsWorld(Mekan? mekan)
    {
        if (mekan == null) return null;
        var off = (KatForMekan(mekan)?.OriginOffset) ?? new Point2D(0, 0);
        return mekan.Poligon
            .Select(p => new Kst.Acoustics.Core.Geometry.Point3D(p.X + off.X, p.Y + off.Y, 0))
            .ToList();
    }

    // ── Oda Ekleme / Silme ──────────────────────────────────────────────────



    // ── Duvar Kontrolü ──────────────────────────────────────────────────────

    [ObservableProperty]
    private string? _duvarKontrolMesaji;

    /// Tüm katlardaki mekanların bölmelerini poligondan yeniden oluşturur,
    /// ardından komşuluk tespitini ve 3D görünümü yeniler.
    /// P0 düzeltmesi: eskiden bu metot her mekanın Bolmeler listesini doğrudan
    /// temizleyip yeni "Duvar" nesneleriyle değiştiriyordu — YapiElemani, Acikliklar,
    /// Menfezler ve Junctions atamaları geri dönüşsüz kayboluyordu. Artık
    /// RefreshKomsuDuvarlar ile aynı güvenli yolu (OdaTespitServisi.KomsuluklariTespitEt)
    /// kullanır; bu servis eski/yeni bölmeleri geometrik örtüşmeyle eşleştirip tüm
    /// atamaları aktarır (bkz. codex8.md §10).
    [RelayCommand]
    private void DuvarKontrol()
    {
        if (ProjeKilitli)
        {
            DuvarKontrolMesaji = "Kilitli projede duvar topolojisi yeniden oluşturulamaz.";
            return;
        }
        PushUndoSnapshot();
        var odaServisi = new OdaTespitServisi();
        int toplamBolme = 0;

        foreach (var kat in Proje.Katlar)
        {
            foreach (var m in kat.Mekanlar)
                m.Alan = AreaOf(m.Poligon);

            odaServisi.KomsuluklariTespitEt(kat.Mekanlar);
            toplamBolme += kat.Mekanlar.Sum(m => m.Bolmeler.Count);
        }

        Kirlet();

        // EditablePolygon'u seçili mekanla senkronize et (B5 — kat offset uygulanır)
        EditablePolygon = PolygonAsWorld(SeciliMekan);

        Guncelle3DGorunum();

        int toplamKomsu = KomsuDuvarlar.Count;
        int disCephe = TumBolmeler().Count(b => b.AliciMekan == null);
        DuvarKontrolMesaji = $"✓ {toplamBolme} duvar kontrol edildi · {toplamKomsu} komşu duvar bulundu · {disCephe} dış cephe";

        // Mesajı 4 saniye sonra temizle
        System.Threading.Tasks.Task.Delay(4000).ContinueWith(_ =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() => DuvarKontrolMesaji = null));
    }

    /// Viewport tıklamasını oda seçimine veya poligon nokta eklemeye yönlendirir.
    public void PickRoomAt(double worldX, double worldY)
    {
        var kat = SeciliKat;
        if (kat == null) return;
        // B5 — Hit-test, kat.OriginOffset uygulanmış görünüm koordinatına göre yapılır.
        // Mekan.Poligon ham DXF koordinatında; tıklama dünya koordinatından offset çıkarılır.
        var localX = worldX - kat.OriginOffset.X;
        var localY = worldY - kat.OriginOffset.Y;
        foreach (var m in kat.Mekanlar)
        {
            if (PointInPolygon(localX, localY, m.Poligon))
            {
                SeciliMekan = m;
                return;
            }
        }
    }

    private static bool PointInPolygon(double x, double y, List<Point2D> poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var pi = poly[i]; var pj = poly[j];
            bool intersects = ((pi.Y > y) != (pj.Y > y)) &&
                              (x < (pj.X - pi.X) * (y - pi.Y) / ((pj.Y - pi.Y) + double.Epsilon) + pi.X);
            if (intersects) inside = !inside;
        }
        return inside;
    }

    private static double AreaOf(List<Point2D> poly)
    {
        if (poly.Count < 3) return 0;
        double s = 0;
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Count];
            s += a.X * b.Y - b.X * a.Y;
        }
        return Math.Abs(s) * 0.5;
    }

    private static void RebuildBolmeEndpoints(Mekan m)
    {
        // Sadece kenar sayısı eşit olduğunda 1:1 eşle; aksi halde mevcut bölme listesini bırak
        if (m.Bolmeler.Count != m.Poligon.Count) return;
        for (int i = 0; i < m.Poligon.Count; i++)
        {
            var a = m.Poligon[i];
            var b = m.Poligon[(i + 1) % m.Poligon.Count];
            var bolme = m.Bolmeler[i];
            bolme.Baslangic = a;
            bolme.Bitis     = b;
            double len = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
            bolme.Alan = len * m.Yukseklik;
        }
    }

    private void PushUndoSnapshot()
    {
        if (ProjeKilitli) return;
        PushSnapshotInternal(_undoStack, AlGuncelSnapshot());
        // Yeni edit → redo geçersiz
        _redoStack.Clear();
        BildirGeriAlYineleSayilari();
    }

    private sealed record KatUndoData(int KatNo, List<Mekan> Mekanlar);
    private sealed record ProjeUndoSnapshot(
        Guid BelgeOturumId,
        int? SeciliKatNo,
        Guid? SeciliMekanId,
        List<KatUndoData> Katlar,
        List<DuseyMekanBirlesimi> DuseyBirlesimleri,
        bool ProjeKilitli);
}

/// UX — "Atama Bekleyenler" sekmesindeki bir iş kalemi (eksik duvar/döşeme/tavan/sınıf vb.).
public sealed class AtamaBekleyenSatiri
{
    public string Ikon { get; }
    public string Renk { get; }
    public string Tur { get; }
    public int KatNo { get; }
    public Mekan? Mekan { get; }
    public Bolme? Bolme { get; }
    public string Baslik { get; }
    public string Ozet { get; }

    public AtamaBekleyenSatiri(string ikon, string renk, string tur, int katNo,
        Mekan? mekan, Bolme? bolme, string baslik, string ozet)
    {
        Ikon = ikon; Renk = renk; Tur = tur; KatNo = katNo;
        Mekan = mekan; Bolme = bolme; Baslik = baslik; Ozet = ozet;
    }
}

public class KomsuDuvarSatiri
{
    public Kat?    Kat         { get; set; }
    public Mekan?  KaynakMekan { get; set; }
    public Mekan?  AliciMekan  { get; set; }
    public Bolme?  Bolme       { get; set; }
    public double  Uzunluk     { get; set; }   // m
    public string  Baslik      { get; set; } = "";
    public string  Ozet        { get; set; } = "";
    public string  BagimsizBolum { get; set; } = "";
    public string  KomsulukTipi { get; set; } = "";
}

public class SonucSatiriViewModel
{
    /// Bölme, sentetik darbe, tesisat ve RT sonuçlarının tamamı için kararlı rapor kimliği.
    public Guid SonucId       { get; set; }
    public string Kat         { get; set; } = "";
    /// Kullanıcı tanımlı kat adı (tabloda "Kat" sütununda gösterilir).
    public string KatAdi      { get; set; } = "";
    /// "Hava" veya "Darbe" — tabloda tür sütununda gösterilir.
    public string HesapTuru   { get; set; } = "";
    public string HesapTuruRenk => HesapTuru switch
    {
        "Darbe"   => "#1A3A5C",
        "Tesisat" => "#4A2A10",
        "Çınlama" => "#3A1A4A",
        _         => "#1A3520",
    };
    public string Mekan       { get; set; } = "";
    public string Bolme       { get; set; } = "";
    public string BagimsizBolum { get; set; } = "";
    public string KomsulukTipi { get; set; } = "";
    public double? DisGurultuDb { get; set; }
    public double? IcGurultuDb { get; set; }
    public double? IcHedefDb { get; set; }
    public double? D2mNt50Db { get; set; }
    public string SpektrumKaynak { get; set; } = "";
    public int GuvenPuani { get; set; }
    public string GuvenSeviyesi { get; set; } = "";
    public string GuvenRengi { get; set; } = "#7894A8";
    public string GuvenOzeti { get; set; } = "";
    public double Deger       { get; set; }
    public string Birim       { get; set; } = "";
    public double Limit       { get; set; }
    public double Belirsizlik { get; set; }
    public double MarjDb      { get; set; }
    public string Durum       { get; set; } = "";
    public string DurumColor  { get; set; } = "";
    public bool   VeriEksik   { get; set; }
    public UyumDurumu Uyum    { get; set; } = UyumDurumu.VeriEksik;
    public string RezonansAciklama { get; set; } = "";
    /// UX — Yönetmelik atıf metni (Tablo 3.2, Ek-2 vb.). Sonuç tablosunda gösterilir.
    public string YonetmelikRef { get; set; } = "";
    public List<OktavBantGorunumu>? OktavBantlari { get; set; }
    /// UX — "Etkisi En Büyük 3 Yol" hesabı için bölme referansı.
    /// Serileştirmeyi etkilemesin diye JsonIgnore'lı; sadece UI seçimde kullanılır.
    [System.Text.Json.Serialization.JsonIgnore]
    public Kst.Acoustics.Core.BuildingAcoustics.Models.Bolme? IlgiliBolme { get; set; }

    // UX — Senaryo karşılaştırma: baseline değerleri (anlık görüntü)
    public double? BaselineDeger { get; set; }
    public double? BaselineMarj { get; set; }
    public string BaselineEtiketi => BaselineDeger is { } b ? $"{b:F1}" : "";
    public string DeltaEtiketi => BaselineMarj is { } b
        ? $"{(MarjDb - b):+0.0;-0.0;0.0} {(HesapTuru == "Çınlama" ? "s marj" : "dB marj")}"
        : "";
    public string DeltaRengi => BaselineMarj is { } b
        ? ((MarjDb - b) >= DeltaEsigi ? "#27AE60" : (MarjDb - b) <= -DeltaEsigi ? "#E74C3C" : "#7894A8")
        : "#7894A8";
    private double DeltaEsigi => HesapTuru == "Çınlama" ? 0.05 : 0.5;

    // "42.3 ± 2.0" formatı
    public string DegerBelirsizlik => $"{Deger:F1} ± {Belirsizlik:F1}";
    public string DisGurultuEtiketi => DisGurultuDb is { } d ? $"{d:F0} dBA" : "";
    public string IcGurultuEtiketi => IcGurultuDb is { } ic && IcHedefDb is { } hedef
        ? $"{ic:F1} / {hedef:F0} dBA"
        : "";
    public string D2mNt50Etiketi => D2mNt50Db is { } d ? $"{d:F1} dB" : "";
    public string GuvenEtiketi => GuvenPuani > 0 ? $"{GuvenSeviyesi} {GuvenPuani}" : "";
}

/// Tek bir oktav bant için görüntü verisi — SonuclarPanel detay satırında gösterilir.
public class OktavBantGorunumu
{
    private const double MaxBarPx = 50.0;

    // ISO 717-1 referans eğrisi (63–4000 Hz, 7 bant); 500 Hz = 52 dB
    private static readonly double[] Iso7171Ref = [33, 36, 42, 52, 55, 56, 56];
    // ISO 717-2 referans eğrisi (63–4000 Hz, 7 bant); 500 Hz = 60 dB
    private static readonly double[] Iso7172Ref = [62, 62, 62, 60, 59, 54, 51];

    public string Frekans          { get; init; } = "";
    public double Deger            { get; init; }
    public string DegerStr         { get; init; } = "";
    public double BarYukseklik     { get; init; }  // piksel (0–50)
    public double BarTopOffset     { get; init; }  // Canvas.Top = MaxBarPx - BarYukseklik
    public double RefDeger         { get; init; }  // kaydırılmış referans eğrisi değeri
    public string RefDegerStr      { get; init; } = "";
    public double RefBarYukseklik  { get; init; }
    public double RefBarTopOffset  { get; init; }  // Canvas.Top for reference line
    public string BandUyumRenk     { get; init; } = "#20C3D8";  // teal=geçti, turuncu=kaldı
    public string Kaynak           { get; init; } = "";  // "Ölçüm" | "Model"

    /// R(f) bantlarından liste üret; yoksa Ln(f) bantlarından üret.
    public static List<OktavBantGorunumu>? OlusturListesi(OktavBantSonucu? hava, OktavDarbeSonucu? darbe)
    {
        if (hava != null && hava.R_f.Length == 7)
            return OlusturHava(hava);
        if (darbe != null && darbe.Ln_f.Length == 7)
            return OlusturDarbe(darbe);
        return null;
    }

    private static List<OktavBantGorunumu> OlusturHava(OktavBantSonucu s)
    {
        string[] freqler = ["63", "125", "250", "500", "1k", "2k", "4k"];
        // Referans eğriyi Rw'ye kaydır: shift = Rw - 52 (500 Hz referansı)
        double shift = s.RwHesap - 52.0;
        double[] refShifted = Iso7171Ref.Select(r => r + shift).ToArray();

        // Ortak ölçek: hem ölçülen hem referans eğrisini kapsayan max
        double max = Math.Max(s.R_f.Max(), refShifted.Max());
        string kaynak = KaynakEtiketi(s.Kaynak);

        return s.R_f.Select((v, i) =>
        {
            double refV = refShifted[i];
            double barH = Px(v,    max);
            double refH = Px(refV, max);
            bool gecti  = v >= refV;
            return new OktavBantGorunumu
            {
                Frekans         = freqler[i],
                Deger           = v,
                DegerStr        = $"{v:F0}",
                BarYukseklik    = barH,
                BarTopOffset    = MaxBarPx - barH,
                RefDeger        = refV,
                RefDegerStr     = $"({refV:F0})",
                RefBarYukseklik = refH,
                RefBarTopOffset = MaxBarPx - refH,
                BandUyumRenk    = gecti ? "#27AE60" : "#E67E22",
                Kaynak          = kaynak
            };
        }).ToList();
    }

    private static List<OktavBantGorunumu> OlusturDarbe(OktavDarbeSonucu s)
    {
        string[] freqler = ["63", "125", "250", "500", "1k", "2k", "4k"];
        // Referans eğriyi Lnw'ye kaydır: shift = Lnw - 60 (500 Hz referansı)
        double shift = s.LnwHesap - 60.0;
        double[] refShifted = Iso7172Ref.Select(r => r + shift).ToArray();

        double max = Math.Max(s.Ln_f.Max(), refShifted.Max());
        string kaynak = KaynakEtiketi(s.Kaynak);

        return s.Ln_f.Select((v, i) =>
        {
            double refV = refShifted[i];
            double barH = Px(v,    max);
            double refH = Px(refV, max);
            // Darbe sesinde: ölçülen < referans = iyi (düşük seviye = daha sessiz)
            bool gecti  = v <= refV;
            return new OktavBantGorunumu
            {
                Frekans         = freqler[i],
                Deger           = v,
                DegerStr        = $"{v:F0}",
                BarYukseklik    = barH,
                BarTopOffset    = MaxBarPx - barH,
                RefDeger        = refV,
                RefDegerStr     = $"({refV:F0})",
                RefBarYukseklik = refH,
                RefBarTopOffset = MaxBarPx - refH,
                BandUyumRenk    = gecti ? "#27AE60" : "#E67E22",
                Kaynak          = kaynak
            };
        }).ToList();
    }

    private static double Px(double v, double max) =>
        max > 0 ? Math.Max(2, v / max * MaxBarPx) : 2;

    private static string KaynakEtiketi(string? kaynak) =>
        BinaAkustikRaporServisi.KaynakEtiketi(kaynak);
}

/// SiniflandirmaPanel DataGrid satırı — Mekan modelini sarmalar ve
/// KaynakDerecesi / AliciSinifi için INotifyPropertyChanged sağlar.
public class MekanSinifSatiri : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    private readonly Kst.Acoustics.Core.BuildingAcoustics.Models.Mekan _mekan;

    public int    KatNo       { get; }
    /// Kullanıcı tanımlı kat adı (boşsa "Kat {KatNo}").
    public string KatAdi      { get; }
    public Kst.Acoustics.Core.BuildingAcoustics.Models.Mekan Mekan => _mekan;
    public string Ad          => _mekan.Ad;
    public string KullanimTipi => _mekan.KullanimTipi;

    // Sabit seçenek listeleri — her satır örneği aynı diziyi paylaşır.
    public string[] KaynakSecenekleri { get; } = ["—", "OG", "YG", "DG"];
    public string[] AliciSecenekleri  { get; } = ["—", "I", "II", "III"];

    public string KaynakDerecesi
    {
        get => string.IsNullOrEmpty(_mekan.KaynakDerecesi) ? "—" : _mekan.KaynakDerecesi;
        set
        {
            var v = (value == "—" || value == null) ? "" : value;
            if (_mekan.KaynakDerecesi != v)
            {
                _mekan.KaynakDerecesi = v;
                OnPropertyChanged();
                OnPropertyChanged(nameof(KaynakRenk));
            }
        }
    }

    public string AliciSinifi
    {
        get => string.IsNullOrEmpty(_mekan.AliciSinifi) ? "—" : _mekan.AliciSinifi;
        set
        {
            var v = (value == "—" || value == null) ? "" : value;
            if (_mekan.AliciSinifi != v)
            {
                _mekan.AliciSinifi = v;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AliciRenk));
            }
        }
    }

    public string KaynakRenk => _mekan.KaynakDerecesi switch
    {
        "OG" => "#E67E22",
        "YG" => "#C0392B",
        "DG" => "#27AE60",
        _    => "#444444",
    };

    public string AliciRenk => _mekan.AliciSinifi switch
    {
        "I"   => "#2980B9",
        "II"  => "#5B8DB8",
        "III" => "#7F8C8D",
        _     => "#444444",
    };

    public MekanSinifSatiri(
        Kst.Acoustics.Core.BuildingAcoustics.Models.Mekan mekan, int katNo, string? katAdi = null)
    {
        _mekan = mekan;
        KatNo  = katNo;
        KatAdi = string.IsNullOrWhiteSpace(katAdi) ? $"Kat {katNo}" : katAdi!;
    }
}

/// UX — "Rapora Dahil Edilecek Odalar ve Komşuluklar" listesi için satır.
public class RaporSatiriViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    private bool _dahilMi = true;

    public bool DahilMi
    {
        get => _dahilMi;
        set { if (_dahilMi != value) { _dahilMi = value; OnPropertyChanged(); } }
    }

    public string KatAdi           { get; init; } = "";
    public string Mekan            { get; init; } = "";
    public string HesapTuru        { get; init; } = "";
    public string Bolme            { get; init; } = "";
    public string KomsulukTipi     { get; init; } = "";
    public string Durum            { get; init; } = "";
    public string DegerBelirsizlik { get; init; } = "";
    /// P0 düzeltmesi — rapor dışa aktarımlarının hangi Bolme'yi hariç tutacağını kararlı
    /// biçimde belirlemek için. Ad tabanlı (Mekan+Bolme+HesapTuru) eşleşme aynı adlı
    /// oda/bölmelerde yanlış satırı eşleştirebiliyordu.
    public System.Guid SonucId     { get; init; }
    public System.Guid BolmeId     => SonucId; // eski XAML/bağlantılar için geriye uyumlu ad

    public static RaporSatiriViewModel FromSonuc(SonucSatiriViewModel s) => new()
    {
        KatAdi           = s.KatAdi,
        Mekan            = s.Mekan,
        HesapTuru        = s.HesapTuru,
        Bolme            = s.Bolme,
        KomsulukTipi     = s.KomsulukTipi,
        Durum            = s.Durum,
        DegerBelirsizlik = s.DegerBelirsizlik,
        SonucId          = s.SonucId,
    };

    public static RaporSatiriViewModel FromRapor(RaporSatiri s, BinaProje proje)
    {
        var katAdi = proje.Katlar.FirstOrDefault(k => k.KatNo.ToString() == s.Kat)?.Ad;
        var durum = s.Durum switch
        {
            nameof(UyumDurumu.Gecti) => "GEÇTİ",
            nameof(UyumDurumu.Kaldi) => "KALDI",
            _ => "VERİ EKSİK",
        };
        var hesapTuru = s.SonucTuru switch
        {
            AkustikRaporSonucTuru.HavaSesi => "Hava",
            AkustikRaporSonucTuru.DisCephe => "Cephe",
            AkustikRaporSonucTuru.DarbeSesi => "Darbe",
            AkustikRaporSonucTuru.TesisatSurekli or AkustikRaporSonucTuru.TesisatKesikli => "Tesisat",
            AkustikRaporSonucTuru.Reverberasyon => "Çınlama",
            _ => s.SonucTuruEtiketi,
        };
        var komsuluk = !string.IsNullOrWhiteSpace(s.AliciMekan)
            ? $"{s.KaynakMekan} → {s.AliciMekan}"
            : s.SonucTuru == AkustikRaporSonucTuru.DisCephe ? "Dış cephe" : s.KaynakMekan;
        var belirsizlik = s.Belirsizlik > 0
            ? $"{s.Deger:F1} ± {s.Belirsizlik:F1} {s.Birim}"
            : $"{s.Deger:F1} {s.Birim}";

        return new RaporSatiriViewModel
        {
            KatAdi = string.IsNullOrWhiteSpace(katAdi) ? $"Kat {s.Kat}" : katAdi,
            Mekan = s.Mekan,
            HesapTuru = hesapTuru,
            Bolme = s.Bolme,
            KomsulukTipi = komsuluk,
            Durum = durum,
            DegerBelirsizlik = belirsizlik,
            SonucId = s.SonucId,
        };
    }
}

public sealed record SpektrumKaynagiInfo(
    SpektrumVeriKaynagi Deger,
    string Etiket,
    string Aciklama);

/// UX — Çözüm önerisi satırı (panel UI bağlantısı).
public class OneriRowViewModel
{
    private readonly Kst.Acoustics.Core.BuildingAcoustics.Calculation.CozumOnerisi _oneri;
    private readonly SonucSatiriViewModel _satir;
    private readonly BinaProjesiViewModel _vm;

    public string OneriAdi  => _oneri.OneriAdi;
    public string ArtisStr  => _oneri.Uyari ? "Uyarı" : $"+{_oneri.ArtisDb:F0} dB";
    public string Aciklama  => _oneri.Aciklama;
    public bool   Uyari     => _oneri.Uyari;
    public string Renk      => _oneri.Uyari
        ? "#5A3A0A"
        : _oneri.ArtisDb >= 12 ? "#1E5C3A"
        : _oneri.ArtisDb >=  5 ? "#2C5A1E"
                                : "#2A3A4A";
    public bool UygulanabilirMi => !_oneri.Uyari;

    public CommunityToolkit.Mvvm.Input.RelayCommand UygulaCommand { get; }

    public OneriRowViewModel(Kst.Acoustics.Core.BuildingAcoustics.Calculation.CozumOnerisi oneri,
        SonucSatiriViewModel satir, BinaProjesiViewModel vm)
    {
        _oneri = oneri; _satir = satir; _vm = vm;
        UygulaCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => _vm.OneriUygula(_satir, _oneri));
    }
}

/// UX — Proje doğrulama paneli satırı: ikon + renk + açıklama + sayı.
public class DogrulamaSatiri
{
    public string Ikon { get; }
    public string Renk { get; }
    public string Aciklama { get; }
    public int Sayi { get; }
    public bool KritikMi { get; }
    public string Seviye => KritikMi ? "Kritik" : "Uyarı";
    public string Etiket => $"{Sayi}";

    public DogrulamaSatiri(string ikon, string renk, string aciklama, int sayi, bool kritikMi = false)
    {
        Ikon = ikon;
        Renk = renk;
        Aciklama = aciklama;
        Sayi = sayi;
        KritikMi = kritikMi;
    }
}

/// UX — "Etkisi En Büyük 3 Yol": bölmedeki en düşük Rij'ye sahip 3 flanking yolu.
/// Düşük Rij = yüksek geçirgenlik = sonuca en çok katkı yapan yol.
public class EtkiliYolGorunumu
{
    public string YolEtiketi   { get; init; } = "";   // örn. "Ff-Alt", "Df-Sol"
    public double Rij          { get; init; }
    public double Kij          { get; init; }
    public double KaynakRw     { get; init; }
    public double AliciRw      { get; init; }
    public double KatkiYuzde   { get; init; }         // 0–100

    public string RijStr     => $"Rij {Rij:F1}";
    public string KijStr     => $"K={Kij:F1}";
    public string ElemanStr  => $"{KaynakRw:F0}→{AliciRw:F0} dB";
    public string KatkiStr   => $"{KatkiYuzde:F0}%";
    public string KatkiRengi => KatkiYuzde switch
    {
        >= 30 => "#E74C3C",
        >= 15 => "#E67E22",
        >= 5  => "#F39C12",
        _     => "#7894A8",
    };

    public static IEnumerable<EtkiliYolGorunumu> HesaplaUcEnZayifYol(
        Kst.Acoustics.Core.BuildingAcoustics.Models.Bolme bolme)
    {
        var paths = Kst.Acoustics.Core.BuildingAcoustics.Calculation.HavaSesiHesapServisi
            .GetOnUcYolFlankingYollari(bolme);
        if (paths.Count == 0) yield break;

        // Toplam transmission: direkt yol + tüm flanking yolları
        double direkR = Kst.Acoustics.Core.BuildingAcoustics.Calculation.HavaSesiHesapServisi
            .CalculateCompositeRw(bolme);
        double tauDirect   = direkR > 0 ? Math.Pow(10, -direkR / 10.0) : 0;
        double tauFlanking = paths.Sum(p => Math.Pow(10, -p.Rij / 10.0));
        double tauTotal    = tauDirect + tauFlanking;
        if (tauTotal <= 0) yield break;

        foreach (var p in paths.OrderBy(x => x.Rij).Take(3))
        {
            double tau = Math.Pow(10, -p.Rij / 10.0);
            yield return new EtkiliYolGorunumu
            {
                YolEtiketi = $"{p.YolTipi}-{p.Kenar}",
                Rij        = p.Rij,
                Kij        = p.Kij,
                KaynakRw   = p.KaynakRw,
                AliciRw    = p.AliciRw,
                KatkiYuzde = 100.0 * tau / tauTotal
            };
        }
    }
}
