namespace Kst.Acoustics.Core.Electric.ZayifAkimTekHat;

/// <summary>
/// Rack kabin veya ana merkez içinde bulunan bir cihaz kalemi.
/// Ad ya hazır <see cref="ElvCihazKatalogu"/>'ndan seçilir ya da manuel girilir.
/// </summary>
public sealed class ElvCihaz
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string KatalogKodu { get; set; } = "";
    public string Ad { get; set; } = "";
    public int Adet { get; set; } = 1;
}

/// <summary>
/// Rack kabinden çıkan tekil zayıf akım hattı. Her data, wireless veya kamera noktası
/// ayrı bir satır olarak metrajlandırılır.
/// </summary>
public sealed class ElvHatMetraj
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int HatNo { get; set; }
    public string Ad { get; set; } = "";
    public string KabloTipi { get; set; } = "Cat6 U/UTP";
    public double Metraj { get; set; }
}

/// <summary>
/// Acil anons hoparlör hattı. Hat üzerindeki hoparlörler birbirinden geçişli
/// ilerlediği için her hattın hoparlör sayısı ayrıca girilir.
/// </summary>
public sealed class ElvAnonsHatti
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int HatNo { get; set; }
    public string Ad { get; set; } = "";
    public int HoparlorSayisi { get; set; }
    public string KabloTipi { get; set; } = "2x1.5 mm² FE180";
    public double Metraj { get; set; }
}

/// <summary>
/// TV sisteminde ana santralden çatıdaki uydu/antenlere çıkan koaksiyel hat.
/// TV sisteminde çatı uydusu her zaman vardır.
/// </summary>
public sealed class ElvTvCatiHatti
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int HatNo { get; set; }
    public string Ad { get; set; } = "";
    public string KabloTipi { get; set; } = "RG-11/U Koaksiyel";
    public double Metraj { get; set; }
}

/// <summary>
/// Yangın algılama loop hattı. Kat panosu olmadan doğrudan yangın algılama
/// santraline gider; aynı katta birden fazla loop olabilir.
/// </summary>
public enum ElvYanginCihazTipi
{
    DumanDedektoru,
    IsiDedektoru,
    KombineDedektor,
    AsmaTavanDedektoru,
    YanginButonu,
    Siren,
    Flasor,
    Bilinmeyen
}

/// <summary>
/// Loop üzerindeki fiziksel cihaz. <see cref="OncekiCihazaMesafeMetre"/> ilk cihazda
/// santral/loop başlangıcından, diğerlerinde önceki cihazdan gelen kablo segmentidir.
/// </summary>
public sealed class ElvYanginLoopCihazi
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public ElvYanginCihazTipi Tip { get; set; }
    public string Ad { get; set; } = "";
    public double OncekiCihazaMesafeMetre { get; set; }
}

public sealed class ElvYanginLoop
{
    private int legacyCihazSayisi;
    private double legacyMetraj;

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int LoopNo { get; set; }
    public string Ad { get; set; } = "";
    public List<ElvYanginLoopCihazi> Cihazlar { get; set; } = new();
    public int CihazSayisi
    {
        get => Cihazlar.Count > 0 ? Cihazlar.Count : legacyCihazSayisi;
        set => legacyCihazSayisi = Math.Max(0, value);
    }
    public string KabloTipi { get; set; } = "J-Y(St)Y 1x2x0.8 Yangın Kablosu";
    public double Metraj
    {
        get => Cihazlar.Count > 0 ? Cihazlar.Sum(c => Math.Max(0, c.OncekiCihazaMesafeMetre)) : legacyMetraj;
        set => legacyMetraj = Math.Max(0, value);
    }
}

/// <summary>Bir kattaki yangın algılama loop grupları. Kat panosu/rack içermez.</summary>
public sealed class ElvYanginKatLoopGrubu
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Ad { get; set; } = "Yangın Loop Grubu";
    public int KatNo { get; set; }
    public string KatAdi { get; set; } = "";
    public int LoopSayisi { get; set; }
    public List<ElvYanginLoop> Looplar { get; set; } = new();

    public int ToplamCihaz => Looplar.Sum(l => l.CihazSayisi);
    public double ToplamLoopMetraj => Looplar.Sum(l => l.Metraj);
}

