using System.Globalization;
using System.Text.RegularExpressions;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Bina.Services;

/// <summary>
/// Plan kotlarını kesit/görünüşte tekrarlanan ve düşey mesafesi kot farkıyla
/// uyuşan referanslarla karşılaştırır. Kat ana referansı, yerel döşeme kotu ve
/// net oda yüksekliği farklı bilgilerdir; birinden diğerini tahmin etmez.
/// </summary>
public static class MimariKatYukseklikServisi
{
    public sealed record YerelKot(string OdaId, double TabanKotuM, IReadOnlyList<string> Kanitlar)
    {
        /// Bare +kot merdiven/zemin seviyesi de olabilir; yalnız açık taban/döşeme adı.
        public bool DosemeKotuAcik { get; init; }
    }
    public sealed record YerelKotSonucu(IReadOnlyList<YerelKot> Kotlar, IReadOnlyList<string> Uyarilar);

    private sealed record KotEtiketi(Point2D Pos, double Kot, string Metin);
    private sealed record KotZinciri(string Baslik, double ReferansY, IReadOnlyList<KotEtiketi> Etiketler,
        IReadOnlyList<double> TekrarlananKotlar);
    private static readonly Regex Kot = new(
        @"^(?:(?:TABAN|DOSEME|KAT)\s+)?KOT(?:U)?\s*[:=]?\s*(?<z>[+\-±]?\d+(?:[.,]\d+)?)\s*(?<u>MM|CM|M)?$|^(?<z>[+\-±]\s*\d+(?:[.,]\d+)?)\s*(?<u>MM|CM|M)?(?:\s*\(\s*[+\-±]\s*\d+(?:[.,]\d+)?\s*\))?$",
        RegexOptions.Compiled);
    private const double ReferansToleransiM = .12;

    /// <summary>Blok eşleştirmesi kurulmadan katlar arasında ölçü aktarılmamalıdır.</summary>
    public static bool BlokEslesmesiGerekli(IReadOnlyList<DxfKatYukleyici.CokluKatAdayi> adaylar)
        => adaylar.Any(a => a.Kume.Metinler.Any(t => KatEtiketServisi.CizimBasligiMi(t.Value) &&
            Regex.IsMatch(Duzelt(t.Value), @"\b[A-Z0-9]+\s+BLOK\b")));

