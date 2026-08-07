using Kst.Acoustics.Core.Electric.CevreAydinlatma.Fotometri;

namespace Kst.Acoustics.Core.Electric.CevreAydinlatma;

/// <summary>
/// Fotometrik veriden (LDT/IES) veya manuel girişten armatür kütüphane tanımı üretir.
/// Lümen metodu (Kademe A) için gerekli güç + akı buradan gelir.
/// </summary>
public static class ArmaturTanimFabrikasi
{
    /// <summary>Ayrıştırılmış fotometriden armatür tanımı kurar (güç/akı/ad eşlenir).</summary>
    public static CevreArmaturTanim FotometridenOlustur(FotometrikVeri fotometri, int id = 0)
    {
        return new CevreArmaturTanim
        {
            Id = id,
            Ad = string.IsNullOrWhiteSpace(fotometri.ArmaturAdi) ? "Armatür" : fotometri.ArmaturAdi,
            GucW = fotometri.ToplamGucW,
            AkiLm = fotometri.ToplamAkiLm,
            FotometriKaynak = fotometri.Kaynak
        };
    }

    /// <summary>LDT (.ldt) dosyasından armatür tanımı.</summary>
    public static CevreArmaturTanim LdtDosyasindan(string dosyaYolu, int id = 0)
    {
        var tanim = FotometridenOlustur(LdtParser.DosyadanOku(dosyaYolu), id);
        tanim.FotometriDosyaYolu = dosyaYolu;
        return tanim;
    }

    /// <summary>IES (.ies) dosyasından armatür tanımı.</summary>
    public static CevreArmaturTanim IesDosyasindan(string dosyaYolu, int id = 0)
    {
        var tanim = FotometridenOlustur(IesParser.DosyadanOku(dosyaYolu), id);
        tanim.FotometriDosyaYolu = dosyaYolu;
        return tanim;
    }
}
