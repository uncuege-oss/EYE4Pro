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
        if (!PozitifOku(SalonGenislikText, "Salon genişliği", out double salonGen)
            || !PozitifOku(SalonDerinlikText, "Salon derinliği", out double salonDer)
            || !PozitifOku(SalonYukText, "Salon yüksekliği", out double salonYuk)
            || !PozitifOku(SahneGenislikText, "Sahne genişliği", out double sahneGen)
            || !PozitifOku(SahneDerinlikText, "Sahne derinliği", out double sahneDer)
            || !PozitifOku(SahneYukText, "Sahne yüksekliği", out double sahneYuk)
            || !PozitifTamSayiOku(SeyirciKademeText, "Seyirci kademe sayısı", out int kademe)
            || !PozitifOku(SeyirciYukText, "Kademe yüksekliği", out double kademeYuk))
            return;

        var sablon = new SalonSablonu
        {
            SalonTip = (SalonTipi)(SalonTipCombo?.SelectedIndex ?? 0),
            SalonGenislikM = salonGen,
            SalonDerinlikM = salonDer,
            SalonYuksekligiM = salonYuk,

            SahneTip = (SahneTipi)(SahneTipCombo?.SelectedIndex ?? 0),
            SahneKonum = (SahneKonum)(SahneKonumCombo?.SelectedIndex ?? 0),
            SahneGenislikM = sahneGen,
            SahneDerinlikM = sahneDer,
            SahneYuksekligiM = sahneYuk,

            SeyirciKademe = kademe,
            SeyirciKademeYukM = kademeYuk
        };

        OlusturulanSalon = SalonParametrikServisi.Olustur(sablon);
        Close();
    }

    private void Iptal_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => Close();

    private bool PozitifOku(TextBox? tb, string alan, out double deger)
    {
        if (double.TryParse(tb?.Text?.Replace(',', '.'),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out deger)
            && double.IsFinite(deger) && deger > 0)
            return true;
        HataGoster($"{alan} için sıfırdan büyük geçerli bir sayı girin.", tb);
        return false;
    }

    private bool PozitifTamSayiOku(TextBox? tb, string alan, out int deger)
    {
        deger = 0;
        if (!PozitifOku(tb, alan, out double ham)) return false;
        if (ham <= 10_000 && System.Math.Abs(ham - System.Math.Round(ham)) < 1e-9)
        {
            deger = (int)System.Math.Round(ham);
            return true;
        }
        HataGoster($"{alan} için 1–10000 arasında tam sayı girin.", tb);
        return false;
    }

    private void HataGoster(string mesaj, TextBox? alan)
    {
        if (HataText is { } hata) { hata.Text = mesaj; hata.IsVisible = true; }
        alan?.Focus();
    }
}
