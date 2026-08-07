using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Kst.Acoustics.App.Electric.BasitProjeCiz.Views;

public partial class KurallarWindow : Window
{
    public KurallarWindow()
    {
        InitializeComponent();
    }

    private void Kapat_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
