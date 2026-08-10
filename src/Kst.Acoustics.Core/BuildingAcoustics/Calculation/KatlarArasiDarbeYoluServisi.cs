using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.BuildingAcoustics.Calculation;

public sealed record KatlarArasiDarbeYolu(
    Kat KaynakKat,
    Mekan KaynakMekan,
    Kat AliciKat,
    Mekan AliciMekan,
    YapiElemani Doseme,
    double OrtusenAlan);

public sealed class KatlarArasiDarbeYoluServisi
{
    /// P1 düzeltmesi (Faz 3) — eskiden yalnız en fazla örtüşen TEK alt mekanı döndürüyordu.
    /// Bir üst oda iki (veya daha fazla) alt odaya taşıyorsa (ör. koridor üstündeki büyük
    /// salon iki küçük alt odaya bölünmüşse) ikinci/üçüncü alıcı tamamen atlanıyordu.
    /// Artık eşik üstü kalan TÜM alt mekanlar dönüyor (en büyük örtüşme önce).
    public IReadOnlyList<KatlarArasiDarbeYolu> BulAltMekanlar(
        IReadOnlyList<Kat> katlar,
        Kat kaynakKat,
        Mekan kaynakMekan,
        double minimumOrtakAlanM2 = 0.5)
    {
        if (kaynakMekan.Sinif == MekanSinifi.HesapDisi ||
            kaynakMekan.ZeminYapiElemani?.Tip != YapiElemaniTipi.Doseme || kaynakMekan.Poligon.Count < 3)
            return Array.Empty<KatlarArasiDarbeYolu>();

        Kat? aliciKat;
        if (!string.IsNullOrWhiteSpace(kaynakKat.BinaGeometriKatId))
        {
            // M50/M08 adapteri katları üstten alta aynı koleksiyon sırasında tutar;
            // üstteki kaynak döşemesinin alıcısı listedeki bir sonraki (alt) kattır.
            var guncelM50Katlari = katlar
                .Where(k => !string.IsNullOrWhiteSpace(k.BinaGeometriKatId))
                .ToList();
            int kaynakIndex = -1;
            for (int i = 0; i < guncelM50Katlari.Count; i++)
            {
                if (!ReferenceEquals(guncelM50Katlari[i], kaynakKat) &&
                    !string.Equals(guncelM50Katlari[i].BinaGeometriKatId, kaynakKat.BinaGeometriKatId,
                        StringComparison.Ordinal)) continue;
                kaynakIndex = i;
                break;
            }
            aliciKat = kaynakIndex >= 0 && kaynakIndex + 1 < guncelM50Katlari.Count
                ? guncelM50Katlari[kaynakIndex + 1]
                : null;
        }
        else
        {
            // Bağlantısız eski M08 projelerinde tarihsel konvansiyon: alt katın
            // KatNo değeri daha küçüktür.
            aliciKat = katlar
                .Where(k => k.KatNo < kaynakKat.KatNo)
                .OrderByDescending(k => k.KatNo)
                .FirstOrDefault();
        }

        if (aliciKat == null)
            return Array.Empty<KatlarArasiDarbeYolu>();

        var kaynakPoly = OffsetPoligon(kaynakMekan.Poligon, kaynakKat.OriginOffset);

        var sonuclar = new List<KatlarArasiDarbeYolu>();
        foreach (var aday in aliciKat.Mekanlar)
        {
            // Balkon/açık teras gibi Hesap Dışı alıcılar yönetmelik darbe
            // değerlendirmesine ve bina performans sınıfına katılmaz.
            if (aday.Sinif == MekanSinifi.HesapDisi || aday.Poligon.Count < 3)
                continue;

            var adayPoly = OffsetPoligon(aday.Poligon, aliciKat.OriginOffset);
            var alan = OrtusenAlan(kaynakPoly, adayPoly);
            if (alan >= minimumOrtakAlanM2)
            {
                sonuclar.Add(new KatlarArasiDarbeYolu(
                    kaynakKat, kaynakMekan, aliciKat, aday, kaynakMekan.ZeminYapiElemani, alan));
            }
        }

        return sonuclar.OrderByDescending(y => y.OrtusenAlan).ToList();
    }