    /// <summary>
    /// Yeni aday kopyaları döndürür. Kaynak kümelere/metinlere dokunmaz. Kesitler
    /// yoksa, ölçek uyuşmuyorsa veya kotlar çelişiyorsa ölçü üretmez.
    /// </summary>
    public static IReadOnlyList<DxfKatYukleyici.CokluKatAdayi> Cozumle(
        IReadOnlyList<DxfKatYukleyici.CokluKatAdayi> adaylar)
    {
        // Blok eşleştirmesi kurulmadan farklı binanın kesitini kullanma. Sadece
        // çizim başlıkları taranır; malzeme notundaki "beton blok" buna dahil değildir.
        if (BlokEslesmesiGerekli(adaylar))
            return adaylar.Select(a => !a.OneriliDahil ? a : a with {
                Olculer = (a.Olculer ?? MimariProjeMetinServisi.YukseklikOku(a.Kume.Metinler)) with {
                    Uyarilar = (a.Olculer?.Uyarilar ?? []).Concat(new[] { "Blok başlıkları var; kesit-plan blok eşleşmesi doğrulanmadan ortak kat kotu aktarılmadı." }).ToList()
                }
            }).ToList();
        var zincirler = adaylar.Where(a => !a.OneriliDahil).SelectMany(a => ZincirleriOku(a.Kume)).ToList();
        if (zincirler.Count == 0) return adaylar.ToList();
        var sonuc = adaylar.ToArray();
        var indisler = Enumerable.Range(0, adaylar.Count).Where(i => adaylar[i].OneriliDahil && adaylar[i].EtiketBulundu)
            .OrderBy(i => adaylar[i].SiraDegeri).ToList();
        var dogrulanan = new Dictionary<int, double>();
        foreach (int i in indisler)
        {
            var aday = adaylar[i];
            // Aynı sıra numarasındaki farklı blok/planları tek düşey bina sayma.
            if (indisler.Count(j => adaylar[j].SiraDegeri == aday.SiraDegeri) != 1) continue;
            var olcu = aday.Olculer ?? MimariProjeMetinServisi.YukseklikOku(aday.Kume.Metinler);
            var planKotlari = KotlariOku(aday.Kume.Metinler);
            var ortak = planKotlari.Select(k => k.Kot).Distinct()
                .Where(z => zincirler.Any(c => c.TekrarlananKotlar.Contains(z))).Order().ToList();
            if (ortak.Count == 0) continue;
            var kanit = olcu.Kanitlar.ToList();
            var uyari = olcu.Uyarilar.ToList();
            if (ortak[^1] - ortak[0] >= 2)
            {
                uyari.Add("Kesit/görünüşle uyuşan birden fazla kat aralığı var; ana taban kotu otomatik seçilmedi.");
                sonuc[i] = aday with { Olculer = olcu with { Uyarilar = uyari } };
                continue;
            }
            double anaKot = ortak[0];
            // Kapalı bir metin katmanı alt ana kotu kesitten kaldırabilir. Açık
            // planda tekrarlanan alt kot varken, görünen üst basamağı bütün katın
            // tabanı yapma (ör. +0 etiketleri gizli, +1 giriş kotları görünür).
            if (planKotlari.Where(k => k.Kot < anaKot - .02).GroupBy(k => k.Kot)
                .Any(g => g.Select(k => k.Pos).Distinct().Count() >= 2))
            {
                uyari.Add("Planda tekrarlanan daha alt kotlar kesit/görünüşte doğrulanamadı; üst yerel kot ana taban yapılmadı. Kot yazısı katmanlarını kontrol edin.");
                sonuc[i] = aday with { Olculer = olcu with { Uyarilar = uyari } };
                continue;
            }
            if (olcu.TabanKotuM is { } onceki && Math.Abs(onceki - anaKot) > .02)
            {
                uyari.Add($"Plandaki {Sayi(onceki)} m taban kotu ile kesit/görünüş ana referansı {Sayi(anaKot)} m uyuşmuyor; değiştirilmedi.");
                sonuc[i] = aday with { Olculer = olcu with { Uyarilar = uyari } };
                continue;
            }
            dogrulanan[i] = anaKot;
            var destek = zincirler.Where(c => c.TekrarlananKotlar.Contains(anaKot)).ToList();
            kanit.Add($"Ana taban referansı {Sayi(anaKot)} m: plan etiketi ve {destek.Count} kesit/görünüş kot zinciri uyuşuyor.");
            kanit.AddRange(destek.Take(3).Select(c => Kanit(c, anaKot)));
            if (ortak.Count > 1)
                uyari.Add($"Kademeli döşeme kotları ({string.Join(", ", ortak.Select(Sayi))} m) var; {Sayi(anaKot)} m yalnız katın alt ana referansıdır. Yerel oda kotları ayrı değerlendirilmelidir.");
            // Eski çelişki mesajı çözümden sonra yanıltıcı olur; yerel kot farkları
            // aşağıdaki ayrı uyarıda korunur, diğer ölçü uyarıları silinmez.
            uyari.RemoveAll(x => x == "Taban kotu için çelişkili değerler var; otomatik atanmadı.");
            if (planKotlari.Any(k => Math.Abs(k.Kot - anaKot) > .02) && ortak.Count == 1)
                uyari.Add("Planda ana referanstan farklı yerel kotlar var; bunlar oda/merdiven/zemin seviyeleri olarak ayrıca kontrol edilmelidir.");
            sonuc[i] = aday with { Olculer = olcu with { TabanKotuM = anaKot, Kanitlar = kanit.Distinct().ToList(), Uyarilar = uyari.Distinct().ToList() } };
        }
        for (int p = 0; p + 1 < indisler.Count; p++)
        {
            int altI = indisler[p], ustI = indisler[p + 1];
            if (!dogrulanan.TryGetValue(altI, out double alt) || !dogrulanan.TryGetValue(ustI, out double ust)) continue;
            var altAday = sonuc[altI]; var ustAday = sonuc[ustI];
            bool ardisik = Math.Abs(ustAday.SiraDegeri - altAday.SiraDegeri - 1) < .01;
            bool cati = ustAday.SiraDegeri == 9999 && altAday.SiraDegeri < 9999;
            if (!ardisik && !cati) continue;
            // Bir görünüşte ara kat etiketi eksik olabilir. Başka bir kesitte
            // doğrulanmış ara kot varsa seyrek zinciri seçip bu katı atlama.
            if (zincirler.Any(c => c.TekrarlananKotlar.Any(z => z - alt >= 2 && ust - z >= 2))) continue;
            var ortakZincir = zincirler.Where(c => c.TekrarlananKotlar.Contains(alt) && c.TekrarlananKotlar.Contains(ust)).ToList();
            if (ortakZincir.Count == 0 || MimariProjeMetinServisi.KotFarkindanYukseklik(alt, ust) is not { } h) continue;
            var olcu = altAday.Olculer!;
            var uyari = olcu.Uyarilar.ToList();
            if (olcu.KatYuksekligiM is { } onceki && Math.Abs(onceki - h) > .02)
            {
                uyari.Add($"Yazılı kat yüksekliği {Sayi(onceki)} m ile kesit kot farkı {Sayi(h)} m uyuşmuyor; yazılı ölçü değiştirilmedi.");
                sonuc[altI] = altAday with { Olculer = olcu with { Uyarilar = uyari } };
                continue;
            }
            var kanit = olcu.Kanitlar.Concat(new[] {
                $"{altAday.OnerilenAd} → {ustAday.OnerilenAd}: {Sayi(ust)} − {Sayi(alt)} = {Sayi(h)} m; aynı kesit/görünüşteki tekrarlanan kotların düşey mesafesiyle doğrulandı. Bu değer ana referanslar arasıdır; net oda yüksekliği değildir."
            }).ToList();
            sonuc[altI] = altAday with { Olculer = olcu with { KatYuksekligiM = h, Kanitlar = kanit, Uyarilar = uyari } };
        }
        return sonuc;
    }

