using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.BuildingAcoustics.Workflow;
using Kst.Acoustics.Core.BuildingAcoustics.Calculation;

namespace Kst.Acoustics.Core.BuildingAcoustics.Reports;

public sealed class E4R08PaketDosyasi
{
    public string Yol { get; set; } = "";
    public long Boyut { get; set; }
    public string Sha256 { get; set; } = "";
}

public sealed class E4R08Manifest
{
    public const string CurrentFormat = "EYE4PRO-M08-FROZEN-REPORT";
    public const int CurrentVersion = 2;

    public string Format { get; set; } = CurrentFormat;
    public int Version { get; set; } = CurrentVersion;
    public Guid ProjeId { get; set; }
    public Guid SnapshotId { get; set; }
    public DateTimeOffset OlusturulmaUtc { get; set; }
    public string RaporParmakIziSha256 { get; set; } = "";
    public List<E4R08PaketDosyasi> Dosyalar { get; set; } = new();
    public string ImzaAlgoritmasi { get; set; } = "";
    public string SertifikaSha256 { get; set; } = "";
    public string ImzaAciklamasi { get; set; } =
        "Kriptografik bütünlük imzasıdır; nitelikli elektronik imza değildir.";
}

public sealed class E4R08PaketOlusturmaSonucu
{
    public required E4R08Manifest Manifest { get; init; }
    public required AkustikRaporAnlikGoruntusu Snapshot { get; init; }
    public bool KriptografikImzali { get; init; }
    public List<string> Uyarilar { get; init; } = new();
}

public sealed class E4R08PaketEkKaydi
{
    public string Tur { get; set; } = "";
    public string KaynakYol { get; set; } = "";
    public string PaketYolu { get; set; } = "";
    public bool PaketeEklendi { get; set; }
    public string Durum { get; set; } = "";
    public string Sha256 { get; set; } = "";
}

public sealed class E4R08PaketDogrulamaSonucu
{
    public E4R08Manifest? Manifest { get; internal set; }
    public AkustikRaporAnlikGoruntusu? Snapshot { get; internal set; }
    public List<string> Hatalar { get; } = new();
    public List<string> Uyarilar { get; } = new();
    public bool ButunlukGecerli { get; internal set; }
    public bool ImzaVar { get; internal set; }
    public bool KriptografikImzaGecerli { get; internal set; }
    public bool SertifikaZinciriGecerli { get; internal set; }
    public bool Gecerli => Hatalar.Count == 0 && ButunlukGecerli && (!ImzaVar || KriptografikImzaGecerli);
}

/// <summary>
/// HTML, sonuç anlık görüntüsü ve SHA-256 manifestini tek, salt okunur teslim paketinde
/// toplar. Opsiyonel X.509 imzası yalnız kriptografik bütünlük sağlar; hukuki/nitelikli
/// elektronik imza olduğuna dair bir iddia taşımaz.
/// </summary>
public static class E4R08RaporPaketServisi
{
    private const long MaxEntryBytes = 64L * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    public static E4R08PaketOlusturmaSonucu Olustur(
        BinaProje proje,
        Stream hedef,
        ISet<Guid>? haricTutulanSonucIdleri = null,
        X509Certificate2? imzaSertifikasi = null,
        DateTimeOffset? olusturulmaUtc = null)
    {
        var snapshot = AkustikRaporSnapshotServisi.Olustur(proje, haricTutulanSonucIdleri, olusturulmaUtc);
        return Olustur(proje, snapshot, hedef, haricTutulanSonucIdleri, imzaSertifikasi);
    }

