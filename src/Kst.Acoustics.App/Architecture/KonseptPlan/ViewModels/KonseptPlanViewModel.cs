using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Kst.Acoustics.Core.Architecture.Circulation;
using Kst.Acoustics.Core.Architecture.Layout;
using Kst.Acoustics.Core.Architecture.Models;
using Kst.Acoustics.Core.Architecture.Rules;
using Kst.Acoustics.Core.Architecture.Services;
using Kst.Acoustics.Core.Architecture.Templates;
using MimariIhtiyacProgrami = Kst.Acoustics.Core.Architecture.Models.IhtiyacProgrami;

namespace Kst.Acoustics.App.Architecture.KonseptPlan.ViewModels;

public sealed class KonseptPlanViewModel : ObservableObject
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private MimariProje _proje = MimariProjeServisi.YeniOlustur("Yeni Mimari Proje");
    private MimariIhtiyacProgrami _program = new();
    private PlanTaslagi _plan = new();
    private KonseptBinaIsleviSecenekViewModel? _seciliBinaIslevi;
    private KatSecenekViewModel? _seciliKatSecenegi;
    private KatTaslagi? _seciliKat;
    private MahalBlogu? _seciliBlok;
    private string? _seciliDuseyElemanId;
    private string _projeAdi = "Yeni Mimari Proje";
    private string _projeTipi = "Ofis";
    private string _kullaniciKapasitesiText = "60";
    private string _katSayisiText = "2";
    private string _dosyaYoluText = "Kaydedilmedi";
    private string _durumMesaji = "Hazır.";
    private string _katAlaniText = "0,0 m²";
    private string _blokSayisiText = "0";
    private string _koridorAlaniText = "0,0 m²";
    private string _baglantisizBlokText = "0";
    private string _cekirdekOzetText = "0 çekirdek / 0 şaft";
    private string _planOzetiText = "Plan taslağı yok";
    private string _planKontrolOzetiText = "0 uyarı";
    private string _seciliBlokKontrolText = "-";
    private string _seciliDuseyElemanKontrolText = "-";

    public KonseptPlanViewModel()
    {
        BinaIslevleri = new ObservableCollection<KonseptBinaIsleviSecenekViewModel>(
            BinaIsleviTanimlari.Tum.Select(t => new KonseptBinaIsleviSecenekViewModel(t.Islev, t.Ad, t.KapasiteEtiketi)));

        SeciliBinaIslevi = BinaIslevleri.FirstOrDefault(s => s.Islev == BinaIslevi.Ofis)
            ?? BinaIslevleri.FirstOrDefault();

        PlanUret();
    }

    public ObservableCollection<KonseptBinaIsleviSecenekViewModel> BinaIslevleri { get; }
    public ObservableCollection<KatSecenekViewModel> Katlar { get; } = new();
    public ObservableCollection<MahalBlokSatiriViewModel> Bloklar { get; } = new();
    public ObservableCollection<KoridorSatiriViewModel> Koridorlar { get; } = new();
    public ObservableCollection<DuseyElemanSatiriViewModel> DuseyElemanlar { get; } = new();
    public ObservableCollection<PlanKontrolSonucuSatiriViewModel> PlanKontrolSonuclari { get; } = new();
    public string? DosyaYolu => _proje.DosyaYolu;

    public PlanTaslagi Plan
    {
        get => _plan;
        private set => SetProperty(ref _plan, value);
    }

    public KonseptBinaIsleviSecenekViewModel? SeciliBinaIslevi
    {
        get => _seciliBinaIslevi;
        set
        {
            if (!SetProperty(ref _seciliBinaIslevi, value))
                return;

            if (value is not null)
                ProjeTipi = value.Ad;
        }
    }

    public KatSecenekViewModel? SeciliKatSecenegi
    {
        get => _seciliKatSecenegi;
        set
        {
            if (!SetProperty(ref _seciliKatSecenegi, value))
                return;

            foreach (var kat in Katlar)
            {
                kat.Secili = ReferenceEquals(kat, value);
            }

            SeciliKat = value?.Model;
            KatIceriginiYenile();
        }
    }

    public KatTaslagi? SeciliKat
    {
        get => _seciliKat;
        private set => SetProperty(ref _seciliKat, value);
    }

    public MahalBlogu? SeciliBlok
    {
        get => _seciliBlok;
        private set
        {
            if (!SetProperty(ref _seciliBlok, value))
                return;

            foreach (var blok in Bloklar)
            {
                blok.Secili = ReferenceEquals(blok.Model, value);
            }

            SeciliBlokDetaylariDegisti();
        }
    }

    public string? SeciliDuseyElemanId
    {
        get => _seciliDuseyElemanId;
        private set
        {
            if (!SetProperty(ref _seciliDuseyElemanId, value))
                return;

            foreach (var eleman in DuseyElemanlar)
            {
                eleman.Secili = string.Equals(eleman.Id, value, StringComparison.OrdinalIgnoreCase);
            }

            SeciliDuseyElemanDetaylariDegisti();
        }
    }

    public string ProjeTipi
    {
        get => _projeTipi;
        set => SetProperty(ref _projeTipi, value);
    }

    public string ProjeAdi
    {
        get => _projeAdi;
        set => SetProperty(ref _projeAdi, value);
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

    public string DurumMesaji
    {
        get => _durumMesaji;
        set => SetProperty(ref _durumMesaji, value);
    }

    public string DosyaYoluText
    {
        get => _dosyaYoluText;
        private set => SetProperty(ref _dosyaYoluText, value);
    }

    public string KatAlaniText
    {
        get => _katAlaniText;
        private set => SetProperty(ref _katAlaniText, value);
    }

    public string BlokSayisiText
    {
        get => _blokSayisiText;
        private set => SetProperty(ref _blokSayisiText, value);
    }

    public string KoridorAlaniText
    {
        get => _koridorAlaniText;
        private set => SetProperty(ref _koridorAlaniText, value);
    }

    public string BaglantisizBlokText
    {
        get => _baglantisizBlokText;
        private set => SetProperty(ref _baglantisizBlokText, value);
    }

    public string CekirdekOzetText
    {
        get => _cekirdekOzetText;
        private set => SetProperty(ref _cekirdekOzetText, value);
    }

    public string PlanOzetiText
    {
        get => _planOzetiText;
        private set => SetProperty(ref _planOzetiText, value);
    }

    public string PlanKontrolOzetiText
    {
        get => _planKontrolOzetiText;
        private set => SetProperty(ref _planKontrolOzetiText, value);
    }

    public string SeciliBlokKontrolText
    {
        get => _seciliBlokKontrolText;
        private set => SetProperty(ref _seciliBlokKontrolText, value);
    }

    public string SeciliDuseyElemanKontrolText
    {
        get => _seciliDuseyElemanKontrolText;
        private set => SetProperty(ref _seciliDuseyElemanKontrolText, value);
    }

    public bool SeciliBlokVar => SeciliBlok is not null;
    public string SeciliBlokAdi => SeciliBlok?.Ad ?? "Blok seçilmedi";
    public string SeciliBlokTipi => SeciliBlok is null ? "-" : MahalTipiYaz(SeciliBlok.MahalTipi);
    public string SeciliBlokSinifi => SeciliBlok is null ? "-" : KullanimSinifiYaz(SeciliBlok.KullanimSinifi);
    public string SeciliBlokAlanText => SeciliBlok is null ? "-" : AlanYaz(SeciliBlok.AlanM2);
    public string SeciliBlokHedefAlanText => SeciliBlok is null ? "-" : AlanYaz(SeciliBlok.HedefAlanM2);
    public string SeciliBlokAlanFarkiText => SeciliBlok is null ? "-" : AlanYaz(SeciliBlok.AlanFarkiM2);
    public string SeciliBlokBoyutText => SeciliBlok is null ? "-" : $"{SayiYaz(SeciliBlok.GenislikM)} x {SayiYaz(SeciliBlok.DerinlikM)} m";
    public string SeciliBlokKonumText => SeciliBlok is null ? "-" : $"X {SayiYaz(SeciliBlok.X)} / Y {SayiYaz(SeciliBlok.Y)}";
    public string SeciliBlokKoridorText => SeciliBlok is null || SeciliKat is null
        ? "-"
        : KoridorYerlesimServisi.KoridoraBagliMi(SeciliKat, SeciliBlok) ? "Bağlı" : "Bağlantısız";
    public string SeciliBlokKilitText => SeciliBlok is null ? "-" : SeciliBlok.Kilitli ? "Kilitli" : "Açık";
    public string SeciliBlokKilitButonText => SeciliBlok?.Kilitli == true ? "Kilidi Aç" : "Kilitle";
    public bool SeciliDuseyElemanVar => !string.IsNullOrWhiteSpace(SeciliDuseyElemanId);
    public string SeciliDuseyElemanAdi => SeciliDuseyElemanSatiri?.Ad ?? "Çekirdek/şaft seçilmedi";
    public string SeciliDuseyElemanTipi => SeciliDuseyElemanSatiri?.Tip ?? "-";
    public string SeciliDuseyElemanKatAraligi => SeciliDuseyElemanSatiri?.KatAraligi ?? "-";
    public string SeciliDuseyElemanBaglanti => SeciliDuseyElemanSatiri?.Baglanti ?? "-";
    private DuseyElemanSatiriViewModel? SeciliDuseyElemanSatiri =>
        DuseyElemanlar.FirstOrDefault(e => string.Equals(e.Id, SeciliDuseyElemanId, StringComparison.OrdinalIgnoreCase));

    public void PlanUret()
    {
        if (SeciliBinaIslevi is null)
            return;

        var kapasite = PozitifTamsayiOku(KullaniciKapasitesiText, 0, 0);
        var katSayisi = PozitifTamsayiOku(KatSayisiText, 1, 1);
        _program = IhtiyacProgramiUretimServisi.Uret(
            SeciliBinaIslevi.Islev,
            kapasite,
            katSayisi,
            string.IsNullOrWhiteSpace(ProjeTipi) ? SeciliBinaIslevi.Ad : ProjeTipi.Trim());

        Plan = PlanTaslagiUretimServisi.Uret(_program, _proje.Id);
        _proje.IhtiyacProgrami = _program;
        _proje.PlanTaslagi = Plan;
        KatlariYenile();
        SeciliKatSecenegi = Katlar.FirstOrDefault();
        PlanKontrolleriniYenile();
        PlanOzetiYenile();
        DurumMesaji = $"{SeciliBinaIslevi.Ad} için konsept plan üretildi.";
    }

    public void YenidenYerlestir()
    {
        var katNo = SeciliKat?.KatNo ?? 1;
        BasitYerlesimServisi.Yerlestir(Plan);
        KoridorYerlesimServisi.KoridorlariUret(Plan);
        _proje.PlanTaslagi = Plan;

        KatlariYenile();
        SeciliKatSecenegi = Katlar.FirstOrDefault(k => k.Model.KatNo == katNo) ?? Katlar.FirstOrDefault();
        PlanKontrolleriniYenile();
        PlanOzetiYenile();
        DurumMesaji = "Plan taslağı yeniden yerleştirildi.";
    }

    public void KatSec(KatSecenekViewModel kat)
    {
        SeciliKatSecenegi = kat;
        DurumMesaji = $"{kat.Ad} görüntüleniyor.";
    }

    public void BlokSec(MahalBlogu? blok)
    {
        SeciliBlok = blok;
        if (blok is not null)
            SeciliDuseyElemanId = null;

        if (blok is not null)
            DurumMesaji = $"{blok.Ad} seçildi.";
    }

    public void BlokSatiriSec(MahalBlokSatiriViewModel satir) => BlokSec(satir.Model);

    public void DuseyElemanSec(string? elemanId)
    {
        if (string.IsNullOrWhiteSpace(elemanId) || SeciliKat is null)
        {
            SeciliDuseyElemanId = null;
            return;
        }

        var eleman = DuseyElemanlar.FirstOrDefault(e => string.Equals(e.Id, elemanId, StringComparison.OrdinalIgnoreCase));
        if (eleman is null)
            return;

        SeciliBlok = null;
        SeciliDuseyElemanId = eleman.Id;
        DurumMesaji = $"{eleman.Ad} seçildi.";
    }

    public void DuseyElemanSatiriSec(DuseyElemanSatiriViewModel satir) => DuseyElemanSec(satir.Id);

    public void BlokTasindi(MahalBlogu blok, double yeniX, double yeniY)
    {
        if (SeciliKat is null || blok.Kilitli)
            return;

        blok.X = Math.Max(0, Math.Round(yeniX, 2));
        blok.Y = Math.Max(0, Math.Round(yeniY, 2));
        KoridorYerlesimServisi.AnaKoridorUret(SeciliKat, Plan.Parametreler);
        _proje.PlanTaslagi = Plan;
        SatirlariModeldenYenile();
        PlanKontrolleriniYenile();
        DurumMesaji = $"{blok.Ad} taşındı.";
    }

    public void SeciliBlokKilidiniDegistir()
    {
        if (SeciliBlok is null)
            return;

        SeciliBlok.Kilitli = !SeciliBlok.Kilitli;
        _proje.PlanTaslagi = Plan;
        SatirlariModeldenYenile();
        PlanKontrolleriniYenile();
        SeciliBlokDetaylariDegisti();
        DurumMesaji = SeciliBlok.Kilitli ? $"{SeciliBlok.Ad} kilitlendi." : $"{SeciliBlok.Ad} kilidi açıldı.";
    }

    public void KontrolSatiriSec(PlanKontrolSonucuSatiriViewModel satir)
    {
        if (!string.IsNullOrWhiteSpace(satir.EtkilenenMahalId))
        {
            foreach (var kat in Plan.Katlar)
            {
                var blok = kat.BlokBul(satir.EtkilenenMahalId);
                if (blok is null)
                    continue;

                SeciliKatSecenegi = Katlar.FirstOrDefault(k => ReferenceEquals(k.Model, kat)) ?? SeciliKatSecenegi;
                BlokSec(blok);
                DurumMesaji = $"{blok.Ad} uyarısı seçildi.";
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(satir.EtkilenenElemanId))
            return;

        foreach (var kat in Plan.Katlar)
        {
            if (kat.CekirdekBul(satir.EtkilenenElemanId) is null &&
                kat.SaftBul(satir.EtkilenenElemanId) is null)
                continue;

            SeciliKatSecenegi = Katlar.FirstOrDefault(k => ReferenceEquals(k.Model, kat)) ?? SeciliKatSecenegi;
            DuseyElemanSec(satir.EtkilenenElemanId);
            DurumMesaji = "Çekirdek/şaft uyarısı seçildi.";
            return;
        }
    }

    public MimariProje KaydaHazirla()
    {
        _proje.ProjeAdi = string.IsNullOrWhiteSpace(ProjeAdi) ? "Yeni Mimari Proje" : ProjeAdi.Trim();
        if (string.IsNullOrWhiteSpace(Plan.ProjeId))
            Plan.ProjeId = _proje.Id;

        _proje.IhtiyacProgrami = _program;
        _proje.PlanTaslagi = Plan;
        _proje.MahalSablonlari = VarsayilanMahalSablonlari.Tum.ToList();
        return _proje;
    }

    public void KayitTamamlandi(string dosyaYolu)
    {
        _proje.DosyaYolu = dosyaYolu;
        DosyaYoluText = Path.GetFileName(dosyaYolu);
        DurumMesaji = $"Kaydedildi: {Path.GetFileName(dosyaYolu)}";
        OnPropertyChanged(nameof(DosyaYolu));
    }

    public void ProjeYukle(MimariProje proje)
    {
        _proje = proje;
        _program = proje.IhtiyacProgrami ?? new MimariIhtiyacProgrami();

        ProjeAdi = string.IsNullOrWhiteSpace(proje.ProjeAdi) ? "Yeni Mimari Proje" : proje.ProjeAdi;
        SeciliBinaIslevi = BinaIslevleri.FirstOrDefault(s => s.Islev == _program.BinaIslevi)
            ?? BinaIslevleri.FirstOrDefault();
        ProjeTipi = string.IsNullOrWhiteSpace(_program.ProjeTipi)
            ? SeciliBinaIslevi?.Ad ?? ""
            : _program.ProjeTipi;
        KullaniciKapasitesiText = _program.KullaniciKapasitesi.ToString(Tr);
        KatSayisiText = Math.Max(1, _program.KatSayisi).ToString(Tr);

        Plan = proje.PlanTaslagi ?? PlanTaslagiUretimServisi.Uret(_program, proje.Id);
        if (string.IsNullOrWhiteSpace(Plan.ProjeId))
            Plan.ProjeId = proje.Id;

        _proje.PlanTaslagi = Plan;
        KatlariYenile();
        SeciliKatSecenegi = Katlar.FirstOrDefault();
        PlanKontrolleriniYenile();
        PlanOzetiYenile();

        DosyaYoluText = string.IsNullOrWhiteSpace(proje.DosyaYolu)
            ? "Kaydedilmedi"
            : Path.GetFileName(proje.DosyaYolu);
        DurumMesaji = $"Açıldı: {DosyaYoluText}";
        OnPropertyChanged(nameof(DosyaYolu));
    }

    private void KatlariYenile()
    {
        Katlar.Clear();
        foreach (var kat in Plan.Katlar.OrderBy(k => k.KatNo))
        {
            Katlar.Add(new KatSecenekViewModel(kat));
        }
    }

    private void KatIceriginiYenile()
    {
        Bloklar.Clear();
        Koridorlar.Clear();
        DuseyElemanlar.Clear();

        if (SeciliKat is null)
        {
            SeciliBlok = null;
            SeciliDuseyElemanId = null;
            KatOzetiniYenile();
            return;
        }

        foreach (var blok in SeciliKat.Bloklar.OrderByDescending(b => b.HedefAlanM2).ThenBy(b => b.Ad, StringComparer.OrdinalIgnoreCase))
        {
            Bloklar.Add(new MahalBlokSatiriViewModel(blok));
        }

        foreach (var koridor in SeciliKat.Koridorlar)
        {
            Koridorlar.Add(new KoridorSatiriViewModel(koridor));
        }

        foreach (var cekirdek in SeciliKat.Cekirdekler.OrderBy(c => c.Tip).ThenBy(c => c.Ad, StringComparer.OrdinalIgnoreCase))
        {
            DuseyElemanlar.Add(DuseyElemanSatiriViewModel.Cekirdekten(cekirdek));
        }

        foreach (var saft in SeciliKat.Saftlar.OrderBy(s => s.TesisatTuru).ThenBy(s => s.Ad, StringComparer.OrdinalIgnoreCase))
        {
            DuseyElemanlar.Add(DuseyElemanSatiriViewModel.Safttan(saft));
        }

        var secili = SeciliKat.Bloklar.FirstOrDefault(b => string.Equals(b.Id, SeciliBlok?.Id, StringComparison.OrdinalIgnoreCase))
            ?? SeciliKat.Bloklar.OrderByDescending(b => b.HedefAlanM2).FirstOrDefault();
        SeciliBlok = secili;
        SeciliDuseyElemanId = DuseyElemanlar.Any(e => string.Equals(e.Id, SeciliDuseyElemanId, StringComparison.OrdinalIgnoreCase))
            ? SeciliDuseyElemanId
            : null;
        KatOzetiniYenile();
    }

    private void SatirlariModeldenYenile()
    {
        foreach (var satir in Bloklar)
        {
            satir.ModelDegisti();
        }

        Koridorlar.Clear();
        DuseyElemanlar.Clear();
        if (SeciliKat is not null)
        {
            foreach (var koridor in SeciliKat.Koridorlar)
            {
                Koridorlar.Add(new KoridorSatiriViewModel(koridor));
            }

            foreach (var cekirdek in SeciliKat.Cekirdekler.OrderBy(c => c.Tip).ThenBy(c => c.Ad, StringComparer.OrdinalIgnoreCase))
            {
                DuseyElemanlar.Add(DuseyElemanSatiriViewModel.Cekirdekten(cekirdek));
            }

            foreach (var saft in SeciliKat.Saftlar.OrderBy(s => s.TesisatTuru).ThenBy(s => s.Ad, StringComparer.OrdinalIgnoreCase))
            {
                DuseyElemanlar.Add(DuseyElemanSatiriViewModel.Safttan(saft));
            }
        }

        KatOzetiniYenile();
        PlanOzetiYenile();
        SeciliBlokDetaylariDegisti();
        SeciliDuseyElemanDetaylariDegisti();
        OnPropertyChanged(nameof(SeciliKat));
        OnPropertyChanged(nameof(SeciliBlok));
    }

    private void PlanKontrolleriniYenile()
    {
        var sonuclar = YonetmelikOnKontrolServisi.PlanTaslagiKontrolleriniCalistir(
            new KontrolBaglami { PlanTaslagi = Plan });

        var sayilacakSonuclar = sonuclar
            .Where(s => s.Seviye != KontrolSeviyesi.Bilgi)
            .ToList();

        PlanKontrolSonuclari.Clear();
        if (sonuclar.Count == 0)
        {
            PlanKontrolSonuclari.Add(new PlanKontrolSonucuSatiriViewModel(new KuralSonucu
            {
                Seviye = KontrolSeviyesi.Bilgi,
                KuralKodu = "M34-PLAN-ON-KONTROL-TEMIZ",
                KaynakKodu = YonetmelikOnKontrolServisi.VarsayilanVeriKaynakKodu,
                Mesaj = "Plan taslağı ön kontrolünde uyarı bulunmadı.",
                CozumOnerisi = ""
            }));
        }
        else
        {
            foreach (var sonuc in sonuclar)
            {
                PlanKontrolSonuclari.Add(new PlanKontrolSonucuSatiriViewModel(sonuc));
            }
        }

        PlanKontrolOzetiText = sayilacakSonuclar.Count == 0
            ? "0 uyarı"
            : $"{sayilacakSonuclar.Count.ToString(Tr)} uyarı";
        PlanKontrolIliskileriniYenile();
    }

    private void KatOzetiniYenile()
    {
        if (SeciliKat is null)
        {
            KatAlaniText = "0,0 m²";
            BlokSayisiText = "0";
            KoridorAlaniText = "0,0 m²";
            BaglantisizBlokText = "0";
            CekirdekOzetText = "0 çekirdek / 0 şaft";
            return;
        }

        KatAlaniText = AlanYaz(SeciliKat.ToplamTaslakAlanM2);
        BlokSayisiText = SeciliKat.BlokSayisi.ToString(Tr);
        KoridorAlaniText = AlanYaz(SeciliKat.KoridorAlanM2);
        BaglantisizBlokText = KoridorYerlesimServisi.BaglantisizBloklariBul(SeciliKat).Count.ToString(Tr);
        CekirdekOzetText = $"{SeciliKat.CekirdekSayisi.ToString(Tr)} çekirdek / {SeciliKat.SaftSayisi.ToString(Tr)} şaft / {AlanYaz(SeciliKat.DuseySirkulasyonAlanM2)}";

        if (SeciliKatSecenegi is not null)
            SeciliKatSecenegi.ModelDegisti();
    }

    private void PlanOzetiYenile()
    {
        PlanOzetiText = $"{Plan.KatSayisi} kat / {Plan.ToplamBlokSayisi} blok / {Plan.ToplamCekirdekSayisi} çekirdek / {AlanYaz(Plan.ToplamTaslakAlanM2)}";
    }

    private void SeciliBlokDetaylariDegisti()
    {
        OnPropertyChanged(nameof(SeciliBlokVar));
        OnPropertyChanged(nameof(SeciliBlokAdi));
        OnPropertyChanged(nameof(SeciliBlokTipi));
        OnPropertyChanged(nameof(SeciliBlokSinifi));
        OnPropertyChanged(nameof(SeciliBlokAlanText));
        OnPropertyChanged(nameof(SeciliBlokHedefAlanText));
        OnPropertyChanged(nameof(SeciliBlokAlanFarkiText));
        OnPropertyChanged(nameof(SeciliBlokBoyutText));
        OnPropertyChanged(nameof(SeciliBlokKonumText));
        OnPropertyChanged(nameof(SeciliBlokKoridorText));
        OnPropertyChanged(nameof(SeciliBlokKilitText));
        OnPropertyChanged(nameof(SeciliBlokKilitButonText));
        PlanKontrolIliskileriniYenile();
    }

    private void SeciliDuseyElemanDetaylariDegisti()
    {
        OnPropertyChanged(nameof(SeciliDuseyElemanVar));
        OnPropertyChanged(nameof(SeciliDuseyElemanAdi));
        OnPropertyChanged(nameof(SeciliDuseyElemanTipi));
        OnPropertyChanged(nameof(SeciliDuseyElemanKatAraligi));
        OnPropertyChanged(nameof(SeciliDuseyElemanBaglanti));
        PlanKontrolIliskileriniYenile();
    }

    private void PlanKontrolIliskileriniYenile()
    {
        var seciliBlokId = SeciliBlok?.Id ?? "";
        var seciliDuseyElemanId = SeciliDuseyElemanId ?? "";
        var iliskiliUyariSayisi = 0;
        var iliskiliDuseyElemanUyariSayisi = 0;
        foreach (var sonuc in PlanKontrolSonuclari)
        {
            var bloklaIliskili = !string.IsNullOrWhiteSpace(seciliBlokId) &&
                string.Equals(sonuc.EtkilenenMahalId, seciliBlokId, StringComparison.OrdinalIgnoreCase);
            var duseyElemanlaIliskili = !string.IsNullOrWhiteSpace(seciliDuseyElemanId) &&
                string.Equals(sonuc.EtkilenenElemanId, seciliDuseyElemanId, StringComparison.OrdinalIgnoreCase);
            var iliskili = bloklaIliskili || duseyElemanlaIliskili;
            sonuc.SeciliBloklaIliskili = iliskili;
            if (bloklaIliskili && sonuc.Seviye != "Bilgi")
                iliskiliUyariSayisi++;
            if (duseyElemanlaIliskili && sonuc.Seviye != "Bilgi")
                iliskiliDuseyElemanUyariSayisi++;
        }

        SeciliBlokKontrolText = string.IsNullOrWhiteSpace(seciliBlokId)
            ? "-"
            : iliskiliUyariSayisi == 0 ? "Uyarı yok" : $"{iliskiliUyariSayisi.ToString(Tr)} uyarı";
        SeciliDuseyElemanKontrolText = string.IsNullOrWhiteSpace(seciliDuseyElemanId)
            ? "-"
            : iliskiliDuseyElemanUyariSayisi == 0 ? "Uyarı yok" : $"{iliskiliDuseyElemanUyariSayisi.ToString(Tr)} uyarı";
    }

    private static int PozitifTamsayiOku(string deger, int varsayilan, int minimum)
    {
        if (!int.TryParse(deger, NumberStyles.Integer, Tr, out var sonuc))
            sonuc = varsayilan;

        return Math.Max(minimum, sonuc);
    }

    internal static string AlanYaz(double deger) => string.Format(Tr, "{0:N1} m²", deger);
    internal static string SayiYaz(double deger) => string.Format(Tr, "{0:N1}", deger);

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
            MahalTipi.Fuaye => "Fuaye",
            MahalTipi.Depo => "Depo",
            MahalTipi.Otopark => "Otopark",
            _ => tip.ToString()
        };

    internal static string CekirdekTipiYaz(CekirdekTipi tip)
        => tip switch
        {
            CekirdekTipi.Merdiven => "Merdiven",
            CekirdekTipi.YanginMerdiveni => "Yangın merdiveni",
            CekirdekTipi.Asansor => "Asansör",
            CekirdekTipi.Rampa => "Rampa",
            CekirdekTipi.Saft => "Şaft",
            CekirdekTipi.CekirdekGrubu => "Çekirdek grubu",
            _ => "Çekirdek"
        };

    internal static string SaftTesisatTuruYaz(SaftTesisatTuru tur)
        => tur switch
        {
            SaftTesisatTuru.Mekanik => "Mekanik şaft",
            SaftTesisatTuru.Elektrik => "Elektrik şaftı",
            SaftTesisatTuru.SihhiTesisat => "Sıhhi tesisat şaftı",
            SaftTesisatTuru.Yangin => "Yangın tesisatı şaftı",
            SaftTesisatTuru.Havalandirma => "Havalandırma şaftı",
            SaftTesisatTuru.DumanTahliye => "Duman tahliye şaftı",
            SaftTesisatTuru.Ortak => "Ortak şaft",
            _ => "Şaft"
        };
}

