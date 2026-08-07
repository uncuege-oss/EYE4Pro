using System;
using Kst.Acoustics.Core.BuildingAcoustics.Models;

namespace Kst.Acoustics.Core.BuildingAcoustics.Calculation;

/// <summary>
/// Yönetmelik EK-5 — tesisat ve servis ekipmanlarından kaynaklanan iç gürültü sınırları.
///
/// Tablo 5.1 (sürekli gürültülü ekipman, L_Aeq,nT): kaynak metinde yalnız 6 bina işlevi
/// için verilmiştir (Konut, Eğitim, Sağlık, Büro, Konaklama, Yurt) ve her satır Tablo 4.1'in
/// karşılık satırından tam 4 dB düşüktür (docx ile satır satır doğrulandı, 2026-07-17).
/// Bu yüzden Tablo 5.1 = EK4 satırı − 4 dB türetimiyle kodlanır; tabloda yer almayan
/// işlevlerde null döner ("tablo kapsamı dışı" — uydurma değer üretilmez).
///
/// Tablo 5.2 (kesikli/diğer ekipman, L_AF,max,nT): işlev/mekandan bağımsız tek satır —
/// A=26, B=30, C=34, D=38, E=42, F=46.
/// </summary>
public static class EK5TesisatServisi
{
    public sealed record TesisatLimiti(string SatirAdi, double LimitDb, string Birim);

    /// Tablo 5.1'in kapsadığı bina işlevleri (kaynak metinde yalnız bunlar var).
    private static bool Tablo51Kapsaminda(BinaIslevi islev) => islev is
        BinaIslevi.Konut or BinaIslevi.EgitimTesisleri or BinaIslevi.SaglikVeYasliEvleri or
        BinaIslevi.BuroIdari or BinaIslevi.Konaklama or BinaIslevi.Yurt;

    /// Tablo 5.1 — sürekli servis ekipmanı sınırı (L_Aeq,nT). Kapsam dışı işlev veya
    /// eşleşmeyen mekan için null.
    public static TesisatLimiti? SurekliLimit(
        BinaIslevi islev, AkustikPerformansSinifi sinif, string? mekanAdi, string? kullanimTipi)
    {
        if (!Tablo51Kapsaminda(islev))
            return null;

        var satir = EK4IcGurultuServisi.SatirBul(islev, mekanAdi, kullanimTipi);
        if (satir == null)
            return null;

        var limit = EK4IcGurultuServisi.SinifIcinDeger(satir.CDegeriDb, sinif) - 4.0;
        return new TesisatLimiti($"Tablo 5.1: {satir.Ad} ({satir.ZamanDilimi})", limit, "dBA L_Aeq,nT");
    }

    /// Tablo 5.2 — kesikli/diğer servis ekipmanı sınırı (L_AF,max,nT); mekandan bağımsız.
    public static TesisatLimiti KesikliLimit(AkustikPerformansSinifi sinif)
    {
        var limit = EK4IcGurultuServisi.SinifIcinDeger(34.0, sinif);
        return new TesisatLimiti("Tablo 5.2: kesikli gürültülü servis ekipmanları", limit, "dBA L_AF,max,nT");
    }

    public sealed record TesisatSonucu(
        string SatirAdi, double GirilenDb, double LimitDb, string Birim, UyumDurumu Durum)
    {
        public double MarjDb => LimitDb - GirilenDb; // pozitif = uygun
    }

    /// Mekanın beyan edilen tesisat düzeylerini EK-5 sınırlarıyla karşılaştırır.
    /// Girilmeyen (0) düzeyler değerlendirilmez; sürekli düzey girilmiş ama Tablo 5.1
    /// kapsam dışıysa VeriEksik döner (sessiz varsayılan yok — codex kuralı).
    public static (TesisatSonucu? Surekli, TesisatSonucu? Kesikli) Degerlendir(
        BinaProje proje, Mekan mekan)
    {
        TesisatSonucu? surekli = null;
        if (mekan.TesisatSurekliDb > 0)
        {
            var limit = SurekliLimit(proje.BinaIslevi, proje.AkustikPerformansSinifi, mekan.Ad, mekan.KullanimTipi);
            surekli = limit == null
                ? new TesisatSonucu("Tablo 5.1: eşleşen satır yok", mekan.TesisatSurekliDb, 0, "dBA L_Aeq,nT", UyumDurumu.VeriEksik)
                : new TesisatSonucu(limit.SatirAdi, mekan.TesisatSurekliDb, limit.LimitDb, limit.Birim,
                    mekan.TesisatSurekliDb <= limit.LimitDb ? UyumDurumu.Gecti : UyumDurumu.Kaldi);
        }

        TesisatSonucu? kesikli = null;
        if (mekan.TesisatKesikliDb > 0)
        {
            var limit = KesikliLimit(proje.AkustikPerformansSinifi);
            kesikli = new TesisatSonucu(limit.SatirAdi, mekan.TesisatKesikliDb, limit.LimitDb, limit.Birim,
                mekan.TesisatKesikliDb <= limit.LimitDb ? UyumDurumu.Gecti : UyumDurumu.Kaldi);
        }

        return (surekli, kesikli);
    }
}