/// <summary>
/// Bir kattaki rack kabin. Kat içindeki data hatlarını ve wireless cihazlarını toplar,
/// kablo ile ana merkeze bağlanır. İçinde hangi cihazların bulunduğu (<see cref="Cihazlar"/>)
/// bu modülün en kritik bilgisidir.
/// </summary>
public sealed class ElvRackKabin
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Ad { get; set; } = "Rack Kabin";

    public int KatNo { get; set; }
    public string KatAdi { get; set; } = "";

    /// <summary>
    /// Bu rack'in bağlandığı üst düğüm. <c>null</c> ise doğrudan Ana Merkez'e bağlanır;
    /// aksi halde başka bir rack'in <see cref="Id"/>'sidir (kabinden kabine atlama / daisy-chain).
    /// </summary>
    public string? UstRackId { get; set; }

    // ── Kat içi cihaz sayıları (M12'den veya manuel) ──
    public int DataHatSayisi { get; set; }
    public int WirelessSayisi { get; set; }
    public int DahiliKameraSayisi { get; set; }
    public int HariciKameraSayisi { get; set; }
    public int PtzKameraSayisi { get; set; }

    /// <summary>Üç kamera tipinin toplamı. Setter eski projelerdeki tek kamera sayısını dahili kameraya taşır.</summary>
    public int KameraSayisi
    {
        get => DahiliKameraSayisi + HariciKameraSayisi + PtzKameraSayisi;
        set
        {
            if (KameraSayisi == 0)
                DahiliKameraSayisi = Math.Max(0, value);
        }
    }
    public int AnonsHatSayisi { get; set; }
    public int TvPrizSayisi { get; set; }

    // ── Telefon sistemi (analog santral): her priz ayrı hat ile santrale iner ──
    public int TelefonPrizSayisi { get; set; }

    // ── Diafon sistemi: kat üniteleri (daire diafonu + kapı zili/panel) santrale hat çeker ──
    // Not: Data'daki DiafonSayisi/DiafonHatlari IP tabanlı ek uçlardır; bunlar analog diafon sistemidir.
    public int DiafonUniteSayisi { get; set; }

    // ── Data sistemi ek uç birim hatları (manuel; Wireless gibi M12'den türetilmez) ──
    public int IpTelefonSayisi { get; set; }
    public int YaziciSayisi { get; set; }
    public int ErisimKontrolSayisi { get; set; }
    public int DiafonSayisi { get; set; }

    // ── Dijital (PoE) acil anons uçları — her uç için ayrı Cat hattı ──
    public int IpTavanHoparlorSayisi { get; set; }
    public int IpDuvarHoparlorSayisi { get; set; }
    public int IpHornHoparlorSayisi { get; set; }
    public int IpProjektorHoparlorSayisi { get; set; }
    public int IpAnonsTerminaliSayisi { get; set; }

    // ── Rack kabinden çıkan tekil hat metrajları ──
    public List<ElvHatMetraj> DataHatlari { get; set; } = new();
    public List<ElvHatMetraj> WirelessHatlari { get; set; } = new();
    public List<ElvHatMetraj> KameraHatlari { get; set; } = new();
    public List<ElvAnonsHatti> AnonsHatlari { get; set; } = new();
    public List<ElvHatMetraj> TvHatlari { get; set; } = new();
    public List<ElvHatMetraj> IpTelefonHatlari { get; set; } = new();
    public List<ElvHatMetraj> YaziciHatlari { get; set; } = new();
    public List<ElvHatMetraj> ErisimKontrolHatlari { get; set; } = new();
    public List<ElvHatMetraj> DiafonHatlari { get; set; } = new();
    public List<ElvHatMetraj> DijitalAnonsUclari { get; set; } = new();
    public List<ElvHatMetraj> TelefonHatlari { get; set; } = new();
    public List<ElvHatMetraj> DiafonUniteHatlari { get; set; } = new();

    // ── Kat içi kablo ──
    public string KatIciKabloTipi { get; set; } = "Cat6 U/UTP";

    /// <summary>
    /// Rack'ten kat içindeki uçlara giden bütün tekil hatların otomatik toplamı.
    /// Setter yalnız eski .e4p13 dosyalarındaki artık kullanılmayan manuel değerin
    /// geriye uyumlu biçimde okunabilmesi için tutulur.
    /// </summary>
    public double KatIciKabloMetraj
    {
        get => ToplamDataHatMetraj
               + ToplamWirelessHatMetraj
               + ToplamKameraHatMetraj
               + ToplamAnonsHatMetraj
               + ToplamTvHatMetraj
               + ToplamIpTelefonHatMetraj
               + ToplamYaziciHatMetraj
               + ToplamErisimKontrolHatMetraj
               + ToplamDiafonHatMetraj
               + ToplamDijitalAnonsHatMetraj
               + ToplamTelefonHatMetraj
               + ToplamDiafonUniteHatMetraj;
        set { /* Eski manuel değer artık tekil hat metrajlarından hesaplanır. */ }
    }

    // ── Rack kabin → Ana Merkez kablosu (manuel) ──
    public string MerkezeKabloTipi { get; set; } = "Cat6 U/UTP";
    public double MerkezeKabloMetraj { get; set; }

    // ── Rack kabin içindeki cihazlar (katalog + manuel) ──
    public List<ElvCihaz> Cihazlar { get; set; } = new();

    /// <summary>Rack içindeki toplam cihaz adedi (kalem değil, adet toplamı).</summary>
    public int CihazAdetToplam => Cihazlar.Sum(c => c.Adet);

    /// <summary>Data sistemi ek uç birim (IP telefon + yazıcı + erişim/PDKS + diafon) toplam sayısı.</summary>
    public int DataEkUcSayisi => IpTelefonSayisi + YaziciSayisi + ErisimKontrolSayisi + DiafonSayisi;
    public double ToplamDataHatMetraj => DataHatlari.Sum(h => h.Metraj);
    public double ToplamWirelessHatMetraj => WirelessHatlari.Sum(h => h.Metraj);
    public double ToplamKameraHatMetraj => KameraHatlari.Sum(h => h.Metraj);
    public int ToplamAnonsHoparlor => AnonsHatlari.Sum(h => h.HoparlorSayisi);
    public double ToplamAnonsHatMetraj => AnonsHatlari.Sum(h => h.Metraj);
    public double ToplamTvHatMetraj => TvHatlari.Sum(h => h.Metraj);
    public double ToplamIpTelefonHatMetraj => IpTelefonHatlari.Sum(h => h.Metraj);
    public double ToplamYaziciHatMetraj => YaziciHatlari.Sum(h => h.Metraj);
    public double ToplamErisimKontrolHatMetraj => ErisimKontrolHatlari.Sum(h => h.Metraj);
    public double ToplamDiafonHatMetraj => DiafonHatlari.Sum(h => h.Metraj);
    public int ToplamDijitalAnonsUcu => IpTavanHoparlorSayisi + IpDuvarHoparlorSayisi
        + IpHornHoparlorSayisi + IpProjektorHoparlorSayisi + IpAnonsTerminaliSayisi;
    public double ToplamDijitalAnonsHatMetraj => DijitalAnonsUclari.Sum(h => h.Metraj);
    public double ToplamTelefonHatMetraj => TelefonHatlari.Sum(h => h.Metraj);
    public double ToplamDiafonUniteHatMetraj => DiafonUniteHatlari.Sum(h => h.Metraj);
}

