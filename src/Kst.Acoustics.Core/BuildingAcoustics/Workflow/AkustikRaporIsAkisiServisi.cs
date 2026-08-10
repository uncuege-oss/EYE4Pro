using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.BuildingAcoustics.Reports;
using Kst.Acoustics.Core.BuildingAcoustics.Calculation;

namespace Kst.Acoustics.Core.BuildingAcoustics.Workflow;

/// <summary>
/// Bir raporun üretildiği anda kullanılan sonuçları taşır. Proje nesnesinin çevrimsel
/// grafiğini kopyalamaz; böylece onaylanan içerik daha sonra proje değişse de okunabilir.
/// </summary>
public sealed class AkustikRaporAnlikGoruntusu
{
    public const string CurrentFormat = "EYE4PRO-M08-REPORT-SNAPSHOT";
    public const int CurrentVersion = 2;

    public string Format { get; set; } = CurrentFormat;
    public int Version { get; set; } = CurrentVersion;
    public Guid SnapshotId { get; set; } = Guid.NewGuid();
    public DateTimeOffset OlusturulmaUtc { get; set; }
    public Guid ProjeId { get; set; }
    public string ProjeAdi { get; set; } = "";
    public string Adres { get; set; } = "";
    public string MusteriAdi { get; set; } = "";
    public string SorumluKisiKurulus { get; set; } = "";
    public string BinaSinifi { get; set; } = "";
    public string BinaIslevi { get; set; } = "";
    public string AkustikPerformansSinifi { get; set; } = "";
    public double DisGurultuDb { get; set; }
    public bool DisGurultuDogrulandi { get; set; }
    public string DisGurultuSpektrumKaynagi { get; set; } = "";
    public int? BinaGeometriRevizyon { get; set; }
    public string BelgeTuru { get; set; } = "";
    public string YayinDurumu { get; set; } = "";
    public string YapiDurumu { get; set; } = "";
    public string RaporNumarasi { get; set; } = "";
    public string RevizyonKodu { get; set; } = "";
    public List<Guid> HaricTutulanSonucIdleri { get; set; } = new();
    /// <summary>Ölçüm, ürün, junction, metraj, kapsam ve belge ayarlarının birleşik özeti.</summary>
    public string RaporEkIcerikSha256 { get; set; } = "";
    public List<AkustikRaporSnapshotSatiri> Sonuclar { get; set; } = new();
    public string RaporParmakIziSha256 { get; set; } = "";
}

public sealed class AkustikRaporSnapshotSatiri
{
    public Guid SonucId { get; set; }
    public string SonucTuru { get; set; } = "";
    public string Kat { get; set; } = "";
    public string Mekan { get; set; } = "";
    public string KaynakMekan { get; set; } = "";
    public string AliciMekan { get; set; } = "";
    public string Bolme { get; set; } = "";
    public double Deger { get; set; }
    public double Belirsizlik { get; set; }
    public string Birim { get; set; } = "";
    public double LimitDeger { get; set; }
    public double? MarjDb { get; set; }
    public bool DahaDusukDahaIyi { get; set; }
    public string Durum { get; set; } = "";
    public string YonetmelikEk { get; set; } = "";
    public string VeriKaynagi { get; set; } = "";
    public int GuvenPuani { get; set; }
    public string GuvenSeviyesi { get; set; } = "";
    public double? DisGurultuDb { get; set; }
    public double? IcGurultuDb { get; set; }
    public double? IcHedefDb { get; set; }
    public double? D2mNt50Db { get; set; }
}

