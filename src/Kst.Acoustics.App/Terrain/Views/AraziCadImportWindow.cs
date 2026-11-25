using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Kst.Acoustics.Core.Terrain.Models;
using Kst.Acoustics.Core.Terrain.Services;
using netDxf;
using netDxf.Units;

namespace Kst.Acoustics.App.Terrain.Views;

/// <summary>Kaynak CAD dosyasını değiştirmeden birim, katman ve kot okuma seçeneklerini gözden geçirir.</summary>
public sealed class AraziCadImportWindow : Window
{
    private readonly DxfDocument _document;
    private readonly string _sourcePath;
    private readonly List<KatmanSatiri> _layers;
    private readonly ComboBox _units = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly CheckBox _readText = new() { Content = "Kot yazılarını da oku (TEXT / MTEXT)", IsChecked = true };
    private readonly TextBox _tolerance = new() { Text = "2", Width = 76 };
    private readonly TextBox _search = new() { PlaceholderText = "Katman ara…" };
    private readonly ListBox _layerList = new();
    private readonly TextBox _preview = new()
    {
        IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
        FontFamily = new FontFamily("Consolas,Menlo,monospace"), FontSize = 12,
        Background = Brush.Parse("#0F1C14"), Foreground = Brush.Parse("#DDF5E5"), BorderBrush = Brush.Parse("#234534")
    };
    private readonly TextBlock _count = Label("");
    private readonly TextBlock _status = Label("Birim ve arazi katmanlarını seçin.");
    private readonly Button _create = ActionButton("Araziyi oluştur", true);
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private AraziImportSonucu? _result;
    private int _revision;
    private bool _busy, _closed, _bulk, _loading = true;

    /// <summary>Onaylanan seçeneklerle hesaplanan yüzey; kabulden sonra yeniden üçgenlemeyi önler.</summary>
    public AraziYuzeyi? Yuzey { get; private set; }

    public AraziCadImportWindow(DxfDocument document, string sourcePath)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _sourcePath = sourcePath;
        Title = "Arazi İçe Aktar — Birim ve Kot Kaynakları";
        Width = 940; Height = 720; MinWidth = 740; MinHeight = 540;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush.Parse("#060E0A");
        Foreground = Brush.Parse("#DDF5E5");

