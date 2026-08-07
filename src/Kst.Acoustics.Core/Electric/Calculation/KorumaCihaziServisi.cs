using System.Linq;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.Core.Electric.Calculation;

/// <summary>
/// Koruma cihazı (otomatik sigorta / TMŞ) tipi ve kesme kapasitesi seçimi.
/// Anma akımı ≤ 63A → otomatik sigorta (MCB), kesme kapasitesi 6 / 10 kA.
/// Anma akımı &gt; 63A → termik manyetik şalter / kompakt şalter (TMŞ / MCCB), 36 / 50 / 100 kA.
/// Kesme kapasitesi, bara üç-faz kısa devre akımının (Ik) üstündeki ilk standart değerdir.
/// </summary>
public static class KorumaCihaziServisi
{
    /// <summary>Bu anma akımının üstünde TMŞ (kompakt şalter) kullanılır; ≤ ise otomatik sigorta.</summary>
    public const int SigortaUstSinirA = 63;

    private static readonly double[] VarsayilanSigortaKesme = { 6, 10 };
    private static readonly double[] VarsayilanTmsKesme = { 36, 50, 100 };

    /// <summary>Anma akımı 63A üstünde mi (TMŞ gerekir)?</summary>
    public static bool TmsGerekli(int anmaAkimA) => anmaAkimA > SigortaUstSinirA;

    /// <summary>Bu cihaz TMŞ mi? &gt;63A zorunlu TMŞ; ayrıca kullanıcı <paramref name="tmsZorla"/> ile
    /// ≤63A bir cihazı da TMŞ'ye zorlayabilir.</summary>
    public static bool TmsAktif(int anmaAkimA, bool tmsZorla) => tmsZorla || TmsGerekli(anmaAkimA);

    /// <summary>Anma akımına göre koruma cihazı tipi etiketi: "Sigorta" (MCB) veya "TMŞ".</summary>
    public static string CihazTipi(int anmaAkimA) => TmsGerekli(anmaAkimA) ? "TMŞ" : "Sigorta";

    /// <summary>Anma akımı + TMŞ zorlamasına göre koruma cihazı tipi etiketi.</summary>
    public static string CihazTipi(int anmaAkimA, bool tmsZorla) => TmsAktif(anmaAkimA, tmsZorla) ? "TMŞ" : "Sigorta";

    /// <summary>
    /// Anma akımı ve bara Ik'sına göre kesme kapasitesini "6kA"/"10kA"/"36kA"/… biçiminde verir.
    /// ≤63A: MCB seçenekleri (6/10), &gt;63A: TMŞ seçenekleri (36/50/100). Kütüphane verilirse
    /// kesme kapasitesi değerleri kütüphaneden türetilir.
    /// </summary>
    public static string KesmeKapasitesiSec(int anmaAkimA, double ikKA, ElektrikKutuphaneServisi? kutuphane = null)
        => KesmeKapasitesiSec(anmaAkimA, ikKA, false, kutuphane);

    /// <summary>
    /// Kesme kapasitesi seçimi; <paramref name="tmsZorla"/> ile ≤63A cihaz TMŞ seçeneklerine zorlanabilir.
    /// </summary>
    public static string KesmeKapasitesiSec(int anmaAkimA, double ikKA, bool tmsZorla, ElektrikKutuphaneServisi? kutuphane)
    {
        bool tms = TmsAktif(anmaAkimA, tmsZorla);
        var opsiyonlar = KesmeKapasitesiOpsiyonlari(tms, kutuphane);

        double secilen = opsiyonlar.FirstOrDefault(k => k >= ikKA);
        if (secilen <= 0)
            secilen = opsiyonlar.Length > 0 ? opsiyonlar[^1] : (tms ? 100 : 10);
        return $"{secilen:0.#}kA";
    }

    /// <summary>Cihaz tipine (TMŞ/sigorta) göre standart kesme kapasitesi değerleri (kA), artan sıralı.
    /// Kütüphane verilirse oradan türetilir; yoksa varsayılan setler kullanılır.</summary>
    public static double[] KesmeKapasitesiOpsiyonlari(bool tms, ElektrikKutuphaneServisi? kutuphane = null)
        => tms ? TmsKesmeOpsiyonlari(kutuphane) : SigortaKesmeOpsiyonlari(kutuphane);

    /// <summary>Verilen "10kA" biçimindeki kesme kapasitesi değeri, cihaz tipinin seçenekleri arasında
    /// geçerli mi? (Kullanıcı manuel seçimini doğrulamak için.)</summary>
    public static bool GecerliKesme(string? deger, bool tms, ElektrikKutuphaneServisi? kutuphane = null)
    {
        if (string.IsNullOrWhiteSpace(deger)) return false;
        var temiz = deger.Replace("kA", "", System.StringComparison.OrdinalIgnoreCase).Trim();
        if (!double.TryParse(temiz, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v)) return false;
        return KesmeKapasitesiOpsiyonlari(tms, kutuphane).Any(k => System.Math.Abs(k - v) < 0.05);
    }

    private static double[] SigortaKesmeOpsiyonlari(ElektrikKutuphaneServisi? k)
    {
        var v = k?.Sigortalar.Select(s => s.KesmeKapasitesiKA).Where(x => x > 0)
            .Distinct().OrderBy(x => x).ToArray();
        return v is { Length: > 0 } ? v : VarsayilanSigortaKesme;
    }

    private static double[] TmsKesmeOpsiyonlari(ElektrikKutuphaneServisi? k)
    {
        var v = k?.Tmsler.Select(t => t.KesmeKapasitesiKA).Where(x => x > 0)
            .Distinct().OrderBy(x => x).ToArray();
        return v is { Length: > 0 } ? v : VarsayilanTmsKesme;
    }
}
