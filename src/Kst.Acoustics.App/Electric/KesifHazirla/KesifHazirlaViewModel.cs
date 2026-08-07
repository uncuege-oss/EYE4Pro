using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Kst.Acoustics.Core.Electric.KesifHazirla;

namespace Kst.Acoustics.App.Electric.KesifHazirla;

public sealed class KesifHazirlaViewModel : INotifyPropertyChanged
{
    private KesifProje _proje = CreateDefaultProject();
    private KesifPoz? _seciliPoz;
    private KesifPoz? _seciliOzelPoz;
    private KesifTablosu? _seciliTablo;
    private KesifSatir? _seciliSatir;
    private KesifIcmalSatiri? _seciliIcmalSatiri;
    private KesifBirimFiyatListesi? _seciliBirimFiyatListesi;
    private string _durumMesaji = "Keşif hazır.";
    private string _aramaMetni = "";
    private string _pozAtamaAramaMetni = "";
    private string _pozAtamaKalemAramaMetni = "";
    private bool _ozelPozModuAktif = false;
    private readonly KesifPozEslemeServisi _eslemeServisi;
    private KesifMetrajSatiri? _seciliEslenmemisKalem;
    private KesifPozEsleme? _seciliEsleme;
    private KesifReferansDosyasi? _seciliReferans;
    private KesifPoz? _atanacakPoz;

    public KesifHazirlaViewModel() : this(new KesifPozEslemeServisi())
    {
    }

