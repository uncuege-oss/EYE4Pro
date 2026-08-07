using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Kst.Acoustics.App.BuildingAcoustics.Views;

/// <summary>Onay diyaloğu sonucu.</summary>
public enum OnaySonucu
{
    Iptal,
    Tamam,
    /// Açış kapısında "patlatmayı geri al ve aç" seçeneği.
    GeriAlVeAc,
    /// Kaydedilmemiş değişiklik uyarısında "kaydetmeden devam et" seçeneği.
    KaydetmedenDevamEt,
}

/// <summary>
/// Genel onay/uyarı modal'ı. Tamam butonu yapılandırılabilir bir sonuç döndürür
/// (Tamam veya GeriAlVeAc); İptal her zaman Iptal döndürür.
/// </summary>
public partial class OnayDiyaloguWindow : Window
{
    private OnaySonucu _tamamSonuc = OnaySonucu.Tamam;
    private OnaySonucu _ekstraSonuc = OnaySonucu.Iptal;

    public OnayDiyaloguWindow()
    {
        InitializeComponent();
    }

    public OnayDiyaloguWindow(string baslik, string mesaj,
        string tamamYazi = "Tamam", OnaySonucu tamamSonuc = OnaySonucu.Tamam,
        string iptalYazi = "İptal",
        string? ekstraYazi = null, OnaySonucu ekstraSonuc = OnaySonucu.Iptal) : this()
    {
        Title = baslik;
        BaslikLabel.Text = baslik;
        MesajLabel.Text = mesaj;
        TamamBtn.Content = tamamYazi;
        IptalBtn.Content = iptalYazi;
        _tamamSonuc = tamamSonuc;

        if (!string.IsNullOrEmpty(ekstraYazi))
        {
            EkstraBtn.Content = ekstraYazi;
            EkstraBtn.IsVisible = true;
            _ekstraSonuc = ekstraSonuc;
        }
    }

    private void Tamam_OnClick(object? sender, RoutedEventArgs e) => Close(_tamamSonuc);

    private void Ekstra_OnClick(object? sender, RoutedEventArgs e) => Close(_ekstraSonuc);

    private void Iptal_OnClick(object? sender, RoutedEventArgs e) => Close(OnaySonucu.Iptal);
}
