using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Kst.Acoustics.Core.Bina.Services;

/// Bir kat planı kümesinin başlık metinlerinden ad + göreli yükseklik sırası tahmin eder
/// (codex50 §9.2). Tahmin her zaman kullanıcı tarafından dağıtım diyaloğunda düzeltilebilir;
/// yanlış tahmin veri kaybına yol açmaz.
public static class KatEtiketServisi
{
    /// <param name="OnerilenAd">Kullanıcıya önerilecek kat adı (örn. "Zemin Kat", "1. Kat").</param>
    /// <param name="SiraDegeri">Göreli yükseklik sırası — büyük değer daha yüksek kat.
    /// Bodrum negatif, zemin 0, normal katlar pozitif tam sayı, çatı çok büyük değer.
    /// Etiket bulunamazsa kümenin X konumu (soldan-sağa) kullanılır — kesin değildir.</param>
    /// <param name="EtiketBulundu">Metinden güvenilir bir eşleşme bulunduysa true.</param>
    public sealed record KatTahmini(string OnerilenAd, double SiraDegeri, bool EtiketBulundu);

    private static readonly Regex TipKatAraligi = new(
        @"(\d+)\s*-\s*(\d+)\s*\.?\s*KATLAR", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BodrumNumarali = new(
        @"(\d+)\s*\.?\s*BODRUM|BODRUM\s*-?\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NormalKat = new(
        @"(\d+)\s*\.?\s*(NORMAL\s*)?KAT\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static KatTahmini TahminEt(KatKumelemeServisi.KatKumesi kume)
    {
        var metinler = kume.Metinler.Select(m => m.Value).ToList();

        // Öncelik sırası önemli: özgül örüntüler (tip kat aralığı, asma, çatı, numaralı
        // bodrum) genel "N. KAT" örüntüsünden ÖNCE denenir; aksi halde "ASMA KAT" gibi
        // metinler yanlışlıkla normal kat sayılabilir.
        foreach (var t in metinler)
        {
            var m = TipKatAraligi.Match(t);
            if (m.Success && int.TryParse(m.Groups[1].Value, out int ilk) && int.TryParse(m.Groups[2].Value, out _))
                return new KatTahmini($"{m.Groups[1].Value}-{m.Groups[2].Value}. Katlar (Tip)", ilk, true);
        }

        foreach (var t in metinler)
            if (Icerir(t, "ASMA") && Icerir(t, "KAT"))
                return new KatTahmini("Asma Kat", 0.5, true);

        foreach (var t in metinler)
            if (Icerir(t, "ÇATI") || Icerir(t, "CATI"))
                return new KatTahmini("Çatı Katı", 9999, true);

        foreach (var t in metinler)
        {
            var m = BodrumNumarali.Match(t);
            if (!m.Success) continue;
            var numStr = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
            if (int.TryParse(numStr, out int no) && no > 0)
                return new KatTahmini($"Bodrum {no}", -no, true);
        }
        foreach (var t in metinler)
            if (Icerir(t, "BODRUM"))
                return new KatTahmini("Bodrum", -1, true);

        foreach (var t in metinler)
            if (Icerir(t, "ZEMİN") || Icerir(t, "ZEMIN"))
                return new KatTahmini("Zemin Kat", 0, true);

        foreach (var t in metinler)
        {
            var m = NormalKat.Match(t);
            if (m.Success && int.TryParse(m.Groups[1].Value, out int no) && no is > 0 and < 200)
                return new KatTahmini($"{no}. Kat", no, true);
        }

        // Etiket bulunamadı — kesin sıra tahmini yapılmaz, kullanıcı diyalogda düzeltir.
        return new KatTahmini("Kat", kume.MinX, false);
    }

    private static bool Icerir(string metin, string kelime)
        => metin.Contains(kelime, StringComparison.OrdinalIgnoreCase);
}
