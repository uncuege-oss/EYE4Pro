using System.Globalization;
using System.Text.RegularExpressions;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Bina.Services;

/// <summary>Yerel plan metinlerinden ölçü okur. Eksik/çelişkili bilgiyi ölçülmüş değer gibi üretmez.</summary>
public static class MimariProjeMetinServisi
{
    public sealed record YukseklikSonucu(double? KatYuksekligiM, double? TabanKotuM,
        IReadOnlyList<string> Kanitlar, IReadOnlyList<string> Uyarilar);
    public sealed record MetinUygulamaSonucu(int Uygulanan, IReadOnlyList<string> Kanitlar,
        IReadOnlyList<string> Uyarilar);

    private const string Sayi = @"\d+(?:[.,]\d+)?";
    private static readonly Regex KatYuksekligi = new(
        @"\bKAT\s*(?:YUKSEKLIGI|YUKSEKLIG|YUKSEKLIK|YUK\.?|H)\s*[:=]?\s*(?<n>" + Sayi + @")\s*(?<u>MM|CM|M)?\b", RegexOptions.Compiled);
    private static readonly Regex NetYukseklik = new(
        @"^(?:(?:NET\s+)?(?:TAVAN\s+|ODA\s+)?YUKSEKLIGI\s*[:=]|H\s*=)\s*(?<n>" + Sayi + @")\s*(?<u>MM|CM|M)?$", RegexOptions.Compiled);
    private static readonly Regex Kot = new(
        @"^(?:(?:TABAN|DOSEME|KAT)\s+)?KOT(?:U)?\s*[:=]?\s*(?<n>[+\-±]?" + Sayi + @")\s*(?<u>MM|CM|M)?$|^(?<n>[+\-±]" + Sayi + @")\s*(?<u>M)?$", RegexOptions.Compiled);
    private static readonly Regex AciklikEtiketi = new(
        @"^(?<tip>KAPI|PENCERE|YK|K|P)\s*[:=]?\s*(?:(?<kod>\d{1,3}[A-Z]?)(?:\s*[-:]\s*|\s+))?(?<w>" + Sayi + @")\s*[/X×*]\s*(?<h>" + Sayi + @")\s*(?<u>MM|CM|M)?\b", RegexOptions.Compiled);
    private static readonly Regex YalnizKod = new(@"^(?<tip>K|P)\s*[-:]?\s*\d{1,3}[A-Z]?$", RegexOptions.Compiled);
    private static readonly Regex YalnizOlcu = new(@"^(?<w>" + Sayi + @")\s*[/X×*]\s*(?<h>" + Sayi + @")\s*(?<u>MM|CM|M)?$", RegexOptions.Compiled);
    private static readonly Regex Esik = new(@"\b(?:DENIZLIK|PARAPET|ESIK|H[DP])\s*[:=]?\s*(?<n>" + Sayi + @")\s*(?<u>MM|CM|M)?\b", RegexOptions.Compiled);

    /// <summary>Yalnız ilgili kata ait metinlerle çağırın. h= net oda yüksekliğidir, katlar arası mesafe değildir.</summary>
    public static YukseklikSonucu YukseklikOku(IEnumerable<(Point2D Pos, string Value)> metinler)
    {
        var heights = new List<(double Deger, string Kanit)>();
        var levels = new List<(double Deger, string Kanit)>();
        foreach (var (_, value) in metinler)
        {
            var text = Duzelt(value);
            foreach (Match match in KatYuksekligi.Matches(text))
                if (Metre(match.Groups["n"].Value, match.Groups["u"].Value) is { } h && h is >= 2 and <= 12)
                    heights.Add((h, value));
            var kot = Kot.Match(text);
            // Mutlak arazi kotları (+919.25 gibi) katın göreli taban kotu değildir.
            if (kot.Success && Metre(kot.Groups["n"].Value, kot.Groups["u"].Value, kot: true) is { } z && Math.Abs(z) <= 100)
                levels.Add((z, value));
        }
        var warnings = new List<string>();
        var evidence = new List<string>();
        double? Unique(List<(double Deger, string Kanit)> values, string kind)
        {
            var distinct = values.Select(x => Math.Round(x.Deger, 3)).Distinct().ToList();
            if (distinct.Count > 1) { warnings.Add($"{kind} için çelişkili değerler var; otomatik atanmadı."); return null; }
            if (distinct.Count == 0) return null;
            evidence.AddRange(values.Select(x => x.Kanit).Distinct());
            return distinct[0];
        }
        var height = Unique(heights, "Kat yüksekliği");
        double? level;
        var groups = levels.GroupBy(v => Math.Round(v.Deger, 3)).OrderByDescending(g => g.Count()).ToList();
        if (groups.Count > 1 && groups[0].Count() >= 2 && groups[0].Count() >= 2 * groups[1].Count()
            && groups[0].Count() >= .6 * levels.Count)
        {
            level = groups[0].Key;
            evidence.AddRange(groups[0].Select(v => v.Kanit).Distinct());
            warnings.Add($"Taban kotu tekrarlanan {level:0.###} m etiketlerinden seçildi; plandaki diğer kotlar kontrol edilmeli.");
        }
        else level = Unique(levels, "Taban kotu");
        return new(height, level, evidence, warnings);
    }

