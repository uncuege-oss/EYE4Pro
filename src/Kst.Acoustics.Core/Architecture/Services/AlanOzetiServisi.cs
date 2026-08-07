using Kst.Acoustics.Core.Architecture.Models;

namespace Kst.Acoustics.Core.Architecture.Services;

/// <summary>
/// Ihtiyac programindan alan ve hacim ozetlerini hesaplar.
/// </summary>
public static class AlanOzetiServisi
{
    public static AlanOzeti Hesapla(IhtiyacProgrami program)
    {
        ArgumentNullException.ThrowIfNull(program);

        var netAlan = program.HedefNetAlanM2;
        var sirkulasyonAlan = program.OnerilenSirkulasyonAlanM2;
        var teknikAlan = program.OnerilenTeknikAlanM2;
        var brutAlan = program.OnerilenBrutAlanM2;
        var hedefFark = program.HedefToplamAlanM2 > 0 ? brutAlan - program.HedefToplamAlanM2 : 0;

        return new AlanOzeti
        {
            NetAlanM2 = netAlan,
            SirkulasyonAlanM2 = sirkulasyonAlan,
            TeknikAlanM2 = teknikAlan,
            BrutAlanM2 = brutAlan,
            HedefToplamAlanM2 = program.HedefToplamAlanM2,
            HedefFarkM2 = hedefFark,
            HedefFarkOrani = program.HedefToplamAlanM2 > 0 ? hedefFark / program.HedefToplamAlanM2 : 0,
            NetHacimM3 = program.Mahaller.Sum(m => m.ToplamHedefHacimM3),
            MahalSayisi = program.Mahaller.Count,
            ToplamMahalAdedi = program.Mahaller.Sum(m => m.Adet),
            KullanimSinifiOzetleri = KullanimSinifiOzetleriniHesapla(program, netAlan)
        };
    }

    private static List<KullanimSinifiAlanOzeti> KullanimSinifiOzetleriniHesapla(
        IhtiyacProgrami program,
        double netAlan)
        => program.Mahaller
            .GroupBy(m => m.KullanimSinifi)
            .OrderBy(g => (int)g.Key)
            .Select(g =>
            {
                var alan = g.Sum(m => m.ToplamHedefAlanM2);
                return new KullanimSinifiAlanOzeti
                {
                    KullanimSinifi = g.Key,
                    MahalSayisi = g.Count(),
                    ToplamAdet = g.Sum(m => m.Adet),
                    AlanM2 = alan,
                    HacimM3 = g.Sum(m => m.ToplamHedefHacimM3),
                    AlanOrani = netAlan > 0 ? alan / netAlan : 0
                };
            })
            .ToList();
}
