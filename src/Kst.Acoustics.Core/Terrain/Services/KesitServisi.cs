using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Terrain.Models;

namespace Kst.Acoustics.Core.Terrain.Services;

/// M61 F5 — bina-arazi kotlu kesit üreticisi. Bina bloklarının ortalama Y'sinden geçen
/// yatay (X ekseni) bir kesit hattı boyunca arazi profilini örnekler ve blokları kesit
/// dikdörtgenlerine projekte eder. Çizim (ölçek/etiket) App katmanındadır.
public static class KesitServisi
{
    public static KesitModeli Olustur(
        AraziTuketim arazi, TemelOturtmaModeli model, IReadOnlyList<BinaAyakIzi> ayaklar, int ornekSayisi = 200)
    {
        var kesit = new KesitModeli();

        // Arazi X sınırları (TIN düğümlerinden).
        var dugumler = arazi.Yuzey.Dugumler;
        if (dugumler.Count == 0) return kesit;
        double minX = dugumler.Min(d => d.X), maxX = dugumler.Max(d => d.X);
        kesit.MinX = minX;
        kesit.MaxX = maxX;

        // Kesit hattı Y'si: blok ayak izlerinin ortalama Y'si (yoksa arazi orta Y).
        double kesitY;
        var tumKose = ayaklar.SelectMany(a => a.Poligon).ToList();
        if (tumKose.Count > 0)
            kesitY = tumKose.Average(p => p.Y);
        else
            kesitY = (dugumler.Min(d => d.Y) + dugumler.Max(d => d.Y)) / 2.0;

        // Arazi profilini örnekle.
        int n = Math.Max(2, ornekSayisi);
        double adim = (maxX - minX) / (n - 1);
        for (int i = 0; i < n; i++)
        {
            double x = minX + i * adim;
            var z = arazi.KotSorgula(x, kesitY);
            if (z.HasValue) kesit.ZeminProfili.Add(new Point2D(x, z.Value));
        }

        // Blokları kesit dikdörtgenlerine projekte et (X aralığı = ayak izi X min/max).
        for (int i = 0; i < model.Bloklar.Count && i < ayaklar.Count; i++)
        {
            var b = model.Bloklar[i];
            var poly = ayaklar[i].Poligon;
            if (poly.Count == 0 || b.Kotlar.Count == 0) continue;

            kesit.Bloklar.Add(new KesitBlok
            {
                BlokId = b.BlokId,
                KademeNo = b.KademeNo,
                XMin = poly.Min(p => p.X),
                XMax = poly.Max(p => p.X),
                TabanKotM = b.BodrumTabanKotuM,
                CatiKotM = b.CatiKotuM,
                SifirKotM = b.SifirKotM
            });
        }

        // Kot sınırları (profil + bloklar).
        var kotlar = kesit.ZeminProfili.Select(p => p.Y).ToList();
        foreach (var b in kesit.Bloklar) { kotlar.Add(b.TabanKotM); kotlar.Add(b.CatiKotM); }
        if (kotlar.Count > 0)
        {
            kesit.MinKot = kotlar.Min();
            kesit.MaxKot = kotlar.Max();
        }

        return kesit;
    }
}
