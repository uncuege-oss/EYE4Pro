using Avalonia.Controls;
using Kst.Acoustics.App.ViewModels;

namespace Kst.Acoustics.App.Views;

public partial class SoundInsulationLibraryWindow : Window
{
    public SoundInsulationLibraryWindow()
    {
        InitializeComponent();
    }

    private void Close_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is SoundInsulationViewModel vm)
            vm.SaveLibrary();
        this.Close();
    }
}
