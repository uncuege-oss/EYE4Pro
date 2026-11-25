using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Salon.Models;

namespace Kst.Acoustics.Core.Salon.Services;

/// M51 — SalonGeometri 2D eskizini 3D yüzeylere (ProjectSurface) çevirir.
/// Aynı çıktı hem 3D önizleme (GeometryPreviewControl) hem M01 köprüsü hem DXF ihracatı için
/// kullanılır. Kurallar:
///   • **Zemin** tipli katmandaki (veya katmanı çözülemeyen) kapalı öğe → **kabuk**:
///     zemin + tavan + çevre duvarları. Birden çok kabuk poligonu union'lanır (çift duvar olmaz).
///   • **Tavan** tipli katmandaki kapalı öğe → z=yükseklik'te tek yüzey (tavan bölgesi);
///     kabuk tavanından bu bölge kırpılır.
///   • Diğer katmanlardaki kapalı öğe → z=0'da tek **bölge** yüzeyi (kategori = katman adı);
///     kabuk zemininden bu bölge kırpılır (çakışık yüzey/çift emilim engellenir).
///   • Açık öğe (çizgi / açık polyline) → her segment **duvar** (kategori = katman adı).
///   • Sahne / seyirci platformları zemine oturuyorsa (taban kotu ≈ 0) footprint'leri
///     kabuk zemininden kırpılır.
/// Gizli katmandaki öğeler atlanır. Eğriler (daire) çokgene fasetlenir. Kabuk duvar/tavan
/// kategorileri varsayılan katman adlarından türetilir (kullanıcı katmanı yeniden adlandırırsa
/// M01 kategorisi de değişir).
public static class Salon3DGorunumServisi
{
    /// Daire fasetlerken kullanılacak varsayılan kenar sayısı.
    public const int DaireKenarSayisi = 48;

    /// Salonun geçerli faset kenar sayısı (12–128'e kelepçeli; 0/negatif → varsayılan).
    public static int Faset(SalonGeometri salon)
        => Math.Clamp(salon.FasetKenarSayisi <= 0 ? DaireKenarSayisi : salon.FasetKenarSayisi, 12, 128);

    /// Zemine oturma toleransı (m) — taban kotu bunun altındaysa platform zemini kapatıyor sayılır.
    private const double ZeminOturmaToleransM = 0.01;

