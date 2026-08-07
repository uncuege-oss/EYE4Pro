namespace Kst.Acoustics.Core.Electric.ZayifAkimTekHat;

public enum ElvKutuphaneKalemTuru
{
    Cihaz,
    Kablo
}

public sealed class ElvOzelKutuphaneKalemi
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string AltBaslikKodu { get; set; } = "Data";
    public ElvKutuphaneKalemTuru Tur { get; set; }
    public string Ad { get; set; } = "";
}

public sealed record ElvKutuphaneAltBasligi(string Kod, string Ad);

/// <summary>M13 alt başlıklarının yerleşik salt-okunur ve kullanıcı tanımlı kataloglarını birleştirir.</summary>
public static class ElvKutuphaneServisi
{
    public static IReadOnlyList<ElvKutuphaneAltBasligi> AltBasliklar { get; } =
    [
        new("Data", "Data"),
        new("Kamera", "Kamera / CCTV"),
        new("KameraIzleme", "CCTV İzleme Noktası"),
        new("AnonsAnalog", "Acil Anons — Analog"),
        new("AnonsDijital", "Acil Anons — Dijital (PoE)"),
        new("TvDirekt", "TV — Direkt LNB"),
        new("TvMerkezi", "TV — Merkezi Koaksiyel"),
        new("TvIp", "TV — IP-TV"),
        new("Yangin", "Yangın Algılama"),
        new("Telefon", "Telefon"),
        new("Diafon", "Diafon")
    ];

    public static IReadOnlyList<string> YerlesikCihazlar(string kod) => kod switch
    {
        "Data" => ElvCihazKatalogu.DataCihazlari,
        "Kamera" => ElvCihazKatalogu.KameraCihazlari,
        "KameraIzleme" => ElvCihazKatalogu.CctvIzlemeNoktasiCihazlari,
        "AnonsAnalog" => ElvCihazKatalogu.AnonsAnalogCihazlari,
        "AnonsDijital" => ElvCihazKatalogu.AnonsDijitalAnaMerkezCihazlari
            .Concat(ElvCihazKatalogu.AnonsDijitalKatRackCihazlari).Distinct().ToList(),
        "TvDirekt" => ElvCihazKatalogu.TvDirektLnbCihazlari,
        "TvMerkezi" => ElvCihazKatalogu.TvMerkeziCihazlari,
        "TvIp" => ElvCihazKatalogu.IpTvCihazlari,
        "Yangin" => ElvCihazKatalogu.YanginCihazlari,
        "Telefon" => ElvCihazKatalogu.TelefonCihazlari,
        "Diafon" => ElvCihazKatalogu.DiafonCihazlari,
        _ => []
    };

    public static IReadOnlyList<string> YerlesikKablolar(string kod) => kod switch
    {
        "Data" => ElvCihazKatalogu.DataKablolari,
        "Kamera" or "KameraIzleme" => ElvCihazKatalogu.KameraKablolari,
        "AnonsAnalog" or "AnonsDijital" => ElvCihazKatalogu.AnonsKablolari,
        "TvDirekt" or "TvMerkezi" => ElvCihazKatalogu.TvKablolari,
        "TvIp" => ElvCihazKatalogu.DataKablolari,
        "Yangin" => ElvCihazKatalogu.YanginKablolari,
        "Telefon" => ElvCihazKatalogu.TelefonKablolari,
        "Diafon" => ElvCihazKatalogu.DiafonKablolari,
        _ => []
    };

    public static IReadOnlyList<string> Birlestir(
        ZayifAkimTekHatProje proje,
        string kod,
        ElvKutuphaneKalemTuru tur,
        IEnumerable<string> yerlesik)
        => yerlesik.Concat(proje.OzelKutuphaneKalemleri
                .Where(k => k.AltBaslikKodu == kod && k.Tur == tur)
                .Select(k => k.Ad))
            .Where(ad => !string.IsNullOrWhiteSpace(ad))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(ad => ad)
            .ToList();
}
