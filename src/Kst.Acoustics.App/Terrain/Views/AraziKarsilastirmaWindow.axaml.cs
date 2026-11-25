using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Kst.Acoustics.App.Terrain.Rendering;
using Kst.Acoustics.Core.Terrain.Models;
using Kst.Acoustics.Core.Terrain.Services;

namespace Kst.Acoustics.App.Terrain.Views;

public partial class AraziKarsilastirmaWindow : Window
{
    private sealed record SeciliYuzey(AraziYuzeyi Yuzey, string Tanim);
    private sealed class Kaynak(YuklenenArazi proje)
    {
        internal YuklenenArazi Proje { get; } = proje;
        private SeciliYuzey? _tasarim;

        internal SeciliYuzey Sec(bool tasarim, CancellationToken token)
        {
            if (!tasarim) return new(Proje.Yuzey, "Mevcut yüzey");
            if (_tasarim != null) return _tasarim;
            var preview = AraziTasarimOnizlemeServisi.Olustur(Proje.Yuzey, Proje.Model.Tasarim);
            var tin = new AraziYuzeyi();
            var indeks = new Dictionary<AraziDugum,int>();
            int Dugum(double x, double y, double z)
            {
                var p = new AraziDugum(x,y,z);
                if (indeks.TryGetValue(p, out int id)) return id;
                id = tin.Dugumler.Count;
                indeks.Add(p,id);
                tin.Dugumler.Add(p);
                return id;
            }
            foreach (var face in preview.Yuzeyler)
            {
                token.ThrowIfCancellationRequested();
                // Preview land and pads are triangles. Vertical cut walls have zero plan area.
                var v = face.Vertices;
                if (v.Count != 3) continue;
                tin.Ucgenler.Add(new(Dugum(v[0].X,v[0].Y,v[0].Z), Dugum(v[1].X,v[1].Y,v[1].Z), Dugum(v[2].X,v[2].Y,v[2].Z)));
            }
            string tanim = $"Tasarım yüzeyi (şev örneklemesi {preview.OrneklemeAraligiM:0.##} m)";
            if (preview.Uyarilar.Count > 0) tanim += " · " + string.Join(" ",preview.Uyarilar.Distinct());
            return _tasarim = new(tin,tanim);
        }
    }

    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private readonly CancellationTokenSource _kapanis = new();
    private Kaynak? _ilk, _son;
    private AraziKarsilastirmasi? _metraj;
    private string _ilkEtiket = "", _sonEtiket = "";
    private bool _hazir, _mesgul;

    public AraziKarsilastirmaWindow() : this(null) { }
    internal AraziKarsilastirmaWindow(YuklenenArazi? ilk)
    {
        InitializeComponent();
        IlkGorunum.CameraMoved += SonGorunum.MatchCamera;
        SonGorunum.CameraMoved += IlkGorunum.MatchCamera;
        SonGorunum.ShowColorKey = false;
        if (ilk != null) KaynakAta(true, ilk);
        Closed += (_, _) => {
            _kapanis.Cancel();
            IlkGorunum.Dispose();
            SonGorunum.Dispose();
        };
        _hazir = true;
        Yerlesim();
    }

    private void KaynakAta(bool ilk, YuklenenArazi proje)
    {
        if (proje.Yuzey.Ucgenler.Count == 0) throw new InvalidDataException("Dosyada karşılaştırılabilir bir arazi yüzeyi yok.");
        var kaynak = new Kaynak(proje);
        if (ilk) _ilk = kaynak; else _son = kaynak;
        var ad = ilk ? IlkAd : SonAd;
        var secim = ilk ? IlkSecim : SonSecim;
        ad.Text = Path.GetFileName(proje.Yol);
        ToolTip.SetTip(ad, proje.Yol);
        bool tasarimVar = proje.Model.Tasarim.Bolgeler.Count > 0;
        secim.IsVisible = tasarimVar;
        secim.SelectedIndex = !ilk && tasarimVar ? 1 : 0;
        (ilk ? IlkBilgi : SonBilgi).Text = $"{proje.Yuzey.Ucgenler.Count.ToString("N0",Tr)} üçgen · " +
            (secim.SelectedIndex == 1 ? "Tasarım yüzeyi" : "Mevcut yüzey");
        Bekleme.Text = _ilk == null ? "İlk hâli yükleyin." : _son == null ? "Son hâli yükleyin." : "Yüzeyler karşılaştırılıyor…";
    }

