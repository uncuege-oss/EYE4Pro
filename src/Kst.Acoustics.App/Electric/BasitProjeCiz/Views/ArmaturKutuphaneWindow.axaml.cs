using System;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Kst.Acoustics.App.Electric.BasitProjeCiz.ViewModels;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;

namespace Kst.Acoustics.App.Electric.BasitProjeCiz.Views;

public partial class ArmaturKutuphaneWindow : Window
{
    private readonly BasitProjeCizViewModel _vm;

    public ArmaturKutuphaneWindow()
    {
        _vm = new BasitProjeCizViewModel();
        InitializeComponent();
        Bagla();
    }

    public ArmaturKutuphaneWindow(BasitProjeCizViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        Bagla();
    }

    private void Bagla()
    {
        DataContext = _vm;
        _vm.PropertyChanged += OnVmPropertyChanged;
        AktifElemaniGuncelle();
    }

    private void AktifElemaniGuncelle()
    {
        var eleman = AktifSeciliEleman();
        _vm.AktifKutuphaneEleman = eleman;
        SembolGuncelle(eleman);
        if (YerlesimMantigiExpander != null && KutuphaneTabControl != null)
        {
            YerlesimMantigiExpander.IsVisible = KutuphaneTabControl.SelectedIndex == 0;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _vm.PropertyChanged -= OnVmPropertyChanged;
        base.OnClosed(e);
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BasitProjeCizViewModel.SeciliKutuphaneArmatür)
                           or nameof(BasitProjeCizViewModel.SeciliKutuphaneAnahtar)
                           or nameof(BasitProjeCizViewModel.SeciliKutuphanePano)
                           or nameof(BasitProjeCizViewModel.SeciliKutuphanePriz)
                           or nameof(BasitProjeCizViewModel.SeciliKutuphaneKlima)
                           or nameof(BasitProjeCizViewModel.SeciliZayifMalzeme))
        {
            AktifElemaniGuncelle();
        }
    }

    internal void TabControl_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        AktifElemaniGuncelle();
    }

    /// Hangi tab açıksa o tab'ın seçili elemanını döner.
    private BpcArmatürTanim? AktifSeciliEleman()
    {
        if (KutuphaneTabControl is null) return null;
        var tab = KutuphaneTabControl.SelectedIndex;
        return tab switch
        {
            1 => _vm.SeciliKutuphaneAnahtar,
            2 => _vm.SeciliKutuphanePano,
            3 => _vm.SeciliKutuphanePriz,
            4 => _vm.SeciliKutuphaneKlima,
            5 => _vm.SeciliZayifMalzeme?.Tanim,
            _ => _vm.SeciliKutuphaneArmatür
        };
    }

    // ─── Sembol çizimi ────────────────────────────────────────────────────

    private void SembolGuncelle(BpcArmatürTanim? armatur)
    {
        if (SembolCanvas is null) return;
        SembolCanvas.Children.Clear();

        SembolCizgiSayText.Text = armatur is null
            ? ""
            : $"Sembol: {armatur.SembolCizgiler.Count} çizgi parçası";

        if (armatur == null)
        {
            SembolYokText.IsVisible = true;
            SembolYokText.Text = "Eleman seçin";
            return;
        }

        if (armatur.SembolCizgiler.Count == 0)
        {
            SembolYokText.IsVisible = true;
            SembolYokText.Text = "Sembol tanımlanmamış";
            return;
        }

        SembolYokText.IsVisible = false;

        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxY = double.MinValue;
        foreach (var s in armatur.SembolCizgiler)
        {
            minX = Math.Min(minX, Math.Min(s.Ax, s.Bx));
            maxX = Math.Max(maxX, Math.Max(s.Ax, s.Bx));
            minY = Math.Min(minY, Math.Min(s.Ay, s.By));
            maxY = Math.Max(maxY, Math.Max(s.Ay, s.By));
        }

        double maxDim = Math.Max(maxX - minX, maxY - minY);
        if (maxDim < 1e-9) return;

        const double canvasSize = 200.0, margin = 15.0;
        double scale = (canvasSize - 2 * margin) / maxDim;
        double cx = canvasSize / 2 - (minX + maxX) / 2 * scale;
        double cy = canvasSize / 2 + (minY + maxY) / 2 * scale;

        // Aydınlatma: yeşil; anahtarlar: camgöbeği; pano: sarı; prizler: turuncu
        var renk = KutuphaneTabControl.SelectedIndex switch
        {
            1 => Color.Parse("#4ECDE0"),  // anahtarlar — camgöbeği
            2 => Color.Parse("#E0C84E"),  // pano — sarı
            3 => Color.Parse("#FF9800"),  // prizler — turuncu
            4 => Color.Parse("#80D8FF"),  // klimalar — açık mavi
            5 => Color.Parse("#4ED0E0"),  // zayıf akım — camgöbeği-mavi
            _ => Color.Parse("#8CC84B")   // aydınlatma — yeşil
        };
        var stroke = new SolidColorBrush(renk);

        foreach (var seg in armatur.SembolCizgiler)
        {
            SembolCanvas.Children.Add(new Line
            {
                StartPoint      = new Avalonia.Point(cx + seg.Ax * scale, cy - seg.Ay * scale),
                EndPoint        = new Avalonia.Point(cx + seg.Bx * scale, cy - seg.By * scale),
                Stroke          = stroke,
                StrokeThickness = 1.6,
                StrokeLineCap   = PenLineCap.Round
            });
        }
    }

    // ── DXF Şablon Dışa Aktar ────────────────────────────────────────────────
    private async void SablonDxfExport_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Sembol Şablonu Kaydet",
            SuggestedFileName = "EYE4PRO_Sembol_Sablonu.dxf",
            FileTypeChoices = [new FilePickerFileType("DXF Dosyası") { Patterns = ["*.dxf"] }],
            DefaultExtension = "dxf"
        });
        if (file is null) return;
        try
        {
            var path = EnsureDxfExtension(file.Path.LocalPath);
            _vm.SablonDxfDisariAktar(path);
            if (!path.Equals(file.Path.LocalPath, StringComparison.OrdinalIgnoreCase))
                _vm.KutuphaneMesaji = $"DWG üretilemediği için şablon DXF olarak kaydedildi: {path}";
        }
        catch (Exception ex)
        {
            _vm.KutuphaneMesaji = $"Şablon kaydedilemedi: {ex.Message}";
        }
    }

    private static string EnsureDxfExtension(string path)
    {
        var extension = System.IO.Path.GetExtension(path);
        return extension.Equals(".dxf", StringComparison.OrdinalIgnoreCase)
            ? path
            : System.IO.Path.ChangeExtension(path, ".dxf");
    }

    // ── DXF'ten Sembolleri Yükle ─────────────────────────────────────────────
    private async void SembolDxfImport_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Sembol DXF Seç",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("DXF Dosyaları") { Patterns = ["*.dxf"] }]
        });
        var path = files.FirstOrDefault()?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            _vm.SembolleriDxftenYukle(path);
            // Önizleme + DataGrid yeniden çizilmeli
            AktifElemaniGuncelle();
        }
        catch (Exception ex)
        {
            _vm.KutuphaneMesaji = $"DXF içe aktarılamadı: {ex.Message}";
        }
    }
}