    public static E4R08PaketOlusturmaSonucu Olustur(
        BinaProje proje,
        AkustikRaporAnlikGoruntusu snapshot,
        Stream hedef,
        ISet<Guid>? haricTutulanSonucIdleri = null,
        X509Certificate2? imzaSertifikasi = null)
    {
        ArgumentNullException.ThrowIfNull(proje);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(hedef);
        if (!hedef.CanWrite) throw new ArgumentException("Hedef akış yazılabilir olmalıdır.", nameof(hedef));
        if (snapshot.ProjeId != proje.Id)
            throw new InvalidOperationException("Rapor anlık görüntüsü başka bir projeye ait.");
        if (!AkustikRaporSnapshotServisi.ParmakIziGecerliMi(snapshot))
            throw new InvalidDataException("Rapor anlık görüntüsünün parmak izi geçersiz.");

        // Proje, onaylanan anlık görüntüden sonra değiştiyse yanlış HTML ile eski snapshot'ın
        // aynı pakete konmasını engelle.
        var current = AkustikRaporSnapshotServisi.Olustur(proje, haricTutulanSonucIdleri, snapshot.OlusturulmaUtc);
        if (!StringComparer.Ordinal.Equals(current.RaporParmakIziSha256, snapshot.RaporParmakIziSha256))
            throw new InvalidOperationException("Proje, rapor anlık görüntüsü alındıktan sonra değişmiş. Yeni snapshot oluşturulmalıdır.");

        var paketProjesi = new BinaAkustikDegerlendirmeServisi()
            .Hesapla(proje, haricTutulanSonucIdleri).HesaplananProje;
        var html = Encoding.UTF8.GetBytes(new MimariAkustikRaporServisi().HtmlOlustur(paketProjesi, haricTutulanSonucIdleri));
        var snapshotJson = AkustikRaporSnapshotServisi.JsonaDonustur(snapshot);
        var uyarilar = new List<string>();
        var paketDosyalari = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["report.html"] = html,
            ["snapshot.json"] = snapshotJson,
            ["data/report-settings.json"] = JsonSerializer.SerializeToUtf8Bytes(paketProjesi.RaporAyarlari, JsonOptions),
            ["data/exclusions.json"] = JsonSerializer.SerializeToUtf8Bytes(
                paketProjesi.RaporKapsamDisiKayitlari
                    .Where(x => haricTutulanSonucIdleri?.Contains(x.SonucId) == true)
                    .OrderBy(x => x.SonucId).ToArray(), JsonOptions),
            ["data/measurements.json"] = JsonSerializer.SerializeToUtf8Bytes(
                paketProjesi.SahaOlcumleri.Where(x => x.Durum == SahaOlcumDurumu.Dogrulandi && x.RaporlamaIcinOnayli)
                    .OrderBy(x => x.Id).ToArray(), JsonOptions),
            ["data/products.json"] = JsonSerializer.SerializeToUtf8Bytes(
                paketProjesi.YapiElemaniKutuphanesi.OrderBy(x => x.Id).ToArray(), JsonOptions),
            ["data/junctions.json"] = JsonSerializer.SerializeToUtf8Bytes(
                paketProjesi.Katlar.SelectMany(k => k.Mekanlar.SelectMany(m => m.Bolmeler.SelectMany(b =>
                    b.Junctions.Select(j => new { Kat = k.Ad, MekanId = m.Id, BolmeId = b.Id, Junction = j }))))
                    .OrderBy(x => x.BolmeId).ThenBy(x => x.Junction.Id).ToArray(), JsonOptions),
        };
        var ekIndeksi = EkDosyalariniEkle(paketProjesi, paketDosyalari, uyarilar);
        paketDosyalari["data/attachment-index.json"] = JsonSerializer.SerializeToUtf8Bytes(ekIndeksi, JsonOptions);
        var manifest = new E4R08Manifest
        {
            ProjeId = proje.Id,
            SnapshotId = snapshot.SnapshotId,
            OlusturulmaUtc = snapshot.OlusturulmaUtc,
            RaporParmakIziSha256 = snapshot.RaporParmakIziSha256,
            Dosyalar = paketDosyalari.Select(x => DosyaBilgisi(x.Key, x.Value)).ToList(),
        };

        byte[]? signature = null;
        byte[]? certificate = null;
        if (imzaSertifikasi != null)
        {
            if (!imzaSertifikasi.HasPrivateKey)
                throw new CryptographicException("İmza sertifikası özel anahtar içermiyor.");
            manifest.ImzaAlgoritmasi = ImzaAlgoritmasi(imzaSertifikasi);
            certificate = imzaSertifikasi.Export(X509ContentType.Cert);
            manifest.SertifikaSha256 = Sha256(certificate);
        }

        var manifestJson = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
        if (imzaSertifikasi != null)
            signature = Imzala(manifestJson, imzaSertifikasi, manifest.ImzaAlgoritmasi);

        using (var zip = new ZipArchive(hedef, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8))
        {
            foreach (var dosya in paketDosyalari)
                Yaz(zip, dosya.Key, dosya.Value);
            Yaz(zip, "manifest.json", manifestJson);
            if (signature != null && certificate != null)
            {
                Yaz(zip, "signature.bin", signature);
                Yaz(zip, "certificate.cer", certificate);
            }
        }

