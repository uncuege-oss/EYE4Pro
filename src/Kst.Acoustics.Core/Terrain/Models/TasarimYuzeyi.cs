using System.Collections.Generic;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Terrain.Models;

/// Tasarım bölgesi tipi.
public enum TasarimBolgeTipi
{
    Platform,  // sabit hedef kotlu düzlük (pad)
    Sev,       // eski bağımsız şev yer tutucusu; yeni tasarımda platformun KenarGecisi kullanılır
    Kademe     // teras/kademe (eğimli arazide birden çok platform)
}

/// Platform kenarının mevcut zemine bağlanma biçimi.
public enum TasarimKenarTipi { DikKesim, Sev }

public sealed class TasarimKenarAyari
{
    public int KenarIndex { get; set; }
    public TasarimKenarTipi Tip { get; set; }
    /// Yatay / düşey oran; null ise bölgenin oranı kullanılır.
    public double? YatayDuseyOrani { get; set; }
}

/// Kullanıcının araziyi getirmek istediği hedef geometri. Bir poligon alan + hedef kot.
/// Platform/kademe içi sabit `HedefKotM`; dış kenarlar dik veya mevcut araziye bağlanan şevdir.
public class TasarimBolgesi
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Ad { get; set; } = "";
    public TasarimBolgeTipi Tip { get; set; } = TasarimBolgeTipi.Platform;
    public double HedefKotM { get; set; }

    /// Eski kayıtlar için korunan düşey/yatay alanı. Yeni kenar şevlerinde YatayDuseyOrani kullanılır.
    public double? SevEgimi { get; set; }

    public List<Point2D> Poligon { get; set; } = new();
    public TasarimKenarTipi KenarGecisi { get; set; } = TasarimKenarTipi.DikKesim;
    /// Örn. 1,5: 1 m düşey fark için 1,5 m yatay mesafe (1D:1,5Y).
    public double YatayDuseyOrani { get; set; } = 1.5;
    /// Zemine bağlanma bu mesafeye kadar aranır; açık uçlar uyarı olarak raporlanır.
    public double SevAzamiMesafeM { get; set; } = 50;
    public List<TasarimKenarAyari> KenarAyarlari { get; set; } = new();
    public TasarimKenarTipi KenarTipi(int index) => KenarAyarlari.FirstOrDefault(k => k.KenarIndex == index)?.Tip ?? KenarGecisi;
    public double KenarOrani(int index) => KenarAyarlari.FirstOrDefault(k => k.KenarIndex == index)?.YatayDuseyOrani ?? YatayDuseyOrani;
}

/// Tasarım (hafriyat sonrası hedef) yüzeyi — tasarım bölgeleri kümesi.
/// `HedefKot(x,y)`: noktayı içeren ilk platform bölgesinin hedef kotunu döndürür;
/// hiçbir platform kapsamıyorsa null. Mevcut TIN gerektiren kenar şevleri için AraziTesviyeServisi kullanılır.
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
