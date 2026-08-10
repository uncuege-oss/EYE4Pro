using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Salon.Models;

namespace Kst.Acoustics.Core.Salon.Services;

/// M51 salon hava hacmi (m³) hesabı — M01 RoomVolume (Sabine V) girdisi.
/// Sınırlayıcı kutu yerine gerçek plan geometrisi kullanılır:
///   V = kabuk plan alanı (kapalı Zemin poligonlarının union'ı) × salon yüksekliği
///       − sahne platform hacmi − seyirci teras katı hacmi − balkon plak hacmi
/// Düşümler yalnız kabuk içinde kalan footprint oranıyla uygulanır (Clipper2 kesişim).
/// Kapalı zemin poligonu yoksa sınırlayıcı kutuya geri düşülür (eski davranış).
public static class SalonHacimServisi
{
    public static double Hesapla(SalonGeometri salon)
    {
        ArgumentNullException.ThrowIfNull(salon);
        double h = salon.SalonYuksekligiM > 0 ? salon.SalonYuksekligiM : 3.0;

        var kabukPoligonlari = KabukPoligonlari(salon);
        if (kabukPoligonlari.Count == 0)
            return BboxHacim(salon, h);

        var kabuk = ClipperAdaptor.Birlestir(kabukPoligonlari);
        double alan = ClipperAdaptor.NetAlan(kabuk);
        if (alan < 1e-9) return BboxHacim(salon, h);

        // Tavan hacmi: düz tavanda alan × h; profilli tavanda ∫ z(y) dA (bant bazlı,
        // her bantta z = a + b·y doğrusal → integral = a·A + b·∫y dA).
        double v = SalonTavanServisi.ProfilliMi(salon) ? ProfilHacmi(salon, kabuk) : alan * h;
        // Sahne platformu: kabuk içindeki alan × platform yüksekliği (salon içinde kalan kısım).
        if (salon.Sahne is { } sahne && sahne.YukseklikM > 0)
        {
            v -= YatayKatiHacmi(salon, kabuk,
                sahne.TabanPoligonu(Salon3DGorunumServisi.Faset(salon)),
                sahne.TabanKotuM, sahne.TabanKotuM + sahne.YukseklikM);
        }

        // Kademeli seyirci: teraslar dolu (katı) kabul edilir.
        // Katı hacim = Σ basamakAlan × basamakKotu; kabuk içinde kalan oranla düşülür.
        foreach (var sek in salon.SeyirciSekciyonlari)
        {
            int n = Math.Max(1, sek.KademeSayisi);
            double kati = 0;
            double xL = sek.MerkezX - sek.GenislikM / 2;
            double xR = sek.MerkezX + sek.GenislikM / 2;
            double yOn = sek.TersYon
                ? sek.MerkezY + sek.DerinlikM / 2
                : sek.MerkezY - sek.DerinlikM / 2;
            double yon = sek.TersYon ? -1.0 : 1.0;
            double kademeDerinligi = sek.DerinlikM / n;
            for (int i = 0; i < n; i++)
            {
                double y1 = yOn + yon * i * kademeDerinligi;
                double y2 = yOn + yon * (i + 1) * kademeDerinligi;
                double yA = Math.Min(y1, y2), yB = Math.Max(y1, y2);
                var basamak = new List<Point2D>
                {
                    new(xL, yA), new(xR, yA), new(xR, yB), new(xL, yB)
                };

                // TabanKotu geometriyi yükseltir; 0 ile taban arasındaki hava katı
                // değildir. Her basamağı kendi gerçek Y aralığında kırpmak, kısmi
                // taşmalarda yüksek/alçak kademelerin aynı alan oranını paylaşmasını önler.
                kati += YatayKatiHacmi(salon, kabuk, basamak,
                    sek.TabanKotuM,
                    sek.TabanKotuM + Math.Max(0, i * sek.KademeYuksekligiM));
            }
            v -= kati;
        }

        // Orkestra çukuru: zemin altına inen hava hacmi eklenir.
        if (salon.OrkestraCukuru is { } cukur && cukur.CukurDerinligiM > 0)
            v += IcAlan(kabuk, cukur.Footprint()) * cukur.CukurDerinligiM;

        // Balkon/loca: döşeme plağı kadar hava kaybı (parapet ihmal edilir).
        foreach (var b in salon.Balkonlar)
        {
            if (b.KalinlikM <= 0) continue;
            v -= YatayKatiHacmi(salon, kabuk, SalonBalkonServisi.Footprint(b),
                b.TabanKotuM - b.KalinlikM, b.TabanKotuM);
        }

        // Katılar bütün havayı tüketmişse sonuç sıfırdır. Brüt hacme geri dönmek,
        // fiziksel olarak dolu bir salonu boşmuş gibi M01'e aktarıyordu.
        return Math.Max(0, v);
    }

