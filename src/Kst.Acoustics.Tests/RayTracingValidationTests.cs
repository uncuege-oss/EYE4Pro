using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Simulation;

namespace Kst.Acoustics.Tests;

/// <summary>
/// Hacim akustiği hesap motorunun (RayTracingService + AcousticParameterCalculator)
/// sayısal doğruluğunu analitik referanslara karşı doğrulayan testler.
///
/// Bu testler "girdi kalitesini" değil, motorun ürettiği fiziği denetler:
///   - Serbest alan ters-kare yasası (alıcı tespiti + yayılım geometrisi)
///   - Ses hızı / geliş zamanı tutarlılığı
///   - Sabine/Eyring kutusu RT60 (bozunma fiziği + enerji ölçek tutarlılığı)
///   - Emilim ↔ RT60 monotonluğu (materyal uygulamasının yönü)
///   - Seed ile tekrar üretilebilirlik (deterministik koşu)
///
/// Determinizm için tüm koşular sabit Seed kullanır.
/// </summary>
public static class RayTracingValidationTests
{
    // ── Serbest alan: direkt ses ters-kare yasasına uyar ──────────────────────
    // Omni kaynaktan N ışın 4π'ye düzgün dağılır. d mesafedeki R yarıçaplı alıcı
    // küresine isabet oranı ≈ R²/(4d²). İki mesafe için isabet oranı (d2/d1)²
    // olmalı; ayrıca direkt gelişin zamanı d/c'ye eşit olmalı.
    public static void FreeFieldDirectSoundFollowsInverseSquare()
    {
        const double d1 = 2.0;
        const double d2 = 4.0;
        const double radius = 0.3;
        const int rayCount = 200_000;
        const double speed = 343.0; // m/s (proje birimi = metre)

        // Alıcı tespiti yalnızca ışın bir yüzeye çarpınca (segment oluşunca) yapılır.
        // Serbest alanı taklit etmek için kaynağı/alıcıları çok büyük bir kapalı
        // kutunun merkezine koyarız: her ışın uzak duvara gider, MaxOrder=0 ile
        // hiç yansıma kaydedilmez → yalnızca direkt ses ölçülür.
        const double box = 60.0, c0 = 30.0;
        var project = new AcousticsProject(
            "SerbestAlan", null, DateTime.UtcNow,
            SurfaceImportFormat.Dxf, SurfaceImportUnit.Meters,
            BoxSurfaces(box, box, box, "W"),
            Sources:   [new AcousticPoint(1, "S", AcousticPointType.Source,   new Point3D(c0, c0, c0))],
            Receivers: [
                new AcousticPoint(2, "R1", AcousticPointType.Receiver, new Point3D(c0 + d1, c0, c0)),
                new AcousticPoint(3, "R2", AcousticPointType.Receiver, new Point3D(c0 + d2, c0, c0))
            ]);

        var p = new SimulationParameters(
            RayCount: rayCount, MaxOrder: 0, ReceiverRadius: radius,
            IsmOrder: 0, TransitionOrder: 1_000_000, Seed: 12345);

        var result = new RayTracingService().Run(project, project.CategoryMaterials, p);

        var eg1 = result.Echograms.First(e => e.ReceiverId == 2);
        var eg2 = result.Echograms.First(e => e.ReceiverId == 3);
        int n1 = eg1.Arrivals.Count;
        int n2 = eg2.Arrivals.Count;

        AssertTrue(n1 > 200 && n2 > 50, $"Direkt ses isabet sayıları yetersiz (n1={n1}, n2={n2}).");

        // Ters-kare: n1/n2 ≈ (d2/d1)² = 4.
        double ratio = n1 / (double)n2;
        double expectedRatio = (d2 / d1) * (d2 / d1);
        AssertRelative(expectedRatio, ratio, 0.15,
            $"Direkt ses ters-kare oranı (n1/n2={ratio:F2}, beklenen {expectedRatio:F2}).");

        // Mutlak tespit olasılığı: n1 ≈ N·R²/(4·d1²).
        double expectedN1 = rayCount * radius * radius / (4.0 * d1 * d1);
        AssertRelative(expectedN1, n1, 0.20,
            $"d1 isabet sayısı (n1={n1}, beklenen {expectedN1:F0}).");

        // Geliş zamanı = d/c (yarıçap nedeniyle ±R/c tolerans).
        double tMin1 = eg1.Arrivals.Min(a => a.TimeSeconds);
        AssertClose(d1 / speed, tMin1, radius / speed + 1e-4,
            $"Direkt ses geliş zamanı (t={tMin1 * 1000:F2} ms).");
    }

