using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Kst.Acoustics.App.Views;
using Kst.Acoustics.Core.Electric.KesifHazirla;

namespace Kst.Acoustics.App.Electric.KesifHazirla;

public partial class KesifHazirlaWindow : Window
{
    private KesifHazirlaViewModel ViewModel => (KesifHazirlaViewModel)DataContext!;
    private string? _acikDosyaYolu;

    private DataGridColumn? _colMiktar;
    private DataGridColumn? _colSonGuncelleme;
    private string _kaydedilenProjeParmakIzi = "";
    private bool _kayitSuruyor;
    private bool _dosyaAciliyor;
    private KesifProjeDegisiklikIzleyici? _degisiklikIzleyici;
    private bool _kayitDurumuGuncellemesiBekliyor;

    private bool BelgeIslemiSuruyor()
    {
        if (!_kayitSuruyor && !_dosyaAciliyor) return false;
        ViewModel.DurumMesaji = "Dosya işlemi sürüyor; tamamlanmasını bekleyin.";
        return true;
    }

    public KesifHazirlaWindow()
    {
        // HATA-10: DataContext, InitializeComponent'ten önce atanır ki
        // başlangıç bağlamaları/handler'lar geçerli ViewModel'e erişsin.
        DataContext = new KesifHazirlaViewModel();
        InitializeComponent();

        // Kolon referanslarını bir kez yakala (çalışma anında başlık metniyle değil referansla karşılaştırılır).
        _colMiktar = dgSatirlar.Columns.FirstOrDefault(c => c.SortMemberPath == nameof(KesifSatir.Miktar));
        _colSonGuncelleme = dgPozKutuphanesi.Columns.FirstOrDefault(c => c.SortMemberPath == nameof(KesifPoz.SonGuncellemeTarihi));

        GuncelleSonGuncellemeKolonu();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ProjeDegisiklikleriniIzle();

        // Kullanıcının elle ayarladığı sütun genişliklerini hatırla (AppData).
        Opened += (_, _) =>
        {
            KesifKolonGenislikDeposu.Uygula("satirlar", dgSatirlar);
            KesifKolonGenislikDeposu.Uygula("pozKutuphanesi", dgPozKutuphanesi);
        };
        Closing += Window_Closing;
        KeyDown += Window_KeyDown;
        Closed += (_, _) =>
        {
            _degisiklikIzleyici?.Dispose();
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        };
        KayitDurumunuTemizle();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(KesifHazirlaViewModel.OzelPozModuAktif)) GuncelleSonGuncellemeKolonu();
        if (e.PropertyName == nameof(KesifHazirlaViewModel.Proje)) ProjeDegisiklikleriniIzle();
    }

    private void ProjeDegisiklikleriniIzle()
    {
        _degisiklikIzleyici?.Dispose();
        _degisiklikIzleyici = new KesifProjeDegisiklikIzleyici(ViewModel.Proje);
        _degisiklikIzleyici.Degisti += (_, _) =>
        {
            txtKayitDurumu.IsVisible = true;
            if (_kayitDurumuGuncellemesiBekliyor) return;
            _kayitDurumuGuncellemesiBekliyor = true;
            // Toplu metraj/kütüphane işlemlerinde her bildirim için büyük proje JSON'unu üretme.
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _kayitDurumuGuncellemesiBekliyor = false;
                KayitDurumunuGuncelle();
            }, Avalonia.Threading.DispatcherPriority.Background);
        };
    }

    private void KayitDurumunuGuncelle() => txtKayitDurumu.IsVisible = DegisiklikVarMi();

    private bool _cikisOnaylandi;
    private bool _launcheriAc;

    private async void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (BelgeIslemiSuruyor())
        {
            e.Cancel = true;
            _launcheriAc = false;
            return;
        }
        if (_cikisOnaylandi)
        {
            // Gerçekten kapanıyor: sütun genişliklerini kaydet.
            KesifKolonGenislikDeposu.Kaydet("satirlar", dgSatirlar);
            KesifKolonGenislikDeposu.Kaydet("pozKutuphanesi", dgPozKutuphanesi);
            return;
        }

        if (!DegisiklikVarMi())
        {
            _cikisOnaylandi = true;
            KesifKolonGenislikDeposu.Kaydet("satirlar", dgSatirlar);
            KesifKolonGenislikDeposu.Kaydet("pozKutuphanesi", dgPozKutuphanesi);
            if (_launcheriAc)
                LauncherAc();
            return;
        }

        // Önce onay iste; diyalog asenkron olduğundan kapanışı iptal edip akışı yürütürüz.
        e.Cancel = true;
        var secim = await CikisOnayWindow.Goster(
            this,
            "Kaydedilmemiş Değişiklikler",
            "Keşif projesinde kaydedilmemiş değişiklikler var. Çıkmadan önce kaydetmek ister misiniz?");
        if (secim == CikisSecimi.Iptal)
        {
            _launcheriAc = false;
            return;
        }

        if (secim == CikisSecimi.KaydetVeCik && !await KaydetVarsayilanAsync())
            return; // kayıt iptal/başarısız → çıkma

        _cikisOnaylandi = true;
        if (_launcheriAc)
            LauncherAc();
        Close();
    }

    /// <summary>Mevcut dosya yoluna kaydeder; yol yoksa "farklı kaydet" akışını açar. Başarılıysa true.</summary>
    private async Task<bool> KaydetVarsayilanAsync()
    {
        if (string.IsNullOrWhiteSpace(_acikDosyaYolu))
        {
            return await FarkliKaydetAsync() && !DegisiklikVarMi();
        }
        return await DosyayaKaydetAsync(_acikDosyaYolu) && !DegisiklikVarMi();
    }

    /// <summary>Elektrik modül menüsünü (launcher) mevcut pencerenin bulunduğu ekranda açar (çift monitör).</summary>
    private void LauncherAc()
    {
        var launcher = new ElectricLauncherWindow();
        var ekran = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (ekran is not null)
        {
            launcher.WindowStartupLocation = WindowStartupLocation.Manual;
            var alan = ekran.WorkingArea;
            var genislik = (int)(launcher.Width * ekran.Scaling);
            var yukseklik = (int)(launcher.Height * ekran.Scaling);
            launcher.Position = new Avalonia.PixelPoint(
                alan.X + Math.Max(0, (alan.Width - genislik) / 2),
                alan.Y + Math.Max(0, (alan.Height - yukseklik) / 2));
        }

        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = launcher;
        launcher.Show();
    }

    private async void TabloAgaci_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if ((e.Key != Avalonia.Input.Key.Delete && e.Key != Avalonia.Input.Key.Back) || ViewModel.SeciliTablo is null)
            return;

        e.Handled = true;
        if (!ViewModel.TabloDuzenlemeyeAcikMi(ViewModel.SeciliTablo, altlariDahil: true)) return;
        var onay = await OnayWindow.Goster(this, "Tablo Sil",
            $"'{ViewModel.SeciliTablo.Ad}' tablosu (ve varsa alt tabloları/satırları) silinecek. Onaylıyor musunuz?");
        if (onay && ViewModel.TabloSilCommand.CanExecute(null))
            ViewModel.TabloSilCommand.Execute(null);
    }

    private async void dgSatirlar_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if ((e.Key != Avalonia.Input.Key.Delete && e.Key != Avalonia.Input.Key.Back) || ViewModel.SeciliSatir is null)
            return;

        e.Handled = true;
        await SeciliSatiriOnaylaVeSil();
    }

    private async System.Threading.Tasks.Task SeciliSatiriOnaylaVeSil()
    {
        if (ViewModel.SeciliSatir is null)
            return;
        if (!ViewModel.TabloDuzenlemeyeAcikMi(ViewModel.SeciliTablo)) return;

        var ad = string.IsNullOrWhiteSpace(ViewModel.SeciliSatir.BirimAdi)
            ? ViewModel.SeciliSatir.PozNo
            : ViewModel.SeciliSatir.BirimAdi;
        var onay = await OnayWindow.Goster(this, "Satır Sil", $"'{ad}' satırı silinecek. Onaylıyor musunuz?");
        if (onay && ViewModel.SatirSilCommand.CanExecute(null))
            ViewModel.SatirSilCommand.Execute(null);
    }

    // HATA-7: "Son Güncelleme" kolonu yalnızca Özel Poz modunda görünür.
    private void GuncelleSonGuncellemeKolonu()
    {
        if (_colSonGuncelleme is not null)
            _colSonGuncelleme.IsVisible = ViewModel.OzelPozModuAktif;
    }

    private async void Yeni_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (BelgeIslemiSuruyor()) return;
        if (!await DegisiklikleriKoruAsync(
                "Yeni Proje",
                "Yeni proje oluşturulursa mevcut kaydedilmemiş değişiklikler kaybolacak.",
                "Kaydetmeden Yeni Proje",
                "Kaydet ve Yeni Proje"))
            return;

        ViewModel.YeniProje();
        _acikDosyaYolu = null;
        KayitDurumunuTemizle();
    }

    private async void Ac_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (BelgeIslemiSuruyor()) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Keşif dosyası aç",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("EYE4PRO Keşif") { Patterns = ["*.e4p14", "*.json"] }]
        });

        var path = files.FirstOrDefault()?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (BelgeIslemiSuruyor()) return;

        if (!await DegisiklikleriKoruAsync(
                "Başka Proje Aç",
                "Başka bir proje açılırsa mevcut kaydedilmemiş değişiklikler kaybolacak.",
                "Kaydetmeden Aç",
                "Kaydet ve Aç"))
            return;

        try
        {
            _dosyaAciliyor = true;
            var proje = await Task.Run(() => KesifJsonServisi.Ac(path));
            ViewModel.ProjeYukle(proje);
            _acikDosyaYolu = path;
            KayitDurumunuTemizle();
        }
        catch (Exception ex)
        {
            ViewModel.DurumMesaji = $"Keşif dosyası açılamadı: {ex.Message}";
        }
        finally { _dosyaAciliyor = false; }
    }

    private async void Kaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_acikDosyaYolu))
        {
            await FarkliKaydetAsync();
            return;
        }

        await DosyayaKaydetAsync(_acikDosyaYolu);
    }

    private async void FarkliKaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await FarkliKaydetAsync();

    private async Task<bool> FarkliKaydetAsync()
    {
        if (BelgeIslemiSuruyor()) return false;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Keşif dosyasını farklı kaydet",
            SuggestedFileName = string.IsNullOrWhiteSpace(_acikDosyaYolu)
                ? "kesif"
                : Path.GetFileNameWithoutExtension(_acikDosyaYolu),
            DefaultExtension = "e4p14",
            FileTypeChoices = [new FilePickerFileType("EYE4PRO Keşif") { Patterns = ["*.e4p14"] }]
        });

        var path = file?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path))
            return false;
        if (!path.EndsWith(".e4p14", StringComparison.OrdinalIgnoreCase))
            path += ".e4p14";

        if (await DosyayaKaydetAsync(path))
        {
            _acikDosyaYolu = path;
            return true;
        }
        return false;
    }

    private async Task<bool> DosyayaKaydetAsync(string path)
    {
        if (BelgeIslemiSuruyor()) return false;
        var proje = ViewModel.Proje;
        _kayitSuruyor = true;
        try
        {
            var kaydedilenParmakIzi = await KesifJsonServisi.KaydetAsync(proje, path);
            if (!ReferenceEquals(proje, ViewModel.Proje)) return false;
            _kaydedilenProjeParmakIzi = kaydedilenParmakIzi;
            KayitDurumunuGuncelle();
            ViewModel.DurumMesaji = $"Keşif dosyası kaydedildi: {Path.GetFileName(path)}"
                + (DegisiklikVarMi() ? ". Kayıt sırasında yapılan yeni değişiklikler için tekrar kaydedin." : "");
            return true;
        }
        catch (Exception ex)
        {
            ViewModel.DurumMesaji = $"Keşif dosyası kaydedilemedi: {ex.Message}";
            return false;
        }
        finally { _kayitSuruyor = false; KayitDurumunuGuncelle(); }
    }

    private async void ExcelIceAktar_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Poz kütüphanesi içe aktar",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Excel / CSV") { Patterns = ["*.xlsx", "*.csv", "*.tsv"] },
                FilePickerFileTypes.All
            ]
        });

        var path = files.FirstOrDefault()?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var pozlar = await Task.Run(() => KesifPozImportServisi.DosyadanOku(path));
            ViewModel.BirimFiyatListesiIceAktar(
                Path.GetFileNameWithoutExtension(path),
                path,
                pozlar,
                digerListeleriPasifeAl: true);
        }
        catch (Exception ex)
        {
            ViewModel.DurumMesaji = $"Poz kütüphanesi içe aktarılamadı: {ex.Message}";
        }
    }

    private async void ExcelDisaAktar_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Keşif Raporunu Excel Olarak Dışa Aktar",
            SuggestedFileName = $"{DosyaAdiTemizle(ViewModel.Proje.Ad)}_kesif_raporu.xlsx",
            DefaultExtension = "xlsx",
            FileTypeChoices =
            [
                new FilePickerFileType("Excel Çalışma Kitabı (.xlsx)") { Patterns = ["*.xlsx"] }
            ]
        });

        var path = file?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path))
            return;
        if (!path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            path += ".xlsx";

        try
        {
            ViewModel.HesaplariYenile();
            if (KesifExcelExportServisi.DosyaAcikMi(path))
            {
                ViewModel.DurumMesaji = "Excel dosyası açık. Dosyayı kapatın veya farklı bir adla kaydedin.";
                return;
            }
            KesifExcelExportServisi.RaporOlustur(ViewModel.Proje, path);
            ViewModel.DurumMesaji = $"Excel keşif raporu kaydedildi: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            ViewModel.DurumMesaji = $"Excel çıktısı oluşturulamadı: {ex.Message}";
        }
    }

    private static string DosyaAdiTemizle(string? value)
    {
        var ad = string.IsNullOrWhiteSpace(value) ? "kesif" : value.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
            ad = ad.Replace(invalid, '_');
        return ad.Replace(' ', '_');
    }

    private void Grid_BeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
    {
        if (ViewModel.SeciliTabloSaltOkunur) e.Cancel = true;
    }

    private void Grid_CellEditEnded(object? sender, DataGridCellEditEndedEventArgs e)
        => ViewModel.HesaplariYenile();

    private void ListGrid_CellEditEnded(object? sender, DataGridCellEditEndedEventArgs e)
        => ViewModel.PozKutuphaneYenile();

    private void PozGrid_CellEditEnded(object? sender, DataGridCellEditEndedEventArgs e)
    {
        if (e.Row.DataContext is KesifPoz poz)
        {
            ViewModel.OzelPozDuzenlemeTamamlandi(poz);
        }
    }

    private void OzelPozlar_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel.SeciliPoz is not null && !ViewModel.SeciliPoz.ListePozuMu)
        {
            ViewModel.SeciliOzelPoz = ViewModel.SeciliPoz;
        }
        var win = new OzelPozlarWindow
        {
            DataContext = ViewModel
        };
        win.ShowDialog(this);
    }

    private void IcmalGoster_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var icmalWindow = new KesifIcmalWindow
        {
            DataContext = ViewModel
        };
        icmalWindow.Show(this);
    }

    private void PozAtama_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.EslemeleriYenile();
        ViewModel.EslenmemisKalemleriYenile();
        ViewModel.FiltrelenmisAtamaPozlariYenile();
        var win = new PozAtamaWindow
        {
            DataContext = ViewModel
        };
        win.Show(this);
    }

    private void Referanslar_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var win = new ReferanslarWindow
        {
            DataContext = ViewModel
        };
        win.Show(this);
    }

    private async void dgSatirlar_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        // HATA-9: Başlık metni yerine kolon referansıyla karşılaştır (yeniden adlandırmaya dayanıklı).
        var dg = sender as DataGrid;
        if (dg?.CurrentColumn is not null && dg.CurrentColumn == _colMiktar)
            return;

        if (ViewModel.SeciliSatir is not null)
            await SeciliSatiriOnaylaVeSil();
    }

    private void dgPozKutuphanesi_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (ViewModel.SeciliPoz is not null && ViewModel.PozuTabloyaEkleCommand.CanExecute(null))
        {
            ViewModel.PozuTabloyaEkleCommand.Execute(null);
        }
    }

    private void Back_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Çıkış onayı + kaydet/çık akışı Window_Closing'de; launcher aynı ekranda açılır.
        _launcheriAc = true;
        Close();
    }

    private async void Window_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        bool komut = e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Meta)
                     || e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control);
        if (!komut || e.Key != Avalonia.Input.Key.S)
            return;

        e.Handled = true;
        if (e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift))
            await FarkliKaydetAsync();
        else
            await KaydetVarsayilanAsync();
    }

    private bool DegisiklikVarMi()
        => !string.Equals(
            _kaydedilenProjeParmakIzi,
            KesifJsonServisi.ParmakIziOlustur(ViewModel.Proje),
            StringComparison.Ordinal);

    private void KayitDurumunuTemizle()
    {
        _kaydedilenProjeParmakIzi = KesifJsonServisi.ParmakIziOlustur(ViewModel.Proje);
        KayitDurumunuGuncelle();
    }

    private async Task<bool> DegisiklikleriKoruAsync(
        string baslik,
        string mesaj,
        string kaydetmedenMetni,
        string kaydetMetni)
    {
        if (!DegisiklikVarMi())
            return true;

        var secim = await CikisOnayWindow.Goster(
            this, baslik, mesaj, kaydetmedenMetni, kaydetMetni);
        return secim switch
        {
            CikisSecimi.KaydetmedenCik => true,
            CikisSecimi.KaydetVeCik => await KaydetVarsayilanAsync(),
            _ => false
        };
    }

    private void TabloAgaci_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is TreeView { SelectedItem: KesifTablosu tablo })
            ViewModel.SeciliTablo = tablo;
    }

    private void Tamamlandi_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
        => e.Handled = true; // Tike çift tıklamak tabloyu yeniden adlandırma penceresini açmasın.

    private void Tamamlandi_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not CheckBox { DataContext: KesifTablosu tablo } tik) return;
        var tamamlandi = tik.IsChecked == true;
        // Model henüz değişmedi (OneWay): açık hücre düzenlemesini kilit devreye girmeden bitir.
        if (tamamlandi && (!dgSatirlar.CommitEdit(DataGridEditingUnit.Cell, true) ||
                          !dgSatirlar.CommitEdit(DataGridEditingUnit.Row, true)))
            ViewModel.DurumMesaji = "Tamamlandı işaretlemeden önce hücredeki geçersiz değeri düzeltin.";
        else
            ViewModel.TabloTamamlanmaDurumuAyarla(tablo, tamamlandi);
        tik.SetCurrentValue(Avalonia.Controls.Primitives.ToggleButton.IsCheckedProperty, tablo.TamamlandiMi);
        e.Handled = true;
    }

    private async void ListBox_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (ViewModel.SeciliTablo is null)
            return;

        var tablo = ViewModel.SeciliTablo;
        if (!ViewModel.TabloDuzenlemeyeAcikMi(tablo)) return;

        var renameWindow = new RenameTableWindow(tablo.Ad);
        await renameWindow.ShowDialog(this);

        if (!string.IsNullOrWhiteSpace(renameWindow.Result))
        {
            ViewModel.TabloAdiniDegistir(tablo, renameWindow.Result);
        }
    }
}
