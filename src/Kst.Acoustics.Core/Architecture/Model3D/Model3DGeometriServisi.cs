using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Architecture.Circulation;
using Kst.Acoustics.Core.Architecture.Layout;

namespace Kst.Acoustics.Core.Architecture.Model3D;

/// <summary>
/// Basit dikdortgen 2D taslaklari 3D uretime hazir geometriye ceviren yardimci servis.
/// </summary>
public static class Model3DGeometriServisi
{
    public static PlanSiniri KatSiniriBul(KatTaslagi? kat, double boslukM = 0)
    {
        if (kat is null)
            return new PlanSiniri();

        var dikdortgenler = new List<(double X, double Y, double GenislikM, double DerinlikM)>();
        if (kat.Sinir.AlanM2 > 0)
            dikdortgenler.Add((kat.Sinir.X, kat.Sinir.Y, kat.Sinir.GenislikM, kat.Sinir.DerinlikM));

        dikdortgenler.AddRange(kat.Bloklar.Where(b => b.YerlesmisMi)
            .Select(b => (b.X, b.Y, b.GenislikM, b.DerinlikM)));
        dikdortgenler.AddRange(kat.Koridorlar.Where(k => k.UzunlukM > 0 && k.GenislikM > 0)
            .Select(KoridorDikdortgeni));
        dikdortgenler.AddRange(kat.Cekirdekler.Where(c => c.YerlesmisMi)
            .Select(c => (c.X, c.Y, c.GenislikM, c.DerinlikM)));
        dikdortgenler.AddRange(kat.Saftlar.Where(s => s.YerlesmisMi)
            .Select(s => (s.X, s.Y, s.GenislikM, s.DerinlikM)));

        if (dikdortgenler.Count == 0)
            return new PlanSiniri();

        var bosluk = Math.Max(0, boslukM);
        var minX = dikdortgenler.Min(d => d.X) - bosluk;
        var minY = dikdortgenler.Min(d => d.Y) - bosluk;
        var maxX = dikdortgenler.Max(d => d.X + Math.Max(0, d.GenislikM)) + bosluk;
        var maxY = dikdortgenler.Max(d => d.Y + Math.Max(0, d.DerinlikM)) + bosluk;

        return new PlanSiniri
        {
            X = minX,
            Y = minY,
            GenislikM = Math.Max(0, maxX - minX),
            DerinlikM = Math.Max(0, maxY - minY)
        };
    }

    public static IReadOnlyList<Duvar3D> DikdortgenDuvarlariUret(
        MahalBlogu? blok,
        double tabanKotuM,
        double yukseklikM,
        double kalinlikM,
        string duvarTipiId = "")
    {
        if (blok is null || !blok.YerlesmisMi)
            return [];

        var x1 = blok.X;
        var y1 = blok.Y;
        var x2 = blok.X + blok.GenislikM;
        var y2 = blok.Y + blok.DerinlikM;
        var idOnEki = $"duvar3d-{blok.Id}";

        return
        [
            DuvarOlustur($"{idOnEki}-ust", blok, x1, y1, x2, y1, tabanKotuM, yukseklikM, kalinlikM, duvarTipiId),
            DuvarOlustur($"{idOnEki}-sag", blok, x2, y1, x2, y2, tabanKotuM, yukseklikM, kalinlikM, duvarTipiId),
            DuvarOlustur($"{idOnEki}-alt", blok, x2, y2, x1, y2, tabanKotuM, yukseklikM, kalinlikM, duvarTipiId),
            DuvarOlustur($"{idOnEki}-sol", blok, x1, y2, x1, y1, tabanKotuM, yukseklikM, kalinlikM, duvarTipiId)
        ];
    }

    private static Duvar3D DuvarOlustur(
        string id,
        MahalBlogu blok,
        double baslangicX,
        double baslangicY,
        double bitisX,
        double bitisY,
        double tabanKotuM,
        double yukseklikM,
        double kalinlikM,
        string duvarTipiId)
        => new()
        {
            Id = id,
            KaynakMahalBloguId = blok.Id,
            KatNo = blok.KatNo,
            BaslangicX = baslangicX,
            BaslangicY = baslangicY,
            BitisX = bitisX,
            BitisY = bitisY,
            TabanKotuM = tabanKotuM,
            YukseklikM = Math.Max(0, yukseklikM),
            KalinlikM = Math.Max(0, kalinlikM),
            DuvarTipiId = duvarTipiId
        };

    private static (double X, double Y, double GenislikM, double DerinlikM) KoridorDikdortgeni(KoridorTaslagi koridor)
    {
        var yarimGenislik = Math.Max(0, koridor.GenislikM) / 2.0;
        var minX = Math.Min(koridor.BaslangicX, koridor.BitisX);
        var maxX = Math.Max(koridor.BaslangicX, koridor.BitisX);
        var minY = Math.Min(koridor.BaslangicY, koridor.BitisY);
        var maxY = Math.Max(koridor.BaslangicY, koridor.BitisY);

        if (koridor.YatayMi)
            return (minX, koridor.BaslangicY - yarimGenislik, maxX - minX, koridor.GenislikM);

        if (koridor.DikeyMi)
            return (koridor.BaslangicX - yarimGenislik, minY, koridor.GenislikM, maxY - minY);

        return (minX - yarimGenislik, minY - yarimGenislik, maxX - minX + koridor.GenislikM, maxY - minY + koridor.GenislikM);
    }
}
