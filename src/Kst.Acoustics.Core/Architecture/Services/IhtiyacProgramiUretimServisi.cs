using Kst.Acoustics.Core.Architecture.Models;
using Kst.Acoustics.Core.Architecture.Templates;

namespace Kst.Acoustics.Core.Architecture.Services;

/// <summary>
/// Bina islevi, kapasite ve kat bilgisine gore ilk ihtiyac programini uretir.
/// </summary>
public static class IhtiyacProgramiUretimServisi
{
    public static IhtiyacProgrami Uret(
        BinaIslevi islev,
        int kapasite = 0,
        int katSayisi = 1,
        string projeTipi = "")
    {
        if (islev == BinaIslevi.Tanimsiz)
            throw new ArgumentException("Bina işlevi seçilmelidir.", nameof(islev));

        if (kapasite < 0)
            throw new ArgumentOutOfRangeException(nameof(kapasite), "Kapasite negatif olamaz.");

        if (katSayisi <= 0)
            throw new ArgumentOutOfRangeException(nameof(katSayisi), "Kat sayısı pozitif olmalıdır.");

        var tanim = BinaIsleviTanimlari.Bul(islev)
            ?? throw new ArgumentException($"Bina işlevi için tanım bulunamadı: {islev}", nameof(islev));

        var program = new IhtiyacProgrami
        {
            BinaIslevi = islev,
            ProjeTipi = string.IsNullOrWhiteSpace(projeTipi) ? tanim.Ad : projeTipi.Trim(),
            KullaniciKapasitesi = kapasite,
            KatSayisi = katSayisi,
            VarsayilanKatYuksekligiM = tanim.VarsayilanKatYuksekligiM,
            SirkulasyonOrani = tanim.VarsayilanSirkulasyonOrani,
            TeknikAlanOrani = tanim.VarsayilanTeknikAlanOrani
        };

        foreach (var sablon in VarsayilanMahalSablonlari.BinaIslevineGore(islev).Where(s => s.Zorunlu))
        {
            program.Mahaller.Add(sablon.GereksinimOlustur(kapasite));
        }

        program.HedefToplamAlanM2 = program.OnerilenBrutAlanM2;
        return program;
    }
}
