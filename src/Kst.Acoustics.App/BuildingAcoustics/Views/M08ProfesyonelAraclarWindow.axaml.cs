using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Kst.Acoustics.App.BuildingAcoustics.ViewModels;

namespace Kst.Acoustics.App.BuildingAcoustics.Views;

public partial class M08ProfesyonelAraclarWindow : Window
{
    private M08ProfesyonelAraclarViewModel Vm => (M08ProfesyonelAraclarViewModel)DataContext!;

    public M08ProfesyonelAraclarWindow() { InitializeComponent(); }
    public M08ProfesyonelAraclarWindow(BinaProjesiViewModel ana) : this() =>
        DataContext = new M08ProfesyonelAraclarViewModel(ana);

    private void TumunuYenile_OnClick(object? s, RoutedEventArgs e) => Guvenli(Vm.TumunuYenile);
    private void GorevleriYenile_OnClick(object? s, RoutedEventArgs e) => Guvenli(Vm.GorevleriYenile);
    private void GoreviTamamla_OnClick(object? s, RoutedEventArgs e) => Guvenli(() => Vm.GoreviTamamla());
    private void GoreveGit_OnClick(object? s, RoutedEventArgs e) => Guvenli(() => Vm.GoreveGit());

    private async void RevizyonKarsilastir_OnClick(object? s, RoutedEventArgs e) => await GuvenliAsync(async () =>
    {
        var p = await Ac("M50 Revizyonunu Karşılaştır", ["*.e4bina.json"]); if (p is not null) Vm.RevizyonKarsilastir(p);
    });
    private async void RevizyonUygula_OnClick(object? s, RoutedEventArgs e) => await GuvenliAsync(async () =>
    {
        if (Vm.RevizyonDosyasi is null) { Vm.DurumMesaji = "Önce bir M50 revizyon dosyasını karşılaştırın."; return; }
        if (await Vm.AnaViewModel.BinaGeometrisiniDosyadanUygula(this, Vm.RevizyonDosyasi)) Vm.TumunuYenile();
    });
    private void SenaryoKaydet_OnClick(object? s, RoutedEventArgs e) => Guvenli(Vm.SenaryoKaydet);
    private void SenaryoUygula_OnClick(object? s, RoutedEventArgs e) => Guvenli(() => Vm.SenaryoUygula());
    private void SenaryoSil_OnClick(object? s, RoutedEventArgs e) => Guvenli(() => Vm.SenaryoSil());

    private void BasarisizlikAcikla_OnClick(object? s, RoutedEventArgs e) => Guvenli(Vm.BasarisizlikAcikla);
    private void FrekansAnalizi_OnClick(object? s, RoutedEventArgs e) => Guvenli(Vm.FrekansAnalizi);
    private async void Optimizasyon_OnClick(object? s, RoutedEventArgs e) => await GuvenliAsync(Vm.OptimizasyonYapAsync);
    private async void Belirsizlik_OnClick(object? s, RoutedEventArgs e) => await GuvenliAsync(Vm.BelirsizlikAnaliziAsync);

    private async void OlcumIceriAktar_OnClick(object? s, RoutedEventArgs e) => await GuvenliAsync(async () =>
    {
        var p = await Ac("Saha Ölçümü İçe Aktar", ["*.csv", "*.json", "*.e4m07"]); if (p is not null) Vm.OlcumIceriAktar(p);
    });
    private async void TopluCsvDisaAktar_OnClick(object? s, RoutedEventArgs e) => await GuvenliAsync(async () =>
    {
        var p = await Kaydet("Toplu M08 CSV", "m08-toplu.csv", ["*.csv"]); if (p is not null) Vm.TopluCsvDisaAktar(p);
    });
    private async void TopluCsvIceriAktar_OnClick(object? s, RoutedEventArgs e) => await GuvenliAsync(async () =>
    {
        var p = await Ac("Toplu M08 CSV İçe Aktar", ["*.csv"]); if (p is not null) Vm.TopluCsvIceriAktar(p);
    });
    private async void SablonKaydet_OnClick(object? s, RoutedEventArgs e) => await GuvenliAsync(async () =>
    {
        var p = await Kaydet("M08 Proje Şablonu", "m08-proje-sablonu.e4t08.json", ["*.e4t08.json"]); if (p is not null) Vm.SablonKaydet(p);
    });
    private async void SablonUygula_OnClick(object? s, RoutedEventArgs e) => await GuvenliAsync(async () =>
    {
        var p = await Ac("M08 Proje Şablonu Uygula", ["*.e4t08.json", "*.json"]); if (p is not null) Vm.SablonUygula(p);
    });
    private void MetrajYenile_OnClick(object? s, RoutedEventArgs e) => Guvenli(Vm.MetrajiYenile);
    private async void MetrajExcel_OnClick(object? s, RoutedEventArgs e) => await GuvenliAsync(async () =>
    {
        var p = await Kaydet("M08 Metraj Excel", "m08-metraj.xlsx", ["*.xlsx"]); if (p is not null) Vm.MetrajExcelOlustur(p);
    });

