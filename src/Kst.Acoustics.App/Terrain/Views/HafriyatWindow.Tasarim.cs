using System.Globalization;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Kst.Acoustics.App.Terrain.Rendering;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Terrain.Models;
using Kst.Acoustics.Core.Terrain.Services;

namespace Kst.Acoustics.App.Terrain.Views;

public partial class HafriyatWindow
{
    private string? _seciliId;
    private int _seciliKenar = -1;
    private TasarimBolgesi? _formBolge;
    private bool _formYukleniyor, _formKirli, _listeYukleniyor;
    private string _formImzasi = "", _hesapImzasi = "";
    private readonly List<string> _geri = new(), _ileri = new();
    private sealed record BolgeSatiri(string Id, string Metin) { public override string ToString() => Metin; }
    private static T Kopya<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    private TasarimBolgesi? SeciliBolge => _arazi?.Tasarim.Bolgeler.FirstOrDefault(b => b.Id == _seciliId);

    private void BolgeListesiniGuncelle(string? id)
    {
        Plan.CizimiBitir();
        _listeYukleniyor = true;
        try
        {
            var rows = _arazi?.Tasarim.Bolgeler.Select((b, i) => new BolgeSatiri(b.Id, $"{i + 1}. {b.Ad}  ·  {b.HedefKotM:0.###} m")).ToList() ?? new();
            BolgeListesi.ItemsSource = rows;
            BolgeListesi.SelectedItem = rows.FirstOrDefault(r => r.Id == id);
            _seciliId = id; _seciliKenar = -1; _formKirli = false;
            FormuYukle();
        }
        finally { _listeYukleniyor = false; }
    }
    private void BolgeSecimiDegisti(object? sender, SelectionChangedEventArgs e)
    {
        if (_listeYukleniyor || _kuruluyor) return;
        if (BolgeListesi.SelectedItem is BolgeSatiri row) BolgeSec(row.Id);
    }
    private void BolgeSec(string id, int kenar = -1)
    {
        if (_mesgul || _arazi == null) return;
        if (_seciliId != id && !BekleyenFormYok())
        {
            _listeYukleniyor = true;
            BolgeListesi.SelectedItem = ((IEnumerable<BolgeSatiri>?)BolgeListesi.ItemsSource)?.FirstOrDefault(r => r.Id == _seciliId);
            _listeYukleniyor = false;
            Plan.SeciliBolgeId = _seciliId; Plan.SeciliKenarIndex = _seciliKenar; Plan.InvalidateVisual();
            return;
        }
        if (_seciliId != id)
        {
            BolgeListesiniGuncelle(id);
        }
        if (kenar != _seciliKenar)
        {
            try { KenarFormunuTaslağaAl(); }
            catch (Exception ex)
            {
                DurumText.Text = ex.Message;
                Plan.SeciliBolgeId = _seciliId; Plan.SeciliKenarIndex = _seciliKenar; Plan.InvalidateVisual();
                return;
            }
            _seciliKenar = kenar; KenarFormunuYukle();
        }
        if (kenar >= 0) KenarPanel.IsExpanded = true;
        Plan.SeciliBolgeId = _seciliId; Plan.SeciliKenarIndex = _seciliKenar; Plan.InvalidateVisual();
    }
    private void FormuYukle()
    {
        _formYukleniyor = true;
        try
        {
            var b = SeciliBolge; _formBolge = b == null ? null : Kopya(b);
            BolgeFormu.IsEnabled = b != null; _formKirli = false;
            BolgeAdiBox.Text = b?.Ad ?? ""; HedefKotBox.Text = b?.HedefKotM.ToString("0.###", CultureInfo.InvariantCulture) ?? "";
            KenarTipiBox.SelectedIndex = b?.KenarGecisi == TasarimKenarTipi.Sev ? 1 : 0;
            SevOraniBox.Text = (b?.YatayDuseyOrani ?? 1.5).ToString("0.###", CultureInfo.InvariantCulture);
            SevMesafeBox.Text = (b?.SevAzamiMesafeM ?? 50).ToString("0.###", CultureInfo.InvariantCulture);
            KenarListesi.ItemsSource = b?.Poligon.Select((p, i) => $"Kenar {i + 1} — {(b.KenarTipi(i) == TasarimKenarTipi.Sev ? "şev" : "dik kesim")}").ToList();
            KenarListesi.SelectedIndex = _seciliKenar;
            BolgeBilgiText.Text = b == null ? "Başlamak için + Bölge Çiz veya + Dikdörtgen seçin." : $"{b.Poligon.Count} köşe · alan {PoligonYardimci.Alan(b.Poligon):N1} m² · köşeleri planda sürükleyebilirsiniz.";
            FormDurumText.Text = "";
            Plan.SeciliBolgeId = b?.Id; Plan.SeciliKenarIndex = _seciliKenar; Plan.InvalidateVisual();
            KenarFormunuYukle();
            _formImzasi = FormImzasi();
        }
        finally { _formYukleniyor = false; }
    }
    private void KenarFormunuYukle()
    {
        bool onceki = _formYukleniyor; _formYukleniyor = true;
        try
        {
            KenarListesi.SelectedIndex = _seciliKenar;
            var ayar = _formBolge?.KenarAyarlari.FirstOrDefault(k => k.KenarIndex == _seciliKenar);
            TekKenarTipiBox.SelectedIndex = ayar == null ? 0 : ayar.Tip == TasarimKenarTipi.DikKesim ? 1 : 2;
            TekKenarOraniBox.Text = (ayar?.YatayDuseyOrani ?? _formBolge?.YatayDuseyOrani ?? 1.5).ToString("0.###", CultureInfo.InvariantCulture);
            TekKenarTipiBox.IsEnabled = TekKenarOraniBox.IsEnabled = _seciliKenar >= 0;
            _formImzasi = FormImzasi();
        }
        finally { _formYukleniyor = onceki; }
    }
    private void KenarSecimiDegisti(object? sender, SelectionChangedEventArgs e)
    {
        if (_formYukleniyor || _kuruluyor || _seciliId == null || KenarListesi.SelectedIndex == _seciliKenar) return;
        int hedef = KenarListesi.SelectedIndex;
        try { KenarFormunuTaslağaAl(); _seciliKenar = hedef; KenarFormunuYukle(); }
        catch (Exception ex)
        {
            _formYukleniyor = true; KenarListesi.SelectedIndex = _seciliKenar; _formYukleniyor = false;
            DurumText.Text = ex.Message;
        }
        Plan.SeciliKenarIndex = _seciliKenar; Plan.InvalidateVisual();
    }
    private void KenarFormunuTaslağaAl()
    {
        if (_formBolge == null || _seciliKenar < 0) return;
        double? oran = null;
        if (TekKenarTipiBox.SelectedIndex == 2)
        {
            if (!Sayi(TekKenarOraniBox.Text, out double sayi) || sayi <= 0) throw new ArgumentException("Seçili kenarın şev oranı pozitif olmalı.");
            oran = sayi;
        }
        _formBolge.KenarAyarlari.RemoveAll(k => k.KenarIndex == _seciliKenar);
        if (TekKenarTipiBox.SelectedIndex > 0) _formBolge.KenarAyarlari.Add(new TasarimKenarAyari {
            KenarIndex = _seciliKenar, Tip = TekKenarTipiBox.SelectedIndex == 1 ? TasarimKenarTipi.DikKesim : TasarimKenarTipi.Sev, YatayDuseyOrani = oran });
    }
    private string FormImzasi() => $"{BolgeAdiBox.Text}|{HedefKotBox.Text}|{KenarTipiBox.SelectedIndex}|{SevOraniBox.Text}|{SevMesafeBox.Text}|{TekKenarTipiBox.SelectedIndex}|{TekKenarOraniBox.Text}";
    private void FormDegisti(object? sender, TextChangedEventArgs e) => FormDegisikliginiIsle();
    private void FormSecimiDegisti(object? sender, SelectionChangedEventArgs e) => FormDegisikliginiIsle();
    private void FormDegisikliginiIsle()
    {
        if (_kuruluyor || _formYukleniyor || _formBolge == null) return;
        string imza = FormImzasi(); if (imza == _formImzasi) return;
        _formImzasi = imza; _formKirli = true;
        FormDurumText.Text = "Değişiklikler bekliyor. Bölgeyi Uygula ile tasarıma aktarın.";
    }
    private bool BekleyenFormYok()
    {
        if (Plan.CizimModu)
        {
            DurumText.Text = "Önce bölge çizimini Tamamla ile bitirin veya İptal edin.";
            return false;
        }
        if (!_formKirli) return true;
        DurumText.Text = "Seçili bölgenin değişikliklerini önce Bölgeyi Uygula ile uygulayın veya Vazgeç ile geri alın.";
        return false;
    }
    private void FormVazgec_OnClick(object? sender, RoutedEventArgs e) { if (!_mesgul) FormuYukle(); }
    private async void BolgeUygula_OnClick(object? sender, RoutedEventArgs e) => await FormuUygulaAsync();
    private async Task<bool> FormuUygulaAsync()
    {
        if (_mesgul || _arazi == null || _formBolge == null) return false;
        if (Plan.CizimModu) { DurumText.Text = "Önce bölge çizimini tamamlayın veya iptal edin."; return false; }
        try
        {
            if (!Sayi(HedefKotBox.Text, out double kot)) throw new ArgumentException("Hedef kot sonlu bir sayı olmalı.");
            if (!Sayi(SevOraniBox.Text, out double oran) || oran <= 0) throw new ArgumentException("Şev yatay/düşey oranı pozitif olmalı.");
            if (!Sayi(SevMesafeBox.Text, out double mesafe) || mesafe <= 0) throw new ArgumentException("Azami şev mesafesi pozitif olmalı.");
            if (string.IsNullOrWhiteSpace(BolgeAdiBox.Text)) throw new ArgumentException("Bölge adını girin.");
            KenarFormunuTaslağaAl();
            var bolge = Kopya(_formBolge);
            bolge.Ad = BolgeAdiBox.Text.Trim(); bolge.HedefKotM = kot;
            bolge.KenarGecisi = KenarTipiBox.SelectedIndex == 1 ? TasarimKenarTipi.Sev : TasarimKenarTipi.DikKesim;
            bolge.YatayDuseyOrani = oran; bolge.SevAzamiMesafeM = mesafe;
            var tasarim = Kopya(_arazi.Tasarim);
            int i = tasarim.Bolgeler.FindIndex(b => b.Id == bolge.Id);
            if (i < 0) return false;
            tasarim.Bolgeler[i] = bolge;
            return await TasarimiUygulaAsync(tasarim, bolge.Id, "Bölge güncellendi.");
        }
        catch (Exception ex) { DurumText.Text = "Bölge uygulanamadı: " + ex.Message; return false; }
    }

