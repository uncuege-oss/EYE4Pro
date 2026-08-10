using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Kst.Acoustics.App.Salon.Views;

public enum SalonCadIslemi
{
    Pah, Fillet, DogrusalDizi, DaireselDizi,
    HizalaSol, HizalaMerkezX, HizalaSag, HizalaAlt, HizalaMerkezY, HizalaUst,
    DagitYatay, DagitDikey, Grupla, GrubuCoz, OfsetliKopya,
    KaliciOlcu, KisitYatay, KisitDikey, KisitUzunluk
}

public sealed record SalonCadIstegi(SalonCadIslemi Islem, double A, double B, int C);

public partial class SalonGelismisCadWindow : Window
{
    private static readonly (SalonCadIslemi Islem, string Ad, string Aciklama, string A, string B, string C)[] Islemler =
    [
        (SalonCadIslemi.Pah, "Pah kır", "Tek seçili, üç köşeli polyline'ın orta köşesini kırar.", "Mesafe (m)", "—", "—"),
        (SalonCadIslemi.Fillet, "Köşe yuvarla (Fillet)", "Tek seçili, üç köşeli polyline'ın orta köşesine teğet yay ekler.", "Yarıçap (m)", "—", "—"),
        (SalonCadIslemi.DogrusalDizi, "Doğrusal dizi", "Seçimi X/Y adımıyla çoğaltır; sayı özgün dahil toplam örnektir.", "ΔX (m)", "ΔY (m)", "Sayı"),
        (SalonCadIslemi.DaireselDizi, "Dairesel dizi", "Seçimi dünya orijini çevresinde eş açılı çoğaltır.", "Toplam açı (°)", "—", "Sayı"),
        (SalonCadIslemi.HizalaSol, "Sola hizala", "Seçili öğelerin sol sınırlarını hizalar.", "—", "—", "—"),
        (SalonCadIslemi.HizalaMerkezX, "Yatay merkeze hizala", "Seçili öğeleri yatay merkez çizgisinde hizalar.", "—", "—", "—"),
        (SalonCadIslemi.HizalaSag, "Sağa hizala", "Seçili öğelerin sağ sınırlarını hizalar.", "—", "—", "—"),
        (SalonCadIslemi.HizalaAlt, "Alta hizala", "Seçili öğelerin alt sınırlarını hizalar.", "—", "—", "—"),
        (SalonCadIslemi.HizalaMerkezY, "Dikey merkeze hizala", "Seçili öğeleri düşey merkez çizgisinde hizalar.", "—", "—", "—"),
        (SalonCadIslemi.HizalaUst, "Üste hizala", "Seçili öğelerin üst sınırlarını hizalar.", "—", "—", "—"),
        (SalonCadIslemi.DagitYatay, "Yatay eşit dağıt", "En az üç seçili öğenin merkezlerini eşit aralıkla dağıtır.", "—", "—", "—"),
        (SalonCadIslemi.DagitDikey, "Dikey eşit dağıt", "En az üç seçili öğenin merkezlerini eşit aralıkla dağıtır.", "—", "—", "—"),
        (SalonCadIslemi.Grupla, "Grupla", "Seçili öğelere ortak kalıcı grup kimliği verir.", "—", "—", "—"),
        (SalonCadIslemi.GrubuCoz, "Grubu çöz", "Seçili öğelerin grup ilişkisini kaldırır.", "—", "—", "—"),
        (SalonCadIslemi.OfsetliKopya, "Ofsetli kopyala/yapıştır", "Seçimi aynı belgeye verilen ofsetle bağımsız kopyalar.", "ΔX (m)", "ΔY (m)", "—"),
        (SalonCadIslemi.KaliciOlcu, "Kalıcı bağlı ölçü ekle", "Seçili öğenin ilk ve son köşesine bağlı ölçü ekler.", "Etiket ofseti (m)", "—", "—"),
        (SalonCadIslemi.KisitYatay, "Yatay kısıt", "Seçili öğenin ilk-son köşesini yatay tutar.", "—", "—", "—"),
        (SalonCadIslemi.KisitDikey, "Dikey kısıt", "Seçili öğenin ilk-son köşesini dikey tutar.", "—", "—", "—"),
        (SalonCadIslemi.KisitUzunluk, "Uzunluk kısıtı", "Seçili öğenin ilk-son köşe uzunluğunu sabitler.", "Uzunluk (m)", "—", "—")
    ];

    public SalonCadIstegi? Istek { get; private set; }
    public SalonGelismisCadWindow()
    {
        InitializeComponent();
        IslemCombo.ItemsSource = Islemler.Select(x => x.Ad).ToList();
        IslemCombo.SelectedIndex = 0;
    }
    private void Islem_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        int i = Math.Clamp(IslemCombo.SelectedIndex, 0, Islemler.Length - 1);
        var x = Islemler[i]; AciklamaText.Text = x.Aciklama;
        ALabel.Text = x.A; BLabel.Text = x.B; CLabel.Text = x.C;
    }
    private void Uygula_OnClick(object? sender, RoutedEventArgs e)
    {
        int i = Math.Clamp(IslemCombo.SelectedIndex, 0, Islemler.Length - 1);
        Istek = new SalonCadIstegi(Islemler[i].Islem, (double)(AValue.Value ?? 0),
            (double)(BValue.Value ?? 0), (int)(CValue.Value ?? 0));
        Close();
    }
    private void Iptal_OnClick(object? sender, RoutedEventArgs e) => Close();
}
