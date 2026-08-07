using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Kst.Acoustics.App.Controls;
using Kst.Acoustics.App.ViewModels;

namespace Kst.Acoustics.App.Views;

public partial class MeasurementWindow : Window
{
    private readonly MeasurementViewModel _vm;
    private bool _kapanisOnaylandi;

    public MeasurementWindow()
    {
        InitializeComponent();
        _vm = new MeasurementViewModel();
        _vm.RequestClose = async () => await TryCloseAsync();

        var viewport = this.FindControl<MeasurementRoomViewport>("RoomViewport");
        if (viewport != null)
        {
            viewport.RoomPicked += (_, e) => _vm.SetupPointPicked(e.World);
            viewport.PointClicked += (_, label) => _vm.SetupPointSelectedByLabel(label);
            _vm.RequestResetRoomView = () => viewport.ResetView();
        }

        DataContext = _vm;
        Closing += OnWindowClosing;
        Closed += (_, _) => _vm.Dispose();
    }

    private async void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_kapanisOnaylandi) return;
        e.Cancel = true;
        await TryCloseAsync();
    }

    private async Task TryCloseAsync()
    {
        if (_kapanisOnaylandi) return;
        if (!await KapanisOnayiAl()) return;

        _kapanisOnaylandi = true;

        var launcher = new LauncherWindow();
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = launcher;
        }
        launcher.Show();
        Close();
    }

    private enum KapanisSecimi { Iptal, KaydetVeCik, KaydetmedenCik }

    private async Task<bool> KapanisOnayiAl()
    {
        if (!_vm.IsDirty) return true; // kaydedilmemiş değişiklik yok → sormadan kapan
        var secim = await KapanisSecimiSor();
        return secim switch
        {
            KapanisSecimi.KaydetVeCik => await Kaydet(),
            KapanisSecimi.KaydetmedenCik => true,
            _ => false
        };
    }

    private async Task<bool> Kaydet()
    {
        await _vm.SaveSessionCommand.ExecuteAsync(null);
        return !_vm.IsDirty; // kullanıcı kaydetme diyalogunu iptal ederse dirty kalır → kapanma iptal
    }

    private async Task<KapanisSecimi> KapanisSecimiSor()
    {
        var dialog = new Window
        {
            Title = "Yerinde Ölçüm - Modülü Kapat",
            Width = 460, Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = Brush.Parse("#0D1E2E")
        };

        var sonuc = KapanisSecimi.Iptal;
        var kaydet = DialogButton("Kaydet ve Çık", "#0E5B82", "#1A8BBF", Brushes.White, 130);
        var kaydetmeden = DialogButton("Kaydetmeden Çık", "#3A1010", "#7A2020", Brush.Parse("#F0B0B0"), 145);
        var iptal = DialogButton("İptal", "#0D2535", "#1A5070", Brush.Parse("#C8E4F4"), 90);

        kaydet.Click += (_, _) => { sonuc = KapanisSecimi.KaydetVeCik; dialog.Close(); };
        kaydetmeden.Click += (_, _) => { sonuc = KapanisSecimi.KaydetmedenCik; dialog.Close(); };
        iptal.Click += (_, _) => dialog.Close();

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 20,
            Children =
            {
                new TextBlock
                {
                    Text = "Kaydedilmemiş ölçüm oturumu var. Kapatmak istediğinize emin misiniz?",
                    Foreground = Brush.Parse("#B0D8F0"),
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 14
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Spacing = 12,
                    Children = { kaydet, kaydetmeden, iptal }
                }
            }
        };

        await dialog.ShowDialog(this);
        return sonuc;
    }

    private static Button DialogButton(string text, string background, string border, IBrush foreground, double width) => new()
    {
        Content = text,
        Width = width,
        HorizontalContentAlignment = HorizontalAlignment.Center,
        Background = Brush.Parse(background),
        BorderBrush = Brush.Parse(border),
        Foreground = foreground,
        BorderThickness = new Avalonia.Thickness(1),
        Padding = new Avalonia.Thickness(0, 8),
        CornerRadius = new Avalonia.CornerRadius(6)
    };
}