public static class AkustikRaporSnapshotServisi
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static AkustikRaporAnlikGoruntusu Olustur(
        BinaProje proje,
        ISet<Guid>? haricTutulanSonucIdleri = null,
        DateTimeOffset? olusturulmaUtc = null)
    {
        ArgumentNullException.ThrowIfNull(proje);

        // Kaydedilmiş hesap DTO'larına güvenme: her snapshot aynı kanonik hesap motorundan,
        // kaynak modeli değiştirmeyen derin kopya üzerinde üretilir.
        var degerlendirme = new BinaAkustikDegerlendirmeServisi()
            .Hesapla(proje, haricTutulanSonucIdleri);
        var hesaplanan = degerlendirme.HesaplananProje;

        var snapshot = new AkustikRaporAnlikGoruntusu
        {
            OlusturulmaUtc = (olusturulmaUtc ?? DateTimeOffset.UtcNow).ToUniversalTime(),
            ProjeId = hesaplanan.Id,
            ProjeAdi = hesaplanan.ProjeAdi?.Trim() ?? "",
            Adres = hesaplanan.Adres?.Trim() ?? "",
            MusteriAdi = hesaplanan.MusteriAdi?.Trim() ?? "",
            SorumluKisiKurulus = hesaplanan.SorumluKisiKurulus?.Trim() ?? "",
            BinaSinifi = hesaplanan.BinaSinifi.ToString(),
            BinaIslevi = hesaplanan.BinaIslevi.ToString(),
            AkustikPerformansSinifi = hesaplanan.AkustikPerformansSinifi.ToString(),
            DisGurultuDb = hesaplanan.DisGurultuDb,
            DisGurultuDogrulandi = hesaplanan.DisGurultuKullaniciTarafindanDogrulandi,
            DisGurultuSpektrumKaynagi = hesaplanan.DisGurultuSpektrumKaynagi.ToString(),
            BinaGeometriRevizyon = hesaplanan.BinaGeometriRevizyon,
            BelgeTuru = hesaplanan.RaporAyarlari.BelgeTuru.ToString(),
            YayinDurumu = hesaplanan.RaporAyarlari.YayinDurumu.ToString(),
            YapiDurumu = hesaplanan.RaporAyarlari.YapiDurumu.ToString(),
            RaporNumarasi = hesaplanan.RaporAyarlari.RaporNumarasi?.Trim() ?? "",
            RevizyonKodu = hesaplanan.RaporAyarlari.RevizyonKodu?.Trim() ?? "",
            HaricTutulanSonucIdleri = (haricTutulanSonucIdleri ?? new HashSet<Guid>()).OrderBy(x => x).ToList(),
            RaporEkIcerikSha256 = RaporEkIcerikParmakIzi(hesaplanan, haricTutulanSonucIdleri),
            Sonuclar = degerlendirme.Satirlar
                .OrderBy(x => x.SonucId)
                .ThenBy(x => x.SonucTuru)
                .Select(SatiraDonustur)
                .ToList(),
        };
        snapshot.RaporParmakIziSha256 = ParmakIziHesapla(snapshot);
        return snapshot;
    }

    public static string ParmakIziHesapla(AkustikRaporAnlikGoruntusu snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        // Snapshot kimliği ve oluşturulma zamanı rapor içeriğinin parçası değildir. Aynı
        // girdiler aynı parmak izini üretir; içerikteki en küçük değişiklik izi değiştirir.
        if (snapshot.Version <= 1)
        {
            var eski = new
            {
                snapshot.Format, snapshot.Version, snapshot.ProjeId, snapshot.ProjeAdi, snapshot.Adres,
                snapshot.MusteriAdi, snapshot.SorumluKisiKurulus, snapshot.BinaSinifi, snapshot.BinaIslevi,
                snapshot.AkustikPerformansSinifi, snapshot.DisGurultuDb, snapshot.DisGurultuDogrulandi,
                snapshot.DisGurultuSpektrumKaynagi, snapshot.BinaGeometriRevizyon,
                Sonuclar = snapshot.Sonuclar.OrderBy(x => x.SonucId).ThenBy(x => x.SonucTuru).ToArray(),
            };
            return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(eski, JsonOptions)));
        }

        var canonical = new
        {
            snapshot.Format, snapshot.Version, snapshot.ProjeId, snapshot.ProjeAdi, snapshot.Adres,
            snapshot.MusteriAdi, snapshot.SorumluKisiKurulus, snapshot.BinaSinifi, snapshot.BinaIslevi,
            snapshot.AkustikPerformansSinifi, snapshot.DisGurultuDb, snapshot.DisGurultuDogrulandi,
            snapshot.DisGurultuSpektrumKaynagi, snapshot.BinaGeometriRevizyon,
            snapshot.BelgeTuru, snapshot.YayinDurumu, snapshot.YapiDurumu,
            snapshot.RaporNumarasi, snapshot.RevizyonKodu,
            HaricTutulanSonucIdleri = snapshot.HaricTutulanSonucIdleri.OrderBy(x => x).ToArray(),
            snapshot.RaporEkIcerikSha256,
            Sonuclar = snapshot.Sonuclar.OrderBy(x => x.SonucId).ThenBy(x => x.SonucTuru).ToArray(),
        };
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(canonical, JsonOptions)));
    }

    public static bool ParmakIziGecerliMi(AkustikRaporAnlikGoruntusu snapshot)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(snapshot.RaporParmakIziSha256) ||
                snapshot.RaporParmakIziSha256.Length != 64 || snapshot.Sonuclar == null)
                return false;
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(snapshot.RaporParmakIziSha256),
                Convert.FromHexString(ParmakIziHesapla(snapshot)));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static byte[] JsonaDonustur(AkustikRaporAnlikGoruntusu snapshot, bool girintili = true) =>
        JsonSerializer.SerializeToUtf8Bytes(snapshot, new JsonSerializerOptions(JsonOptions) { WriteIndented = girintili });

    public static AkustikRaporAnlikGoruntusu JsonOku(ReadOnlySpan<byte> json)
    {
        var snapshot = JsonSerializer.Deserialize<AkustikRaporAnlikGoruntusu>(json, JsonOptions)
            ?? throw new InvalidDataException("Rapor anlık görüntüsü okunamadı.");
        snapshot.Sonuclar ??= new();
        snapshot.HaricTutulanSonucIdleri ??= new();
        return snapshot;
    }

    public static string RaporEkIcerikParmakIzi(BinaProje proje, ISet<Guid>? haric = null)
    {
        var haricler = haric ?? new HashSet<Guid>();
        var icerik = new
        {
            Ayarlar = proje.RaporAyarlari,
            KapsamDisi = proje.RaporKapsamDisiKayitlari
                .Where(x => haricler.Contains(x.SonucId)).OrderBy(x => x.SonucId).ToArray(),
            OnayliOlcumler = proje.SahaOlcumleri
                .Where(x => x.Durum == SahaOlcumDurumu.Dogrulandi && x.RaporlamaIcinOnayli)
                .OrderBy(x => x.Id).ToArray(),
            Junctionlar = proje.Katlar.SelectMany(k => k.Mekanlar.SelectMany(m =>
                m.Bolmeler.Where(b => !haricler.Contains(b.Id)).SelectMany(b => b.Junctions.Select(j => new
                {
                    Kat = k.Ad, MekanId = m.Id, BolmeId = b.Id, j.Kenar, j.GeometriTipi,
                    j.BaglantiTipi, j.StrukturelTipi, j.Uzunluk, j.KijOverrideDb,
                    j.DetayReferansi, j.UygulamaNotu,
                })))).OrderBy(x => x.BolmeId).ThenBy(x => x.Kenar).ThenBy(x => x.GeometriTipi).ToArray(),
            Urunler = proje.YapiElemaniKutuphanesi.OrderBy(x => x.Id).Select(x => new
            {
                x.Id, x.Ad, x.Uretici, x.UrunKodu, x.Katmanlar, x.Rw, x.C, x.Ctr, x.Lnw, x.DeltaLw,
                x.SpektrumKaynagi, x.DarbeSpektrumKaynagi, Belgeler = x.Belgeler.OrderBy(b => b.Id).ToArray(),
            }).ToArray(),
            Metraj = new AkustikMetrajServisi().Hesapla(proje).OrderBy(x => x.YapiElemaniId).ToArray(),
            proje.DisGurultuKaynaklari,
        };
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(icerik, JsonOptions)));
    }

    private static AkustikRaporSnapshotSatiri SatiraDonustur(RaporSatiri x) => new()
    {
        SonucId = x.SonucId,
        SonucTuru = x.SonucTuru.ToString(),
        Kat = x.Kat,
        Mekan = x.Mekan,
        KaynakMekan = x.KaynakMekan,
        AliciMekan = x.AliciMekan,
        Bolme = x.Bolme,
        Deger = GuvenliSayi(x.Deger),
        Belirsizlik = GuvenliSayi(x.Belirsizlik),
        Birim = x.Birim,
        LimitDeger = GuvenliSayi(x.LimitDeger),
        MarjDb = double.IsFinite(x.MarjDb) ? x.MarjDb : null,
        DahaDusukDahaIyi = x.DahaDusukDahaIyi,
        Durum = x.Durum,
        YonetmelikEk = x.YonetmelikEk,
        VeriKaynagi = x.VeriKaynagi,
        GuvenPuani = x.GuvenPuani,
        GuvenSeviyesi = x.GuvenSeviyesi,
        DisGurultuDb = GuvenliSayi(x.DisGurultuDb),
        IcGurultuDb = GuvenliSayi(x.IcGurultuDb),
        IcHedefDb = GuvenliSayi(x.IcHedefDb),
        D2mNt50Db = GuvenliSayi(x.D2mNt50Db),
    };

    private static double GuvenliSayi(double value) => double.IsFinite(value) ? value : 0;
    private static double? GuvenliSayi(double? value) => value is { } n && double.IsFinite(n) ? n : null;
}

