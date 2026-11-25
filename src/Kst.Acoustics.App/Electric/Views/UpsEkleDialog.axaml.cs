using System.Linq;
using Avalonia.Controls;
using Kst.Acoustics.App.Electric.ViewModels;
using Kst.Acoustics.Core.Electric.Calculation;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.App.Electric.Views;

public partial class UpsEkleDialog : Window
{
    private readonly TekHatSemasiViewModel _vm = null!;
    private readonly Pano _besleyenPano = null!;
    private readonly Ups? _mevcut;
    private readonly bool _duzenle;

    public bool Silindi { get; private set; }

    public UpsEkleDialog() { InitializeComponent(); }

    // 2 parametreli yapıcı her zaman "yeni ekle" modudur; belirli bir UPS'i düzenlemek için
    // 3 parametreli yapıcıya o UPS geçilir (bir besleyen pano birden fazla UPS besleyebilir).
    public UpsEkleDialog(TekHatSemasiViewModel vm, Pano besleyenPano)
        : this(vm, besleyenPano, null) { }

    public UpsEkleDialog(TekHatSemasiViewModel vm, Pano besleyenPano, Ups? duzenlenecek)
    {
        InitializeComponent();
        _vm = vm;
        _besleyenPano = besleyenPano;
        _mevcut = duzenlenecek;
        _duzenle = _mevcut != null;

        BaslikText.Text = _duzenle ? "🔋 UPS'i Düzenle" : "🔋 UPS Ekle";
        BesleyenPanoText.Text = $"{besleyenPano.Ad}  ({besleyenPano.Kat})";
        SilButton.IsVisible = _duzenle;
        KaydetButton.Content = _duzenle ? "Güncelle" : "Ekle";

        // Kritik pano (varsa)
        var kritikPano = _mevcut != null
            ? vm.Proje.TumPanolar.FirstOrDefault(p => p.Ad == _mevcut.KritikPanoAd)
            : null;
        KritikPanoText.Text = kritikPano != null
            ? $"{kritikPano.Ad}  ({kritikPano.Kat})"
            : $"{besleyenPano.Ad}-Kritik  (yeni — kritik devreleri sonra ekleyin)";
        KritikYukText.Text = kritikPano != null
            ? $"{kritikPano.ToplamGucW / 1000.0:N1} kW / {kritikPano.ToplamGorunurVA / 1000.0:N1} kVA"
            : "0 kW (kritik panoya devre eklenince hesaplanır)";

        // UPS / Akü combo'ları
        UpsCombo.ItemsSource = new object[] { "(otomatik tayin)" }
            .Concat(vm.UpsTipleri.Cast<object>()).ToList();
        UpsCombo.SelectedIndex = 0;
        AkuCombo.ItemsSource = new object[] { "(ilk tip)" }
            .Concat(vm.AkuTipleri.Cast<object>()).ToList();
        AkuCombo.SelectedIndex = 0;

        // Varsayılan otonomi 10 dk
        OtonomiCombo.SelectedIndex = 1;

        if (_duzenle && _mevcut != null)
        {
            OtonomiCombo.SelectedIndex = _mevcut.OtonomiDk switch { 5 => 0, 15 => 2, 30 => 3, _ => 1 };
            EmniyetFaktoruBox.Text = _mevcut.EmniyetFaktoru.ToString("0.##");
            if (_mevcut.UpsTanimManuel && !string.IsNullOrEmpty(_mevcut.UpsTanimAd))
            {
                var t = vm.UpsTipleri.FirstOrDefault(u => u.Ad == _mevcut.UpsTanimAd);
                if (t != null) UpsCombo.SelectedItem = t;
            }
            if (!string.IsNullOrEmpty(_mevcut.AkuTanimAd))
            {
                var a = vm.AkuTipleri.FirstOrDefault(a => a.Ad == _mevcut.AkuTanimAd);
                if (a != null) AkuCombo.SelectedItem = a;
            }
        }

        OnerileriGuncelle();
        OtonomiCombo.SelectionChanged += (_, _) => OnerileriGuncelle();
        EmniyetFaktoruBox.TextChanged += (_, _) => OnerileriGuncelle();
        AkuCombo.SelectionChanged += (_, _) => OnerileriGuncelle();
    }

    private int SeciliOtonomi() => OtonomiCombo.SelectedIndex switch { 0 => 5, 2 => 15, 3 => 30, _ => 10 };

    private void OnerileriGuncelle()
    {
        // Önerilen UPS — düzenleme modunda gerçek kritik yük, ekleme modunda 0
        var kritikPano = _mevcut != null
            ? _vm.Proje.TumPanolar.FirstOrDefault(p => p.Ad == _mevcut.KritikPanoAd)
            : null;
        double kvA = kritikPano?.ToplamGorunurVA / 1000.0 ?? 0;
        double kW = kritikPano?.ToplamGucW / 1000.0 ?? 0;
        double emniyet = NumberHelper.Parse(EmniyetFaktoruBox.Text, 1.25);

        if (kvA > 0)
        {
            var oner = UpsHesapServisi.UygunUpsSec(kvA * emniyet, kW * emniyet, _vm.Kutuphane);
            OnerilenUpsText.Text = oner == null
                ? "Önerilen: kütüphanede uygun UPS yok"
                : $"Önerilen: {oner.Ad}  ({oner.GucKVA}kVA/{oner.GucKW}kW)";
        }
        else
        {
            OnerilenUpsText.Text = "Kritik panoya devre ekledikçe UPS önerisi güncellenir.";
        }

        var aku = AkuCombo.SelectedItem as AkuTanim ?? _vm.AkuTipleri.FirstOrDefault();
        AkuOzetText.Text = aku == null
            ? ""
            : $"{aku.Ad} — {aku.BlokGerilimV}V {aku.KapasiteAh}Ah, {SeciliOtonomi()}dk'da blok başı {aku.BlokGucW(SeciliOtonomi()):N0}W";
    }

    private void Kaydet_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        double emniyet = NumberHelper.Parse(EmniyetFaktoruBox.Text, 1.25);
        if (emniyet < 1.0) emniyet = 1.0;
        if (emniyet > 2.0) emniyet = 2.0;
        int otonomi = SeciliOtonomi();
        var upsTanim = UpsCombo.SelectedItem as UpsTanim;
        var akuTanim = AkuCombo.SelectedItem as AkuTanim;

        if (_duzenle && _mevcut != null)
        {
            _mevcut.OtonomiDk = otonomi;
            _mevcut.EmniyetFaktoru = emniyet;
            _mevcut.UpsTanimAd = upsTanim?.Ad ?? "";
            _mevcut.UpsTanimManuel = upsTanim != null;
            _mevcut.AkuTanimAd = akuTanim?.Ad ?? "";
            _vm.TumunuHesapla();
        }
        else
        {
            _vm.UpsEkle(_besleyenPano, otonomi, emniyet, upsTanim, akuTanim);
        }
        Close(true);
    }

    private void Sil_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_mevcut != null) _vm.UpsSil(_mevcut);
        Silindi = true;
        Close(true);
    }

    private void Iptal_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);
}
