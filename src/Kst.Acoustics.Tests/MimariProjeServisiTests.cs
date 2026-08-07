using System;
using System.IO;
using Kst.Acoustics.Core.Architecture.Models;
using Kst.Acoustics.Core.Architecture.Services;

namespace Kst.Acoustics.Tests;

public static class MimariProjeServisiTests
{
    public static void E4MimariRoundTrip()
    {
        var yol = Path.Combine(Path.GetTempPath(), $"e4mimari-test-{Guid.NewGuid():N}.e4mimari.json");
        try
        {
            var proje = MimariProjeServisi.YeniOlustur("Mimari Test");
            proje.IhtiyacProgrami.BinaIslevi = BinaIslevi.KonferansVeToplanti;
            proje.IhtiyacProgrami.ProjeTipi = "Konferans merkezi";
            proje.IhtiyacProgrami.KullaniciKapasitesi = 250;
            proje.IhtiyacProgrami.KatSayisi = 2;
            proje.IhtiyacProgrami.SirkulasyonOrani = 0.22;
            proje.IhtiyacProgrami.TeknikAlanOrani = 0.08;

            proje.IhtiyacProgrami.Mahaller.Add(new MahalGereksinimi
            {
                Id = "mah-01",
                MahalTipi = MahalTipi.KonferansSalonu,
                KullanimSinifi = KullanimSinifi.AnaKullanim,
                Ad = "Ana Konferans Salonu",
                Adet = 1,
                KisiKapasitesi = 250,
                MinimumAlanM2 = 250,
                IdealAlanM2 = 320,
                HedefAlanM2 = 340,
                MinimumNetYukseklikM = 4.5,
                HedefNetYukseklikM = 5.0
            });

            proje.MahalSablonlari.Add(new MahalSablonu
            {
                Kod = "KONF-ANA",
                VarsayilanAd = "Konferans Salonu",
                MahalTipi = MahalTipi.KonferansSalonu,
                KullanimSinifi = KullanimSinifi.AnaKullanim,
                UygunBinaIslevleri = [BinaIslevi.Kulturel, BinaIslevi.KonferansVeToplanti],
                VarsayilanAdet = 1,
                MinimumAlanM2 = 200,
                IdealAlanM2 = 320,
                MinimumNetYukseklikM = 4.5,
                KisiBasinaAlanM2 = 1.2,
                DisCepheGerekli = false
            });

            MimariProjeServisi.Kaydet(proje, yol);

            if (!File.Exists(yol))
                throw new InvalidOperationException("Mimari proje dosyası oluşturulmadı.");
            if (proje.SemaSurum != MimariProjeServisi.GuncelSemaSurum)
                throw new InvalidOperationException("Kaydet sonrası SemaSurum güncel olmalı.");

            var acilan = MimariProjeServisi.Ac(yol);

            if (acilan.ProjeAdi != "Mimari Test")
                throw new InvalidOperationException($"ProjeAdi uyuşmuyor: {acilan.ProjeAdi}");
            if (acilan.DosyaYolu != yol)
                throw new InvalidOperationException("DosyaYolu yükleme sonrası set edilmeli.");
            if (acilan.SemaSurum != MimariProjeServisi.GuncelSemaSurum)
                throw new InvalidOperationException("SemaSurum yükleme sonrası güncel olmalı.");
            if (acilan.IhtiyacProgrami.BinaIslevi != BinaIslevi.KonferansVeToplanti)
                throw new InvalidOperationException("BinaIslevi round-trip sonrası korunmalı.");
            if (acilan.IhtiyacProgrami.Mahaller.Count != 1)
                throw new InvalidOperationException($"1 mahal gereksinimi beklenir; {acilan.IhtiyacProgrami.Mahaller.Count} bulundu.");
            if (acilan.MahalSablonlari.Count != 1)
                throw new InvalidOperationException($"1 mahal şablonu beklenir; {acilan.MahalSablonlari.Count} bulundu.");

            var mahal = acilan.IhtiyacProgrami.Mahaller[0];
            if (mahal.Id != "mah-01" || mahal.MahalTipi != MahalTipi.KonferansSalonu)
                throw new InvalidOperationException("Mahal gereksinimi kimliği veya tipi korunmadı.");
            if (Math.Abs(mahal.ToplamHedefHacimM3 - 1700.0) > 1e-9)
                throw new InvalidOperationException($"Mahal hacmi beklenen değerde değil: {mahal.ToplamHedefHacimM3}");

            var sablon = acilan.MahalSablonlari[0];
            if (sablon.UygunBinaIslevleri.Count != 2)
                throw new InvalidOperationException("Şablon bina işlevleri korunmalı.");
            if (!sablon.UygunBinaIslevleri.Contains(BinaIslevi.Kulturel))
                throw new InvalidOperationException("Şablon Kulturel bina işlevini korumalı.");
        }
        finally
        {
            if (File.Exists(yol)) File.Delete(yol);
        }
    }
}
