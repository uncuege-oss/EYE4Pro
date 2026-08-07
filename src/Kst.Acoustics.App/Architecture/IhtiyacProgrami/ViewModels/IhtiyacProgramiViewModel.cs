using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Kst.Acoustics.Core.Architecture.Models;
using Kst.Acoustics.Core.Architecture.Rules;
using Kst.Acoustics.Core.Architecture.Services;
using Kst.Acoustics.Core.Architecture.Templates;
using MimariIhtiyacProgrami = Kst.Acoustics.Core.Architecture.Models.IhtiyacProgrami;

namespace Kst.Acoustics.App.Architecture.IhtiyacProgrami.ViewModels;

public sealed class IhtiyacProgramiViewModel : ObservableObject
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private MimariProje _proje = MimariProjeServisi.YeniOlustur("Yeni Mimari Proje");
    private MimariIhtiyacProgrami _program = new();
    private bool _programYukleniyor;
    private BinaIsleviSecenekViewModel? _seciliBinaIslevi;
    private string _projeAdi = "Yeni Mimari Proje";
    private string _projeTipi = "";
    private string _kullaniciKapasitesiText = "50";
    private string _katSayisiText = "1";
    private string _dosyaYoluText = "Kaydedilmedi";
    private string _netAlanText = "0,0 m²";
    private string _sirkulasyonAlanText = "0,0 m²";
    private string _teknikAlanText = "0,0 m²";
    private string _brutAlanText = "0,0 m²";
    private string _hedefFarkText = "0,0 m²";
    private string _netHacimText = "0,0 m³";
    private string _mahalSayisiText = "0 mahal";
    private string _durumMesaji = "Hazır.";

    public IhtiyacProgramiViewModel()
    {
        BinaIslevleri = new ObservableCollection<BinaIsleviSecenekViewModel>(
            BinaIsleviTanimlari.Tum.Select(t => new BinaIsleviSecenekViewModel(
                t.Islev,
                t.Ad,
                t.Aciklama,
                t.KapasiteEtiketi)));

        SeciliBinaIslevi = BinaIslevleri.FirstOrDefault(s => s.Islev == BinaIslevi.Ofis)
            ?? BinaIslevleri.FirstOrDefault();
    }

    public ObservableCollection<BinaIsleviSecenekViewModel> BinaIslevleri { get; }
    public ObservableCollection<MahalGereksinimiSatiriViewModel> Mahaller { get; } = new();
    public ObservableCollection<KullanimSinifiAlanSatiriViewModel> KullanimSinifiOzetleri { get; } = new();
    public ObservableCollection<DogrulamaSonucuSatiriViewModel> DogrulamaSonuclari { get; } = new();
    public string? DosyaYolu => _proje.DosyaYolu;

    public BinaIsleviSecenekViewModel? SeciliBinaIslevi
    {
        get => _seciliBinaIslevi;
        set
        {
            if (!SetProperty(ref _seciliBinaIslevi, value))
                return;

            if (value is not null)
                ProjeTipi = value.Ad;

            OnPropertyChanged(nameof(SeciliBinaIsleviAciklama));
            OnPropertyChanged(nameof(KapasiteEtiketi));
            if (!_programYukleniyor)
                ProgramiYenile();
        }
    }

    public string SeciliBinaIsleviAciklama => SeciliBinaIslevi?.Aciklama ?? "";
    public string KapasiteEtiketi => SeciliBinaIslevi?.KapasiteEtiketi ?? "Kapasite";

    public string ProjeAdi
    {
        get => _projeAdi;
        set => SetProperty(ref _projeAdi, value);
    }

    public string ProjeTipi
    {
        get => _projeTipi;
        set => SetProperty(ref _projeTipi, value);
    }

    public string KullaniciKapasitesiText
    {
        get => _kullaniciKapasitesiText;
        set => SetProperty(ref _kullaniciKapasitesiText, value);
    }

    public string KatSayisiText
    {
        get => _katSayisiText;
        set => SetProperty(ref _katSayisiText, value);
    }

    public string DosyaYoluText
    {
        get => _dosyaYoluText;
        private set => SetProperty(ref _dosyaYoluText, value);
    }

    public string NetAlanText
    {
        get => _netAlanText;
        private set => SetProperty(ref _netAlanText, value);
    }

    public string SirkulasyonAlanText
    {
        get => _sirkulasyonAlanText;
        private set => SetProperty(ref _sirkulasyonAlanText, value);
    }

    public string TeknikAlanText
    {
        get => _teknikAlanText;
        private set => SetProperty(ref _teknikAlanText, value);
    }

    public string BrutAlanText
    {
        get => _brutAlanText;
        private set => SetProperty(ref _brutAlanText, value);
    }

    public string HedefFarkText
    {
        get => _hedefFarkText;
        private set => SetProperty(ref _hedefFarkText, value);
    }

    public string NetHacimText
    {
        get => _netHacimText;
        private set => SetProperty(ref _netHacimText, value);
    }

    public string MahalSayisiText
    {
        get => _mahalSayisiText;
        private set => SetProperty(ref _mahalSayisiText, value);
    }

    public string DurumMesaji
    {
        get => _durumMesaji;
        set => SetProperty(ref _durumMesaji, value);
    }

    public void ProgramiYenile()
    {
        if (SeciliBinaIslevi is null)
            return;

        var kapasite = PozitifTamsayiOku(KullaniciKapasitesiText, 0, 0);
        var katSayisi = PozitifTamsayiOku(KatSayisiText, 1, 1);

        _program = IhtiyacProgramiUretimServisi.Uret(
            SeciliBinaIslevi.Islev,
            kapasite,
            katSayisi,
            ProjeTipi);

        MahalleriBagla();
        OzetiYenile();
        DurumMesaji = $"{SeciliBinaIslevi.Ad} için ihtiyaç programı üretildi.";
    }

    public void YeniMahalEkle()
    {
        var mahal = new MahalGereksinimi
        {
            Ad = "Yeni Mahal",
            MahalTipi = MahalTipi.Ofis,
            KullanimSinifi = KullanimSinifi.AnaKullanim,
            Adet = 1,
            MinimumAlanM2 = 8,
            IdealAlanM2 = 12,
            HedefAlanM2 = 12,
            MinimumNetYukseklikM = _program.VarsayilanKatYuksekligiM > 0 ? Math.Min(2.8, _program.VarsayilanKatYuksekligiM) : 2.8,
            HedefNetYukseklikM = _program.VarsayilanKatYuksekligiM > 0 ? Math.Min(2.8, _program.VarsayilanKatYuksekligiM) : 2.8,
            Zorunlu = false
        };

        _program.Mahaller.Add(mahal);
        Mahaller.Add(new MahalGereksinimiSatiriViewModel(mahal, MahalDegisti));
        OzetiYenile();
        DurumMesaji = "Yeni mahal eklendi.";
    }

    public void MahalKopyala(MahalGereksinimiSatiriViewModel satir)
    {
        var kaynak = satir.Model;
        var kopya = new MahalGereksinimi
        {
            MahalTipi = kaynak.MahalTipi,
            KullanimSinifi = kaynak.KullanimSinifi,
            Ad = $"{kaynak.Ad} Kopya",
            Adet = kaynak.Adet,
            KisiKapasitesi = kaynak.KisiKapasitesi,
            MinimumAlanM2 = kaynak.MinimumAlanM2,
            IdealAlanM2 = kaynak.IdealAlanM2,
            HedefAlanM2 = kaynak.HedefAlanM2,
            MinimumNetYukseklikM = kaynak.MinimumNetYukseklikM,
            HedefNetYukseklikM = kaynak.HedefNetYukseklikM,
            Zorunlu = false,
            Notlar = kaynak.Notlar
        };

        var kaynakIndeks = Mahaller.IndexOf(satir);
        var eklemeIndeksi = kaynakIndeks >= 0 ? kaynakIndeks + 1 : Mahaller.Count;
        _program.Mahaller.Insert(eklemeIndeksi, kopya);
        Mahaller.Insert(eklemeIndeksi, new MahalGereksinimiSatiriViewModel(kopya, MahalDegisti));
        OzetiYenile();
        DurumMesaji = $"'{kaynak.Ad}' kopyalandı.";
    }

    public void MahalSil(MahalGereksinimiSatiriViewModel satir)
    {
        if (!Mahaller.Remove(satir))
            return;

        _program.Mahaller.Remove(satir.Model);
        OzetiYenile();
        DurumMesaji = $"'{satir.Ad}' silindi.";
    }

    public MimariProje KaydaHazirla()
    {
        ProgramMetaVerisiniGuncelle();
        _proje.ProjeAdi = string.IsNullOrWhiteSpace(ProjeAdi) ? "Yeni Mimari Proje" : ProjeAdi.Trim();
        _proje.IhtiyacProgrami = _program;
        _proje.MahalSablonlari = VarsayilanMahalSablonlari.Tum.ToList();
        return _proje;
    }

    public void KayitTamamlandi(string dosyaYolu)
    {
        _proje.DosyaYolu = dosyaYolu;
        DosyaYoluText = Path.GetFileName(dosyaYolu);
        DurumMesaji = $"Kaydedildi: {Path.GetFileName(dosyaYolu)}";
        DogrulamayiYenile();
        OnPropertyChanged(nameof(DosyaYolu));
    }

    public void ProjeYukle(MimariProje proje)
    {
        _proje = proje;
        _program = proje.IhtiyacProgrami ?? new MimariIhtiyacProgrami();

        _programYukleniyor = true;
        ProjeAdi = string.IsNullOrWhiteSpace(proje.ProjeAdi) ? "Yeni Mimari Proje" : proje.ProjeAdi;
        SeciliBinaIslevi = BinaIslevleri.FirstOrDefault(s => s.Islev == _program.BinaIslevi)
            ?? BinaIslevleri.FirstOrDefault();
        ProjeTipi = _program.ProjeTipi;
        KullaniciKapasitesiText = _program.KullaniciKapasitesi.ToString(Tr);
        KatSayisiText = Math.Max(1, _program.KatSayisi).ToString(Tr);
        _programYukleniyor = false;

        DosyaYoluText = string.IsNullOrWhiteSpace(proje.DosyaYolu)
            ? "Kaydedilmedi"
            : Path.GetFileName(proje.DosyaYolu);
        MahalleriBagla();
        OzetiYenile();
        DurumMesaji = $"Açıldı: {DosyaYoluText}";
        OnPropertyChanged(nameof(DosyaYolu));
    }

    public void DogrulamayiYenile()
    {
        var sonuclar = YonetmelikOnKontrolServisi.IlkVeriKontrolleriniCalistir(KaydaHazirla());
        DogrulamaSonuclari.Clear();

        if (sonuclar.Count == 0)
        {
            DogrulamaSonuclari.Add(new DogrulamaSonucuSatiriViewModel(
                KontrolSeviyesi.Bilgi,
                "M34-VERI-TEMIZ",
                "Ön kontrol tamamlandı. Kritik hata veya uyarı bulunmadı.",
                "",
                YonetmelikOnKontrolServisi.VarsayilanVeriKaynakKodu));
            return;
        }

        foreach (var sonuc in sonuclar)
        {
            DogrulamaSonuclari.Add(new DogrulamaSonucuSatiriViewModel(sonuc));
        }
    }

    private void MahalDegisti()
    {
        OzetiYenile();
        DurumMesaji = "Mahal değerleri güncellendi.";
    }

    private void MahalleriBagla()
    {
        Mahaller.Clear();
        foreach (var mahal in _program.Mahaller)
        {
            Mahaller.Add(new MahalGereksinimiSatiriViewModel(mahal, MahalDegisti));
        }
    }

    private void OzetiYenile()
    {
        ProgramMetaVerisiniGuncelle();
        var ozet = AlanOzetiServisi.Hesapla(_program);

        KullanimSinifiOzetleri.Clear();
        foreach (var sinifOzeti in ozet.KullanimSinifiOzetleri)
        {
            KullanimSinifiOzetleri.Add(new KullanimSinifiAlanSatiriViewModel(sinifOzeti));
        }

        NetAlanText = AlanYaz(ozet.NetAlanM2);
        SirkulasyonAlanText = AlanYaz(ozet.SirkulasyonAlanM2);
        TeknikAlanText = AlanYaz(ozet.TeknikAlanM2);
        BrutAlanText = AlanYaz(ozet.BrutAlanM2);
        HedefFarkText = AlanYaz(ozet.HedefFarkM2);
        NetHacimText = HacimYaz(ozet.NetHacimM3);
        MahalSayisiText = $"{ozet.MahalSayisi} satır / {ozet.ToplamMahalAdedi} mahal";
        DogrulamayiYenile();
    }

    private void ProgramMetaVerisiniGuncelle()
    {
        if (SeciliBinaIslevi is not null)
            _program.BinaIslevi = SeciliBinaIslevi.Islev;

        _program.ProjeTipi = ProjeTipi.Trim();
        _program.KullaniciKapasitesi = PozitifTamsayiOku(KullaniciKapasitesiText, 0, 0);
        _program.KatSayisi = PozitifTamsayiOku(KatSayisiText, 1, 1);
    }

    private static int PozitifTamsayiOku(string deger, int varsayilan, int minimum)
    {
        if (!int.TryParse(deger, NumberStyles.Integer, Tr, out var sonuc))
            sonuc = varsayilan;

        return Math.Max(minimum, sonuc);
    }

    internal static bool DoubleOku(string deger, out double sonuc)
    {
        if (double.TryParse(deger, NumberStyles.Float, Tr, out sonuc))
            return true;

        return double.TryParse(deger.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out sonuc);
    }

    internal static string AlanYaz(double deger) => string.Format(Tr, "{0:N1} m²", deger);
    internal static string AlanDegerYaz(double deger) => string.Format(Tr, "{0:N1}", deger);
    internal static string HacimYaz(double deger) => string.Format(Tr, "{0:N1} m³", deger);
    internal static string YukseklikDegerYaz(double deger) => string.Format(Tr, "{0:N2}", deger);

    internal static string KullanimSinifiYaz(KullanimSinifi sinif)
        => sinif switch
        {
            KullanimSinifi.AnaKullanim => "Ana kullanım",
            KullanimSinifi.YardimciKullanim => "Yardımcı kullanım",
            KullanimSinifi.Sirkulasyon => "Sirkülasyon",
            KullanimSinifi.IslakHacim => "Islak hacim",
            KullanimSinifi.TeknikHacim => "Teknik hacim",
            KullanimSinifi.Servis => "Servis",
            KullanimSinifi.Depolama => "Depolama",
            KullanimSinifi.DuseySirkulasyon => "Düşey sirkülasyon",
            KullanimSinifi.AcikAlan => "Açık alan",
            KullanimSinifi.Otopark => "Otopark",
            KullanimSinifi.GuvenlikVeYangin => "Güvenlik ve yangın",
            KullanimSinifi.HesapDisi => "Hesap dışı",
            _ => "Tanımsız"
        };

    internal static string MahalTipiYaz(MahalTipi tip)
        => tip switch
        {
            MahalTipi.AcikOfis => "Açık ofis",
            MahalTipi.ToplantiOdasi => "Toplantı odası",
            MahalTipi.TeknikOda => "Teknik oda",
            MahalTipi.ServerOdasi => "Server odası",
            MahalTipi.KonferansSalonu => "Konferans salonu",
            MahalTipi.BeklemeAlani => "Bekleme alanı",
            MahalTipi.ElektrikOdasi => "Elektrik odası",
            MahalTipi.YatakOdasi => "Yatak odası",
            MahalTipi.MuayeneOdasi => "Muayene odası",
            MahalTipi.PersonelOdasi => "Personel odası",
            MahalTipi.OgretmenOdasi => "Öğretmen odası",
            MahalTipi.Restoran => "Restoran",
            MahalTipi.Mutfak => "Mutfak",
            MahalTipi.Banyo => "Banyo",
            MahalTipi.Wc => "WC",
            MahalTipi.Koridor => "Koridor",
            MahalTipi.Merdiven => "Merdiven",
            MahalTipi.Asansor => "Asansör",
            MahalTipi.Saft => "Şaft",
            _ => tip.ToString()
        };
}

