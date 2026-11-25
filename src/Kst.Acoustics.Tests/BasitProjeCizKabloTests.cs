using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.App.Electric.BasitProjeCiz.ViewModels;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;
using netDxf;
using netDxf.Entities;

namespace Kst.Acoustics.Tests;

public static class BasitProjeCizKabloTests
{
    public static void ArmatürYerlesimiEskiMantikGelseDeGridDizilimKullanir()
    {
        var tanim = new BpcArmatürTanim
        {
            Ad = "Eski Kayıt Armatürü",
            BlokAdi = "E4P_LIGHT",
            SembolAdi = "L",
            YerlesimMantigi = BpcArmaturlestirmeMantigi.OdaMerkezineBirAdet,
            GridAraligiM = 4.0
        };
        var oda = new BasitProjeOda
        {
            Ad = "Grid Oda",
            Poligon = [new(0, 0), new(8, 0), new(8, 8), new(0, 8)]
        };

        var yeni = ArmaturYerlesimServisi.YerlesimOlustur(oda, tanim);
        var noktalar = yeni.Select(e => (Math.Round(e.X, 3), Math.Round(e.Y, 3))).ToHashSet();
        var beklenen = new HashSet<(double X, double Y)> { (2, 2), (2, 6), (6, 2), (6, 6) };

        if (yeni.Count != 4 || !beklenen.SetEquals(noktalar))
            throw new InvalidOperationException($"Eski yerleşim mantığı gelse bile grid noktaları bekleniyordu. Gelen: {string.Join("; ", yeni.Select(e => $"{e.X:0.###},{e.Y:0.###}"))}");
    }

    public static void OtomatikArmatürYerlesimiSadeceGridAdaylariniKullanir()
    {
        var tanim = new BpcArmatürTanim
        {
            Id = "grid-lineer",
            Ad = "Lineer Koridor",
            BlokAdi = "E4P_LINEAR",
            MontajTipi = "Duvar",
            YerlesimMantigi = BpcArmaturlestirmeMantigi.KoridorEkseni,
            GridAraligiM = 4.0
        };
        var oda = new BasitProjeOda
        {
            Ad = "Grid Oda",
            Poligon = [new(0, 0), new(8, 0), new(8, 8), new(0, 8)]
        };

        var servis = new BasitProjeCizOtomatikYerlestirServisi(new BasitProjeCizGeometriServisi());
        var yeni = servis.OdayaYerlestir(
            oda,
            tanim,
            yerlesimTipi: "Adet",
            montajTipi: "Tavan",
            gridAraligiM: 4.0,
            duvarAraligiM: 1.0,
            adetAdetYerlesimiIcin: 4,
            acikliklar: new List<BpcAciklik>(),
            mevcutElemanlar: new List<BasitProjeEleman>(),
            sadecaEksikleri: true,
            out var uyari);

        if (uyari is not null)
            throw new InvalidOperationException($"Grid armatür yerleşimi uyarısız olmalı. Uyarı: {uyari}");
        if (yeni.Count != 4)
            throw new InvalidOperationException($"4 grid armatürü bekleniyordu. Bulunan: {yeni.Count}.");
        if (yeni.Any(e => e.Tip != BpcElemanTipi.Armatür || e.BlokAdi != "E4P_LINEAR"))
            throw new InvalidOperationException("Otomatik armatür yerleşimi armatür tipini ve blok adını korumalı.");
    }

    public static void OtomatikAydinlatmaKablolamaBosArmaturleriBaglar()
    {
        var kat = new BasitProjeKat();
        var oda = new BasitProjeOda { Ad = "Aydinlatma Oda", Poligon = [new(0, 0), new(5, 0), new(5, 5), new(0, 5)] };
        kat.Odalar.Add(oda);

        var anahtar = new BasitProjeEleman { Tip = BpcElemanTipi.Anahtar, Ad = "A1", OdaId = oda.Id, X = 0.5, Y = 1 };
        var arm1 = new BasitProjeEleman { Tip = BpcElemanTipi.Armatür, Ad = "L1", OdaId = oda.Id, X = 2, Y = 2 };
        var arm2 = new BasitProjeEleman { Tip = BpcElemanTipi.Armatür, Ad = "L2", OdaId = oda.Id, X = 3, Y = 3 };
        var buat = new BasitProjeEleman { Tip = BpcElemanTipi.Buat, Ad = "B1", OdaId = oda.Id, X = 4.5, Y = 4 };

        kat.Elemanlar.Add(anahtar);
        kat.Elemanlar.Add(arm1);
        kat.Elemanlar.Add(arm2);
        kat.Elemanlar.Add(buat);

        var devre = BasitProjeCizKabloServisi.EnsureDevre(kat, anahtar.Id);
        BasitProjeCizKabloServisi.MahaldekiBosArmaturleriAnahtaraBagla(kat, devre, anahtar.Id);

        if (devre.ArmaturElemanIds.Count != 2)
            throw new InvalidOperationException($"Mahal otomatik kablolama 2 armatür bağlamalıydı, bulunan: {devre.ArmaturElemanIds.Count}");

        if (devre.BuatId != buat.Id)
            throw new InvalidOperationException("Otomatik buat ataması başarısız oldu.");

        var oda2 = new BasitProjeOda { Ad = "Aydinlatma Oda 2", Poligon = [new(10, 0), new(15, 0), new(15, 5), new(10, 5)] };
        kat.Odalar.Add(oda2);

        var anahtar2 = new BasitProjeEleman { Tip = BpcElemanTipi.Anahtar, Ad = "A2", OdaId = oda2.Id, X = 10.5, Y = 1 };
        var arm3 = new BasitProjeEleman { Tip = BpcElemanTipi.Armatür, Ad = "L3", OdaId = oda2.Id, X = 12, Y = 2 };
        kat.Elemanlar.Add(anahtar2);
        kat.Elemanlar.Add(arm3);

        BasitProjeCizKabloServisi.KatAydinlatmasiniOtomatikKablola(kat);

        var devre2 = kat.Devreler.FirstOrDefault(d => d.AnahtarElemanId == anahtar2.Id);
        if (devre2 == null || devre2.ArmaturElemanIds.Count != 1 || !devre2.ArmaturElemanIds.Contains(arm3.Id))
            throw new InvalidOperationException("Kat geneli otomatik kablolama oda 2'deki armatürü bağlamalıydı.");
    }

    public static void ManuallyAssignedSwitchAndSensorUseExpectedBuatRouting()
    {
        var kat = new BasitProjeKat();
        kat.Odalar.Add(new BasitProjeOda
        {
            Ad = "Oda",
            Poligon = [new(0, 0), new(6, 0), new(6, 4), new(0, 4)]
        });

        var anahtar = new BasitProjeEleman
        {
            Tip = BpcElemanTipi.Anahtar,
            Ad = "A1",
            X = 0,
            Y = 1
        };
        var sensor = new BasitProjeEleman
        {
            Tip = BpcElemanTipi.Sensör,
            Ad = "S1",
            X = 3,
            Y = 2
        };
        var buat = new BasitProjeEleman
        {
            Tip = BpcElemanTipi.Buat,
            Ad = "B1",
            X = 5,
            Y = 3
        };

        kat.Elemanlar.Add(anahtar);
        kat.Elemanlar.Add(sensor);
        kat.Elemanlar.Add(buat);

        var anahtarDevre = BasitProjeCizKabloServisi.EnsureDevre(kat, anahtar.Id);
        var sensorDevre = BasitProjeCizKabloServisi.EnsureDevre(kat, sensor.Id);
        BasitProjeCizKabloServisi.ButaAta(kat, anahtarDevre, buat.Id);
        BasitProjeCizKabloServisi.ButaAta(kat, sensorDevre, buat.Id);

        foreach (var devre in kat.Devreler)
            BasitProjeCizKabloServisi.RotaHesapla(kat, devre, BpcRotaTipi.DaisyChain);

        var anahtarKablo = kat.Kablolar.Single(k => k.DevreId == anahtarDevre.Id);
        var sensorKablo = kat.Kablolar.Single(k => k.DevreId == sensorDevre.Id);

        if (anahtarKablo.Rota.Count < 4)
            throw new InvalidOperationException("Anahtar-buat kablosu duvar kenarından yürümeli ve ara duvar noktaları içermeli.");

        if (sensorKablo.Rota.Count != 2)
            throw new InvalidOperationException("Sensör-buat kablosu oda ortasından serbest/doğrudan gitmeli.");
    }

    // Grid X ve Y aralıkları ayrı girildiğinde dikdörtgen grid üretilmeli.
    public static void GridXveYAraliklariAyriCalisir()
    {
        var poligon = new List<Point2D> { new(0, 0), new(8, 0), new(8, 8), new(0, 8) };

        // 2m x 4m dikdörtgen grid: x'te 4 sütun (1,3,5,7), y'de 2 satır (2,6) → 8 nokta
        var dikdortgen = Kst.Acoustics.Core.Electric.Ortak.YerlesimGeometri
            .IzgaraAdaylariUret(poligon, 2.0, 4.0, 0.2);
        if (dikdortgen.Count != 8)
            throw new InvalidOperationException($"2x4 m grid 8 nokta üretmeli; bulunan: {dikdortgen.Count}.");
        if (dikdortgen.Select(p => Math.Round(p.Y, 3)).Distinct().Count() != 2)
            throw new InvalidOperationException("Y=4 m aralığında 2 satır beklenir.");
        if (dikdortgen.Select(p => Math.Round(p.X, 3)).Distinct().Count() != 4)
            throw new InvalidOperationException("X=2 m aralığında 4 sütun beklenir.");

        // Tek adım (kare) overload eski davranışı korumalı: 2 m → 4x4 = 16 nokta
        var kare = Kst.Acoustics.Core.Electric.Ortak.YerlesimGeometri
            .IzgaraAdaylariUret(poligon, 2.0, 0.2);
        if (kare.Count != 16)
            throw new InvalidOperationException($"2 m kare grid 16 nokta üretmeli; bulunan: {kare.Count}.");

        // Mahal satırı: EfektifGridY — genel ayar / oda-özel ayrımı
        var satir = new BpcMahalSatiri { GridAraligiM = 2.0, GridAraligiYM = 4.0 };
        var ayar = satir.HucreGetirVeyaOlustur("oda1");
        ayar.GenelAyarKullan = true;
        if (satir.EfektifGridY("oda1") != 4.0)
            throw new InvalidOperationException("Genel ayar modunda satırın GridAraligiYM değeri dönmeli.");
        ayar.GenelAyarKullan = false;
        ayar.GridAraligiYM = 1.5;
        if (satir.EfektifGridY("oda1") != 1.5)
            throw new InvalidOperationException("Oda-özel modda hücrenin GridAraligiYM değeri dönmeli.");
        ayar.GridAraligiYM = null;
        if (satir.EfektifGridY("oda1") != null)
            throw new InvalidOperationException("Oda-özel modda boş Y → null (X ile aynı) dönmeli.");
    }

    // Mahal listesinde "Sensör" işaretli oda-özel hücre: her armatüre ayrı sensör
    // eklenir, duvar anahtarı oluşmaz ve her armatür kendi sensörü üzerinden oda
    // buatına bağlanır.
    public static void MahalSensorluHucreHerArmatureSensorEklerVeBuataBaglar()
    {
        var kutuphane = new BasitProjeCizKutuphaneServisi();
        var tanim = new BpcArmatürTanim
        {
            Id = "sensor-test-armatur",
            Ad = "Sensör Test LED",
            BlokAdi = "E4P_U_LED",
            SembolAdi = "U",
            MontajTipi = "Tavan",
            GucW = 18
        };
        kutuphane.Armatürler.Add(tanim);

        var kat = new BasitProjeKat();
        var oda = new BasitProjeOda
        {
            Ad = "Sensör Oda",
            Poligon = [new(0, 0), new(6, 0), new(6, 6), new(0, 6)],
            MahalListesineDahil = true
        };
        kat.Odalar.Add(oda);

        var satir = new BpcMahalSatiri
        {
            Tip = BpcElemanTipi.Armatür,
            ArmatürTanimId = tanim.Id,
            ArmatürTanimAd = tanim.Ad,
            YerlesimTipi = "Adet"
        };
        var ayar = satir.HucreGetirVeyaOlustur(oda.Id);
        ayar.Aktif = true;
        ayar.GenelAyarKullan = false; // oda-özel mod
        ayar.YerlesimTipi = "Adet";
        ayar.Adet = 3;
        ayar.Sensorlu = true;
        kat.MahalListesi.Add(satir);

        var servis = new BasitProjeCizOtomatikYerlestirServisi(new BasitProjeCizGeometriServisi());
        var (yeni, _) = servis.TumMahalListesindenYerlestir(kat, kutuphane, sadecaEksikleri: false);
        foreach (var e in yeni) kat.Elemanlar.Add(e);

        var armaturler = kat.Elemanlar.Where(e => BasitProjeCizKabloServisi.ElemanArmaturMi(e.Tip)).ToList();
        var sensorler  = kat.Elemanlar.Where(e => e.Tip == BpcElemanTipi.Sensör).ToList();
        var anahtarlar = kat.Elemanlar.Where(e => e.Tip == BpcElemanTipi.Anahtar).ToList();
        var buatlar    = kat.Elemanlar.Where(e => e.Tip == BpcElemanTipi.Buat).ToList();

        if (armaturler.Count != 3)
            throw new InvalidOperationException($"3 armatür bekleniyordu; bulunan: {armaturler.Count}.");
        if (armaturler.Any(a => !a.SensorKontrollu))
            throw new InvalidOperationException("Sensörlü hücreden gelen armatürler SensorKontrollu işaretlenmeli.");
        if (sensorler.Count != 3)
            throw new InvalidOperationException($"Her armatüre ayrı sensör bekleniyordu (3); bulunan: {sensorler.Count}.");
        if (sensorler.Any(s => !s.SensorKontrollu))
            throw new InvalidOperationException("Üretilen sensörler SensorKontrollu işaretlenmeli.");
        if (anahtarlar.Count != 0)
            throw new InvalidOperationException($"Sensörlü grupta duvar anahtarı oluşmamalı; bulunan: {anahtarlar.Count}.");
        if (buatlar.Count < 1)
            throw new InvalidOperationException("Sensör kontrollü armatürler için en az 1 oda buatı oluşmalı.");

        // Kablolama: her armatür kendi sensörü üzerinden buata bağlanmalı.
        BasitProjeCizKabloServisi.KatAydinlatmasiniOtomatikKablola(kat);

        var sensorIds = sensorler.Select(s => s.Id).ToHashSet();
        var buatId = buatlar[0].Id;
        var sensorDevreleri = kat.Devreler
            .Where(d => sensorIds.Contains(d.AnahtarElemanId ?? ""))
            .ToList();

        if (sensorDevreleri.Count != 3)
            throw new InvalidOperationException($"3 sensör devresi bekleniyordu; bulunan: {sensorDevreleri.Count}.");

        var baglananArmaturler = sensorDevreleri.SelectMany(d => d.ArmaturElemanIds).Distinct().ToList();
        if (baglananArmaturler.Count != 3)
            throw new InvalidOperationException($"3 armatürün tamamı sensör devrelerine bağlanmalı; bağlanan: {baglananArmaturler.Count}.");

        if (sensorDevreleri.Any(d => d.BuatId != buatId))
            throw new InvalidOperationException("Her sensör devresinin buatı oda buatı olmalı.");
    }

    public static void VavienTravelCableFollowsConnectedWalls()
    {
        var odalar = new List<BasitProjeOda>
        {
            new()
            {
                Ad = "Alt hol",
                Poligon = [new(0, 0), new(2, 0), new(2, 2), new(0, 2)]
            },
            new()
            {
                Ad = "Ust hol",
                Poligon = [new(2, 2), new(8, 2), new(8, 4), new(2, 4)]
            }
        };

        var start = new Point2D(0, 1);
        var end = new Point2D(8, 3);
        var rota = BasitProjeCizKabloServisi.DuvarBoyuncaRota(start, end, odalar);

        if (rota.Count < 4)
            throw new InvalidOperationException($"Vavien seyahat kablosu duvar kırıklarını izlemeli. Nokta sayısı: {rota.Count}.");

        var ortakKose = new Point2D(2, 2);
        if (!rota.Any(p => Distance(p, ortakKose) < 0.25) &&
            !Enumerable.Range(0, rota.Count - 1).Any(i => PointToSegmentDistance(ortakKose, rota[i], rota[i + 1]) < 0.25))
            throw new InvalidOperationException("Rota bağlı oda köşesinden geçmedi; doğrudan köprü çiziyor olabilir.");

        var direct = Distance(start, end);
        var routeLength = RouteLength(rota);
        if (routeLength <= direct * 1.10)
            throw new InvalidOperationException("Duvar rotası düz çizgiye fazla yakın; duvar çevresini dolaşmıyor.");
    }

    public static void DisconnectedRoomBuatRouteUsesRightAngleBridge()
    {
        var odalar = new List<BasitProjeOda>
        {
            new()
            {
                Ad = "Sol oda",
                Poligon = [new(0, 0), new(2, 0), new(2, 2), new(0, 2)]
            },
            new()
            {
                Ad = "Sag oda",
                Poligon = [new(4, 2), new(6, 2), new(6, 4), new(4, 4)]
            }
        };

        var rota = BasitProjeCizKabloServisi.DuvarBoyuncaRota(
            new Point2D(0, 1),
            new Point2D(6, 3),
            odalar);

        if (rota.Count < 6)
            throw new InvalidOperationException($"Farklı oda-buat rotası kırıklı olmalı. Nokta sayısı: {rota.Count}.");

        for (int i = 1; i < rota.Count - 2; i++)
        {
            var a = rota[i];
            var b = rota[i + 1];
            if (Distance(a, b) > 0.15 && Math.Abs(a.X - b.X) > 1e-6 && Math.Abs(a.Y - b.Y) > 1e-6)
                throw new InvalidOperationException(
                    $"Farklı oda fallback rotası çapraz iç segment üretmemeli. Segment {i}: ({a.X:F2},{a.Y:F2}) -> ({b.X:F2},{b.Y:F2})");
        }
    }

    public static void SameWallBuatRouteUsesShortestStraightPath()
    {
        var odalar = new List<BasitProjeOda>
        {
            new()
            {
                Ad = "Ust oda",
                Poligon = [new(0, 1), new(4, 1), new(4, 4), new(0, 4)]
            },
            new()
            {
                Ad = "Alt oda",
                Poligon = [new(0, -2), new(4, -2), new(4, 1), new(0, 1)]
            }
        };

        var start = new Point2D(4, 3);
        var end = new Point2D(4, -1);
        var rota = BasitProjeCizKabloServisi.DuvarBoyuncaRota(start, end, odalar);

        var routeLength = RouteLength(rota);
        var direct = Distance(start, end);
        if (routeLength > direct + 0.2)
            throw new InvalidOperationException($"Aynı duvar hattındaki buat rotası uzamamalı. Rota: {routeLength:F2}, direkt: {direct:F2}.");

        if (rota.Any(p => Math.Abs(p.X - start.X) > 0.15))
            throw new InvalidOperationException("Aynı duvar hattındaki rota çevreden dolaşmamalı.");
    }

    public static void OverlappingCableSegmentsAreVisuallySeparated()
    {
        var kablolar = new List<BpcKablo>
        {
            new()
            {
                DevreId = "1",
                Tip = BpcKabloTipi.GucKablosu,
                Rota = [new(1, 0), new(1, 4)]
            },
            new()
            {
                DevreId = "2",
                Tip = BpcKabloTipi.GucKablosu,
                Rota = [new(1, 0), new(1, 4)]
            }
        };

        var segs = BasitProjeCizKabloServisi.KabloSegmentleri(kablolar);
        if (segs.Count != 2)
            throw new InvalidOperationException("İki kablo segmenti bekleniyordu.");

        var d = Math.Abs(segs[0].A.X - segs[1].A.X) + Math.Abs(segs[0].A.Y - segs[1].A.Y);
        if (d < 0.035)
            throw new InvalidOperationException("Üst üste binen kablo segmentleri görselde ayrılmalı.");
    }

    // EnYakinDuvar: kare oda kenarlarında iç normal her zaman oda içine bakmalı ve
    // duvara snap sembol döndürme açısı (θ=atan2(-nx,ny)) beklenen yönü vermeli.
    public static void EnYakinDuvarIcNormalVeDonusAcisiDogru()
    {
        var kare = new List<Point2D> { new(0, 0), new(10, 0), new(10, 10), new(0, 10) };

        void Kontrol(double x, double y, double beklenenNx, double beklenenNy, double beklenenAci, string ad)
        {
            var (_, _, _, _, nx, ny, _) = Kst.Acoustics.Core.Electric.Ortak.YerlesimGeometri.EnYakinDuvar(kare, x, y);
            if (Math.Abs(nx - beklenenNx) > 1e-6 || Math.Abs(ny - beklenenNy) > 1e-6)
                throw new InvalidOperationException($"{ad}: iç normal ({nx:F2},{ny:F2}) beklenen ({beklenenNx},{beklenenNy}) değil.");
            double aci = Math.Atan2(-nx, ny) * 180.0 / Math.PI;
            if (Math.Abs(((aci - beklenenAci + 540) % 360) - 180) > 0.5)
                throw new InvalidOperationException($"{ad}: dönüş açısı {aci:F1}° beklenen {beklenenAci}° değil.");
        }

        Kontrol(5, 0.3, 0, 1, 0, "alt duvar");     // iç normal yukarı → 0°
        Kontrol(9.7, 5, -1, 0, 90, "sağ duvar");   // iç normal sola → 90°
        Kontrol(5, 9.7, 0, -1, 180, "üst duvar");  // iç normal aşağı → 180°
        Kontrol(0.3, 5, 1, 0, -90, "sol duvar");   // iç normal sağa → -90°
    }

    // A2/A3 regresyonu: oda merkezi alan-ağırlıklı centroid olmalı; bir kenarda
    // yoğunlaşan köşeler merkezi kaydırmamalı. Boş poligon (0,0) döner (bölme koruması).
    public static void OdaMerkeziAlanAgirlikliCentroiddir()
    {
        // 10×10 kare, alt kenarda fazladan doğrusal köşeler → köşe ortalaması (5, 2.5)
        // olur; gerçek centroid (5, 5).
        var oda = new BasitProjeOda
        {
            Poligon = [new(0, 0), new(2, 0), new(4, 0), new(6, 0), new(8, 0),
                       new(10, 0), new(10, 10), new(0, 10)]
        };
        var m = oda.Merkez;
        if (Math.Abs(m.X - 5.0) > 1e-6 || Math.Abs(m.Y - 5.0) > 1e-6)
            throw new InvalidOperationException($"Oda merkezi (5,5) olmalı (alan-ağırlıklı). Bulunan: ({m.X:F3},{m.Y:F3}).");
        if (Math.Abs(m.Y - 2.5) < 1e-6)
            throw new InvalidOperationException("Oda merkezi köşe ortalamasına (2.5) düşmemeli.");

        var bos = new BasitProjeOda { Poligon = [] };
        var mb = bos.Merkez;
        if (mb.X != 0 || mb.Y != 0)
            throw new InvalidOperationException($"Boş poligon merkezi (0,0) olmalı. Bulunan: ({mb.X},{mb.Y}).");
    }

    // DXF çıktı: duvara snap edilmiş (duvar montajlı) sembol, tabanı duvara paralel olacak
    // şekilde döndürülmeli. Sağ duvardaki priz ≈ 90°, tavan montajlı armatür 0° (döndürülmez).
    public static void DxfCiktiDuvarSemboluDuvaraParalelDondurur()
    {
        var tmpDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "eye4pro_dxf_rot_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(tmpDir);
        var path = System.IO.Path.Combine(tmpDir, "rot.dxf");
        try
        {
            var kutuphane = new BasitProjeCizKutuphaneServisi();
            var kat = new BasitProjeKat { KatNo = 1 };
            var oda = new BasitProjeOda { Ad = "O", Poligon = [new(0, 0), new(10, 0), new(10, 10), new(0, 10)] };
            kat.Odalar.Add(oda);

            // Sağ duvara yakın duvar montajlı priz → ~90°
            var priz = new BasitProjeEleman { Tip = BpcElemanTipi.Priz, BlokAdi = "E4P_PRIZ", MontajTipi = "Duvar", OdaId = oda.Id, X = 9.8, Y = 5 };
            // Oda ortasında tavan montajlı armatür → 0° (döndürülmez)
            var arm = new BasitProjeEleman { Tip = BpcElemanTipi.Armatür, BlokAdi = "E4P_LED_PANEL_60x60", MontajTipi = "Tavan", OdaId = oda.Id, X = 5, Y = 5 };
            kat.Elemanlar.Add(priz);
            kat.Elemanlar.Add(arm);

            var tanimlar = kutuphane.Prizler.Concat(kutuphane.Armatürler).ToList();
            new BasitProjeCizDxfServisi().DxfCiktiAl(kat, path, tanimlar);

            var doc = netDxf.DxfDocument.Load(path);
            var inserts = doc.Entities.Inserts.ToList();

            var prizInsert = inserts.FirstOrDefault(i => string.Equals(i.Block.Name, "E4P_PRIZ", StringComparison.OrdinalIgnoreCase));
            if (prizInsert == null)
                throw new InvalidOperationException("DXF'te E4P_PRIZ INSERT bulunamadı.");
            double rot = ((prizInsert.Rotation % 360) + 360) % 360;
            if (Math.Abs(rot - 90) > 1.0)
                throw new InvalidOperationException($"Sağ duvardaki priz ~90° döndürülmeli; bulunan {rot:F1}°.");

            var armInsert = inserts.FirstOrDefault(i => string.Equals(i.Block.Name, "E4P_LED_PANEL_60x60", StringComparison.OrdinalIgnoreCase));
            if (armInsert != null)
            {
                double armRot = ((armInsert.Rotation % 360) + 360) % 360;
                if (armRot > 0.5 && armRot < 359.5)
                    throw new InvalidOperationException($"Tavan montajlı armatür döndürülmemeli; bulunan {armRot:F1}°.");
            }
        }
        finally
        {
            try { System.IO.Directory.Delete(tmpDir, recursive: true); } catch { }
        }
    }

    public static void BuatPanoRoutesStayInsideSelectedRoomsAndRenumber()
    {
        var kat = new BasitProjeKat();
        var oda1 = new BasitProjeOda { Ad = "Buat oda", Poligon = [new(0, 0), new(3, 0), new(3, 3), new(0, 3)] };
        var oda2 = new BasitProjeOda { Ad = "Geçiş", Poligon = [new(3, 0), new(6, 0), new(6, 3), new(3, 3)] };
        var oda3 = new BasitProjeOda { Ad = "Pano oda", Poligon = [new(6, 0), new(9, 0), new(9, 3), new(6, 3)] };
        kat.Odalar.Add(oda1);
        kat.Odalar.Add(oda2);
        kat.Odalar.Add(oda3);

        var buat1 = new BasitProjeEleman { Tip = BpcElemanTipi.Buat, Ad = "B1", X = 1, Y = 1.5 };
        var buat2 = new BasitProjeEleman { Tip = BpcElemanTipi.Buat, Ad = "B2", X = 1, Y = 2.2 };
        var pano = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, Ad = "KP", X = 8, Y = 1.5 };
        kat.Elemanlar.Add(buat1);
        kat.Elemanlar.Add(buat2);
        kat.Elemanlar.Add(pano);

        var b1 = BasitProjeCizKabloServisi.BuatPanoBagla(kat, buat1.Id, pano.Id, [oda2.Id]);
        BasitProjeCizKabloServisi.BuatPanoBagla(kat, buat2.Id, pano.Id, [oda2.Id]);
        BasitProjeCizKabloServisi.BuatPanoRotalariniHesapla(kat);

        var panoKablolari = kat.Kablolar.Where(k => k.Tip == BpcKabloTipi.PanoKablosu).ToList();
        if (panoKablolari.Count != 2)
            throw new InvalidOperationException("İki buat-pano kablosu bekleniyordu.");
        if (panoKablolari.Select(k => k.PanoSira).OrderBy(x => x).SequenceEqual([1, 2]) == false)
            throw new InvalidOperationException("Aydınlatma pano kabloları A1/A2 olarak numaralanmalı.");
        if (panoKablolari.Any(k => k.Rota.Any(p => p.Y < -0.05 || p.Y > 3.05)))
            throw new InvalidOperationException("Buat-pano rotası seçili geçiş odaları dışına taşmamalı.");

        kat.BuatPanoBaglantilari.Remove(b1);
        BasitProjeCizKabloServisi.BuatPanoRotalariniHesapla(kat);
        var kalan = kat.Kablolar.Single(k => k.Tip == BpcKabloTipi.PanoKablosu);
        if (kalan.PanoSira != 1)
            throw new InvalidOperationException("Hat silinince aydınlatma pano numarası yeniden A1 olmalı.");
    }

    public static void BuatPanoRouteDoesNotUseLongDiagonalAcrossRooms()
    {
        var kat = new BasitProjeKat();
        var oda1 = new BasitProjeOda { Ad = "Buat oda", Poligon = [new(0, 0), new(3, 0), new(3, 3), new(0, 3)] };
        var oda2 = new BasitProjeOda { Ad = "Geçiş", Poligon = [new(3, 0), new(6, 0), new(6, 3), new(3, 3)] };
        var oda3 = new BasitProjeOda { Ad = "Pano oda", Poligon = [new(6, 0), new(9, 0), new(9, 3), new(6, 3)] };
        kat.Odalar.Add(oda1);
        kat.Odalar.Add(oda2);
        kat.Odalar.Add(oda3);

        var buat = new BasitProjeEleman { Tip = BpcElemanTipi.Buat, Ad = "B", X = 1, Y = 2.5 };
        var pano = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, Ad = "KP", X = 8, Y = 0.5 };
        kat.Elemanlar.Add(buat);
        kat.Elemanlar.Add(pano);

        BasitProjeCizKabloServisi.BuatPanoBagla(kat, buat.Id, pano.Id, [oda2.Id]);
        BasitProjeCizKabloServisi.BuatPanoRotalariniHesapla(kat);

        var kablo = kat.Kablolar.Single(k => k.Tip == BpcKabloTipi.PanoKablosu);
        for (int i = 0; i < kablo.Rota.Count - 1; i++)
        {
            var a = kablo.Rota[i];
            var b = kablo.Rota[i + 1];
            if (Distance(a, b) > 0.15 && Math.Abs(a.X - b.X) > 1e-6 && Math.Abs(a.Y - b.Y) > 1e-6)
                throw new InvalidOperationException("Buat-pano farklı odalar arasında uzun çapraz segment kullanmamalı.");
        }
    }

