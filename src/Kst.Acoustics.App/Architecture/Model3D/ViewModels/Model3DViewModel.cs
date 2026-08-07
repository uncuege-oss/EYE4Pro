using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Kst.Acoustics.Core.Architecture.Circulation;
using Kst.Acoustics.Core.Architecture.Layout;
using Kst.Acoustics.Core.Architecture.Model3D;
using Kst.Acoustics.Core.Architecture.Models;
using Kst.Acoustics.Core.Architecture.Services;
using Kst.Acoustics.Core.Architecture.Templates;
using MimariIhtiyacProgrami = Kst.Acoustics.Core.Architecture.Models.IhtiyacProgrami;

namespace Kst.Acoustics.App.Architecture.Model3D.ViewModels;

public sealed class Model3DViewModel : ObservableObject
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private MimariProje _proje = MimariProjeServisi.YeniOlustur("Yeni Mimari Proje");
    private MimariIhtiyacProgrami _program = new();
    private PlanTaslagi _plan = new();
    private Bim3DModel _model = new();
    private string _projeAdi = "Yeni Mimari Proje";
    private string _dosyaYoluText = "Kaydedilmedi";
    private string _durumMesaji = "Hazır.";
    private string _modelOzetiText = "3D model yok";
    private string _toplamAlanText = "0,0 m²";
    private string _toplamHacimText = "0,0 m³";
    private string _katSayisiText = "0";
    private string _elemanSayisiText = "0";

    public Model3DViewModel()
    {
        OrnekModelOlustur();
    }

    public ObservableCollection<Model3DKatSatiriViewModel> Katlar { get; } = new();
    public ObservableCollection<Model3DElemanOzetSatiriViewModel> ElemanOzetleri { get; } = new();

    public string? DosyaYolu => _proje.DosyaYolu;

    public PlanTaslagi Plan
    {
        get => _plan;
        private set => SetProperty(ref _plan, value);
    }

    public Bim3DModel Model
    {
        get => _model;
        private set => SetProperty(ref _model, value);
    }

    public string ProjeAdi
    {
        get => _projeAdi;
        set => SetProperty(ref _projeAdi, value);
    }

    public string DosyaYoluText
    {
        get => _dosyaYoluText;
        private set => SetProperty(ref _dosyaYoluText, value);
    }

    public string DurumMesaji
    {
        get => _durumMesaji;
        set => SetProperty(ref _durumMesaji, value);
    }

    public string ModelOzetiText
    {
        get => _modelOzetiText;
        private set => SetProperty(ref _modelOzetiText, value);
    }

    public string ToplamAlanText
    {
        get => _toplamAlanText;
        private set => SetProperty(ref _toplamAlanText, value);
    }

    public string ToplamHacimText
    {
        get => _toplamHacimText;
        private set => SetProperty(ref _toplamHacimText, value);
    }

    public string KatSayisiText
    {
        get => _katSayisiText;
        private set => SetProperty(ref _katSayisiText, value);
    }

    public string ElemanSayisiText
    {
        get => _elemanSayisiText;
        private set => SetProperty(ref _elemanSayisiText, value);
    }

    public void OrnekModelOlustur()
    {
        _program = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 60, katSayisi: 2, projeTipi: "Ofis");
        _proje = MimariProjeServisi.YeniOlustur("Yeni Mimari Proje");
        _proje.IhtiyacProgrami = _program;
        _proje.MahalSablonlari = VarsayilanMahalSablonlari.Tum.ToList();
        _plan = PlanTaslagiUretimServisi.Uret(_program, _proje.Id);
        OrnekCekirdekEkle(_plan);
        _proje.PlanTaslagi = _plan;
        ProjeAdi = _proje.ProjeAdi;
        DosyaYoluText = "Kaydedilmedi";
        ModeliYenidenUret("Örnek 3D model üretildi.");
        OnPropertyChanged(nameof(DosyaYolu));
    }

    public void ModeliYenidenUret(string durumMesaji = "3D model yenilendi.")
    {
        if (string.IsNullOrWhiteSpace(Plan.ProjeId))
            Plan.ProjeId = _proje.Id;

        Model = Model3DUretimServisi.Uret(Plan);
        _proje.PlanTaslagi = Plan;
        OzetleriYenile();
        DurumMesaji = durumMesaji;
    }

    public void ProjeYukle(MimariProje proje)
    {
        _proje = proje;
        _program = proje.IhtiyacProgrami ?? new MimariIhtiyacProgrami();
        ProjeAdi = string.IsNullOrWhiteSpace(proje.ProjeAdi) ? "Yeni Mimari Proje" : proje.ProjeAdi;

        Plan = proje.PlanTaslagi ?? PlanTaslagiUretimServisi.Uret(_program, proje.Id);
        if (string.IsNullOrWhiteSpace(Plan.ProjeId))
            Plan.ProjeId = proje.Id;

        if (Plan.Model3D is null || Plan.Model3D.Katlar.Count == 0)
            Model = Model3DUretimServisi.Uret(Plan);
        else
            Model = Plan.Model3D;

        _proje.PlanTaslagi = Plan;
        DosyaYoluText = string.IsNullOrWhiteSpace(proje.DosyaYolu)
            ? "Kaydedilmedi"
            : Path.GetFileName(proje.DosyaYolu);
        OzetleriYenile();
        DurumMesaji = $"Açıldı: {DosyaYoluText}";
        OnPropertyChanged(nameof(DosyaYolu));
    }

    public MimariProje KaydaHazirla()
    {
        _proje.ProjeAdi = string.IsNullOrWhiteSpace(ProjeAdi) ? "Yeni Mimari Proje" : ProjeAdi.Trim();
        if (string.IsNullOrWhiteSpace(Plan.ProjeId))
            Plan.ProjeId = _proje.Id;

        Plan.Model3D = Model;
        _proje.IhtiyacProgrami = _program;
        _proje.PlanTaslagi = Plan;
        _proje.MahalSablonlari = VarsayilanMahalSablonlari.Tum.ToList();
        return _proje;
    }

    public void KayitTamamlandi(string dosyaYolu)
    {
        _proje.DosyaYolu = dosyaYolu;
        DosyaYoluText = Path.GetFileName(dosyaYolu);
        DurumMesaji = $"Kaydedildi: {DosyaYoluText}";
        OnPropertyChanged(nameof(DosyaYolu));
    }

    private void OzetleriYenile()
    {
        Katlar.Clear();
        foreach (var kat in Model.Katlar.OrderBy(k => k.KatNo))
        {
            Katlar.Add(new Model3DKatSatiriViewModel(kat));
        }

        ElemanOzetleri.Clear();
        ElemanOzetleri.Add(new Model3DElemanOzetSatiriViewModel("Mahaller", Model.ToplamMahalSayisi.ToString(Tr), HacimYaz(Model.ToplamMahalHacmiM3)));
        ElemanOzetleri.Add(new Model3DElemanOzetSatiriViewModel("Duvarlar", Model.ToplamDuvarSayisi.ToString(Tr), AlanYaz(Model.ToplamDuvarAlaniM2)));
        ElemanOzetleri.Add(new Model3DElemanOzetSatiriViewModel("Döşemeler", Model.Katlar.Sum(k => k.Dosemeler.Count).ToString(Tr), AlanYaz(Model.ToplamDosemeAlaniM2)));
        ElemanOzetleri.Add(new Model3DElemanOzetSatiriViewModel("Çekirdekler", Model.ToplamCekirdekHacmiSayisi.ToString(Tr), HacimYaz(Model.Katlar.Sum(k => k.ToplamCekirdekHacmiM3))));
        ElemanOzetleri.Add(new Model3DElemanOzetSatiriViewModel("Şaftlar", Model.ToplamSaftHacmiSayisi.ToString(Tr), HacimYaz(Model.Katlar.Sum(k => k.ToplamSaftHacmiM3))));

        KatSayisiText = Model.ToplamKatSayisi.ToString(Tr);
        ElemanSayisiText = (Model.ToplamMahalSayisi + Model.ToplamDuvarSayisi + Model.ToplamCekirdekHacmiSayisi + Model.ToplamSaftHacmiSayisi).ToString(Tr);
        ToplamAlanText = AlanYaz(Model.ToplamDosemeAlaniM2);
        ToplamHacimText = HacimYaz(Model.ToplamMahalHacmiM3);
        ModelOzetiText = $"{Model.ToplamKatSayisi.ToString(Tr)} kat / {Model.ToplamMahalSayisi.ToString(Tr)} mahal / {HacimYaz(Model.ToplamMahalHacmiM3)}";
    }

    private static void OrnekCekirdekEkle(PlanTaslagi plan)
    {
        var kat = plan.KatBul(1);
        if (kat is null || kat.Cekirdekler.Count > 0)
            return;

        var sinirSag = kat.Sinir.X + Math.Max(0, kat.Sinir.GenislikM);
        var x = Math.Max(1, sinirSag - 4.2);
        var y = Math.Max(1, kat.Sinir.Y + 1.2);

        kat.Cekirdekler.Add(new CekirdekElemani
        {
            Id = "ornek-cekirdek-ana",
            Ad = "Ana Merdiven",
            Tip = CekirdekTipi.Merdiven,
            KatNo = 1,
            BaslangicKatNo = 1,
            BitisKatNo = Math.Max(1, plan.KatSayisi),
            X = x,
            Y = y,
            GenislikM = 2.4,
            DerinlikM = 4.2,
            NetGenislikM = 1.5,
            AksKodu = "A-1"
        });

        kat.Saftlar.Add(new SaftTaslagi
        {
            Id = "ornek-saft-ana",
            Ad = "Teknik Şaft",
            TesisatTuru = SaftTesisatTuru.Ortak,
            KatNo = 1,
            BaslangicKatNo = 1,
            BitisKatNo = Math.Max(1, plan.KatSayisi),
            X = x + 2.8,
            Y = y,
            GenislikM = 1.1,
            DerinlikM = 1.4,
            AksKodu = "S-1"
        });
    }

    internal static string AlanYaz(double deger) => string.Format(Tr, "{0:N1} m²", deger);
    internal static string HacimYaz(double deger) => string.Format(Tr, "{0:N1} m³", deger);
    internal static string SayiYaz(double deger) => string.Format(Tr, "{0:N1}", deger);
}

public sealed class Model3DKatSatiriViewModel
{
    public Model3DKatSatiriViewModel(Kat3DModel model)
    {
        Ad = model.Ad;
        Kot = $"{Model3DViewModel.SayiYaz(model.TabanKotuM)} / {Model3DViewModel.SayiYaz(model.UstKotuM)} m";
        Mahaller = model.Mahaller.Count.ToString(CultureInfo.GetCultureInfo("tr-TR"));
        Alan = Model3DViewModel.AlanYaz(model.ToplamDosemeAlaniM2);
        Hacim = Model3DViewModel.HacimYaz(model.ToplamMahalHacmiM3);
    }

    public string Ad { get; }
    public string Kot { get; }
    public string Mahaller { get; }
    public string Alan { get; }
    public string Hacim { get; }
}

public sealed record Model3DElemanOzetSatiriViewModel(string Ad, string Adet, string Deger);
