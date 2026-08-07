using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Kst.Acoustics.Core.Electric.Models;

/// <summary>
/// Yük tipi tanımı — kütüphaneden yüklenir.
/// </summary>
public class YukTipiTanim
{
    public string Ad { get; set; } = "";
    public double BirimGucW { get; set; }
    public string SigortaCinsi { get; set; } = "B"; // B, C, D
    public double TalepFaktoru { get; set; } = 1.0;
    public string Aciklama { get; set; } = "";

    public override string ToString() => $"{Ad} ({BirimGucW}W)";
}

/// <summary>
/// Kaçak akım rölesi tanımı — kütüphaneden yüklenir.
/// </summary>
public class KacakAkimRolesiTanim
{
    public string Ad { get; set; } = "";
    public int SensitiviteMa { get; set; } = 30;   // 30, 100, 300
    public int AnmaAkimA { get; set; } = 40;        // Klasik rölede anma, troid rölede izleme üst sınırı
    public int KutupSayisi { get; set; } = 2;       // 2P veya 4P
    public string Tip { get; set; } = "AC";         // AC, A, F
    public bool TroidTipiMi { get; set; }
    public int AkimTrafosuSekonderA { get; set; } = 5;
    public int MinAkimTrafosuIcCapMm { get; set; }

    public override string ToString() => TroidTipiMi
        ? $"{SensitiviteMa}mA Troid {AnmaAkimA}A {Tip}"
        : $"{SensitiviteMa}mA {KutupSayisi}P {AnmaAkimA}A {Tip}";
}

/// <summary>
/// Akım trafosu tanımı. M11'de troid kaçak akım rölesi için toroidal akım trafosu
/// seçimi bu kütüphaneden yapılır.
/// </summary>
public class AkimTrafosuTanim
{
    public string Ad { get; set; } = "";
    public string Tip { get; set; } = "Toroid";
    public int PrimerA { get; set; } = 160;
    public int SekonderA { get; set; } = 5;
    public int IcCapMm { get; set; } = 70;
    public string Sinif { get; set; } = "RCD";
    public int GerilimV { get; set; } = 400;
    public string Aciklama { get; set; } = "";

    public override string ToString() => $"{Ad} ({PrimerA}/{SekonderA}A {Tip}, Ø{IcCapMm}mm)";
}

/// <summary>
/// Kondansatör tanımı — kütüphaneden yüklenir.
/// </summary>
public class KondansatorTanim
{
    public string Ad { get; set; } = "";
    public double GucKVar { get; set; }
    public string FazTipi { get; set; } = "Trifaze"; // Monofaze, Trifaze
    public int GerilimV { get; set; } = 400;

    public override string ToString() => $"{GucKVar} kVAr {FazTipi}";
}

/// <summary>
/// Astronomik zaman rölesi tanımı — kütüphaneden yüklenir.
/// Aydınlatma panosu girişinde kullanılır.
/// </summary>
public class AstronomikZamanRolesiTanim
{
    public string Ad { get; set; } = "";
    public int AnmaAkimA { get; set; } = 16;
    public int CikisSayisi { get; set; } = 1;
    public int KutupSayisi { get; set; } = 1;
    public string BeslemeGerilimV { get; set; } = "230";
    public string Aciklama { get; set; } = "";

    [JsonPropertyName("BesgelimGerilimV")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EskiBesgelimGerilimV
    {
        get => null;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
                BeslemeGerilimV = value;
        }
    }

    public override string ToString() => $"{Ad} ({AnmaAkimA}A, {CikisSayisi} çıkış)";
}

/// <summary>
/// Kontaktör tanımı — kütüphaneden yüklenir.
/// Aydınlatma panosunda her aydınlatma çıkışına otomatik atanır.
/// </summary>
public class KontaktorTanim
{
    public string Ad { get; set; } = "";
    public int AnmaAkimA { get; set; } = 25; // AC-1 / AC-3 yaklaşık
    public int KutupSayisi { get; set; } = 4;
    public int BobinGerilimV { get; set; } = 230;
    public string Aciklama { get; set; } = "";

    public override string ToString() => $"{Ad} ({AnmaAkimA}A {KutupSayisi}P)";
}

