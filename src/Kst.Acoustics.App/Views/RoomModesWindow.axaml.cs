using Avalonia.Controls;
using Kst.Acoustics.App.ViewModels;

namespace Kst.Acoustics.App.Views;

public partial class RoomModesWindow : Window
{
    public RoomModesWindow()
    {
        InitializeComponent();
        var vm = new RoomModesViewModel();
        vm.RequestClose = () => 
        {
            var launcher = new LauncherWindow();
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = launcher;
            }
            launcher.Show();
            Close();
        };
        DataContext = vm;
    }
}
