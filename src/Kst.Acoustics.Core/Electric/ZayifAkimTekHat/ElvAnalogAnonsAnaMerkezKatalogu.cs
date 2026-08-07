namespace Kst.Acoustics.Core.Electric.ZayifAkimTekHat;

public enum ElvAnalogMerkezCihazKategorisi
{
    Kontrol,
    Amplifikator,
    Mikrofon,
    MesajVeSes,
    HatDenetimi,
    YanginEntegrasyonu,
    GucVeAku,
    IzlemeVeRack
}

public sealed record ElvAnalogMerkezKatalogKalemi(
    string Kod,
    string Ad,
    ElvAnalogMerkezCihazKategorisi Kategori,
    string Standard,
    double NominalGucW = 0,
    int KanalVeyaZonSayisi = 0,
    bool YedekOlabilir = false,
    bool TemelSistemKalemi = false);

/// <summary>Analog 100 V acil anons ana merkezi için üreticiden bağımsız cihaz kataloğu.</summary>
public static class ElvAnalogAnonsAnaMerkezKatalogu
{
    public static IReadOnlyList<ElvAnalogMerkezKatalogKalemi> Kalemler { get; } =
    [
        new("CTRL-2Z", "EN 54-16 Anons Kontrol Ünitesi 2 Zon", ElvAnalogMerkezCihazKategorisi.Kontrol, "EN 54-16", KanalVeyaZonSayisi: 2, TemelSistemKalemi: true),
        new("CTRL-4Z", "EN 54-16 Anons Kontrol Ünitesi 4 Zon", ElvAnalogMerkezCihazKategorisi.Kontrol, "EN 54-16", KanalVeyaZonSayisi: 4, TemelSistemKalemi: true),
        new("CTRL-6Z", "EN 54-16 Anons Kontrol Ünitesi 6 Zon", ElvAnalogMerkezCihazKategorisi.Kontrol, "EN 54-16", KanalVeyaZonSayisi: 6, TemelSistemKalemi: true),
        new("CTRL-8Z", "EN 54-16 Anons Kontrol Ünitesi 8 Zon", ElvAnalogMerkezCihazKategorisi.Kontrol, "EN 54-16", KanalVeyaZonSayisi: 8, TemelSistemKalemi: true),
        new("CTRL-16Z", "EN 54-16 Anons Kontrol Ünitesi 16 Zon", ElvAnalogMerkezCihazKategorisi.Kontrol, "EN 54-16", KanalVeyaZonSayisi: 16, TemelSistemKalemi: true),
        new("ROUTER-6Z", "Zon Yönlendirici 6 Zon / 12 Hat", ElvAnalogMerkezCihazKategorisi.Kontrol, "EN 54-16", KanalVeyaZonSayisi: 6),
        new("ROUTER-12Z", "Zon Yönlendirici 12 Zon / 24 Hat", ElvAnalogMerkezCihazKategorisi.Kontrol, "EN 54-16", KanalVeyaZonSayisi: 12),

        new("AMP-120", "100 V Güç Amplifikatörü 120 W", ElvAnalogMerkezCihazKategorisi.Amplifikator, "EN 54-16 sistem bileşeni", 120, 1, true),
        new("AMP-240", "100 V Güç Amplifikatörü 240 W", ElvAnalogMerkezCihazKategorisi.Amplifikator, "EN 54-16 sistem bileşeni", 240, 1, true),
        new("AMP-480", "100 V Güç Amplifikatörü 480 W", ElvAnalogMerkezCihazKategorisi.Amplifikator, "EN 54-16 sistem bileşeni", 480, 1, true),
        new("AMP-500", "100 V Güç Amplifikatörü 500 W", ElvAnalogMerkezCihazKategorisi.Amplifikator, "EN 54-16 sistem bileşeni", 500, 1, true),
        new("AMP-1000", "100 V Güç Amplifikatörü 1000 W", ElvAnalogMerkezCihazKategorisi.Amplifikator, "EN 54-16 sistem bileşeni", 1000, 1, true),
        new("AMP-4X120", "4 Kanallı 100 V Güç Amplifikatörü 4x120 W", ElvAnalogMerkezCihazKategorisi.Amplifikator, "EN 54-16 sistem bileşeni", 480, 4, true),
        new("AMP-4X250", "4 Kanallı 100 V Güç Amplifikatörü 4x250 W", ElvAnalogMerkezCihazKategorisi.Amplifikator, "EN 54-16 sistem bileşeni", 1000, 4, true),

        new("MIC-EMG", "Denetlenen Acil Anons Mikrofonu", ElvAnalogMerkezCihazKategorisi.Mikrofon, "EN 54-16", TemelSistemKalemi: true),
        new("MIC-FIRE", "İtfaiyeci Mikrofon Paneli", ElvAnalogMerkezCihazKategorisi.Mikrofon, "EN 54-16"),
        new("MIC-CALL", "Zon Seçimli Anons Mikrofonu", ElvAnalogMerkezCihazKategorisi.Mikrofon, "EN 54-16 sistem bileşeni"),
        new("MIC-EXT", "Mikrofon Tuş Takımı Genişleme Modülü", ElvAnalogMerkezCihazKategorisi.Mikrofon, "EN 54-16 sistem bileşeni"),

        new("MSG-MAN", "Denetlenen Mesaj Yöneticisi", ElvAnalogMerkezCihazKategorisi.MesajVeSes, "EN 54-16", TemelSistemKalemi: true),
        new("DSP", "Dijital Ses İşlemcisi (DSP)", ElvAnalogMerkezCihazKategorisi.MesajVeSes, "EN 54-16 sistem bileşeni"),
        new("MIXER", "Rack Tipi Mikser / Ses Matrisi", ElvAnalogMerkezCihazKategorisi.MesajVeSes, ""),
        new("BGM", "Arka Plan Müzik Kaynağı", ElvAnalogMerkezCihazKategorisi.MesajVeSes, ""),

        new("LINE-SUP", "Hoparlör Hat Denetim Modülü", ElvAnalogMerkezCihazKategorisi.HatDenetimi, "EN 54-16", TemelSistemKalemi: true),
        new("EOL-MON", "EOL İzleme Giriş Modülü", ElvAnalogMerkezCihazKategorisi.HatDenetimi, "EN 54-16"),
        new("IMP-MON", "Hat Empedans İzleme Modülü", ElvAnalogMerkezCihazKategorisi.HatDenetimi, "EN 54-16"),
        new("AUDIO-MON", "16 Kanal Ses/Hat Monitor Paneli", ElvAnalogMerkezCihazKategorisi.HatDenetimi, ""),

        new("FACP-IO", "Yangın Paneli Denetimli Giriş/Çıkış Modülü", ElvAnalogMerkezCihazKategorisi.YanginEntegrasyonu, "EN 54-16", TemelSistemKalemi: true),
        new("RELAY-IO", "Programlanabilir Röle I/O Modülü", ElvAnalogMerkezCihazKategorisi.YanginEntegrasyonu, "EN 54-16 sistem bileşeni"),
        new("EVAC-TRIGGER", "Tahliye Senaryosu Tetik Modülü", ElvAnalogMerkezCihazKategorisi.YanginEntegrasyonu, "EN 54-16"),

        new("PSU-24", "EN 54-4 24 V Güç Kaynağı ve Şarj Ünitesi", ElvAnalogMerkezCihazKategorisi.GucVeAku, "EN 54-4", TemelSistemKalemi: true),
        new("BAT-12-17", "12 V 17 Ah Akü", ElvAnalogMerkezCihazKategorisi.GucVeAku, "EN 54-4 sistem hesabı"),
        new("BAT-12-26", "12 V 26 Ah Akü", ElvAnalogMerkezCihazKategorisi.GucVeAku, "EN 54-4 sistem hesabı"),
        new("BAT-12-40", "12 V 40 Ah Akü", ElvAnalogMerkezCihazKategorisi.GucVeAku, "EN 54-4 sistem hesabı"),
        new("BAT-CAB", "Akü Kabini", ElvAnalogMerkezCihazKategorisi.GucVeAku, ""),
        new("UPS", "Online UPS", ElvAnalogMerkezCihazKategorisi.GucVeAku, ""),

        new("PDU", "PDU (Güç Dağıtım)", ElvAnalogMerkezCihazKategorisi.IzlemeVeRack, ""),
        new("FAN", "Rack Fan Ünitesi", ElvAnalogMerkezCihazKategorisi.IzlemeVeRack, ""),
        new("ENV", "Rack Sıcaklık/Nem İzleme Sensörü", ElvAnalogMerkezCihazKategorisi.IzlemeVeRack, ""),
        new("KABLO", "Kablo Düzenleyici", ElvAnalogMerkezCihazKategorisi.IzlemeVeRack, ""),
        new("RAF", "Rack Rafı", ElvAnalogMerkezCihazKategorisi.IzlemeVeRack, ""),
        new("KOR-1U", "Kör Panel 1U", ElvAnalogMerkezCihazKategorisi.IzlemeVeRack, "")
    ];

