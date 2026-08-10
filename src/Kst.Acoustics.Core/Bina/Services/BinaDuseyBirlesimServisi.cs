using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Bina.Services;

public sealed record BinaPatlatmaSonucu(bool Basarili, string Mesaj, BinaDuseyBirlesim? Birlesim);

/// <summary>
/// M50 — Döşeme/tavan patlatma servisi. M08'deki DuseyMekanBirlesimServisi'nin
/// BinaGeometri modeline uyarlanmış hali.
///
/// Birleşim YIKICI DEĞİLDİR: oda poligonları değişmez, yalnız birleşim kaydı tutulur.
/// Modüller (M08) bu kayda bakarak ara döşemeyi/tavanı bastırır ve hacimleri toplar.
///
/// Kilit kararı: patlatma üye odaların geometrisini KİLİTLEMEZ — kullanıcı köşe
/// taşıyabilir. Ancak üye oda silinirse birleşim askıda kalmasın diye OdaSilindi()
/// çağrısıyla ilgili birleşimler otomatik kaldırılır (Id kararlılığı korunur:
/// birleşim üyeleri yalnız OdaId üzerinden bağlanır, Id'ler asla yeniden üretilmez).
/// </summary>
public static class BinaDuseyBirlesimServisi
{
    /// Seçili odanın TAVANINI üst kattaki (centroid'i kapsayan) odaya bağlar.
    public static BinaPatlatmaSonucu TavanPatlat(BinaGeometri geo, BinaKat kat, BinaOda oda) =>
        Patlat(geo, kat, oda, asagiBak: false);

    /// Seçili odanın DÖŞEMESİNİ alt kattaki (centroid'i kapsayan) odaya bağlar.
    public static BinaPatlatmaSonucu DosemePatlat(BinaGeometri geo, BinaKat kat, BinaOda oda) =>
        Patlat(geo, kat, oda, asagiBak: true);

    private static BinaPatlatmaSonucu Patlat(BinaGeometri geo, BinaKat kat, BinaOda oda, bool asagiBak)
    {
        int katIndex = geo.Katlar.IndexOf(kat);
        int hedefIndex = asagiBak ? katIndex + 1 : katIndex - 1;
        var hedefKat = katIndex >= 0 && hedefIndex >= 0 && hedefIndex < geo.Katlar.Count
            ? geo.Katlar[hedefIndex]
            : null;
        if (hedefKat is null)
        {
            string yon = asagiBak ? "alt" : "üst";
            return new BinaPatlatmaSonucu(false, $"Komşu {yon} kat yok; patlatılamaz.", null);
        }

        var hedefOda = KapsayanOdaBul(hedefKat, kat, oda);
        if (hedefOda is null)
        {
            string yon = asagiBak ? "alt" : "üst";
            return new BinaPatlatmaSonucu(false,
                $"'{hedefKat.Ad}' katında bu odanın alanını kapsayan oda bulunamadı; {yon} hacim ile birleştirilemez.",
                null);
        }

        // Var olan aktif birleşime katıl (zincirleme patlatma), yoksa yeni oluştur.
        // Kaynak ve hedef oda FARKLI iki aktif birleşimin üyesiyse (iki ayrı zincir
        // burada buluşuyor), ikisini TEK birleşimde toplamak gerekir — aksi halde
        // hedef oda iki aktif birleşimde birden kalır (belirsiz TavanAcik/DosemeAcik).
        var birlesimOda = geo.DuseyBirlesimleri.FirstOrDefault(b => b.AktifMi &&
            b.Uyeler.Any(u => u.OdaId == oda.Id));
        var birlesimHedef = geo.DuseyBirlesimleri.FirstOrDefault(b => b.AktifMi &&
            b.Uyeler.Any(u => u.OdaId == hedefOda.Id));

        BinaDuseyBirlesim birlesim;
        bool yeni;
        if (birlesimOda is not null && birlesimHedef is not null && !ReferenceEquals(birlesimOda, birlesimHedef))
        {
            foreach (var u in birlesimHedef.Uyeler)
                if (!birlesimOda.Uyeler.Any(x => x.OdaId == u.OdaId))
                    birlesimOda.Uyeler.Add(u);
            geo.DuseyBirlesimleri.Remove(birlesimHedef);
            birlesim = birlesimOda;
            yeni = false;
        }
        else
        {
            birlesim = birlesimOda ?? birlesimHedef ?? new BinaDuseyBirlesim();
            yeni = birlesimOda is null && birlesimHedef is null;
        }

        UyeEkleVeyaGuncelle(birlesim, kat, oda.Id,
            tavanAcik: !asagiBak ? true : null,
            dosemeAcik: asagiBak ? true : null);
        UyeEkleVeyaGuncelle(birlesim, hedefKat, hedefOda.Id,
            tavanAcik: asagiBak ? true : null,
            dosemeAcik: !asagiBak ? true : null);

        birlesim.Uyeler = birlesim.Uyeler
            .OrderBy(u => KatListeIndex(geo, u))
            .ToList();
        if (yeni) geo.DuseyBirlesimleri.Add(birlesim);
        UstAltKatNoyuHesapla(geo, birlesim);

        string yonStr = asagiBak ? "Döşeme" : "Tavan";
        return new BinaPatlatmaSonucu(true,
            $"{yonStr} patlatıldı: {oda.Ad} ↔ {hedefOda.Ad} (düşey hacim birleşti).",
            birlesim);
    }

