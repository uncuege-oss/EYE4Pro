using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.BuildingAcoustics.Calculation;

public sealed record DuseyBirlesimSonucu(bool Basarili, string Mesaj, DuseyMekanBirlesimi? Birlesim);

/// <summary>
/// UX — Düşey birleşim (döşeme / tavan patlat) servisi.
/// Eski "iki kat aynı poligon" zinciri kuralı kaldırıldı. Yeni mantık:
///   • Tavan Patlat: seçili odanın TAVANI açılır → üst katta, bu odanın
///     centroid'ini içeren oda ile birleştirilir.
///   • Döşeme Patlat: seçili odanın DÖŞEMESİ açılır → alt katta, bu odanın
///     centroid'ini içeren oda ile birleştirilir.
/// Küçük oda + büyük oda yönlü çalışır (küçük → büyük yönü tipik).
/// </summary>
public sealed class DuseyMekanBirlesimServisi
{
    public const double CoordinateToleranceMeters = 0.02;

    /// UX — Seçili odanın tavanını üst kattaki (kapsayan) odaya bağlar.
    public DuseyBirlesimSonucu TavanPatlat(BinaProje proje, Kat seciliKat, Mekan seciliMekan) =>
        PatlatIcsel(proje, seciliKat, seciliMekan, asagiBak: false);

    /// UX — Seçili odanın döşemesini alt kattaki (kapsayan) odaya bağlar.
    public DuseyBirlesimSonucu DosemePatlat(BinaProje proje, Kat seciliKat, Mekan seciliMekan) =>
        PatlatIcsel(proje, seciliKat, seciliMekan, asagiBak: true);

    private DuseyBirlesimSonucu PatlatIcsel(BinaProje proje, Kat seciliKat, Mekan seciliMekan, bool asagiBak)
    {
        var hedefKat = KomsuKatBul(proje, seciliKat, asagiBak);
        if (hedefKat == null)
        {
            var yon = asagiBak ? "alt" : "üst";
            return new DuseyBirlesimSonucu(false, $"Komşu {yon} kat yok; patlatılamaz.", null);
        }

        var hedefMekan = KapsayanMekanBul(hedefKat, seciliKat, seciliMekan);
        if (hedefMekan == null)
        {
            var yon = asagiBak ? "alt" : "üst";
            return new DuseyBirlesimSonucu(false,
                $"{hedefKat.KatNo}. katta bu mekanın alanını kapsayan bir oda bulunamadı; {yon} hacim ile birleştirilemez.",
                null);
        }

        // Yıkıcı OLMAYAN birleşim: her iki oda kendi duvarlarıyla korunur (duvarlar poligon
        // kenarlarından türediği için silinmez). Üst oda listede gizlenir → "tek isim".
        // Atama panelinde hayatta kalan odanın altında iki odanın da duvarları birlikte gösterilir.
        var birlesim = proje.DuseyMekanBirlesimleri
            .FirstOrDefault(b => b.AktifMi &&
                                  b.Uyeler.Any(u => u.MekanId == seciliMekan.Id || u.MekanId == hedefMekan.Id));
        bool yeni = birlesim == null;
        birlesim ??= new DuseyMekanBirlesimi();

        // Seçili mekan: tavan/döşeme açıkla (yönüne göre)
        AddOrUpdateMember(birlesim, seciliKat.BinaGeometriKatId, seciliKat.KatNo, seciliMekan.Id,
            tavanAcik: !asagiBak ? true : (bool?)null,
            dosemeAcik: asagiBak ? true : (bool?)null);
        // Hedef mekan: ters yön
        AddOrUpdateMember(birlesim, hedefKat.BinaGeometriKatId, hedefKat.KatNo, hedefMekan.Id,
            tavanAcik: asagiBak ? true : (bool?)null,
            dosemeAcik: !asagiBak ? true : (bool?)null);

        birlesim.Uyeler = birlesim.Uyeler.OrderBy(u => u.KatNo).ToList();
        if (yeni) proje.DuseyMekanBirlesimleri.Add(birlesim);

        var yonStr = asagiBak ? "Döşeme" : "Tavan";
        return new DuseyBirlesimSonucu(true,
            $"{yonStr} patlatıldı: {seciliMekan.Ad} ↔ {hedefMekan.Ad} (odalar birleşti; duvarlar tek panelde atanabilir).",
            birlesim);
    }

