using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Architecture.Layout;
using Kst.Acoustics.Core.Architecture.Materials;
using Kst.Acoustics.Core.Architecture.Model3D;
using Kst.Acoustics.Core.Architecture.Models;
using Kst.Acoustics.Core.Architecture.Rules;
using Kst.Acoustics.Core.Architecture.Services;

namespace Kst.Acoustics.Core.Architecture.Reports;

/// <summary>
/// M37 icin `MimariProje` verisinden yapisal rapor modellerini (tablolar/cizelgeler) uretir.
/// Bicim (HTML/CSV) katmani bu modelleri render eder; servis yalniz okur, geometriyi degistirmez.
/// </summary>
public static class MimariRaporUretimServisi
{
    /// <summary>
    /// Tum M37 rapor tablolarini tek nesnede uretir (HTML/CSV builder'larinin girdisi).
    /// </summary>
    public static MimariRaporu TumRaporuOlustur(MimariProje? proje)
        => new()
        {
            ProjeAdi = proje?.ProjeAdi ?? "",
            MahalListesi = MahalListesiOlustur(proje),
            AlanTablosu = AlanTablosuOlustur(proje),
            KapiPencereCizelgesi = KapiPencereCizelgesiOlustur(proje),
            FinisajCizelgesi = FinisajCizelgesiOlustur(proje),
            OnKontrol = OnKontrolRaporuOlustur(proje)
        };

    /// <summary>
    /// Mahal listesi tablosunu uretir. 3D model mahalleri varsa yerlesmis instance'lardan
    /// (gercek alan/hacim), yoksa ihtiyac programi gereksinimlerinden (hedef alan/hacim) uretir.
    /// </summary>
    public static MahalListesiTablosu MahalListesiOlustur(MimariProje? proje)
    {
        if (proje is null)
            return new MahalListesiTablosu();

        var model = proje.PlanTaslagi?.Model3D;
        var modelMahalleri = model?.Katlar.SelectMany(k => k.Mahaller).ToList() ?? [];

        var satirlar = modelMahalleri.Count > 0
            ? ModeldenSatirlar(model!, proje.PlanTaslagi)
            : ProgramdanSatirlar(proje.IhtiyacProgrami);

        return new MahalListesiTablosu { Satirlar = satirlar };
    }

    /// <summary>
    /// Alan tablosunu uretir. Ihtiyac programi yoksa bos tablo doner; aksi halde
    /// <see cref="AlanOzetiServisi"/> ciktisini raporlanabilir tabloya cevirir.
    /// </summary>
    public static AlanTablosu AlanTablosuOlustur(MimariProje? proje)
    {
        var program = proje?.IhtiyacProgrami;
        if (program is null || program.Mahaller.Count == 0)
            return new AlanTablosu();

        var ozet = AlanOzetiServisi.Hesapla(program);
        return new AlanTablosu
        {
            NetAlanM2 = ozet.NetAlanM2,
            SirkulasyonAlanM2 = ozet.SirkulasyonAlanM2,
            TeknikAlanM2 = ozet.TeknikAlanM2,
            BrutAlanM2 = ozet.BrutAlanM2,
            HedefToplamAlanM2 = ozet.HedefToplamAlanM2,
            HedefFarkM2 = ozet.HedefFarkM2,
            HedefFarkOrani = ozet.HedefFarkOrani,
            NetHacimM3 = ozet.NetHacimM3,
            MahalSayisi = ozet.MahalSayisi,
            ToplamMahalAdedi = ozet.ToplamMahalAdedi,
            KullanimKirilimi = ozet.KullanimSinifiOzetleri
                .Select(k => new AlanKirilimSatiri
                {
                    KullanimSinifi = k.KullanimSinifi,
                    MahalSayisi = k.MahalSayisi,
                    ToplamAdet = k.ToplamAdet,
                    AlanM2 = k.AlanM2,
                    HacimM3 = k.HacimM3,
                    AlanOrani = k.AlanOrani
                })
                .ToList()
        };
    }