    private static void UyeEkleVeyaGuncelle(BinaDuseyBirlesim birlesim, BinaKat kat, string odaId,
        bool? tavanAcik = null, bool? dosemeAcik = null)
    {
        // OdaId global benzersiz (bir oda tek kata aittir) → kimlik olarak OdaId yeter.
        var uye = birlesim.Uyeler.FirstOrDefault(u => u.OdaId == odaId);
        if (uye is null)
        {
            birlesim.Uyeler.Add(new BinaBirlesimUyesi
            {
                KatId = kat.Id,
                KatNo = kat.KatNo,
                OdaId = odaId,
                TavanAcik = tavanAcik ?? false,
                DosemeAcik = dosemeAcik ?? false,
            });
            return;
        }
        uye.KatId = kat.Id;
        uye.KatNo = kat.KatNo;
        if (tavanAcik.HasValue)  uye.TavanAcik  = uye.TavanAcik  || tavanAcik.Value;
        if (dosemeAcik.HasValue) uye.DosemeAcik = uye.DosemeAcik || dosemeAcik.Value;
    }

    /// Üyenin katını KARARLI KatId ile bulur; eski/boş KatId'de KatNo'ya düşer.
    private static BinaKat? KatBul(BinaGeometri geo, BinaBirlesimUyesi uye)
    {
        if (!string.IsNullOrEmpty(uye.KatId))
        {
            var k = geo.Katlar.FirstOrDefault(x => x.Id == uye.KatId);
            if (k is not null) return k;
        }
        return geo.Katlar.FirstOrDefault(x => x.KatNo == uye.KatNo);
    }

    private static int KatListeIndex(BinaGeometri geo, BinaBirlesimUyesi uye)
    {
        var kat = KatBul(geo, uye);
        return kat is null ? -1 : geo.Katlar.IndexOf(kat);
    }

    private static void UstAltKatNoyuHesapla(BinaGeometri geo, BinaDuseyBirlesim birlesim)
    {
        var sirali = birlesim.Uyeler
            .Select(u => (Uye: u, Index: KatListeIndex(geo, u)))
            .Where(x => x.Index >= 0)
            .ToList();
        if (sirali.Count > 0)
        {
            birlesim.UstKatNo = sirali.MinBy(x => x.Index).Uye.KatNo;
            birlesim.AltKatNo = sirali.MaxBy(x => x.Index).Uye.KatNo;
        }
        else if (birlesim.Uyeler.Count > 0)
        {
            birlesim.AltKatNo = birlesim.Uyeler.Min(u => u.KatNo);
            birlesim.UstKatNo = birlesim.Uyeler.Max(u => u.KatNo);
        }
    }

    /// Kat sıralaması/numarası değişince çağrılır: her üyenin KatNo'sunu kararlı
    /// KatId'sinden tazeler ve birleşimlerin Üst/Alt KatNo'sunu yeniden hesaplar.
    /// Böylece düşey birleşimler reorder/rename sonrası doğru katları gösterir.
    public static void KatNolariYenile(BinaGeometri geo)
    {
        foreach (var b in geo.DuseyBirlesimleri)
        {
            foreach (var u in b.Uyeler)
            {
                if (string.IsNullOrEmpty(u.KatId)) continue;
                var kat = geo.Katlar.FirstOrDefault(k => k.Id == u.KatId);
                if (kat is not null) u.KatNo = kat.KatNo;
            }
            UstAltKatNoyuHesapla(geo, b);
        }
    }

    /// Migrasyon: KatId'si boş üyeleri (eski `.e4bina` dosyaları) mevcut KatNo
    /// eşlemesinden doldurur. Bir kez çalıştırılır, sonrası KatId kararlıdır.
    public static void KatIdleriTamamla(BinaGeometri geo)
    {
        foreach (var b in geo.DuseyBirlesimleri)
            foreach (var u in b.Uyeler)
                if (string.IsNullOrEmpty(u.KatId))
                {
                    var kat = geo.Katlar.FirstOrDefault(k => k.KatNo == u.KatNo);
                    if (kat is not null) u.KatId = kat.Id;
                }
    }

    /// Odanın bağlı olduğu birleşimi kaldırır (patlatmayı geri al).
    public static bool GeriAl(BinaGeometri geo, string odaId, out string mesaj)
    {
        var birlesim = BirlesimBul(geo, odaId);
        if (birlesim is null)
        {
            mesaj = "Seçili oda bir düşey patlatmaya bağlı değil.";
            return false;
        }
        geo.DuseyBirlesimleri.Remove(birlesim);
        mesaj = "Patlatma geri alındı; ara döşeme/tavan yeniden kapalı sayılır.";
        return true;
    }