    /// <summary>Yalnız ardışık olduğu doğrulanmış iki katın kotları ile kullanılmalıdır.</summary>
    public static double? KotFarkindanYukseklik(double altKot, double ustKot)
    {
        double h = ustKot - altKot;
        return double.IsFinite(h) && h is >= 2 and <= 12 ? h : null;
    }

    public static MetinUygulamaSonucu OdaYukseklikleriniUygula(BinaKat kat)
    {
        int count = 0;
        var evidence = new List<string>();
        var warnings = new List<string>();
        foreach (var oda in kat.Odalar)
        {
            var values = kat.HamMetinler.Where(t => Icinde(t.Pos, oda.Poligon))
                .Where(t => !KatYuksekligi.IsMatch(Duzelt(t.Value)))
                .Select(t => (Text: t.Value, Match: NetYukseklik.Match(Duzelt(t.Value))))
                .Where(t => t.Match.Success)
                .Select(t => (t.Text, Height: Metre(t.Match.Groups["n"].Value, t.Match.Groups["u"].Value)))
                .Where(t => t.Height is >= 1.8 and <= 12).ToList();
            var heights = values.Select(t => Math.Round(t.Height!.Value, 3)).Distinct().ToList();
            if (heights.Count > 1) { warnings.Add($"{oda.Ad}: birden fazla net yükseklik var; atanmadı."); continue; }
            if (heights.Count == 0) continue;
            oda.YukseklikOverrideM = heights[0];
            evidence.Add($"{oda.Ad}: {values[0].Text}");
            count++;
        }
        return new(count, evidence, warnings);
    }

