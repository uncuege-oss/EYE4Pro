using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Landscape.Models;
using Kst.Acoustics.Core.Terrain.Models;
using Kst.Acoustics.Core.Terrain.Services;

namespace Kst.Acoustics.Core.Landscape.Services;

/// M70 F1 — park altlıklarının (arazi `.e4arazi`, bina `.e4bina`) **salt-okunur**
/// tüketimi ve park sınırının kurulması. Altlık geometrileri DEĞİŞTİRİLMEZ
/// (codex70 §3, maincodex §4.5/§4.7/§4.8). Modelde yalnız yol + revizyon damgası
/// saklanır; canlı arazi tüketici nesnesi (`AraziTuketim`) çağırana döndürülür.
public static class ParkAltlikServisi
{
    /// `.e4arazi.json`'ı salt-okunur yükler; modele yol + revizyon işler ve kot
    /// sorgusu için canlı `AraziTuketim` döndürür. Üretici M60 (Terrain).
    public static AraziTuketim AraziYukle(ParkModeli park, string dosyaYolu)
    {
        var tuketim = AraziTuketimServisi.Yukle(dosyaYolu);
        park.AraziDosyaYolu = dosyaYolu;
        park.AraziRevizyon = tuketim.Revizyon;
        return tuketim;
    }

    /// `.e4bina.json`'ı salt-okunur yükler; ayak izi + setback'ten bir
    /// `BinaDislama` üretip modele ekler. Bina geometrisi M50'de düzenlenir.
    /// Ayak izi türetimi mevcut <see cref="BinaAyakIziAdaptoru"/> ile paylaşılır
    /// (kod kopyası yok — maincodex §4.4).
    public static BinaDislama BinaYukle(ParkModeli park, string dosyaYolu, double setbackM)
    {
        var bina = BinaGeometriServisi.Ac(dosyaYolu);
        var ayak = BinaAyakIziAdaptoru.Donustur(bina);

        var dislama = new BinaDislama
        {
            BinaAdi = ayak.BlokId,
            AyakIzi = ayak.Poligon.ToList(),
            SetbackM = setbackM
        };

        park.BinaDislamalari.Add(dislama);
        park.BinaDosyaYolu = dosyaYolu;
        park.BinaRevizyon = bina.GeometriRevizyon;
        return dislama;
    }

    /// Park sınırını arazi TIN düğümlerinin konveks kabuğundan kurar. Arazi yoksa
    /// veya düğüm yetersizse sınır boş kalır (kullanıcı DXF/elle girer).
    public static void ParkSiniriAraziden(ParkModeli park, AraziTuketim arazi)
    {
        var noktalar = arazi.Yuzey.Dugumler
            .Select(d => new Point2D(d.X, d.Y))
            .ToList();
        park.Sinir.Poligon = PoligonYardimci.KonveksKabuk(noktalar);
    }

    /// Park sınırını DXF'ten/elle gelen kapalı poligonla kurar (arazi gerektirmez).
    public static void ParkSiniriAyarla(ParkModeli park, IEnumerable<Point2D> poligon)
        => park.Sinir.Poligon = poligon.ToList();

    /// Yüklü arazinin güncellik durumu (eskime kontrolü — açılışta "yeniden doğrula?").
    public static AltlikDurumu AraziDurumu(ParkModeli park, AraziTuketim? arazi)
    {
        if (arazi is null || park.AraziDosyaYolu is null) return AltlikDurumu.Yok;
        return arazi.Revizyon == park.AraziRevizyon ? AltlikDurumu.Bagli : AltlikDurumu.Eskimis;
    }

    /// Yüklü binanın güncellik durumu. `binaRevizyon` o an `.e4bina`'dan okunan değer.
    public static AltlikDurumu BinaDurumu(ParkModeli park, int? binaRevizyon)
    {
        if (binaRevizyon is null || park.BinaDosyaYolu is null) return AltlikDurumu.Yok;
        return binaRevizyon == park.BinaRevizyon ? AltlikDurumu.Bagli : AltlikDurumu.Eskimis;
    }
}