/// <summary>
/// Darbe kesici (darbe akım anahtarı / impuls rölesi) tanımı — linyeye opsiyonel atanır.
/// Tek noktadan/çok noktadan aydınlatma kumandasında kullanılır; tipik anma akımı 16–63A.
/// </summary>
public class DarbeKesiciTanim
{
    public string Ad { get; set; } = "";
    public int AnmaAkimA { get; set; } = 16;
    public int KutupSayisi { get; set; } = 1;
    public int BobinGerilimV { get; set; } = 230;
    public string Aciklama { get; set; } = "";

    public override string ToString() => $"{Ad} ({AnmaAkimA}A {KutupSayisi}P)";
}

/// <summary>
/// Otomatik sigorta (MCB) tanımı — anma akımı ≤ 63A devrelerde kullanılır. Kesme kapasitesi
/// tipik 6 veya 10 kA. 63A üstü için <see cref="TmsTanim"/> (kompakt şalter) kullanılır.
/// </summary>
public class SigortaTanim
{
    public string Ad { get; set; } = "";
    public int AnmaAkimA { get; set; } = 16;
    /// <summary>Açma karakteristiği — B (resistif), C (motor/endüktif), D (ağır endüktif).</summary>
    public string Karakteristik { get; set; } = "C";
    /// <summary>Kesme kapasitesi (kA) — MCB için tipik 6 veya 10.</summary>
    public double KesmeKapasitesiKA { get; set; } = 6;
    public int KutupSayisi { get; set; } = 1;
    public string Aciklama { get; set; } = "";

    public override string ToString() => $"{Ad} ({AnmaAkimA}A {Karakteristik} {KesmeKapasitesiKA:0.#}kA)";
}

/// <summary>
/// Termik manyetik şalter / kompakt şalter (TMŞ / MCCB) tanımı — anma akımı 63A üstü
/// devrelerde kullanılır. Kesme kapasitesi tipik 36, 50 veya 100 kA.
/// </summary>
public class TmsTanim
{
    public string Ad { get; set; } = "";
    public int AnmaAkimA { get; set; } = 100;
    /// <summary>Kesme kapasitesi (kA) — TMŞ için tipik 36 / 50 / 100.</summary>
    public double KesmeKapasitesiKA { get; set; } = 36;
    public int KutupSayisi { get; set; } = 3;
    /// <summary>Ayarlanabilir termik koruma alt sınırı (A); 0 ise sabit.</summary>
    public int AyarAltA { get; set; }
    public string Aciklama { get; set; } = "";

    public override string ToString() => $"{Ad} ({AnmaAkimA}A {KesmeKapasitesiKA:0.#}kA {KutupSayisi}P)";
}

/// <summary>
/// Bıçaklı sigorta tanımı (NH serisi) — kompanzasyon panosu girişi için.
/// </summary>
public class BicakliSigortaTanim
{
    public string Ad { get; set; } = "";
    public string Boy { get; set; } = "NH00"; // NH00, NH1, NH2, NH3, NH4
    public int AnmaAkimA { get; set; } = 100;
    public string Karakteristik { get; set; } = "gG"; // gG, aM
    public int KesmeKapasitesiKA { get; set; } = 120;
    public int GerilimV { get; set; } = 500;
    public string Aciklama { get; set; } = "";

    public override string ToString() => $"{Ad} ({Boy} {AnmaAkimA}A {Karakteristik})";
}

/// <summary>
/// Kompanzasyon rölesi tanımı — kademe yöneten otomatik reaktif güç kontrol rölesi.
/// </summary>
public class KompanzasyonRolesiTanim
{
    public string Ad { get; set; } = "";
    public int KademeSayisi { get; set; } = 12;     // 6, 12, 18
    public bool SvcDestekli { get; set; } = false;  // SVC (statik) destekli mi
    public string CtOrani { get; set; } = "200/5";  // Akım trafosu oranı
    public string IletisimArayuzu { get; set; } = "RS-485"; // RS-485, Modbus, vs
    public string Aciklama { get; set; } = "";

    public override string ToString() => $"{Ad} ({KademeSayisi} kademe{(SvcDestekli ? " SVC" : "")})";
}