    public static void BuatPanoRouteDoesNotEnterUnselectedRooms()
    {
        var kat = new BasitProjeKat();
        var sol = new BasitProjeOda { Ad = "Buat oda", Poligon = [new(0, 0), new(3, 0), new(3, 3), new(0, 3)] };
        var yasak = new BasitProjeOda { Ad = "İzinsiz oda", Poligon = [new(3, 0), new(6, 0), new(6, 3), new(3, 3)] };
        var sag = new BasitProjeOda { Ad = "Pano oda", Poligon = [new(6, 0), new(9, 0), new(9, 3), new(6, 3)] };
        kat.Odalar.Add(sol);
        kat.Odalar.Add(yasak);
        kat.Odalar.Add(sag);

        var buat = new BasitProjeEleman { Tip = BpcElemanTipi.Buat, Ad = "B", X = 1, Y = 1.5 };
        var pano = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, Ad = "KP", X = 8, Y = 1.5 };
        kat.Elemanlar.Add(buat);
        kat.Elemanlar.Add(pano);

        BasitProjeCizKabloServisi.BuatPanoBagla(kat, buat.Id, pano.Id, []);
        var uyarilar = BasitProjeCizKabloServisi.BuatPanoRotalariniHesapla(kat);

        // Yeni sözleşme: izinli geçiş yolu yoksa BOŞ rotalı kablo üretilmez;
        // bağlantı korunur ve kullanıcıya gösterilecek uyarı döner.
        if (kat.Kablolar.Any(k => k.Tip == BpcKabloTipi.PanoKablosu))
            throw new InvalidOperationException("İzinli geçiş yolu yoksa buat-pano kablosu üretilmemeli (yasak odadan fallback dahil).");
        if (uyarilar.Count != 1)
            throw new InvalidOperationException("Rota bulunamayınca kullanıcıya bir uyarı dönmeli.");
        if (kat.BuatPanoBaglantilari.Count != 1)
            throw new InvalidOperationException("Rota bulunamasa da buat-pano bağlantısı korunmalı.");
    }

    public static void PanoBeslemeRouteUsesSelectedRoomsAndWalls()
    {
        var kat = new BasitProjeKat();
        var sol = new BasitProjeOda { Ad = "Tali pano oda", Poligon = [new(0, 0), new(3, 0), new(3, 3), new(0, 3)] };
        var gecis = new BasitProjeOda { Ad = "Geçiş holü", Poligon = [new(3, 0), new(6, 0), new(6, 3), new(3, 3)] };
        var sag = new BasitProjeOda { Ad = "Kat pano oda", Poligon = [new(6, 0), new(9, 0), new(9, 3), new(6, 3)] };
        var yasak = new BasitProjeOda { Ad = "Alt izinsiz oda", Poligon = [new(3, -3), new(6, -3), new(6, 0), new(3, 0)] };
        kat.Odalar.Add(sol);
        kat.Odalar.Add(gecis);
        kat.Odalar.Add(sag);
        kat.Odalar.Add(yasak);

        var tali = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, BlokAdi = "E4P_TALI_PANO", Ad = "TP", X = 1, Y = 1.5 };
        var katPano = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, BlokAdi = "E4P_KAT_PANOSU", Ad = "KP", X = 8, Y = 1.5 };
        kat.Elemanlar.Add(tali);
        kat.Elemanlar.Add(katPano);

        var rota = BasitProjeCizKabloServisi.PanoBeslemeRota(kat, tali, katPano, [gecis.Id]);
        if (rota.Count < 4)
            throw new InvalidOperationException($"Tali pano besleme rotası duvar kırıklarıyla çizilmeli. Nokta sayısı: {rota.Count}.");
        if (RouteEntersRoom(rota, yasak))
            throw new InvalidOperationException("Tali pano besleme kablosu Geçiş seçilmeyen odaya girmemeli.");

        for (int i = 1; i < rota.Count - 2; i++)
        {
            var a = rota[i];
            var b = rota[i + 1];
            if (Distance(a, b) > 0.15 && Math.Abs(a.X - b.X) > 1e-6 && Math.Abs(a.Y - b.Y) > 1e-6)
                throw new InvalidOperationException("Tali pano besleme rotası oda içinden çapraz geçmemeli.");
        }
    }

    public static void PanoBuatRoutesUseElementRoomIdsOnRoomBoundary()
    {
        var kat = new BasitProjeKat();
        var sol = new BasitProjeOda { Ad = "Buat oda", Poligon = [new(0, 0), new(3, 0), new(3, 3), new(0, 3)] };
        var gecis = new BasitProjeOda { Ad = "Geçiş holü", Poligon = [new(3, 0), new(6, 0), new(6, 3), new(3, 3)] };
        var sag = new BasitProjeOda { Ad = "Pano oda", Poligon = [new(6, 0), new(9, 0), new(9, 3), new(6, 3)] };
        kat.Odalar.Add(sol);
        kat.Odalar.Add(gecis);
        kat.Odalar.Add(sag);

        var buat = new BasitProjeEleman { Tip = BpcElemanTipi.Buat, Ad = "B", OdaId = sol.Id, X = 3, Y = 1.5 };
        var pano = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, BlokAdi = "E4P_KAT_PANOSU", Ad = "KP", OdaId = sag.Id, X = 6, Y = 1.5 };
        kat.Elemanlar.Add(buat);
        kat.Elemanlar.Add(pano);

        BasitProjeCizKabloServisi.BuatPanoBagla(kat, buat.Id, pano.Id, [gecis.Id]);
        BasitProjeCizKabloServisi.BuatPanoRotalariniHesapla(kat);

        var kablo = kat.Kablolar.Single(k => k.Tip == BpcKabloTipi.PanoKablosu);
        if (kablo.Rota.Count < 4)
            throw new InvalidOperationException("Duvar/sınır üstündeki buat-pano elemanları OdaId ile rota üretebilmeli.");

        var besleme = BasitProjeCizKabloServisi.PanoBeslemeRota(kat, buat, pano, [gecis.Id]);
        if (besleme.Count < 4)
            throw new InvalidOperationException("Duvar/sınır üstündeki pano besleme elemanları OdaId ile rota üretebilmeli.");
    }

    public static void PanoBeslemeRouteWithoutAllowedTransitionStaysEmpty()
    {
        var kat = new BasitProjeKat();
        var sol = new BasitProjeOda { Ad = "Tali pano oda", Poligon = [new(0, 0), new(3, 0), new(3, 3), new(0, 3)] };
        var yasak = new BasitProjeOda { Ad = "İzinsiz oda", Poligon = [new(3, 0), new(6, 0), new(6, 3), new(3, 3)] };
        var sag = new BasitProjeOda { Ad = "Kat pano oda", Poligon = [new(6, 0), new(9, 0), new(9, 3), new(6, 3)] };
        kat.Odalar.Add(sol);
        kat.Odalar.Add(yasak);
        kat.Odalar.Add(sag);

        var tali = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, BlokAdi = "E4P_TALI_PANO", Ad = "TP", OdaId = sol.Id, X = 1, Y = 1.5 };
        var katPano = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, BlokAdi = "E4P_KAT_PANOSU", Ad = "KP", OdaId = sag.Id, X = 8, Y = 1.5 };
        kat.Elemanlar.Add(tali);
        kat.Elemanlar.Add(katPano);

        var rota = BasitProjeCizKabloServisi.PanoBeslemeRota(kat, tali, katPano, []);
        if (rota.Count != 0)
            throw new InvalidOperationException("Geçiş izni yoksa pano besleme rotası yasak odadan düz fallback çizmemeli.");
    }

    public static void PanoBuatRouteBridgesSmallOpeningsBetweenAllowedRooms()
    {
        var kat = new BasitProjeKat();
        var sol = new BasitProjeOda { Ad = "Buat oda", Poligon = [new(0, 0), new(3, 0), new(3, 3), new(0, 3)] };
        var gecis = new BasitProjeOda { Ad = "Geçiş", GecisSecili = true, Poligon = [new(3.45, 0), new(6.45, 0), new(6.45, 3), new(3.45, 3)] };
        var sag = new BasitProjeOda { Ad = "Pano oda", Poligon = [new(6.9, 0), new(9.9, 0), new(9.9, 3), new(6.9, 3)] };
        kat.Odalar.Add(sol);
        kat.Odalar.Add(gecis);
        kat.Odalar.Add(sag);

        var buat = new BasitProjeEleman { Tip = BpcElemanTipi.Buat, Ad = "B", OdaId = sol.Id, X = 1, Y = 1.5 };
        var pano = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, BlokAdi = "E4P_KAT_PANOSU", Ad = "KP", OdaId = sag.Id, X = 8.9, Y = 1.5 };
        kat.Elemanlar.Add(buat);
        kat.Elemanlar.Add(pano);

        BasitProjeCizKabloServisi.BuatPanoBagla(kat, buat.Id, pano.Id, [gecis.Id]);
        BasitProjeCizKabloServisi.BuatPanoRotalariniHesapla(kat);

        var kablo = kat.Kablolar.Single(k => k.Tip == BpcKabloTipi.PanoKablosu);
        if (kablo.Rota.Count < 4)
        {
            var rotaText = string.Join(" -> ", kablo.Rota.Select(p => $"{p.X:F2},{p.Y:F2}"));
            throw new InvalidOperationException(
                $"Küçük açıklık/kapı boşluğu olan izinli odalar arasında buat-pano rotası üretilebilmeli. Nokta sayısı: {kablo.Rota.Count}; Rota: {rotaText}");
        }

        for (int i = 0; i < kablo.Rota.Count - 1; i++)
        {
            var a = kablo.Rota[i];
            var b = kablo.Rota[i + 1];
            if (Distance(a, b) > 1.6 && Math.Abs(a.X - b.X) > 1e-6 && Math.Abs(a.Y - b.Y) > 1e-6)
                throw new InvalidOperationException("Küçük açıklık köprüsü uzun çapraz rotaya dönüşmemeli.");
        }
    }

    public static void MekanikRouteFollowsWalls()
    {
        var kat = new BasitProjeKat();
        kat.Odalar.Add(new BasitProjeOda
        {
            Ad = "Mekanik oda",
            Poligon = [new(0, 0), new(6, 0), new(6, 4), new(0, 4)]
        });

        var klima = new BasitProjeEleman { Tip = BpcElemanTipi.KlimaIcUnite, Ad = "KİÜ", X = 0, Y = 3 };
        var pano = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, Ad = "KP", X = 6, Y = 1 };
        kat.Elemanlar.Add(klima);
        kat.Elemanlar.Add(pano);
        kat.MekanikDevreleri.Add(new BpcMekanikDevre { KlimaElemanId = klima.Id, PanoId = pano.Id });

        BasitProjeCizKabloServisi.MekanikRotalariniHesapla(kat);

        var kablo = kat.Kablolar.Single(k => k.Tip == BpcKabloTipi.MekanikKablosu);
        if (kablo.Rota.Count < 4)
            throw new InvalidOperationException($"Mekanik kablo duvar rotası üretmeli. Nokta sayısı: {kablo.Rota.Count}.");

        for (int i = 0; i < kablo.Rota.Count - 1; i++)
        {
            var a = kablo.Rota[i];
            var b = kablo.Rota[i + 1];
            if (Distance(a, b) > 0.15 && Math.Abs(a.X - b.X) > 1e-6 && Math.Abs(a.Y - b.Y) > 1e-6)
                throw new InvalidOperationException("Mekanik kablo oda içinden uzun çapraz segment kullanmamalı.");
        }
    }

    public static void PanoIcerikLinyeArmaturGucVeSigortayiOzetler()
    {
        var kat = new BasitProjeKat();
        kat.Odalar.Add(new BasitProjeOda
        {
            Ad = "Oda",
            Poligon = [new(0, 0), new(6, 0), new(6, 4), new(0, 4)]
        });

        var tanim = new BpcArmatürTanim { Ad = "LED Panel", BlokAdi = "E4P_LED", GucW = 100 };

        var anahtar = new BasitProjeEleman { Tip = BpcElemanTipi.Anahtar, Ad = "A1", X = 0, Y = 1 };
        var arm1 = new BasitProjeEleman { Tip = BpcElemanTipi.Armatür, Ad = "L-1", ArmatürTanimId = tanim.Id, X = 2, Y = 2 };
        var arm2 = new BasitProjeEleman { Tip = BpcElemanTipi.Armatür, Ad = "L-2", ArmatürTanimId = tanim.Id, X = 4, Y = 2 };
        var buat = new BasitProjeEleman { Tip = BpcElemanTipi.Buat, Ad = "B1", X = 5, Y = 3 };
        var pano = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, Ad = "KP", X = 1, Y = 2 };
        kat.Elemanlar.Add(anahtar);
        kat.Elemanlar.Add(arm1);
        kat.Elemanlar.Add(arm2);
        kat.Elemanlar.Add(buat);
        kat.Elemanlar.Add(pano);

        var devre = BasitProjeCizKabloServisi.EnsureDevre(kat, anahtar.Id);
        BasitProjeCizKabloServisi.ArmaturAtama(kat, devre, arm1.Id);
        BasitProjeCizKabloServisi.ArmaturAtama(kat, devre, arm2.Id);
        BasitProjeCizKabloServisi.ButaAta(kat, devre, buat.Id);
        BasitProjeCizKabloServisi.BuatPanoBagla(kat, buat.Id, pano.Id, []);
        BasitProjeCizKabloServisi.BuatPanoRotalariniHesapla(kat);

        var ozetler = BasitProjeCizPanoServisi.PanoOzetleri(kat, [tanim]);
        if (ozetler.Count != 1)
            throw new InvalidOperationException($"Tek pano özeti bekleniyordu. Bulunan: {ozetler.Count}.");

        var ozet = ozetler[0];
        if (ozet.Linyeler.Count != 1)
            throw new InvalidOperationException($"Tek linye bekleniyordu. Bulunan: {ozet.Linyeler.Count}.");

        var linye = ozet.Linyeler[0];
        if (linye.ElemanSayisi != 2)
            throw new InvalidOperationException($"Linyede 2 armatür bekleniyordu. Bulunan: {linye.ElemanSayisi}.");
        if (Math.Abs(linye.GucW - 200) > 1e-6)
            throw new InvalidOperationException($"Linye gücü 200 W olmalı. Bulunan: {linye.GucW}.");
        if (linye.Sira != 1 || linye.Etiket != "A1")
            throw new InvalidOperationException($"Linye A1 olarak numaralanmalı. Bulunan: {linye.Etiket}.");
        if (linye.SigortaA != 10)
            throw new InvalidOperationException($"200 W aydınlatma için linye sigortası en az 10A olmalı. Bulunan: {linye.SigortaA}A.");

        if (ozet.ElemanToplam != 2)
            throw new InvalidOperationException($"Pano toplam armatürü 2 olmalı. Bulunan: {ozet.ElemanToplam}.");
        if (Math.Abs(ozet.ToplamGucW - 200) > 1e-6)
            throw new InvalidOperationException($"Pano toplam gücü 200 W olmalı. Bulunan: {ozet.ToplamGucW}.");
        if (ozet.GirisSigortaA != 6)
            throw new InvalidOperationException($"Pano giriş sigortası 6A olmalı. Bulunan: {ozet.GirisSigortaA}A.");
    }

    public static void PanoIcerikMekanikGucVeSigortayiOzetler()
    {
        var kat = new BasitProjeKat();
        kat.Odalar.Add(new BasitProjeOda
        {
            Ad = "Mekanik oda",
            Poligon = [new(0, 0), new(6, 0), new(6, 4), new(0, 4)]
        });

        var klimaTanim = new BpcArmatürTanim { Ad = "Klima Dış Ünite", BlokAdi = "E4P_KLIMA_DIS", GucW = 2000 };
        var klima = new BasitProjeEleman { Tip = BpcElemanTipi.KlimaDisUnite, Ad = "KDÜ", BlokAdi = klimaTanim.BlokAdi, X = 0, Y = 2 };
        var pano = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, Ad = "KP", X = 6, Y = 2 };
        kat.Elemanlar.Add(klima);
        kat.Elemanlar.Add(pano);
        kat.MekanikDevreleri.Add(new BpcMekanikDevre { KlimaElemanId = klima.Id, PanoId = pano.Id });
        BasitProjeCizKabloServisi.MekanikRotalariniHesapla(kat);

        var ozet = BasitProjeCizPanoServisi.PanoOzetleri(kat, [klimaTanim]).Single();
        var linye = ozet.Linyeler.Single();
        if (linye.Etiket != "M1")
            throw new InvalidOperationException($"Mekanik linye M1 olmalı. Bulunan: {linye.Etiket}.");
        if (Math.Abs(linye.GucW - 2000) > 1e-6)
            throw new InvalidOperationException($"Mekanik güç 2000 W olmalı. Bulunan: {linye.GucW}.");
        if (linye.SigortaText != "10A C")
            throw new InvalidOperationException($"2000 W klima için 10A C bekleniyor. Bulunan: {linye.SigortaText}.");
        if (Math.Abs(ozet.ToplamGucW - 2000) > 1e-6)
            throw new InvalidOperationException($"Pano toplam mekanik gücü 2000 W olmalı. Bulunan: {ozet.ToplamGucW}.");
    }

    public static void PanoHesabiBosGucteSigortaUretmezVePaftayaGoreFiltreler()
    {
        var kat = new BasitProjeKat();
        var pano = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, Ad = "KP", X = 0, Y = 0 };
        var buat = new BasitProjeEleman { Tip = BpcElemanTipi.Buat, Ad = "B", X = 1, Y = 0 };
        var anahtar = new BasitProjeEleman { Tip = BpcElemanTipi.Anahtar, Ad = "A", X = 2, Y = 0 };
        var arm = new BasitProjeEleman { Tip = BpcElemanTipi.Armatür, Ad = "L", BlokAdi = "BILINMEYEN", X = 3, Y = 0 };
        var klima = new BasitProjeEleman { Tip = BpcElemanTipi.KlimaDisUnite, Ad = "KDÜ", CustomGucW = 2000, X = 4, Y = 0 };
        kat.Elemanlar.Add(pano);
        kat.Elemanlar.Add(buat);
        kat.Elemanlar.Add(anahtar);
        kat.Elemanlar.Add(arm);
        kat.Elemanlar.Add(klima);

        var devre = BasitProjeCizKabloServisi.EnsureDevre(kat, anahtar.Id);
        BasitProjeCizKabloServisi.ArmaturAtama(kat, devre, arm.Id);
        BasitProjeCizKabloServisi.ButaAta(kat, devre, buat.Id);
        BasitProjeCizKabloServisi.BuatPanoBagla(kat, buat.Id, pano.Id, [], "A");
        kat.MekanikDevreleri.Add(new BpcMekanikDevre { KlimaElemanId = klima.Id, PanoId = pano.Id });
        BasitProjeCizKabloServisi.BuatPanoRotalariniHesapla(kat);
        BasitProjeCizKabloServisi.MekanikRotalariniHesapla(kat);

        var ayd = BasitProjeCizPanoServisi.PanoOzetleri(kat, [], BpcPaftaTipi.Aydinlatma).Single();
        if (ayd.Linyeler.Count != 1 || ayd.Linyeler[0].LinyePrefix != "A")
            throw new InvalidOperationException("Aydınlatma paftasında yalnız A linyesi görünmeli.");
        if (ayd.Linyeler[0].SigortaText != "—")
            throw new InvalidOperationException($"0 W aydınlatma linyesi sigorta üretmemeli. Bulunan: {ayd.Linyeler[0].SigortaText}.");
        if (ayd.Linyeler[0].EksikGucTanimSayisi != 1)
            throw new InvalidOperationException("Gücü bulunamayan armatür uyarı sayısına girmeli.");

        var mek = BasitProjeCizPanoServisi.PanoOzetleri(kat, [], BpcPaftaTipi.Mekanik).Single();
        if (mek.Linyeler.Count != 1 || mek.Linyeler[0].LinyePrefix != "M")
            throw new InvalidOperationException("Mekanik paftasında yalnız M linyesi görünmeli.");

        // Pano içeriği UI'ı pafta=null ile çağırır: bir pano fiziksel olarak TÜM linyelerini
        // (A + M; varsa P) birlikte göstermeli — paftaya göre sigortalar kaybolmamalı.
        var hepsi = BasitProjeCizPanoServisi.PanoOzetleri(kat, [], aktifPafta: null).Single();
        if (!hepsi.Linyeler.Any(l => l.LinyePrefix == "A") || !hepsi.Linyeler.Any(l => l.LinyePrefix == "M"))
            throw new InvalidOperationException(
                "Pafta filtresiz (null) pano özeti hem aydınlatma (A) hem mekanik (M) linyelerini içermeli.");
    }

    public static void AyniBuatAydinlatmaVePrizBaglantilariniAyriTutar()
    {
        var kat = new BasitProjeKat();
        var pano = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, Ad = "KP", X = 0, Y = 0 };
        var buat = new BasitProjeEleman { Tip = BpcElemanTipi.Buat, Ad = "B", X = 1, Y = 0 };
        kat.Elemanlar.Add(pano);
        kat.Elemanlar.Add(buat);

        var a = BasitProjeCizKabloServisi.BuatPanoBagla(kat, buat.Id, pano.Id, [], "A");
        var p = BasitProjeCizKabloServisi.BuatPanoBagla(kat, buat.Id, pano.Id, [], "P");
        BasitProjeCizKabloServisi.BuatPanoRotalariniHesapla(kat);

        if (a.Id == p.Id || kat.BuatPanoBaglantilari.Count != 2)
            throw new InvalidOperationException("Aynı buat için A ve P bağlantıları ayrı kayıtlar olmalı.");

        var kablolar = kat.Kablolar.Where(k => k.Tip == BpcKabloTipi.PanoKablosu).ToList();
        if (kablolar.Count != 2)
            throw new InvalidOperationException("Aynı buattan A ve P için iki pano kablosu çıkmalı.");
        if (kablolar.Any(k => k.PanoSira != 1))
            throw new InvalidOperationException("A ve P numaralandırması ayrı ayrı 1'den başlamalı.");
    }

    public static void TrifazePrizPanoHesabinda400VTrifazeAkimKullanir()
    {
        var kat = new BasitProjeKat();
        var pano = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, Ad = "KP", X = 0, Y = 0 };
        var buat = new BasitProjeEleman { Tip = BpcElemanTipi.Buat, Ad = "B", X = 1, Y = 0 };
        var priz = new BasitProjeEleman { Tip = BpcElemanTipi.TrifazePriz, Ad = "3P", CustomGucW = 10000, X = 2, Y = 0 };
        kat.Elemanlar.Add(pano);
        kat.Elemanlar.Add(buat);
        kat.Elemanlar.Add(priz);
        var devre = new BpcPrizDevre { BuatId = buat.Id };
        devre.PrizElemanIds.Add(priz.Id);
        kat.PrizDevreleri.Add(devre);
        BasitProjeCizKabloServisi.BuatPanoBagla(kat, buat.Id, pano.Id, [], "P");
        BasitProjeCizKabloServisi.BuatPanoRotalariniHesapla(kat);

        var linye = BasitProjeCizPanoServisi.PanoOzetleri(kat, [], BpcPaftaTipi.Priz).Single().Linyeler.Single();
        if (linye.SigortaA != 16)
            throw new InvalidOperationException($"10 kW trifaze priz için 16A bekleniyor. Bulunan: {linye.SigortaA}A.");
    }

    public static void PanoNumaralariHerPanodaAyricaBaslar()
    {
        var kat = new BasitProjeKat();
        var pano1 = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, Ad = "KP1", X = 0, Y = 0 };
        var pano2 = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, Ad = "KP2", X = 10, Y = 0 };
        var b1 = new BasitProjeEleman { Tip = BpcElemanTipi.Buat, Ad = "B1", X = 1, Y = 0 };
        var b2 = new BasitProjeEleman { Tip = BpcElemanTipi.Buat, Ad = "B2", X = 11, Y = 0 };
        kat.Elemanlar.Add(pano1);
        kat.Elemanlar.Add(pano2);
        kat.Elemanlar.Add(b1);
        kat.Elemanlar.Add(b2);

        BasitProjeCizKabloServisi.BuatPanoBagla(kat, b1.Id, pano1.Id, [], "A");
        BasitProjeCizKabloServisi.BuatPanoBagla(kat, b2.Id, pano2.Id, [], "A");
        BasitProjeCizKabloServisi.BuatPanoRotalariniHesapla(kat);

        var siralar = kat.Kablolar.Where(k => k.Tip == BpcKabloTipi.PanoKablosu).Select(k => k.PanoSira).ToList();
        if (siralar.Count != 2 || siralar.Any(s => s != 1))
            throw new InvalidOperationException($"Her pano kendi A1 numarasıyla başlamalı. Bulunan: {string.Join(",", siralar)}.");
    }

    public static void DxfEtiketleriAitOlduklariPaftaLayerindaYazar()
    {
        var kat = new BasitProjeKat();
        var arm = new BasitProjeEleman { Tip = BpcElemanTipi.Armatür, Ad = "L", EkranAdi = "L1", X = 1, Y = 1 };
        var klima = new BasitProjeEleman { Tip = BpcElemanTipi.KlimaDisUnite, Ad = "KDÜ", EkranAdi = "KDÜ", X = 2, Y = 1 };
        var data = new BasitProjeEleman { Tip = BpcElemanTipi.DataPriz, Ad = "D", EkranAdi = "D", X = 3, Y = 1 };
        kat.Elemanlar.Add(arm);
        kat.Elemanlar.Add(klima);
        kat.Elemanlar.Add(data);

        kat.BuatPanoBaglantilari.Add(new BpcBuatPanoBaglanti { Id = "A-BAG", LinyePrefix = "A" });
        kat.BuatPanoBaglantilari.Add(new BpcBuatPanoBaglanti { Id = "P-BAG", LinyePrefix = "P" });
        kat.Kablolar.Add(new BpcKablo { DevreId = "A-BAG", Tip = BpcKabloTipi.PanoKablosu, PanoSira = 1, Rota = [new(0, 0), new(1, 0)] });
        kat.Kablolar.Add(new BpcKablo { DevreId = "P-BAG", Tip = BpcKabloTipi.PanoKablosu, PanoSira = 1, Rota = [new(0, 1), new(1, 1)] });
        kat.Kablolar.Add(new BpcKablo { DevreId = "D1", Tip = BpcKabloTipi.ZayifAkimKablosu, SistemAnahtar = BpcPaftaTipi.Data, PanoSira = 1, Rota = [new(0, 2), new(1, 2)] });
        kat.Topraklama.Seritler.Add(new BpcTopraklamaSerit { A = new(0, 5), B = new(1, 5), Tip = BpcTopraklamaSeritTipi.GalvanizSerit });
        kat.Topraklama.Seritler.Add(new BpcTopraklamaSerit { A = new(0, 5.5), B = new(1, 5.5), Tip = BpcTopraklamaSeritTipi.OrguBakir });
        kat.Topraklama.EkTopraklamaVar = true;
        kat.Topraklama.Kaziklar.Add(new Point2D(0.5, 5));
        kat.KabloTavalari.Add(new BpcKabloTavasi
        {
            Ad = "KT1",
            Adet = 2,
            GenislikMm = 100,
            YukseklikMm = 50,
            EtKalinligiMm = 1.2,
            AgirHizmet = true,
            Rota = [new(0, 3), new(1, 3)],
            TaliHatlar =
            [
                new BpcKabloTavasiTaliHat { GenislikMm = 50, Rota = [new(0.5, 4), new(0.5, 3)] }
            ]
        });

        var path = Path.Combine(Path.GetTempPath(), $"e4p-dxf-layer-test-{Guid.NewGuid():N}.dxf");
        try
        {
            new BasitProjeCizDxfServisi().DxfCiktiAl(kat, path, []);
            var doc = DxfDocument.Load(path);
            var texts = doc.Entities.Texts.ToList();

            AssertTextLayer(texts, "L1", "E4P-LIGHTING");
            AssertTextLayer(texts, "KDÜ", "E4P-MECHANICAL");
            AssertTextLayer(texts, "D", "E4P-DATA");
            AssertTextLayer(texts, "A1", "E4P-KABLO-PANO");
            AssertTextLayer(texts, "P1", "E4P-KABLO-PANO-PRIZ");
            AssertTextLayer(texts, "D1", "E4P-KABLO-DATA");
            AssertTextLayer(texts, "KT1-2x100x50x1.2 Ağır Hizmet", "E4P-KABLO-TAVASI");
            AssertTextLayer(texts, "30x3.5 Galv. Topraklama Seridi", "E4P-TOPRAKLAMA");
            AssertTextLayer(texts, "35 mm Orgulu Bakir Ek Topraklama", "E4P-TOPRAKLAMA-BAKIR");

            var tavaCizgileri = doc.Entities.Lines.Count(l => l.Layer.Name == "E4P-KABLO-TAVASI");
            if (tavaCizgileri < 16)
                throw new InvalidOperationException("2 adet yan yana ana/tali kablo tavası DXF'te iki ayrı tava geometrisi olarak çizilmeli.");

            if (texts.Any(t => t.Layer.Name == "E4P-ANNOTATION"))
                throw new InvalidOperationException("Pafta etiketleri ortak E4P-ANNOTATION layerında kalmamalı.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public static void ZayifAkimDataHattiRotaVeMerkezOzeti()
    {
        var kat = new BasitProjeKat();
        kat.Odalar.Add(new BasitProjeOda
        {
            Ad = "Ofis",
            Poligon = [new(0, 0), new(8, 0), new(8, 5), new(0, 5)]
        });

        var dp1  = new BasitProjeEleman { Tip = BpcElemanTipi.DataPriz, Ad = "D-1", X = 1, Y = 0, PaftaTipi = BpcPaftaTipi.Data };
        var dp2  = new BasitProjeEleman { Tip = BpcElemanTipi.DataPriz, Ad = "D-2", X = 3, Y = 0, PaftaTipi = BpcPaftaTipi.Data };
        var rack = new BasitProjeEleman { Tip = BpcElemanTipi.DataRack, Ad = "RACK", X = 8, Y = 4, PaftaTipi = BpcPaftaTipi.Data };
        kat.Elemanlar.Add(dp1);
        kat.Elemanlar.Add(dp2);
        kat.Elemanlar.Add(rack);

        var devre = new BpcSistemDevre { Sistem = BpcPaftaTipi.Data, MerkezId = rack.Id };
        devre.CihazIds.Add(dp1.Id);
        devre.CihazIds.Add(dp2.Id);
        kat.SistemDevreleri.Add(devre);

        BasitProjeCizKabloServisi.TumSistemRotalariniHesapla(kat);

        var kablo = kat.Kablolar.Single(k => k.Tip == BpcKabloTipi.ZayifAkimKablosu);
        if (kablo.SistemAnahtar != BpcPaftaTipi.Data)
            throw new InvalidOperationException($"Kablo sistem anahtarı Data olmalı. Bulunan: {kablo.SistemAnahtar}.");
        if (kablo.PanoSira != 1)
            throw new InvalidOperationException($"Data linyesi 1 numaralanmalı. Bulunan: {kablo.PanoSira}.");
        if (kablo.Rota.Count < 3)
            throw new InvalidOperationException($"Data kablosu duvar rotası üretmeli. Nokta: {kablo.Rota.Count}.");

        var ozet = BasitProjeCizPanoServisi.MerkezOzetleri(kat, BpcPaftaTipi.Data).Single();
        var linye = ozet.Linyeler.Single();
        if (linye.Etiket != "D1")
            throw new InvalidOperationException($"Data linyesi D1 olmalı. Bulunan: {linye.Etiket}.");
        if (linye.ElemanSayisi != 2)
            throw new InvalidOperationException($"Data linyesinde 2 cihaz bekleniyordu. Bulunan: {linye.ElemanSayisi}.");
        if (linye.SigortaText != "—")
            throw new InvalidOperationException($"Zayıf akım linyesinde sigorta gösterilmemeli. Bulunan: {linye.SigortaText}.");
    }

    public static void ZayifAkimCctvYildizAyriHatlar()
    {
        var kat = new BasitProjeKat();
        kat.Odalar.Add(new BasitProjeOda
        {
            Ad = "Koridor",
            Poligon = [new(0, 0), new(10, 0), new(10, 4), new(0, 4)]
        });

        var cam1 = new BasitProjeEleman { Tip = BpcElemanTipi.Kamera, Ad = "CAM-1", X = 2, Y = 2, PaftaTipi = BpcPaftaTipi.Cctv };
        var cam2 = new BasitProjeEleman { Tip = BpcElemanTipi.Kamera, Ad = "CAM-2", X = 6, Y = 2, PaftaTipi = BpcPaftaTipi.Cctv };
        var nvr  = new BasitProjeEleman { Tip = BpcElemanTipi.CctvKayit, Ad = "NVR", X = 10, Y = 3, PaftaTipi = BpcPaftaTipi.Cctv };
        kat.Elemanlar.Add(cam1);
        kat.Elemanlar.Add(cam2);
        kat.Elemanlar.Add(nvr);

        // Yıldız: her kamera kendi hattı
        var d1 = new BpcSistemDevre { Sistem = BpcPaftaTipi.Cctv, MerkezId = nvr.Id };
        d1.CihazIds.Add(cam1.Id);
        var d2 = new BpcSistemDevre { Sistem = BpcPaftaTipi.Cctv, MerkezId = nvr.Id };
        d2.CihazIds.Add(cam2.Id);
        kat.SistemDevreleri.Add(d1);
        kat.SistemDevreleri.Add(d2);

        BasitProjeCizKabloServisi.TumSistemRotalariniHesapla(kat);

        var kablolar = kat.Kablolar.Where(k => k.Tip == BpcKabloTipi.ZayifAkimKablosu).ToList();
        if (kablolar.Count != 2)
            throw new InvalidOperationException($"İki ayrı CCTV hattı bekleniyordu. Bulunan: {kablolar.Count}.");

        var siralar = kablolar.Select(k => k.PanoSira).OrderBy(s => s).ToList();
        if (siralar[0] != 1 || siralar[1] != 2)
            throw new InvalidOperationException($"CCTV hatları C1 ve C2 numaralanmalı. Bulunan: {string.Join(",", siralar)}.");

        var ozet = BasitProjeCizPanoServisi.MerkezOzetleri(kat, BpcPaftaTipi.Cctv).Single();
        if (ozet.Linyeler.Count != 2)
            throw new InvalidOperationException($"NVR'da 2 kanal bekleniyordu. Bulunan: {ozet.Linyeler.Count}.");
        if (ozet.Linyeler.Any(l => l.ElemanSayisi != 1))
            throw new InvalidOperationException("Her CCTV hattında 1 kamera olmalı.");
    }

    public static void ZayifAkimCctvSwitchMerkezOlabilir()
    {
        var kat = new BasitProjeKat();
        kat.Odalar.Add(new BasitProjeOda
        {
            Ad = "Kat",
            Poligon = [new(0, 0), new(10, 0), new(10, 5), new(0, 5)]
        });

        // Katta NVR yok; kameralar ağ switch'inde toplanır
        var cam = new BasitProjeEleman { Tip = BpcElemanTipi.Kamera, Ad = "CAM-1", X = 2, Y = 2, PaftaTipi = BpcPaftaTipi.Cctv };
        var sw  = new BasitProjeEleman { Tip = BpcElemanTipi.AgAnahtari, Ad = "SW", X = 10, Y = 4, PaftaTipi = BpcPaftaTipi.Cctv };
        kat.Elemanlar.Add(cam);
        kat.Elemanlar.Add(sw);

        var devre = new BpcSistemDevre { Sistem = BpcPaftaTipi.Cctv, MerkezId = sw.Id };
        devre.CihazIds.Add(cam.Id);
        kat.SistemDevreleri.Add(devre);

        BasitProjeCizKabloServisi.TumSistemRotalariniHesapla(kat);

        var kablo = kat.Kablolar.Single(k => k.Tip == BpcKabloTipi.ZayifAkimKablosu);
        if (kablo.SistemAnahtar != BpcPaftaTipi.Cctv)
            throw new InvalidOperationException("Switch'e giden kamera kablosu CCTV sistemine ait olmalı.");

        var ozet = BasitProjeCizPanoServisi.MerkezOzetleri(kat, BpcPaftaTipi.Cctv).Single();
        if (ozet.Pano.Tip != BpcElemanTipi.AgAnahtari)
            throw new InvalidOperationException("Merkez özeti ağ switch'inde olmalı.");
        if (ozet.Linyeler.Single().ElemanSayisi != 1)
            throw new InvalidOperationException("Switch'te 1 kamera bekleniyordu.");
    }

    public static void ZayifAkimYanginLoopKapaliCevrim()
    {
        var kat = new BasitProjeKat();
        kat.Odalar.Add(new BasitProjeOda
        {
            Ad = "Kat",
            Poligon = [new(0, 0), new(10, 0), new(10, 8), new(0, 8)]
        });

        var santral = new BasitProjeEleman { Tip = BpcElemanTipi.YanginSantrali, Ad = "YSP", X = 0, Y = 0, PaftaTipi = BpcPaftaTipi.Yangin };
        var d1 = new BasitProjeEleman { Tip = BpcElemanTipi.DumanDedektor, Ad = "DD-1", X = 3, Y = 4, PaftaTipi = BpcPaftaTipi.Yangin };
        var d2 = new BasitProjeEleman { Tip = BpcElemanTipi.DumanDedektor, Ad = "DD-2", X = 7, Y = 4, PaftaTipi = BpcPaftaTipi.Yangin };
        var buton = new BasitProjeEleman { Tip = BpcElemanTipi.YanginButonu, Ad = "YB-1", X = 9, Y = 0, PaftaTipi = BpcPaftaTipi.Yangin };
        kat.Elemanlar.Add(santral);
        kat.Elemanlar.Add(d1);
        kat.Elemanlar.Add(d2);
        kat.Elemanlar.Add(buton);

        var loop = new BpcSistemDevre { Sistem = BpcPaftaTipi.Yangin, MerkezId = santral.Id };
        loop.CihazIds.Add(d1.Id);
        loop.CihazIds.Add(d2.Id);
        loop.CihazIds.Add(buton.Id);
        kat.SistemDevreleri.Add(loop);

        BasitProjeCizKabloServisi.TumSistemRotalariniHesapla(kat);

        var kablo = kat.Kablolar.Single(k => k.Tip == BpcKabloTipi.ZayifAkimKablosu);
        if (kablo.SistemAnahtar != BpcPaftaTipi.Yangin)
            throw new InvalidOperationException($"Yangın kablosu beklenmedi. Sistem: {kablo.SistemAnahtar}.");
        if (kablo.PanoSira != 1)
            throw new InvalidOperationException($"Yangın loop'u Y1 numaralanmalı. Bulunan: {kablo.PanoSira}.");

        // Kapalı çevrim: rota santralde başlamalı ve santralde bitmelidir
        var bas = kablo.Rota[0];
        var son = kablo.Rota[^1];
        if (Distance(bas, son) > 0.2)
            throw new InvalidOperationException($"Loop kapalı olmalı: baş ({bas.X:F2},{bas.Y:F2}) ile son ({son.X:F2},{son.Y:F2}) çakışmalı.");
        if (Distance(bas, new Point2D(santral.X, santral.Y)) > 0.6)
            throw new InvalidOperationException("Loop santralde başlamalı.");

        var ozet = BasitProjeCizPanoServisi.MerkezOzetleri(kat, BpcPaftaTipi.Yangin).Single();
        var linye = ozet.Linyeler.Single();
        if (linye.Etiket != "Y1")
            throw new InvalidOperationException($"Yangın linyesi Y1 olmalı. Bulunan: {linye.Etiket}.");
        if (linye.ElemanSayisi != 3)
            throw new InvalidOperationException($"Loop'ta 3 cihaz bekleniyordu. Bulunan: {linye.ElemanSayisi}.");
    }

    public static void ZayifAkimDiafonYildizSantraleBaglanir()
    {
        var kat = new BasitProjeKat();
        kat.Odalar.Add(new BasitProjeOda
        {
            Ad = "Mesken",
            Poligon = [new(0, 0), new(16, 0), new(16, 8), new(0, 8)]
        });

        var santral = new BasitProjeEleman { Tip = BpcElemanTipi.DiafonSantrali, Ad = "DFS", X = 1, Y = 1, PaftaTipi = BpcPaftaTipi.Diafon };
        var diafon = new BasitProjeEleman { Tip = BpcElemanTipi.Diafon, Ad = "DF-1", X = 10, Y = 2, PaftaTipi = BpcPaftaTipi.Diafon };
        var zil = new BasitProjeEleman { Tip = BpcElemanTipi.KapiZili, Ad = "KZ-1", X = 12, Y = 2, PaftaTipi = BpcPaftaTipi.Diafon };
        kat.Elemanlar.Add(santral);
        kat.Elemanlar.Add(diafon);
        kat.Elemanlar.Add(zil);

        var diafonDevresi = new BpcSistemDevre { Sistem = BpcPaftaTipi.Diafon, MerkezId = santral.Id };
        diafonDevresi.CihazIds.Add(diafon.Id);
        kat.SistemDevreleri.Add(diafonDevresi);

        var zilDevresi = new BpcSistemDevre { Sistem = BpcPaftaTipi.Diafon, MerkezId = santral.Id };
        zilDevresi.CihazIds.Add(zil.Id);
        kat.SistemDevreleri.Add(zilDevresi);

        BasitProjeCizKabloServisi.TumSistemRotalariniHesapla(kat);

        var kablolar = kat.Kablolar.Where(k => k.Tip == BpcKabloTipi.ZayifAkimKablosu && k.SistemAnahtar == BpcPaftaTipi.Diafon).ToList();
        if (kablolar.Count != 2)
            throw new InvalidOperationException($"Diafon sisteminde 2 ayrı yıldız hat bekleniyordu. Bulunan: {kablolar.Count}.");
        if (kablolar.Any(k => k.Rota.Count < 2))
            throw new InvalidOperationException("Diafon kabloları santrale rota üretmeli.");

        var ozet = BasitProjeCizPanoServisi.MerkezOzetleri(kat, BpcPaftaTipi.Diafon).Single();
        if (ozet.Pano.Tip != BpcElemanTipi.DiafonSantrali)
            throw new InvalidOperationException("Diafon merkezi diafon santrali olmalı.");
        if (ozet.Linyeler.Count != 2 || ozet.Linyeler.Any(l => !l.Etiket.StartsWith("DF", StringComparison.Ordinal)))
            throw new InvalidOperationException("Diafon hatları DF prefix'i ile numaralanmalı.");
    }

    private static void AssertTextLayer(IEnumerable<Text> texts, string value, string expectedLayer)
    {
        var text = texts.FirstOrDefault(t => t.Value == value);
        if (text == null)
            throw new InvalidOperationException($"DXF text bulunamadı: {value}");
        if (text.Layer.Name != expectedLayer)
            throw new InvalidOperationException($"{value} etiketi {expectedLayer} layerında olmalı. Bulunan: {text.Layer.Name}");
    }

    private static double RouteLength(IReadOnlyList<Point2D> points)
    {
        double total = 0;
        for (int i = 0; i < points.Count - 1; i++)
            total += Distance(points[i], points[i + 1]);
        return total;
    }

    private static bool RouteEntersRoom(IReadOnlyList<Point2D> rota, BasitProjeOda oda)
    {
        for (int i = 0; i < rota.Count - 1; i++)
        {
            const int samples = 16;
            var a = rota[i];
            var b = rota[i + 1];
            for (int s = 0; s <= samples; s++)
            {
                double t = s / (double)samples;
                var p = new Point2D(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
                if (PointInPolygonForTest(p, oda.Poligon))
                    return true;
            }
        }

        return false;
    }

    private static bool PointInPolygonForTest(Point2D p, IReadOnlyList<Point2D> poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var pi = poly[i];
            var pj = poly[j];
            if (((pi.Y > p.Y) != (pj.Y > p.Y)) &&
                (p.X < (pj.X - pi.X) * (p.Y - pi.Y) / ((pj.Y - pi.Y) + 1e-12) + pi.X))
            {
                inside = !inside;
            }
        }
        return inside;
    }

    private static double Distance(Point2D a, Point2D b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double PointToSegmentDistance(Point2D p, Point2D a, Point2D b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lenSq = dx * dx + dy * dy;
        if (lenSq < 1e-12) return Distance(p, a);
        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq, 0, 1);
        return Distance(p, new Point2D(a.X + dx * t, a.Y + dy * t));
    }

    public static void MahalListesiOtomatikYerlestirArmaturFallbackTest()
    {
        var kutuphane = new BasitProjeCizKutuphaneServisi();
        var geometri = new BasitProjeCizGeometriServisi();
        var servis = new BasitProjeCizOtomatikYerlestirServisi(geometri);

        var kat = new BasitProjeKat();
        var oda = new BasitProjeOda
        {
            Ad = "Test Oda",
            Poligon = new List<Point2D> { new(0, 0), new(5, 0), new(5, 5), new(0, 5) }
        };
        kat.Odalar.Add(oda);

        // Add a room list item with Armatür type and null/unresolved ArmatürTanimId
        var satir = new BpcMahalSatiri
        {
            Tip = BpcElemanTipi.Armatür,
            ArmatürTanimId = null, // Empty definition
            ArmatürTanimAd = "Aydınlatma Armatürü"
        };
        var ayar = satir.HucreGetirVeyaOlustur(oda.Id);
        ayar.Aktif = true;
        ayar.YerlesimTipi = "Adet";
        ayar.GenelAyarKullan = false;
        ayar.Adet = 2;

        kat.MahalListesi.Add(satir);

        var (yeni, uyarilar) = servis.TumMahalListesindenYerlestir(kat, kutuphane, false);

        if (yeni.Count != 2)
            throw new InvalidOperationException($"Aydınlatma armatürleri otomatik yerleşmeliydi (beklenen: 2, bulunan: {yeni.Count}).");

        if (yeni.Any(e => e.Tip != BpcElemanTipi.Armatür))
            throw new InvalidOperationException("Yerleştirilen elemanların tipi Armatür olmalıdır.");
    }

    public static void MahalListesiTamYenilemeManuelElemaniKorur()
    {
        var vm = new BasitProjeCizViewModel();
        var kat = new BasitProjeKat { Ad = "Kat", KatNo = 1 };
        var oda = new BasitProjeOda
        {
            Ad = "Oda",
            Poligon = [new(0, 0), new(6, 0), new(6, 6), new(0, 6)]
        };
        kat.Odalar.Add(oda);

        var manuel = new BasitProjeEleman
        {
            Id = "manuel-priz",
            Tip = BpcElemanTipi.Priz,
            Ad = "Manuel Priz",
            BlokAdi = "E4P_PRIZ",
            ArmatürTanimId = "manuel-tanim",
            OdaId = oda.Id,
            X = 1,
            Y = 1
        };
        var eskiOtomatik = new BasitProjeEleman
        {
            Id = "eski-oto",
            Tip = BpcElemanTipi.Priz,
            Ad = "Eski Otomatik Priz",
            BlokAdi = "E4P_PRIZ",
            ArmatürTanimId = "oto-tanim",
            OdaId = oda.Id,
            X = 2,
            Y = 2,
            OtomatikYerlesimKaynak = BasitProjeCizOtomatikYerlestirServisi.MahalListesiKaynak
        };
        kat.Elemanlar.Add(manuel);
        kat.Elemanlar.Add(eskiOtomatik);

        var satir = new BpcMahalSatiri
        {
            Tip = BpcElemanTipi.Priz,
            ArmatürTanimId = null,
            ArmatürTanimAd = "Topraklı Priz"
        };
        var ayar = satir.HucreGetirVeyaOlustur(oda.Id);
        ayar.Aktif = true;
        ayar.GenelAyarKullan = false;
        ayar.YerlesimTipi = "Adet";
        ayar.MontajTipi = "Duvar";
        ayar.Adet = 2;
        kat.MahalListesi.Add(satir);

        vm.Katlar.Add(kat);
        vm.SeciliKat = kat;
        vm.AktifPafta = BpcPaftaTipi.Priz;
        vm.SadecaEksikleriEkle = false;
        vm.MahalListesindenOtomatikYerlestirCommand.Execute(null);

        if (!kat.Elemanlar.Any(e => e.Id == "manuel-priz"))
            throw new InvalidOperationException("Mahal listesi tam yenilemesi manuel elemanı silmemeli.");

        if (kat.Elemanlar.Any(e => e.Id == "eski-oto"))
            throw new InvalidOperationException("Mahal listesi tam yenilemesi eski otomatik elemanı temizlemeli.");

        // İstenen iki prizden biri zaten elle konmuştur. İlk adımda eksik tek priz üretilir.
        var otomatikElemanlar = kat.Elemanlar
            .Where(e => e.OtomatikYerlesimKaynak == BasitProjeCizOtomatikYerlestirServisi.MahalListesiKaynak)
            .ToList();
        if (otomatikElemanlar.Count != 1 || otomatikElemanlar[0].Tip != BpcElemanTipi.Priz)
            throw new InvalidOperationException("Tam yenileme elle yerleştirilen prizi saymalı ve yalnızca eksik prizi üretmeli.");
    }

    public static void MahalListesiKilitliProjedeOtomatikYerlestirmeYapmaz()
    {
        var vm = new BasitProjeCizViewModel();
        var kat = new BasitProjeKat { Ad = "Kat", KatNo = 1 };
        var oda = new BasitProjeOda
        {
            Ad = "Oda",
            Poligon = [new(0, 0), new(5, 0), new(5, 5), new(0, 5)]
        };
        kat.Odalar.Add(oda);

        var satir = new BpcMahalSatiri { Tip = BpcElemanTipi.Priz };
        var ayar = satir.HucreGetirVeyaOlustur(oda.Id);
        ayar.Aktif = true;
        ayar.GenelAyarKullan = false;
        ayar.Adet = 2;
        kat.MahalListesi.Add(satir);

        vm.Katlar.Add(kat);
        vm.SeciliKat = kat;
        vm.ProjeKilitli = true;
        vm.MahalListesindenOtomatikYerlestirCommand.Execute(null);

        if (kat.Elemanlar.Count != 0)
            throw new InvalidOperationException("Kilitli projede mahal listesinden otomatik yerleştirme yapılmamalı.");
    }

    // Pano hiyerarşisi (kullanıcı kuralı): tali pano (E4P_TALI_PANO) → kat panosu
    // (E4P_KAT_PANOSU). Roller BlokAdi ile ayrışır; oda içi/dışı konumdan bağımsız.
    public static void OtomatikBaglamaTaliPanoyuKatPanosunaBaglar()
    {
        var vm = new BasitProjeCizViewModel();
        var kat = new BasitProjeKat { Ad = "Kat", KatNo = 1 };
        var oda = new BasitProjeOda { Ad = "Oda", Poligon = [new(0, 0), new(10, 0), new(10, 10), new(0, 10)] };
        var gecis = new BasitProjeOda { Ad = "Geçiş", GecisSecili = true, Poligon = [new(10, 0), new(13, 0), new(13, 10), new(10, 10)] };
        kat.Odalar.Add(oda);
        kat.Odalar.Add(gecis);

        // KP = kat panosu, TP = tali pano. Rol BlokAdi ile; rota geçiş izni olan mahalden geçer.
        var kp = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, Ad = "KP", BlokAdi = "E4P_KAT_PANOSU", OdaId = oda.Id, X = 2, Y = 2 };
        var tp = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, Ad = "TP", BlokAdi = "E4P_TALI_PANO", OdaId = gecis.Id, X = 12, Y = 5 };
        kat.Elemanlar.Add(kp);
        kat.Elemanlar.Add(tp);

        vm.Katlar.Add(kat);
        vm.SeciliKat = kat;

        vm.BosUclariEnYakinPanoyaBaglaCommand.Execute(null);

        // Tali pano (alt) → kat panosu (üst) besleme hattı kurulmalı.
        if (!kat.PanoBeslemeHatlari.Any(h => h.TaliPanoId == tp.Id && h.AnaPanoId == kp.Id))
            throw new InvalidOperationException(
                "Tali pano (E4P_TALI_PANO) kat panosuna (E4P_KAT_PANOSU) beslenmeli; rol BlokAdi ile belirlenir.");
    }

    // Kural: buat, mahalinde tali pano varsa ona; yoksa kat panosuna bağlanır.
    public static void BuatHedefPanoMahaldekiTaliPanoyuTercihEder()
    {
        var oda = new BasitProjeOda { Ad = "Oda", Poligon = [new(0, 0), new(10, 0), new(10, 10), new(0, 10)] };
        var buat = new BasitProjeEleman { Tip = BpcElemanTipi.Buat, OdaId = oda.Id, X = 5, Y = 5 };
        var kp = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, BlokAdi = "E4P_KAT_PANOSU", OdaId = "", X = 1, Y = 1 };
        var tpOda = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, BlokAdi = "E4P_TALI_PANO", OdaId = oda.Id, X = 9, Y = 9 };

        // Mahalde tali pano var → buat tali panoya gitmeli (kat panosu daha yakın olsa bile)
        var hedef = BasitProjeCizKabloServisi.BuatIcinHedefPano(buat, [kp, tpOda]);
        if (hedef?.Id != tpOda.Id)
            throw new InvalidOperationException("Mahalinde tali pano olan buat, o tali panoya bağlanmalı.");

        // Mahalde tali pano yok → en yakın kat panosuna
        var hedef2 = BasitProjeCizKabloServisi.BuatIcinHedefPano(buat, [kp]);
        if (hedef2?.Id != kp.Id)
            throw new InvalidOperationException("Mahalinde tali pano yoksa buat kat panosuna bağlanmalı.");
    }

    public static void MahalListesiDuvarAraligiYerlesimAdediniEtkiler()
    {
        var servis = new BasitProjeCizOtomatikYerlestirServisi(new BasitProjeCizGeometriServisi());
        var oda = new BasitProjeOda
        {
            Ad = "Duvar Oda",
            Poligon = [new(0, 0), new(8, 0), new(8, 6), new(0, 6)]
        };
        var tanim = new BpcArmatürTanim
        {
            Id = "priz",
            Ad = "Topraklı Priz",
            BlokAdi = "E4P_PRIZ",
            MontajTipi = "Duvar"
        };

        // Priz için "Duvar aralıklı": kullanıcı adet girer; farklı duvar aralığı farklı konum önerir
        // 8x6 odada 5m aralıkla max ~5 konum, 2m aralıkla max ~12 konum → farklı adet girerek test
        var seyrek = servis.OdayaYerlestir(
            oda, tanim, "Duvar aralıklı", "Duvar", 3.0, 5.0, 3,
            [], [], false, out _);
        var sik = servis.OdayaYerlestir(
            oda, tanim, "Duvar aralıklı", "Duvar", 3.0, 2.0, 6,
            [], [], false, out _);

        if (sik.Count <= seyrek.Count)
            throw new InvalidOperationException($"Duvar aralığı küçülünce daha çok priz istenilebilmeli. Seyrek: {seyrek.Count}, sık: {sik.Count}.");

        if (sik.Any(e => e.DuvarAraligiM != 2.0 || e.OtomatikYerlesimKaynak != BasitProjeCizOtomatikYerlestirServisi.MahalListesiKaynak))
            throw new InvalidOperationException("Duvar aralığı ve otomatik kaynak işareti yeni elemanlara yazılmalı.");
    }

    // Priz "Adet" modunda MontajTipi "Tavan" (satır/hücre varsayılanı) olsa bile duvar kenarına yerleşmeli.
    public static void PrizAdetModuMontajTipiTavanOlsaDaDuvarKenarindaYerlesir()
    {
        var servis = new BasitProjeCizOtomatikYerlestirServisi(new BasitProjeCizGeometriServisi());
        var oda = new BasitProjeOda
        {
            Ad = "Priz Oda",
            Poligon = [new(0, 0), new(6, 0), new(6, 5), new(0, 5)]
        };
        var tanim = new BpcArmatürTanim
        {
            Id = "priz",
            Ad = "Topraklı Priz",
            BlokAdi = "E4P_PRIZ",
            MontajTipi = "Duvar"
        };

        // MontajTipi = "Tavan" — BpcMahalSatiri/BpcMahalHucreAyar varsayılanı;
        // "Adet" modunda bu yanlışlıkla IcMekanAdaylar'ı tetiklemeli değil.
        var placed = servis.OdayaYerlestir(
            oda, tanim, "Adet", "Tavan", 3.0, 3.0, 3,
            [], [], false, out _);

        if (placed.Count != 3)
            throw new InvalidOperationException($"3 priz yerleştirilmeliydi. Bulunan: {placed.Count}.");

        // Duvar kenarı kontrolü: her eleman oda merkezinden en az %30 uzaklıkta olmalı
        // (gerçek duvar yakınlığı için DuvarAdaylariUret'in DuvarGiris = 0.12 m ofset kullandığını biliyoruz)
        var merkez = new Point2D(3.0, 2.5);
        double yaricap = Math.Sqrt(3 * 3 + 2.5 * 2.5); // köşeden merkeze ~3.9m
        foreach (var e in placed)
        {
            double uzaklik = Math.Sqrt((e.X - merkez.X) * (e.X - merkez.X) + (e.Y - merkez.Y) * (e.Y - merkez.Y));
            if (uzaklik < 0.8)
                throw new InvalidOperationException(
                    $"Priz oda merkezine çok yakın ({uzaklik:F2} m). Duvar kenarına yerleştirilmeli.");
        }
    }

    public static void AnahtarlarYalnizKapiAcikligiYaninaYerlesir()
    {
        var servis = new BasitProjeCizOtomatikYerlestirServisi(new BasitProjeCizGeometriServisi());
        var oda = new BasitProjeOda
        {
            Ad = "Anahtar Oda",
            Poligon = [new(0, 0), new(5, 0), new(5, 4), new(0, 4)]
        };
        var anahtar = new BpcArmatürTanim
        {
            Id = "anahtar",
            Ad = "Anahtar",
            BlokAdi = "E4P_ANAHTAR",
            MontajTipi = "Duvar"
        };

        var aciklikBas = new Point2D(2, 0);
        var aciklikBit = new Point2D(3, 0);
        var pencere = new BpcAciklik
        {
            Bas = aciklikBas,
            Bit = aciklikBit,
            Tip = BpcAciklikTipi.Pencere,
            Kaynak = "manuel"
        };
        var kapi = new BpcAciklik
        {
            Bas = aciklikBas,
            Bit = aciklikBit,
            Tip = BpcAciklikTipi.Kapı,
            Kaynak = "manuel"
        };

        var pencereYaninda = servis.OdayaYerlestir(
            oda, anahtar, "Adet", "Duvar", 3.0, 3.0, 1,
            [pencere], [], false, out _).Single();
        var kapiYaninda = servis.OdayaYerlestir(
            oda, anahtar, "Adet", "Duvar", 3.0, 3.0, 1,
            [kapi], [], false, out _).Single();

        double pencereMesafe = PointToSegmentDistance(new Point2D(pencereYaninda.X, pencereYaninda.Y), aciklikBas, aciklikBit);
        double kapiMesafe = PointToSegmentDistance(new Point2D(kapiYaninda.X, kapiYaninda.Y), aciklikBas, aciklikBit);

        if (pencereMesafe < 0.40)
            throw new InvalidOperationException($"Anahtar pencere açıklığı koruma bölgesine yerleşmemeli. Mesafe: {pencereMesafe:0.###} m.");

        if (kapiMesafe > 0.35)
            throw new InvalidOperationException($"Anahtar kapı açıklığının yanına yerleşmeli. Mesafe: {kapiMesafe:0.###} m.");
    }

    // Regresyon: genel ayar Grid/Duvar olsa bile yalnız açıkça etkinleştirilen
    // (Aktif=true) hücreler yerleşmeli; tüm odalara otomatik atama YAPMAMALI.
    public static void MahalListesiGridGenelAyarSadeceAktifHucreyiYerlestirir()
    {
        var kutuphane = new BasitProjeCizKutuphaneServisi();
        var servis = new BasitProjeCizOtomatikYerlestirServisi(new BasitProjeCizGeometriServisi());

        var kat = new BasitProjeKat();
        var oda1 = new BasitProjeOda
        {
            Ad = "Aktif Oda",
            Poligon = [new(0, 0), new(6, 0), new(6, 6), new(0, 6)]
        };
        var oda2 = new BasitProjeOda
        {
            Ad = "Pasif Oda",
            Poligon = [new(10, 0), new(16, 0), new(16, 6), new(10, 6)]
        };
        kat.Odalar.Add(oda1);
        kat.Odalar.Add(oda2);

        var satir = new BpcMahalSatiri
        {
            Tip = BpcElemanTipi.Armatür,
            ArmatürTanimId = null,
            ArmatürTanimAd = "Aydınlatma Armatürü",
            YerlesimTipi = "Grid",   // GENEL ayar Grid
            GridAraligiM = 3.0
        };

        // Yalnız oda1 hücresini etkinleştir; oda2 varsayılan (Aktif=false) kalsın.
        var ayar1 = satir.HucreGetirVeyaOlustur(oda1.Id);
        ayar1.Aktif = true; // GenelAyarKullan varsayılan true → genel Grid ayarını kullanır
        satir.HucreGetirVeyaOlustur(oda2.Id); // pasif: Aktif=false

        kat.MahalListesi.Add(satir);

        var (yeni, _) = servis.TumMahalListesindenYerlestir(kat, kutuphane, false);

        if (yeni.Count == 0)
            throw new InvalidOperationException("Aktif oda hücresine Grid armatürleri yerleşmeliydi.");

        if (yeni.Any(e => e.OdaId == oda2.Id))
            throw new InvalidOperationException("Etkinleştirilmemiş oda hücresine (Aktif=false) yerleştirme yapılmamalı — genel Grid ayarı tüm odalara atama YAPMAMALI.");

        if (yeni.Any(e => e.OdaId != oda1.Id))
            throw new InvalidOperationException("Yerleştirilen armatürler yalnız aktif odaya ait olmalı.");
    }

    // M-2: Eski (sürümsüz) Grid/Duvar projeleri yüklemede yeni semantiğe taşınır;
    // genel-ayar kullanan hücreler Aktif yapılır, şema sürümü güncellenir.
    public static void MahalGridAktiflikMigrasyonuEskiProjeyiTasir()
    {
        var proje = new BasitProjeCizProje(); // SemaSurum = 0 (eski)
        var kat = new BasitProjeKat();
        var oda1 = new BasitProjeOda { Ad = "O1", Poligon = [new(0, 0), new(4, 0), new(4, 4), new(0, 4)] };
        var oda2 = new BasitProjeOda { Ad = "O2", Poligon = [new(5, 0), new(9, 0), new(9, 4), new(5, 4)] };
        kat.Odalar.Add(oda1);
        kat.Odalar.Add(oda2);

        var gridSatir = new BpcMahalSatiri { Tip = BpcElemanTipi.Armatür, YerlesimTipi = "Grid" };
        var adetSatir = new BpcMahalSatiri { Tip = BpcElemanTipi.Priz, YerlesimTipi = "Adet" };
        kat.MahalListesi.Add(gridSatir);
        kat.MahalListesi.Add(adetSatir);
        proje.Katlar.Add(kat);

        int n = BasitProjeCizOtomatikYerlestirServisi.MahalGridAktiflikMigrasyonu(proje);

        if (n != 2)
            throw new InvalidOperationException($"Grid satırının 2 oda hücresi aktif edilmeliydi. Bulunan: {n}.");
        if (!gridSatir.EfektifAktif(oda1.Id) || !gridSatir.EfektifAktif(oda2.Id))
            throw new InvalidOperationException("Migrasyon sonrası Grid satırı tüm odalarda aktif olmalı (eski davranış korunur).");
        if (adetSatir.EfektifAktif(oda1.Id))
            throw new InvalidOperationException("Adet satırı migrasyondan etkilenmemeli (yalnız Grid/Duvar taşınır).");
        // Migrasyon kendi kilometre taşı sürümünü (1) damgalar; GuncelSemaSurum'a
        // damgalama kayıt sırasında yapılır (sürüm 2: M50 geometri bağlantısı).
        if (proje.SemaSurum < 1)
            throw new InvalidOperationException("Migrasyon sonrası şema sürümü en az 1 olmalı.");

        // Tekrar çağrı no-op olmalı (güncel sürümlü proje yeniden taşınmaz).
        if (BasitProjeCizOtomatikYerlestirServisi.MahalGridAktiflikMigrasyonu(proje) != 0)
            throw new InvalidOperationException("Güncel sürümlü proje yeniden migrate edilmemeli.");
    }

    // Otomatik aydınlatma buatı: her 4 anahtar için 1 buat üretir, oda içine yerleşir,
    // otomatik kaynak işaretlenir ve ikinci çağrıda (sadece eksikler) tekrar üretmez.
    public static void OtomatikAydinlatmaBuatiHer4AnahtaraBirUretir()
    {
        var kutuphane = new BasitProjeCizKutuphaneServisi();
        var servis = new BasitProjeCizOtomatikYerlestirServisi(new BasitProjeCizGeometriServisi());
        var kat = new BasitProjeKat();
        var oda = new BasitProjeOda { Ad = "Oda", Poligon = [new(0, 0), new(10, 0), new(10, 8), new(0, 8)] };
        kat.Odalar.Add(oda);

        for (int i = 0; i < 9; i++)
            kat.Elemanlar.Add(new BasitProjeEleman
            {
                Tip = BpcElemanTipi.Anahtar, Ad = $"A{i}", OdaId = oda.Id,
                X = 0.5 + i * 0.15, Y = 0.2
            });

        var (yeni, _) = servis.TumMahalListesindenYerlestir(kat, kutuphane, false);

        var buatlar = yeni.Where(e => e.Tip == BpcElemanTipi.Buat).ToList();
        if (buatlar.Count != 3)
            throw new InvalidOperationException($"9 anahtar için 3 buat beklenir (her 4 anahtara 1). Bulunan: {buatlar.Count}.");
        if (buatlar.Any(b => b.OtomatikYerlesimKaynak != BasitProjeCizOtomatikYerlestirServisi.MahalListesiKaynak))
            throw new InvalidOperationException("Otomatik buatlar mahal listesi kaynak işareti taşımalı.");
        if (buatlar.Any(b => !PoligonYardimci.IsPointInside(new Point2D(b.X, b.Y), oda.Poligon)))
            throw new InvalidOperationException("Otomatik buatlar oda poligonu içinde olmalı.");

        // Üretilen buatları kata ekleyip ikinci kez çalıştır → idempotent olmalı
        foreach (var b in buatlar) kat.Elemanlar.Add(b);
        var (yeni2, _) = servis.TumMahalListesindenYerlestir(kat, kutuphane, true);
        if (yeni2.Any(e => e.Tip == BpcElemanTipi.Buat))
            throw new InvalidOperationException("Yeterli buat varken yeni buat üretilmemeli (idempotent).");
    }

    // Çoklu buat varken kablolama her anahtarı en yakın buata bağlar.
    public static void KatAydinlatmaKablolamaEnYakinBuataBaglar()
    {
        var kat = new BasitProjeKat();
        var oda = new BasitProjeOda { Ad = "Uzun Oda", Poligon = [new(0, 0), new(20, 0), new(20, 6), new(0, 6)] };
        kat.Odalar.Add(oda);

        var anahtar1 = new BasitProjeEleman { Tip = BpcElemanTipi.Anahtar, Ad = "A1", OdaId = oda.Id, X = 1, Y = 1 };
        var anahtar2 = new BasitProjeEleman { Tip = BpcElemanTipi.Anahtar, Ad = "A2", OdaId = oda.Id, X = 18, Y = 1 };
        // Farklı blok adı → iki ayrı grup → her grup ayrı anahtara dağıtılır.
        var arm1 = new BasitProjeEleman { Tip = BpcElemanTipi.Armatür, Ad = "L1", BlokAdi = "E4P_LED_A", OdaId = oda.Id, X = 3, Y = 3 };
        var arm2 = new BasitProjeEleman { Tip = BpcElemanTipi.Armatür, Ad = "L2", BlokAdi = "E4P_LED_B", OdaId = oda.Id, X = 16, Y = 3 };
        var buat1 = new BasitProjeEleman { Tip = BpcElemanTipi.Buat, Ad = "B1", OdaId = oda.Id, X = 2, Y = 2 };
        var buat2 = new BasitProjeEleman { Tip = BpcElemanTipi.Buat, Ad = "B2", OdaId = oda.Id, X = 17, Y = 2 };
        foreach (var e in new[] { anahtar1, anahtar2, arm1, arm2, buat1, buat2 }) kat.Elemanlar.Add(e);

        BasitProjeCizKabloServisi.KatAydinlatmasiniOtomatikKablola(kat);

        var devre1 = kat.Devreler.FirstOrDefault(d => d.AnahtarElemanId == anahtar1.Id);
        var devre2 = kat.Devreler.FirstOrDefault(d => d.AnahtarElemanId == anahtar2.Id);
        if (devre1 == null || devre2 == null)
            throw new InvalidOperationException("Her iki anahtar için devre oluşmalıydı.");
        if (devre1.BuatId != buat1.Id)
            throw new InvalidOperationException("A1 anahtarı en yakın buatına (B1) bağlanmalıydı.");
        if (devre2.BuatId != buat2.Id)
            throw new InvalidOperationException("A2 anahtarı en yakın buatına (B2) bağlanmalıydı.");

        // Anahtar→buat kablosu çizilmeli
        BasitProjeCizKabloServisi.RotaHesapla(kat, devre1, BpcRotaTipi.DaisyChain);
        if (!kat.Kablolar.Any(k => k.DevreId == devre1.Id))
            throw new InvalidOperationException("A1 devresi için kablo rotası çizilmeliydi.");
    }

    // #3a: Bir buatta 9'dan fazla armatür → aydınlatma linyesi trifaze olur.
    public static void Buatta9UstuArmaturLinyeyiTrifazeYapar()
    {
        var kat = new BasitProjeKat();
        var oda = new BasitProjeOda { Ad = "Oda", Poligon = [new(0, 0), new(10, 0), new(10, 10), new(0, 10)] };
        kat.Odalar.Add(oda);

        var pano = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, Ad = "Pano", OdaId = oda.Id, X = 9, Y = 9 };
        var buat = new BasitProjeEleman { Tip = BpcElemanTipi.Buat, Ad = "B1", OdaId = oda.Id, X = 5, Y = 5 };
        var anahtar = new BasitProjeEleman { Tip = BpcElemanTipi.Anahtar, Ad = "A1", OdaId = oda.Id, X = 1, Y = 1 };
        kat.Elemanlar.Add(pano); kat.Elemanlar.Add(buat); kat.Elemanlar.Add(anahtar);

        var devre = BasitProjeCizKabloServisi.EnsureDevre(kat, anahtar.Id);
        for (int i = 0; i < 10; i++)
        {
            var arm = new BasitProjeEleman { Tip = BpcElemanTipi.Armatür, Ad = $"L{i}", OdaId = oda.Id, X = i, Y = 2, CustomGucW = 100 };
            kat.Elemanlar.Add(arm);
            devre.ArmaturElemanIds.Add(arm.Id);
        }
        BasitProjeCizKabloServisi.ButaAta(kat, devre, buat.Id);
        BasitProjeCizKabloServisi.BuatPanoBagla(kat, buat.Id, pano.Id, new List<string>(), "A");

        var ozetler = BasitProjeCizPanoServisi.PanoOzetleri(kat, new List<BpcArmatürTanim>(), BpcPaftaTipi.Aydinlatma);
        var linye = ozetler.SelectMany(o => o.Linyeler).FirstOrDefault(l => l.LinyePrefix == "A");
        if (linye == null)
            throw new InvalidOperationException("Aydınlatma linyesi oluşmalıydı.");
        if (linye.Faz != Kst.Acoustics.Core.Electric.Models.FazTipi.Trifaze)
            throw new InvalidOperationException($"10 armatürlü linye trifaze olmalıydı. Faz: {linye.Faz}.");
        if (!linye.SigortaText.Contains("3F"))
            throw new InvalidOperationException("Trifaze linyenin sigorta metni '3F' içermeli.");
    }

    // Özellik 1: Mahal listesi hücresindeki oda-özel İsim + Watt yerleşen elemanlara uygulanır.
    public static void MahalHucreOdaOzelIsimWattUygulanir()
    {
        var kutuphane = new BasitProjeCizKutuphaneServisi();
        var servis = new BasitProjeCizOtomatikYerlestirServisi(new BasitProjeCizGeometriServisi());
        var kat = new BasitProjeKat();
        var oda = new BasitProjeOda { Ad = "Salon", Poligon = [new(0, 0), new(5, 0), new(5, 5), new(0, 5)] };
        kat.Odalar.Add(oda);

        var satir = new BpcMahalSatiri
        {
            Tip = BpcElemanTipi.Armatür,
            ArmatürTanimId = null,
            ArmatürTanimAd = "Aydınlatma Armatürü"
        };
        var ayar = satir.HucreGetirVeyaOlustur(oda.Id);
        ayar.Aktif = true;
        ayar.GenelAyarKullan = false;
        ayar.YerlesimTipi = "Adet";
        ayar.Adet = 2;
        ayar.OzelAd = "Salon Spot";
        ayar.OzelGucW = 18;
        kat.MahalListesi.Add(satir);

        var (yeni, _) = servis.TumMahalListesindenYerlestir(kat, kutuphane, false);
        var armaturler = yeni.Where(e => e.Tip == BpcElemanTipi.Armatür).ToList();

        if (armaturler.Count != 2)
            throw new InvalidOperationException($"2 armatür yerleşmeliydi. Bulunan: {armaturler.Count}.");
        if (armaturler.Any(e => e.Ad != "Salon Spot"))
            throw new InvalidOperationException("Oda-özel isim yerleşen elemanlara uygulanmalı.");
        if (armaturler.Any(e => e.CustomGucW != 18))
            throw new InvalidOperationException("Oda-özel güç (W) yerleşen elemanlara uygulanmalı.");
    }

    // Özellik 3: Buat→pano önceliği — odadaki tali pano > atanmış pano > en yakın.
    public static void BuatIcinHedefPanoOnceligi()
    {
        var kat = new BasitProjeKat();
        var oda1 = new BasitProjeOda { Ad = "O1", Poligon = [new(0, 0), new(5, 0), new(5, 5), new(0, 5)] };
        var oda2 = new BasitProjeOda { Ad = "O2", Poligon = [new(10, 0), new(15, 0), new(15, 5), new(10, 5)] };
        kat.Odalar.Add(oda1);
        kat.Odalar.Add(oda2);

        var buat = new BasitProjeEleman { Tip = BpcElemanTipi.Buat, Ad = "B1", OdaId = oda1.Id, X = 2, Y = 2 };
        // Rol BlokAdi ile belirlenir: E4P_TALI_PANO = tali, E4P_KAT_PANOSU = kat panosu.
        var taliPano = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, Ad = "Tali", BlokAdi = "E4P_TALI_PANO", OdaId = oda1.Id, X = 4, Y = 4 };
        var katPano = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, Ad = "Kat", BlokAdi = "E4P_KAT_PANOSU", OdaId = oda2.Id, X = 12, Y = 2 };
        katPano.BagliOdaIds.Add(oda1.Id); // oda1 bu panoya atanmış

        // 1) Mahalde tali pano varken → tali pano
        var panolar = new[] { taliPano, katPano };
        var hedef1 = BasitProjeCizKabloServisi.BuatIcinHedefPano(buat, panolar);
        if (hedef1 != taliPano)
            throw new InvalidOperationException("Odada tali pano varsa buat ona bağlanmalı.");

        // 2) Tali pano yokken, atanmış pano (BagliOdaIds) → katPano
        var panolar2 = new[] { katPano };
        var hedef2 = BasitProjeCizKabloServisi.BuatIcinHedefPano(buat, panolar2);
        if (hedef2 != katPano)
            throw new InvalidOperationException("Tali pano yoksa odası atanmış kat panosuna gitmeli.");

        // 3) Atama da yokken → en yakın
        katPano.BagliOdaIds.Clear();
        var uzakPano = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, Ad = "Uzak", OdaId = oda2.Id, X = 100, Y = 100 };
        var hedef3 = BasitProjeCizKabloServisi.BuatIcinHedefPano(buat, new[] { katPano, uzakPano });
        if (hedef3 != katPano)
            throw new InvalidOperationException("Atama yoksa en yakın panoya gitmeli.");
    }

    // K-1 + K-2: Manuel priz kablolama undo snapshot alır ve geri al priz devresini temizler.
    public static void ManuelPrizKablolamaUndoIleGeriAlinir()
    {
        var vm = new BasitProjeCizViewModel();
        var kat = new BasitProjeKat { Ad = "Kat", KatNo = 1 };
        kat.Odalar.Add(new() { Id = "oda", Poligon = [new(0, 0), new(4, 0), new(4, 4), new(0, 4)] });
        kat.Elemanlar.Add(new() { Tip = BpcElemanTipi.Buat, PaftaTipi = BpcPaftaTipi.Priz, OdaId = "oda", X = 0, Y = 1 });
        var priz1 = new BasitProjeEleman { Id = "p1", OdaId = "oda", Tip = BpcElemanTipi.Priz, Ad = "P1", X = 1, Y = 1 };
        var priz2 = new BasitProjeEleman { Id = "p2", OdaId = "oda", Tip = BpcElemanTipi.Priz, Ad = "P2", X = 2, Y = 1 };
        kat.Elemanlar.Add(priz1);
        kat.Elemanlar.Add(priz2);
        vm.Katlar.Add(kat);
        vm.SeciliKat = kat;
        vm.AktifPafta = BpcPaftaTipi.Priz;
        vm.PrizAsamasi = BpcPrizAsamasi.MahalKablosu;
        vm.KabloModuToggle();

        vm.KabloModuTiklama(0, 1); // önce mahal içindeki priz buatını seç
        vm.KabloModuTiklama(1, 1); // priz1 seç → priz devresi oluşur
        vm.KabloModuTiklama(2, 1); // priz2 ekle

        int prizSayisi = kat.PrizDevreleri.Sum(d => d.PrizElemanIds.Count);
        if (prizSayisi != 2)
            throw new InvalidOperationException($"Manuel priz kablolama 2 prizi devreye eklemeliydi. Bulunan: {prizSayisi}.");

        // İki adım geri al → priz devresi tamamen geri alınmalı
        vm.ElemanGeriAlCommand.Execute(null);
        vm.ElemanGeriAlCommand.Execute(null);

        if (kat.PrizDevreleri.Sum(d => d.PrizElemanIds.Count) != 0)
            throw new InvalidOperationException("Geri al priz devresini (snapshot kapsamı) temizlemeliydi.");

        // İleri al → priz ataması geri gelmeli
        vm.ElemanIleriAlCommand.Execute(null);
        vm.ElemanIleriAlCommand.Execute(null);
        if (kat.PrizDevreleri.Sum(d => d.PrizElemanIds.Count) != 2)
            throw new InvalidOperationException("İleri al priz atamasını geri getirmeliydi.");
    }

    // ── 2026-07-18 düzeltme paketi regresyonları ─────────────────────────────

    // A6: İkinci priz kablolamada zaten atanmış prizler yeni devreye GİRMEZ
    // (buat sayısı değişince aynı priz iki devreye eklenip çift kablo/çift yük üretiyordu).
    public static void PrizIkinciKablolamaCiftDevreUretmez()
    {
        var kat = new BasitProjeKat();
        var oda = new BasitProjeOda { Ad = "Oda", Poligon = [new(0, 0), new(6, 0), new(6, 6), new(0, 6)] };
        kat.Odalar.Add(oda);

        var buat1 = new BasitProjeEleman { Tip = BpcElemanTipi.Buat, Ad = "PB1", OdaId = oda.Id, X = 1, Y = 1, PaftaTipi = BpcPaftaTipi.Priz };
        kat.Elemanlar.Add(buat1);
        for (int i = 0; i < 3; i++)
            kat.Elemanlar.Add(new BasitProjeEleman { Tip = BpcElemanTipi.Priz, Ad = $"P{i}", OdaId = oda.Id, X = 0.5 + i, Y = 0.2 });

        BasitProjeCizKabloServisi.KatPrizleriniOtomatikKablola(kat);
        if (kat.PrizDevreleri.Sum(d => d.PrizElemanIds.Count) != 3)
            throw new InvalidOperationException("İlk kablolama 3 prizi devreye eklemeliydi.");

        // Buat ekle + yeni prizler ekle → yeniden kablola
        var buat2 = new BasitProjeEleman { Tip = BpcElemanTipi.Buat, Ad = "PB2", OdaId = oda.Id, X = 5, Y = 5, PaftaTipi = BpcPaftaTipi.Priz };
        kat.Elemanlar.Add(buat2);
        for (int i = 0; i < 4; i++)
            kat.Elemanlar.Add(new BasitProjeEleman { Tip = BpcElemanTipi.Priz, Ad = $"Y{i}", OdaId = oda.Id, X = 5.5, Y = 1 + i });

        BasitProjeCizKabloServisi.KatPrizleriniOtomatikKablola(kat);

        var tumAtamalar = kat.PrizDevreleri.SelectMany(d => d.PrizElemanIds).ToList();
        if (tumAtamalar.Count != tumAtamalar.Distinct().Count())
            throw new InvalidOperationException("Bir priz birden fazla devreye atanmamalı (çift kablo/çift yük).");
        if (tumAtamalar.Count != 7)
            throw new InvalidOperationException($"7 prizin tamamı tek devrede olmalı. Bulunan atama: {tumAtamalar.Count}.");
    }

    // A5: Aydınlatma buatı ihtiyacı sayılırken PRİZ buatları sayılmaz —
    // odada priz buatı varken ikinci yerleştirme aydınlatma buatını yine üretmeli.
    public static void AydinlatmaBuatSayimiPrizBuatiniSaymaz()
    {
        var kutuphane = new BasitProjeCizKutuphaneServisi();
        var kat = new BasitProjeKat();
        var oda = new BasitProjeOda { Ad = "Oda", Poligon = [new(0, 0), new(6, 0), new(6, 6), new(0, 6)] };
        kat.Odalar.Add(oda);

        // Mevcut durum: 1 anahtar + 1 PRİZ buatı (aydınlatma buatı YOK)
        kat.Elemanlar.Add(new BasitProjeEleman { Tip = BpcElemanTipi.Anahtar, Ad = "A", OdaId = oda.Id, X = 0.2, Y = 1 });
        kat.Elemanlar.Add(new BasitProjeEleman { Tip = BpcElemanTipi.Buat, Ad = "PB", OdaId = oda.Id, X = 3, Y = 3, PaftaTipi = BpcPaftaTipi.Priz });

        var servis = new BasitProjeCizOtomatikYerlestirServisi(new BasitProjeCizGeometriServisi());
        var (yeni, _) = servis.TumMahalListesindenYerlestir(kat, kutuphane, sadecaEksikleri: false);

        if (!yeni.Any(e => e.Tip == BpcElemanTipi.Buat && e.PaftaTipi == BpcPaftaTipi.Aydinlatma))
            throw new InvalidOperationException("Priz buatı varken aydınlatma buatı yine üretilmeliydi (PaftaTipi filtresi).");
    }

    // A11: Kapasite muhasebesi — ikinci kablolama dolu anahtarı atlayıp yeni armatürleri
    // boş anahtara vermeli (eskiden dolu anahtara üstüne ekleyip kapasiteyi aşıyordu).
    public static void KapasiteMuhasebesiDoluAnahtariAtlar()
    {
        var kat = new BasitProjeKat();
        var oda = new BasitProjeOda { Ad = "Oda", Poligon = [new(0, 0), new(8, 0), new(8, 8), new(0, 8)] };
        kat.Odalar.Add(oda);

        var anahtar1 = new BasitProjeEleman { Id = "a1", Tip = BpcElemanTipi.Anahtar, Ad = "A1", OdaId = oda.Id, X = 0.1, Y = 0.1 };
        var anahtar2 = new BasitProjeEleman { Id = "a2", Tip = BpcElemanTipi.Anahtar, Ad = "A2", OdaId = oda.Id, X = 7.9, Y = 7.9 };
        kat.Elemanlar.Add(anahtar1);
        kat.Elemanlar.Add(anahtar2);
        for (int i = 0; i < 9; i++)
            kat.Elemanlar.Add(new BasitProjeEleman { Tip = BpcElemanTipi.Armatür, Ad = $"L{i}", BlokAdi = "E4P_LIGHT", OdaId = oda.Id, X = 1 + i * 0.5, Y = 1 });

        BasitProjeCizKabloServisi.KatAydinlatmasiniOtomatikKablola(kat);
        if (BasitProjeCizKabloServisi.AnahtarMevcutYuk(kat, "a1") != 9)
            throw new InvalidOperationException("İlk kablolamada A1 kapasitesi (9) dolmalıydı.");

        // 3 yeni armatür → dolu A1 atlanıp A2'ye gitmeli
        for (int i = 0; i < 3; i++)
            kat.Elemanlar.Add(new BasitProjeEleman { Tip = BpcElemanTipi.Armatür, Ad = $"N{i}", BlokAdi = "E4P_LIGHT", OdaId = oda.Id, X = 6, Y = 6 + i * 0.4 });

        BasitProjeCizKabloServisi.KatAydinlatmasiniOtomatikKablola(kat);

        int yuk1 = BasitProjeCizKabloServisi.AnahtarMevcutYuk(kat, "a1");
        int yuk2 = BasitProjeCizKabloServisi.AnahtarMevcutYuk(kat, "a2");
        if (yuk1 != 9)
            throw new InvalidOperationException($"A1 kapasitesi aşılmamalı (9). Bulunan: {yuk1}.");
        if (yuk2 != 3)
            throw new InvalidOperationException($"Yeni 3 armatür boş A2'ye atanmalıydı. Bulunan: {yuk2}.");
    }

    // A8: "Açıklık Yanı" prizleri kapı çevresine toplanır; odanın uzak duvarına dağılmaz.
    public static void AciklikYaniPrizlerKapiYaninaToplanir()
    {
        var oda = new BasitProjeOda { Ad = "Oda", Poligon = [new(0, 0), new(6, 0), new(6, 6), new(0, 6)] };
        var kapi = new BpcAciklik { Bas = new(0.8, 0), Bit = new(1.7, 0), Tip = BpcAciklikTipi.Kapı };
        var tanim = new BpcArmatürTanim { Ad = "Priz", BlokAdi = "E4P_PRIZ", SembolAdi = "P", MontajTipi = "Duvar" };

        var servis = new BasitProjeCizOtomatikYerlestirServisi(new BasitProjeCizGeometriServisi());
        var yeni = servis.OdayaYerlestir(
            oda, tanim, "Açıklık Yanı", "Duvar", 3.0, 1.0, 3,
            [kapi], [], false, out _);

        if (yeni.Count != 3)
            throw new InvalidOperationException($"3 priz bekleniyordu. Bulunan: {yeni.Count}.");

        var kapiOrta = new Point2D(1.25, 0);
        foreach (var p in yeni)
        {
            double d = Math.Sqrt((p.X - kapiOrta.X) * (p.X - kapiOrta.X) + (p.Y - kapiOrta.Y) * (p.Y - kapiOrta.Y));
            if (d > 3.5)
                throw new InvalidOperationException(
                    $"Açıklık Yanı prizi kapı çevresinde kalmalı (≤3.5 m). Bulunan: ({p.X:0.##},{p.Y:0.##}) → {d:0.##} m.");
        }
    }

    // A4: Undo/redo tali panonun kullanıcı seçimli üst kat panosu atamasını korur.
    public static void UndoTaliPanoHedefiniKorur()
    {
        var vm = new BasitProjeCizViewModel();
        var kat = new BasitProjeKat { Ad = "Kat", KatNo = 1 };
        var tali = new BasitProjeEleman
        {
            Id = "tp", Tip = BpcElemanTipi.KatPanosu, BlokAdi = "E4P_TALI_PANO",
            Ad = "TP", X = 1, Y = 1, TaliPanoHedefPanoId = "kp-hedef"
        };
        var armatur = new BasitProjeEleman { Id = "l1", Tip = BpcElemanTipi.Armatür, Ad = "L", X = 2, Y = 2 };
        kat.Elemanlar.Add(tali);
        kat.Elemanlar.Add(armatur);
        vm.Katlar.Add(kat);
        vm.SeciliKat = kat;

        vm.ManuelElemanKonumGuncelle(armatur, 3, 3); // snapshot alınır
        vm.ElemanGeriAlCommand.Execute(null);        // snapshot'tan geri yükle

        var geriYuklenen = kat.Elemanlar.First(e => e.Id == "tp");
        if (geriYuklenen.TaliPanoHedefPanoId != "kp-hedef")
            throw new InvalidOperationException("Undo sonrası TaliPanoHedefPanoId korunmalı (CloneEleman kapsamı).");
    }

    // A7: Mahal satırı silme undo ile geri gelir (snapshot mahal listesini de kapsar).
    public static void UndoMahalSatiriniGeriGetirir()
    {
        var vm = new BasitProjeCizViewModel();
        var kat = new BasitProjeKat { Ad = "Kat", KatNo = 1 };
        var satir = new BpcMahalSatiri { Tip = BpcElemanTipi.Armatür, ArmatürTanimAd = "Test LED" };
        kat.MahalListesi.Add(satir);
        vm.Katlar.Add(kat);
        vm.SeciliKat = kat;

        vm.MahalSatiriKaldir(kat, satir);
        if (kat.MahalListesi.Count != 0)
            throw new InvalidOperationException("Satır silinmeliydi.");

        vm.ElemanGeriAlCommand.Execute(null);
        if (kat.MahalListesi.Count != 1 || kat.MahalListesi[0].ArmatürTanimAd != "Test LED")
            throw new InvalidOperationException("Undo mahal satırını geri getirmeli (snapshot kapsamı).");
    }

    // K1/K4 (2026-07-19 kablo modu UX): armatür tıklaması rotayı ANINDA çizer
    // ("Hesapla" adımı yok); aynı armatüre ikinci tık devreden çıkarır.
    public static void KabloModuTiklamaCanliRotaCizerVeIkinciTikCikarir()
    {
        var vm = new BasitProjeCizViewModel();
        var kat = new BasitProjeKat { Ad = "Kat", KatNo = 1 };
        var oda = new BasitProjeOda { Ad = "Oda", Poligon = [new(0, 0), new(8, 0), new(8, 8), new(0, 8)] };
        kat.Odalar.Add(oda);
        var anahtar = new BasitProjeEleman { Id = "sw", Tip = BpcElemanTipi.Anahtar, Ad = "A", OdaId = oda.Id, X = 0.2, Y = 0.2 };
        var armatur = new BasitProjeEleman { Id = "lm", Tip = BpcElemanTipi.Armatür, Ad = "L", OdaId = oda.Id, X = 4, Y = 4 };
        kat.Elemanlar.Add(anahtar);
        kat.Elemanlar.Add(armatur);
        vm.Katlar.Add(kat);
        vm.SeciliKat = kat;
        vm.KabloModuToggle();

        vm.KabloModuTiklama(0.2, 0.2); // anahtar seç
        vm.KabloModuTiklama(4, 4);     // armatür ata → canlı rota

        var devre = kat.Devreler.FirstOrDefault(d => d.AnahtarElemanId == "sw");
        if (devre == null || devre.ArmaturElemanIds.Count != 1)
            throw new InvalidOperationException("Armatür tıklaması devreye atamalıydı.");
        if (!kat.Kablolar.Any(k => k.DevreId == devre.Id && k.Rota.Count >= 2))
            throw new InvalidOperationException("Canlı rota: atama tıklamasında kablo hemen çizilmeli (Hesapla adımı olmadan).");
        if (string.IsNullOrEmpty(vm.KabloAdimGosterge))
            throw new InvalidOperationException("Adım göstergesi dolu olmalı.");

        vm.KabloModuTiklama(4, 4); // ikinci tık → çıkar
        if (devre.ArmaturElemanIds.Count != 0)
            throw new InvalidOperationException("Aynı armatüre ikinci tık devreden çıkarmalı.");

        // Esc karşılığı: aktif hat iptali durumu sıfırlar
        vm.KabloModuHattiIptal();
        if (vm.KabloAktifHatVarMi)
            throw new InvalidOperationException("Hat iptalinden sonra aktif hat kalmamalı.");
    }

    // K12: Duvar graf önbelleği — aynı geometriyle rotalar deterministik;
    // oda geometrisi değişince önbellek ıskalar ve rota yeni geometriyi izler.
    public static void DuvarGrafOnbellegiDeterministikVeGecersizlesir()
    {
        var kat = new BasitProjeKat();
        var sol = new BasitProjeOda { Ad = "Sol", Poligon = [new(0, 0), new(3, 0), new(3, 3), new(0, 3)] };
        var orta = new BasitProjeOda { Ad = "Orta", GecisSecili = true, Poligon = [new(3, 0), new(6, 0), new(6, 3), new(3, 3)] };
        var sag = new BasitProjeOda { Ad = "Sağ", Poligon = [new(6, 0), new(9, 0), new(9, 3), new(6, 3)] };
        kat.Odalar.Add(sol);
        kat.Odalar.Add(orta);
        kat.Odalar.Add(sag);

        var buat = new BasitProjeEleman { Tip = BpcElemanTipi.Buat, Ad = "B", OdaId = sol.Id, X = 1, Y = 1.5 };
        var pano = new BasitProjeEleman { Tip = BpcElemanTipi.KatPanosu, Ad = "KP", OdaId = sag.Id, X = 8, Y = 1.5 };
        kat.Elemanlar.Add(buat);
        kat.Elemanlar.Add(pano);
        BasitProjeCizKabloServisi.BuatPanoBagla(kat, buat.Id, pano.Id, [orta.Id]);

        BasitProjeCizKabloServisi.BuatPanoRotalariniHesapla(kat);
        var rota1 = kat.Kablolar.Single(k => k.Tip == BpcKabloTipi.PanoKablosu).Rota
            .Select(p => (Math.Round(p.X, 6), Math.Round(p.Y, 6))).ToList();

        // Aynı geometriyle ikinci hesap (önbellekten) birebir aynı rotayı vermeli.
        BasitProjeCizKabloServisi.BuatPanoRotalariniHesapla(kat);
        var rota2 = kat.Kablolar.Single(k => k.Tip == BpcKabloTipi.PanoKablosu).Rota
            .Select(p => (Math.Round(p.X, 6), Math.Round(p.Y, 6))).ToList();
        if (!rota1.SequenceEqual(rota2))
            throw new InvalidOperationException("Önbellekli ikinci hesap aynı rotayı üretmeli (determinizm).");

        // Geometri değişikliği: sağ oda büyür → anahtar değişir, rota yeni geometride hesaplanır.
        sag.Poligon = [new(6, 0), new(12, 0), new(12, 3), new(6, 3)];
        pano.X = 11;
        BasitProjeCizKabloServisi.BuatPanoRotalariniHesapla(kat);
        var rota3 = kat.Kablolar.Single(k => k.Tip == BpcKabloTipi.PanoKablosu).Rota;
        if (rota3.Count < 2)
            throw new InvalidOperationException("Geometri değişince rota yeniden hesaplanabilmeli (önbellek ıskalamalı).");
        if (Math.Abs(rota3[^1].X - 11) > 0.9)
            throw new InvalidOperationException("Rota yeni pano konumunda bitmeli (bayat önbellek kullanılmamalı).");
    }

    // M6: Tüm Katlara Yerleştir — birden fazla katta çalışır, her katta doğru
    // yerleştirme motorunu kullanır (kat seçimini de kısa süreliğine değiştirip geri getirir).
    public static void TumKatlaraYerlestirBirdenFazlaKatiIsler()
    {
        var vm = new BasitProjeCizViewModel();
        var kutuphane = new BasitProjeCizKutuphaneServisi();
        var tanim = kutuphane.Armatürler.First();
        var tip = BasitProjeCizKutuphaneServisi.TipBlokAdindan(tanim.BlokAdi) ?? BpcElemanTipi.Armatür;

        BasitProjeKat KatOlustur(string ad)
        {
            var kat = new BasitProjeKat { Ad = ad, KatNo = vm.Katlar.Count + 1 };
            var oda = new BasitProjeOda { Ad = "Oda", Poligon = [new(0, 0), new(4, 0), new(4, 4), new(0, 4)] };
            kat.Odalar.Add(oda);
            var satir = new BpcMahalSatiri { Tip = tip, ArmatürTanimId = tanim.Id, YerlesimTipi = "Adet" };
            var ayar = satir.HucreGetirVeyaOlustur(oda.Id);
            ayar.Aktif = true; ayar.Adet = 2;
            kat.MahalListesi.Add(satir);
            return kat;
        }

        var kat1 = KatOlustur("Zemin");
        var kat2 = KatOlustur("1. Kat");
        var katBos = new BasitProjeKat { Ad = "Boş Kat", KatNo = 3 }; // mahal listesi yok → atlanmalı
        vm.Katlar.Add(kat1);
        vm.Katlar.Add(kat2);
        vm.Katlar.Add(katBos);
        vm.SeciliKat = kat1;

        vm.AydinlatmaAsamasi = BpcAydinlatmaAsamasi.Armatur;
        var rapor = vm.TumKatlaraYerlestir();

        if (kat1.Elemanlar.Count == 0 || kat2.Elemanlar.Count == 0)
            throw new InvalidOperationException("Her iki kat da yerleştirilmeliydi.");
        if (rapor.Contains("Boş Kat"))
            throw new InvalidOperationException("Mahal listesi olmayan kat rapora girmemeliydi.");
        if (!ReferenceEquals(vm.SeciliKat, kat1))
            throw new InvalidOperationException("İşlem sonunda önceki seçili kata dönülmeliydi.");
        vm.ElemanGeriAlCommand.Execute(null);
        if (kat1.Elemanlar.Count != 0 || kat2.Elemanlar.Count != 0)
            throw new InvalidOperationException("Tüm katlara yerleştirme tek işlem olarak geri alınmalı.");
        vm.ElemanIleriAlCommand.Execute(null);
        if (kat1.Elemanlar.Count == 0 || kat2.Elemanlar.Count == 0)
            throw new InvalidOperationException("İleri alma iki kattaki yerleşimi de geri getirmeli.");
    }

    // A1: IsDirty — mutasyon kirletir, başarılı kayıt temizler.
    public static void IsDirtyMutasyondaKirlenirKayittaTemizlenir()
    {
        var vm = new BasitProjeCizViewModel();
        var kat = new BasitProjeKat { Ad = "Kat", KatNo = 1 };
        var el = new BasitProjeEleman { Tip = BpcElemanTipi.Armatür, Ad = "L", X = 1, Y = 1 };
        kat.Elemanlar.Add(el);
        vm.Katlar.Add(kat);
        vm.SeciliKat = kat;

        if (vm.IsDirty)
            throw new InvalidOperationException("Yeni VM temiz başlamalı.");

        vm.ManuelElemanKonumGuncelle(el, 2, 2);
        if (!vm.IsDirty)
            throw new InvalidOperationException("Eleman taşıma IsDirty işaretlemeli.");
        if (!vm.PencereBasligi.StartsWith("* "))
            throw new InvalidOperationException("Kirli projede pencere başlığı '*' ile başlamalı.");

        var tmp = Path.Combine(Path.GetTempPath(), $"m12-isdirty-{Guid.NewGuid():N}.e4p12");
        try
        {
            if (!vm.ProjeyiKaydet(tmp))
                throw new InvalidOperationException("Kayıt başarılı olmalıydı.");
            if (vm.IsDirty)
                throw new InvalidOperationException("Başarılı kayıt IsDirty'yi temizlemeli.");
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }
}