        // Katman önerileri yalnız ortak okuyucudan gelir; pencere ayrıca KOT veya POINT
        // katmanlarını ekleyerek mevcut zemin / proje kotu ayrımını bozmamalıdır.
        _layers = new List<KatmanSatiri>();

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        var header = new StackPanel { Margin = new Thickness(18, 14), Spacing = 6 };
        header.Children.Add(new TextBlock { Text = "DWG / DXF kotlarını incele", FontSize = 21, FontWeight = FontWeight.SemiBold });
        header.Children.Add(Label(Path.GetFileName(sourcePath)));
        header.Children.Add(Label("Yükleyeceğiniz arazi yüzeyini temsil eden katmanları seçin. Kot yazıları metre cinsinden okunur.", "#9FD3B4"));
        root.Children.Add(header);

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("300,*"), Margin = new Thickness(18, 0) };
        Grid.SetRow(body, 1); root.Children.Add(body);
        var layerPanel = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto"), Margin = new Thickness(0, 0, 14, 0) };
        layerPanel.Children.Add(_search);
        var layerButtons = new WrapPanel { Margin = new Thickness(0, 7, 0, 4) };
        var suggested = ActionButton("Önerilen"); var all = ActionButton("Tümü"); var none = ActionButton("Hiçbiri");
        suggested.Click += (_, _) => SelectLayers(null); all.Click += (_, _) => SelectLayers(true); none.Click += (_, _) => SelectLayers(false);
        layerButtons.Children.Add(suggested); layerButtons.Children.Add(all); layerButtons.Children.Add(none);
        Grid.SetRow(layerButtons, 1); layerPanel.Children.Add(layerButtons);
        _count.Margin = new Thickness(2, 3, 2, 7); Grid.SetRow(_count, 2); layerPanel.Children.Add(_count);
        _layerList.Background = Brush.Parse("#0A1610"); _layerList.BorderBrush = Brush.Parse("#234534");
        _layerList.BorderThickness = new Thickness(1);
        _layerList.ItemTemplate = new FuncDataTemplate<KatmanSatiri>((row, _) =>
        {
            var content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            var check = new CheckBox
            {
                Content = new TextBlock { Text = row!.Ad, MaxWidth = 200, TextTrimming = TextTrimming.CharacterEllipsis },
                Foreground = Brush.Parse("#DDF5E5"), MinHeight = 30, FontSize = 12
            };
            check.Bind(CheckBox.IsCheckedProperty, new Binding(nameof(KatmanSatiri.Secili)) { Mode = BindingMode.TwoWay });
            ToolTip.SetTip(check, row.Ad);
            content.Children.Add(check);
            var amount = Label(row.Adet.ToString("N0"), "#6FB78E"); amount.VerticalAlignment = VerticalAlignment.Center;
            amount.Margin = new Thickness(6, 0, 0, 0); Grid.SetColumn(amount, 1); content.Children.Add(amount);
            return content;
        });
        Grid.SetRow(_layerList, 3); layerPanel.Children.Add(_layerList);
        var hint = Label("CAD üçgenlerini ve sınırlarını korumak için yalnız yüzey katmanını seçin. Eşyükselti eğrilerinden yüzey oluşturmak için ilgili kontur katmanlarını seçin.", "#8EB69E");
        hint.Margin = new Thickness(2, 9, 2, 0); Grid.SetRow(hint, 4); layerPanel.Children.Add(hint); body.Children.Add(layerPanel);

        var right = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*") };
        Grid.SetColumn(right, 1); body.Children.Add(right);
        var options = new StackPanel { Spacing = 8 };
        options.Children.Add(Label($"Dosyada tanımlı koordinat birimi: {UnitName(document.DrawingVariables.InsUnits)}", "#7BD3A0"));
        var choices = new List<BirimSecimi>
        {
            new("Dosyadaki birimi kullan", null), new("Metre — 1 birim = 1 m", 1),
            new("Santimetre — 1 birim = 0,01 m", .01), new("Milimetre — 1 birim = 0,001 m", .001)
        };
        _units.ItemsSource = choices;
        _units.SelectedIndex = document.DrawingVariables.InsUnits == DrawingUnits.Unitless ? -1 : 0;
        _units.PlaceholderText = "Birim tanımsız: metre / cm / mm seçin";
        options.Children.Add(_units);
        if (document.DrawingVariables.InsUnits == DrawingUnits.Unitless)
            options.Children.Add(Label("Bu çizimin birimi tanımlanmamış. Araziyi oluşturmadan önce koordinat birimini seçin.", "#E7C16D"));
        _readText.Foreground = Brush.Parse("#DDF5E5"); options.Children.Add(_readText);
        var toleranceRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var toleranceLabel = Label("Nokta / yazı eşleme mesafesi (m):"); toleranceLabel.VerticalAlignment = VerticalAlignment.Center;
        toleranceRow.Children.Add(toleranceLabel); toleranceRow.Children.Add(_tolerance); options.Children.Add(toleranceRow);
        right.Children.Add(options);
        var previewLabel = Label("Okunan kotlar ve kontrol sonucu", "#7BD3A0"); previewLabel.FontWeight = FontWeight.SemiBold;
        previewLabel.Margin = new Thickness(0, 14, 0, 8); Grid.SetRow(previewLabel, 1); right.Children.Add(previewLabel);
        Grid.SetRow(_preview, 2); right.Children.Add(_preview);

        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Margin = new Thickness(18, 12, 14, 14) };
        _status.VerticalAlignment = VerticalAlignment.Center; _status.Margin = new Thickness(0, 0, 12, 0); footer.Children.Add(_status);
        var cancel = ActionButton("Vazgeç"); cancel.Click += (_, _) => Close(null); Grid.SetColumn(cancel, 1); footer.Children.Add(cancel);
        _create.IsEnabled = false; _create.Click += (_, _) => { if (_result is not null && !_busy) Close(_result); };
        Grid.SetColumn(_create, 2); footer.Children.Add(_create); Grid.SetRow(footer, 2); root.Children.Add(footer); Content = root;

        _search.TextChanged += (_, _) => FilterLayers();
        _units.SelectionChanged += (_, _) => SchedulePreview();
        _readText.IsCheckedChanged += (_, _) => SchedulePreview();
        _tolerance.TextChanged += (_, _) => SchedulePreview();
        _timer.Tick += async (_, _) => { _timer.Stop(); await UpdatePreviewAsync(); };
        Opened += async (_, _) => { FitToScreen(); await LoadLayersAsync(); };
        Closed += (_, _) => { _closed = true; _timer.Stop(); foreach (var row in _layers) row.PropertyChanged -= LayerChanged; };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(null); e.Handled = true; } };
        FilterLayers();
    }

    private void FilterLayers()
    {
        var search = _search.Text?.Trim() ?? "";
        _layerList.ItemsSource = _layers.Where(r => search.Length == 0 || r.Ad.Contains(search, StringComparison.CurrentCultureIgnoreCase)).ToList();
    }

    private async Task LoadLayersAsync()
    {
        _status.Text = "Bloklar ve arazi katmanları okunuyor…";
        try
        {
            // Boş seçim yalnız envanteri okur; blok içindeki etkin katmanlar da listelenir.
            var inventory = await Task.Run(() => new DxfAraziParser
                { Katmanlar = new HashSet<string>(StringComparer.OrdinalIgnoreCase) }.Parse(_document));
            if (_closed) return;
            foreach (var row in _layers) row.PropertyChanged -= LayerChanged;
            _layers.Clear();
            foreach (var layer in inventory.Katmanlar.OrderByDescending(k => k.AraziKatmaniOnerisi).ThenBy(k => k.Ad, StringComparer.CurrentCultureIgnoreCase))
            {
                var row = new KatmanSatiri(layer.Ad, layer.ElemanSayisi, layer.AraziKatmaniOnerisi);
                row.PropertyChanged += LayerChanged; _layers.Add(row);
            }
            if (inventory.BirimOnayiGerekiyor && _units.SelectedIndex == 0) _units.SelectedIndex = -1;
            FilterLayers();
        }
        catch (Exception ex)
        {
            if (!_closed) _preview.Text = "Katman envanteri okunamadı: " + ex.Message;
        }
        finally { _loading = false; if (!_closed) SchedulePreview(); }
    }

    private void SelectLayers(bool? selected)
    {
        _bulk = true;
        foreach (var row in _layers) row.Secili = selected ?? row.Onerilen;
        _bulk = false; SchedulePreview();
    }

    private void LayerChanged(object? sender, PropertyChangedEventArgs e) { if (!_bulk) SchedulePreview(); }

    private void SchedulePreview()
    {
        if (_closed) return;
        _revision++; _result = null; Yuzey = null; _create.IsEnabled = false;
        _count.Text = $"{_layers.Count(r => r.Secili)} / {_layers.Count} katman seçili";
        if (_loading) return;
        _status.Text = "Kot kaynakları kontrol ediliyor…";
        _timer.Stop(); _timer.Start();
    }

    private async Task UpdatePreviewAsync()
    {
        if (_closed || _busy || _loading) return;
        if (_units.SelectedItem is not BirimSecimi unit ||
            (unit.Scale is null && _document.DrawingVariables.InsUnits == DrawingUnits.Unitless))
        {
            _preview.Text = "Koordinat birimi seçilmedi. Kot yazıları metre kabul edilir; geometrik X, Y ve Z değerleri seçiminize göre çevrilir.";
            _status.Text = "Devam etmek için metre, santimetre veya milimetre seçin."; return;
        }
        if (!double.TryParse((_tolerance.Text ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double tolerance)
            || !double.IsFinite(tolerance) || tolerance <= 0)
        {
            _status.Text = "Eşleme mesafesi pozitif ve sonlu bir sayı olmalıdır."; _preview.Text = "Eşleme mesafesini düzeltin."; return;
        }
        var selected = _layers.Where(r => r.Secili).Select(r => r.Ad).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (selected.Count == 0)
        {
            _preview.Text = "Arazi noktalarını, eşyükselti eğrilerini ve ilgili kot yazılarını içeren katmanları seçin.";
            _status.Text = "En az bir katman seçin."; return;
        }
        bool readText = _readText.IsChecked == true;
        int version = _revision; _busy = true;
        try
        {
            var result = await Task.Run(() =>
            {
                var parser = new DxfAraziParser
                {
                    MetrePerBirim = unit.Scale, Katmanlar = selected,
                    MetinKotlariniOku = readText, EtiketEslesmeToleransiM = tolerance
                };
                var imported = parser.Parse(_document);
                if (imported.BirimOnayiGerekiyor)
                    throw new InvalidOperationException("Dosyanın koordinat birimi desteklenmiyor. Metre, santimetre veya milimetre seçin.");
                imported.Arazi.DosyaYolu = Path.GetFullPath(_sourcePath);
                if (imported.Arazi.Kaynak is not null)
                    imported.Arazi.Kaynak.DosyaYolu = Path.GetFullPath(_sourcePath);
                imported.Arazi.AraziAdi = Path.GetFileNameWithoutExtension(_sourcePath);
                var surface = new AraziYuzeyServisi().Olustur(imported.Arazi);
                return (imported, surface);
            });
            if (_closed || version != _revision) return;
            var text = new StringBuilder();
            text.AppendLine($"Kaynak: {Path.GetFileName(_sourcePath)}");
            text.AppendLine($"Seçili katman: {selected.Count:N0}");
            text.AppendLine(string.Join(", ", selected.OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase).Take(20)) + (selected.Count > 20 ? "…" : ""));
            text.AppendLine($"Koordinat ölçeği: 1 birim = {result.imported.UygulananOlcek:G} m");
            text.AppendLine($"Arazi noktası: {result.imported.Arazi.Noktalar.Count:N0}");
            text.AppendLine($"Nokta kot kaynağı — geometri: {result.imported.GeometrikKotSayisi:N0} / yazı: {result.imported.MetindenKotSayisi:N0}");
            text.AppendLine($"Kontur: {result.imported.Arazi.Konturlar.Count:N0}");
            text.AppendLine($"Kotu çözülemeyen kontur: {result.imported.KotsuzKonturSayisi:N0}");
            text.AppendLine($"Yüzey: {result.surface.Dugumler.Count:N0} düğüm / {result.surface.Ucgenler.Count:N0} üçgen");
            if (result.imported.Arazi.KaynakUcgenleri != null) text.AppendLine("Yüzey sınırları ve bağlantıları CAD üçgenlerinden alındı.");
            if (result.surface.Dugumler.Count > 0)
            {
                text.AppendLine($"En düşük kot: {result.surface.Dugumler.Min(p => p.Z):N3} m");
                text.AppendLine($"En yüksek kot: {result.surface.Dugumler.Max(p => p.Z):N3} m");
                text.AppendLine($"X aralığı: {result.surface.Dugumler.Min(p => p.X):N2} – {result.surface.Dugumler.Max(p => p.X):N2} m");
                text.AppendLine($"Y aralığı: {result.surface.Dugumler.Min(p => p.Y):N2} – {result.surface.Dugumler.Max(p => p.Y):N2} m");
            }
            if (result.imported.Uyarilar.Count > 0)
            {
                text.AppendLine(); text.AppendLine("KONTROL NOTLARI");
                foreach (string warning in result.imported.Uyarilar) text.AppendLine("• " + warning);
            }
            if (result.surface.Ucgenler.Count == 0)
                text.AppendLine("Yüzey üretilemedi. Farklı konumlarda, aynı doğru üzerinde olmayan en az üç geçerli kot gerekir.");
            _preview.Text = text.ToString();
            _result = result.surface.Ucgenler.Count > 0 ? result.imported : null;
            Yuzey = _result is not null ? result.surface : null;
            _create.IsEnabled = _result is not null;
            _status.Text = _result is not null ? "Kot aralığını kontrol edin; seçili verilerden arazi oluşturulacak." : "Yüzey için yeterli kot yok; katman ve okuma seçeneklerini kontrol edin.";
        }
        catch (Exception ex)
        {
            if (_closed || version != _revision) return;
            _result = null; Yuzey = null; _create.IsEnabled = false;
            _preview.Text = "Arazi oluşturulamadı:\n" + ex.Message;
            _status.Text = "Kot kaynakları kontrol edilmeli.";
        }
        finally
        {
            _busy = false;
            if (!_closed && version != _revision) { _timer.Stop(); _timer.Start(); }
        }
    }

    private void FitToScreen()
    {
        var screen = (Owner is not null ? Screens.ScreenFromWindow(Owner) : null) ?? Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null) return;
        double scale = screen.Scaling > 0 ? screen.Scaling : 1;
        double width = Math.Max(320, screen.WorkingArea.Width / scale - 24);
        double height = Math.Max(280, screen.WorkingArea.Height / scale - 64);
        MinWidth = Math.Min(MinWidth, width); MinHeight = Math.Min(MinHeight, height);
        Width = Math.Min(Width, width); Height = Math.Min(Height, height);
    }

    private static string UnitName(DrawingUnits unit) => unit switch
    {
        DrawingUnits.Unitless => "Tanımsız", DrawingUnits.Meters => "Metre", DrawingUnits.Centimeters => "Santimetre",
        DrawingUnits.Millimeters => "Milimetre", _ => unit.ToString()
    };

    private static TextBlock Label(string text, string color = "#B8DAC6") => new()
        { Text = text, Foreground = Brush.Parse(color), FontSize = 12, TextWrapping = TextWrapping.Wrap };

    private static Button ActionButton(string text, bool primary = false) => new()
    {
        Content = text, Padding = new Thickness(10, 7), Margin = new Thickness(0, 0, 5, 3), CornerRadius = new CornerRadius(5),
        Background = Brush.Parse(primary ? "#2E7D4F" : "#122C1D"), BorderBrush = Brush.Parse("#315A40"), Foreground = Brush.Parse("#DDF5E5"), FontSize = 12
    };

    private sealed record BirimSecimi(string Label, double? Scale) { public override string ToString() => Label; }

    private sealed class KatmanSatiri : INotifyPropertyChanged
    {
        public string Ad { get; }
        public int Adet { get; }
        public bool Onerilen { get; }
        private bool _selected;
        public bool Secili { get => _selected; set { if (_selected == value) return; _selected = value; Changed(); } }
        public KatmanSatiri(string name, int count, bool suggested) { Ad = name; Adet = count; Onerilen = suggested; _selected = suggested; }
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
