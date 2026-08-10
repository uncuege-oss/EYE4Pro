using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.BuildingAcoustics.Reports;
using Kst.Acoustics.Core.BuildingAcoustics.Calculation;

namespace Kst.Acoustics.Core.BuildingAcoustics.Workflow;

public sealed record M08RaporKontrolBulgusu(
    string Kod,
    RaporOnKontrolSeviyesi Seviye,
    string Mesaj);

public sealed class M08RaporUygulanabilirlikSonucu
{
    public bool AkustikProjeGerekli { get; internal set; }
    public bool AkustikProjeKarariKesin { get; internal set; } = true;
    public List<string> AkustikProjeGerekceleri { get; } = new();
    public List<M08RaporKontrolBulgusu> Bulgular { get; } = new();
    public bool YayinaEngelVar => Bulgular.Any(x => x.Seviye == RaporOnKontrolSeviyesi.Engel);
    public int EngelSayisi => Bulgular.Count(x => x.Seviye == RaporOnKontrolSeviyesi.Engel);
    public int UyariSayisi => Bulgular.Count(x => x.Seviye == RaporOnKontrolSeviyesi.Uyari);
}

/// <summary>
/// Madde 5/6 belge yolunu ve EK-10 ölçüm tabanlı çıktı kapılarını denetler. Bu servis
/// bir uzman kararının yerine geçmez; eksik eşik verisini varsaymak yerine görünür bulgu üretir.
/// </summary>
public static class M08RaporUygulanabilirlikServisi
{
    public static readonly IReadOnlyList<M08RaporSecenegi<M08RaporBelgeTuru>> BelgeTurleri =
    [
        new(M08RaporBelgeTuru.MimariAkustikRaporu, "Mimari Akustik Raporu", "Madde 5/5 tasarım raporu"),
        new(M08RaporBelgeTuru.AkustikProjeRaporPaketi, "Akustik Proje Rapor Paketi", "Madde 5/6 ayrı proje ve pafta paketi"),
        new(M08RaporBelgeTuru.SahaOlcumRaporu, "Saha Ölçüm Raporu", "Yapım/uygulama sonrası uzman ölçüm raporu"),
        new(M08RaporBelgeTuru.AkustikPerformansBelgesi, "Akustik Performans Belgesi", "EK-10 ölçüm tabanlı sınıflandırma belgesi"),
    ];

    public static readonly IReadOnlyList<M08RaporSecenegi<M08RaporYayinDurumu>> YayinDurumlari =
    [
        new(M08RaporYayinDurumu.Taslak, "Taslak", "Eksik alanlar görünür; resmî yayına uygun değildir"),
        new(M08RaporYayinDurumu.KontrolEdildi, "Kontrol Edildi", "Teknik kontrol tamamlandı"),
        new(M08RaporYayinDurumu.OnayaHazir, "Onaya Hazır", "Ön kontrol engeli bulunmamalıdır"),
        new(M08RaporYayinDurumu.Imzali, "İmzalı", "Yetkili imza ve onay kaydı bulunmalıdır"),
    ];

    public static readonly IReadOnlyList<M08RaporSecenegi<M08YapiDurumu>> YapiDurumlari =
    [
        new(M08YapiDurumu.YeniYapi, "Yeni yapı", "Yeni yapılarda en az C sınıfı hedefi"),
        new(M08YapiDurumu.KullanimDegisikligi, "Kullanım değişikliği", "Etkilenen kapsamda en az D sınıfı"),
        new(M08YapiDurumu.EsasliTadilat, "Esaslı tadilat", "Etkilenen kapsamda en az D sınıfı"),
        new(M08YapiDurumu.MevcutYapiDegerlendirme, "Mevcut yapı değerlendirmesi", "Kapsam ve hedef uzman tarafından belirtilir"),
    ];

