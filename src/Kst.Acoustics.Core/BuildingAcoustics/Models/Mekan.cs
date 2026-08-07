using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.BuildingAcoustics.Models;

public enum MekanSinifi
{
    Tanimsiz,
    Kaynak,   // gürültü kaynağı (Salon, Mutfak, Yemek, Hol)
    Alici,    // hassas alıcı (Yatak, Çocuk, Ebeveyn, Çalışma)
    Gecis,    // geçiş/servis (Koridor, WC, Banyo, Antre, Merdiven)
    HesapDisi // Balkon, açık teras vb. hesap dışı alanlar
}

public class Mekan : INotifyPropertyChanged
{
    public Guid Id { get; set; } = Guid.NewGuid();
    private string _ad = "";
    public string Ad
    {
        get => _ad;
        set
        {
            if (_ad == value) return;
            _ad = value;
            OnPropertyChanged();
        }
    }
    public Guid? BagimsizBolumId { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    private string _kullanimTipi = "";
    /// Mekan kullanım tipi: Yatak Odası, Salon, Ofis vb. — UI/DXF etiketinden gelir.
    /// M2 — Değiştiğinde, Sinif Tanimsiz ise MekanKullanimiTipi parse'ından otomatik türetir.
    public string KullanimTipi
    {
        get => _kullanimTipi;
        set
        {
            if (_kullanimTipi == value) return;
            _kullanimTipi = value;
            if (_sinif == MekanSinifi.Tanimsiz)
                _sinif = TureteMekanSinifi(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(SinifEtiketi));
            OnPropertyChanged(nameof(SinifRenk));
        }
    }

    private MekanSinifi _sinif = MekanSinifi.Tanimsiz;
    /// Akustik sınıf — KullanimTipi'nden otomatik tahmin, kullanıcı override edebilir.
    public MekanSinifi Sinif
    {
        get => _sinif;
        set => _sinif = value;
    }

    /// M2 — Kullanım tipi metninden 4 ana akustik sınıfa eşleme.
    /// Aynı zamanda MekanKullanimiHelper.Parse ile tutarlı olmalı.
    private static MekanSinifi TureteMekanSinifi(string kullanim)
    {
        var tip = MekanKullanimiHelper.Parse(kullanim);
        return tip switch
        {
            MekanKullanimiTipi.YatakOdasi    => MekanSinifi.Alici,
            MekanKullanimiTipi.HastaneOdasi  => MekanSinifi.Alici,
            MekanKullanimiTipi.Ofis          => MekanSinifi.Alici,
            MekanKullanimiTipi.Sinif         => MekanSinifi.Alici,
            MekanKullanimiTipi.OturmaOdasi   => MekanSinifi.Kaynak,
            MekanKullanimiTipi.Mutfak        => MekanSinifi.Kaynak,
            MekanKullanimiTipi.AcikBuro      => MekanSinifi.Kaynak,
            MekanKullanimiTipi.HolKoridoru   => MekanSinifi.Gecis,
            MekanKullanimiTipi.Banyo         => MekanSinifi.Gecis,
            MekanKullanimiTipi.HesapDisi     => MekanSinifi.HesapDisi,
            _                                => MekanSinifi.Tanimsiz,
        };
    }
    public string SinifEtiketi => Sinif switch
    {
        MekanSinifi.Kaynak => "Kaynak",
        MekanSinifi.Alici  => "Alıcı",
        MekanSinifi.Gecis  => "Geçiş",
        MekanSinifi.HesapDisi => "Hesap Dışı",
        _                  => "Tanımsız",
    };

    public string SinifRenk => Sinif switch
    {
        MekanSinifi.Kaynak => "#E67E22",
        MekanSinifi.Alici  => "#2980B9",
        MekanSinifi.Gecis  => "#7F8C8D",
        MekanSinifi.HesapDisi => "#555555",
        _                  => "#444444",
    };

    public string KaynakRenkSidebar => KaynakDerecesi switch
    {
        "OG" => "#A0500A",
        "YG" => "#8B1A1A",
        "DG" => "#1A6B3A",
        _    => "#333333",
    };

    public string AliciRenkSidebar => AliciSinifi switch
    {
        "I"  => "#1A5A8B",
        "II" => "#3A6A8B",
        "III"=> "#4A5A5F",
        _    => "#333333",
    };

    /// UX — Atama tamam mı: tüm duvarlara (Bolme.YapiElemani) + her duvardaki açıklıklara
    /// (kapı/pencere) + döşeme + tavan malzemesi atanmış mı. Mekan listesinde yeşil
    /// işaretleme için kullanılır. P1 düzeltmesi — eskiden açıklık malzemesi kontrol
    /// edilmiyordu; kapı/pencere malzemesi eksikken de oda "atama tamam" (yeşil)
    /// görünebiliyordu, hesap motoru ise sessizce varsayılan 28/32 dB'ye düşüyordu.
    [JsonIgnore]
    public bool AtamaTamamMi =>
        Bolmeler.Count > 0 &&
        Bolmeler.All(b => b.YapiElemani != null && b.Acikliklar.All(a => a.YapiElemani != null)) &&
        ZeminYapiElemani != null &&
        TavanYapiElemani != null;

    /// UX — Oda adı rengi: atama bitince yeşil, aksi halde beyaz.
    [JsonIgnore]
    public string AtamaRenk => AtamaTamamMi ? "#2ECC71" : "#FFFFFF";

    /// UX — Liste ikon rengi: atama bitince yeşil, aksi halde nötr gri.
    [JsonIgnore]
    public string AtamaIsaretRenk => AtamaTamamMi ? "#2ECC71" : "#7894A8";

    /// UX — Duvar/döşeme/tavan ataması değiştiğinde listedeki yeşil işaretlemeyi tazeler.
    public void BildirAtamaDurumu()
    {
        OnPropertyChanged(nameof(AtamaTamamMi));
        OnPropertyChanged(nameof(AtamaRenk));
        OnPropertyChanged(nameof(AtamaIsaretRenk));
    }

    private double _alan;
    public double Alan // m2
    {
        get => _alan;
        set
        {
            if (_alan == value) return;
            _alan = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Hacim));
            OnPropertyChanged(nameof(HacimEtiketi));
            OnPropertyChanged(nameof(AlanHacimEtiketi));
        }
    }

    private double _yukseklik;
    public double Yukseklik // m
    {
        get => _yukseklik;
        set
        {
            if (_yukseklik == value) return;
            _yukseklik = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Hacim));
            OnPropertyChanged(nameof(HacimEtiketi));
            OnPropertyChanged(nameof(AlanHacimEtiketi));
        }
    }

    public double Hacim => Alan * Yukseklik; // m3

    /// UX — Düşey birleşim toplam hacmi. Bu mekan bir birleşime dahilse,
    /// birleşimdeki TÜM üyelerin Hacim toplamı. Hiçbir birleşime dahil değilse null.
    /// Serileştirilmez; DuseyMekanBirlesimServisi.HacimleriGuncelle ile set edilir.
    [JsonIgnore]
    public double? BirlesikHacimM3
    {
        get => _birlesikHacimM3;
        set
        {
            if (_birlesikHacimM3 == value) return;
            _birlesikHacimM3 = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HacimEtiketi));
            OnPropertyChanged(nameof(HacimTooltip));
            OnPropertyChanged(nameof(AlanHacimEtiketi));
        }
    }
    private double? _birlesikHacimM3;

    /// UX — Liste satırında gösterilen hacim etiketi.
    /// Birleşik ise "Σ 60 m³"; değilse "30 m³".
    public string HacimEtiketi => BirlesikHacimM3 is { } b
        ? $"Σ {b:F0} m³"
        : $"{Hacim:F0} m³";

    public string HacimTooltip => BirlesikHacimM3 is { } b
        ? $"Birleşik hacim: {b:F0} m³ (bu odanın katkısı {Hacim:F0} m³)"
        : "Mekan hacmi: alan × yükseklik";

    /// UX — Alan + hacim birleşik etiketi (liste satırı ve MEKAN başlığı için).
    /// Birleşik hacim varsa "51 m² · Σ 60 m³", yoksa "51 m² · 30 m³".
    public string AlanHacimEtiketi => BirlesikHacimM3 is { } b
        ? $"{Alan:F0} m² · Σ {b:F0} m³"
        : $"{Alan:F0} m² · {Hacim:F0} m³";

    public List<Point2D> Poligon { get; set; } = new();

    // Mekanı çevreleyen bölmeler
    private ObservableCollection<Bolme> _bolmeler = new();
    public ObservableCollection<Bolme> Bolmeler
    {
        get => _bolmeler;
        set
        {
            if (ReferenceEquals(_bolmeler, value)) return;
            BolmelerAboneCoz(_bolmeler);
            _bolmeler = value ?? new ObservableCollection<Bolme>();
            BolmelerAboneOl(_bolmeler);
            OnPropertyChanged();
            BildirAtamaDurumu();
        }
    }

    public Mekan() => BolmelerAboneOl(_bolmeler);

    // UX — Bölme malzemesi (YapiElemani) değiştiğinde atama-durumu yeşil işaretlemesini
    // hangi atama yolundan gelirse gelsin merkezî olarak tazelemek için koleksiyona abone olunur.
    private void BolmelerAboneOl(ObservableCollection<Bolme> col)
    {
        col.CollectionChanged += BolmelerCollectionChanged;
        foreach (var b in col) b.PropertyChanged += BolmePropertyChanged;
    }

    private void BolmelerAboneCoz(ObservableCollection<Bolme> col)
    {
        col.CollectionChanged -= BolmelerCollectionChanged;
        foreach (var b in col) b.PropertyChanged -= BolmePropertyChanged;
    }

    private void BolmelerCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null) foreach (Bolme b in e.OldItems) b.PropertyChanged -= BolmePropertyChanged;
        if (e.NewItems != null) foreach (Bolme b in e.NewItems) b.PropertyChanged += BolmePropertyChanged;
        BildirAtamaDurumu();
    }

    private void BolmePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Bolme.YapiElemani)) BildirAtamaDurumu();
    }

    // TS EN 12354-1 yanal iletim yolları için oda yüzey elemanları
    private YapiElemani? _zeminYapiElemani;
    public YapiElemani? ZeminYapiElemani
    {
        get => _zeminYapiElemani;
        set
        {
            if (ReferenceEquals(_zeminYapiElemani, value)) return;
            _zeminYapiElemani = value;
            OnPropertyChanged();
            BildirAtamaDurumu();
        }
    }

    private YapiElemani? _tavanYapiElemani;
    public YapiElemani? TavanYapiElemani
    {
        get => _tavanYapiElemani;
        set
        {
            if (ReferenceEquals(_tavanYapiElemani, value)) return;
            _tavanYapiElemani = value;
            OnPropertyChanged();
            BildirAtamaDurumu();
        }
    }
    
    // Akustik Özellikler
    public double RT60 { get; set; } // Çınlama Süresi (s) - Tahmini veya hesaplanan

    // EK-2 çift sınıflandırma
    private string _kaynakDerecesi = "";
    /// Kaynak mekan gürültü derecesi: "OG" (Orta), "YG" (Yüksek), "DG" (Düşük). Boş = tanımsız.
    public string KaynakDerecesi
    {
        get => _kaynakDerecesi;
        set
        {
            if (_kaynakDerecesi == value) return;
            _kaynakDerecesi = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(KaynakRenkSidebar));
        }
    }

    private string _aliciSinifi = "";
    /// Alıcı mekan hassasiyet sınıfı: "I" (Çok hassas), "II" (Hassas), "III" (Az hassas). Boş = tanımsız.
    public string AliciSinifi
    {
        get => _aliciSinifi;
        set
        {
            if (_aliciSinifi == value) return;
            _aliciSinifi = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AliciRenkSidebar));
        }
    }

    /// A9 — Kaynak/alıcı mekan toplam emilim alanı A = Σ(αi·Si) [m²Sabin].
    /// 0 ise Sabine formülüyle T=0.5 s kabulüyle tahmini: A = 0.16·V/T = 0.32·V.
    public double EmilimAlani { get; set; } = 0.0;

    /// Gerçek veya tahmini emilim alanını döndürür.
    public double EtkiliEmilimAlani => EmilimAlani > 0 ? EmilimAlani : 0.32 * Hacim;

    // ── EK-5 tesisat/servis ekipmanı gürültü girdileri (kullanıcı beyanı) ────
    /// Sürekli çalışan servis ekipmanlarının bu mekanda oluşturduğu gürültü düzeyi
    /// L_Aeq,nT (dBA) — ölçüm veya üretici verisinden. 0 = girilmedi (değerlendirilmez).
    public double TesisatSurekliDb { get; set; }

    /// Kesikli çalışan servis ekipmanlarının en yüksek düzeyi L_AF,max,nT (dBA).
    /// 0 = girilmedi (değerlendirilmez).
    public double TesisatKesikliDb { get; set; }
}
