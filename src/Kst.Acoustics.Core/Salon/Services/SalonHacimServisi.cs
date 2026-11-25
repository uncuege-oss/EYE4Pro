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
        if (salon.SerbestTasarim) return SalonTasarimMeshServisi.Uret(salon).Hacim;
        double h = salon.SalonYuksekligiM > 0 ? salon.SalonYuksekligiM : 3.0;

        var kabukPoligonlari = KabukPoligonlari(salon);
        if (kabukPoligonlari.Count == 0)
            return BboxHacim(salon, h);

        var kabuk = ClipperAdaptor.Birlestir(kabukPoligonlari);
        var copy=SalonGeometriServisi.KopyadanYukle(SalonGeometriServisi.KopyaJson(salon));
        SalonTasarimServisi.Hazirla(copy);
        if(SalonTasarimMeshServisi.Cakismalar(SalonTasarimServisi.Paneller(copy,kabuk)).Count>0)
            throw new InvalidDataException("Üst üste gelen bölgeler için hacim hesaplanamaz; yerleşimi düzeltin.");
        double alan = ClipperAdaptor.NetAlan(kabuk);
        if (alan < 1e-9) return BboxHacim(salon, h);

        // Tavan hacmi: düz tavanda alan × h; profilli tavanda ∫ z(y) dA (bant bazlı,
        // her bantta z = a + b·y doğrusal → integral = a·A + b·∫y dA).
        double v = SalonTavanServisi.ProfilliMi(salon) ? ProfilHacmi(salon, kabuk) : alan * h;
        double maksKot = SalonTavanServisi.MaksKot(salon);

        // Sahne platformu: kabuk içindeki alan × platform yüksekliği (salon içinde kalan kısım).
        if (salon.Sahne is { } sahne && sahne.YukseklikM > 0)
        {
            double icAlan = IcAlan(kabuk, sahne.TabanPoligonu(Salon3DGorunumServisi.Faset(salon)));
            double yukseklik = Math.Clamp(sahne.YukseklikM, 0, Math.Max(0, maksKot - sahne.TabanKotuM));
            v -= icAlan * yukseklik;
        }

        // Kademeli seyirci: teraslar dolu (katı) kabul edilir.
        // Katı hacim = Σ basamakAlan × basamakKotu; kabuk içinde kalan oranla düşülür.
        foreach (var sek in salon.SeyirciSekciyonlari)
        {
            var fp = SalonSeyirciServisi.Footprint(sek);
            double fpAlan = Math.Abs(ClipperAdaptor.IsaretliAlan(fp));
            if (fpAlan < 1e-9) continue;
            int n = Math.Max(1, sek.KademeSayisi);
            double x0=sek.MerkezX-sek.GenislikM/2, x1=sek.MerkezX+sek.GenislikM/2;
            double front=sek.MerkezY+(sek.TersYon?1:-1)*sek.DerinlikM/2;
            double direction=sek.TersYon?-1:1;
            for(int i=0;i<n;i++)
            {
                double y0=front+direction*i*sek.DerinlikM/n,y1=front+direction*(i+1)*sek.DerinlikM/n;
                var step=new List<Point2D>{new(x0,Math.Min(y0,y1)),new(x1,Math.Min(y0,y1)),new(x1,Math.Max(y0,y1)),new(x0,Math.Max(y0,y1))};
                double availableHeight=Math.Max(0,maksKot-Math.Max(0,sek.TabanKotuM));
                v-=IcAlan(kabuk,step)*Math.Clamp(i*sek.KademeYuksekligiM,0,availableHeight);
            }
        }

        // Orkestra çukuru: zemin altına inen hava hacmi eklenir.
        if (salon.OrkestraCukuru is { } cukur && cukur.CukurDerinligiM > 0)
            v += IcAlan(kabuk, cukur.Footprint()) * cukur.CukurDerinligiM;

        // Balkon/loca: döşeme plağı kadar hava kaybı (parapet ihmal edilir).
        foreach (var b in salon.Balkonlar)
        {
            if (b.KalinlikM <= 0) continue;
            double icAlan = IcAlan(kabuk, SalonBalkonServisi.Footprint(b));
            v -= icAlan * b.KalinlikM;
        }

        // Düşümler hacmi tüketmişse (uç parametreler) plan hacmine geri düş.
        return v > 1e-6 ? v : alan * h;
    }

    /// 3D üretimle aynı küme: görünür katmandaki Zemin tipli (veya katmansız) kapalı öğeler.
    private static List<IReadOnlyList<Point2D>> KabukPoligonlari(SalonGeometri salon)
    {
        var sonuc = new List<IReadOnlyList<Point2D>>();
        foreach (var oge in salon.CizimOgeleri)
        {
            var katman = salon.Katmanlar.FirstOrDefault(k => k.Id == oge.KatmanId);

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