public sealed record BinaIsleviSecenekViewModel(
    BinaIslevi Islev,
    string Ad,
    string Aciklama,
    string KapasiteEtiketi);

public sealed class MahalGereksinimiSatiriViewModel : ObservableObject
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private readonly Action _degisti;
    private string _ad;
    private string _adetText;
    private string _hedefAlanText;
    private string _hedefNetYukseklikText;

    public MahalGereksinimiSatiriViewModel(MahalGereksinimi model, Action degisti)
    {
        Model = model;
        _degisti = degisti;
        _ad = model.Ad;
        _adetText = model.Adet.ToString(Tr);
        _hedefAlanText = IhtiyacProgramiViewModel.AlanDegerYaz(model.HedefAlanM2);
        _hedefNetYukseklikText = IhtiyacProgramiViewModel.YukseklikDegerYaz(model.HedefNetYukseklikM);
    }

    public MahalGereksinimi Model { get; }
    public string MahalTipi => IhtiyacProgramiViewModel.MahalTipiYaz(Model.MahalTipi);
    public string KullanimSinifi => IhtiyacProgramiViewModel.KullanimSinifiYaz(Model.KullanimSinifi);
    public string ToplamAlan => IhtiyacProgramiViewModel.AlanYaz(Model.ToplamHedefAlanM2);
    public string Hacim => IhtiyacProgramiViewModel.HacimYaz(Model.ToplamHedefHacimM3);
    public string Durum => Model.Zorunlu ? "Zorunlu" : "Opsiyonel";

    public string Ad
    {
        get => _ad;
        set
        {
            if (!SetProperty(ref _ad, value))
                return;

            Model.Ad = value.Trim();
            _degisti();
        }
    }

    public string AdetText
    {
        get => _adetText;
        set
        {
            if (!SetProperty(ref _adetText, value))
                return;

            if (int.TryParse(value, NumberStyles.Integer, Tr, out var adet))
            {
                Model.Adet = Math.Max(1, adet);
                SayisalSonuclarDegisti();
            }
        }
    }

    public string HedefAlanText
    {
        get => _hedefAlanText;
        set
        {
            if (!SetProperty(ref _hedefAlanText, value))
                return;

            if (IhtiyacProgramiViewModel.DoubleOku(value, out var alan))
            {
                Model.HedefAlanM2 = Math.Max(0, alan);
                SayisalSonuclarDegisti();
            }
        }
    }

    public string HedefNetYukseklikText
    {
        get => _hedefNetYukseklikText;
        set
        {
            if (!SetProperty(ref _hedefNetYukseklikText, value))
                return;

            if (IhtiyacProgramiViewModel.DoubleOku(value, out var yukseklik))
            {
                Model.HedefNetYukseklikM = Math.Max(0.1, yukseklik);
                SayisalSonuclarDegisti();
            }
        }
    }

    private void SayisalSonuclarDegisti()
    {
        OnPropertyChanged(nameof(ToplamAlan));
        OnPropertyChanged(nameof(Hacim));
        _degisti();
    }
}