    private static Kat? KomsuKatBul(BinaProje proje, Kat seciliKat, bool asagiBak)
    {
        // M50 bağlantılı projelerde koleksiyon fiziksel olarak üstten alta sıralıdır;
        // KatNo da bu görsel sırayı izler (üst=1). Yönü KatNo aritmetiğiyle tersine
        // çevirmek yerine kararlı M50 sırasından bul. Bağlantısız eski M08 projelerinde
        // tarihsel alt=küçük / üst=büyük KatNo kuralı geriye uyum için korunur.
        if (!string.IsNullOrWhiteSpace(seciliKat.BinaGeometriKatId))
        {
            var guncelM50Katlari = proje.Katlar
                .Where(k => !string.IsNullOrWhiteSpace(k.BinaGeometriKatId))
                .ToList();
            int index = guncelM50Katlari.IndexOf(seciliKat);
            int hedefIndex = asagiBak ? index + 1 : index - 1;
            return index >= 0 && hedefIndex >= 0 && hedefIndex < guncelM50Katlari.Count
                ? guncelM50Katlari[hedefIndex]
                : null;
        }

        return proje.Katlar.FirstOrDefault(k =>
            asagiBak ? k.KatNo == seciliKat.KatNo - 1 : k.KatNo == seciliKat.KatNo + 1);
    }

    private static void AddOrUpdateMember(DuseyMekanBirlesimi birlesim, string katId, int katNo, Guid mekanId,
        bool? tavanAcik = null, bool? dosemeAcik = null)
    {
        var uye = birlesim.Uyeler.FirstOrDefault(u =>
            (!string.IsNullOrWhiteSpace(katId) && !string.IsNullOrWhiteSpace(u.KatId)
                ? string.Equals(u.KatId, katId, StringComparison.Ordinal)
                : u.KatNo == katNo) && u.MekanId == mekanId);
        if (uye == null)
        {
            uye = new DuseyMekanBirlesimUyesi
            {
                KatId = katId,
                KatNo = katNo,
                MekanId = mekanId,
                TavanAcik = tavanAcik ?? false,
                DosemeAcik = dosemeAcik ?? false,
            };
            birlesim.Uyeler.Add(uye);
            return;
        }
        if (!string.IsNullOrWhiteSpace(katId)) uye.KatId = katId;
        uye.KatNo = katNo;
        if (tavanAcik.HasValue) uye.TavanAcik = uye.TavanAcik || tavanAcik.Value;
        if (dosemeAcik.HasValue) uye.DosemeAcik = uye.DosemeAcik || dosemeAcik.Value;
    }

    /// <summary>
    /// Bir mekanın düşey birleşimdeki DİĞER üye odalarını (partnerlerini) döndürür.
    /// Atama panelinde duvarları birleştirip göstermek için kullanılır.
    /// </summary>
    public static IEnumerable<Mekan> BirlesikPartnerMekanlar(BinaProje proje, Mekan mekan)
    {
        var b = BirlesimBul(proje, mekan.Id);
        if (b == null) yield break;
        foreach (var uye in b.Uyeler)
        {
            if (uye.MekanId == mekan.Id) continue;
            var kat = proje.Katlar.FirstOrDefault(k => k.KatNo == uye.KatNo);
            var m = kat?.Mekanlar.FirstOrDefault(x => x.Id == uye.MekanId);
            if (m != null) yield return m;
        }
    }

    /// <summary>
    /// Tüm aktif patlatmaları (yeniden→eskiye) snapshot'tan geri alır: etkilenen katların
    /// oda listelerini patlat öncesi haline döndürür ve birleşim kayıtlarını kaldırır.
    /// Proje açışında ve "patlatmayı geri al" akışında kullanılır.
    /// </summary>
    public static void TumPatlatmalariGeriAl(BinaProje proje)
    {
        var aktifler = proje.DuseyMekanBirlesimleri.Where(b => b.AktifMi).ToList();
        // Sonradan eklenen önce geri alınır (yığın çakışmasını önler)
        for (int i = aktifler.Count - 1; i >= 0; i--)
            GeriAlSnapshot(proje, aktifler[i]);
    }