    // ── Sabine/Eyring kutusu: ray-traced RT60 ≈ analitik Eyring ────────────────
    // Reverb kuyruğu kapatılır (TransitionOrder çok yüksek) → RT60 tamamen
    // ışın izlemenin ürettiği bozunma eğrisinden gelir, enjekte edilen Eyring
    // kuyruğundan DEĞİL. Bu, dairesel olmayan gerçek bir doğrulamadır.
    public static void SabineBoxRt60MatchesEyring()
    {
        const double lx = 6, ly = 4, lz = 3;
        double v = lx * ly * lz;            // 72 m³
        double sTotal = 2 * (lx * ly + lx * lz + ly * lz); // 108

        // ÖNEMLİ: Eyring'in geçerli olması için TÜM yüzeylerde tek materyal
        // (bant başına düzgün emilim) kullanılır. Emici zemin + sert duvar gibi
        // düzgün-olmayan emilim, ışın izlemede enerji hapsine yol açar ve Eyring'i
        // geçersiz kılar (gerçek fizik, motor hatası değil). Düzgün emilimde
        // saf-specular ışın izleme bile ergodik limitte Eyring'e yakınsar.
        var mat = MaterialLibrary.FindById("carpet_thin")!;
        var catMat = new Dictionary<string, string> { ["ZEMIN"] = mat.Id, ["DUVAR"] = mat.Id };

        var project = BuildBox(lx, ly, lz, catMat, v,
            source:   new Point3D(1.5, 1.0, 1.5),
            receiver: new Point3D(4.5, 3.0, 1.5));

        // Reverb kuyruğu kapalı (TransitionOrder çok yüksek) → RT60 tamamen ışın
        // izlemenin bozunma eğrisinden gelir, enjekte edilen Eyring kuyruğundan değil.
        var p = new SimulationParameters(
            RayCount: 40_000, MaxOrder: 250, ReceiverRadius: 0.5,
            IsmOrder: 1, TransitionOrder: 1_000_000, Seed: 4242);

        var result = new RayTracingService().Run(project, catMat, p);
        var ap = AcousticParameterCalculator.Compute(result.Echograms[0], p);

        // Analitik Eyring (düzgün bant emilimi).
        foreach (var freq in new[] { 500, 1000, 2000 })
        {
            int bi = BandIndex(freq);
            double alpha = mat.Alpha[bi];
            double eyring = 0.161 * v / (-sTotal * Math.Log(1.0 - alpha));

            double rt = ap.Bands.First(b => b.FrequencyHz == freq).RT60;
            AssertFiniteVal(rt, $"{freq} Hz RT60");
            AssertRelative(eyring, rt, 0.35,
                $"{freq} Hz ray-traced RT60={rt:F2}s, Eyring={eyring:F2}s (α={alpha:F2}).");
        }
    }

