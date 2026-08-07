using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.BuildingAcoustics.Models;

namespace Kst.Acoustics.Core.BuildingAcoustics.Calculation;

public record HesapGuvenilirlikSonucu(
    int Puan,
    string Seviye,
    string Renk,
    IReadOnlyList<string> Uyarilar)
{
    public string Ozet => Uyarilar.Count == 0
        ? $"{Seviye} ({Puan}/100)"
        : $"{Seviye} ({Puan}/100): {string.Join("; ", Uyarilar.Take(3))}";
}

public class HesapGuvenilirlikServisi
{
    public HesapGuvenilirlikSonucu Degerlendir(BinaProje proje, Bolme bolme)
    {
        var puan = 100;
        var uyarilar = new List<string>();

        if (bolme.YapiElemani == null)
            return new HesapGuvenilirlikSonucu(15, "Veri eksik", "#F1C40F", ["Yapı elemanı atanmamış"]);

        if (!bolme.HasGeometry || bolme.Alan <= 0)
        {
            puan -= 12;
            uyarilar.Add("Geometri/alan verisi zayıf");
        }

        DegerlendirYapiElemani(bolme.YapiElemani, "Ana eleman", ref puan, uyarilar);

        foreach (var aciklik in bolme.Acikliklar)
        {
            if (aciklik.YapiElemani == null)
            {
                puan -= 10;
                uyarilar.Add($"{aciklik.Tip} varsayılan yalıtım değeriyle hesaplandı");
            }
            else
            {
                DegerlendirYapiElemani(aciklik.YapiElemani, aciklik.Tip.ToString(), ref puan, uyarilar, 8);
            }
        }

        // A8 — Menfez Dn,e değerlendirme: spektrum yoksa veya tipik tablo değeri kullanılıyorsa puan kırpılır.
        foreach (var menfez in bolme.Menfezler)
        {
            if (menfez.Tip != MenfezTipi.Bagimsiz &&
                Math.Abs(menfez.DneDb - MenfezVarsayilan.DneIcin(menfez.Tip)) < 0.5)
            {
                puan -= 6;
                uyarilar.Add($"Menfez ({menfez.Tip}) tipik Dn,e tablosundan; üretici verisi yok");
            }
            if (!menfez.HasSpectrum)
            {
                puan -= 3;
                uyarilar.Add($"Menfez ({menfez.Tip}) 1/3 oktav spektrum verisi yok; tek sayı Dn,e uniform uygulanıyor");
            }
        }

        if (bolme.YapiElemani.Tip != YapiElemaniTipi.Doseme)
            DegerlendirHavaSesiDetaylari(bolme, ref puan, uyarilar);
        else
            DegerlendirDarbeSesiDetaylari(bolme.YapiElemani, ref puan, uyarilar);

        if (bolme.IsDisCephe)
            DegerlendirDisCephe(proje, bolme, ref puan, uyarilar);

        if (bolme.KomsulukTipi == BolmeKomsulukTipi.Bilinmiyor)
        {
            puan -= 5;
            uyarilar.Add("Bağımsız bölüm komşuluğu atanmamış");
        }

        puan = Math.Clamp(puan, 0, 100);
        var (seviye, renk) = puan switch
        {
            >= 85 => ("Yüksek", "#27AE60"),
            >= 70 => ("İyi", "#2ECC71"),
            >= 55 => ("Orta", "#F39C12"),
            >= 40 => ("Düşük", "#E67E22"),
            _ => ("Riskli", "#E74C3C")
        };

        return new HesapGuvenilirlikSonucu(puan, seviye, renk, uyarilar.Distinct().ToList());
    }

    private static void DegerlendirYapiElemani(
        YapiElemani eleman,
        string etiket,
        ref int puan,
        List<string> uyarilar,
        int modelCeza = 15)
    {
        if (!OktavBantElemanServisi.TamHavaSpektrumuVar(eleman))
        {
            puan -= modelCeza;
            uyarilar.Add($"{etiket} spektrumu eksik; model/tahmin kullanıldı");
            return;
        }

        switch (OktavBantElemanServisi.EtkinSpektrumKaynagi(eleman))
        {
            case SpektrumVeriKaynagi.Olcum:
                return;
            case SpektrumVeriKaynagi.Uretici:
                puan -= Math.Max(2, modelCeza / 4);
                uyarilar.Add($"{etiket} spektrumu üretici beyanı; laboratuvar ölçümü değil");
                return;
            case SpektrumVeriKaynagi.Varsayilan:
                puan -= modelCeza + 5;
                uyarilar.Add($"{etiket} spektrumu varsayılan değerlerden");
                return;
            default:
                puan -= modelCeza;
                uyarilar.Add($"{etiket} spektrumu model/tahmin");
                return;
        }
    }

