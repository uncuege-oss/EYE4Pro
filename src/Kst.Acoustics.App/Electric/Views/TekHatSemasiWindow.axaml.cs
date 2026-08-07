using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kst.Acoustics.Core.Electric.Models;
using Kst.Acoustics.Core.Electric.Calculation;
using Kst.Acoustics.App.Electric.ViewModels;

namespace Kst.Acoustics.App.Electric.Views;

public partial class TekHatSemasiWindow : Window
{
    private readonly TekHatSemasiViewModel _vm;
    private readonly ObservableCollection<string> _panoIsimleri = new();

    // Per-floor cumulative Y start and actual height (computed each SemaCiz call)
    private readonly Dictionary<string, double> _floorStartY = new();
    private readonly Dictionary<string, double> _floorActualHeight = new();
    // OG (trafo) bloğunun çizileceği üst Y (SemaCiz'de hesaplanır): Trafo Katı'na göre en üstte
    // veya seçilen katın üstündeki ayrılmış bantta.
    private double _ogTopY;

    // Şema kanvası zoom/pan durumu
    private readonly ScaleTransform _semaScale = new();
    private readonly TranslateTransform _semaTranslate = new();
    private bool _semaPanAktif;
    private Avalonia.Point _semaPanBaslangic;
    private double _semaPanTx;
    private double _semaPanTy;

    // Hızlı toplu giriş için şema çizimini geciktiren debounce zamanlayıcısı
    private readonly DispatcherTimer _semaCizTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private double _ozetPanelGenislik = 320;

    // İçerik (pano + kat planı) sınır kutusu — sığdırma için her çizimde hesaplanır
    private double _icerikMinX, _icerikMaxX, _icerikMinY, _icerikMaxY;
    private bool _ilkSigdirYapildi;

    // Kablo çakışma önleme: çizilen yatay kablo parçalarının kaydı (Y, Xmin, Xmax).
    // Her SemaCiz başında sıfırlanır; yeni yatay kablo bu kayda göre boş kanala yerleşir.
    private readonly System.Collections.Generic.List<(double Y, double X1, double X2)> _kabloYatayKanallar = new();
    private const double KabloKanalAdim = 7.0; // kanallar arası min dikey mesafe

    // Kontrol (doğrulama) modu: açıkken panolar eksikliklerine göre kırmızı/sarı renklendirilir,
    // üzerine gelince eksiklik listesi tooltip olarak çıkar; boş alana tıklayınca normale döner.
    private bool _kontrolModu;
    private bool _karUiYukleniyor = true;
    private string _kaydedilenProjeParmakIzi = "";
    private bool _kapanisOnaylandi;
    private bool _launcheriAc;


    public TekHatSemasiWindow()
    {
        _ = typeof(Linye); // Force load Kst.Acoustics.Core assembly for XamlLoader to prevent FileNotFoundException
        InitializeComponent();
        SemaZoomPanBagla();
        _semaCizTimer.Tick += (_, _) => { _semaCizTimer.Stop(); SemaCiz(); };
        _vm = new TekHatSemasiViewModel();
        DataContext = _vm;
        _karUiYukleniyor = false;
        _kaydedilenProjeParmakIzi = ProjeParmakIziOlustur();
        Closing += Window_Closing;
        KeyDown += Window_KeyDown;

        Opened += (_, _) =>
        {
            // Yük tipi ComboBox
            YukTipiCombo.ItemsSource = _vm.YukTipleri;
            YukTipiCombo.DisplayMemberBinding = new Avalonia.Data.Binding("Ad");
            if (_vm.YukTipleri.Count > 0)
                YukTipiCombo.SelectedIndex = 0;

            // Pano seçici
            PanoSecici.ItemsSource = _panoIsimleri;

            // Kat seçenekleri
            KatComboGuncelle();
            UpsTopolojiArayuzGuncelle();

            // Linye satırına sağ-tık → (Merkezi modda) 'Kritik Panoya Taşı'
            LinyeGrid.AddHandler(InputElement.PointerPressedEvent, LinyeGrid_OnRightClickSelect,
                Avalonia.Interactivity.RoutingStrategies.Tunnel);
            LinyeGrid.ContextRequested += LinyeGrid_OnContextRequested;

            PanoOzetText.Text = "1. Üst çubuktan pano adı ve kat girin, ardından '+ Pano' düğmesine tıklayın.";
            SemaCiz();
        };
    }

    private void KatComboGuncelle()
    {
        var katlar = _vm.Proje.Bina?.KatEtiketleri() ?? new System.Collections.Generic.List<string> { "Z" };
        YeniPanoKat.ItemsSource = katlar;
        if (katlar.Count > 0) YeniPanoKat.SelectedIndex = 0;
    }

}
