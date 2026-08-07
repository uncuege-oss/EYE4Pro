namespace Kst.Acoustics.Core.Electric.ZayifAkimTekHat;

/// <summary>100 V analog hoparlör hattının A veya B kolu.</summary>
public enum ElvAnonsHatKolu
{
    A,
    B
}

public enum ElvAnonsHatTopolojisi
{
    TekHat,
    CiftHatAB
}

public enum ElvHatDenetimYontemi
{
    Yok,
    Eol,
    PilotTon,
    Empedans,
    EolVeEmpedans
}

public enum ElvHoparlorHattiDurumu
{
    Bilinmiyor,
    Normal,
    AcikDevre,
    KisaDevre,
    ToprakKacagi
}

/// <summary>Aynı tip ve güç tap değerindeki analog 100 V hoparlör grubu.</summary>
public sealed class ElvAnalogHoparlorGrubu
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Ad { get; set; } = "Hoparlör Grubu";
    public string KatalogKodu { get; set; } = "";
    public string HoparlorTipi { get; set; } = "Tavan Tipi 100 V";
    public int Adet { get; set; }
    public double TapGucuW { get; set; } = 6;
    public bool En54_24Uygun { get; set; }
    public string MahalNotu { get; set; } = "";

    public double ToplamGucW => Math.Max(0, Adet) * Math.Max(0, TapGucuW);
}

/// <summary>100 V hattında panel/grup başlangıcı ile her hoparlör arasındaki fiziksel kablo parçası.</summary>
public sealed class ElvAnalogKabloSegmenti
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int SiraNo { get; set; }
    public string HoparlorGrupId { get; set; } = "";
    public int GrupIciHoparlorNo { get; set; }
    public string Baslangic { get; set; } = "";
    public string Bitis { get; set; } = "";
    public double Metraj { get; set; }
}

/// <summary>Bir tahliye zonunun denetlenen analog 100 V hoparlör hattı.</summary>
public sealed class ElvAnalogHoparlorHatti
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Ad { get; set; } = "Hat A";
    public ElvAnonsHatKolu Kol { get; set; } = ElvAnonsHatKolu.A;
    public string? AmplifikatorId { get; set; }
    public string KabloTipi { get; set; } = "2x1.5 mm² FE180";
    public double Metraj { get; set; }
    public bool EolDenetimiVar { get; set; }
    public bool EmpedansDenetimiVar { get; set; }
    public bool KisaDevreIzolatoruVar { get; set; }
    public ElvHatDenetimYontemi DenetimYontemi { get; set; } = ElvHatDenetimYontemi.Yok;
    public int DalSayisi { get; set; } = 1;
    public int EolModulAdedi { get; set; }
    public double PilotTonFrekansiHz { get; set; } = 20_000;
    public string GuzergahKodu { get; set; } = "";
    public ElvHoparlorHattiDurumu HatDurumu { get; set; } = ElvHoparlorHattiDurumu.Bilinmiyor;
    public DateTime? SonDenetimTarihi { get; set; }
    public List<ElvAnalogHoparlorGrubu> HoparlorGruplari { get; set; } = new();
    public List<ElvAnalogKabloSegmenti> KabloSegmentleri { get; set; } = new();

    public int ToplamHoparlor => HoparlorGruplari.Sum(g => Math.Max(0, g.Adet));
    public double ToplamGucW => HoparlorGruplari.Sum(g => g.ToplamGucW);
    public double SegmentToplamMetraj => KabloSegmentleri.Sum(s => Math.Max(0, s.Metraj));
}

/// <summary>Tahliye senaryosunda birlikte kontrol edilen analog anons zonu.</summary>
public sealed class ElvAnonsZonu
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Ad { get; set; } = "Tahliye Zonu";
    public int KatNo { get; set; }
    public string KatAdi { get; set; } = "";
    public string? AmplifikatorId { get; set; }
    public ElvAnonsHatTopolojisi Topoloji { get; set; } = ElvAnonsHatTopolojisi.TekHat;
    public ElvAnalogHoparlorHatti HatA { get; set; } = new();
    public ElvAnalogHoparlorHatti? HatB { get; set; }

    // Eski/elle oluşturulmuş kayıtlarda Topoloji alanı bulunmasa bile Hat B varsa çift hat kabul edilir.
    public bool CiftHatli => HatB is not null;
    public int ToplamHoparlor => HatA.ToplamHoparlor + (HatB?.ToplamHoparlor ?? 0);
    public double ToplamGucW => HatA.ToplamGucW + (HatB?.ToplamGucW ?? 0);
}

/// <summary>Ana veya yedek 100 V güç amplifikatörü.</summary>
public sealed class ElvAnonsAmplifikatoru
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Ad { get; set; } = "100 V Güç Amplifikatörü";
    public double NominalGucW { get; set; } = 240;
    public bool Yedek { get; set; }
    public int KanalSayisi { get; set; } = 1;
}

/// <summary>Yangın paneli tetikleriyle çalışan tahliye mesajı ve zon senaryosu.</summary>
public sealed class ElvAnonsMesajSenaryosu
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Ad { get; set; } = "Genel Tahliye";
    public int Oncelik { get; set; } = 1;
    public string MesajAdi { get; set; } = "Tahliye Mesajı";
    public List<string> ZonIds { get; set; } = new();
    public string YanginPaneliTetikAdi { get; set; } = "";
}

/// <summary>EN 54-4 kapsamındaki ana ve ikincil güç kaynağı bilgileri.</summary>
public sealed class ElvAnonsGucKaynagi
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Ad { get; set; } = "Acil Anons Güç Kaynağı";
    public double NominalGerilimV { get; set; } = 24;
    public double AkuKapasitesiAh { get; set; }
    public int AkuAdedi { get; set; } = 2;
    public bool En54_4Uygun { get; set; }

    // ── EN 54-4 akü boyutlandırma girdileri (eski dosyalarda yoktur; varsayılanla okunur) ──
    /// <summary>Bekleme (quiescent) akımı; santral + denetim devreleri.</summary>
    public double BeklemeAkimiA { get; set; } = 0.5;
    /// <summary>Bekleme süresi. Türkiye yangın yönetmeliği pratiği 24 saattir.</summary>
    public double BeklemeSuresiSaat { get; set; } = 24;
    /// <summary>Alarm (tahliye anonsu) süresi. Tipik 30 dakika.</summary>
    public double AlarmSuresiDakika { get; set; } = 30;
}
