using System;
using System.Linq;
using Kst.Acoustics.Core.Architecture.Models;
using Kst.Acoustics.Core.Architecture.Rules;

namespace Kst.Acoustics.Tests;

public static class MimariProjeDogrulamaTests
{
    public static void BosProjeKritikSonucUretir()
    {
        var sonuclar = MimariProjeDogrulamaServisi.Dogrula(null);

        if (!sonuclar.Any(s => s.Kod == "MIMARI_PROJE_BOS" && s.Seviye == KontrolSeviyesi.Kritik))
            throw new InvalidOperationException("Boş mimari proje kritik sonuç üretmeliydi.");
    }

    public static void EksikBinaIsleviVeBosMahalListesiYakalanir()
    {
        var proje = MimariProjeServisiIcermeyenYeniProje();
        var sonuclar = MimariProjeDogrulamaServisi.Dogrula(proje);

        if (!sonuclar.Any(s => s.Kod == "BINA_ISLEVI_EKSIK" && s.Seviye == KontrolSeviyesi.Kritik))
            throw new InvalidOperationException("Eksik bina işlevi kritik sonuç üretmeliydi.");
        if (!sonuclar.Any(s => s.Kod == "MAHAL_LISTESI_BOS" && s.Seviye == KontrolSeviyesi.Uyari))
            throw new InvalidOperationException("Boş mahal listesi uyarı üretmeliydi.");
    }

    public static void NegatifAlanVeGecersizAdetYakalanir()
    {
        var proje = MimariProjeServisiIcermeyenYeniProje();
        proje.IhtiyacProgrami.BinaIslevi = BinaIslevi.Ofis;
        proje.IhtiyacProgrami.Mahaller.Add(new MahalGereksinimi
        {
            Id = "mah-01",
            Ad = "Toplantı Odası",
            MahalTipi = MahalTipi.ToplantiOdasi,
            KullanimSinifi = KullanimSinifi.AnaKullanim,
            Adet = 0,
            MinimumAlanM2 = 12,
            IdealAlanM2 = 18,
            HedefAlanM2 = -5,
            HedefNetYukseklikM = 3.0
        });

        var sonuclar = MimariProjeDogrulamaServisi.Dogrula(proje);

        if (!sonuclar.Any(s => s.Kod == "MAHAL_ADEDI_GECERSIZ" && s.Seviye == KontrolSeviyesi.Kritik))
            throw new InvalidOperationException("Geçersiz mahal adedi kritik sonuç üretmeliydi.");
        if (!sonuclar.Any(s => s.Kod == "MAHAL_ALANI_NEGATIF" && s.Seviye == KontrolSeviyesi.Kritik))
            throw new InvalidOperationException("Negatif mahal alanı kritik sonuç üretmeliydi.");
    }

    public static void GecerliMinimalProjeKritikUretmez()
    {
        var proje = MimariProjeServisiIcermeyenYeniProje();
        proje.IhtiyacProgrami.BinaIslevi = BinaIslevi.Ofis;
        proje.IhtiyacProgrami.Mahaller.Add(new MahalGereksinimi
        {
            Id = "mah-01",
            Ad = "Açık Ofis",
            MahalTipi = MahalTipi.AcikOfis,
            KullanimSinifi = KullanimSinifi.AnaKullanim,
            Adet = 1,
            MinimumAlanM2 = 50,
            IdealAlanM2 = 75,
            HedefAlanM2 = 80,
            MinimumNetYukseklikM = 2.6,
            HedefNetYukseklikM = 3.0
        });

        var sonuclar = MimariProjeDogrulamaServisi.Dogrula(proje);

        if (sonuclar.Any(s => s.Seviye == KontrolSeviyesi.Kritik))
            throw new InvalidOperationException($"Geçerli minimal proje kritik sonuç üretmemeliydi: {string.Join(", ", sonuclar.Select(s => s.Kod))}");
    }

    private static MimariProje MimariProjeServisiIcermeyenYeniProje()
        => new()
        {
            ProjeAdi = "Doğrulama Testi",
            IhtiyacProgrami = new IhtiyacProgrami()
        };

}
