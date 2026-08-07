using Avalonia.Controls;
using Kst.Acoustics.Core.Electric.KesifHazirla;

namespace Kst.Acoustics.App.Electric.KesifHazirla;

public partial class KesifIcmalWindow : Window
{
    public KesifIcmalWindow()
    {
        InitializeComponent();
    }

    private void IcmalGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is KesifHazirlaViewModel viewModel &&
            sender is DataGrid { SelectedItem: KesifIcmalSatiri satir })
        {
            // DataGridCollectionView sıralamasından sonra da alt kırılım seçilen gerçek satırı izlesin.
            viewModel.SeciliIcmalSatiri = satir;
        }
    }
}
