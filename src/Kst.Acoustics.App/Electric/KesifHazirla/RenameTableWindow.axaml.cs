using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Kst.Acoustics.App.Electric.KesifHazirla;

public partial class RenameTableWindow : Window
{
    public string? Result { get; private set; }

    public RenameTableWindow()
    {
        InitializeComponent();
    }

    public RenameTableWindow(string initialName) : this()
    {
        var txtName = this.FindControl<TextBox>("TxtName");
        if (txtName != null)
        {
            txtName.Text = initialName;
            txtName.Focus();
            txtName.SelectAll();
        }
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        var txtName = this.FindControl<TextBox>("TxtName");
        Result = txtName?.Text;
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close();
    }

    private void TxtName_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Save_Click(sender, e);
        }
        else if (e.Key == Key.Escape)
        {
            Cancel_Click(sender, e);
        }
    }
}
