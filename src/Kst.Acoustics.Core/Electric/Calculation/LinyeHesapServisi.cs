using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.Core.Electric.Calculation;

/// <summary>
/// Linye hesap servisi — akım, sigorta boyutlandırma.
/// </summary>
public static class LinyeHesapServisi
{
    // IEC 60898/60947 standart sigorta-şalter değerleri. ≤63A MCB; üstü TMŞ/kompakt şalter.
    // Büyük trifaze linyeler (ör. büyük motor) 250A'da kalmasın diye üst standartlar (315–630A) eklendi.
    private static readonly int[] StandartSigortaA =
        { 6, 10, 13, 16, 20, 25, 32, 40, 50, 63, 80, 100, 125, 160, 200, 250, 315, 400, 500, 630 };

    /// <summary>
    /// Linye akımını hesaplar.
    /// Monofaze: I = P / (V × cosφ)
    /// Trifaze:  I = P / (√3 × V × cosφ)
    /// </summary>
    public static double AkimHesapla(double gucW, double gerilimV, double cosF, FazTipi fazTipi)
    {
        if (gucW <= 0 || gerilimV <= 0 || cosF <= 0) return 0;
        cosF = NumberHelper.NormalizeCosF(cosF);

        return fazTipi switch
        {
            FazTipi.Monofaze => gucW / (gerilimV * cosF),
            FazTipi.Trifaze => gucW / (Math.Sqrt(3) * gerilimV * cosF),
            _ => 0
        };
    }

    /// <summary>
    /// IEC 60898 standart sigorta değerine yuvarlar (hesaplanan akımın üstündeki en yakın).
    /// </summary>
    public static int SigortaBoyutlandir(double akimA, int minimumA = 0)
    {
        var hedefAkim = Math.Max(akimA, minimumA);
        if (hedefAkim <= 0) return 0;
        foreach (var s in StandartSigortaA)
        {
            if (s >= hedefAkim) return s;
        }
        return StandartSigortaA[^1]; // Max
    }

    public static int MinimumSigortaA(string? yukTipiAd)
    {
        var ad = yukTipiAd?.Trim() ?? "";
        if (ad.Contains("Motor", StringComparison.OrdinalIgnoreCase)) return 20;
        if (ad.Contains("Priz", StringComparison.OrdinalIgnoreCase)) return 16;
        if (ad.Contains("Aydınlatma", StringComparison.OrdinalIgnoreCase) ||
            ad.Contains("Aydinlatma", StringComparison.OrdinalIgnoreCase)) return 10;
        if (ad.Contains("Mekanik", StringComparison.OrdinalIgnoreCase)) return 6;
        return 0;
    }

    /// <summary>
    /// Yük tipine göre sigorta cinsi — B: resistif, C: motor/trafo, D: ağır motor.
    /// </summary>
    public static SigortaCinsi SigortaCinsiTayin(string sigortaCinsiStr)
    {
        return sigortaCinsiStr?.ToUpperInvariant() switch
        {
            "C" => SigortaCinsi.C,
            "D" => SigortaCinsi.D,
            _ => SigortaCinsi.B
        };
    }


    /// <summary>
    /// Tek bir linye için tüm hesaplamaları yapar.
    /// </summary>
    public static void LinyeHesapla(Linye linye, double fazGerilimV, double hatGerilimV, double cosF)
    {
        var gerilim = linye.FazTipi == FazTipi.Monofaze ? fazGerilimV : hatGerilimV;
        linye.AkimA = AkimHesapla(linye.ToplamGucW, gerilim, cosF, linye.FazTipi);
        linye.SigortaAmper = SigortaBoyutlandir(linye.AkimA, MinimumSigortaA(linye.YukTipiAd));
        // SigortaCinsi linye oluşturulurken yük tipinden atanır ve kullanıcı düzenleyebilir;
        // hesap sırasında korunur. KesmeKapasitesi anma akımına göre (≤63A sigorta / >63A TMŞ; kullanıcı
        // ≤63A'i TMŞ'ye zorlamış olabilir) taban değere ayarlanır; kısa devre hesabı çalışınca
        // KisaDevreHesapServisi bara Ik'sına göre yeniden yazar. Kullanıcı manuel seçtiyse dokunulmaz.
        if (!linye.KesmeKapasitesiManuel)
            linye.KesmeKapasitesi = KorumaCihaziServisi.KesmeKapasitesiSec(linye.SigortaAmper, 0, linye.TmsZorla, null);
    }
}
