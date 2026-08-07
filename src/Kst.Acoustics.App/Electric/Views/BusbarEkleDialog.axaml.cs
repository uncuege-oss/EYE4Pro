using System.Linq;
using Avalonia.Controls;
using Kst.Acoustics.App.Electric.ViewModels;
using Kst.Acoustics.Core.Electric.Calculation;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.App.Electric.Views;

public partial class BusbarEkleDialog : Window
{
    private readonly TekHatSemasiViewModel _vm;
    private readonly BusbarHatti? _duzenlenen;

    public BusbarEkleDialog() { InitializeComponent(); _vm = null!; _duzenlenen = null; }

    public BusbarEkleDialog(TekHatSemasiViewModel vm, BusbarHatti? duzenlenen = null)
    {
        InitializeComponent();
        _vm = vm;
        _duzenlenen = duzenlenen;

        // Üst pano olabilecek panolar — Ana veya Tali (busbar oradan da çıkabilir)
        var ustPanoAdlari = _vm.Panolar
            .Where(p => _duzenlenen == null || p.BaglandigiBusbarId != _duzenlenen.Id)
            .Select(p => p.Ad)
            .ToList();
        UstPanoCombo.ItemsSource = ustPanoAdlari;
        if (ustPanoAdlari.Count > 0) UstPanoCombo.SelectedIndex = 0;

        BusbarTipiCombo.ItemsSource = _vm.BusbarTipleri;
        BusbarTipiCombo.DisplayMemberBinding = new Avalonia.Data.Binding("Ad");
        if (_vm.BusbarTipleri.Count > 0) BusbarTipiCombo.SelectedIndex = 0;

        OtomatikTipCheck.IsCheckedChanged += (_, _) => TipComboDurumGuncelle();

        if (_duzenlenen != null)
        {
            Title = "Busbar Hattı Düzenle";
            BaslikText.Text = $"Busbar Hattı Düzenle — {_duzenlenen.Ad}";
            KaydetButton.Content = "Kaydet";
            BusbarAdBox.Text = _duzenlenen.Ad;
            UstPanoCombo.SelectedItem = _duzenlenen.UstPanoAd;
            OtomatikTipCheck.IsChecked = _duzenlenen.OtomatikTipSec;
            BusbarTipiCombo.SelectedItem = _vm.BusbarTipleri.FirstOrDefault(t => t.Ad == _duzenlenen.BusbarTipiAd)
                ?? _vm.BusbarTipleri.FirstOrDefault();
            UzunlukBox.Text = _duzenlenen.ToplamUzunlukM.ToString("0.##");
        }
        else
        {
            BusbarAdBox.Text = $"Busbar-{_vm.Busbarlar.Count + 1}";
        }

        TipComboDurumGuncelle();
    }

    private void Iptal_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    private void Ekle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var ustAd = UstPanoCombo.SelectedItem as string;
        var tip = BusbarTipiCombo.SelectedItem as BusbarTanim;
        if (ustAd == null || tip == null) return;

        var ustPano = _vm.Panolar.FirstOrDefault(p => p.Ad == ustAd);
        if (ustPano == null) return;

        if (!NumberHelper.TryParse(UzunlukBox.Text, out double uzunlukM) || uzunlukM < 0) uzunlukM = 20;
        bool otomatikTip = OtomatikTipCheck.IsChecked ?? true;

        if (_duzenlenen == null)
        {
            var bb = _vm.BusbarEkle(ustPano, tip, uzunlukM, otomatikTip);
            bb.Ad = string.IsNullOrWhiteSpace(BusbarAdBox.Text) ? bb.Ad : BusbarAdBox.Text.Trim();
            _vm.BusbarYenidenDuzenle(bb);
        }
        else
        {
            _vm.BusbarDuzenle(
                _duzenlenen,
                BusbarAdBox.Text ?? "",
                ustPano,
                tip,
                otomatikTip,
                uzunlukM);
        }

        _vm.TumunuHesapla();
        Close();
    }

    private void TipComboDurumGuncelle()
    {
        BusbarTipiCombo.IsEnabled = !(OtomatikTipCheck.IsChecked ?? true);
    }
}
