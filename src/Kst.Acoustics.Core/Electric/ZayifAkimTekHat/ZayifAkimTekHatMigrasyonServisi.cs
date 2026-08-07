namespace Kst.Acoustics.Core.Electric.ZayifAkimTekHat;

public sealed record ElvMigrasyonSonucu(bool Basarili, int EskiSurum, int YeniSurum, string Mesaj);

/// <summary>
/// .e4p13 dosyaları için şema sürümü migrasyonu (M12 SemaSurum deseni).
/// Eski dosyada <see cref="ZayifAkimTekHatProje.SemaSurum"/> alanı yoktur (0 okunur);
/// açılışta buradan geçirilip güncel sürüme taşınır. Gelecekten (daha yeni sürüm)
/// gelen dosya migre edilmez, açılış reddedilir.
/// </summary>
public static class ZayifAkimTekHatMigrasyonServisi
{
    public static ElvMigrasyonSonucu Uygula(ZayifAkimTekHatProje proje)
    {
        int eski = proje.SemaSurum;
        if (eski > ZayifAkimTekHatProje.GuncelSemaSurum)
            return new(false, eski, eski,
                $"Dosya şema sürümü ({eski}) bu programın desteklediğinden ({ZayifAkimTekHatProje.GuncelSemaSurum}) yeni. Programı güncelleyin.");

        if (eski < 1)
            SurumeGecir1(proje);
        if (eski < 2)
            proje.OzelKutuphaneKalemleri ??= [];
        if (eski < 3)
        {
            proje.TvSistemi.Teknoloji = TvDagitimTeknolojisi.MerkeziKoaksiyel;
            ElvTvTopolojiServisi.TvUclariniLegacydenTamamla(proje.TvSistemi);
            foreach (var kalem in proje.OzelKutuphaneKalemleri.Where(k => k.AltBaslikKodu == "Tv"))
                kalem.AltBaslikKodu = "TvMerkezi";
        }

        proje.SemaSurum = ZayifAkimTekHatProje.GuncelSemaSurum;
        return new(true, eski, proje.SemaSurum,
            eski == ZayifAkimTekHatProje.GuncelSemaSurum
                ? "Şema sürümü güncel."
                : $"Şema {eski} → {proje.SemaSurum} sürümüne taşındı.");
    }

    /// <summary>Sürüm 1: eski "Cat6 UTP" kablo adı katalogdaki "Cat6 U/UTP" ile birleştirilir.</summary>
    private static void SurumeGecir1(ZayifAkimTekHatProje proje)
    {
        var tumRacklar = proje.DataSistemi.RackKabinler
            .Concat(proje.KameraSistemi.RackKabinler)
            .Concat(proje.AnonsSistemi.RackKabinler)
            .Concat(proje.TvSistemi.RackKabinler);

        foreach (var rack in tumRacklar)
        {
            rack.KatIciKabloTipi = KabloAdiniGuncelle(rack.KatIciKabloTipi);
            rack.MerkezeKabloTipi = KabloAdiniGuncelle(rack.MerkezeKabloTipi);
            foreach (var hat in rack.DataHatlari.Concat(rack.WirelessHatlari)
                         .Concat(rack.KameraHatlari).Concat(rack.TvHatlari)
                         .Concat(rack.IpTelefonHatlari).Concat(rack.YaziciHatlari)
                         .Concat(rack.ErisimKontrolHatlari).Concat(rack.DiafonHatlari)
                         .Concat(rack.DijitalAnonsUclari))
                hat.KabloTipi = KabloAdiniGuncelle(hat.KabloTipi);
        }
    }

    private static string KabloAdiniGuncelle(string kablo)
        => string.Equals(kablo?.Trim(), "Cat6 UTP", StringComparison.OrdinalIgnoreCase)
            ? "Cat6 U/UTP"
            : kablo ?? "";
}
