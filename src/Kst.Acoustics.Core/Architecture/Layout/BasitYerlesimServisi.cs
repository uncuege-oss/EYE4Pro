using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Architecture.Models;

namespace Kst.Acoustics.Core.Architecture.Layout;

/// <summary>
/// M32 icin ilk deterministik ve kaba dikdortgen blok yerlesimini uretir.
/// </summary>
public static class BasitYerlesimServisi
{
    public static void Yerlestir(PlanTaslagi? plan)
    {
        if (plan is null)
            return;

        foreach (var kat in plan.Katlar.OrderBy(k => k.KatNo))
        {
            Yerlestir(kat, plan.Parametreler);
        }
    }

    public static void Yerlestir(KatTaslagi? kat, YerlesimParametreleri? parametreler = null)
    {
        if (kat is null)
            return;

        parametreler ??= new YerlesimParametreleri();
        var bosluk = Math.Max(0.1, parametreler.BloklarArasiBoslukM);
        var toplamAlan = Math.Max(1, kat.BlokHedefAlanM2);
        var satirGenisligi = Math.Max(12, Math.Sqrt(toplamAlan) * 1.8);

        var x = bosluk;
        var y = bosluk;
        var satirDerinligi = 0.0;
        var maksimumX = 0.0;

        foreach (var blok in kat.Bloklar.OrderByDescending(b => b.HedefAlanM2).ThenBy(b => b.Ad, StringComparer.OrdinalIgnoreCase))
        {
            Boyutlandir(blok);

            if (x > bosluk && x + blok.GenislikM > satirGenisligi)
            {
                x = bosluk;
                y += satirDerinligi + bosluk;
                satirDerinligi = 0;
            }

            if (!blok.Kilitli)
            {
                blok.X = x;
                blok.Y = y;
                blok.KatNo = Math.Max(1, kat.KatNo);
            }

            maksimumX = Math.Max(maksimumX, blok.X + blok.GenislikM + bosluk);
            satirDerinligi = Math.Max(satirDerinligi, blok.DerinlikM);
            x += blok.GenislikM + bosluk;
        }

        kat.HedefAlanM2 = kat.HedefAlanM2 > 0 ? kat.HedefAlanM2 : kat.BlokHedefAlanM2;
        kat.Sinir = new PlanSiniri
        {
            X = 0,
            Y = 0,
            GenislikM = Math.Max(satirGenisligi + bosluk, maksimumX),
            DerinlikM = kat.Bloklar.Count == 0 ? 0 : y + satirDerinligi + bosluk
        };
    }

    public static void Boyutlandir(MahalBlogu? blok)
    {
        if (blok is null || blok.Kilitli)
            return;

        var hedefAlan = Math.Max(0, blok.HedefAlanM2);
        if (hedefAlan <= 0)
        {
            blok.GenislikM = 0;
            blok.DerinlikM = 0;
            blok.HedefEnM = 0;
            blok.HedefBoyM = 0;
            return;
        }

        var oran = EnBoyOrani(blok);
        var genislik = Math.Sqrt(hedefAlan * oran);
        var derinlik = hedefAlan / genislik;

        blok.GenislikM = genislik;
        blok.DerinlikM = derinlik;
        blok.HedefEnM = genislik;
        blok.HedefBoyM = derinlik;
    }

    public static bool AlanToleransiIcindeMi(MahalBlogu? blok, double toleransOrani)
    {
        if (blok is null)
            return false;

        if (blok.HedefAlanM2 <= 0)
            return blok.AlanM2 <= 0;

        return Math.Abs(blok.AlanFarkOrani) <= Math.Max(0, toleransOrani);
    }

    public static bool CakisiyorMu(MahalBlogu? a, MahalBlogu? b, double toleransM = 1e-9)
    {
        if (a is null || b is null || ReferenceEquals(a, b))
            return false;

        if (!a.YerlesmisMi || !b.YerlesmisMi)
            return false;

        return a.X < b.X + b.GenislikM - toleransM &&
               a.X + a.GenislikM > b.X + toleransM &&
               a.Y < b.Y + b.DerinlikM - toleransM &&
               a.Y + a.DerinlikM > b.Y + toleransM;
    }

    public static IReadOnlyList<(MahalBlogu A, MahalBlogu B)> CakisanBlokCiftleri(KatTaslagi? kat)
    {
        if (kat is null || kat.Bloklar.Count < 2)
            return [];

        var cakisanlar = new List<(MahalBlogu A, MahalBlogu B)>();
        for (var i = 0; i < kat.Bloklar.Count; i++)
        {
            for (var j = i + 1; j < kat.Bloklar.Count; j++)
            {
                if (CakisiyorMu(kat.Bloklar[i], kat.Bloklar[j]))
                    cakisanlar.Add((kat.Bloklar[i], kat.Bloklar[j]));
            }
        }

        return cakisanlar;
    }

    private static double EnBoyOrani(MahalBlogu blok)
        => blok.MahalTipi switch
        {
            MahalTipi.Koridor or MahalTipi.Hol or MahalTipi.Antre => 4.0,
            MahalTipi.KonferansSalonu or MahalTipi.Salon or MahalTipi.AcikOfis => 1.6,
            MahalTipi.Otopark or MahalTipi.Depo => 2.0,
            MahalTipi.Saft or MahalTipi.Asansor or MahalTipi.Wc or MahalTipi.Banyo => 1.0,
            _ => blok.KullanimSinifi switch
            {
                KullanimSinifi.Sirkulasyon => 4.0,
                KullanimSinifi.DuseySirkulasyon => 1.0,
                KullanimSinifi.TeknikHacim => 1.1,
                _ => 1.25
            }
        };
}
