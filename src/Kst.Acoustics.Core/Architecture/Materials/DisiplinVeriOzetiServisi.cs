using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Architecture.Model3D;
using Kst.Acoustics.Core.Architecture.Models;

namespace Kst.Acoustics.Core.Architecture.Materials;

/// <summary>
/// M36 malzeme/atama/3D verisinden akustik, mekanik-enerji, elektrik ve metraj disiplinlerinin
/// okuyabilecegi salt-okunur ozetleri uretir. Bos/eksik veride guvenli calisir.
/// </summary>
public static class DisiplinVeriOzetiServisi
{
    public static DisiplinVeriOzeti Hesapla(MimariProje? proje)
    {
        if (proje is null)
            return new DisiplinVeriOzeti();

        var malzemeListesi = MalzemeListesi(proje);
        var malzemeDict = MalzemeSozlugu(malzemeListesi);
        var tipDict = TipSozlugu(proje);
        var geom = GeometriSozlugu(proje);

        var elemanlar = ElemanVerileriniUret(proje, malzemeListesi, malzemeDict, tipDict, geom);

        return new DisiplinVeriOzeti
        {
            Elemanlar = elemanlar,
            Akustik = AkustikOzetiKur(elemanlar),
            Enerji = EnerjiOzetiKur(elemanlar),
            Elektrik = ElektrikOzetiKur(proje, tipDict),
            Metraj = MetrajOzetiKur(elemanlar)
        };
    }

    private static List<DisiplinElemanVerisi> ElemanVerileriniUret(
        MimariProje proje,
        List<MimariMalzeme> malzemeListesi,
        Dictionary<string, MimariMalzeme> malzemeDict,
        Dictionary<string, YapiElemaniTipi> tipDict,
        Dictionary<(MalzemeAtamaHedefTuru, string), GeometriKaydi> geom)
    {
        // (Tur, ElemanId) -> (TipId, acik malzeme Id'leri). Once 3D varsayilan tip Id'leri, sonra acik atamalar.
        var atamaHaritasi = new Dictionary<(MalzemeAtamaHedefTuru, string), (string TipId, List<string> MalzemeIdleri)>();

        var model = proje.PlanTaslagi?.Model3D;
        if (model is not null)
        {
            foreach (var kat in model.Katlar)
            {
                foreach (var duvar in kat.Duvarlar.Where(d => !string.IsNullOrWhiteSpace(d.DuvarTipiId)))
                    atamaHaritasi[(MalzemeAtamaHedefTuru.Duvar, duvar.Id)] = (duvar.DuvarTipiId, []);
                foreach (var doseme in kat.Dosemeler.Where(d => !string.IsNullOrWhiteSpace(d.DosemeTipiId)))
                    atamaHaritasi[(MalzemeAtamaHedefTuru.Doseme, doseme.Id)] = (doseme.DosemeTipiId, []);
                foreach (var tavan in kat.Tavanlar.Where(t => !string.IsNullOrWhiteSpace(t.TavanTipiId)))
                    atamaHaritasi[(MalzemeAtamaHedefTuru.Tavan, tavan.Id)] = (tavan.TavanTipiId, []);
            }
        }

        foreach (var atama in proje.MalzemeAtamalari.Where(a => a.GecerliMi))
        {
            atamaHaritasi[(atama.KaynakElemanTuru, atama.KaynakElemanId)] =
                (atama.YapiElemaniTipiId, atama.MalzemeIdleri ?? []);
        }

        var elemanlar = new List<DisiplinElemanVerisi>();
        foreach (var ((tur, elemanId), (tipId, acikMalzemeIdleri)) in atamaHaritasi)
        {
            if (!tipDict.TryGetValue(tipId, out var tip))
                continue;

            var ozet = tip.PerformansOzetiHesapla(malzemeListesi);
            var katmanMalzemeleri = tip.Katmanlar
                .Where(k => k.MalzemeBaglantisiVar)
                .Select(k => malzemeDict.GetValueOrDefault(k.MalzemeId))
                .Where(m => m is not null)
                .Select(m => m!)
                .ToList();

            var geometri = geom.GetValueOrDefault((tur, elemanId));
            var malzemeAdlari = katmanMalzemeleri
                .Select(m => m.Ad)
                .Concat(acikMalzemeIdleri.Select(id => malzemeDict.GetValueOrDefault(id)?.Ad ?? id))
                .Where(ad => !string.IsNullOrWhiteSpace(ad))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            elemanlar.Add(new DisiplinElemanVerisi
            {
                ElemanTuru = tur,
                ElemanId = elemanId,
                ElemanAdi = geometri?.Ad ?? TurAdi(tur),
                KatNo = geometri?.KatNo ?? 1,
                TipId = tip.Id,
                TipAdi = string.IsNullOrWhiteSpace(tip.Ad) ? tip.Kod : tip.Ad,
                AlanM2 = geometri?.AlanM2 ?? 0,
                HacimM3 = geometri?.HacimM3 ?? 0,
                AkustikRw = ozet.AkustikRw,
                YutuculukAw = ozet.YutuculukAw,
                AkustikNot = katmanMalzemeleri
                    .Select(m => m.Performans.AkustikNot)
                    .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? "",
                UDegeriWm2K = ozet.UDegeriWm2K,
                DisKabukMu = katmanMalzemeleri.Any(m => m.TeknikSinifVar(MalzemeTeknikSinifi.DisKabuk)),
                YanginDayanimDakika = ozet.YanginDayanimDakika,
                YanginTepkiSinifi = ozet.YanginTepkiSinifi,
                IslakHacimUygun = katmanMalzemeleri.Any(m => m.IslakHacimUygun),
                KaymazlikSinifi = katmanMalzemeleri
                    .Select(m => m.Performans.KaymazlikSinifi)
                    .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? "",
                MetrajBirimi = tip.VarsayilanMetrajBirimi,
                KatmanSayisi = tip.KatmanSayisi,
                TahminiAgirlikKgM2 = ozet.TahminiAgirlikKgM2,
                MalzemeAdlari = malzemeAdlari
            });
        }

        return elemanlar
            .OrderBy(e => e.KatNo)
            .ThenBy(e => (int)e.ElemanTuru)
            .ThenBy(e => e.ElemanAdi, StringComparer.CurrentCulture)
            .ToList();
    }