/// <summary>
/// Tüm rack kabinlerin kablo ile bağlandığı bina ana merkezi.
/// İçindeki cihazlar (core switch, router, sunucu vb.) ayrıca tanımlanır.
/// </summary>
public sealed class ElvMerkez
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Ad { get; set; } = "Ana Merkez";
    public int? KatNo { get; set; } = 0;
    public List<ElvCihaz> Cihazlar { get; set; } = new();

    public int CihazAdetToplam => Cihazlar.Sum(c => c.Adet);
}

/// <summary>
/// Data sistemi: kat rack kabinleri + bina ana merkezi.
/// Topoloji: [kat data + wireless] → [kat rack kabin] → [ana merkez].
/// </summary>
public sealed class ElvDataSistemi
{
    public ElvMerkez AnaMerkez { get; set; } = new();
    public List<ElvRackKabin> RackKabinler { get; set; } = new();

    public int ToplamDataHat => RackKabinler.Sum(r => r.DataHatSayisi);
    public int ToplamWireless => RackKabinler.Sum(r => r.WirelessSayisi);
    public int ToplamIpTelefon => RackKabinler.Sum(r => r.IpTelefonSayisi);
    public int ToplamYazici => RackKabinler.Sum(r => r.YaziciSayisi);
    public int ToplamErisimKontrol => RackKabinler.Sum(r => r.ErisimKontrolSayisi);
    public int ToplamDiafon => RackKabinler.Sum(r => r.DiafonSayisi);
    public double ToplamMerkezKablo => RackKabinler.Sum(r => r.MerkezeKabloMetraj);
    public double ToplamDataHatMetraj => RackKabinler.Sum(r => r.ToplamDataHatMetraj);
    public double ToplamWirelessHatMetraj => RackKabinler.Sum(r => r.ToplamWirelessHatMetraj);
    public double ToplamIpTelefonHatMetraj => RackKabinler.Sum(r => r.ToplamIpTelefonHatMetraj);
    public double ToplamYaziciHatMetraj => RackKabinler.Sum(r => r.ToplamYaziciHatMetraj);
    public double ToplamErisimKontrolHatMetraj => RackKabinler.Sum(r => r.ToplamErisimKontrolHatMetraj);
    public double ToplamDiafonHatMetraj => RackKabinler.Sum(r => r.ToplamDiafonHatMetraj);
}

