using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Architecture.Exporting;
using Kst.Acoustics.Core.Architecture.Models;
using Kst.Acoustics.Core.Bina.Models;

namespace Kst.Acoustics.Core.Architecture.Coordination;

/// <summary>
/// İki mimari tasarım (veya iki `BinaGeometri`) arasındaki revizyon farkını üretir.
/// Mahaller kararlı oda Id'siyle, katlar KatNo ile eşleştirilir.
/// </summary>
public static class RevizyonFarkiServisi
{
    private const double AlanToleransiM2 = 1e-6;

    /// <summary>İki mimari projeyi karşılaştırır (her ikisi `BinaGeometriAdapteri` ile geometriye çevrilir).</summary>
    public static RevizyonFarki Karsilastir(MimariProje? eski, MimariProje? yeni)
        => Karsilastir(BinaGeometriAdapteri.Donustur(eski), BinaGeometriAdapteri.Donustur(yeni));

    /// <summary>İki bina geometrisini karşılaştırır.</summary>
    public static RevizyonFarki Karsilastir(BinaGeometri? eski, BinaGeometri? yeni)
    {
        var eskiOdalar = Duzlestir(eski);
        var yeniOdalar = Duzlestir(yeni);

        var eklenen = new List<RevizyonMahalGirisi>();
        var silinen = new List<RevizyonMahalGirisi>();
        var degisen = new List<RevizyonMahalDegisikligi>();

        foreach (var (id, yeniOda) in yeniOdalar)
        {
            if (!eskiOdalar.TryGetValue(id, out var eskiOda))
            {
                eklenen.Add(Giris(id, yeniOda));
                continue;
            }

            var adDegisti = !string.Equals(eskiOda.Ad, yeniOda.Ad, StringComparison.Ordinal);
            var katDegisti = eskiOda.KatNo != yeniOda.KatNo;
            var alanDegisti = Math.Abs(eskiOda.AlanM2 - yeniOda.AlanM2) > AlanToleransiM2;
            if (adDegisti || katDegisti || alanDegisti)
            {
                degisen.Add(new RevizyonMahalDegisikligi
                {
                    OdaId = id,
                    EskiAd = eskiOda.Ad,
                    YeniAd = yeniOda.Ad,
                    EskiKatNo = eskiOda.KatNo,
                    YeniKatNo = yeniOda.KatNo,
                    EskiAlanM2 = eskiOda.AlanM2,
                    YeniAlanM2 = yeniOda.AlanM2
                });
            }
        }

        foreach (var (id, eskiOda) in eskiOdalar)
        {
            if (!yeniOdalar.ContainsKey(id))
                silinen.Add(Giris(id, eskiOda));
        }

        var eskiKatlar = KatNolari(eski);
        var yeniKatlar = KatNolari(yeni);

        return new RevizyonFarki
        {
            EklenenMahaller = eklenen.OrderBy(m => m.KatNo).ThenBy(m => m.Ad, StringComparer.CurrentCulture).ToList(),
            SilinenMahaller = silinen.OrderBy(m => m.KatNo).ThenBy(m => m.Ad, StringComparer.CurrentCulture).ToList(),
            DegisenMahaller = degisen.OrderBy(m => m.YeniKatNo).ThenBy(m => m.YeniAd, StringComparer.CurrentCulture).ToList(),
            EklenenKatlar = yeniKatlar.Except(eskiKatlar).OrderBy(k => k).ToList(),
            SilinenKatlar = eskiKatlar.Except(yeniKatlar).OrderBy(k => k).ToList(),
            ToplamAlanDeltaM2 = yeniOdalar.Values.Sum(o => o.AlanM2) - eskiOdalar.Values.Sum(o => o.AlanM2)
        };
    }

    private static RevizyonMahalGirisi Giris(string id, OdaKaydi oda)
        => new() { OdaId = id, Ad = oda.Ad, KatNo = oda.KatNo, AlanM2 = oda.AlanM2 };

    private static Dictionary<string, OdaKaydi> Duzlestir(BinaGeometri? geo)
    {
        var dict = new Dictionary<string, OdaKaydi>(StringComparer.Ordinal);
        if (geo is null)
            return dict;

        foreach (var kat in geo.Katlar)
        {
            foreach (var oda in kat.Odalar)
            {
                if (!string.IsNullOrWhiteSpace(oda.Id))
                    dict[oda.Id] = new OdaKaydi(oda.Ad, kat.KatNo, oda.AlanM2);
            }
        }

        return dict;
    }

    private static HashSet<int> KatNolari(BinaGeometri? geo)
        => geo is null ? [] : geo.Katlar.Select(k => k.KatNo).ToHashSet();

    private sealed record OdaKaydi(string Ad, int KatNo, double AlanM2);
}
