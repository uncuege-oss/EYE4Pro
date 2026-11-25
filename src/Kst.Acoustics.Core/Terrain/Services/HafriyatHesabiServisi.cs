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
        if (!double.IsFinite(GridAraligiM) || GridAraligiM <= 0 ||
            !double.IsFinite(GridAraligiM * GridAraligiM) || GridAraligiM * GridAraligiM == 0)
            throw new ArgumentException("GridAraligiM pozitif, sonlu ve temsil edilebilir olmalı.");
        foreach (var bolge in tasarim.Bolgeler)
        {
            if (!double.IsFinite(bolge.HedefKotM) ||
                bolge.Poligon.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y)))
                throw new ArgumentException("Tasarım bölgesi koordinatları ve hedef kotu sonlu olmalıdır.");
        }

        var sonuc = new HafriyatSonucu { HucreAlaniM2 = GridAraligiM * GridAraligiM };

        if (tasarim.Bolgeler.Count == 0) return sonuc;
        var kotIndeksi = new AraziKotIndeksi(mevcut);
        var tesviye = new AraziTesviyeServisi(kotIndeksi, tasarim);
        double minX=tesviye.Sinir.MinX, minY=tesviye.Sinir.MinY, maxX=tesviye.Sinir.MaxX, maxY=tesviye.Sinir.MaxY;

        double hucreAlani = sonuc.HucreAlaniM2;
        double cols = Math.Max(0, Math.Ceiling((maxX - minX) / GridAraligiM - 0.5));
        double rows = Math.Max(0, Math.Ceiling((maxY - minY) / GridAraligiM - 0.5));
        if (!double.IsFinite(cols) || !double.IsFinite(rows) || cols > 2_000_000 || rows > 2_000_000 ||
            cols * rows > 2_000_000 || minX + GridAraligiM == minX || minY + GridAraligiM == minY)
            throw new ArgumentException("Hafriyat ızgarası çok büyük veya adımı koordinat hassasiyetinin altında; grid aralığını artırın.");

        // Bölge bazlı dökümü referansla eşle (aynı TasarimBolgesi örneği → aynı döküm).
        var bolgeDokum = new Dictionary<TasarimBolgesi, HafriyatBolgeSonucu>();

        // Izgara hücre merkezlerini tara.
        for (int col = 0; col < (int)cols; col++)
        {
            double x = minX + (col + 0.5) * GridAraligiM;
            for (int row = 0; row < (int)rows; row++)
            {
                double y = minY + (row + 0.5) * GridAraligiM;
                var ornek = tesviye.Sorgula(x, y);
                if (ornek is null)
                {
                    if (tesviye.PlatformSorgula(x,y) is not null && kotIndeksi.KotSorgula(x,y) is null)
                        sonuc.KapsamDisiHucreSayisi++;
                    continue;
                }

                sonuc.DegerlendirilenHucreSayisi++;
                if(ornek.Tur==TesviyeTur.Sev) sonuc.SevHucreSayisi++;
                double fark = ornek.MevcutKotM - ornek.HedefKotM;

                var dokum = BolgeDokumu(bolgeDokum, sonuc, ornek.Bolge);
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

        if(!double.IsFinite(sonuc.YarmaHacmiM3)||!double.IsFinite(sonuc.DolguHacmiM3))
            throw new ArgumentException("Hafriyat hacmi sayı sınırını aşıyor; kot ve koordinatları kontrol edin.");
        sonuc.Uyarilar = tesviye.Uyarilar.ToList();
        return sonuc;
    }

    private static HafriyatBolgeSonucu BolgeDokumu(
        Dictionary<TasarimBolgesi, HafriyatBolgeSonucu> harita,
        HafriyatSonucu sonuc, TasarimBolgesi bolge)
    {
        if (!harita.TryGetValue(bolge, out var dokum))
        {
            dokum = new HafriyatBolgeSonucu { BolgeId = bolge.Id, BolgeAdi = bolge.Ad, HedefKotM = bolge.HedefKotM };
            harita[bolge] = dokum;
            sonuc.BolgeSonuclari.Add(dokum);
        }
        return dokum;
    }

}
