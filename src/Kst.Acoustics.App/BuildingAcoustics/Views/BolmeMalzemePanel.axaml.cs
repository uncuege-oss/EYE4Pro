using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Kst.Acoustics.Core.BuildingAcoustics.Calculation;
using Kst.Acoustics.Core.BuildingAcoustics.Database;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.Common.Geometry;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AciklikOnayar = Kst.Acoustics.Core.BuildingAcoustics.Models.AciklikOnayar;

namespace Kst.Acoustics.App.BuildingAcoustics.Views;

public partial class BolmeMalzemePanel : UserControl
{
    public Action<Bolme?>? OnBolmeSecildi { get; set; }
    /// Düşey birleşimde gösterilecek partner oda duvarlarını sağlayan kanca (VM tarafından bağlanır).
    public Func<Mekan, IEnumerable<Bolme>>? EkBolmeSaglayici { get; set; }
    private List<Bolme> _gosterilenBolmeler = new();
    public Action<YapiElemani?>? OnDosemeKataUygula { get; set; }
    public Action<BolmeAcikligi?>? OnAciklikDegisti { get; set; }
    /// Bölme yapı elemanı (duvar malzemesi) atandığında çağrılır — VM'in doğrulama panelini tazelemesi için.
    public Action? OnBolmeMalzemesiAtandi { get; set; }
    /// Junction (kenar bağlantı) ayarları değiştiğinde çağrılır — VM'in IsDirty işaretlemesi için.
    /// Junction/Menfez INotifyPropertyChanged uygulamadığından (List&lt;T&gt;), bu kanca olmadan
    /// değişiklik VM'e hiç yansımaz.
    public Action? OnJunctionDegisti { get; set; }

    private List<YapiElemani> _tumMalzemeler;
    private List<YapiElemani> _aciklikMalzemeleri = new();
    private Bolme? _secilenBolme;
    private YapiElemani? _secilenMalzeme;
    private BolmeAcikligi? _editingAciklik;
    private BolmeAcikligiTipi _editingTip;
    private bool _contextUpdating;
    private bool _onayarUpdating;
    private bool _junctionUpdating;
    private List<AciklikOnayar> _mevcutOnayarlar = new();

    public BolmeMalzemePanel()
    {
        InitializeComponent();
        _tumMalzemeler = LoadMaterials();
        MalzemeListesi.ItemsSource = _tumMalzemeler;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _secilenBolme = null;
        _editingAciklik = null;
        _tumMalzemeler = LoadMaterials();
        MalzemeListesi.ItemsSource = _tumMalzemeler;
        DosemeCombo.ItemsSource = _tumMalzemeler
            .Where(m => m.Tip == YapiElemaniTipi.Doseme)
            .ToList();
        OnBolmeSecildi?.Invoke(null);
        SeciliBolmeLabel.Text = "";
        MevcutMalzemeLabel.Text = "";
        AciklikBolumu.IsVisible = false;
        JunctionBolumu.IsVisible = false;
        JunctionStackPanel.Children.Clear();
        JunctionOzetLabel.Text = "";
        AciklikForm.IsVisible = false;
        AciklikStackPanel.Children.Clear();

        _contextUpdating = true;
        if (DataContext is Mekan mekan)
        {
            // Düşey birleşimde: bu odanın + partner oda(lar)ın duvarları birlikte gösterilir,
            // böylece birleşik hacmin tüm duvarları tek panelden atanabilir.
            _gosterilenBolmeler = new List<Bolme>(mekan.Bolmeler);
            var ekler = EkBolmeSaglayici?.Invoke(mekan);
            if (ekler != null) _gosterilenBolmeler.AddRange(ekler);

            BolmeSayisiLabel.Text = _gosterilenBolmeler.Count > mekan.Bolmeler.Count
                ? $"({mekan.Bolmeler.Count} + {_gosterilenBolmeler.Count - mekan.Bolmeler.Count} birleşik)"
                : $"({mekan.Bolmeler.Count})";
            BolmeListesi.ItemsSource = _gosterilenBolmeler;
            DosemeCombo.SelectedItem = FindMatchingMaterial(mekan.ZeminYapiElemani, YapiElemaniTipi.Doseme);
        }
        else
        {
            _gosterilenBolmeler = new List<Bolme>();
            BolmeSayisiLabel.Text = "";
            BolmeListesi.ItemsSource = null;
            DosemeCombo.SelectedItem = null;
        }
        _contextUpdating = false;

        GuncelleAtaBtn();
    }

    private static List<YapiElemani> LoadMaterials() =>
        YapiElemaniKutuphanesi.TumElemanlar
            .OrderBy(e => e.Tip)
            .ThenByDescending(e => e.Rw)
            .ThenBy(e => e.Ad)
            .ToList();

