using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Architecture.Materials;
using Kst.Acoustics.Core.Architecture.Model3D;
using Kst.Acoustics.Core.Architecture.Models;

namespace Kst.Acoustics.Core.Architecture.Coordination;

/// <summary>
/// Mimari projenin içsel tutarlılığını denetler: malzeme atamalarının bağlı olduğu 3D elemanlar
/// hâlâ var mı, atama/finisaj profilleri geçerli yapı elemanı tipine mi işaret ediyor.
/// </summary>
public static class MutabakatServisi
{
    public const string AskidaAtamaKodu = "M39-ASKIDA-ATAMA";
    public const string GecersizTipKodu = "M39-GECERSIZ-TIP";

    public static MutabakatRaporu Denetle(MimariProje? proje)
    {
        var bulgular = new List<MutabakatBulgusu>();
        if (proje is null)
            return new MutabakatRaporu();

        var elemanIdleri = ElemanIdSozlugu(proje.PlanTaslagi?.Model3D);
        var tipKatalogu = TipKatalogu(proje);

        foreach (var atama in proje.MalzemeAtamalari)
        {
            if (!ElemanVarMi(elemanIdleri, atama.KaynakElemanTuru, atama.KaynakElemanId))
            {
                bulgular.Add(new MutabakatBulgusu
                {
                    Tur = MutabakatBulguTuru.AskidaAtama,
                    KuralKodu = AskidaAtamaKodu,
                    KaynakId = atama.Id,
                    EtkilenenElemanId = atama.KaynakElemanId,
                    Aciklama = $"'{atama.KaynakElemanTuru}' türündeki '{atama.KaynakElemanId}' elemanı 3D modelde bulunamadı; atama askıda."
                });
            }

            if (!string.IsNullOrWhiteSpace(atama.YapiElemaniTipiId) && !tipKatalogu.Contains(atama.YapiElemaniTipiId))
            {
                bulgular.Add(new MutabakatBulgusu
                {
                    Tur = MutabakatBulguTuru.GecersizTipReferansi,
                    KuralKodu = GecersizTipKodu,
                    KaynakId = atama.Id,
                    EtkilenenElemanId = atama.KaynakElemanId,
                    Aciklama = $"Atama, katalogda olmayan yapı elemanı tipine işaret ediyor: '{atama.YapiElemaniTipiId}'."
                });
            }
        }

        foreach (var profil in proje.FinisajProfilleri)
        {
            foreach (var (etiket, tipId) in new[]
                     {
                         ("zemin", profil.ZeminTipiId),
                         ("duvar", profil.DuvarTipiId),
                         ("tavan", profil.TavanTipiId)
                     })
            {
                if (!string.IsNullOrWhiteSpace(tipId) && !tipKatalogu.Contains(tipId))
                {
                    bulgular.Add(new MutabakatBulgusu
                    {
                        Tur = MutabakatBulguTuru.GecersizTipReferansi,
                        KuralKodu = GecersizTipKodu,
                        KaynakId = profil.Kod,
                        Aciklama = $"'{profil.Kod}' finisaj profilinin {etiket} tipi katalogda yok: '{tipId}'."
                    });
                }
            }
        }

        return new MutabakatRaporu { Bulgular = bulgular };
    }

    private static bool ElemanVarMi(
        IReadOnlyDictionary<MalzemeAtamaHedefTuru, HashSet<string>> idler,
        MalzemeAtamaHedefTuru tur,
        string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        return idler.TryGetValue(tur, out var set) && set.Contains(id);
    }

    private static Dictionary<MalzemeAtamaHedefTuru, HashSet<string>> ElemanIdSozlugu(Bim3DModel? model)
    {
        var dict = new Dictionary<MalzemeAtamaHedefTuru, HashSet<string>>
        {
            [MalzemeAtamaHedefTuru.Mahal] = new(StringComparer.Ordinal),
            [MalzemeAtamaHedefTuru.Duvar] = new(StringComparer.Ordinal),
            [MalzemeAtamaHedefTuru.Doseme] = new(StringComparer.Ordinal),
            [MalzemeAtamaHedefTuru.Tavan] = new(StringComparer.Ordinal),
            [MalzemeAtamaHedefTuru.Kapi] = new(StringComparer.Ordinal),
            [MalzemeAtamaHedefTuru.Pencere] = new(StringComparer.Ordinal),
            [MalzemeAtamaHedefTuru.Cekirdek] = new(StringComparer.Ordinal),
            [MalzemeAtamaHedefTuru.Saft] = new(StringComparer.Ordinal)
        };

        if (model is null)
            return dict;

        foreach (var kat in model.Katlar)
        {
            foreach (var m in kat.Mahaller) dict[MalzemeAtamaHedefTuru.Mahal].Add(m.Id);
            foreach (var d in kat.Duvarlar) dict[MalzemeAtamaHedefTuru.Duvar].Add(d.Id);
            foreach (var d in kat.Dosemeler) dict[MalzemeAtamaHedefTuru.Doseme].Add(d.Id);
            foreach (var t in kat.Tavanlar) dict[MalzemeAtamaHedefTuru.Tavan].Add(t.Id);
            foreach (var a in kat.Acikliklar)
            {
                var tur = a.Tip == Aciklik3DTipi.Pencere ? MalzemeAtamaHedefTuru.Pencere : MalzemeAtamaHedefTuru.Kapi;
                dict[tur].Add(a.Id);
            }
            foreach (var c in kat.CekirdekHacimleri) dict[MalzemeAtamaHedefTuru.Cekirdek].Add(c.Id);
            foreach (var s in kat.SaftHacimleri) dict[MalzemeAtamaHedefTuru.Saft].Add(s.Id);
        }

        return dict;
    }

    private static HashSet<string> TipKatalogu(MimariProje proje)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tip in VarsayilanYapiElemaniTipleri.Tum)
            set.Add(tip.Id);
        foreach (var tip in proje.YapiElemaniTipleri)
            if (!string.IsNullOrWhiteSpace(tip.Id))
                set.Add(tip.Id);

        return set;
    }
}