    private static AkustikDisiplinOzeti AkustikOzetiKur(List<DisiplinElemanVerisi> elemanlar)
    {
        var akustikSatirlar = elemanlar.Where(e => e.AkustikVeriVar).ToList();
        return new AkustikDisiplinOzeti
        {
            ElemanSayisi = elemanlar.Count,
            AkustikVeriliElemanSayisi = akustikSatirlar.Count,
            EnYuksekRw = elemanlar.Count == 0 ? 0 : elemanlar.Max(e => e.AkustikRw),
            EnYuksekYutuculukAw = elemanlar.Count == 0 ? 0 : elemanlar.Max(e => e.YutuculukAw),
            Satirlar = akustikSatirlar
        };
    }

    private static EnerjiDisiplinOzeti EnerjiOzetiKur(List<DisiplinElemanVerisi> elemanlar)
    {
        var enerjiSatirlar = elemanlar.Where(e => e.EnerjiVeriVar || e.DisKabukMu).ToList();
        var uDegerliler = elemanlar.Where(e => e.EnerjiVeriVar).ToList();
        return new EnerjiDisiplinOzeti
        {
            DisKabukElemanSayisi = elemanlar.Count(e => e.DisKabukMu),
            UDegerliElemanSayisi = uDegerliler.Count,
            EnDusukUDegeri = uDegerliler.Count == 0 ? 0 : uDegerliler.Min(e => e.UDegeriWm2K),
            EnYuksekUDegeri = uDegerliler.Count == 0 ? 0 : uDegerliler.Max(e => e.UDegeriWm2K),
            Satirlar = enerjiSatirlar
        };
    }