        return new() { Manifest = manifest, Snapshot = snapshot, KriptografikImzali = signature != null, Uyarilar = uyarilar };
    }

    public static E4R08PaketOlusturmaSonucu DosyayaOlustur(
        BinaProje proje,
        string dosyaYolu,
        ISet<Guid>? haricTutulanSonucIdleri = null,
        X509Certificate2? imzaSertifikasi = null,
        DateTimeOffset? olusturulmaUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dosyaYolu);
        var tamYol = Path.GetFullPath(dosyaYolu);
        Directory.CreateDirectory(Path.GetDirectoryName(tamYol)!);
        var temp = tamYol + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            E4R08PaketOlusturmaSonucu result;
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                result = Olustur(proje, stream, haricTutulanSonucIdleri, imzaSertifikasi, olusturulmaUtc);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, tamYol, overwrite: true);
            return result;
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    public static E4R08PaketDogrulamaSonucu Dogrula(Stream kaynak, bool sertifikaZinciriniDogrula = false)
    {
        ArgumentNullException.ThrowIfNull(kaynak);
        var result = new E4R08PaketDogrulamaSonucu();
        try
        {
            using var zip = new ZipArchive(kaynak, ZipArchiveMode.Read, leaveOpen: true, Encoding.UTF8);
            var duplicates = zip.Entries.GroupBy(x => x.FullName, StringComparer.Ordinal)
                .Where(x => x.Count() > 1).Select(x => x.Key).ToArray();
            if (duplicates.Length > 0)
                throw new InvalidDataException("Pakette yinelenen dosya adı var: " + string.Join(", ", duplicates));

            var manifestBytes = Oku(zip, "manifest.json", required: true)!;
            var manifest = JsonSerializer.Deserialize<E4R08Manifest>(manifestBytes, JsonOptions)
                ?? throw new InvalidDataException("Manifest okunamadı.");
            result.Manifest = manifest;
            if (manifest.Format != E4R08Manifest.CurrentFormat || manifest.Version is < 1 or > E4R08Manifest.CurrentVersion)
                throw new InvalidDataException("Desteklenmeyen .e4r08 paket biçimi.");

            manifest.Dosyalar ??= new();
            var manifestPaths = manifest.Dosyalar.Select(x => x.Yol).ToHashSet(StringComparer.Ordinal);
            if (manifest.Dosyalar.Count != manifestPaths.Count)
                throw new InvalidDataException("Manifestte yinelenen dosya yolu var.");
            var zorunlu = manifest.Version == 1
                ? new HashSet<string>(["report.html", "snapshot.json"], StringComparer.Ordinal)
                : new HashSet<string>(
                [
                    "report.html", "snapshot.json", "data/report-settings.json", "data/exclusions.json",
                    "data/measurements.json", "data/products.json", "data/junctions.json",
                    "data/attachment-index.json",
                ], StringComparer.Ordinal);
            if (manifest.Version == 1 ? !manifestPaths.SetEquals(zorunlu) : !zorunlu.IsSubsetOf(manifestPaths))
                throw new InvalidDataException("Manifest zorunlu rapor ve veri dosyalarını tanımlamıyor.");
            foreach (var item in manifest.Dosyalar)
            {
                if (!GuvenliYol(item.Yol))
                    throw new InvalidDataException($"Güvensiz paket yolu: {item.Yol}");
                var bytes = Oku(zip, item.Yol, required: true)!;
                if (bytes.LongLength != item.Boyut || !StringComparer.OrdinalIgnoreCase.Equals(Sha256(bytes), item.Sha256))
                    result.Hatalar.Add($"{item.Yol} bütünlük denetimini geçemedi.");
            }

            var snapshotBytes = Oku(zip, "snapshot.json", required: true)!;
            var snapshot = AkustikRaporSnapshotServisi.JsonOku(snapshotBytes);
            result.Snapshot = snapshot;
            if (snapshot.ProjeId != manifest.ProjeId || snapshot.SnapshotId != manifest.SnapshotId ||
                !StringComparer.OrdinalIgnoreCase.Equals(snapshot.RaporParmakIziSha256, manifest.RaporParmakIziSha256) ||
                !AkustikRaporSnapshotServisi.ParmakIziGecerliMi(snapshot))
                result.Hatalar.Add("Snapshot kimliği veya rapor parmak izi manifest ile uyuşmuyor.");

            var signature = Oku(zip, "signature.bin", required: false);
            var certBytes = Oku(zip, "certificate.cer", required: false);
            result.ImzaVar = signature != null || certBytes != null || !string.IsNullOrWhiteSpace(manifest.ImzaAlgoritmasi);
            var allowedEntries = manifestPaths.ToHashSet(StringComparer.Ordinal);
            allowedEntries.Add("manifest.json");
            if (result.ImzaVar)
            {
                allowedEntries.Add("signature.bin");
                allowedEntries.Add("certificate.cer");
            }
            var extras = zip.Entries.Select(x => x.FullName).Where(x => !allowedEntries.Contains(x)).ToArray();
            if (extras.Length > 0)
                result.Hatalar.Add("Pakette manifest dışı dosya var: " + string.Join(", ", extras));
            if (result.ImzaVar)
            {
                if (signature == null || certBytes == null || string.IsNullOrWhiteSpace(manifest.ImzaAlgoritmasi))
                    result.Hatalar.Add("Kriptografik imza bileşenleri eksik.");
                else if (!StringComparer.OrdinalIgnoreCase.Equals(Sha256(certBytes), manifest.SertifikaSha256))
                    result.Hatalar.Add("İmza sertifikası özeti manifest ile uyuşmuyor.");
                else
                {
                    using var cert = X509CertificateLoader.LoadCertificate(certBytes);
                    result.KriptografikImzaGecerli = ImzayiDogrula(manifestBytes, signature, cert, manifest.ImzaAlgoritmasi);
                    if (!result.KriptografikImzaGecerli)
                        result.Hatalar.Add("Manifest kriptografik imzası doğrulanamadı.");
                    if (sertifikaZinciriniDogrula)
                    {
                        using var chain = new X509Chain();
                        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                        result.SertifikaZinciriGecerli = chain.Build(cert);
                        if (!result.SertifikaZinciriGecerli)
                            result.Uyarilar.Add("Sertifika güven zinciri doğrulanamadı; içerik imzası yine de ayrı olarak denetlendi.");
                    }
                }
            }
            result.ButunlukGecerli = result.Hatalar.Count == 0;
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or CryptographicException or IOException or ArgumentException or FormatException)
        {
            result.Hatalar.Add(ex.Message);
            result.ButunlukGecerli = false;
        }
        return result;
    }

    private static List<E4R08PaketEkKaydi> EkDosyalariniEkle(
        BinaProje proje,
        IDictionary<string, byte[]> paket,
        ICollection<string> uyarilar)
    {
        var indeks = new List<E4R08PaketEkKaydi>();
        if (proje.RaporAyarlari.BelgeTuru == M08RaporBelgeTuru.AkustikPerformansBelgesi ||
            !string.IsNullOrWhiteSpace(proje.RaporAyarlari.BinaFotografiYolu))
        {
            var fotoYolu = proje.RaporAyarlari.BinaFotografiYolu;
            var fotoAdi = GuvenliDosyaAdi(Path.GetFileName(fotoYolu), "bina-fotografi.bin");
            EkDosyasiEkle("Bina fotoğrafı", fotoYolu, $"attachments/building/{fotoAdi}",
                "", paket, indeks, uyarilar);
        }
        foreach (var urun in proje.YapiElemaniKutuphanesi.OrderBy(x => x.Id))
        foreach (var belge in urun.Belgeler.OrderBy(x => x.Id))
        {
            var ad = $"{belge.Id:N}-{GuvenliDosyaAdi(Path.GetFileName(belge.DosyaYolu), "belge.bin")}";
            EkDosyasiEkle("Ürün belgesi", belge.DosyaYolu, $"attachments/products/{urun.Id:N}/{ad}",
                belge.Sha256, paket, indeks, uyarilar);
        }

        foreach (var olcum in proje.SahaOlcumleri
                     .Where(x => x.Durum == SahaOlcumDurumu.Dogrulandi && x.RaporlamaIcinOnayli)
                     .OrderBy(x => x.Id))
        {
            var ad = $"{olcum.Id:N}-{GuvenliDosyaAdi(Path.GetFileName(olcum.KaynakDosya), "olcum.bin")}";
            EkDosyasiEkle("Ölçüm kaynak dosyası", olcum.KaynakDosya, $"attachments/measurements/{ad}",
                olcum.KaynakDosyaSha256, paket, indeks, uyarilar);
        }
        return indeks;
    }

    private static void EkDosyasiEkle(
        string tur,
        string kaynak,
        string paketYolu,
        string beklenenSha256,
        IDictionary<string, byte[]> paket,
        ICollection<E4R08PaketEkKaydi> indeks,
        ICollection<string> uyarilar)
    {
        var kayit = new E4R08PaketEkKaydi { Tur = tur, KaynakYol = kaynak ?? "", PaketYolu = paketYolu };
        indeks.Add(kayit);
        if (string.IsNullOrWhiteSpace(kaynak) || !File.Exists(kaynak))
        {
            kayit.Durum = "Kaynak dosya bulunamadı";
            uyarilar.Add($"{tur} pakete eklenemedi: {kaynak}");
            return;
        }
        var bilgi = new FileInfo(kaynak);
        if (bilgi.Length > MaxEntryBytes)
        {
            kayit.Durum = "Dosya boyut sınırını aşıyor";
            uyarilar.Add($"{tur} 64 MB sınırını aştığı için pakete eklenemedi: {kaynak}");
            return;
        }
        var bytes = File.ReadAllBytes(kaynak);
        var sha = Sha256(bytes);
        kayit.Sha256 = sha;
        if (!string.IsNullOrWhiteSpace(beklenenSha256) &&
            !StringComparer.OrdinalIgnoreCase.Equals(sha, beklenenSha256))
        {
            kayit.Durum = "Kayıtlı SHA-256 ile dosya uyuşmuyor";
            uyarilar.Add($"{tur} bütünlük doğrulamasını geçemedi: {kaynak}");
            return;
        }
        paket[paketYolu] = bytes;
        kayit.PaketeEklendi = true;
        kayit.Durum = "Pakete eklendi";
    }

    private static string GuvenliDosyaAdi(string? ad, string varsayilan)
    {
        if (string.IsNullOrWhiteSpace(ad)) return varsayilan;
        var sb = new StringBuilder(ad.Length);
        foreach (var c in ad)
            sb.Append(char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_');
        var sonuc = sb.ToString().Trim('.', '_');
        return string.IsNullOrWhiteSpace(sonuc) ? varsayilan : sonuc;
    }

    private static E4R08PaketDosyasi DosyaBilgisi(string path, byte[] bytes) =>
        new() { Yol = path, Boyut = bytes.LongLength, Sha256 = Sha256(bytes) };

    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static void Yaz(ZipArchive zip, string path, byte[] bytes)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        entry.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using var output = entry.Open();
        output.Write(bytes);
    }

    private static byte[]? Oku(ZipArchive zip, string path, bool required)
    {
        var entry = zip.GetEntry(path);
        if (entry == null)
        {
            if (required) throw new InvalidDataException($"Pakette {path} bulunamadı.");
            return null;
        }
        if (!GuvenliYol(entry.FullName) || entry.Length < 0 || entry.Length > MaxEntryBytes)
            throw new InvalidDataException($"Paket girdisi güvenli değil veya çok büyük: {path}");
        using var input = entry.Open();
        using var memory = new MemoryStream((int)entry.Length);
        var buffer = new byte[81920];
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (memory.Length + read > MaxEntryBytes)
                throw new InvalidDataException($"Paket girdisi çok büyük: {path}");
            memory.Write(buffer, 0, read);
        }
        return memory.ToArray();
    }

    private static bool GuvenliYol(string path) =>
        !string.IsNullOrWhiteSpace(path) &&
        !Path.IsPathRooted(path) &&
        !path.Contains("..", StringComparison.Ordinal) &&
        !path.Contains('\\') &&
        !path.StartsWith("/", StringComparison.Ordinal);

    private static string ImzaAlgoritmasi(X509Certificate2 certificate)
    {
        using var rsa = certificate.GetRSAPrivateKey();
        if (rsa != null) return "RSA-PKCS1-SHA256";
        using var ecdsa = certificate.GetECDsaPrivateKey();
        if (ecdsa != null) return "ECDSA-SHA256";
        throw new CryptographicException("Yalnız RSA veya ECDSA özel anahtarı desteklenir.");
    }

    private static byte[] Imzala(byte[] data, X509Certificate2 certificate, string algorithm)
    {
        if (algorithm == "RSA-PKCS1-SHA256")
        {
            using var rsa = certificate.GetRSAPrivateKey()!;
            return rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        using var ecdsa = certificate.GetECDsaPrivateKey()!;
        return ecdsa.SignData(data, HashAlgorithmName.SHA256);
    }

    private static bool ImzayiDogrula(byte[] data, byte[] signature, X509Certificate2 certificate, string algorithm)
    {
        if (algorithm == "RSA-PKCS1-SHA256")
        {
            using var rsa = certificate.GetRSAPublicKey();
            return rsa != null && rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        if (algorithm == "ECDSA-SHA256")
        {
            using var ecdsa = certificate.GetECDsaPublicKey();
            return ecdsa != null && ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256);
        }
        return false;
    }
}