    /// Geriye dönük uyum — yalnız en fazla örtüşen (varsa) tek alt mekanı döner.
    public KatlarArasiDarbeYolu? BulAltMekan(
        IReadOnlyList<Kat> katlar,
        Kat kaynakKat,
        Mekan kaynakMekan,
        double minimumOrtakAlanM2 = 0.5)
        => BulAltMekanlar(katlar, kaynakKat, kaynakMekan, minimumOrtakAlanM2).FirstOrDefault();

    /// Darbe yolu için DETERMİNİSTİK sentetik bölme kimliği. Darbe değerlendirmesi geçici
    /// (mekan.Bolmeler'de yaşamayan) Bolme nesneleri üzerinden yapılır; "Rapora Dahil"
    /// filtresi ve rapor yeniden üretimi arasında aynı yolun aynı Id'yi alması için Id
    /// kaynak+alıcı mekan Guid'lerinden türetilir (rastgele Guid.NewGuid yerine).
    public static Guid DarbeYoluId(Guid kaynakMekanId, Guid aliciMekanId)
    {
        var a = kaynakMekanId.ToByteArray();
        var b = aliciMekanId.ToByteArray();
        var c = new byte[16];
        for (int i = 0; i < 16; i++)
            c[i] = (byte)(a[i] ^ b[(i + 7) % 16] ^ 0x5A);
        return new Guid(c);
    }

    /// Projedeki tüm katlar arası darbe yollarını bulup her biri için HESAPLANMIŞ
    /// (ApparentLnw/ApparentLnTw dolu) sentetik döşeme bölmesi üretir. ViewModel'in
    /// Hesapla() akışıyla aynı mantık — rapor servisleri projeden bağımsız olarak
    /// aynı darbe satırlarını üretebilsin diye Core'da paylaşılır. Aktif düşey
    /// birleşimle bağlı (patlatılmış) mekan çiftleri değerlendirilmez.
    public IReadOnlyList<Bolme> DegerlendirilmisDarbeBolmeleri(BinaProje proje)
    {
        var darbeServisi = new DarbeSesiHesapServisi();
        var sonuclar = new List<Bolme>();

        foreach (var kat in proje.Katlar)
        {
            foreach (var mekan in kat.Mekanlar)
            {
                if (mekan.Sinif == MekanSinifi.HesapDisi)
                    continue;

                foreach (var yol in BulAltMekanlar(proje.Katlar, kat, mekan))
                {
                    if (DuseyMekanBirlesimServisi.AyniBirlesimdeAcik(
                            proje,
                            yol.AliciKat.KatNo, yol.AliciMekan.Id,
                            yol.KaynakKat.KatNo, yol.KaynakMekan.Id))
                        continue;

                    var dosemeBolmesi = new Bolme
                    {
                        Id = DarbeYoluId(yol.KaynakMekan.Id, yol.AliciMekan.Id),
                        Ad = $"Döşeme ({yol.KaynakMekan.Ad} → {yol.AliciMekan.Ad})",
                        Alan = Math.Max(yol.OrtusenAlan, 0.01),
                        YapiElemani = yol.Doseme,
                        KaynakMekan = yol.KaynakMekan,
                        AliciMekan = yol.AliciMekan
                    };
                    dosemeBolmesi.ApparentLnw = darbeServisi.HesaplaApparentLnw(dosemeBolmesi);
                    dosemeBolmesi.ApparentLnTw = darbeServisi.HesaplaApparentLnTw(dosemeBolmesi);
                    sonuclar.Add(dosemeBolmesi);
                }
            }
        }

        return sonuclar;
    }

    private static List<Point2D> OffsetPoligon(List<Point2D> poligon, Point2D offset) =>
        poligon.Select(p => new Point2D(p.X + offset.X, p.Y + offset.Y)).ToList();

    /// P1 düzeltmesi (Faz 3) — eskiden dikdörtgen bounding-box kesişimi kullanılıyordu; L, U,
    /// eğik veya konkav odalarda gerçekte çakışmayan mekanlar arasında yanlışlıkla darbe yolu
    /// üretilebiliyordu. Artık gerçek poligon kesişimi (Clipper2) kullanılıyor.
    private static double OrtusenAlan(IReadOnlyList<Point2D> a, IReadOnlyList<Point2D> b)
    {
        var kesisim = ClipperAdaptor.Kesisim(new[] { a }, new[] { b });
        return ClipperAdaptor.NetAlan(kesisim);
    }
}
