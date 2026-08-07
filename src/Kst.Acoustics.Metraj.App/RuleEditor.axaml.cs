using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Kst.Acoustics.Metraj.App;

public partial class RuleEditor : Window
{
    public RuleEditor()
    {
        InitializeComponent();
    }

    public RuleEditor(string dwgPath) : this()
    {
        DataContext = new RuleEditorViewModel(dwgPath);
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
