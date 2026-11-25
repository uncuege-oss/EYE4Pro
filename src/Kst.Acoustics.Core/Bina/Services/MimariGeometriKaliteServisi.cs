using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Bina.Services;

/// İçe aktarımdaki şüpheli mahal sınırlarını ve yanlış çatı hacimlerini görünür
/// kılar. Oda adaylarını silmez, sınır veya ölçü uydurmaz; kaynak çizim korunur.
public static class MimariGeometriKaliteServisi
{
    public static IReadOnlyList<string> Uygula(BinaKat kat)
    {
        var notlar = new List<string>();
        var etiketler = kat.HamMetinler
            .Select(t => (t.Pos, Etiket: MimariOdaEtiketServisi.Degerlendir(t.Value)))
            .Where(t => t.Etiket is { Puan: >= 100 })
            .Select(t => (t.Pos, Ad: t.Etiket!.Ad)).ToList();
        bool zenginEtiketli = etiketler.Count >= 5;
        bool cati = Regex.IsMatch(Normalize(kat.Ad), @"\bCATI\b|\bTERAS\b");
        int adsiz = 0, notIsimli = 0, gecersiz = 0, cokEtiketli = 0, kapaliMahal = 0;
        foreach (var oda in kat.Odalar)
        {
            if (oda.Poligon.Count < 3 || oda.Poligon.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y)))
            {
                gecersiz++;
                notlar.Add($"'{oda.Ad}': geçerli kapalı oda sınırı yok; geometri kontrol edilmeli.");
                continue;
            }
            double alan = PoligonYardimci.Alan(oda.Poligon);
            if (!double.IsFinite(alan) || alan < 0.5 || alan > 5000 || !double.IsFinite(oda.AlanM2) ||
                Math.Abs(alan - oda.AlanM2) > Math.Max(0.5, alan * 0.05))
            {
                gecersiz++;
                notlar.Add($"'{oda.Ad}': alan/sınır tutarlılığı kontrol edilmeli (sınır alanı {alan:0.##} m², kayıt {oda.AlanM2:0.##} m²).");
            }
            var icEtiketler = etiketler.Where(t => PoligonYardimci.IsPointInside(t.Pos, oda.Poligon)).ToList();
            if (icEtiketler.Any(t => KapaliMahalAdiMi(t.Ad))) kapaliMahal++;
            if (icEtiketler.Count == 0 && zenginEtiketli) adsiz++;
            if (MimariOdaEtiketServisi.Degerlendir(oda.Ad) is null && !Regex.IsMatch(oda.Ad, @"^Oda\s*\d*$", RegexOptions.IgnoreCase))
                notIsimli++;

            var farkli = icEtiketler.GroupBy(t => Normalize(t.Ad)).Select(g => g.First()).ToList();
            // Tek bağlı hacimde birden fazla kullanım adı olabilir. Ham metinler
            // kayıttan sonra bulunmasa da adlar kaybolmasın; geometriyi adlara
            // göre keyfi olarak bölmeyelim.
            if (kat.HamMetinler.Count > 0)
                oda.KaynakMahalAdlari = farkli.Select(t => t.Ad).ToList();
            if (farkli.Count >= 2 && farkli.Any(a => farkli.Any(b => a.Ad != b.Ad && Mesafe(a.Pos, b.Pos) > 0.75)))
            {
                cokEtiketli++;
                notlar.Add($"'{oda.Ad}': aynı sınır içinde birden fazla mahal adı var ({string.Join(", ", farkli.Take(5).Select(t => t.Ad))}). Ayırıcı duvar/kapı sınırı doğrulanamadı; açık bağlantılı kullanım alanı olabilir. Kaynak adlar birlikte korundu.");
            }
        }
        if (adsiz > 0)
            notlar.Add($"{adsiz} oda adayında mahal etiketi bulunamadı; bu çizimde diğer mahaller adlandırılmış. Tarama, tefriş veya birleşmiş boşluk sınırları kontrol edilmeli.");
        if (notIsimli > 0)
            notlar.Add($"{notIsimli} oda adayının adı mahal adı yerine çizim notu/imalat kodu olabilir; ad ve sınır kontrol edilmeli.");
        if (cati && kat.Odalar.Count > 0 && kapaliMahal == 0)
        {
            // Çatı taramasındaki kapalı boşluklar oda kanıtı değildir. Mevcut
            // görünürlük seçeneğiyle hacimleri kapatmak geri alınabilir; kaynak
            // DXF ve bütün oda adayları düzenleme için kalmaya devam eder.
            kat.BloklariGoster = false;
            notlar.Add($"Çatı/teras çizimindeki {kat.Odalar.Count} alan adayında kapalı mahal etiketi bulunamadı. Çatı taramasını oda hacmine dönüştürmemek için 3D oda blokları kapatıldı; kaynak çizim ve adaylar korundu. Kontrolden sonra 'Blokları göster' ile açılabilir.");
        }
        if (gecersiz + cokEtiketli + adsiz + notIsimli > 0)
            notlar.Add("Otomatik geometri kontrol gerektiriyor; uyarılar giderilmeden mahal alanları ve hacimleri kesin sonuç olarak kullanılmamalı.");
        return notlar;
    }

    private static bool KapaliMahalAdiMi(string ad)
        => MimariOdaEtiketServisi.ContainsRoomName(ad) && !Regex.IsMatch(Normalize(ad), @"\b(?:TERAS(?:I)?|BALKON(?:U)?|CATI|BAHCE(?:SI)?|AVLU|OTURUM|OLUK)\b");

    private static double Mesafe(Point2D a, Point2D b)
        => Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));

    private static string Normalize(string text)
        => Regex.Replace(text.ToUpperInvariant().Replace('ı', 'I').Replace('İ','I').Replace('Ç','C').Replace('Ş','S').Replace('Ğ','G').Replace('Ö','O').Replace('Ü','U'), @"\s+", " ").Trim();
}
