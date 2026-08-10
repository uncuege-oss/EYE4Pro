using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Kst.Acoustics.Core.BuildingAcoustics.Models;

// Bu dosya M08'in ileri iş akışlarında paylaşılan, proje dosyasına yazılabilen
// sözleşmeleri tek yerde tutar. UI bağımlılığı içermez.

public static class M08KararliKimlik
{
    /// <summary>Aynı metin için süreç/makine bağımsız kararlı Guid üretir.</summary>
    public static Guid Olustur(string kapsam, params string?[] parcalar)
    {
        var metin = kapsam.Trim().ToUpperInvariant() + "|" +
                    string.Join('|', parcalar.Select(p => (p ?? "").Trim().ToUpperInvariant()));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(metin));
        var bytes = hash[..16];
        // UUID v5/variant bitleri; hash algoritması SHA-256 olsa da biçim standart Guid'dir.
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    public static Guid YapiElemani(YapiElemani eleman) =>
        Olustur("M08-YAPI-ELEMANI", ((int)eleman.Tip).ToString(), eleman.Ad);
}

public enum SenaryoHedefTuru
{
    Proje,
    Kat,
    Mekan,
    Bolme,
    Aciklik,
    YapiElemani,
    Junction,
}

public sealed class SenaryoDegisiklikKaydi
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public SenaryoHedefTuru HedefTuru { get; set; }
    public Guid? HedefId { get; set; }
    /// <summary>M50 kat/açıklık kimliği gibi Guid olmayan kararlı hedef kimliği.</summary>
    public string HariciHedefId { get; set; } = "";
    public string AlanAdi { get; set; } = "";
    public string OncekiDegerJson { get; set; } = "";
    public string YeniDegerJson { get; set; } = "";
}

public sealed class SenaryoSonucOzeti
{
    public Guid SonucId { get; set; }
    public string SonucTuru { get; set; } = "";
    public double Deger { get; set; }
    public double Limit { get; set; }
    public double Marj { get; set; }
    public string Durum { get; set; } = "";
    public string Birim { get; set; } = "";
}

public sealed class AkustikSenaryo
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Ad { get; set; } = "Yeni Senaryo";
    public string Aciklama { get; set; } = "";
    public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
    public DateTime GuncellemeTarihi { get; set; } = DateTime.Now;
    public int? KaynakBinaGeometriRevizyon { get; set; }
    public string GirdiParmakIzi { get; set; } = "";
    public bool GuncelDegil { get; set; }
    public string GuncellikNotu { get; set; } = "";
    public List<SenaryoDegisiklikKaydi> Degisiklikler { get; set; } = new();
    public List<SenaryoSonucOzeti> Sonuclar { get; set; } = new();
}

public enum ProjeOnayTuru
{
    Hesap,
    Rapor,
    SahaOlcumu,
    Revizyon,
    Diger,
}

public enum ProjeOnayDurumu
{
    Taslak,
    Incelemede,
    Onaylandi,
    Reddedildi,
    IptalEdildi,
}

/// <summary>M08'in birbirine karıştırılmaması gereken yönetmelik çıktı profilleri.</summary>
public enum M08RaporBelgeTuru
{
    MimariAkustikRaporu,
    AkustikProjeRaporPaketi,
    SahaOlcumRaporu,
    AkustikPerformansBelgesi,
}

public enum M08RaporYayinDurumu
{
    Taslak,
    KontrolEdildi,
    OnayaHazir,
    Imzali,
}

public enum M08YapiDurumu
{
    YeniYapi,
    KullanimDegisikligi,
    EsasliTadilat,
    MevcutYapiDegerlendirme,
}

public sealed record M08RaporSecenegi<T>(T Deger, string Ad, string Aciklama)
{
    public override string ToString() => Ad;
}

/// <summary>
/// ISO'nun yayımlanmış standart kataloğunda 10.08.2026 tarihinde doğrulanan baskılar.
/// Taslak/CD/AWI çalışmaları yayımlanmış standart sayılmaz. Değerler her proje belgesine
/// kopyalanır; böylece gelecekte katalog değişse de eski raporun dayanağı korunur.
/// </summary>
public static class M08GuncelStandartSurumleri
{
    public const string Hesap =
        "TS EN ISO 12354-1:2017; TS EN ISO 12354-2:2017; TS EN ISO 12354-3:2017";
    public const string Derecelendirme =
        "TS EN ISO 717-1:2020; TS EN ISO 717-2:2020";
    public const string Olcum =
        "TS EN ISO 16283-1:2014 + A1:2017; TS EN ISO 16283-2:2020; TS EN ISO 16283-3:2016";
    public const string ServisOlcumu =
        "TS EN ISO 10052:2021; TS EN ISO 16032:2024";
    public const string Belirsizlik = "ISO 12999-1:2020";
    public const string DogrulamaNotu =
        "Yayımlanmış güncel ISO baskıları 10.08.2026 tarihinde doğrulanmıştır; resmî teslimde TSE ulusal kabul durumu ayrıca kontrol edilmelidir.";
}