/// <summary>
/// Kamera sistemi: kat kameraları → kat rack/PoE switch → CCTV ana merkez/NVR.
/// Data sistemiyle aynı rack merkezli mantığı kullanır.
/// </summary>
public sealed class ElvCctvIzlemeNoktasi
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Ad { get; set; } = "CCTV İzleme Noktası";
    public int KatNo { get; set; }
    public string KatAdi { get; set; } = "";
    /// <summary>Null ise CCTV Ana Merkez'e, doluysa belirtilen CCTV rack kabinine bağlanır.</summary>
    public string? BagliRackId { get; set; }
    public string BaglantiKabloTipi { get; set; } = "Cat6 U/UTP";
    public double BaglantiKabloMetraj { get; set; }
    public List<ElvCihaz> Cihazlar { get; set; } = new();
}

public sealed class ElvKameraSistemi
{
    public ElvMerkez AnaMerkez { get; set; } = new() { Ad = "CCTV Ana Merkez" };
    public List<ElvRackKabin> RackKabinler { get; set; } = new();
    public List<ElvCctvIzlemeNoktasi> IzlemeNoktalari { get; set; } = new();

    public int ToplamKamera => RackKabinler.Sum(r => r.KameraSayisi);
    public double ToplamMerkezKablo => RackKabinler.Sum(r => r.MerkezeKabloMetraj);
    public double ToplamKameraHatMetraj => RackKabinler.Sum(r => r.ToplamKameraHatMetraj);
    public double ToplamIzlemeNoktasiKabloMetraj => IzlemeNoktalari.Sum(n => n.BaglantiKabloMetraj);
}

/// <summary>
/// Acil anons sistemi: merkezde amfi/mikser/mikrofon, katlarda bir veya daha fazla
/// geçişli hoparlör hattı.
/// </summary>
public sealed class ElvAnonsSistemi
{
    public ElvMerkez AnaMerkez { get; set; } = new() { Ad = "Acil Anons Merkezi" };
    public List<ElvRackKabin> RackKabinler { get; set; } = new();
    /// <summary>Sistem topolojisi: "Analog" veya "Dijital (PoE)".</summary>
    public string Teknoloji { get; set; } = "Analog";
    // Yeni analog 100 V domain modeli. Eski RackKabinler/AnonsHatlari alanları geriye
    // uyumluluk için korunur; yeni analog geliştirme bu koleksiyonlar üzerinde ilerler.
    public List<ElvAnonsZonu> AnalogZonlar { get; set; } = new();
    public List<ElvAnonsAmplifikatoru> AnalogAmplifikatorler { get; set; } = new();
    public List<ElvAnonsMesajSenaryosu> AnalogMesajSenaryolari { get; set; } = new();
    public ElvAnonsGucKaynagi AnalogGucKaynagi { get; set; } = new();

