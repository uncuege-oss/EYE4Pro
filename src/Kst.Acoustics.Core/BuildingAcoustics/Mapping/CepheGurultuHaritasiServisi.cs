using System.Globalization;
using System.Text;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.BuildingAcoustics.Reports;
using Kst.Acoustics.Core.BuildingAcoustics.Calculation;

namespace Kst.Acoustics.Core.BuildingAcoustics.Mapping;

public sealed class CepheGurultuHucresi
{
    public int KatNo { get; set; }
    public string KatAdi { get; set; } = "";
    public Guid MekanId { get; set; }
    public string MekanAdi { get; set; } = "";
    public Guid BolmeId { get; set; }
    public string BolmeAdi { get; set; } = "";
    public Guid SonucId { get; set; }
    public double BaslangicX { get; set; }
    public double BaslangicY { get; set; }
    public double BitisX { get; set; }
    public double BitisY { get; set; }
    public double UzunlukM { get; set; }
    public double AlanM2 { get; set; }
    public double LgagDb { get; set; }
    public double? DnTATrDb { get; set; }
    public double? IcGurultuDb { get; set; }
    public double? IcHedefDb { get; set; }
    public double? MarjDb { get; set; }
    public string Durum { get; set; } = "";
    public string Renk { get; set; } = "#7F8C8D";
}

public sealed class CepheGurultuHaritasi
{
    public Guid ProjeId { get; set; }
    public string ProjeAdi { get; set; } = "";
    public DateTimeOffset OlusturulmaUtc { get; set; }
    public List<CepheGurultuHucresi> Hucreler { get; set; } = new();
}

/// <summary>
/// Dış cephe duvarlarını gerçek plan koordinatlarında renklendirir. Harita, kaynak
/// yayılım simülasyonu değil M08'deki oda-birleşik cephe sonuçlarının plan izdüşümüdür.
/// </summary>
public static class CepheGurultuHaritasiServisi
{
    public static CepheGurultuHaritasi Olustur(
        BinaProje proje,
        ISet<Guid>? haricTutulanSonucIdleri = null,
        DateTimeOffset? zamanUtc = null)
    {
        ArgumentNullException.ThrowIfNull(proje);
        var hesap = new BinaAkustikDegerlendirmeServisi().Hesapla(proje, haricTutulanSonucIdleri);
        proje = hesap.HesaplananProje;
        var rows = hesap.Satirlar
            .Where(x => x.SonucTuru == AkustikRaporSonucTuru.DisCephe)
            .ToDictionary(x => x.SonucId);
        var map = new CepheGurultuHaritasi
        {
            ProjeId = proje.Id,
            ProjeAdi = proje.ProjeAdi,
            OlusturulmaUtc = (zamanUtc ?? DateTimeOffset.UtcNow).ToUniversalTime(),
        };

        foreach (var kat in proje.Katlar.OrderBy(x => x.KatNo))
        foreach (var mekan in kat.Mekanlar.OrderBy(x => x.Id))
        {
            var resultId = RaporSonucIdleri.DisCephe(mekan.Id);
            if (!rows.TryGetValue(resultId, out var row)) continue;
            foreach (var wall in mekan.Bolmeler.Where(x => x.IsDisCephe).OrderBy(x => x.Id))
            {
                var x1 = wall.Baslangic.X + kat.OriginOffset.X;
                var y1 = wall.Baslangic.Y + kat.OriginOffset.Y;
                var x2 = wall.Bitis.X + kat.OriginOffset.X;
                var y2 = wall.Bitis.Y + kat.OriginOffset.Y;
                var length = Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
                if (length <= 0 && wall.Alan > 0 && mekan.Yukseklik > 0)
                    length = wall.Alan / mekan.Yukseklik;
                map.Hucreler.Add(new()
                {
                    KatNo = kat.KatNo,
                    KatAdi = kat.Ad,
                    MekanId = mekan.Id,
                    MekanAdi = mekan.Ad,
                    BolmeId = wall.Id,
                    BolmeAdi = wall.Ad,
                    SonucId = resultId,
                    BaslangicX = x1,
                    BaslangicY = y1,
                    BitisX = x2,
                    BitisY = y2,
                    UzunlukM = length,
                    AlanM2 = wall.Alan,
                    LgagDb = row.DisGurultuDb ?? proje.DisGurultuDb,
                    DnTATrDb = row.Deger > 0 ? row.Deger : null,
                    IcGurultuDb = row.IcGurultuDb,
                    IcHedefDb = row.IcHedefDb,
                    MarjDb = double.IsFinite(row.MarjDb) ? row.MarjDb : null,
                    Durum = row.Durum,
                    Renk = Renk(row),
                });
            }
        }
        return map;
    }

    public static string CsvOlustur(CepheGurultuHaritasi map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var sb = new StringBuilder();
        sb.AppendLine("Kat No;Kat;Mekan;Bölme;Bölme Id;Sonuç Id;Başlangıç X (m);Başlangıç Y (m);Bitiş X (m);Bitiş Y (m);Uzunluk (m);Alan (m²);Lgag (dBA);DnT,A,tr (dB);İç gürültü (dBA);İç hedef (dBA);Marj (dB);Durum");
        foreach (var x in map.Hucreler.OrderBy(x => x.KatNo).ThenBy(x => x.MekanAdi).ThenBy(x => x.BolmeId))
        {
            sb.Append(x.KatNo).Append(';').Append(Csv(x.KatAdi)).Append(';').Append(Csv(x.MekanAdi)).Append(';')
                .Append(Csv(x.BolmeAdi)).Append(';').Append(x.BolmeId).Append(';').Append(x.SonucId).Append(';')
                .Append(N(x.BaslangicX)).Append(';').Append(N(x.BaslangicY)).Append(';')
                .Append(N(x.BitisX)).Append(';').Append(N(x.BitisY)).Append(';')
                .Append(N(x.UzunlukM)).Append(';').Append(N(x.AlanM2)).Append(';').Append(N(x.LgagDb)).Append(';')
                .Append(N(x.DnTATrDb)).Append(';').Append(N(x.IcGurultuDb)).Append(';')
                .Append(N(x.IcHedefDb)).Append(';').Append(N(x.MarjDb)).Append(';').Append(Csv(x.Durum)).AppendLine();
        }
        return sb.ToString();
    }

