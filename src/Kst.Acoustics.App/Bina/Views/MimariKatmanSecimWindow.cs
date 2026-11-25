using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Kst.Acoustics.Core.Bina.Services;

namespace Kst.Acoustics.App.Bina.Views;

/// <summary>CAD içeriği ve üretim girdisi aynı katman seçimini kullanır.</summary>
public sealed class MimariKatmanSecimWindow : Window
{
    private readonly MimariKatmanliCizim _cizim;
    private readonly List<KatmanSatiri> _satirlar;
    private readonly ListBox _liste = new();
    private readonly TextBox _ara = new() { PlaceholderText = "Katman ara…" };
    private readonly TextBlock _durum = Label("Önizleme hazırlanıyor…", "#A8C5D9");
    private readonly TextBlock _sayac = Label("", "#A8C5D9");
    private readonly Button _uret = Button("Üret", true);
    private readonly MimariKatmanOnizlemeCanvas _canvas = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private int _revision;
    private bool _bulk, _busy, _closed;

    public MimariKatmanSecimWindow(MimariKatmanliCizim cizim)
    {
        _cizim = cizim;
        Title = "Mimari Proje — Katmanları Seç";
        Width = 1220; Height = 800; MinWidth = 820; MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush.Parse("#08121E");
        _satirlar = cizim.Katmanlar.OrderBy(k => k.Ad, StringComparer.CurrentCultureIgnoreCase)
            .Select(k => new KatmanSatiri(k.Ad, k.NesneSayisi, k.VarsayilanAcik, k.KotYukseklikEtiketiSayisi)).ToList();
        foreach (var item in _satirlar) item.PropertyChanged += KatmanDegisti;

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        var header = new StackPanel { Margin = new Thickness(20, 16), Spacing = 5 };
        header.Children.Add(new TextBlock { Text = "Katmanları seç, çizimi sadeleştir", FontSize = 22,
            FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#E0F0FA"), TextWrapping = TextWrapping.Wrap });
        var subtitle = Label("Açık katmanlar önizlemede görünür. Üret, yalnız bu verilerden katları ve odaları oluşturur.", "#94B5CC");
        subtitle.TextWrapping = TextWrapping.Wrap;
        header.Children.Add(subtitle);
        root.Children.Add(header);

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("290,*"), Margin = new Thickness(12, 0) };
        Grid.SetRow(body, 1); root.Children.Add(body);
        var layerPanel = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto"), Margin = new Thickness(0, 0, 12, 0) };
        layerPanel.Children.Add(_ara);
        var buttons = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 6) };
        var all = Button("Tümünü aç"); var none = Button("Tümünü kapat"); var defaults = Button("Dosyadaki durum");
        all.Click += (_, _) => TopluSec(true); none.Click += (_, _) => TopluSec(false); defaults.Click += (_, _) => TopluSec(null);
        buttons.Children.Add(all); buttons.Children.Add(none); buttons.Children.Add(defaults);
        Grid.SetRow(buttons, 1); layerPanel.Children.Add(buttons);
        _sayac.Margin = new Thickness(4, 0, 4, 7); Grid.SetRow(_sayac, 2); layerPanel.Children.Add(_sayac);
        _liste.Background = Brush.Parse("#0C1B29");
        _liste.BorderThickness = new Thickness(1); _liste.BorderBrush = Brush.Parse("#203C51");
        // Sanallaştırılmış satır kaydırma/arama sırasında önce boşaltılır, sonra
        // başka bir katmana bağlanır. Tüm alanlar güncel DataContext'i izler.
        _liste.ItemTemplate = new FuncDataTemplate<KatmanSatiri>((_, _) =>
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 2) };
            var name = new TextBlock { MaxWidth = 180, TextTrimming = TextTrimming.CharacterEllipsis };
            name.Bind(TextBlock.TextProperty, new Binding(nameof(KatmanSatiri.Ad)));
            var check = new CheckBox { Content = name,
                Foreground = Brush.Parse("#D2E6F2"), FontSize = 12, MinHeight = 28 };
            check.Bind(CheckBox.IsCheckedProperty, new Binding(nameof(KatmanSatiri.Acik)) { Mode = BindingMode.TwoWay });
            check.Bind(ToolTip.TipProperty, new Binding(nameof(KatmanSatiri.Ipucu)));
            var count = Label("", "#648BA7");
            count.Bind(TextBlock.TextProperty, new Binding(nameof(KatmanSatiri.NesneSayisi)) { StringFormat = "{0:N0}" });
            count.VerticalAlignment = VerticalAlignment.Center;
            count.Margin = new Thickness(6, 0, 3, 0); Grid.SetColumn(count, 1);
            grid.Children.Add(check); grid.Children.Add(count); return grid;
        }, supportsRecycling: true);
        Grid.SetRow(_liste, 3); layerPanel.Children.Add(_liste);
        var hint = Label("Oda adları, plan ve kesit kotları için yazı katmanlarını açık tutun. Katman adının üzerine gelerek ölçü yazısı bilgisini görebilirsiniz.", "#8EABBf");
        hint.TextWrapping = TextWrapping.Wrap; hint.Margin = new Thickness(4, 9, 4, 4); Grid.SetRow(hint, 4); layerPanel.Children.Add(hint);
        body.Children.Add(layerPanel);

        var preview = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        Grid.SetColumn(preview, 1); body.Children.Add(preview);
        var toolbar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(6, 0, 0, 7) };
        var info = Label("Fare tekerleği: yakınlaştır  ·  Sürükle: kaydır", "#779EBB");
        info.VerticalAlignment = VerticalAlignment.Center; info.TextWrapping = TextWrapping.Wrap; toolbar.Children.Add(info);
        var fit = Button("Ekrana sığdır"); fit.Click += (_, _) => _canvas.Sigdir(); Grid.SetColumn(fit, 1); toolbar.Children.Add(fit);
        preview.Children.Add(toolbar);
        var border = new Border { BorderBrush = Brush.Parse("#244354"), BorderThickness = new Thickness(1), Child = _canvas };
        Grid.SetRow(border, 1); preview.Children.Add(border);
        var sourceWarning = Label(cizim.UnitWarning ?? "Katmanları açıp kapatmak kaynak dosyayı değiştirmez.", "#BFA56C");
        sourceWarning.TextWrapping = TextWrapping.Wrap; sourceWarning.Margin = new Thickness(4, 8);
        Grid.SetRow(sourceWarning, 2); preview.Children.Add(sourceWarning);

        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Margin = new Thickness(20, 12, 16, 16) };
        _durum.TextWrapping = TextWrapping.Wrap; _durum.VerticalAlignment = VerticalAlignment.Center; footer.Children.Add(_durum);
        var cancel = Button("Vazgeç"); cancel.Click += (_, _) => Close(null); Grid.SetColumn(cancel, 1); footer.Children.Add(cancel);
        _uret.IsEnabled = false; _uret.MinWidth = 110; _uret.Click += (_, _) => Close(AcikAdlar()); Grid.SetColumn(_uret, 2); footer.Children.Add(_uret);
        Grid.SetRow(footer, 2); root.Children.Add(footer); Content = root;
        _ara.TextChanged += (_, _) => ListeyiSuz();
        _timer.Tick += async (_, _) => { _timer.Stop(); await OnizlemeYenileAsync(); };
        Opened += (_, _) =>
        {
            EkranaYerlestir();
            body.ColumnDefinitions[0].Width = new GridLength(Math.Clamp((Width - 24) * .32, 200, 290));
            YenilemePlanla();
        };
        Closed += (_, _) => { _closed = true; _timer.Stop(); foreach (var row in _satirlar) row.PropertyChanged -= KatmanDegisti; };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(null); e.Handled = true; } };
        ListeyiSuz(); SayaciYenile();
    }

    private void EkranaYerlestir()
    {
        var screen = (Owner is not null ? Screens.ScreenFromWindow(Owner) : null)
            ?? Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null) return;
        var work = screen.WorkingArea;
        double scale = screen.Scaling > 0 ? screen.Scaling : 1;
        // Width/Height mantıksal, WorkingArea/Position ekran pikselidir.
        // Başlık çubuğu ve kenarlar için ayrıca yer bırakılır.
        double maxWidth = Math.Max(320, work.Width / scale - 24);
        double maxHeight = Math.Max(280, work.Height / scale - 64);
        MinWidth = Math.Min(MinWidth, maxWidth);
        MinHeight = Math.Min(MinHeight, maxHeight);
        Width = Math.Min(Width, maxWidth);
        Height = Math.Min(Height, maxHeight);
        WindowStartupLocation = WindowStartupLocation.Manual;
        Position = new PixelPoint(
            work.X + Math.Max(0, (work.Width - (int)Math.Round(Width * scale)) / 2),
            work.Y + Math.Max(8, (work.Height - (int)Math.Round((Height + 40) * scale)) / 2));
    }

    private List<string> AcikAdlar() => _satirlar.Where(r => r.Acik).Select(r => r.Ad).ToList();
    private void ListeyiSuz()
    {
        var text = AramaAnahtari(_ara.Text?.Trim() ?? "");
        _liste.ItemsSource = _satirlar.Where(r => text.Length == 0 || AramaAnahtari(r.Ad).Contains(text, StringComparison.Ordinal)).ToList();
    }

    // CAD katman kimliği ve görünen ad aynen kalır; yalnız arama eşleştirmesi
    // siva/sıva ve olcu/ölçü gibi Türkçe klavye farklarını tolere eder.
    private static string AramaAnahtari(string value) => value.ToUpperInvariant()
        .Replace('ı', 'I').Replace('İ', 'I').Replace('Ş', 'S').Replace('Ç', 'C')
        .Replace('Ğ', 'G').Replace('Ö', 'O').Replace('Ü', 'U');

    private void TopluSec(bool? value)
    {
        _bulk = true;
        foreach (var row in _satirlar) row.Acik = value ?? row.VarsayilanAcik;
        _bulk = false; YenilemePlanla();
    }

    private void KatmanDegisti(object? sender, PropertyChangedEventArgs e) { if (!_bulk) YenilemePlanla(); }
    private void SayaciYenile() => _sayac.Text = $"{_satirlar.Count(r => r.Acik)} / {_satirlar.Count} katman açık";
    private void YenilemePlanla()
    {
        if (_closed) return;
        _revision++; SayaciYenile(); _uret.IsEnabled = false;
        _durum.Text = "Önizleme güncelleniyor…";
        _timer.Stop(); _timer.Start();
    }

    private async Task OnizlemeYenileAsync()
    {
        if (_closed || _busy) return;
        _busy = true;
        int version = _revision;
        var selected = AcikAdlar();
        try
        {
            var snapshot = await Task.Run(() => _cizim.HamKatOlustur(selected));
            if (_closed || version != _revision) return;
            _canvas.CizimiGoster(snapshot);
            var detection = snapshot.HamTespitSegmentler.Where(s => double.IsFinite(s.A.X) && double.IsFinite(s.A.Y) &&
                double.IsFinite(s.B.X) && double.IsFinite(s.B.Y) && (s.A.X != s.B.X || s.A.Y != s.B.Y)).ToList();
            bool hasGeometry = detection.Count > 0 &&
                detection.Max(s => Math.Max(s.A.X, s.B.X)) - detection.Min(s => Math.Min(s.A.X, s.B.X)) > 1e-6 &&
                detection.Max(s => Math.Max(s.A.Y, s.B.Y)) - detection.Min(s => Math.Min(s.A.Y, s.B.Y)) > 1e-6;
            _uret.IsEnabled = hasGeometry;
            _durum.Text = hasGeometry
                ? $"{snapshot.HamSegmentler.Count:N0} çizgi · {snapshot.HamMetinler.Count:N0} yazı. Hazır olduğunuzda Üret'e basın."
                : "Açık katmanlarda üretilecek çizgi yok. Duvar ve plan katmanlarını açın.";
            if (hasGeometry && _satirlar.Any(r => !r.Acik && r.KotSayisi > 0))
                _durum.Text += " Kapalı katmanlarda kot/yükseklik yazıları var; kat yüksekliği eksik kalabilir.";
        }
        catch (Exception ex) { if (!_closed && version == _revision) { _durum.Text = $"Önizleme hazırlanamadı: {ex.Message}"; _uret.IsEnabled = false; } }
        finally
        {
            _busy = false;
            if (!_closed && version != _revision) { _timer.Stop(); _timer.Start(); }
        }
    }

    private static TextBlock Label(string text, string color) => new() { Text = text, FontSize = 12, Foreground = Brush.Parse(color) };
    private static Button Button(string text, bool primary = false) => new()
    {
        Content = text, Padding = new Thickness(10, 7), Margin = new Thickness(0, 0, 5, 3), CornerRadius = new CornerRadius(5),
        Background = Brush.Parse(primary ? "#176891" : "#102A3C"), BorderBrush = Brush.Parse(primary ? "#3193BF" : "#25475E"),
        Foreground = Brush.Parse("#E2F0F9"), FontSize = 12
    };

    private sealed class KatmanSatiri : INotifyPropertyChanged
    {
        public string Ad { get; }
        public int NesneSayisi { get; }
        public bool VarsayilanAcik { get; }
        public int KotSayisi { get; }
        public string Ipucu => KotSayisi > 0 ? $"{Ad}\n{KotSayisi} kot/yükseklik yazısı içerir. Kat yüksekliği için gerekli olabilir." : Ad;
        private bool _acik;
        public bool Acik { get => _acik; set { if (_acik == value) return; _acik = value; OnPropertyChanged(); } }
        public KatmanSatiri(string ad, int count, bool open, int kotSayisi) { Ad = ad; NesneSayisi = count; VarsayilanAcik = open; _acik = open; KotSayisi = kotSayisi; }
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
