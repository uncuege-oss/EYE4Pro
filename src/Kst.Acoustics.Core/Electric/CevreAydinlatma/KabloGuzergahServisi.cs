using System;
using System.Collections.Generic;
using Kst.Acoustics.Core.Electric.Calculation;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.Core.Electric.CevreAydinlatma;

/// <summary>Bir aydınlatma devresi (panodan beslenen direk dizisi) için kablo hesabı sonucu.</summary>
public sealed class KabloHesapSonucu
{
    public double ToplamGucW { get; set; }
    public double ToplamAkimA { get; set; }
    public double ToplamUzunlukM { get; set; }

    /// <summary>Dağıtık yük için gerilim düşümü eşdeğeri uzunluk (moment / toplam akım).</summary>
    public double EsdegerUzunlukM { get; set; }

    public double KesitMm2 { get; set; }
    public string KesitAd { get; set; } = "";
    public double GerilimDususYuzde { get; set; }
}

/// <summary>
/// Panodan çıkan radyal aydınlatma devresi (direk dizisi) için kablo kesiti + gerilim düşümü hesabı.
/// Dağıtık yük gerilim düşümü, elektriksel moment (Σ I_segment·L_segment) ile eşdeğer uzunluğa
/// indirilir; kesit boyutlandırma M11 <see cref="KabloHesapServisi.KesitHesapla"/> ile yapılır
/// (termal + ΔU, TS HD 60364). Park feeder'ları uzun olduğundan ΔU genelde belirleyicidir.
/// </summary>
public static class KabloGuzergahServisi
{
    /// <param name="direkYukleriW">Sıralı direk yükleri (W); ilk eleman panoya en yakın direk.</param>
    /// <param name="segmentBoylariM">Segment uzunlukları (m): pano→direk1, direk1→direk2, … (yük listesiyle aynı sayıda).</param>
    public static KabloHesapSonucu DevreyiHesapla(
        IReadOnlyList<double> direkYukleriW,
        IReadOnlyList<double> segmentBoylariM,
        FazTipi faz = FazTipi.Monofaze,
        double izinVerilenDusumYuzde = 5.0,
        double cosF = 0.9)
    {
        if (direkYukleriW.Count == 0)
            throw new ArgumentException("Devrede en az bir direk olmalı.", nameof(direkYukleriW));
        if (direkYukleriW.Count != segmentBoylariM.Count)
            throw new ArgumentException("Yük ve segment listeleri aynı uzunlukta olmalı.");

        double gerilimV = faz == FazTipi.Monofaze ? 230.0 : 400.0;
        double bolen = faz == FazTipi.Monofaze ? gerilimV * cosF : Math.Sqrt(3) * gerilimV * cosF;

        int n = direkYukleriW.Count;

        // Segment akımı = o segmentten beslenen (downstream) tüm direklerin akımı.
        // Akı (moment) = Σ segmentAkımı · segmentBoyu.
        double toplamGuc = 0;
        double toplamUzunluk = 0;
        double moment = 0;
        for (int i = 0; i < n; i++)
        {
            double downstreamGuc = 0;
            for (int j = i; j < n; j++)
                downstreamGuc += direkYukleriW[j];

            double segmentAkim = downstreamGuc / bolen;
            moment += segmentAkim * segmentBoylariM[i];
            toplamUzunluk += segmentBoylariM[i];
            toplamGuc += direkYukleriW[i];
        }

        double toplamAkim = toplamGuc / bolen;
        double esdegerUzunluk = toplamAkim > 0 ? moment / toplamAkim : 0;

        var (kesit, kesitAd, dusumYuzde) = KabloHesapServisi.KesitHesapla(
            toplamAkim, esdegerUzunluk, faz, gerilimV, izinVerilenDusumYuzde, cosF);

        return new KabloHesapSonucu
        {
            ToplamGucW = toplamGuc,
            ToplamAkimA = toplamAkim,
            ToplamUzunlukM = toplamUzunluk,
            EsdegerUzunlukM = esdegerUzunluk,
            KesitMm2 = kesit,
            KesitAd = kesitAd,
            GerilimDususYuzde = dusumYuzde
        };
    }
}