    public static ElvAnalogMerkezKatalogKalemi? Bul(string? kod)
        => Kalemler.FirstOrDefault(k => string.Equals(k.Kod, kod, StringComparison.OrdinalIgnoreCase));

    public static ElvCihaz CihazOlustur(string kod, int adet = 1)
    {
        var kalem = Bul(kod) ?? throw new ArgumentException("Analog anons ana merkez katalog kodu bulunamadı.", nameof(kod));
        return new ElvCihaz { KatalogKodu = kalem.Kod, Ad = kalem.Ad, Adet = Math.Max(1, adet) };
    }

    public static ElvAnonsAmplifikatoru AmplifikatorOlustur(string kod, bool yedek = false)
    {
        var kalem = Bul(kod);
        if (kalem is null || kalem.Kategori != ElvAnalogMerkezCihazKategorisi.Amplifikator)
            throw new ArgumentException("Seçilen katalog kalemi amplifikatör değil.", nameof(kod));
        return new ElvAnonsAmplifikatoru
        {
            Ad = yedek ? $"Yedek {kalem.Ad}" : kalem.Ad,
            NominalGucW = kalem.NominalGucW,
            KanalSayisi = Math.Max(1, kalem.KanalVeyaZonSayisi),
            Yedek = yedek
        };
    }
}