    public static List<ProjectSurface> YuzeyleriOlustur(SalonGeometri salon)
    {
        if (salon.SerbestTasarim) return SalonTasarimMeshServisi.Uret(salon).Yuzeyler;
        var surfaces = new List<ProjectSurface>();
        int id = 1;
        const double taban = 0.0;
        int faset = Faset(salon);

        string zeminAd = KatmanAdi(salon, SalonKatmanTipi.Zemin, "Zemin");
        string tavanAd = KatmanAdi(salon, SalonKatmanTipi.Tavan, "Tavan");
        string duvarAd = KatmanAdi(salon, SalonKatmanTipi.DuvarSeyirci, "Duvar");
        string kapiAd = KatmanAdi(salon, SalonKatmanTipi.Kapi, "Kapı");
        string pencereAd = KatmanAdi(salon, SalonKatmanTipi.Pencere, "Pencere");

        var kabukPoligonlari = new List<IReadOnlyList<Point2D>>();  // Zemin tipli kapalı öğeler
        var zeminBolgeleri = new List<(List<Point2D> Poly, string Kategori)>(); // z=0 bölge öğeleri
        var tavanBolgeleri = new List<(List<Point2D> Poly, string Kategori)>(); // z=h bölge öğeleri

        foreach (var oge in salon.CizimOgeleri)
        {
            var katman = salon.Katmanlar.FirstOrDefault(k => k.Id == oge.KatmanId);


            var poly = oge.KapaliPoligon(faset);
            if (poly.Count >= 3)
            {
                if (katman is null || katman.Tip == SalonKatmanTipi.Zemin)
                    kabukPoligonlari.Add(poly);
                else if (katman.Tip == SalonKatmanTipi.Tavan)
                    tavanBolgeleri.Add((poly, KategoriAdi(katman, tavanAd)));
                else
                    zeminBolgeleri.Add((poly, KategoriAdi(katman, zeminAd)));
            }
            else
            {
                string kategori = KategoriAdi(katman, duvarAd);
                foreach (var (a, b) in oge.Segmentler(faset))
                    DuvarYuzeyleriEkle(surfaces, ref id, salon, a, b, kategori, kapiAd, pencereAd);
            }
        }

        // ── Kabuk: union(zemin poligonları) → zemin (kırpılmış) + tavan (kırpılmış) + duvarlar
        List<List<Point2D>>? kabukHalkalari = null; // seyirci/balkon kırpma için
        if (kabukPoligonlari.Count > 0)
        {
            var kabuk = ClipperAdaptor.Birlestir(kabukPoligonlari);
            kabukHalkalari = kabuk;

            // Zemini kapatan footprint'ler: bölge öğeleri + zemine oturan sahne/seyirci
            // + orkestra çukuru açıklığı.
            var zeminKirpici = new List<IReadOnlyList<Point2D>>();
            zeminKirpici.AddRange(zeminBolgeleri.Select(b => (IReadOnlyList<Point2D>)b.Poly));
            if (salon.Sahne is { } sh && Math.Abs(sh.TabanKotuM) < ZeminOturmaToleransM)
                zeminKirpici.Add(sh.TabanPoligonu(faset));
            foreach (var sek in salon.SeyirciSekciyonlari)
                if (Math.Abs(sek.TabanKotuM) < ZeminOturmaToleransM)
                    zeminKirpici.Add(SalonSeyirciServisi.Footprint(sek));
            if (salon.OrkestraCukuru is { } ck && ck.CukurDerinligiM > 1e-6)
                zeminKirpici.Add(ck.Footprint());

            foreach (var p in SalonPoligonServisi.BasitPoligonlaraAyir(
                         ClipperAdaptor.Fark(kabuk, zeminKirpici)))
                surfaces.Add(new ProjectSurface(id++, zeminAd, zeminAd,
                    p.Select(q => new Point3D(q.X, q.Y, taban)).ToList()));

            // Tavan: profil yoksa düz tek yüzey; profil varsa Y bantlarına bölünmüş
            // eğik paneller (her bantta z, y'ye doğrusal bağlı → panel düzlemsel).
            var tavanKirpilmis = ClipperAdaptor.Fark(kabuk,
                tavanBolgeleri.Select(b => (IReadOnlyList<Point2D>)b.Poly));
            foreach (var (p, _) in TavanPanelleri(salon, tavanKirpilmis))
                surfaces.Add(new ProjectSurface(id++, tavanAd, tavanAd,
                    p.Select(q => new Point3D(q.X, q.Y, SalonTavanServisi.Kot(salon, q.Y))).ToList()));

            // Çevre duvarları: union halkalarının tüm kenarları (delik halkası = iç avlu
            // duvarı). Duvar üst kenarı tavan profilini izler; açıklıklar kesilir.
            foreach (var halka in kabuk)
                for (int i = 0; i < halka.Count; i++)
                    DuvarYuzeyleriEkle(surfaces, ref id, salon,
                        halka[i], halka[(i + 1) % halka.Count], duvarAd, kapiAd, pencereAd);
        }

        // ── Bölge yüzeyleri (kabuk olsun olmasın üretilir)
        foreach (var (poly, kategori) in zeminBolgeleri)
            surfaces.Add(new ProjectSurface(id++, kategori, kategori,
                poly.Select(p => new Point3D(p.X, p.Y, taban)).ToList()));
        foreach (var (poly, kategori) in tavanBolgeleri)
            foreach (var (panel, _) in TavanPanelleri(salon, new List<List<Point2D>> { poly }))
                surfaces.Add(new ProjectSurface(id++, kategori, kategori,
                    panel.Select(p => new Point3D(p.X, p.Y, SalonTavanServisi.Kot(salon, p.Y))).ToList()));

        // ── Parametrik sahne → yükseltilmiş platform (üst yüzey + çevre etek).
        if (salon.Sahne is { } sahne)
        {
            var poly = sahne.TabanPoligonu(faset);
            if (poly.Count >= 3)
            {
                double zAlt = sahne.TabanKotuM;
                double zUst = sahne.TabanKotuM + (sahne.YukseklikM > 0 ? sahne.YukseklikM : 0.0);

                surfaces.Add(new ProjectSurface(id++, "Sahne", KatmanAdi(salon,SalonKatmanTipi.Sahne,"Sahne"),
                    poly.Select(p => new Point3D(p.X, p.Y, zUst)).ToList()));

                if (zUst - zAlt > 1e-6)
                    for (int i = 0; i < poly.Count; i++)
                        surfaces.Add(DuvarYuzeyi(id++, poly[i], poly[(i + 1) % poly.Count], zAlt, zUst - zAlt, KatmanAdi(salon,SalonKatmanTipi.Sahne,"Sahne")+"-Yan"));
            }
        }

        // ── Orkestra çukuru → zemin açıklığı (yukarıda kırpıldı) + taban + çevre duvarları
        if (salon.OrkestraCukuru is { } cukur && cukur.CukurDerinligiM > 1e-6)
        {
            string orkestraAd = KatmanAdi(salon, SalonKatmanTipi.Orkestra, "Orkestra");
            double zTaban = -cukur.CukurDerinligiM;
            var fp = cukur.Footprint();

            foreach (var p in SalonPoligonServisi.KabugaKirp(kabukHalkalari, fp))
                surfaces.Add(new ProjectSurface(id++, "Orkestra Çukuru Taban", orkestraAd,
                    p.Select(q => new Point3D(q.X, q.Y, zTaban)).ToList()));

            double xL = cukur.MerkezX - cukur.GenislikM / 2, xR = cukur.MerkezX + cukur.GenislikM / 2;
            double yA = cukur.MerkezY - cukur.DerinlikM / 2, yB = cukur.MerkezY + cukur.DerinlikM / 2;
            foreach (double y in new[] { yA, yB })
                foreach (var (px1, px2) in SalonPoligonServisi.YatayParcalar(kabukHalkalari, y, xL, xR))
                    surfaces.Add(new ProjectSurface(id++, "Orkestra Çukuru Duvar", orkestraAd, new List<Point3D>
                    {
                        new(px1, y, zTaban), new(px2, y, zTaban), new(px2, y, 0), new(px1, y, 0)
                    }));
            foreach (double x in new[] { xL, xR })
                foreach (var (py1, py2) in SalonPoligonServisi.DikeyParcalar(kabukHalkalari, x, yA, yB))
                    surfaces.Add(new ProjectSurface(id++, "Orkestra Çukuru Duvar", orkestraAd, new List<Point3D>
                    {
                        new(x, py1, zTaban), new(x, py2, zTaban), new(x, py2, 0), new(x, py1, 0)
                    }));
        }

        // ── Kademeli seyirci alanları → basamak döşemeleri + korniş (kabuğa kırpılır)
        foreach (var sek in salon.SeyirciSekciyonlari)
            surfaces.AddRange(SalonSeyirciServisi.Yuzeyler(sek, ref id, kabukHalkalari));

        // ── Balkon/loca platformları (kabuğa kırpılır)
        foreach (var balkon in salon.Balkonlar)
            surfaces.AddRange(SalonBalkonServisi.Yuzeyler(balkon, ref id, kabukHalkalari));

        var categoryMap=new Dictionary<string,string> {
            ["Seyirci"]=KatmanAdi(salon,SalonKatmanTipi.Seyirci,"Seyirci"),
            ["Korniş"]=KatmanAdi(salon,SalonKatmanTipi.Kornis,"Korniş"),
            ["Balkon"]=KatmanAdi(salon,SalonKatmanTipi.Balkon,"Balkon"),
            ["Balkon-Alt"]=KatmanAdi(salon,SalonKatmanTipi.Balkon,"Balkon")+"-Alt",
            ["Balkon-Parapet"]=KatmanAdi(salon,SalonKatmanTipi.Balkon,"Balkon")+"-Parapet" };
        return surfaces.Select(p=>categoryMap.TryGetValue(p.Category,out var category)?p with { Category=category }:p).ToList();
    }

