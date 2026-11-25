using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Kst.Acoustics.Core.Bina.Services;

/// Kat başlıklarını oda/döşeme notlarından ayırır. Bir kümede çelişen plan başlıkları
/// varsa rastgele kat seçmek yerine ad/sıra için kullanıcı kontrolü ister.
public static class KatEtiketServisi
{
    public sealed record KatTahmini(string OnerilenAd, double SiraDegeri, bool EtiketBulundu);

    private const string Son = @"(?:\s+PLANI)?$";
    private static readonly Regex TipKatAraligi = new(@"^(\d+)\s*[-–]\s*(\d+)\s*\.?\s*KATLAR?(?:I)?" + Son, RegexOptions.Compiled);
    private static readonly Regex BodrumNumarali = new(@"^(?:(\d+)\s*\.?\s*BODRUM(?:\s+KAT(?:I)?)?|BODRUM\s*-?\s*(\d+)(?:\s+KAT(?:I)?)?)" + Son, RegexOptions.Compiled);
    private static readonly Regex NormalKat = new(@"^(\d+)\s*\.?\s*(?:NORMAL\s*)?KAT(?:I)?" + Son, RegexOptions.Compiled);
    private static readonly Regex PlanDisi = new(
        @"^(?:(?:[A-Z0-9]+\s*[-–]\s*[A-Z0-9]+|[A-Z0-9]+|ON|ARKA|SAG|SOL|KUZEY|GUNEY|DOGU|BATI)\s+)?(?:KESIT(?:I)?|GORUNUS(?:U)?|CEPHE(?:SI)?)(?:\s+[A-Z0-9]+)?$|^(?:VAZIYET|YERLESIM)\s+PLANI$|^(?:KAPI|PENCERE|DOGRAM[A-Z]*)\s+(?:DETAY(?:I|LARI)?|CIZELGESI|LISTESI)$|^(?:(?:ZEMIN|ASMA|\d+\.?)(?:\s+KAT)?\s+)?(?:ASMA\s+)?TAVAN\s+PLANI$",
        RegexOptions.Compiled);

    public static KatTahmini TahminEt(KatKumelemeServisi.KatKumesi kume)
    {
        var adaylar = kume.Metinler
            .Select(m => (Metin: BaslikTemizle(m.Value), Tahmin: MetindenTahmin(m.Value), Olcekli: OlcekliBaslikMi(m.Value)))
            .Where(x => x.Tahmin is not null).ToList();
        // Antetteki eski/kopyalanmış "Zemin Kat Planı" yazısı, çizimin yanındaki
        // "1. Kat Planı 1/50" başlığını geçersiz kılamaz.
        var olcekli = adaylar.Where(a => a.Olcekli).ToList();
        if (olcekli.Count > 0) adaylar = olcekli;
        // "1. Kat Planı" gerçek çizim başlığıdır; kesitteki kat kotu yazıları ve
        // "Zemin Kat" gibi basit referanslar ancak böyle bir başlık yoksa kullanılır.
        var planBasliklari = adaylar.Where(a => a.Metin.EndsWith(" PLANI", StringComparison.Ordinal)).ToList();
        if (planBasliklari.Count > 0) adaylar = planBasliklari;
        var farkli = adaylar.Select(a => a.Tahmin!).Distinct().ToList();
        return farkli.Count == 1 ? farkli[0] : new KatTahmini("Kat", kume.MinX, false);
    }

    /// Dışarıdaki metnin en yakın çizime atanmasına uygun bir çizim başlığı mı?
    public static bool CizimBasligiMi(string metin)
        => MetindenTahmin(metin) is not null || PlanDisiBasligiMi(metin);

    public static bool PlanDisiBasligiMi(string metin)
        => PlanDisi.IsMatch(BaslikTemizle(metin));

    public static bool AcikPlanBasligiMi(string metin)
        => BaslikTemizle(metin).EndsWith(" PLANI", StringComparison.Ordinal) && MetindenTahmin(metin) is not null;

    public static bool OlcekliBaslikMi(string metin)
        => CizimBasligiMi(metin) && Regex.IsMatch(metin, @"\b1\s*[:/]\s*\d+\s*$");

    private static KatTahmini? MetindenTahmin(string metin)
    {
        var t = BaslikTemizle(metin);
        var m = TipKatAraligi.Match(t);
        if (m.Success && int.TryParse(m.Groups[1].Value, out int ilk) &&
            int.TryParse(m.Groups[2].Value, out int son) && ilk > 0 && son >= ilk && son < 200)
            return new($"{ilk}-{son}. Katlar (Tip)", ilk, true);

        m = BodrumNumarali.Match(t);
        if (m.Success && int.TryParse(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value, out int bodrum) && bodrum is > 0 and < 100)
            return new($"Bodrum {bodrum}", -bodrum, true);
        if (Regex.IsMatch(t, @"^BODRUM(?:\s+KAT(?:I)?)?" + Son)) return new("Bodrum", -1, true);
        if (Regex.IsMatch(t, @"^ZEMIN\s+KAT(?:I)?" + Son)) return new("Zemin Kat", 0, true);
        if (Regex.IsMatch(t, @"^ASMA\s+KAT(?:I)?" + Son)) return new("Asma Kat", 0.5, true);
        if (Regex.IsMatch(t, @"^CATI\s+ALTI\s+VE\s+TERAS\s+PLANI$")) return new("Çatı Altı ve Teras", 9999, true);
        if (Regex.IsMatch(t, @"^CATI(?:\s+KAT(?:I)?)?" + Son)) return new("Çatı Katı", 9999, true);
        m = NormalKat.Match(t);
        if (m.Success && int.TryParse(m.Groups[1].Value, out int kat) && kat is > 0 and < 200)
            return new($"{kat}. Kat", kat, true);
        return null;
    }

    private static string BaslikTemizle(string metin)
    {
        var t = (metin ?? "").ToUpperInvariant().Replace('ı', 'I').Replace('İ', 'I').Replace('Ş', 'S')
            .Replace('Ğ', 'G').Replace('Ü', 'U').Replace('Ö', 'O').Replace('Ç', 'C');
        t = t.Replace("\\P", " ");
        t = Regex.Replace(t, @"\s+", " ").Trim();
        // Ölçek ve kot notları başlık sonuna yazılabilir; malzeme/mahâl metinlerini
        // kesip yanlış kat başlığına dönüştürmemek için yalnız sayısal son ekleri çıkar.
        t = Regex.Replace(t, @"\s*(?:OLCEK\s*[:=]?\s*)?1\s*[:/]\s*\d+\s*$", "").Trim();
        t = Regex.Replace(t, @"\s*[\(\[]?\s*(?:KOT(?:U)?\s*[:=]?\s*)?[+±-]\s*\d+[.,]\d+\s*(?:M)?\s*[\)\]]?\s*$", "").Trim();
        t = Regex.Replace(t, @"^(?:[A-Z0-9]+\s+BLOK\s*[-:]?\s*)", "");
        return t.Trim();
    }
}
