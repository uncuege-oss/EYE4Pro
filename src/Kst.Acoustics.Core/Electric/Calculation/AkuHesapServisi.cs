using System;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.Core.Electric.Calculation;

/// <summary>
/// UPS akü grubu boyutlandırması — IEEE 485 sabit-güç yöntemi.
/// DC bara gerilimine göre seri blok sayısı, otonomi süresine göre blok başına
/// gerekli güç ve gereken paralel string sayısı hesaplanır.
/// </summary>
public static class AkuHesapServisi
{
    /// <summary>
    /// Seri blok sayısını DC bara nominal gerilimine göre belirler (en yakın tam blok).
    /// </summary>
    public static int SeriBlokSayisi(UpsTanim ups, AkuTanim aku)
    {
        if (aku.BlokGerilimV <= 0) return 0;
        int n = (int)Math.Round(ups.DcBaraGerilimV / aku.BlokGerilimV, MidpointRounding.AwayFromZero);
        return Math.Max(1, n);
    }

    /// <summary>
    /// Akü grubunu boyutlandırır ve sonuçları <paramref name="ups"/> üzerine yazar.
    /// İnverter giriş gücü kritik yükten ve UPS veriminden türetilir.
    /// </summary>
    public static void Hesapla(Ups ups, UpsTanim upsTanim, AkuTanim akuTanim)
    {
        double eta = Math.Clamp(upsTanim.VerimYuzde, 50, 100) / 100.0;

        // İnverter giriş gücü (W) — kritik aktif yük / verim
        double inverterGirisW = (ups.KritikYukKW * 1000.0) / eta;
        ups.InverterGirisGucW = inverterGirisW;

        // Seri blok sayısı — DC bara / blok gerilimi
        int seri = SeriBlokSayisi(upsTanim, akuTanim);
        ups.SeriBlokSayisi = seri;

        // Bir string için blok başına gerekli güç (W/blok)
        double blokBasinaGerekli = seri > 0 ? inverterGirisW / seri : 0;
        ups.BlokBasinaGerekliGucW = blokBasinaGerekli;

        // Akünün bu otonomide blok başına verebileceği güç (W/blok)
        double blokKapasiteW = akuTanim.BlokGucW(ups.OtonomiDk);

        // Gereken paralel string sayısı — bir blok yetmezse kol ekle
        int paralel = 1;
        if (blokKapasiteW > 0 && blokBasinaGerekli > blokKapasiteW)
            paralel = (int)Math.Ceiling(blokBasinaGerekli / blokKapasiteW);
        ups.ParalelStringSayisi = Math.Max(1, paralel);

        ups.ToplamBlokSayisi = seri * ups.ParalelStringSayisi;
        ups.ToplamAh = ups.ParalelStringSayisi * akuTanim.KapasiteAh;
        ups.ToplamEnerjiKWh = ups.ToplamBlokSayisi * akuTanim.BlokGerilimV * akuTanim.KapasiteAh / 1000.0;

        // Deşarj sonu gerilimi DC alt sınırını sağlamalı; tablo da geçerli olmalı.
        bool desarjGerilimiUygun = seri * akuTanim.DesarjSonuGerilimV >= upsTanim.MinDcBaraGerilimV;
        ups.AkuYeterliMi = blokKapasiteW > 0 && desarjGerilimiUygun;
    }
}
