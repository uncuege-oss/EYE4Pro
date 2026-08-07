using System.Collections.Generic;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Terrain.Models;

/// M61 — bina/blok ayak izi (temel oturtma girdisi). Tam bina geometrisi yerine
/// oturtma için gereken sade veri: plan poligonu + kat bilgisi. İleride M50 `.e4bina`
/// veya mimari M35 kütlesinden türetilebilir.
public class BinaAyakIzi
{
    public string BlokId { get; set; } = "";
    public List<Point2D> Poligon { get; set; } = new();
    public double KatYuksekligiM { get; set; } = 3.0;
    public int ZeminUstuKatSayisi { get; set; } = 1;
    public int BodrumKatSayisi { get; set; }
}

/// Kot tanımı tipi.
public enum KotTipi
{
    Giris,           // ±0.00 (bina giriş kotu)
    Bodrum,          // en alt bodrum taban kotu
    Cikis,           // arka/yan çıkış kotu
    YanginMerdiveni, // yangın merdiveni çıkış kotu
    Cati             // çatı/üst kot
}

/// Belirli bir kot tanımı (referans).
public class KotTanimi
{
    public KotTipi Tip { get; set; }
    public double KotM { get; set; }

    public KotTanimi() { }
    public KotTanimi(KotTipi tip, double kotM) { Tip = tip; KotM = kotM; }
}

/// Bir bloğun araziye oturtulmuş sonucu.
public class BlokOturma
{
    public string BlokId { get; set; } = "";

    /// Kademe sırası (en düşük zeminden başlayarak 0,1,2…) — eğimli arazide kademeli bina.
    public int KademeNo { get; set; }

    /// Ayak izi altındaki ortalama tabii zemin kotu (m).
    public double OrtalamaZeminKotuM { get; set; }

    /// ±0.00 datum (bina giriş kotu).
    public double SifirKotM { get; set; }

    /// En alt bodrum taban kotu.
    public double BodrumTabanKotuM { get; set; }

    /// Çatı (en üst kat üst) kotu.
    public double CatiKotuM { get; set; }

    /// Bodrum tabanına inmek için gereken kazı derinliği (m, ≥0).
    public double KaziDerinligiM { get; set; }

    /// ±0.00 ile ortalama zemin farkı (pozitif = zemin üstünde/serbest yükseklik).
    public double SerbestYukseklikM { get; set; }

    public List<KotTanimi> Kotlar { get; set; } = new();
    public List<string> Uyarilar { get; set; } = new();
}

/// M61 temel oturtma modelinin köküdür. Salt-okunur arazi + ayak izlerinden üretilir.
public class TemelOturtmaModeli
{
    public int SemaSurum { get; set; } = 1;
    public string AraziAdi { get; set; } = "";
    public int AraziRevizyon { get; set; }
    public List<BlokOturma> Bloklar { get; set; } = new();
}