/// <summary>
/// Raporun hukuki/teknik kimliğini proje hesabından ayırır. Eski projelerde güvenli
/// varsayılan Mimari Akustik Raporu + Taslak'tır; boş alanlar rapor ön kontrolünde
/// açık veri eksiği olarak gösterilir.
/// </summary>
public sealed class M08RaporAyarlari
{
    public M08RaporBelgeTuru BelgeTuru { get; set; } = M08RaporBelgeTuru.MimariAkustikRaporu;
    public M08RaporYayinDurumu YayinDurumu { get; set; } = M08RaporYayinDurumu.Taslak;
    public M08YapiDurumu YapiDurumu { get; set; } = M08YapiDurumu.YeniYapi;
    public string RaporNumarasi { get; set; } = "";
    public string RevizyonKodu { get; set; } = "00";
    public DateTime? YayinTarihi { get; set; }
    /// <summary>EK-10 performans belgesi için geçerlilik sonu; varsayılan yayın tarihinden on yıldır.</summary>
    public DateTime? BelgeGecerlilikTarihi { get; set; }
    /// <summary>EK-10 kapakta kullanılacak bina fotoğrafının yerel dosya yolu.</summary>
    public string BinaFotografiYolu { get; set; } = "";
    public string Ada { get; set; } = "";
    public string Parsel { get; set; } = "";
    public string RuhsatNumarasi { get; set; } = "";
    /// <summary>Madde 5/6 değerlendirmesinde bodrum/çatı istisnaları düşülmüş kat sayısı.</summary>
    public int KapsamaEsasKatSayisi { get; set; }
    /// <summary>Ruhsat projesindeki toplam inşaat alanı; net oda alanı toplamı değildir.</summary>
    public double ToplamInsaatAlaniM2 { get; set; }
    public bool BirdenFazlaBinaIsleviVar { get; set; }
    public bool OzelAkustikTasarimGerekli { get; set; }
    public bool AkustikProjeHazirlandi { get; set; }
    public string HazirlayanAdi { get; set; } = "";
    public string HazirlayanUnvani { get; set; } = "";
    public string HazirlayanYetkiNo { get; set; } = "";
    public string KontrolEdenAdi { get; set; } = "";
    public string OnaylayanAdi { get; set; } = "";
    public string MimariProjeRevizyonu { get; set; } = "";
    public string MekanikProjeRevizyonu { get; set; } = "";
    public string DisGurultuYontemi { get; set; } = "";
    public string DisGurultuKonumu { get; set; } = "";
    public string DisGurultuKaynakBelgesi { get; set; } = "";
    public DateTime? DisGurultuVeriTarihi { get; set; }
    public string MevzuatSurumu { get; set; } =
        "RG 31.05.2017/30082; değişiklik RG 31.05.2018/30437";
    public string StandartSurumNotu { get; set; } = M08GuncelStandartSurumleri.DogrulamaNotu;
    public string HesapStandartlariSurumu { get; set; } = M08GuncelStandartSurumleri.Hesap;
    public string DerecelendirmeStandartlariSurumu { get; set; } = M08GuncelStandartSurumleri.Derecelendirme;
    public string OlcumStandartlariSurumu { get; set; } = M08GuncelStandartSurumleri.Olcum;
    public string ServisOlcumStandartlariSurumu { get; set; } = M08GuncelStandartSurumleri.ServisOlcumu;
    public string BelirsizlikStandardiSurumu { get; set; } = M08GuncelStandartSurumleri.Belirsizlik;
}

/// <summary>Rapordan çıkarılan sonucun görünür ve denetlenebilir kapsam kaydı.</summary>
public sealed class M08RaporKapsamDisiKaydi
{
    public Guid SonucId { get; set; }
    public string Gerekce { get; set; } = "";
    public string Kullanici { get; set; } = "";
    public DateTime Tarih { get; set; } = DateTime.Now;
}

public sealed class ProjeOnayKaydi
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ProjeOnayTuru Tur { get; set; }
    public ProjeOnayDurumu Durum { get; set; } = ProjeOnayDurumu.Taslak;
    public string Aciklama { get; set; } = "";
    public string Onaylayan { get; set; } = "";
    public string Kurulus { get; set; } = "";
    public string ImzaOzeti { get; set; } = "";
    public string GirdiParmakIzi { get; set; } = "";
    public DateTime Tarih { get; set; } = DateTime.Now;
}

public enum DenetimEylemi
{
    Olusturma,
    Degistirme,
    Silme,
    IceriAktarma,
    DisaAktarma,
    Hesaplama,
    Onaylama,
    Kilitleme,
    Diger,
}

