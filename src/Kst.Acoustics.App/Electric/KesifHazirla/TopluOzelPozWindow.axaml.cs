using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Kst.Acoustics.Core.Electric.KesifHazirla;

namespace Kst.Acoustics.App.Electric.KesifHazirla;

public partial class TopluOzelPozWindow : Window
{
    private readonly KesifHazirlaViewModel? _viewModel;
    private readonly ObservableCollection<KesifTopluPozSatiri> _satirlar = new();
    private bool _panoOkunuyor;

    public TopluOzelPozWindow(KesifHazirlaViewModel viewModel) : this()
        => _viewModel = viewModel;

    public TopluOzelPozWindow()
    {
        InitializeComponent();
        for (var i = 0; i < 20; i++) _satirlar.Add(new());
        gridPozlar.ItemsSource = _satirlar;
        gridPozlar.AddHandler(KeyDownEvent, Grid_KeyDown, RoutingStrategies.Tunnel);
        Opened += (_, _) =>
        {
            gridPozlar.SelectedIndex = 0;
            gridPozlar.CurrentColumn = gridPozlar.Columns[0];
            gridPozlar.Focus();
        };
    }

    private async void Grid_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.V || !(e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta))) return;
        e.Handled = true;
        await YapistirAsync();
    }

    private async void Yapistir_OnClick(object? sender, RoutedEventArgs e) => await YapistirAsync();

    private bool DuzenlemeyiTamamla() => gridPozlar.CommitEdit(DataGridEditingUnit.Cell, true)
        && gridPozlar.CommitEdit(DataGridEditingUnit.Row, true);

    private async Task YapistirAsync()
    {
        if (_panoOkunuyor || !DuzenlemeyiTamamla()) return;
        _panoOkunuyor = true;
        try
        {
            var satir = Math.Max(0, gridPozlar.SelectedIndex);
            var sutun = gridPozlar.CurrentColumn is null ? 0 : gridPozlar.Columns.IndexOf(gridPozlar.CurrentColumn);
            if (sutun > 3) { txtMesaj.Text = "Yapıştırmak için ilk dört sütundan birini seçin."; return; }
            var text = Clipboard is null ? null : await Clipboard.TryGetTextAsync();
            if (string.IsNullOrWhiteSpace(text)) { txtMesaj.Text = "Panoda metin yok. Önce Excel'deki hücreleri Ctrl+C ile kopyalayın."; return; }
            var kayitlar = KesifTopluPozServisi.PanoCoz(text, sutun);
            if (kayitlar.Count == 0) { txtMesaj.Text = "Panoda aktarılacak poz satırı bulunamadı."; return; }
            // Önce bütün pano doğrulanır; fazla sütun veya bozuk alıntı mevcut taslağı değiştirmez.
            while (_satirlar.Count < satir + kayitlar.Count + 1) _satirlar.Add(new());
            for (var i = 0; i < kayitlar.Count; i++)
                for (var j = 0; j < kayitlar[i].Length; j++) _satirlar[satir + i].HucreAyarla(sutun + j, kayitlar[i][j]);
            txtMesaj.Text = $"{kayitlar.Count} satır yapıştırıldı. Kontrol edip Özel Pozlara Ekle düğmesine basın.";
            gridPozlar.Focus();
        }
        catch (Exception ex) { txtMesaj.Text = $"Yapıştırılamadı: {ex.Message}"; }
        finally { _panoOkunuyor = false; }
    }

    private void SatirEkle_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!DuzenlemeyiTamamla()) return;
        var yeni = new KesifTopluPozSatiri();
        _satirlar.Add(yeni);
        gridPozlar.SelectedItem = yeni;
        gridPozlar.CurrentColumn = gridPozlar.Columns[0];
        gridPozlar.ScrollIntoView(yeni, gridPozlar.CurrentColumn);
        gridPozlar.Focus();
    }

    private void SatirSil_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!DuzenlemeyiTamamla() || gridPozlar.SelectedItem is not KesifTopluPozSatiri row) return;
        var index = gridPozlar.SelectedIndex;
        _satirlar.Remove(row);
        if (_satirlar.Count == 0) _satirlar.Add(new());
        gridPozlar.SelectedIndex = Math.Min(index, _satirlar.Count - 1);
    }

    private void Grid_CellEditEnded(object? sender, DataGridCellEditEndedEventArgs e)
    {
        if (e.Row.DataContext is KesifTopluPozSatiri row) row.Hata = "";
    }

    private void Ekle_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || _panoOkunuyor || !DuzenlemeyiTamamla()) return;
        if (_viewModel.TopluOzelPozEkle(_satirlar, out var mesaj)) { Close(); return; }
        txtMesaj.Text = mesaj;
        var hatali = _satirlar.FirstOrDefault(x => x.Hata.Length > 0);
        if (hatali is not null) { gridPozlar.SelectedItem = hatali; gridPozlar.ScrollIntoView(hatali, null); }
    }

    private void Iptal_OnClick(object? sender, RoutedEventArgs e) => Close();
}