    private static void GeriAlSnapshot(BinaProje proje, DuseyMekanBirlesimi birlesim)
    {
        var odaServisi = new Kst.Acoustics.Core.BuildingAcoustics.Parsers.OdaTespitServisi();
        if (birlesim.GeriAlmaUstKatMekanlari != null)
        {
            var ust = proje.Katlar.FirstOrDefault(k => k.KatNo == birlesim.UstKatNo);
            if (ust != null)
            {
                ust.Mekanlar = MekanKlonServisi.KlonlaListe(birlesim.GeriAlmaUstKatMekanlari);
                odaServisi.KomsuluklariTespitEt(ust.Mekanlar);
            }
        }
        if (birlesim.GeriAlmaAltKatMekanlari != null)
        {
            var alt = proje.Katlar.FirstOrDefault(k => k.KatNo == birlesim.AltKatNo);
            if (alt != null)
            {
                alt.Mekanlar = MekanKlonServisi.KlonlaListe(birlesim.GeriAlmaAltKatMekanlari);
                odaServisi.KomsuluklariTespitEt(alt.Mekanlar);
            }
        }
        proje.DuseyMekanBirlesimleri.Remove(birlesim);
    }

    /// UX — Patlatmayı geri alır: snapshot'tan etkilenen katların odalarını eski haline döndürür.
    /// Geçilen mekan, patlatmanın hayatta kalan (birleşik) odası olabilir ya da snapshot içindeki bir oda.
    public bool BirlesimdenCikar(BinaProje proje, Mekan mekan, out string mesaj)
    {
        var birlesim = proje.DuseyMekanBirlesimleri.FirstOrDefault(b => b.AktifMi &&
            (b.Uyeler.Any(u => u.MekanId == mekan.Id) ||
             (b.GeriAlmaUstKatMekanlari?.Any(m => m.Id == mekan.Id) ?? false) ||
             (b.GeriAlmaAltKatMekanlari?.Any(m => m.Id == mekan.Id) ?? false)));
        if (birlesim == null)
        {
            mesaj = "Seçili oda bir düşey patlatmaya bağlı değil.";
            return false;
        }

        GeriAlSnapshot(proje, birlesim);
        mesaj = "Patlatma geri alındı; üst kat odası ve ara döşeme eski haline döndürüldü.";
        return true;
    }

    /// İki oda birbirine bir aktif birleşim içinden bağlı mı (acoustic suppression için).
    public static bool AyniBirlesimdeAcik(BinaProje proje, int altKatNo, Guid altMekanId, int ustKatNo, Guid ustMekanId)
    {
        var altKatId = proje.Katlar.FirstOrDefault(k => k.KatNo == altKatNo)?.BinaGeometriKatId;
        var ustKatId = proje.Katlar.FirstOrDefault(k => k.KatNo == ustKatNo)?.BinaGeometriKatId;
        return proje.DuseyMekanBirlesimleri.Any(b =>
            b.AktifMi &&
            b.Uyeler.Any(u => KatEslesir(u, altKatId, altKatNo) &&
                              u.MekanId == altMekanId && u.TavanAcik) &&
            b.Uyeler.Any(u => KatEslesir(u, ustKatId, ustKatNo) &&
                              u.MekanId == ustMekanId && u.DosemeAcik));
    }

    private static bool KatEslesir(DuseyMekanBirlesimUyesi uye, string? katId, int katNo) =>
        !string.IsNullOrWhiteSpace(uye.KatId) && !string.IsNullOrWhiteSpace(katId)
            ? string.Equals(uye.KatId, katId, StringComparison.Ordinal)
            : uye.KatNo == katNo;

    public static DuseyMekanBirlesimi? BirlesimBul(BinaProje proje, Guid mekanId) =>
        proje.DuseyMekanBirlesimleri.FirstOrDefault(b => b.AktifMi && b.Uyeler.Any(u => u.MekanId == mekanId));

