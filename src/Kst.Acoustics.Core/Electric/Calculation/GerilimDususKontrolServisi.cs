using System.Linq;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.Core.Electric.Calculation;

/// <summary>
/// Kümülatif (toplam) gerilim düşümü kontrolü — TS HD 60364-5-52.
/// Her segment (trafo→ana pano, pano→pano, busbar, son linye) kendi
/// <see cref="ElektrikProje.IzinVerilenGerilimDususYuzde"/> sınırında boyutlanır; ancak kaynaktan
/// en uçtaki yüke kadar birikimli düşüm <see cref="ElektrikProje.IzinVerilenToplamGerilimDususYuzde"/>
/// (tipik %5) sınırını aşabilir. Bu servis zinciri yukarıdan aşağı çözer, her pano ve linyeye
/// kümülatif ΔU'yu yazar ve toplam sınırı aşanları işaretler.
/// <para>
/// Tüm segment ΔU'ları (kablo + busbar) hesaplandıktan SONRA çağrılmalıdır
/// (PanoKabloHesapla / LinyeKabloHesapla / BusbarHesapla / OrtaGerilimHesapla sonrası).
/// </para>
/// </summary>
public static class GerilimDususKontrolServisi
{
    private const double Tolerans = 1e-6;

    public static void Hesapla(ElektrikProje proje)
    {
        double toplamSinir = proje.IzinVerilenToplamGerilimDususYuzde > 0
            ? proje.IzinVerilenToplamGerilimDususYuzde
            : 5.0;

        // Üstten alta çöz: üst panonun kümülatif değeri bu pano hesaplanmadan önce hazır olur.
        foreach (var pano in proje.TumPanolar.OrderBy(DerinlikBul))
        {
            double ustKumulatif = pano.UstPano?.KumulatifGerilimDususYuzde ?? 0;
            pano.KumulatifGerilimDususYuzde = ustKumulatif + SegmentDususYuzde(pano, proje);
            pano.GerilimDususuAsiyor = pano.KumulatifGerilimDususYuzde > toplamSinir + Tolerans;

            foreach (var linye in pano.Linyeler)
            {
                linye.KumulatifGerilimDususYuzde =
                    pano.KumulatifGerilimDususYuzde + linye.KabloGerilimDususYuzde;
                linye.GerilimDususuAsiyor = linye.KumulatifGerilimDususYuzde > toplamSinir + Tolerans;
            }
        }
    }

    /// <summary>Bir panonun üst panosundan (veya kaynaktan) kendi barasına kadar olan
    /// segment gerilim düşümü (%). Busbar beslemede busbar segmenti + çıkış kutusu kablosu toplanır.</summary>
    private static double SegmentDususYuzde(Pano pano, ElektrikProje proje)
    {
        // Kök pano: trafo→ana pano bağlantısı (OG varsa KabloGerilimDususYuzde'ye yazılmıştır),
        // trafosuz sistemde kaynak barası referanstır → 0.
        if (pano.UstPano == null)
            return proje.OrtaGerilim != null ? pano.KabloGerilimDususYuzde : 0;

        // Busbar besleme: busbar başlangıcından çıkış kutusuna kadar ΔU + kutu→pano kablosu ΔU.
        if (pano.BaglantiTipi == BaglantiTipi.Busbar && !string.IsNullOrEmpty(pano.BaglandigiBusbarId))
        {
            var busbar = proje.TumBusbarlar.FirstOrDefault(b => b.Id == pano.BaglandigiBusbarId);
            var kutu = busbar?.CikisKutulari.FirstOrDefault(k =>
                string.Equals(k.BagliPanoAd, pano.Ad, System.StringComparison.OrdinalIgnoreCase));
            double busbarDusum = kutu?.GerilimDususYuzde ?? 0;
            return busbarDusum + pano.KabloGerilimDususYuzde;
        }

        // Doğrudan kablo besleme.
        return pano.KabloGerilimDususYuzde;
    }

    private static int DerinlikBul(Pano pano)
    {
        int d = 0;
        var p = pano;
        while (p.UstPano != null) { d++; p = p.UstPano; }
        return d;
    }
}
