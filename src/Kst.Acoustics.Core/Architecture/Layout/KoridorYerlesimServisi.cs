using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Architecture.Models;

namespace Kst.Acoustics.Core.Architecture.Layout;

/// <summary>
/// M32 konsept plan icin kaba koridor akslarini ve blok baglantilarini uretir.
/// </summary>
public static class KoridorYerlesimServisi
{
    public static void KoridorlariUret(PlanTaslagi? plan)
    {
        if (plan is null)
            return;

        foreach (var kat in plan.Katlar.OrderBy(k => k.KatNo))
        {
            AnaKoridorUret(kat, plan.Parametreler);
        }
    }

    public static void AnaKoridorUret(KatTaslagi? kat, YerlesimParametreleri? parametreler = null)
    {
        if (kat is null)
            return;

        kat.Koridorlar.Clear();

        var yerlesmisBloklar = kat.Bloklar
            .Where(b => b is not null)
            .Where(b => b.YerlesmisMi)
            .ToList();
        if (yerlesmisBloklar.Count == 0)
            return;

        var baglanacakBloklar = kat.Bloklar
            .Where(KoridoraBaglanmaliMi)
            .ToList();
        if (baglanacakBloklar.Count == 0)
            return;

        parametreler ??= new YerlesimParametreleri();
        var bosluk = Math.Max(0.1, parametreler.BloklarArasiBoslukM);
        var koridorGenisligi = Math.Max(0.9, parametreler.VarsayilanKoridorGenisligiM);

        var minimumX = yerlesmisBloklar.Min(b => b.X);
        var maksimumX = yerlesmisBloklar.Max(b => b.X + b.GenislikM);
        var maksimumY = yerlesmisBloklar.Max(b => b.Y + b.DerinlikM);
        if (maksimumX - minimumX < koridorGenisligi)
            maksimumX = minimumX + koridorGenisligi;

        var koridorMerkezY = maksimumY + bosluk + koridorGenisligi / 2.0;
        var koridor = new KoridorTaslagi
        {
            Ad = $"{kat.Ad} Ana Koridor",
            BaslangicX = minimumX,
            BaslangicY = koridorMerkezY,
            BitisX = maksimumX,
            BitisY = koridorMerkezY,
            GenislikM = koridorGenisligi,
            BagliBlokIdleri = baglanacakBloklar
                .Select(b => b.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

        kat.Koridorlar.Add(koridor);
        SiniriKoridoraGoreGenislet(kat, koridor, bosluk);
    }

    public static IReadOnlyList<MahalBlogu> BaglanmasiGerekenBloklariBul(KatTaslagi? kat)
        => kat?.Bloklar
            .Where(KoridoraBaglanmaliMi)
            .OrderBy(b => b.KatNo)
            .ThenBy(b => b.Ad, StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

    public static IReadOnlyList<MahalBlogu> BaglantisizBloklariBul(KatTaslagi? kat)
    {
        if (kat is null)
            return [];

        var bagliBlokIdleri = new HashSet<string>(
            kat.Koridorlar
                .Where(k => k is not null)
                .SelectMany(k => k.BagliBlokIdleri ?? [])
                .Where(id => !string.IsNullOrWhiteSpace(id)),
            StringComparer.OrdinalIgnoreCase);

        return kat.Bloklar
            .Where(KoridoraBaglanmaliMi)
            .Where(b => !bagliBlokIdleri.Contains(b.Id))
            .OrderBy(b => b.Ad, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool KoridoraBagliMi(KatTaslagi? kat, MahalBlogu? blok)
    {
        if (kat is null || blok is null || string.IsNullOrWhiteSpace(blok.Id))
            return false;

        return kat.Koridorlar.Any(k =>
            k.BagliBlokIdleri?.Any(id => string.Equals(id, blok.Id, StringComparison.OrdinalIgnoreCase)) == true);
    }

    public static bool KoridoraBaglanmaliMi(MahalBlogu? blok)
    {
        if (blok is null)
            return false;

        return blok.KullanimSinifi is not
            (KullanimSinifi.Sirkulasyon or
             KullanimSinifi.DuseySirkulasyon or
             KullanimSinifi.AcikAlan or
             KullanimSinifi.HesapDisi);
    }

    private static void SiniriKoridoraGoreGenislet(KatTaslagi kat, KoridorTaslagi koridor, double bosluk)
    {
        var mevcutSag = kat.Sinir.X + Math.Max(0, kat.Sinir.GenislikM);
        var mevcutAlt = kat.Sinir.Y + Math.Max(0, kat.Sinir.DerinlikM);
        var koridorSag = Math.Max(koridor.BaslangicX, koridor.BitisX) + bosluk;
        var koridorAlt = Math.Max(koridor.BaslangicY, koridor.BitisY) + koridor.GenislikM / 2.0 + bosluk;

        kat.Sinir.GenislikM = Math.Max(kat.Sinir.GenislikM, koridorSag - kat.Sinir.X);
        kat.Sinir.DerinlikM = Math.Max(kat.Sinir.DerinlikM, koridorAlt - kat.Sinir.Y);

        if (mevcutSag <= 0)
            kat.Sinir.GenislikM = Math.Max(kat.Sinir.GenislikM, koridorSag);

        if (mevcutAlt <= 0)
            kat.Sinir.DerinlikM = Math.Max(kat.Sinir.DerinlikM, koridorAlt);
    }
}
