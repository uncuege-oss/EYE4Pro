using System.Text.Json;
using System.Text.Json.Serialization;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.BuildingAcoustics.Parsers;
using Kst.Acoustics.Core.BuildingAcoustics.Reports;
using Kst.Acoustics.Core.BuildingAcoustics.Calculation;

namespace Kst.Acoustics.Core.BuildingAcoustics.Portfolio;

public enum M08PortfoyDosyaDurumu { Gecerli, Hasarli, Desteklenmeyen, Erisilemedi }

public sealed class M08PortfoyOzeti
{
    public string DosyaYolu { get; set; } = "";
    public M08PortfoyDosyaDurumu DosyaDurumu { get; set; }
    public string Hata { get; set; } = "";
    public long DosyaBoyutu { get; set; }
    public DateTimeOffset SonYazmaUtc { get; set; }
    public Guid? ProjeId { get; set; }
    public string ProjeAdi { get; set; } = "";
    public string Adres { get; set; } = "";
    public string MusteriAdi { get; set; } = "";
    public string AkustikPerformansSinifi { get; set; } = "";
    public int SchemaVersion { get; set; }
    public int KatSayisi { get; set; }
    public int MekanSayisi { get; set; }
    public int SonucSayisi { get; set; }
    public int GectiSayisi { get; set; }
    public int KaldiSayisi { get; set; }
    public int VeriEksikSayisi { get; set; }
    public int AcikGorevSayisi { get; set; }
    public int OnaySayisi { get; set; }
    public int SahaOlcumuSayisi { get; set; }
    public bool YenidenHesapOnerilir { get; set; }
    public string HesapOzetiKaynagi { get; set; } = "";
}

public sealed class M08PortfoyRaporu
{
    public string Klasor { get; set; } = "";
    public DateTimeOffset TaranmaUtc { get; set; }
    public List<M08PortfoyOzeti> Projeler { get; set; } = new();
    public int GecerliDosyaSayisi => Projeler.Count(x => x.DosyaDurumu == M08PortfoyDosyaDurumu.Gecerli);
    public int SorunluDosyaSayisi => Projeler.Count - GecerliDosyaSayisi;
    public int ToplamMekanSayisi => Projeler.Sum(x => x.MekanSayisi);
    public int ToplamUygunsuzlukSayisi => Projeler.Sum(x => x.KaldiSayisi + x.VeriEksikSayisi);
}

public static class M08PortfoyServisi
{
    private const long MaxProjectBytes = 256L * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReferenceHandler = ReferenceHandler.Preserve,
        PropertyNameCaseInsensitive = true,
        MaxDepth = 128,
    };

    public static M08PortfoyRaporu Tara(
        string klasor,
        bool altKlasorler = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(klasor);
        var root = Path.GetFullPath(klasor);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        var report = new M08PortfoyRaporu { Klasor = root, TaranmaUtc = DateTimeOffset.UtcNow };
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = altKlasorler,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            MatchCasing = MatchCasing.CaseInsensitive,
        };

        foreach (var path in Directory.EnumerateFiles(root, "*", options)
                     .Where(x => string.Equals(Path.GetExtension(x), ".e4p08", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            report.Projeler.Add(Oku(path));
        }
        return report;
    }

    private static M08PortfoyOzeti Oku(string path)
    {
        var result = new M08PortfoyOzeti { DosyaYolu = path };
        try
        {
            var info = new FileInfo(path);
            result.DosyaBoyutu = info.Length;
            result.SonYazmaUtc = info.LastWriteTimeUtc;
            if (info.Length > MaxProjectBytes)
                throw new InvalidDataException("Proje dosyası portföy tarama sınırını aşıyor (256 MB).");
            var json = File.ReadAllText(path);
            if (!BinaProjeMigrator.DosyaKimliginiDogrula(json, path, out var identityError))
            {
                result.DosyaDurumu = identityError.Contains("daha yeni", StringComparison.OrdinalIgnoreCase)
                    ? M08PortfoyDosyaDurumu.Desteklenmeyen
                    : M08PortfoyDosyaDurumu.Hasarli;
                result.Hata = identityError;
                return result;
            }
            var project = JsonSerializer.Deserialize<BinaProje>(json, JsonOptions)
                ?? throw new InvalidDataException("Proje JSON içeriği okunamadı.");
            var sourceSchemaVersion = project.SchemaVersion;
            BinaProjeMigrator.Normalize(project);
            var rows = new BinaAkustikDegerlendirmeServisi().Hesapla(project).Satirlar;
            result.DosyaDurumu = M08PortfoyDosyaDurumu.Gecerli;
            result.ProjeId = project.Id;
            result.ProjeAdi = project.ProjeAdi;
            result.Adres = project.Adres;
            result.MusteriAdi = project.MusteriAdi;
            result.AkustikPerformansSinifi = project.AkustikPerformansSinifi.ToString();
            result.SchemaVersion = sourceSchemaVersion;
            result.KatSayisi = project.Katlar.Count;
            result.MekanSayisi = project.Katlar.Sum(x => x.Mekanlar.Count);
            result.SonucSayisi = rows.Count;
            result.GectiSayisi = rows.Count(x => string.Equals(x.Durum, "Gecti", StringComparison.OrdinalIgnoreCase));
            result.KaldiSayisi = rows.Count(x => string.Equals(x.Durum, "Kaldi", StringComparison.OrdinalIgnoreCase));
            result.VeriEksikSayisi = rows.Count(x => string.Equals(x.Durum, "VeriEksik", StringComparison.OrdinalIgnoreCase));
            result.AcikGorevSayisi = project.GorevDurumlari?.Count(x => x.Durum is not (GorevDurumu.Tamamlandi or GorevDurumu.Muaf)) ?? 0;
            result.OnaySayisi = project.OnayKayitlari?.Count(x => x.Durum == ProjeOnayDurumu.Onaylandi) ?? 0;
            result.SahaOlcumuSayisi = project.SahaOlcumleri?.Count ?? 0;
            result.YenidenHesapOnerilir = rows.Count == 0 || result.VeriEksikSayisi > 0;
            result.HesapOzetiKaynagi = "Dosya, M08 kanonik rapor servisiyle salt okunur olarak yeniden değerlendirildi.";
        }
        catch (UnauthorizedAccessException ex)
        {
            result.DosyaDurumu = M08PortfoyDosyaDurumu.Erisilemedi;
            result.Hata = ex.Message;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            result.DosyaDurumu = M08PortfoyDosyaDurumu.Hasarli;
            result.Hata = ex.Message;
        }
        return result;
    }
}
