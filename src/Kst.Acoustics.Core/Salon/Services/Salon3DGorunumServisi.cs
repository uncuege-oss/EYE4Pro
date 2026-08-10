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
        var surfaces = new List<ProjectSurface>();
        int id = 1;
        const double taban = 0.0;
        int faset = Faset(salon);

        string zeminAd = KatmanAdi(salon, SalonKatmanTipi.Zemin, "Zemin");
        string tavanAd = KatmanAdi(salon, SalonKatmanTipi.Tavan, "Tavan");
        string duvarAd = KatmanAdi(salon, SalonKatmanTipi.DuvarSeyirci, "Duvar");
        string kapiAd = KatmanAdi(salon, SalonKatmanTipi.Kapi, "Kapı");
        string pencereAd = KatmanAdi(salon, SalonKatmanTipi.Pencere, "Pencere");
        string sahneAd = KatmanAdi(salon, SalonKatmanTipi.Sahne, "Sahne");
        var sahneYanKatmani = salon.Katmanlar.FirstOrDefault(k => k.Tip == SalonKatmanTipi.DuvarSahne);
        string sahneYanAd = sahneYanKatmani is { Ad.Length: > 0 } &&
                            !string.Equals(sahneYanKatmani.Ad, "Duvar-Sahne", StringComparison.OrdinalIgnoreCase)
            ? sahneYanKatmani.Ad
            : sahneAd + "-Yan";
        string seyirciAd = KatmanAdi(salon, SalonKatmanTipi.Seyirci, "Seyirci");
        string kornisAd = KatmanAdi(salon, SalonKatmanTipi.Kornis, "Korniş");
        string balkonAd = KatmanAdi(salon, SalonKatmanTipi.Balkon, "Balkon");
        string yansiticiAd = KatmanAdi(salon, SalonKatmanTipi.Yansitici, "Yansıtıcı");

        var kabukPoligonlari = new List<IReadOnlyList<Point2D>>();  // Zemin tipli kapalı öğeler
        var zeminBolgeleri = new List<(List<Point2D> Poly, string Kategori)>(); // z=0 bölge öğeleri
        var tavanBolgeleri = new List<(List<Point2D> Poly, string Kategori)>(); // z=h bölge öğeleri
        var duvarlar = new List<(Point2D A, Point2D B, string Kategori)>();

        foreach (var oge in salon.CizimOgeleri)
        {
            var katman = salon.Katmanlar.FirstOrDefault(k => k.Id == oge.KatmanId);
            if (katman is { Gorunur: false }) continue;

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
                    duvarlar.Add((a, b, kategori));
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
                    p.AsEnumerable().Reverse()
                        .Select(q => new Point3D(q.X, q.Y, taban)).ToList()));

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
                    duvarlar.Add((halka[i], halka[(i + 1) % halka.Count], duvarAd));
        }

        // Her açıklığı bütün duvar adayları arasındaki tek en yakın segmente bağla.
        // Önceki segment-içi tarama, köşe noktasındaki aynı açıklığı iki komşu
        // duvarda birden keserek L biçimli kapı/pencere üretiyordu.
        var duvarAcikliklari = Enumerable.Range(0, duvarlar.Count)
            .Select(_ => new List<(DuvarAcikligi Aciklik, double S)>())
            .ToList();
        var duvarSegmentleri = duvarlar.Select(d => (d.A, d.B)).ToList();
        foreach (var ac in salon.Acikliklar)
        {
            if (ac.GenislikM <= 1e-6 || ac.YukseklikM <= 1e-6) continue;
            int duvar = EnYakinDuvarIndeksi(duvarSegmentleri, ac, out double s, out _, out _);
            if (duvar >= 0) duvarAcikliklari[duvar].Add((ac, s));
        }
        for (int i = 0; i < duvarlar.Count; i++)
        {
            var d = duvarlar[i];
            DuvarYuzeyleriEkle(surfaces, ref id, salon, d.A, d.B, d.Kategori,
                kapiAd, pencereAd, duvarAcikliklari[i]);
        }

        // ── Bölge yüzeyleri (kabuk olsun olmasın üretilir)
        // Son çizilen bölge örtüşmede önceliklidir; önceki bölgeler daha sonraki
        // footprint'lerden kırpılır. Böylece aynı düzlemde çift M01/DXF yüzeyi oluşmaz.
        for (int i = 0; i < zeminBolgeleri.Count; i++)
        {
            var kategori = zeminBolgeleri[i].Kategori;
            foreach (var poly in BolgeParcalari(zeminBolgeleri, i))
                surfaces.Add(new ProjectSurface(id++, kategori, kategori,
                    poly.AsEnumerable().Reverse()
                        .Select(p => new Point3D(p.X, p.Y, taban)).ToList()));
        }
        for (int i = 0; i < tavanBolgeleri.Count; i++)
        {
            var kategori = tavanBolgeleri[i].Kategori;
            foreach (var (panel, _) in TavanPanelleri(salon, BolgeParcalari(tavanBolgeleri, i)))
                surfaces.Add(new ProjectSurface(id++, kategori, kategori,
                    panel.Select(p => new Point3D(p.X, p.Y, SalonTavanServisi.Kot(salon, p.Y))).ToList()));
        }

        // ── Parametrik sahne → yükseltilmiş platform (üst yüzey + çevre etek).
        if (salon.Sahne is { } sahne)
        {
            var poly = sahne.TabanPoligonu(faset);
            if (poly.Count >= 3)
            {
                double zAlt = sahne.TabanKotuM;
                double zUst = sahne.TabanKotuM + (sahne.YukseklikM > 0 ? sahne.YukseklikM : 0.0);

                var parcalar = kabukHalkalari is { Count: > 0 }
                    ? SalonPoligonServisi.KabugaKirp(kabukHalkalari, poly)
                    : SalonPoligonServisi.BasitPoligonlaraAyir(
                        ClipperAdaptor.Birlestir(new[] { (IReadOnlyList<Point2D>)poly }));
                foreach (var parca in parcalar)
                {
                    surfaces.Add(new ProjectSurface(id++, "Sahne", sahneAd,
                        parca.Select(p => new Point3D(p.X, p.Y, zUst)).ToList()));

                    if (zUst - zAlt > 1e-6)
                        for (int i = 0; i < parca.Count; i++)
                            surfaces.Add(DuvarYuzeyi(id++, parca[i], parca[(i + 1) % parca.Count],
                                zAlt, zUst - zAlt, sahneYanAd));
                }
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
            surfaces.AddRange(SalonSeyirciServisi.Yuzeyler(
                sek, ref id, kabukHalkalari, seyirciAd, kornisAd));

        // ── Balkon/loca platformları (kabuğa kırpılır)
        foreach (var balkon in salon.Balkonlar)
            surfaces.AddRange(SalonBalkonServisi.Yuzeyler(
                balkon, ref id, kabukHalkalari, balkonAd));

        // ── Akustik bulut/reflektörler. Panel görünürlüğünün yanında bağlı katman
        // görünürlüğü de geçerlidir; kategori daima katmanın kullanıcıca düzenlenmiş
        // adından gelir. Kırık/boş eski bağ güvenli semantik Yansıtıcı katmanına düşer.
        foreach (var panel in salon.YansiticiPaneller.Where(p => p.Gorunur))
        {
            var katman = salon.Katmanlar.FirstOrDefault(k => k.Id == panel.KatmanId)
                          ?? salon.Katmanlar.FirstOrDefault(k => k.Tip == SalonKatmanTipi.Yansitici);
            if (katman is { Gorunur: false }) continue;
            string kategori = string.IsNullOrWhiteSpace(katman?.Ad) ? yansiticiAd : katman!.Ad;
            surfaces.Add(SalonYansiticiServisi.YuzeyOlustur(panel, id++, kategori));
        }

        return surfaces;
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

    /// Aynı düzlemdeki bölgelerde son çizilen öğeye öncelik verir; bu bölgenin daha
    /// sonra gelen footprint'lerle örtüşmeyen basit parçalarını döndürür.
    private static List<List<Point2D>> BolgeParcalari(
        IReadOnlyList<(List<Point2D> Poly, string Kategori)> bolgeler, int index)
    {
        var ana = ClipperAdaptor.Birlestir(
            new[] { (IReadOnlyList<Point2D>)bolgeler[index].Poly });
        if (ana.Count == 0) return new();

        var sonrakiler = ClipperAdaptor.Birlestir(
            bolgeler.Skip(index + 1).Select(b => (IReadOnlyList<Point2D>)b.Poly));
        return SalonPoligonServisi.BasitPoligonlaraAyir(
            ClipperAdaptor.Fark(ana, sonrakiler));
    }

    /// Açıklığın bir duvar segmentine bağlanması için merkezin segmente en fazla
    /// uzaklığı (m).
    public const double AciklikToleransM = 0.15;

    /// Açıklığın bağlanacağı tek duvarı seçer. Uzaklık birincil ölçüttür; eşit
    /// uzaklıkta açıklık genişliğinin daha az taştığı segment tercih edilir.
    internal static int EnYakinDuvarIndeksi(
        IReadOnlyList<(Point2D A, Point2D B)> segmentler,
        DuvarAcikligi aciklik,
        out double s,
        out double boy,
        out double uzaklik)
    {
        int enIyi = -1;
        s = boy = 0;
        uzaklik = double.MaxValue;
        double enAzTasma = double.MaxValue;

        for (int i = 0; i < segmentler.Count; i++)
        {
            var (a, b) = segmentler[i];
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double boy2 = dx * dx + dy * dy;
            if (boy2 < 1e-18) continue;
            double adayBoy = Math.Sqrt(boy2);
            double t = ((aciklik.MerkezX - a.X) * dx + (aciklik.MerkezY - a.Y) * dy) / boy2;
            t = Math.Clamp(t, 0, 1);
            double px = a.X + t * dx, py = a.Y + t * dy;
            double adayUzaklik = Math.Sqrt(
                (aciklik.MerkezX - px) * (aciklik.MerkezX - px)
                + (aciklik.MerkezY - py) * (aciklik.MerkezY - py));
            if (adayUzaklik > AciklikToleransM) continue;

            double adayS = t * adayBoy;
            double yari = Math.Max(0, aciklik.GenislikM) / 2;
            double tasma = Math.Max(0, yari - adayS)
                           + Math.Max(0, adayS + yari - adayBoy);
            if (adayUzaklik < uzaklik - 1e-9
                || (Math.Abs(adayUzaklik - uzaklik) <= 1e-9 && tasma < enAzTasma - 1e-9))
            {
                enIyi = i;
                s = adayS;
                boy = adayBoy;
                uzaklik = adayUzaklik;
                enAzTasma = tasma;
            }
        }

        return enIyi;
    }

    /// Tabanı z=0'da, üst kenarı tavan profilini izleyen düşey duvar üretir; üzerine
    /// düşen açıklıklar (kapı/pencere) duvardan kesilir ve kendi kategorileriyle yüzey
    /// olarak doldurulur (kabuk sızdırmaz kalır, M01'de ayrı malzeme atanır).
    /// Duvar (s,z) düzleminde poligonlaştırılır (s = kenar boyunca mesafe); Clipper
    /// fark/kesişim + keyhole ile parçalanır. Duvar düşey düzlemde kaldığından üretilen
    /// tüm çokgenler düzlemseldir.
    private static void DuvarYuzeyleriEkle(List<ProjectSurface> surfaces, ref int id,
        SalonGeometri salon, Point2D a, Point2D b, string kategori,
        string kapiAd, string pencereAd,
        IReadOnlyList<(DuvarAcikligi Aciklik, double S)> kesikler)
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