    /// Tavan halka kümesini panellere ayırır. Profil yoksa yalnız keyhole ayrıştırma;
    /// profil varsa halkalar kırılım Y'lerinde bantlara bölünür — her bantta kot y'ye
    /// doğrusal bağlı olduğundan panel düzlemseldir (ışın izleme/DXF için geçerli yüzey).
    private static IEnumerable<(List<Point2D> Panel, int Bant)> TavanPanelleri(
        SalonGeometri salon, List<List<Point2D>> halkalar)
    {
        if (!SalonTavanServisi.ProfilliMi(salon))
        {
            foreach (var p in SalonPoligonServisi.BasitPoligonlaraAyir(halkalar))
                yield return (p, 0);
            yield break;
        }

        var tum = halkalar.SelectMany(hh => hh).ToList();
        if (tum.Count == 0) yield break;
        double minX = tum.Min(p => p.X) - 1, maxX = tum.Max(p => p.X) + 1;
        double minY = tum.Min(p => p.Y) - 1, maxY = tum.Max(p => p.Y) + 1;

        var kirilimlar = SalonTavanServisi.SiraliNoktalar(salon)
            .Select(n => n.YM)
            .Where(y => y > minY && y < maxY)
            .Distinct()
            .ToList();

        var sinirlar = new List<double> { minY };
        sinirlar.AddRange(kirilimlar);
        sinirlar.Add(maxY);

        for (int b = 0; b + 1 < sinirlar.Count; b++)
        {
            double ya = sinirlar[b], yb = sinirlar[b + 1];
            if (yb - ya < 1e-9) continue;
            var bant = new List<Point2D>
            {
                new(minX, ya), new(maxX, ya), new(maxX, yb), new(minX, yb)
            };
            var kesit = ClipperAdaptor.Kesisim(halkalar, new[] { (IReadOnlyList<Point2D>)bant });
            foreach (var p in SalonPoligonServisi.BasitPoligonlaraAyir(kesit))
                yield return (p, b);
        }
    }

