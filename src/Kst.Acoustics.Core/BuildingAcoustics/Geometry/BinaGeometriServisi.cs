using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.BuildingAcoustics.Calculation;
using Kst.Acoustics.Core.BuildingAcoustics.Reports;

namespace Kst.Acoustics.Core.BuildingAcoustics.Geometry;

public class BinaGeometriServisi
{
    private readonly BinaAkustikRaporServisi _raporServisi = new();

    public List<ProjectSurface> BinaModelineCevir(BinaProje proje, bool sonuclariGoster = false,
                                                  int? sadeceBuKatNo = null,
                                                  bool isiHaritasi = false)
    {
        var surfaces = new List<ProjectSurface>();
        int currentId = 1;

        var tabanZ = 0.0;
        var ekstruzyon = new EkstruzyonServisi();

        foreach (var kat in proje.Katlar.AsEnumerable().Reverse())
        {
            if (sadeceBuKatNo.HasValue && kat.KatNo != sadeceBuKatNo.Value)
            {
                tabanZ += kat.KatYuksekligi;
                continue;
            }
            // B5 — kat orijin ofseti (farklı DXF koordinat sistemleri için hizalama)
            var off = kat.OriginOffset;

            foreach (var mekan in kat.Mekanlar)
            {
                if (mekan.Poligon.Count >= 3)
                {
                    // UX — Isı haritası: mekanın Doseme/Tavan kategorisini en zayıf bölme marjına göre belirle.
                    string dosemeKat = "Doseme";
                    string tavanKat  = "Tavan";
                    if (isiHaritasi && !HesapDisiMekan(mekan))
                    {
                        var heat = MekanIsiKategorisi(proje, mekan);
                        if (heat != null) { dosemeKat = heat; tavanKat = heat; }
                    }

                    if (!DosemeAcikMi(proje, kat, mekan, asagiBak: true))
                    {
                        surfaces.Add(new ProjectSurface(
                            currentId++,
                            $"{mekan.Ad} Döşeme",
                            dosemeKat,
                            mekan.Poligon.Select(p => new Point3D(p.X + off.X, p.Y + off.Y, tabanZ)).ToList()));
                    }

                    if (!DosemeAcikMi(proje, kat, mekan, asagiBak: false))
                    {
                        surfaces.Add(new ProjectSurface(
                            currentId++,
                            $"{mekan.Ad} Tavan",
                            tavanKat,
                            mekan.Poligon.Select(p => new Point3D(p.X + off.X, p.Y + off.Y, tabanZ + mekan.Yukseklik)).ToList()));
                    }
                }

                foreach (var bolme in mekan.Bolmeler)
                {
                    string kategori;
                    if (HesapDisiMekan(mekan) || HesapDisiMekan(bolme.AliciMekan))
                    {
                        kategori = bolme.YapiElemani?.Tip.ToString() ?? "Tanimsiz";
                    }
                    else if (!sonuclariGoster && !isiHaritasi)
                    {
                        kategori = bolme.YapiElemani?.Tip.ToString() ?? "Tanimsiz";
                    }
                    else if (!isiHaritasi)
                    {
                        // Sonuç görünümü rapor/DXF ile aynı kanonik satırı kullanır;
                        // özellikle birleşik dış cephe hedefi ve VeriEksik kararı ayrışmaz.
                        var satir = _raporServisi.BolmeSatiriOlustur(proje, kat, mekan, bolme);
                        kategori = SonucKategorisi(satir, isiHaritasi: false);
                    }
                    else
                    {
                        var satir = _raporServisi.BolmeSatiriOlustur(proje, kat, mekan, bolme);
                        kategori = SonucKategorisi(satir, isiHaritasi: false);
                    }

                    // B4 — CSG duvar panelleri (pencere/kapı boşlukları çıkarılmış) + açıklık yüzeyleri
                    var tumYuzeyler = ekstruzyon.Mekani3DyeCevir(
                        new Mekan { Bolmeler = new System.Collections.ObjectModel.ObservableCollection<Bolme> { bolme }, Poligon = new List<Point2D>(), Yukseklik = mekan.Yukseklik },
                        tabanZ, off);

                    foreach (var y in tumYuzeyler)
                    {
                        if (y.Koseler.Count < 3) continue;
                        // Duvar panelleri uyum kategorisine, açıklıklar (Pencere/Kapi) doğrudan
                        // kendi tiplerine boyanır — 3D'de cam/kapı tipik renkleriyle görünür.
                        string surfKategori = y.Tip switch
                        {
                            "Pencere" => "Pencere",
                            "Kapi"    => "Kapi",
                            _         => kategori,
                        };
                        surfaces.Add(new ProjectSurface(currentId++, bolme.Ad, surfKategori,
                            y.Koseler.Select(k => new Point3D(k.X, k.Y, k.Z)).ToList()));
                    }
                }
            }

            tabanZ += kat.KatYuksekligi;
        }

        return surfaces;
    }

