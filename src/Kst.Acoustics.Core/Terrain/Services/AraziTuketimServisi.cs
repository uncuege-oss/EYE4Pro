using System.Linq;
using Kst.Acoustics.Core.Terrain.Models;

namespace Kst.Acoustics.Core.Terrain.Services;

/// `.e4arazi.json` artifact'ının **tüketici tarafı** salt-okunur yükleyicisi.
/// M15 (çevre aydınlatma), M61 (temel kota oturtma) ve mimari bu servisle araziyi
/// yükler; arazi geometrisini DEĞİŞTİRMEZ, yalnız kot sorgular. Üretici M60'tır.
public sealed class AraziTuketim
{
    public required AraziModeli Arazi { get; init; }
    public required AraziYuzeyi Yuzey { get; init; }

    public int Revizyon => Arazi.GeometriRevizyon;
    public string AraziAdi => Arazi.AraziAdi;
    public int UcgenSayisi => Yuzey.Ucgenler.Count;

    /// (x,y) için arazi kotu (TIN interpolasyonu); yüzey dışındaysa null.
    public double? KotSorgula(double x, double y) => Yuzey.KotSorgula(x, y);

    /// TIN düğümlerinin kot aralığı (min, max); boşsa null.
    public (double Min, double Max)? KotAraligi()
    {
        if (Yuzey.Dugumler.Count == 0) return null;
        double min = Yuzey.Dugumler.Min(d => d.Z);
        double max = Yuzey.Dugumler.Max(d => d.Z);
        return (min, max);
    }
}

public static class AraziTuketimServisi
{
    /// `.e4arazi.json` dosyasını salt-okunur yükler ve TIN'i kurar.
    public static AraziTuketim Yukle(string dosyaYolu)
    {
        var arazi = AraziGeometriServisi.Ac(dosyaYolu);
        var yuzey = new AraziYuzeyServisi().Olustur(arazi);
        return new AraziTuketim { Arazi = arazi, Yuzey = yuzey };
    }
}