    public static M08RaporUygulanabilirlikSonucu Degerlendir(
        BinaProje proje,
        ISet<Guid>? haricTutulanSonucIdleri = null)
    {
        ArgumentNullException.ThrowIfNull(proje);
        var a = proje.RaporAyarlari ?? new M08RaporAyarlari();
        var result = new M08RaporUygulanabilirlikSonucu();
        void Add(string kod, RaporOnKontrolSeviyesi seviye, string mesaj) =>
            result.Bulgular.Add(new(kod, seviye, mesaj));
        void Gerekce(string metin)
        {
            result.AkustikProjeGerekli = true;
            result.AkustikProjeGerekceleri.Add(metin);
        }

        if (a.KapsamaEsasKatSayisi <= 0)
        {
            result.AkustikProjeKarariKesin = false;
            Add("M08-RAPOR-KAT-SAYISI", RaporOnKontrolSeviyesi.Uyari,
                "Madde 5/6 için kapsama esas kat sayısı girilmedi.");
        }
        if (a.ToplamInsaatAlaniM2 <= 0)
        {
            result.AkustikProjeKarariKesin = false;
            Add("M08-RAPOR-INSAAT-ALANI", RaporOnKontrolSeviyesi.Uyari,
                "Madde 5/6 için ruhsat toplam inşaat alanı girilmedi.");
        }

        if (proje.BinaIslevi == BinaIslevi.Konut && a.KapsamaEsasKatSayisi > 7)
            Gerekce("Konut yapısında kapsama esas kat sayısı 7'den fazla.");
        if (proje.BinaIslevi != BinaIslevi.Konut &&
            a.KapsamaEsasKatSayisi > 2 && a.ToplamInsaatAlaniM2 > 2000)
            Gerekce("Konut dışı yapıda alan 2.000 m²'yi ve kat sayısı 2'yi aşıyor.");
        if (a.BirdenFazlaBinaIsleviVar)
            Gerekce("Yapıda birden fazla bina işlevi bulunuyor.");
        if (a.OzelAkustikTasarimGerekli)
            Gerekce("Özel akustik tasarım gerektiren kullanım bulunuyor.");
        if (proje.AkustikPerformansSinifi is AkustikPerformansSinifi.A or AkustikPerformansSinifi.B)
            Gerekce($"Hedef akustik performans sınıfı {proje.AkustikPerformansSinifi}.");

        var resmiYayin = a.YayinDurumu is M08RaporYayinDurumu.OnayaHazir or M08RaporYayinDurumu.Imzali;
        var metadataSeviyesi = resmiYayin ? RaporOnKontrolSeviyesi.Engel : RaporOnKontrolSeviyesi.Uyari;
        if (string.IsNullOrWhiteSpace(a.RaporNumarasi))
            Add("M08-RAPOR-NO", metadataSeviyesi, "Rapor numarası girilmedi.");
        if (string.IsNullOrWhiteSpace(a.RevizyonKodu))
            Add("M08-RAPOR-REVIZYON", metadataSeviyesi, "Rapor revizyon kodu girilmedi.");
        if (string.IsNullOrWhiteSpace(a.HazirlayanAdi))
            Add("M08-RAPOR-HAZIRLAYAN", metadataSeviyesi, "Hazırlayan kişi girilmedi.");
        if (string.IsNullOrWhiteSpace(a.HazirlayanUnvani))
            Add("M08-RAPOR-UNVAN", metadataSeviyesi, "Hazırlayan unvanı girilmedi.");
        if (string.IsNullOrWhiteSpace(a.MevzuatSurumu))
            Add("M08-RAPOR-MEVZUAT", metadataSeviyesi, "Rapora esas mevzuat sürümü belirtilmedi.");
        if (string.IsNullOrWhiteSpace(proje.ProjeAdi) || string.IsNullOrWhiteSpace(proje.Adres))
            Add("M08-RAPOR-PROJE-KIMLIGI", metadataSeviyesi, "Proje adı ve proje adresi tamamlanmalıdır.");
        if (string.IsNullOrWhiteSpace(a.DisGurultuYontemi) || string.IsNullOrWhiteSpace(a.DisGurultuKonumu) ||
            string.IsNullOrWhiteSpace(a.DisGurultuKaynakBelgesi) || a.DisGurultuVeriTarihi is null)
            Add("M08-RAPOR-DIS-GURULTU-KANITI", metadataSeviyesi,
                "Dış gürültü yöntemi, konumu, kaynak belgesi ve veri tarihi tamamlanmalıdır.");
        if (string.IsNullOrWhiteSpace(a.HesapStandartlariSurumu) ||
            string.IsNullOrWhiteSpace(a.DerecelendirmeStandartlariSurumu))
            Add("M08-RAPOR-STANDART-SURUMU", metadataSeviyesi,
                "Hesap ve derecelendirme standartlarının kullanılan sürümleri belirtilmelidir.");
        if (a.YayinDurumu == M08RaporYayinDurumu.Imzali && string.IsNullOrWhiteSpace(a.OnaylayanAdi))
            Add("M08-RAPOR-ONAYLAYAN", RaporOnKontrolSeviyesi.Engel, "İmzalı belge için onaylayan kişi girilmelidir.");

        if (result.AkustikProjeGerekli && a.BelgeTuru == M08RaporBelgeTuru.MimariAkustikRaporu)
            Add("M08-RAPOR-BELGE-YOLU", resmiYayin ? RaporOnKontrolSeviyesi.Engel : RaporOnKontrolSeviyesi.Uyari,
                "Madde 5/6 koşulu oluştu; yalnız Mimari Akustik Raporu yeterli değildir, Akustik Proje Rapor Paketi seçilmelidir.");
        if (a.BelgeTuru == M08RaporBelgeTuru.AkustikProjeRaporPaketi && resmiYayin && !a.AkustikProjeHazirlandi)
            Add("M08-RAPOR-PAFTA-PAKETI", RaporOnKontrolSeviyesi.Engel,
                "Akustik proje pafta ve detay paketinin tamamlandığı doğrulanmadı.");

        var olcumTabanli = a.BelgeTuru is M08RaporBelgeTuru.SahaOlcumRaporu or M08RaporBelgeTuru.AkustikPerformansBelgesi;
        var onayliOlcumler = proje.SahaOlcumleri.Where(x =>
            x.Durum == SahaOlcumDurumu.Dogrulandi && x.RaporlamaIcinOnayli).ToList();
        if (olcumTabanli && onayliOlcumler.Count == 0)
            Add("M08-RAPOR-OLCUM-YOK", RaporOnKontrolSeviyesi.Engel,
                "Ölçüm tabanlı belge için doğrulanmış ve raporlamaya onaylı ölçüm bulunmuyor.");

        if (olcumTabanli)
        {
            if (string.IsNullOrWhiteSpace(a.OlcumStandartlariSurumu) ||
                string.IsNullOrWhiteSpace(a.ServisOlcumStandartlariSurumu) ||
                string.IsNullOrWhiteSpace(a.BelirsizlikStandardiSurumu))
                Add("M08-RAPOR-OLCUM-STANDART-SURUMU", metadataSeviyesi,
                    "Ölçüm, servis ekipmanı ölçümü ve belirsizlik standartlarının sürümleri belirtilmelidir.");
            foreach (var o in onayliOlcumler)
            {
                if (string.IsNullOrWhiteSpace(o.Standart) || string.IsNullOrWhiteSpace(o.Cihaz) ||
                    string.IsNullOrWhiteSpace(o.Operator) || o.Belirsizlik is null)
                    Add("M08-RAPOR-OLCUM-IZLENEBILIRLIK", resmiYayin ? RaporOnKontrolSeviyesi.Engel : RaporOnKontrolSeviyesi.Uyari,
                        $"{o.Ad}: standart, cihaz, operatör ve belirsizlik alanları tamamlanmalıdır.");
            }
        }

        if (a.BelgeTuru == M08RaporBelgeTuru.AkustikPerformansBelgesi)
        {
            if (proje.BagimsizBolumler.Count == 0)
                Add("M08-RAPOR-BAGIMSIZ-BOLUM", RaporOnKontrolSeviyesi.Engel,
                    "Performans belgesi örneklemesi için bağımsız bölüm tanımlanmalıdır.");
            if (string.IsNullOrWhiteSpace(a.BinaFotografiYolu) || !File.Exists(a.BinaFotografiYolu))
                Add("M08-RAPOR-BINA-FOTOGRAFI", metadataSeviyesi,
                    "EK-10 kapak bina fotoğrafı bulunamadı; geçerli bir dosya yolu seçilmelidir.");
            var ikiDbAsanlar = new OlcumKarsilastirmaServisi().Karsilastir(proje, true)
                .Where(x => x.OlcumMarji is < -2.0)
                .ToList();
            foreach (var satir in ikiDbAsanlar)
                Add("M08-RAPOR-OLCUM-2DB", RaporOnKontrolSeviyesi.Engel,
                    $"{satir.OlcumAdi}: ölçüm sonucu sınır değeri 2 dB'den fazla olumsuz yönde aşıyor ({satir.OlcumMarji:F1} dB marj).");
            PerformansBelgesiOrneklemesiniDenetle(proje, haricTutulanSonucIdleri, onayliOlcumler, Add);
        }

        if (haricTutulanSonucIdleri is { Count: > 0 })
        {
            var gerekceli = proje.RaporKapsamDisiKayitlari.Select(x => x.SonucId).ToHashSet();
            foreach (var id in haricTutulanSonucIdleri.Where(x => !gerekceli.Contains(x)))
                Add("M08-RAPOR-HARIC-GEREKCE", RaporOnKontrolSeviyesi.Uyari,
                    $"Kapsam dışı bırakılan {id:D} sonucu için gerekçe girilmedi.");
        }

        return result;
    }

