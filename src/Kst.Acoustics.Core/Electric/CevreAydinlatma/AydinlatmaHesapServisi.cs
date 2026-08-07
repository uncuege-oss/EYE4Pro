using System;

namespace Kst.Acoustics.Core.Electric.CevreAydinlatma;

/// <summary>
/// Kademe A — lümen metodu ile dış/çevre aydınlatma hesabı. Ortalama yatay aydınlık ve hedef lükse
/// göre gerekli armatür sayısı / önerilen direk aralığı. (Nokta-nokta / EN 13201 Kademe B sonraki faz.)
/// MF = bakım faktörü, UF = kullanım faktörü.
/// </summary>
public static class AydinlatmaHesapServisi
{
    /// <summary>
    /// Ortalama yatay aydınlık (lüks): <c>E = N·Φ·MF·UF / A</c>.
    /// </summary>
    public static double OrtalamaAydinlik(
        int armaturSayisi, double akiLm, double alanM2,
        double bakimFaktoru = 0.8, double kullanimFaktoru = 0.5)
    {
        if (alanM2 <= 0)
            throw new ArgumentException("Alan pozitif olmalı.", nameof(alanM2));
        if (akiLm < 0 || armaturSayisi < 0)
            throw new ArgumentException("Akı ve armatür sayısı negatif olamaz.");

        return armaturSayisi * akiLm * bakimFaktoru * kullanimFaktoru / alanM2;
    }

    /// <summary>
    /// Hedef lükse ulaşmak için gereken armatür sayısı (yukarı yuvarlanır):
    /// <c>N = ceil(E·A / (Φ·MF·UF))</c>.
    /// </summary>
    public static int GerekliArmaturSayisi(
        double hedefLux, double alanM2, double akiLm,
        double bakimFaktoru = 0.8, double kullanimFaktoru = 0.5)
    {
        if (hedefLux <= 0)
            throw new ArgumentException("Hedef aydınlık pozitif olmalı.", nameof(hedefLux));
        double tekArmaturKatkisi = akiLm * bakimFaktoru * kullanimFaktoru;
        if (tekArmaturKatkisi <= 0)
            throw new ArgumentException("Akı ve faktörler pozitif olmalı.");

        return (int)Math.Ceiling(hedefLux * alanM2 / tekArmaturKatkisi);
    }

    /// <summary>
    /// Yol/şerit aydınlatmasında hedef lükse göre önerilen direk aralığı (m). Bir armatürün
    /// genişliği <paramref name="seritGenislikM"/> olan şeride katkısı: <c>E = Φ·MF·UF / (S·W)</c>
    /// → <c>S = Φ·MF·UF / (E·W)</c>.
    /// </summary>
    public static double OnerilenDirekAralik(
        double hedefLux, double seritGenislikM, double akiLm,
        double bakimFaktoru = 0.8, double kullanimFaktoru = 0.5)
    {
        if (hedefLux <= 0 || seritGenislikM <= 0)
            throw new ArgumentException("Hedef aydınlık ve şerit genişliği pozitif olmalı.");

        return akiLm * bakimFaktoru * kullanimFaktoru / (hedefLux * seritGenislikM);
    }
}