public sealed record KonseptBinaIsleviSecenekViewModel(BinaIslevi Islev, string Ad, string KapasiteEtiketi);

public sealed class KatSecenekViewModel : ObservableObject
{
    private bool _secili;

    public KatSecenekViewModel(KatTaslagi model)
    {
        Model = model;
    }

    public KatTaslagi Model { get; }
    public string Ad => Model.Ad;
    public string Ozet => $"{Model.BlokSayisi} blok / {KonseptPlanViewModel.AlanYaz(Model.ToplamTaslakAlanM2)}";

    public bool Secili
    {
        get => _secili;
        set => SetProperty(ref _secili, value);
    }

    public void ModelDegisti() => OnPropertyChanged(nameof(Ozet));
}

public sealed class MahalBlokSatiriViewModel : ObservableObject
{
    private bool _secili;

    public MahalBlokSatiriViewModel(MahalBlogu model)
    {
        Model = model;
    }

    public MahalBlogu Model { get; }
    public string Ad => Model.Ad;
    public string Tip => KonseptPlanViewModel.MahalTipiYaz(Model.MahalTipi);
    public string Sinif => KonseptPlanViewModel.KullanimSinifiYaz(Model.KullanimSinifi);
    public string Alan => KonseptPlanViewModel.AlanYaz(Model.AlanM2);
    public string Konum => $"X {KonseptPlanViewModel.SayiYaz(Model.X)} / Y {KonseptPlanViewModel.SayiYaz(Model.Y)}";
    public string Kilit => Model.Kilitli ? "Kilitli" : "Açık";

