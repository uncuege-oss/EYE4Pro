using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kst.Acoustics.Core.Electric.ZayifAkimTekHat;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kst.Acoustics.App.Electric.ZayifAkimTekHat;

public partial class ZayifAkimTekHatViewModel : ObservableObject
{
    [ObservableProperty] private ZayifAkimTekHatProje proje = new();
    [ObservableProperty] private ObservableCollection<ElvRackKabin> rackKabinler = new();
    [ObservableProperty] private ElvRackKabin? seciliRack;
    [ObservableProperty] private ObservableCollection<ElvCctvIzlemeNoktasi> cctvIzlemeNoktalari = new();
    [ObservableProperty] private ElvCctvIzlemeNoktasi? seciliCctvIzlemeNoktasi;
    [ObservableProperty] private ObservableCollection<ElvYanginKatLoopGrubu> yanginKatLoopGruplari = new();
    [ObservableProperty] private ElvYanginKatLoopGrubu? seciliYanginKatLoopGrubu;
    [ObservableProperty] private ElvYanginLoop? seciliYanginLoop;
    [ObservableProperty] private ObservableCollection<ElvYanginLoopCihazi> seciliYanginLoopCihazlari = new();
    [ObservableProperty] private ElvYanginLoopCihazi? seciliYanginLoopCihazi;
    [ObservableProperty] private ObservableCollection<ElvAnonsZonu> analogAnonsZonlari = new();
    [ObservableProperty] private ElvAnonsZonu? seciliAnalogAnonsZonu;
    [ObservableProperty] private ElvAnalogHoparlorGrubu? seciliAnalogHatAGrubu;
    [ObservableProperty] private ElvAnalogHoparlorGrubu? seciliAnalogHatBGrubu;
    [ObservableProperty] private bool merkezSecili;
    [ObservableProperty] private ObservableCollection<ElvCihaz> seciliCihazlar = new();
    [ObservableProperty] private ElvCihaz? seciliCihaz;
    [ObservableProperty] private string yeniCihazAdi = "";
    [ObservableProperty] private int yeniCihazAdet = 1;
    [ObservableProperty] private string durumMesaji = "Hazır.";
    [ObservableProperty] private bool yukleniyor;
    [ObservableProperty] private bool isDirty;
    [ObservableProperty] private int semaRevizyon;
    [ObservableProperty] private string aktifSistem = "Data";
    [ObservableProperty] private string yeniAnalogHoparlorKodu = "TAVAN-6W";
    [ObservableProperty] private int yeniAnalogHoparlorAdedi = 1;
    [ObservableProperty] private double yeniAnalogHoparlorTapGucu = 6;
    [ObservableProperty] private string yeniAnalogAmplifikatorKodu = "AMP-240";
    [ObservableProperty] private bool yeniAnalogAmplifikatorYedek;
    [ObservableProperty] private ElvAnonsAmplifikatoru? seciliAnalogAmplifikator;
    [ObservableProperty] private ObservableCollection<string> analogDogrulamaSatirlari = new();
    [ObservableProperty] private ObservableCollection<string> kapasiteDenetimSatirlari = new();
    [ObservableProperty] private ObservableCollection<ElvIpTvAkisi> ipTvAkislari = new();
    [ObservableProperty] private ElvIpTvAkisi? seciliIpTvAkisi;

