using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Kst.Acoustics.Core.Bina.Models;

namespace Kst.Acoustics.App.Bina.Views;

public sealed class MimariIceriAktarimRaporuWindow : Window
{
    public MimariIceriAktarimRaporuWindow(string text, BinaKat? cizim)
    {
        Title = "Mimari Proje — İçe Aktarma Raporu";
        Width = 820; Height = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        bool hasDrawing = cizim is not null &&
            (cizim.HamSegmentler.Count > 0 || cizim.DxfSegmentler.Count > 0 || cizim.HamMetinler.Count > 0);
        var message = new TextBlock {
            Text = string.IsNullOrWhiteSpace(text) ? "Bu projede kayıtlı içe aktarma notu yok." : text,
            TextWrapping = TextWrapping.Wrap
        };
        var inspect = new Button { Content = "Çizimi incele", IsEnabled = hasDrawing };
        inspect.Click += async (_, _) =>
        {
            inspect.IsEnabled = false;
            try { await new MimariCizimIncelemeWindow(cizim!).ShowDialog(this); }
            catch (Exception ex) { message.Text += $"\n\nÇizim görüntülenemedi: {ex.Message}"; }
            finally { inspect.IsEnabled = hasDrawing; }
        };
        var close = new Button { Content = "Kapat" };
        close.Click += (_, _) => Close();
        var footer = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10, Margin = new Thickness(0, 12, 0, 0) };
        footer.Children.Add(close); footer.Children.Add(inspect);
        var grid = new Grid { RowDefinitions = new RowDefinitions("*,Auto,Auto"), Margin = new Thickness(20) };
        grid.Children.Add(new ScrollViewer { Content = message });
        var hint = new TextBlock { Text = hasDrawing
            ? "Çizimi incele, yüklenen CAD çizgisini gösterir; kat veya oda oluşması gerekmez."
            : "İncelenecek çizim yok. Önce bir mimari proje yükleyin veya çizimi olan bir kat seçin.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) };
        Grid.SetRow(hint, 1); grid.Children.Add(hint);
        Grid.SetRow(footer, 2); grid.Children.Add(footer);
        Content = grid;
    }
}