    /// Açıklığın bir duvar segmentine bağlanması için merkezin segmente en fazla
    /// uzaklığı (m).
    public const double AciklikToleransM = 0.15;

    /// Tabanı z=0'da, üst kenarı tavan profilini izleyen düşey duvar üretir; üzerine
    /// düşen açıklıklar (kapı/pencere) duvardan kesilir ve kendi kategorileriyle yüzey
    /// olarak doldurulur (kabuk sızdırmaz kalır, M01'de ayrı malzeme atanır).
    /// Duvar (s,z) düzleminde poligonlaştırılır (s = kenar boyunca mesafe); Clipper
    /// fark/kesişim + keyhole ile parçalanır. Duvar düşey düzlemde kaldığından üretilen
    /// tüm çokgenler düzlemseldir.
    private static void DuvarYuzeyleriEkle(List<ProjectSurface> surfaces, ref int id,
        SalonGeometri salon, Point2D a, Point2D b, string kategori,
        string kapiAd, string pencereAd)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double boy = Math.Sqrt(dx * dx + dy * dy);
        if (boy < 1e-9) return;

        Point3D Harita(Point2D sz) =>
            new(a.X + sz.X / boy * dx, a.Y + sz.X / boy * dy, sz.Y);

        // (s,z) dış hattı: taban + üst kenar (tavan profili kırılımları ara köşe).
        var hat = new List<Point2D>
        {
            new(0, 0),
            new(boy, 0),
            new(boy, SalonTavanServisi.Kot(salon, b.Y))
        };
        if (SalonTavanServisi.ProfilliMi(salon) && Math.Abs(a.Y - b.Y) > 1e-9)
        {
            double yLo = Math.Min(a.Y, b.Y), yHi = Math.Max(a.Y, b.Y);
            var aradakiler = SalonTavanServisi.SiraliNoktalar(salon)
                .Select(n => n.YM)
                .Where(y => y > yLo + 1e-9 && y < yHi - 1e-9)
                .Distinct()
                .Select(y => (S: (y - a.Y) / (b.Y - a.Y) * boy, Kot: SalonTavanServisi.Kot(salon, y)))
                .OrderByDescending(p => p.S) // b→a gezinme yönü (s azalan)
                .ToList();
            foreach (var (s, kot) in aradakiler)
                hat.Add(new Point2D(s, kot));
        }
        hat.Add(new Point2D(0, SalonTavanServisi.Kot(salon, a.Y)));