/// <summary>
/// Şönt reaktör (SVC — statik VAR kompanzasyon için) tanımı.
/// Detuned (akortsuz) reaktör — harmonik filtreleme için.
/// </summary>
public class ReaktorTanim
{
    public string Ad { get; set; } = "";
    public double GucKVar { get; set; } = 25;
    public double AkortFrekansHz { get; set; } = 189;  // 189Hz (5.67%) veya 134Hz (14%)
    public double AkortYuzde { get; set; } = 5.67;     // p = (50/fr)² × 100
    public int GerilimV { get; set; } = 400;
    public string FazTipi { get; set; } = "Trifaze";
    public string Aciklama { get; set; } = "";

    public override string ToString() => $"{Ad} ({GucKVar} kVAr, {AkortFrekansHz}Hz / %{AkortYuzde})";
}

/// <summary>
/// Harmonik filtre tanımı — kompanzasyon panosunda harmonik bozulmayı azaltmak için.
/// </summary>
public class HarmonikFiltreTanim
{
    public string Ad { get; set; } = "";
    public string Tipi { get; set; } = "Pasif"; // Pasif, Aktif, Hibrit
    public double FiltreFrekansHz { get; set; } = 250; // Akortlu filtre için (5. harmonik = 250 Hz)
    public double GucKVar { get; set; } = 50;
    public int AnmaAkimA { get; set; } = 80;
    public double ThdYuzde { get; set; } = 5.0;  // Hedef THD %
    public int GerilimV { get; set; } = 400;
    public string Aciklama { get; set; } = "";

    public override string ToString() => $"{Ad} ({Tipi} {GucKVar}kVAr)";
}

/// <summary>
/// SVC (statik) kompanzasyon için tristör yük sürücüsü.
/// Her SVC kademesi için tristör modülü.
/// </summary>
public class SvcSurucuTanim
{
    public string Ad { get; set; } = "";
    public double KademeGucKVar { get; set; } = 25;
    public int AnmaAkimA { get; set; } = 60;
    public int GerilimV { get; set; } = 400;
    public string SogutmaTipi { get; set; } = "Doğal"; // Doğal, Fanlı
    public string Aciklama { get; set; } = "";

    public override string ToString() => $"{Ad} ({KademeGucKVar} kVAr, {AnmaAkimA}A)";
}

/// <summary>
/// Jeneratör motor (içten yanmalı) tanımı.
/// </summary>
public class MotorTanim
{
    public string Ad { get; set; } = "";
    public string Marka { get; set; } = "";
    public string Model { get; set; } = "";
    public string YakitTipi { get; set; } = "Dizel"; // Dizel, Doğalgaz, Benzin, LPG
    public int SilindirSayisi { get; set; } = 6;
    public double HacimL { get; set; } = 6.0;     // motor hacmi litre
    public int DevirRpm { get; set; } = 1500;     // 50Hz için 1500, 60Hz için 1800
    public double GucKW { get; set; } = 100;      // motor mekanik çıkış gücü
    public string Sogutma { get; set; } = "Su";    // Su, Hava
    public string Aciklama { get; set; } = "";

    public override string ToString() => $"{Ad} ({Marka} {Model}, {GucKW}kW {YakitTipi})";
}

/// <summary>
/// Jeneratör alternatör (senkron makine) tanımı.
/// </summary>
public class AlternatorTanim
{
    public string Ad { get; set; } = "";
    public string Marka { get; set; } = "";
    public string Model { get; set; } = "";
    public double AnmaGucKVA { get; set; } = 100;
    public int GerilimV { get; set; } = 400;
    public string FazTipi { get; set; } = "Trifaze";
    public double CosF { get; set; } = 0.8;       // alternatör nominal güç faktörü
    public int FrekansHz { get; set; } = 50;
    public double VerimYuzde { get; set; } = 92;  // %
    public string KutuplanmaTipi { get; set; } = "Fırçasız"; // Fırçasız, Fırçalı
    public string Aciklama { get; set; } = "";

    public override string ToString() => $"{Ad} ({Marka} {Model}, {AnmaGucKVA}kVA)";
}

/// <summary>
/// Jeneratör (motor + alternatör + şase) tanımı.
/// PRP = Prime Power (sürekli, yük değişkenli), ESP = Standby (acil, sınırlı saat).
/// Tipik olarak ESP ≈ PRP × 1.1.
/// </summary>
public class JeneratorTanim
{
    public string Ad { get; set; } = "";
    public string Marka { get; set; } = "";
    public string Model { get; set; } = "";

