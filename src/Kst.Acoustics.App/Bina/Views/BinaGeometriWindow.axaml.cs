using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Platform.Storage;
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

    // Geri butonunun döneceği pencere fabrikası — her launcher kendi tipini iletir.
    private readonly Func<Window>? _geriPencere;

    // Parameterless ctor: Avalonia AXAML loader için zorunlu.
    public BinaGeometriWindow() : this(null) { }

    public BinaGeometriWindow(Func<Window>? geriPencere)
    {
        InitializeComponent();
        DataContext = _vm;
        _geriPencere = geriPencere;
        Opened += OnOpened;
        Closing += OnWindowClosing;
    }

    private BinaKat? _izlenenKat; // DxfGoster/BloklariGoster PropertyChanged aboneliği için

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
            };

            // Düşey birleşim işaretleri (⬆/⬇ etiketleri)
            c.OdaBirlesimSorgu = odaId => (
                BinaDuseyBirlesimServisi.TavanAcikMi(_vm.Geo, odaId),
                BinaDuseyBirlesimServisi.DosemeAcikMi(_vm.Geo, odaId));
        }
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BinaGeometriViewModel.SeciliKat))
            SyncEditPanelFromVm();
        else if (e.PropertyName == nameof(BinaGeometriViewModel.KatOfsetX))
            SyncOfsetXText();
        else if (e.PropertyName == nameof(BinaGeometriViewModel.KatOfsetY))
            SyncOfsetYText();
        else if (e.PropertyName == nameof(BinaGeometriViewModel.SeciliOdaId))
            SyncIzlenenOda();
    }

    private BinaOda? _izlenenOda; // oda adı düzenlemesini dirty + repaint için izle

    private void SyncIzlenenOda()
    {
        if (_izlenenOda is not null)
            _izlenenOda.PropertyChanged -= OnOdaPropertyChanged;
        _izlenenOda = _vm.SeciliOda;
        if (_izlenenOda is not null)
            _izlenenOda.PropertyChanged += OnOdaPropertyChanged;
    }

    private void OnOdaPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BinaOda.Ad))
        {
            _vm.Kirlet();
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
                _vm.Kirlet();
                PlanCanvas?.InvalidateVisual();
                break;
            case nameof(BinaKat.Ad):
            case nameof(BinaKat.KatYuksekligiM):
                _vm.Kirlet();
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

    // ── Dosya işlemleri ───────────────────────────────────────────────────────

    private async void Yeni_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!await KaydedilmemisDegisiklikOnayiAl("Kaydedilmemiş değişiklikler var. Yeni bina geometrisi oluşturmak istiyor musunuz?"))
            return;

        _vm.Geo = BinaGeometriServisi.YeniOlustur();
        _vm.SeciliKat = null;
        _vm.UndoYiginlariniTemizle();
        _vm.DurumMesaji = "Yeni bina geometrisi oluşturuldu.";
    }

    private async void Ac_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
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

        DosyaAc(path);
    }

    public void DosyaAc(string path)
    {
        try
        {
            _vm.Geo = BinaGeometriServisi.Ac(path);
            _vm.SeciliKat = _vm.Geo.Katlar.Count > 0 ? _vm.Geo.Katlar[0] : null;
            _vm.UndoYiginlariniTemizle();
            _vm.DurumMesaji = $"Açıldı: {Path.GetFileName(path)}  (Rev. {_vm.Geo.GeometriRevizyon})";
        }
        catch (Exception ex)
        {
            _vm.DurumMesaji = $"Açılamadı: {ex.Message}";
        }
    }

    private async void Kaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await Kaydet();
    }

    private async void FarkliKaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await FarkliKaydet();

    private async Task<bool> Kaydet()
    {
        if (string.IsNullOrWhiteSpace(_vm.Geo.DosyaYolu))
            return await FarkliKaydet();

        return KaydetYola(_vm.Geo.DosyaYolu);
    }

    private async Task<bool> FarkliKaydet()
    {
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
        int no = _vm.Geo.Katlar.Count + 1;
        var kat = new BinaKat
        {
            Id = Guid.NewGuid().ToString(),
            Ad = $"Kat {no}",
            KatNo = no,
            KatYuksekligiM = 3.0
        };
        _vm.Geo.Katlar.Add(kat);
        _vm.SeciliKat = kat;
        _vm.Kirlet();
        _vm.DurumMesaji = $"'{kat.Ad}' eklendi.";
    }

    private async void DxfYukle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "DXF Kat Planı Seç",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("DXF Dosyaları") { Patterns = ["*.dxf"] }]
        });
        var path = files.Count > 0 ? files[0].Path.LocalPath : null;
        if (string.IsNullOrWhiteSpace(path)) return;

        _vm.DurumMesaji = "DXF taranıyor…";
        try
        {
            var yukleyici = new BinaServices.DxfKatYukleyici();

            // Tek dosyada yan yana birden fazla kat planı olup olmadığını önce
            // otomatik tespit et (codex50 §9). Çoklu plan bulunursa dağıtım
            // diyaloğu açılır; tek plansa mevcut tek-kat akışı aynen sürer.
            var cokluSonuc = await Task.Run(() => yukleyici.CokluKatTespitEt(path));

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
                var yeniKatlar = await Task.Run(() =>
                {
                    var liste = new List<BinaKat>();
                    for (int i = 0; i < secilenler.Count; i++)
                    {
                        var satir = secilenler[i];
                        liste.Add(yukleyici.CokluKatUret(satir.Aday, baslangicNo + i + 1, satir.Ad, satir.YukseklikM));
                    }
                    return liste;
                });

                foreach (var yeniKat in yeniKatlar) _vm.Geo.Katlar.Add(yeniKat);
                _vm.SeciliKat = yeniKatlar[0];
                _vm.Kirlet();
                PlanCanvas?.FitAll();
                _vm.DurumMesaji = $"{yeniKatlar.Count} kat DXF'ten dağıtıldı: " +
                    string.Join(", ", yeniKatlar.Select(k => k.Ad));
                return;
            }

            int no = _vm.Geo.Katlar.Count + 1;
            var katAd = $"Kat {no}";

            var kat = await Task.Run(() => yukleyici.Yukle(path, no, katAd, 3.0));

            _vm.Geo.Katlar.Add(kat);
            _vm.SeciliKat = kat;
            _vm.Kirlet();
            PlanCanvas?.FitAll();

            string uyari = kat.UnitWarning is { } w ? $"  ⚠ {w}" : "";
            _vm.DurumMesaji = $"DXF yüklendi: '{kat.Ad}'  · {kat.Odalar.Count} oda, {kat.Acikliklar.Count} açıklık{uyari}";
        }
        catch (Exception ex)
        {
            _vm.DurumMesaji = $"DXF yüklenemedi: {ex.Message}";
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

        _vm.DurumMesaji = $"'{kat.Ad}' için DXF yükleniyor…";
        try
        {
            var yeniKat = await Task.Run(() =>
            {
                var yukleyici = new BinaServices.DxfKatYukleyici();
                return yukleyici.Yukle(path, kat.KatNo, kat.Ad, kat.KatYuksekligiM);
            });

            var eskiOdalar = kat.Odalar.ToList();
            var eskiAcikliklar = kat.Acikliklar.ToList();

            // Id kararlılığı (codex50 §3.5): yeni odalar eskilerle merkez-örtüşme
            // üzerinden eşleştirilir; eşleşmeyen eski odalar "silinmiş" sayılır.
            var silinenOdaIdler = BinaServices.OdaEslestirmeServisi.Eslestir(yeniKat.Odalar, eskiOdalar);

            kat.DxfFilePath  = path;
            kat.DxfSegmentler = yeniKat.DxfSegmentler;
            kat.DxfArclar    = yeniKat.DxfArclar;
            kat.UnitWarning  = yeniKat.UnitWarning;

            // Flood-fill ile tespit edilen oda blokları kata aktarılır (Id'leri
            // yukarıda eşleştirmeyle korunmuş olabilir).
            kat.Odalar.Clear();
            foreach (var oda in yeniKat.Odalar) kat.Odalar.Add(oda);

            // Açıklıklar: manuel eklenen / iptal edilen kayıtlar eski listeden korunur,
            // otomatik tespit edilenler yeni duvar geometrisine göre yenilenir.
            var arclarTuple = kat.DxfArclar
                .Select(a => (a.Merkez, a.R, a.BasAciDeg, a.BitAciDeg))
                .ToList();
            kat.Acikliklar = BinaServices.AciklikTespitServisi.Bul(
                yeniKat.HamSegmentler, arclarTuple, eskiAcikliklar);
            BinaServices.AciklikYardimci.DisindaKalanlariSil(kat);
            BinaServices.AciklikYardimci.KomsuDuvarlariTamamla(kat);
            kat.SayilariYenile();
            kat.HamSegmentler = yeniKat.HamSegmentler;
            kat.HamMetinler  = yeniKat.HamMetinler;

            // Askıda birleşim bırakma: eşleşmeyen (silinmiş sayılan) eski odaların
            // düşey birleşimleri kaldırılır (aksi halde hayalet açık tavan/döşeme kalır).
            int kaldirilanBirlesim = 0;
            foreach (var id in silinenOdaIdler)
                kaldirilanBirlesim += BinaDuseyBirlesimServisi.OdaSilindi(_vm.Geo, id);

            _vm.SeciliOdaId = null;
            _vm.SeciliAciklikId = null;
            _vm.Kirlet();

            if (KatDxfDosyaText is { } dt) dt.Text = Path.GetFileName(path);
            PlanCanvas?.FitAll();

            string uyariStr = yeniKat.UnitWarning is { } w ? $"  ⚠ {w}" : "";
            string birlesimStr = kaldirilanBirlesim > 0 ? $", {kaldirilanBirlesim} düşey birleşim kaldırıldı" : "";
            _vm.DurumMesaji = $"DXF güncellendi: '{kat.Ad}'  · {kat.Odalar.Count} oda, {kat.Acikliklar.Count} açıklık, {kat.DxfSegmentler.Count} segment{birlesimStr}{uyariStr}";
        }
        catch (Exception ex)
        {
            _vm.DurumMesaji = $"DXF yüklenemedi: {ex.Message}";
        }
    }

    // ── Kat yüksekliği TextBox ────────────────────────────────────────────────

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
                out double v) && v > 0)
        {
            kat.KatYuksekligiM = v;
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
        // TextBox içinde yazarken kısayollar devreye girmesin
        if (e.Source is TextBox) return;

        // Cmd/Ctrl+Z = geri al, Shift+Cmd/Ctrl+Z = yinele
        bool cmdVeyaCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Meta)
                        || e.KeyModifiers.HasFlag(KeyModifiers.Control);
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
        PlanCanvas?.AciklikEkleBaslat(BinaAciklikTipi.Kapi);
        _vm.DurumMesaji = "Kapı yerleştirme: duvar üzerinde başlangıç ve bitiş noktasına tıklayın.";
    }

    private void PencereEkle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        PlanCanvas?.AciklikEkleBaslat(BinaAciklikTipi.Pencere);
        _vm.DurumMesaji = "Pencere yerleştirme: duvar üzerinde başlangıç ve bitiş noktasına tıklayın.";
    }

    private void AciklikEkleIptal_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        PlanCanvas?.AciklikEkleIptal();
        _vm.DurumMesaji = "Açıklık yerleştirme iptal edildi.";
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
        var ac = _vm.SeciliAciklik;
        if (ac is null || AciklikTipCombo is not { } combo) return;
        ac.Tip = combo.SelectedIndex == 1 ? BinaAciklikTipi.Pencere : BinaAciklikTipi.Kapi;
        _vm.Kirlet();
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
                out double v) && v > 0)
        {
            if (System.Math.Abs(ac.YukseklikM - v) > 1e-9) _vm.Kirlet();
            ac.YukseklikM = v;
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
                out double v) && v >= 0)
        {
            if (System.Math.Abs(ac.EsikYuksekligiM - v) > 1e-9) _vm.Kirlet();
            ac.EsikYuksekligiM = v;
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
