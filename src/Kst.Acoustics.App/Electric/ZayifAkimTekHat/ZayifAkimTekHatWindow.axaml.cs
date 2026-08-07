using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Kst.Acoustics.App.Views;
using Kst.Acoustics.Core.Electric.ZayifAkimTekHat;
using System.Globalization;

namespace Kst.Acoustics.App.Electric.ZayifAkimTekHat;

public partial class ZayifAkimTekHatWindow : Window
{
    private ZayifAkimTekHatViewModel? viewModel;
    private bool anonsTeknolojiSecimiGuncelleniyor;

    public ZayifAkimTekHatWindow()
    {
        InitializeComponent();
        viewModel = new ZayifAkimTekHatViewModel();
        DataContext = viewModel;
    }

    private async void KatDuzenle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (viewModel?.Proje == null) return;
        var dlg = new Kst.Acoustics.App.Electric.Views.BinaKonfigDialog(viewModel.Proje.Bina);
        await dlg.ShowDialog(this);
        if (dlg.Onaylandi)
            viewModel.BinaKatlariniUygula();
    }

    private async void Kutuphane_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (viewModel is null) return;
        var pencere = new ZayifAkimKutuphaneWindow(viewModel.Proje, viewModel.AktifKutuphaneAltBaslikKodu);
        await pencere.ShowDialog(this);
        if (pencere.ViewModel.Degisti)
            viewModel.KutuphaneDegisti();
    }

    // Elektrik modül menüsüne (launcher) dön — aynı ekranda aç, bu pencereyi kapat.
    private void AnaMenuyeDon_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var launcher = new ElectricLauncherWindow
        {
            WindowStartupLocation = WindowStartupLocation.Manual
        };

        var screen = this.Screens?.ScreenFromWindow(this);
        if (screen != null)
        {
            var area = screen.WorkingArea;
            int w = System.Math.Min(1200, area.Width);
            int h = System.Math.Min(800, area.Height);
            launcher.Position = new PixelPoint(
                area.X + (area.Width - w) / 2,
                area.Y + (area.Height - h) / 2);
        }
        else
        {
            launcher.Position = this.Position;
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = launcher;
        launcher.Show();
        Close();
    }

    // Rack/Loop grubu ekle — önce hangi kata ekleneceğini sor, sonra ekle.
    private async void RackEkle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (viewModel is null) return;

        bool yangin = viewModel.AktifSistem == "Yangin";
        int? onSecili = yangin
            ? viewModel.SeciliYanginKatLoopGrubu?.KatNo
            : viewModel.SeciliRack?.KatNo;
        string baslik = yangin
            ? "Loop grubu hangi kata eklensin?"
            : "Rack kabin hangi kata eklensin?";

        // Yangın loop grubu üst bağlantı seçmez; rack sistemlerinde Ana Merkez/rack seçilir.
        var baglantiSecenekleri = yangin ? null : viewModel.BaglantiSecenekleriUret(null);

        var dlg = new KatSecDialog(viewModel.Katlar, onSecili, baslik, baglantiSecenekleri);
        await dlg.ShowDialog(this);
        if (dlg.Onaylandi)
            viewModel.RackEkleKata(dlg.SeciliKatNo, dlg.SeciliUstRackId);
    }

    // Üst bağlantı seçimi değişince modele uygula (SelectedValue yazması güvenilir olmadığından).
    private void UstBaglanti_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is RackBaglantiSecenek sec)
            viewModel?.UstBaglantiUygula(sec.Id);
    }

    private void CctvIzlemeBaglanti_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is RackBaglantiSecenek sec)
            viewModel?.CctvIzlemeBaglantisiUygula(sec.Id);
    }

    private async void AnonsTeknoloji_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (anonsTeknolojiSecimiGuncelleniyor || viewModel is null
            || sender is not ComboBox { SelectedItem: string yeni }
            || yeni == viewModel.SeciliAnonsTeknolojisi) return;

        string eski = viewModel.SeciliAnonsTeknolojisi;
        bool onay = !viewModel.AnonsTeknolojiDegisikligindeSilinecekVeriVar
                    || await AnonsTeknolojiDegisikliginiOnayla(yeni);
        if (onay)
            viewModel.AnonsTeknolojisiniDegistirVeTemizle(yeni);
        else
        {
            anonsTeknolojiSecimiGuncelleniyor = true;
            AnonsTeknolojiCombo.SelectedItem = eski;
            anonsTeknolojiSecimiGuncelleniyor = false;
        }
    }

    private async System.Threading.Tasks.Task<bool> AnonsTeknolojiDegisikliginiOnayla(string yeni)
    {
        var pencere = new Window
        {
            Title = "Anons Teknolojisini Değiştir",
            Width = 470,
            Height = 220,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Avalonia.Media.Brushes.Black
        };
        var aciklama = new TextBlock
        {
            Text = $"Sistem {yeni} olarak değiştirilecek.\n\nMevcut tüm ana merkez ve kat kabineti cihazları ile kablo/hat bilgileri silinecek. Emin misiniz?",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Foreground = Avalonia.Media.Brushes.White
        };
        var iptal = new Button { Content = "İptal", MinWidth = 90 };
        var onay = new Button { Content = "Evet, Değiştir", MinWidth = 130 };
        iptal.Click += (_, _) => pencere.Close(false);
        onay.Click += (_, _) => pencere.Close(true);
        pencere.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 20,
            Children =
            {
                aciklama,
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Spacing = 10,
                    Children = { iptal, onay }
                }
            }
        };
        return await pencere.ShowDialog<bool>(this);
    }

    private void SemaControl_OnSecimIstendi(object? sender, SemaSecimEventArgs e)
    {
        if (viewModel is null) return;
        if (e.AnaMerkez)
            viewModel.MerkeziSec();
        else if (e.Rack is not null)
            viewModel.RackSec(e.Rack);
        else if (e.YanginGrubu is not null)
            viewModel.YanginLoopGrubuSec(e.YanginGrubu);
        else if (e.AnalogZon is not null)
            viewModel.AnalogAnonsZonuSec(e.AnalogZon);
        else if (e.IzlemeNoktasi is not null)
            viewModel.CctvIzlemeNoktasiSec(e.IzlemeNoktasi);
    }

    // Aktif sistemin şema canvas'ını PNG olarak kaydeder (birebir ekran görüntüsü).
    private async void PngCiktiAl_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (viewModel is null) return;
        try
        {
            var kontrol = SemaControl;
            int w = (int)Math.Ceiling(Math.Max(1, kontrol.Bounds.Width));
            int h = (int)Math.Ceiling(Math.Max(1, kontrol.Bounds.Height));

            var sistemAdi = viewModel.AktifSistem switch
            {
                "Kamera" => "kamera",
                "Anons" => "acil_anons",
                "Tv" => "tv",
                "Yangin" => "yangin_algilama",
                "Telefon" => "telefon",
                "Diafon" => "diafon",
                _ => "data"
            };
            var oneriAd = $"{(string.IsNullOrWhiteSpace(viewModel.Proje.ProjeAdi) ? "zayif_akim" : viewModel.Proje.ProjeAdi)}_{sistemAdi}.png";

            var dosya = await StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "PNG Çıktısı Kaydet",
                SuggestedFileName = oneriAd,
                DefaultExtension = "png",
                FileTypeChoices = [new Avalonia.Platform.Storage.FilePickerFileType("PNG Görüntü") { Patterns = ["*.png"] }]
            });
            var hedef = dosya?.Path.LocalPath;
            if (string.IsNullOrWhiteSpace(hedef)) return;
            if (!hedef.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                hedef += ".png";

            // 2x ölçek: CAD altlığı olarak kullanılabilecek netlikte raster.
            var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(
                new PixelSize(w * 2, h * 2), new Vector(192, 192));
            bitmap.Render(kontrol);
            bitmap.Save(hedef);
            viewModel.DurumMesaji = $"✓ PNG: {hedef}";
        }
        catch (Exception ex)
        {
            viewModel.DurumMesaji = $"✗ PNG hatası: {ex.Message}";
        }
    }

    // Rack alanı (TextBox) düzenlemesi bitince şemayı tazele.
    private void Alan_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => viewModel?.DuzenlemeTamamlandi();

    private void MerkezeKabloMetraj_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (viewModel is null || sender is not TextBox { Text: { } metin }) return;
        if (!double.TryParse(metin, NumberStyles.Float, CultureInfo.CurrentCulture, out var deger)
            && !double.TryParse(metin, NumberStyles.Float, CultureInfo.InvariantCulture, out deger))
            return;

        viewModel.MerkezeKabloMetrajiDegistir(deger);
    }

    private void YanginKabloMetraji_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (viewModel is null || sender is not TextBox { DataContext: ElvYanginLoopCihazi cihaz, Text: { } metin }) return;
        if (!double.TryParse(metin, NumberStyles.Float, CultureInfo.CurrentCulture, out var deger)
            && !double.TryParse(metin, NumberStyles.Float, CultureInfo.InvariantCulture, out deger))
            return;

        viewModel.YanginLoopKabloMetrajiDegisti(cihaz, deger);
    }

    // Cihaz tablosu hücre düzenlemesi bitince şemayı tazele.
    private void Cihaz_CellEditEnded(object? sender, DataGridCellEditEndedEventArgs e)
        => viewModel?.DuzenlemeTamamlandi();
}