public enum RaporOnKontrolSeviyesi { Bilgi, Uyari, Engel }

public sealed record RaporOnKontrolBulgusu(
    string Kod,
    RaporOnKontrolSeviyesi Seviye,
    string Mesaj,
    Guid? SonucId = null);

public sealed class RaporOnKontrolSonucu
{
    public List<RaporOnKontrolBulgusu> Bulgular { get; } = new();
    public bool OnaylanabilirMi => Bulgular.All(x => x.Seviye != RaporOnKontrolSeviyesi.Engel);
    public int EngelSayisi => Bulgular.Count(x => x.Seviye == RaporOnKontrolSeviyesi.Engel);
    public int UyariSayisi => Bulgular.Count(x => x.Seviye == RaporOnKontrolSeviyesi.Uyari);
}

public static class RaporOnKontrolServisi
{
    public static RaporOnKontrolSonucu Denetle(AkustikRaporAnlikGoruntusu snapshot, bool onayIcin = true)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var result = new RaporOnKontrolSonucu();
        void Add(string code, RaporOnKontrolSeviyesi level, string message, Guid? id = null) =>
            result.Bulgular.Add(new(code, level, message, id));

        if (snapshot.Format != AkustikRaporAnlikGoruntusu.CurrentFormat ||
            snapshot.Version is < 1 or > AkustikRaporAnlikGoruntusu.CurrentVersion)
            Add("M08-RAPOR-BICIM", RaporOnKontrolSeviyesi.Engel, "Rapor anlık görüntüsü desteklenmeyen biçimde.");
        if (!AkustikRaporSnapshotServisi.ParmakIziGecerliMi(snapshot))
            Add("M08-RAPOR-PARMAK-IZI", RaporOnKontrolSeviyesi.Engel, "Rapor içeriğinin SHA-256 parmak izi doğrulanamadı.");
        if (string.IsNullOrWhiteSpace(snapshot.ProjeAdi))
            Add("M08-RAPOR-PROJE-ADI", RaporOnKontrolSeviyesi.Engel, "Proje adı girilmelidir.");
        if (string.IsNullOrWhiteSpace(snapshot.Adres))
            Add("M08-RAPOR-ADRES", RaporOnKontrolSeviyesi.Engel, "Proje adresi girilmelidir.");
        if (string.IsNullOrWhiteSpace(snapshot.MusteriAdi))
            Add("M08-RAPOR-MUSTERI", RaporOnKontrolSeviyesi.Engel, "Müşteri / yapı sahibi girilmelidir.");
        if (string.IsNullOrWhiteSpace(snapshot.SorumluKisiKurulus))
            Add("M08-RAPOR-SORUMLU", RaporOnKontrolSeviyesi.Engel, "Sorumlu kişi ve kuruluş girilmelidir.");
        if (snapshot.Sonuclar.Count == 0)
            Add("M08-RAPOR-SONUC-YOK", RaporOnKontrolSeviyesi.Engel, "Rapora dahil edilmiş değerlendirme sonucu yok.");
        if (snapshot.Version >= 2 && string.IsNullOrWhiteSpace(snapshot.RaporEkIcerikSha256))
            Add("M08-RAPOR-EK-ICERIK", RaporOnKontrolSeviyesi.Engel,
                "Rapor ayarı, ölçüm, ürün ve birleşim eklerinin içerik özeti eksik.");

