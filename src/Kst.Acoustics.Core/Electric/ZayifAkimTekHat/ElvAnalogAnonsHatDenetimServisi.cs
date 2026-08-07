namespace Kst.Acoustics.Core.Electric.ZayifAkimTekHat;

public enum ElvDenetimSorunSeviyesi
{
    Uyari,
    Hata
}

public sealed record ElvHatDenetimSorunu(
    ElvDenetimSorunSeviyesi Seviye,
    string ZonId,
    string HatId,
    string Mesaj);

public sealed record ElvHatDenetimRaporu(IReadOnlyList<ElvHatDenetimSorunu> Sorunlar)
{
    public bool Gecerli => Sorunlar.All(s => s.Seviye != ElvDenetimSorunSeviyesi.Hata);
    public int HataSayisi => Sorunlar.Count(s => s.Seviye == ElvDenetimSorunSeviyesi.Hata);
    public int UyariSayisi => Sorunlar.Count(s => s.Seviye == ElvDenetimSorunSeviyesi.Uyari);
}

/// <summary>100 V hoparlör hatlarının EOL, pilot ton, empedans, güzergâh ve arıza denetimi.</summary>
public static class ElvAnalogAnonsHatDenetimServisi
{
    public static ElvHatDenetimRaporu Denetle(ElvAnonsSistemi sistem)
    {
        var sorunlar = new List<ElvHatDenetimSorunu>();
        foreach (var zon in sistem.AnalogZonlar)
        {
            HattiDenetle(zon, zon.HatA, sorunlar);
            if (zon.HatB is not null)
                HattiDenetle(zon, zon.HatB, sorunlar);

            if (zon.CiftHatli && zon.HatB is not null
                && !string.IsNullOrWhiteSpace(zon.HatA.GuzergahKodu)
                && string.Equals(zon.HatA.GuzergahKodu, zon.HatB.GuzergahKodu, StringComparison.CurrentCultureIgnoreCase))
            {
                sorunlar.Add(new(ElvDenetimSorunSeviyesi.Hata, zon.Id, zon.HatB.Id,
                    "Hat A ve Hat B bağımsız güzergâhlardan geçirilmelidir."));
            }
        }
        return new(sorunlar);
    }

    private static void HattiDenetle(ElvAnonsZonu zon, ElvAnalogHoparlorHatti hat,
        List<ElvHatDenetimSorunu> sorunlar)
    {
        if (hat.ToplamHoparlor <= 0) return;

        var yontem = EtkinDenetimYontemi(hat);
        if (yontem == ElvHatDenetimYontemi.Yok)
            Hata("Aktif hoparlör hattında hat denetimi tanımlı değil.");

        bool eolGerekli = yontem is ElvHatDenetimYontemi.Eol or ElvHatDenetimYontemi.EolVeEmpedans;
        int dalSayisi = Math.Max(1, hat.DalSayisi);
        if (eolGerekli && hat.EolModulAdedi < dalSayisi)
            Hata($"EOL modülü yetersiz: {dalSayisi} dal için en az {dalSayisi}, tanımlı {hat.EolModulAdedi}.");

        if (yontem == ElvHatDenetimYontemi.PilotTon && hat.PilotTonFrekansiHz <= 0)
            Hata("Pilot ton denetimi için geçerli frekans tanımlanmalıdır.");

        if (!YanginaDayanimliKablo(hat.KabloTipi))
            Hata("Hoparlör hattı kablosu yangına dayanımlı FE180/NHXH/JE-H(St)H sınıfında değil.");

        if (hat.HatDurumu is ElvHoparlorHattiDurumu.AcikDevre
            or ElvHoparlorHattiDurumu.KisaDevre
            or ElvHoparlorHattiDurumu.ToprakKacagi)
            Hata($"Hat arızası: {HatDurumuMetni(hat.HatDurumu)}.");
        else if (hat.HatDurumu == ElvHoparlorHattiDurumu.Bilinmiyor)
            Uyari("Hat durumu henüz doğrulanmamış.");

        if (hat.ToplamHoparlor > 20 && !hat.KisaDevreIzolatoruVar)
            Uyari("20'den fazla hoparlör bulunan hatta kısa devre izolatörü değerlendirilmelidir.");

        void Hata(string mesaj) => sorunlar.Add(new(ElvDenetimSorunSeviyesi.Hata, zon.Id, hat.Id, mesaj));
        void Uyari(string mesaj) => sorunlar.Add(new(ElvDenetimSorunSeviyesi.Uyari, zon.Id, hat.Id, mesaj));
    }

    private static ElvHatDenetimYontemi EtkinDenetimYontemi(ElvAnalogHoparlorHatti hat)
    {
        if (hat.DenetimYontemi != ElvHatDenetimYontemi.Yok) return hat.DenetimYontemi;
        // Faz 1 alanlarıyla oluşturulmuş kayıtların geriye uyumluluğu.
        if (hat.EolDenetimiVar && hat.EmpedansDenetimiVar) return ElvHatDenetimYontemi.EolVeEmpedans;
        if (hat.EolDenetimiVar) return ElvHatDenetimYontemi.Eol;
        if (hat.EmpedansDenetimiVar) return ElvHatDenetimYontemi.Empedans;
        return ElvHatDenetimYontemi.Yok;
    }

    private static bool YanginaDayanimliKablo(string? kablo)
        => !string.IsNullOrWhiteSpace(kablo)
           && (kablo.Contains("FE180", StringComparison.OrdinalIgnoreCase)
               || kablo.Contains("NHXH", StringComparison.OrdinalIgnoreCase)
               || kablo.Contains("JE-H(St)H", StringComparison.OrdinalIgnoreCase));

    private static string HatDurumuMetni(ElvHoparlorHattiDurumu durum) => durum switch
    {
        ElvHoparlorHattiDurumu.AcikDevre => "açık devre",
        ElvHoparlorHattiDurumu.KisaDevre => "kısa devre",
        ElvHoparlorHattiDurumu.ToprakKacagi => "toprak kaçağı",
        _ => durum.ToString()
    };
}
