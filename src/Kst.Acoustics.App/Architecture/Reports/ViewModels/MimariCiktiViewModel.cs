using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Kst.Acoustics.Core.Architecture.Models;
using Kst.Acoustics.Core.Architecture.Layout;
using Kst.Acoustics.Core.Architecture.Model3D;
using Kst.Acoustics.Core.Architecture.Reports;
using Kst.Acoustics.Core.Architecture.Services;
using Kst.Acoustics.Core.Architecture.Materials;
using Kst.Acoustics.Core.Architecture.Templates;
using MimariIhtiyacProgrami = Kst.Acoustics.Core.Architecture.Models.IhtiyacProgrami;

namespace Kst.Acoustics.App.Architecture.Reports.ViewModels;

/// <summary>
/// M37/M39 çıktı merkezi: yüklü mimari projeden HTML/CSV/XLSX rapor, OBJ ve `.e4bina` üretimi.
/// Pencere yalnız okur ve dışa aktarır; projeyi düzenlemez.
/// </summary>
public sealed class MimariCiktiViewModel : ObservableObject
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private MimariProje _proje = MimariProjeServisi.YeniOlustur("Yeni Mimari Proje");
    private string _projeAdi = "Yeni Mimari Proje";
    private string _dosyaYoluText = "Kaydedilmedi";
    private string _durumMesaji = "Hazır.";
    private string _ozetText = "—";
    private string _onKontrolText = "—";

    public MimariCiktiViewModel()
    {
        OrnekProjeOlustur();
    }

    public MimariProje Proje => _proje;

    public string ProjeAdi
    {
        get => _projeAdi;
        private set => SetProperty(ref _projeAdi, value);
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

    public string OzetText
    {
        get => _ozetText;
        private set => SetProperty(ref _ozetText, value);
    }

    public string OnKontrolText
    {
        get => _onKontrolText;
        private set => SetProperty(ref _onKontrolText, value);
    }

    public void OrnekProjeOlustur()
    {
        var program = IhtiyacProgramiUretimServisi.Uret(BinaIslevi.Ofis, kapasite: 60, katSayisi: 2, projeTipi: "Ofis");
        var proje = MimariProjeServisi.YeniOlustur("Yeni Mimari Proje");
        proje.IhtiyacProgrami = program;
        proje.MahalSablonlari = VarsayilanMahalSablonlari.Tum.ToList();
        proje.Malzemeler = VarsayilanMimariMalzemeler.Tum.ToList();
        proje.YapiElemaniTipleri = VarsayilanYapiElemaniTipleri.Tum.ToList();
        proje.FinisajProfilleri = VarsayilanMahalFinisajProfilleri.Tum.ToList();
        proje.PlanTaslagi = PlanTaslagiUretimServisi.Uret(program, proje.Id);
        Model3DUretimServisi.Uret(proje.PlanTaslagi);

        Yukle(proje, "Örnek mimari proje yüklendi.");
        DosyaYoluText = "Kaydedilmedi";
    }

    public void ProjeYukle(MimariProje proje)
    {
        if (proje.PlanTaslagi is { } plan && (plan.Model3D is null || plan.Model3D.Katlar.Count == 0))
            Model3DUretimServisi.Uret(plan);

        Yukle(proje, $"Açıldı: {(string.IsNullOrWhiteSpace(proje.DosyaYolu) ? "Kaydedilmedi" : Path.GetFileName(proje.DosyaYolu))}");
        DosyaYoluText = string.IsNullOrWhiteSpace(proje.DosyaYolu) ? "Kaydedilmedi" : Path.GetFileName(proje.DosyaYolu);
    }

    private void Yukle(MimariProje proje, string durum)
    {
        _proje = proje;
        ProjeAdi = string.IsNullOrWhiteSpace(proje.ProjeAdi) ? "Yeni Mimari Proje" : proje.ProjeAdi;
        OzetiYenile();
        DurumMesaji = durum;
    }

    public void OzetiYenile()
    {
        var mahalListesi = MimariRaporUretimServisi.MahalListesiOlustur(_proje);
        var alan = MimariRaporUretimServisi.AlanTablosuOlustur(_proje);
        var onKontrol = MimariRaporUretimServisi.OnKontrolRaporuOlustur(_proje);

        OzetText = $"{mahalListesi.SatirSayisi.ToString(Tr)} mahal · "
                 + $"{alan.NetAlanM2.ToString("N1", Tr)} m² net · "
                 + $"{alan.BrutAlanM2.ToString("N1", Tr)} m² brüt";
        OnKontrolText = onKontrol.VeriVar
            ? $"Ön kontrol: {onKontrol.KritikSayisi.ToString(Tr)} kritik · {onKontrol.UyariSayisi.ToString(Tr)} uyarı · {onKontrol.KontrolEdilemediSayisi.ToString(Tr)} kontrol edilemedi"
            : "Ön kontrol: uyarı yok";
    }
}