    // Test ve özel veritabanı yolu için ayrıştırılmış ctor.
    public KesifHazirlaViewModel(KesifPozEslemeServisi eslemeServisi)
    {
        _eslemeServisi = eslemeServisi;
        YeniProjeCommand = new RelayCommand(YeniProje);
        PozEkleCommand = new RelayCommand(PozEkle);
        PozSilCommand = new RelayCommand(PozSil);
        OzelPozEkleCommand = new RelayCommand(OzelPozEkle);
        OzelPozSilCommand = new RelayCommand(OzelPozSil);
        OzelPozGuncelleCommand = new RelayCommand(OzelPozGuncelle);
        TabloEkleCommand = new RelayCommand(TabloEkle);
        AltTabloEkleCommand = new RelayCommand(AltTabloEkle);
        TabloSilCommand = new RelayCommand(TabloSil);
        PozuTabloyaEkleCommand = new RelayCommand(PozuTabloyaEkle);
        SatirSilCommand = new RelayCommand(SatirSil);
        IcmaliGuncelleCommand = new RelayCommand(HesaplariYenile);
        BirimFiyatListesiSilCommand = new RelayCommand(BirimFiyatListesiSil);
        BirimFiyatListesiniAktifYapCommand = new RelayCommand(BirimFiyatListesiniAktifYap);
        BirimFiyatListesiniPasifeAlCommand = new RelayCommand(BirimFiyatListesiniPasifeAl);
        ToggleOzelPozModuCommand = new RelayCommand(ToggleOzelPozModu);
        SeciliTablo = Proje.Tablolar.FirstOrDefault();
        HesaplariYenile();
        OzelPozlarYenile();
        FiltrelenmisPozKutuphaneYenile();
        FiltrelenmisAtamaPozlariYenile();
        EslemeleriYenile();
        EslenmemisKalemleriYenile();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public KesifProje Proje
    {
        get => _proje;
        private set
        {
            if (SetProperty(ref _proje, value))
            {
                OnPropertyChanged(nameof(PozKutuphane));
                OnPropertyChanged(nameof(OzelPozlar));
                OnPropertyChanged(nameof(BirimFiyatListeleri));
                OnPropertyChanged(nameof(Tablolar));
                OnPropertyChanged(nameof(ReferansDosyalari));
                OnPropertyChanged(nameof(ProjeBilgileri));
                OnPropertyChanged(nameof(GenelToplam));
                FiltrelenmisPozKutuphaneYenile();
                FiltrelenmisAtamaPozlariYenile();
            }
        }
    }

    public ObservableCollection<KesifPoz> PozKutuphane => Proje.PozKutuphane;
    public ObservableCollection<KesifBirimFiyatListesi> BirimFiyatListeleri => Proje.BirimFiyatListeleri;
    public ObservableCollection<KesifTablosu> Tablolar => Proje.Tablolar;
    public ObservableCollection<KesifIcmalSatiri> IcmalSatirlari { get; } = new();

    public KesifPoz? SeciliPoz { get => _seciliPoz; set => SetProperty(ref _seciliPoz, value); }
    public KesifPoz? SeciliOzelPoz { get => _seciliOzelPoz; set => SetProperty(ref _seciliOzelPoz, value); }
    public ObservableCollection<KesifPoz> OzelPozlar { get; } = new();
    public ObservableCollection<KesifPoz> FiltrelenmisPozKutuphane { get; } = new();
    public ObservableCollection<KesifPoz> FiltrelenmisAtamaPozlari { get; } = new();

    // --- Referanslar ve Poz Atama ---
    public ObservableCollection<KesifReferansDosyasi> ReferansDosyalari => Proje.ReferansDosyalari;
    public ObservableCollection<KesifPozEsleme> PozEslemeleri { get; } = new();
    public ObservableCollection<KesifMetrajSatiri> EslenmemisKalemler { get; } = new();
    public ObservableCollection<KesifPozEsleme> FiltrelenmisPozEslemeleri { get; } = new();
    public ObservableCollection<KesifMetrajSatiri> FiltrelenmisEslenmemisKalemler { get; } = new();

    public KesifReferansDosyasi? SeciliReferans { get => _seciliReferans; set => SetProperty(ref _seciliReferans, value); }
    public KesifMetrajSatiri? SeciliEslenmemisKalem { get => _seciliEslenmemisKalem; set => SetProperty(ref _seciliEslenmemisKalem, value); }
    public KesifPozEsleme? SeciliEsleme { get => _seciliEsleme; set => SetProperty(ref _seciliEsleme, value); }

    /// <summary>Poz Atama penceresinde seçilen poz (eşleştirilecek/güncellenecek).</summary>
    public KesifPoz? AtanacakPoz { get => _atanacakPoz; set => SetProperty(ref _atanacakPoz, value); }

    public string PozAtamaAramaMetni
    {
        get => _pozAtamaAramaMetni;
        set
        {
            if (SetProperty(ref _pozAtamaAramaMetni, value ?? ""))
                FiltrelenmisAtamaPozlariYenile();
        }
    }

    public string PozAtamaKalemAramaMetni
    {
        get => _pozAtamaKalemAramaMetni;
        set
        {
            if (SetProperty(ref _pozAtamaKalemAramaMetni, value ?? ""))
                PozAtamaKalemFiltreleriniYenile();
        }
    }

    public string EslenmemisKalemOzet
        => $"Eşlenmemiş Kalemler ({FiltrelenmisEslenmemisKalemler.Count}/{EslenmemisKalemler.Count})";

    public string TanimliEslemeOzet
        => $"Tanımlı Eşleşmeler ({FiltrelenmisPozEslemeleri.Count}/{PozEslemeleri.Count})";

    public bool OzelPozModuAktif
    {
        get => _ozelPozModuAktif;
        set
        {
            if (SetProperty(ref _ozelPozModuAktif, value))
            {
                OnPropertyChanged(nameof(OzelPozModuAktif));
                OnPropertyChanged(nameof(BirimFiyatModuAktif));
                OnPropertyChanged(nameof(ToggleModuButonMetni));
                OnPropertyChanged(nameof(ModTitle));
                OnPropertyChanged(nameof(IsGridReadOnly));
                FiltrelenmisPozKutuphaneYenile();
            }
        }
    }

    public bool BirimFiyatModuAktif => !OzelPozModuAktif;
    public string ToggleModuButonMetni => OzelPozModuAktif ? "Birim Fiyatlara Geç" : "Özel Pozlara Geç";
    public string ModTitle => OzelPozModuAktif ? "Özel Pozlar" : "Birim Fiyat Pozları";
    public bool IsGridReadOnly => !OzelPozModuAktif;

    public string AramaMetni
    {
        get => _aramaMetni;
        set
        {
            if (SetProperty(ref _aramaMetni, value ?? ""))
            {
                FiltrelenmisPozKutuphaneYenile();
            }
        }
    }

    public KesifBirimFiyatListesi? SeciliBirimFiyatListesi
    {
        get => _seciliBirimFiyatListesi;
        set => SetProperty(ref _seciliBirimFiyatListesi, value);
    }

    public KesifTablosu? SeciliTablo
    {
        get => _seciliTablo;
        set
        {
            if (SetProperty(ref _seciliTablo, value))
            {
                OnPropertyChanged(nameof(SeciliTabloSatirlari));
                OnPropertyChanged(nameof(SeciliTabloToplam));
                OnPropertyChanged(nameof(SeciliTabloYaprakMi));
            }
        }
    }

    /// <summary>Seçili tablo satır kabul ediyor mu (alt tablosu yok = yaprak). Başlık tablolarında satır editörü pasif.</summary>
    public bool SeciliTabloYaprakMi => SeciliTablo?.YaprakMi ?? false;

    /// <summary>Sağ paneldeki "Proje Bilgileri" künyesi.</summary>
    public KesifProjeBilgileri ProjeBilgileri => Proje.Bilgiler;

    public KesifSatir? SeciliSatir { get => _seciliSatir; set => SetProperty(ref _seciliSatir, value); }

    public KesifIcmalSatiri? SeciliIcmalSatiri
    {
        get => _seciliIcmalSatiri;
        set
        {
            if (SetProperty(ref _seciliIcmalSatiri, value))
                OnPropertyChanged(nameof(SeciliIcmalKirilimlar));
        }
    }

    public ObservableCollection<KesifSatir>? SeciliTabloSatirlari => SeciliTablo?.Satirlar;
    public ObservableCollection<KesifIcmalKirilim>? SeciliIcmalKirilimlar => SeciliIcmalSatiri?.Kirilimlar;
    public decimal SeciliTabloToplam => SeciliTablo?.Toplam ?? 0;
    public decimal GenelToplam => Proje.GenelToplam;

    public string DurumMesaji { get => _durumMesaji; set => SetProperty(ref _durumMesaji, value); }

    public ICommand YeniProjeCommand { get; }
    public ICommand PozEkleCommand { get; }
    public ICommand PozSilCommand { get; }
    public ICommand OzelPozEkleCommand { get; }
    public ICommand OzelPozSilCommand { get; }
    public ICommand OzelPozGuncelleCommand { get; }
    public ICommand TabloEkleCommand { get; }
    public ICommand AltTabloEkleCommand { get; }
    public ICommand TabloSilCommand { get; }
    public ICommand PozuTabloyaEkleCommand { get; }
    public ICommand SatirSilCommand { get; }
    public ICommand IcmaliGuncelleCommand { get; }
    public ICommand BirimFiyatListesiSilCommand { get; }
    public ICommand BirimFiyatListesiniAktifYapCommand { get; }
    public ICommand BirimFiyatListesiniPasifeAlCommand { get; }
    public ICommand ToggleOzelPozModuCommand { get; }

    public void YeniProje()
    {
        _aramaMetni = "";
        _pozAtamaAramaMetni = "";
        _pozAtamaKalemAramaMetni = "";
        OnPropertyChanged(nameof(AramaMetni));
        OnPropertyChanged(nameof(PozAtamaAramaMetni));
        OnPropertyChanged(nameof(PozAtamaKalemAramaMetni));
        Proje = CreateDefaultProject();
        SeciliPoz = FiltrelenmisPozKutuphane.FirstOrDefault();
        SeciliBirimFiyatListesi = Proje.BirimFiyatListeleri.FirstOrDefault();
        SeciliTablo = Proje.Tablolar.FirstOrDefault();
        SeciliSatir = null;
        DurumMesaji = "Yeni keşif oluşturuldu.";
        HesaplariYenile();
        OzelPozlarYenile();
        EslenmemisKalemleriYenile();
    }

    public void ProjeYukle(KesifProje proje)
    {
        _aramaMetni = "";
        _pozAtamaAramaMetni = "";
        _pozAtamaKalemAramaMetni = "";
        OnPropertyChanged(nameof(AramaMetni));
        OnPropertyChanged(nameof(PozAtamaAramaMetni));
        OnPropertyChanged(nameof(PozAtamaKalemAramaMetni));
        Proje = proje;
        // Eski projelerde orijinal birim fiyat yok; güncel fiyatı baz al (değişmemiş sayılır).
        foreach (var tablo in KesifTabloAgaci.TumTablolar(Proje))
            foreach (var satir in tablo.Satirlar)
                satir.OrijinalBirimFiyat ??= satir.BirimFiyat;
        PozKutuphaneYenile();
        SeciliPoz = FiltrelenmisPozKutuphane.FirstOrDefault();
        SeciliBirimFiyatListesi = Proje.BirimFiyatListeleri.FirstOrDefault();
        SeciliTablo = Proje.Tablolar.FirstOrDefault();
        SeciliSatir = SeciliTablo?.Satirlar.FirstOrDefault();
        DurumMesaji = "Keşif dosyası açıldı.";
        HesaplariYenile();
        OzelPozlarYenile();
        EslenmemisKalemleriYenile();
    }

    public void BirimFiyatListesiIceAktar(string listeAdi, string kaynakDosya, IEnumerable<KesifPoz> pozlar, bool digerListeleriPasifeAl)
    {
        try
        {
            var liste = KesifBirimFiyatListesiServisi.ListeEkle(Proje, listeAdi, kaynakDosya, pozlar, digerListeleriPasifeAl);
            SeciliBirimFiyatListesi = liste;
            FiltrelenmisPozKutuphaneYenile();
            SeciliPoz = FiltrelenmisPozKutuphane.FirstOrDefault();
            OnPropertyChanged(nameof(PozKutuphane));
            OnPropertyChanged(nameof(BirimFiyatListeleri));
            DurumMesaji = $"{liste.Ad} yüklendi. {liste.PozSayisi} poz aktif kütüphaneye alındı.";
        }
        catch (Exception ex)
        {
            DurumMesaji = $"Birim fiyat listesi yüklenemedi: {ex.Message}";
        }
    }

    private void BirimFiyatListesiSil()
    {
        if (SeciliBirimFiyatListesi is null)
            return;

        Proje.BirimFiyatListeleri.Remove(SeciliBirimFiyatListesi);
        SeciliBirimFiyatListesi = Proje.BirimFiyatListeleri.FirstOrDefault();
        PozKutuphaneYenile();
        DurumMesaji = "Birim fiyat listesi silindi.";
    }

    private void BirimFiyatListesiniAktifYap()
    {
        if (SeciliBirimFiyatListesi is null)
            return;

        KesifBirimFiyatListesiServisi.ListeyiAktifYap(Proje, SeciliBirimFiyatListesi);
        PozKutuphaneYenile();
        DurumMesaji = $"{SeciliBirimFiyatListesi.Ad} aktif edildi.";
    }

    private void BirimFiyatListesiniPasifeAl()
    {
        if (SeciliBirimFiyatListesi is null)
            return;

        KesifBirimFiyatListesiServisi.ListeyiPasifeAl(Proje, SeciliBirimFiyatListesi);
        PozKutuphaneYenile();
        DurumMesaji = $"{SeciliBirimFiyatListesi.Ad} pasife alındı.";
    }

    public void PozKutuphaneYenile()
    {
        KesifBirimFiyatListesiServisi.PozKutuphaneYenile(Proje);
        FiltrelenmisPozKutuphaneYenile();
        FiltrelenmisAtamaPozlariYenile();
        SeciliPoz = FiltrelenmisPozKutuphane.FirstOrDefault();
        OzelPozlarYenile();
        OnPropertyChanged(nameof(PozKutuphane));
        OnPropertyChanged(nameof(BirimFiyatListeleri));
    }

    public void OzelPozlarYenile()
    {
        OzelPozlar.Clear();
        foreach (var poz in Proje.PozKutuphane.Where(p => !p.ListePozuMu))
        {
            OzelPozlar.Add(poz);
        }
        if (SeciliOzelPoz == null || !OzelPozlar.Contains(SeciliOzelPoz))
        {
            SeciliOzelPoz = OzelPozlar.FirstOrDefault();
        }
    }

    public void FiltrelenmisPozKutuphaneYenile()
    {
        var aranan = _aramaMetni.Trim();
        var oncekiSecili = _seciliPoz;

        FiltrelenmisPozKutuphane.Clear();

        var baseList = Proje.PozKutuphane.Where(p => p.ListePozuMu == !OzelPozModuAktif);

        IEnumerable<KesifPoz> filtrelenmis;
        if (string.IsNullOrWhiteSpace(aranan))
        {
            filtrelenmis = baseList;
        }
        else
        {
            filtrelenmis = baseList.Where(p => 
                (p.PozNo != null && p.PozNo.Contains(aranan, StringComparison.CurrentCultureIgnoreCase)) ||
                (p.BirimAdi != null && p.BirimAdi.Contains(aranan, StringComparison.CurrentCultureIgnoreCase))
            );
        }

        foreach (var poz in filtrelenmis)
        {
            FiltrelenmisPozKutuphane.Add(poz);
        }

        if (oncekiSecili != null && FiltrelenmisPozKutuphane.Contains(oncekiSecili))
        {
            SeciliPoz = oncekiSecili;
        }
        else
        {
            SeciliPoz = FiltrelenmisPozKutuphane.FirstOrDefault();
        }
    }

    public void FiltrelenmisAtamaPozlariYenile()
    {
        var aranan = _pozAtamaAramaMetni.Trim();
        var oncekiSecili = _atanacakPoz;

        FiltrelenmisAtamaPozlari.Clear();
        var pozlar = string.IsNullOrWhiteSpace(aranan)
            ? Proje.PozKutuphane.AsEnumerable()
            : Proje.PozKutuphane.Where(p =>
                p.PozNo.Contains(aranan, StringComparison.CurrentCultureIgnoreCase) ||
                p.BirimAdi.Contains(aranan, StringComparison.CurrentCultureIgnoreCase));

        foreach (var poz in pozlar)
            FiltrelenmisAtamaPozlari.Add(poz);

        AtanacakPoz = oncekiSecili is not null && FiltrelenmisAtamaPozlari.Contains(oncekiSecili)
            ? oncekiSecili
            : FiltrelenmisAtamaPozlari.FirstOrDefault();
    }

    public void PozAtamaKalemFiltreleriniYenile()
    {
        var aranan = _pozAtamaKalemAramaMetni.Trim();
        var oncekiKalem = SeciliEslenmemisKalem;
        var oncekiEsleme = SeciliEsleme;

        FiltrelenmisEslenmemisKalemler.Clear();
        foreach (var kalem in EslenmemisKalemler.Where(k =>
                     string.IsNullOrWhiteSpace(aranan) || KalemAramaylaEslesir(k, aranan)))
            FiltrelenmisEslenmemisKalemler.Add(kalem);

        FiltrelenmisPozEslemeleri.Clear();
        foreach (var esleme in PozEslemeleri.Where(e =>
                     string.IsNullOrWhiteSpace(aranan) || EslemeAramaylaEslesir(e, aranan)))
            FiltrelenmisPozEslemeleri.Add(esleme);

        SeciliEslenmemisKalem = oncekiKalem is not null && FiltrelenmisEslenmemisKalemler.Contains(oncekiKalem)
            ? oncekiKalem
            : FiltrelenmisEslenmemisKalemler.FirstOrDefault();
        SeciliEsleme = oncekiEsleme is not null && FiltrelenmisPozEslemeleri.Contains(oncekiEsleme)
            ? oncekiEsleme
            : FiltrelenmisPozEslemeleri.FirstOrDefault();

        OnPropertyChanged(nameof(EslenmemisKalemOzet));
        OnPropertyChanged(nameof(TanimliEslemeOzet));
    }

    private static bool KalemAramaylaEslesir(KesifMetrajSatiri kalem, string aranan)
        => MetinIcerir(kalem.AnaGrup, aranan)
           || MetinIcerir(kalem.AltGrup, aranan)
           || MetinIcerir(kalem.TeknikOzellik, aranan)
           || MetinIcerir(kalem.Birim, aranan)
           || MetinIcerir(kalem.Aciklama, aranan);

    private static bool EslemeAramaylaEslesir(KesifPozEsleme esleme, string aranan)
        => MetinIcerir(esleme.AnaGrup, aranan)
           || MetinIcerir(esleme.AltGrup, aranan)
           || MetinIcerir(esleme.TeknikOzellik, aranan)
           || MetinIcerir(esleme.Birim, aranan)
           || MetinIcerir(esleme.PozNo, aranan)
           || MetinIcerir(esleme.BirimAdi, aranan);

    private static bool MetinIcerir(string? metin, string aranan)
        => !string.IsNullOrWhiteSpace(metin)
           && metin.Contains(aranan, StringComparison.CurrentCultureIgnoreCase);

    private void ToggleOzelPozModu()
    {
        OzelPozModuAktif = !OzelPozModuAktif;
    }

    public void HesaplariYenile()
    {
        var seciliAnahtar = SeciliSatir is null
            ? null
            : KesifSatirBirlestirmeServisi.AnahtarUret(SeciliSatir.PozNo, SeciliSatir.BirimAdi, SeciliSatir.Birim);
        var birlestirilen = KesifSatirBirlestirmeServisi.ProjeyiBirlestir(Proje);
        if (seciliAnahtar is not null && (SeciliSatir is null || SeciliTablo?.Satirlar.Contains(SeciliSatir) != true))
        {
            SeciliSatir = SeciliTablo?.Satirlar.FirstOrDefault(s =>
                KesifSatirBirlestirmeServisi.AnahtarUret(s.PozNo, s.BirimAdi, s.Birim) == seciliAnahtar);
        }

        foreach (var tablo in Proje.Tablolar)
            tablo.ToplamiYenile();

        ReferansBilgileriniYenile();
        Proje.ToplamlariYenile();
        IcmalSatirlari.Clear();
        foreach (var row in KesifHesapServisi.IcmalOlustur(Proje))
            IcmalSatirlari.Add(row);

        SeciliIcmalSatiri = IcmalSatirlari.FirstOrDefault();
        OnPropertyChanged(nameof(SeciliTabloToplam));
        OnPropertyChanged(nameof(GenelToplam));
        DurumMesaji = birlestirilen > 0
            ? $"{birlestirilen} yinelenen poz birleştirildi. İcmalde {IcmalSatirlari.Count} poz/birim var."
            : $"İcmal güncellendi. {IcmalSatirlari.Count} poz/birim var.";
    }

    private void PozEkle()
    {
        var poz = new KesifPoz { PozNo = BenzersizPozNo("YENİ"), BirimAdi = "Yeni imalat", Birim = "adet", ListePozuMu = false };
        ManuelPozEkle(poz);
        Proje.PozKutuphane.Add(poz);
        FiltrelenmisPozKutuphaneYenile();
        SeciliPoz = poz;
        DurumMesaji = "Poz eklendi.";
    }

    // HATA-6: Varsayılan poz numarası benzersiz olmalı. Aksi halde birden çok
    // "Özel Poz" aynı PozNo+Birim ile icmalde tek satırda birleşip toplanır.
    private string BenzersizPozNo(string onek)
    {
        int n = 1;
        string aday;
        do
        {
            aday = $"{onek}-{n:000}";
            n++;
        }
        while (Proje.PozKutuphane.Any(p => string.Equals(p.PozNo, aday, StringComparison.OrdinalIgnoreCase)));
        return aday;
    }

    private void PozSil()
    {
        if (SeciliPoz is null)
            return;

        if (SeciliPoz.ListePozuMu && SeciliPoz.BirimFiyatListesiId is Guid listId)
        {
            var list = Proje.BirimFiyatListeleri.FirstOrDefault(l => l.Id == listId);
            list?.Pozlar.Remove(SeciliPoz);
        }

        if (!SeciliPoz.ListePozuMu)
            ManuelPozSil(SeciliPoz);

        Proje.PozKutuphane.Remove(SeciliPoz);
        FiltrelenmisPozKutuphaneYenile();
        SeciliPoz = FiltrelenmisPozKutuphane.FirstOrDefault();
        DurumMesaji = "Poz kütüphaneden silindi.";
    }

    private void OzelPozEkle()
    {
        var poz = new KesifPoz
        {
            PozNo = BenzersizPozNo("ÖZEL"),
            BirimAdi = "Yeni özel imalat",
            Birim = "adet",
            BirimFiyat = 0,
            ListePozuMu = false,
            SonGuncellemeTarihi = DateTime.Now
        };
        ManuelPozEkle(poz);
        Proje.PozKutuphane.Add(poz);
        FiltrelenmisPozKutuphaneYenile();
        SeciliPoz = poz;
        OzelPozlarYenile();
        SeciliOzelPoz = poz;   // HATA-8: yeni eklenen poz seçili kalsın
        DurumMesaji = "Özel poz eklendi.";
    }

    private void OzelPozSil()
    {
        var target = OzelPozModuAktif ? SeciliPoz : SeciliOzelPoz;
        if (target is null)
            return;

        ManuelPozSil(target);
        Proje.PozKutuphane.Remove(target);
        FiltrelenmisPozKutuphaneYenile();
        OzelPozlarYenile();
        SeciliPoz = FiltrelenmisPozKutuphane.FirstOrDefault();
        DurumMesaji = "Özel poz silindi.";
    }

    private void OzelPozGuncelle()
    {
        if (SeciliOzelPoz is null)
            return;

        SeciliOzelPoz.SonGuncellemeTarihi = DateTime.Now;
        HesaplariYenile();
        DurumMesaji = "Özel poz güncellendi.";
    }

    public void OzelPozDuzenlemeTamamlandi(KesifPoz poz)
    {
        if (poz is null)
            return;
        poz.SonGuncellemeTarihi = DateTime.Now;
        HesaplariYenile();
    }

    private void ManuelPozEkle(KesifPoz poz)
    {
        poz.ListePozuMu = false;
        poz.BirimFiyatListesiId = null;
        poz.BirimFiyatListesiAdi = "";
        if (Proje.ManuelPozlar.All(p => p.Id != poz.Id))
            Proje.ManuelPozlar.Add(poz);
    }

    private void ManuelPozSil(KesifPoz poz)
    {
        var mevcut = Proje.ManuelPozlar.FirstOrDefault(p => p.Id == poz.Id);
        if (mevcut is not null)
            Proje.ManuelPozlar.Remove(mevcut);
    }

    private void TabloEkle()
    {
        var tablo = new KesifTablosu { Ad = $"Keşif Tablosu {Proje.Tablolar.Count + 1}" };
        Proje.Tablolar.Add(tablo);
        SeciliTablo = tablo;
        DurumMesaji = "Keşif tablosu eklendi.";
    }

    private void AltTabloEkle()
    {
        if (SeciliTablo is null)
        {
            DurumMesaji = "Alt tablo eklemek için bir tablo seçin.";
            return;
        }

        if (KesifTabloAgaci.Derinlik(Proje, SeciliTablo) >= KesifTabloAgaci.MaksimumDerinlik)
        {
            DurumMesaji = $"En fazla {KesifTabloAgaci.MaksimumDerinlik} seviye olabilir; bu tabloya alt tablo eklenemez.";
            return;
        }

        // Üst tablolar yalnız başlıktır: satırı olan bir tabloya alt tablo eklenince satırlar kaybolmamalı.
        if (SeciliTablo.Satirlar.Count > 0)
        {
            DurumMesaji = "Bu tabloda satır var. Üst tablolar yalnız başlık olabilir; önce satırları silin veya boş bir tabloya alt tablo ekleyin.";
            return;
        }

        var ebeveyn = SeciliTablo;
        var alt = new KesifTablosu { Ad = $"{ebeveyn.Ad} - Alt {ebeveyn.AltTablolar.Count + 1}" };
        ebeveyn.AltTablolar.Add(alt);
        SeciliTablo = alt;
        ebeveyn.ToplamiYenile();
        DurumMesaji = "Alt keşif tablosu eklendi.";
    }

    private void TabloSil()
    {
        if (SeciliTablo is null)
            return;

        var ebeveyn = KesifTabloAgaci.Ebeveyn(Proje, SeciliTablo);
        if (ebeveyn is null)
        {
            // En üst seviye tablo — en az bir tane kalmalı.
            if (Proje.Tablolar.Count <= 1)
                return;
            Proje.Tablolar.Remove(SeciliTablo);
            SeciliTablo = Proje.Tablolar.FirstOrDefault();
        }
        else
        {
            ebeveyn.AltTablolar.Remove(SeciliTablo);
            SeciliTablo = ebeveyn;
            ebeveyn.ToplamiYenile();
        }

        HesaplariYenile();
    }

    private void PozuTabloyaEkle()
    {
        if (SeciliPoz is null || SeciliTablo is null)
            return;

        if (SeciliTablo.AltTabloVarMi)
        {
            DurumMesaji = "Bu tablo alt tablolar içeriyor (başlık tablosu); satır yalnız alt/yaprak tablolara eklenebilir.";
            return;
        }

        var anahtar = KesifSatirBirlestirmeServisi.AnahtarUret(SeciliPoz.PozNo, SeciliPoz.BirimAdi, SeciliPoz.Birim);
        var mevcut = SeciliTablo.Satirlar.FirstOrDefault(s =>
            KesifSatirBirlestirmeServisi.AnahtarUret(s.PozNo, s.BirimAdi, s.Birim) == anahtar);
        if (mevcut is not null)
        {
            KesifSatirBirlestirmeServisi.ManuelMiktarEkle(mevcut, 1);
            SeciliSatir = mevcut;
            HesaplariYenile();
            DurumMesaji = $"{SeciliPoz.PozNo} tabloda zaten vardı; miktarı 1 artırıldı.";
            return;
        }

        var satir = new KesifSatir
        {
            PozId = SeciliPoz.Id,
            PozNo = SeciliPoz.PozNo,
            BirimAdi = SeciliPoz.BirimAdi,
            Birim = SeciliPoz.Birim,
            BirimFiyat = SeciliPoz.BirimFiyat,
            OrijinalBirimFiyat = SeciliPoz.BirimFiyat,
            Miktar = 1,
            Aciklama = SeciliPoz.Aciklama
        };

        SeciliTablo.Satirlar.Add(satir);
        SeciliSatir = satir;
        HesaplariYenile();
    }

    private void SatirSil()
    {
        if (SeciliTablo is null || SeciliSatir is null)
            return;

        SeciliTablo.Satirlar.Remove(SeciliSatir);
        SeciliSatir = SeciliTablo.Satirlar.FirstOrDefault();
        HesaplariYenile();
    }

    // ============================ Referanslar ve Poz Atama ============================

    /// <summary>Yeni bir metraj referansını projeye ekler ve eşleşmiş kalemleri keşfe işler.</summary>
    public void ReferansEkle(KesifReferansDosyasi referans)
    {
        Proje.ReferansDosyalari.Add(referans);
        SeciliReferans = referans;
        var uretilen = OtomatikSatirlariUret(referans);
        EslenmemisKalemleriYenile();
        HesaplariYenile();
        var eslenmemis = referans.MetrajSatirlari.Count - uretilen;
        DurumMesaji = eslenmemis > 0
            ? $"{referans.DosyaAdi} yüklendi. {uretilen} satır keşfe işlendi, {eslenmemis} kalem Poz Atama bekliyor."
            : $"{referans.DosyaAdi} yüklendi. {uretilen} satır keşfe işlendi.";
    }

    /// <summary>Referansı ve ondan üretilmiş otomatik keşif satırlarını kaldırır.
    /// Global poz eşleşmelerine DOKUNMAZ (öğrenilen veri korunur).</summary>
    public void ReferansSil(KesifReferansDosyasi referans)
    {
        if (referans is null)
            return;

        OtomatikSatirlariTemizle(referans.Id);
        Proje.ReferansDosyalari.Remove(referans);
        SeciliReferans = Proje.ReferansDosyalari.FirstOrDefault();
        EslenmemisKalemleriYenile();
        HesaplariYenile();
        DurumMesaji = $"{referans.DosyaAdi} referansı silindi. Poz eşleşmeleri korundu.";
    }

    /// <summary>Bir metraj kalemine poz atar, global veritabanına öğretir ve tüm referanslar için keşfi yeniler.</summary>
    public void EslemeAtaVeUygula(KesifMetrajSatiri kalem, KesifPoz poz)
    {
        if (kalem is null || poz is null)
            return;

        _eslemeServisi.EkleVeyaGuncelle(kalem, poz.PozNo, poz.BirimAdi);
        EslemeleriYenile();
        TumReferanslariYenidenIsle();
        EslenmemisKalemleriYenile();
        HesaplariYenile();
        DurumMesaji = $"'{kalem.TeknikOzellik}' → {poz.PozNo} eşlendi ve öğrenildi.";
    }

    /// <summary>Eşlenmemiş bütün metraj kalemleri için kütüphanedeki en uygun pozu bulup öğrenir.</summary>
    public void TumEslenmemisPozlariOtomatikAta()
    {
        var kalemler = EslenmemisKalemler.ToList();
        if (kalemler.Count == 0)
        {
            DurumMesaji = "Otomatik atanacak eşlenmemiş metraj kalemi yok.";
            return;
        }

        var sonuclar = KesifOtomatikPozEslemeServisi.Esle(kalemler, Proje.PozKutuphane);
        foreach (var sonuc in sonuclar)
        {
            _eslemeServisi.EkleVeyaGuncelle(
                sonuc.Kalem,
                sonuc.Poz.PozNo,
                sonuc.Poz.BirimAdi,
                hemenKaydet: false);
        }

        if (sonuclar.Count > 0)
            _eslemeServisi.Kaydet();

        EslemeleriYenile();
        TumReferanslariYenidenIsle();
        EslenmemisKalemleriYenile();
        HesaplariYenile();

        var bulunamayan = kalemler.Count - sonuclar.Count;
        DurumMesaji = bulunamayan == 0
            ? $"Otomatik atama tamamlandı: {sonuclar.Count} kalem eşlendi. Hatalı seçimleri alttan güncelleyebilirsiniz."
            : $"Otomatik atama: {sonuclar.Count} kalem eşlendi, {bulunamayan} kalem için uygun poz bulunamadı.";
    }

    /// <summary>Tanımlı bir eşleşmenin pozunu değiştirir (öğrenilmiş kaydı günceller) ve keşfi yeniler.</summary>
    public void EslemeGuncelle(KesifPozEsleme esleme, KesifPoz poz)
    {
        if (esleme is null || poz is null)
            return;

        esleme.PozNo = poz.PozNo;
        esleme.BirimAdi = poz.BirimAdi;
        esleme.SonGuncellemeTarihi = DateTime.Now;
        _eslemeServisi.Kaydet();
        PozAtamaKalemFiltreleriniYenile();
        TumReferanslariYenidenIsle();
        HesaplariYenile();
        DurumMesaji = $"Eşleşme güncellendi: {esleme.TeknikOzellik} → {poz.PozNo}.";
    }

    /// <summary>Tanımlı bir eşleşmeyi global veritabanından siler ve keşfi yeniler.</summary>
    public void EslemeSil(KesifPozEsleme esleme)
    {
        if (esleme is null)
            return;

        _eslemeServisi.Sil(esleme.Id);
        EslemeleriYenile();
        TumReferanslariYenidenIsle();
        EslenmemisKalemleriYenile();
        HesaplariYenile();
        DurumMesaji = "Poz eşleşmesi silindi.";
    }

    /// <summary>Bir referansın metraj satırlarından eşleşmesi olanları SEÇİLİ tabloya otomatik satır olarak ekler.
    /// Önce o referansın eski otomatik satırlarını temizler (idempotent). Üretilen satır sayısını döndürür.</summary>
    private int OtomatikSatirlariUret(KesifReferansDosyasi referans)
    {
        OtomatikSatirlariTemizle(referans.Id);

        var hedef = SeciliTablo;
        if (hedef is null)
        {
            DurumMesaji = "Önce bir keşif tablosu seçin; otomatik satırlar oraya yazılır.";
            return 0;
        }

        if (hedef.AltTabloVarMi)
        {
            DurumMesaji = "Seçili tablo başlık tablosudur (alt tabloları var); otomatik satırlar için bir yaprak tablo seçin.";
            return 0;
        }

        var uretilen = 0;
        foreach (var kalem in referans.MetrajSatirlari)
        {
            var esleme = _eslemeServisi.EslemeBul(kalem);
            if (esleme is null)
                continue;

            var poz = Proje.PozKutuphane.FirstOrDefault(p =>
                string.Equals(p.PozNo, esleme.PozNo, StringComparison.OrdinalIgnoreCase));

            var pozNo = esleme.PozNo;
            var birimAdi = poz?.BirimAdi ?? esleme.BirimAdi;
            var birim = string.IsNullOrWhiteSpace(kalem.Birim) ? (poz?.Birim ?? "adet") : kalem.Birim;
            var anahtar = KesifSatirBirlestirmeServisi.AnahtarUret(pozNo, birimAdi, birim);
            var satir = hedef.Satirlar.FirstOrDefault(s =>
                KesifSatirBirlestirmeServisi.AnahtarUret(s.PozNo, s.BirimAdi, s.Birim) == anahtar);

            if (satir is null)
            {
                satir = new KesifSatir
                {
                    PozId = poz?.Id ?? Guid.Empty,
                    PozNo = pozNo,
                    BirimAdi = birimAdi,
                    Birim = birim,
                    BirimFiyat = poz?.BirimFiyat ?? 0,
                    OrijinalBirimFiyat = poz?.BirimFiyat ?? 0,
                    Miktar = 0,
                    Aciklama = kalem.TeknikOzellik
                };
                hedef.Satirlar.Add(satir);
            }

            KesifSatirBirlestirmeServisi.OtomatikKatkiEkle(
                satir, referans.Id, referans.KaynakModul, kalem.Miktar);
            uretilen++;
        }

        return uretilen;
    }

    /// <summary>Verilen referanstan üretilmiş tüm otomatik satırları her tablodan kaldırır.</summary>
    private void OtomatikSatirlariTemizle(Guid referansId)
    {
        foreach (var tablo in KesifTabloAgaci.TumTablolar(Proje))
        {
            foreach (var satir in tablo.Satirlar.ToList())
            {
                KesifSatirBirlestirmeServisi.LegacyKatkiyiTamamla(satir);
                if (satir.OtomatikKatkilar.All(k => k.KaynakReferansId != referansId))
                    continue;
                if (KesifSatirBirlestirmeServisi.ReferansKatkisiniSil(satir, referansId))
                    tablo.Satirlar.Remove(satir);
            }
        }
    }

    /// <summary>Her keşif satırı için fare balonu metnini (hangi referanstan ne miktar geldiği) günceller.
    /// Manuel satırlarda null bırakır (balon gösterilmez).</summary>
    private void ReferansBilgileriniYenile()
    {
        foreach (var tablo in KesifTabloAgaci.TumTablolar(Proje))
        {
            foreach (var satir in tablo.Satirlar)
            {
                KesifSatirBirlestirmeServisi.LegacyKatkiyiTamamla(satir);
                if (satir.OtomatikKatkilar.Count == 0)
                {
                    satir.ReferansBilgisi = null;
                    continue;
                }

                var birim = string.IsNullOrWhiteSpace(satir.Birim) ? "" : " " + satir.Birim;
                var satirlar = new List<string>();
                foreach (var katki in satir.OtomatikKatkilar)
                {
                    var ad = Proje.ReferansDosyalari.FirstOrDefault(r => r.Id == katki.KaynakReferansId)?.DosyaAdi;
                    if (string.IsNullOrWhiteSpace(ad))
                        ad = string.IsNullOrWhiteSpace(katki.KaynakModul) ? "Referans" : katki.KaynakModul;
                    satirlar.Add($"• {ad}: {katki.Miktar:0.##}{birim}");
                }

                var otomatikToplam = satir.OtomatikKatkilar.Sum(k => k.Miktar);
                var manuel = satir.Miktar - otomatikToplam;
                if (manuel > 0.0000001m)
                    satirlar.Add($"• Manuel: {manuel:0.##}{birim}");

                satir.ReferansBilgisi = "Referans katkıları:\n" + string.Join("\n", satirlar);
            }
        }
    }

    /// <summary>Tüm referansları yeniden işler (eşleşme eklendi/silindi sonrası senkronizasyon).</summary>
    private void TumReferanslariYenidenIsle()
    {
        foreach (var referans in Proje.ReferansDosyalari)
            OtomatikSatirlariUret(referans);
    }

    public void EslemeleriYenile()
    {
        PozEslemeleri.Clear();
        foreach (var esleme in _eslemeServisi.Tum())
            PozEslemeleri.Add(esleme);
        PozAtamaKalemFiltreleriniYenile();
    }

    /// <summary>"Kontrol edildi" tiki değişince eşleşme veritabanını diske yazar.</summary>
    public void EslemeKontrolDurumuKaydet()
    {
        _eslemeServisi.Kaydet();
        var kontrolsuz = PozEslemeleri.Count(e => !e.KontrolEdildi);
        DurumMesaji = kontrolsuz > 0
            ? $"Kontrol durumu kaydedildi. {kontrolsuz} atama henüz kontrol edilmedi."
            : "Tüm poz atamaları kontrol edildi.";
    }

    /// <summary>Tüm referansların metraj kalemlerinden, global veritabanında eşleşmesi OLMAYAN
    /// benzersiz kalemleri (anahtara göre) Poz Atama listesine doldurur.</summary>
    public void EslenmemisKalemleriYenile()
    {
        EslenmemisKalemler.Clear();
        var gorulen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var referans in Proje.ReferansDosyalari)
        {
            foreach (var kalem in referans.MetrajSatirlari)
            {
                if (_eslemeServisi.EslemeBul(kalem) is not null)
                    continue;
                if (gorulen.Add(kalem.EslemeAnahtari))
                    EslenmemisKalemler.Add(kalem);
            }
        }

        PozAtamaKalemFiltreleriniYenile();
    }

    private static KesifProje CreateDefaultProject()
    {
        var proje = new KesifProje { Ad = "Yeni Keşif" };
        proje.Tablolar.Add(new KesifTablosu { Ad = "Keşif Tablosu 1" });
        return proje;
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