public sealed class DenetimKaydi
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Tarih { get; set; } = DateTime.Now;
    public DenetimEylemi Eylem { get; set; }
    public string Kullanici { get; set; } = "";
    public string HedefTuru { get; set; } = "";
    public string HedefId { get; set; } = "";
    public string Aciklama { get; set; } = "";
    public string OncekiDegerOzeti { get; set; } = "";
    public string YeniDegerOzeti { get; set; } = "";
    public string GirdiParmakIzi { get; set; } = "";
}

public enum SahaOlcumTuru
{
    BolmeHavaSesi,
    DisCepheHavaSesi,
    DarbeSesi,
    /// <summary>Eski projelerde servis/sürekli ölçümü için kullanılan ad.</summary>
    IcGurultuSurekli,
    /// <summary>Eski projelerde servis/kesikli ölçümü için kullanılan ad.</summary>
    IcGurultuKesikli,
    Reverberasyon,
    DisGurultu,
    IcOrtamArkaPlanGurultusu,
    ServisEkipmaniSurekli,
    ServisEkipmaniKesikli,
}

public enum SahaOlcumDurumu
{
    Taslak,
    Dogrulandi,
    Reddedildi,
}

public sealed class SahaOlcumKaydi
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Ad { get; set; } = "Ölçüm";
    public SahaOlcumTuru Tur { get; set; }
    public SahaOlcumDurumu Durum { get; set; } = SahaOlcumDurumu.Taslak;
    public string KatKimligi { get; set; } = "";
    public Guid? KaynakMekanId { get; set; }
    public Guid? AliciMekanId { get; set; }
    public Guid? BolmeId { get; set; }
    public double Deger { get; set; }
    public string Birim { get; set; } = "dB";
    public double? Belirsizlik { get; set; }
    public List<double> BantFrekanslariHz { get; set; } = new();
    public List<double> BantDegerleri { get; set; } = new();
    public DateTime OlcumTarihi { get; set; } = DateTime.Now;
    public string Standart { get; set; } = "";
    public string Yontem { get; set; } = "";
    public string Cihaz { get; set; } = "";
    public string Operator { get; set; } = "";
    public string KaynakDosya { get; set; } = "";
    public string KaynakDosyaSha256 { get; set; } = "";
    public string KaynakKayitId { get; set; } = "";
    public string Notlar { get; set; } = "";
    public bool RaporlamaIcinOnayli { get; set; }
}

public enum AkustikGorevKategorisi
{
    Geometri,
    Atama,
    Siniflandirma,
    Olcum,
    UrunBelgesi,
    Uygunsuzluk,
    Rapor,
    Koordinasyon,
    Diger,
}

public enum AkustikGorevOnceligi
{
    Dusuk,
    Normal,
    Yuksek,
    Kritik,
}

public enum GorevDurumu
{
    Acik,
    DevamEdiyor,
    Beklemede,
    Tamamlandi,
    Muaf,
}

/// <summary>
/// Otomatik üretilen iş kaleminin kullanıcıya ait durum/not katmanı. Otomatik
/// kalemin kendisi proje dosyasına yazılmaz; böylece veri düzeldiğinde bayat görev kalmaz.
/// </summary>
public sealed class AkustikGorevKaydi
{
    public string Anahtar { get; set; } = "";
    /// <summary>Kullanıcının kendisinin eklediği ve otomatik kontrole bağlı olmayan kalem.</summary>
    public bool ManuelMi { get; set; }
    public string Baslik { get; set; } = "";
    public string Aciklama { get; set; } = "";
    public AkustikGorevKategorisi Kategori { get; set; } = AkustikGorevKategorisi.Diger;
    public AkustikGorevOnceligi Oncelik { get; set; } = AkustikGorevOnceligi.Normal;
    public string KatKimligi { get; set; } = "";
    public Guid? MekanId { get; set; }
    public Guid? BolmeId { get; set; }
    public Guid? YapiElemaniId { get; set; }
    public GorevDurumu Durum { get; set; } = GorevDurumu.Acik;
    public string Sorumlu { get; set; } = "";
    public DateTime? SonTarih { get; set; }
    public string Not { get; set; } = "";
    public DateTime GuncellemeTarihi { get; set; } = DateTime.Now;
}

