using System.Linq;
using Avalonia.Controls;
using Kst.Acoustics.App.Electric.ViewModels;
using Kst.Acoustics.Core.Electric.Models;
using Kst.Acoustics.Core.Electric.Calculation;

namespace Kst.Acoustics.App.Electric.Views;

public partial class LinyeDuzenleDialog : Window
{
    private readonly TekHatSemasiViewModel _vm;
    private readonly Linye _linye;

    public LinyeDuzenleDialog() { InitializeComponent(); _vm = null!; _linye = null!; }

    public LinyeDuzenleDialog(TekHatSemasiViewModel vm, Linye linye)
    {
        InitializeComponent();
        _vm = vm;
        _linye = linye;

        BaslikText.Text = $"Linye Düzenle — {linye.LinyeNo}";
        LinyeNoBox.Text = linye.LinyeNo;

        YukTipiCombo.ItemsSource = _vm.YukTipleri;
        YukTipiCombo.DisplayMemberBinding = new Avalonia.Data.Binding("Ad");
        var yukTipi = _vm.YukTipleri.FirstOrDefault(y => y.Ad == linye.YukTipiAd);
        if (yukTipi != null) YukTipiCombo.SelectedItem = yukTipi;
        else if (_vm.YukTipleri.Count > 0) YukTipiCombo.SelectedIndex = 0;

        FazTipiCombo.SelectedIndex = linye.FazTipi == FazTipi.Trifaze ? 1 : 0;
        SortiBox.Text = linye.SortiSayisi.ToString();
        GucBox.Text = linye.BirimGucW.ToString("0.##");
        TalepBox.Text = linye.TalepFaktoru.ToString("0.##");
        KabloMesafeBox.Text = linye.KabloMesafeM.ToString("0.##");
        SigortaCinsiCombo.SelectedIndex = linye.SigortaCinsi switch
        {
            SigortaCinsi.C => 1,
            SigortaCinsi.D => 2,
            _ => 0
        };
        AciklamaBox.Text = linye.Aciklama;
    }

    private void Iptal_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    private void Kaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _linye.LinyeNo = string.IsNullOrWhiteSpace(LinyeNoBox.Text) ? _linye.LinyeNo : LinyeNoBox.Text.Trim();
        if (YukTipiCombo.SelectedItem is YukTipiTanim yt) _linye.YukTipiAd = yt.Ad;
        _linye.FazTipi = FazTipiCombo.SelectedIndex == 1 ? FazTipi.Trifaze : FazTipi.Monofaze;
        if (NumberHelper.TryParseInt(SortiBox.Text, out int sorti)) _linye.SortiSayisi = sorti;
        if (NumberHelper.TryParse(GucBox.Text, out double guc)) _linye.BirimGucW = guc;
        if (NumberHelper.TryParse(TalepBox.Text, out double talep))
        {
            if (talep > 1.0)
            {
                BaslikText.Text = "⚠ Talep faktörü 1'den fazla olamaz!";
                BaslikText.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E0A040"));
                return;
            }
            if (talep <= 0) talep = 1.0;
            _linye.TalepFaktoru = talep;
        }
        if (NumberHelper.TryParse(KabloMesafeBox.Text, out double mesafe)) _linye.KabloMesafeM = mesafe;
        _linye.SigortaCinsi = SigortaCinsiCombo.SelectedIndex switch
        {
            1 => SigortaCinsi.C,
            2 => SigortaCinsi.D,
            _ => SigortaCinsi.B
        };
        _linye.Aciklama = AciklamaBox.Text ?? "";

        _vm.TumunuHesapla();
        Close();
    }
}