        // Bu duvara düşen açıklıklar (merkez segment üzerinde, tolerans içinde).
        var kesikler = new List<(DuvarAcikligi Aciklik, double S)>();
        foreach (var ac in salon.Acikliklar)
        {
            if (ac.GenislikM <= 1e-6 || ac.YukseklikM <= 1e-6) continue;
            double t = ((ac.MerkezX - a.X) * dx + (ac.MerkezY - a.Y) * dy) / (boy * boy);
            t = Math.Clamp(t, 0, 1);
            double px = a.X + t * dx, py = a.Y + t * dy;
            double uzaklik = Math.Sqrt((ac.MerkezX - px) * (ac.MerkezX - px)
                                       + (ac.MerkezY - py) * (ac.MerkezY - py));
            if (uzaklik <= AciklikToleransM)
                kesikler.Add((ac, t * boy));
        }

        if (kesikler.Count == 0)
        {
            surfaces.Add(new ProjectSurface(id++, kategori, kategori, hat.Select(Harita).ToList()));
            return;
        }

        List<Point2D> Delik(DuvarAcikligi ac, double s) => new()
        {
            new(s - ac.GenislikM / 2, ac.AltKotM),
            new(s + ac.GenislikM / 2, ac.AltKotM),
            new(s + ac.GenislikM / 2, ac.AltKotM + ac.YukseklikM),
            new(s - ac.GenislikM / 2, ac.AltKotM + ac.YukseklikM)
        };

        // Duvar parçaları (delikler çıkarılmış, keyhole ile basitleştirilmiş).
        var delikler = kesikler.Select(k => (IReadOnlyList<Point2D>)Delik(k.Aciklik, k.S)).ToList();
        foreach (var p in SalonPoligonServisi.BasitPoligonlaraAyir(
                     ClipperAdaptor.Fark(new[] { (IReadOnlyList<Point2D>)hat }, delikler)))
            surfaces.Add(new ProjectSurface(id++, kategori, kategori, p.Select(Harita).ToList()));

        // Açıklık dolgu yüzeyleri (duvar dış hattına kırpılır — üstten taşan kesilir).
        foreach (var (ac, s) in kesikler)
        {
            string acKategori = ac.Tip == AciklikTipi.Pencere ? pencereAd : kapiAd;
            foreach (var p in SalonPoligonServisi.BasitPoligonlaraAyir(
                         ClipperAdaptor.Kesisim(new[] { (IReadOnlyList<Point2D>)hat },
                             new[] { (IReadOnlyList<Point2D>)Delik(ac, s) })))
                surfaces.Add(new ProjectSurface(id++, ac.Ad, acKategori, p.Select(Harita).ToList()));
        }
    }

    private static ProjectSurface DuvarYuzeyi(int id, Point2D a, Point2D b, double taban, double h, string kategori)
        => new(id, kategori, kategori, new List<Point3D>
        {
            new(a.X, a.Y, taban),
            new(b.X, b.Y, taban),
            new(b.X, b.Y, taban + h),
            new(a.X, a.Y, taban + h)
        });

    /// Verilen tipteki ilk katmanın adı; yoksa <paramref name="varsayilan"/>.
    private static string KatmanAdi(SalonGeometri salon, SalonKatmanTipi tip, string varsayilan)
    {
        var ad = salon.Katmanlar.FirstOrDefault(k => k.Tip == tip)?.Ad;
        return string.IsNullOrWhiteSpace(ad) ? varsayilan : ad!;
    }

    /// Öğenin katman adı (M01 kategorisi); katman yoksa/adsızsa <paramref name="varsayilan"/>.
    private static string KategoriAdi(SalonKatman? k, string varsayilan)
        => string.IsNullOrWhiteSpace(k?.Ad) ? varsayilan : k!.Ad;
}
