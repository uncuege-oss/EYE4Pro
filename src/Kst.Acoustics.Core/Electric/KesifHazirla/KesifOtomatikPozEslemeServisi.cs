using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Kst.Acoustics.Core.Electric.KesifHazirla;

public sealed record KesifOtomatikPozEslemeSonucu(
    KesifMetrajSatiri Kalem,
    KesifPoz Poz,
    double Puan);

/// <summary>
/// Metrajın teknik tanımı ile aktif poz kütüphanesindeki imalat adlarını karşılaştırır.
/// Birim, model/tip, kesit ve elektriksel sayısal değerler serbest metin benzerliğinden
/// daha yüksek ağırlıktadır. Son karar kullanıcı tarafından Poz Atama ekranında düzeltilebilir.
/// </summary>
public static partial class KesifOtomatikPozEslemeServisi
{
    private const double MinimumPuan = 20;

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "ve", "ile", "icin", "tip", "tipi", "adet", "standart", "olcu", "sistemi",
        "cihazi", "hatti", "panosu", "kablo", "kablosu", "sigorta", "mm2", "m2"
    };

    private static readonly HashSet<string> ModelTokens = new(StringComparer.Ordinal)
    {
        "nhxmh", "n2xh", "n2xsy", "na2xsy", "tms", "nh00", "kxa", "koa",
        "svc", "ats", "apd", "aksa", "eline", "standby"
    };

    public static IReadOnlyList<KesifOtomatikPozEslemeSonucu> Esle(
        IEnumerable<KesifMetrajSatiri> kalemler,
        IEnumerable<KesifPoz> pozlar)
    {
        var adaylar = pozlar
            .Where(p => p.AktifMi && p.BirimFiyat > 0 && !string.IsNullOrWhiteSpace(p.PozNo) && !string.IsNullOrWhiteSpace(p.BirimAdi))
            .Select(p => new HazirPoz(p))
            .ToList();

        var sonuclar = new List<KesifOtomatikPozEslemeSonucu>();
        foreach (var kalem in kalemler)
        {
            var hazirKalem = new HazirKalem(kalem);
            HazirPoz? enIyi = null;
            var enIyiPuan = double.MinValue;

            foreach (var aday in adaylar)
            {
                if (!string.Equals(hazirKalem.Birim, aday.Birim, StringComparison.Ordinal))
                    continue;
                if (!AlanUygunMu(hazirKalem, aday))
                    continue;

                var puan = Puanla(hazirKalem, aday);
                if (puan > enIyiPuan ||
                    (Math.Abs(puan - enIyiPuan) < 0.001 && enIyi is not null && aday.Ad.Length < enIyi.Ad.Length))
                {
                    enIyi = aday;
                    enIyiPuan = puan;
                }
            }

            if (enIyi is not null && enIyiPuan >= MinimumPuan)
                sonuclar.Add(new KesifOtomatikPozEslemeSonucu(kalem, enIyi.Poz, enIyiPuan));
        }

        return sonuclar;
    }

    private static double Puanla(HazirKalem kalem, HazirPoz aday)
    {
        var puan = 8d; // Birim eşleşmesi.

        var teknikAgirlik = 0d;
        var teknikEslesen = 0d;
        foreach (var token in kalem.TeknikTokenlar)
        {
            var agirlik = kalem.Spesifikasyonlar.Contains(token) ? 12d
                : kalem.Modeller.Contains(token) ? 14d
                : token.Length >= 5 ? 5d
                : 3d;
            teknikAgirlik += agirlik;
            if (aday.Tokenlar.Contains(token))
                teknikEslesen += agirlik;
        }

        if (teknikAgirlik > 0)
            puan += 45d * teknikEslesen / teknikAgirlik;

        if (kalem.Spesifikasyonlar.Count > 0)
        {
            var benzerlik = kalem.Spesifikasyonlar
                .Average(s => SpesifikasyonBenzerligi(s, aday.Spesifikasyonlar));
            puan += 35d * benzerlik;
            puan -= 28d * (1d - benzerlik);
        }

        if (kalem.Modeller.Count > 0)
        {
            var eslesen = kalem.Modeller.Count(m => ModelEslesiyor(m, aday));
            puan += 25d * eslesen / kalem.Modeller.Count;
            puan -= 18d * (kalem.Modeller.Count - eslesen) / kalem.Modeller.Count;
        }

        puan += GrupKapsamaPuani(kalem.AltGrupTokenlari, aday.Tokenlar, 12);
        puan += GrupKapsamaPuani(kalem.AnaGrupTokenlari, aday.Tokenlar, 5);

        if (kalem.AnaGrup == "pano")
            puan += 15;

        if (aday.Ad.Contains(kalem.Teknik, StringComparison.Ordinal))
            puan += 20;

        return puan;
    }

    private static bool AlanUygunMu(HazirKalem kalem, HazirPoz aday)
    {
        bool Adayda(params string[] tokenlar) => tokenlar.Any(aday.TumTokenlar.Contains);

        // Anma değerleri metin puanıyla telafi edilemez. Akım/kA/pano alanında mevcut
        // üst sınır/bant kuralları, diğer fiziksel büyüklüklerde sayısal eşitlik aranır.
        foreach (var spec in kalem.Spesifikasyonlar)
        {
            var match = TypedSpecRegex().Match(spec);
            if (!match.Success) continue;
            var uygun = match.Groups["unit"].Value is "a" or "ka" or "m2"
                ? SpesifikasyonBenzerligi(spec, aday.Spesifikasyonlar) > 0
                : aday.Spesifikasyonlar.Any(s => AyniAnmaDegeri(spec, s));
            if (!uygun) return false;
        }

        if (kalem.AnaGrup == "kablo")
        {
            if (!Adayda("kablo", "kablolar"))
                return false;

            var kabloModelleri = kalem.Modeller.Where(m => m is "nhxmh" or "n2xh" or "n2xsy" or "na2xsy").ToList();
            if (kabloModelleri.Count > 0 && !kabloModelleri.All(aday.Tokenlar.Contains))
                return false;
        }

        var boyutlar = kalem.Spesifikasyonlar.Where(s => s.Contains('x')).ToList();
        if (boyutlar.Count > 0 && !boyutlar.All(aday.Spesifikasyonlar.Contains))
            return false;

        if (kalem.TeknikTokenlar.Contains("trifaze") &&
            !(Adayda("trifaze") || (Adayda("3") && Adayda("fazli"))))
            return false;

        if (kalem.AnaGrup == "koruma cihazi")
        {
            if (kalem.AltGrup.Contains("kompakt salter", StringComparison.Ordinal) &&
                !(Adayda("kompakt") && Adayda("salter", "salterler")))
                return false;
            if (kalem.AltGrup.Contains("otomatik sigorta", StringComparison.Ordinal) &&
                !(Adayda("otomatik") && Adayda("sigorta", "sigortalar")))
                return false;
            if (kalem.AltGrup.Contains("bicakli sigorta", StringComparison.Ordinal) &&
                !(Adayda("bicakli", "nh") && Adayda("sigorta", "sigortalar")))
                return false;

            var akimlar = kalem.Spesifikasyonlar.Where(s => s.EndsWith('a') && !s.EndsWith("ka", StringComparison.Ordinal)).ToList();
            if (akimlar.Count > 0 && akimlar.All(s => SpesifikasyonBenzerligi(s, aday.Spesifikasyonlar) <= 0))
                return false;
        }

        if (kalem.AnaGrup == "busbar")
        {
            if (!Adayda("busbar"))
                return false;
            if (kalem.AltGrup.Contains("cikis kutusu", StringComparison.Ordinal) &&
                !Adayda("cikis", "kutusu", "modulu"))
                return false;
        }

        if (kalem.AnaGrup == "pano")
        {
            if (!Adayda("pano", "panolar", "tablo", "tablolar"))
                return false;
            if (kalem.AltGrup.Contains("dikili tip", StringComparison.Ordinal))
            {
                var derinlikCm = kalem.Spesifikasyonlar.FirstOrDefault(s => s.EndsWith("cm", StringComparison.Ordinal));
                if (derinlikCm is not null &&
                    decimal.TryParse(derinlikCm[..^2], NumberStyles.Number, CultureInfo.InvariantCulture, out var cm) &&
                    !aday.Ad.Contains($"derinlik en az {cm * 10:0} mm", StringComparison.Ordinal))
                    return false;
            }
        }

        if (kalem.AnaGrup == "kompanzasyon")
        {
            if (kalem.AltGrup == "kondansator" && !Adayda("kondansator", "kondansatorler"))
                return false;
            if (kalem.AltGrup == "reaktor" && !Adayda("reaktor", "reaktoru"))
                return false;
            if (kalem.AltGrup.Contains("rolesi", StringComparison.Ordinal) && !Adayda("role", "rolesi"))
                return false;
        }

        if (kalem.AnaGrup == "jenerator sistemi")
        {
            if (kalem.AltGrup == "jenerator" && !Adayda("jenerator", "jeneratorler"))
                return false;
            if (kalem.AltGrup.Contains("senkron", StringComparison.Ordinal) &&
                !(Adayda("senkron") && Adayda("pano", "panosu")))
                return false;
            if (kalem.AltGrup.Contains("transfer salteri", StringComparison.Ordinal) &&
                !(Adayda("transfer") && Adayda("salter", "salteri")))
                return false;
        }

        return true;
    }

    private static bool AyniAnmaDegeri(string kaynak, string aday)
    {
        var k = TypedSpecRegex().Match(kaynak);
        var a = TypedSpecRegex().Match(aday);
        return k.Success && a.Success && k.Groups["unit"].Value == a.Groups["unit"].Value &&
               decimal.TryParse(k.Groups["value"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var kv) &&
               decimal.TryParse(a.Groups["value"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var av) && kv == av;
    }

    private static bool ModelEslesiyor(string model, HazirPoz aday)
        => model switch
        {
            "tms" => aday.TumTokenlar.Contains("kompakt") &&
                     (aday.TumTokenlar.Contains("salter") || aday.TumTokenlar.Contains("salterler")),
            "eline" => aday.TumTokenlar.Contains("busbar"),
            _ => aday.Tokenlar.Contains(model)
        };

    private static double SpesifikasyonBenzerligi(string kaynak, HashSet<string> adaylar)
    {
        if (adaylar.Contains(kaynak) || adaylar.Any(a => AyniAnmaDegeri(kaynak, a)))
            return 1;

        var kaynakMatch = TypedSpecRegex().Match(kaynak);
        if (!kaynakMatch.Success ||
            !decimal.TryParse(kaynakMatch.Groups["value"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var kaynakDeger))
            return 0;

        var birim = kaynakMatch.Groups["unit"].Value;
        var enIyi = 0d;
        foreach (var aday in adaylar)
        {
            var adayMatch = TypedSpecRegex().Match(aday);
            if (!adayMatch.Success || adayMatch.Groups["unit"].Value != birim ||
                !decimal.TryParse(adayMatch.Groups["value"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var adayDeger))
                continue;

            if (adayDeger < kaynakDeger || adayDeger <= 0)
                continue;

            var oran = (double)(kaynakDeger / adayDeger);
            var benzerlik = birim switch
            {
                "a" => oran,
                "ka" => oran >= 0.70 ? oran * 0.75 : 0,
                "m2" => oran >= 0.50 ? oran * 0.90 : 0,
                _ => 0
            };
            enIyi = Math.Max(enIyi, benzerlik);
        }
        return enIyi;
    }

    private static double GrupKapsamaPuani(HashSet<string> kaynak, HashSet<string> hedef, double azami)
    {
        if (kaynak.Count == 0)
            return 0;
        return azami * kaynak.Count(hedef.Contains) / kaynak.Count;
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var decomposed = value.ToLower(new CultureInfo("tr-TR")).Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;

            var c = ch switch
            {
                'ı' => 'i',
                'ş' => 's',
                'ğ' => 'g',
                '²' => '2',
                ',' => '.',
                _ => ch
            };
            sb.Append(char.IsLetterOrDigit(c) || c is '.' or '+' ? c : ' ');
        }

        return SpaceRegex().Replace(sb.ToString(), " ").Trim();
    }

    private static HashSet<string> Tokenlar(string value)
        => value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 1 && !StopWords.Contains(t))
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> Spesifikasyonlar(string value)
    {
        var sonuc = DimensionRegex().Matches(value)
            .Select(m => m.Groups["dim"].Value)
            .Concat(SpecRegex().Matches(value)
            .Select(m => m.Value.Replace(" ", "", StringComparison.Ordinal))
            )
            .ToHashSet(StringComparer.Ordinal);

        if (sonuc.Count == 0)
        {
            foreach (var token in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (StandaloneNumberRegex().IsMatch(token))
                    sonuc.Add(token);
            }
        }
        return sonuc;
    }

    private sealed class HazirPoz
    {
        public HazirPoz(KesifPoz poz)
        {
            Poz = poz;
            Ad = Normalize(poz.BirimAdi);
            Birim = Normalize(poz.Birim);
            Tokenlar = KesifOtomatikPozEslemeServisi.Tokenlar(Ad);
            TumTokenlar = Ad.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
            Spesifikasyonlar = KesifOtomatikPozEslemeServisi.Spesifikasyonlar(Ad);
        }

        public KesifPoz Poz { get; }
        public string Ad { get; }
        public string Birim { get; }
        public HashSet<string> Tokenlar { get; }
        public HashSet<string> TumTokenlar { get; }
        public HashSet<string> Spesifikasyonlar { get; }
    }

    private sealed class HazirKalem
    {
        public HazirKalem(KesifMetrajSatiri kalem)
        {
            Kalem = kalem;
            Teknik = Normalize(kalem.TeknikOzellik);
            Birim = Normalize(kalem.Birim);
            TeknikTokenlar = KesifOtomatikPozEslemeServisi.Tokenlar(Teknik);
            Spesifikasyonlar = KesifOtomatikPozEslemeServisi.Spesifikasyonlar(Teknik);
            Modeller = TeknikTokenlar.Where(ModelTokens.Contains).ToHashSet(StringComparer.Ordinal);
            AltGrupTokenlari = KesifOtomatikPozEslemeServisi.Tokenlar(Normalize(kalem.AltGrup));
            AnaGrupTokenlari = KesifOtomatikPozEslemeServisi.Tokenlar(Normalize(kalem.AnaGrup));
            AltGrup = Normalize(kalem.AltGrup);
            AnaGrup = Normalize(kalem.AnaGrup);
        }

        public KesifMetrajSatiri Kalem { get; }
        public string Teknik { get; }
        public string Birim { get; }
        public HashSet<string> TeknikTokenlar { get; }
        public HashSet<string> Spesifikasyonlar { get; }
        public HashSet<string> Modeller { get; }
        public HashSet<string> AltGrupTokenlari { get; }
        public HashSet<string> AnaGrupTokenlari { get; }
        public string AltGrup { get; }
        public string AnaGrup { get; }
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpaceRegex();

    [GeneratedRegex(@"\b\d+(?:\.\d+)?\s*(?:ka|a|kvar|kva|kw|kv|hz|p|m2|cm|mm2)\b")]
    private static partial Regex SpecRegex();

    [GeneratedRegex(@"\b(?<dim>(?:\d+(?:\.\d+)?x)+\d+(?:\.\d+)?)(?:s|rm|re)?\b")]
    private static partial Regex DimensionRegex();

    [GeneratedRegex(@"^\d+(?:\.\d+)?$")]
    private static partial Regex StandaloneNumberRegex();

    [GeneratedRegex(@"^(?<value>\d+(?:\.\d+)?)(?<unit>ka|a|kvar|kva|kw|kv|hz|p|m2|cm|mm2)$")]
    private static partial Regex TypedSpecRegex();
}