    // ── Emilim arttıkça RT60 azalır (materyal uygulamasının yönü) ──────────────
    public static void MoreAbsorptionShortensRt60()
    {
        const double lx = 6, ly = 4, lz = 3;
        double v = lx * ly * lz;

        AcousticsProject Build(string matId) => BuildBox(
            lx, ly, lz,
            new Dictionary<string, string> { ["ZEMIN"] = matId, ["DUVAR"] = matId }, v,
            source:   new Point3D(1.5, 1.0, 1.5),
            receiver: new Point3D(4.5, 3.0, 1.5));

        var p = new SimulationParameters(RayCount: 20_000, MaxOrder: 150, ReceiverRadius: 0.5, Seed: 7);

        var svc = new RayTracingService();
        var lowAbs  = Build("plaster");       // düşük emilim → uzun RT
        var highAbs = Build("carpet_thick");  // yüksek (orta-bant) emilim → kısa RT

        var rtLow  = AcousticParameterCalculator.Compute(
            svc.Run(lowAbs,  lowAbs.CategoryMaterials,  p).Echograms[0], p).RT60_Mid;
        var rtHigh = AcousticParameterCalculator.Compute(
            svc.Run(highAbs, highAbs.CategoryMaterials, p).Echograms[0], p).RT60_Mid;

        AssertFiniteVal(rtLow, "Düşük emilim RT60_Mid");
        AssertFiniteVal(rtHigh, "Yüksek emilim RT60_Mid");
        AssertTrue(rtLow > rtHigh * 1.5,
            $"Daha yüksek emilim RT60'ı belirgin düşürmeli (düşük={rtLow:F2}s, yüksek={rtHigh:F2}s).");
    }

    // ── Aynı Seed ile koşu tekrar üretilebilir ────────────────────────────────
    public static void SeededSimulationIsReproducible()
    {
        const double lx = 6, ly = 4, lz = 3;
        double v = lx * ly * lz;
        var catMat = new Dictionary<string, string> { ["ZEMIN"] = "plaster", ["DUVAR"] = "plaster" };
        var project = BuildBox(lx, ly, lz, catMat, v,
            source:   new Point3D(1.5, 1.0, 1.5),
            receiver: new Point3D(4.5, 3.0, 1.5));

        var p = new SimulationParameters(RayCount: 15_000, MaxOrder: 120, ReceiverRadius: 0.5, Seed: 999);

        var svc = new RayTracingService();
        var run1 = AcousticParameterCalculator.Compute(svc.Run(project, catMat, p).Echograms[0], p);
        var run2 = AcousticParameterCalculator.Compute(svc.Run(project, catMat, p).Echograms[0], p);

        AssertFiniteVal(run1.RT60_Mid, "Koşu 1 RT60_Mid");
        AssertClose(run1.RT60_Mid, run2.RT60_Mid, 1e-6,
            $"Aynı Seed iki koşuda aynı RT60 vermeli (r1={run1.RT60_Mid:F6}, r2={run2.RT60_Mid:F6}).");

        // Farklı Seed: sonuç istatistiksel olarak aynı civarda ama bit-aynı değil.
        var pAlt = p with { Seed = 1000 };
        var run3 = AcousticParameterCalculator.Compute(svc.Run(project, catMat, pAlt).Echograms[0], p);
        AssertTrue(Math.Abs(run3.RT60_Mid - run1.RT60_Mid) > 0,
            "Farklı Seed birebir aynı sonucu vermemeli (RNG bağımlılığı doğrulanır).");
    }