    /// <summary>
    /// Kapi-pencere cizelgesi v1'i uretir. 3D model acikliklarini tip + olcu + yapi elemani tipine
    /// gore gruplayip pozisyon (K1, P1...) ve adet uretir; atamadan tip/malzeme adlarini cozer.
    /// </summary>
    public static KapiPencereCizelgesi KapiPencereCizelgesiOlustur(MimariProje? proje)
    {
        var model = proje?.PlanTaslagi?.Model3D;
        var acikliklar = model?.Katlar.SelectMany(k => k.Acikliklar).Where(a => a is not null).ToList() ?? [];
        if (acikliklar.Count == 0)
            return new KapiPencereCizelgesi();

        var tipDict = TipSozlugu(proje!);
        var malzemeDict = MalzemeSozlugu(proje!);
        var atamaDict = proje!.MalzemeAtamalari
            .Where(a => a.KaynakElemanTuru is MalzemeAtamaHedefTuru.Kapi or MalzemeAtamaHedefTuru.Pencere)
            .GroupBy(a => a.KaynakElemanId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var gruplar = acikliklar
            .Select(a =>
            {
                var atama = atamaDict.GetValueOrDefault(a.Id);
                var tipId = atama?.YapiElemaniTipiId ?? "";
                return new
                {
                    Aciklik = a,
                    PencereMi = a.Tip == Aciklik3DTipi.Pencere,
                    Genislik = Math.Round(Math.Max(0, a.GenislikM), 3),
                    Yukseklik = Math.Round(Math.Max(0, a.YukseklikM), 3),
                    TipId = tipId,
                    Atama = atama
                };
            })
            .GroupBy(x => (x.PencereMi, x.Genislik, x.Yukseklik, x.TipId))
            .OrderBy(g => g.Key.PencereMi)
            .ThenByDescending(g => g.Key.Genislik)
            .ThenByDescending(g => g.Key.Yukseklik)
            .ThenBy(g => g.Key.TipId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var satirlar = new List<KapiPencereSatiri>();
        var kapiSayaci = 0;
        var pencereSayaci = 0;
        foreach (var grup in gruplar)
        {
            var ilk = grup.First();
            var poz = grup.Key.PencereMi ? $"P{++pencereSayaci}" : $"K{++kapiSayaci}";
            var tip = string.IsNullOrWhiteSpace(grup.Key.TipId) ? null : tipDict.GetValueOrDefault(grup.Key.TipId);

            satirlar.Add(new KapiPencereSatiri
            {
                Poz = poz,
                PencereMi = grup.Key.PencereMi,
                GenislikM = grup.Key.Genislik,
                YukseklikM = grup.Key.Yukseklik,
                Adet = grup.Count(),
                YapiElemaniTipiAdi = tip is null ? "Atanmadı" : (string.IsNullOrWhiteSpace(tip.Ad) ? tip.Kod : tip.Ad),
                MalzemeAdlari = MalzemeAdlariCozumle(tip, ilk.Atama, malzemeDict),
                BirimAlanM2 = ilk.Aciklik.AlanM2
            });
        }

        return new KapiPencereCizelgesi { Satirlar = satirlar };
    }

    /// <summary>
    /// Finisaj cizelgesini uretir. Mahal listesi satirlarini M36 finisaj onerisiyle eslestirip
    /// her mahal icin zemin/duvar/tavan tip adlarini ve uyari notunu cozer.
    /// </summary>
    public static FinisajCizelgesi FinisajCizelgesiOlustur(MimariProje? proje)
    {
        var mahalListesi = MahalListesiOlustur(proje);
        if (!mahalListesi.VeriVar || proje is null)
            return new FinisajCizelgesi();

        var tipDict = TipSozlugu(proje);
        var satirlar = new List<FinisajCizelgesiSatiri>();

        foreach (var mahal in mahalListesi.Satirlar)
        {
            var profil = MahalFinisajOneriServisi.Oner(mahal.MahalTipi, mahal.KullanimSinifi, proje.FinisajProfilleri);
            var islak = MahalTipiYardimcilari.IslakHacimMi(mahal.MahalTipi, mahal.KullanimSinifi);
            var notlar = profil.UyariNotlari.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();

            satirlar.Add(new FinisajCizelgesiSatiri
            {
                KatNo = mahal.KatNo,
                MahalNo = mahal.MahalNo,
                MahalAdi = mahal.Ad,
                MahalTipi = mahal.MahalTipi,
                ZeminTipiAdi = TipAdiCozumle(tipDict, profil.ZeminTipiId),
                DuvarTipiAdi = TipAdiCozumle(tipDict, profil.DuvarTipiId),
                TavanTipiAdi = TipAdiCozumle(tipDict, profil.TavanTipiId),
                IslakHacim = islak,
                Not = notlar.Count > 0 ? string.Join("; ", notlar) : (islak ? "Islak hacim" : "")
            });
        }

        return new FinisajCizelgesi { Satirlar = satirlar };
    }

    /// <summary>
    /// On kontrol raporunu uretir. M34 ilk veri + plan kontrolleri ve M36 finisaj uyarilarini
    /// (tek giris noktasi <see cref="YonetmelikOnKontrolServisi.IlkVeriKontrolleriniCalistir(MimariProje?)"/>)
    /// seviye/kaynak/kural kodlu rapor satirlarina cevirir.
    /// </summary>
    public static OnKontrolRaporu OnKontrolRaporuOlustur(MimariProje? proje)
    {
        if (proje is null)
            return new OnKontrolRaporu();

        var sonuclar = YonetmelikOnKontrolServisi.IlkVeriKontrolleriniCalistir(proje);
        var satirlar = sonuclar
            .Select(s => new OnKontrolSatiri
            {
                Seviye = s.Seviye,
                KuralKodu = s.KuralKodu,
                KaynakKodu = s.KaynakKodu,
                Baslik = s.Baslik,
                Mesaj = s.Mesaj,
                CozumOnerisi = s.CozumOnerisi,
                EtkilenenEleman = EtkilenenElemanMetni(s),
                VeriEksigi = s.VeriEksigi ?? ""
            })
            .ToList();

        return new OnKontrolRaporu { Satirlar = satirlar };
    }

    private static string EtkilenenElemanMetni(KuralSonucu sonuc)
    {
        var parcalar = new List<string>();
        if (!string.IsNullOrWhiteSpace(sonuc.EtkilenenMahalId)) parcalar.Add($"Mahal: {sonuc.EtkilenenMahalId}");
        if (!string.IsNullOrWhiteSpace(sonuc.EtkilenenKatId)) parcalar.Add($"Kat: {sonuc.EtkilenenKatId}");
        if (!string.IsNullOrWhiteSpace(sonuc.EtkilenenElemanId)) parcalar.Add($"Eleman: {sonuc.EtkilenenElemanId}");
        return string.Join(" | ", parcalar);
    }

    private static string TipAdiCozumle(Dictionary<string, YapiElemaniTipi> tipDict, string? tipId)
    {
        if (string.IsNullOrWhiteSpace(tipId) || !tipDict.TryGetValue(tipId, out var tip))
            return "";

        return string.IsNullOrWhiteSpace(tip.Ad) ? tip.Kod : tip.Ad;
    }

    private static List<string> MalzemeAdlariCozumle(
        YapiElemaniTipi? tip,
        MalzemeAtamasi? atama,
        Dictionary<string, MimariMalzeme> malzemeDict)
    {
        var idler = new List<string>();
        if (atama?.MalzemeIdleri is { Count: > 0 } acikIdler)
            idler.AddRange(acikIdler);
        else if (tip is not null)
            idler.AddRange(tip.MalzemeIdleri);

        return idler
            .Select(id => malzemeDict.GetValueOrDefault(id)?.Ad ?? id)
            .Where(ad => !string.IsNullOrWhiteSpace(ad))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToList();
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

    private static Dictionary<string, MimariMalzeme> MalzemeSozlugu(MimariProje proje)
    {
        var dict = new Dictionary<string, MimariMalzeme>(StringComparer.OrdinalIgnoreCase);

        void Ekle(MimariMalzeme malzeme)
        {
            if (!string.IsNullOrWhiteSpace(malzeme.Id))
                dict[malzeme.Id] = malzeme;
            if (!string.IsNullOrWhiteSpace(malzeme.Kod))
                dict[malzeme.Kod] = malzeme;
        }

        foreach (var malzeme in VarsayilanMimariMalzemeler.Tum)
            Ekle(malzeme);
        foreach (var malzeme in proje.Malzemeler)
            Ekle(malzeme);

        return dict;
    }

    private static List<MahalListesiSatiri> ModeldenSatirlar(
        Model3D.Bim3DModel model,
        PlanTaslagi? plan)
    {
        var blokKullanim = BlokKullanimSozlugu(plan);
        var satirlar = new List<MahalListesiSatiri>();

        foreach (var kat in model.Katlar.OrderBy(k => k.KatNo))
        {
            var sira = 0;
            foreach (var mahal in kat.Mahaller.OrderBy(m => m.Ad, StringComparer.CurrentCulture))
            {
                sira++;
                var kullanim = blokKullanim.GetValueOrDefault(mahal.KaynakMahalBloguId, KullanimSinifi.Tanimsiz);
                satirlar.Add(new MahalListesiSatiri
                {
                    KatNo = kat.KatNo,
                    MahalNo = MahalNo(kat.KatNo, sira),
                    Ad = string.IsNullOrWhiteSpace(mahal.Ad) ? "Mahal" : mahal.Ad,
                    MahalTipi = mahal.Tip,
                    KullanimSinifi = kullanim,
                    Adet = 1,
                    AlanM2 = mahal.AlanM2,
                    HacimM3 = mahal.HacimM3
                });
            }
        }

        return satirlar;
    }

    private static List<MahalListesiSatiri> ProgramdanSatirlar(IhtiyacProgrami? program)
    {
        var mahaller = program?.Mahaller;
        if (mahaller is null || mahaller.Count == 0)
            return [];

        var satirlar = new List<MahalListesiSatiri>();
        var sira = 0;
        foreach (var mahal in mahaller)
        {
            if (mahal is null)
                continue;

            sira++;
            satirlar.Add(new MahalListesiSatiri
            {
                KatNo = 0,
                MahalNo = sira.ToString("000"),
                Ad = string.IsNullOrWhiteSpace(mahal.Ad) ? mahal.MahalTipi.ToString() : mahal.Ad,
                MahalTipi = mahal.MahalTipi,
                KullanimSinifi = mahal.KullanimSinifi,
                Adet = Math.Max(0, mahal.Adet),
                AlanM2 = mahal.ToplamHedefAlanM2,
                HacimM3 = mahal.ToplamHedefHacimM3
            });
        }

        return satirlar;
    }

    private static Dictionary<string, KullanimSinifi> BlokKullanimSozlugu(PlanTaslagi? plan)
    {
        var dict = new Dictionary<string, KullanimSinifi>(StringComparer.OrdinalIgnoreCase);
        if (plan is null)
            return dict;

        foreach (var kat in plan.Katlar)
        {
            foreach (var blok in kat.Bloklar)
            {
                if (!string.IsNullOrWhiteSpace(blok.Id))
                    dict[blok.Id] = blok.KullanimSinifi;
            }
        }

        return dict;
    }

    private static string MahalNo(int katNo, int sira) => $"{katNo}{sira:00}";
}
