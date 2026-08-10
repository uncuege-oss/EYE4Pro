using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Kst.Acoustics.App.Bina.ViewModels;
using Kst.Acoustics.App.Views;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Bina.Services;
using BinaServices = Kst.Acoustics.Core.Bina.Services;

namespace Kst.Acoustics.App.Bina.Views;

public partial class BinaGeometriWindow : Window
{
    private readonly BinaGeometriViewModel _vm = new();
    private bool _kapanisOnaylandi;
    private bool _isBusy;
    private bool _kurtarmaYaziliyor;
    private bool _kurtarmaIsleriKapandi;
    private CancellationTokenSource? _islemCts;
    private BinaAciklikTipi _aktifAciklikTipi = BinaAciklikTipi.Kapi;
    private readonly DispatcherTimer _otomatikKurtarmaTimer = new() { Interval = TimeSpan.FromSeconds(45) };
    private readonly List<string> _sonDosyalar = new();
    private sealed record SonDosyaOgesi(string DosyaYolu)
    {
        public string Ad => Path.GetFileName(DosyaYolu);
    }

    private static readonly JsonSerializerOptions KurtarmaJsonAyar = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    private static readonly object KurtarmaYayinKilidi = new();
    private static long s_kurtarmaYayinNesli;

    private static string UygulamaVeriKlasoru => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Eye4Pro", "M50");
    private static string KurtarmaDosyasi => Path.Combine(UygulamaVeriKlasoru, "otomatik-kurtarma.e4bina.json");
    private static string SonDosyalarDosyasi => Path.Combine(UygulamaVeriKlasoru, "son-dosyalar.txt");

    private static string YeniKurtarmaGeciciDosyasi()
        => $"{KurtarmaDosyasi}.{Guid.NewGuid():N}.tmp";

    private static bool KurtarmaYayiniGecerliMi(
        bool isDirty,
        bool pencereKapandi,
        long beklenenNesil,
        long guncelNesil,
        string yazilanDurum,
        string guncelDurum)
        => isDirty &&
           !pencereKapandi &&
           beklenenNesil == guncelNesil &&
           string.Equals(yazilanDurum, guncelDurum, StringComparison.Ordinal);

    private static void KurtarmaYayinlariniGecersizKil()
    {
        lock (KurtarmaYayinKilidi)
            Interlocked.Increment(ref s_kurtarmaYayinNesli);
    }

    private void KurtarmaIsleriniKapat()
    {
        if (_kurtarmaIsleriKapandi) return;
        _kurtarmaIsleriKapandi = true;
        KurtarmaYayinlariniGecersizKil();
    }

    // Geri butonunun döneceği pencere fabrikası — her launcher kendi tipini iletir.
    private readonly Func<Window>? _geriPencere;
    private readonly bool _yardimciMod;

    // Parameterless ctor: Avalonia AXAML loader için zorunlu.
    public BinaGeometriWindow() : this(null) { }

    public BinaGeometriWindow(Func<Window>? geriPencere, bool yardimciMod = false)
    {
        InitializeComponent();
        DataContext = _vm;
        _geriPencere = geriPencere;
        _yardimciMod = yardimciMod;
        Opened += OnOpened;
        Closing += OnWindowClosing;
        Closed += OnWindowClosed;
        _otomatikKurtarmaTimer.Tick += OtomatikKurtarma_Tick;
    }

    private BinaKat? _izlenenKat; // DxfGoster/BloklariGoster PropertyChanged aboneliği için

    private void ModGoster(string mod)
    {
        if (ModDurumuText is { } t) t.Text = $"MOD: {mod}";
    }

    private void BusyAyarla(bool busy, string mesaj = "")
    {
        _isBusy = busy;
        if (IslemOverlay is { } o) o.IsVisible = busy;
        if (IslemMesajText is { } t) t.Text = string.IsNullOrWhiteSpace(mesaj) ? "İşlem sürüyor…" : mesaj;
        if (IslemIptalBtn is { } b) b.IsEnabled = busy;
    }

    private void EtkilesimleriIptalEt()
    {
        PlanCanvas?.EtkilesimleriIptalEt();
        ModGoster("SEÇİM");
    }