    // ── Fiziksel invariantlar gerçek bir koşuda korunur ───────────────────────
    // Bozunma kalitesi alanlarının (R², dinamik aralık) dolduğunu ve temel
    // akustik tutarlılıkların (C80≥C50, D50∈[0,1], RT pozitif, R²≈1) sağlandığını
    // denetler — binleme/regresyon zincirinin sağlığı için regresyon kalkanı.
    public static void PhysicalInvariantsHoldOnSabineBox()
    {
        const double lx = 6, ly = 4, lz = 3;
        double v = lx * ly * lz;
        var catMat = new Dictionary<string, string> { ["ZEMIN"] = "carpet_thin", ["DUVAR"] = "carpet_thin" };
        var project = BuildBox(lx, ly, lz, catMat, v,
            source:   new Point3D(1.5, 1.0, 1.5),
            receiver: new Point3D(4.5, 3.0, 1.5));

        var p = new SimulationParameters(
            RayCount: 40_000, MaxOrder: 250, ReceiverRadius: 0.5,
            IsmOrder: 1, TransitionOrder: 1_000_000, Seed: 2024);

        var ap = AcousticParameterCalculator.Compute(
            new RayTracingService().Run(project, catMat, p).Echograms[0], p);

        var b1k = ap.Bands.First(b => b.FrequencyHz == 1000);

        AssertTrue(b1k.RT60 > 0, $"RT60 pozitif olmalı (RT60={b1k.RT60:F3}).");
        AssertTrue(b1k.C80 >= b1k.C50 - 1e-6,
            $"C80, C50'den küçük olamaz (C50={b1k.C50:F2}, C80={b1k.C80:F2}).");
        AssertBetweenVal(b1k.D50, 0.0, 1.0, "D50");
        AssertTrue(b1k.Ts >= 0, $"Ts negatif olamaz (Ts={b1k.Ts:F1} ms).");

        // Binleme/regresyon kalite alanları dolu ve makul.
        AssertFiniteVal(b1k.DecayFitR2, "1k DecayFitR2");
        AssertTrue(b1k.DecayFitR2 > 0.95,
            $"Düzgün kutuda bozunma çok doğrusal olmalı (R²={b1k.DecayFitR2:F3}).");
        AssertFiniteVal(b1k.DynamicRangeDb, "1k DynamicRangeDb");
        AssertTrue(b1k.DynamicRangeDb <= -35.0,
            $"Bozunma en az -35 dB'e inmeli (ulaşılan={b1k.DynamicRangeDb:F0} dB).");
    }

    // ── Yönlülük pattern'leri toplam gücü korur (A1 güç-normalizasyonu) ────────
    // Güç-normalize edilmiş cardioid/figure8/supercardioid omni ile AYNI toplam
    // gücü yaymalı. Yansıtıcı odada reverberant enerji yalnız kaynak gücüne bağlı;
    // kaynağı çevreleyen SİMETRİK alıcılarla yön sapması iptal olur → toplam ≈ omni.
    // (A1 öncesi cardioid omni'nin ~1/3'ünü yayıyordu → bu test onu yakalar.)
    public static void DirectivityPatternsConserveTotalPower()
    {
        const double lx = 6, ly = 4, lz = 3;
        double v = lx * ly * lz;

        double TotalEnergy(DirectivityPattern pattern)
        {
            var surfaces = BoxSurfaces(lx, ly, lz, "DUVAR");
            var catMat = new Dictionary<string, string> { ["DUVAR"] = "plaster" };
            var project = new AcousticsProject(
                "Kutu", null, DateTime.UtcNow, SurfaceImportFormat.Dxf, SurfaceImportUnit.Meters, surfaces,
                Sources:   [new AcousticPoint(1, "S", AcousticPointType.Source, new Point3D(3.0, 2.0, 1.5), pattern, 0, 0, 0)],
                Receivers: [
                    new AcousticPoint(2, "Rxp", AcousticPointType.Receiver, new Point3D(4.5, 2.0, 1.5)),
                    new AcousticPoint(3, "Rxn", AcousticPointType.Receiver, new Point3D(1.5, 2.0, 1.5)),
                    new AcousticPoint(4, "Ryp", AcousticPointType.Receiver, new Point3D(3.0, 3.0, 1.5)),
                    new AcousticPoint(5, "Ryn", AcousticPointType.Receiver, new Point3D(3.0, 1.0, 1.5))
                ],
                CategoryMaterials: catMat, RoomVolume: v);

            var p = new SimulationParameters(RayCount: 40_000, MaxOrder: 120, ReceiverRadius: 0.5, Seed: 123);
            var result = new RayTracingService().Run(project, catMat, p);
            double sum = 0;
            foreach (var eg in result.Echograms)
                foreach (var a in eg.Arrivals) sum += a.E1k;
            return sum;
        }

        double omni = TotalEnergy(DirectivityPattern.Omnidirectional);
        AssertTrue(omni > 0, "Omni toplam enerji pozitif olmalı.");

        foreach (var pattern in new[]
        {
            DirectivityPattern.Cardioid, DirectivityPattern.Figure8, DirectivityPattern.SuperCardioid
        })
        {
            double e = TotalEnergy(pattern);
            // Güç-normalize: toplam güç omni ile aynı mertebede (simetrik alıcı + MC
            // + kalıntı asimetri için %30 tolerans). A1 öncesi oran ~0.33 olurdu.
            AssertRelative(omni, e, 0.30,
                $"{pattern} toplam gücü omni ile eşleşmeli (oran {e / omni:F3}); güç-normalize bozulmuş olabilir.");
        }
    }

