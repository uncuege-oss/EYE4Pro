using Avalonia.Controls;
using Kst.Acoustics.Core.Salon.Models;
using Kst.Acoustics.Core.Salon.Services;

namespace Kst.Acoustics.App.Salon.Views;

/// Hazır salon şablonu sihirbazı. "Oluştur" ile üretilen salon <see cref="OlusturulanSalon"/>'da
/// döner; çağıran pencere onu uygular.
public partial class SalonSablonWindow : Window
{
    public SalonGeometri? OlusturulanSalon { get; private set; }

    public SalonSablonWindow()
    {
        InitializeComponent();
    }

    private void Olustur_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var sablon = new SalonSablonu
        {
            SalonTip = (SalonTipi)(SalonTipCombo?.SelectedIndex ?? 0),
            SalonGenislikM = Parse(SalonGenislikText, 24),
            SalonDerinlikM = Parse(SalonDerinlikText, 30),
            SalonYuksekligiM = Parse(SalonYukText, 10),

            SahneTip = (SahneTipi)(SahneTipCombo?.SelectedIndex ?? 0),
            SahneKonum = (SahneKonum)(SahneKonumCombo?.SelectedIndex ?? 0),
            SahneGenislikM = Parse(SahneGenislikText, 14),
            SahneDerinlikM = Parse(SahneDerinlikText, 8),
            SahneYuksekligiM = Parse(SahneYukText, 1.0),

            SeyirciKademe = (int)System.Math.Round(Parse(SeyirciKademeText, 8)),
            SeyirciKademeYukM = Parse(SeyirciYukText, 0.5)
        };

        OlusturulanSalon = SalonParametrikServisi.Olustur(sablon);
        Close();
    }

    private void Iptal_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => Close();

    private static double Parse(TextBox? tb, double varsayilan)
        => double.TryParse(tb?.Text?.Replace(',', '.'),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double v) && v > 0
            ? v : varsayilan;
}