    private bool AktifEditoruUygula(object? aktifEditor = null)
    {
        aktifEditor ??= FocusManager?.GetFocusedElement();
        return BinaEditorCommitYardimci.Uygula(aktifEditor,
            (BinaAdText, (Action)UygulaBinaAdi),
            (KatAdText, (Action)UygulaKatAdi),
            (OdaAdText, (Action)UygulaOdaAdi),
            (KatYukseklikText, (Action)UygulaKatYukseklik),
            (OfsetXText, (Action)UygulaOfsetX),
            (OfsetYText, (Action)UygulaOfsetY),
            (AciklikYukseklikText, (Action)UygulaAciklikYukseklik),
            (AciklikEsikText, (Action)UygulaAciklikEsik));
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (PlanCanvas is { } c)
        {
            c.OnOdaSecildi = oda =>
            {
                _vm.SeciliOdaId = oda?.Id;
                if (oda is not null)
                    _vm.DurumMesaji = $"Seçili: {oda.Ad}  ({oda.AlanM2:F1} m²)";
            };

            // Oda düzenleme callback'leri
            c.OnOdaCizimTamam     = noktalar => { _vm.OdaEkle(noktalar); PlanCanvas?.InvalidateVisual(); };
            c.OnVertexTasimaBasladi = () => _vm.UndoSnapshotAl(); // lazy-Snap: sürükleme başına tek snapshot
            c.OnVertexTasindi     = (idx, x, y) => _vm.VertexGuncelle(idx, x, y);
            c.OnKenarTasindi      = (idx, x0, y0, x1, y1) => _vm.KenarGuncelle(idx, x0, y0, x1, y1);
            c.OnVertexTasimaBitti = () => _vm.OdaGeometriDuzenlemeBitti();
            c.OnVertexSilIstegi   = idx => _vm.VertexSil(idx);
            c.OnKenarCiftTiklandi = (idx, x, y) => _vm.VertexEkleSonra(idx, x, y);
            c.OnCizimModuDegisti  = aktif =>
            {
                if (OdaCizBtn      is { } b1) b1.IsVisible = !aktif;
                if (OdaCizBitirBtn is { } b2) b2.IsVisible = aktif;
                if (OdaCizIptalBtn is { } b3) b3.IsVisible = aktif;
                if (CanvasIpucu    is { } ip)
                    ip.Text = aktif
                        ? "Tıkla = nokta ekle · ilk noktaya tıkla / çift tık = bitir · Esc = iptal"
                        : "Köşe sürükle · kenar sürükle = duvarı taşı · kenara çift tık = köşe ekle · köşeye sağ tık = köşe sil";
                if (aktif)
                    _vm.DurumMesaji = "Oda çizim modu: plana tıklayarak köşeleri yerleştirin (en az 3 nokta).";
                ModGoster(aktif ? "ODA ÇİZ" : "SEÇİM");
            };

            // Açıklık callback'leri
            c.OnAciklikSecildi = ac =>
            {
                _vm.SeciliAciklikId = ac?.Id;
                SyncAciklikPanel();
                if (ac is not null)
                {
                    string tipAd = ac.Tip == BinaAciklikTipi.Pencere ? "Pencere" : "Kapı";
                    _vm.DurumMesaji = $"Seçili: {tipAd}  ({ac.GenislikM:F2} m, kaynak: {ac.Kaynak ?? "?"})";
                }
            };
            c.OnAciklikEklendi = (bas, bit, tip) =>
            {
                _vm.AciklikEkleManuel(bas, bit, tip);
                SyncAciklikPanel();
                PlanCanvas?.InvalidateVisual();
            };
            c.OnAciklikEkleModuDegisti = aktif =>
            {
                if (KapiEkleBtn         is { } b1) b1.IsVisible = !aktif;
                if (PencereEkleBtn      is { } b2) b2.IsVisible = !aktif;
                if (AciklikEkleIptalBtn is { } b3) b3.IsVisible = aktif;
                if (aktif) ModGoster(_aktifAciklikTipi == BinaAciklikTipi.Kapi ? "KAPI EKLE" : "PENCERE EKLE");
                else if (PlanCanvas?.OdaCizimModu != true) ModGoster("SEÇİM");
            };

            // Düşey birleşim işaretleri (⬆/⬇ etiketleri)
            c.OdaBirlesimSorgu = odaId => (
                BinaDuseyBirlesimServisi.TavanAcikMi(_vm.Geo, odaId),
                BinaDuseyBirlesimServisi.DosemeAcikMi(_vm.Geo, odaId));
        }
        _vm.PropertyChanged += OnVmPropertyChanged;
        SyncEditPanelFromVm();
        SyncIzlenenOda();
        SyncAciklikPanel();
        ModGoster("SEÇİM");
        SonDosyalariYukle();
        if (KurtarmaAcBtn is { } kb) kb.IsVisible = File.Exists(KurtarmaDosyasi);
        _otomatikKurtarmaTimer.Start();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        KurtarmaIsleriniKapat();
        _otomatikKurtarmaTimer.Stop();
        _otomatikKurtarmaTimer.Tick -= OtomatikKurtarma_Tick;
        _vm.PropertyChanged -= OnVmPropertyChanged;
        if (_izlenenKat is not null) _izlenenKat.PropertyChanged -= OnKatPropertyChanged;
        if (_izlenenOda is not null) _izlenenOda.PropertyChanged -= OnOdaPropertyChanged;
        _islemCts?.Cancel();
        _islemCts?.Dispose();
        _islemCts = null;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BinaGeometriViewModel.SeciliKat) ||
            e.PropertyName == nameof(BinaGeometriViewModel.Geo))
        {
            SyncEditPanelFromVm();
            SyncIzlenenOda();
            SyncAciklikPanel();
        }
        else if (e.PropertyName == nameof(BinaGeometriViewModel.KatOfsetX))
            SyncOfsetXText();
        else if (e.PropertyName == nameof(BinaGeometriViewModel.KatOfsetY))
            SyncOfsetYText();
        else if (e.PropertyName == nameof(BinaGeometriViewModel.SeciliOdaId))
            SyncIzlenenOda();
        else if (e.PropertyName == nameof(BinaGeometriViewModel.SeciliAciklikId))
            SyncAciklikPanel();
    }

    private BinaOda? _izlenenOda; // oda adı düzenlemesini dirty + repaint için izle

    private void SyncIzlenenOda()
    {
        if (_izlenenOda is not null)
            _izlenenOda.PropertyChanged -= OnOdaPropertyChanged;
        _izlenenOda = _vm.SeciliOda;
        if (_izlenenOda is not null)
            _izlenenOda.PropertyChanged += OnOdaPropertyChanged;
        if (OdaAdText is { } odaAd)
            odaAd.Text = _izlenenOda?.Ad ?? "";
    }

    private void OnOdaPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BinaOda.Ad))
        {
            PlanCanvas?.InvalidateVisual(); // canvas oda etiketini tazele
        }
    }

    private void SyncEditPanelFromVm()
    {
        var kat = _vm.SeciliKat;

        // Kat değişince eski aboneliği kes, yenisine bağlan
        if (_izlenenKat is not null)
            _izlenenKat.PropertyChanged -= OnKatPropertyChanged;
        _izlenenKat = kat;
        if (kat is not null)
            kat.PropertyChanged += OnKatPropertyChanged;

        // Yükseklik textbox
        if (KatYukseklikText is { } yt)
            yt.Text = kat?.KatYuksekligiM.ToString("F2") ?? "";
        if (KatAdText is { } katAd)
            katAd.Text = kat?.Ad ?? "";
        if (BinaAdText is { } binaAd)
            binaAd.Text = _vm.Geo.BinaAdi;
        if (DxfGosterCheck is { } dxfCheck)
            dxfCheck.IsChecked = kat?.DxfGoster ?? false;
        if (BloklariGosterCheck is { } blokCheck)
            blokCheck.IsChecked = kat?.BloklariGoster ?? false;

        SyncOfsetXText();
        SyncOfsetYText();

        // DXF dosya adı etiketi
        if (KatDxfDosyaText is { } dt)
            dt.Text = kat is not null && !string.IsNullOrEmpty(kat.DxfFilePath)
                ? Path.GetFileName(kat.DxfFilePath)
                : "";
    }

    private void OnKatPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(BinaKat.DxfGoster):
            case nameof(BinaKat.BloklariGoster):
                PlanCanvas?.InvalidateVisual();
                break;
        }
    }

    private void SyncOfsetXText()
    {
        if (OfsetXText is { } x)
            x.Text = _vm.KatOfsetX.ToString("F2");
    }

    private void SyncOfsetYText()
    {
        if (OfsetYText is { } y)
            y.Text = _vm.KatOfsetY.ToString("F2");
    }

    // ── Navigasyon ────────────────────────────────────────────────────────────

    private async void Geri_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        AktifEditoruUygula();
        if (!await KapanisOnayiAl()) return;

        if (_yardimciMod)
        {
            var owner = Owner;
            _kapanisOnaylandi = true;
            KurtarmaIsleriniKapat();
            Close();
            owner?.Activate();
            owner?.Focus();
            return;
        }

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
            int w = (int)(double.IsNaN(win.Width)  || win.Width  < 100 ? 1200 : win.Width);
            int h = (int)(double.IsNaN(win.Height) || win.Height < 100 ? 800  : win.Height);
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
        KurtarmaIsleriniKapat();
        Close();
    }

    private async void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_kapanisOnaylandi) return;

        if (_isBusy) _islemCts?.Cancel();
        AktifEditoruUygula();

        e.Cancel = true;
        if (!await KapanisOnayiAl()) return;

        _kapanisOnaylandi = true;
        KurtarmaIsleriniKapat();
        Close();
    }

    // ── Dosya işlemleri ───────────────────────────────────────────────────────

    private async void Yeni_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        AktifEditoruUygula();
        if (!await KaydedilmemisDegisiklikOnayiAl("Kaydedilmemiş değişiklikler var. Yeni bina geometrisi oluşturmak istiyor musunuz?"))
            return;

        EtkilesimleriIptalEt();
        KurtarmaYayinlariniGecersizKil();
        _vm.Geo = BinaGeometriServisi.YeniOlustur();
        _vm.SeciliKat = null;
        _vm.UndoYiginlariniTemizle();
        _vm.DurumMesaji = "Yeni bina geometrisi oluşturuldu.";
    }

    private async void Ac_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        AktifEditoruUygula();
        if (!await KaydedilmemisDegisiklikOnayiAl("Kaydedilmemiş değişiklikler var. Başka bir dosya açmak istiyor musunuz?"))
            return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Bina Geometrisi Aç",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("EYE4PRO Bina Geometrisi (.e4bina.json)")
                { Patterns = ["*.e4bina.json"] }]
        });
        var path = files.Count > 0 ? files[0].Path.LocalPath : null;
        if (string.IsNullOrWhiteSpace(path)) return;

        EtkilesimleriIptalEt();
        DosyaAc(path);
    }

    public void DosyaAc(string path)
    {
        try
        {
            EtkilesimleriIptalEt();
            var acilan = BinaGeometriServisi.Ac(path);
            KurtarmaYayinlariniGecersizKil();
            _vm.Geo = acilan;
            _vm.SeciliKat = _vm.Geo.Katlar.Count > 0 ? _vm.Geo.Katlar[0] : null;
            _vm.UndoYiginlariniTemizle();
            if (!string.Equals(path, KurtarmaDosyasi, StringComparison.OrdinalIgnoreCase))
                SonDosyayaEkle(path);
            _vm.DurumMesaji = $"Açıldı: {Path.GetFileName(path)}  (Rev. {_vm.Geo.GeometriRevizyon})";
        }
        catch (Exception ex)
        {
            _vm.DurumMesaji = $"Açılamadı: {ex.Message}";
        }
    }

    private void SonDosyalariYukle()
    {
        _sonDosyalar.Clear();
        try
        {
            if (File.Exists(SonDosyalarDosyasi))
                _sonDosyalar.AddRange(File.ReadAllLines(SonDosyalarDosyasi)
                    .Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(8));
        }
        catch
        {
            // Son dosyalar kolaylık özelliğidir; bozuk/erişilemez liste ana akışı engellemez.
        }
        if (SonDosyalarCombo is { } combo)
            combo.ItemsSource = _sonDosyalar.Select(p => new SonDosyaOgesi(p)).ToList();
    }

    private void SonDosyayaEkle(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        string tamYol;
        try { tamYol = Path.GetFullPath(path); }
        catch { return; }

        _sonDosyalar.RemoveAll(p => string.Equals(p, tamYol, StringComparison.OrdinalIgnoreCase));
        _sonDosyalar.Insert(0, tamYol);
        if (_sonDosyalar.Count > 8) _sonDosyalar.RemoveRange(8, _sonDosyalar.Count - 8);
        try
        {
            Directory.CreateDirectory(UygulamaVeriKlasoru);
            File.WriteAllLines(SonDosyalarDosyasi, _sonDosyalar);
        }
        catch
        {
            // Salt-okunur profilde dosya açma/kaydetme yine çalışmaya devam eder.
        }
        if (SonDosyalarCombo is { } combo)
            combo.ItemsSource = _sonDosyalar.Select(p => new SonDosyaOgesi(p)).ToList();
    }

    private async void SonDosyalar_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SonDosyalarCombo is not { SelectedItem: SonDosyaOgesi oge } combo) return;
        string path = oge.DosyaYolu;
        combo.SelectedIndex = -1;
        if (!File.Exists(path))
        {
            _vm.DurumMesaji = $"Son dosya artık bulunamıyor: {path}";
            SonDosyalariYukle();
            return;
        }
        if (!await KaydedilmemisDegisiklikOnayiAl("Kaydedilmemiş değişiklikler var. Son kullanılan dosya açılsın mı?"))
            return;
        DosyaAc(path);
    }

    private async void OtomatikKurtarma_Tick(object? sender, EventArgs e)
    {
        if (!_vm.IsDirty || _isBusy || _kurtarmaYaziliyor || _kurtarmaIsleriKapandi) return;
        _kurtarmaYaziliyor = true;
        string gecici = YeniKurtarmaGeciciDosyasi();
        long beklenenNesil = Interlocked.Read(ref s_kurtarmaYayinNesli);
        try
        {
            var kopya = _vm.KurtarmaKopyasiOlustur();
            string yazilanDurum = await Task.Run(() =>
            {
                Directory.CreateDirectory(UygulamaVeriKlasoru);
                string json = JsonSerializer.Serialize(kopya, KurtarmaJsonAyar);
                File.WriteAllText(gecici, json);
                return json;
            });

            if (_kurtarmaIsleriKapandi || !_vm.IsDirty) return;
            string guncelDurum = JsonSerializer.Serialize(
                _vm.KurtarmaKopyasiOlustur(), KurtarmaJsonAyar);

            lock (KurtarmaYayinKilidi)
            {
                long guncelNesil = Interlocked.Read(ref s_kurtarmaYayinNesli);
                if (!KurtarmaYayiniGecerliMi(
                        _vm.IsDirty,
                        _kurtarmaIsleriKapandi,
                        beklenenNesil,
                        guncelNesil,
                        yazilanDurum,
                        guncelDurum))
                    return;

                File.Move(gecici, KurtarmaDosyasi, overwrite: true);
                Interlocked.Increment(ref s_kurtarmaYayinNesli);
            }
            if (KurtarmaAcBtn is { } btn) btn.IsVisible = true;
        }
        catch (Exception ex)
        {
            if (!_kurtarmaIsleriKapandi)
                _vm.DurumMesaji = $"Otomatik kurtarma yazılamadı: {ex.Message}";
        }
        finally
        {
            try
            {
                if (File.Exists(gecici)) File.Delete(gecici);
            }
            catch
            {
                // Geçici dosya temizliği bir sonraki otomatik kurtarmayı engellemez.
            }
            _kurtarmaYaziliyor = false;
        }
    }

    private void KurtarmaDosyasiniTemizle()
    {
        lock (KurtarmaYayinKilidi)
        {
            Interlocked.Increment(ref s_kurtarmaYayinNesli);
            try
            {
                if (File.Exists(KurtarmaDosyasi)) File.Delete(KurtarmaDosyasi);
                if (File.Exists(KurtarmaDosyasi + ".tmp")) File.Delete(KurtarmaDosyasi + ".tmp");
            }
            catch
            {
                // Başarılı asıl kaydı kurtarma temizliği hatasıyla gölgelemeyiz.
            }
        }
        if (KurtarmaAcBtn is { } btn) btn.IsVisible = false;
    }

    private async void KurtarmaAc_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!File.Exists(KurtarmaDosyasi))
        {
            if (KurtarmaAcBtn is { } btn) btn.IsVisible = false;
            return;
        }
        if (!await KaydedilmemisDegisiklikOnayiAl("Mevcut kaydedilmemiş değişikliklerin yerine otomatik kurtarma kopyası açılsın mı?"))
            return;
        try
        {
            var kurtarilan = BinaGeometriServisi.Ac(KurtarmaDosyasi);
            // Kurtarma kopyası özgün dosyayı sessizce ezmemeli; ilk normal kayıt Save As ister.
            kurtarilan.DosyaYolu = null;
            KurtarmaYayinlariniGecersizKil();
            _vm.Geo = kurtarilan;
            _vm.Kirlet();
            _vm.DurumMesaji = "Otomatik kurtarma kopyası açıldı. Güvenli bir konuma kaydedin.";
        }
        catch (Exception ex)
        {
            _vm.DurumMesaji = $"Kurtarma kopyası açılamadı: {ex.Message}";
        }
    }

    private void Window_DragOver(object? sender, DragEventArgs e)
    {
        var dosyalar = e.DataTransfer.TryGetFiles();
        bool uygun = !_isBusy && dosyalar is not null && dosyalar.Any(f =>
        {
            string p = f.Path.LocalPath;
            return p.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase) ||
                   p.EndsWith(".e4bina.json", StringComparison.OrdinalIgnoreCase);
        });
        e.DragEffects = uygun ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object? sender, DragEventArgs e)
    {
        if (_isBusy) return;
        string? path = e.DataTransfer.TryGetFiles()?.FirstOrDefault()?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path)) return;
        e.Handled = true;

        if (path.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase))
        {
            await DxfYoluIceAktar(path);
            return;
        }
        if (!path.EndsWith(".e4bina.json", StringComparison.OrdinalIgnoreCase))
        {
            _vm.DurumMesaji = "Yalnız .dxf veya .e4bina.json dosyaları bırakılabilir.";
            return;
        }
        if (!await KaydedilmemisDegisiklikOnayiAl("Kaydedilmemiş değişiklikler var. Bırakılan bina geometrisi açılsın mı?"))
            return;
        DosyaAc(path);
    }

    private async void Kaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await Kaydet();
    }

    private async void FarkliKaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await FarkliKaydet();

    private async Task<bool> Kaydet()
    {
        AktifEditoruUygula();
        if (string.IsNullOrWhiteSpace(_vm.Geo.DosyaYolu))
            return await FarkliKaydet();

        return KaydetYola(_vm.Geo.DosyaYolu);
    }

    private async Task<bool> FarkliKaydet()
    {
        AktifEditoruUygula();
        var ad = string.IsNullOrWhiteSpace(_vm.Geo.BinaAdi) ? "Bina" : _vm.Geo.BinaAdi;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Bina Geometrisi Kaydet",
            DefaultExtension = "e4bina.json",
            SuggestedFileName = $"{ad}.e4bina.json",
            FileTypeChoices = [new FilePickerFileType("EYE4PRO Bina Geometrisi (.e4bina.json)")
                { Patterns = ["*.e4bina.json"] }]
        });
        var path = file?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path)) return false;
        return KaydetYola(path);
    }

    private bool KaydetYola(string path)
    {
        try
        {
            BinaGeometriServisi.Kaydet(_vm.Geo, path);
            _vm.TemizIsaretle();
            SonDosyayaEkle(path);
            KurtarmaDosyasiniTemizle();
            _vm.DurumMesaji = $"Kaydedildi: {Path.GetFileName(path)}  (Rev. {_vm.Geo.GeometriRevizyon})";
            return true;
        }
        catch (Exception ex)
        {
            _vm.DurumMesaji = $"Kaydedilemedi: {ex.Message}";
            return false;
        }
    }

    // ── Kat yönetimi ──────────────────────────────────────────────────────────

    private void KatList_SelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is BinaKat seçilen)
        {
            _vm.SeciliKat = seçilen;
            PlanCanvas?.FitAll();
        }
    }

    private void OdaList_SelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is BinaOda oda)
        {
            if (_vm.SeciliOdaId != oda.Id)
            {
                _vm.SeciliOdaId = oda.Id;
                _vm.DurumMesaji = $"Seçili: {oda.Ad}  ({oda.AlanM2:F1} m²)";
            }
            PlanCanvas?.InvalidateVisual();
        }
    }

    private void KatEkle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        EtkilesimleriIptalEt();
        _vm.KatEkle();
    }

    private async void DxfYukle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isBusy) return;
        AktifEditoruUygula();
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "DXF Kat Planı Seç",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("DXF Dosyaları") { Patterns = ["*.dxf"] }]
        });
        var path = files.Count > 0 ? files[0].Path.LocalPath : null;
        if (string.IsNullOrWhiteSpace(path)) return;

        await DxfYoluIceAktar(path);
    }

    private async Task DxfYoluIceAktar(string path)
    {
        EtkilesimleriIptalEt();
        _islemCts?.Cancel();
        using var cts = new CancellationTokenSource();
        _islemCts = cts;
        BusyAyarla(true, "DXF analiz ediliyor…");
        _vm.DurumMesaji = "DXF taranıyor…";
        try
        {
            var yukleyici = new BinaServices.DxfKatYukleyici();

            // Tek dosyada yan yana birden fazla kat planı olup olmadığını önce
            // otomatik tespit et (codex50 §9). Çoklu plan bulunursa dağıtım
            // diyaloğu açılır; tek plansa mevcut tek-kat akışı aynen sürer.
            var cokluSonuc = await Task.Run(() => yukleyici.CokluKatTespitEt(path), cts.Token);
            cts.Token.ThrowIfCancellationRequested();

            if (cokluSonuc is not null)
            {
                var dialog = new CokluKatDagitimWindow(cokluSonuc);
                bool onay = await dialog.ShowDialog<bool>(this);
                if (!onay)
                {
                    _vm.DurumMesaji = "Çoklu kat dağıtımı iptal edildi.";
                    return;
                }

                var secilenler = dialog.OnaylananSatirlar;
                if (secilenler.Count == 0)
                {
                    _vm.DurumMesaji = "Hiçbir kat seçilmedi — dağıtım yapılmadı.";
                    return;
                }

                int baslangicNo = _vm.Geo.Katlar.Count;
                BusyAyarla(true, "Kat planları üretiliyor…");
                var yeniKatlar = await Task.Run(() =>
                {
                    var liste = new List<BinaKat>();
                    for (int i = 0; i < secilenler.Count; i++)
                    {
                        var satir = secilenler[i];
                        liste.Add(yukleyici.CokluKatUret(satir.Aday, baslangicNo + i + 1, satir.Ad, satir.YukseklikM));
                    }
                    return liste;
                }, cts.Token);
                cts.Token.ThrowIfCancellationRequested();

                _vm.KatlarEkle(yeniKatlar);
                PlanCanvas?.FitAll();
                _vm.DurumMesaji = $"{yeniKatlar.Count} kat DXF'ten dağıtıldı: " +
                    string.Join(", ", yeniKatlar.Select(k => k.Ad));
                return;
            }

            int no = _vm.Geo.Katlar.Count + 1;
            var katAd = $"Kat {no}";

            var kat = await Task.Run(() => yukleyici.Yukle(path, no, katAd, 3.0), cts.Token);
            cts.Token.ThrowIfCancellationRequested();

            _vm.KatlarEkle([kat]);
            PlanCanvas?.FitAll();

            string uyari = kat.UnitWarning is { } w ? $"  ⚠ {w}" : "";
            _vm.DurumMesaji = $"DXF yüklendi: '{kat.Ad}'  · {kat.Odalar.Count} oda, {kat.Acikliklar.Count} açıklık{uyari}";
        }
        catch (OperationCanceledException)
        {
            _vm.DurumMesaji = "DXF içe aktarma iptal edildi.";
        }
        catch (Exception ex)
        {
            _vm.DurumMesaji = $"DXF yüklenemedi: {ex.Message}";
        }
        finally
        {
            if (ReferenceEquals(_islemCts, cts)) _islemCts = null;
            BusyAyarla(false);
        }
    }

    // ── Kat sırası / silme ────────────────────────────────────────────────────

    private void KatYukari_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.KatYukari();
        PlanCanvas?.InvalidateVisual();
    }

    private void KatAsagi_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.KatAsagi();
        PlanCanvas?.InvalidateVisual();
    }

    private async void KatSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var kat = _vm.SeciliKat;
        if (kat is null) return;

        var onay = await OnayCal($"'{kat.Ad}' katını silmek istiyor musunuz?", "Kat Sil");
        if (!onay) return;

        _vm.KatSil();
        PlanCanvas?.FitAll();
    }

    private void KatlariHizala_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.KatlariHizala();
        SyncOfsetXText();
        SyncOfsetYText();
        PlanCanvas?.FitAll();
    }

    // ── Seçili kat için DXF yükleme ───────────────────────────────────────────

    private async void DxfYukleSeciliKat_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var kat = _vm.SeciliKat;
        if (kat is null) return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "DXF Kat Planı Seç",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("DXF Dosyaları") { Patterns = ["*.dxf"] }]
        });
        var path = files.Count > 0 ? files[0].Path.LocalPath : null;
        if (string.IsNullOrWhiteSpace(path)) return;

        if ((kat.Odalar.Count > 0 || kat.DxfSegmentler.Count > 0) &&
            !await OnayCal($"'{kat.Ad}' katının mevcut DXF/oda geometrisi değiştirilecek. Devam edilsin mi?", "Kat DXF'ini Değiştir"))
            return;

        EtkilesimleriIptalEt();
        var beklenenGeo = _vm.Geo;
        _islemCts?.Cancel();
        using var cts = new CancellationTokenSource();
        _islemCts = cts;
        BusyAyarla(true, $"'{kat.Ad}' için DXF yükleniyor…");
        _vm.DurumMesaji = $"'{kat.Ad}' için DXF yükleniyor…";
        try
        {
            var yeniKat = await Task.Run(() =>
            {
                var yukleyici = new BinaServices.DxfKatYukleyici();
                return yukleyici.Yukle(path, kat.KatNo, kat.Ad, kat.KatYuksekligiM);
            }, cts.Token);
            cts.Token.ThrowIfCancellationRequested();

            var sonuc = _vm.SeciliKataDxfUygula(beklenenGeo, kat, yeniKat, path);
            _vm.DurumMesaji = sonuc.Mesaj;
            if (!sonuc.Basarili) return;

            if (KatDxfDosyaText is { } dt) dt.Text = Path.GetFileName(path);
            PlanCanvas?.FitAll();
        }
        catch (OperationCanceledException)
        {
            _vm.DurumMesaji = "DXF yükleme iptal edildi.";
        }
        catch (Exception ex)
        {
            _vm.DurumMesaji = $"DXF yüklenemedi: {ex.Message}";
        }
        finally
        {
            if (ReferenceEquals(_islemCts, cts)) _islemCts = null;
            BusyAyarla(false);
        }
    }

    // ── Kat yüksekliği TextBox ────────────────────────────────────────────────

    private void BinaAdi_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => UygulaBinaAdi();

    private void BinaAdi_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Return or Key.Enter) { UygulaBinaAdi(); e.Handled = true; }
    }

    private void UygulaBinaAdi()
    {
        _vm.BinaAdiGuncelle(BinaAdText?.Text);
        if (BinaAdText is { } tb) tb.Text = _vm.Geo.BinaAdi;
    }

    private void KatAdi_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => UygulaKatAdi();

    private void KatAdi_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Return or Key.Enter) { UygulaKatAdi(); e.Handled = true; }
    }

    private void UygulaKatAdi()
    {
        _vm.KatAdiGuncelle(KatAdText?.Text);
        if (KatAdText is { } tb) tb.Text = _vm.SeciliKat?.Ad ?? "";
    }

    private void OdaAdi_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => UygulaOdaAdi();

    private void OdaAdi_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Return or Key.Enter) { UygulaOdaAdi(); e.Handled = true; }
    }

    private void UygulaOdaAdi()
    {
        _vm.OdaAdiGuncelle(OdaAdText?.Text);
        if (OdaAdText is { } tb) tb.Text = _vm.SeciliOda?.Ad ?? "";
        PlanCanvas?.InvalidateVisual();
    }

    private void DxfGoster_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.KatGorunumuGuncelle(dxfGoster: DxfGosterCheck?.IsChecked == true);
        PlanCanvas?.InvalidateVisual();
    }

    private void BloklariGoster_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.KatGorunumuGuncelle(bloklariGoster: BloklariGosterCheck?.IsChecked == true);
        PlanCanvas?.InvalidateVisual();
    }

    private void KatYukseklik_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => UygulaKatYukseklik();

    private void KatYukseklik_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return) UygulaKatYukseklik();
    }

    private void UygulaKatYukseklik()
    {
        if (_vm.SeciliKat is not { } kat) return;
        if (double.TryParse(KatYukseklikText?.Text?.Replace(',', '.'),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double v))
        {
            _vm.KatYuksekligiGuncelle(v);
        }
        // Geri senkronize et (geçersiz giriş eski değere döner)
        if (KatYukseklikText is { } tb) tb.Text = kat.KatYuksekligiM.ToString("F2");
    }

    // ── Ofset X TextBox + ± butonları ─────────────────────────────────────────

    private void OfsetX_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => UygulaOfsetX();

    private void OfsetX_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return) UygulaOfsetX();
    }

    private void UygulaOfsetX()
    {
        if (double.TryParse(OfsetXText?.Text?.Replace(',', '.'),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double v))
            _vm.KatOfsetX = v;
        SyncOfsetXText();
        PlanCanvas?.InvalidateVisual();
    }

    private void OfsetXAzalt_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.KatOfsetX -= 0.5;
        SyncOfsetXText();
        PlanCanvas?.InvalidateVisual();
    }

    private void OfsetXArttir_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.KatOfsetX += 0.5;
        SyncOfsetXText();
        PlanCanvas?.InvalidateVisual();
    }

    // ── Ofset Y TextBox + ± butonları ─────────────────────────────────────────

    private void OfsetY_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => UygulaOfsetY();

    private void OfsetY_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return) UygulaOfsetY();
    }

    private void UygulaOfsetY()
    {
        if (double.TryParse(OfsetYText?.Text?.Replace(',', '.'),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double v))
            _vm.KatOfsetY = v;
        SyncOfsetYText();
        PlanCanvas?.InvalidateVisual();
    }

    private void OfsetYAzalt_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.KatOfsetY -= 0.5;
        SyncOfsetYText();
        PlanCanvas?.InvalidateVisual();
    }

    private void OfsetYArttir_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.KatOfsetY += 0.5;
        SyncOfsetYText();
        PlanCanvas?.InvalidateVisual();
    }

    private void ReferansTemizle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.ReferansKat = null;
        if (ReferansKatCombo is { } c) c.SelectedItem = null;
    }

    // ── Oda çizim / düzenleme ─────────────────────────────────────────────────

    private void OdaCiz_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => PlanCanvas?.OdaCizimBaslat();

    private void OdaCizBitir_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => PlanCanvas?.OdaCizimBitir();

    private void OdaCizIptal_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        PlanCanvas?.OdaCizimIptal();
        _vm.DurumMesaji = "Oda çizimi iptal edildi.";
    }

    private async void OdaSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var oda = _vm.SeciliOda;
        if (oda is null) return;
        var onay = await OnayCal($"'{oda.Ad}' odasını silmek istiyor musunuz?", "Oda Sil");
        if (!onay) return;
        _vm.SeciliOdaSil();
        PlanCanvas?.InvalidateVisual();
    }

    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (_isBusy)
        {
            if (e.Key == Key.Escape)
            {
                _islemCts?.Cancel();
                e.Handled = true;
            }
            return;
        }

        bool cmdVeyaCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Meta)
                        || e.KeyModifiers.HasFlag(KeyModifiers.Control);
        // Belge kısayolları TextBox odaktayken de çalışır. Z/Delete gibi metin
        // düzenleme komutlarıysa aşağıdaki erken dönüşten sonra ele alınır.
        if (cmdVeyaCtrl && e.Key == Key.S)
        {
            AktifEditoruUygula(e.Source);
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                FarkliKaydet_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs());
            else
                Kaydet_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs());
            e.Handled = true;
            return;
        }
        if (cmdVeyaCtrl && e.Key == Key.I)
        {
            AktifEditoruUygula(e.Source);
            DxfYukle_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs());
            e.Handled = true;
            return;
        }
        if (cmdVeyaCtrl && e.Key == Key.O)
        {
            AktifEditoruUygula(e.Source);
            Ac_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs());
            e.Handled = true;
            return;
        }
        if (cmdVeyaCtrl && e.Key == Key.N)
        {
            AktifEditoruUygula(e.Source);
            Yeni_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs());
            e.Handled = true;
            return;
        }

        // TextBox içinde yazarken undo/delete uygulama geometrisine gitmesin.
        if (e.Source is TextBox) return;

        // Cmd/Ctrl+Z = geri al, Shift+Cmd/Ctrl+Z = yinele
        if (cmdVeyaCtrl && e.Key == Key.Z)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                IleriAlRedo_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs());
            else
                GeriAlUndo_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs());
            e.Handled = true;
            return;
        }
        switch (e.Key)
        {
            case Key.Escape when PlanCanvas?.OdaCizimModu == true:
                PlanCanvas.OdaCizimIptal();
                _vm.DurumMesaji = "Oda çizimi iptal edildi.";
                e.Handled = true;
                break;
            case Key.Escape when PlanCanvas?.AciklikEkleModu == true:
                PlanCanvas.AciklikEkleIptal();
                _vm.DurumMesaji = "Açıklık yerleştirme iptal edildi.";
                e.Handled = true;
                break;
            case Key.Return or Key.Enter when PlanCanvas?.OdaCizimModu == true:
                PlanCanvas.OdaCizimBitir();
                e.Handled = true;
                break;
            case Key.F:
                PlanCanvas?.FitAll();
                e.Handled = true;
                break;
            case Key.D when _vm.KatVarMi:
                PlanCanvas?.OdaCizimBaslat();
                e.Handled = true;
                break;
            case Key.K when _vm.KatVarMi:
                KapiEkle_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.P when _vm.KatVarMi:
                PencereEkle_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.Delete or Key.Back when _vm.AciklikSecili && PlanCanvas?.OdaCizimModu != true:
                AciklikSil_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.Delete or Key.Back when _vm.OdaSecili && PlanCanvas?.OdaCizimModu != true:
                OdaSil_OnClick(null, new Avalonia.Interactivity.RoutedEventArgs());
                e.Handled = true;
                break;
        }
    }

    // ── Undo / Redo + 3D önizleme ─────────────────────────────────────────────

    private void GeriAlUndo_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.GeriAl();
        SyncAciklikPanel();
        PlanCanvas?.InvalidateVisual();
    }

    private void IleriAlRedo_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.IleriAl();
        SyncAciklikPanel();
        PlanCanvas?.InvalidateVisual();
    }

    private void Onizleme3D_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var pencere = new Bina3DOnizlemeWindow(_vm.Geo);
        pencere.Show(this);
    }

    // ── Döşeme/tavan patlatma ─────────────────────────────────────────────────

    private void TavanPatlat_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.TavanPatlat();
        PlanCanvas?.InvalidateVisual();
    }

    private void DosemePatlat_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.DosemePatlat();
        PlanCanvas?.InvalidateVisual();
    }

    private void PatlatmaGeriAl_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.PatlatmaGeriAl();
        PlanCanvas?.InvalidateVisual();
    }

    // ── Açıklık yönetimi ──────────────────────────────────────────────────────

    private void AciklikTara_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.AciklikTara();
        PlanCanvas?.InvalidateVisual();
    }

    private void KapiEkle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _aktifAciklikTipi = BinaAciklikTipi.Kapi;
        PlanCanvas?.AciklikEkleBaslat(BinaAciklikTipi.Kapi);
        _vm.DurumMesaji = "Kapı yerleştirme: duvar üzerinde başlangıç ve bitiş noktasına tıklayın.";
    }

    private void PencereEkle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _aktifAciklikTipi = BinaAciklikTipi.Pencere;
        PlanCanvas?.AciklikEkleBaslat(BinaAciklikTipi.Pencere);
        _vm.DurumMesaji = "Pencere yerleştirme: duvar üzerinde başlangıç ve bitiş noktasına tıklayın.";
    }

    private void AciklikEkleIptal_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        PlanCanvas?.AciklikEkleIptal();
        _vm.DurumMesaji = "Açıklık yerleştirme iptal edildi.";
    }

    private void IslemIptal_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!_isBusy) return;
        if (IslemIptalBtn is { } btn) btn.IsEnabled = false;
        if (IslemMesajText is { } mesaj) mesaj.Text = "İptal ediliyor…";
        _islemCts?.Cancel();
    }

    private async void AciklikSil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var ac = _vm.SeciliAciklik;
        if (ac is null) return;
        string tipAd = ac.Tip == BinaAciklikTipi.Pencere ? "pencereyi" : "kapıyı";
        var onay = await OnayCal($"Seçili {tipAd} silmek istiyor musunuz?", "Açıklık Sil");
        if (!onay) return;
        _vm.SeciliAciklikSil();
        PlanCanvas?.InvalidateVisual();
    }

    private void AciklikIptal_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.SeciliAciklikIptalToggle();
        PlanCanvas?.InvalidateVisual();
    }

    private bool _aciklikTipSyncing;

    private void AciklikTip_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_aciklikTipSyncing) return;
        if (_vm.SeciliAciklik is null || AciklikTipCombo is not { } combo) return;
        _vm.SeciliAciklikTipGuncelle(
            combo.SelectedIndex == 1 ? BinaAciklikTipi.Pencere : BinaAciklikTipi.Kapi);
        PlanCanvas?.InvalidateVisual();
    }

    /// Seçili açıklık değişince toolbar alanlarını doldurur.
    private void SyncAciklikPanel()
    {
        var ac = _vm.SeciliAciklik;
        _aciklikTipSyncing = true;
        if (AciklikTipCombo is { } combo)
            combo.SelectedIndex = ac?.Tip == BinaAciklikTipi.Pencere ? 1 : 0;
        _aciklikTipSyncing = false;

        if (AciklikGenislikText is { } gt)
            gt.Text = ac is null ? "" : $"{ac.GenislikM:F2} m";
        if (AciklikYukseklikText is { } yt)
            yt.Text = ac is null ? "" : ac.YukseklikM.ToString("F2");
        if (AciklikEsikText is { } et)
            et.Text = ac is null ? "" : ac.EtkinEsikYuksekligiM.ToString("F2");
        if (AciklikIptalCheck is { } ic)
            ic.IsChecked = ac?.IptalEdildi ?? false;
    }

    private void AciklikYukseklik_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => UygulaAciklikYukseklik();

    private void AciklikYukseklik_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return) UygulaAciklikYukseklik();
    }

    private void UygulaAciklikYukseklik()
    {
        var ac = _vm.SeciliAciklik;
        if (ac is null) return;
        if (double.TryParse(AciklikYukseklikText?.Text?.Replace(',', '.'),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double v))
        {
            _vm.SeciliAciklikYukseklikGuncelle(v);
        }
        if (AciklikYukseklikText is { } tb) tb.Text = ac.YukseklikM.ToString("F2");
    }

    private void AciklikEsik_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => UygulaAciklikEsik();

    private void AciklikEsik_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return) UygulaAciklikEsik();
    }

    private void UygulaAciklikEsik()
    {
        var ac = _vm.SeciliAciklik;
        if (ac is null) return;
        if (double.TryParse(AciklikEsikText?.Text?.Replace(',', '.'),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double v))
        {
            _vm.SeciliAciklikEsikGuncelle(v);
        }
        if (AciklikEsikText is { } tb) tb.Text = ac.EtkinEsikYuksekligiM.ToString("F2");
    }

    private enum KapanisSecimi
    {
        Iptal,
        KaydetVeCik,
        KaydetmedenCik
    }

    private async Task<bool> KapanisOnayiAl()
        => await KaydedilmemisDegisiklikOnayiAl("Modülü kapatmak istediğinize emin misiniz?");

    /// Kaydedilmemiş değişiklik varsa kullanıcıya sorar (codex §4.2); temizse sormadan devam eder.
    private async Task<bool> KaydedilmemisDegisiklikOnayiAl(string mesaj)
    {
        if (!_vm.IsDirty) return true;
        var secim = await KapanisSecimiSor(mesaj);
        switch (secim)
        {
            case KapanisSecimi.KaydetVeCik:
                return await Kaydet();
            case KapanisSecimi.KaydetmedenCik:
                return true;
            default:
                return false;
        }
    }

    private async Task<KapanisSecimi> KapanisSecimiSor(string mesaj)
    {
        var dialog = new Window
        {
            Title = "Kaydedilmemiş Değişiklikler",
            Width = 500,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = Avalonia.Media.Brush.Parse("#0D1E2E")
        };

        var sonuc = KapanisSecimi.Iptal;
        var kaydet = DialogButton("Kaydet ve Devam Et", "#0E5B82", "#1A8BBF", Avalonia.Media.Brushes.White, 150);
        var kaydetmeden = DialogButton("Kaydetmeden Devam Et", "#3A1010", "#7A2020", Avalonia.Media.Brush.Parse("#F0B0B0"), 165);
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
                    Text = mesaj,
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

    private static Button DialogButton(
        string text,
        string background,
        string border,
        Avalonia.Media.IBrush foreground,
        double width)
        => new()
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

    // ── Onay dialog yardımcısı ────────────────────────────────────────────────

    private async Task<bool> OnayCal(string mesaj, string baslik)
    {
        var dialog = new Window
        {
            Title = baslik,
            Width = 360, Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = Avalonia.Media.Brush.Parse("#0D1E2E")
        };

        var sonuc = false;
        var evet = new Button
        {
            Content = "Evet", Width = 90,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Background = Avalonia.Media.Brush.Parse("#0E5B82"),
            BorderBrush = Avalonia.Media.Brush.Parse("#1A8BBF"),
            Foreground = Avalonia.Media.Brushes.White,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            Padding = new Thickness(14, 8),
            CornerRadius = new Avalonia.CornerRadius(6)
        };
        var hayir = new Button
        {
            Content = "Hayır", Width = 90,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Background = Avalonia.Media.Brush.Parse("#0D2535"),
            BorderBrush = Avalonia.Media.Brush.Parse("#1A5070"),
            Foreground = Avalonia.Media.Brush.Parse("#C8E4F4"),
            Padding = new Thickness(14, 8),
            CornerRadius = new Avalonia.CornerRadius(6)
        };
        evet.Click += (_, _) => { sonuc = true; dialog.Close(); };
        hayir.Click += (_, _) => dialog.Close();

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 20,
            Children =
            {
                new TextBlock
                {
                    Text = mesaj,
                    Foreground = Avalonia.Media.Brush.Parse("#B0D8F0"),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                },
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Spacing = 12,
                    Children = { evet, hayir }
                }
            }
        };

        await dialog.ShowDialog(this);
        return sonuc;
    }
}
