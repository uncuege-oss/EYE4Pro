using System;
using System.Collections.Generic;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Electric.CevreAydinlatma;

/// <summary>
/// TT sistem topraklama hesapları (M15 Faz 7): şerit + kazık yayılma direnci ve RCD dokunma gerilimi
/// kriteri. Standart çapası TS HD 60364-5-54 / IEC 60364-7-714. Kazık etkileşim (kullanım) faktörü
/// ihmal edilir (paralel kombinasyon iyimser sonuç verir).
/// </summary>
public static class TopraklamaServisi
{
    /// <summary>Şerit güzergâhının (polyline) toplam uzunluğu (m).</summary>
    public static double GuzergahUzunlugu(IReadOnlyList<Point2D> rota)
    {
        double toplam = 0;
        for (int i = 0; i < rota.Count - 1; i++)
        {
            double dx = rota[i + 1].X - rota[i].X;
            double dy = rota[i + 1].Y - rota[i].Y;
            toplam += Math.Sqrt(dx * dx + dy * dy);
        }
        return toplam;
    }

    /// <summary>
    /// Tek düşey kazık yayılma direnci (Ω): <c>R = ρ/(2πL)·(ln(4L/d) − 1)</c>.
    /// </summary>
    public static double KazikDirenci(double ozdirencOhmM, double kazikBoyM, double kazikCapM)
    {
        if (kazikBoyM <= 0 || kazikCapM <= 0)
            throw new ArgumentException("Kazık boyu ve çapı pozitif olmalı.");
        return ozdirencOhmM / (2 * Math.PI * kazikBoyM) * (Math.Log(4 * kazikBoyM / kazikCapM) - 1);
    }

    /// <summary>
    /// Yatay gömülü şerit yayılma direnci (Ω): <c>R = ρ/(πL)·ln(2L²/(w·h))</c>.
    /// </summary>
    public static double SeritDirenci(double ozdirencOhmM, double seritUzunlukM, double seritGenislikM, double gomulmeDerinlikM)
    {
        if (seritUzunlukM <= 0 || seritGenislikM <= 0 || gomulmeDerinlikM <= 0)
            throw new ArgumentException("Şerit uzunluğu, genişliği ve derinliği pozitif olmalı.");
        return ozdirencOhmM / (Math.PI * seritUzunlukM)
            * Math.Log(2 * seritUzunlukM * seritUzunlukM / (seritGenislikM * gomulmeDerinlikM));
    }

    /// <summary>
    /// Şerit + N paralel kazık toplam direnci (Ω). Kazıklar arası etkileşim ihmal edilir
    /// (kullanım faktörü 1 → iyimser).
    /// </summary>
    public static double ToplamDirenc(double seritDirenci, double tekKazikDirenci, int kazikSayisi)
    {
        double iletkenlik = 0;
        if (seritDirenci > 0)
            iletkenlik += 1.0 / seritDirenci;
        if (kazikSayisi > 0 && tekKazikDirenci > 0)
            iletkenlik += kazikSayisi / tekKazikDirenci;
        return iletkenlik > 0 ? 1.0 / iletkenlik : double.PositiveInfinity;
    }

    /// <summary>TT'de izin verilen en yüksek yayılma direnci: <c>R_A ≤ U_L / I_Δn</c> (U_L = 50 V).</summary>
    public static double IzinVerilenMaxDirenc(double rcdAnmaAkimA, double izinGerilimV = 50.0)
    {
        if (rcdAnmaAkimA <= 0)
            throw new ArgumentException("RCD anma akımı pozitif olmalı.", nameof(rcdAnmaAkimA));
        return izinGerilimV / rcdAnmaAkimA;
    }

    /// <summary>TT dokunma gerilimi kriteri sağlanıyor mu: <c>R_A · I_Δn ≤ U_L</c>.</summary>
    public static bool RcdUygunMu(double yayilmaDirenciOhm, double rcdAnmaAkimA, double izinGerilimV = 50.0)
        => yayilmaDirenciOhm <= IzinVerilenMaxDirenc(rcdAnmaAkimA, izinGerilimV) + 1e-9;
}