    public static MetinUygulamaSonucu AciklikBoyutlariniUygula(BinaKat kat)
    {
        var warnings = new List<string>();
        var evidence = new List<string>();
        var assignments = new Dictionary<BinaAciklik, List<(double W, double H, double? Sill, BinaAciklikTipi Tip, string Text)>>();
        var texts = kat.HamMetinler.Select(t => (t.Pos, Original: t.Value, Text: Duzelt(t.Value))).ToList();
        var physicalGroups = FizikselAciklikGruplari(kat.Acikliklar.Where(a => !a.IptalEdildi && !a.ManuelEklendi));
        foreach (var text in texts)
        {
            var match = AciklikEtiketi.Match(text.Text);
            var code = YalnizKod.Match(text.Text);
            var bare = YalnizOlcu.Match(text.Text);
            bool bareWindow = false;
            string source = text.Original;
            string type;
            if (match.Success) type = match.Groups["tip"].Value;
            else if (code.Success)
            {
                var sizes = texts.Where(t => Uzaklik(t.Pos, text.Pos) <= .65)
                    .Select(t => (t.Original, Match: YalnizOlcu.Match(t.Text))).Where(t => t.Match.Success).ToList();
                if (sizes.Count != 1) continue;
                match = sizes[0].Match;
                type = code.Groups["tip"].Value;
                source += " " + sizes[0].Original;
            }
            else if (bare.Success) { match = bare; type = "P"; bareWindow = true; }
            else continue;
            var width = Metre(match.Groups["w"].Value, match.Groups["u"].Value);
            var height = Metre(match.Groups["h"].Value, match.Groups["u"].Value);
            if (width is not (>= .25 and <= 8) || height is not (>= .25 and <= 6)) continue;
            var tip = type.StartsWith('P') ? BinaAciklikTipi.Pencere : BinaAciklikTipi.Kapi;
            var nearbyGroups = physicalGroups
                .Where(g => (!bareWindow || g.Any(a => a.Tip == BinaAciklikTipi.Pencere)) &&
                    g.Min(a => NoktaSegmentUzakligi(text.Pos, a.Bas, a.Bit)) <= 1.5).ToList();
            if (nearbyGroups.Count == 0) continue;
            // Önce yazılı genişlikle uyuşan geometri seçilir. Yakındaki daha dar bir
            // kapı kanadı veya başka bir açıklık doğru ölçülü adayı engellememelidir.
            var candidates = new List<OlcuEslesmesi>();
            foreach (var group in nearbyGroups)
            {
                var compatible = group.Where(a => GenislikUyusur(Uzaklik(a.Bas, a.Bit), width.Value)).ToList();
                if (compatible.Count > 0)
                    candidates.Add(new(group, compatible.Min(a => NoktaSegmentUzakligi(text.Pos, a.Bas, a.Bit))));
            }
            // Çift kanat: yalnız aynı doğru üzerinde birleşen iki kısa aralığın
            // birleşik genişliği etikete uyuyorsa. Noktalar/genişlikler değiştirilmez.
            if (tip == BinaAciklikTipi.Kapi && width.Value >= 1.15)
                for (int i = 0; i < nearbyGroups.Count; i++)
                    for (int j = i + 1; j < nearbyGroups.Count; j++)
                    {
                        double bestDistance = double.PositiveInfinity;
                        foreach (var a in nearbyGroups[i])
                            foreach (var b in nearbyGroups[j])
                                if (BirlesikKanatAraligi(a, b, width.Value) is { } pair)
                                    bestDistance = Math.Min(bestDistance, NoktaSegmentUzakligi(text.Pos, pair.Bas, pair.Bit));
                        if (double.IsFinite(bestDistance))
                            candidates.Add(new(nearbyGroups[i].Concat(nearbyGroups[j]).ToList(), bestDistance));
                    }
            candidates = candidates.OrderBy(c => c.Distance).ToList();
            if (candidates.Count == 0)
            {
                var nearest = nearbyGroups.SelectMany(g => g).MinBy(a => NoktaSegmentUzakligi(text.Pos, a.Bas, a.Bit))!;
                warnings.Add($"'{source}': çizilen genişlik {Uzaklik(nearest.Bas, nearest.Bit):0.##} m ile uyuşmuyor; kontrol edilmeli.");
                continue;
            }
            if (candidates.Count > 1 && candidates[1].Distance - candidates[0].Distance < .15)
            { warnings.Add($"'{source}': birden fazla yakın açıklık var; ölçü atanmadı."); continue; }
            var sillMatch = Esik.Match(text.Text);
            double? sill = sillMatch.Success ? Metre(sillMatch.Groups["n"].Value, sillMatch.Groups["u"].Value) : null;
            if (sill is < 0 or > 3) sill = null;
            foreach (var opening in candidates[0].Acikliklar)
            {
                if (!assignments.TryGetValue(opening, out var entries)) assignments[opening] = entries = new();
                entries.Add((width.Value, height.Value, sill, tip, source));
            }
        }
        int count = 0;
        foreach (var (opening, values) in assignments)
        {
            if (values.Select(v => (v.W, v.H, v.Sill, v.Tip)).Distinct().Count() != 1)
            { warnings.Add($"Açıklık {opening.Id}: çelişkili ölçü etiketleri var; atanmadı."); continue; }
            var v = values[0];
            opening.Tip = v.Tip;
            opening.GenislikM = Uzaklik(opening.Bas, opening.Bit);
            opening.YukseklikM = v.H;
            if (v.Sill is { } sill) { opening.EsikYuksekligiM = sill; opening.EsikYuksekligiBelirtildi = true; }
            if (opening.EtkinEsikYuksekligiM + v.H > kat.KatYuksekligiM + .05)
                warnings.Add($"'{v.Text}': üst kenarı kat yüksekliğini aşıyor; kat yüksekliği/denizlik kontrol edilmeli.");
            opening.Kaynak = (opening.Kaynak ?? "geometri").Split("; etiket:")[0] + "; etiket: " + v.Text;
            evidence.Add(v.Text);
            count++;
        }
        return new(count, evidence, warnings);
    }

    private sealed record OlcuEslesmesi(List<BinaAciklik> Acikliklar, double Distance);

    private static bool GenislikUyusur(double actual, double annotated)
        => actual > .05 && Math.Abs(actual - annotated) <= Math.Max(.20, annotated * .2);

    private static List<List<BinaAciklik>> FizikselAciklikGruplari(IEnumerable<BinaAciklik> openings)
    {
        var groups = new List<List<BinaAciklik>>();
        foreach (var opening in openings)
        {
            var group = groups.FirstOrDefault(g => AyniAciklikAraligi(g[0], opening));
            if (group is null) groups.Add([opening]);
            else group.Add(opening);
        }
        return groups;
    }