        foreach (var row in snapshot.Sonuclar.Where(x =>
                     string.Equals(x.Durum, "VeriEksik", StringComparison.OrdinalIgnoreCase)))
            Add("M08-RAPOR-VERI-EKSIK", RaporOnKontrolSeviyesi.Engel,
                $"{row.Kat} / {row.Mekan} / {row.Bolme} sonucunda veri eksik.", row.SonucId);

        if (snapshot.Sonuclar.Any(x => x.SonucTuru == nameof(AkustikRaporSonucTuru.DisCephe)))
        {
            if (snapshot.DisGurultuDb <= 0 || !double.IsFinite(snapshot.DisGurultuDb))
                Add("M08-RAPOR-DIS-GURULTU", RaporOnKontrolSeviyesi.Engel, "Geçerli dış gürültü seviyesi girilmelidir.");
            if (!snapshot.DisGurultuDogrulandi)
                Add("M08-RAPOR-DIS-GURULTU-DOGRULAMA",
                    onayIcin ? RaporOnKontrolSeviyesi.Engel : RaporOnKontrolSeviyesi.Uyari,
                    "Dış gürültü girdisinin ölçüm veya doğrulanmış hesap kaynağı teyit edilmedi.");
            if (string.Equals(snapshot.DisGurultuSpektrumKaynagi, nameof(SpektrumVeriKaynagi.Belirsiz), StringComparison.Ordinal))
                Add("M08-RAPOR-SPEKTRUM-KAYNAGI", RaporOnKontrolSeviyesi.Uyari,
                    "Dış gürültü spektrumunun veri kaynağı belirsiz.");
        }

