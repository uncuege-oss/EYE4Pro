using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Architecture.Circulation;
using Kst.Acoustics.Core.Architecture.Layout;

namespace Kst.Acoustics.Core.Architecture.Model3D;

/// <summary>
/// M32/M33 2D taslagindan M35 hafif 3D BIM modelini uretir.
/// </summary>
public static class Model3DUretimServisi
{
    public static Bim3DModel Uret(PlanTaslagi? plan, Model3DUretimSecenekleri? secenekler = null)
    {
        secenekler ??= new Model3DUretimSecenekleri();
        var model = new Bim3DModel
        {
            Id = string.IsNullOrWhiteSpace(plan?.Id) ? Guid.NewGuid().ToString() : $"model3d-{plan.Id}",
            Ad = "3D BIM Model",
            KaynakPlanId = plan?.Id ?? "",
            Notlar = plan is null
                ? "Plan taslagi olmadan bos 3D model olusturuldu."
                : "M35 2D taslaktan 3D uretim servisiyle olusturuldu."
        };

        if (plan is null)
            return model;

        var katlar = plan.Katlar
            .Where(k => k is not null)
            .OrderBy(k => k.KatNo)
            .ToList();
        if (katlar.Count == 0)
        {
            if (secenekler.PlanaYaz)
                plan.Model3D = model;

            return model;
        }

        var minKatNo = katlar.Min(k => k.KatNo);
        var tumCekirdekler = katlar
            .SelectMany(k => k.Cekirdekler)
            .Where(c => c is not null)
            .DistinctBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var tumSaftlar = katlar
            .SelectMany(k => k.Saftlar)
            .Where(s => s is not null)
            .DistinctBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var kat in katlar)
        {
            model.Katlar.Add(KatUret(kat, minKatNo, tumCekirdekler, tumSaftlar, secenekler));
        }

        if (secenekler.PlanaYaz)
            plan.Model3D = model;

