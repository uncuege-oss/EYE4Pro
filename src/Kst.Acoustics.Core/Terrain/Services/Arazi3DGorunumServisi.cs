using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Terrain.Models;

namespace Kst.Acoustics.Core.Terrain.Services;

/// M60 — Arazi TIN ve tasarım platformlarını izometrik önizleme yüzeylerine
/// (`ProjectSurface`) çevirir. App `GeometryPreviewControl` girdisidir
/// (M50 `Bina3DGorunumServisi` muadili). Hesap içermez; salt geometri dönüşümü.
public static class Arazi3DGorunumServisi
{
    public const string KategoriArazi = "AraziYuzey";
    public const string KategoriPlatform = "TasarimPlatform";
    public const string KategoriBlok = "TemelBlok";

    /// Ayak izi poligonunu `altKot`–`ustKot` arası dik bir kutu (taban + tavan + duvarlar)
    /// olarak yüzeylere çevirir — temel bloğunun 3B gösterimi.
    public static List<ProjectSurface> BlokKutusuYuzeyleri(
        IReadOnlyList<Point2D> poligon, double altKot, double ustKot, string ad, int baslangicId = 20000)
    {
        var liste = new List<ProjectSurface>();
        if (poligon.Count < 3) return liste;
        int id = baslangicId;

        liste.Add(new ProjectSurface(id++, $"{ad} taban", KategoriBlok,
            poligon.Select(p => new Point3D(p.X, p.Y, altKot)).ToList()));
        liste.Add(new ProjectSurface(id++, $"{ad} tavan", KategoriBlok,
            poligon.Select(p => new Point3D(p.X, p.Y, ustKot)).ToList()));

        for (int i = 0; i < poligon.Count; i++)
        {
            var a = poligon[i];
            var b = poligon[(i + 1) % poligon.Count];
            liste.Add(new ProjectSurface(id++, $"{ad} duvar", KategoriBlok, new List<Point3D>
            {
                new(a.X, a.Y, altKot), new(b.X, b.Y, altKot),
                new(b.X, b.Y, ustKot), new(a.X, a.Y, ustKot)
            }));
        }
        return liste;
    }

    /// TIN üçgenlerini birer yüzey olarak üretir.
    public static List<ProjectSurface> YuzeyleriOlustur(AraziYuzeyi yuzey)
    {
        var liste = new List<ProjectSurface>();
        int id = 1;
        foreach (var t in yuzey.Ucgenler)
        {
            var a = yuzey.Dugumler[t.A];
            var b = yuzey.Dugumler[t.B];
            var c = yuzey.Dugumler[t.C];
            liste.Add(new ProjectSurface(id++, "Arazi", KategoriArazi, new List<Point3D>
            {
                new(a.X, a.Y, a.Z), new(b.X, b.Y, b.Z), new(c.X, c.Y, c.Z)
            }));
        }
        return liste;
    }

    /// Yarma/dolgu renk haritası: TIN üçgeni id'si → [0..1] yoğunluk
    /// (GeometryPreviewControl HitIntensityMap heatmap'i için). Eşleme:
    ///   diff = mevcut(üçgen merkezi kotu) − tasarım hedef kotu
    ///   t = 0.5 + 0.5·(diff / maxAbs) → dolgu (diff&lt;0) maviye, yarma (diff&gt;0) kırmızıya.
    /// Üçgen id'leri <see cref="YuzeyleriOlustur"/> ile birebir hizalıdır (1'den artan, aynı sıra).
    /// Tasarım bölgesi dışındaki üçgenler haritaya girmez (nötr/soluk kalır).
    public static Dictionary<int, double> HafriyatRenkHaritasi(AraziYuzeyi yuzey, TasarimYuzeyi tasarim)
    {
        var tesviye = new AraziTesviyeServisi(yuzey, tasarim);
        var diffler = new Dictionary<int, double>();
        int id = 1;
        double maxAbs = 0;
        foreach (var t in yuzey.Ucgenler)
        {
            var a = yuzey.Dugumler[t.A];
            var b = yuzey.Dugumler[t.B];
            var c = yuzey.Dugumler[t.C];
            double cx = (a.X + b.X + c.X) / 3.0;
            double cy = (a.Y + b.Y + c.Y) / 3.0;
            double cz = (a.Z + b.Z + c.Z) / 3.0;

            var hedef = tesviye.Sorgula(cx, cy);
            if (hedef is not null)
            {
                double d = cz - hedef.HedefKotM;
                diffler[id] = d;
                double abs = d < 0 ? -d : d;
                if (abs > maxAbs) maxAbs = abs;
            }
            id++;
        }

        var harita = new Dictionary<int, double>();
        if (maxAbs < 1e-9) return harita;

        foreach (var kv in diffler)
        {
            double tnorm = 0.5 + 0.5 * (kv.Value / maxAbs);
            harita[kv.Key] = tnorm < 0 ? 0 : (tnorm > 1 ? 1 : tnorm);
        }
        return harita;
    }

    /// Tasarım platform/kademe poligonlarını hedef kotta düz yüzey olarak üretir.
    public static List<ProjectSurface> PlatformYuzeyleri(TasarimYuzeyi tasarim, int baslangicId = 10000)
    {
        var liste = new List<ProjectSurface>();
        int id = baslangicId;
        foreach (var b in tasarim.Bolgeler)
        {
            if (b.Poligon.Count < 3) continue;
            var v = new List<Point3D>(b.Poligon.Count);
            foreach (var p in b.Poligon)
                v.Add(new Point3D(p.X, p.Y, b.HedefKotM));
            string ad = string.IsNullOrWhiteSpace(b.Ad) ? "Platform" : b.Ad;
            liste.Add(new ProjectSurface(id++, ad, KategoriPlatform, v));
        }
        return liste;
    }
}
