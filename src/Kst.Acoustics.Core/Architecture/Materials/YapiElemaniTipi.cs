using System;
using System.Collections.Generic;
using System.Linq;

namespace Kst.Acoustics.Core.Architecture.Materials;

/// <summary>
/// Duvar, doseme, tavan, kapi ve pencere icin katmanli yapi elemani tipi.
/// </summary>
public class YapiElemaniTipi
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Kod { get; set; } = "";
    public string Ad { get; set; } = "";
    public YapiElemaniSinifi Sinif { get; set; } = YapiElemaniSinifi.Tanimsiz;
    public List<YapiElemaniKatmani> Katmanlar { get; set; } = new();
    public MetrajBirimi VarsayilanMetrajBirimi { get; set; } = MetrajBirimi.Metrekare;
    public string KaynakKodu { get; set; } = "M36";
    public string Notlar { get; set; } = "";

    public int KatmanSayisi => Katmanlar.Count;
    public double ToplamKalinlikM => Katmanlar.Sum(k => k.KullanilanKalinlikM);
    public bool KatmanliMi => Katmanlar.Count > 0;
    public bool KalinlikliMi => ToplamKalinlikM > 0;
    public IReadOnlyList<string> MalzemeIdleri => Katmanlar
        .Where(k => k.MalzemeBaglantisiVar)
        .Select(k => k.MalzemeId)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    public bool HedefTuruIleUyumluMu(MalzemeAtamaHedefTuru hedefTuru)
        => (Sinif, hedefTuru) switch
        {
            (YapiElemaniSinifi.Duvar, MalzemeAtamaHedefTuru.Duvar) => true,
            (YapiElemaniSinifi.Doseme, MalzemeAtamaHedefTuru.Doseme) => true,
            (YapiElemaniSinifi.Tavan, MalzemeAtamaHedefTuru.Tavan) => true,
            (YapiElemaniSinifi.Kapi, MalzemeAtamaHedefTuru.Kapi) => true,
            (YapiElemaniSinifi.Pencere, MalzemeAtamaHedefTuru.Pencere) => true,
            (YapiElemaniSinifi.Cekirdek, MalzemeAtamaHedefTuru.Cekirdek) => true,
            (YapiElemaniSinifi.Saft, MalzemeAtamaHedefTuru.Saft) => true,
            (YapiElemaniSinifi.MahalFinisaji, MalzemeAtamaHedefTuru.Mahal) => true,
            _ => false
        };

    public YapiElemaniPerformansOzeti PerformansOzetiHesapla(IEnumerable<MimariMalzeme> malzemeler)
    {
        var malzemeListesi = malzemeler?.ToList() ?? [];
        var ozet = new YapiElemaniPerformansOzeti
        {
            YapiElemaniTipiId = Id,
            ToplamKalinlikM = ToplamKalinlikM,
            KatmanSayisi = Katmanlar.Count
        };

        foreach (var katman in Katmanlar.Where(k => k.MalzemeBaglantisiVar))
        {
            var malzeme = MalzemeBul(malzemeListesi, katman.MalzemeId);
            if (malzeme is null)
                continue;

            ozet.MalzemeSayisi++;
            ozet.YanginDayanimDakika = Math.Max(ozet.YanginDayanimDakika, malzeme.Performans.YanginDayanimDakika);
            ozet.AkustikRw = Math.Max(ozet.AkustikRw, malzeme.Performans.SesAzaltimRw);
            ozet.YutuculukAw = Math.Max(ozet.YutuculukAw, malzeme.Performans.YutuculukAw);

            if (!string.IsNullOrWhiteSpace(malzeme.Performans.YanginTepkiSinifi) &&
                string.IsNullOrWhiteSpace(ozet.YanginTepkiSinifi))
            {
                ozet.YanginTepkiSinifi = malzeme.Performans.YanginTepkiSinifi;
            }

            if (malzeme.Performans.UDegeriWm2K > 0 &&
                (ozet.UDegeriWm2K <= 0 || malzeme.Performans.UDegeriWm2K < ozet.UDegeriWm2K))
            {
                ozet.UDegeriWm2K = malzeme.Performans.UDegeriWm2K;
            }

            ozet.TahminiAgirlikKgM2 += KatmanAgirligiKgM2(katman, malzeme);
        }

        return ozet;
    }

    private static MimariMalzeme? MalzemeBul(IEnumerable<MimariMalzeme> malzemeler, string malzemeId)
        => malzemeler.FirstOrDefault(m =>
            string.Equals(m.Id, malzemeId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(m.Kod, malzemeId, StringComparison.OrdinalIgnoreCase));

    private static double KatmanAgirligiKgM2(YapiElemaniKatmani katman, MimariMalzeme malzeme)
    {
        if (malzeme.Metraj.YogunlukKgM3 > 0 && katman.KullanilanKalinlikM > 0)
            return malzeme.Metraj.YogunlukKgM3 * katman.KullanilanKalinlikM;

        return malzeme.Metraj.KullanilanBirimAgirlikKg;
    }
}
