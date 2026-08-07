using Avalonia.Controls;
using Kst.Acoustics.Core.Terrain.Models;

namespace Kst.Acoustics.App.Terrain.Views;

/// M61 F5 — kotlu kesit penceresi.
public partial class KesitWindow : Window
{
    public KesitWindow() : this(null) { }

    public KesitWindow(KesitModeli? model)
    {
        InitializeComponent();
        if (model is not null)
        {
            Kesit.Model = model;
            BaslikText.Text = $"Bina-Arazi Kotlu Kesit — {model.Bloklar.Count} blok";
        }
    }

    private void Kapat_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}