    // ── Sonuç birim-değişmez: metre vs milimetre aynı RT'yi vermeli (C2) ───────
    // Geometri, alıcı yarıçapı ve ses hızı hep proje birimindedir → zaman ve enerji
    // oranları birimden bağımsız olmalı. Geometri toleransları (yansıma offset'i,
    // gölge epsilon'u) birime ölçeklendiğinden mm projesi de aynı ışın yollarını izler.
    public static void ResultIsUnitInvariantMetersVsMillimeters()
    {
        const double lx = 6, ly = 4, lz = 3;
        double vM3 = lx * ly * lz;

        // Run'ın ürettiği ComputedParameters'ı kullan (UnitScale enjeksiyonu dahil) →
        // hem RT (geometri/recFactor) hem G (referans) birim-değişmezliğini sınar.
        (double Rt, double G) Compute(SurfaceImportUnit unit, double s)
        {
            var surfaces = BoxSurfaces(lx * s, ly * s, lz * s, "DUVAR");
            var catMat = new Dictionary<string, string> { ["DUVAR"] = "plaster" };
            var project = new AcousticsProject(
                "Kutu", null, DateTime.UtcNow, SurfaceImportFormat.Dxf, unit, surfaces,
                Sources:   [new AcousticPoint(1, "S", AcousticPointType.Source,   new Point3D(1.5 * s, 1.0 * s, 1.5 * s))],
                Receivers: [new AcousticPoint(2, "R", AcousticPointType.Receiver, new Point3D(4.5 * s, 3.0 * s, 1.5 * s))],
                CategoryMaterials: catMat, RoomVolume: vM3);   // RoomVolume daima m³

            var p = new SimulationParameters(RayCount: 20_000, MaxOrder: 120, ReceiverRadius: 0.5 * s, Seed: 55);
            var ap = new RayTracingService().Run(project, catMat, p).ComputedParameters![0];
            return (ap.RT60_Mid, ap.Bands.First(b => b.FrequencyHz == 1000).G);
        }

        var (rtM, gM)   = Compute(SurfaceImportUnit.Meters,      1.0);
        var (rtMm, gMm) = Compute(SurfaceImportUnit.Millimeters, 1000.0);

        AssertFiniteVal(rtM,  "Metre RT60_Mid");
        AssertFiniteVal(rtMm, "Milimetre RT60_Mid");
        AssertRelative(rtM, rtMm, 0.03,
            $"Metre ve mm aynı odada eşit RT vermeli (m={rtM:F3}s, mm={rtMm:F3}s); birim epsilon/recFactor ölçeği bozulmuş olabilir.");
        AssertFiniteVal(gM,  "Metre G(1k)");
        AssertFiniteVal(gMm, "Milimetre G(1k)");
        AssertClose(gM, gMm, 1.0,
            $"Metre ve mm aynı odada eşit G vermeli (m={gM:F2}dB, mm={gMm:F2}dB); G referansı birim varsayıyor olabilir.");
    }

