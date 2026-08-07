using System.Collections.Generic;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Landscape.Models;

/// M70 park tasarımının kök modeli. `.e4p70` olarak serileştirilir; sert zemin
/// yerleşimi `.e4park.json` paylaşılan artifact'ı olarak dışa verilir (codex70 §5).
/// Arazi (M60) ve bina (M50) altlıkları salt-okunur tüketilir; bu geometriler
/// burada DEĞİŞTİRİLMEZ (codex70 §3, maincodex §4.5/§4.7/§4.8).
public class ParkModeli : IParkGeometri
{
    /// Şema versiyonu — migrasyon tetikleyicisi (M12/M50/M60 deseni).
    public int SemaSurum { get; set; } = 1;

    /// Her kayıtta artan revizyon damgası (tüketici fark kontrolü için).
    public int GeometriRevizyon { get; set; } = 1;

    public string ParkAdi { get; set; } = "";
    public string? DosyaYolu { get; set; }

    /// Tüketilen altlıkların yolları + gördüğümüz son revizyon (eskime kontrolü).
    public string? AraziDosyaYolu { get; set; }
    public int? AraziRevizyon { get; set; }
    public string? BinaDosyaYolu { get; set; }
    public int? BinaRevizyon { get; set; }

    /// Park dış sınırı (kapalı poligon). Arazi sınırından veya DXF'ten gelir.
    public ParkSiniri Sinir { get; set; } = new();

    /// Arazi içindeki binalar + yapı yaklaşma (setback) dışlamaları.
    public List<BinaDislama> BinaDislamalari { get; set; } = new();

    /// Sert zemin öğeleri (yol, döşeme, oturma, bank, süs havuzu).
    public List<ParkOgesi> Ogeler { get; set; } = new();

    /// Sonraki Id için sayaç (kararlı Id üretimi — codex70 §4).
    public int SonrakiOgeId { get; set; } = 1;

    // IParkGeometri salt-okunur görünümleri
    IReadOnlyList<Point2D> IParkGeometri.Sinir => Sinir.Poligon;
    IReadOnlyList<IParkOgesi> IParkGeometri.Ogeler => Ogeler;
}

/// Park dış sınırı.
public class ParkSiniri
{
    public List<Point2D> Poligon { get; set; } = new();
}

/// Arazi içinde bir bina + yapı yaklaşma mesafesi. Etkin dışlama poligonu
/// (ayak izi + setback offset) türetilmiş veridir; serileştirilmeyip F2'de
/// Clipper2 ile yeniden üretilir (codex70 §6).
public class BinaDislama
{
    public string BinaAdi { get; set; } = "";
    /// Bina ayak izi (kapalı poligon — `.e4bina`'dan salt-okunur).
    public List<Point2D> AyakIzi { get; set; } = new();
    /// Yapı yaklaşma (setback) mesafesi — ayak izi bu kadar dışa şişirilir.
    public double SetbackM { get; set; }
}

/// Park alan/oran özeti (F4'te hesaplanır; türetilmiş — serileştirme opsiyonel).
public class AlanOzeti
{
    public double ToplamParkM2 { get; set; }
    public double KullanilabilirM2 { get; set; }   // park − bina − setback
    public double SertZeminM2 { get; set; }
    public double YesilAlanM2 { get; set; }
    public double YesilOran { get; set; }          // 0..1
    public double SertZeminOran { get; set; }       // 0..1
    public List<string> Uyarilar { get; set; } = new();
}