    // ── Duvar Listesi ────────────────────────────────────────────────────────

    private void BolmeListesi_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _secilenBolme = BolmeListesi.SelectedItem as Bolme;
        _editingAciklik = null;
        AciklikForm.IsVisible = false;

        if (_secilenBolme != null)
        {
            SeciliBolmeLabel.Text = _secilenBolme.Ad;
            MevcutMalzemeLabel.Text = _secilenBolme.YapiElemani != null
                ? $"← {_secilenBolme.YapiElemani.Ad} · net {_secilenBolme.NetOpakAlan:F2}/{_secilenBolme.Alan:F2} m²"
                : $"← malzeme yok · net {_secilenBolme.NetOpakAlan:F2}/{_secilenBolme.Alan:F2} m²";
            AciklikBolumu.IsVisible = true;
            JunctionBolumu.IsVisible = true;
            GuncelleJunctionPaneli();
        }
        else
        {
            SeciliBolmeLabel.Text = "";
            MevcutMalzemeLabel.Text = "";
            AciklikBolumu.IsVisible = false;
            JunctionBolumu.IsVisible = false;
            JunctionStackPanel.Children.Clear();
            JunctionOzetLabel.Text = "";
        }

        GuncelleAciklikListesi();
        GuncelleMenfezListesi();
        OnBolmeSecildi?.Invoke(_secilenBolme);
        GuncelleAtaBtn();
    }

    // ── A8 — Menfez / Havalandırma Yönetimi ──────────────────────────────────

    private void MenfezEkle_Click(object? sender, RoutedEventArgs e)
    {
        if (_secilenBolme == null) return;
        if (sender is not Button btn || btn.Tag is not string tagText) return;
        if (!Enum.TryParse<MenfezTipi>(tagText, true, out var tip)) tip = MenfezTipi.Mekanik;

        var menfez = new Menfez
        {
            Tip = tip,
            DneDb = MenfezVarsayilan.DneIcin(tip),
            Ad = tip.ToString()
        };
        _secilenBolme.Menfezler.Add(menfez);
        GuncelleMenfezListesi();
        OnAciklikDegisti?.Invoke(null); // ZorlaCizimYenile tetiklesin
    }

    private void GuncelleMenfezListesi()
    {
        MenfezStackPanel.Children.Clear();
        if (_secilenBolme == null) return;

        if (_secilenBolme.Menfezler.Count == 0)
        {
            MenfezStackPanel.Children.Add(new TextBlock
            {
                Text = "— menfez yok —",
                Foreground = new SolidColorBrush(Color.Parse("#556677")),
                FontSize = 10,
                Margin = new Avalonia.Thickness(4, 2, 0, 2)
            });
            return;
        }

        foreach (var menfez in _secilenBolme.Menfezler.ToList())
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("90, *, 90, Auto"),
                Margin = new Avalonia.Thickness(0, 1, 0, 1)
            };

            var tipCombo = new ComboBox
            {
                FontSize = 10,
                Padding = new Avalonia.Thickness(4, 2),
                Background = new SolidColorBrush(Color.Parse("#0E2233")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.Parse("#1E4060")),
                BorderThickness = new Avalonia.Thickness(1),
                ItemsSource = Enum.GetValues<MenfezTipi>()
            };
            tipCombo.SelectedItem = menfez.Tip;
            tipCombo.SelectionChanged += (_, _) =>
            {
                if (tipCombo.SelectedItem is MenfezTipi yeni)
                {
                    var eskiVarsayilan = MenfezVarsayilan.DneIcin(menfez.Tip);
                    menfez.Tip = yeni;
                    // Dne hâlâ eski tip'in tablo varsayılanıysa yeni tipin varsayılanına geç.
                    if (Math.Abs(menfez.DneDb - eskiVarsayilan) < 0.5 && yeni != MenfezTipi.Bagimsiz)
                        menfez.DneDb = MenfezVarsayilan.DneIcin(yeni);
                    GuncelleMenfezListesi();
                    OnAciklikDegisti?.Invoke(null);
                }
            };
            Grid.SetColumn(tipCombo, 0);

            var aciklamaBlock = new TextBlock
            {
                Text = $"Tipik: {MenfezVarsayilan.DneIcin(menfez.Tip):F0} dB",
                Foreground = new SolidColorBrush(Color.Parse("#667788")),
                FontSize = 9,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Avalonia.Thickness(6, 0, 6, 0)
            };
            Grid.SetColumn(aciklamaBlock, 1);

            var dneBox = new TextBox
            {
                Text = menfez.DneDb.ToString("F0", CultureInfo.InvariantCulture),
                FontSize = 10,
                Padding = new Avalonia.Thickness(4, 2),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            dneBox.LostFocus += (_, _) =>
            {
                if (double.TryParse(dneBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)
                    && v >= 0 && v <= 100)
                {
                    menfez.DneDb = v;
                    OnAciklikDegisti?.Invoke(null);
                }
                else
                {
                    dneBox.Text = menfez.DneDb.ToString("F0", CultureInfo.InvariantCulture);
                }
            };
            Grid.SetColumn(dneBox, 2);

            var silBtn = new Button
            {
                Content = "✕",
                FontSize = 10,
                Padding = new Avalonia.Thickness(6, 2),
                Background = new SolidColorBrush(Color.Parse("#2A0A0A")),
                Foreground = new SolidColorBrush(Color.Parse("#FF8888")),
                Margin = new Avalonia.Thickness(4, 0, 0, 0)
            };
            silBtn.Click += (_, _) =>
            {
                _secilenBolme.Menfezler.Remove(menfez);
                GuncelleMenfezListesi();
                OnAciklikDegisti?.Invoke(null);
            };
            Grid.SetColumn(silBtn, 3);

            grid.Children.Add(tipCombo);
            grid.Children.Add(aciklamaBlock);
            grid.Children.Add(dneBox);
            grid.Children.Add(silBtn);
            MenfezStackPanel.Children.Add(grid);
        }
    }

    // ── Junction / Flanking Kenarları ────────────────────────────────────────

    private void GuncelleJunctionPaneli()
    {
        JunctionStackPanel.Children.Clear();
        if (_secilenBolme == null)
            return;

        HavaSesiHesapServisi.EnsureDefaultJunctions(_secilenBolme);
        _junctionUpdating = true;
        foreach (var junction in _secilenBolme.Junctions.OrderBy(j => j.Kenar))
            JunctionStackPanel.Children.Add(BuildJunctionSatiri(junction));
        _junctionUpdating = false;
        GuncelleJunctionOzet();
    }

    private Control BuildJunctionSatiri(Junction junction)
    {
        var grid = new Grid { Margin = new Avalonia.Thickness(0, 1) };
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(44)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(72)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(42)));

        var kenar = new TextBlock
        {
            Text = JunctionKenarEtiketi(junction.Kenar),
            Foreground = Brush.Parse("#D8F6FF"),
            FontSize = 10,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(kenar, 0);

        var baglanti = new ComboBox
        {
            ItemsSource = Enum.GetValues<BaglantiTipi>(),
            SelectedItem = junction.BaglantiTipi,
            FontSize = 10,
            Margin = new Avalonia.Thickness(2, 0),
            Background = Brush.Parse("#0E2233"),
            Foreground = Brushes.White
        };
        baglanti.SelectionChanged += (_, _) =>
        {
            if (_junctionUpdating || baglanti.SelectedItem is not BaglantiTipi tip) return;
            junction.BaglantiTipi = tip;
            junction.KijOverrideDb = 0;
            GuncelleJunctionOzet();
            OnBolmeSecildi?.Invoke(_secilenBolme);
            OnJunctionDegisti?.Invoke();
        };
        Grid.SetColumn(baglanti, 1);

        var geometri = new ComboBox
        {
            ItemsSource = Enum.GetValues<JunctionGeometriTipi>(),
            SelectedItem = junction.GeometriTipi,
            FontSize = 10,
            Margin = new Avalonia.Thickness(2, 0),
            Background = Brush.Parse("#0E2233"),
            Foreground = Brushes.White
        };
        geometri.SelectionChanged += (_, _) =>
        {
            if (_junctionUpdating || geometri.SelectedItem is not JunctionGeometriTipi tip) return;
            junction.GeometriTipi = tip;
            GuncelleJunctionOzet();
            OnBolmeSecildi?.Invoke(_secilenBolme);
            OnJunctionDegisti?.Invoke();
        };
        Grid.SetColumn(geometri, 2);

        // Ek2 — strüktürel güçlendirme seçici (Yok/Kiriş/PerdeDuvar)
        var strukturel = new ComboBox
        {
            ItemsSource = Enum.GetValues<JunctionStrukturelTipi>(),
            SelectedItem = junction.StrukturelTipi,
            FontSize = 10,
            Margin = new Avalonia.Thickness(2, 0),
            Background = Brush.Parse("#0E2233"),
            Foreground = Brushes.White
        };
        strukturel.SelectionChanged += (_, _) =>
        {
            if (_junctionUpdating || strukturel.SelectedItem is not JunctionStrukturelTipi tip) return;
            junction.StrukturelTipi = tip;
            GuncelleJunctionOzet();
            OnBolmeSecildi?.Invoke(_secilenBolme);
            OnJunctionDegisti?.Invoke();
        };
        Grid.SetColumn(strukturel, 3);

        var kijBox = new TextBox
        {
            Text = junction.KijOverrideDb > 0 ? junction.KijOverrideDb.ToString("F1", CultureInfo.InvariantCulture) : "",
            PlaceholderText = $"Kij {junction.KijDb:F0}",
            FontSize = 10,
            Padding = new Avalonia.Thickness(4, 2),
            Margin = new Avalonia.Thickness(2, 0)
        };
        kijBox.LostFocus += (_, _) =>
        {
            if (_junctionUpdating) return;
            if (string.IsNullOrWhiteSpace(kijBox.Text))
                junction.KijOverrideDb = 0;
            else if (TryParsePositive(kijBox.Text, out var kij))
                junction.KijOverrideDb = kij;
            GuncelleJunctionOzet();
            OnBolmeSecildi?.Invoke(_secilenBolme);
            OnJunctionDegisti?.Invoke();
        };
        Grid.SetColumn(kijBox, 4);

        var kijLabel = new TextBlock
        {
            Text = $"{junction.KijDb:F0} dB",
            Foreground = Brush.Parse("#20C3D8"),
            FontSize = 10,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(kijLabel, 5);

        grid.Children.Add(kenar);
        grid.Children.Add(baglanti);
        grid.Children.Add(strukturel);
        grid.Children.Add(geometri);
        grid.Children.Add(kijBox);
        grid.Children.Add(kijLabel);
        return grid;
    }

    private void GuncelleJunctionOzet()
    {
        if (_secilenBolme == null)
        {
            JunctionOzetLabel.Text = "";
            return;
        }

        var yollar = HavaSesiHesapServisi.GetOnUcYolFlankingYollari(_secilenBolme);
        JunctionOzetLabel.Text = yollar.Count == 0
            ? "Yol özeti için ayırıcı duvar, komşu mekan, döşeme/tavan veya yan duvar elemanı atanmalı."
            : $"{yollar.Count} flanking yolu · en zayıf {yollar.Min(y => y.Rij):F1} dB · ort. Kij {yollar.Average(y => y.Kij):F1} dB";
    }

    private static string JunctionKenarEtiketi(JunctionKenarTipi kenar) => kenar switch
    {
        JunctionKenarTipi.Alt => "Alt",
        JunctionKenarTipi.Ust => "Üst",
        JunctionKenarTipi.Sol => "Sol",
        JunctionKenarTipi.Sag => "Sağ",
        _ => kenar.ToString()
    };

    public void SelectBolmeAndAciklik(Bolme? bolme, BolmeAcikligi? aciklik)
    {
        if (bolme == null || aciklik == null) return;
        BolmeListesi.SelectedItem = bolme;
        _secilenBolme = bolme;
        AciklikDuzenleBaslat(aciklik);
    }

    /// UX — "Atama Bekleyenler" navigasyonu: belirtilen duvarı listede seç ve görünür kıl.
    public void SecBolme(Bolme? bolme)
    {
        if (bolme == null) return;
        if (!_gosterilenBolmeler.Contains(bolme)) return; // bu odanın veya birleşik partnerin duvarı değil
        BolmeListesi.SelectedItem = bolme;   // SelectionChanged tetiklenir, etiketler güncellenir
        BolmeListesi.ScrollIntoView(bolme);
    }

    // ── Açıklıklar Listesi ───────────────────────────────────────────────────

    private void GuncelleAciklikListesi()
    {
        AciklikStackPanel.Children.Clear();
        if (_secilenBolme == null) return;

        if (_secilenBolme.Acikliklar.Count == 0)
        {
            AciklikStackPanel.Children.Add(new TextBlock
            {
                Text = "açıklık yok",
                Foreground = Brush.Parse("#445566"),
                FontSize = 9,
                Margin = new Avalonia.Thickness(2, 1)
            });
            return;
        }

        foreach (var aciklik in _secilenBolme.Acikliklar)
            AciklikStackPanel.Children.Add(BuildAciklikSatiri(aciklik));

        AciklikStackPanel.Children.Add(new TextBlock
        {
            Text = $"Açıklık: {_secilenBolme.AciklikAlani:F2} m² · net opak: {_secilenBolme.NetOpakAlan:F2} m²",
            Foreground = Brush.Parse("#7895A8"),
            FontSize = 9,
            Margin = new Avalonia.Thickness(2, 3, 2, 0)
        });
    }

    private Control BuildAciklikSatiri(BolmeAcikligi aciklik)
    {
        var grid = new Grid { Margin = new Avalonia.Thickness(0, 1) };
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        var isPencere = aciklik.Tip == BolmeAcikligiTipi.Pencere;
        var tipLabel = new TextBlock
        {
            Text = isPencere ? "PNC" : "KPI",
            Foreground = Brush.Parse(isPencere ? "#88BBDD" : "#88BB99"),
            FontSize = 9,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 32,
            Margin = new Avalonia.Thickness(0, 0, 4, 0)
        };
        Grid.SetColumn(tipLabel, 0);

        var infoStack = new StackPanel { Spacing = 0 };
        infoStack.Children.Add(new TextBlock
        {
            Text = $"{aciklik.Genislik * 100:F0}×{aciklik.Yukseklik * 100:F0} cm · {aciklik.Alan:F2} m²",
            Foreground = Brushes.White,
            FontSize = 10
        });
        infoStack.Children.Add(new TextBlock
        {
            Text = aciklik.YapiElemani?.Ad ?? "— malzeme yok —",
            Foreground = aciklik.YapiElemani != null
                ? Brush.Parse("#66AA88")
                : Brush.Parse("#886644"),
            FontSize = 9
        });
        Grid.SetColumn(infoStack, 1);

        var duzenleBtn = new Button
        {
            Content = "Malzeme",
            Padding = new Avalonia.Thickness(5, 2),
            FontSize = 10,
            Background = Brush.Parse("#182830"),
            Foreground = Brush.Parse("#88AABB"),
            Margin = new Avalonia.Thickness(4, 0, 2, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        duzenleBtn.Click += (_, _) => AciklikDuzenleBaslat(aciklik);
        Grid.SetColumn(duzenleBtn, 2);

        grid.Children.Add(tipLabel);
        grid.Children.Add(infoStack);
        grid.Children.Add(duzenleBtn);
        return grid;
    }

    private void AciklikSatirSil(BolmeAcikligi aciklik)
    {
        _secilenBolme?.Acikliklar.Remove(aciklik);
        if (ReferenceEquals(_editingAciklik, aciklik))
        {
            _editingAciklik = null;
            AciklikForm.IsVisible = false;
        }
        GuncelleAciklikListesi();
        OnAciklikDegisti?.Invoke(null);
    }

    // ── Ekleme / Düzenleme Formu ─────────────────────────────────────────────

    private void PencereEkle_Click(object? sender, RoutedEventArgs e)
    {
        _editingAciklik = null;
        AciklikFormGoster(BolmeAcikligiTipi.Pencere, null);
    }

    private void KapiEkle_Click(object? sender, RoutedEventArgs e)
    {
        _editingAciklik = null;
        AciklikFormGoster(BolmeAcikligiTipi.Kapi, null);
    }

    private void AciklikDuzenleBaslat(BolmeAcikligi aciklik)
    {
        _editingAciklik = aciklik;
        AciklikFormGoster(aciklik.Tip, aciklik);
    }

    private void AciklikFormGoster(BolmeAcikligiTipi tip, BolmeAcikligi? existing)
    {
        _editingTip = tip;
        var tipAdi = tip == BolmeAcikligiTipi.Pencere ? "Pencere" : "Kapı";

        // M50 geometri sahipliği — eski projede manuel oluşturulmuş olsa dahi açıklığın
        // tip/boyut/eşiği M08'de değiştirilmez; burada yalnız akustik malzeme atanır.
        AciklikFormBaslik.Text = existing == null
            ? $"{tipAdi} — M50'de oluşturulmalı"
            : $"{tipAdi} — geometri salt-okunur, malzeme atanabilir";
        AciklikTipCombo.IsEnabled = false;
        AciklikOnayarCombo.IsEnabled = false;
        AciklikGenislikBox.IsReadOnly = true;
        AciklikYukseklikBox.IsReadOnly = true;
        AciklikEsikBox.IsReadOnly = true;

        _onayarUpdating = true;
        AciklikTipCombo.ItemsSource = new[] { "Pencere", "Kapı" };
        AciklikTipCombo.SelectedIndex = tip == BolmeAcikligiTipi.Pencere ? 0 : 1;
        _onayarUpdating = false;

        // Önayar combo doldur
        DoldurAciklikOnayarlari(tip);

        // Boyut alanlarını doldur
        if (existing != null)
        {
            AciklikGenislikBox.Text = (existing.Genislik * 100).ToString("F0");
            AciklikYukseklikBox.Text = (existing.Yukseklik * 100).ToString("F0");
            AciklikEsikBox.Text = existing.EsikYuksekligiM > 0
                ? (existing.EsikYuksekligiM * 100).ToString("F0")
                : "";
        }
        else
        {
            AciklikGenislikBox.Text = tip == BolmeAcikligiTipi.Pencere ? "120" : "90";
            AciklikYukseklikBox.Text = tip == BolmeAcikligiTipi.Pencere ? "120" : "210";
            AciklikEsikBox.Text = "";
        }

        // Malzeme combo doldur
        DoldurAciklikMalzemeleri(tip);

        if (existing?.YapiElemani != null)
            AciklikMalzemeCombo.SelectedItem =
                _aciklikMalzemeleri.FirstOrDefault(m =>
                    m.Ad.Equals(existing.YapiElemani.Ad, StringComparison.OrdinalIgnoreCase))
                ?? (object?)_aciklikMalzemeleri.FirstOrDefault();
        else
            AciklikMalzemeCombo.SelectedIndex = _aciklikMalzemeleri.Count > 0 ? 0 : -1;

        AciklikSilBtn.IsVisible = false;
        AciklikForm.IsVisible = true;
    }

    private void DoldurAciklikOnayarlari(BolmeAcikligiTipi tip)
    {
        _onayarUpdating = true;
        _mevcutOnayarlar = AciklikOnayarKutuphanesi.TumOnayarlar
            .Where(o => o.Tip == tip)
            .ToList();
        AciklikOnayarCombo.ItemsSource = _mevcutOnayarlar
            .Select(o => $"{o.Ad} ({o.GenislikCm:F0}×{o.YukseklikCm:F0})")
            .ToList();
        AciklikOnayarCombo.SelectedIndex = -1;
        _onayarUpdating = false;
    }

    private void DoldurAciklikMalzemeleri(BolmeAcikligiTipi tip)
    {
        _aciklikMalzemeleri = _tumMalzemeler
            .Where(m => m.Tip == (tip == BolmeAcikligiTipi.Pencere
                ? YapiElemaniTipi.Pencere
                : YapiElemaniTipi.Kapi))
            .ToList();
        AciklikMalzemeCombo.ItemsSource = _aciklikMalzemeleri;
        AciklikMalzemeCombo.SelectedIndex = _aciklikMalzemeleri.Count > 0 ? 0 : -1;
    }

    private void AciklikTip_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_onayarUpdating) return;
        _editingTip = AciklikTipCombo.SelectedIndex == 1
            ? BolmeAcikligiTipi.Kapi
            : BolmeAcikligiTipi.Pencere;
        DoldurAciklikOnayarlari(_editingTip);
        DoldurAciklikMalzemeleri(_editingTip);
    }

    private void AciklikOnayar_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_onayarUpdating) return;
        var idx = AciklikOnayarCombo.SelectedIndex;
        if (idx < 0 || idx >= _mevcutOnayarlar.Count) return;

        var onayar = _mevcutOnayarlar[idx];
        AciklikGenislikBox.Text = onayar.GenislikCm.ToString("F0");
        AciklikYukseklikBox.Text = onayar.YukseklikCm.ToString("F0");
    }

    private void AciklikKaydet_Click(object? sender, RoutedEventArgs e)
    {
        if (_secilenBolme == null) return;

        // M08 her açıklıkta yalnız malzeme eşlemesini yazar; boyut/tip/eşik M50'nindir.
        if (_editingAciklik != null)
        {
            _editingAciklik.YapiElemani = AciklikMalzemeCombo.SelectedItem as YapiElemani;
            AciklikForm.IsVisible = false;
            GuncelleAciklikListesi();
            OnAciklikDegisti?.Invoke(_editingAciklik);
            return;
        }

        if (!TryParsePositive(AciklikGenislikBox.Text, out var genislikCm)) return;
        if (!TryParsePositive(AciklikYukseklikBox.Text, out var yukseklikCm)) return;
        // Eşik: boş ise 0 (tip varsayılanına düşer); değer girilirse onu kullan
        double esikM = 0.0;
        if (!string.IsNullOrWhiteSpace(AciklikEsikBox.Text)
            && double.TryParse(AciklikEsikBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var esikCm)
            && esikCm >= 0)
        {
            esikM = esikCm / 100.0;
        }

        var genislik = genislikCm / 100.0;
        var yukseklik = yukseklikCm / 100.0;
        _editingTip = AciklikTipCombo.SelectedIndex == 1
            ? BolmeAcikligiTipi.Kapi
            : BolmeAcikligiTipi.Pencere;
        var malzeme = AciklikMalzemeCombo.SelectedItem as YapiElemani;

        BolmeAcikligi? kaydedilen;
        if (_editingAciklik != null)
        {
            _editingAciklik.Tip = _editingTip;
            _editingAciklik.Genislik = genislik;
            _editingAciklik.Yukseklik = yukseklik;
            _editingAciklik.EsikYuksekligiM = esikM;
            _editingAciklik.YapiElemani = malzeme;
            HesaplaAciklikGeometry(_secilenBolme, _editingAciklik);
            kaydedilen = _editingAciklik;
        }
        else
        {
            var aciklik = new BolmeAcikligi
            {
                Tip = _editingTip,
                Genislik = genislik,
                Yukseklik = yukseklik,
                EsikYuksekligiM = esikM,
                YapiElemani = malzeme
            };
            HesaplaAciklikGeometry(_secilenBolme, aciklik);
            _secilenBolme.Acikliklar.Add(aciklik);
            kaydedilen = aciklik;
        }

        _editingAciklik = kaydedilen;
        AciklikForm.IsVisible = false;
        GuncelleAciklikListesi();
        OnAciklikDegisti?.Invoke(kaydedilen);
    }

    private void AciklikSil_Click(object? sender, RoutedEventArgs e)
    {
        if (_editingAciklik != null && _secilenBolme != null)
            _secilenBolme.Acikliklar.Remove(_editingAciklik);
        _editingAciklik = null;
        AciklikForm.IsVisible = false;
        GuncelleAciklikListesi();
        OnAciklikDegisti?.Invoke(null);
    }

    private void AciklikIptal_Click(object? sender, RoutedEventArgs e)
    {
        _editingAciklik = null;
        AciklikForm.IsVisible = false;
    }

    // Pencere: duvarda ortalanır. Kapı: aynı yatay konum, zemine sıfır (Y=0 Z eksen haklı).
    private static void HesaplaAciklikGeometry(Bolme bolme, BolmeAcikligi aciklik)
    {
        if (!bolme.HasGeometry) return;

        var dx = bolme.Bitis.X - bolme.Baslangic.X;
        var dy = bolme.Bitis.Y - bolme.Baslangic.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-9) return;

        var tx = dx / len;
        var ty = dy / len;
        var midX = (bolme.Baslangic.X + bolme.Bitis.X) / 2;
        var midY = (bolme.Baslangic.Y + bolme.Bitis.Y) / 2;
        var half = aciklik.Genislik / 2;

        aciklik.Baslangic = new Point2D(midX - tx * half, midY - ty * half);
        aciklik.Bitis = new Point2D(midX + tx * half, midY + ty * half);
    }

    // ── Malzeme Arama + Atama ────────────────────────────────────────────────

    private void AramaKutusu_TextChanged(object? sender, TextChangedEventArgs e)
    {
        var query = (AramaKutusu.Text ?? "").Trim();

        var filtered = query.Length == 0
            ? _tumMalzemeler
            : _tumMalzemeler.Where(m =>
                m.Ad.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                m.Tip.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
                m.Rw.ToString("F0").Contains(query, StringComparison.OrdinalIgnoreCase))
              .ToList();

        MalzemeListesi.ItemsSource = filtered;
        _secilenMalzeme = null;
        SeciliMalzemeLabel.Text = "";
        GuncelleAtaBtn();
    }

    // UX — Drag-drop: malzeme öğesini sürükle, viewport'taki bölmeye bırak.
    // Avalonia 12 yeni DataTransfer API'si karmaşık olduğu için
    // uygulama içi statik referans kullanıyoruz (sadece app içi sürükle-bırak).
    public static YapiElemani? AktifSurukleMalzemesi { get; set; }

    private Avalonia.Point? _dragStartPoint;
    private YapiElemani? _potansiyelDrag;

    private void MalzemeListesi_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        _dragStartPoint = e.GetPosition(MalzemeListesi);
        _potansiyelDrag = (e.Source as Avalonia.Controls.Control)?.DataContext as YapiElemani;
    }

    private void MalzemeListesi_PointerMoved(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (_dragStartPoint == null || _potansiyelDrag == null) return;
        var p = e.GetPosition(MalzemeListesi);
        if (System.Math.Abs(p.X - _dragStartPoint.Value.X) < 6 &&
            System.Math.Abs(p.Y - _dragStartPoint.Value.Y) < 6) return;
        AktifSurukleMalzemesi = _potansiyelDrag;
        _dragStartPoint = null;
        _potansiyelDrag = null;
    }

    private void MalzemeListesi_PointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        _dragStartPoint = null;
        _potansiyelDrag = null;
        // AktifSurukleMalzemesi viewport'un drop handler'ı tarafından sıfırlanır.
    }

    private void MalzemeListesi_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _secilenMalzeme = MalzemeListesi.SelectedItem as YapiElemani;
        SeciliMalzemeLabel.Text = _secilenMalzeme?.Ad ?? "";
        GuncelleAtaBtn();
    }

    private void MalzemeListesi_DoubleTapped(object? sender, TappedEventArgs e)
    {
        AtalariGerceklestir(false);
    }

    private void AtaBtn_OnClick(object? sender, RoutedEventArgs e)
    {
        AtalariGerceklestir(false);
    }

    private void TumunuUygula_OnClick(object? sender, RoutedEventArgs e)
    {
        AtalariGerceklestir(tumune: true);
    }

    private void BosaUygula_OnClick(object? sender, RoutedEventArgs e)
    {
        AtalariGerceklestir(tumune: true, yalnizBos: true);
    }

    private void AtalariGerceklestir(bool tumune, bool yalnizBos = false)
    {
        if (_secilenMalzeme == null) return;
        if (DataContext is not Mekan mekan) return;

        if (tumune)
        {
            // Birleşik panelde gösterilen tüm duvarlar (bu oda + partner oda duvarları) kapsanır.
            var hedefler = _gosterilenBolmeler.Count > 0 ? _gosterilenBolmeler : (IEnumerable<Bolme>)mekan.Bolmeler;
            foreach (var bolme in hedefler.ToList())
            {
                if (yalnizBos && bolme.YapiElemani != null) continue; // mevcut atamaları koru
                AtaDuvarMalzemesi(bolme, _secilenMalzeme);
            }
        }
        else
        {
            if (_secilenBolme == null) return;
            AtaDuvarMalzemesi(_secilenBolme, _secilenMalzeme);
            MevcutMalzemeLabel.Text = $"← {_secilenMalzeme.Ad}";
        }

        // VM'e atama yapıldı haberi → doğrulama paneli yenilensin
        OnBolmeMalzemesiAtandi?.Invoke();
    }

    // ── Döşeme ───────────────────────────────────────────────────────────────

    private void DosemeCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_contextUpdating) return;
        if (DataContext is not Mekan mekan) return;
        if (DosemeCombo.SelectedItem is not YapiElemani eleman) return;
        if (eleman.Tip != YapiElemaniTipi.Doseme) return;

        mekan.ZeminYapiElemani = eleman;
        mekan.TavanYapiElemani = eleman;
    }

    private void DosemeKataUygulaBtn_OnClick(object? sender, RoutedEventArgs e)
    {
        OnDosemeKataUygula?.Invoke(DosemeCombo.SelectedItem as YapiElemani);
    }

    // ── Yardımcılar ──────────────────────────────────────────────────────────

    private static void AtaDuvarMalzemesi(Bolme bolme, YapiElemani malzeme)
    {
        if (malzeme.Tip != YapiElemaniTipi.Duvar)
            return;

        bolme.YapiElemani = malzeme;
        HavaSesiHesapServisi.EnsureDefaultJunctions(bolme);
        foreach (var junction in bolme.Junctions.Where(j => j.KijOverrideDb <= 0))
            junction.BaglantiTipi = malzeme.BaglantiTipi;
        var kaynak = bolme.KaynakMekan;
        var alici = bolme.AliciMekan;
        if (kaynak == null || alici == null)
            return;

        var karsi = alici.Bolmeler.FirstOrDefault(b =>
            b.AliciMekan == kaynak &&
            SameSegment(b.Baslangic, b.Bitis, bolme.Baslangic, bolme.Bitis));
        if (karsi != null)
            karsi.YapiElemani = malzeme;
    }

    private static bool SameSegment(Point2D a1, Point2D a2, Point2D b1, Point2D b2)
    {
        const double tolerance = 0.02;
        return (Distance(a1, b1) <= tolerance && Distance(a2, b2) <= tolerance) ||
               (Distance(a1, b2) <= tolerance && Distance(a2, b1) <= tolerance);
    }

    private static double Distance(Point2D a, Point2D b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private YapiElemani? FindMatchingMaterial(YapiElemani? current, params YapiElemaniTipi[] types)
    {
        if (current == null) return null;
        return _tumMalzemeler.FirstOrDefault(m =>
            types.Contains(m.Tip) &&
            m.Ad.Equals(current.Ad, StringComparison.OrdinalIgnoreCase)) ?? current;
    }

    private void GuncelleAtaBtn()
    {
        var duvarMalzemesi = _secilenMalzeme?.Tip == YapiElemaniTipi.Duvar;
        AtaBtn.IsEnabled = duvarMalzemesi && _secilenBolme != null;
        TumunuUygulaBtn.IsEnabled = duvarMalzemesi && DataContext is Mekan;
        BosaUygulaBtn.IsEnabled = duvarMalzemesi && DataContext is Mekan;
    }

    private static bool TryParsePositive(string? text, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var normalized = text.Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out result)
               && result > 0;
    }
}
