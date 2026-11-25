using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Kst.Acoustics.App.Controls;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Salon.Models;
using Kst.Acoustics.Core.Salon.Services;

namespace Kst.Acoustics.App.Salon.Views;

public partial class SalonTasarlaWindow
{
    private readonly StackPanel _ozellikler = new() { Spacing = 8, Margin = new Thickness(12) };
    private readonly ListBox _nesneler = new() { MinHeight = 100, MaxHeight = 180 };
    private readonly ListBox _sorunlar = new() { MaxHeight = 160 };
    private readonly TextBlock _bilgi = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(10), Foreground = Brushes.LightBlue };
    private readonly GeometryPreviewControl _canli3D = new() { Projection = GeometryProjectionMode.Isometric3D, UnitLabel = "m" };
    private readonly SalonKesitCanvas _kesit = new();

    private readonly DispatcherTimer _yenileTimer = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private TabControl _gorunum = null!;
    private SalonIslevi _cizimIslevi = SalonIslevi.Sinir;
    private bool _secimEsleniyor;
    private string _sonIcerik = "";
    private readonly List<SalonKatman> _izlenenKatmanlar = new();

    private Button Btn(string label, Action action, string? tip = null)
    {
        var b = new Button { Content = label, Margin = new Thickness(3), Padding = new Thickness(10, 6) };
        if (tip != null) ToolTip.SetTip(b, tip);
        b.Click += (_, _) => Guvenli(action); return b;
    }
    private void Guvenli(Action action)
    { try { action(); } catch (Exception ex) { _bilgi.Text = ex.Message; } }
    private MenuItem Item(string text, Action action)
    { var item = new MenuItem { Header = text }; item.Click += (_, _) => Guvenli(action); return item; }
    private Menu Menu(string title, params MenuItem[] items)
        => new() { ItemsSource = new[] { new MenuItem { Header = title, ItemsSource = items } } };

    private void TasarimArayuzuKur()
    {
        _nesneler.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<SalonCizimOgesi>((o, _) => new TextBlock { [!TextBlock.TextProperty] = new Avalonia.Data.Binding(nameof(SalonCizimOgesi.Ad)), TextTrimming = TextTrimming.CharacterEllipsis });
        var canvas = CizimCanvas;
        if (canvas.Parent is Panel parent) parent.Children.Remove(canvas);
        var root = new Grid { RowDefinitions = new("Auto,*,Auto") };
        var toolbar = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8) };
        toolbar.Children.Add(Menu("Dosya", Item("Yeni", () => Yeni_OnClick(null, new())), Item("Aç…", () => Ac_OnClick(null, new())), Item("Kaydet", () => { Focus(); _ = Kaydet(); }), Item("Farklı kaydet…", () => { Focus(); _ = FarkliKaydet(); }), Item("Hazır salon…", () => Sablon_OnClick(null, new())), Item("Geri", () => Geri_OnClick(null, new()))));
        toolbar.Children.Add(Btn("↶", GeriAl, "Geri al · Ctrl+Z")); toolbar.Children.Add(Btn("↷", Yinele, "Yinele · Ctrl+Y"));
        toolbar.Children.Add(Btn("Seç / Düzenle", () => CizimAraci(SalonAraci.Sec), "Nesneyi seçip sürükleyin. Köşe tutamağıyla biçim verin; Shift+kenar ile paralel taşıyın."));
        toolbar.Children.Add(Btn("Salon sınırı", () => CizmeyeBasla(SalonIslevi.Sinir)));
        toolbar.Children.Add(Menu("Bölge", new[] { SalonIslevi.Sahne, SalonIslevi.Seyirci, SalonIslevi.Balkon, SalonIslevi.Cukur, SalonIslevi.Bosluk, SalonIslevi.Koridor, SalonIslevi.Tavan }.Select(r => Item(SalonTasarimServisi.Ad(r), () => CizmeyeBasla(r))).ToArray()));
        toolbar.Children.Add(Btn("Kapı / Pencere", AciklikPaneli));
        toolbar.Children.Add(Btn("Ölçü", () => CizimAraci(SalonAraci.Olcum)));
        toolbar.Children.Add(Menu("Şekil", Item("Serbest sınır", () => CizimAraci(SalonAraci.Polyline)), Item("Dikdörtgen", () => CizimAraci(SalonAraci.Dikdortgen)), Item("Daire", () => CizimAraci(SalonAraci.Daire)), Item("Düz / yay kenar (A)", () => { canvas.SinirKavisModu = !canvas.SinirKavisModu; _bilgi.Text = canvas.SinirKavisModu ? "Yay: bitiş ve kavis noktası seçin." : "Düz kenar."; }), Item("Sınırı tamamla", canvas.SiniriKapat)));
        toolbar.Children.Add(Menu("Düzenle", Item("Taşı", () => CizimAraci(SalonAraci.Tasi)), Item("Kopyala", () => canvas.SecimiTasi(1, 1, true)), Item("Döndür", () => CizimAraci(SalonAraci.Dondur)), Item("Aynala", () => CizimAraci(SalonAraci.Aynala)), Item("Birleştir", () => SecimBirlestir(false)), Item("Son seçileni çıkar", () => SecimBirlestir(true)), Item("Çizgi / yayları sınır yap", SecimSinirYap), Item("Sil", canvas.SeciliyiSil)));
        toolbar.Children.Add(Menu("Gelişmiş", Item("Trim", () => CizimAraci(SalonAraci.Trim)), Item("Uzat", () => CizimAraci(SalonAraci.Uzat)), Item("Paralel kopya", () => CizimAraci(SalonAraci.Offset)), Item("Yardımcı çizgi", () => { _cizimIslevi = SalonIslevi.Yardimci; CizimAraci(SalonAraci.Cizgi); }), Item("İç duvar", () => { _cizimIslevi = SalonIslevi.Duvar; CizimAraci(SalonAraci.Cizgi); }), Item("Katmanlar / çıktı kalitesi", KatmanPaneli)));
        toolbar.Children.Add(Btn("Kesit çiz", () => CizimAraci(SalonAraci.Kesit)));
        toolbar.Children.Add(Btn("Kontrol", () => { YenileModel(); _sorunlar.IsVisible = true; }));
        toolbar.Children.Add(Btn("DXF aktar", () => DxfDisaAktar_OnClick(null, new())));
        toolbar.Children.Add(Btn("Sığdır", canvas.Sigdir));
        root.Children.Add(toolbar);
        var body = new Grid { ColumnDefinitions = new("*,300") }; Grid.SetRow(body, 1); root.Children.Add(body);
        _gorunum = new TabControl { ItemsSource = new[] { new TabItem { Header = "Plan", Content = canvas }, new TabItem { Header = "Kesit", Content = _kesit }, new TabItem { Header = "3D", Content = _canli3D } } }; body.Children.Add(_gorunum);
        var sidebar = new DockPanel { Background = Brush.Parse("#101D29") }; Grid.SetColumn(sidebar, 1); body.Children.Add(sidebar);
        var treePanel = new StackPanel { Spacing = 5, Margin = new Thickness(10) }; treePanel.Children.Add(new TextBlock { Text = "NESNELER", Foreground = Brushes.LightBlue }); treePanel.Children.Add(_nesneler); treePanel.Children.Add(_sorunlar); DockPanel.SetDock(treePanel, Dock.Top); sidebar.Children.Add(treePanel);
        sidebar.Children.Add(new ScrollViewer { Content = _ozellikler, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        var bottom = new DockPanel(); Grid.SetRow(bottom, 2); root.Children.Add(bottom);
        var snaps = new WrapPanel { Margin = new Thickness(6) };
        var snap = Btn("", () => canvas.SnapToggle()); var grid = Btn("", () => canvas.GridToggle()); var ortho = Btn("", () => canvas.OrthoToggle()); var polar = Btn("", () => canvas.PolarToggle());
        foreach (var button in new[] { snap, grid, ortho, polar }) { button.Padding = new Thickness(7, 5); snaps.Children.Add(button); }
        void SnapDurumu() { snap.Content = $"Yakalama {(canvas.NesneYakalama ? "✓" : "–")} F3"; grid.Content = $"Izgara {(canvas.IzgaraYakalama ? "✓" : "–")} F9"; ortho.Content = $"Dik {(canvas.Ortho ? "✓" : "–")} F8"; polar.Content = $"Açı {(canvas.AciYakalama ? "✓" : "–")} F10"; }
        canvas.YakalamaDegisti = SnapDurumu; SnapDurumu();
        DockPanel.SetDock(snaps, Dock.Right); bottom.Children.Add(snaps); bottom.Children.Add(_bilgi);
        TasarimHost.Children.Add(root);
        _nesneler.SelectionChanged += (_, _) => { if (_secimEsleniyor) return; canvas.Sec(_nesneler.SelectedItem as SalonCizimOgesi); };
        _sorunlar.SelectionChanged += (_, _) => { if (_sorunlar.SelectedItem is SalonSorunu issue) { _gorunum.SelectedIndex = 0; canvas.Sec(_vm.Salon.CizimOgeleri.FirstOrDefault(o => o.Id == issue.NesneId)); canvas.Odaklan(canvas.SeciliOge); } };
        _yenileTimer.Tick += (_, _) => { _yenileTimer.Stop(); YenileModel(); };
        _kesit.Degisecek = SnapshotAl; _kesit.Degisti = () => { _vm.IsDirty = true; YenileModel(); };
        _kesit.NoktaSecildi = i =>
        {
            _ozellikler.Children.Clear(); Baslik("Kesit tavan noktası");
            Sayi("Kesitte mesafe (m)", _kesit.Profil[i].X, x => _kesit.NoktaDuzenle(i, x, _kesit.Profil[i].Y), 0, 10000);
            Sayi("Tavan kotu (m)", _kesit.Profil[i].Y, z => _kesit.NoktaDuzenle(i, _kesit.Profil[i].X, z), .1, 500);
        };
        Closed += (_, _) => _yenileTimer.Stop();
    }

    private void TasarimBagla()
    {
        if (_gorunum == null) return;
        var c = CizimCanvas;
        _nesneler.ItemsSource = _vm.Salon.CizimOgeleri;
        foreach (var k in _izlenenKatmanlar) k.PropertyChanged -= KatmanGorunumDegisti;
        _izlenenKatmanlar.Clear(); _izlenenKatmanlar.AddRange(_vm.Salon.Katmanlar);
        foreach (var k in _izlenenKatmanlar) k.PropertyChanged += KatmanGorunumDegisti;
        c.OnDegisti = () => { _vm.IsDirty = true; SalonTasarimServisi.BaglariGuncelle(_vm.Salon); _yenileTimer.Stop(); _yenileTimer.Start(); };
        c.OnSecimDegisti = items => { _secimEsleniyor = true; _nesneler.SelectedItem = items.LastOrDefault(); _secimEsleniyor = false; OzellikPaneli(items.LastOrDefault()); };
        c.OnDurum = m => { _vm.DurumMesaji = m; _bilgi.Text = m; };
        c.OgeHazirla = o => { o.OrtakSiniraDonustur(); o.Islev = _cizimIslevi; o.Ad = $"{SalonTasarimServisi.Ad(o.Islev)} {_vm.Salon.CizimOgeleri.Count + 1}"; o.KatmanId = _vm.Salon.Katmanlar.FirstOrDefault(k => k.Tip == SalonTasarimServisi.KatmanTipi(o.Islev))?.Id ?? ""; o.KotM = o.Islev == SalonIslevi.Balkon ? 4 : o.Islev == SalonIslevi.Tavan ? _vm.Salon.SalonYuksekligiM : 0; o.KotReferansi = SalonCizimCanvas.Merkez(o); };
        c.KesitSecildi = (a, b) => { _kesit.A = a; _kesit.B = b; _kesit.Salon = _vm.Salon; _kesit.ProfiliSifirla(); _gorunum.SelectedIndex = 1; _bilgi.Text = "Kesit: tavana çift tıklayarak kırılım ekleyin, turuncu noktaları sürükleyerek kot verin."; };
        c.OnAciklikYerlestir = AciklikYerlestir;
        OzellikPaneli(null); _sonIcerik = ""; _yenileTimer.Start();
    }
    private void KatmanGorunumDegisti(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    { if (e.PropertyName == nameof(SalonKatman.Gorunur)) { _vm.IsDirty = true; CizimCanvas.InvalidateVisual(); } }
    private void CizimAraci(SalonAraci a) { _gorunum.SelectedIndex = 0; CizimCanvas.Arac = a; CizimCanvas.Focus(); }
    private void CizmeyeBasla(SalonIslevi r) { _cizimIslevi = r; CizimAraci(SalonAraci.Polyline); _bilgi.Text = $"{SalonTasarimServisi.Ad(r)}: noktaları çizin; A ile yay, Enter ile kapatın."; }
    private void Degistir(Action action)
    {
        SnapshotAl();
        try { action(); SalonTasarimServisi.BaglariGuncelle(_vm.Salon); _vm.IsDirty = _gecmis.Kirli(_vm.Salon); _vm.BasligiYenile(); CizimCanvas.InvalidateVisual(); _yenileTimer.Stop(); _yenileTimer.Start(); }
        catch (Exception ex) { if (_gecmis.Vazgec(_vm.Salon) is { } restored) SalonuGeriYukle(restored); _bilgi.Text = ex.Message; }
    }
    private void Baslik(string s) => _ozellikler.Children.Add(new TextBlock { Text = s, FontSize = 15, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
    private void Metin(string label, string value, Action<string> setter)
    {
        _ozellikler.Children.Add(new TextBlock { Text = label, Foreground = Brushes.LightBlue }); var box = new TextBox { Text = value }; _ozellikler.Children.Add(box);
        box.LostFocus += (_, _) => { if (box.Text != value) { var next = box.Text ?? ""; Degistir(() => setter(next)); value = next; } };
    }
    private void Sayi(string label, double value, Action<double> setter, double min = -100000, double max = 100000, bool history = true)
    {
        _ozellikler.Children.Add(new TextBlock { Text = label, Foreground = Brushes.LightBlue }); var box = new TextBox { Text = value.ToString("0.###", CultureInfo.CurrentCulture) }; _ozellikler.Children.Add(box);
        void Apply() { if (!double.TryParse(box.Text?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var x) || !double.IsFinite(x) || x < min || x > max) { box.BorderBrush = Brushes.OrangeRed; _bilgi.Text = $"{label}: {min} ile {max} arasında bir sayı girin."; return; } box.ClearValue(TextBox.BorderBrushProperty); if (Math.Abs(x - value) > 1e-9) { if (history) Degistir(() => setter(x)); else setter(x); value = x; } }
        box.LostFocus += (_, _) => Apply(); box.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { Apply(); e.Handled = true; } };
    }
    private void Secenek<T>(string label, IEnumerable<T> items, T current, Action<T> setter, bool history = true)
    {
        _ozellikler.Children.Add(new TextBlock { Text = label, Foreground = Brushes.LightBlue }); var combo = new ComboBox { ItemsSource = items.ToList(), SelectedItem = current, HorizontalAlignment = HorizontalAlignment.Stretch }; _ozellikler.Children.Add(combo);
        combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is T v) { if (history) Degistir(() => setter(v)); else setter(v); } };
    }
    private void Onay(string label, bool val, Action<bool> setter, bool history = true) { var c = new CheckBox { Content = label, IsChecked = val }; _ozellikler.Children.Add(c); c.IsCheckedChanged += (_, _) => { if (history) Degistir(() => setter(c.IsChecked == true)); else setter(c.IsChecked == true); }; }

    private void OzellikPaneli(SalonCizimOgesi? o)
    {
        _ozellikler.Children.Clear();
        if (o == null) { Baslik("Salon"); Metin("Salon adı", _vm.Salon.SalonAdi, x => _vm.Salon.SalonAdi = x); Sayi("Genel tavan kotu (m)", _vm.Salon.SalonYuksekligiM, x => _vm.Salon.SalonYuksekligiM = x, .1, 500); _ozellikler.Children.Add(new TextBlock { Text = "Sınır çizin; bölge ekleyin; seçip biçim verin. Shift+tık çoklu seçim. Shift+kenar paralel taşıma. Kenara çift tık köşe ekleme.", TextWrapping = TextWrapping.Wrap }); return; }
        Baslik(o.Ad); Metin("Ad", o.Ad, x => o.Ad = x);
        var roles = Enum.GetValues<SalonIslevi>().Where(r => r != SalonIslevi.Eski).Select(r => new RolSecimi(r)).ToList();
        Secenek("İşlev", roles, roles.First(r => r.Rol == o.Islev), x => { o.Islev = x.Rol; Dispatcher.UIThread.Post(() => OzellikPaneli(o)); o.KatmanId = _vm.Salon.Katmanlar.FirstOrDefault(k => k.Tip == SalonTasarimServisi.KatmanTipi(o.Islev))?.Id ?? ""; });
        Onay("Hesaba / çıktıya dahil", o.CiktiyaDahil, x => o.CiktiyaDahil = x);
        if (o.Islev is SalonIslevi.Sahne or SalonIslevi.Balkon or SalonIslevi.Seyirci or SalonIslevi.Tavan) Sayi("Kot (m)", o.KotM, x => o.KotM = x);
        if (o.Islev is SalonIslevi.Sahne or SalonIslevi.Cukur) Sayi(o.Islev == SalonIslevi.Cukur ? "Çukur derinliği (m)" : "Yükselme (m)", o.YukseklikM, x => o.YukseklikM = x, 0, 200);
        if (o.Islev == SalonIslevi.Balkon) { Sayi("Döşeme kalınlığı (m)", o.KalinlikM, x => o.KalinlikM = x, 0, 10); Sayi("Parapet (m)", o.ParapetM, x => o.ParapetM = x, 0, 10); }
        if (o.Islev == SalonIslevi.Seyirci)
        {
            Secenek("Zemin", Enum.GetValues<SalonZeminBicimi>(), o.ZeminBicimi, x => o.ZeminBicimi = x);
            _ozellikler.Children.Add(Btn("Sahneye yönelt", () => { var stage = _vm.Salon.CizimOgeleri.FirstOrDefault(s => s.Islev == SalonIslevi.Sahne && s.CiktiyaDahil); if (stage == null) { _bilgi.Text = "Önce bir sahne ekleyin."; return; } Degistir(() => { var target = SalonCizimCanvas.Merkez(stage); var center = SalonCizimCanvas.Merkez(o); o.YonDerece = Math.Atan2(center.Y - target.Y, center.X - target.X) * 180 / Math.PI; o.SiraMerkezi = target; }); OzellikPaneli(o); }));
            Sayi("Yükselme yönü (°)", o.YonDerece, x => o.YonDerece = x, -360, 360); Sayi("Kademe sayısı", o.KademeSayisi, x => o.KademeSayisi = (int)Math.Round(x), 1, 500); Sayi("Kademe yükselmesi (m)", o.KademeYuksekligiM, x => o.KademeYuksekligiM = x, 0, 5);
            Sayi("Sıra aralığı (m)", o.SiraAraligiM, x => o.SiraAraligiM = x, .3, 5); Sayi("Koltuk genişliği (m)", o.KoltukGenisligiM, x => o.KoltukGenisligiM = x, .25, 2); Onay("Yaylı koltuk sıraları", o.YayliSiralar, x => o.YayliSiralar = x);
            Sayi("Sıra merkezi X", o.SiraMerkezi.X, x => o.SiraMerkezi = new(x, o.SiraMerkezi.Y)); Sayi("Sıra merkezi Y", o.SiraMerkezi.Y, x => o.SiraMerkezi = new(o.SiraMerkezi.X, x));
            var parents = new List<BagSecimi> { new(null, "Bağımsız") }; parents.AddRange(_vm.Salon.CizimOgeleri.Where(b => b.Islev == SalonIslevi.Balkon).Select(b => new BagSecimi(b.Id, b.Ad))); Secenek("Üzerinde bulunduğu balkon", parents, parents.FirstOrDefault(b => b.Id == o.UstNesneId) ?? parents[0], b => { o.UstNesneId = b.Id; SalonTasarimServisi.BaglariGuncelle(_vm.Salon); });
        }
        if (o.Islev == SalonIslevi.Tavan) { Sayi("X yönünde eğim (%)", o.EgimX * 100, x => o.EgimX = x / 100, -500, 500); Sayi("Y yönünde eğim (%)", o.EgimY * 100, x => o.EgimY = x / 100, -500, 500); }
        var ops = new WrapPanel(); ops.Children.Add(Btn("Kopyala", () => CizimCanvas.SecimiTasi(1, 1, true))); ops.Children.Add(Btn("Döndür", () => CizimAraci(SalonAraci.Dondur))); ops.Children.Add(Btn("Sil", CizimCanvas.SeciliyiSil)); _ozellikler.Children.Add(ops);
        double rotation = 0;
        Sayi("Döndür (°)", 0, x => { CizimCanvas.SecimiDondur(x - rotation); rotation = x; }, -360, 360, false);
        if (o.Tip is SalonCizimTipi.Polyline or SalonCizimTipi.Dikdortgen) KenarPaneli(o);
    }
    private sealed record RolSecimi(SalonIslevi Rol) { public override string ToString() => SalonTasarimServisi.Ad(Rol); }
    private sealed record BagSecimi(string? Id, string Ad) { public override string ToString() => Ad; }
    private void KenarPaneli(SalonCizimOgesi o, int edge = 0)
    {
        o.KenarlariHazirla(); int count = o.Kapali ? o.Noktalar.Count : o.Noktalar.Count - 1; if (count < 1) return;
        int start = _ozellikler.Children.Count;
        Secenek("Düzenlenecek kenar", Enumerable.Range(1, count), edge + 1, v =>
        {
            while (_ozellikler.Children.Count > start) _ozellikler.Children.RemoveAt(start);
            KenarPaneli(o, v - 1);
        }, false);
        var next = (edge + 1) % o.Noktalar.Count;
        Sayi("Kenar kiriş uzunluğu (m)", Mesafe(o.Noktalar[edge], o.Noktalar[next]), v => { var a = o.Noktalar[edge]; var b = o.Noktalar[next]; double len = Mesafe(a, b); if (len > 1e-8) o.Noktalar[next] = new(a.X + (b.X - a.X) * v / len, a.Y + (b.Y - a.Y) * v / len); }, .001, 10000);
        Sayi("Kenar yay açısı (°); 0 = düz", Math.Atan(o.KenarKavisleri[edge]) * 720 / Math.PI, v => { o.Tip = SalonCizimTipi.Polyline; o.KenarKavisleri[edge] = Math.Tan(v * Math.PI / 720); }, -350, 350);
        _ozellikler.Children.Add(Btn("Köşeyi pahla / yuvarla…", () => KosePaneli(o, edge)));
    }
    private static double Mesafe(Point2D a, Point2D b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
    private void KosePaneli(SalonCizimOgesi o, int index)
    { double distance = .5; bool round = true; _ozellikler.Children.Clear(); Baslik("Köşe düzenle"); Sayi("Köşeden mesafe (m)", distance, x => distance = x, .001, 100, false); Onay("Yuvarlat", round, x => round = x, false); _ozellikler.Children.Add(Btn("Uygula", () => { Degistir(() => SalonSinirDuzenleme.Kose(o, index, distance, round)); OzellikPaneli(o); })); }
    private void AciklikPaneli()
    {
        _ozellikler.Children.Clear(); Baslik("Kapı / Pencere");
        var selected = _vm.SeciliAciklik;
        if (selected != null) { _aciklikTip = selected.Tip; _aciklikGen = selected.GenislikM; _aciklikYuk = selected.YukseklikM; _aciklikKot = selected.AltKotM; }
        Secenek("Tür", new[] { "Kapı", "Pencere" }, _aciklikTip == AciklikTipi.Pencere ? "Pencere" : "Kapı", v => _aciklikTip = v == "Kapı" ? AciklikTipi.Kapi : AciklikTipi.Pencere, false);
        Sayi("Genişlik (m)", _aciklikGen, x => _aciklikGen = x, .1, 100, false); Sayi("Yükseklik (m)", _aciklikYuk, x => _aciklikYuk = x, .1, 100, false); Sayi("Alt kot (m)", _aciklikKot, x => _aciklikKot = x, 0, 100, false);
        _ozellikler.Children.Add(Btn("Duvara yerleştir", () => CizimAraci(SalonAraci.Aciklik)));
        var list = new ListBox { ItemsSource = _vm.Salon.Acikliklar, MaxHeight = 150, SelectedItem = selected };
        list.SelectionChanged += (_, _) => { _vm.SeciliAciklik = list.SelectedItem as DuvarAcikligi; AciklikPaneli(); }; _ozellikler.Children.Add(list);
        if (selected != null)
        {
            Sayi("Kenar üzerindeki konum (%)", selected.KenarOrani * 100, x => { selected.KenarOrani = x / 100; SalonTasarimServisi.BaglariGuncelle(_vm.Salon); }, 0, 100);
            _ozellikler.Children.Add(Btn("Ölçüleri uygula", () => { Degistir(() => { selected.Tip = _aciklikTip; selected.GenislikM = _aciklikGen; selected.YukseklikM = _aciklikYuk; selected.AltKotM = _aciklikKot; }); }));
            _ozellikler.Children.Add(Btn("Açıklığı sil", () => { Degistir(() => { _vm.Acikliklar.Remove(selected); _vm.SeciliAciklik = null; }); AciklikPaneli(); }));
        }
    }
    private void KatmanPaneli()
    {
        _ozellikler.Children.Clear(); Baslik("Görünüm ve çıktı kalitesi"); Sayi("Izgara adımı (m)", CizimCanvas.GridAdimM, x => CizimCanvas.GridAdimM = x, .01, 100); Sayi("Açı yakalama adımı (°)", CizimCanvas.PolarAdimDerece, x => CizimCanvas.PolarAdimDerece = x, 1, 90); Sayi("Eğri çıktı çözünürlüğü", _vm.Salon.FasetKenarSayisi, x => _vm.Salon.FasetKenarSayisi = (int)x, 12, 128);
        foreach (var k in _vm.Salon.Katmanlar) { Onay(k.Ad + " görünür", k.Gorunur, x => k.Gorunur = x); Metin("Kategori", k.Ad, x => k.Ad = x); }
    }
    private void SecimBirlestir(bool subtract)
    { var selection = CizimCanvas.Secim.ToList(); Degistir(() => SalonSinirDuzenleme.Birlestir(_vm.Salon, selection, subtract)); CizimCanvas.Sec(null); }
    private void SecimSinirYap() { var selection = CizimCanvas.Secim.ToList(); Degistir(() => SalonSinirDuzenleme.Bagla(_vm.Salon, selection)); CizimCanvas.Sec(null); }
    private void YenileModel()
    {
        try
        {
            _vm.IsDirty = _gecmis.Kirli(_vm.Salon); _vm.BasligiYenile();
            SalonTasarimServisi.BaglariGuncelle(_vm.Salon); _kesit.Salon = _vm.Salon; _kesit.InvalidateVisual();
            var issues = SalonTasarimDogrulama.Kontrol(_vm.Salon); _sorunlar.ItemsSource = issues;
            var json = SalonGeometriServisi.KopyaJson(_vm.Salon); if (json == _sonIcerik) return; _sonIcerik = json;
            if (issues.Any(i => i.Hata)) { CizimCanvas.Koltuklar.Clear(); CizimCanvas.InvalidateVisual(); _canli3D.Surfaces = new List<Kst.Acoustics.Core.Projects.ProjectSurface>(); _bilgi.Text = issues.First(i => i.Hata).Mesaj; return; }
            var mesh = SalonTasarimMeshServisi.Uret(_vm.Salon); _canli3D.Surfaces = mesh.Yuzeyler;

            var seatsByObject = _vm.Salon.CizimOgeleri.Where(o => o.Islev == SalonIslevi.Seyirci && o.CiktiyaDahil).ToDictionary(o => o.Id, o => SalonTasarimServisi.Koltuklar(_vm.Salon, o));
            CizimCanvas.Koltuklar = seatsByObject; CizimCanvas.InvalidateVisual(); int seats = seatsByObject.Values.Sum(v => v.Count);
            _bilgi.Text = $"{SalonTasarimServisi.Alan(SalonTasarimServisi.Kabuk(_vm.Salon)):0.##} m² · {mesh.Hacim:0.##} m³ · Yerleşen koltuk: {seats} · {issues.Count} uyarı";
        }
        catch (Exception ex) { _canli3D.Surfaces = new List<Kst.Acoustics.Core.Projects.ProjectSurface>(); _bilgi.Text = ex.Message; }
    }
}
