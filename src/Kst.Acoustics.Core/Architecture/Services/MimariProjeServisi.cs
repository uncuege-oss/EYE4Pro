using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kst.Acoustics.Core.Architecture.Circulation;
using Kst.Acoustics.Core.Architecture.Layout;
using Kst.Acoustics.Core.Architecture.Materials;
using Kst.Acoustics.Core.Architecture.Model3D;
using Kst.Acoustics.Core.Architecture.Models;

namespace Kst.Acoustics.Core.Architecture.Services;

/// <summary>
/// `.e4mimari.json` dosyasinin kaydet / ac / migrasyon servisi.
/// </summary>
public static class MimariProjeServisi
{
    public const int GuncelSemaSurum = 1;

    private static readonly JsonSerializerOptions JsonAyar = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static MimariProje YeniOlustur(string projeAdi = "")
        => new()
        {
            ProjeAdi = string.IsNullOrWhiteSpace(projeAdi) ? "Yeni Mimari Proje" : projeAdi,
            SemaSurum = GuncelSemaSurum,
            OlusturmaZamani = DateTime.Now,
            GuncellemeZamani = DateTime.Now
        };

    public static void Kaydet(MimariProje proje, string dosyaYolu)
    {
        if (string.IsNullOrWhiteSpace(dosyaYolu))
            throw new ArgumentException("Mimari proje dosya yolu boş olamaz.", nameof(dosyaYolu));

        proje.SemaSurum = GuncelSemaSurum;
        proje.DosyaYolu = dosyaYolu;
        proje.GuncellemeZamani = DateTime.Now;

        var json = JsonSerializer.Serialize(proje, JsonAyar);
        File.WriteAllText(dosyaYolu, json);
    }

    public static MimariProje Ac(string dosyaYolu)
    {
        if (!File.Exists(dosyaYolu))
            throw new FileNotFoundException($"Mimari proje dosyası bulunamadı: {dosyaYolu}");

        var json = File.ReadAllText(dosyaYolu);
        var proje = JsonSerializer.Deserialize<MimariProje>(json, JsonAyar)
                    ?? throw new InvalidDataException("Dosya okunamadı veya boş.");

        proje.DosyaYolu = dosyaYolu;
        Migrate(proje);
        Normalize(proje);
        return proje;
    }

    private static void Migrate(MimariProje proje)
    {
        if (proje.SemaSurum >= GuncelSemaSurum) return;

        // Sürüm 0 -> 1: ilk resmi mimari BIM şeması; özel dönüşüm gerekmez.
        proje.SemaSurum = GuncelSemaSurum;
    }

    private static void Normalize(MimariProje proje)
    {
        proje.IhtiyacProgrami ??= new IhtiyacProgrami();
        proje.MahalSablonlari ??= new();
        proje.Malzemeler ??= new();
        proje.YapiElemaniTipleri ??= new();
        proje.FinisajProfilleri ??= new();
        proje.MalzemeAtamalari ??= new();
        proje.IhtiyacProgrami.Mahaller ??= new();
        if (proje.PlanTaslagi is not null)
        {
            if (string.IsNullOrWhiteSpace(proje.PlanTaslagi.ProjeId))
                proje.PlanTaslagi.ProjeId = proje.Id;

            NormalizePlanTaslagi(proje.PlanTaslagi);
        }

        foreach (var sablon in proje.MahalSablonlari)
        {
            sablon.UygunBinaIslevleri ??= new();
            sablon.YakinOlmali ??= new();
            sablon.UzakDurmali ??= new();
        }

        foreach (var malzeme in proje.Malzemeler)
        {
            NormalizeMalzeme(malzeme);
        }

        foreach (var tip in proje.YapiElemaniTipleri)
        {
            NormalizeYapiElemaniTipi(tip);
        }

        foreach (var atama in proje.MalzemeAtamalari)
        {
            atama.MalzemeIdleri ??= new();
        }

        foreach (var profil in proje.FinisajProfilleri)
        {
            profil.UyariNotlari ??= new();
        }
    }

    private static void NormalizeMalzeme(MimariMalzeme malzeme)
    {
        malzeme.TeknikSiniflar ??= new();
        malzeme.UygunMahalTipleri ??= new();
        malzeme.UygunKullanimSiniflari ??= new();
        malzeme.Performans ??= new MalzemePerformansi();
        malzeme.Metraj ??= new MetrajBilgisi();
    }

    private static void NormalizeYapiElemaniTipi(YapiElemaniTipi tip)
    {
        tip.Katmanlar ??= new();
        foreach (var katman in tip.Katmanlar)
        {
            if (string.IsNullOrWhiteSpace(katman.Id))
                katman.Id = Guid.NewGuid().ToString();
        }
    }

    private static void NormalizePlanTaslagi(PlanTaslagi plan)
    {
        plan.Katlar ??= new();
        plan.Iliskiler ??= new();
        plan.KacisGrafi ??= new KacisGrafi();
        plan.KacisGrafi.Dugumler ??= new();
        plan.KacisGrafi.Kenarlar ??= new();
        plan.Parametreler ??= new YerlesimParametreleri();
        if (plan.Model3D is not null)
            NormalizeModel3D(plan, plan.Model3D);

        foreach (var kat in plan.Katlar)
        {
            kat.Bloklar ??= new();
            kat.Koridorlar ??= new();
            kat.Cekirdekler ??= new();
            kat.CekirdekGruplari ??= new();
            kat.Saftlar ??= new();
            kat.Sinir ??= new PlanSiniri();

            foreach (var koridor in kat.Koridorlar)
            {
                koridor.BagliBlokIdleri ??= new();
            }

            foreach (var cekirdek in kat.Cekirdekler)
            {
                cekirdek.BagliKoridorIdleri ??= new();
                cekirdek.BagliMahalIdleri ??= new();
            }

            foreach (var grup in kat.CekirdekGruplari)
            {
                grup.ElemanIdleri ??= new();
            }

            foreach (var saft in kat.Saftlar)
            {
                saft.BagliMahalIdleri ??= new();
            }
        }
    }

    private static void NormalizeModel3D(PlanTaslagi plan, Bim3DModel model)
    {
        if (string.IsNullOrWhiteSpace(model.KaynakPlanId))
            model.KaynakPlanId = plan.Id;

        model.Katlar ??= new();
        foreach (var kat in model.Katlar)
        {
            kat.Dosemeler ??= new();
            kat.Tavanlar ??= new();
            kat.Mahaller ??= new();
            kat.Duvarlar ??= new();
            kat.Acikliklar ??= new();
            kat.CekirdekHacimleri ??= new();
            kat.SaftHacimleri ??= new();

            foreach (var duvar in kat.Duvarlar)
            {
                duvar.AciklikIdleri ??= new();
            }
        }
    }
}
