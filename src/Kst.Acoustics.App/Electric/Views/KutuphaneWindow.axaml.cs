using System.Linq;
using Avalonia.Controls;
using Kst.Acoustics.App.Electric.ViewModels;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.App.Electric.Views;

public partial class KutuphaneWindow : Window
{
    /// <summary>Busbar malzeme seçenekleri — kütüphane gridindeki combo için.</summary>
    public static System.Collections.Generic.IReadOnlyList<string> BusbarMalzemeleri { get; } =
        new[] { "Bakır", "Alüminyum" };

    /// <summary>Trafo tipi seçenekleri (combo).</summary>
    public static System.Collections.Generic.IReadOnlyList<TrafoTipi> TrafoTipDegerleri { get; } =
        new[] { TrafoTipi.Yagli, TrafoTipi.Hermetik, TrafoTipi.KuruTip };

    /// <summary>OG hücre tipi seçenekleri (combo).</summary>
    public static System.Collections.Generic.IReadOnlyList<OgHucreTipi> OgHucreTipDegerleri { get; } =
        new[] { OgHucreTipi.Giris, OgHucreTipi.Olcu, OgHucreTipi.BaraBaglama, OgHucreTipi.Cikis, OgHucreTipi.TrafoKoruma };

    /// <summary>OG hücre koruma tipi seçenekleri (combo).</summary>
    public static System.Collections.Generic.IReadOnlyList<OgHucreKorumaTipi> OgKorumaTipDegerleri { get; } =
        new[] { OgHucreKorumaTipi.YukAyirici, OgHucreKorumaTipi.YukAyiriciSigortali, OgHucreKorumaTipi.Kesicili };

    private readonly TekHatSemasiViewModel _vm;

    public KutuphaneWindow() { InitializeComponent(); _vm = null!; }

    public KutuphaneWindow(TekHatSemasiViewModel vm)
    {
        InitializeComponent();
        _vm = vm;

        YukGrid.ItemsSource = _vm.YukTipleri;
        KarGrid.ItemsSource = _vm.KarTipleri;
        AkimTrafosuGrid.ItemsSource = _vm.AkimTrafosuTipleri;
        KondGrid.ItemsSource = _vm.KondansatorTipleri;
        BusbarGrid.ItemsSource = _vm.BusbarTipleri;
        AzrGrid.ItemsSource = _vm.AzrTipleri;
        KontGrid.ItemsSource = _vm.KontaktorTipleri;
        SigortaGrid.ItemsSource = _vm.SigortaTipleri;
        TmsGrid.ItemsSource = _vm.TmsTipleri;
        BicakGrid.ItemsSource = _vm.BicakliSigortaTipleri;
        KompRoleGrid.ItemsSource = _vm.KompanzasyonRolesiTipleri;
        ReaktorGrid.ItemsSource = _vm.ReaktorTipleri;
        SvcGrid.ItemsSource = _vm.SvcSurucuTipleri;
        HarmonikGrid.ItemsSource = _vm.HarmonikFiltreTipleri;
        MotorGrid.ItemsSource = _vm.MotorTipleri;
        AlternatorGrid.ItemsSource = _vm.AlternatorTipleri;
        JeneratorGrid.ItemsSource = _vm.JeneratorTipleri;
        TransferSalterGrid.ItemsSource = _vm.TransferSalterTipleri;
        TrafoGrid.ItemsSource = _vm.TrafoTipleri;
        OgHucreGrid.ItemsSource = _vm.OgHucreTipleri;

        // Pencere Kaydet'e basılmadan kapatılsa da düzenlemeler diske yazılır; aksi halde
        // oturum boyunca görünen yeni kayıtlar program kapanınca kayboluyordu.
        Closing += (_, _) =>
        {
            foreach (var grid in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(this)
                         .OfType<DataGrid>())
                grid.CommitEdit();
            _vm.KutuphaneyiKaydet();
        };
    }

