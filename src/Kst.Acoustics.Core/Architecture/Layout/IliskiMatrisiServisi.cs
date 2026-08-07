using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Architecture.Models;

namespace Kst.Acoustics.Core.Architecture.Layout;

/// <summary>
/// M32 icin ilk varsayilan mahal iliskilerini uretir.
/// </summary>
public static class IliskiMatrisiServisi
{
    public static IReadOnlyList<MahalIliskisi> VarsayilanIliskileriUret(PlanTaslagi? plan)
        => VarsayilanIliskileriUret(plan?.Katlar.SelectMany(k => k.Bloklar));

    public static IReadOnlyList<MahalIliskisi> VarsayilanIliskileriUret(IEnumerable<MahalBlogu>? bloklar)
    {
        var blokListesi = (bloklar ?? [])
            .Where(b => b is not null)
            .Where(b => !string.IsNullOrWhiteSpace(b.Id))
            .ToList();

        if (blokListesi.Count == 0)
            return [];

        var iliskiler = new List<MahalIliskisi>();
        var eklenenler = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var blok in blokListesi)
        {
            if (KoridorYerlesimServisi.KoridoraBaglanmaliMi(blok))
            {
                Ekle(iliskiler, eklenenler, blok, null, IliskiTipi.KoridoraBaglanmali, IliskiOnceligi.Kritik,
                    $"{blok.Ad} koridor ağına bağlanmalı.");
            }

            if (CepheAlmaliMi(blok))
            {
                Ekle(iliskiler, eklenenler, blok, null, IliskiTipi.CepheAlmali, IliskiOnceligi.Yuksek,
                    $"{blok.Ad} doğal ışık ve kullanım kalitesi için cephe almalı.");
            }

            if (CekirdegeYakinOlmaliMi(blok))
            {
                Ekle(iliskiler, eklenenler, blok, null, IliskiTipi.CekirdegeYakinOlmali, IliskiOnceligi.Yuksek,
                    $"{blok.Ad} düşey çekirdek veya servis omurgasına yakın olmalı.");
            }
        }

        foreach (var kaynak in blokListesi)
        {
            foreach (var hedef in blokListesi)
            {
                if (ReferenceEquals(kaynak, hedef))
                    continue;

                if (YakinOlmaliMi(kaynak, hedef))
                {
                    Ekle(iliskiler, eklenenler, kaynak, hedef, IliskiTipi.YakinOlmali, IliskiOnceligi.Yuksek,
                        $"{kaynak.Ad} ile {hedef.Ad} yakın çözülmeli.");
                }

                if (UzakDurmaliMi(kaynak, hedef))
                {
                    Ekle(iliskiler, eklenenler, kaynak, hedef, IliskiTipi.UzakDurmali, IliskiOnceligi.Normal,
                        $"{kaynak.Ad} ile {hedef.Ad} gürültü, servis veya mahremiyet nedeniyle ayrılmalı.");
                }

                if (AyniKattaOlmaliMi(kaynak, hedef))
                {
                    Ekle(iliskiler, eklenenler, kaynak, hedef, IliskiTipi.AyniKattaOlmali, IliskiOnceligi.Normal,
                        $"{kaynak.Ad} ile {hedef.Ad} aynı katta değerlendirilmelidir.");
                }
            }
        }