    /// <summary>
    /// Yalnız oda içinde, sınırdan en az 8 cm uzaktaki tekil göreli kot adaylarını
    /// bildirir. Bu mekânsal eşleşme döşeme/merdiven ayrımını veya üst döşemeyi
    /// kanıtlamaz. Komşu odaya/yakın kanada yaymaz; net yükseklik üretmez.
    /// Dönen koordinatlar katın mevcut (normalize edilmiş olabilen) çerçevesindedir.
    /// </summary>
    public static YerelKotSonucu YerelKotlariOku(BinaKat kat)
    {
        var etiketler = KotlariOku(kat.HamMetinler);
        var sonuc = new List<YerelKot>();
        var uyarilar = new List<string>();
        foreach (var oda in kat.Odalar)
        {
            var icindekiler = etiketler.Where(k => KesinIcinde(k.Pos, oda.Poligon)).ToList();
            if (icindekiler.Count == 0) continue;
            var kotlar = icindekiler.Select(k => k.Kot).Distinct().ToList();
            if (kotlar.Count != 1)
            {
                uyarilar.Add($"{oda.Ad}: aynı oda sınırında farklı kotlar ({string.Join(", ", kotlar.Select(Sayi))} m) var; yerel taban kotu atanmadı.");
                continue;
            }
            if (Regex.IsMatch(Duzelt(oda.Ad), @"\b(?:MERDIVEN|RAMPA)\b"))
            {
                uyarilar.Add($"{oda.Ad}: merdiven/rampa kotu bütün oda tabanına uygulanmadı.");
                continue;
            }
            sonuc.Add(new(oda.Id, kotlar[0], icindekiler.Select(k => $"'{k.Metin}' ({Sayi(k.Pos.X)}, {Sayi(k.Pos.Y)}) oda sınırı içinde.").ToList())
            {
                DosemeKotuAcik = icindekiler.Any(k => Regex.IsMatch(Duzelt(k.Metin), @"^(?:TABAN|DOSEME)\s+KOT(?:U)?\b"))
            });
        }
        return new(sonuc, uyarilar);
    }