        foreach (var row in snapshot.Sonuclar.Where(x =>
                     string.IsNullOrWhiteSpace(x.VeriKaynagi) ||
                     x.VeriKaynagi.Contains("Belirsiz", StringComparison.OrdinalIgnoreCase)))
            Add("M08-RAPOR-IZLENEBILIRLIK", RaporOnKontrolSeviyesi.Uyari,
                $"{row.Kat} / {row.Mekan} sonucunun veri kökeni tam izlenebilir değil.", row.SonucId);

        Add("M08-RAPOR-ON-DEGERLENDIRME", RaporOnKontrolSeviyesi.Bilgi,
            "Bu çıktı hesap tabanlı mimari akustik ön değerlendirmedir; saha ölçüm belgesi veya nitelikli elektronik imza değildir.");
        return result;
    }
}

public sealed class RaporOnayIslemiSonucu
{
    public required RaporOnKontrolSonucu OnKontrol { get; init; }
    public ProjeOnayKaydi? OnayKaydi { get; init; }
    public DenetimKaydi? DenetimKaydi { get; init; }
}

public static class RaporOnayServisi
{
    /// <summary>Ön kontrol geçerse onay ve denetim kaydını projeye ekler.</summary>
    public static RaporOnayIslemiSonucu Onayla(
        BinaProje proje,
        AkustikRaporAnlikGoruntusu snapshot,
        string onaylayan,
        string kurulus = "",
        string aciklama = "",
        string imzaOzeti = "",
        DateTimeOffset? tarihUtc = null)
    {
        ArgumentNullException.ThrowIfNull(proje);
        ArgumentNullException.ThrowIfNull(snapshot);
        var check = RaporOnKontrolServisi.Denetle(snapshot, onayIcin: true);
        if (snapshot.ProjeId != proje.Id)
            check.Bulgular.Add(new("M08-RAPOR-PROJE-KIMLIGI", RaporOnKontrolSeviyesi.Engel,
                "Anlık görüntü başka bir projeye ait."));
        else
        {
            var allIds = new BinaAkustikRaporServisi().TabloVerisiOlustur(proje).Select(x => x.SonucId).ToHashSet();
            var includedIds = snapshot.Sonuclar.Select(x => x.SonucId).ToHashSet();
            allIds.ExceptWith(includedIds);
            var current = AkustikRaporSnapshotServisi.Olustur(proje, allIds, snapshot.OlusturulmaUtc);
            if (!StringComparer.OrdinalIgnoreCase.Equals(current.RaporParmakIziSha256, snapshot.RaporParmakIziSha256))
                check.Bulgular.Add(new("M08-RAPOR-GUNCELLIK", RaporOnKontrolSeviyesi.Engel,
                    "Proje, anlık görüntü alındıktan sonra değişmiş. Onay için yeni snapshot oluşturulmalıdır."));
        }
        if (string.IsNullOrWhiteSpace(onaylayan))
            check.Bulgular.Add(new("M08-RAPOR-ONAYLAYAN", RaporOnKontrolSeviyesi.Engel,
                "Onaylayan kişi belirtilmelidir."));
        if (!check.OnaylanabilirMi)
            return new() { OnKontrol = check };

        var tarih = (tarihUtc ?? DateTimeOffset.UtcNow).UtcDateTime;
        var approval = new ProjeOnayKaydi
        {
            Tur = ProjeOnayTuru.Rapor,
            Durum = ProjeOnayDurumu.Onaylandi,
            Onaylayan = onaylayan.Trim(),
            Kurulus = kurulus.Trim(),
            Aciklama = aciklama.Trim(),
            ImzaOzeti = imzaOzeti.Trim(),
            GirdiParmakIzi = snapshot.RaporParmakIziSha256,
            Tarih = tarih,
        };
        var audit = new DenetimKaydi
        {
            Tarih = tarih,
            Eylem = DenetimEylemi.Onaylama,
            Kullanici = approval.Onaylayan,
            HedefTuru = "M08RaporAnlikGoruntusu",
            HedefId = snapshot.SnapshotId.ToString("D"),
            Aciklama = "Rapor anlık görüntüsü onaylandı.",
            YeniDegerOzeti = $"SHA-256:{snapshot.RaporParmakIziSha256}",
            GirdiParmakIzi = snapshot.RaporParmakIziSha256,
        };
        proje.OnayKayitlari.Add(approval);
        proje.DenetimKayitlari.Add(audit);
        return new() { OnKontrol = check, OnayKaydi = approval, DenetimKaydi = audit };
    }
}