    // Prime Power (PRP) — sürekli işletme
    public double PrimeGucKVA { get; set; } = 100;
    public double PrimeGucKW { get; set; } = 80;  // PRP × cosφ (genelde 0.8)

    // Standby Power (ESP) — acil yedek
    public double StandbyGucKVA { get; set; } = 110;
    public double StandbyGucKW { get; set; } = 88;

    public string MotorAd { get; set; } = "";       // MotorTanim referansı
    public string AlternatorAd { get; set; } = ""; // AlternatorTanim referansı
    public int GerilimV { get; set; } = 400;
    public int FrekansHz { get; set; } = 50;
    public string Kabin { get; set; } = "Sessiz"; // Açık, Sessiz, Süper Sessiz
    public double SesSeviyeDb { get; set; } = 75; // dB(A) @ 7m
    public double YakitTuketimLh100 { get; set; } = 22; // L/h tam yükte (100%)
    public double YakitDeposuL { get; set; } = 200;
    public double AgirlikKg { get; set; } = 2000;
    public string Aciklama { get; set; } = "";

    public override string ToString() => $"{Ad} ({Marka} {Model}, PRP {PrimeGucKVA}kVA / ESP {StandbyGucKVA}kVA)";
}

/// <summary>
/// Otomatik / Manuel transfer şalteri (ATS / MTS) — transfer panosunda kullanılır.
/// </summary>
public class TransferSalterTanim
{
    public string Ad { get; set; } = "";
    public string Marka { get; set; } = "";
    public string Model { get; set; } = "";
    public int AnmaAkimA { get; set; } = 250;
    public int KutupSayisi { get; set; } = 4;
    public int GerilimV { get; set; } = 400;
    public string Tipi { get; set; } = "ATS";  // ATS (Otomatik), MTS (Manuel)
    public int TransferSuresiMs { get; set; } = 100; // şebeke-jeneratör geçiş süresi
    public string KontrolTipi { get; set; } = "Mikroişlemci";
    public string Aciklama { get; set; } = "";

    public override string ToString() => $"{Ad} ({Marka} {Model}, {AnmaAkimA}A {KutupSayisi}P {Tipi})";
}

/// <summary>
/// UPS (Kesintisiz Güç Kaynağı / KGK) tanımı — kütüphaneden seçilir.
/// Online (çift dönüşüm / VFI) veya line-interactive. Kritik panoyu besler.
/// </summary>
public class UpsTanim
{
    public string Ad { get; set; } = "";
    public string Marka { get; set; } = "";
    public string Model { get; set; } = "";

    /// <summary>Çıkış görünür güç (kVA).</summary>
    public double GucKVA { get; set; } = 10;

    /// <summary>Çıkış aktif güç (kW). GucKW/GucKVA = çıkış güç faktörü.</summary>
    public double GucKW { get; set; } = 9;

    /// <summary>AC-AC toplam verim η (%) — kayıpların hesabı için.</summary>
    public double VerimYuzde { get; set; } = 95;

    /// <summary>Giriş güç faktörü (PFC ile ~0.99). Giriş akımı hesabında kullanılır.</summary>
    public double GirisCosF { get; set; } = 0.99;

    /// <summary>Akü DC bara nominal gerilimi (V) — seri blok sayısını belirler.</summary>
    public double DcBaraGerilimV { get; set; } = 384;

    /// <summary>Akü deşarj sonu minimum DC bara gerilimi (V) — inverter alt sınırı.</summary>
    public double MinDcBaraGerilimV { get; set; } = 320;

    /// <summary>Giriş şalteri anma akımı (A) — akü şarj akımı dahil seçilir.</summary>
    public int GirisSalterA { get; set; } = 32;

    /// <summary>Bypass / çıkış şalteri anma akımı (A).</summary>
    public int BypassSalterA { get; set; } = 32;

    /// <summary>Topoloji — "Online (VFI)" veya "Line-interactive".</summary>
    public string Topoloji { get; set; } = "Online (VFI)";

    public string Aciklama { get; set; } = "";