    private static List<KotZinciri> ZincirleriOku(KatKumelemeServisi.KatKumesi kume)
    {
        var basliklar = kume.Metinler.Where(t => KatEtiketServisi.PlanDisiBasligiMi(t.Value) &&
            Regex.IsMatch(Duzelt(t.Value), @"\b(?:KESIT|KESITI|GORUNUS|GORUNUSU|CEPHE|CEPHESI)\b")).ToList();
        if (basliklar.Count == 0) return [];
        var kotlar = KotlariOku(kume.Metinler).OrderBy(k => k.Pos.Y - k.Kot).ToList();
        var zincirler = new List<KotZinciri>();
        // Tam aralık sınırı kullanılır: zincirleme yakınlıkla birbirinden uzak
        // referanslar birleşmez. Eşit koordinattaki CAD kopyaları oy sayılmaz.
        for (int bas = 0; bas < kotlar.Count;)
        {
            double referans = kotlar[bas].Pos.Y - kotlar[bas].Kot;
            int son = bas + 1;
            while (son < kotlar.Count && kotlar[son].Pos.Y - kotlar[son].Kot - referans <= ReferansToleransiM) son++;
            var grup = kotlar.GetRange(bas, son - bas);
            var tekrar = grup.GroupBy(k => k.Kot).Where(g => g.Select(k => k.Pos.X).Max() - g.Select(k => k.Pos.X).Min() >= .5)
                .Select(g => g.Key).Order().ToList();
            if (tekrar.Count >= 2 && tekrar[^1] - tekrar[0] >= 2)
            {
                double cx = grup.Average(k => k.Pos.X), cy = grup.Average(k => k.Pos.Y);
                var baslik = basliklar.MinBy(t => Math.Pow(t.Pos.X - cx, 2) + Math.Pow(t.Pos.Y - cy, 2));
                zincirler.Add(new(baslik.Value, grup.Average(k => k.Pos.Y - k.Kot), grup, tekrar));
            }
            bas = son;
        }
        return zincirler;
    }

    private static List<KotEtiketi> KotlariOku(IEnumerable<(Point2D Pos, string Value)> metinler)
    {
        var sonuc = new List<KotEtiketi>();
        foreach (var (pos, value) in metinler)
        {
            if (!double.IsFinite(pos.X) || !double.IsFinite(pos.Y)) continue;
            var m = Kot.Match(Duzelt(value));
            if (!m.Success || !double.TryParse(m.Groups["z"].Value.Replace(" ", "").Replace('±', '+').Replace(',', '.'),
                NumberStyles.Float, CultureInfo.InvariantCulture, out double z)) continue;
            z *= m.Groups["u"].Value switch { "MM" => .001, "CM" => .01, _ => 1 };
            if (!double.IsFinite(z) || Math.Abs(z) > 100) continue;
            sonuc.Add(new(pos, Math.Round(z, 3), value));
        }
        return sonuc;
    }

    private static string Kanit(KotZinciri zincir, double kot)
    {
        var noktalar = zincir.Etiketler.Where(k => k.Kot == kot).Take(2)
            .Select(k => $"'{k.Metin}' ({Sayi(k.Pos.X)}, {Sayi(k.Pos.Y)})");
        return $"{zincir.Baslik}: {string.Join("; ", noktalar)}; ortak düşey referans {Sayi(zincir.ReferansY)} m.";
    }

    private static string Duzelt(string s) => Regex.Replace(s.ToUpperInvariant().Replace('ı', 'I').Replace('İ', 'I').Replace('Ş', 'S')
        .Replace('Ğ', 'G').Replace('Ü', 'U').Replace('Ö', 'O').Replace('Ç', 'C').Replace("\\P", " "), @"\s+", " ").Trim();
    private static string Sayi(double n) => n.ToString("0.###", CultureInfo.InvariantCulture);
    private static bool KesinIcinde(Point2D p, IReadOnlyList<Point2D> poligon)
    {
        bool icinde = false;
        for (int i = 0, j = poligon.Count - 1; i < poligon.Count; j = i++)
        {
            var a = poligon[j]; var b = poligon[i];
            double dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
            double t = l2 < 1e-12 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2, 0, 1);
            if (Math.Pow(p.X - a.X - t * dx, 2) + Math.Pow(p.Y - a.Y - t * dy, 2) < .08 * .08) return false;
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X) icinde = !icinde;
        }
        return icinde;
    }
}
