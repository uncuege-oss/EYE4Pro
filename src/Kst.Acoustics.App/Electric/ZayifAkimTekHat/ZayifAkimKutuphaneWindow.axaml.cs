using Avalonia.Controls;
using Kst.Acoustics.Core.Electric.ZayifAkimTekHat;

namespace Kst.Acoustics.App.Electric.ZayifAkimTekHat;

public partial class ZayifAkimKutuphaneWindow : Window
{
    public ZayifAkimKutuphaneViewModel ViewModel { get; }

    public ZayifAkimKutuphaneWindow() : this(new ZayifAkimTekHatProje(), "Data")
    {
    }

    public ZayifAkimKutuphaneWindow(ZayifAkimTekHatProje proje, string baslangicKodu)
    {
        InitializeComponent();
        ViewModel = new ZayifAkimKutuphaneViewModel(proje, baslangicKodu);
        DataContext = ViewModel;
    }

    private void Kapat_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}
