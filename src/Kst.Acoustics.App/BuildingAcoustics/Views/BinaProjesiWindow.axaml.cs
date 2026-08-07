using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Kst.Acoustics.App.BuildingAcoustics.ViewModels;
using Kst.Acoustics.App.Controls;
using Kst.Acoustics.App.Views;

namespace Kst.Acoustics.App.BuildingAcoustics.Views;

public partial class BinaProjesiWindow : Window
{
    private bool _kapanisOnaylandi;

    public BinaProjesiWindow()
    {
        InitializeComponent();
        var vm = new BinaProjesiViewModel();
        vm.RequestClose = () => _ = AnaMenuyeDon(vm);
        DataContext = vm;
        Closing += OnWindowClosing;

        var preview = this.FindControl<GeometryPreviewControl>("GeometryPreview");
        if (preview is not null)
        {
            // 4.4 sökümü: geometri düzenleme (vertex/edge sürükleme, köşe ekle/sil,
            // manuel açıklık, kat hizalama sürüklemesi) M50'ye taşındı — M08'de
            // geometriyi değiştiren canvas etkileşimi kalmaz.
            preview.OnViewportClicked    = (x, y, multiSelect) =>
            {
                if (!vm.TrySelectAciklik(x, y))
                    vm.PickRoomAt(x, y);
            };
            // UX — Hover hit-test
            preview.OnHoverWorldPoint = (x, y)                       => vm.GuncelleHover(x, y);
            preview.OnHoverLeave      = ()                           => vm.TemizleHover();

            // ViewModel property → control property senkronizasyonu
            vm.PropertyChanged += (_, e) =>
            {
                switch (e.PropertyName)
                {
                    // 5.1 temizliği: oda çizim / manuel açıklık / kat hizalama
                    // forwarding'leri kaldırıldı — geometri düzenleme M50'de.
                    case nameof(vm.SeciliBolmeWallA):
                        preview.SelectedWallA = vm.SeciliBolmeWallA;
                        break;
                    case nameof(vm.SeciliBolmeWallB):
                        preview.SelectedWallB = vm.SeciliBolmeWallB;
                        break;
                    case nameof(vm.SeciliAciklikSegment):
                        preview.HighlightAciklikSegment = vm.SeciliAciklikSegment;
                        break;
                }
            };
        }

        // Ribbon kategorisi açık/kapalı → sol panel kolon genişliği toggle
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.KategoriPaneliAcik))
                GuncelleSolMenuGenislik(vm.KategoriPaneliAcik);
        };

        var bolmePanel = this.FindControl<BolmeMalzemePanel>("BolmeMalzemePaneli");
        if (bolmePanel != null)
        {
            bolmePanel.OnBolmeSecildi = b => vm.SeciliBolmePaneli = b;
            bolmePanel.EkBolmeSaglayici = vm.BirlesikPartnerDuvarlari;
            bolmePanel.OnDosemeKataUygula = vm.SeciliKatDosemeUygula;
            bolmePanel.OnAciklikDegisti = a =>
            {
                vm.SeciliAciklikPaneli = a;
                vm.ZorlaCizimYenile();
                vm.Kirlet(); // Menfez/Açıklık listeleri INotifyPropertyChanged uygulamıyor
                // P1 — açıklık (kapı/pencere) malzemesi AtamaTamamMi'yi etkiler; BolmeAcikligi
                // INotifyPropertyChanged uygulamadığından mekan listesindeki yeşil işaretleme
                // elle tazelenmeli, aksi halde eksik atama sonrası bile yeşil kalabilir.
                vm.SeciliMekan?.BildirAtamaDurumu();
            };
            // Bölme malzemesi atandığında doğrulama panelini tazele (HESAP ÖNCESİ KONTROL)
            bolmePanel.OnBolmeMalzemesiAtandi = () =>
            {
                vm.GuncelleDogrulamaUyarisi();
                vm.ZorlaCizimYenile();
                vm.Kirlet();
            };
            // Junction ayarları INotifyPropertyChanged uygulamıyor — IsDirty burada işaretlenir.
            bolmePanel.OnJunctionDegisti = () => vm.Kirlet();
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(vm.SeciliAciklikPaneli))
                    bolmePanel.SelectBolmeAndAciklik(vm.SeciliBolmePaneli, vm.SeciliAciklikPaneli);
                // "Atama Bekleyenler"den bir duvara navigasyon: oda paneli yeniden kurulduktan
                // sonra ilgili duvarı seç (Dispatcher ile ertelenir).
                else if (e.PropertyName == nameof(vm.SeciliEksikBolme) && vm.SeciliEksikBolme is { } eksik)
                    Avalonia.Threading.Dispatcher.UIThread.Post(
                        () => bolmePanel.SecBolme(eksik),
                        Avalonia.Threading.DispatcherPriority.Background);
            };
        }

        // ESC → iptal, ENTER → bitir
        KeyDown += (_, e) =>
        {
            // Cmd/Ctrl+Shift+Z → Yinele (redo)
            if ((e.KeyModifiers.HasFlag(KeyModifiers.Meta) || e.KeyModifiers.HasFlag(KeyModifiers.Control)) &&
                e.KeyModifiers.HasFlag(KeyModifiers.Shift) &&
                e.Key == Key.Z)
            {
                vm.YineleCommand.Execute(null);
                e.Handled = true;
                return;
            }
            // Cmd/Ctrl+Z → Geri Al (undo)
            if ((e.KeyModifiers.HasFlag(KeyModifiers.Meta) || e.KeyModifiers.HasFlag(KeyModifiers.Control)) &&
                e.Key == Key.Z)
            {
                vm.GeriAlCommand.Execute(null);
                e.Handled = true;
                return;
            }

            // 5.1 temizliği: Delete ile mekan/açıklık silme ve oda çizim
            // kısayolları kaldırıldı — geometri yalnız M50'de düzenlenir.
        };
    }

    /// Bağlı bina geometrisini M50'de açar. M08 penceresi açık kalır; kullanıcı M50'de
    /// kaydettikten sonra "Bina Geometrisi Yükle" ile güncel revizyonu içe alır.
    private void M50DeDuzenle_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not BinaProjesiViewModel vm) return;
        if (vm.ProjeKilitli)
        {
            vm.DxfUyariMesaji = "Kilitli projede M50 geometri düzenlemesi açılamaz.";
            return;
        }

        var window = new Kst.Acoustics.App.Bina.Views.BinaGeometriWindow(null);
        var path = vm.Proje.BinaGeometriYolu;
        var oncekiRevizyon = vm.Proje.BinaGeometriRevizyon;
        if (!string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path))
            window.DosyaAc(path);

        window.Closed += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) return;
            try
            {
                var guncel = Kst.Acoustics.Core.Bina.Services.BinaGeometriServisi.Ac(path);
                if (oncekiRevizyon is null || guncel.GeometriRevizyon != oncekiRevizyon.Value)
                    vm.BagliGeometriDegisikliginiBildir(guncel.GeometriRevizyon);
            }
            catch (System.Exception ex)
            {
                vm.DxfUyariMesaji = $"M50 geometri değişikliği denetlenemedi: {ex.Message}";
            }
        };

        window.Show();
        vm.DxfUyariMesaji = !string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path)
            ? "Bağlı geometri M50'de açıldı. Kaydettikten sonra 'Bina Geometrisi Yükle' ile M08'i güncelleyin."
            : "M50 açıldı. Geometriyi .e4bina olarak kaydedip 'Bina Geometrisi Yükle' ile M08'e aktarın.";
    }

    /// "Ana Menü" — kaydedilmemiş değişiklik varsa önce sorar, sonra LauncherWindow'a döner.
    private async Task AnaMenuyeDon(BinaProjesiViewModel vm)
    {
        if (!await vm.OnayKaydedilmemisDegisiklik(this, "Ana Menü'ye dön")) return;

        var launcher = new LauncherWindow();
        // Çoklu ekran fix — Ana Menü'ye dönerken launcher, modülün bulunduğu ekranda açılmalı
        // (LauncherWindow XAML'i CenterScreen → birincil monitöre kaçıyordu).
        launcher.WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.Manual;
        var screen = this.Screens?.ScreenFromWindow(this);
        if (screen != null)
        {
            var area = screen.WorkingArea;
            int w = (int)(double.IsNaN(launcher.Width)  || launcher.Width  < 100 ? 1120 : launcher.Width);
            int h = (int)(double.IsNaN(launcher.Height) || launcher.Height < 100 ? 760  : launcher.Height);
            w = System.Math.Min(w, area.Width);
            h = System.Math.Min(h, area.Height);
            launcher.Position = new Avalonia.PixelPoint(
                area.X + (area.Width  - w) / 2,
                area.Y + (area.Height - h) / 2);
        }
        else
        {
            launcher.Position = this.Position;
        }

        if (Avalonia.Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = launcher;
        launcher.Show();
        _kapanisOnaylandi = true;
        vm.Dispose();
        Close();
    }

    /// Pencere X ile kapatılırken kaydedilmemiş değişiklik varsa sorar (maincodex §4.2).
    private async void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_kapanisOnaylandi) return;
        if (DataContext is not BinaProjesiViewModel vm) return;

        e.Cancel = true;
        if (!await vm.OnayKaydedilmemisDegisiklik(this, "Pencereyi kapat")) return;

        _kapanisOnaylandi = true;
        vm.Dispose();
        Close();
    }

    // Kat ismi: tek tık yalnızca satırı seçer; çift tık TextBlock'u gizleyip TextBox'ı açar.
    private void KatAdi_BeginEdit(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is not TextBlock text || text.Parent is not Panel panel) return;
        var edit = panel.Children.OfType<TextBox>().FirstOrDefault();
        if (edit == null) return;
        text.IsVisible = false;
        edit.IsVisible = true;
        edit.Focus();
        edit.SelectAll();
    }

    private void KatAdi_EndEdit(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not TextBox edit || edit.Parent is not Panel panel) return;
        edit.IsVisible = false;
        var text = panel.Children.OfType<TextBlock>().FirstOrDefault();
        if (text != null) text.IsVisible = true;
    }

    // Enter/Escape düzenlemeyi bitirir (değer UpdateSourceTrigger=PropertyChanged ile zaten yazılı).
    private void KatAdi_EditKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter && e.Key != Key.Escape) return;
        if (sender is not TextBox edit || edit.Parent is not Panel panel) return;
        edit.IsVisible = false;
        var text = panel.Children.OfType<TextBlock>().FirstOrDefault();
        if (text != null) text.IsVisible = true;
        e.Handled = true;
    }

    // ── Yönetmelik butonları ─────────────────────────────────────────────────

    private static readonly string YonetmelikKlasor =
        System.IO.Path.Combine(AppContext.BaseDirectory, "Yonetmelik");

    private void YonetmelikBtn_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Butona tıklanınca context menüyü açar
        if (sender is Button btn)
            btn.ContextMenu?.Open(btn);
    }

    private void YonetmelikPdf_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => YonetmelikDosyaAc("*.pdf", "GeneratePdf.pdf");

    private void YonetmelikEk_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => YonetmelikDosyaAc("*.docx", "7.5.23616-Ek.docx");

    private void YonetmelikDosyaAc(string aramaDeseni, string tercihliDosya)
    {
        foreach (var klasor in new[] { YonetmelikKlasor })
        {
            var tam = System.IO.Path.Combine(klasor, tercihliDosya);
            if (System.IO.File.Exists(tam))
            {
                AcDosya(tam);
                return;
            }
            // Tam yol yoksa desenle ara
            if (System.IO.Directory.Exists(klasor))
            {
                var bulunan = System.IO.Directory.GetFiles(klasor, aramaDeseni).FirstOrDefault();
                if (bulunan != null) { AcDosya(bulunan); return; }
            }
        }

        if (DataContext is ViewModels.BinaProjesiViewModel vm)
            vm.DxfUyariMesaji = $"Yönetmelik dosyası bulunamadı: {tercihliDosya}";
    }

    private static void AcDosya(string yol)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = yol,
                UseShellExecute = true   // işletim sistemi varsayılan uygulamasıyla açar
            });
        }
        catch { /* yok say */ }
    }

    // UX — DXF uyarı bandını kapat (× butonu)
    private void DxfUyariKapat_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is BuildingAcoustics.ViewModels.BinaProjesiViewModel vm)
            vm.DxfUyariMesaji = null;
    }

    // ── Sol/Alt menü toggle ─────────────────────────────────────────────────

    private double _solMenuSonGenislik = 320;
    private double _altMenuSonYukseklik = 280;
    private bool _altMenuAcik = true;

    /// Ribbon kategori değişikliğine bağlı sol panel genişlik toggle.
    /// Açık: kullanıcının önceki splitter genişliği (varsayılan 320); kapalı: 0 + splitter gizli.
    private void GuncelleSolMenuGenislik(bool acik)
    {
        if (AnaGrid.ColumnDefinitions.Count < 3) return;
        var col = AnaGrid.ColumnDefinitions[0];
        var splitterCol = AnaGrid.ColumnDefinitions[1];
        if (acik)
        {
            col.Width = new Avalonia.Controls.GridLength(_solMenuSonGenislik);
            splitterCol.Width = new Avalonia.Controls.GridLength(6);
            SolMenuSplitter.IsVisible = true;
        }
        else
        {
            if (col.Width.IsAbsolute && col.Width.Value > 0)
                _solMenuSonGenislik = col.Width.Value;
            col.Width = new Avalonia.Controls.GridLength(0);
            splitterCol.Width = new Avalonia.Controls.GridLength(0);
            SolMenuSplitter.IsVisible = false;
        }
    }

    private void AltMenuToggle_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (OrtaGrid.RowDefinitions.Count < 4) return;
        var splitterRow = OrtaGrid.RowDefinitions[2];
        var altRow      = OrtaGrid.RowDefinitions[3];
        if (_altMenuAcik)
        {
            if (altRow.Height.IsAbsolute && altRow.Height.Value > 0)
                _altMenuSonYukseklik = altRow.Height.Value;
            splitterRow.Height = new Avalonia.Controls.GridLength(0);
            altRow.Height      = new Avalonia.Controls.GridLength(0);
            AltMenuToggleBtn.Content = "⮝";
        }
        else
        {
            splitterRow.Height = new Avalonia.Controls.GridLength(6);
            altRow.Height      = new Avalonia.Controls.GridLength(_altMenuSonYukseklik);
            AltMenuToggleBtn.Content = "⮟";
        }
        _altMenuAcik = !_altMenuAcik;
    }

    // UX — Drag-drop yapı elemanı atama (basit pointer-based; Avalonia 12 statik referans yöntemi).
    // BolmeMalzemePanel'den sürüklenmiş malzeme varsa viewport'a tıklama bırakıldığında uygulanır.
    private void Viewport_PointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        var malzeme = BolmeMalzemePanel.AktifSurukleMalzemesi;
        if (malzeme == null) return;
        // Drag-drop'ı her durumda temizle
        BolmeMalzemePanel.AktifSurukleMalzemesi = null;

        if (DataContext is not BuildingAcoustics.ViewModels.BinaProjesiViewModel vm) return;
        var preview = this.FindControl<Kst.Acoustics.App.Controls.GeometryPreviewControl>("GeometryPreview");
        if (preview == null) return;
        var local = e.GetPosition(preview);
        if (!preview.TryGetWorldPoint(local, out double wx, out double wy)) return;
        vm.AtaMalzemeyeDragDrop(malzeme, wx, wy);
    }
}