    private async void IlkYukle(object? sender, RoutedEventArgs e) => await DosyaSecAsync(true);
    private async void SonYukle(object? sender, RoutedEventArgs e) => await DosyaSecAsync(false);
    private async Task DosyaSecAsync(bool ilk)
    {
        if (_mesgul) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
            Title = ilk ? "İlk hâli yükle" : "Son hâli yükle",
            FileTypeFilter = [new FilePickerFileType("Arazi projeleri ve CAD") { Patterns = ["*.e4arazi.json","*.e4p60","*.dwg","*.dxf"] }]
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path != null) await ProjeYukleAsync(ilk, path);
    }

    internal async Task ProjeYukleAsync(bool ilk, string path)
    {
        if (_mesgul) return;
        Mesgul(true);
        try
        {
            Durum.Text = (ilk ? "İlk hâl" : "Son hâl") + " okunuyor…";
            var proje = await AraziProjeOkuyucu.AcAsync(this, path, _kapanis.Token);
            if (proje == null) { Durum.Text = "Dosya seçimi iptal edildi."; return; }
            _kapanis.Token.ThrowIfCancellationRequested();
            KaynakAta(ilk, proje);
            await KarsilastirAsync();
        }
        catch (OperationCanceledException) when (_kapanis.IsCancellationRequested) { }
        catch (Exception ex) { Durum.Text = ex.Message; }
        finally { Mesgul(false); }
    }

    private async void YuzeyDegisti(object? sender, SelectionChangedEventArgs e)
    {
        if (!_hazir || _mesgul) return;
        Mesgul(true);
        try { await KarsilastirAsync(); }
        catch (OperationCanceledException) when (_kapanis.IsCancellationRequested) { }
        catch (Exception ex) { Durum.Text = ex.Message; }
        finally { Mesgul(false); }
    }

    private async Task KarsilastirAsync()
    {
        _metraj = null;
        Yarma.Text = Dolgu.Text = Net.Text = Alan.Text = "—";
        Kapsam.Text = "";
        BosSahne.IsVisible = true;
        IlkGorunum.Scene(null, null, false);
        SonGorunum.Scene(null, null, false);
        var ilk = _ilk; var son = _son;
        if (ilk == null || son == null)
        {
            Durum.Text = "İki dosyada aynı koordinat sistemi ve kot referansını kullanın.";
            return;
        }
        Bekleme.Text = "Ortak yüzey ve metraj hesaplanıyor…";
        Durum.Text = Bekleme.Text;
        bool ilkTasarim = IlkSecim.SelectedIndex == 1, sonTasarim = SonSecim.SelectedIndex == 1;
        var token = _kapanis.Token;
        try
        {
            var sonuc = await Task.Run(() => {
                var baslangic = ilk.Sec(ilkTasarim,token);
                var bitis = son.Sec(sonTasarim,token);
                var miktar = AraziKarsilastirmaServisi.Hesapla(baslangic.Yuzey,bitis.Yuzey,token);
                var aglar = TerrainMesh.Compare(miktar,baslangic.Yuzey,bitis.Yuzey,token);
                return (miktar, aglar, baslangic, bitis);
            }, token);
            token.ThrowIfCancellationRequested();
            _metraj = sonuc.miktar;
            IlkBilgi.Text = sonuc.baslangic.Tanim; SonBilgi.Text = sonuc.bitis.Tanim;
            _ilkEtiket = Path.GetFileName(ilk.Proje.Yol) + " · " + sonuc.baslangic.Tanim;
            _sonEtiket = Path.GetFileName(son.Proje.Yol) + " · " + sonuc.bitis.Tanim;
            IlkGorunum.Scene(sonuc.aglar.First,null,false,true);
            SonGorunum.Scene(sonuc.aglar.Last,null,false,true);
            Yarma.Text = Birim(_metraj.YarmaM3,"m³"); Dolgu.Text = Birim(_metraj.DolguM3,"m³");
            Net.Text = Birim(_metraj.NetM3,"m³"); Alan.Text = Birim(_metraj.OrtakAlanM2,"m²");
            Kapsam.Text = $"Yarma alanı {Birim(_metraj.YarmaAlaniM2,"m²")} · Dolgu alanı {Birim(_metraj.DolguAlaniM2,"m²")} · " +
                $"Eşit kot {Birim(_metraj.EsitAlanM2,"m²")} · Yalnız ilk hâlde {Birim(Math.Max(0,_metraj.IlkAlanM2-_metraj.OrtakAlanM2),"m²")} · " +
                $"Yalnız son hâlde {Birim(Math.Max(0,_metraj.SonAlanM2-_metraj.OrtakAlanM2),"m²")}";
            Durum.Text = "Metraj ve 3B görünüm iki yüzeyin ortak alanını kapsar. Kot farkı ilk hâl eksi son hâldir.";
            BosSahne.IsVisible = false;
        }
        catch
        {
            Bekleme.Text = "Karşılaştırma tamamlanamadı.";
            throw;
        }
    }

    private static string Birim(double deger, string birim) => deger.ToString("N2",Tr) + " " + birim;
    private void Mesgul(bool evet)
    {
        _mesgul = evet;
        Kaynaklar.IsEnabled = !evet;
        Csv.IsEnabled = !evet && _metraj != null;
    }
    private void Sigdir(object? sender, RoutedEventArgs e) => IlkGorunum.ResetViewport();
    private void ModDegisti(object? sender, SelectionChangedEventArgs e) => Yerlesim();
    private void SahneBoyutuDegisti(object? sender, SizeChangedEventArgs e) => Yerlesim();
    private void PerdeDegisti(object? sender, RangeBaseValueChangedEventArgs e) => Yerlesim();
    private void Yerlesim()
    {
        if (!_hazir) return;
        bool perde = Mod.SelectedIndex == 1;
        Grid.SetColumnSpan(IlkGorunum, perde ? 2 : 1);
        Grid.SetColumn(SonGorunum, perde ? 0 : 1);
        Grid.SetColumnSpan(SonGorunum, perde ? 2 : 1);
        Perde.IsVisible = perde;
        double x = Sahne.Bounds.Width * (perde ? Perde.Value / 100 : .5);
        Ayirici.Margin = new Thickness(x,0,0,0);
        SonGorunum.Clip = perde ? new RectangleGeometry(new Rect(x,0,Math.Max(0,Sahne.Bounds.Width-x),Sahne.Bounds.Height)) : null;
    }

    private async void CsvKaydet(object? sender, RoutedEventArgs e)
    {
        if (_metraj == null || _mesgul) return;
        Mesgul(true);
        string? gecici = null;
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {
                Title = "İlk ve son hâl metrajı", SuggestedFileName = "ilk-son-hafriyat-metraj.csv", DefaultExtension = "csv",
                FileTypeChoices = [new FilePickerFileType("CSV") { Patterns = ["*.csv"] }]
            });
            string? path = file?.TryGetLocalPath();
            if (path == null) return;
            var metraj = _metraj;
            gecici = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await Task.Run(() => {
                using var writer = new StreamWriter(gecici,false,new UTF8Encoding(true));
                AraziMetrajCsv.Yaz(writer,metraj,_ilkEtiket,_sonEtiket,_kapanis.Token);
            },_kapanis.Token);
            _kapanis.Token.ThrowIfCancellationRequested();
            File.Move(gecici,path,true);
            Durum.Text = "Metraj kaydedildi · " + path;
        }
        catch (OperationCanceledException) when (_kapanis.IsCancellationRequested) { }
        catch (Exception ex) { Durum.Text = "Metraj kaydedilemedi. " + ex.Message; }
        finally
        {
            Mesgul(false);
            if (gecici != null && File.Exists(gecici))
            {
                try { File.Delete(gecici); } catch (IOException) { }
            }
        }
    }
}
