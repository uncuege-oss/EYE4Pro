using Avalonia.Controls;
using Avalonia.Interactivity;
using Kst.Acoustics.Core.Electric.KesifHazirla;

namespace Kst.Acoustics.App.Electric.KesifHazirla;

public partial class OzelPozlarWindow : Window
{
    private KesifHazirlaViewModel ViewModel => (KesifHazirlaViewModel)DataContext!;

    public OzelPozlarWindow()
    {
        InitializeComponent();
    }

    private void Kapat_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OzelGrid_CellEditEnded(object? sender, DataGridCellEditEndedEventArgs e)
    {
        if (e.Row.DataContext is KesifPoz poz)
        {
            ViewModel.OzelPozDuzenlemeTamamlandi(poz);
        }
    }
}