    /// Oda silindiğinde askıda birleşim kalmasın: odayı içeren tüm birleşimleri kaldırır.
    /// Kaldırılan birleşim sayısını döner.
    public static int OdaSilindi(BinaGeometri geo, string odaId)
    {
        var iliskili = geo.DuseyBirlesimleri
            .Where(b => b.Uyeler.Any(u => u.OdaId == odaId))
            .ToList();
        foreach (var b in iliskili)
            geo.DuseyBirlesimleri.Remove(b);
        return iliskili.Count;
    }

    public static BinaDuseyBirlesim? BirlesimBul(BinaGeometri geo, string odaId) =>
        geo.DuseyBirlesimleri.FirstOrDefault(b => b.AktifMi && b.Uyeler.Any(u => u.OdaId == odaId));

    /// Odanın TAVAN yüzeyi (üst kata bakan) birleşim gereği açık mı?
    public static bool TavanAcikMi(BinaGeometri geo, string odaId) =>
        BirlesimBul(geo, odaId)?.Uyeler.FirstOrDefault(u => u.OdaId == odaId)?.TavanAcik == true;

    /// Odanın DÖŞEME yüzeyi (alt kata bakan) birleşim gereği açık mı?
    public static bool DosemeAcikMi(BinaGeometri geo, string odaId) =>
        BirlesimBul(geo, odaId)?.Uyeler.FirstOrDefault(u => u.OdaId == odaId)?.DosemeAcik == true;

    /// İki oda aynı aktif birleşimde VE alt odanın tavanı + üst odanın döşemesi
    /// açık mı (akustik bastırma kararları için)? Yalnız "aynı zincirde olmak"
    /// yetmez — zincirleme birleşimde araya giren, birbirine doğrudan komşu
    /// OLMAYAN odalar da yanlışlıkla "açık" sayılmasın diye bayraklar denetlenir.
    /// OdaId global benzersiz olduğundan eşleşme yalnız OdaId üzerinden yapılır;
    /// KatNo parametreleri geriye dönük imza uyumu için korunur.
    public static bool AyniBirlesimdeAcik(BinaGeometri geo,
        int altKatNo, string altOdaId, int ustKatNo, string ustOdaId)
    {
        return geo.DuseyBirlesimleri.Any(b =>
            b.AktifMi &&
            b.Uyeler.Any(u => u.OdaId == altOdaId && u.TavanAcik) &&
            b.Uyeler.Any(u => u.OdaId == ustOdaId && u.DosemeAcik));
    }

    /// Birleşik toplam hacim (m³): üye odaların alan × etkin yükseklik toplamı.
    public static double BirlesikHacimM3(BinaGeometri geo, BinaDuseyBirlesim birlesim)
    {
        double toplam = 0;
        foreach (var uye in birlesim.Uyeler)
        {
            var kat = KatBul(geo, uye);
            var oda = kat?.Odalar.FirstOrDefault(o => o.Id == uye.OdaId);
            if (kat is null || oda is null) continue;
            toplam += oda.AlanM2 * (oda.YukseklikOverrideM ?? kat.KatYuksekligiM);
        }
        return toplam;
    }

    // ── Kapsama yardımcıları ──────────────────────────────────────────────────

    /// hedefKat'ta, kucukOda'nın centroid'ini (offset uygulanmış dünya koordinatında)
    /// içeren odalardan alanı en büyük olanı döner.
    private static BinaOda? KapsayanOdaBul(BinaKat hedefKat, BinaKat kucukKat, BinaOda kucukOda)
    {
        if (kucukOda.Poligon.Count < 3) return null;

        var icNokta = kucukOda.Merkez;
        var centroidWorld = new Point2D(
            icNokta.X + kucukKat.OriginOffset.X,
            icNokta.Y + kucukKat.OriginOffset.Y);

        BinaOda? best = null;
        double bestAlan = -1;
        foreach (var aday in hedefKat.Odalar)
        {
            if (aday.Poligon.Count < 3) continue;
            var adayWorld = aday.Poligon
                .Select(p => new Point2D(p.X + hedefKat.OriginOffset.X, p.Y + hedefKat.OriginOffset.Y))
                .ToList();
            if (!NoktaPoligonIcinde(centroidWorld, adayWorld)) continue;
            double alan = PoligonAlan(adayWorld);
            if (alan > bestAlan)
            {
                bestAlan = alan;
                best = aday;
            }
        }
        return best;
    }

    private static bool NoktaPoligonIcinde(Point2D p, IReadOnlyList<Point2D> poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var pi = poly[i]; var pj = poly[j];
            if ((pi.Y > p.Y) != (pj.Y > p.Y) &&
                p.X < (pj.X - pi.X) * (p.Y - pi.Y) / ((pj.Y - pi.Y) + double.Epsilon) + pi.X)
                inside = !inside;
        }
        return inside;
    }

    private static double PoligonAlan(IReadOnlyList<Point2D> poly)
    {
        double area = 0;
        for (int i = 0; i < poly.Count; i++)
        {
            int j = (i + 1) % poly.Count;
            area += poly[i].X * poly[j].Y - poly[j].X * poly[i].Y;
        }
        return Math.Abs(area) / 2.0;
    }
}