    /// <summary>Çıkış güç faktörü (kW/kVA).</summary>
    [JsonIgnore]
    public double CikisCosF => GucKVA > 0 ? GucKW / GucKVA : 0.9;

    public override string ToString() => $"{Ad} ({Marka} {Model}, {GucKVA}kVA/{GucKW}kW {Topoloji})";
}

/// <summary>
/// Trafo (güç transformatörü) tanımı — kütüphaneden seçilir. OG sisteminde tek trafo kullanılır.
/// Fiziksel ölçüler köşk yerleşim çizimi için tutulur.
/// </summary>
public class TrafoTanim
{
    public string Ad { get; set; } = "";
    public string Marka { get; set; } = "";
    public string Model { get; set; } = "";

    /// <summary>Anma gücü (kVA) — standart seri: 100…2500.</summary>
    public double GucKVA { get; set; } = 630;

    /// <summary>Soğutma/yalıtım tipi (yağlı / hermetik / kuru).</summary>
    public TrafoTipi Tip { get; set; } = TrafoTipi.Yagli;

    /// <summary>Primer (OG) gerilim (kV).</summary>
    public double PrimerGerilimKV { get; set; } = 34.5;

    /// <summary>Sekonder (AG) gerilim (V).</summary>
    public int SekonderGerilimV { get; set; } = 400;

    /// <summary>Kısa devre gerilimi uk (%) — empedansı belirler (≤630kVA ~4, ≥800kVA ~6).</summary>
    public double UkYuzde { get; set; } = 4.0;

    /// <summary>Bağlantı/vektör grubu (örn. Dyn11).</summary>
    public string VektorGrubu { get; set; } = "Dyn11";

    /// <summary>Boşta (demir) kaybı (W).</summary>
    public double BostaKayipW { get; set; }

    /// <summary>Tam yükte yük (bakır) kaybı (W).</summary>
    public double YukKaybiW { get; set; }

    /// <summary>Soğutma sınıfı (yağlı "ONAN", kuru "AN").</summary>
    public string SogutmaTipi { get; set; } = "ONAN";

    // Fiziksel ölçüler (köşk yerleşimi için)
    public double GenislikMm { get; set; } = 1500;
    public double DerinlikMm { get; set; } = 1200;
    public double YukseklikMm { get; set; } = 1700;
    public double AgirlikKg { get; set; } = 2000;

    public string Aciklama { get; set; } = "";

    public override string ToString()
    {
        string tip = Tip switch
        {
            TrafoTipi.Yagli => "Yağlı",
            TrafoTipi.Hermetik => "Hermetik",
            TrafoTipi.KuruTip => "Kuru Tip",
            _ => Tip.ToString()
        };
        return $"{Ad} ({GucKVA:N0} kVA • {tip} • {PrimerGerilimKV:0.#}/{SekonderGerilimV / 1000.0:0.###}kV)";
    }
}

/// <summary>
/// OG hücresi (metal mahfazalı modüler hücre) tanımı — kütüphaneden seçilir.
/// Fiziksel genişlik köşk yerleşim çizimi için tutulur.
/// </summary>
public class OgHucreTanim
{
    public string Ad { get; set; } = "";
    public string Marka { get; set; } = "";
    public string Model { get; set; } = "";

    /// <summary>Hücre işlevi.</summary>
    public OgHucreTipi HucreTipi { get; set; } = OgHucreTipi.Cikis;

    /// <summary>Koruma/manevra elemanı tipi (hücre çeşidi).</summary>
    public OgHucreKorumaTipi KorumaTipi { get; set; } = OgHucreKorumaTipi.YukAyirici;

    /// <summary>Anma gerilimi (kV) — 12 / 17.5 / 24 / 36.</summary>
    public double AnmaGerilimKV { get; set; } = 36;

    /// <summary>Anma akımı (A) — bara/ayırıcı, tipik 630 / 1250.</summary>
    public int AnmaAkimA { get; set; } = 630;

    /// <summary>Kısa süreli dayanım akımı (kA, 1 s).</summary>
    public double KisaSureDayanimKA { get; set; } = 16;

    /// <summary>Tepe (dinamik) dayanım akımı (kA).</summary>
    public double TepeDayanimKA { get; set; } = 40;