    // ── Izgara haritası tüm kaynakları toplar (C1) ────────────────────────────
    // İkinci kaynağı bir hücrenin yanına koyunca o hücrenin netliği (C80) belirgin
    // artmalı. C1 bozuksa (yalnız sources[0] kullanılırsa) harita değişmez kalır.
    public static void GridMapCombinesAllSources()
    {
        const double lx = 8, ly = 6, lz = 3;
        double v = lx * ly * lz;
        var catMat = new Dictionary<string, string> { ["DUVAR"] = "plaster" };
        var grid = new ReceiverGrid(1.5, 1, 7, 1, 5, 1.0);   // 7 sütun × 5 satır

        GridMapResult RunGrid(AcousticPoint[] srcs)
        {
            var surfaces = BoxSurfaces(lx, ly, lz, "DUVAR");
            var project = new AcousticsProject(
                "Kutu", null, DateTime.UtcNow, SurfaceImportFormat.Dxf, SurfaceImportUnit.Meters, surfaces,
                Sources: srcs,
                // Izgara, erken-return korumasını geçmek için en az bir normal alıcı ister.
                Receivers: [new AcousticPoint(99, "R", AcousticPointType.Receiver, new Point3D(4, 3, 1.5))],
                CategoryMaterials: catMat, RoomVolume: v);
            var p = new SimulationParameters(RayCount: 20_000, MaxOrder: 100, ReceiverRadius: 0.5,
                                             Seed: 321, ReceiverGrid: grid);
            var map = new RayTracingService().Run(project, catMat, p).GridMap;
            AssertTrue(map != null, "Izgara haritası üretilmeli.");
            return map!;
        }

        var s1 = new AcousticPoint(1, "S1", AcousticPointType.Source, new Point3D(2.0, 2.0, 1.5));
        var s2 = new AcousticPoint(2, "S2", AcousticPointType.Source, new Point3D(6.5, 4.0, 1.5));

        var oneSrc = RunGrid([s1]);
        var twoSrc = RunGrid([s1, s2]);

        // Hücre (row=3, col=5) ≈ (6,4) → S2'ye 0.5 m. İkinci kaynak güçlü direkt ses ekler.
        double c80One = oneSrc.GetValue(GridMapParameter.C80, 3, 5);
        double c80Two = twoSrc.GetValue(GridMapParameter.C80, 3, 5);

        AssertFiniteVal(c80One, "Tek kaynak C80(3,5)");
        AssertFiniteVal(c80Two, "Çift kaynak C80(3,5)");
        AssertTrue(c80Two > c80One + 1.0,
            $"İkinci kaynak yakın hücrede netliği artırmalı (tek={c80One:F2}, çift={c80Two:F2} dB); ızgara tüm kaynakları toplamıyor olabilir.");
    }

    // ── Ayna modu: alıcı ısı haritası yüzey kimliğiyle beslenir ───────────────
    public static void LightTracingMirrorModeTagsReceiverHits()
    {
        const int mirrorId = 10;
        const int receiverId = 20;
        var surfaces = new List<ProjectSurface>
        {
            new(mirrorId, "Ayna Zemin", "DUVAR",
            [
                new(-5, -4, 0), new(5, -4, 0), new(5, 4, 0), new(-5, 4, 0)
            ]),
            new(receiverId, "Alıcı Perde", "DUVAR",
            [
                new(-2, 4, 0.4), new(2, 4, 0.4), new(2, 4, 2.6), new(-2, 4, 2.6)
            ])
        };

        var result = LightTracingService.Run(
            source: new Point3D(0, -2, 1.5),
            mirrorIds: new HashSet<int> { mirrorId },
            receiverIds: new HashSet<int> { receiverId },
            surfaces: surfaces,
            categoryMaterials: new Dictionary<string, string> { ["DUVAR"] = "plaster" },
            rayCount: 120,
            maxBounces: 2);

        AssertTrue(result.TotalHits > 0, "Ayna modunda alıcı yüzeye yansıyan ışın bulunmalı.");
        AssertTrue(result.ReceiverHitPoints.All(h => h.SurfaceId == receiverId),
            "Isı haritası hit noktaları alıcı yüzey ID'sini taşımalı.");
        AssertTrue(result.RayPaths.Count >= 2,
            "Ayna modunda en az bir kaynak→ayna ve ayna→alıcı segmenti çizime gitmeli.");
        AssertTrue(result.SurfaceIntensity.ContainsKey(receiverId),
            "Alıcı yüzey yoğunluk haritasında görünmeli.");
    }