    private static ElektrikDisiplinOzeti ElektrikOzetiKur(MimariProje proje, Dictionary<string, YapiElemaniTipi> tipDict)
    {
        var mahaller = new List<ElektrikMahalYuzeyOzeti>();
        var model = proje.PlanTaslagi?.Model3D;
        if (model is null)
            return new ElektrikDisiplinOzeti();

        foreach (var kat in model.Katlar.OrderBy(k => k.KatNo))
        {
            foreach (var mahal in kat.Mahaller.OrderBy(m => m.Ad, StringComparer.CurrentCulture))
            {
                var profil = MahalFinisajOneriServisi.Oner(mahal.Tip, KullanimSinifi.Tanimsiz, proje.FinisajProfilleri);
                var zemin = TipAdiBul(tipDict, profil.ZeminTipiId);
                var duvar = TipAdiBul(tipDict, profil.DuvarTipiId);
                var tavan = TipAdiBul(tipDict, profil.TavanTipiId);
                var islak = MahalTipiYardimcilari.IslakHacimMi(mahal.Tip, KullanimSinifi.Tanimsiz);

                mahaller.Add(new ElektrikMahalYuzeyOzeti
                {
                    MahalId = mahal.Id,
                    MahalAdi = string.IsNullOrWhiteSpace(mahal.Ad) ? "Mahal" : mahal.Ad,
                    KatNo = mahal.KatNo,
                    ZeminTipiAdi = zemin,
                    DuvarTipiAdi = duvar,
                    TavanTipiAdi = tavan,
                    IslakHacim = islak,
                    KaplamaNotu = KaplamaNotuKur(zemin, duvar, tavan, islak)
                });
            }
        }

        return new ElektrikDisiplinOzeti
        {
            MahalSayisi = mahaller.Count,
            Mahaller = mahaller
        };
    }

    private static MetrajDisiplinOzeti MetrajOzetiKur(List<DisiplinElemanVerisi> elemanlar)
    {
        var turOzetleri = elemanlar
            .GroupBy(e => e.ElemanTuru)
            .OrderBy(g => (int)g.Key)
            .Select(g => new MetrajTuruOzeti
            {
                ElemanTuru = g.Key,
                Birim = g.First().MetrajBirimi,
                ElemanSayisi = g.Count(),
                ToplamAlanM2 = g.Sum(e => e.AlanM2),
                ToplamHacimM3 = g.Sum(e => e.HacimM3),
                ToplamAgirlikKg = g.Sum(e => e.ToplamAgirlikKg)
            })
            .ToList();

        return new MetrajDisiplinOzeti
        {
            ElemanSayisi = elemanlar.Count,
            ToplamAlanM2 = elemanlar.Sum(e => e.AlanM2),
            ToplamHacimM3 = elemanlar.Sum(e => e.HacimM3),
            ToplamAgirlikKg = elemanlar.Sum(e => e.ToplamAgirlikKg),
            TurOzetleri = turOzetleri,
            Satirlar = elemanlar
        };
    }

    private static string KaplamaNotuKur(string zemin, string duvar, string tavan, bool islak)
    {
        var parcalar = new List<string>();
        if (!string.IsNullOrWhiteSpace(zemin)) parcalar.Add($"Zemin: {zemin}");
        if (!string.IsNullOrWhiteSpace(duvar)) parcalar.Add($"Duvar: {duvar}");
        if (!string.IsNullOrWhiteSpace(tavan)) parcalar.Add($"Tavan: {tavan}");
        if (islak) parcalar.Add("Islak hacim");
        return parcalar.Count == 0 ? "Finisaj önerisi yok" : string.Join(" | ", parcalar);
    }

    private static List<MimariMalzeme> MalzemeListesi(MimariProje proje)
    {
        var dict = new Dictionary<string, MimariMalzeme>(StringComparer.OrdinalIgnoreCase);
        foreach (var malzeme in VarsayilanMimariMalzemeler.Tum)
            if (!string.IsNullOrWhiteSpace(malzeme.Id))
                dict[malzeme.Id] = malzeme;
        foreach (var malzeme in proje.Malzemeler)
            if (!string.IsNullOrWhiteSpace(malzeme.Id))
                dict[malzeme.Id] = malzeme;

        return dict.Values.ToList();
    }

