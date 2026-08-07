using System;
using System.Collections.Generic;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Terrain.Models;

namespace Kst.Acoustics.Core.Terrain.Services;

/// F5 — Hafriyat (yarma/dolgu) hesabı, grid (ızgara) yöntemi.
///
/// Tasarım bölgelerinin kapsadığı alan ızgaraya bölünür; her hücre merkezinde
/// mevcut TIN kotu ile tasarım hedef kotu karşılaştırılır:
///   fark = mevcut − tasarım
///   fark > 0 → yarma (cut),  fark < 0 → dolgu (fill).
/// Hücre hacmi = |fark| × hücre alanı (orta-nokta / ortalama-uç-alan yaklaşımı).
///
/// MVP varsayımı: hücre tamamen tasarım bölgesi içinde sayılır (merkez testi).
/// Daha hassas kenar payı ve TIN-TIN kompozit hacim F9'a bırakılmıştır.
public class HafriyatHesabiServisi
{
    /// Izgara hücre kenarı (metre). Küçük = daha hassas, daha yavaş.
    public double GridAraligiM { get; set; } = 1.0;

    public HafriyatSonucu Hesapla(AraziYuzeyi mevcut, TasarimYuzeyi tasarim)
    {
        if (GridAraligiM <= 0) throw new ArgumentException("GridAraligiM pozitif olmalı.");

        var sonuc = new HafriyatSonucu { HucreAlaniM2 = GridAraligiM * GridAraligiM };

        if (!SinirBul(tasarim, out double minX, out double minY, out double maxX, out double maxY))
            return sonuc; // tasarım bölgesi yok

        double hucreAlani = sonuc.HucreAlaniM2;

        // Bölge bazlı dökümü referansla eşle (aynı TasarimBolgesi örneği → aynı döküm).
        var bolgeDokum = new Dictionary<TasarimBolgesi, HafriyatBolgeSonucu>();

        // Izgara hücre merkezlerini tara.
        for (double x = minX + GridAraligiM / 2.0; x < maxX; x += GridAraligiM)
        {
            for (double y = minY + GridAraligiM / 2.0; y < maxY; y += GridAraligiM)
            {
                var bolge = tasarim.HedefBolge(x, y);
                if (bolge is null) continue; // tasarım bölgesi dışı → hafriyat yok

                double? mevcutKot = mevcut.KotSorgula(x, y);
                if (mevcutKot is null)
                {
                    sonuc.KapsamDisiHucreSayisi++;
                    continue; // tasarım var ama mevcut yüzey kapsamıyor
                }

                sonuc.DegerlendirilenHucreSayisi++;
                double fark = mevcutKot.Value - bolge.HedefKotM;

                var dokum = BolgeDokumu(bolgeDokum, sonuc, bolge);
                dokum.HucreSayisi++;

                if (fark > 0)
                {
                    double h = fark * hucreAlani;
                    sonuc.YarmaHacmiM3 += h;
                    sonuc.YarmaHucreSayisi++;
                    dokum.YarmaHacmiM3 += h;
                }
                else if (fark < 0)
                {
                    double h = -fark * hucreAlani;
                    sonuc.DolguHacmiM3 += h;
                    sonuc.DolguHucreSayisi++;
                    dokum.DolguHacmiM3 += h;
                }
            }
        }

        return sonuc;
    }

    private static HafriyatBolgeSonucu BolgeDokumu(
        Dictionary<TasarimBolgesi, HafriyatBolgeSonucu> harita,
        HafriyatSonucu sonuc, TasarimBolgesi bolge)
    {
        if (!harita.TryGetValue(bolge, out var dokum))
        {
            dokum = new HafriyatBolgeSonucu { BolgeAdi = bolge.Ad, HedefKotM = bolge.HedefKotM };
            harita[bolge] = dokum;
            sonuc.BolgeSonuclari.Add(dokum);
        }
        return dokum;
    }

    private static bool SinirBul(
        TasarimYuzeyi tasarim, out double minX, out double minY, out double maxX, out double maxY)
    {
        minX = minY = double.MaxValue;
        maxX = maxY = double.MinValue;
        bool var = false;

        foreach (var b in tasarim.Bolgeler)
        {
            foreach (var p in b.Poligon)
            {
                var = true;
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }
        }

        return var;
    }
}