    /// UX — Mekanın "ısı haritası" kategorisini hesaplar: bölmeler arasındaki en kötü marj.
    /// Yarı saydam fill için ayrı kategoriler (HEAT_*) döndürür.
    private string? MekanIsiKategorisi(BinaProje proje, Mekan mekan)
    {
        double? enKotuMarj = null;
        foreach (var b in mekan.Bolmeler)
        {
            if (HesapDisiMekan(b.KaynakMekan) || HesapDisiMekan(b.AliciMekan))
                continue;
            var satir = _raporServisi.BolmeSatiriOlustur(proje, null, mekan, b);
            if (satir.Durum == nameof(UyumDurumu.VeriEksik) || double.IsNaN(satir.MarjDb))
                return "HEAT_VERI_EKSIK";
            var m = satir.MarjDb;
            if (enKotuMarj == null || m < enKotuMarj) enKotuMarj = m;
        }
        if (enKotuMarj == null) return "HEAT_VERI_EKSIK";
        return MarjKategorisi(enKotuMarj.Value, isiHaritasi: true);
    }

    private static string SonucKategorisi(RaporSatiri satir, bool isiHaritasi)
    {
        if (satir.Durum == nameof(UyumDurumu.VeriEksik) || double.IsNaN(satir.MarjDb))
            return isiHaritasi ? "HEAT_VERI_EKSIK" : "VERI_EKSIK";
        return MarjKategorisi(satir.MarjDb, isiHaritasi);
    }

    private static string MarjKategorisi(double marj, bool isiHaritasi)
    {
        var onEk = isiHaritasi ? "HEAT_" : "";
        return marj >= 3.0 ? onEk + "UYGUN_GUVENLI"
             : marj >= 1.0 ? onEk + "UYGUN_SINIRDA"
             : marj >= -1.0 ? onEk + "SINIR"
             : marj >= -3.0 ? onEk + "UYGUNSUZ_HAFIF"
                            : onEk + "UYGUNSUZ";
    }

    private static bool HesapDisiMekan(Mekan? mekan) =>
        mekan != null &&
        (
            mekan.Sinif == MekanSinifi.HesapDisi ||
            EK2SiniflandirmaServisi.HesapDisiMi(mekan.KullanimTipi) ||
            MekanKullanimiHelper.Parse(mekan.KullanimTipi) == MekanKullanimiTipi.HesapDisi
        );

    private static bool DosemeAcikMi(BinaProje proje, Kat kat, Mekan mekan, bool asagiBak)
    {
        // UX — TavanAcik / DosemeAcik bayraklarına göre: asagiBak=true → bu mekanın
        // döşemesi açık mı; asagiBak=false → tavanı açık mı.
        return asagiBak
            ? DuseyMekanBirlesimServisi.DosemeAcikMi(proje, mekan)
            : DuseyMekanBirlesimServisi.TavanAcikMi(proje, mekan);
    }

    private static IReadOnlyList<Point3D> CreateWallVertices(Bolme bolme, double tabanZ, double yukseklik)
    {
        if (!bolme.HasGeometry)
            return [];

        var tavanZ = tabanZ + yukseklik;
        return
        [
            new Point3D(bolme.Baslangic.X, bolme.Baslangic.Y, tabanZ),
            new Point3D(bolme.Bitis.X, bolme.Bitis.Y, tabanZ),
            new Point3D(bolme.Bitis.X, bolme.Bitis.Y, tavanZ),
            new Point3D(bolme.Baslangic.X, bolme.Baslangic.Y, tavanZ)
        ];
    }
}