    private static Dictionary<string, MimariMalzeme> MalzemeSozlugu(IEnumerable<MimariMalzeme> malzemeler)
    {
        var dict = new Dictionary<string, MimariMalzeme>(StringComparer.OrdinalIgnoreCase);
        foreach (var malzeme in malzemeler)
        {
            if (!string.IsNullOrWhiteSpace(malzeme.Id))
                dict[malzeme.Id] = malzeme;
            if (!string.IsNullOrWhiteSpace(malzeme.Kod))
                dict[malzeme.Kod] = malzeme;
        }

        return dict;
    }

    private static Dictionary<string, YapiElemaniTipi> TipSozlugu(MimariProje proje)
    {
        var dict = new Dictionary<string, YapiElemaniTipi>(StringComparer.OrdinalIgnoreCase);
        foreach (var tip in VarsayilanYapiElemaniTipleri.Tum)
            dict[tip.Id] = tip;
        foreach (var tip in proje.YapiElemaniTipleri)
            if (!string.IsNullOrWhiteSpace(tip.Id))
                dict[tip.Id] = tip;

        return dict;
    }

    private static string TipAdiBul(Dictionary<string, YapiElemaniTipi> tipDict, string? tipId)
    {
        if (string.IsNullOrWhiteSpace(tipId) || !tipDict.TryGetValue(tipId, out var tip))
            return "";

        return string.IsNullOrWhiteSpace(tip.Ad) ? tip.Kod : tip.Ad;
    }

    private static Dictionary<(MalzemeAtamaHedefTuru, string), GeometriKaydi> GeometriSozlugu(MimariProje proje)
    {
        var dict = new Dictionary<(MalzemeAtamaHedefTuru, string), GeometriKaydi>();
        var model = proje.PlanTaslagi?.Model3D;
        if (model is null)
            return dict;

        foreach (var kat in model.Katlar)
        {
            foreach (var mahal in kat.Mahaller)
                dict[(MalzemeAtamaHedefTuru.Mahal, mahal.Id)] =
                    new GeometriKaydi(string.IsNullOrWhiteSpace(mahal.Ad) ? "Mahal" : mahal.Ad, kat.KatNo, mahal.AlanM2, mahal.HacimM3);

            foreach (var doseme in kat.Dosemeler)
                dict[(MalzemeAtamaHedefTuru.Doseme, doseme.Id)] =
                    new GeometriKaydi("Döşeme", kat.KatNo, doseme.AlanM2, doseme.HacimM3);

            foreach (var tavan in kat.Tavanlar)
                dict[(MalzemeAtamaHedefTuru.Tavan, tavan.Id)] =
                    new GeometriKaydi("Tavan", kat.KatNo, tavan.AlanM2, tavan.HacimM3);

            foreach (var duvar in kat.Duvarlar)
                dict[(MalzemeAtamaHedefTuru.Duvar, duvar.Id)] =
                    new GeometriKaydi("Duvar", kat.KatNo, duvar.AlanM2, duvar.HacimM3);

            foreach (var aciklik in kat.Acikliklar)
            {
                var tur = aciklik.Tip == Aciklik3DTipi.Pencere
                    ? MalzemeAtamaHedefTuru.Pencere
                    : MalzemeAtamaHedefTuru.Kapi;
                var ad = aciklik.Tip == Aciklik3DTipi.Pencere ? "Pencere" : "Kapı";
                dict[(tur, aciklik.Id)] = new GeometriKaydi(ad, aciklik.KatNo, aciklik.AlanM2, 0);
            }
        }

        return dict;
    }

    private static string TurAdi(MalzemeAtamaHedefTuru tur) => tur switch
    {
        MalzemeAtamaHedefTuru.Mahal => "Mahal",
        MalzemeAtamaHedefTuru.Duvar => "Duvar",
        MalzemeAtamaHedefTuru.Doseme => "Döşeme",
        MalzemeAtamaHedefTuru.Tavan => "Tavan",
        MalzemeAtamaHedefTuru.Kapi => "Kapı",
        MalzemeAtamaHedefTuru.Pencere => "Pencere",
        MalzemeAtamaHedefTuru.Cekirdek => "Çekirdek",
        MalzemeAtamaHedefTuru.Saft => "Şaft",
        _ => "Eleman"
    };

    private sealed record GeometriKaydi(string Ad, int KatNo, double AlanM2, double HacimM3);
}