    /// <summary>Pencere başlığı; kaydedilmemiş değişiklik varsa '*' eklenir (maincodex §4.2).</summary>
    public string PencereBasligi => $"Zayıf Akım Tek Hat Şeması — {Proje.ProjeAdi}{(IsDirty ? " *" : "")}";

    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(PencereBasligi));

    public ElvDataSistemi DataSistemi => Proje.DataSistemi;
    public ElvKameraSistemi KameraSistemi => Proje.KameraSistemi;
    public ElvAnonsSistemi AnonsSistemi => Proje.AnonsSistemi;
    public ElvTvSistemi TvSistemi => Proje.TvSistemi;
    public ElvYanginSistemi YanginSistemi => Proje.YanginSistemi;
    public ElvTelefonSistemi TelefonSistemi => Proje.TelefonSistemi;
    public ElvDiafonSistemi DiafonSistemi => Proje.DiafonSistemi;
    public List<ELVKatInfo> Katlar => Proje.Katlar;
    public ElvMerkez AnaMerkez => AktifSistem switch
    {
        "Kamera" => Proje.KameraSistemi.AnaMerkez,
        "Anons" => Proje.AnonsSistemi.AnaMerkez,
        "Tv" => Proje.TvSistemi.Teknoloji == TvDagitimTeknolojisi.IpTv
            ? Proje.TvSistemi.IpTv.AnaMerkez : Proje.TvSistemi.AnaMerkez,
        "Yangin" => Proje.YanginSistemi.AnaMerkez,
        "Telefon" => Proje.TelefonSistemi.AnaMerkez,
        "Diafon" => Proje.DiafonSistemi.AnaMerkez,
        _ => Proje.DataSistemi.AnaMerkez
    };
    public string AktifKutuphaneAltBaslikKodu => AktifSistem switch
    {
        "Kamera" => SeciliCctvIzlemeNoktasi is not null ? "KameraIzleme" : "Kamera",
        "Anons" => AnonsSistemi.Teknoloji == "Dijital (PoE)" ? "AnonsDijital" : "AnonsAnalog",
        "Tv" => TvSistemi.Teknoloji switch
        {
            TvDagitimTeknolojisi.DirektLnb => "TvDirekt",
            TvDagitimTeknolojisi.IpTv => "TvIp",
            _ => "TvMerkezi"
        },
        "Yangin" => "Yangin",
        "Telefon" => "Telefon",
        "Diafon" => "Diafon",
        _ => "Data"
    };

    private IReadOnlyList<string> YerlesikAktifCihazlar() => AktifSistem switch
    {
        "Kamera" => SeciliCctvIzlemeNoktasi is not null
            ? ElvCihazKatalogu.CctvIzlemeNoktasiCihazlari
            : ElvCihazKatalogu.KameraCihazlari,
        "Anons" => AnonsSistemi.Teknoloji == "Dijital (PoE)"
            ? (MerkezSecili ? ElvCihazKatalogu.AnonsDijitalAnaMerkezCihazlari : ElvCihazKatalogu.AnonsDijitalKatRackCihazlari)
            : ElvCihazKatalogu.AnonsAnalogCihazlari,
        "Tv" => TvSistemi.Teknoloji switch
        {
            TvDagitimTeknolojisi.DirektLnb => ElvCihazKatalogu.TvDirektLnbCihazlari,
            TvDagitimTeknolojisi.IpTv => ElvCihazKatalogu.IpTvCihazlari,
            _ => ElvCihazKatalogu.TvMerkeziCihazlari
        },
        "Yangin" => ElvCihazKatalogu.YanginCihazlari,
        "Telefon" => ElvCihazKatalogu.TelefonCihazlari,
        "Diafon" => ElvCihazKatalogu.DiafonCihazlari,
        // Data: Ana Merkez (sistem odası) ve kat rack farklı ekipman barındırır.
        _ => MerkezSecili ? ElvCihazKatalogu.DataAnaMerkezCihazlari : ElvCihazKatalogu.DataKatRackCihazlari
    };
    public IReadOnlyList<string> CihazKatalogu => ElvKutuphaneServisi.Birlestir(
        Proje, AktifKutuphaneAltBaslikKodu, ElvKutuphaneKalemTuru.Cihaz, YerlesikAktifCihazlar());

    /// <summary>Kat İçi / Merkeze kablo tipi seçimi için aktif sisteme göre kablo kütüphanesi.</summary>
    private IReadOnlyList<string> YerlesikAktifKablolar() => AktifSistem switch
    {
        "Kamera" => ElvCihazKatalogu.KameraKablolari,
        "Anons" => ElvCihazKatalogu.AnonsKablolari,
        "Tv" => TvSistemi.Teknoloji == TvDagitimTeknolojisi.IpTv
            ? ElvCihazKatalogu.DataKablolari : ElvCihazKatalogu.TvKablolari,
        "Yangin" => ElvCihazKatalogu.YanginKablolari,
        "Telefon" => ElvCihazKatalogu.TelefonKablolari,
        "Diafon" => ElvCihazKatalogu.DiafonKablolari,
        _ => ElvCihazKatalogu.DataKablolari
    };
    public IReadOnlyList<string> KabloKatalogu => ElvKutuphaneServisi.Birlestir(
        Proje, AktifKutuphaneAltBaslikKodu, ElvKutuphaneKalemTuru.Kablo, YerlesikAktifKablolar());
    public string SistemBaslik => AktifSistem switch
    {
        "Kamera" => "KAMERA SİSTEMİ",
        "Anons" => "ACİL ANONS SİSTEMİ",
        "Tv" => "TV SİSTEMİ",
        "Yangin" => "YANGIN ALGILAMA SİSTEMİ",
        "Telefon" => "TELEFON SİSTEMİ",
        "Diafon" => "DİAFON SİSTEMİ",
        _ => "DATA SİSTEMİ"
    };
    public string MerkezButonMetni => AktifSistem switch
    {
        "Kamera" => "CCTV Ana Merkez",
        "Anons" => "Acil Anons Merkezi",
        "Tv" => "TV Ana Santral",
        "Yangin" => "Yangın Algılama Santrali",
        "Telefon" => "Telefon Santrali",
        "Diafon" => "Diafon Santrali",
        _ => "Ana Merkez"
    };
    public string RackEkleMetni => AktifSistem switch
    {
        "Kamera" => "+ CCTV Rack Ekle",
        "Anons" => AnalogAnonsAktif ? "+ Tahliye Zonu Ekle" : "+ Anons Hat Grubu Ekle",
        "Tv" => TvIpAktif ? "+ IP-TV Rack Ekle" : "+ TV Dağıtım Grubu Ekle",
        "Yangin" => "+ Loop Grubu Ekle",
        "Telefon" => "+ Telefon Hat Grubu Ekle",
        "Diafon" => "+ Diafon Ünite Grubu Ekle",
        _ => "+ Rack Ekle"
    };
    public string ToplayiciBaslik => AktifSistem == "Yangin" ? "LOOP GRUPLARI"
        : TvDirektAktif ? "DİREKT LNB HATLARI"
        : AnalogAnonsAktif ? "TAHLİYE ZONLARI" : "RACK KABİNLER";
    public string ToplayiciSayacText => AktifSistem == "Yangin" ? $"{YanginKatLoopGruplari.Count} kat"
        : TvDirektAktif ? $"{TvSistemi.DirektLnb.Hatlar.Count} hat"
        : AnalogAnonsAktif ? $"{AnalogAnonsZonlari.Count} zon" : $"{RackKabinler.Count} adet";
    public bool RackListesiGorunur => AktifSistem != "Yangin";
    public bool AnalogAnonsAktif => AktifSistem == "Anons" && AnonsSistemi.Teknoloji == "Analog";
    public bool StandartRackListesiGorunur => RackListesiGorunur && !AnalogAnonsAktif && !TvDirektAktif;
    public bool RackIslemleriGorunur => !TvDirektAktif;
    public bool CctvIzlemeNoktalariGorunur => AktifSistem == "Kamera";
    public bool AnalogAnonsZonListesiGorunur => AnalogAnonsAktif;
    public bool AnalogAnonsZonDuzenlenebilir => AnalogAnonsAktif && SeciliAnalogAnonsZonu is not null && !MerkezSecili;
    public IReadOnlyList<ElvAnalogHoparlorKatalogKalemi> AnalogHoparlorKatalogu => ElvAnalogHoparlorKatalogu.Kalemler;
    public IReadOnlyList<ElvAnalogMerkezKatalogKalemi> AnalogAmplifikatorKatalogu =>
        ElvAnalogAnonsAnaMerkezKatalogu.Kalemler.Where(k => k.Kategori == ElvAnalogMerkezCihazKategorisi.Amplifikator).ToList();
    public bool SeciliAnalogCiftHat
    {
        get => SeciliAnalogAnonsZonu?.CiftHatli ?? false;
        set
        {
            if (SeciliAnalogAnonsZonu is null || value == SeciliAnalogAnonsZonu.CiftHatli) return;
            if (value) ElvAnalogAnonsTopolojiServisi.CiftHatEtkinlestir(SeciliAnalogAnonsZonu);
            else ElvAnalogAnonsTopolojiServisi.TekHataDonustur(SeciliAnalogAnonsZonu, hatBVerisiniHatAyaTasi: true);
            Yenile();
        }
    }
    public string AnalogGucOzeti
    {
        get
        {
            var r = ElvAnalogAnonsGucServisi.Hesapla(AnonsSistemi);
            return $"Hoparlör yükü: {r.ToplamHoparlorYukuW:0.#} W | Rezervli: {r.ToplamRezervliGucW:0.#} W | "
                   + $"Yedek: {r.MevcutYedekAmplifikatorGucuW:0.#}/{r.GerekliYedekAmplifikatorGucuW:0.#} W | Sorun: {r.Sorunlar.Count}";
        }
    }

    // ── EN 54-4 güç kaynağı / akü ve mesaj senaryoları ─────────────────────
    public ElvAnonsGucKaynagi AnalogGucKaynagi => AnonsSistemi.AnalogGucKaynagi;

    public string AnalogAkuOzeti
    {
        get
        {
            var r = ElvAnalogAnonsAkuServisi.Hesapla(AnonsSistemi);
            return $"Alarm akımı: {r.AlarmAkimiA:0.##} A | Gerekli: {r.GerekliAh:0.#} Ah | "
                   + $"Mevcut: {r.MevcutAh:0.#} Ah | {(r.Yeterli ? "Yeterli" : "YETERSİZ")}";
        }
    }

    [ObservableProperty] private ElvAnonsMesajSenaryosu? seciliAnalogMesajSenaryosu;

    [RelayCommand]
    public void AnalogMesajSenaryosuEkle()
    {
        var senaryo = new ElvAnonsMesajSenaryosu
        {
            Ad = $"Senaryo {AnonsSistemi.AnalogMesajSenaryolari.Count + 1}",
            Oncelik = AnonsSistemi.AnalogMesajSenaryolari.Count + 1
        };
        AnonsSistemi.AnalogMesajSenaryolari.Add(senaryo);
        SeciliAnalogMesajSenaryosu = senaryo;
        Yenile();
        DurumMesaji = "Mesaj senaryosu eklendi.";
    }

    [RelayCommand]
    public void AnalogMesajSenaryosuSil()
    {
        if (SeciliAnalogMesajSenaryosu is null) return;
        AnonsSistemi.AnalogMesajSenaryolari.Remove(SeciliAnalogMesajSenaryosu);
        SeciliAnalogMesajSenaryosu = AnonsSistemi.AnalogMesajSenaryolari.FirstOrDefault();
        Yenile();
        DurumMesaji = "Mesaj senaryosu silindi.";
    }

    /// <summary>Genel tahliye senaryosunun tipik kurulumu: seçili senaryo tüm zonları kapsar.</summary>
    [RelayCommand]
    public void AnalogSenaryoyaTumZonlariAta()
    {
        if (SeciliAnalogMesajSenaryosu is null) return;
        SeciliAnalogMesajSenaryosu.ZonIds = AnonsSistemi.AnalogZonlar.Select(z => z.Id).ToList();
        Yenile();
        DurumMesaji = $"Senaryoya {SeciliAnalogMesajSenaryosu.ZonIds.Count} zon atandı.";
    }

    /// <summary>Data/Kamera sistemlerinde cihaz adlarındaki "N Port"/"N Kanal" bilgisiyle
    /// switch/patch panel/NVR kapasitesini uç ve kamera sayılarıyla karşılaştırır.</summary>
    public bool KapasiteDenetimGorunur => AktifSistem is "Data" or "Kamera" || TvIpAktif || TvDirektAktif;

    [RelayCommand]
    public void KapasiteDenetle()
    {
        if (AktifSistem == "Tv")
        {
            var sorunlar = ElvTvTopolojiServisi.Dogrula(TvSistemi);
            KapasiteDenetimSatirlari = new ObservableCollection<string>(sorunlar.Select(s =>
                $"[{(s.Hata ? "Hata" : "Uyarı")}] {s.Kaynak}: {s.Mesaj}"));
            if (sorunlar.Count == 0)
                KapasiteDenetimSatirlari.Add(TvIpAktif
                    ? $"[Bilgi] IP-TV: Gerekli merkez kapasitesi {TvSistemi.IpTv.GerekliMerkezBantGenisligiMbps:0.#} Mbps"
                    : "[Bilgi] Direkt LNB kapasitesi uygun.");
            DurumMesaji = sorunlar.Count == 0 ? "TV sistemi doğrulaması temiz." : $"TV doğrulaması: {sorunlar.Count} sorun.";
            return;
        }
        var rapor = AktifSistem == "Kamera"
            ? ElvKapasiteDenetimServisi.KameraDenetle(KameraSistemi)
            : ElvKapasiteDenetimServisi.DataDenetle(DataSistemi);
        KapasiteDenetimSatirlari = new ObservableCollection<string>(rapor.Sorunlar.Select(s =>
            $"[{(s.Seviye == ElvKapasiteSorunSeviyesi.Uyari ? "Uyarı" : "Bilgi")}] {s.Kaynak}: {s.Mesaj}"));
        DurumMesaji = rapor.Temiz
            ? "Kapasite denetimi temiz."
            : $"Kapasite denetimi: {rapor.UyariSayisi} uyarı.";
    }

    [RelayCommand]
    public void AnalogSistemiDogrula()
    {
        var rapor = ElvAnalogAnonsDogrulamaServisi.Dogrula(AnonsSistemi);
        AnalogDogrulamaSatirlari = new ObservableCollection<string>(rapor.Maddeler.Select(m =>
            $"[{m.Seviye}] {m.Kaynak}: {m.Mesaj}"));
        DurumMesaji = rapor.Gecerli
            ? $"Analog doğrulama tamamlandı: {rapor.UyariSayisi} uyarı."
            : $"Analog doğrulama: {rapor.HataSayisi} hata, {rapor.UyariSayisi} uyarı.";
    }
    public bool YanginListesiGorunur => AktifSistem == "Yangin";
    public bool YanginLoopEditorGorunur => AktifSistem == "Yangin" && !MerkezSecili && SeciliYanginLoop is not null;
    public string YanginLoopEditorBaslik => SeciliYanginLoop is null
        ? "LOOP CİHAZ ZİNCİRİ"
        : $"{SeciliYanginLoop.Ad} — {SeciliYanginLoop.CihazSayisi} cihaz / {SeciliYanginLoop.Metraj:0.##} m";
    public string BirincilSayacBaslik => AktifSistem switch
    {
        "Kamera" => "Kamera Sayısı",
        "Anons" => "Anons Hat Sayısı",
        "Tv" => "TV Priz Sayısı",
        "Yangin" => "Loop Sayısı",
        "Telefon" => "Telefon Priz Sayısı",
        "Diafon" => "Diafon Ünite Sayısı",
        _ => "Data Hat Sayısı"
    };
    public string IkincilSayacBaslik => AktifSistem == "Data" ? "Wireless Sayısı" : "";
    public bool IkincilSayacGorunur => AktifSistem == "Data";
    public bool StandartRackSayaclariGorunur => AktifSistem != "Kamera" && !DijitalAnonsAktif;
    public bool KameraTipSayaclariGorunur => RackDuzenlenebilir && AktifSistem == "Kamera";
    public string KatIciKabloBaslik => AktifSistem switch
    {
        "Anons" => "Hat Kablo",
        "Tv" => "Koaksiyel Hat Kablo",
        _ => "Kat İçi Kablo"
    };
    public string ToplamBirincilText => AktifSistem == "Kamera"
        ? $"Toplam kamera: {KameraSistemi.ToplamKamera}  |  İzleme noktası: {KameraSistemi.IzlemeNoktalari.Count}"
        : AktifSistem == "Anons"
            ? (DijitalAnonsAktif
                ? $"Toplam dijital uç: {AnonsSistemi.ToplamHat}  |  IP hoparlör: {AnonsSistemi.ToplamHoparlor}"
                : $"Toplam anons hattı: {AnonsSistemi.ToplamHat}  |  Hoparlör: {AnonsSistemi.ToplamHoparlor}")
            : AktifSistem == "Tv"
                ? TvDirektAktif
                    ? $"TV ucu: {TvSistemi.OrtakUclar.Count} | LNB çıkışı: {TvSistemi.DirektLnb.GerekliCikis}/{TvSistemi.DirektLnb.LnbCikisSayisi * Math.Max(1, TvSistemi.DirektLnb.LnbSayisi)}"
                    : TvIpAktif ? TvIpBantGenisligiOzeti
                    : $"Toplam TV prizi: {TvSistemi.ToplamTvPriz}  |  Çatı uydu: {TvSistemi.CatiUyduSayisi}"
                : AktifSistem == "Yangin"
                    ? $"Toplam loop: {YanginSistemi.ToplamLoop}  |  Cihaz: {YanginSistemi.ToplamCihaz}"
                    : AktifSistem == "Telefon"
                        ? $"Toplam telefon prizi: {TelefonSistemi.ToplamTelefonPriz}"
                        : AktifSistem == "Diafon"
                            ? $"Toplam diafon ünitesi: {DiafonSistemi.ToplamDiafonUnite}"
            : $"Toplam data hattı: {DataSistemi.ToplamDataHat}";
    public string ToplamIkincilText => AktifSistem == "Data"
        ? $"WiFi: {DataSistemi.ToplamWireless}  |  IP Tel: {DataSistemi.ToplamIpTelefon}  |  Yazıcı: {DataSistemi.ToplamYazici}  |  Erişim/PDKS: {DataSistemi.ToplamErisimKontrol}  |  Diafon/IP Ses: {DataSistemi.ToplamDiafon}"
        : "";
    public string ToplamMerkezKabloText => AktifSistem == "Yangin"
        ? ""
        : AktifSistem == "Kamera"
            ? KabloMetrajOzeti("Omurga/izleme kabloları",
                KameraSistemi.RackKabinler.Select(r => (r.MerkezeKabloTipi, r.MerkezeKabloMetraj))
                    .Concat(KameraSistemi.IzlemeNoktalari.Select(n => (n.BaglantiKabloTipi, n.BaglantiKabloMetraj))))
            : KabloMetrajOzeti("Merkez kabloları", AktifRackListesi().Select(r => (r.MerkezeKabloTipi, r.MerkezeKabloMetraj)));
    public string ToplamHatMetrajText => KabloMetrajOzeti(
        AktifSistem == "Yangin" ? "Loop kabloları" : "Kat içi kablolar", AktifKatIciKablolar());
    public bool ToplamMerkezKabloGorunur => AktifSistem != "Yangin";
    public bool DataHatMetrajGorunur => RackDuzenlenebilir && AktifSistem == "Data";
    public bool WirelessHatMetrajGorunur => RackDuzenlenebilir && AktifSistem == "Data";
    public bool KameraHatMetrajGorunur => RackDuzenlenebilir && AktifSistem == "Kamera";
    public bool DijitalAnonsAktif => AktifSistem == "Anons" && AnonsSistemi.Teknoloji == "Dijital (PoE)";
    public bool AnonsHatlariGorunur => RackDuzenlenebilir && AktifSistem == "Anons" && !DijitalAnonsAktif;
    public bool DijitalAnonsUclariGorunur => RackDuzenlenebilir && DijitalAnonsAktif;
    public bool AnonsTeknolojisiGorunur => AktifSistem == "Anons";
    public IReadOnlyList<string> AnonsTeknolojileri { get; } = ["Analog", "Dijital (PoE)"];
    public string SeciliAnonsTeknolojisi
    {
        get => AnonsSistemi.Teknoloji;
        set => AnonsTeknolojisiniDegistirVeTemizle(value);
    }

    public bool AnonsTeknolojiDegisikligindeSilinecekVeriVar =>
        AnonsSistemi.AnaMerkez.Cihazlar.Count > 0
        || AnonsSistemi.RackKabinler.Any(r => r.Cihazlar.Count > 0
            || r.AnonsHatlari.Count > 0 || r.DijitalAnonsUclari.Count > 0
            || r.MerkezeKabloMetraj > 0);

    public void AnonsTeknolojisiniDegistirVeTemizle(string value)
    {
        if (value is not ("Analog" or "Dijital (PoE)") || AnonsSistemi.Teknoloji == value) return;

        AnonsSistemi.AnaMerkez.Cihazlar.Clear();
        AnonsSistemi.AnalogZonlar.Clear();
        AnonsSistemi.AnalogAmplifikatorler.Clear();
        AnonsSistemi.AnalogMesajSenaryolari.Clear();
        AnonsSistemi.AnalogGucKaynagi = new ElvAnonsGucKaynagi();
        foreach (var rack in AnonsSistemi.RackKabinler)
        {
            rack.Cihazlar.Clear();
            rack.AnonsHatlari.Clear();
            rack.DijitalAnonsUclari.Clear();
            rack.AnonsHatSayisi = 0;
            rack.IpTavanHoparlorSayisi = 0;
            rack.IpDuvarHoparlorSayisi = 0;
            rack.IpHornHoparlorSayisi = 0;
            rack.IpProjektorHoparlorSayisi = 0;
            rack.IpAnonsTerminaliSayisi = 0;
            rack.KatIciKabloMetraj = 0;
            rack.MerkezeKabloMetraj = 0;
        }

        AnonsSistemi.Teknoloji = value;
        string katKablo = value == "Dijital (PoE)" ? "Cat6A F/UTP LSZH" : "2x1.5 mm² FE180";
        string merkezKablo = value == "Dijital (PoE)" ? "Fiber 12 Core Singlemode" : "2x1.5 mm² FE180";
        foreach (var rack in AnonsSistemi.RackKabinler)
        {
            rack.KatIciKabloTipi = katKablo;
            rack.MerkezeKabloTipi = merkezKablo;
        }

        if (MerkezSecili) CihazlariYukle(AnonsSistemi.AnaMerkez.Cihazlar);
        else if (SeciliRack is not null) CihazlariYukle(SeciliRack.Cihazlar);
        Yenile();
        OnPropertyChanged(nameof(SeciliAnonsTeknolojisi));
        DurumMesaji = $"Acil anons teknolojisi {value} olarak değiştirildi; cihaz ve kablolar temizlendi.";
    }
    public IReadOnlyList<string> TvTeknolojileri { get; } =
        ["Direkt Çok Çıkışlı LNB", "Merkezi Koaksiyel", "IP-TV (Cat/Fiber)"];
    public IReadOnlyList<string> TvMerkeziSantralTipleri { get; } = ["Multiswitch IF", "İşlemeli Headend"];
    public bool TvTeknolojisiGorunur => AktifSistem == "Tv";
    public bool TvDirektAktif => AktifSistem == "Tv" && TvSistemi.Teknoloji == TvDagitimTeknolojisi.DirektLnb;
    public bool TvMerkeziAktif => AktifSistem == "Tv" && TvSistemi.Teknoloji == TvDagitimTeknolojisi.MerkeziKoaksiyel;
    public bool TvIpAktif => AktifSistem == "Tv" && TvSistemi.Teknoloji == TvDagitimTeknolojisi.IpTv;
    public string SeciliTvTeknolojisi
    {
        get => TvSistemi.Teknoloji switch
        {
            TvDagitimTeknolojisi.DirektLnb => TvTeknolojileri[0],
            TvDagitimTeknolojisi.IpTv => TvTeknolojileri[2],
            _ => TvTeknolojileri[1]
        };
        set
        {
            var teknoloji = value == TvTeknolojileri[0] ? TvDagitimTeknolojisi.DirektLnb
                : value == TvTeknolojileri[2] ? TvDagitimTeknolojisi.IpTv
                : TvDagitimTeknolojisi.MerkeziKoaksiyel;
            if (TvSistemi.Teknoloji == teknoloji) return;
            ElvTvTopolojiServisi.TeknolojiyiHazirla(TvSistemi, teknoloji);
            SeciliRack = null;
            MerkeziSec();
            Yenile();
            DurumMesaji = $"TV teknolojisi: {SeciliTvTeknolojisi}";
        }
    }
    public int DirektTvUcSayisi
    {
        get => TvSistemi.OrtakUclar.Count;
        set
        {
            int adet = Math.Max(0, value);
            while (TvSistemi.OrtakUclar.Count < adet)
            {
                int no = TvSistemi.OrtakUclar.Count + 1;
                TvSistemi.OrtakUclar.Add(new ElvTvUcu { Ad = $"TV Prizi {no}", KatAdi = "Zemin Kat" });
            }
            while (TvSistemi.OrtakUclar.Count > adet)
                TvSistemi.OrtakUclar.RemoveAt(TvSistemi.OrtakUclar.Count - 1);
            ElvTvTopolojiServisi.DirektHatlariEsitle(TvSistemi);
            Yenile();
        }
    }
    public string TvIpBantGenisligiOzeti =>
        $"Uç: {TvSistemi.IpTv.ToplamUc} | Gerekli merkez: {TvSistemi.IpTv.GerekliMerkezBantGenisligiMbps:0.#} Mbps";

    [RelayCommand]
    public void IpTvAkisiEkle()
    {
        int no = TvSistemi.IpTv.Akislar.Count + 1;
        var akis = new ElvIpTvAkisi
        {
            KanalAdi = $"Kanal {no}",
            MulticastGrupIp = $"239.10.{(no - 1) / 250}.{(no - 1) % 250 + 1}",
            Port = 5000 + no
        };
        TvSistemi.IpTv.Akislar.Add(akis);
        Yenile();
        SeciliIpTvAkisi = akis;
        DurumMesaji = "IP-TV kanal/akışı eklendi.";
    }

    [RelayCommand]
    public void IpTvAkisiSil()
    {
        if (SeciliIpTvAkisi is null) return;
        TvSistemi.IpTv.Akislar.Remove(SeciliIpTvAkisi);
        Yenile();
        SeciliIpTvAkisi = IpTvAkislari.FirstOrDefault();
        DurumMesaji = "IP-TV kanal/akışı silindi.";
    }

    public bool TvHatMetrajGorunur => RackDuzenlenebilir && AktifSistem == "Tv" && !TvDirektAktif;
    public bool TelefonHatMetrajGorunur => RackDuzenlenebilir && AktifSistem == "Telefon";
    public bool DiafonUniteHatMetrajGorunur => RackDuzenlenebilir && AktifSistem == "Diafon";
    public bool TvCatiHatlariGorunur => MerkezSecili && TvMerkeziAktif;
    public bool YanginLooplariGorunur => YanginKatDuzenlenebilir && AktifSistem == "Yangin";
    public double SeciliRackKatIciKabloMetraj => SeciliRack?.KatIciKabloMetraj ?? 0;
    public int TvCatiUyduSayisi
    {
        get => TvSistemi.CatiUyduSayisi;
        set
        {
            TvSistemi.CatiUyduSayisi = Math.Max(1, value);
            TvCatiHatlariniSayiyaEsitle(TvSistemi);
            Yenile();
        }
    }
    public int AnaMerkezKatNo
    {
        get => AnaMerkez.KatNo ?? 0;
        set
        {
            AnaMerkez.KatNo = value;
            Yenile();
        }
    }

    public int SeciliRackBirincilSayac
    {
        get => SeciliRack is null
            ? 0
            : AktifSistem switch
            {
                "Kamera" => SeciliRack.KameraSayisi,
                "Anons" => SeciliRack.AnonsHatSayisi,
                "Tv" => SeciliRack.TvPrizSayisi,
                "Telefon" => SeciliRack.TelefonPrizSayisi,
                "Diafon" => SeciliRack.DiafonUniteSayisi,
                _ => SeciliRack.DataHatSayisi
            };
        set
        {
            if (SeciliRack is null) return;
            if (AktifSistem == "Kamera")
            {
                SeciliRack.KameraSayisi = Math.Max(0, value);
                HatListesiniSayiyaEsitle(SeciliRack.KameraHatlari, SeciliRack.KameraSayisi, "Kamera");
            }
            else if (AktifSistem == "Anons")
            {
                SeciliRack.AnonsHatSayisi = Math.Max(0, value);
                AnonsHatListesiniSayiyaEsitle(SeciliRack.AnonsHatlari, SeciliRack.AnonsHatSayisi, AnonsVarsayilanKablo);
            }
            else if (AktifSistem == "Tv")
            {
                SeciliRack.TvPrizSayisi = Math.Max(0, value);
                HatListesiniSayiyaEsitle(SeciliRack.TvHatlari, SeciliRack.TvPrizSayisi,
                    TvIpAktif ? "IP-TV" : "TV", TvIpAktif ? "Cat6A F/UTP LSZH" : "RG-6/U Koaksiyel");
            }
            else if (AktifSistem == "Telefon")
            {
                SeciliRack.TelefonPrizSayisi = Math.Max(0, value);
                HatListesiniSayiyaEsitle(SeciliRack.TelefonHatlari, SeciliRack.TelefonPrizSayisi, "Telefon", TelefonVarsayilanKablo);
            }
            else if (AktifSistem == "Diafon")
            {
                SeciliRack.DiafonUniteSayisi = Math.Max(0, value);
                HatListesiniSayiyaEsitle(SeciliRack.DiafonUniteHatlari, SeciliRack.DiafonUniteSayisi, "Diafon Ünitesi", DiafonVarsayilanKablo);
            }
            else
            {
                SeciliRack.DataHatSayisi = Math.Max(0, value);
                HatListesiniSayiyaEsitle(SeciliRack.DataHatlari, SeciliRack.DataHatSayisi, "Data");
            }
            Yenile();
        }
    }

    public int SeciliYanginLoopSayisi
    {
        get => SeciliYanginKatLoopGrubu?.LoopSayisi ?? 0;
        set
        {
            if (SeciliYanginKatLoopGrubu is null) return;
            SeciliYanginKatLoopGrubu.LoopSayisi = Math.Max(0, value);
            YanginLoopListesiniSayiyaEsitle(SeciliYanginKatLoopGrubu.Looplar, SeciliYanginKatLoopGrubu.LoopSayisi);
            Yenile();
        }
    }

    public int SeciliRackIkincilSayac
    {
        get => SeciliRack?.WirelessSayisi ?? 0;
        set
        {
            if (SeciliRack is null) return;
            SeciliRack.WirelessSayisi = Math.Max(0, value);
            HatListesiniSayiyaEsitle(SeciliRack.WirelessHatlari, SeciliRack.WirelessSayisi, "Wireless");
            Yenile();
        }
    }

    public int SeciliRackDahiliKameraSayisi
    {
        get => SeciliRack?.DahiliKameraSayisi ?? 0;
        set => KameraTipSayisiniDegistir(r => r.DahiliKameraSayisi = Math.Max(0, value));
    }

    public int SeciliRackHariciKameraSayisi
    {
        get => SeciliRack?.HariciKameraSayisi ?? 0;
        set => KameraTipSayisiniDegistir(r => r.HariciKameraSayisi = Math.Max(0, value));
    }

    public int SeciliRackPtzKameraSayisi
    {
        get => SeciliRack?.PtzKameraSayisi ?? 0;
        set => KameraTipSayisiniDegistir(r => r.PtzKameraSayisi = Math.Max(0, value));
    }

    public int SeciliRackIpTavanHoparlorSayisi
    {
        get => SeciliRack?.IpTavanHoparlorSayisi ?? 0;
        set => DijitalAnonsUcSayisiniDegistir(r => r.IpTavanHoparlorSayisi = Math.Max(0, value));
    }
    public int SeciliRackIpDuvarHoparlorSayisi
    {
        get => SeciliRack?.IpDuvarHoparlorSayisi ?? 0;
        set => DijitalAnonsUcSayisiniDegistir(r => r.IpDuvarHoparlorSayisi = Math.Max(0, value));
    }
    public int SeciliRackIpHornHoparlorSayisi
    {
        get => SeciliRack?.IpHornHoparlorSayisi ?? 0;
        set => DijitalAnonsUcSayisiniDegistir(r => r.IpHornHoparlorSayisi = Math.Max(0, value));
    }
    public int SeciliRackIpProjektorHoparlorSayisi
    {
        get => SeciliRack?.IpProjektorHoparlorSayisi ?? 0;
        set => DijitalAnonsUcSayisiniDegistir(r => r.IpProjektorHoparlorSayisi = Math.Max(0, value));
    }
    public int SeciliRackIpAnonsTerminaliSayisi
    {
        get => SeciliRack?.IpAnonsTerminaliSayisi ?? 0;
        set => DijitalAnonsUcSayisiniDegistir(r => r.IpAnonsTerminaliSayisi = Math.Max(0, value));
    }

    private void DijitalAnonsUcSayisiniDegistir(Action<ElvRackKabin> degistir)
    {
        if (SeciliRack is null) return;
        degistir(SeciliRack);
        DijitalAnonsUcListesiniEsitle(SeciliRack);
        Yenile();
    }

    private void KameraTipSayisiniDegistir(Action<ElvRackKabin> degistir)
    {
        if (SeciliRack is null) return;
        degistir(SeciliRack);
        KameraHatListesiniSayiyaEsitle(SeciliRack);
        Yenile();
    }

    // ── Data sistemi ek uç birim hat sayıları (manuel giriş) ──
    private const string DataKabloVarsayilan = "Cat6 U/UTP";

    public int SeciliRackIpTelefonSayisi
    {
        get => SeciliRack?.IpTelefonSayisi ?? 0;
        set
        {
            if (SeciliRack is null) return;
            SeciliRack.IpTelefonSayisi = Math.Max(0, value);
            HatListesiniSayiyaEsitle(SeciliRack.IpTelefonHatlari, SeciliRack.IpTelefonSayisi, "IP Telefon", DataKabloVarsayilan);
            Yenile();
        }
    }

    public int SeciliRackYaziciSayisi
    {
        get => SeciliRack?.YaziciSayisi ?? 0;
        set
        {
            if (SeciliRack is null) return;
            SeciliRack.YaziciSayisi = Math.Max(0, value);
            HatListesiniSayiyaEsitle(SeciliRack.YaziciHatlari, SeciliRack.YaziciSayisi, "Yazıcı", DataKabloVarsayilan);
            Yenile();
        }
    }

    public int SeciliRackErisimKontrolSayisi
    {
        get => SeciliRack?.ErisimKontrolSayisi ?? 0;
        set
        {
            if (SeciliRack is null) return;
            SeciliRack.ErisimKontrolSayisi = Math.Max(0, value);
            HatListesiniSayiyaEsitle(SeciliRack.ErisimKontrolHatlari, SeciliRack.ErisimKontrolSayisi, "Erişim Kontrol", DataKabloVarsayilan);
            Yenile();
        }
    }

    public int SeciliRackDiafonSayisi
    {
        get => SeciliRack?.DiafonSayisi ?? 0;
        set
        {
            if (SeciliRack is null) return;
            SeciliRack.DiafonSayisi = Math.Max(0, value);
            HatListesiniSayiyaEsitle(SeciliRack.DiafonHatlari, SeciliRack.DiafonSayisi, "Diafon/IP Ses", DataKabloVarsayilan);
            Yenile();
        }
    }

    public bool DataEkHatlarGorunur => RackDuzenlenebilir && AktifSistem == "Data";
    public bool IpTelefonHatMetrajGorunur => DataEkHatlarGorunur && (SeciliRack?.IpTelefonSayisi ?? 0) > 0;
    public bool YaziciHatMetrajGorunur => DataEkHatlarGorunur && (SeciliRack?.YaziciSayisi ?? 0) > 0;
    public bool ErisimKontrolHatMetrajGorunur => DataEkHatlarGorunur && (SeciliRack?.ErisimKontrolSayisi ?? 0) > 0;
    public bool DiafonHatMetrajGorunur => DataEkHatlarGorunur && (SeciliRack?.DiafonSayisi ?? 0) > 0;

    // ── Rack üst bağlantısı (Ana Merkez veya başka bir rack) ──
    public bool UstBaglantiGorunur => RackDuzenlenebilir;

    /// <summary>Üst bağlantı seçenekleri: Ana Merkez + tüm rack'ler. Rack'ten bağımsızdır
    /// (kararlı ItemsSource) — kendine bağlama ve döngü kontrolü <see cref="UstBaglantiUygula"/>'da yapılır.</summary>
    public IReadOnlyList<RackBaglantiSecenek> UstBaglantiSecenekleri => BaglantiSecenekleriUret(null);
    public IReadOnlyList<RackBaglantiSecenek> CctvIzlemeBaglantiSecenekleri
    {
        get
        {
            var liste = new List<RackBaglantiSecenek>
            {
                new(AnaMerkezSecenekId, $"Ana Merkez ({KameraSistemi.AnaMerkez.Ad})")
            };
            liste.AddRange(KameraSistemi.RackKabinler.Select(r => new RackBaglantiSecenek(r.Id, $"{r.Ad} ({r.KatAdi})")));
            return liste;
        }
    }

    public string SeciliCctvIzlemeBaglantiId
    {
        get => SeciliCctvIzlemeNoktasi?.BagliRackId ?? AnaMerkezSecenekId;
        set => CctvIzlemeBaglantisiUygula(value);
    }

    public int SeciliCctvIzlemeKatNo
    {
        get => SeciliCctvIzlemeNoktasi?.KatNo ?? 0;
        set
        {
            if (SeciliCctvIzlemeNoktasi is null) return;
            SeciliCctvIzlemeNoktasi.KatNo = value;
            SeciliCctvIzlemeNoktasi.KatAdi = Proje.Katlar.FirstOrDefault(k => k.KatNo == value)?.Ad ?? $"{value}. Kat";
            IsDirty = true;
            SemaRevizyon++;
        }
    }

    public void CctvIzlemeBaglantisiUygula(string? secenekId)
    {
        if (SeciliCctvIzlemeNoktasi is null) return;
        var yeni = string.IsNullOrEmpty(secenekId) ? null : secenekId;
        if (yeni is not null && KameraSistemi.RackKabinler.All(r => r.Id != yeni)) yeni = null;
        if (SeciliCctvIzlemeNoktasi.BagliRackId == yeni) return;
        SeciliCctvIzlemeNoktasi.BagliRackId = yeni;
        IsDirty = true;
        SemaRevizyon++;
        OnPropertyChanged(nameof(SeciliCctvIzlemeBaglantiId));
        var ad = CctvIzlemeBaglantiSecenekleri.FirstOrDefault(x => x.Id == (yeni ?? AnaMerkezSecenekId))?.Ad ?? "Ana Merkez";
        DurumMesaji = $"İzleme noktası bağlantısı: {ad}";
    }

    // Ana Merkez için "" (boş) sentinel; rack için gerçek Id. UstRackId modelde null=merkez.
    public const string AnaMerkezSecenekId = "";

    public string SeciliUstRackId
    {
        get => SeciliRack?.UstRackId ?? AnaMerkezSecenekId;
        set => UstBaglantiUygula(value);
    }

    /// <summary>Seçili rack'in üst bağlantısını uygular (ComboBox SelectionChanged'den de çağrılır).
    /// <paramref name="secenekId"/> boş/"" ise Ana Merkez'e bağlanır.</summary>
    public void UstBaglantiUygula(string? secenekId)
    {
        if (SeciliRack is null) return;
        var yeni = string.IsNullOrEmpty(secenekId) ? null : secenekId;
        if (SeciliRack.UstRackId == yeni) return;

        // Kendine bağlama veya döngü (alt rack'e bağlama) engeli.
        if (yeni != null && (yeni == SeciliRack.Id || AltRackIdleri(SeciliRack).Contains(yeni)))
        {
            DurumMesaji = "Geçersiz bağlantı: rack kendisine veya alt rack'ine bağlanamaz.";
            OnPropertyChanged(nameof(SeciliUstRackId)); // ComboBox'ı gerçek değere geri al
            return;
        }

        SeciliRack.UstRackId = yeni;
        // Bağlantı seçenekleri burada değişmedi. ItemsSource'u yeniden kurmak ComboBox'ın
        // seçimini geçici olarak Ana Merkez'e düşürüp SelectionChanged üzerinden modeli
        // tekrar null'a çekebiliyor. Yalnız şema ve özet alanlarını yenile.
        Yenile(baglantiSecenekleriniYenile: false);
        var ad = UstBaglantiSecenekleri.FirstOrDefault(o => o.Id == (yeni ?? AnaMerkezSecenekId))?.Ad ?? "Ana Merkez";
        DurumMesaji = $"Bağlantı: {ad}";
    }

    /// <summary>Ana Merkez (Id="") + tüm rack'ler. <paramref name="haricRack"/> verilirse (ör. rack
    /// ekleme penceresi) yalnız o rack hariç tutulur; kendine/döngü kontrolü uygulama anındadır.</summary>
    public List<RackBaglantiSecenek> BaglantiSecenekleriUret(ElvRackKabin? haricRack)
    {
        var liste = new List<RackBaglantiSecenek> { new(AnaMerkezSecenekId, $"Ana Merkez ({AnaMerkez.Ad})") };
        foreach (var r in AktifRackListesi())
        {
            if (haricRack != null && r.Id == haricRack.Id) continue;
            liste.Add(new RackBaglantiSecenek(r.Id, $"{r.Ad} ({r.KatAdi})"));
        }
        return liste;
    }

    /// <summary>Verilen rack'in tüm alt (children→...) rack Id'leri — döngü engelleme için.</summary>
    private HashSet<string> AltRackIdleri(ElvRackKabin rack)
    {
        var sonuc = new HashSet<string>();
        var kuyruk = new Queue<string>();
        kuyruk.Enqueue(rack.Id);
        while (kuyruk.Count > 0)
        {
            var id = kuyruk.Dequeue();
            foreach (var c in AktifRackListesi().Where(r => r.UstRackId == id))
                if (sonuc.Add(c.Id)) kuyruk.Enqueue(c.Id);
        }
        return sonuc;
    }

    // Sağ paneldeki düzenleyici başlığı/durumu
    public string DuzenleyiciBaslik => MerkezSecili
        ? $"Ana Merkez — {AnaMerkez.Ad}"
        : SeciliCctvIzlemeNoktasi is { } n ? $"İzleme Noktası: {n.Ad}"
        : AktifSistem == "Yangin" && SeciliYanginKatLoopGrubu is { } y ? $"Loop Grubu: {y.Ad}"
        : SeciliRack is { } r ? $"Rack: {r.Ad}" : "Seçim yok";
    public bool RackDuzenlenebilir => !MerkezSecili && SeciliRack != null;
    public bool CctvIzlemeNoktasiDuzenlenebilir => AktifSistem == "Kamera" && SeciliCctvIzlemeNoktasi is not null;
    public bool YanginKatDuzenlenebilir => AktifSistem == "Yangin" && !MerkezSecili && SeciliYanginKatLoopGrubu != null;
    public bool CihazPanelGorunur => MerkezSecili || (AktifSistem != "Yangin" && !AnalogAnonsAktif);

    public ZayifAkimTekHatViewModel()
    {
        YeniProje();
    }

    // ─── Proje yaşam döngüsü ────────────────────────────────────────────────

    [RelayCommand]
    public void YeniProje()
    {
        Proje = new ZayifAkimTekHatProje { ProjeAdi = "Yeni Proje" };
        Proje.KatlariBinadanGuncelle();
        AktifSistem = "Data";
        SeciliRack = null;
        MerkeziSec();
        Yenile(kirlet: false);
        IsDirty = false;
        DurumMesaji = "Yeni proje oluşturuldu.";
    }

    [RelayCommand]
    public async Task M12denIceAktar()
    {
        var storage = StorageProvider();
        if (storage is null) return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "M12 Proje Dosyası İçe Aktar",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("EYE4PRO M12 Projesi") { Patterns = ["*.e4p12"] }]
        });

        var path = files.FirstOrDefault()?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path)) return;

        Yukleniyor = true;
        DurumMesaji = "Yükleniyor...";
        try
        {
            // Yükleme arka planda, Proje ataması (PropertyChanged) UI thread'inde yapılmalı.
            var yeniProje = await Task.Run(() => ZayifAkimTekHatImportServisi.M12DenYukle(path));
            Proje = yeniProje;
            Yenile(kirlet: false);
            var ilkRack = AktifRackListesi().FirstOrDefault();
            if (ilkRack != null) RackSec(ilkRack); else MerkeziSec();
            IsDirty = true; // içe aktarılan içerik henüz .e4p13 olarak kaydedilmedi
            DurumMesaji = $"✓ İçe aktarıldı: {DataSistemi.RackKabinler.Count} data rack, {KameraSistemi.RackKabinler.Count} CCTV rack, {AnonsSistemi.RackKabinler.Count} anons grup, {TvSistemi.RackKabinler.Count} TV grup, {YanginSistemi.KatLoopGruplari.Count} yangın katı.";
        }
        catch (Exception ex)
        {
            DurumMesaji = $"✗ Hata: {ex.Message}";
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    [RelayCommand]
    public async Task ProjeAc()
    {
        var storage = StorageProvider();
        if (storage is null) return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Zayıf Akım Projesi Aç",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("EYE4PRO Zayıf Akım") { Patterns = ["*.e4p13"] }]
        });

        var path = files.FirstOrDefault()?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            var json = await File.ReadAllTextAsync(path);
            var loaded = JsonSerializer.Deserialize<ZayifAkimTekHatProje>(json, JsonOpts)
                         ?? throw new InvalidOperationException("Proje okunamadı.");

            // Şema sürümü denetimi + eski dosyaların güncel şemaya taşınması.
            var migrasyon = ZayifAkimTekHatMigrasyonServisi.Uygula(loaded);
            if (!migrasyon.Basarili)
            {
                DurumMesaji = $"✗ {migrasyon.Mesaj}";
                return;
            }

            loaded.KaynakDosyaYolu = path;
            Proje = loaded;
            Yenile(kirlet: false);
            var ilkRack = AktifRackListesi().FirstOrDefault();
            if (ilkRack != null) RackSec(ilkRack); else MerkeziSec();
            // Migrasyon yapıldıysa içerik dosyadakinden farklıdır; kaydedilmesi beklenir.
            IsDirty = migrasyon.EskiSurum != migrasyon.YeniSurum;
            DurumMesaji = migrasyon.EskiSurum != migrasyon.YeniSurum
                ? $"✓ Açıldı: {Path.GetFileName(path)} — {migrasyon.Mesaj}"
                : $"✓ Açıldı: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            DurumMesaji = $"✗ Açma hatası: {ex.Message}";
        }
    }

    // Kaydet: bilinen bir dosya yolu varsa doğrudan üzerine yazar; yoksa Farklı Kaydet'e düşer.
    [RelayCommand]
    public async Task ProjeKaydet()
    {
        var yol = Proje.KaynakDosyaYolu;
        if (string.IsNullOrWhiteSpace(yol))
        {
            await ProjeFarkliKaydet();
            return;
        }
        await DosyayaKaydet(yol);
    }

    // Farklı Kaydet: her zaman dosya seçtirir.
    [RelayCommand]
    public async Task ProjeFarkliKaydet()
    {
        var storage = StorageProvider();
        if (storage is null) return;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Zayıf Akım Projesi Farklı Kaydet",
            SuggestedFileName = string.IsNullOrWhiteSpace(Proje.ProjeAdi) ? "zayif_akim.e4p13" : $"{Proje.ProjeAdi}.e4p13",
            DefaultExtension = "e4p13",
            FileTypeChoices = [new FilePickerFileType("EYE4PRO Zayıf Akım") { Patterns = ["*.e4p13"] }]
        });

        var path = file?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!path.EndsWith(".e4p13", StringComparison.OrdinalIgnoreCase))
            path += ".e4p13";

        await DosyayaKaydet(path);
    }

    private async Task DosyayaKaydet(string path)
    {
        try
        {
            Proje.SemaSurum = ZayifAkimTekHatProje.GuncelSemaSurum;
            var json = JsonSerializer.Serialize(Proje, JsonOpts);
            await File.WriteAllTextAsync(path, json);
            Proje.KaynakDosyaYolu = path;
            IsDirty = false;
            DurumMesaji = $"✓ Kaydedildi: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            DurumMesaji = $"✗ Kaydetme hatası: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task DxfCiktiAl()
    {
        try
        {
            var sistemAdi = AktifSistem switch
            {
                "Kamera" => "kamera",
                "Anons" => "acil_anons",
                "Tv" => "tv",
                "Yangin" => "yangin_algilama",
                "Telefon" => "telefon",
                "Diafon" => "diafon",
                _ => "data"
            };
            var dosyaAdi = $"{(string.IsNullOrWhiteSpace(Proje.ProjeAdi) ? "zayif_akim" : Proje.ProjeAdi)}_{sistemAdi}.dxf";

            // Kullanıcı hedefi seçer; sessizce Belgeler'e yazılıp üzerine yazılmaz.
            var storage = StorageProvider();
            if (storage is null) return;
            var dosya = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "DXF Çıktısı Kaydet",
                SuggestedFileName = dosyaAdi,
                DefaultExtension = "dxf",
                FileTypeChoices = [new FilePickerFileType("DXF Çizim") { Patterns = ["*.dxf"] }]
            });
            var hedef = dosya?.Path.LocalPath;
            if (string.IsNullOrWhiteSpace(hedef)) return;
            if (!hedef.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase))
                hedef += ".dxf";

            if (AktifSistem == "Kamera")
                ZayifAkimTekHatDxfServisi.KameraDxfCiktiAl(Proje.KameraSistemi, hedef);
            else if (AktifSistem == "Anons")
                ZayifAkimTekHatDxfServisi.AnonsDxfCiktiAl(Proje.AnonsSistemi, hedef);
            else if (AktifSistem == "Tv")
                ZayifAkimTekHatDxfServisi.TvDxfCiktiAl(Proje.TvSistemi, hedef);
            else if (AktifSistem == "Yangin")
                ZayifAkimTekHatDxfServisi.YanginDxfCiktiAl(Proje.YanginSistemi, hedef);
            else if (AktifSistem == "Telefon")
                ZayifAkimTekHatDxfServisi.TelefonDxfCiktiAl(Proje.TelefonSistemi, hedef);
            else if (AktifSistem == "Diafon")
                ZayifAkimTekHatDxfServisi.DiafonDxfCiktiAl(Proje.DiafonSistemi, hedef);
            else
                ZayifAkimTekHatDxfServisi.DataDxfCiktiAl(Proje.DataSistemi, hedef);
            DurumMesaji = $"✓ DXF: {hedef}";
        }
        catch (Exception ex)
        {
            DurumMesaji = $"✗ DXF hatası: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SvgCiktiAl()
    {
        try
        {
            var sistemAdi = AktifSistem switch
            {
                "Kamera" => "kamera",
                "Anons" => "acil_anons",
                "Tv" => "tv",
                "Yangin" => "yangin_algilama",
                "Telefon" => "telefon",
                "Diafon" => "diafon",
                _ => "data"
            };
            var dosyaAdi = $"{(string.IsNullOrWhiteSpace(Proje.ProjeAdi) ? "zayif_akim" : Proje.ProjeAdi)}_{sistemAdi}.svg";

            var storage = StorageProvider();
            if (storage is null) return;
            var dosya = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "SVG Çıktısı Kaydet",
                SuggestedFileName = dosyaAdi,
                DefaultExtension = "svg",
                FileTypeChoices = [new FilePickerFileType("SVG Çizim") { Patterns = ["*.svg"] }]
            });
            var hedef = dosya?.Path.LocalPath;
            if (string.IsNullOrWhiteSpace(hedef)) return;
            if (!hedef.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                hedef += ".svg";

            if (AktifSistem == "Kamera")
                ZayifAkimTekHatSvgServisi.KameraSvgCiktiAl(Proje.KameraSistemi, hedef);
            else if (AktifSistem == "Anons")
                ZayifAkimTekHatSvgServisi.AnonsSvgCiktiAl(Proje.AnonsSistemi, hedef);
            else if (AktifSistem == "Tv")
                ZayifAkimTekHatSvgServisi.TvSvgCiktiAl(Proje.TvSistemi, hedef);
            else if (AktifSistem == "Yangin")
                ZayifAkimTekHatSvgServisi.YanginSvgCiktiAl(Proje.YanginSistemi, hedef);
            else if (AktifSistem == "Telefon")
                ZayifAkimTekHatSvgServisi.TelefonSvgCiktiAl(Proje.TelefonSistemi, hedef);
            else if (AktifSistem == "Diafon")
                ZayifAkimTekHatSvgServisi.DiafonSvgCiktiAl(Proje.DiafonSistemi, hedef);
            else
                ZayifAkimTekHatSvgServisi.DataSvgCiktiAl(Proje.DataSistemi, hedef);
            DurumMesaji = $"✓ SVG: {hedef}";
        }
        catch (Exception ex)
        {
            DurumMesaji = $"✗ SVG hatası: {ex.Message}";
        }
    }

    // ─── Rack / Merkez seçimi ───────────────────────────────────────────────

    [RelayCommand]
    public void RackEkle()
    {
        int varsayilan = SeciliRack?.KatNo ?? SeciliYanginKatLoopGrubu?.KatNo
                         ?? Proje.Katlar.FirstOrDefault()?.KatNo ?? 0;
        RackEkleKata(varsayilan);
    }

    /// <summary>Belirtilen kata rack/loop grubu ekler (kat seçim penceresinden çağrılır).
    /// <paramref name="ustRackId"/> null ise Ana Merkez'e, değilse verilen rack'e bağlanır.</summary>
    public void RackEkleKata(int katNo, string? ustRackId = null)
    {
        if (AnalogAnonsAktif)
        {
            AnalogAnonsZonuEkle(katNo);
            return;
        }
        if (AktifSistem == "Yangin")
        {
            YanginLoopGrubuEkle(katNo);
            return;
        }

        var katAdi = Proje.Katlar.FirstOrDefault(k => k.KatNo == katNo)?.Ad ?? $"{katNo}. Kat";
        // Geçersiz/kendine referans veren üst id'yi Ana Merkez'e indir.
        if (ustRackId != null && AktifRackListesi().All(r => r.Id != ustRackId))
            ustRackId = null;
        var rack = new ElvRackKabin
        {
            Ad = AktifSistem switch
            {
                "Kamera" => $"{katAdi} CCTV Rack",
                "Anons" => $"{katAdi} Anons Hatları",
                "Tv" => TvIpAktif ? $"{katAdi} IP-TV Rack" : $"{katAdi} TV Hatları",
                "Telefon" => $"{katAdi} Telefon Hatları",
                "Diafon" => $"{katAdi} Diafon Üniteleri",
                _ => $"{katAdi} Rack"
            },
            KatNo = katNo,
            KatAdi = katAdi,
            UstRackId = ustRackId,
            KatIciKabloTipi = AktifSistem switch
            {
                "Anons" => DijitalAnonsAktif ? "Cat6A F/UTP LSZH" : AnonsVarsayilanKablo,
                "Tv" => TvIpAktif ? "Cat6A F/UTP LSZH" : "RG-6/U Koaksiyel",
                "Telefon" => TelefonVarsayilanKablo,
                "Diafon" => DiafonVarsayilanKablo,
                _ => "Cat6 U/UTP"
            },
            MerkezeKabloTipi = AktifSistem switch
            {
                "Anons" => DijitalAnonsAktif ? "Fiber 12 Core Singlemode" : AnonsVarsayilanKablo,
                "Tv" => TvIpAktif ? "Fiber 12 Core Singlemode" : "RG-6/U Koaksiyel",
                "Telefon" => "J-Y(St)Y 10x2x0.6 Telefon Kablosu",
                "Diafon" => "J-Y(St)Y 5x2x0.8 Diafon Kablosu",
                _ => "Cat6 U/UTP"
            }
        };
        HatListesiniSayiyaEsitle(rack.DataHatlari, rack.DataHatSayisi, "Data");
        HatListesiniSayiyaEsitle(rack.WirelessHatlari, rack.WirelessSayisi, "Wireless");
        KameraHatListesiniSayiyaEsitle(rack);
        AnonsHatListesiniSayiyaEsitle(rack.AnonsHatlari, rack.AnonsHatSayisi, AnonsVarsayilanKablo);
        DijitalAnonsUcListesiniEsitle(rack);
        HatListesiniSayiyaEsitle(rack.TvHatlari, rack.TvPrizSayisi,
            TvIpAktif ? "IP-TV" : "TV", TvIpAktif ? "Cat6A F/UTP LSZH" : "RG-6/U Koaksiyel");
        HatListesiniSayiyaEsitle(rack.TelefonHatlari, rack.TelefonPrizSayisi, "Telefon", TelefonVarsayilanKablo);
        HatListesiniSayiyaEsitle(rack.DiafonUniteHatlari, rack.DiafonUniteSayisi, "Diafon Ünitesi", DiafonVarsayilanKablo);
        AktifRackListesi().Add(rack);
        AktifRackListesi().Sort((a, b) => b.KatNo.CompareTo(a.KatNo));
        Yenile();
        SeciliRack = rack;
        RackSec(rack);
        DurumMesaji = "Rack kabin eklendi.";
    }

    [RelayCommand]
    public void CctvIzlemeNoktasiEkle()
    {
        if (AktifSistem != "Kamera") return;
        int katNo = SeciliRack?.KatNo ?? Proje.Katlar.FirstOrDefault()?.KatNo ?? 0;
        string katAdi = Proje.Katlar.FirstOrDefault(k => k.KatNo == katNo)?.Ad ?? $"{katNo}. Kat";
        var nokta = new ElvCctvIzlemeNoktasi
        {
            Ad = $"İzleme Noktası {KameraSistemi.IzlemeNoktalari.Count + 1}",
            KatNo = katNo,
            KatAdi = katAdi,
            BagliRackId = SeciliRack?.Id
        };
        KameraSistemi.IzlemeNoktalari.Add(nokta);
        Yenile();
        CctvIzlemeNoktasiSec(nokta);
        DurumMesaji = "CCTV izleme noktası eklendi.";
    }

    [RelayCommand]
    public void CctvIzlemeNoktasiSil()
    {
        if (SeciliCctvIzlemeNoktasi is null) return;
        KameraSistemi.IzlemeNoktalari.Remove(SeciliCctvIzlemeNoktasi);
        Yenile();
        var sonraki = KameraSistemi.IzlemeNoktalari.FirstOrDefault();
        if (sonraki is not null) CctvIzlemeNoktasiSec(sonraki); else MerkeziSec();
        DurumMesaji = "CCTV izleme noktası silindi.";
    }

    [RelayCommand]
    public void RackSil()
    {
        if (AnalogAnonsAktif)
        {
            if (SeciliAnalogAnonsZonu is null) return;
            AnonsSistemi.AnalogZonlar.Remove(SeciliAnalogAnonsZonu);
            SeciliAnalogAnonsZonu = AnonsSistemi.AnalogZonlar.FirstOrDefault();
            Yenile();
            DurumMesaji = "Tahliye zonu silindi.";
            return;
        }
        if (AktifSistem == "Yangin")
        {
            YanginLoopGrubuSil();
            return;
        }

        if (SeciliRack is null) return;
        var silinenId = SeciliRack.Id;
        var silinenUst = SeciliRack.UstRackId; // çocukları silinen rack'in üstüne taşı
        AktifRackListesi().Remove(SeciliRack);
        foreach (var r in AktifRackListesi().Where(r => r.UstRackId == silinenId))
            r.UstRackId = silinenUst;
        if (AktifSistem == "Kamera")
            foreach (var nokta in KameraSistemi.IzlemeNoktalari.Where(n => n.BagliRackId == silinenId))
                nokta.BagliRackId = silinenUst;
        Yenile();
        SeciliRack = RackKabinler.FirstOrDefault();
        if (SeciliRack != null) RackSec(SeciliRack); else MerkeziSec();
        DurumMesaji = "Rack kabin silindi.";
    }

    [RelayCommand]
    public void MerkeziSec()
    {
        MerkezSecili = true;
        SeciliRack = null;
        SeciliCctvIzlemeNoktasi = null;
        SeciliYanginKatLoopGrubu = null;
        SeciliAnalogAnonsZonu = null;
        CihazlariYukle(AnaMerkez.Cihazlar);
        BildirSecimDegisti();
        OnPropertyChanged(nameof(AnaMerkezKatNo));
    }

    public void RackSec(ElvRackKabin rack)
    {
        MerkezSecili = false;
        SeciliRack = rack;
        SeciliCctvIzlemeNoktasi = null;
        SeciliYanginKatLoopGrubu = null;
        CihazlariYukle(rack.Cihazlar);
        HatListeleriniYukle(rack);
        BildirSecimDegisti();
        BildirRackSayaclari();
    }

    public void CctvIzlemeNoktasiSec(ElvCctvIzlemeNoktasi nokta)
    {
        MerkezSecili = false;
        SeciliRack = null;
        SeciliYanginKatLoopGrubu = null;
        SeciliAnalogAnonsZonu = null;
        SeciliCctvIzlemeNoktasi = nokta;
        CihazlariYukle(nokta.Cihazlar);
        BildirSecimDegisti();
    }

    public void AnalogAnonsZonuSec(ElvAnonsZonu zon)
    {
        MerkezSecili = false;
        SeciliRack = null;
        SeciliYanginKatLoopGrubu = null;
        SeciliAnalogAnonsZonu = zon;
        BildirSecimDegisti();
        OnPropertyChanged(nameof(SeciliAnalogCiftHat));
        OnPropertyChanged(nameof(SeciliHatAAmplifikatorId));
        OnPropertyChanged(nameof(SeciliHatBAmplifikatorId));
    }

    private void AnalogAnonsZonuEkle(int katNo)
    {
        var katAdi = Proje.Katlar.FirstOrDefault(k => k.KatNo == katNo)?.Ad ?? $"{katNo}. Kat";
        var zon = ElvAnalogAnonsTopolojiServisi.ZonOlustur($"{katAdi} Tahliye Zonu", katNo, katAdi, ciftHatli: true);
        AnonsSistemi.AnalogZonlar.Add(zon);
        AnalogAnonsZonlari = new ObservableCollection<ElvAnonsZonu>(AnonsSistemi.AnalogZonlar.OrderByDescending(z => z.KatNo));
        AnalogAnonsZonuSec(zon);
        Yenile();
        DurumMesaji = "A/B tahliye zonu eklendi.";
    }

    [RelayCommand]
    public void AnalogHoparlorEkle()
    {
        if (SeciliAnalogAnonsZonu is null) return;
        var sonuc = ElvAnalogAnonsTopolojiServisi.HoparlorleriDengeliDagit(
            SeciliAnalogAnonsZonu, YeniAnalogHoparlorKodu, YeniAnalogHoparlorAdedi, YeniAnalogHoparlorTapGucu);
        DurumMesaji = sonuc.Mesaj;
        if (sonuc.Basarili) Yenile();
    }

    [RelayCommand]
    public void AnalogHatAGrubuSil()
    {
        if (SeciliAnalogAnonsZonu is null || SeciliAnalogHatAGrubu is null) return;
        ElvAnalogAnonsTopolojiServisi.HoparlorGrubunuSil(SeciliAnalogAnonsZonu.HatA, SeciliAnalogHatAGrubu.Id);
        SeciliAnalogHatAGrubu = null;
        Yenile();
    }

    [RelayCommand]
    public void AnalogHatBGrubuSil()
    {
        if (SeciliAnalogAnonsZonu?.HatB is null || SeciliAnalogHatBGrubu is null) return;
        ElvAnalogAnonsTopolojiServisi.HoparlorGrubunuSil(SeciliAnalogAnonsZonu.HatB, SeciliAnalogHatBGrubu.Id);
        SeciliAnalogHatBGrubu = null;
        Yenile();
    }

    [RelayCommand]
    public void AnalogAmplifikatorEkle()
    {
        try
        {
            AnonsSistemi.AnalogAmplifikatorler.Add(
                ElvAnalogAnonsAnaMerkezKatalogu.AmplifikatorOlustur(YeniAnalogAmplifikatorKodu, YeniAnalogAmplifikatorYedek));
            Yenile();
            DurumMesaji = "Analog amplifikatör eklendi.";
        }
        catch (Exception ex) { DurumMesaji = ex.Message; }
    }

    [RelayCommand]
    public void AnalogAmplifikatorSil()
    {
        if (SeciliAnalogAmplifikator is null) return;
        var id = SeciliAnalogAmplifikator.Id;
        AnonsSistemi.AnalogAmplifikatorler.Remove(SeciliAnalogAmplifikator);
        // Silinen amplifikatöre yapılmış zon/hat atamaları askıda kalmasın.
        foreach (var zon in AnonsSistemi.AnalogZonlar)
        {
            if (zon.AmplifikatorId == id) zon.AmplifikatorId = null;
            if (zon.HatA.AmplifikatorId == id) zon.HatA.AmplifikatorId = null;
            if (zon.HatB is { } hatB && hatB.AmplifikatorId == id) hatB.AmplifikatorId = null;
        }
        SeciliAnalogAmplifikator = null;
        Yenile();
        DurumMesaji = "Amplifikatör silindi; hat atamaları kaldırıldı.";
    }

    // ── Analog hat → amplifikatör ataması ───────────────────────────────────
    public const string AmplifikatorYokSecenekId = "";

    /// <summary>Hat atama seçenekleri: (Atanmamış) + yedek olmayan amplifikatörler.
    /// Yedek amplifikatöre normal işletme hattı atanamaz (güç servisi kuralı).</summary>
    public IReadOnlyList<RackBaglantiSecenek> AnalogAmplifikatorSecenekleri
    {
        get
        {
            var liste = new List<RackBaglantiSecenek> { new(AmplifikatorYokSecenekId, "(Atanmamış)") };
            liste.AddRange(AnonsSistemi.AnalogAmplifikatorler
                .Where(a => !a.Yedek)
                .Select(a => new RackBaglantiSecenek(a.Id, $"{a.Ad} ({a.NominalGucW:0.#} W)")));
            return liste;
        }
    }

    public string SeciliHatAAmplifikatorId
    {
        get => SeciliAnalogAnonsZonu?.HatA.AmplifikatorId ?? AmplifikatorYokSecenekId;
        set
        {
            if (SeciliAnalogAnonsZonu is null) return;
            var yeni = string.IsNullOrEmpty(value) ? null : value;
            if (SeciliAnalogAnonsZonu.HatA.AmplifikatorId == yeni) return;
            SeciliAnalogAnonsZonu.HatA.AmplifikatorId = yeni;
            Yenile();
        }
    }

    public string SeciliHatBAmplifikatorId
    {
        get => SeciliAnalogAnonsZonu?.HatB?.AmplifikatorId ?? AmplifikatorYokSecenekId;
        set
        {
            if (SeciliAnalogAnonsZonu?.HatB is null) return;
            var yeni = string.IsNullOrEmpty(value) ? null : value;
            if (SeciliAnalogAnonsZonu.HatB.AmplifikatorId == yeni) return;
            SeciliAnalogAnonsZonu.HatB.AmplifikatorId = yeni;
            Yenile();
        }
    }

    // ── Analog hat denetimi (EOL / pilot ton / empedans) seçenekleri ────────
    public sealed record DenetimYontemiSecenek(ElvHatDenetimYontemi Deger, string Ad);

    public IReadOnlyList<DenetimYontemiSecenek> HatDenetimYontemleri { get; } =
    [
        new(ElvHatDenetimYontemi.Yok, "Yok"),
        new(ElvHatDenetimYontemi.Eol, "EOL"),
        new(ElvHatDenetimYontemi.PilotTon, "Pilot Ton"),
        new(ElvHatDenetimYontemi.Empedans, "Empedans"),
        new(ElvHatDenetimYontemi.EolVeEmpedans, "EOL + Empedans")
    ];

    partial void OnSeciliRackChanged(ElvRackKabin? value)
    {
        // Listeden bir rack seçilince (Ana Merkez'deyken bile) o rack'e geç ve düzenlenebilir yap.
        if (value != null)
        {
            MerkezSecili = false;
            CihazlariYukle(value.Cihazlar);
            HatListeleriniYukle(value);
            BildirSecimDegisti();
            BildirRackSayaclari();
        }
    }

    partial void OnSeciliCctvIzlemeNoktasiChanged(ElvCctvIzlemeNoktasi? value)
    {
        if (value is not null) CctvIzlemeNoktasiSec(value);
    }

    partial void OnSeciliYanginKatLoopGrubuChanged(ElvYanginKatLoopGrubu? value)
    {
        if (value != null)
        {
            YanginLoopGrubuSec(value);
        }
    }

    partial void OnSeciliYanginLoopChanged(ElvYanginLoop? value)
    {
        SeciliYanginLoopCihazlari = new ObservableCollection<ElvYanginLoopCihazi>(value?.Cihazlar ?? []);
        SeciliYanginLoopCihazi = SeciliYanginLoopCihazlari.FirstOrDefault();
        OnPropertyChanged(nameof(YanginLoopEditorGorunur));
        OnPropertyChanged(nameof(YanginLoopEditorBaslik));
    }

    partial void OnSeciliAnalogAnonsZonuChanged(ElvAnonsZonu? value)
    {
        if (value is not null) AnalogAnonsZonuSec(value);
    }

    public void YanginLoopGrubuSec(ElvYanginKatLoopGrubu grup)
    {
        MerkezSecili = false;
        SeciliRack = null;
        SeciliYanginKatLoopGrubu = grup;
        YanginLoopListesiniSayiyaEsitle(grup.Looplar, grup.LoopSayisi);
        SeciliYanginLoop = grup.Looplar.FirstOrDefault();
        SeciliCihazlar = new ObservableCollection<ElvCihaz>();
        SeciliCihaz = null;
        BildirSecimDegisti();
        OnPropertyChanged(nameof(SeciliYanginLoopSayisi));
    }

    private void YanginLoopGrubuEkle(int katNo)
    {
        var katAdi = Proje.Katlar.FirstOrDefault(k => k.KatNo == katNo)?.Ad ?? $"{katNo}. Kat";
        var grup = new ElvYanginKatLoopGrubu
        {
            Ad = $"{katAdi} Yangın Loopları",
            KatNo = katNo,
            KatAdi = katAdi,
            LoopSayisi = 1,
            Looplar =
            [
                new ElvYanginLoop { LoopNo = 1, Ad = "Loop 1" }
            ]
        };
        YanginSistemi.KatLoopGruplari.Add(grup);
        YanginSistemi.KatLoopGruplari.Sort((a, b) => b.KatNo.CompareTo(a.KatNo));
        Yenile();
        SeciliYanginKatLoopGrubu = grup;
        YanginLoopGrubuSec(grup);
        DurumMesaji = "Yangın loop grubu eklendi.";
    }

    private void YanginLoopGrubuSil()
    {
        if (SeciliYanginKatLoopGrubu is null) return;
        YanginSistemi.KatLoopGruplari.Remove(SeciliYanginKatLoopGrubu);
        Yenile();
        SeciliYanginKatLoopGrubu = YanginKatLoopGruplari.FirstOrDefault();
        if (SeciliYanginKatLoopGrubu != null) YanginLoopGrubuSec(SeciliYanginKatLoopGrubu); else MerkeziSec();
        DurumMesaji = "Yangın loop grubu silindi.";
    }

    [RelayCommand]
    public void YanginLoopCihaziEkle(string? tipMetni)
    {
        if (SeciliYanginLoop is null || !Enum.TryParse<ElvYanginCihazTipi>(tipMetni, out var tip)) return;
        var loop = SeciliYanginLoop;
        var grup = SeciliYanginKatLoopGrubu;
        // Eski .e4p13 dosyalarında yalnız toplam adet/metraj vardı. İlk tür bazlı eklemede
        // bu kayıtları kaybetmeden anonim cihaz düğümlerine dönüştür.
        if (loop.Cihazlar.Count == 0 && loop.CihazSayisi > 0)
        {
            int eskiAdet = loop.CihazSayisi;
            double eskiMetraj = loop.Metraj;
            for (int i = 0; i < eskiAdet; i++)
                loop.Cihazlar.Add(new ElvYanginLoopCihazi
                {
                    Tip = ElvYanginCihazTipi.Bilinmeyen,
                    Ad = "Eski Kayıt — Tür Belirtilmemiş",
                    OncekiCihazaMesafeMetre = i == 0 ? eskiMetraj : 0
                });
        }
        var cihaz = new ElvYanginLoopCihazi
        {
            Tip = tip,
            Ad = YanginCihazAdi(tip),
            OncekiCihazaMesafeMetre = 0
        };
        loop.Cihazlar.Add(cihaz);
        SeciliYanginLoopCihazlari = new ObservableCollection<ElvYanginLoopCihazi>(loop.Cihazlar);
        SeciliYanginLoopCihazi = cihaz;
        Yenile();
        SeciliYanginKatLoopGrubu = grup;
        SeciliYanginLoop = loop;
        SeciliYanginLoopCihazi = cihaz;
        OnPropertyChanged(nameof(YanginLoopEditorBaslik));
        DurumMesaji = $"{cihaz.Ad}, {loop.Ad} üzerine eklendi.";
    }

    [RelayCommand]
    public void YanginLoopCihaziSil()
    {
        if (SeciliYanginLoop is null || SeciliYanginLoopCihazi is null) return;
        var loop = SeciliYanginLoop;
        var grup = SeciliYanginKatLoopGrubu;
        loop.Cihazlar.Remove(SeciliYanginLoopCihazi);
        SeciliYanginLoopCihazlari = new ObservableCollection<ElvYanginLoopCihazi>(loop.Cihazlar);
        SeciliYanginLoopCihazi = SeciliYanginLoopCihazlari.FirstOrDefault();
        Yenile();
        SeciliYanginKatLoopGrubu = grup;
        SeciliYanginLoop = loop;
        OnPropertyChanged(nameof(YanginLoopEditorBaslik));
        DurumMesaji = "Loop cihazı silindi.";
    }

    [RelayCommand]
    public void YanginLoopCihaziSec(ElvYanginLoopCihazi? cihaz)
    {
        if (cihaz is not null && SeciliYanginLoop?.Cihazlar.Contains(cihaz) == true)
            SeciliYanginLoopCihazi = cihaz;
    }

    /// <summary>
    /// Loop segment metrajı yazılırken genel koleksiyonları yeniden kurmadan yalnız hesapları
    /// ve canvas'ı tazeler. Böylece TextBox odağı ve alttaki loop editörü açık kalır.
    /// </summary>
    public void YanginLoopKabloMetrajiDegisti(ElvYanginLoopCihazi cihaz, double metraj)
    {
        if (SeciliYanginLoop is null || !SeciliYanginLoop.Cihazlar.Contains(cihaz)) return;
        cihaz.OncekiCihazaMesafeMetre = Math.Max(0, metraj);
        IsDirty = true;
        SemaRevizyon++;
        OnPropertyChanged(nameof(YanginLoopEditorBaslik));
        OnPropertyChanged(nameof(ToplamHatMetrajText));
        DurumMesaji = $"{SeciliYanginLoop.Ad} kablo metrajı güncellendi.";
    }

    private static string YanginCihazAdi(ElvYanginCihazTipi tip) => tip switch
    {
        ElvYanginCihazTipi.DumanDedektoru => "Duman Dedektörü",
        ElvYanginCihazTipi.IsiDedektoru => "Isı Dedektörü",
        ElvYanginCihazTipi.KombineDedektor => "Kombine Dedektör",
        ElvYanginCihazTipi.AsmaTavanDedektoru => "Asma Tavan Dedektör Grubu",
        ElvYanginCihazTipi.YanginButonu => "Yangın İhbar Butonu",
        ElvYanginCihazTipi.Siren => "Yangın Sireni",
        ElvYanginCihazTipi.Flasor => "Flaşör",
        ElvYanginCihazTipi.Bilinmeyen => "Tür Belirtilmemiş Yangın Cihazı",
        _ => "Yangın Cihazı"
    };

    // ─── Cihaz ekleme/çıkarma (katalog + manuel) ────────────────────────────

    [RelayCommand]
    public void CihazEkle()
    {
        var hedef = AktifCihazListesi();
        if (hedef is null) return;
        var ad = string.IsNullOrWhiteSpace(YeniCihazAdi) ? "Yeni Cihaz" : YeniCihazAdi.Trim();
        var cihaz = new ElvCihaz { Ad = ad, Adet = Math.Max(1, YeniCihazAdet) };
        hedef.Add(cihaz);
        CihazlariYukle(hedef);
        SeciliCihaz = cihaz;
        YeniCihazAdi = "";
        YeniCihazAdet = 1;
        IsDirty = true;
        SemaRevizyon++;
        DurumMesaji = $"Cihaz eklendi: {ad} ×{cihaz.Adet}";
    }

    [RelayCommand]
    public void CihazSil()
    {
        var hedef = AktifCihazListesi();
        if (hedef is null || SeciliCihaz is null) return;
        hedef.Remove(SeciliCihaz);
        CihazlariYukle(hedef);
        SeciliCihaz = SeciliCihazlar.FirstOrDefault();
        IsDirty = true;
        SemaRevizyon++;
        DurumMesaji = "Cihaz silindi.";
    }

    /// <summary>DataGrid'de cihaz/kablo/sayı düzenlemesi sonrası şema ve toplamları tazeler.</summary>
    public void DuzenlemeTamamlandi()
    {
        if (SeciliCctvIzlemeNoktasi is not null)
        {
            IsDirty = true;
            SemaRevizyon++;
            OnPropertyChanged(nameof(DuzenleyiciBaslik));
            OnPropertyChanged(nameof(ToplamHatMetrajText));
            DurumMesaji = "İzleme noktası güncellendi.";
            return;
        }
        if (SeciliRack != null)
            HatListeleriniYukle(SeciliRack);
        if (SeciliYanginKatLoopGrubu != null)
            SeciliYanginKatLoopGrubu.LoopSayisi = SeciliYanginKatLoopGrubu.Looplar.Count;
        if (SeciliAnalogAnonsZonu is not null)
        {
            ElvAnalogAnonsTopolojiServisi.KabloSegmentleriniEsitle(SeciliAnalogAnonsZonu.HatA);
            if (SeciliAnalogAnonsZonu.HatB is not null)
                ElvAnalogAnonsTopolojiServisi.KabloSegmentleriniEsitle(SeciliAnalogAnonsZonu.HatB);
        }
        Yenile();
        DurumMesaji = "Güncellendi.";
    }

    /// <summary>Metin alanından gelen merkez kablo metrajını uygular ve şemayı anında tazeler.</summary>
    public void MerkezeKabloMetrajiDegistir(double metraj)
    {
        if (SeciliRack is null || double.IsNaN(metraj) || double.IsInfinity(metraj)) return;
        SeciliRack.MerkezeKabloMetraj = Math.Max(0, metraj);
        SemaRevizyon++;
        IsDirty = true;
        OnPropertyChanged(nameof(ToplamMerkezKabloText));
    }

    /// <summary>Kat Düzenle dialog'u sonrası bina kat listesini yeniler.</summary>
    public void BinaKatlariniUygula()
    {
        Proje.KatlariBinadanGuncelle();
        Yenile();
        DurumMesaji = "Kat yapısı güncellendi.";
    }

    public void KutuphaneDegisti()
    {
        IsDirty = true;
        OnPropertyChanged(nameof(CihazKatalogu));
        OnPropertyChanged(nameof(KabloKatalogu));
        DurumMesaji = "Kullanıcı kütüphanesi güncellendi.";
    }

    // ─── Yardımcılar ────────────────────────────────────────────────────────

    private List<ElvCihaz>? AktifCihazListesi()
        => MerkezSecili ? AnaMerkez.Cihazlar : SeciliCctvIzlemeNoktasi?.Cihazlar ?? SeciliRack?.Cihazlar;

    private List<ElvRackKabin> AktifRackListesi()
        => AktifSistem switch
        {
            "Kamera" => KameraSistemi.RackKabinler,
            "Anons" => AnonsSistemi.RackKabinler,
            "Tv" => TvDirektAktif ? [] : TvIpAktif ? TvSistemi.IpTv.RackKabinler : TvSistemi.RackKabinler,
            "Telefon" => TelefonSistemi.RackKabinler,
            "Diafon" => DiafonSistemi.RackKabinler,
            _ => DataSistemi.RackKabinler
        };

    private string AnonsVarsayilanKablo => AnonsSistemi.Teknoloji == "Dijital (PoE)"
        ? "Cat6A F/UTP LSZH"
        : "2x1.5 mm² FE180";

    private const string TelefonVarsayilanKablo = "J-Y(St)Y 2x2x0.6 Telefon Kablosu";
    private const string DiafonVarsayilanKablo = "J-Y(St)Y 3x2x0.8 Diafon Kablosu";

    private IEnumerable<(string Tip, double Metraj)> AktifKatIciKablolar()
    {
        if (AktifSistem == "Yangin")
        {
            foreach (var hat in YanginSistemi.KatLoopGruplari.SelectMany(g => g.Looplar))
                yield return (hat.KabloTipi, hat.Metraj);
            yield break;
        }
        if (TvDirektAktif)
        {
            foreach (var hat in TvSistemi.DirektLnb.Hatlar)
                yield return (hat.KabloTipi, hat.Metraj);
            yield break;
        }

        foreach (var rack in AktifRackListesi())
        {
            if (AktifSistem == "Kamera")
            {
                foreach (var hat in rack.KameraHatlari) yield return (hat.KabloTipi, hat.Metraj);
            }
            else if (AktifSistem == "Anons")
            {
                if (DijitalAnonsAktif)
                    foreach (var hat in rack.DijitalAnonsUclari) yield return (hat.KabloTipi, hat.Metraj);
                else
                    foreach (var hat in rack.AnonsHatlari) yield return (hat.KabloTipi, hat.Metraj);
            }
            else if (AktifSistem == "Tv")
            {
                foreach (var hat in rack.TvHatlari) yield return (hat.KabloTipi, hat.Metraj);
            }
            else if (AktifSistem == "Telefon")
            {
                foreach (var hat in rack.TelefonHatlari) yield return (hat.KabloTipi, hat.Metraj);
            }
            else if (AktifSistem == "Diafon")
            {
                foreach (var hat in rack.DiafonUniteHatlari) yield return (hat.KabloTipi, hat.Metraj);
            }
            else
            {
                foreach (var hat in rack.DataHatlari.Concat(rack.WirelessHatlari)
                             .Concat(rack.IpTelefonHatlari).Concat(rack.YaziciHatlari)
                             .Concat(rack.ErisimKontrolHatlari).Concat(rack.DiafonHatlari))
                    yield return (hat.KabloTipi, hat.Metraj);
            }
        }

        if (TvMerkeziAktif)
            foreach (var hat in TvSistemi.CatiHatlari)
                yield return (hat.KabloTipi, hat.Metraj);
    }

    private static string KabloMetrajOzeti(string baslik, IEnumerable<(string Tip, double Metraj)> kablolar)
    {
        var gruplar = kablolar
            .Where(k => k.Metraj > 0)
            .GroupBy(k => string.IsNullOrWhiteSpace(k.Tip) ? "Belirtilmemiş" : k.Tip.Trim(), StringComparer.CurrentCultureIgnoreCase)
            .Select(g => $"{g.Key}: {g.Sum(x => x.Metraj):0.#} m")
            .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return gruplar.Count == 0 ? $"{baslik}: 0 m" : $"{baslik}: {string.Join("  |  ", gruplar)}";
    }

    private void CihazlariYukle(List<ElvCihaz> liste)
    {
        SeciliCihazlar = new ObservableCollection<ElvCihaz>(liste);
        SeciliCihaz = SeciliCihazlar.FirstOrDefault();
    }

    private void HatListeleriniYukle(ElvRackKabin rack)
    {
        HatListesiniSayiyaEsitle(rack.DataHatlari, rack.DataHatSayisi, "Data");
        HatListesiniSayiyaEsitle(rack.WirelessHatlari, rack.WirelessSayisi, "Wireless");
        KameraHatListesiniSayiyaEsitle(rack);
        AnonsHatListesiniSayiyaEsitle(rack.AnonsHatlari, rack.AnonsHatSayisi, AnonsVarsayilanKablo);
        DijitalAnonsUcListesiniEsitle(rack);
        HatListesiniSayiyaEsitle(rack.TvHatlari, rack.TvPrizSayisi,
            TvIpAktif ? "IP-TV" : "TV", TvIpAktif ? "Cat6A F/UTP LSZH" : "RG-6/U Koaksiyel");
        HatListesiniSayiyaEsitle(rack.TelefonHatlari, rack.TelefonPrizSayisi, "Telefon", TelefonVarsayilanKablo);
        HatListesiniSayiyaEsitle(rack.DiafonUniteHatlari, rack.DiafonUniteSayisi, "Diafon Ünitesi", DiafonVarsayilanKablo);
        HatListesiniSayiyaEsitle(rack.IpTelefonHatlari, rack.IpTelefonSayisi, "IP Telefon", DataKabloVarsayilan);
        HatListesiniSayiyaEsitle(rack.YaziciHatlari, rack.YaziciSayisi, "Yazıcı", DataKabloVarsayilan);
        HatListesiniSayiyaEsitle(rack.ErisimKontrolHatlari, rack.ErisimKontrolSayisi, "Erişim Kontrol", DataKabloVarsayilan);
        HatListesiniSayiyaEsitle(rack.DiafonHatlari, rack.DiafonSayisi, "Diafon/IP Ses", DataKabloVarsayilan);
    }

    private static void HatListesiniSayiyaEsitle(List<ElvHatMetraj> liste, int adet, string prefix, string kabloTipi = "Cat6 U/UTP")
    {
        adet = Math.Max(0, adet);
        while (liste.Count < adet)
        {
            int no = liste.Count + 1;
            liste.Add(new ElvHatMetraj
            {
                HatNo = no,
                Ad = $"{prefix} {no}",
                KabloTipi = kabloTipi
            });
        }

        while (liste.Count > adet)
            liste.RemoveAt(liste.Count - 1);

        for (int i = 0; i < liste.Count; i++)
        {
            liste[i].HatNo = i + 1;
            if (string.IsNullOrWhiteSpace(liste[i].Ad))
                liste[i].Ad = $"{prefix} {i + 1}";
            if (string.IsNullOrWhiteSpace(liste[i].KabloTipi))
                liste[i].KabloTipi = kabloTipi;
        }
    }

    private static void KameraHatListesiniSayiyaEsitle(ElvRackKabin rack)
    {
        HatListesiniSayiyaEsitle(rack.KameraHatlari, rack.KameraSayisi, "Kamera");
        for (int i = 0; i < rack.KameraHatlari.Count; i++)
        {
            string yeniAd;
            if (i < rack.DahiliKameraSayisi)
                yeniAd = $"Dahili Kamera {i + 1}";
            else if (i < rack.DahiliKameraSayisi + rack.HariciKameraSayisi)
                yeniAd = $"Harici Kamera {i - rack.DahiliKameraSayisi + 1}";
            else
                yeniAd = $"PTZ Kamera {i - rack.DahiliKameraSayisi - rack.HariciKameraSayisi + 1}";

            var eskiAd = rack.KameraHatlari[i].Ad;
            if (string.IsNullOrWhiteSpace(eskiAd)
                || eskiAd.StartsWith("Kamera ", StringComparison.CurrentCultureIgnoreCase)
                || eskiAd.StartsWith("Dahili Kamera ", StringComparison.CurrentCultureIgnoreCase)
                || eskiAd.StartsWith("Harici Kamera ", StringComparison.CurrentCultureIgnoreCase)
                || eskiAd.StartsWith("PTZ Kamera ", StringComparison.CurrentCultureIgnoreCase))
                rack.KameraHatlari[i].Ad = yeniAd;
        }
    }

    private static void DijitalAnonsUcListesiniEsitle(ElvRackKabin rack)
    {
        HatListesiniSayiyaEsitle(rack.DijitalAnonsUclari, rack.ToplamDijitalAnonsUcu, "IP Anons Ucu", "Cat6A F/UTP LSZH");
        var tipler = new (int Adet, string Ad)[]
        {
            (rack.IpTavanHoparlorSayisi, "IP Tavan Hoparlörü"),
            (rack.IpDuvarHoparlorSayisi, "IP Duvar/Kabin Hoparlörü"),
            (rack.IpHornHoparlorSayisi, "IP Horn Hoparlör"),
            (rack.IpProjektorHoparlorSayisi, "IP Projektör Hoparlör"),
            (rack.IpAnonsTerminaliSayisi, "IP Anons Terminali")
        };
        int index = 0;
        foreach (var (adet, ad) in tipler)
        {
            for (int no = 1; no <= adet; no++, index++)
            {
                var eski = rack.DijitalAnonsUclari[index].Ad;
                if (string.IsNullOrWhiteSpace(eski) || eski.StartsWith("IP ", StringComparison.CurrentCultureIgnoreCase))
                    rack.DijitalAnonsUclari[index].Ad = $"{ad} {no}";
            }
        }
    }

    private static void TvCatiHatlariniSayiyaEsitle(ElvTvSistemi tv)
    {
        tv.CatiUyduSayisi = Math.Max(1, tv.CatiUyduSayisi);
        while (tv.CatiHatlari.Count < tv.CatiUyduSayisi)
        {
            int no = tv.CatiHatlari.Count + 1;
            tv.CatiHatlari.Add(new ElvTvCatiHatti { HatNo = no, Ad = $"Çatı Uydu {no}" });
        }

        while (tv.CatiHatlari.Count > tv.CatiUyduSayisi)
            tv.CatiHatlari.RemoveAt(tv.CatiHatlari.Count - 1);

        for (int i = 0; i < tv.CatiHatlari.Count; i++)
        {
            tv.CatiHatlari[i].HatNo = i + 1;
            if (string.IsNullOrWhiteSpace(tv.CatiHatlari[i].Ad))
                tv.CatiHatlari[i].Ad = $"Çatı Uydu {i + 1}";
            if (string.IsNullOrWhiteSpace(tv.CatiHatlari[i].KabloTipi))
                tv.CatiHatlari[i].KabloTipi = "RG-11/U Koaksiyel";
        }
    }

    private static void AnonsHatListesiniSayiyaEsitle(List<ElvAnonsHatti> liste, int adet, string kabloTipi = "2x1.5 mm² FE180")
    {
        adet = Math.Max(0, adet);
        while (liste.Count < adet)
        {
            int no = liste.Count + 1;
            liste.Add(new ElvAnonsHatti
            {
                HatNo = no,
                Ad = $"Anons Hattı {no}",
                KabloTipi = kabloTipi
            });
        }

        while (liste.Count > adet)
            liste.RemoveAt(liste.Count - 1);

        for (int i = 0; i < liste.Count; i++)
        {
            liste[i].HatNo = i + 1;
            if (string.IsNullOrWhiteSpace(liste[i].Ad))
                liste[i].Ad = $"Anons Hattı {i + 1}";
            if (string.IsNullOrWhiteSpace(liste[i].KabloTipi))
                liste[i].KabloTipi = kabloTipi;
        }
    }

    private static void YanginLoopListesiniSayiyaEsitle(List<ElvYanginLoop> liste, int adet)
    {
        adet = Math.Max(0, adet);
        while (liste.Count < adet)
        {
            int no = liste.Count + 1;
            liste.Add(new ElvYanginLoop
            {
                LoopNo = no,
                Ad = $"Loop {no}",
                KabloTipi = "J-Y(St)Y 1x2x0.8 Yangın Kablosu"
            });
        }

        while (liste.Count > adet)
            liste.RemoveAt(liste.Count - 1);

        for (int i = 0; i < liste.Count; i++)
        {
            liste[i].LoopNo = i + 1;
            if (string.IsNullOrWhiteSpace(liste[i].Ad))
                liste[i].Ad = $"Loop {i + 1}";
            if (string.IsNullOrWhiteSpace(liste[i].KabloTipi))
                liste[i].KabloTipi = "J-Y(St)Y 1x2x0.8 Yangın Kablosu";
        }
    }

    private void Yenile(bool baglantiSecenekleriniYenile = true, bool kirlet = true)
    {
        // Yenile hemen her veri değişiminden sonra çağrılır; sistem sekmesi değişimi ve
        // yükleme akışları kirlet:false geçer (yükleme sonunda IsDirty zaten sıfırlanır).
        if (kirlet) IsDirty = true;
        RackKabinler = new ObservableCollection<ElvRackKabin>(AktifRackListesi());
        CctvIzlemeNoktalari = new ObservableCollection<ElvCctvIzlemeNoktasi>(KameraSistemi.IzlemeNoktalari);
        IpTvAkislari = new ObservableCollection<ElvIpTvAkisi>(TvSistemi.IpTv.Akislar);
        YanginKatLoopGruplari = new ObservableCollection<ElvYanginKatLoopGrubu>(YanginSistemi.KatLoopGruplari);
        AnalogAnonsZonlari = new ObservableCollection<ElvAnonsZonu>(AnonsSistemi.AnalogZonlar.OrderByDescending(z => z.KatNo));
        SemaRevizyon++;
        OnPropertyChanged(nameof(DataSistemi));
        OnPropertyChanged(nameof(KameraSistemi));
        OnPropertyChanged(nameof(AnonsSistemi));
        OnPropertyChanged(nameof(TvSistemi));
        OnPropertyChanged(nameof(YanginSistemi));
        OnPropertyChanged(nameof(Katlar));
        OnPropertyChanged(nameof(AnaMerkez));
        OnPropertyChanged(nameof(CihazKatalogu));
        OnPropertyChanged(nameof(KabloKatalogu));
        OnPropertyChanged(nameof(SistemBaslik));
        OnPropertyChanged(nameof(MerkezButonMetni));
        OnPropertyChanged(nameof(RackEkleMetni));
        OnPropertyChanged(nameof(ToplayiciBaslik));
        OnPropertyChanged(nameof(ToplayiciSayacText));
        OnPropertyChanged(nameof(RackListesiGorunur));
        OnPropertyChanged(nameof(StandartRackListesiGorunur));
        OnPropertyChanged(nameof(RackIslemleriGorunur));
        OnPropertyChanged(nameof(CctvIzlemeNoktalariGorunur));
        OnPropertyChanged(nameof(CctvIzlemeBaglantiSecenekleri));
        OnPropertyChanged(nameof(SeciliCctvIzlemeBaglantiId));
        OnPropertyChanged(nameof(SeciliCctvIzlemeKatNo));
        OnPropertyChanged(nameof(AnalogAnonsAktif));
        OnPropertyChanged(nameof(AnalogAnonsZonListesiGorunur));
        OnPropertyChanged(nameof(AnalogAnonsZonDuzenlenebilir));
        OnPropertyChanged(nameof(AnalogGucOzeti));
        OnPropertyChanged(nameof(AnalogGucKaynagi));
        OnPropertyChanged(nameof(AnalogAkuOzeti));
        OnPropertyChanged(nameof(YanginListesiGorunur));
        OnPropertyChanged(nameof(BirincilSayacBaslik));
        OnPropertyChanged(nameof(IkincilSayacBaslik));
        OnPropertyChanged(nameof(IkincilSayacGorunur));
        OnPropertyChanged(nameof(StandartRackSayaclariGorunur));
        OnPropertyChanged(nameof(KameraTipSayaclariGorunur));
        OnPropertyChanged(nameof(KatIciKabloBaslik));
        OnPropertyChanged(nameof(ToplamBirincilText));
        OnPropertyChanged(nameof(ToplamIkincilText));
        OnPropertyChanged(nameof(ToplamMerkezKabloText));
        OnPropertyChanged(nameof(ToplamMerkezKabloGorunur));
        OnPropertyChanged(nameof(ToplamHatMetrajText));
        OnPropertyChanged(nameof(DataHatMetrajGorunur));
        OnPropertyChanged(nameof(WirelessHatMetrajGorunur));
        OnPropertyChanged(nameof(KameraHatMetrajGorunur));
        OnPropertyChanged(nameof(AnonsHatlariGorunur));
        OnPropertyChanged(nameof(DijitalAnonsAktif));
        OnPropertyChanged(nameof(DijitalAnonsUclariGorunur));
        OnPropertyChanged(nameof(AnonsTeknolojisiGorunur));
        OnPropertyChanged(nameof(SeciliAnonsTeknolojisi));
        OnPropertyChanged(nameof(TvHatMetrajGorunur));
        OnPropertyChanged(nameof(TvTeknolojisiGorunur));
        OnPropertyChanged(nameof(SeciliTvTeknolojisi));
        OnPropertyChanged(nameof(TvDirektAktif));
        OnPropertyChanged(nameof(TvMerkeziAktif));
        OnPropertyChanged(nameof(TvIpAktif));
        OnPropertyChanged(nameof(DirektTvUcSayisi));
        OnPropertyChanged(nameof(TvIpBantGenisligiOzeti));
        OnPropertyChanged(nameof(TelefonHatMetrajGorunur));
        OnPropertyChanged(nameof(DiafonUniteHatMetrajGorunur));
        OnPropertyChanged(nameof(TelefonSistemi));
        OnPropertyChanged(nameof(DiafonSistemi));
        OnPropertyChanged(nameof(TvCatiHatlariGorunur));
        OnPropertyChanged(nameof(TvCatiUyduSayisi));
        OnPropertyChanged(nameof(YanginLooplariGorunur));
        OnPropertyChanged(nameof(YanginLoopEditorGorunur));
        OnPropertyChanged(nameof(YanginLoopEditorBaslik));
        // Seçenekler yalnız rack kümesi/sistemi değiştiğinde tazelenir. Salt bağlantı
        // değişiminde ItemsSource kararlı kalmalı; aksi halde ComboBox Ana Merkez'e döner.
        if (baglantiSecenekleriniYenile)
            OnPropertyChanged(nameof(UstBaglantiSecenekleri));
        BildirRackSayaclari();
        OnPropertyChanged(nameof(SeciliYanginLoopSayisi));
        OnPropertyChanged(nameof(CihazPanelGorunur));
        OnPropertyChanged(nameof(CctvIzlemeNoktasiDuzenlenebilir));
        OnPropertyChanged(nameof(SeciliAnalogCiftHat));
        OnPropertyChanged(nameof(AnalogAmplifikatorSecenekleri));
        OnPropertyChanged(nameof(SeciliHatAAmplifikatorId));
        OnPropertyChanged(nameof(SeciliHatBAmplifikatorId));
        OnPropertyChanged(nameof(KapasiteDenetimGorunur));
        OnPropertyChanged(nameof(AnaMerkezKatNo));
        OnPropertyChanged(nameof(PencereBasligi));
        BildirSecimDegisti();
    }

    // Seçili rack'in tüm sayaç ve Data ek-hat görünürlük property'lerini tazeler.
    private void BildirRackSayaclari()
    {
        OnPropertyChanged(nameof(SeciliRackBirincilSayac));
        OnPropertyChanged(nameof(SeciliRackIkincilSayac));
        OnPropertyChanged(nameof(SeciliRackIpTelefonSayisi));
        OnPropertyChanged(nameof(SeciliRackYaziciSayisi));
        OnPropertyChanged(nameof(SeciliRackErisimKontrolSayisi));
        OnPropertyChanged(nameof(SeciliRackDiafonSayisi));
        OnPropertyChanged(nameof(SeciliRackDahiliKameraSayisi));
        OnPropertyChanged(nameof(SeciliRackHariciKameraSayisi));
        OnPropertyChanged(nameof(SeciliRackPtzKameraSayisi));
        OnPropertyChanged(nameof(SeciliRackIpTavanHoparlorSayisi));
        OnPropertyChanged(nameof(SeciliRackIpDuvarHoparlorSayisi));
        OnPropertyChanged(nameof(SeciliRackIpHornHoparlorSayisi));
        OnPropertyChanged(nameof(SeciliRackIpProjektorHoparlorSayisi));
        OnPropertyChanged(nameof(SeciliRackIpAnonsTerminaliSayisi));
        OnPropertyChanged(nameof(SeciliRackKatIciKabloMetraj));
        OnPropertyChanged(nameof(DataEkHatlarGorunur));
        OnPropertyChanged(nameof(IpTelefonHatMetrajGorunur));
        OnPropertyChanged(nameof(YaziciHatMetrajGorunur));
        OnPropertyChanged(nameof(ErisimKontrolHatMetrajGorunur));
        OnPropertyChanged(nameof(DiafonHatMetrajGorunur));
        OnPropertyChanged(nameof(UstBaglantiGorunur));
        OnPropertyChanged(nameof(SeciliUstRackId));
    }

    private void BildirSecimDegisti()
    {
        OnPropertyChanged(nameof(DuzenleyiciBaslik));
        OnPropertyChanged(nameof(RackDuzenlenebilir));
        OnPropertyChanged(nameof(CctvIzlemeNoktasiDuzenlenebilir));
        OnPropertyChanged(nameof(YanginKatDuzenlenebilir));
        OnPropertyChanged(nameof(YanginLooplariGorunur));
        OnPropertyChanged(nameof(CihazPanelGorunur));
        OnPropertyChanged(nameof(AnalogAnonsZonDuzenlenebilir));
        OnPropertyChanged(nameof(AnaMerkezKatNo));
        // Data'da Ana Merkez ↔ kat rack seçimine göre cihaz kataloğu değişir.
        OnPropertyChanged(nameof(CihazKatalogu));
        OnPropertyChanged(nameof(CctvIzlemeBaglantiSecenekleri));
        OnPropertyChanged(nameof(SeciliCctvIzlemeBaglantiId));
        OnPropertyChanged(nameof(SeciliCctvIzlemeKatNo));
    }

    [RelayCommand]
    public void SistemSec(string sistem)
    {
        if (sistem is not ("Data" or "Kamera" or "Anons" or "Tv" or "Yangin" or "Telefon" or "Diafon")) return;
        AktifSistem = sistem;
        SeciliRack = null;
        SeciliCctvIzlemeNoktasi = null;
        SeciliYanginKatLoopGrubu = null;
        Yenile(kirlet: false);
        MerkeziSec();
        DurumMesaji = $"{SistemBaslik} seçildi.";
    }

    partial void OnAktifSistemChanged(string value)
    {
        YeniCihazAdi = "";
        YeniCihazAdet = 1;
        Yenile(kirlet: false);
    }

    private static IStorageProvider? StorageProvider()
        => (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow?.StorageProvider;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };
}

/// <summary>Rack üst bağlantı seçimi öğesi. <see cref="Id"/> null ise Ana Merkez.</summary>
public sealed record RackBaglantiSecenek(string? Id, string Ad);
