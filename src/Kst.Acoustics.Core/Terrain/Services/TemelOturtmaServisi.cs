using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Terrain.Models;

namespace Kst.Acoustics.Core.Terrain.Services;

/// M61 — Bina temeli kota oturtma motoru. Salt-okunur arazi (M60 `.e4arazi`,
/// `AraziTuketim`) üzerine bina/blok ayak izlerini oturtur; ±0.00 datum, bodrum/giriş/
/// çıkış/yangın merdiveni kotları, kazı derinliği ve kademe sırasını üretir.
/// Arazi veya bina geometrisini DEĞİŞTİRMEZ.
public class TemelOturtmaServisi
{
    /// Tüm blokları oturtur ve kademe sırasını (artan zemin kotu) atar.
    public TemelOturtmaModeli Oturt(AraziTuketim arazi, IEnumerable<BinaAyakIzi> bloklar)
    {
        var model = new TemelOturtmaModeli
        {
            AraziAdi = arazi.AraziAdi,
            AraziRevizyon = arazi.Revizyon
        };

        foreach (var blok in bloklar)
            model.Bloklar.Add(OturtBlok(arazi, blok));

        // Kademe sırası: en düşük ortalama zeminden başlayarak.
        int kademe = 0;
        foreach (var b in model.Bloklar.OrderBy(b => b.OrtalamaZeminKotuM))
            b.KademeNo = kademe++;

        return model;
    }

    /// Tek bloğu oturtur. `sifirKotOverride` verilirse ±0.00 ona sabitlenir;
    /// yoksa ayak izi altındaki ortalama tabii zemin kotuna oturtulur.
    public BlokOturma OturtBlok(AraziTuketim arazi, BinaAyakIzi blok, double? sifirKotOverride = null)
    {
        var sonuc = new BlokOturma { BlokId = blok.BlokId };

        var (ortalama, kapsamDisi, ornekSayisi) = ZeminKotuOrnekle(arazi, blok.Poligon);
        if (ornekSayisi == 0)
        {
            sonuc.Uyarilar.Add("Ayak izi tamamen arazi dışında; oturtma yapılamadı.");
            return sonuc;
        }
        if (kapsamDisi > 0)
            sonuc.Uyarilar.Add($"Ayak izinin {kapsamDisi} örnek noktası arazi dışında; ortalama mevcut noktalardan alındı.");

        sonuc.OrtalamaZeminKotuM = ortalama;
        sonuc.SifirKotM = sifirKotOverride ?? ortalama;

        double katY = blok.KatYuksekligiM;
        sonuc.BodrumTabanKotuM = sonuc.SifirKotM - blok.BodrumKatSayisi * katY;
        sonuc.CatiKotuM = sonuc.SifirKotM + blok.ZeminUstuKatSayisi * katY;
        sonuc.KaziDerinligiM = Math.Max(0.0, sonuc.OrtalamaZeminKotuM - sonuc.BodrumTabanKotuM);
        sonuc.SerbestYukseklikM = sonuc.SifirKotM - sonuc.OrtalamaZeminKotuM;

        sonuc.Kotlar.Add(new KotTanimi(KotTipi.Giris, sonuc.SifirKotM));
        sonuc.Kotlar.Add(new KotTanimi(KotTipi.Cikis, sonuc.SifirKotM));
        sonuc.Kotlar.Add(new KotTanimi(KotTipi.YanginMerdiveni, sonuc.SifirKotM));
        sonuc.Kotlar.Add(new KotTanimi(KotTipi.Cati, sonuc.CatiKotuM));
        if (blok.BodrumKatSayisi > 0)
            sonuc.Kotlar.Add(new KotTanimi(KotTipi.Bodrum, sonuc.BodrumTabanKotuM));

        // Mantık uyarıları.
        if (blok.BodrumKatSayisi > 0 && sonuc.BodrumTabanKotuM >= sonuc.OrtalamaZeminKotuM)
            sonuc.Uyarilar.Add("Bodrum taban kotu tabii zeminin üstünde; kazı oluşmaz (±0.00 fazla yüksek).");
        if (sonuc.SerbestYukseklikM < -0.5)
            sonuc.Uyarilar.Add($"±0.00 tabii zeminin {(-sonuc.SerbestYukseklikM):0.##} m altında; bina gömülü kalıyor.");

        return sonuc;
    }

    // Ayak izi köşeleri + merkezinde arazi kotunu örnekleyip ortalamayı döndürür.
    private static (double Ortalama, int KapsamDisi, int OrnekSayisi) ZeminKotuOrnekle(
        AraziTuketim arazi, IReadOnlyList<Point2D> poligon)
    {
        double toplam = 0;
        int adet = 0, kapsamDisi = 0;

        void Ornekle(double x, double y)
        {
            var z = arazi.KotSorgula(x, y);
            if (z.HasValue) { toplam += z.Value; adet++; }
            else kapsamDisi++;
        }

        foreach (var p in poligon) Ornekle(p.X, p.Y);

        if (poligon.Count > 0)
        {
            double cx = poligon.Average(p => p.X);
            double cy = poligon.Average(p => p.Y);
            Ornekle(cx, cy);
        }

        return adet == 0 ? (0, kapsamDisi, 0) : (toplam / adet, kapsamDisi, adet);
    }
}