    public bool Secili
    {
        get => _secili;
        set => SetProperty(ref _secili, value);
    }

    public void ModelDegisti()
    {
        OnPropertyChanged(nameof(Alan));
        OnPropertyChanged(nameof(Konum));
        OnPropertyChanged(nameof(Kilit));
    }
}

public sealed class KoridorSatiriViewModel
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public KoridorSatiriViewModel(KoridorTaslagi model)
    {
        Ad = model.Ad;
        Uzunluk = $"{KonseptPlanViewModel.SayiYaz(model.UzunlukM)} m";
        Alan = KonseptPlanViewModel.AlanYaz(model.AlanM2);
        BagliBlokSayisi = model.BagliBlokIdleri.Count.ToString(Tr);
    }

    public string Ad { get; }
    public string Uzunluk { get; }
    public string Alan { get; }
    public string BagliBlokSayisi { get; }
}

public sealed class DuseyElemanSatiriViewModel : ObservableObject
{
    private bool _secili;

    private DuseyElemanSatiriViewModel(
        string id,
        string ad,
        string tip,
        string katAraligi,
        string alan,
        string konum,
        string baglanti)
    {
        Id = id;
        Ad = ad;
        Tip = tip;
        KatAraligi = katAraligi;
        Alan = alan;
        Konum = konum;
        Baglanti = baglanti;
    }