/// <summary>Core iş listesi servisinin salt-okunur, her çağrıda yeniden üretilen görünümü.</summary>
public sealed class AkustikIsKalemi
{
    public string Anahtar { get; set; } = "";
    public AkustikGorevKategorisi Kategori { get; set; }
    public AkustikGorevOnceligi Oncelik { get; set; } = AkustikGorevOnceligi.Normal;
    public GorevDurumu Durum { get; set; } = GorevDurumu.Acik;
    public string Baslik { get; set; } = "";
    public string Aciklama { get; set; } = "";
    public string OnerilenEylem { get; set; } = "";
    public string KatKimligi { get; set; } = "";
    public Guid? MekanId { get; set; }
    public Guid? BolmeId { get; set; }
    public Guid? YapiElemaniId { get; set; }
    public string Sorumlu { get; set; } = "";
    public DateTime? SonTarih { get; set; }
    public string Not { get; set; } = "";
    public bool Otomatik { get; set; } = true;
}

public enum AkustikMetrajBirimi
{
    Metrekare,
    Metre,
    Adet,
    Metrekup,
}

public sealed class AkustikMetrajAyar
{
    public string ParaBirimi { get; set; } = "TRY";
    public decimal VarsayilanFireYuzdesi { get; set; }
    public bool IcDuvarlariTekYuzSay { get; set; } = true;
    public bool HesapDisiMekanlariDahilEt { get; set; }
}

public sealed class AkustikMetrajSatiri
{
    public Guid YapiElemaniId { get; set; }
    public string YapiElemaniAdi { get; set; } = "";
    public YapiElemaniTipi Tip { get; set; }
    public string PozNo { get; set; } = "";
    public AkustikMetrajBirimi Birim { get; set; }
    public decimal NetMiktar { get; set; }
    public decimal FireYuzdesi { get; set; }
    public decimal Miktar { get; set; }
    public decimal BirimFiyat { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
    public decimal ToplamTutar { get; set; }
    public List<AkustikMetrajKaynagi> Kaynaklar { get; set; } = new();
}

public sealed class AkustikMetrajKaynagi
{
    public string KatKimligi { get; set; } = "";
    public Guid? MekanId { get; set; }
    public Guid? BolmeId { get; set; }
    public string Aciklama { get; set; } = "";
    public decimal Miktar { get; set; }
}

public enum UrunBelgesiTuru
{
    AkustikTestRaporu,
    TeknikFoy,
    UygunlukBelgesi,
    YanginBelgesi,
    CevreselBeyan,
    MontajTalimati,
    Diger,
}

public sealed class UrunBelgesi
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public UrunBelgesiTuru Tur { get; set; }
    public string Ad { get; set; } = "";
    public string DosyaYolu { get; set; } = "";
    public string Url { get; set; } = "";
    public string BelgeNo { get; set; } = "";
    public string VerenKurulus { get; set; } = "";
    public DateTime? YayinTarihi { get; set; }
    public DateTime? GecerlilikTarihi { get; set; }
    public string Sha256 { get; set; } = "";
    public string MimeType { get; set; } = "";
    public string Notlar { get; set; } = "";

    [JsonIgnore]
    public bool SuresiDolmus => GecerlilikTarihi is { } tarih && tarih.Date < DateTime.Today;
}

public sealed class BinaAkustikSablon
{
    public int SchemaVersion { get; set; } = 1;
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Ad { get; set; } = "Yeni Şablon";
    public string Aciklama { get; set; } = "";
    public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
    public DateTime GuncellemeTarihi { get; set; } = DateTime.Now;
    public BinaIslevi BinaIslevi { get; set; } = BinaIslevi.Konut;
    public BinaSinifi BinaSinifi { get; set; } = BinaSinifi.S1;
    public AkustikPerformansSinifi AkustikPerformansSinifi { get; set; } = AkustikPerformansSinifi.C;
    public double DisGurultuDb { get; set; } = 65;
    public CepheMaruziyet DisGurultuCepheMaruziyeti { get; set; } = CepheMaruziyet.SerbestAlan;
    public List<MekanSablonKurali> MekanKurallari { get; set; } = new();
    public List<BolmeSablonKurali> BolmeKurallari { get; set; } = new();
}

public sealed class MekanSablonKurali
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AdKalibi { get; set; } = "";
    public bool RegexMi { get; set; }
    public string KullanimTipi { get; set; } = "";
    public string KaynakDerecesi { get; set; } = "";
    public string AliciSinifi { get; set; } = "";
    public Guid? ZeminYapiElemaniId { get; set; }
    public Guid? TavanYapiElemaniId { get; set; }
}

public sealed class BolmeSablonKurali
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool? DisCepheMi { get; set; }
    public BolmeKomsulukTipi? KomsulukTipi { get; set; }
    public string KaynakMekanAdKalibi { get; set; } = "";
    public string AliciMekanAdKalibi { get; set; } = "";
    public Guid? YapiElemaniId { get; set; }
}

public sealed class SablonUygulamaSonucu
{
    public int MekanGuncellemeSayisi { get; set; }
    public int BolmeAtamaSayisi { get; set; }
    public int AtlananSayisi { get; set; }
    public List<string> Uyarilar { get; set; } = new();
}
