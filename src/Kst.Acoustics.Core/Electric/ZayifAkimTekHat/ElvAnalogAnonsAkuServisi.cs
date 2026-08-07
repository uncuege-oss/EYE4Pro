namespace Kst.Acoustics.Core.Electric.ZayifAkimTekHat;

public sealed record ElvAkuHesapSonucu(
    double BeklemeAkimiA,
    double AlarmAkimiA,
    double GerekliAh,
    double MevcutAh,
    bool Yeterli,
    IReadOnlyList<string> Sorunlar);

/// <summary>
/// EN 54-4 ikincil güç kaynağı (akü) ön boyutlandırması.
/// Gerekli kapasite = (bekleme akımı × bekleme süresi + alarm akımı × alarm süresi) × yaşlanma katsayısı.
/// Alarm akımı amplifikatör rezervli yükünden türetilir (sınıf-D verim varsayımı).
/// Bu bir ön kontroldür; kesin hesap üretici akım değerleriyle yapılır.
/// </summary>
public static class ElvAnalogAnonsAkuServisi
{
    /// <summary>EN 54-4 pratiğinde akü yaşlanma/emniyet katsayısı.</summary>
    public const double YaslanmaKatsayisi = 1.25;

    /// <summary>Amplifikatör DC çekişi için varsayılan verim (sınıf-D ~%65 sistem verimi).</summary>
    public const double AmplifikatorVerimi = 0.65;

    public static ElvAkuHesapSonucu Hesapla(ElvAnonsSistemi sistem, double rezervYuzdesi = ElvAnalogAnonsGucServisi.VarsayilanRezervYuzdesi)
    {
        var kaynak = sistem.AnalogGucKaynagi;
        var sorunlar = new List<string>();

        double gerilim = kaynak.NominalGerilimV > 0 ? kaynak.NominalGerilimV : 24;
        var gucRaporu = ElvAnalogAnonsGucServisi.Hesapla(sistem, rezervYuzdesi);
        double alarmAkimi = gucRaporu.ToplamRezervliGucW / (AmplifikatorVerimi * gerilim);
        double beklemeAkimi = Math.Max(0, kaynak.BeklemeAkimiA);

        double gerekliAh = (beklemeAkimi * Math.Max(0, kaynak.BeklemeSuresiSaat)
                            + alarmAkimi * Math.Max(0, kaynak.AlarmSuresiDakika) / 60.0)
                           * YaslanmaKatsayisi;

        // 12 V aküler 24 V sistemde seri çift çalışır; paralel set sayısı kapasiteyi belirler.
        int setSayisi = gerilim >= 24 ? kaynak.AkuAdedi / 2 : Math.Max(0, kaynak.AkuAdedi);
        double mevcutAh = Math.Max(0, kaynak.AkuKapasitesiAh) * setSayisi;

        bool yeterli = mevcutAh + 0.001 >= gerekliAh;

        if (sistem.AnalogZonlar.Count > 0 && kaynak.AkuKapasitesiAh <= 0)
            sorunlar.Add("EN 54-4 akü kapasitesi tanımlanmamış.");
        else if (!yeterli && gerekliAh > 0)
            sorunlar.Add($"Akü kapasitesi yetersiz: gerekli {gerekliAh:0.#} Ah, mevcut {mevcutAh:0.#} Ah.");
        if (gerilim >= 24 && kaynak.AkuAdedi % 2 != 0)
            sorunlar.Add("24 V sistemde akü adedi çift olmalıdır (12 V aküler seri çift çalışır).");
        if (sistem.AnalogZonlar.Count > 0 && !kaynak.En54_4Uygun)
            sorunlar.Add("Güç kaynağının EN 54-4 uygunluğu onaylanmamış.");

        return new(beklemeAkimi, alarmAkimi, gerekliAh, mevcutAh, yeterli, sorunlar);
    }
}