    public int ToplamHat => Teknoloji == "Dijital (PoE)"
        ? RackKabinler.Sum(r => r.ToplamDijitalAnonsUcu)
        : RackKabinler.Sum(r => r.AnonsHatSayisi);
    public int ToplamHoparlor => Teknoloji == "Dijital (PoE)"
        ? RackKabinler.Sum(r => r.IpTavanHoparlorSayisi + r.IpDuvarHoparlorSayisi
            + r.IpHornHoparlorSayisi + r.IpProjektorHoparlorSayisi)
        : RackKabinler.Sum(r => r.ToplamAnonsHoparlor);
    public double ToplamAnonsHatMetraj => Teknoloji == "Dijital (PoE)"
        ? RackKabinler.Sum(r => r.ToplamDijitalAnonsHatMetraj)
        : RackKabinler.Sum(r => r.ToplamAnonsHatMetraj);
    public double ToplamMerkezKablo => RackKabinler.Sum(r => r.MerkezeKabloMetraj);
}

/// <summary>
/// TV sistemi: her TV prizi ayrı koaksiyel kablo ile ana TV santraline iner;
/// ana santralden çatıdaki uydu/antenlere koaksiyel hatlar çıkar.
/// </summary>
public sealed class ElvTvSistemi
{
    public TvDagitimTeknolojisi Teknoloji { get; set; } = TvDagitimTeknolojisi.MerkeziKoaksiyel;
    public List<ElvTvUcu> OrtakUclar { get; set; } = new();
    public ElvDirektLnbSistemi DirektLnb { get; set; } = new();
    public ElvIpTvSistemi IpTv { get; set; } = new();
    public string MerkeziSantralTipi { get; set; } = "Multiswitch IF";
    public ElvMerkez AnaMerkez { get; set; } = new() { Ad = "TV Ana Santral" };
    public List<ElvRackKabin> RackKabinler { get; set; } = new();
    public int CatiUyduSayisi { get; set; } = 1;
    public List<ElvTvCatiHatti> CatiHatlari { get; set; } = new()
    {
        new ElvTvCatiHatti { HatNo = 1, Ad = "Çatı Uydu 1" }
    };

    public int ToplamTvPriz => RackKabinler.Sum(r => r.TvPrizSayisi);
    public double ToplamTvHatMetraj => RackKabinler.Sum(r => r.ToplamTvHatMetraj);
    public double ToplamCatiHatMetraj => CatiHatlari.Sum(h => h.Metraj);
}

/// <summary>
/// Telefon sistemi (analog santral): her telefon prizi ayrı hat ile kat üzerinden
/// telefon santraline iner. TV sistemiyle aynı yıldız topolojiyi kullanır.
/// </summary>
public sealed class ElvTelefonSistemi
{
    public ElvMerkez AnaMerkez { get; set; } = new() { Ad = "Telefon Santrali" };
    public List<ElvRackKabin> RackKabinler { get; set; } = new();

    public int ToplamTelefonPriz => RackKabinler.Sum(r => r.TelefonPrizSayisi);
    public double ToplamTelefonHatMetraj => RackKabinler.Sum(r => r.ToplamTelefonHatMetraj);
    public double ToplamMerkezKablo => RackKabinler.Sum(r => r.MerkezeKabloMetraj);
}

/// <summary>
/// Diafon / kapı zili sistemi: kat üniteleri (daire diafonu, kapı paneli, zil)
/// diafon santraline hat çeker.
/// </summary>
public sealed class ElvDiafonSistemi
{
    public ElvMerkez AnaMerkez { get; set; } = new() { Ad = "Diafon Santrali" };
    public List<ElvRackKabin> RackKabinler { get; set; } = new();

    public int ToplamDiafonUnite => RackKabinler.Sum(r => r.DiafonUniteSayisi);
    public double ToplamDiafonUniteHatMetraj => RackKabinler.Sum(r => r.ToplamDiafonUniteHatMetraj);
    public double ToplamMerkezKablo => RackKabinler.Sum(r => r.MerkezeKabloMetraj);
}

/// <summary>
/// Yangın algılama sistemi: kat panosu olmadan her kat loop'u yangın algılama
/// santraline doğrudan gider.
/// </summary>
public sealed class ElvYanginSistemi
{
    public ElvMerkez AnaMerkez { get; set; } = new() { Ad = "Yangın Algılama Santrali" };
    public List<ElvYanginKatLoopGrubu> KatLoopGruplari { get; set; } = new();

    public int ToplamLoop => KatLoopGruplari.Sum(k => k.LoopSayisi);
    public int ToplamCihaz => KatLoopGruplari.Sum(k => k.ToplamCihaz);
    public double ToplamLoopMetraj => KatLoopGruplari.Sum(k => k.ToplamLoopMetraj);
}
