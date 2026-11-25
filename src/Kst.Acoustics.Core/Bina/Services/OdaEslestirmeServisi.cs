using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Bina.Services;

/// Bir kat yeniden taranınca (DXF yeniden yükle, boşluktan yeniden tara) yeni odaları
/// eski odalarla merkez-örtüşme üzerinden eşleştirir — Id kararlılığı (codex50 §3.5).
public static class OdaEslestirmeServisi
{
    /// Yeni odaları eskilerle eşleştirir: eşleşen yeni oda eski Id'yi ve
    /// YukseklikOverrideM'i devralır. Döner: eşleşmeyen (silinmiş sayılan) eski Id'ler.
    public static List<string> Eslestir(IReadOnlyList<BinaOda> yeniOdalar, IReadOnlyList<BinaOda> eskiOdalar)
    {
        var eslesenEskiIdler = new HashSet<string>();
        if (eskiOdalar.Count == 0) return new List<string>();

        foreach (var yeni in yeniOdalar)
        {
            if (yeni.Poligon.Count < 3) continue;

            var eslesen = eskiOdalar.FirstOrDefault(eski =>
                !eslesenEskiIdler.Contains(eski.Id) &&
                PoligonYardimci.IsPointInside(yeni.Merkez, eski.Poligon));

            eslesen ??= eskiOdalar.FirstOrDefault(eski =>
                !eslesenEskiIdler.Contains(eski.Id) &&
                eski.Poligon.Count >= 3 &&
                PoligonYardimci.IsPointInside(eski.Merkez, yeni.Poligon));

            if (eslesen is null) continue;

            yeni.Id = eslesen.Id;
            yeni.YukseklikOverrideM = eslesen.YukseklikOverrideM;
            yeni.TabanKotuOverrideM = eslesen.TabanKotuOverrideM;
            eslesenEskiIdler.Add(eslesen.Id);
        }

        return eskiOdalar
            .Where(e => !eslesenEskiIdler.Contains(e.Id))
            .Select(e => e.Id)
            .ToList();
    }
}