    private static void PerformansBelgesiOrneklemesiniDenetle(
        BinaProje proje,
        ISet<Guid>? haric,
        IReadOnlyList<SahaOlcumKaydi> olcumler,
        Action<string, RaporOnKontrolSeviyesi, string> add)
    {
        var satirlar = new BinaAkustikRaporServisi().TabloVerisiOlustur(proje, haric);
        var gruplar = new (string Ad, Func<RaporSatiri, bool> Sonuc, Func<SahaOlcumKaydi, bool> Olcum)[]
        {
            ("İç bölme hava sesi", x => x.SonucTuru == AkustikRaporSonucTuru.HavaSesi,
                x => x.Tur == SahaOlcumTuru.BolmeHavaSesi),
            ("Dış cephe hava sesi", x => x.SonucTuru == AkustikRaporSonucTuru.DisCephe,
                x => x.Tur == SahaOlcumTuru.DisCepheHavaSesi),
            ("Darbe sesi", x => x.SonucTuru == AkustikRaporSonucTuru.DarbeSesi,
                x => x.Tur == SahaOlcumTuru.DarbeSesi),
            ("Servis/iç gürültü", x => x.SonucTuru is AkustikRaporSonucTuru.TesisatSurekli or AkustikRaporSonucTuru.TesisatKesikli,
                x => x.Tur is SahaOlcumTuru.IcGurultuSurekli or SahaOlcumTuru.IcGurultuKesikli or
                    SahaOlcumTuru.ServisEkipmaniSurekli or SahaOlcumTuru.ServisEkipmaniKesikli),
            ("Reverberasyon", x => x.SonucTuru == AkustikRaporSonucTuru.Reverberasyon,
                x => x.Tur == SahaOlcumTuru.Reverberasyon),
        };
        var tekBirim = proje.BagimsizBolumler.Count <= 1;
        var minTemel = tekBirim ? 2 : 3;
        var oran = proje.RaporAyarlari.AkustikProjeHazirlandi ? 0.05 : 0.10;

        foreach (var grup in gruplar)
        {
            var aday = satirlar.Count(grup.Sonuc);
            if (aday == 0)
            {
                add("M08-RAPOR-KAPSAM-BELIRSIZ", RaporOnKontrolSeviyesi.Engel,
                    $"{grup.Ad} için değerlendirilebilir sonuç yok; kapsam dışıysa uzman gerekçesiyle belirtilmelidir.");
                continue;
            }
            var gereken = Math.Max(minTemel, (int)Math.Ceiling(aday * oran));
            var mevcut = olcumler.Count(grup.Olcum);
            if (mevcut < gereken)
                add("M08-RAPOR-ORNEKLEME", RaporOnKontrolSeviyesi.Engel,
                    $"{grup.Ad}: en az {gereken} onaylı örnek gerekir, mevcut {mevcut}.");
        }

        var icOrtamAdayi = proje.Katlar.SelectMany(k => k.Mekanlar)
            .Count(m => m.Sinif != MekanSinifi.HesapDisi &&
                EK4IcGurultuServisi.LimitBul(proje.BinaIslevi, proje.AkustikPerformansSinifi, m.Ad, m.KullanimTipi) != null);
        if (icOrtamAdayi == 0)
        {
            add("M08-RAPOR-IC-ORTAM-KAPSAMI", RaporOnKontrolSeviyesi.Engel,
                "İç ortam arka plan gürültüsü için EK-4 ile eşleşen mekân bulunamadı.");
        }
        else
        {
            var gereken = Math.Max(minTemel, (int)Math.Ceiling(icOrtamAdayi * oran));
            var mevcut = olcumler.Count(x => x.Tur == SahaOlcumTuru.IcOrtamArkaPlanGurultusu);
            if (mevcut < gereken)
                add("M08-RAPOR-IC-ORTAM-ORNEKLEME", RaporOnKontrolSeviyesi.Engel,
                    $"İç ortam arka plan gürültüsü: en az {gereken} onaylı örnek gerekir, mevcut {mevcut}.");
        }
    }

    public static string BelgeTuruAdi(M08RaporBelgeTuru tur) =>
        BelgeTurleri.First(x => EqualityComparer<M08RaporBelgeTuru>.Default.Equals(x.Deger, tur)).Ad;

    public static string YayinDurumuAdi(M08RaporYayinDurumu durum) =>
        YayinDurumlari.First(x => EqualityComparer<M08RaporYayinDurumu>.Default.Equals(x.Deger, durum)).Ad;

    public static string YapiDurumuAdi(M08YapiDurumu durum) =>
        YapiDurumlari.First(x => EqualityComparer<M08YapiDurumu>.Default.Equals(x.Deger, durum)).Ad;
}
