using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Architecture.Models;

namespace Kst.Acoustics.Core.Architecture.Layout;

/// <summary>
/// M31 ihtiyac programindan M32 konsept plan taslagi uretir.
/// </summary>
public static class PlanTaslagiUretimServisi
{
    public static PlanTaslagi Uret(MimariProje? proje)
    {
        var plan = Uret(proje?.IhtiyacProgrami, proje?.Id ?? "");
        return plan;
    }

    public static PlanTaslagi Uret(IhtiyacProgrami? program, string projeId = "")
    {
        var plan = new PlanTaslagi
        {
            ProjeId = projeId,
            Parametreler = new YerlesimParametreleri()
        };

        if (program is null)
            return plan;

        var katSayisi = Math.Max(1, program.KatSayisi);
        plan.Parametreler.VarsayilanKatSayisi = katSayisi;

        for (var katNo = 1; katNo <= katSayisi; katNo++)
        {
            plan.Katlar.Add(new KatTaslagi
            {
                KatNo = katNo,
                Ad = $"Kat {katNo}",
                HedefAlanM2 = program.HedefToplamAlanM2 > 0 ? program.HedefToplamAlanM2 / katSayisi : 0
            });
        }

        var bloklar = BloklariUret(program).OrderByDescending(b => b.HedefAlanM2).ThenBy(b => b.Ad, StringComparer.OrdinalIgnoreCase);
        foreach (var blok in bloklar)
        {
            var hedefKat = EnAzYukluKat(plan.Katlar);
            blok.KatNo = hedefKat.KatNo;
            hedefKat.Bloklar.Add(blok);
        }

        foreach (var kat in plan.Katlar)
        {
            if (kat.HedefAlanM2 <= 0)
                kat.HedefAlanM2 = kat.BlokHedefAlanM2;
        }

        BasitYerlesimServisi.Yerlestir(plan);
        KoridorYerlesimServisi.KoridorlariUret(plan);
        plan.Iliskiler = IliskiMatrisiServisi.VarsayilanIliskileriUret(plan).ToList();
        plan.Notlar = "M32 ilk taslak yerleşim ve koridor aksı algoritmasıyla üretildi.";

        return plan;
    }

    public static IReadOnlyList<MahalBlogu> BloklariUret(IhtiyacProgrami? program)
    {
        if (program is null)
            return [];

        var bloklar = new List<MahalBlogu>();
        foreach (var mahal in program.Mahaller)
        {
            if (mahal is null)
                continue;

            bloklar.Add(MahalBlogu.Gereksinimden(mahal));
        }

        return bloklar;
    }

    private static KatTaslagi EnAzYukluKat(IReadOnlyList<KatTaslagi> katlar)
    {
        if (katlar.Count == 0)
            throw new InvalidOperationException("Plan taslağı için en az bir kat gereklidir.");

        return katlar
            .OrderBy(k => k.BlokHedefAlanM2)
            .ThenBy(k => k.KatNo)
            .First();
    }
}
