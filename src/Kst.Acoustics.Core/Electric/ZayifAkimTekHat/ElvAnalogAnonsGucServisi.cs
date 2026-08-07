namespace Kst.Acoustics.Core.Electric.ZayifAkimTekHat;

public sealed record ElvAmplifikatorGucSonucu(
    string AmplifikatorId,
    string Ad,
    bool Yedek,
    double HatYukuW,
    double RezervliGerekliGucW,
    double NominalGucW,
    int AtananHatSayisi,
    int KanalSayisi,
    bool KapasiteYeterli,
    bool KanalYeterli);

public sealed record ElvZonGucSonucu(string ZonId, string Ad, double HatAYukuW, double HatBYukuW, double ToplamYukW);

public sealed record ElvAnalogAnonsGucRaporu(
    double RezervYuzdesi,
    double ToplamHoparlorYukuW,
    double ToplamRezervliGucW,
    double GerekliYedekAmplifikatorGucuW,
    double MevcutYedekAmplifikatorGucuW,
    bool YedekKapasitesiYeterli,
    IReadOnlyList<ElvZonGucSonucu> Zonlar,
    IReadOnlyList<ElvAmplifikatorGucSonucu> Amplifikatorler,
    IReadOnlyList<string> Sorunlar)
{
    public bool Gecerli => Sorunlar.Count == 0;
}

/// <summary>Analog 100 V hoparlör yükü, amplifikatör rezervi ve N+1 yedek kapasitesi hesabı.</summary>
public static class ElvAnalogAnonsGucServisi
{
    public const double VarsayilanRezervYuzdesi = 20;
    public static IReadOnlyList<double> StandartAmplifikatorGucleriW { get; } = [120, 240, 480, 500, 1000];

    public static ElvAnalogAnonsGucRaporu Hesapla(ElvAnonsSistemi sistem, double rezervYuzdesi = VarsayilanRezervYuzdesi)
    {
        rezervYuzdesi = Math.Max(0, rezervYuzdesi);
        double katsayi = 1 + rezervYuzdesi / 100.0;
        var sorunlar = new List<string>();
        var zonSonuclari = new List<ElvZonGucSonucu>();
        var hatAtamalari = new List<(string? AmplifikatorId, ElvAnalogHoparlorHatti Hat, string ZonAdi)>();

        foreach (var zon in sistem.AnalogZonlar)
        {
            double a = zon.HatA.ToplamGucW;
            double b = zon.HatB?.ToplamGucW ?? 0;
            zonSonuclari.Add(new(zon.Id, zon.Ad, a, b, a + b));
            hatAtamalari.Add((zon.HatA.AmplifikatorId ?? zon.AmplifikatorId, zon.HatA, zon.Ad));
            if (zon.HatB is not null)
                hatAtamalari.Add((zon.HatB.AmplifikatorId ?? zon.AmplifikatorId, zon.HatB, zon.Ad));
        }

        var ampSonuclari = new List<ElvAmplifikatorGucSonucu>();
        foreach (var amp in sistem.AnalogAmplifikatorler)
        {
            var atananlar = hatAtamalari.Where(a => a.AmplifikatorId == amp.Id).ToList();
            double yuk = atananlar.Sum(a => a.Hat.ToplamGucW);
            double gerekli = amp.Yedek ? 0 : yuk * katsayi;
            bool kapasite = amp.Yedek || amp.NominalGucW + 0.001 >= gerekli;
            bool kanal = atananlar.Count <= Math.Max(0, amp.KanalSayisi);
            ampSonuclari.Add(new(amp.Id, amp.Ad, amp.Yedek, yuk, gerekli, amp.NominalGucW,
                atananlar.Count, amp.KanalSayisi, kapasite, kanal));
            if (!kapasite)
                sorunlar.Add($"{amp.Ad}: {gerekli:0.##} W rezervli yük, {amp.NominalGucW:0.##} W kapasiteyi aşıyor.");
            if (!kanal)
                sorunlar.Add($"{amp.Ad}: {atananlar.Count} hat atanmış, kanal kapasitesi {amp.KanalSayisi}.");
            if (amp.Yedek && atananlar.Count > 0)
                sorunlar.Add($"{amp.Ad}: yedek amplifikatöre normal işletme hattı atanamaz.");
        }

        var ampIdleri = sistem.AnalogAmplifikatorler.Select(a => a.Id).ToHashSet();
        foreach (var atama in hatAtamalari.Where(a => a.Hat.ToplamGucW > 0))
        {
            if (string.IsNullOrWhiteSpace(atama.AmplifikatorId))
                sorunlar.Add($"{atama.ZonAdi} / {atama.Hat.Ad}: amplifikatör atanmamış.");
            else if (!ampIdleri.Contains(atama.AmplifikatorId))
                sorunlar.Add($"{atama.ZonAdi} / {atama.Hat.Ad}: atanan amplifikatör bulunamadı.");
        }

        double gerekliYedek = ampSonuclari.Where(a => !a.Yedek).Select(a => a.RezervliGerekliGucW).DefaultIfEmpty(0).Max();
        double mevcutYedek = sistem.AnalogAmplifikatorler.Where(a => a.Yedek).Sum(a => Math.Max(0, a.NominalGucW));
        bool yedekYeterli = mevcutYedek + 0.001 >= gerekliYedek;
        if (!yedekYeterli && gerekliYedek > 0)
            sorunlar.Add($"N+1 yedek kapasitesi yetersiz: gerekli {gerekliYedek:0.##} W, mevcut {mevcutYedek:0.##} W.");

        double toplamYuk = zonSonuclari.Sum(z => z.ToplamYukW);
        return new(rezervYuzdesi, toplamYuk, toplamYuk * katsayi, gerekliYedek, mevcutYedek,
            yedekYeterli, zonSonuclari, ampSonuclari, sorunlar.Distinct().ToList());
    }

    public static double OnerilenStandartAmplifikatorGucu(double hoparlorYukuW, double rezervYuzdesi = VarsayilanRezervYuzdesi)
    {
        double gerekli = Math.Max(0, hoparlorYukuW) * (1 + Math.Max(0, rezervYuzdesi) / 100.0);
        return StandartAmplifikatorGucleriW.FirstOrDefault(g => g + 0.001 >= gerekli, Math.Ceiling(gerekli));
    }
}