    /// 3D üretimle aynı küme: görünür katmandaki Zemin tipli (veya katmansız) kapalı öğeler.
    private static List<IReadOnlyList<Point2D>> KabukPoligonlari(SalonGeometri salon)
    {
        var sonuc = new List<IReadOnlyList<Point2D>>();
        foreach (var oge in salon.CizimOgeleri)
        {
            var katman = salon.Katmanlar.FirstOrDefault(k => k.Id == oge.KatmanId);
            if (katman is { Gorunur: false }) continue;
            if (katman is not null && katman.Tip != SalonKatmanTipi.Zemin) continue;

            var poly = oge.KapaliPoligon(Salon3DGorunumServisi.Faset(salon));
            if (poly.Count >= 3) sonuc.Add(poly);
        }
        return sonuc;
    }

    /// Profilli tavan hacmi: kabuk, kırılım Y'lerinde bantlara bölünür; her bantta
    /// z(y) = a + b·y → ∫z dA = a·A + b·∫y dA (işaretli alan + Y-momenti, delikler dahil).
    private static double ProfilHacmi(SalonGeometri salon, List<List<Point2D>> kabuk)
    {
        var tum = kabuk.SelectMany(hh => hh).ToList();
        if (tum.Count == 0) return 0;
        double minY = tum.Min(p => p.Y) - 1, maxY = tum.Max(p => p.Y) + 1;
        double minX = tum.Min(p => p.X) - 1, maxX = tum.Max(p => p.X) + 1;

        var noktalar = SalonTavanServisi.SiraliNoktalar(salon);
        var sinirlar = new List<double> { minY };
        sinirlar.AddRange(noktalar.Select(nk => nk.YM).Where(y => y > minY && y < maxY).Distinct());
        sinirlar.Add(maxY);

        double v = 0;
        for (int b = 0; b + 1 < sinirlar.Count; b++)
        {
            double ya = sinirlar[b], yb = sinirlar[b + 1];
            if (yb - ya < 1e-9) continue;

            // Bant içinde kot doğrusal: iki uçtan katsayıları çöz.
            double yOrta1 = ya + (yb - ya) * 1e-6, yOrta2 = yb - (yb - ya) * 1e-6;
            double z1 = SalonTavanServisi.Kot(salon, yOrta1);
            double z2 = SalonTavanServisi.Kot(salon, yOrta2);
            double egim = (z2 - z1) / (yOrta2 - yOrta1);
            double sabit = z1 - egim * yOrta1;

            var bant = new List<Point2D> { new(minX, ya), new(maxX, ya), new(maxX, yb), new(minX, yb) };
            foreach (var halka in ClipperAdaptor.Kesisim(kabuk, new[] { (IReadOnlyList<Point2D>)bant }))
            {
                double aSigned = ClipperAdaptor.IsaretliAlan(halka);
                double mSigned = SalonPoligonServisi.IsaretliYMomenti(halka);
                v += sabit * aSigned + egim * mSigned;
            }
        }
        return Math.Abs(v);
    }

    /// Footprint'in kabuk içinde kalan alanı (m²).
    private static double IcAlan(List<List<Point2D>> kabuk, IReadOnlyList<Point2D> footprint)
        => footprint.Count < 3
            ? 0
            : ClipperAdaptor.NetAlan(ClipperAdaptor.Kesisim(kabuk, new[] { footprint }));

