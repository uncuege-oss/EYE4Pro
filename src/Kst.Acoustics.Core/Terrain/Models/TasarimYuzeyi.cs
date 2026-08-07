using System.Collections.Generic;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Terrain.Models;

/// Tasarım bölgesi tipi.
public enum TasarimBolgeTipi
{
    Platform,  // sabit hedef kotlu düzlük (pad)
    Sev,       // eğimli bağlantı (şev) — ileri faz: SevEgimi ile araziye iner
    Kademe     // teras/kademe (eğimli arazide birden çok platform)
}

/// Kullanıcının araziyi getirmek istediği hedef geometri. Bir poligon alan + hedef kot.
/// Platform: poligon içi sabit `HedefKotM`. Şev/Kademe için `SevEgimi` ileri fazda kullanılır.
public class TasarimBolgesi
{
    public string Ad { get; set; } = "";
    public TasarimBolgeTipi Tip { get; set; } = TasarimBolgeTipi.Platform;
    public double HedefKotM { get; set; }

    /// Şev eğim oranı (düşey/yatay), örn. 0.5 = 1/2. Platformda kullanılmaz.
    public double? SevEgimi { get; set; }

    public List<Point2D> Poligon { get; set; } = new();
}

/// Tasarım (hafriyat sonrası hedef) yüzeyi — tasarım bölgeleri kümesi.
/// `HedefKot(x,y)`: noktayı içeren ilk platform bölgesinin hedef kotunu döndürür;
/// hiçbir bölge kapsamıyorsa null (o noktada hafriyat yok = mevcut kot korunur).
public class TasarimYuzeyi
{
    public List<TasarimBolgesi> Bolgeler { get; set; } = new();

    public double? HedefKot(double x, double y) => HedefBolge(x, y)?.HedefKotM;

    /// (x,y) noktasını içeren ilk platform/kademe bölgesini döndürür; yoksa null.
    public TasarimBolgesi? HedefBolge(double x, double y)
    {
        var p = new Point2D(x, y);
        foreach (var b in Bolgeler)
        {
            if (b.Tip != TasarimBolgeTipi.Platform && b.Tip != TasarimBolgeTipi.Kademe) continue;
            if (b.Poligon.Count >= 3 && PoligonYardimci.IsPointInside(p, b.Poligon))
                return b;
        }
        return null;
    }
}
