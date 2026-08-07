using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Electric.CevreAydinlatma;
using Kst.Acoustics.Core.Terrain.Services;

namespace Kst.Acoustics.App.Electric.CevreAydinlatma;

/// <summary>
/// Çevre Aydınlatma (M15) penceresi ViewModel'i. Core servislerini orkestre eder: DXF'ten yüzey
/// yükleme, güzergâhlara otomatik direk yerleşimi ve M11 tek hat aktarımı. Dosya/diyalog işleri
/// pencere kod-arkasındadır; buradaki metotlar GUI'siz test edilebilir.
/// </summary>
public partial class CevreAydinlatmaViewModel : ObservableObject
{
    [ObservableProperty] private CevreAydinlatmaProje proje = new();
    [ObservableProperty] private ObservableCollection<CevreYuzey> yuzeyler = new();
    [ObservableProperty] private ObservableCollection<AydinlatmaDiregi> direkler = new();
    [ObservableProperty] private ObservableCollection<CizgiSegment> arkaPlan = new();
    [ObservableProperty] private CevreYuzey? seciliYuzey;
    [ObservableProperty] private string durumMesaji = "Hazır.";

    /// M60 Terrain köprüsü: yüklenen arazi (salt-okunur tüketim). null = arazi yok.
    [ObservableProperty] private string araziBilgisi = "Arazi yüklenmedi.";
    private AraziTuketim? _arazi;

    /// <summary>
    /// M60 `.e4arazi.json` arazisini salt-okunur yükler ve mevcut direklere arazi
    /// zemin kotunu (dip kotu) atar. Arazi geometrisi değiştirilmez.
    /// </summary>
    public void AraziYukleVeKotAta(string e4araziYolu)
    {
        _arazi = AraziTuketimServisi.Yukle(e4araziYolu);

        int atanan = 0, disarida = 0;
        foreach (var direk in Proje.Direkler)
        {
            var kot = _arazi.KotSorgula(direk.X, direk.Y);
            direk.ZeminKotuM = kot;
            if (kot.HasValue) atanan++; else disarida++;
        }

        string aralik = _arazi.KotAraligi() is { } r
            ? $"{r.Min:0.##}–{r.Max:0.##} m"
            : "—";
        string ad = string.IsNullOrWhiteSpace(_arazi.AraziAdi) ? "Arazi" : _arazi.AraziAdi;
        AraziBilgisi = $"{ad} (rev {_arazi.Revizyon}) yüklendi · kot aralığı {aralik} · {_arazi.UcgenSayisi} üçgen.";
        DurumMesaji = Proje.Direkler.Count == 0
            ? $"Arazi yüklendi. Henüz direk yok; yerleştirdikten sonra kotlar otomatik atanır."
            : $"Arazi yüklendi: {atanan} direğe zemin kotu atandı" +
              (disarida > 0 ? $", {disarida} direk arazi dışında." : ".");
    }

    /// <summary>Yüzey kategori ComboBox'ı için seçenekler.</summary>
    public System.Array Kategoriler => System.Enum.GetValues(typeof(YuzeyKategori));

    /// <summary>DXF dosyasından yüzeyleri (katmandan otomatik kategori) + arka plan çizgilerini yükler.</summary>
    public void DosyadanYuzeyYukle(string dxfYolu)
    {
        var (yuzeylerListe, arkaPlanListe) = CevreYuzeyImportServisi.DosyadanYukleTam(dxfYolu, out var uyari);
        Proje.DxfDosyaYolu = dxfYolu;
        Proje.BirimUyarisi = uyari;
        Proje.ArkaPlanSegmentleri.Clear();
        Proje.ArkaPlanSegmentleri.AddRange(arkaPlanListe);
        ArkaPlan = new ObservableCollection<CizgiSegment>(arkaPlanListe);
        YuzeyleriAyarla(yuzeylerListe);
        DurumMesaji = uyari ?? $"{yuzeylerListe.Count} yüzey, {arkaPlanListe.Count} arka plan çizgisi yüklendi.";
    }

    /// <summary>Yüzey listesini projeye ve görünür koleksiyona yazar.</summary>
    public void YuzeyleriAyarla(IReadOnlyList<CevreYuzey> yeniYuzeyler)
    {
        Proje.Yuzeyler.Clear();
        Proje.Yuzeyler.AddRange(yeniYuzeyler);
        Yuzeyler = new ObservableCollection<CevreYuzey>(yeniYuzeyler);
    }