    private static bool AyniAciklikAraligi(BinaAciklik a, BinaAciklik b)
    {
        double length = Uzaklik(a.Bas, a.Bit), otherLength = Uzaklik(b.Bas, b.Bit);
        if (length < .05 || otherLength < .05) return false;
        double ux = (a.Bit.X - a.Bas.X) / length, uy = (a.Bit.Y - a.Bas.Y) / length;
        if (Math.Abs(ux * (b.Bit.Y - b.Bas.Y) - uy * (b.Bit.X - b.Bas.X)) / otherLength > .06) return false;
        double t0 = (b.Bas.X - a.Bas.X) * ux + (b.Bas.Y - a.Bas.Y) * uy;
        double t1 = (b.Bit.X - a.Bas.X) * ux + (b.Bit.Y - a.Bas.Y) * uy;
        double endpointTolerance = Math.Min(.15, length * .15);
        if (Math.Abs(Math.Min(t0, t1)) > endpointTolerance || Math.Abs(Math.Max(t0, t1) - length) > endpointTolerance) return false;
        double offset = Math.Max(Math.Abs((b.Bas.X - a.Bas.X) * uy - (b.Bas.Y - a.Bas.Y) * ux),
            Math.Abs((b.Bit.X - a.Bas.X) * uy - (b.Bit.Y - a.Bas.Y) * ux));
        double wallTolerance = (a.Kaynak?.Contains("yansima") == true || b.Kaynak?.Contains("yansima") == true) ? .8 : .35;
        return offset <= wallTolerance;
    }

    private static (Point2D Bas, Point2D Bit)? BirlesikKanatAraligi(BinaAciklik a, BinaAciklik b, double annotatedWidth)
    {
        if (a.Tip != BinaAciklikTipi.Kapi || b.Tip != BinaAciklikTipi.Kapi) return null;
        double length = Uzaklik(a.Bas, a.Bit), otherLength = Uzaklik(b.Bas, b.Bit);
        if (length < .3 || otherLength < .3 || length > annotatedWidth * .8 || otherLength > annotatedWidth * .8) return null;
        if (Math.Max(length, otherLength) / Math.Min(length, otherLength) > 2.5) return null;
        double ux = (a.Bit.X - a.Bas.X) / length, uy = (a.Bit.Y - a.Bas.Y) / length;
        if (Math.Abs(ux * (b.Bit.Y - b.Bas.Y) - uy * (b.Bit.X - b.Bas.X)) / otherLength > .04) return null;
        if (Math.Abs((b.Bas.X - a.Bas.X) * uy - (b.Bas.Y - a.Bas.Y) * ux) > .06 ||
            Math.Abs((b.Bit.X - a.Bas.X) * uy - (b.Bit.Y - a.Bas.Y) * ux) > .06) return null;
        double t0 = (b.Bas.X - a.Bas.X) * ux + (b.Bas.Y - a.Bas.Y) * uy;
        double t1 = (b.Bit.X - a.Bas.X) * ux + (b.Bit.Y - a.Bas.Y) * uy;
        double bMin = Math.Min(t0, t1), bMax = Math.Max(t0, t1);
        double gap = Math.Max(bMin - length, -bMax);
        if (gap is < -.04 or > .08) return null;
        double min = Math.Min(0, bMin), max = Math.Max(length, bMax);
        if (Math.Abs(max - min - annotatedWidth) > Math.Max(.12, annotatedWidth * .12)) return null;
        return (new(a.Bas.X + min * ux, a.Bas.Y + min * uy), new(a.Bas.X + max * ux, a.Bas.Y + max * uy));
    }

    private static string Duzelt(string text) => Regex.Replace(text.ToUpperInvariant().Replace('ı', 'I')
        .Replace('İ', 'I').Replace('Ş', 'S').Replace('Ğ', 'G').Replace('Ü', 'U').Replace('Ö', 'O').Replace('Ç', 'C')
        .Replace("\\P", " ").Replace('\n', ' ').Replace('\r', ' '), @"\s+", " ").Trim();

    private static double? Metre(string value, string unit, bool kot = false)
    {
        if (!double.TryParse(value.Replace('±', '+').Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double n) || !double.IsFinite(n)) return null;
        return unit switch { "MM" => n / 1000, "CM" => n / 100, "M" => n, _ => !kot && Math.Abs(n) >= 20 ? n / 100 : n };
    }
    private static double Uzaklik(Point2D a, Point2D b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    private static double NoktaSegmentUzakligi(Point2D p, Point2D a, Point2D b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
        if (l2 < 1e-12) return Uzaklik(p, a);
        double t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2, 0, 1);
        return Uzaklik(p, new(a.X + t * dx, a.Y + t * dy));
    }
    private static bool Icinde(Point2D p, IReadOnlyList<Point2D> polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var a = polygon[i]; var b = polygon[j];
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return inside;
    }
}