    private void SinirCiz_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_yuzey == null || _mesgul || !BekleyenFormYok()) return;
        GorunumSekmeleri.SelectedIndex = 0; Plan.CizimeBasla();
        DurumText.Text = "Yeni bölgenin köşelerini sırayla seçin, sonra Tamamla'ya basın.";
    }
    private void Dikdortgen_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_yuzey == null || _mesgul || !BekleyenFormYok()) return;
        GorunumSekmeleri.SelectedIndex = 0; Plan.DikdortgenCizimeBasla();
        DurumText.Text = "Dikdörtgenin karşılıklı iki köşesini seçin, sonra Tamamla'ya basın.";
    }
    private async void SinirTamamla_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_mesgul || _yuzey == null || _arazi == null || !Plan.CizimModu) return;
        if (_formKirli)
        {
            DurumText.Text = "Mevcut bölgenin formunda bekleyen değişiklik var. Çizimi İptal edip Bölgeyi Uygula'yı seçin; veya formda Vazgeç'i seçip çizimi tamamlayın.";
            return;
        }
        try
        {
            var sinir = Plan.CizimNoktalari.ToList();
            var kotlar = sinir.Select(p => _kotIndeksi?.KotSorgula(p.X, p.Y)).Where(z => z.HasValue).Select(z => z!.Value).ToList();
            if (kotlar.Count == 0) throw new ArgumentException("Arazi içinde bir bölge çizin.");
            double kot = Math.Round(kotlar.Average(), 3);
            var yeni = AraziTasarimServisi.PlatformOlustur(_yuzey, kot, sinir, $"Bölge {_arazi.Tasarim.Bolgeler.Count + 1}").Bolgeler[0];
            var tasarim = Kopya(_arazi.Tasarim); tasarim.Bolgeler.Add(yeni);
            if (await TasarimiUygulaAsync(tasarim, yeni.Id, "Bölge oluşturuldu. Başlangıç kotu köşelerdeki mevcut kotların ortalamasıdır; hedef kotu düzenleyin."))
                Plan.CizimiBitir();
        }
        catch (Exception ex) { DurumText.Text = "Bölge oluşturulamadı: " + ex.Message; }
    }
    private void SinirIptal_OnClick(object? sender, RoutedEventArgs e) { if (!_mesgul) { Plan.CizimiBitir(); DurumText.Text = "Çizim / kot seçimi iptal edildi."; } }
    private async Task SinirDuzenleAsync(string id, List<Point2D> noktalar)
    {
        if (_mesgul || _yuzey == null || _arazi == null || !BekleyenFormYok()) { Plan.InvalidateVisual(); return; }
        try
        {
            var tasarim = Kopya(_arazi.Tasarim); var b = tasarim.Bolgeler.FirstOrDefault(x => x.Id == id);
            if (b == null) return;
            AraziTasarimServisi.PlatformOlustur(_yuzey, b.HedefKotM, noktalar);
            b.Poligon = noktalar;
            await TasarimiUygulaAsync(tasarim, id, "Bölge sınırı güncellendi.");
        }
        catch (Exception ex) { DurumText.Text = "Sınır uygulanamadı: " + ex.Message; Plan.OnizlemeyiGuncelle(); }
    }
    private void KotSec_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_mesgul || SeciliBolge == null) return;
        if (Plan.CizimModu) { DurumText.Text = "Kot seçmeden önce bölge çizimini tamamlayın veya iptal edin."; return; }
        GorunumSekmeleri.SelectedIndex = 0; Plan.KotSecimineBasla();
        DurumText.Text = "Hedef kutusuna almak istediğiniz mevcut arazi noktasına tıklayın.";
    }
    private void Sigdir_OnClick(object? sender, RoutedEventArgs e)
    {
        if (GorunumSekmeleri.SelectedIndex == 1) Onizleme3D.ResetViewport(); else Plan.Sigdir();
    }
    private async void BolgeSil_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_mesgul || _arazi == null || _seciliId == null || !BekleyenFormYok()) return;
        var tasarim = Kopya(_arazi.Tasarim); tasarim.Bolgeler.RemoveAll(b => b.Id == _seciliId);
        await TasarimiUygulaAsync(tasarim, tasarim.Bolgeler.FirstOrDefault()?.Id, "Bölge silindi. Geri Al ile geri getirebilirsiniz.");
    }
    private async void Yukari_OnClick(object? sender, RoutedEventArgs e) => await SirayiDegistir(-1);
    private async void Asagi_OnClick(object? sender, RoutedEventArgs e) => await SirayiDegistir(1);
    private async Task SirayiDegistir(int delta)
    {
        if (_mesgul || _arazi == null || !BekleyenFormYok()) return;
        var tasarim = Kopya(_arazi.Tasarim); int i = tasarim.Bolgeler.FindIndex(b => b.Id == _seciliId);
        if (i < 0 || i + delta < 0 || i + delta >= tasarim.Bolgeler.Count) return;
        (tasarim.Bolgeler[i], tasarim.Bolgeler[i + delta]) = (tasarim.Bolgeler[i + delta], tasarim.Bolgeler[i]);
        await TasarimiUygulaAsync(tasarim, _seciliId, "Bölge önceliği güncellendi.");
    }

    private async Task<bool> TasarimiUygulaAsync(TasarimYuzeyi tasarim, string? selected, string mesaj, bool history = true)
    {
        if (_mesgul || _arazi == null || _yuzey == null) return false;
        Mesgul(true, "Bölge sınırları ve şev birleşimleri kontrol ediliyor…");
        try
        {
            var evaluator = await Task.Run(() => new AraziTesviyeServisi(_yuzey, tasarim), _kapanis.Token);
            if (history) { _geri.Add(JsonSerializer.Serialize(_arazi.Tasarim)); if (_geri.Count > 60) _geri.RemoveAt(0); _ileri.Clear(); }
            _arazi.Tasarim = tasarim; _tesviye = evaluator; _onizleme = null; _renkler = null;
            BolgeListesiniGuncelle(selected); Plan.Tasarim = tasarim; SonucuTemizle(); Plan.OnizlemeyiGuncelle();
            await OnizlemeHazirlaAsync();
            if (tasarim.Bolgeler.Count > 0) YuzeyGorunumBox.SelectedIndex = 1; else YuzeyGorunumBox.SelectedIndex = 0;
            GorunumuUygula(); DurumText.Text = mesaj;
            return true;
        }
        catch (Exception ex) { DurumText.Text = "Tasarım uygulanamadı: " + ex.Message; return false; }
        finally { Mesgul(false); }
    }
    private async void GeriAl_OnClick(object? sender, RoutedEventArgs e) => await GecmisiUygula(true);
    private async void IleriAl_OnClick(object? sender, RoutedEventArgs e) => await GecmisiUygula(false);
    private async Task GecmisiUygula(bool geri)
    {
        if (_mesgul || _arazi == null || !BekleyenFormYok()) return;
        if (Plan.CizimModu) { DurumText.Text = "Tasarım geçmişine dönmeden önce bölge çizimini tamamlayın veya iptal edin."; return; }
        var from = geri ? _geri : _ileri; var to = geri ? _ileri : _geri;
        if (from.Count == 0) return;
        string onceki = JsonSerializer.Serialize(_arazi.Tasarim);
        var restored = JsonSerializer.Deserialize<TasarimYuzeyi>(from[^1])!;
        if (await TasarimiUygulaAsync(restored, restored.Bolgeler.FirstOrDefault()?.Id, geri ? "Son tasarım işlemi geri alındı." : "Tasarım işlemi yeniden uygulandı.", false))
        { from.RemoveAt(from.Count - 1); to.Add(onceki); GecmisButonlari(); }
    }
    private void GecmisButonlari()
    {
        if (GeriAlButton == null || IleriAlButton == null) return;
        GeriAlButton.IsEnabled = !_mesgul && _geri.Count > 0; IleriAlButton.IsEnabled = !_mesgul && _ileri.Count > 0;
    }
    private async Task OnizlemeHazirlaAsync()
    {
        if (_arazi == null || _yuzey == null || _arazi.Tasarim.Bolgeler.Count == 0) { _onizleme = null; _renkler = null; return; }
        var tasarim = _arazi.Tasarim; var yuzey = _yuzey; var mevcutAg = _mevcutAg!;
        try
        {
            var tuple = await Task.Run(() => {
                var on = AraziTasarimOnizlemeServisi.Olustur(yuzey, tasarim, 2);
                var mesh = TerrainMesh.Design(on.Yuzeyler, mevcutAg);
                return (onizleme: new Onizleme(mesh, on.OrneklemeAraligiM, on.Uyarilar),
                    renkler: mevcutAg.HeatColors(Arazi3DGorunumServisi.HafriyatRenkHaritasi(yuzey, tasarim)));
            }, _kapanis.Token);
            _onizleme = tuple.onizleme; _renkler = tuple.renkler;
            RaporText.Text = KontrolNotlari();
        }
        catch (Exception ex)
        {
            _onizleme = null; _renkler = null;
            RaporText.Text = "Tasarım kaydı hazır; 3B önizleme üretilemedi: " + ex.Message;
        }
    }
    private string KontrolNotlari()
    {
        var notes = (_tesviye?.Uyarilar ?? []).Concat(_onizleme?.Uyarilar ?? []).Distinct().ToList();
        return (notes.Count == 0 ? "Bölge ve şev kontrollerinde uyarı yok." : string.Join("\n", notes)) +
            "\n\nHacimler henüz hesaplanmadı. Tüm Bölgelerin Hafriyatını Hesapla ile bölge bazlı sonucu alın.";
    }
    private void GorunumDegisti(object? sender, SelectionChangedEventArgs e) { if (!_kuruluyor) GorunumuUygula(); }
    private void GorunumuUygula()
    {
        if (_yuzey == null) return;
        int mode = YuzeyGorunumBox.SelectedIndex;
        Plan.Tasarim = _arazi?.Tasarim;
        Plan.HedefKotSorgula = _tesviye == null ? null : (x, y) => _tesviye.Sorgula(x, y)?.HedefKotM;
        Plan.RenkHaritasi = mode == 2; Plan.TasarimGorunumu = mode == 1; Plan.OnizlemeyiGuncelle();
        Onizleme3D.Scene(mode == 1 && _onizleme != null ? _onizleme.Ag : _mevcutAg,
            mode == 2 ? _renkler : null, mode == 2);
        OnizlemeBilgiText.Text = mode == 0 ? "Mevcut zemin" : mode == 2 ? "Kırmızı: yarma · mavi: dolgu" :
            _onizleme != null ? $"Tasarım · şev örneklemesi {_onizleme.OrneklemeAraligiM:0.##} m" : "Tasarım önizlemesi yok; mevcut zemin";
    }
    private string HesapImzasi() => $"{GridBox.Text}|{KabarmaBox.Text}|{SikismaBox.Text}";
    private void HesapAyariDegisti(object? sender, TextChangedEventArgs e)
    {
        if (_kuruluyor || _arazi == null) return;
        string signature = HesapImzasi(); if (signature == _hesapImzasi) return;
        _hesapImzasi = signature; SonucuTemizle();
    }
    private void SonucuTemizle()
    {
        SonucDurumText.Text = _arazi?.Tasarim.Bolgeler.Count > 0 ? "Tasarım hazır; güncel hacim için hesaplayın." : "Mevcut arazi hazır; henüz tasarım bölgesi yok.";
        RaporText.Text = _arazi?.Tasarim.Bolgeler.Count > 0 ? KontrolNotlari() : "Başlamak için plan üzerinde bir bölge çizin. Mevcut arazi değiştirilmedi.";
    }
    private async void Hesapla_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_mesgul || _arazi == null || _yuzey == null) return;
        if (Plan.CizimModu) { DurumText.Text = "Önce bölge çizimini tamamlayın veya iptal edin."; return; }
        if (_formKirli && !await FormuUygulaAsync()) return;
        if (_arazi.Tasarim.Bolgeler.Count == 0) { DurumText.Text = "Önce en az bir tasarım bölgesi oluşturun."; return; }
        try
        {
            if (!Sayi(GridBox.Text, out var grid) || grid <= 0) throw new ArgumentException("Izgara aralığı pozitif olmalı.");
            if (!Sayi(KabarmaBox.Text, out var kabarma) || kabarma < 0 || !Sayi(SikismaBox.Text, out var sikisma) || sikisma < 0)
                throw new ArgumentException("Kabarma ve sıkışma yüzdeleri sıfır veya pozitif olmalı.");
            Mesgul(true, "Tüm bölgeler ve kenar şevleri için hacimler hesaplanıyor…");
            var sonuc = await Task.Run(() => new HafriyatHesabiServisi { GridAraligiM = grid }.Hesapla(_yuzey, _arazi.Tasarim), _kapanis.Token);
            if (sonuc.DegerlendirilenHucreSayisi == 0) throw new InvalidOperationException("Izgara üzerinde hesaplanabilir alan yok; ızgara aralığını küçültün.");
            var rapor = HafriyatRaporServisi.Olustur(sonuc, new HafriyatKatsayilari { KabarmaYuzdesi = kabarma, SikismaYuzdesi = sikisma });
            if (!double.IsFinite(rapor.KazilanGevsekHacimM3) || !double.IsFinite(rapor.DolguIcinGerekliBankHacimM3)) throw new ArgumentException("Hesap katsayıları sayı sınırını aşıyor.");
            RaporText.Text = rapor.Ozet + $"\nIzgara: {grid:0.###} m · {sonuc.DegerlendirilenHucreSayisi:N0} hücre\n" + string.Join("\n", _tesviye?.Uyarilar ?? []);
            SonucDurumText.Text = "Hesap güncel. Platformlar ve şevler dahildir.";
            YuzeyGorunumBox.SelectedIndex = 2; GorunumuUygula();
            DurumText.Text = $"Yarma {sonuc.YarmaHacmiM3:N1} m³ · Dolgu {sonuc.DolguHacmiM3:N1} m³ · Net {sonuc.NetHacimM3:N1} m³";
        }
        catch (Exception ex) { DurumText.Text = "Hesap yapılamadı: " + ex.Message; }
        finally { Mesgul(false); }
    }
}
