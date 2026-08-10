using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Kst.Acoustics.Core.Salon.Models;
using Kst.Acoustics.Core.Salon.Services;

namespace Kst.Acoustics.App.Salon.Views;

public partial class SalonAnalizWindow : Window
{
    private readonly SalonGeometri _salon;
    private readonly Action? _degismedenOnce;
    private IReadOnlyList<SalonYansiticiPanel> _panelOnerisi = [];
    public bool Degisti { get; private set; }
    public SalonKaliteBulgusu? SeciliKaliteBulgusu { get; private set; }

    public SalonAnalizWindow() : this(SalonGeometriServisi.YeniOlustur()) { }

    public SalonAnalizWindow(SalonGeometri salon, Action? degismedenOnce = null)
    {
        _salon = salon ?? throw new ArgumentNullException(nameof(salon));
        _degismedenOnce = degismedenOnce;
        InitializeComponent();
        SekciyonCombo.ItemsSource = _salon.SeyirciSekciyonlari;
        SekciyonCombo.SelectedIndex = _salon.SeyirciSekciyonlari.Count > 0 ? 0 : -1;
        Yenile();
    }

    private SalonKullanimTipi Kullanim => (SalonKullanimTipi)Math.Clamp(KullanimCombo.SelectedIndex, 0, 3);

    private void Yenile()
    {
        try
        {
            var a = SalonAkustikOnKontrolServisi.Hesapla(_salon, Kullanim);
            var sb = new StringBuilder();
            sb.AppendLine($"Hava hacmi: {a.HavaHacmiM3:N1} m³");
            sb.AppendLine($"Gerçek yerleşim: {a.KoltukSayisi:N0} yer · {a.KoltukBasinaHacimM3:N1} m³/koltuk");
            sb.AppendLine($"Toplam 3B yüzey: {a.ToplamYuzeyAlaniM2:N1} m²");
            sb.AppendLine();
            sb.AppendLine("Kaba RT60 (malzeme ataması öncesi):");
            foreach (var b in a.RtBantlari)
                sb.AppendLine($"  {b.FrekansHz,4} Hz  {b.TahminiRt60S,5:0.00} s   hedef {b.HedefMinS:0.0}–{b.HedefMaxS:0.0} s {(b.Hedefte ? "✓" : "!")}");
            if (a.Bulgular.Count > 0)
            {
                sb.AppendLine(); sb.AppendLine("Bulgular:");
                foreach (var x in a.Bulgular) sb.AppendLine("• " + x);
            }
            sb.AppendLine();
            sb.AppendLine("Not: Bu hızlı kontrol varsayılan yutum kabulleri kullanır; nihai sonuç M01 malzeme ataması ve simülasyonudur.");
            OzetText.Text = sb.ToString();

            var kalite = SalonKaliteHaritasiServisi.AnalizEt(_salon);
            KaliteList.ItemsSource = kalite.Bulgular;
            KaliteOzetText.Text = kalite.Ozet + (kalite.M01IcinHazir ? " · M01 için engelleyici hata yok" : " · Önce hataları düzeltin");
            KesitiYenile();
            GorusuYenile();
            DurumText.Text = "Analiz güncellendi.";
        }
        catch (Exception ex)
        {
            DurumText.Text = "Analiz tamamlanamadı: " + ex.Message;
        }
    }

    private void GorusuYenile()
    {
        if (SekciyonCombo.SelectedItem is not SeyirciSekciyonu s)
        {
            GorusCanvas.Yerlesim = null; GorusCanvas.Rapor = null;
            GorusOzetText.Text = "Seyirci bölümü yok.";
            return;
        }
        var y = SalonKoltukYerlesimServisi.Uret(s, _salon.Sahne);
        var g = SalonGorusHattiServisi.AnalizEt(_salon, s);
        GorusCanvas.Yerlesim = y; GorusCanvas.Rapor = g;
        GorusOzetText.Text = $"{y.NormalKoltukSayisi} koltuk + {y.TekerlekliSandalyeYeriSayisi} erişilebilir yer · " +
                             $"C ort. {g.OrtalamaCMm:0} mm · minimum {g.MinimumCMm:0} mm · kabul %{g.IyiGorusYuzdesi:0}";
    }

    private void KesitiYenile()
    {
        double p = (double)(KesitKonum.Value ?? 0);
        KesitCanvas.Kesit = KesitTipCombo.SelectedIndex == 1
            ? SalonKesitServisi.EnineKesit(_salon, p)
            : SalonKesitServisi.BoyunaKesit(_salon, p);
    }

    private void PanelOner_OnClick(object? sender, RoutedEventArgs e)
    {
        int sutun = (int)(PanelSutun.Value ?? 3), sira = (int)(PanelSira.Value ?? 2);
        _panelOnerisi = SalonYansiticiServisi.OtomatikDizi(_salon, sutun, sira);
        PanelList.ItemsSource = _panelOnerisi.Select(x => $"{x.Ad} · {x.GenislikM:0.##}×{x.DerinlikM:0.##} m · " +
            $"({x.MerkezX:0.##}, {x.MerkezY:0.##}, {x.MerkezZ:0.##}) · eğim X {x.XEgimDerece:0.#}° / Y {x.YEgimDerece:0.#}°");
        PanelOzetText.Text = $"{_panelOnerisi.Count} panel önerildi";
    }

    private void PanelUygula_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_panelOnerisi.Count == 0) { DurumText.Text = "Önce bir panel önerisi hesaplayın."; return; }
        _degismedenOnce?.Invoke();
        string katman = _salon.Katmanlar.FirstOrDefault(x => x.Tip == SalonKatmanTipi.Yansitici)?.Id ?? "";
        foreach (var p in _panelOnerisi)
        {
            p.KatmanId = katman;
            _salon.YansiticiPaneller.Add(p);
        }
        Degisti = true;
        _panelOnerisi = [];
        PanelList.ItemsSource = null;
        PanelOzetText.Text = $"Salonda {_salon.YansiticiPaneller.Count} yansıtıcı var";
        DurumText.Text = "Yansıtıcı dizisi salona eklendi.";
        Yenile();
    }

    private void KaliteList_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (KaliteList.SelectedItem is SalonKaliteBulgusu b)
        {
            SeciliKaliteBulgusu = b;
            DurumText.Text = b.OdakNoktasi is { } p
                ? $"Ana çizimde odak: x={p.X:0.##}, y={p.Y:0.##} m"
                : "Bu bulgunun tek bir odak noktası yok.";
        }
    }

    private void Yenile_OnClick(object? sender, RoutedEventArgs e) => Yenile();
    private void Kullanim_SelectionChanged(object? sender, SelectionChangedEventArgs e) { if (IsLoaded) Yenile(); }
    private void Sekciyon_SelectionChanged(object? sender, SelectionChangedEventArgs e) { if (IsLoaded) GorusuYenile(); }
    private void Kesit_SelectionChanged(object? sender, SelectionChangedEventArgs e) { if (IsLoaded) KesitiYenile(); }
    private void KesitKonum_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e) { if (IsLoaded) KesitiYenile(); }
    private void Kapat_OnClick(object? sender, RoutedEventArgs e) => Close();
}