public sealed class KullanimSinifiAlanSatiriViewModel
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public KullanimSinifiAlanSatiriViewModel(KullanimSinifiAlanOzeti ozet)
    {
        Sinif = IhtiyacProgramiViewModel.KullanimSinifiYaz(ozet.KullanimSinifi);
        MahalSayisi = ozet.MahalSayisi.ToString(Tr);
        ToplamAdet = ozet.ToplamAdet.ToString(Tr);
        Alan = IhtiyacProgramiViewModel.AlanYaz(ozet.AlanM2);
        Hacim = IhtiyacProgramiViewModel.HacimYaz(ozet.HacimM3);
        Oran = string.Format(Tr, "%{0:N1}", ozet.AlanOrani * 100);
    }

    public string Sinif { get; }
    public string MahalSayisi { get; }
    public string ToplamAdet { get; }
    public string Alan { get; }
    public string Hacim { get; }
    public string Oran { get; }
}

public sealed class DogrulamaSonucuSatiriViewModel
{
    public DogrulamaSonucuSatiriViewModel(MimariKontrolSonucu sonuc)
        : this(sonuc.Seviye, sonuc.Kod, sonuc.Mesaj, sonuc.CozumOnerisi ?? "", sonuc.Kaynak ?? "")
    {
    }

    public DogrulamaSonucuSatiriViewModel(KuralSonucu sonuc)
        : this(
            sonuc.Seviye,
            sonuc.KuralKodu,
            sonuc.Mesaj,
            sonuc.CozumOnerisi,
            sonuc.KaynakKodu,
            sonuc.VeriEksigi)
    {
    }

