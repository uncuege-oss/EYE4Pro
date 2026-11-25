using System.ComponentModel;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Kst.Acoustics.App.Salon.ViewModels;
using Kst.Acoustics.App.Views;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Salon.Models;
using Kst.Acoustics.Core.Salon.Services;

namespace Kst.Acoustics.App.Salon.Views;

public partial class SalonTasarlaWindow : Window
{
    private readonly SalonTasarlaViewModel _vm = new();
    private readonly SalonDuzenlemeGecmisi _gecmis = new();
    private readonly Func<Window>? _geriPencere;
    private bool _kapanisOnaylandi, _onayBekleniyor;
    private AciklikTipi _aciklikTip;
    private double _aciklikGen = 1, _aciklikYuk = 2.1, _aciklikKot;
    public SalonTasarlaWindow() : this(null) { }
    public SalonTasarlaWindow(Func<Window>? geriPencere)
    {
        InitializeComponent(); DataContext = _vm; _geriPencere = geriPencere;
        TasarimArayuzuKur(); CizimCanvas.OnDegisecek = SnapshotAl;
        BaglaCanvas(); UndoSifirla();
        Closing += OnWindowClosing;
    }
    private void SnapshotAl() => _gecmis.Degisecek(_vm.Salon);
    private void UndoSifirla() => _gecmis.Sifirla(_vm.Salon);
    private void GeriAl() { Focus(); if (_gecmis.Geri(_vm.Salon) is { } s) SalonuGeriYukle(s); }
    private void Yinele() { Focus(); if (_gecmis.Ileri(_vm.Salon) is { } s) SalonuGeriYukle(s); }
    private void SalonuGeriYukle(SalonGeometri salon)
    { _vm.Salon = salon; BaglaCanvas(); _vm.IsDirty = _gecmis.Kirli(salon); _vm.BasligiYenile(); }
    private void BaglaCanvas()
    {
        SalonTasarimServisi.Hazirla(_vm.Salon); CizimCanvas.Salon = _vm.Salon;
        CizimCanvas.AktifKatman = _vm.SeciliKatman; _kesit.Salon = _vm.Salon; _kesit.ProfiliSifirla(); TasarimBagla();
    }
    private void AciklikYerlestir(Point2D p)
    {
        var ac = new DuvarAcikligi { Tip = _aciklikTip, Ad = $"{(_aciklikTip == AciklikTipi.Kapi ? "Kapı" : "Pencere")} {_vm.Acikliklar.Count + 1}", GenislikM = _aciklikGen, YukseklikM = _aciklikYuk, AltKotM = _aciklikKot, MerkezX = p.X, MerkezY = p.Y };
        SalonTasarimServisi.AcikligiBagla(_vm.Salon, ac);
        if (ac.DuvarId == null) { _bilgi.Text = "Salon sınırı veya iç duvar üzerinde bir nokta seçin."; return; }
        SnapshotAl(); _vm.Acikliklar.Add(ac); _vm.SeciliAciklik = ac; YenileModel(); AciklikPaneli();
    }
    private async void DxfDisaAktar_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Focus();
        try
        {
            var errors = SalonTasarimDogrulama.Kontrol(_vm.Salon).Where(i => i.Hata).ToList();
            if (errors.Count > 0) { _bilgi.Text = errors[0].Mesaj; return; }
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Salon DXF Dışa Aktar", DefaultExtension = "dxf", SuggestedFileName = _vm.Salon.SalonAdi + ".dxf", FileTypeChoices = [new FilePickerFileType("DXF (3DFACE)") { Patterns = ["*.dxf"] }] });
            if (file == null) return; SalonDxfExportServisi.DisaAktar(_vm.Salon, file.Path.LocalPath); _bilgi.Text = "DXF dışa aktarıldı: " + file.Name;
        }
        catch (Exception ex) { _bilgi.Text = "DXF aktarılamadı: " + ex.Message; }
    }
    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (ctrl && e.Key == Key.S) { Focus(); _ = Kaydet(); e.Handled = true; return; }
        if (e.Source is TextBox) return;
        if (ctrl) { if (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Shift) || e.Key == Key.Y) { Yinele(); e.Handled = true; } else if (e.Key == Key.Z) { GeriAl(); e.Handled = true; } return; }
        switch (e.Key)
        {
            case Key.Escape: CizimCanvas.CizimiIptal(); break;
            case Key.Delete: CizimCanvas.SeciliyiSil(); break;
            case Key.F3: CizimCanvas.SnapToggle(); break;
            case Key.F8: CizimCanvas.OrthoToggle(); break;
            case Key.F9: CizimCanvas.GridToggle(); break;
            case Key.F10: CizimCanvas.PolarToggle(); break;
            case Key.V: CizimAraci(SalonAraci.Sec); break;
            default: return;
        }
        e.Handled = true;
    }
    private async void Yeni_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!await KapanisOnayiAl()) return;
        _vm.Salon = SalonGeometriServisi.YeniOlustur();
        _vm.IsDirty = false;
        _vm.DurumMesaji = "Yeni salon geometrisi oluşturuldu.";
        _vm.BasligiYenile();
        BaglaCanvas();
        UndoSifirla();
        CizimCanvas?.GorunumuSifirla();
    }

    private async void Sablon_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!await KapanisOnayiAl()) return;

        var dialog = new SalonSablonWindow();
        await dialog.ShowDialog(this);
        if (dialog.OlusturulanSalon is not { } yeni) return;

        _vm.Salon = yeni;
        _vm.IsDirty = true; // henüz kaydedilmemiş içerik
        _vm.DurumMesaji = "Şablondan salon oluşturuldu.";
        _vm.BasligiYenile();
        BaglaCanvas();
        UndoSifirla();
        _gecmis.Sifirla(_vm.Salon, false);
        CizimCanvas?.Sigdir();
    }

    private async void Ac_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Salon Geometrisi Aç",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("EYE4PRO Salon Geometrisi (.e4salon.json)")
                { Patterns = ["*.e4salon.json"] }]
        });
        var path = files.Count > 0 ? files[0].Path.LocalPath : null;
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!await KapanisOnayiAl()) return;
        DosyaAc(path);
    }

    public void DosyaAc(string path)
    {
        try
        {
            _vm.Salon = SalonGeometriServisi.Ac(path);
            _vm.IsDirty = false;
            _vm.DurumMesaji = $"Açıldı: {Path.GetFileName(path)}  (Rev. {_vm.Salon.GeometriRevizyon})";
            _vm.BasligiYenile();
            BaglaCanvas();
            UndoSifirla();
            CizimCanvas?.Sigdir();
        }
        catch (Exception ex)
        {
            _bilgi.Text = $"Açılamadı: {ex.Message}";
        }
    }

    private async void Kaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await Kaydet();

    private async void FarkliKaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await FarkliKaydet();

    private async Task<bool> Kaydet()
    {
        if (string.IsNullOrWhiteSpace(_vm.Salon.DosyaYolu))
            return await FarkliKaydet();
        return KaydetYola(_vm.Salon.DosyaYolu);
    }

    private async Task<bool> FarkliKaydet()
    {
        var ad = string.IsNullOrWhiteSpace(_vm.Salon.SalonAdi) ? "Salon" : _vm.Salon.SalonAdi;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Salon Geometrisi Kaydet",
            DefaultExtension = "e4salon.json",
            SuggestedFileName = $"{ad}.e4salon.json",
            FileTypeChoices = [new FilePickerFileType("EYE4PRO Salon Geometrisi (.e4salon.json)")
                { Patterns = ["*.e4salon.json"] }]
        });
        var path = file?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path)) return false;
        return KaydetYola(path);
    }

    private bool KaydetYola(string path)
    {
        try
        {
            SalonGeometriServisi.Kaydet(_vm.Salon, path);
            _gecmis.Kaydedildi(_vm.Salon);
            _vm.IsDirty = false;
            _bilgi.Text = $"Kaydedildi: {Path.GetFileName(path)}  (Rev. {_vm.Salon.GeometriRevizyon})";
            _vm.BasligiYenile();
            return true;
        }
        catch (Exception ex)
        {
            _bilgi.Text = $"Kaydedilemedi: {ex.Message}";
            return false;
        }
    }

    private async void Geri_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!await KapanisOnayiAl()) return;
        var hedef = _geriPencere?.Invoke() ?? new EngineeringHomeWindow();
        OpenWindowOnSameScreen(hedef);
    }

    private void OpenWindowOnSameScreen(Window win)
    {
        win.WindowStartupLocation = WindowStartupLocation.Manual;
        var screen = Screens?.ScreenFromWindow(this);
        if (screen != null)
        {
            var area = screen.WorkingArea;
            int w = (int)(double.IsNaN(win.Width) || win.Width < 100 ? 1200 : win.Width);
            int h = (int)(double.IsNaN(win.Height) || win.Height < 100 ? 800 : win.Height);
            w = Math.Min(w, area.Width);
            h = Math.Min(h, area.Height);
            win.Position = new PixelPoint(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2);
        }
        else
        {
            win.Position = Position;
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = win;
        win.Show();
        _kapanisOnaylandi = true;
        Close();
    }

    private async void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_kapanisOnaylandi) return;
        e.Cancel = true;
        if (!await KapanisOnayiAl()) return;
        _kapanisOnaylandi = true;
        Close();
    }

    private enum KapanisSecimi { Iptal, KaydetVeCik, KaydetmedenCik }

    private async Task<bool> KapanisOnayiAl()
    {
        Focus(); _vm.IsDirty = _gecmis.Kirli(_vm.Salon);
        if (!_vm.IsDirty) return true; // kaydedilmemiş değişiklik yok → sormadan kapan
        if (_onayBekleniyor) return false;
        _onayBekleniyor = true; KapanisSecimi secim;
        try { secim = await KapanisSecimiSor(); } finally { _onayBekleniyor = false; }
        return secim switch
        {
            KapanisSecimi.KaydetVeCik => await Kaydet(),
            KapanisSecimi.KaydetmedenCik => true,
            _ => false
        };
    }

    private async Task<KapanisSecimi> KapanisSecimiSor()
    {
        var dialog = new Window
        {
            Title = "Kaydedilmemiş Değişiklikler",
            Width = 460,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = Avalonia.Media.Brush.Parse("#0D1E2E")
        };

        var sonuc = KapanisSecimi.Iptal;
        var kaydet = DialogButton("Kaydet ve Devam", "#0E5B82", "#1A8BBF", Avalonia.Media.Brushes.White, 130);
        var kaydetmeden = DialogButton("Kaydetmeden Devam", "#3A1010", "#7A2020", Avalonia.Media.Brush.Parse("#F0B0B0"), 145);
        var iptal = DialogButton("İptal", "#0D2535", "#1A5070", Avalonia.Media.Brush.Parse("#C8E4F4"), 90);

        kaydet.Click += (_, _) => { sonuc = KapanisSecimi.KaydetVeCik; dialog.Close(); };
        kaydetmeden.Click += (_, _) => { sonuc = KapanisSecimi.KaydetmedenCik; dialog.Close(); };
        iptal.Click += (_, _) => dialog.Close();

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 20,
            Children =
            {
                new TextBlock
                {
                    Text = "Değişiklikleri kaydetmek ister misiniz?",
                    Foreground = Avalonia.Media.Brush.Parse("#B0D8F0"),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    FontSize = 14
                },
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Spacing = 12,
                    Children = { kaydet, kaydetmeden, iptal }
                }
            }
        };

        await dialog.ShowDialog(this);
        return sonuc;
    }

    private static Button DialogButton(string text, string background, string border,
        Avalonia.Media.IBrush foreground, double width) => new()
        {
            Content = text,
            Width = width,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Background = Avalonia.Media.Brush.Parse(background),
            BorderBrush = Avalonia.Media.Brush.Parse(border),
            Foreground = foreground,
            Padding = new Thickness(14, 8),
            CornerRadius = new Avalonia.CornerRadius(6)
        };
}
