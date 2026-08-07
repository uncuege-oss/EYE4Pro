using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Kst.Acoustics.App.Electric.ViewModels;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.App.Electric.Views;

public partial class UstPanoSecDialog : Window
{
    private sealed class PanoOge
    {
        public Pano Pano { get; init; } = null!;
        public string Etiket { get; init; } = "";
    }

    private readonly TekHatSemasiViewModel _vm = null!;
    private readonly Pano _pano = null!;
    private readonly List<PanoOge> _adaylar = new();

    /// <summary>Üst pano değiştirildiyse true.</summary>
    public bool Degisti { get; private set; }

    public UstPanoSecDialog() { InitializeComponent(); }

    public UstPanoSecDialog(TekHatSemasiViewModel vm, Pano pano)
    {
        InitializeComponent();
        _vm = vm;
        _pano = pano;
        BaslikText.Text = $"Üst Panoyu Değiştir — {pano.Ad}";

        // Kat sırası (binadan; üstten alta). Adayları bu sıraya göre dizmek için indeks tablosu.
        var katlar = vm.Proje?.Bina?.KatEtiketleri() ?? new List<string> { "Z" };
        int KatIndeks(string? kat)
        {
            int i = katlar.IndexOf(kat ?? "");
            return i >= 0 ? i : int.MaxValue; // bilinmeyen kat en sona
        }

        // Geçerli üst pano adayları: kendisi, mevcut üst panosu, alt ağacındaki panolar
        // ve kompanzasyon/transfer panoları hariç. Kat sırasına, sonra ada göre dizilir.
        foreach (var p in vm.Panolar
                     .Where(p => p != pano
                                 && p != pano.UstPano
                                 && !vm.AltAgactaMi(pano, p)
                                 && p.PanoTipi != PanoTipi.Kompanzasyon
                                 && p.PanoTipi != PanoTipi.TransferPanosu)
                     .OrderBy(p => KatIndeks(p.Kat))
                     .ThenBy(p => p.Ad))
        {
            _adaylar.Add(new PanoOge { Pano = p, Etiket = $"{p.Ad}  ({p.Kat})" });
        }

        AramaBox.TextChanged += (_, _) => ListeyiTazele();
        ListeyiTazele();
    }

    private void ListeyiTazele()
    {
        string ara = AramaBox.Text?.Trim() ?? "";
        var filtre = string.IsNullOrEmpty(ara)
            ? _adaylar
            : _adaylar.Where(o => o.Etiket.Contains(ara, System.StringComparison.OrdinalIgnoreCase)).ToList();
        PanoListe.ItemsSource = new ObservableCollection<PanoOge>(filtre);
        if (PanoListe.ItemCount > 0) PanoListe.SelectedIndex = 0;
    }

    private void Iptal_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    private void PanoListe_OnDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e) => Uygula();

    private void Bagla_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Uygula();

    private void Uygula()
    {
        if (PanoListe.SelectedItem is not PanoOge oge) return;
        Degisti = _vm.UstPanoyuDegistir(_pano, oge.Pano);
        Close();
    }
}
