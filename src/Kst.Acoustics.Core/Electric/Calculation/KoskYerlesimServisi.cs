using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.Core.Electric.Calculation;

/// <summary>
/// Köşk içi fiziksel yerleşim (plan görünümü) — OG hücrelerini genişliklerine göre ön duvar
/// boyunca yan yana dizer, trafoyu ayrı bölmesine yerleştirir ve köşk dış zarfını hesaplar.
/// Saf geometri; hem köşk yerleşim çizimi (canvas) hem DXF köşk bloğu bu sonucu kullanır.
/// Tüm ölçüler mm cinsindendir; X soldan, Y köşk ön cephesinden artar.
/// </summary>
public static class KoskYerlesimServisi
{
    /// <summary>İşletme/erişim payı (mm) — köşk dış duvarı ile ekipman arası.</summary>
    public const double KenarPayiMm = 600;
    /// <summary>Hücre sırası ile trafo bölmesi arasındaki koridor (mm).</summary>
    public const double KoridorMm = 1200;

    public sealed class Eleman
    {
        public string Etiket { get; init; } = "";
        public string Tur { get; init; } = "Hucre"; // "Hucre" / "Trafo"
        public double X { get; init; }
        public double Y { get; init; }
        public double Genislik { get; init; }
        public double Derinlik { get; init; }
    }

    public sealed class Yerlesim
    {
        public double GenislikMm { get; set; }
        public double DerinlikMm { get; set; }
        public List<Eleman> Elemanlar { get; } = new();
    }

    public static Yerlesim Hesapla(OrtaGerilimSistemi og, ElektrikKutuphaneServisi kutuphane)
    {
        var y = new Yerlesim();

        // ── OG hücre sırası (ön duvar boyunca) ──
        double hucreY = KenarPayiMm;
        double x = KenarPayiMm;
        double maxHucreDerinlik = 0;
        foreach (var h in og.Hucreler)
        {
            var tanim = kutuphane.OgHucreleri.FirstOrDefault(t => t.Ad == h.HucreTanimAd);
            double w = tanim?.GenislikMm ?? 500;
            double d = tanim?.DerinlikMm ?? 1000;
            y.Elemanlar.Add(new Eleman
            {
                Etiket = HucreEtiket(h.HucreTipi),
                Tur = "Hucre",
                X = x, Y = hucreY, Genislik = w, Derinlik = d
            });
            x += w;
            if (d > maxHucreDerinlik) maxHucreDerinlik = d;
        }
        double hucreSiraGenislik = x - KenarPayiMm; // toplam hücre genişliği

        // ── Trafo bölmesi (hücre sırasının arkasında) ──
        var trafo = kutuphane.Trafolar.FirstOrDefault(t => t.Ad == og.TrafoTanimAd);
        double trafoW = trafo?.GenislikMm ?? 1500;
        double trafoD = trafo?.DerinlikMm ?? 1200;
        double trafoY = hucreY + maxHucreDerinlik + KoridorMm;
        y.Elemanlar.Add(new Eleman
        {
            Etiket = trafo != null ? $"TRAFO {trafo.GucKVA:0}kVA" : "TRAFO",
            Tur = "Trafo",
            X = KenarPayiMm, Y = trafoY, Genislik = trafoW, Derinlik = trafoD
        });

        // ── Köşk dış zarfı ──
        double icGenislik = System.Math.Max(hucreSiraGenislik, trafoW);
        double hesapGenislik = icGenislik + 2 * KenarPayiMm;
        double hesapDerinlik = trafoY + trafoD + KenarPayiMm;

        y.GenislikMm = og.KoskGenislikMm > 0 ? og.KoskGenislikMm : hesapGenislik;
        y.DerinlikMm = og.KoskDerinlikMm > 0 ? og.KoskDerinlikMm : hesapDerinlik;
        return y;
    }

    private static string HucreEtiket(OgHucreTipi t) => t switch
    {
        OgHucreTipi.Giris => "Giriş",
        OgHucreTipi.Olcu => "Ölçü",
        OgHucreTipi.BaraBaglama => "Bara Bağ.",
        OgHucreTipi.Cikis => "Çıkış",
        OgHucreTipi.TrafoKoruma => "Tr. Koruma",
        _ => t.ToString()
    };
}