    public DogrulamaSonucuSatiriViewModel(
        KontrolSeviyesi seviye,
        string kod,
        string mesaj,
        string cozum,
        string kaynakKodu = "",
        string? veriEksigi = null)
    {
        var kaynak = VarsayilanYonetmelikKaynakProfilleri.Bul(kaynakKodu);
        Seviye = SeviyeYaz(seviye);
        Kod = kod;
        KaynakKodu = string.IsNullOrWhiteSpace(kaynakKodu) ? "-" : kaynakKodu;
        KaynakAdi = kaynak?.Ad ?? "Kaynak profili tanımlı değil";
        Kaynak = $"Kaynak: {KaynakKodu} · {KaynakAdi}";
        Mesaj = mesaj;
        Cozum = cozum;
        CozumVar = !string.IsNullOrWhiteSpace(cozum);
        VeriEksigi = string.IsNullOrWhiteSpace(veriEksigi) ? "" : $"Eksik veri: {veriEksigi}";
        VeriEksigiVar = !string.IsNullOrWhiteSpace(VeriEksigi);
    }

    public string Seviye { get; }
    public string Kod { get; }
    public string KaynakKodu { get; }
    public string KaynakAdi { get; }
    public string Kaynak { get; }
    public string Mesaj { get; }
    public string Cozum { get; }
    public bool CozumVar { get; }
    public string VeriEksigi { get; }
    public bool VeriEksigiVar { get; }

    private static string SeviyeYaz(KontrolSeviyesi seviye)
        => seviye switch
        {
            KontrolSeviyesi.Kritik => "Kritik",
            KontrolSeviyesi.Uyari => "Uyarı",
            KontrolSeviyesi.KontrolEdilemedi => "Kontrol edilemedi",
            _ => "Bilgi"
        };
}