    /// Yatay bir katının salon havasıyla gerçek 3B kesişim hacmi. Katı footprint'i
    /// kabuğa; [zAlt,zUst] aralığı da zemin ve (gerekirse profilli) tavana kırpılır.
    private static double YatayKatiHacmi(
        SalonGeometri salon,
        List<List<Point2D>> kabuk,
        IReadOnlyList<Point2D> footprint,
        double zAlt,
        double zUst)
    {
        if (footprint.Count < 3) return 0;
        if (zUst < zAlt) (zAlt, zUst) = (zUst, zAlt);
        zAlt = Math.Max(0, zAlt);
        if (zUst - zAlt < 1e-9) return 0;

        var ic = ClipperAdaptor.Kesisim(kabuk, new[] { footprint });
        if (ic.Count == 0) return 0;

        if (!SalonTavanServisi.ProfilliMi(salon))
        {
            double tavan = SalonTavanServisi.Kot(salon, 0);
            double yukseklik = Math.Max(0, Math.Min(zUst, tavan) - zAlt);
            return ClipperAdaptor.NetAlan(ic) * yukseklik;
        }

        var tum = ic.SelectMany(hh => hh).ToList();
        double minY = tum.Min(p => p.Y), maxY = tum.Max(p => p.Y);
        if (maxY - minY < 1e-9) return 0;

        // Önce tavan kırılımlarını, sonra katı alt/üst kotuyla kesişen ek Y
        // noktalarını sınır yap. Böylece her bantta clamp edilmiş yükseklik ya
        // sıfır, ya sabit, ya da doğrusal kalır ve analitik integral alınabilir.
        var temel = new List<double> { minY };
        temel.AddRange(SalonTavanServisi.SiraliNoktalar(salon)
            .Select(n => n.YM)
            .Where(y => y > minY && y < maxY)
            .Distinct());
        temel.Add(maxY);
        temel.Sort();

        var sinirlar = new List<double>(temel);
        for (int i = 0; i + 1 < temel.Count; i++)
        {
            double ya = temel[i], yb = temel[i + 1];
            if (yb - ya < 1e-9) continue;
            double e = (yb - ya) * 1e-6;
            double y1 = ya + e, y2 = yb - e;
            double z1 = SalonTavanServisi.Kot(salon, y1);
            double z2 = SalonTavanServisi.Kot(salon, y2);
            double egim = (z2 - z1) / (y2 - y1);
            if (Math.Abs(egim) < 1e-12) continue;
            double sabit = z1 - egim * y1;
            foreach (double kot in new[] { zAlt, zUst })
            {
                double y = (kot - sabit) / egim;
                if (y > ya + 1e-9 && y < yb - 1e-9) sinirlar.Add(y);
            }
        }
        sinirlar = sinirlar.Distinct().OrderBy(y => y).ToList();

        double hacim = 0;
        double tamYukseklik = zUst - zAlt;
        double minX = tum.Min(p => p.X) - 1, maxX = tum.Max(p => p.X) + 1;
        for (int i = 0; i + 1 < sinirlar.Count; i++)
        {
            double ya = sinirlar[i], yb = sinirlar[i + 1];
            if (yb - ya < 1e-9) continue;
            double e = (yb - ya) * 1e-6;
            double y1 = ya + e, y2 = yb - e;
            double z1 = SalonTavanServisi.Kot(salon, y1);
            double z2 = SalonTavanServisi.Kot(salon, y2);
            double egim = (z2 - z1) / (y2 - y1);
            double sabit = z1 - egim * y1;
            double zOrta = SalonTavanServisi.Kot(salon, (ya + yb) / 2);

            var bant = new List<Point2D>
            {
                new(minX, ya), new(maxX, ya), new(maxX, yb), new(minX, yb)
            };
            foreach (var halka in ClipperAdaptor.Kesisim(ic, new[] { (IReadOnlyList<Point2D>)bant }))
            {
                double a = ClipperAdaptor.IsaretliAlan(halka);
                if (zOrta <= zAlt)
                    continue;
                if (zOrta >= zUst)
                    hacim += a * tamYukseklik;
                else
                    hacim += (sabit - zAlt) * a
                              + egim * SalonPoligonServisi.IsaretliYMomenti(halka);
            }
        }
        return Math.Max(0, Math.Abs(hacim));
    }

    /// Geriye dönüş: tüm öğe/platform footprint'lerinin sınırlayıcı kutusu × yükseklik.
    private static double BboxHacim(SalonGeometri salon, double h)
    {
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        bool any = false;

        void Genislet(IEnumerable<Point2D> pts)
        {
            foreach (var p in pts)
            {
                if (p.X < minX) minX = p.X; if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y; if (p.Y > maxY) maxY = p.Y;
                any = true;
            }
        }

        foreach (var oge in salon.CizimOgeleri)
            foreach (var (a, b) in oge.Segmentler(Salon3DGorunumServisi.Faset(salon)))
            { Genislet(new[] { a, b }); }
        if (salon.Sahne is { } sahne)
            Genislet(sahne.TabanPoligonu(Salon3DGorunumServisi.Faset(salon)));
        foreach (var sek in salon.SeyirciSekciyonlari)
            Genislet(SalonSeyirciServisi.Footprint(sek));
        foreach (var b in salon.Balkonlar)
            Genislet(SalonBalkonServisi.Footprint(b));

        if (!any) return 0;
        return Math.Max(0, maxX - minX) * Math.Max(0, maxY - minY) * h;
    }
}