    /// UX — Tüm mekanların BirlesikHacimM3 değerini günceller (UI listesi etiket için).
    /// Aktif birleşimlerin üyelerine toplam hacim atanır; birleşime dahil olmayanlarda null bırakılır.
    /// Patlatma / kaldırma sonrası BinaProjesiViewModel tarafından çağrılır.
    public static void HacimleriGuncelle(BinaProje proje)
    {
        // Önce tüm mekanları sıfırla
        foreach (var kat in proje.Katlar)
            foreach (var m in kat.Mekanlar)
                m.BirlesikHacimM3 = null;

        // Aktif birleşimlerde toplam hacim hesapla ve üyelere yaz
        foreach (var b in proje.DuseyMekanBirlesimleri.Where(b => b.AktifMi))
        {
            var uyeMekanlar = new List<Mekan>();
            double toplam = 0;
            foreach (var uye in b.Uyeler)
            {
                var kat = proje.Katlar.FirstOrDefault(k => k.KatNo == uye.KatNo);
                var mekan = kat?.Mekanlar.FirstOrDefault(m => m.Id == uye.MekanId);
                if (mekan == null) continue;
                uyeMekanlar.Add(mekan);
                toplam += mekan.Hacim;
            }
            foreach (var m in uyeMekanlar)
                m.BirlesikHacimM3 = toplam;
        }
    }

    /// Belirli bir mekanın TAVAN yüzeyinin (üst kata bakan) açık olup olmadığını döner.
    public static bool TavanAcikMi(BinaProje proje, Mekan mekan)
    {
        var b = BirlesimBul(proje, mekan.Id);
        return b?.Uyeler.FirstOrDefault(u => u.MekanId == mekan.Id)?.TavanAcik == true;
    }

    /// Belirli bir mekanın DÖŞEME yüzeyinin (alt kata bakan) açık olup olmadığını döner.
    public static bool DosemeAcikMi(BinaProje proje, Mekan mekan)
    {
        var b = BirlesimBul(proje, mekan.Id);
        return b?.Uyeler.FirstOrDefault(u => u.MekanId == mekan.Id)?.DosemeAcik == true;
    }

    // ── Kapsama (containment) yardımcıları ───────────────────────────────

    /// <summary>
    /// hedefKat'taki mekanlar arasında, kucukMekan'ın centroid'ini (offset uygulanmış) içerenleri arar.
    /// Birden fazla aday varsa alanı en büyük olan döner (en uygun kapsayan).
    /// </summary>
    private static Mekan? KapsayanMekanBul(Kat hedefKat, Kat kucukKat, Mekan kucukMekan)
    {
        if (kucukMekan.Poligon == null || kucukMekan.Poligon.Count < 3) return null;

        // Centroid (world coord)
        double cx = kucukMekan.Poligon.Average(p => p.X) + kucukKat.OriginOffset.X;
        double cy = kucukMekan.Poligon.Average(p => p.Y) + kucukKat.OriginOffset.Y;
        var centroidWorld = new Point2D(cx, cy);

        Mekan? best = null;
        double bestAlan = -1;
        foreach (var aday in hedefKat.Mekanlar)
        {
            if (aday.Poligon == null || aday.Poligon.Count < 3) continue;
            var adayWorld = aday.Poligon
                .Select(p => new Point2D(p.X + hedefKat.OriginOffset.X, p.Y + hedefKat.OriginOffset.Y))
                .ToList();
            if (!PointInPolygon(centroidWorld, adayWorld)) continue;
            double alan = PolygonArea(adayWorld);
            if (alan > bestAlan)
            {
                bestAlan = alan;
                best = aday;
            }
        }
        return best;
    }

    private static bool PointInPolygon(Point2D p, IReadOnlyList<Point2D> poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var pi = poly[i]; var pj = poly[j];
            bool intersects = ((pi.Y > p.Y) != (pj.Y > p.Y)) &&
                              (p.X < (pj.X - pi.X) * (p.Y - pi.Y) / ((pj.Y - pi.Y) + double.Epsilon) + pi.X);
            if (intersects) inside = !inside;
        }
        return inside;
    }

    private static double PolygonArea(IReadOnlyList<Point2D> poly)
    {
        double area = 0;
        for (int i = 0; i < poly.Count; i++)
        {
            var j = (i + 1) % poly.Count;
            area += poly[i].X * poly[j].Y - poly[j].X * poly[i].Y;
        }
        return Math.Abs(area) / 2.0;
    }
}
