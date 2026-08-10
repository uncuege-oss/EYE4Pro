using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Kst.Acoustics.Core.Salon.Models;
using Kst.Acoustics.Core.Salon.Services;

namespace Kst.Acoustics.App.Salon.Views;

public partial class SalonSurumlerWindow : Window
{
    private readonly SalonGeometri _salon;
    private readonly string _klasor;
    public SalonGeometri? GeriYuklenenSalon { get; private set; }

    public SalonSurumlerWindow() : this(SalonGeometriServisi.YeniOlustur(), SalonSurumGecmisiServisi.VarsayilanKlasor) { }
    public SalonSurumlerWindow(SalonGeometri salon, string? klasor = null)
    {
        _salon = salon;
        _klasor = klasor ?? SalonSurumGecmisiServisi.VarsayilanKlasor;
        InitializeComponent();
        Yenile();
    }
    private void Yenile()
    {
        var liste = SalonSurumGecmisiServisi.Listele(_klasor);
        SurumList.ItemsSource = liste;
        SurumList.SelectedIndex = liste.Count > 0 ? 0 : -1;
        DurumText.Text = liste.Count == 0 ? "Henüz kayıtlı sürüm yok." : $"{liste.Count} sürüm bulundu.";
    }
    private void SurumAl_OnClick(object? sender, RoutedEventArgs e)
    {
        try { SalonSurumGecmisiServisi.AnlikSurumKaydet(_salon, _klasor); Yenile(); DurumText.Text = "Adlandırılmış sürüm kaydedildi."; }
        catch (Exception ex) { DurumText.Text = "Sürüm alınamadı: " + ex.Message; }
    }
    private void GeriYukle_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SurumList.SelectedItem is not SalonSurumBilgisi s) { DurumText.Text = "Önce bir sürüm seçin."; return; }
        try { GeriYuklenenSalon = SalonSurumGecmisiServisi.GeriYukle(s); Close(); }
        catch (Exception ex) { DurumText.Text = "Sürüm açılamadı: " + ex.Message; }
    }
    private void Kapat_OnClick(object? sender, RoutedEventArgs e) => Close();
}
