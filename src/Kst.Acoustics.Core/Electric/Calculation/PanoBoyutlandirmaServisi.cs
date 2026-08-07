using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.Core.Electric.Calculation;

public sealed record PanoOlcusuSecenegi(double GenislikM, double YukseklikM)
{
    public double AlanM2 => GenislikM * YukseklikM;

    public string Etiket
    {
        get
        {
            var tr = CultureInfo.GetCultureInfo("tr-TR");
            return $"{GenislikM.ToString("0.00", tr)} x {YukseklikM.ToString("0.00", tr)} m " +
                   $"({AlanM2.ToString("0.00", tr)} m²)";
        }
    }
}

/// <summary>Modül ve klemens yerleşim alanından standart pano ölçüsünü seçer.</summary>
public static class PanoBoyutlandirmaServisi
{
    private const double ModulAlaniM2 = 0.01;
    private const double KlemensAlaniM2 = 0.005;
    private const double DuvarTipiAzamiAlanM2 = 1.0;

    public static IReadOnlyList<PanoOlcusuSecenegi> StandartOlculer { get; } =
    [
        new(0.10, 0.20),
        new(0.20, 0.30),
        new(0.30, 0.40),
        new(0.40, 0.50),
        new(0.50, 0.60),
        new(0.60, 0.70),
        new(0.70, 0.80),
        new(0.80, 0.90),
        new(0.90, 1.00),
        new(1.00, 1.00)
    ];

    public static IReadOnlyList<int> DikiliTipDerinlikleriCm { get; } = [40, 50, 60, 70, 80, 90];

    public static void Hesapla(Pano pano)
    {
        pano.KlemensSayisi = KlemensSayisiHesapla(pano);
        pano.ModulAlaniM2 = pano.ModulSayisi * ModulAlaniM2;
        pano.KlemensAlaniM2 = pano.KlemensSayisi * KlemensAlaniM2;
        pano.GerekliPanoAlaniM2 = pano.ModulAlaniM2 + pano.KlemensAlaniM2;

        if (pano.GovdeTipi == PanoGovdeTipi.DuvarTipi &&
            pano.GerekliPanoAlaniM2 > DuvarTipiAzamiAlanM2)
        {
            pano.GovdeTipi = PanoGovdeTipi.DikiliTip;
            pano.PanoDerinlikCm = 90;
        }

        if (!DikiliTipDerinlikleriCm.Contains(pano.PanoDerinlikCm))
            pano.PanoDerinlikCm = 90;

        if (!pano.PanoOlcusuOtomatik)
            return;

        var secilen = OtomatikOlcuyuSec(pano.GerekliPanoAlaniM2);
        pano.PanoGenislikM = secilen.GenislikM;
        pano.PanoYukseklikM = secilen.YukseklikM;
    }

    public static PanoOlcusuSecenegi OtomatikOlcuyuSec(double gerekliAlanM2) =>
        StandartOlculer.FirstOrDefault(o => o.AlanM2 + 0.000001 >= gerekliAlanM2)
        ?? StandartOlculer[^1];

    /// <summary>
    /// Pano metraj kimliği — hem ekranda hem Excel/DXF metrajında kullanılan tekil etiket.
    /// <b>Duvar tipi</b>: seçilen standart ön-yüz alanı (ör. 0,26 m² gerekli → "Duvar Tipi 0,30 m²");
    /// ham gerekli alan değil, kataloğa yuvarlanmış standart alan gösterilir.
    /// <b>Dikili tip</b>: derinlik (ör. "Dikili Tip 90 cm derinlik"); m² gösterilmez.
    /// </summary>
    public static string MetrajEtiketi(Pano pano)
    {
        var tr = CultureInfo.GetCultureInfo("tr-TR");
        return pano.GovdeTipi == PanoGovdeTipi.DikiliTip
            ? $"Dikili Tip {pano.PanoDerinlikCm} cm derinlik"
            : $"Duvar Tipi {pano.PanoOnYuzAlaniM2.ToString("0.00", tr)} m²";
    }

    /// <summary>
    /// Pano metraj gruplama anahtarı. Duvar tipi standart ön-yüz alanına (0,01 m² hassasiyetle),
    /// dikili tip derinliğe göre gruplanır; böylece farklı ham gerekli alanlı ama aynı standart
    /// ölçüye/derinliğe oturan panolar metrajda tek satırda toplanır.
    /// </summary>
    public static (PanoGovdeTipi Govde, double StandartAlanM2, int DerinlikCm) MetrajAnahtari(Pano pano)
        => pano.GovdeTipi == PanoGovdeTipi.DikiliTip
            ? (PanoGovdeTipi.DikiliTip, 0.0, pano.PanoDerinlikCm)
            : (PanoGovdeTipi.DuvarTipi, Math.Round(pano.PanoOnYuzAlaniM2, 2, MidpointRounding.AwayFromZero), 0);

    public static PanoOlcusuSecenegi? OlcuyuBul(double genislikM, double yukseklikM) =>
        StandartOlculer.FirstOrDefault(o =>
            Math.Abs(o.GenislikM - genislikM) < 0.0001 &&
            Math.Abs(o.YukseklikM - yukseklikM) < 0.0001);

    private static int KlemensSayisiHesapla(Pano pano)
    {
        if (pano.PanoTipi == PanoTipi.KontrolPanosu)
            return Math.Max(pano.KontrolButonlari.Count * 2, pano.KontrolSinyalKablolari.Sum(k => k.DamarSayisi));

        int girisKlemensi = 5;
        int linyeKlemensi = pano.Linyeler.Sum(l => l.FazTipi == FazTipi.Trifaze ? 5 : 3);
        int altPanoKlemensi = pano.AltPanolar.Count * 5;
        return girisKlemensi + linyeKlemensi + altPanoKlemensi;
    }
}