    // ── Yardımcılar ───────────────────────────────────────────────────────────

    private static int BandIndex(int freq) => freq switch
    {
        63 => 0, 125 => 1, 250 => 2, 500 => 3,
        1000 => 4, 2000 => 5, 4000 => 6, 8000 => 7,
        _ => throw new ArgumentOutOfRangeException(nameof(freq))
    };

    private static List<ProjectSurface> BoxSurfaces(double lx, double ly, double lz, string category) =>
    [
        new(1, "Zemin",   category, [new(0,0,0),  new(lx,0,0),  new(lx,ly,0),  new(0,ly,0)]),
        new(2, "Tavan",   category, [new(0,0,lz), new(lx,0,lz), new(lx,ly,lz), new(0,ly,lz)]),
        new(3, "DuvarY0", category, [new(0,0,0),  new(lx,0,0),  new(lx,0,lz),  new(0,0,lz)]),
        new(4, "DuvarY1", category, [new(0,ly,0), new(lx,ly,0), new(lx,ly,lz), new(0,ly,lz)]),
        new(5, "DuvarX0", category, [new(0,0,0),  new(0,ly,0),  new(0,ly,lz),  new(0,0,lz)]),
        new(6, "DuvarX1", category, [new(lx,0,0), new(lx,ly,0), new(lx,ly,lz), new(lx,0,lz)])
    ];

    private static AcousticsProject BuildBox(
        double lx, double ly, double lz,
        IReadOnlyDictionary<string, string> categoryMaterials,
        double volume, Point3D source, Point3D receiver)
    {
        // Zemin "ZEMIN", diğer 5 yüzey "DUVAR" kategorisinde.
        var surfaces = BoxSurfaces(lx, ly, lz, "DUVAR");
        surfaces[0] = surfaces[0] with { Category = "ZEMIN" };

        return new AcousticsProject(
            "Kutu", null, DateTime.UtcNow,
            SurfaceImportFormat.Dxf, SurfaceImportUnit.Meters,
            surfaces,
            Sources:   [new AcousticPoint(1, "S", AcousticPointType.Source,   source)],
            Receivers: [new AcousticPoint(2, "R", AcousticPointType.Receiver, receiver)],
            CategoryMaterials: new Dictionary<string, string>(categoryMaterials),
            RoomVolume: volume);
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void AssertClose(double expected, double actual, double tolerance, string label)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException($"{label}: beklenen {expected:F4}, gerçek {actual:F4}, tolerans {tolerance:F4}.");
    }

    private static void AssertRelative(double expected, double actual, double relTolerance, string label)
    {
        if (expected == 0) throw new InvalidOperationException($"{label}: beklenen 0 olamaz.");
        var rel = Math.Abs(actual - expected) / Math.Abs(expected);
        if (rel > relTolerance)
            throw new InvalidOperationException($"{label}: bağıl sapma {rel:P1} > {relTolerance:P0}.");
    }

    private static void AssertFiniteVal(double value, string label)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new InvalidOperationException($"{label} sonlu değil ({value}).");
    }

    private static void AssertBetweenVal(double value, double min, double max, string label)
    {
        if (double.IsNaN(value) || value < min || value > max)
            throw new InvalidOperationException($"{label}: [{min}, {max}] beklenir, gerçek {value}.");
    }
}