    private static void DegerlendirHavaSesiDetaylari(Bolme bolme, ref int puan, List<string> uyarilar)
    {
        if (bolme.UcuncuOktavHavaSesiSonucu?.Kaynak is not ("Olcum" or "Uretici"))
        {
            puan -= 8;
            uyarilar.Add("ISO 717 tek sayı sonucu model spektrumdan üretildi");
        }

        if (!bolme.IsDisCephe)
        {
            if (bolme.Junctions.Count < 4)
            {
                puan -= 10;
                uyarilar.Add("Yanal iletim bağlantıları otomatik varsayılacak");
            }
            else if (bolme.Junctions.All(j => j.KijOverrideDb <= 0))
            {
                puan -= 6;
                uyarilar.Add("Kij değerleri bağlantı tipinden tahmini");
            }
        }

        if (bolme.YapiElemani?.KritikFrekansHz is >= 100 and <= 3150)
        {
            puan -= 5;
            uyarilar.Add("Kritik frekans değerlendirme bandında");
        }
    }

    private static void DegerlendirDarbeSesiDetaylari(YapiElemani eleman, ref int puan, List<string> uyarilar)
    {
        var tam = OktavBantElemanServisi.TamOktavDarbeVerisiVar(eleman);
        var kaynak = OktavBantElemanServisi.EtkinDarbeSpektrumKaynagi(eleman);
        if (!tam || kaynak is SpektrumVeriKaynagi.Model or SpektrumVeriKaynagi.Belirsiz)
        {
            puan -= 18;
            uyarilar.Add(tam
                ? "Darbe sesi spektrumu model/tahmin"
                : "Darbe sesi spektrumu eksik; model/tahmin kullanıldı");
        }
        else if (kaynak == SpektrumVeriKaynagi.Uretici)
        {
            puan -= 5;
            uyarilar.Add("Darbe sesi spektrumu üretici beyanı; laboratuvar ölçümü değil");
        }
        else if (kaynak == SpektrumVeriKaynagi.Varsayilan)
        {
            puan -= 22;
            uyarilar.Add("Darbe sesi spektrumu varsayılan değerlerden");
        }

        if (eleman.DinamikRijitlikMNm3 <= 0 && eleman.DeltaLw > 0)
        {
            puan -= 6;
            uyarilar.Add("Yüzer döşeme için dinamik rijitlik girilmemiş");
        }
    }

    private static void DegerlendirDisCephe(BinaProje proje, Bolme bolme, ref int puan, List<string> uyarilar)
    {
        if (!DisGurultuServisi.TamDisGurultuSpektrumuVar(proje))
        {
            puan -= 15;
            uyarilar.Add("Dış gürültü spektrumu normalize trafik varsayımı");
        }
        else
        {
            switch (proje.DisGurultuSpektrumKaynagi)
            {
                case SpektrumVeriKaynagi.Olcum:
                    break;
                case SpektrumVeriKaynagi.Uretici:
                    puan -= 5;
                    uyarilar.Add("Dış gürültü spektrumu üretici/üçüncü taraf beyanı");
                    break;
                case SpektrumVeriKaynagi.Model:
                    puan -= 12;
                    uyarilar.Add("Dış gürültü spektrumu model sonucu");
                    break;
                case SpektrumVeriKaynagi.Varsayilan:
                    puan -= 15;
                    uyarilar.Add("Dış gürültü spektrumu varsayılan");
                    break;
                default:
                    puan -= 10;
                    uyarilar.Add("Dış gürültü spektrumunun ölçüm/üretici kökeni belirtilmemiş");
                    break;
            }
        }

        if (bolme.DisCepheSpektralSonuc == null)
        {
            puan -= 8;
            uyarilar.Add("Dış cephe spektral sonucu üretilmemiş");
        }

        if (bolme.Acikliklar.Count == 0)
        {
            puan -= 4;
            uyarilar.Add("Cephede açıklık/pencere tanımı yok");
        }
    }
}