    public static string SvgOlustur(CepheGurultuHaritasi map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var groups = map.Hucreler.GroupBy(x => new { x.KatNo, x.KatAdi }).OrderBy(x => x.Key.KatNo).ToList();
        var width = 1000;
        var panelHeight = 350;
        var height = 150 + Math.Max(1, groups.Count) * panelHeight;
        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\">");
        sb.Append("<style>text{font-family:Arial,sans-serif;fill:#17212b}.title{font-size:22px;font-weight:700}.sub{font-size:12px;fill:#536475}.floor{font-size:16px;font-weight:700}.label{font-size:10px;paint-order:stroke;stroke:#fff;stroke-width:3px;stroke-linejoin:round}.wall{stroke-linecap:round}</style>");
        sb.Append("<rect width=\"100%\" height=\"100%\" fill=\"#f6f8fa\"/>");
        sb.Append($"<text class=\"title\" x=\"40\" y=\"38\">{Xml(map.ProjeAdi)} — Cephe Gürültü Sonuç Haritası</text>");
        sb.Append("<text class=\"sub\" x=\"40\" y=\"62\">Plan izdüşümüdür; renkler oda-birleşik M08 cephe değerlendirmesini gösterir, gürültü yayılım simülasyonu değildir.</text>");
        Legend(sb);
        if (groups.Count == 0)
            sb.Append("<text x=\"40\" y=\"145\" class=\"floor\">Haritalanabilir dış cephe sonucu yok.</text>");

        var panel = 0;
        foreach (var group in groups)
        {
            var top = 110 + panel++ * panelHeight;
            var points = group.SelectMany(x => new[] { (x.BaslangicX, x.BaslangicY), (x.BitisX, x.BitisY) }).ToList();
            var minX = points.Min(x => x.Item1); var maxX = points.Max(x => x.Item1);
            var minY = points.Min(x => x.Item2); var maxY = points.Max(x => x.Item2);
            var spanX = Math.Max(1, maxX - minX); var spanY = Math.Max(1, maxY - minY);
            var scale = Math.Min(880 / spanX, 260 / spanY);
            double X(double x) => 60 + (x - minX) * scale;
            double Y(double y) => top + 290 - (y - minY) * scale;
            sb.Append($"<rect x=\"30\" y=\"{top}\" width=\"940\" height=\"320\" rx=\"8\" fill=\"#fff\" stroke=\"#d8e0e8\"/>");
            sb.Append($"<text class=\"floor\" x=\"48\" y=\"{top + 26}\">Kat {group.Key.KatNo} — {Xml(group.Key.KatAdi)}</text>");
            foreach (var cell in group)
            {
                var x1 = X(cell.BaslangicX); var y1 = Y(cell.BaslangicY);
                var x2 = X(cell.BitisX); var y2 = Y(cell.BitisY);
                sb.Append($"<line class=\"wall\" x1=\"{N(x1)}\" y1=\"{N(y1)}\" x2=\"{N(x2)}\" y2=\"{N(y2)}\" stroke=\"{cell.Renk}\" stroke-width=\"10\">");
                sb.Append($"<title>{Xml(cell.MekanAdi)} / {Xml(cell.BolmeAdi)} — DnT,A,tr {N(cell.DnTATrDb)} dB; marj {N(cell.MarjDb)} dB; {Xml(cell.Durum)}</title></line>");
                sb.Append($"<text class=\"label\" x=\"{N((x1 + x2) / 2 + 5)}\" y=\"{N((y1 + y2) / 2 - 5)}\">{Xml(cell.MekanAdi)} {N(cell.MarjDb)} dB</text>");
            }
        }
        return sb.Append("</svg>").ToString();
    }

    private static string Renk(RaporSatiri row)
    {
        if (string.Equals(row.Durum, "VeriEksik", StringComparison.OrdinalIgnoreCase)) return "#D6A800";
        if (!string.Equals(row.Durum, "Gecti", StringComparison.OrdinalIgnoreCase)) return "#C0392B";
        if (!double.IsFinite(row.MarjDb) || row.MarjDb < 1) return "#E67E22";
        if (row.MarjDb < 3) return "#F1C40F";
        return "#239B56";
    }

    private static void Legend(StringBuilder sb)
    {
        var items = new[] { ("#239B56", "Geçti ≥3 dB"), ("#F1C40F", "Geçti <3 dB"), ("#E67E22", "Sınırda"), ("#C0392B", "Kaldı"), ("#D6A800", "Veri eksik") };
        var x = 40;
        foreach (var (color, label) in items)
        {
            sb.Append($"<rect x=\"{x}\" y=\"78\" width=\"14\" height=\"14\" rx=\"2\" fill=\"{color}\"/><text class=\"sub\" x=\"{x + 20}\" y=\"90\">{label}</text>");
            x += 150;
        }
    }

    private static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string N(double? value) => value is { } n && double.IsFinite(n) ? N(n) : "—";
    private static string Csv(string? value)
    {
        value ??= "";
        return value.IndexOfAny([';', '"', '\r', '\n']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
    private static string Xml(string? value) => (value ?? "")
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
        .Replace("\"", "&quot;").Replace("'", "&apos;");
}