        return model;
    }

    private static Kat3DModel KatUret(
        KatTaslagi kat,
        int minKatNo,
        List<CekirdekElemani> tumCekirdekler,
        List<SaftTaslagi> tumSaftlar,
        Model3DUretimSecenekleri secenekler)
    {
        var katYuksekligi = PozitifVeya(secenekler.VarsayilanKatYuksekligiM, 3.2);
        var netYukseklik = NetYukseklik(secenekler.VarsayilanNetYukseklikM, katYuksekligi);
        var tabanKotu = (kat.KatNo - minKatNo) * katYuksekligi;

        var kat3D = new Kat3DModel
        {
            Id = string.IsNullOrWhiteSpace(kat.Id) ? Guid.NewGuid().ToString() : $"kat3d-{kat.Id}",
            KaynakKatTaslagiId = kat.Id,
            KatNo = kat.KatNo,
            Ad = kat.Ad,
            TabanKotuM = tabanKotu,
            KatYuksekligiM = katYuksekligi,
            NetYukseklikM = netYukseklik
        };

        var sinir = Model3DGeometriServisi.KatSiniriBul(kat, secenekler.PlanSiniriBoslukM);
        if (secenekler.DosemeVeTavanUret && sinir.AlanM2 > 0)
        {
            kat3D.Dosemeler.Add(new Doseme3D
            {
                Id = $"doseme3d-{kat.Id}",
                KaynakKatTaslagiId = kat.Id,
                KatNo = kat.KatNo,
                X = sinir.X,
                Y = sinir.Y,
                Z = tabanKotu,
                GenislikM = sinir.GenislikM,
                DerinlikM = sinir.DerinlikM,
                KalinlikM = PozitifVeya(secenekler.VarsayilanDosemeKalinligiM, 0.2),
                DosemeTipiId = secenekler.VarsayilanDosemeTipiId
            });

            kat3D.Tavanlar.Add(new Tavan3D
            {
                Id = $"tavan3d-{kat.Id}",
                KaynakKatTaslagiId = kat.Id,
                KatNo = kat.KatNo,
                X = sinir.X,
                Y = sinir.Y,
                Z = tabanKotu + netYukseklik,
                GenislikM = sinir.GenislikM,
                DerinlikM = sinir.DerinlikM,
                KalinlikM = PozitifVeya(secenekler.VarsayilanTavanKalinligiM, 0.1),
                TavanTipiId = secenekler.VarsayilanTavanTipiId
            });
        }

        foreach (var blok in kat.Bloklar.Where(b => b.YerlesmisMi).OrderBy(b => b.Ad, StringComparer.OrdinalIgnoreCase))
        {
            kat3D.Mahaller.Add(new MahalHacmi3D
            {
                Id = $"mahal3d-{blok.Id}",
                KaynakMahalBloguId = blok.Id,
                Ad = blok.Ad,
                Tip = blok.MahalTipi,
                KatNo = kat.KatNo,
                X = blok.X,
                Y = blok.Y,
                Z = tabanKotu,
                GenislikM = blok.GenislikM,
                DerinlikM = blok.DerinlikM,
                YukseklikM = netYukseklik,
                Notlar = blok.Notlar
            });

            if (secenekler.DuvarlariUret)
            {
                kat3D.Duvarlar.AddRange(Model3DGeometriServisi.DikdortgenDuvarlariUret(
                    blok,
                    tabanKotu,
                    katYuksekligi,
                    PozitifVeya(secenekler.VarsayilanDuvarKalinligiM, 0.2),
                    secenekler.VarsayilanDuvarTipiId));
            }
        }

        if (secenekler.CekirdekVeSaftHacimleriniUret)
        {
            CekirdekHacimleriniEkle(kat3D, tumCekirdekler, kat.KatNo, tabanKotu, katYuksekligi);
            SaftHacimleriniEkle(kat3D, tumSaftlar, kat.KatNo, tabanKotu, katYuksekligi);
        }

        return kat3D;
    }

    private static void CekirdekHacimleriniEkle(
        Kat3DModel kat3D,
        IEnumerable<CekirdekElemani> cekirdekler,
        int katNo,
        double tabanKotu,
        double katYuksekligi)
    {
        foreach (var cekirdek in cekirdekler
                     .Where(c => c.YerlesmisMi && c.KatAraligindaMi(katNo))
                     .OrderBy(c => c.Ad, StringComparer.OrdinalIgnoreCase))
        {
            kat3D.CekirdekHacimleri.Add(new CekirdekHacmi3D
            {
                Id = $"cekirdek3d-{cekirdek.Id}-kat{katNo}",
                KaynakCekirdekId = cekirdek.Id,
                Ad = cekirdek.Ad,
                Tip = cekirdek.Tip,
                KatNo = katNo,
                BaslangicKatNo = cekirdek.BaslangicKatNo,
                BitisKatNo = cekirdek.BitisKatNo,
                X = cekirdek.X,
                Y = cekirdek.Y,
                Z = tabanKotu,
                GenislikM = cekirdek.GenislikM,
                DerinlikM = cekirdek.DerinlikM,
                YukseklikM = katYuksekligi,
                AksKodu = cekirdek.AksKodu,
                Notlar = cekirdek.Notlar
            });
        }
    }

    private static void SaftHacimleriniEkle(
        Kat3DModel kat3D,
        IEnumerable<SaftTaslagi> saftlar,
        int katNo,
        double tabanKotu,
        double katYuksekligi)
    {
        foreach (var saft in saftlar
                     .Where(s => s.YerlesmisMi && s.KatAraligindaMi(katNo))
                     .OrderBy(s => s.Ad, StringComparer.OrdinalIgnoreCase))
        {
            kat3D.SaftHacimleri.Add(new SaftHacmi3D
            {
                Id = $"saft3d-{saft.Id}-kat{katNo}",
                KaynakSaftId = saft.Id,
                Ad = saft.Ad,
                TesisatTuru = saft.TesisatTuru,
                KatNo = katNo,
                BaslangicKatNo = saft.BaslangicKatNo,
                BitisKatNo = saft.BitisKatNo,
                X = saft.X,
                Y = saft.Y,
                Z = tabanKotu,
                GenislikM = saft.GenislikM,
                DerinlikM = saft.DerinlikM,
                YukseklikM = katYuksekligi,
                AksKodu = saft.AksKodu,
                Notlar = saft.Notlar
            });
        }
    }

    private static double PozitifVeya(double deger, double varsayilan)
        => deger > 0 ? deger : varsayilan;

    private static double NetYukseklik(double netYukseklik, double katYuksekligi)
    {
        if (netYukseklik <= 0)
            return Math.Max(0, katYuksekligi);

        return Math.Min(netYukseklik, Math.Max(0, katYuksekligi));
    }
}