    // === Yük Tipleri ===
    private void YukYeni_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var yeni = new YukTipiTanim { Ad = "Yeni Yük", BirimGucW = 100, SigortaCinsi = "B", TalepFaktoru = 1.0, VarsayilanFazTipi = FazTipi.Monofaze };
        _vm.Kutuphane.YukTipleri.Add(yeni);
        _vm.YukTipleri.Add(yeni);
    }
    private void YukSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (YukGrid.SelectedItem is YukTipiTanim sel)
        {
            _vm.Kutuphane.YukTipleri.Remove(sel);
            _vm.YukTipleri.Remove(sel);
        }
    }

    // === KAR ===
    private void KarYeni_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var yeni = new KacakAkimRolesiTanim { Ad = "Yeni KAR", SensitiviteMa = 30, AnmaAkimA = 40, KutupSayisi = 4, Tip = "AC" };
        _vm.Kutuphane.KacakAkimRoleleri.Add(yeni);
        _vm.KarTipleri.Add(yeni);
    }
    private void KarSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (KarGrid.SelectedItem is KacakAkimRolesiTanim sel)
        {
            _vm.Kutuphane.KacakAkimRoleleri.Remove(sel);
            _vm.KarTipleri.Remove(sel);
        }
    }

    // === Akım Trafosu ===
    private void AkimTrafosuYeni_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var yeni = new AkimTrafosuTanim
        {
            Ad = "Yeni Toroid AT",
            Tip = "Toroid",
            PrimerA = 250,
            SekonderA = 5,
            IcCapMm = 70,
            Sinif = "RCD",
            GerilimV = 400
        };
        _vm.Kutuphane.AkimTrafolari.Add(yeni);
        _vm.AkimTrafosuTipleri.Add(yeni);
    }

    private void AkimTrafosuSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (AkimTrafosuGrid.SelectedItem is AkimTrafosuTanim sel)
        {
            _vm.Kutuphane.AkimTrafolari.Remove(sel);
            _vm.AkimTrafosuTipleri.Remove(sel);
        }
    }

    // === Kondansatör ===
    private void KondYeni_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var yeni = new KondansatorTanim { Ad = "Yeni Kondansatör", GucKVar = 10, FazTipi = "Trifaze", GerilimV = 400 };
        _vm.Kutuphane.Kondansatorler.Add(yeni);
        _vm.KondansatorTipleri.Add(yeni);
    }
    private void KondSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (KondGrid.SelectedItem is KondansatorTanim sel)
        {
            _vm.Kutuphane.Kondansatorler.Remove(sel);
            _vm.KondansatorTipleri.Remove(sel);
        }
    }

    // === Busbar ===
    private void BusbarYeni_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var yeni = new BusbarTanim { Ad = "Yeni Busbar", NominalAkimA = 250, Malzeme = "Bakır", KesitMm2 = "95", DirencMOhmPerM = 0.21, ReaktansMOhmPerM = 0.09 };
        _vm.Kutuphane.BusbarTipleri.Add(yeni);
        _vm.BusbarTipleri.Add(yeni);
    }
    private void BusbarSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (BusbarGrid.SelectedItem is BusbarTanim sel)
        {
            _vm.Kutuphane.BusbarTipleri.Remove(sel);
            _vm.BusbarTipleri.Remove(sel);
        }
    }

    // === AZR ===
    private void AzrYeni_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var yeni = new AstronomikZamanRolesiTanim { Ad = "Yeni AZR", AnmaAkimA = 16, CikisSayisi = 1, KutupSayisi = 1, BeslemeGerilimV = "230" };
        _vm.Kutuphane.AstronomikZamanRoleleri.Add(yeni);
        _vm.AzrTipleri.Add(yeni);
    }
    private void AzrSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (AzrGrid.SelectedItem is AstronomikZamanRolesiTanim sel)
        {
            _vm.Kutuphane.AstronomikZamanRoleleri.Remove(sel);
            _vm.AzrTipleri.Remove(sel);
        }
    }

    // === Kontaktör ===
    private void KontYeni_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var yeni = new KontaktorTanim { Ad = "Yeni Kontaktör", AnmaAkimA = 25, KutupSayisi = 4, BobinGerilimV = 230 };
        _vm.Kutuphane.Kontaktorler.Add(yeni);
        _vm.KontaktorTipleri.Add(yeni);
    }
    private void KontSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (KontGrid.SelectedItem is KontaktorTanim sel)
        {
            _vm.Kutuphane.Kontaktorler.Remove(sel);
            _vm.KontaktorTipleri.Remove(sel);
        }
    }

    // === Sigorta (MCB) ===
    private void SigortaYeni_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var yeni = new SigortaTanim { Ad = "Yeni Sigorta", AnmaAkimA = 16, Karakteristik = "C", KesmeKapasitesiKA = 6, KutupSayisi = 1 };
        _vm.Kutuphane.Sigortalar.Add(yeni);
        _vm.SigortaTipleri.Add(yeni);
    }
    private void SigortaSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (SigortaGrid.SelectedItem is SigortaTanim sel)
        {
            _vm.Kutuphane.Sigortalar.Remove(sel);
            _vm.SigortaTipleri.Remove(sel);
        }
    }

    // === TMŞ (Kompakt Şalter / MCCB) ===
    private void TmsYeni_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var yeni = new TmsTanim { Ad = "Yeni TMŞ", AnmaAkimA = 100, KesmeKapasitesiKA = 36, KutupSayisi = 3 };
        _vm.Kutuphane.Tmsler.Add(yeni);
        _vm.TmsTipleri.Add(yeni);
    }
    private void TmsSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (TmsGrid.SelectedItem is TmsTanim sel)
        {
            _vm.Kutuphane.Tmsler.Remove(sel);
            _vm.TmsTipleri.Remove(sel);
        }
    }

    // === Bıçaklı Sigorta ===
    private void BicakYeni_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var yeni = new BicakliSigortaTanim { Ad = "Yeni NH Sigorta", Boy = "NH00", AnmaAkimA = 100, Karakteristik = "gG", KesmeKapasitesiKA = 120, GerilimV = 500 };
        _vm.Kutuphane.BicakliSigortalar.Add(yeni);
        _vm.BicakliSigortaTipleri.Add(yeni);
    }
    private void BicakSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (BicakGrid.SelectedItem is BicakliSigortaTanim sel)
        {
            _vm.Kutuphane.BicakliSigortalar.Remove(sel);
            _vm.BicakliSigortaTipleri.Remove(sel);
        }
    }

    // === Kompanzasyon Rölesi ===
    private void KompRoleYeni_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var yeni = new KompanzasyonRolesiTanim { Ad = "Yeni Komp Rölesi", KademeSayisi = 12, SvcDestekli = false, CtOrani = "200/5", IletisimArayuzu = "RS-485" };
        _vm.Kutuphane.KompanzasyonRoleleri.Add(yeni);
        _vm.KompanzasyonRolesiTipleri.Add(yeni);
    }
    private void KompRoleSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (KompRoleGrid.SelectedItem is KompanzasyonRolesiTanim sel)
        {
            _vm.Kutuphane.KompanzasyonRoleleri.Remove(sel);
            _vm.KompanzasyonRolesiTipleri.Remove(sel);
        }
    }

    // === Reaktör ===
    private void ReaktorYeni_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var yeni = new ReaktorTanim { Ad = "Yeni Reaktör", GucKVar = 25, AkortFrekansHz = 189, AkortYuzde = 5.67, GerilimV = 400, FazTipi = "Trifaze" };
        _vm.Kutuphane.Reaktorler.Add(yeni);
        _vm.ReaktorTipleri.Add(yeni);
    }
    private void ReaktorSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ReaktorGrid.SelectedItem is ReaktorTanim sel)
        {
            _vm.Kutuphane.Reaktorler.Remove(sel);
            _vm.ReaktorTipleri.Remove(sel);
        }
    }

    // === SVC Sürücü ===
    private void SvcYeni_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var yeni = new SvcSurucuTanim { Ad = "Yeni SVC Sürücü", KademeGucKVar = 25, AnmaAkimA = 60, GerilimV = 400, SogutmaTipi = "Doğal" };
        _vm.Kutuphane.SvcSuruculer.Add(yeni);
        _vm.SvcSurucuTipleri.Add(yeni);
    }
    private void SvcSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (SvcGrid.SelectedItem is SvcSurucuTanim sel)
        {
            _vm.Kutuphane.SvcSuruculer.Remove(sel);
            _vm.SvcSurucuTipleri.Remove(sel);
        }
    }

    // === Harmonik Filtre ===
    private void HarmonikYeni_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var yeni = new HarmonikFiltreTanim { Ad = "Yeni Filtre", Tipi = "Pasif", FiltreFrekansHz = 250, GucKVar = 50, AnmaAkimA = 80, ThdYuzde = 5.0, GerilimV = 400 };
        _vm.Kutuphane.HarmonikFiltreler.Add(yeni);
        _vm.HarmonikFiltreTipleri.Add(yeni);
    }
    private void HarmonikSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (HarmonikGrid.SelectedItem is HarmonikFiltreTanim sel)
        {
            _vm.Kutuphane.HarmonikFiltreler.Remove(sel);
            _vm.HarmonikFiltreTipleri.Remove(sel);
        }
    }

    // === Motor ===
    private void MotorYeni_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var yeni = new MotorTanim { Ad = "Yeni Motor", Marka = "—", Model = "—", YakitTipi = "Dizel", SilindirSayisi = 6, HacimL = 6.0, DevirRpm = 1500, GucKW = 100, Sogutma = "Su" };
        _vm.Kutuphane.Motorlar.Add(yeni);
        _vm.MotorTipleri.Add(yeni);
    }
    private void MotorSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (MotorGrid.SelectedItem is MotorTanim sel)
        {
            _vm.Kutuphane.Motorlar.Remove(sel);
            _vm.MotorTipleri.Remove(sel);
        }
    }

    // === Alternatör ===
    private void AlternatorYeni_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var yeni = new AlternatorTanim { Ad = "Yeni Alternatör", Marka = "—", Model = "—", AnmaGucKVA = 100, GerilimV = 400, FazTipi = "Trifaze", CosF = 0.8, FrekansHz = 50, VerimYuzde = 92 };
        _vm.Kutuphane.Alternatorler.Add(yeni);
        _vm.AlternatorTipleri.Add(yeni);
    }
    private void AlternatorSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (AlternatorGrid.SelectedItem is AlternatorTanim sel)
        {
            _vm.Kutuphane.Alternatorler.Remove(sel);
            _vm.AlternatorTipleri.Remove(sel);
        }
    }

    // === Jeneratör ===
    private void JeneratorYeni_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var yeni = new JeneratorTanim { Ad = "Yeni Jeneratör", Marka = "—", Model = "—", PrimeGucKVA = 100, PrimeGucKW = 80, StandbyGucKVA = 110, StandbyGucKW = 88, GerilimV = 400, FrekansHz = 50, Kabin = "Sessiz" };
        _vm.Kutuphane.Jeneratorler.Add(yeni);
        _vm.JeneratorTipleri.Add(yeni);
    }
    private void JeneratorSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (JeneratorGrid.SelectedItem is JeneratorTanim sel)
        {
            _vm.Kutuphane.Jeneratorler.Remove(sel);
            _vm.JeneratorTipleri.Remove(sel);
        }
    }

    // === Transfer Şalteri ===
    private void TransferSalterYeni_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var yeni = new TransferSalterTanim { Ad = "Yeni ATS", Marka = "—", Model = "—", AnmaAkimA = 250, KutupSayisi = 4, GerilimV = 400, Tipi = "ATS", TransferSuresiMs = 120 };
        _vm.Kutuphane.TransferSalterleri.Add(yeni);
        _vm.TransferSalterTipleri.Add(yeni);
    }
    private void TransferSalterSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (TransferSalterGrid.SelectedItem is TransferSalterTanim sel)
        {
            _vm.Kutuphane.TransferSalterleri.Remove(sel);
            _vm.TransferSalterTipleri.Remove(sel);
        }
    }

    // === Trafo ===
    private void TrafoYeni_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var yeni = new TrafoTanim { Ad = "Yeni Trafo", Marka = "—", Tip = TrafoTipi.Yagli, GucKVA = 630, PrimerGerilimKV = 34.5, SekonderGerilimV = 400, UkYuzde = 4, VektorGrubu = "Dyn11", SogutmaTipi = "ONAN", GenislikMm = 1600, DerinlikMm = 1150, YukseklikMm = 1650 };
        _vm.Kutuphane.Trafolar.Add(yeni);
        _vm.TrafoTipleri.Add(yeni);
    }
    private void TrafoSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (TrafoGrid.SelectedItem is TrafoTanim sel)
        {
            _vm.Kutuphane.Trafolar.Remove(sel);
            _vm.TrafoTipleri.Remove(sel);
        }
    }

    // === OG Hücre ===
    private void OgHucreYeni_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var yeni = new OgHucreTanim { Ad = "Yeni Hücre", HucreTipi = OgHucreTipi.Cikis, KorumaTipi = OgHucreKorumaTipi.YukAyirici, AnmaGerilimKV = 36, AnmaAkimA = 630, KisaSureDayanimKA = 16, TepeDayanimKA = 40, YalitimTipi = "SF6", GenislikMm = 500, DerinlikMm = 1000, YukseklikMm = 1800 };
        _vm.Kutuphane.OgHucreleri.Add(yeni);
        _vm.OgHucreTipleri.Add(yeni);
    }
    private void OgHucreSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (OgHucreGrid.SelectedItem is OgHucreTanim sel)
        {
            _vm.Kutuphane.OgHucreleri.Remove(sel);
            _vm.OgHucreTipleri.Remove(sel);
        }
    }

    // === Pencere ===
    private void KutuphaneyiKaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.KutuphaneyiKaydet();
        StatusText.Text = "✓ Kütüphane kaydedildi.";
    }

    private void Kapat_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}
