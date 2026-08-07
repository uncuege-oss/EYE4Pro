using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using Kst.Acoustics.Core.Electric.Models;
using Kst.Acoustics.Core.Electric.Calculation;

namespace Kst.Acoustics.App.Electric.ViewModels;

public partial class TekHatSemasiViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? prop = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

    private readonly ElektrikKutuphaneServisi _kutuphane;

    /// <summary>Kütüphane servisine dışarıdan (kütüphane penceresi için) erişim.</summary>
    public ElektrikKutuphaneServisi Kutuphane => _kutuphane;

    public ElektrikProje Proje { get; private set; }

    /// <summary>Aktif proje dosyası yolu (null ise henüz kaydedilmemiş).</summary>
    public string? ProjeDosyaYolu { get; set; }

    // Pano ağacı
    public ObservableCollection<Pano> Panolar { get; } = new();

    private Pano? _seciliPano;
    public Pano? SeciliPano
    {
        get => _seciliPano;
        set
        {
            _seciliPano = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PanoOzet));
            OnPropertyChanged(nameof(KompanzasyonSonucu));
            OnPropertyChanged(nameof(KompanzasyonGorunur));
        }
    }

    // Kütüphane listeleri
    public ObservableCollection<YukTipiTanim> YukTipleri { get; }
    public ObservableCollection<KacakAkimRolesiTanim> KarTipleri { get; }
    public ObservableCollection<AkimTrafosuTanim> AkimTrafosuTipleri { get; }
    public ObservableCollection<KondansatorTanim> KondansatorTipleri { get; }
    public ObservableCollection<BusbarTanim> BusbarTipleri { get; }
    public ObservableCollection<AstronomikZamanRolesiTanim> AzrTipleri { get; }
    public ObservableCollection<KontaktorTanim> KontaktorTipleri { get; }
    public ObservableCollection<SigortaTanim> SigortaTipleri { get; }
    public ObservableCollection<TmsTanim> TmsTipleri { get; }
    public ObservableCollection<BicakliSigortaTanim> BicakliSigortaTipleri { get; }
    public ObservableCollection<KompanzasyonRolesiTanim> KompanzasyonRolesiTipleri { get; }
    public ObservableCollection<ReaktorTanim> ReaktorTipleri { get; }
    public ObservableCollection<SvcSurucuTanim> SvcSurucuTipleri { get; }
    public ObservableCollection<HarmonikFiltreTanim> HarmonikFiltreTipleri { get; }
    public ObservableCollection<MotorTanim> MotorTipleri { get; }
    public ObservableCollection<AlternatorTanim> AlternatorTipleri { get; }
    public ObservableCollection<JeneratorTanim> JeneratorTipleri { get; }
    public ObservableCollection<TransferSalterTanim> TransferSalterTipleri { get; }
    public ObservableCollection<UpsTanim> UpsTipleri { get; }
    public ObservableCollection<AkuTanim> AkuTipleri { get; }
    public ObservableCollection<TrafoTanim> TrafoTipleri { get; }
    public ObservableCollection<OgHucreTanim> OgHucreTipleri { get; }

    // Tüm busbar hatları (proje seviyesi)
    public ObservableCollection<BusbarHatti> Busbarlar { get; } = new();

    /// <summary>Koruma cihazı tipi seçenekleri — linye ve çıkış sigortası "Cihaz" açılır menüleri için.</summary>
    public string[] CihazTipleri { get; } = { "Sigorta", "TMŞ" };

    // Yeni linye girişi
    private string _yeniLinyeNo = "";
    public string YeniLinyeNo { get => _yeniLinyeNo; set { _yeniLinyeNo = value; OnPropertyChanged(); } }

    private YukTipiTanim? _yeniYukTipi;
    public YukTipiTanim? YeniYukTipi
    {
        get => _yeniYukTipi;
        set
        {
            _yeniYukTipi = value;
            if (value != null)
                YeniBirimGuc = value.BirimGucW;
            OnPropertyChanged();
        }
    }

    private int _yeniSortiSayisi = 1;
    public int YeniSortiSayisi { get => _yeniSortiSayisi; set { _yeniSortiSayisi = value; OnPropertyChanged(); } }

    private double _yeniBirimGuc = 300;
    public double YeniBirimGuc { get => _yeniBirimGuc; set { _yeniBirimGuc = value; OnPropertyChanged(); } }

    private double _yeniTalepFaktoru = 1.0;
    public double YeniTalepFaktoru { get => _yeniTalepFaktoru; set { _yeniTalepFaktoru = value; OnPropertyChanged(); } }

    private string _yeniAciklama = "";
    public string YeniAciklama { get => _yeniAciklama; set { _yeniAciklama = value; OnPropertyChanged(); } }

    private FazTipi _yeniFazTipi = FazTipi.Monofaze;
    public FazTipi YeniFazTipi { get => _yeniFazTipi; set { _yeniFazTipi = value; OnPropertyChanged(); } }

    // Pano özet
    public string PanoOzet
    {
        get
        {
            if (SeciliPano == null) return "";
            var p = SeciliPano;
            if (p.PanoTipi == PanoTipi.KontrolPanosu)
                return $"Kontrol Butonu: {p.KontrolButonlari.Count} | " +
                       $"Sinyal Kablosu: {p.KontrolSinyalKablolari.Count} hat | " +
                       $"Damar: {p.KontrolSinyalKablolari.Sum(k => k.DamarSayisi)} | " +
                       $"Metraj: {p.KontrolSinyalKablolari.Sum(k => k.MetrajM):N1} m";
            return $"Toplam: {p.ToplamGucW:N0} W | " +
                   $"I: {p.GirisAkimA:N1} A | " +
                   $"Giriş Sigorta: {p.GirisSigortaA}A | " +
                   $"KAR: {PanoKarOzet(p)} | " +
                   $"Modül: {p.ModulSayisi} | " +
                   $"Kablo: {p.KabloKesiti} {p.KabloTipi}";
        }
    }

    private static string PanoKarOzet(Pano pano)
    {
        if (pano.KacakAkimRolesi == null) return "-";
        if (!pano.KacakAkimRolesi.TroidTipiMi) return pano.KacakAkimRolesi.Ad;
        return pano.KacakAkimAkimTrafosu == null
            ? $"{pano.KacakAkimRolesi.Ad} + AT yok"
            : $"{pano.KacakAkimRolesi.Ad} + {pano.KacakAkimAkimTrafosu.PrimerA}/{pano.KacakAkimAkimTrafosu.SekonderA}A";
    }

    // Faz dağılımı
    public string FazDagilimOzet
    {
        get
        {
            if (SeciliPano?.FazDagilimi == null) return "";
            var f = SeciliPano.FazDagilimi;
            return $"R: {f.R_W:N0}W ({f.R_A:N1}A)  |  " +
                   $"S: {f.S_W:N0}W ({f.S_A:N1}A)  |  " +
                   $"T: {f.T_W:N0}W ({f.T_A:N1}A)  |  " +
                   $"Dengesizlik: %{f.DengesizlikYuzde:N1}";
        }
    }

    // Kompanzasyon
    public KompanzasyonSonucu? KompanzasyonSonucu => Proje.Kompanzasyon;
    public bool KompanzasyonGorunur =>
        Proje.Kompanzasyon != null && Proje.Kompanzasyon.KondansatorKademeleri.Count > 0;

    public TekHatSemasiViewModel()
    {
        _kutuphane = new ElektrikKutuphaneServisi();
        Proje = new ElektrikProje();

        YukTipleri = new ObservableCollection<YukTipiTanim>(_kutuphane.YukTipleri);
        KarTipleri = new ObservableCollection<KacakAkimRolesiTanim>(_kutuphane.KacakAkimRoleleri);
        AkimTrafosuTipleri = new ObservableCollection<AkimTrafosuTanim>(_kutuphane.AkimTrafolari);
        KondansatorTipleri = new ObservableCollection<KondansatorTanim>(_kutuphane.Kondansatorler);
        BusbarTipleri = new ObservableCollection<BusbarTanim>(_kutuphane.BusbarTipleri);
        AzrTipleri = new ObservableCollection<AstronomikZamanRolesiTanim>(_kutuphane.AstronomikZamanRoleleri);
        KontaktorTipleri = new ObservableCollection<KontaktorTanim>(_kutuphane.Kontaktorler);
        SigortaTipleri = new ObservableCollection<SigortaTanim>(_kutuphane.Sigortalar);
        TmsTipleri = new ObservableCollection<TmsTanim>(_kutuphane.Tmsler);
        BicakliSigortaTipleri = new ObservableCollection<BicakliSigortaTanim>(_kutuphane.BicakliSigortalar);
        KompanzasyonRolesiTipleri = new ObservableCollection<KompanzasyonRolesiTanim>(_kutuphane.KompanzasyonRoleleri);
        ReaktorTipleri = new ObservableCollection<ReaktorTanim>(_kutuphane.Reaktorler);
        SvcSurucuTipleri = new ObservableCollection<SvcSurucuTanim>(_kutuphane.SvcSuruculer);
        HarmonikFiltreTipleri = new ObservableCollection<HarmonikFiltreTanim>(_kutuphane.HarmonikFiltreler);
        MotorTipleri = new ObservableCollection<MotorTanim>(_kutuphane.Motorlar);
        AlternatorTipleri = new ObservableCollection<AlternatorTanim>(_kutuphane.Alternatorler);
        JeneratorTipleri = new ObservableCollection<JeneratorTanim>(_kutuphane.Jeneratorler);
        TransferSalterTipleri = new ObservableCollection<TransferSalterTanim>(_kutuphane.TransferSalterleri);
        UpsTipleri = new ObservableCollection<UpsTanim>(_kutuphane.Upsler);
        AkuTipleri = new ObservableCollection<AkuTanim>(_kutuphane.Akuler);
        TrafoTipleri = new ObservableCollection<TrafoTanim>(_kutuphane.Trafolar);
        OgHucreTipleri = new ObservableCollection<OgHucreTanim>(_kutuphane.OgHucreleri);

        if (YukTipleri.Count > 0)
            YeniYukTipi = YukTipleri[0];
    }

    /// <summary>Kütüphane değişikliklerini disk'e yaz ve UI listelerini yenile.</summary>
    public void KutuphaneyiKaydet()
    {
        _kutuphane.Kaydet();
        SenkronizeEt(YukTipleri, _kutuphane.YukTipleri);
        SenkronizeEt(KarTipleri, _kutuphane.KacakAkimRoleleri);
        SenkronizeEt(AkimTrafosuTipleri, _kutuphane.AkimTrafolari);
        SenkronizeEt(KondansatorTipleri, _kutuphane.Kondansatorler);
        SenkronizeEt(BusbarTipleri, _kutuphane.BusbarTipleri);
        SenkronizeEt(AzrTipleri, _kutuphane.AstronomikZamanRoleleri);
        SenkronizeEt(KontaktorTipleri, _kutuphane.Kontaktorler);
        SenkronizeEt(SigortaTipleri, _kutuphane.Sigortalar);
        SenkronizeEt(TmsTipleri, _kutuphane.Tmsler);
        SenkronizeEt(BicakliSigortaTipleri, _kutuphane.BicakliSigortalar);
        SenkronizeEt(KompanzasyonRolesiTipleri, _kutuphane.KompanzasyonRoleleri);
        SenkronizeEt(ReaktorTipleri, _kutuphane.Reaktorler);
        SenkronizeEt(SvcSurucuTipleri, _kutuphane.SvcSuruculer);
        SenkronizeEt(HarmonikFiltreTipleri, _kutuphane.HarmonikFiltreler);
        SenkronizeEt(MotorTipleri, _kutuphane.Motorlar);
        SenkronizeEt(AlternatorTipleri, _kutuphane.Alternatorler);
        SenkronizeEt(JeneratorTipleri, _kutuphane.Jeneratorler);
        SenkronizeEt(TransferSalterTipleri, _kutuphane.TransferSalterleri);
        SenkronizeEt(UpsTipleri, _kutuphane.Upsler);
        SenkronizeEt(AkuTipleri, _kutuphane.Akuler);
        SenkronizeEt(TrafoTipleri, _kutuphane.Trafolar);
        SenkronizeEt(OgHucreTipleri, _kutuphane.OgHucreleri);
    }

    private static void SenkronizeEt<T>(ObservableCollection<T> hedef, List<T> kaynak)
    {
        hedef.Clear();
        foreach (var item in kaynak) hedef.Add(item);
    }

    public static string LinyePrefixBul(string? yukTipiAd)
    {
        var ad = yukTipiAd?.Trim() ?? "";
        if (ad.Contains("Aydınlatma", StringComparison.OrdinalIgnoreCase)) return "A";
        if (ad.Contains("Priz", StringComparison.OrdinalIgnoreCase)) return "P";
        if (ad.Contains("Motor", StringComparison.OrdinalIgnoreCase) ||
            ad.Contains("Mekanik", StringComparison.OrdinalIgnoreCase)) return "M";
        if (ad.Contains("UPS", StringComparison.OrdinalIgnoreCase) ||
            ad.Contains("KGK", StringComparison.OrdinalIgnoreCase)) return "U";
        if (ad.Contains("Jeneratör", StringComparison.OrdinalIgnoreCase)) return "J";

        var ilkHarf = ad.FirstOrDefault(char.IsLetter);
        return ilkHarf == default
            ? "L"
            : ilkHarf.ToString().ToUpper(new CultureInfo("tr-TR"));
    }

    public static string SiradakiLinyeNoBul(Pano pano, string? yukTipiAd)
    {
        var prefix = LinyePrefixBul(yukTipiAd);
        int mevcut = pano.Linyeler.Count(l => LinyePrefixBul(l.YukTipiAd) == prefix);
        return $"{prefix}{mevcut + 1}";
    }

    public static void LinyeleriYenidenNumaralandir(Pano pano)
    {
        var sayaclar = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var linye in pano.Linyeler)
        {
            if (string.IsNullOrWhiteSpace(linye.Id))
                linye.Id = Guid.NewGuid().ToString();
            var prefix = LinyePrefixBul(linye.YukTipiAd);
            sayaclar.TryGetValue(prefix, out int sira);
            sira++;
            sayaclar[prefix] = sira;
            linye.LinyeNo = $"{prefix}{sira}";
        }
    }

    public IEnumerable<Pano> KontrolKaynakPanolari(string? kat, Pano kontrolPanosu)
    {
        return Proje.TumPanolar
            .Where(p => p != kontrolPanosu
                        && p.PanoTipi != PanoTipi.KontrolPanosu
                        && (string.IsNullOrWhiteSpace(kat) || string.Equals(p.Kat, kat, StringComparison.OrdinalIgnoreCase))
                        && KontrolIcinUygunDarbeLinyeleri(p, kontrolPanosu).Any())
            .OrderBy(p => p.Kat)
            .ThenBy(p => p.Ad);
    }

    public IEnumerable<Linye> KontrolIcinUygunDarbeLinyeleri(Pano kaynakPano, Pano kontrolPanosu)
    {
        return kaynakPano.Linyeler
            .Where(l => l.DarbeKesiciMi
                        && !string.IsNullOrWhiteSpace(l.Id)
                        && !kontrolPanosu.KontrolButonlari.Any(a => a.LinyeId == l.Id)
                        && !LinyeKontrolAtanmisMi(l.Id, kontrolPanosu))
            .OrderBy(l => l.LinyeNo);
    }

    public bool LinyeKontrolAtanmisMi(string linyeId, Pano? haricKontrolPanosu = null)
    {
        if (string.IsNullOrWhiteSpace(linyeId)) return false;
        return Proje.TumPanolar
            .Where(p => p.PanoTipi == PanoTipi.KontrolPanosu && p != haricKontrolPanosu)
            .Any(p => p.KontrolButonlari.Any(a => a.LinyeId == linyeId));
    }

    public KontrolButonuAtama? KontrolButonuEkle(Pano kontrolPanosu, Pano kaynakPano, Linye linye)
    {
        if (kontrolPanosu.PanoTipi != PanoTipi.KontrolPanosu) return null;
        if (!linye.DarbeKesiciMi) return null;
        if (string.IsNullOrWhiteSpace(linye.Id))
            linye.Id = Guid.NewGuid().ToString();
        if (kontrolPanosu.KontrolButonlari.Any(a => a.LinyeId == linye.Id)) return null;
        if (LinyeKontrolAtanmisMi(linye.Id, kontrolPanosu)) return null;

        var atama = new KontrolButonuAtama
        {
            KaynakPanoAd = kaynakPano.Ad,
            LinyeId = linye.Id,
            LinyeNo = linye.LinyeNo,
            LinyeAciklama = linye.Aciklama,
            SigortaAmper = linye.SigortaAmper
        };

        kontrolPanosu.KontrolButonlari.Add(atama);
        KontrolSinyalKablolariniGuncelle(kontrolPanosu);
        TumunuHesapla();
        return atama;
    }

    public int KontrolButonlarininHepsiniEkle(Pano kontrolPanosu, Pano kaynakPano)
    {
        if (kontrolPanosu.PanoTipi != PanoTipi.KontrolPanosu) return 0;
        var linyeler = KontrolIcinUygunDarbeLinyeleri(kaynakPano, kontrolPanosu).ToList();
        foreach (var linye in linyeler)
        {
            kontrolPanosu.KontrolButonlari.Add(new KontrolButonuAtama
            {
                KaynakPanoAd = kaynakPano.Ad,
                LinyeId = linye.Id,
                LinyeNo = linye.LinyeNo,
                LinyeAciklama = linye.Aciklama,
                SigortaAmper = linye.SigortaAmper
            });
        }

        if (linyeler.Count == 0) return 0;
        KontrolSinyalKablolariniGuncelle(kontrolPanosu);
        TumunuHesapla();
        return linyeler.Count;
    }

    public void KontrolButonuSil(Pano kontrolPanosu, KontrolButonuAtama atama)
    {
        if (kontrolPanosu.KontrolButonlari.Remove(atama))
        {
            KontrolSinyalKablolariniGuncelle(kontrolPanosu);
            TumunuHesapla();
        }
    }

    public void KontrolSinyalKablolariniGuncelle(Pano kontrolPanosu)
    {
        if (kontrolPanosu.PanoTipi != PanoTipi.KontrolPanosu) return;

        var eskiKablolar = kontrolPanosu.KontrolSinyalKablolari
            .GroupBy(k => k.KaynakPanoAd, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var yeniKablolar = kontrolPanosu.KontrolButonlari
            .Where(a => !string.IsNullOrWhiteSpace(a.KaynakPanoAd))
            .GroupBy(a => a.KaynakPanoAd, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                eskiKablolar.TryGetValue(g.Key, out var eski);
                return new KontrolSinyalKablosu
                {
                    KaynakPanoAd = g.Key,
                    DamarSayisi = g.Count(),
                    KesitMm2 = eski?.KesitMm2 > 0 ? eski.KesitMm2 : 1.5,
                    MetrajM = eski?.MetrajM ?? 0,
                    KabloTipi = string.IsNullOrWhiteSpace(eski?.KabloTipi) ? "Sinyal" : eski.KabloTipi
                };
            })
            .ToList();

        kontrolPanosu.KontrolSinyalKablolari.Clear();
        foreach (var kablo in yeniKablolar)
            kontrolPanosu.KontrolSinyalKablolari.Add(kablo);
    }

    private void KontrolButonSnapshotlariniGuncelle()
    {
        foreach (var kontrol in Proje.TumPanolar.Where(p => p.PanoTipi == PanoTipi.KontrolPanosu))
        {
            foreach (var atama in kontrol.KontrolButonlari.ToList())
            {
                var kaynak = Proje.TumPanolar.FirstOrDefault(p =>
                    string.Equals(p.Ad, atama.KaynakPanoAd, StringComparison.OrdinalIgnoreCase));
                var linye = kaynak?.Linyeler.FirstOrDefault(l => l.Id == atama.LinyeId);
                if (kaynak == null || linye == null || !linye.DarbeKesiciMi)
                {
                    kontrol.KontrolButonlari.Remove(atama);
                    continue;
                }

                atama.LinyeNo = linye.LinyeNo;
                atama.LinyeAciklama = linye.Aciklama;
                atama.SigortaAmper = linye.SigortaAmper;
            }
            KontrolSinyalKablolariniGuncelle(kontrol);
        }
    }

    private void KontrolReferanslariniPanoIcinSil(string panoAd)
    {
        foreach (var kontrol in Proje.TumPanolar.Where(p => p.PanoTipi == PanoTipi.KontrolPanosu))
        {
            kontrol.KontrolButonlari.RemoveAll(a =>
                string.Equals(a.KaynakPanoAd, panoAd, StringComparison.OrdinalIgnoreCase));
            KontrolSinyalKablolariniGuncelle(kontrol);
        }
    }

    private void KontrolReferanslariniLinyeIcinSil(string linyeId)
    {
        if (string.IsNullOrWhiteSpace(linyeId)) return;
        foreach (var kontrol in Proje.TumPanolar.Where(p => p.PanoTipi == PanoTipi.KontrolPanosu))
        {
            kontrol.KontrolButonlari.RemoveAll(a => a.LinyeId == linyeId);
            KontrolSinyalKablolariniGuncelle(kontrol);
        }
    }

    public void TumunuHesapla()
    {
        foreach (var linye in Proje.TumPanolar.SelectMany(p => p.Linyeler))
            if (string.IsNullOrWhiteSpace(linye.Id))
                linye.Id = Guid.NewGuid().ToString();

        // Alt panolardan üste doğru hesapla (dip-first)
        foreach (var pano in Proje.TumPanolar.OrderBy(p => DerinlikBul(p)).Reverse())
        {
            PanoHesapServisi.PanoHesapla(pano, Proje, _kutuphane);
            KabloHesapServisi.PanoKabloHesapla(pano, Proje);
        }

        // Busbar hesapları (her panonun çıkan busbar hatları)
        foreach (var bb in Proje.TumBusbarlar)
            BusbarHesapServisi.Hesapla(bb, Proje, _kutuphane);

        // Orta gerilim (OG) — trafo seçimi + akım + bağlantı; kaynak empedansını (Zq+Zt) belirler.
        // Kısa devreden ÖNCE çalışmalı: KisaDevreHesapServisi kaynağı OG trafosundan türetir.
        if (Proje.OrtaGerilim != null && Proje.KokPano != null)
            OrtaGerilimHesapServisi.Hesapla(Proje.OrtaGerilim, Proje.KokPano, Proje, _kutuphane);

        // Kompanzasyon (ana pano) — mevcut sonucu koru (kullanıcı override etmiş olabilir).
        // Kullanıcı elle kompanzasyon panosu eklediyse tetik gücü altında da hesap sonucunu
        // tut; aksi halde kademe/reaktör seçimleri anında tekrar 6 varsayılana düşer.
        // Kısa devreden ÖNCE çalışır: kompanzasyon panosunun bank akımı/kablosu burada kesinleşir,
        // böylece KisaDevreHesapServisi panonun gerçek kablosuyla Ik hesaplar (aksi halde ilk
        // geçişteki varsayılan 2,5 mm² kabloyla bulunurdu).
        bool kompanzasyonPanosuVar = Proje.TumPanolar.Any(p => p.PanoTipi == PanoTipi.Kompanzasyon);
        bool kompanzasyonGerekli = Proje.KokPano != null &&
            KompanzasyonHesapServisi.KompanzasyonGerekliMi(Proje.KokPano, Proje.KompanzasyonTetikKW);
        bool kompanzasyonElleAyarli = Proje.Kompanzasyon?.KademelerElleAyarlandi == true;

        if (Proje.KokPano != null && (kompanzasyonGerekli || kompanzasyonPanosuVar || kompanzasyonElleAyarli))
        {
            Proje.Kompanzasyon = KompanzasyonHesapServisi.Hesapla(
                Proje.KokPano, Proje, _kutuphane, Proje.Kompanzasyon);
        }
        else if (Proje.Kompanzasyon != null && !Proje.Kompanzasyon.KademelerElleAyarlandi)
        {
            // Tetik altındaysa ve kullanıcı elle ayarlamadıysa kompanzasyonu kaldır
            Proje.Kompanzasyon = null;
        }

        // Proje-seviyesi kompanzasyon hesabı sonrası kompanzasyon panolarını boyutlandır:
        // yük bağlı olmadığından akım/sigorta/kablo aksi halde minimum çıkar — kondansatör bankı
        // kVAr'ından gerçek bank akımı türetilir, sonra otomatik bileşenler ve kablo güncellenir.
        foreach (var pano in Proje.TumPanolar.Where(p => p.PanoTipi == PanoTipi.Kompanzasyon))
        {
            PanoHesapServisi.KompanzasyonPanoBoyutlandir(pano, Proje, _kutuphane);
            if (!pano.KompanzasyonElleAyarlandi)
                PanoHesapServisi.KompanzasyonOtomatikSec(pano, Proje, _kutuphane);
            KabloHesapServisi.PanoKabloHesapla(pano, Proje);
        }

        // Kısa devre (Ik3) — kaynaktan aşağı; kesme kapasiteleri bara Ik'sına göre güncellenir.
        // Tüm kablo kesitleri (kompanzasyon panosu dahil) hazır olduğundan empedans zinciri tam.
        KisaDevreHesapServisi.Hesapla(Proje, _kutuphane);

        // Jeneratör/senkron pano hesabı (varsa)
        if (Proje.JeneratorGruplari.Count > 0)
            JeneratorHesapServisi.HesaplaTumu(Proje, _kutuphane);
        else if (Proje.Jenerator != null && Proje.KokPano != null)
            JeneratorHesapServisi.Hesapla(Proje.Jenerator, Proje.KokPano, Proje, _kutuphane);

        // UPS hesapları (varsa) — her UPS'in kritik panosu üzerinden
        UpsHesapServisi.HesaplaTumu(Proje, _kutuphane);

        // Kümülatif gerilim düşümü kontrolü — tüm segment ΔU'ları (kablo + busbar + OG) kesinleştikten
        // sonra kaynaktan uca birikimli düşümü hesaplar ve toplam sınırı aşan pano/linyeleri işaretler.
        GerilimDususKontrolServisi.Hesapla(Proje);

        // Kontrol panolarındaki buton listeleri linye açıklaması/sigorta değişikliklerini izler.
        KontrolButonSnapshotlariniGuncelle();

        // UI güncelle
        OnPropertyChanged(nameof(PanoOzet));
        OnPropertyChanged(nameof(FazDagilimOzet));
        OnPropertyChanged(nameof(KompanzasyonSonucu));
        OnPropertyChanged(nameof(KompanzasyonGorunur));
        OnPropertyChanged(nameof(AktifJenerator));
        OnPropertyChanged(nameof(AktifJeneratorGrubu));
        OnPropertyChanged(nameof(JeneratorVar));
    }

    /// <summary>
    /// Kaydetmeden önce serileştirilecek modeli bellekteki gerçek ağaçla tutarlı hale getirir:
    /// her panonun UstPanoAd'ı gerçek üst pano nesnesinden türetilir, eksik panolar TumPanolar'a
    /// eklenir ve KokPano bağlanır. Aksi halde UstPanoAd set etmeyen bir işlemden sonra kaydedip
    /// açınca bağlantılar kopabilir.
    /// </summary>
    public void KaydetmeyeHazirla()
    {
        foreach (var p in Panolar)
            if (!Proje.TumPanolar.Contains(p))
                Proje.TumPanolar.Add(p);

        foreach (var p in Proje.TumPanolar)
            p.UstPanoAd = p.UstPano?.Ad;

        if (Proje.KokPano == null || !Proje.TumPanolar.Contains(Proje.KokPano))
            Proje.KokPano = Proje.TumPanolar.FirstOrDefault(p => p.UstPano == null && p.PanoTipi == PanoTipi.AnaPano)
                            ?? Proje.TumPanolar.FirstOrDefault(p => p.UstPano == null);

        Proje.Jenerator = Proje.JeneratorGruplari.FirstOrDefault()?.Jeneratorler.FirstOrDefault() ?? Proje.Jenerator;
        KontrolButonSnapshotlariniGuncelle();
    }

    /// <summary>Projeyi tamamen değiştirir — yükleme sonrası kullanılır.</summary>
    public void ProjeyiYukle(ElektrikProje yeniProje)
    {
        Proje = yeniProje;
        Panolar.Clear();
        foreach (var p in yeniProje.TumPanolar) Panolar.Add(p);
        Busbarlar.Clear();
        foreach (var b in yeniProje.TumBusbarlar) Busbarlar.Add(b);

        // 1) UstPano referansları: önce JSON'daki UstPanoAd'tan, yoksa busbar yoluyla
        foreach (var p in yeniProje.TumPanolar)
        {
            p.UstPano = null; // sıfırla
            if (!string.IsNullOrEmpty(p.UstPanoAd))
            {
                p.UstPano = yeniProje.TumPanolar.FirstOrDefault(x => x.Ad == p.UstPanoAd && x != p);
            }
            else if (!string.IsNullOrEmpty(p.BaglandigiBusbarId))
            {
                var bb = yeniProje.TumBusbarlar.FirstOrDefault(b => b.Id == p.BaglandigiBusbarId);
                if (bb != null)
                    p.UstPano = yeniProje.TumPanolar.FirstOrDefault(x => x.Ad == bb.UstPanoAd);
                if (p.UstPano != null) p.UstPanoAd = p.UstPano.Ad;
            }
        }

        // 2) AltPanolar listesini UstPano'dan yeniden kur
        foreach (var p in yeniProje.TumPanolar) p.AltPanolar.Clear();
        foreach (var p in yeniProje.TumPanolar)
            if (p.UstPano != null && !p.UstPano.AltPanolar.Contains(p))
                p.UstPano.AltPanolar.Add(p);

        // 3) Busbar.CikanBusbarlar referanslarını ilgili üst panolara yeniden bağla
        foreach (var pano in yeniProje.TumPanolar) pano.CikanBusbarlar.Clear();
        foreach (var bb in yeniProje.TumBusbarlar)
        {
            var ustP = yeniProje.TumPanolar.FirstOrDefault(p => p.Ad == bb.UstPanoAd);
            if (ustP != null && !ustP.CikanBusbarlar.Contains(bb))
                ustP.CikanBusbarlar.Add(bb);
            BusbarYenidenDuzenle(bb);
        }

        // 4) KokPano kimliğini TumPanolar'daki örnekle eşle. JSON (IgnoreCycles) referans
        //    kimliğini korumaz; aksi halde KokPano ayrı bir nesne kalır ve TumunuHesapla
        //    onu güncellemez (kompanzasyon tetiği yanlış okur).
        yeniProje.KokPano =
            yeniProje.TumPanolar.FirstOrDefault(p => p.Ad == yeniProje.KokPano?.Ad)
            ?? yeniProje.TumPanolar.FirstOrDefault(p => p.PanoTipi == PanoTipi.AnaPano)
            ?? yeniProje.KokPano;

        // 5) Jeneratör: eski tek jeneratör alanını yeni senkron panolu grup modeline taşı
        JeneratorGruplariniNormalizeEt(yeniProje);

        // 6) OG sistemi: ana pano referansı kayıp/tutarsızsa kök panoya çek
        if (yeniProje.OrtaGerilim != null)
        {
            bool gecerli = yeniProje.TumPanolar.Any(p => p.Ad == yeniProje.OrtaGerilim.AnaPanoAd);
            if (!gecerli && yeniProje.KokPano != null)
                yeniProje.OrtaGerilim.AnaPanoAd = yeniProje.KokPano.Ad;
        }

        SeciliPano = Panolar.FirstOrDefault();
        TumunuHesapla();
    }

    private void JeneratorGruplariniNormalizeEt(ElektrikProje proje)
    {
        if (proje.JeneratorGruplari.Count == 0 && proje.Jenerator != null)
        {
            var eski = proje.Jenerator;
            proje.JeneratorGruplari.Add(new JeneratorGrubu
            {
                AnaPanoAd = string.IsNullOrWhiteSpace(eski.AnaPanoAd)
                    ? proje.KokPano?.Ad ?? ""
                    : eski.AnaPanoAd,
                SenkronPanoAd = string.IsNullOrWhiteSpace(eski.TransferPanoAd)
                    ? $"Senkron-{proje.KokPano?.Ad ?? "Ana Pano"}"
                    : eski.TransferPanoAd,
                TransferSalterAd = eski.TransferSalterAd,
                TransferSalterManuel = eski.TransferSalterManuel,
                Mod = eski.Mod,
                EmniyetFaktoru = eski.EmniyetFaktoru <= 0 ? 1.25 : eski.EmniyetFaktoru,
                SenkronHedefPanoKabloMesafeM = Math.Max(0, eski.SenkronHedefPanoKabloMesafeM),
                Jeneratorler = new List<Jenerator> { eski }
            });
        }

        foreach (var grup in proje.JeneratorGruplari)
        {
            if (string.IsNullOrWhiteSpace(grup.AnaPanoAd) && proje.KokPano != null)
                grup.AnaPanoAd = proje.KokPano.Ad;
            if (string.IsNullOrWhiteSpace(grup.SenkronPanoAd))
                grup.SenkronPanoAd = $"Senkron-{grup.AnaPanoAd}";
            grup.SenkronHedefPanoKabloMesafeM = Math.Max(0, grup.SenkronHedefPanoKabloMesafeM);

            var ana = proje.TumPanolar.FirstOrDefault(p => p.Ad == grup.AnaPanoAd)
                ?? proje.KokPano
                ?? proje.TumPanolar.FirstOrDefault(p => p.PanoTipi == PanoTipi.AnaPano);
            if (ana != null)
                grup.AnaPanoAd = ana.Ad;

            var senkron = proje.TumPanolar.FirstOrDefault(p => p.Ad == grup.SenkronPanoAd);
            if (senkron == null)
            {
                senkron = new Pano
                {
                    Ad = grup.SenkronPanoAd,
                    Kat = ana?.Kat ?? "Z",
                    PanoTipi = PanoTipi.TransferPanosu
                };
                proje.TumPanolar.Add(senkron);
                Panolar.Add(senkron);
            }
            else
            {
                senkron.PanoTipi = PanoTipi.TransferPanosu;
                if (string.IsNullOrWhiteSpace(senkron.Kat) && ana != null)
                    senkron.Kat = ana.Kat;
            }

            foreach (var jen in grup.Jeneratorler)
            {
                jen.AnaPanoAd = grup.AnaPanoAd;
                jen.TransferPanoAd = grup.SenkronPanoAd;
                jen.Mod = grup.Mod;
                jen.EmniyetFaktoru = grup.EmniyetFaktoru;
                jen.SenkronPanoKabloMesafeM = Math.Max(0, jen.SenkronPanoKabloMesafeM);
                if (string.IsNullOrWhiteSpace(jen.TransferSalterAd))
                    jen.TransferSalterAd = grup.TransferSalterAd;
            }
        }

        proje.Jenerator = proje.JeneratorGruplari.FirstOrDefault()?.Jeneratorler.FirstOrDefault() ?? proje.Jenerator;
    }

    private int DerinlikBul(Pano pano)
    {
        int d = 0;
        var p = pano;
        while (p.UstPano != null) { d++; p = p.UstPano; }
        return d;
    }

    // Kütüphane yönetimi
    public void YukTipiEkle(YukTipiTanim tanim)
    {
        _kutuphane.YukTipleri.Add(tanim);
        YukTipleri.Add(tanim);
        _kutuphane.Kaydet();
    }

    public void YukTipiSil(YukTipiTanim tanim)
    {
        _kutuphane.YukTipleri.Remove(tanim);
        YukTipleri.Remove(tanim);
        _kutuphane.Kaydet();
    }

    public void KarEkle(KacakAkimRolesiTanim tanim)
    {
        _kutuphane.KacakAkimRoleleri.Add(tanim);
        KarTipleri.Add(tanim);
        _kutuphane.Kaydet();
    }

    public void KondansatorEkle(KondansatorTanim tanim)
    {
        _kutuphane.Kondansatorler.Add(tanim);
        KondansatorTipleri.Add(tanim);
        _kutuphane.Kaydet();
    }

}
