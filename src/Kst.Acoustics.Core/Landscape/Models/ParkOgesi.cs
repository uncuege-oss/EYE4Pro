using System.Collections.Generic;
using System.Text.Json.Serialization;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Landscape.Models;

/// Park sert zemin öğesinin soyut tabanı. Her öğenin kararlı bir `Id`'si vardır
/// (revizyon/mutabakat için — M50/M60 deseni). `ZeminKotuM` arazi (M60) yüklüyse
/// `AraziTuketim.KotSorgula` ile doldurulur; yoksa null (geriye-uyumlu).
/// `.e4p70`/`.e4park.json` serileştirmesi `$tip` ayraç alanıyla polimorfiktir.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$tip")]
[JsonDerivedType(typeof(YuruyusYolu), "yol")]
[JsonDerivedType(typeof(DosemeAlani), "doseme")]
[JsonDerivedType(typeof(OturmaAlani), "oturma")]
[JsonDerivedType(typeof(Bank), "bank")]
[JsonDerivedType(typeof(SusHavuzu), "havuz")]
public abstract class ParkOgesi : IParkOgesi
{
    /// Kararlı kimlik — yeniden yükleme/mutabakatta öğe eşlemesi için korunur.
    public int Id { get; set; }

    /// Tür ayracı `$tip` ile zaten serileştirilir; ayrıca yazmaya gerek yok.
    [JsonIgnore]
    public abstract ParkOgeTipi Tip { get; }

    /// Kullanıcı etiketi (örn. "Ana meydan", "Doğu yürüyüş yolu").
    public string Ad { get; set; } = "";

    /// Araziden okunan zemin kotu (m). Arazi altlığı yoksa null.
    public double? ZeminKotuM { get; set; }
}

/// Yürüyüş yolu / patika — güzergâh çizgisi + genişlik.
public class YuruyusYolu : ParkOgesi
{
    public override ParkOgeTipi Tip => ParkOgeTipi.YuruyusYolu;

    /// Yol orta-ekseni (poliçizgi). Şerit, `GenislikM`/2 ile iki yana ötelenir.
    public List<Point2D> Guzergah { get; set; } = new();
    public double GenislikM { get; set; } = 1.5;
    public KaplamaTipi Kaplama { get; set; } = KaplamaTipi.BetonPlak;
}

/// Döşeme / meydan alanı — kapalı poligon.
public class DosemeAlani : ParkOgesi
{
    public override ParkOgeTipi Tip => ParkOgeTipi.DosemeAlani;

    public List<Point2D> Poligon { get; set; } = new();
    public KaplamaTipi Kaplama { get; set; } = KaplamaTipi.DogalTas;
}

/// Oturma alanı — kapalı poligon (banklar bu alana veya yola yerleştirilebilir).
public class OturmaAlani : ParkOgesi
{
    public override ParkOgeTipi Tip => ParkOgeTipi.OturmaAlani;

    public List<Point2D> Poligon { get; set; } = new();
    public KaplamaTipi Kaplama { get; set; } = KaplamaTipi.Ahsap;
}

/// Tekil bank — konum + yön.
public class Bank : ParkOgesi
{
    public override ParkOgeTipi Tip => ParkOgeTipi.Bank;

    public Point2D Konum { get; set; }
    /// Bankın baktığı yön (derece, 0 = +X ekseni).
    public double YonDeg { get; set; }
    public string Model { get; set; } = "";
}

/// Süs havuzu / su öğesi — kapalı poligon + opsiyonel derinlik.
public class SusHavuzu : ParkOgesi
{
    public override ParkOgeTipi Tip => ParkOgeTipi.SusHavuzu;

    public List<Point2D> Poligon { get; set; } = new();
    public double? DerinlikM { get; set; }
    public KaplamaTipi KenarKaplama { get; set; } = KaplamaTipi.Granit;
}