        return Sirala(iliskiler);
    }

    public static IReadOnlyList<MahalIliskisi> Sirala(IEnumerable<MahalIliskisi>? iliskiler)
        => (iliskiler ?? [])
            .OrderByDescending(i => i.OncelikPuani)
            .ThenBy(i => i.IliskiTipi)
            .ThenBy(i => i.KaynakMahalId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.HedefMahalId, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static void Ekle(
        List<MahalIliskisi> iliskiler,
        HashSet<string> eklenenler,
        MahalBlogu kaynak,
        MahalBlogu? hedef,
        IliskiTipi tip,
        IliskiOnceligi oncelik,
        string aciklama)
    {
        var hedefId = hedef?.Id ?? "";
        var anahtar = $"{tip}|{SiraliIkili(kaynak.Id, hedefId, HedefSirasizMi(tip))}";
        if (!eklenenler.Add(anahtar))
            return;

        iliskiler.Add(new MahalIliskisi
        {
            KaynakMahalId = kaynak.Id,
            HedefMahalId = hedefId,
            IliskiTipi = tip,
            Oncelik = oncelik,
            Aciklama = aciklama
        });
    }

    private static string SiraliIkili(string kaynakId, string hedefId, bool sirasiz)
    {
        if (!sirasiz || string.IsNullOrWhiteSpace(hedefId))
            return $"{kaynakId}>{hedefId}";

        return string.Compare(kaynakId, hedefId, StringComparison.OrdinalIgnoreCase) <= 0
            ? $"{kaynakId}>{hedefId}"
            : $"{hedefId}>{kaynakId}";
    }

    private static bool HedefSirasizMi(IliskiTipi tip)
        => tip is IliskiTipi.YakinOlmali or IliskiTipi.UzakDurmali or IliskiTipi.AyniKattaOlmali;

    private static bool CepheAlmaliMi(MahalBlogu blok)
        => blok.KullanimSinifi == KullanimSinifi.AnaKullanim ||
           blok.MahalTipi is MahalTipi.Salon
               or MahalTipi.OturmaOdasi
               or MahalTipi.YatakOdasi
               or MahalTipi.CocukOdasi
               or MahalTipi.AcikOfis
               or MahalTipi.Ofis
               or MahalTipi.ToplantiOdasi
               or MahalTipi.Derslik
               or MahalTipi.Kutuphane
               or MahalTipi.HastaOdasi
               or MahalTipi.Restoran
               or MahalTipi.Kafe;

    private static bool CekirdegeYakinOlmaliMi(MahalBlogu blok)
        => blok.KullanimSinifi is KullanimSinifi.IslakHacim or KullanimSinifi.TeknikHacim or KullanimSinifi.GuvenlikVeYangin ||
           blok.MahalTipi is MahalTipi.Wc
               or MahalTipi.Banyo
               or MahalTipi.TeknikOda
               or MahalTipi.ElektrikOdasi
               or MahalTipi.MekanikOda
               or MahalTipi.ServerOdasi
               or MahalTipi.Saft
               or MahalTipi.Otopark;

    private static bool YakinOlmaliMi(MahalBlogu kaynak, MahalBlogu hedef)
    {
        if (IslakHacimMi(kaynak) && hedef.MahalTipi == MahalTipi.Saft)
            return true;

        if (kaynak.MahalTipi == MahalTipi.Saft && IslakHacimMi(hedef))
            return true;

        if (TeknikOmurgaMi(kaynak) && TeknikOmurgaMi(hedef))
            return true;

        if (MutfakIliskiliMi(kaynak) && MutfakIliskiliMi(hedef))
            return true;

        if (KonferansGrubuMi(kaynak) && KonferansGrubuMi(hedef))
            return true;

        if (OfisGrubuMi(kaynak) && OfisGrubuMi(hedef))
            return true;

        if (OtoparkCekirdekIliskisiMi(kaynak, hedef))
            return true;

        return false;
    }

    private static bool UzakDurmaliMi(MahalBlogu kaynak, MahalBlogu hedef)
    {
        if (GurultuluServisMi(kaynak) && SessizKullanimMi(hedef))
            return true;

        if (SessizKullanimMi(kaynak) && GurultuluServisMi(hedef))
            return true;

        return false;
    }

    private static bool AyniKattaOlmaliMi(MahalBlogu kaynak, MahalBlogu hedef)
    {
        if (kaynak.MahalTipi == MahalTipi.AcikOfis && hedef.MahalTipi == MahalTipi.ToplantiOdasi)
            return true;

        if (kaynak.MahalTipi == MahalTipi.ToplantiOdasi && hedef.MahalTipi == MahalTipi.AcikOfis)
            return true;

        if (kaynak.MahalTipi == MahalTipi.Salon && hedef.MahalTipi == MahalTipi.Mutfak)
            return true;

        if (kaynak.MahalTipi == MahalTipi.Mutfak && hedef.MahalTipi == MahalTipi.Salon)
            return true;

        return false;
    }

    private static bool IslakHacimMi(MahalBlogu blok)
        => blok.KullanimSinifi == KullanimSinifi.IslakHacim ||
           blok.MahalTipi is MahalTipi.Wc or MahalTipi.Banyo;

    private static bool TeknikOmurgaMi(MahalBlogu blok)
        => blok.KullanimSinifi == KullanimSinifi.TeknikHacim ||
           blok.MahalTipi is MahalTipi.TeknikOda
               or MahalTipi.ElektrikOdasi
               or MahalTipi.MekanikOda
               or MahalTipi.ServerOdasi
               or MahalTipi.Saft;

    private static bool GurultuluServisMi(MahalBlogu blok)
        => blok.MahalTipi is MahalTipi.MekanikOda
               or MahalTipi.JeneratorOdasi
               or MahalTipi.KazanDairesi
               or MahalTipi.Otopark
               or MahalTipi.Mutfak ||
           blok.KullanimSinifi is KullanimSinifi.TeknikHacim or KullanimSinifi.Servis or KullanimSinifi.Otopark;

    private static bool SessizKullanimMi(MahalBlogu blok)
        => blok.MahalTipi is MahalTipi.YatakOdasi
               or MahalTipi.CocukOdasi
               or MahalTipi.HastaOdasi
               or MahalTipi.Derslik
               or MahalTipi.Kutuphane;

    private static bool MutfakIliskiliMi(MahalBlogu blok)
        => blok.MahalTipi is MahalTipi.Mutfak
               or MahalTipi.Salon
               or MahalTipi.Restoran
               or MahalTipi.Kafe;

    private static bool KonferansGrubuMi(MahalBlogu blok)
        => blok.MahalTipi is MahalTipi.KonferansSalonu
               or MahalTipi.Fuaye
               or MahalTipi.Sahne
               or MahalTipi.Kulis;

    private static bool OfisGrubuMi(MahalBlogu blok)
        => blok.MahalTipi is MahalTipi.AcikOfis
               or MahalTipi.ToplantiOdasi
               or MahalTipi.Resepsiyon
               or MahalTipi.BeklemeAlani;

    private static bool OtoparkCekirdekIliskisiMi(MahalBlogu kaynak, MahalBlogu hedef)
        => (kaynak.MahalTipi == MahalTipi.Otopark && CekirdekMi(hedef)) ||
           (hedef.MahalTipi == MahalTipi.Otopark && CekirdekMi(kaynak));

    private static bool CekirdekMi(MahalBlogu blok)
        => blok.KullanimSinifi == KullanimSinifi.DuseySirkulasyon ||
           blok.MahalTipi is MahalTipi.Cekirdek
               or MahalTipi.Merdiven
               or MahalTipi.YanginMerdiveni
               or MahalTipi.Asansor
               or MahalTipi.Rampa;
}