    /// <summary>Yalıtım ortamı ("SF6" / "Hava" / "Kuru").</summary>
    public string YalitimTipi { get; set; } = "SF6";

    // Fiziksel ölçüler (köşk yerleşimi için)
    public double GenislikMm { get; set; } = 500;
    public double DerinlikMm { get; set; } = 1000;
    public double YukseklikMm { get; set; } = 1800;

    public string Aciklama { get; set; } = "";

    public override string ToString()
    {
        string islev = HucreTipi switch
        {
            OgHucreTipi.Giris => "Giriş",
            OgHucreTipi.Olcu => "Ölçü",
            OgHucreTipi.BaraBaglama => "Bara Bağlama",
            OgHucreTipi.Cikis => "Çıkış",
            OgHucreTipi.TrafoKoruma => "Trafo Koruma",
            _ => HucreTipi.ToString()
        };
        return $"{Ad} ({islev} • {AnmaGerilimKV:0.#}kV {AnmaAkimA}A)";
    }
}

/// <summary>
/// Akü deşarj tablosu noktası — IEEE 485 sabit-güç yöntemi.
/// Belirli bir otonomi süresi için blok başına verilebilen güç (W),
/// deşarj sonu gerilimine kadar.
/// </summary>
public class AkuDesarjNoktasi
{
    /// <summary>Otonomi süresi (dakika).</summary>
    public int Dakika { get; set; }

    /// <summary>Bu süre boyunca blok başına verilebilen güç (W/blok), Uf'e kadar.</summary>
    public double BlokGucW { get; set; }
}

/// <summary>
/// Akü (blok / monoblok) tanımı — UPS akü grubu boyutlandırması için.
/// Sabit-güç deşarj tablosu (W/blok @ dakika) ile IEEE 485 yöntemine göre seçilir.
/// </summary>
public class AkuTanim
{
    public string Ad { get; set; } = "";
    public string Marka { get; set; } = "";
    public string Model { get; set; } = "";

    /// <summary>Kimya/tip — "VRLA" (kurşun-asit) veya "Li-ion".</summary>
    public string Tip { get; set; } = "VRLA";

    /// <summary>Blok nominal gerilimi (V) — VRLA monoblok 12V / hücre 2V, Li ~3.2V.</summary>
    public double BlokGerilimV { get; set; } = 12;

    /// <summary>Nominal kapasite (Ah) — referans C-oranında.</summary>
    public double KapasiteAh { get; set; } = 100;

    /// <summary>Deşarj sonu gerilimi (V/blok) — örn. 12V blokta ~10.5V (1.75V/hücre).</summary>
    public double DesarjSonuGerilimV { get; set; } = 10.5;

    /// <summary>Sabit-güç deşarj tablosu (W/blok @ dakika).</summary>
    public List<AkuDesarjNoktasi> DesarjTablosu { get; set; } = new();

    public double AgirlikKg { get; set; } = 30;
    public string Aciklama { get; set; } = "";

    /// <summary>
    /// Verilen otonomi süresi için blok başına güç (W). Tablo noktaları arasında
    /// doğrusal interpolasyon; tablodaki en uzun sürenin üstünde veri yoksa 0 döner.
    /// </summary>
    public double BlokGucW(int dakika)
    {
        if (DesarjTablosu == null || DesarjTablosu.Count == 0) return 0;
        var sirali = DesarjTablosu.OrderBy(n => n.Dakika).ToList();
        if (dakika <= sirali[0].Dakika) return sirali[0].BlokGucW;
        if (dakika == sirali[^1].Dakika) return sirali[^1].BlokGucW;
        if (dakika > sirali[^1].Dakika) return 0;
        for (int i = 0; i < sirali.Count - 1; i++)
        {
            var a = sirali[i];
            var b = sirali[i + 1];
            if (dakika >= a.Dakika && dakika <= b.Dakika)
            {
                double t = (double)(dakika - a.Dakika) / (b.Dakika - a.Dakika);
                return a.BlokGucW + t * (b.BlokGucW - a.BlokGucW);
            }
        }
        return sirali[^1].BlokGucW;
    }

    public override string ToString() => $"{Ad} ({Marka} {Model}, {BlokGerilimV}V {KapasiteAh}Ah {Tip})";
}
