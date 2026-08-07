using Avalonia.Controls;
using Kst.Acoustics.App.ViewModels;

namespace Kst.Acoustics.App.Views;

public partial class NoiseBarrierWindow : Window
{
    public NoiseBarrierWindow()
    {
        InitializeComponent();
        DataContext = new NoiseBarrierViewModel();
    }
}