    /// <summary>
    /// Seçili yüzeyin (ör. yeşil alan) kenarını kapalı güzergâh olarak ekler. Park aydınlatmasında
    /// "yeşil alan çevresine direk" akışı; serbest çizim gerektirmez. Eklenen güzergâhı döner.
    /// </summary>
    public AydinlatmaGuzergahi? SecilenYuzeydenGuzergahEkle(
        double aralikM, double kenarOfsetM, int? direkTanimId, int? armaturTanimId)
    {
        var yuzey = SeciliYuzey;
        if (yuzey is null || yuzey.Poligon.Count < 3)
        {
            DurumMesaji = "Önce kenarı ≥3 köşeli bir yüzey seçin.";
            return null;
        }

        int id = Proje.Guzergahlar.Count == 0 ? 1 : Proje.Guzergahlar.Max(g => g.Id) + 1;
        var guzergah = new AydinlatmaGuzergahi
        {
            Id = id,
            Ad = $"{yuzey.Ad} çevresi",
            Kapali = true,
            Rota = new List<Point2D>(yuzey.Poligon),
            DirekAralikM = aralikM,
            KenarOfsetM = kenarOfsetM,
            DirekTanimId = direkTanimId,
            ArmaturTanimId = armaturTanimId
        };
        Proje.Guzergahlar.Add(guzergah);
        DurumMesaji = $"'{guzergah.Ad}' güzergâhı eklendi (aralık {aralikM:0.#} m).";
        return guzergah;
    }

    /// <summary>Tüm güzergâhlara otomatik direk yerleştirir; görünür direk listesini yeniler.</summary>
    public int TumDirekleriYerlestir()
    {
        Proje.Direkler.Clear();
        int id = 1;
        foreach (var g in Proje.Guzergahlar)
        {
            var noktalar = g.Kapali
                ? DirekYerlesimServisi.PoligonCevresineYerlestir(g.Rota, g.DirekAralikM, g.KenarOfsetM)
                : DirekYerlesimServisi.GuzergahaYerlestir(g.Rota, g.DirekAralikM);

            foreach (var direk in DirekYerlesimServisi.DireklerOlustur(noktalar, g.Id, g.DirekTanimId, g.ArmaturTanimId))
            {
                direk.Id = id++;
                Proje.Direkler.Add(direk);
            }
        }

        Direkler = new ObservableCollection<AydinlatmaDiregi>(Proje.Direkler);
        DurumMesaji = $"{Proje.Direkler.Count} direk yerleştirildi.";
        return Proje.Direkler.Count;
    }

    /// <summary>Projeyi .e4p15 olarak kaydeder.</summary>
    public void ProjeKaydet(string dosyaYolu)
    {
        CevreAydinlatmaJsonServisi.Kaydet(Proje, dosyaYolu);
        DurumMesaji = $"Kaydedildi: {System.IO.Path.GetFileName(dosyaYolu)}";
    }

    /// <summary>.e4p15 projesini açar ve görünür koleksiyonları yeniler.</summary>
    public void ProjeAc(string dosyaYolu)
    {
        Proje = CevreAydinlatmaJsonServisi.Ac(dosyaYolu);
        Yuzeyler = new ObservableCollection<CevreYuzey>(Proje.Yuzeyler);
        Direkler = new ObservableCollection<AydinlatmaDiregi>(Proje.Direkler);
        ArkaPlan = new ObservableCollection<CizgiSegment>(Proje.ArkaPlanSegmentleri);
        DurumMesaji = $"Açıldı: {System.IO.Path.GetFileName(dosyaYolu)} "
            + $"({Proje.Yuzeyler.Count} yüzey, {Proje.Direkler.Count} direk).";
    }

    /// <summary>M11 tek hat aktarımı için ElektrikProje üretir (sonucu pencere .e4p11 olarak yazar).</summary>
    public CevreAydinlatmaTekHatAktarimServisi.Sonuc TekHataAktarimiOlustur()
    {
        var sonuc = CevreAydinlatmaTekHatAktarimServisi.Donustur(Proje);
        DurumMesaji = sonuc.Basarili
            ? $"Tek hat aktarımı hazır ({sonuc.Proje!.KokPano?.Linyeler.Count ?? 0} linye)."
            : $"Aktarım başarısız: {sonuc.Hata}";
        return sonuc;
    }
}