    private void TopluMekanaUygula_OnClick(object? s, RoutedEventArgs e) => Guvenli(() =>
        Vm.TopluMekanaUygula(TopluMekanGrid.SelectedItems.Cast<M08TopluMekanSatiri>()));
    private void TopluTabloYenile_OnClick(object? s, RoutedEventArgs e) => Guvenli(Vm.TopluTabloyuYenile);
    private void TopluBolmeyeUygula_OnClick(object? s, RoutedEventArgs e) => Guvenli(() =>
        Vm.TopluBolmeyeUygula(TopluBolmeGrid.SelectedItems.Cast<M08TopluBolmeSatiri>()));
    private void TopluGrid_OnBeginningEdit(object? s, DataGridBeginningEditEventArgs e) => Vm.GridDuzenlemesiBaslat();
    private void TopluMekanGrid_OnCellEditEnded(object? s, DataGridCellEditEndedEventArgs e) => Vm.GridDuzenlemesiBitti();
    private void TopluBolmeGrid_OnCellEditEnded(object? s, DataGridCellEditEndedEventArgs e) => Vm.GridDuzenlemesiBitti();

    private async void IfcDisaAktar_OnClick(object? s, RoutedEventArgs e) => await MetinKaydet("IFC4 Akustik BIM", "m08-acoustic.ifc", ["*.ifc"], Vm.IfcOlustur);
    private async void CepheSvg_OnClick(object? s, RoutedEventArgs e) => await MetinKaydet("Cephe Gürültü Haritası", "m08-cephe.svg", ["*.svg"], Vm.CepheSvgOlustur);
    private async void CepheCsv_OnClick(object? s, RoutedEventArgs e) => await MetinKaydet("Cephe Gürültü Haritası CSV", "m08-cephe.csv", ["*.csv"], Vm.CepheCsvOlustur);
    private async void PortfoyKlasoru_OnClick(object? s, RoutedEventArgs e) => await GuvenliAsync(async () =>
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "M08 Portföy Klasörü", AllowMultiple = false });
        var p = folders.FirstOrDefault()?.Path.LocalPath; if (!string.IsNullOrWhiteSpace(p)) await Vm.PortfoyTaraAsync(p);
    });

    private void RaporOnKontrol_OnClick(object? s, RoutedEventArgs e) => Guvenli(() => Vm.RaporOnKontrol());
    private async void RaporOnizle_OnClick(object? s, RoutedEventArgs e) => await GuvenliAsync(async () =>
    {
        var p = Path.Combine(Path.GetTempPath(), $"eye4pro-m08-{Vm.Proje.Id:N}.html");
        await AtomikYaz(p, Vm.RaporHtmlOlustur());
        Process.Start(new ProcessStartInfo(p) { UseShellExecute = true });
        Vm.DurumMesaji = "Rapor önizlemesi varsayılan tarayıcıda açıldı.";
    });
    private async void RaporDondur_OnClick(object? s, RoutedEventArgs e) => await GuvenliAsync(async () =>
    {
        var p = await Kaydet("Dondurulmuş M08 Rapor Paketi", "m08-rapor.e4r08", ["*.e4r08"]); if (p is not null) Vm.RaporDondur(p);
    });
    private async void RaporDogrula_OnClick(object? s, RoutedEventArgs e) => await GuvenliAsync(async () =>
    {
        var p = await Ac("Dondurulmuş M08 Raporunu Doğrula", ["*.e4r08"]); if (p is not null) Vm.RaporDogrula(p);
    });
    private void RaporOnayla_OnClick(object? s, RoutedEventArgs e) => Guvenli(() => Vm.RaporOnayla());
    private async void SahaPaketiDisaAktar_OnClick(object? s, RoutedEventArgs e) => await MetinKaydet("Çevrimdışı M08 Saha Paketi", "m08-saha.html", ["*.html"], Vm.SahaPaketiHtmlOlustur);
    private async void SahaPaketiIceriAktar_OnClick(object? s, RoutedEventArgs e) => await GuvenliAsync(async () =>
    {
        var p = await Ac("M08 Saha Dönüşünü İçe Aktar", ["*.json"]); if (p is not null) Vm.SahaPaketiIceriAktar(p);
    });

    private async Task MetinKaydet(string title, string name, IReadOnlyList<string> patterns, Func<string> content) => await GuvenliAsync(async () =>
    {
        var p = await Kaydet(title, name, patterns); if (p is null) return;
        await AtomikYaz(p, content());
        Vm.DisaAktarimKaydiEkle(title, p);
    });
    private async Task<string?> Ac(string title, IReadOnlyList<string> patterns)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = title, AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(title) { Patterns = patterns }] }); return files.FirstOrDefault()?.Path.LocalPath;
    }
    private async Task<string?> Kaydet(string title, string name, IReadOnlyList<string> patterns)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = title, SuggestedFileName = name,
            FileTypeChoices = [new FilePickerFileType(title) { Patterns = patterns }] }); return file?.Path.LocalPath;
    }
    private static async Task AtomikYaz(string path, string content)
    {
        var full = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var tmp = full + ".tmp-" + Guid.NewGuid().ToString("N");
        try { await File.WriteAllTextAsync(tmp, content); File.Move(tmp, full, true); }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }
    private void Guvenli(Action action) { try { action(); } catch (Exception ex) { Vm.DurumMesaji = "İşlem başarısız: " + ex.Message; } }
    private async Task GuvenliAsync(Func<Task> action) { try { await action(); } catch (Exception ex) { Vm.DurumMesaji = "İşlem başarısız: " + ex.Message; } }
}