    public string Id { get; }
    public string Ad { get; }
    public string Tip { get; }
    public string KatAraligi { get; }
    public string Alan { get; }
    public string Konum { get; }
    public string Baglanti { get; }

    public bool Secili
    {
        get => _secili;
        set => SetProperty(ref _secili, value);
    }

    public static DuseyElemanSatiriViewModel Cekirdekten(CekirdekElemani model)
        => new(
            model.Id,
            string.IsNullOrWhiteSpace(model.Ad) ? KonseptPlanViewModel.CekirdekTipiYaz(model.Tip) : model.Ad,
            KonseptPlanViewModel.CekirdekTipiYaz(model.Tip),
            $"{model.AltKatNo}-{model.UstKatNo}",
            KonseptPlanViewModel.AlanYaz(model.AlanM2),
            $"X {KonseptPlanViewModel.SayiYaz(model.X)} / Y {KonseptPlanViewModel.SayiYaz(model.Y)}",
            $"{model.BagliKoridorIdleri.Count} koridor");

    public static DuseyElemanSatiriViewModel Safttan(SaftTaslagi model)
        => new(
            model.Id,
            string.IsNullOrWhiteSpace(model.Ad) ? KonseptPlanViewModel.SaftTesisatTuruYaz(model.TesisatTuru) : model.Ad,
            KonseptPlanViewModel.SaftTesisatTuruYaz(model.TesisatTuru),
            $"{model.AltKatNo}-{model.UstKatNo}",
            KonseptPlanViewModel.AlanYaz(model.AlanM2),
            $"X {KonseptPlanViewModel.SayiYaz(model.X)} / Y {KonseptPlanViewModel.SayiYaz(model.Y)}",
            $"{model.BagliMahalIdleri.Count} mahal");
}

