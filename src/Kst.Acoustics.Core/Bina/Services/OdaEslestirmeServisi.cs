using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Bina.Services;

/// Bir kat yeniden taranınca (DXF yeniden yükle, boşluktan yeniden tara) yeni odaları
/// eski odalarla gerçek poligon örtüşmesi üzerinden eşleştirir — Id kararlılığı
/// (codex50 §3.5). Bölünme/birleşme gibi belirsiz değişimlerde eski Id otomatik
/// devredilmez; tüketici mutabakatı bunu silinen+yeni oda olarak gösterebilir.
public static class OdaEslestirmeServisi
{
    /// Yeni odaları eskilerle eşleştirir: eşleşen yeni oda eski Id'yi ve
    /// YukseklikOverrideM'i ve bilinmeyen ileri-uyum alanlarını devralır.
    /// Döner: eşleşmeyen (silinmiş sayılan) eski Id'ler.
    public static List<string> Eslestir(IReadOnlyList<BinaOda> yeniOdalar, IReadOnlyList<BinaOda> eskiOdalar)
    {
        var eslesenEskiIdler = new HashSet<string>();
        if (eskiOdalar.Count == 0) return new List<string>();

        const double minIou = 0.60;
        const double belirsizlikFarki = 0.10;
        var adaylar = new List<(BinaOda Yeni, BinaOda Eski, double Iou)>();
        foreach (var yeni in yeniOdalar.Where(o => o.Poligon.Count >= 3))
        {
            double yeniAlan = Math.Abs(ClipperAdaptor.IsaretliAlan(yeni.Poligon));
            if (yeniAlan <= 1e-8) continue;
            foreach (var eski in eskiOdalar.Where(o => o.Poligon.Count >= 3))
            {
                double eskiAlan = Math.Abs(ClipperAdaptor.IsaretliAlan(eski.Poligon));
                if (eskiAlan <= 1e-8) continue;
                double kesisim = ClipperAdaptor.NetAlan(
                    ClipperAdaptor.Kesisim(new[] { yeni.Poligon }, new[] { eski.Poligon }));
                if (kesisim <= 1e-8) continue;
                double birlesim = yeniAlan + eskiAlan - kesisim;
                if (birlesim > 1e-8) adaylar.Add((yeni, eski, kesisim / birlesim));
            }
        }

        // Yalnız iki yönde de açık ara en iyi olan yüksek-IoU çifti kabul et.
        // Bu, parser sırasına bağlı greedy Id devrini ve split/merge'de keyfi yarıyı önler.
        foreach (var aday in adaylar.OrderByDescending(a => a.Iou))
        {
            if (aday.Iou < minIou || eslesenEskiIdler.Contains(aday.Eski.Id)) continue;
            var yeniSirasi = adaylar.Where(a => ReferenceEquals(a.Yeni, aday.Yeni))
                .OrderByDescending(a => a.Iou).ToList();
            var eskiSirasi = adaylar.Where(a => ReferenceEquals(a.Eski, aday.Eski))
                .OrderByDescending(a => a.Iou).ToList();
            if (!ReferenceEquals(yeniSirasi[0].Eski, aday.Eski) ||
                !ReferenceEquals(eskiSirasi[0].Yeni, aday.Yeni)) continue;
            if (yeniSirasi.Count > 1 && yeniSirasi[0].Iou - yeniSirasi[1].Iou < belirsizlikFarki) continue;
            if (eskiSirasi.Count > 1 && eskiSirasi[0].Iou - eskiSirasi[1].Iou < belirsizlikFarki) continue;

            aday.Yeni.Id = aday.Eski.Id;
            aday.Yeni.YukseklikOverrideM = aday.Eski.YukseklikOverrideM;
            aday.Yeni.EkAlanlar = aday.Eski.EkAlanlar?.ToDictionary(
                kv => kv.Key, kv => kv.Value.Clone(), StringComparer.Ordinal);
            eslesenEskiIdler.Add(aday.Eski.Id);
        }

        return eskiOdalar
            .Where(e => !eslesenEskiIdler.Contains(e.Id))
            .Select(e => e.Id)
            .ToList();
    }
}
