using System.Globalization;
using System.Text;
using Kst.Acoustics.Core.Terrain.Models;

namespace Kst.Acoustics.Core.Terrain.Services;

/// F6 — Hafriyat özet raporu üreticisi. Ham yarma/dolgu hacimlerine kabarma/sıkışma
/// katsayılarını uygular, bank dengeyi ve Türkçe özet metni üretir. UI'sizdir.
public static class HafriyatRaporServisi
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static HafriyatRaporu Olustur(HafriyatSonucu sonuc, HafriyatKatsayilari? katsayilar = null)
    {
        var k = katsayilar ?? HafriyatKatsayilari.Yok;

        var rapor = new HafriyatRaporu
        {
            YarmaBankHacimM3 = sonuc.YarmaHacmiM3,
            DolguSikismisHacimM3 = sonuc.DolguHacmiM3,
            KazilanGevsekHacimM3 = sonuc.YarmaHacmiM3 * (1 + k.KabarmaYuzdesi / 100.0),
            DolguIcinGerekliBankHacimM3 = sonuc.DolguHacmiM3 * (1 + k.SikismaYuzdesi / 100.0)
        };

        rapor.Ozet = OzetMetni(sonuc, rapor, k);
        return rapor;
    }

    private static string OzetMetni(HafriyatSonucu sonuc, HafriyatRaporu rapor, HafriyatKatsayilari k)
    {
        string M3(double v) => v.ToString("N1", Tr) + " m³";

        var sb = new StringBuilder();
        sb.AppendLine("HAFRİYAT ÖZETİ");
        sb.AppendLine($"Yarma (kazı): {M3(rapor.YarmaBankHacimM3)}");
        sb.AppendLine($"Dolgu: {M3(rapor.DolguSikismisHacimM3)}");
        sb.AppendLine($"Net: {M3(sonuc.NetHacimM3)}");

        if (k.KabarmaYuzdesi > 0)
            sb.AppendLine($"Kazılan gevşek (kabarma %{k.KabarmaYuzdesi.ToString("0.#", Tr)}): {M3(rapor.KazilanGevsekHacimM3)}");
        if (k.SikismaYuzdesi > 0)
            sb.AppendLine($"Dolgu için gereken bank (sıkışma %{k.SikismaYuzdesi.ToString("0.#", Tr)}): {M3(rapor.DolguIcinGerekliBankHacimM3)}");

        sb.AppendLine(rapor.FazlaMalzemeVar
            ? $"Bank denge: {M3(rapor.DengeBankHacimM3)} FAZLA (depoya nakil)"
            : $"Bank denge: {M3(-rapor.DengeBankHacimM3)} EKSİK (dış ödünç)");

        if (sonuc.BolgeSonuclari.Count > 0)
        {
            sb.AppendLine("Bölge dökümü:");
            foreach (var b in sonuc.BolgeSonuclari)
            {
                string ad = string.IsNullOrWhiteSpace(b.BolgeAdi) ? "(adsız)" : b.BolgeAdi;
                sb.AppendLine($"  {ad} (hedef {b.HedefKotM.ToString("0.##", Tr)} m): " +
                              $"yarma {M3(b.YarmaHacmiM3)}, dolgu {M3(b.DolguHacmiM3)}");
            }
        }

        if (sonuc.KapsamDisiHucreSayisi > 0)
            sb.AppendLine($"Uyarı: {sonuc.KapsamDisiHucreSayisi} hücre mevcut yüzey kapsamı dışında.");

        return sb.ToString();
    }
}