public sealed class PlanKontrolSonucuSatiriViewModel : ObservableObject
{
    private bool _seciliBloklaIliskili;

    public PlanKontrolSonucuSatiriViewModel(KuralSonucu model)
    {
        Model = model;
        var kaynak = VarsayilanYonetmelikKaynakProfilleri.Bul(model.KaynakKodu);
        Kaynak = string.IsNullOrWhiteSpace(model.KaynakKodu)
            ? "-"
            : $"{model.KaynakKodu} · {kaynak?.Ad ?? "Kaynak profili tanımlı değil"}";
    }

    public KuralSonucu Model { get; }
    public string Seviye => SeviyeYaz(Model.Seviye);
    public string Kod => Model.KuralKodu;
    public string Mesaj => Model.Mesaj;
    public string Cozum => Model.CozumOnerisi;
    public string Kaynak { get; }
    public string EtkilenenMahalId => Model.EtkilenenMahalId ?? "";
    public string EtkilenenElemanId => Model.EtkilenenElemanId ?? "";
    public string Etki => string.IsNullOrWhiteSpace(Model.EtkilenenMahalId)
        ? string.IsNullOrWhiteSpace(Model.EtkilenenElemanId)
            ? string.IsNullOrWhiteSpace(Model.EtkilenenKatId) ? "Plan" : "Kat"
            : "Çekirdek/şaft"
        : "Blok";
    public bool CozumVar => !string.IsNullOrWhiteSpace(Cozum);
    public bool SeciliBlokEtiketiGoster => SeciliBloklaIliskili;

    public bool SeciliBloklaIliskili
    {
        get => _seciliBloklaIliskili;
        set
        {
            if (!SetProperty(ref _seciliBloklaIliskili, value))
                return;

            OnPropertyChanged(nameof(SeciliBlokEtiketiGoster));
        }
    }

    private static string SeviyeYaz(KontrolSeviyesi seviye)
        => seviye switch
        {
            KontrolSeviyesi.Kritik => "Kritik",
            KontrolSeviyesi.Uyari => "Uyarı",
            KontrolSeviyesi.KontrolEdilemedi => "Kontrol edilemedi",
            _ => "Bilgi"
        };
}
