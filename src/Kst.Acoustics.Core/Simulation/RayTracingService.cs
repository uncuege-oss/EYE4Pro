using System.Collections.Concurrent;
using System.Diagnostics;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.Core.Simulation;

public sealed class RayTracingService
{
    // ─── ISO 9613-1 hava emilimi ─────────────────────────────────────────────

    private static readonly int[] BandFreqs = [63, 125, 250, 500, 1000, 2000, 4000, 8000];

    /// <summary>
    /// ISO 9613-1:1993 Bölüm 6.2 formülüne göre bant başına hava emilim katsayısı (dB/m).
    /// </summary>
    private static double[] ComputeAirAbsorptionDbPerMeter(double T_celsius, double RH_percent)
    {
        double T   = T_celsius + 273.15;
        const double T0  = 293.15;
        const double T01 = 273.16;

        double C     = -6.8346 * Math.Pow(T01 / T, 1.261) + 4.6151;
        double pvsat = Math.Pow(10, C);
        double h     = (RH_percent / 100.0) * pvsat;

        double frO = 24 + 4.04e4 * h * (0.02 + h) / (0.391 + h);
        double frN = Math.Sqrt(T0 / T) * (9 + 280 * h * Math.Exp(-4.170 * (Math.Pow(T0 / T, 1.0 / 3.0) - 1)));

        double[] result = new double[8];
        for (int i = 0; i < 8; i++)
        {
            double f  = BandFreqs[i];
            double f2 = f * f;
            result[i] = 8.686 * f2 * (
                1.84e-11 * Math.Sqrt(T / T0) +
                Math.Pow(T / T0, -2.5) * (
                    0.01275 * Math.Exp(-2239.1 / T) / (frO + f2 / frO) +
                    0.1068  * Math.Exp(-3352.0 / T) / (frN + f2 / frN)));
        }
        return result;
    }

    // ─── Hız ve birim dönüşümü ────────────────────────────────────────────────

    private static double SpeedInUnit(SurfaceImportUnit unit) => unit switch
    {
        SurfaceImportUnit.Meters      => 343.0,
        SurfaceImportUnit.Centimeters => 34_300.0,
        SurfaceImportUnit.Millimeters => 343_000.0,
        SurfaceImportUnit.Inches      => 13_503.937,
        SurfaceImportUnit.Feet        => 1125.328, // 343.0 / 0.3048
        _ => 343.0
    };

    private static double UnitScale(SurfaceImportUnit unit) => unit switch
    {
        SurfaceImportUnit.Meters      => 1.0,
        SurfaceImportUnit.Centimeters => 1.0 / 100.0,
        SurfaceImportUnit.Millimeters => 1.0 / 1000.0,
        SurfaceImportUnit.Inches      => 1.0 / 39.3701,
        SurfaceImportUnit.Feet        => 0.3048,
        _ => 1.0
    };

    // ─── Geç yansıma kuyruğu için RT60 tahmini ────────────────────────────────

    /// <summary>
    /// Üçgen veri setinden Eyring formülüyle per-bant RT60 tahmini yapar.
    /// Geçiş mertebesi kuyruğu hesaplaması için kullanılır.
    /// <paramref name="volumeOverrideM3"/> &gt; 0 ise (proje/kullanıcı hacmi) bounding-box
    /// tahmini yerine bu değer kullanılır — kutu olmayan odalarda doğru RT/kuyruk için.
    /// </summary>
    private static (double[] rt60, double V_m3) EstimateRT60Bands(RayTriangle[] triangles, double unitScale, double volumeOverrideM3 = 0)
    {
        double totalArea = 0;
        double[] absArea  = new double[8];

        // Bounding box for volume estimate
        double xmin = double.MaxValue, xmax = double.MinValue;
        double ymin = double.MaxValue, ymax = double.MinValue;
        double zmin = double.MaxValue, zmax = double.MinValue;

        foreach (ref readonly var tri in triangles.AsSpan())
        {
            // Triangle area via cross product
            double abx = tri.V1.X - tri.V0.X, aby = tri.V1.Y - tri.V0.Y, abz = tri.V1.Z - tri.V0.Z;
            double acx = tri.V2.X - tri.V0.X, acy = tri.V2.Y - tri.V0.Y, acz = tri.V2.Z - tri.V0.Z;
            double cx = aby*acz - abz*acy, cy = abz*acx - abx*acz, cz = abx*acy - aby*acx;
            double area = 0.5 * Math.Sqrt(cx*cx + cy*cy + cz*cz);
            totalArea += area;

            absArea[0] += area * (1 - tri.R0); absArea[1] += area * (1 - tri.R1);
            absArea[2] += area * (1 - tri.R2); absArea[3] += area * (1 - tri.R3);
            absArea[4] += area * (1 - tri.R4); absArea[5] += area * (1 - tri.R5);
            absArea[6] += area * (1 - tri.R6); absArea[7] += area * (1 - tri.R7);

            // Üçgenin üç köşesini dizi tahsisi olmadan kapsa (büyük meshlerde GC baskısı).
            xmin = Math.Min(xmin, Math.Min(tri.V0.X, Math.Min(tri.V1.X, tri.V2.X)));
            xmax = Math.Max(xmax, Math.Max(tri.V0.X, Math.Max(tri.V1.X, tri.V2.X)));
            ymin = Math.Min(ymin, Math.Min(tri.V0.Y, Math.Min(tri.V1.Y, tri.V2.Y)));
            ymax = Math.Max(ymax, Math.Max(tri.V0.Y, Math.Max(tri.V1.Y, tri.V2.Y)));
            zmin = Math.Min(zmin, Math.Min(tri.V0.Z, Math.Min(tri.V1.Z, tri.V2.Z)));
            zmax = Math.Max(zmax, Math.Max(tri.V0.Z, Math.Max(tri.V1.Z, tri.V2.Z)));
        }

        double us2 = unitScale * unitScale;
        double S_m2 = totalArea * us2;
        // Proje/kullanıcı hacmi geçerliyse onu kullan; yoksa bounding-box tahmini.
        double V_bbox = (xmax - xmin) * (ymax - ymin) * (zmax - zmin) * unitScale * unitScale * unitScale;
        double V_m3   = volumeOverrideM3 > 1e-6 ? volumeOverrideM3 : V_bbox;

        var rt60 = new double[8];
        for (int i = 0; i < 8; i++)
        {
            double alphaMean = totalArea > 0 ? absArea[i] / totalArea : 0.1;
            alphaMean = Math.Clamp(alphaMean, 0.001, 0.9999);
            // Eyring: RT60 = 0.161 × V / (−S × ln(1 − ᾱ))
            rt60[i] = S_m2 > 0 && V_m3 > 0
                ? AcousticConstants.SabineConstant * V_m3 / (-S_m2 * Math.Log(1.0 - alphaMean))
                : 1.0;
            rt60[i] = Math.Clamp(rt60[i], 0.05, 20.0);
        }
        return (rt60, V_m3);
    }

    // ─── Dış API ──────────────────────────────────────────────────────────────

    public SimulationResult Run(
        AcousticsProject                   project,
        IReadOnlyDictionary<string,string> categoryMaterials,
        SimulationParameters               p,
        IProgress<int>?                    progress = null,
        CancellationToken                  ct       = default)
    {
        var sw = Stopwatch.StartNew();
        ct.ThrowIfCancellationRequested();
        progress?.Report(1);

        var triangles = BuildTriangles(project.Surfaces, categoryMaterials, project.SurfaceMaterials, ct);
        progress?.Report(3);
        var bvh       = BuildBvh(triangles, ct);
        progress?.Report(5);
        // ISM görüntü-kaynak testleri yüzey başına üçgen grubunu kullanır. Üçgenler
        // koşu boyunca değişmediğinden bir kez kur; her kaynakta yeniden kurma (B4).
        var surfaceTriangles = BuildSurfaceTriangleMap(triangles, ct);
        var sources   = project.Sources;
        var receivers = project.Receivers;

        if (sources.Count == 0 || receivers.Count == 0 || triangles.Length == 0)
            return new SimulationResult([], p, sw.Elapsed);

        var echograms = new List<EchogramData>(sources.Count * receivers.Count);
        foreach (var s in sources)
            foreach (var r in receivers)
                echograms.Add(new EchogramData(s.Id, r.Id, s.Label, r.Label));

        var speedOfSound = SpeedInUnit(project.Unit);
        var unitScale    = UnitScale(project.Unit);
        // Geometri toleransları (yansıma offset'i, gölge-ışın epsilon'u) metre cinsinden
        // tanımlı; proje birimine ölçekle (proje-birimi/metre = 1/unitScale). Böylece
        // tolerans birimden bağımsız ~sabit fiziksel mesafe kalır — mm/inch projelerde
        // öz-kesişim/sızıntı sağlamlığı (C2). Metre projesinde epsScale=1 → değişim yok.
        var epsScale     = 1.0 / unitScale;
        // Akustik parametre hesabı (özellikle G referansı 10m) metre varsayar; birim
        // ölçeğini parametrelere enjekte et ki AcousticParameterCalculator doğru çevirsin.
        p = p with { UnitScale = unitScale };

        // Dinamik ISO 9613-1 hava emilimi
        var airAbsPerMeter = ComputeAirAbsorptionDbPerMeter(p.AirTemperatureCelsius, p.RelativeHumidity);
        var airAbs = airAbsPerMeter.Select(v => v * unitScale).ToArray();

        // Geç yansıma kuyruğu için RT60 tahmini.
        // Proje hacmi (signed-mesh ile hesaplanmış, m³) varsa kullanılır;
        // yoksa üçgen bounding-box'ından tahmin edilir.
        var (rt60Est, volEst) = EstimateRT60Bands(triangles, unitScale, project.RoomVolume);

        var totalRays   = sources.Count * p.RayCount;
        var doneRays    = 0;

        foreach (var source in sources)
        {
            ct.ThrowIfCancellationRequested();
            var src          = source;
            var srcEchograms = echograms.Where(e => e.SourceId == src.Id).ToArray();

            var srcDir = DirectionFromAngles(src.DirectivityAzimuthDeg, src.DirectivityElevationDeg);
            var (speaker, sensGain) = ResolveSpeaker(src);

            // ISM — yapılandırılabilir mertebe
            if (p.IsmOrder >= 1)
                AddIsmArrivals(src, srcDir, speaker, sensGain,
                               project.Surfaces, categoryMaterials, project.SurfaceMaterials,
                               triangles, bvh, surfaceTriangles, receivers, srcEchograms, p, speedOfSound, airAbs,
                               epsScale, ismOrder: p.IsmOrder, ct);

            // Stokastik ışın izleme
            var srcSalt   = unchecked(src.Id * 2_000_003);
            var tailSeeds = new ConcurrentBag<TailSeed>();
            Parallel.For(0, p.RayCount,
                new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = Environment.ProcessorCount },
                i =>
                {
                    var rng = RayRng(p.Seed, unchecked(srcSalt + i));
                    var dir = RandomOnSphere(rng);
                    Span<double> q = stackalloc double[8];
                    FillDirectivity(q, src, speaker, sensGain, dir, srcDir);
                    TraceRay(src.Position, dir, q, triangles, bvh, receivers,
                             srcEchograms, p, speedOfSound, airAbs, tailSeeds, epsScale, rng, ct);

                    var done = Interlocked.Increment(ref doneRays);
                    if (done % 500 == 0)
                        progress?.Report(5 + done * 70 / totalRays);
                });

            // Geç-yansıma kuyruğunu toplayıp alıcı-başına zaman histogramı olarak yay (B1).
            EmitReverbTail(tailSeeds, rt60Est, volEst, speedOfSound, unitScale, srcEchograms, p, srcSalt, ct);
        }

        foreach (var eg in echograms)
        {
            ct.ThrowIfCancellationRequested();
            eg.SortArrivals();
        }
        progress?.Report(76);

        // ─── Izgara harita simülasyonu ────────────────────────────────────────
        GridMapResult? gridMap = null;
        if (p.ReceiverGrid is { } gridDef && triangles.Length > 0 && sources.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var maskOutsideRoom = p.MaskGridOutsideRoom &&
                                  RoomVolumeCalculator.Estimate(project.Surfaces, project.Unit).Origin == RoomVolumeOrigin.ClosedMesh;
            gridMap = RunGridSimulation(
                gridDef, sources, project.Surfaces, categoryMaterials, project.SurfaceMaterials,
                triangles, bvh, surfaceTriangles, p, speedOfSound, airAbs, rt60Est, volEst, unitScale,
                maskOutsideRoom, ct);
        }
        progress?.Report(92);

        // Akustik parametreleri bir kez hesapla; hem Reliability analizi hem nihai
        // sonuç aynı diziyi paylaşır (eskiden Compute her ekogram için 2× koşuyordu — B2).
        var computedParams = new AcousticParameters[echograms.Count];
        for (var i = 0; i < echograms.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            computedParams[i] = AcousticParameterCalculator.Compute(echograms[i], p);
        }

        var analyzed = new SimulationResult(echograms, p, sw.Elapsed)
        {
            GridMap = gridMap,
            ComputedParameters = computedParams
        };
        ct.ThrowIfCancellationRequested();
        var reliability = SimulationReliabilityAnalyzer.Analyze(project, categoryMaterials, analyzed);
        ct.ThrowIfCancellationRequested();
        sw.Stop();
        progress?.Report(100);

        return new SimulationResult(echograms, p, sw.Elapsed)
        {
            GridMap = gridMap,
            Reliability = reliability,
            ComputedParameters = computedParams
        };
    }

    // ─── Izgara simülasyonu ───────────────────────────────────────────────────

    private static GridMapResult? RunGridSimulation(
        ReceiverGrid                       gridDef,
        IReadOnlyList<AcousticPoint>       sources,
        IReadOnlyList<ProjectSurface>      surfaces,
        IReadOnlyDictionary<string,string> categoryMaterials,
        IReadOnlyDictionary<string,string>? surfaceMaterials,
        RayTriangle[]                      triangles,
        BvhNode[]                          bvh,
        Dictionary<int, RayTriangle[]>     surfaceTriangles,
        SimulationParameters               p,
        double                             speedOfSound,
        double[]                           airAbs,
        double[]                           rt60Est,
        double                             volEst,
        double                             unitScale,
        bool                               maskOutsideRoom,
        CancellationToken                  ct)
    {
        int cols = gridDef.Cols;
        int rows = gridDef.Rows;
        if (cols <= 0 || rows <= 0 || gridDef.TotalCells > 10_000) return null;

        var epsScale = 1.0 / unitScale;   // geometri toleransı birim ölçeği (C2)

        // Tüm hücreleri önce oluştur. Kapalı kabukta oda dışı hücreler
        // simülasyona hiç sokulmaz; haritada NaN olarak kalır. Bu hem yanlış
        // "duvar dışı RT" değerlerini hem de gereksiz O(ışın×hücre) işi önler.
        var allPositions = new Point3D[rows * cols];
        int idx = 0;
        for (int r = 0; r < rows; r++)
        {
            ct.ThrowIfCancellationRequested();
            double y = gridDef.YMin + r * gridDef.CellSize;
            for (int c = 0; c < cols; c++)
            {
                double x   = gridDef.XMin + c * gridDef.CellSize;
                allPositions[idx] = new Point3D(x, y, gridDef.PlaneZ);
                idx++;
            }
        }

        var activeFlatIndices = Enumerable.Range(0, allPositions.Length)
            .Where(i => !maskOutsideRoom || IsPointInsideMesh(allPositions[i], triangles))
            .ToArray();

        // Izgara gerçekten bütünüyle oda dışında olabilir. Bu durumda bütün
        // hücreleri tekrar aktif etmek dışarıda sahte RT/C80 üretirdi; doğrudan
        // tamamı NaN bir harita döndür ve pahalı ışın döngüsünü atla.
        if (activeFlatIndices.Length == 0)
        {
            var emptyRt60 = new double[rows, cols];
            var emptyC80 = new double[rows, cols];
            var emptyC50 = new double[rows, cols];
            var emptySti = new double[rows, cols];
            FillWithNaN(emptyRt60);
            FillWithNaN(emptyC80);
            FillWithNaN(emptyC50);
            FillWithNaN(emptySti);
            return new GridMapResult(gridDef, cols, rows, emptyRt60, emptyC80, emptyC50, emptySti);
        }

        var gridReceivers = new AcousticPoint[activeFlatIndices.Length];
        var gridEchograms = new EchogramData[activeFlatIndices.Length];
        for (var i = 0; i < activeFlatIndices.Length; i++)
        {
            var flat = activeFlatIndices[i];
            var row = flat / cols;
            var col = flat % cols;
            var gid = 1_000_000 + flat;
            gridReceivers[i] = new AcousticPoint(gid, $"G{row}_{col}", AcousticPointType.Receiver, allPositions[flat]);
            gridEchograms[i] = new EchogramData(0, gid, "Tüm Kaynaklar", $"G{row}_{col}");
        }

        // Stokastik — azaltılmış ışın sayısı (harita için 1/5 yeterli), kaynak başına.
        int gridRayCount = Math.Max(500, p.RayCount / 5);
        var gridTailSeeds = new ConcurrentBag<TailSeed>();

        // Her kaynağın katkısını AYNI grid echogramlarına topla (enerji süperpozisyonu);
        // haritalanan RT/C80/C50/STI oran tabanlı olduğundan birleşik alanı yansıtır (C1).
        foreach (var source in sources)
        {
            ct.ThrowIfCancellationRequested();
            var srcDir = DirectionFromAngles(source.DirectivityAzimuthDeg, source.DirectivityElevationDeg);
            var (speaker, sensGain) = ResolveSpeaker(source);

            // ISM
            if (p.IsmOrder >= 1)
                AddIsmArrivals(source, srcDir, speaker, sensGain,
                               surfaces, categoryMaterials, surfaceMaterials, triangles, bvh, surfaceTriangles,
                               gridReceivers, gridEchograms, p, speedOfSound, airAbs, epsScale, ismOrder: 1, ct);

            var gridSalt = unchecked(source.Id * 2_000_003 + 999_983);
            Parallel.For(0, gridRayCount,
                new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = Environment.ProcessorCount },
                i =>
                {
                    var rng = RayRng(p.Seed, unchecked(gridSalt + i));
                    var dir = RandomOnSphere(rng);
                    Span<double> q = stackalloc double[8];
                    FillDirectivity(q, source, speaker, sensGain, dir, srcDir);
                    TraceRay(source.Position, dir, q, triangles, bvh, gridReceivers,
                             gridEchograms, p, speedOfSound, airAbs, gridTailSeeds, epsScale, rng, ct);
                });
        }

        // Tüm kaynakların kuyruk tohumları tek histogramda (diffüz alan süperpozisyonu).
        EmitReverbTail(gridTailSeeds, rt60Est, volEst, speedOfSound, unitScale, gridEchograms, p, 999_983, ct);

        foreach (var eg in gridEchograms)
        {
            ct.ThrowIfCancellationRequested();
            eg.SortArrivals();
        }

        // Akustik parametre hesabı
        var rt60Map = new double[rows, cols];
        var c80Map  = new double[rows, cols];
        var c50Map  = new double[rows, cols];
        var stiMap  = new double[rows, cols];
        FillWithNaN(rt60Map);
        FillWithNaN(c80Map);
        FillWithNaN(c50Map);
        FillWithNaN(stiMap);

        var gridParams = p with { RayCount = gridRayCount };
        for (int i = 0; i < gridEchograms.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var ap  = AcousticParameterCalculator.Compute(gridEchograms[i], gridParams);
            var flat = activeFlatIndices[i];
            int row = flat / cols;
            int col = flat % cols;
            rt60Map[row, col] = ap.RT60_Mid;
            var b1k = ap.Bands.FirstOrDefault(b => b.FrequencyHz == 1000);
            c80Map[row, col] = b1k?.C80 ?? double.NaN;
            c50Map[row, col] = b1k?.C50 ?? double.NaN;
            stiMap[row, col] = ap.STI;
        }

        return new GridMapResult(gridDef, cols, rows, rt60Map, c80Map, c50Map, stiMap);
    }

    private static void FillWithNaN(double[,] values)
    {
        for (var row = 0; row < values.GetLength(0); row++)
            for (var col = 0; col < values.GetLength(1); col++)
                values[row, col] = double.NaN;
    }

    private static bool IsPointInsideMesh(Point3D point, RayTriangle[] triangles)
    {
        // Eksenlere paralel olmayan sabit yön; köşe/kenar üzerinden geçme
        // olasılığını azaltır. Aynı düzlemdeki iki üçgenin ortak kenarında
        // yinelenen t değerleri tek kesişim sayılır.
        var direction = new Vector3D(1.0, 0.3713906763541037, 0.13750352374993502).Normalize();
        var distances = new List<double>();
        foreach (ref readonly var triangle in triangles.AsSpan())
        {
            if (RayTriangleIntersect(point, direction, in triangle, out var t) && t > 1e-7)
                distances.Add(t);
        }

        if (distances.Count == 0) return false;
        distances.Sort();
        var unique = 1;
        for (var i = 1; i < distances.Count; i++)
        {
            var tolerance = Math.Max(1e-7, Math.Abs(distances[i]) * 1e-9);
            if (Math.Abs(distances[i] - distances[i - 1]) > tolerance)
                unique++;
        }
        return (unique & 1) == 1;
    }

    // ─── Kaynak yönelimi ─────────────────────────────────────────────────────

    private static Vector3D DirectionFromAngles(double azDeg, double elDeg)
    {
        double az = azDeg * Math.PI / 180.0;
        double el = elDeg * Math.PI / 180.0;
        return new Vector3D(
            Math.Cos(el) * Math.Cos(az),
            Math.Cos(el) * Math.Sin(az),
            Math.Sin(el)).Normalize();
    }

    private static double DirectivityFactor(DirectivityPattern pattern, Vector3D rayDir, Vector3D srcDir)
    {
        if (pattern == DirectivityPattern.Omnidirectional) return 1.0;

        var rLen = Math.Sqrt(rayDir.X*rayDir.X + rayDir.Y*rayDir.Y + rayDir.Z*rayDir.Z);
        if (rLen < 1e-10) return 1.0;
        var rx = rayDir.X / rLen; var ry = rayDir.Y / rLen; var rz = rayDir.Z / rLen;

        double cosTheta = Math.Clamp(rx * srcDir.X + ry * srcDir.Y + rz * srcDir.Z, -1.0, 1.0);

        // Güç-normalize: çarpanlar pattern'in küre ortalamasını 1'e çeker → toplam
        // yayılan güç omni ile aynı, yönlülük indeksi (DI) korunur (A1). Aksi halde
        // cardioid omni'nin 1/3'ü kadar güç yayıp G/SPL'i sistematik düşürüyordu.
        //   Cardioid ((1+cosθ)/2)²  → ort 1/3      → ×3
        //   Figure8  cos²θ          → ort 1/3      → ×3
        //   SuperCard (0.366+0.634cosθ)² → ort 0.2679 → ×3.7322
        return pattern switch
        {
            DirectivityPattern.Cardioid      => 3.0      * Math.Pow((1 + cosTheta) / 2.0, 2),
            DirectivityPattern.SuperCardioid => 3.732157 * Math.Pow(0.366 + 0.634 * cosTheta, 2),
            DirectivityPattern.Figure8       => 3.0      * cosTheta * cosTheta,
            _                                => 1.0
        };
    }

    // Kaynak yönelimini bir kez çöz (hoparlör DB araması + bant duyarlılık kazançları
    // ışın yönünden bağımsızdır). Sonuç ışın döngüsünde tekrar tekrar kullanılır.
    private static (AcousticLoudspeaker? speaker, double[]? sensitivityGain) ResolveSpeaker(AcousticPoint source)
    {
        var speaker = string.IsNullOrWhiteSpace(source.LoudspeakerId)
            ? null
            : LoudspeakerDatabase.Instance.FindById(source.LoudspeakerId);

        if (speaker?.Balloons is not { Length: >= 8 })
            return (null, null);

        var gain = new double[8];
        for (var i = 0; i < 8; i++)
        {
            var sensitivityDb = speaker.Sensitivity != null && speaker.Sensitivity.Length > i && speaker.Sensitivity[i] > 0
                ? speaker.Sensitivity[i]
                : AcousticConstants.ReferenceSensitivityDb;
            gain[i] = Math.Pow(10.0, (sensitivityDb - AcousticConstants.ReferenceSensitivityDb) / 10.0);
        }
        return (speaker, gain);
    }

    // 8 bant yön faktörünü çağıranın verdiği tampona (stackalloc Span) yazar — ışın
    // başına dizi tahsisi yok. speaker/sensitivityGain ResolveSpeaker'dan gelir.
    private static void FillDirectivity(
        Span<double> dst, AcousticPoint source,
        AcousticLoudspeaker? speaker, double[]? sensitivityGain,
        Vector3D rayDir, Vector3D srcDir)
    {
        if (speaker is not null && sensitivityGain is not null)
        {
            var (theta, phi) = LocalAngles(rayDir, srcDir, source.DirectivityRollDeg);
            for (var i = 0; i < 8; i++)
            {
                var db = Math.Clamp(speaker.Balloons[i].GetAttenuation(theta, phi), -80.0, 20.0);
                dst[i] = sensitivityGain[i] * Math.Pow(10.0, db / 10.0);
            }
            return;
        }

        var q = DirectivityFactor(source.Directivity, rayDir, srcDir);
        dst.Fill(q);
    }

    private static (double Theta, double Phi) LocalAngles(Vector3D rayDir, Vector3D forward, double rollDeg)
    {
        var f = forward.Normalize();
        var r = rayDir.Normalize();
        if (f.Length < 1e-10 || r.Length < 1e-10) return (0, 0);

        var upRef = Math.Abs(f.Z) > 0.95
            ? new Vector3D(0, 1, 0)
            : new Vector3D(0, 0, 1);

        var right = GeometryMath.Cross(upRef, f).Normalize();
        var up = GeometryMath.Cross(f, right).Normalize();

        if (Math.Abs(rollDeg) > 1e-9)
        {
            var roll = rollDeg * Math.PI / 180.0;
            var cos = Math.Cos(roll);
            var sin = Math.Sin(roll);
            var rolledRight = (right * cos) + (up * sin);
            var rolledUp = (up * cos) - (right * sin);
            right = rolledRight.Normalize();
            up = rolledUp.Normalize();
        }

        var cosTheta = Math.Clamp(GeometryMath.Dot(r, f), -1.0, 1.0);
        var theta = Math.Acos(cosTheta) * 180.0 / Math.PI;
        var x = GeometryMath.Dot(r, right);
        var y = GeometryMath.Dot(r, up);
        var phi = Math.Atan2(y, x) * 180.0 / Math.PI;
        if (phi < 0) phi += 360.0;

        return (theta, phi);
    }

    // ─── ISM (görüntü kaynağı yöntemi) ──────────────────────────────────────

    private static void AddIsmArrivals(
        AcousticPoint                      source,
        Vector3D                           srcDir,
        AcousticLoudspeaker?               speaker,
        double[]?                          sensitivityGain,
        IReadOnlyList<ProjectSurface>      surfaces,
        IReadOnlyDictionary<string,string> categoryMaterials,
        IReadOnlyDictionary<string,string>? surfaceMaterials,
        RayTriangle[]                      triangles,
        BvhNode[]                          bvh,
        Dictionary<int, RayTriangle[]>     surfaceTriangles,
        IReadOnlyList<AcousticPoint>       receivers,
        EchogramData[]                     echograms,
        SimulationParameters               p,
        double                             speedOfSound,
        double[]                           airAbs,
        double                             epsScale,
        int                                ismOrder,
        CancellationToken                  ct)
    {
        var sourcePos = source.Position;

        for (int si = 0; si < surfaces.Count; si++)
        {
            ct.ThrowIfCancellationRequested();
            var surf = surfaces[si];
            if (surf.Vertices.Count < 3) continue;

            var mat = ResolveMaterial(surf, surfaceMaterials, categoryMaterials);
            var n   = ComputeNormal(surf.Vertices[0], surf.Vertices[1], surf.Vertices[2]);
            var d   = GeometryMath.Dot(n, GeometryMath.ToVector(new Point3D(0,0,0), surf.Vertices[0]));

            var distSrc = GeometryMath.Dot(n, GeometryMath.ToVector(new Point3D(0,0,0), sourcePos)) - d;
            if (Math.Abs(distSrc) < 1e-6 * epsScale) continue;

            var imgSrc = new Point3D(
                sourcePos.X - 2 * distSrc * n.X,
                sourcePos.Y - 2 * distSrc * n.Y,
                sourcePos.Z - 2 * distSrc * n.Z);

            if (!surfaceTriangles.TryGetValue(surf.Id, out var surfTris) || surfTris.Length == 0) continue;

            for (int ri = 0; ri < receivers.Count; ri++)
            {
                if ((ri & 31) == 0) ct.ThrowIfCancellationRequested();
                var rcv = receivers[ri];
                var eg  = echograms[ri];
                var toRcv = new Vector3D(
                    rcv.Position.X - imgSrc.X,
                    rcv.Position.Y - imgSrc.Y,
                    rcv.Position.Z - imgSrc.Z);
                var toRcvLen = Math.Sqrt(toRcv.X*toRcv.X + toRcv.Y*toRcv.Y + toRcv.Z*toRcv.Z);
                if (toRcvLen < 1e-10) continue;

                var toRcvDir = new Vector3D(toRcv.X/toRcvLen, toRcv.Y/toRcvLen, toRcv.Z/toRcvLen);

                IntersectBvh(imgSrc, toRcvDir, triangles, bvh, out var tSurf, out var surfHitIdx);
                if (surfHitIdx < 0) continue;
                // Sadece kendi yüzeyimize mi vurduk?
                if (triangles[surfHitIdx].SurfaceId != surf.Id) continue;
                double tHit = tSurf;

                var hitPt = new Point3D(
                    imgSrc.X + toRcvDir.X * tHit,
                    imgSrc.Y + toRcvDir.Y * tHit,
                    imgSrc.Z + toRcvDir.Z * tHit);

                var tRcv = GeometryMath.Dot(toRcvDir, new Vector3D(
                    rcv.Position.X - imgSrc.X,
                    rcv.Position.Y - imgSrc.Y,
                    rcv.Position.Z - imgSrc.Z));
                if (tRcv < tHit - 1e-3 * epsScale) continue;

                if (!IsPathClear(sourcePos, hitPt, surf.Id, triangles, bvh, epsScale)) continue;
                if (!IsPathClear(hitPt, rcv.Position, surf.Id, triangles, bvh, epsScale)) continue;

                var d1    = Distance(sourcePos, hitPt);
                var d2    = Distance(hitPt, rcv.Position);
                var total = d1 + d2;
                var time  = total / speedOfSound;

                var srcToHit = new Vector3D(hitPt.X - sourcePos.X, hitPt.Y - sourcePos.Y, hitPt.Z - sourcePos.Z);
                double[] q = new double[8];
                FillDirectivity(q, source, speaker, sensitivityGain, srcToHit, srcDir);

                AddIsmArrival(eg, time, total, airAbs, mat, null, toRcvDir, q,
                              p.RayCount, p.ReceiverRadius);
            }

            if (ismOrder < 2) continue;

            for (int sj = 0; sj < surfaces.Count; sj++)
            {
                if ((sj & 15) == 0) ct.ThrowIfCancellationRequested();
                if (sj == si) continue;
                var surf2 = surfaces[sj];
                if (surf2.Vertices.Count < 3) continue;

                var mat2 = ResolveMaterial(surf2, surfaceMaterials, categoryMaterials);
                var n2   = ComputeNormal(surf2.Vertices[0], surf2.Vertices[1], surf2.Vertices[2]);
                var d2v  = GeometryMath.Dot(n2, GeometryMath.ToVector(new Point3D(0,0,0), surf2.Vertices[0]));

                var distImg = GeometryMath.Dot(n2, GeometryMath.ToVector(new Point3D(0,0,0), imgSrc)) - d2v;
                if (Math.Abs(distImg) < 1e-6 * epsScale) continue;

                var imgSrc2 = new Point3D(
                    imgSrc.X - 2 * distImg * n2.X,
                    imgSrc.Y - 2 * distImg * n2.Y,
                    imgSrc.Z - 2 * distImg * n2.Z);

                if (!surfaceTriangles.TryGetValue(surf2.Id, out var surfTris2) || surfTris2.Length == 0) continue;

                for (int ri = 0; ri < receivers.Count; ri++)
                {
                    if ((ri & 31) == 0) ct.ThrowIfCancellationRequested();
                    var rcv = receivers[ri];
                    var eg  = echograms[ri];
                    var toRcv = new Vector3D(
                        rcv.Position.X - imgSrc2.X,
                        rcv.Position.Y - imgSrc2.Y,
                        rcv.Position.Z - imgSrc2.Z);
                    var toRcvLen = Math.Sqrt(toRcv.X*toRcv.X + toRcv.Y*toRcv.Y + toRcv.Z*toRcv.Z);
                    if (toRcvLen < 1e-10) continue;

                    var toRcvDir = new Vector3D(toRcv.X/toRcvLen, toRcv.Y/toRcvLen, toRcv.Z/toRcvLen);

                    IntersectBvh(imgSrc2, toRcvDir, triangles, bvh, out var tSurf2, out var surfHitIdx2);
                    if (surfHitIdx2 < 0 || triangles[surfHitIdx2].SurfaceId != surf2.Id) continue;
                    double tHit2 = tSurf2;

                    var hitPt2 = new Point3D(
                        imgSrc2.X + toRcvDir.X * tHit2,
                        imgSrc2.Y + toRcvDir.Y * tHit2,
                        imgSrc2.Z + toRcvDir.Z * tHit2);

                    var dir1    = new Vector3D(hitPt2.X - imgSrc.X, hitPt2.Y - imgSrc.Y, hitPt2.Z - imgSrc.Z);
                    var dir1Len = Math.Sqrt(dir1.X*dir1.X + dir1.Y*dir1.Y + dir1.Z*dir1.Z);
                    if (dir1Len < 1e-10) continue;
                    var dir1N = new Vector3D(dir1.X/dir1Len, dir1.Y/dir1Len, dir1.Z/dir1Len);

                    IntersectBvh(imgSrc, dir1N, triangles, bvh, out var tSurf1, out var surfHitIdx1);
                    if (surfHitIdx1 < 0 || triangles[surfHitIdx1].SurfaceId != surf.Id) continue;
                    double tHit1 = tSurf1;

                    var hitPt1 = new Point3D(
                        imgSrc.X + dir1N.X * tHit1,
                        imgSrc.Y + dir1N.Y * tHit1,
                        imgSrc.Z + dir1N.Z * tHit1);

                    if (!IsPathClear(sourcePos, hitPt1, surf.Id,   triangles, bvh, epsScale)) continue;
                    if (!IsPathClear(hitPt1,   hitPt2, surf.Id,   triangles, bvh, epsScale)) continue;
                    if (!IsPathClear(hitPt2,   rcv.Position, surf2.Id, triangles, bvh, epsScale)) continue;

                    var dp1   = Distance(sourcePos, hitPt1);
                    var dp2   = Distance(hitPt1, hitPt2);
                    var dp3   = Distance(hitPt2, rcv.Position);
                    var total = dp1 + dp2 + dp3;
                    var time  = total / speedOfSound;

                    var srcToHit1 = new Vector3D(hitPt1.X - sourcePos.X, hitPt1.Y - sourcePos.Y, hitPt1.Z - sourcePos.Z);
                    double[] q = new double[8];
                    FillDirectivity(q, source, speaker, sensitivityGain, srcToHit1, srcDir);

                    AddIsmArrival(eg, time, total, airAbs, mat, mat2, toRcvDir, q,
                                  p.RayCount, p.ReceiverRadius);
                }
            }
        }
    }

    private static void AddIsmArrival(
        EchogramData eg, double time, double totalDist,
        double[] airAbs, AcousticMaterial mat1, AcousticMaterial? mat2,
        Vector3D dir, double[] directivityQ,
        int rayCount, double receiverRadius)
    {
        var geometricScale = totalDist > 1e-9
            ? rayCount * receiverRadius * receiverRadius / (4.0 * totalDist * totalDist)
            : 0.0;

        double e0 = geometricScale * directivityQ[0] * AirFactor(airAbs[0], totalDist) * (1 - mat1.Alpha[0]);
        double e1 = geometricScale * directivityQ[1] * AirFactor(airAbs[1], totalDist) * (1 - mat1.Alpha[1]);
        double e2 = geometricScale * directivityQ[2] * AirFactor(airAbs[2], totalDist) * (1 - mat1.Alpha[2]);
        double e3 = geometricScale * directivityQ[3] * AirFactor(airAbs[3], totalDist) * (1 - mat1.Alpha[3]);
        double e4 = geometricScale * directivityQ[4] * AirFactor(airAbs[4], totalDist) * (1 - mat1.Alpha[4]);
        double e5 = geometricScale * directivityQ[5] * AirFactor(airAbs[5], totalDist) * (1 - mat1.Alpha[5]);
        double e6 = geometricScale * directivityQ[6] * AirFactor(airAbs[6], totalDist) * (1 - mat1.Alpha[6]);
        double e7 = geometricScale * directivityQ[7] * AirFactor(airAbs[7], totalDist) * (1 - mat1.Alpha[7]);

        if (mat2 is not null)
        {
            e0 *= 1 - mat2.Alpha[0]; e1 *= 1 - mat2.Alpha[1];
            e2 *= 1 - mat2.Alpha[2]; e3 *= 1 - mat2.Alpha[3];
            e4 *= 1 - mat2.Alpha[4]; e5 *= 1 - mat2.Alpha[5];
            e6 *= 1 - mat2.Alpha[6]; e7 *= 1 - mat2.Alpha[7];
        }

        eg.AddArrival(new EchogramArrival(time, e0, e1, e2, e3, e4, e5, e6, e7, dir.X, dir.Y, dir.Z));
    }

    // ─── Tek ışın izleme ──────────────────────────────────────────────────────

    private static void TraceRay(
        Point3D          origin,
        Vector3D         dir,
        ReadOnlySpan<double> directivityQ,
        RayTriangle[]    triangles,
        BvhNode[]        bvh,
        IReadOnlyList<AcousticPoint> receivers,
        EchogramData[]   echograms,
        SimulationParameters p,
        double           speedOfSound,
        double[]         airAbs,
        ConcurrentBag<TailSeed> tailSeeds,
        double           epsScale,
        Random           rng,
        CancellationToken ct)
    {
        double e0 = directivityQ[0], e1 = directivityQ[1], e2 = directivityQ[2], e3 = directivityQ[3];
        double e4 = directivityQ[4], e5 = directivityQ[5], e6 = directivityQ[6], e7 = directivityQ[7];
        double cumDist = 0;

        // ── FIX HATA-1: ISM ile çift sayım önleme ───────────────────────────
        // ISM, 1..IsmOrder mertebe saf-specular yansımaları zaten deterministik
        // olarak ekliyor. Işın izleyici aynı yolu aynı enerji ölçeğinde yakalarsa
        // erken yansıma enerjisi ~2× olur. Bu yüzden bir ışın hâlâ saf-specular
        // yoldaysa ve mertebesi ISM kapsamındaysa alıcıya KAYIT YAPMAYIZ.
        // Diffüz saçılım gerçekleşince yol ISM kapsamı dışına çıkar ve normal kayda döner.
        bool allSpecular = true;

        for (var bounce = 0; bounce <= p.MaxOrder; bounce++)
        {
            if ((bounce & 15) == 0) ct.ThrowIfCancellationRequested();
            IntersectBvh(origin, dir, triangles, bvh, out var tMin, out var hitIdx);

            if (hitIdx < 0) break;

            var hitPoint = GeometryMath.Add(origin, dir * tMin);

            // bounce = bu segmentten önce gerçekleşen yansıma sayısı (= mertebe).
            // bounce 0 = direkt ses (ISM kapsamı dışı, daima kaydet).
            bool ismHandlesThis = bounce >= 1 && bounce <= p.IsmOrder && allSpecular;

            if (!ismHandlesThis)
            {
                for (int ri = 0; ri < receivers.Count; ri++)
                {
                    var rcv = receivers[ri];
                    var eg  = echograms[ri];
                    var tHit = ReceiverHitDistance(origin, dir, tMin, rcv.Position, p.ReceiverRadius);
                    if (tHit is null) continue;

                    var segDist = cumDist + tHit.Value;
                    var time    = segDist / speedOfSound;

                    eg.AddArrival(new EchogramArrival(time,
                        e0 * AirFactor(airAbs[0], segDist),
                        e1 * AirFactor(airAbs[1], segDist),
                        e2 * AirFactor(airAbs[2], segDist),
                        e3 * AirFactor(airAbs[3], segDist),
                        e4 * AirFactor(airAbs[4], segDist),
                        e5 * AirFactor(airAbs[5], segDist),
                        e6 * AirFactor(airAbs[6], segDist),
                        e7 * AirFactor(airAbs[7], segDist),
                        dir.X, dir.Y, dir.Z));
                }
            }

            cumDist += tMin;

            // ── FIX #3: Geçiş mertebesi — istatistiksel diffüz kuyruk ────────
            // Işının taşıdığı enerjiyi ve geliş zamanını tohum olarak biriktir;
            // tüm tohumların kuyruğu döngü sonunda histogramda toplanır (B1).
            if (bounce >= p.TransitionOrder)
            {
                tailSeeds.Add(new TailSeed(cumDist / speedOfSound, e0, e1, e2, e3, e4, e5, e6, e7));
                break;
            }

            if (Math.Max(Math.Max(e0, e1), Math.Max(Math.Max(e2, e3), Math.Max(Math.Max(e4, e5), Math.Max(e6, e7)))) < p.EnergyThreshold)
                break;

            ref var tri = ref triangles[hitIdx];
            e0 *= tri.R0; e1 *= tri.R1; e2 *= tri.R2; e3 *= tri.R3;
            e4 *= tri.R4; e5 *= tri.R5; e6 *= tri.R6; e7 *= tri.R7;

            var normal = tri.Normal;
            if (GeometryMath.Dot(dir, normal) > 0) normal = new Vector3D(-normal.X, -normal.Y, -normal.Z);

            // ── FIX #1: Lambert kosinüs ağırlıklı saçılım ────────────────────
            bool diffuse = rng.NextDouble() < tri.Scattering;
            dir = diffuse
                ? LambertHemisphere(normal, rng)
                : GeometryMath.Reflect(dir, normal);

            // Diffüz saçılımdan sonra yol artık saf-specular değil → ISM kapsamı dışı.
            if (diffuse) allSpecular = false;

            origin = GeometryMath.Add(hitPoint, normal * (1e-4 * epsScale));
        }
    }

    // ─── FIX #3 / B1: Geç yansıma kuyruğu — toplu histogram olarak yayma ──────

    /// <summary>
    /// Geçiş mertebesine ulaşan tüm ışınların taşıdığı enerjiyi (tohumlar) ortak bir
    /// zaman-binli histogramda toplar ve bin başına TEK geliş olarak her alıcıya yayar.
    /// <para>Eski sürüm her ışın için her alıcıya ≤200 geliş ekliyordu → geliş sayısı
    /// ışın×alıcı×200 ile patlıyordu (B1). Enerji-zaman dağılımı bin çözünürlüğüne
    /// (2.5 ms) kadar korunur; toplam enerji ve geometrik bozunma birebir aynıdır.</para>
    /// <para>Kuyruk gelişlerinin yönü diffüz alanda izotropiktir ve LF penceresi
    /// (5–80 ms) dışında kaldığından hiçbir akustik parametreyi etkilemez; rastgele
    /// (deterministik) atanır.</para>
    /// </summary>
    private static void EmitReverbTail(
        IReadOnlyCollection<TailSeed> seeds,
        double[] rt60,
        double   volume,
        double   speedOfSound,
        double   unitScale,
        EchogramData[] echograms,
        SimulationParameters p,
        int      dirSalt,
        CancellationToken ct)
    {
        if (seeds.Count == 0 || echograms.Length == 0) return;

        const double dt       = 0.015;   // 15 ms bozunma adımı (geometrik seri — değişmedi)
        const int    maxSteps = 200;     // max 3 saniye kuyruk
        const double binSec   = 0.0025;  // 2.5 ms toplama bini → STI faz hatası ihmal edilebilir

        // Adım başı bozunma katsayısı q = exp(-13.8 × dt / RT60).
        Span<double> q = stackalloc double[8];
        for (int i = 0; i < 8; i++)
            q[i] = Math.Exp(-AcousticConstants.SchroederDecayFactor * dt / rt60[i]);

        // Enerji ölçekleme: ışının taşıdığı enerjiyi alıcı kesit alanına göre ölçekle.
        // Diffüz alan enerji yoğunluğu: E_rec = E_ray * (c * dt * pi * R^2) / (4 * V).
        // c ve R proje birimindedir ama V daima m³ → c·R²'yi metreye çevir, yoksa
        // recFactor birime bağlı olur (mm projesinde kuyruk enerjisini şişirir — C2).
        double cM = speedOfSound * unitScale;     // m/s
        double rM = p.ReceiverRadius * unitScale; // m
        double recFactor = (volume > 1e-6)
            ? (cM * dt * Math.PI * rM * rM) / (4.0 * volume)
            : 0.0;
        if (recFactor <= 0) return;

        // Histogram zaman aralığı: en erken/geç tohumdan + tam kuyruk uzunluğu.
        double minT0 = double.MaxValue, maxT0 = 0;
        foreach (var s in seeds) { if (s.T0 < minT0) minT0 = s.T0; if (s.T0 > maxT0) maxT0 = s.T0; }

        int baseBin = (int)(minT0 / binSec);
        int lastBin = (int)((maxT0 + maxSteps * dt) / binSec) + 1;
        int nBins   = Math.Max(1, lastBin - baseBin + 1);

        // Kaynak düzeyinde bant×bin enerji histogramı (enerji alıcılar arasında aynı).
        var hist = new double[nBins * 8];

        Span<double> tailE = stackalloc double[8];
        foreach (var s in seeds)
        {
            ct.ThrowIfCancellationRequested();
            tailE[0] = s.E0 * (1 - q[0]) * recFactor; tailE[1] = s.E1 * (1 - q[1]) * recFactor;
            tailE[2] = s.E2 * (1 - q[2]) * recFactor; tailE[3] = s.E3 * (1 - q[3]) * recFactor;
            tailE[4] = s.E4 * (1 - q[4]) * recFactor; tailE[5] = s.E5 * (1 - q[5]) * recFactor;
            tailE[6] = s.E6 * (1 - q[6]) * recFactor; tailE[7] = s.E7 * (1 - q[7]) * recFactor;

            for (int n = 0; n < maxSteps; n++)
            {
                double t   = s.T0 + (n + 0.5) * dt;
                int    bin = Math.Clamp((int)(t / binSec) - baseBin, 0, nBins - 1);
                int    off = bin * 8;

                bool anyAbove = false;
                for (int i = 0; i < 8; i++)
                {
                    hist[off + i] += tailE[i];
                    tailE[i] *= q[i];
                    if (tailE[i] > 1e-10) anyAbove = true;
                }
                if (!anyAbove) break;
            }
        }

        // Histogramı her alıcıya bin başına tek geliş olarak yay.
        var rng = new Random(unchecked(dirSalt + 777));
        for (int b = 0; b < nBins; b++)
        {
            if ((b & 63) == 0) ct.ThrowIfCancellationRequested();
            int off = b * 8;
            if (hist[off] <= 0 && hist[off+1] <= 0 && hist[off+2] <= 0 && hist[off+3] <= 0 &&
                hist[off+4] <= 0 && hist[off+5] <= 0 && hist[off+6] <= 0 && hist[off+7] <= 0)
                continue;

            double t = (baseBin + b + 0.5) * binSec;
            foreach (var eg in echograms)
            {
                var d = RandomOnSphere(rng);
                eg.AddArrival(new EchogramArrival(t,
                    hist[off+0], hist[off+1], hist[off+2], hist[off+3],
                    hist[off+4], hist[off+5], hist[off+6], hist[off+7],
                    d.X, d.Y, d.Z));
            }
        }
    }

    // ─── FIX #1: Lambert kosinüs ağırlıklı yarı-küre örnekleme ──────────────

    /// <summary>
    /// Malley yöntemiyle kosinüs ağırlıklı yarı-küre örneklemesi.
    /// Lambert saçılım yasasını doğru modeller (pdf = cosθ/π).
    /// Tekdüze yarı-küre örneklemeye göre fiziksel olarak daha doğru.
    /// </summary>
    private static Vector3D LambertHemisphere(Vector3D normal, Random rng)
    {
        var r1  = rng.NextDouble();
        var r2  = rng.NextDouble();
        var phi = 2.0 * Math.PI * r2;
        var sqr = Math.Sqrt(r1);

        // Yerel çerçevede: x = √r₁·cos(φ), y = √r₁·sin(φ), z = √(1−r₁)
        var lx = sqr * Math.Cos(phi);
        var ly = sqr * Math.Sin(phi);
        var lz = Math.Sqrt(1.0 - r1);

        // Normal'i z-ekseni olarak kullanan ortonormal taban
        var up  = Math.Abs(normal.Z) < 0.999
            ? new Vector3D(0, 0, 1)
            : new Vector3D(1, 0, 0);

        // Teğet = normal × up, normallenmiş
        double tx = normal.Y * up.Z - normal.Z * up.Y;
        double ty = normal.Z * up.X - normal.X * up.Z;
        double tz = normal.X * up.Y - normal.Y * up.X;
        double tl = Math.Sqrt(tx*tx + ty*ty + tz*tz);
        tx /= tl; ty /= tl; tz /= tl;

        // Bitiğent = normal × teğet (zaten birim uzunluğunda)
        double bx = normal.Y * tz - normal.Z * ty;
        double by = normal.Z * tx - normal.X * tz;
        double bz = normal.X * ty - normal.Y * tx;

        return new Vector3D(
            lx*tx + ly*bx + lz*normal.X,
            lx*ty + ly*by + lz*normal.Y,
            lx*tz + ly*bz + lz*normal.Z);
    }

    // ─── Tıkama ve yardımcılar ────────────────────────────────────────────────

    // excludeSurfaceId: kullanılmıyor — epsilon offset başlangıç yüzeyini doğal olarak atlar
    private static bool IsPathClear(Point3D from, Point3D to, int excludeSurfaceId, RayTriangle[] triangles, BvhNode[] bvh, double epsScale)
    {
        var diff = new Vector3D(to.X - from.X, to.Y - from.Y, to.Z - from.Z);
        var len  = Math.Sqrt(diff.X*diff.X + diff.Y*diff.Y + diff.Z*diff.Z);
        if (len < 1e-10) return true;

        var dir = new Vector3D(diff.X / len, diff.Y / len, diff.Z / len);

        // from noktasının üzerinde durduğu yüzeyle öz-kesişimi atlamak için epsilon offset
        // (proje birimine ölçekli — C2; metrede epsScale=1).
        double eps = 2e-4 * epsScale;
        var origin = new Point3D(from.X + dir.X * eps, from.Y + dir.Y * eps, from.Z + dir.Z * eps);
        var maxT   = len - eps - 1e-4 * epsScale;
        if (maxT <= 0) return true;

        IntersectBvh(origin, dir, triangles, bvh, out var t, out var hitIdx);
        return hitIdx < 0 || t >= maxT;
    }

    // ─── BVH API ──────────────────────────────────────────────────────────────

    private static BvhNode[] BuildBvh(RayTriangle[] triangles, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (triangles.Length == 0) return [];
        var nodes = new List<BvhNode>();
        var indices = Enumerable.Range(0, triangles.Length).ToArray();
        BuildBvhRecursive(triangles, indices, 0, triangles.Length, nodes, ct);
        
        // Üçgenleri BVH sırasına göre yeniden dizelim ki leaf düğümler doğru aralığı okusun
        var sorted = new RayTriangle[triangles.Length];
        for (int i = 0; i < triangles.Length; i++) sorted[i] = triangles[indices[i]];
        Array.Copy(sorted, triangles, triangles.Length);

        return nodes.ToArray();
    }

    private static int BuildBvhRecursive(
        RayTriangle[] triangles,
        int[] indices,
        int start,
        int count,
        List<BvhNode> nodes,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var nodeIdx = nodes.Count;
        nodes.Add(default);

        var bounds = BoundingBox.FromPoints(triangles[indices[start]].V0, triangles[indices[start]].V1, triangles[indices[start]].V2);
        for (int i = 1; i < count; i++)
        {
            if ((i & 255) == 0) ct.ThrowIfCancellationRequested();
            var tri = triangles[indices[start + i]];
            bounds.Encapsulate(BoundingBox.FromPoints(tri.V0, tri.V1, tri.V2));
        }

        if (count <= 4)
        {
            nodes[nodeIdx] = new BvhNode { Bounds = bounds, TriangleCount = count, LeftIndex = start };
            return nodeIdx;
        }

        double dx = bounds.Max.X - bounds.Min.X;
        double dy = bounds.Max.Y - bounds.Min.Y;
        double dz = bounds.Max.Z - bounds.Min.Z;
        int axis = dx > dy ? (dx > dz ? 0 : 2) : (dy > dz ? 1 : 2);
        double mid = axis switch { 0 => (bounds.Min.X + bounds.Max.X) / 2, 1 => (bounds.Min.Y + bounds.Max.Y) / 2, _ => (bounds.Min.Z + bounds.Max.Z) / 2 };

        int split = start;
        for (int i = 0; i < count; i++)
        {
            if ((i & 255) == 0) ct.ThrowIfCancellationRequested();
            var tri = triangles[indices[start + i]];
            double center = axis switch { 0 => (tri.V0.X + tri.V1.X + tri.V2.X) / 3, 1 => (tri.V0.Y + tri.V1.Y + tri.V2.Y) / 3, _ => (tri.V0.Z + tri.V1.Z + tri.V2.Z) / 3 };
            if (center < mid)
            {
                (indices[start + i], indices[split]) = (indices[split], indices[start + i]);
                split++;
            }
        }

        if (split == start || split == start + count) split = start + count / 2;

        int left = BuildBvhRecursive(triangles, indices, start, split - start, nodes, ct);
        int right = BuildBvhRecursive(triangles, indices, split, count - (split - start), nodes, ct);

        nodes[nodeIdx] = new BvhNode { Bounds = bounds, LeftIndex = left, RightIndex = right };
        return nodeIdx;
    }

    private static void IntersectBvh(Point3D origin, Vector3D dir, RayTriangle[] triangles, BvhNode[] bvh, out double tMin, out int hitIdx)
    {
        tMin = double.MaxValue;
        hitIdx = -1;
        if (bvh.Length == 0) return;
        IntersectBvhRecursive(0, origin, dir, triangles, bvh, ref tMin, ref hitIdx);
    }

    private static void IntersectBvhRecursive(int nodeIdx, Point3D origin, Vector3D dir, RayTriangle[] triangles, BvhNode[] bvh, ref double tMin, ref int hitIdx)
    {
        ref readonly var node = ref bvh[nodeIdx];
        if (!node.Bounds.Intersect(origin, dir, out var bTMin, out var bTMax) || bTMin > tMin) return;

        if (node.TriangleCount > 0)
        {
            for (int i = 0; i < node.TriangleCount; i++)
            {
                if (RayTriangleIntersect(origin, dir, in triangles[node.LeftIndex + i], out var t) && t < tMin)
                {
                    tMin = t;
                    hitIdx = node.LeftIndex + i;
                }
            }
            return;
        }

        // Front-to-back: çocukların kutu giriş mesafesine göre YAKIN olanı önce ziyaret
        // et. Yakın taraf bir kesişim bulunca uzak alt-ağaç `bTMin > tMin` ile budanır.
        // Sonuç değişmez (her zaman global en küçük t bulunur), test sayısı azalır.
        int leftIdx  = node.LeftIndex;
        int rightIdx = node.RightIndex;
        bool hitL = bvh[leftIdx].Bounds.Intersect(origin, dir, out var lEntry, out _);
        bool hitR = bvh[rightIdx].Bounds.Intersect(origin, dir, out var rEntry, out _);

        if (hitL && hitR)
        {
            int near = leftIdx, far = rightIdx;
            if (rEntry < lEntry) { near = rightIdx; far = leftIdx; }
            IntersectBvhRecursive(near, origin, dir, triangles, bvh, ref tMin, ref hitIdx);
            IntersectBvhRecursive(far,  origin, dir, triangles, bvh, ref tMin, ref hitIdx);
        }
        else if (hitL) IntersectBvhRecursive(leftIdx,  origin, dir, triangles, bvh, ref tMin, ref hitIdx);
        else if (hitR) IntersectBvhRecursive(rightIdx, origin, dir, triangles, bvh, ref tMin, ref hitIdx);
    }

    private static double Distance(Point3D a, Point3D b)
    {
        var dx = b.X - a.X; var dy = b.Y - a.Y; var dz = b.Z - a.Z;
        return Math.Sqrt(dx*dx + dy*dy + dz*dz);
    }

    private static double AirFactor(double dbPerUnit, double dist) =>
        Math.Pow(10, -dbPerUnit * dist / 10.0);

    // ─── Geometri yardımcıları ────────────────────────────────────────────────

    private static bool RayTriangleIntersect(
        Point3D origin, Vector3D dir, in RayTriangle tri, out double t)
    {
        var edge1 = tri.Edge1;
        var edge2 = tri.Edge2;
        var h     = GeometryMath.Cross(dir, edge2);
        var a     = GeometryMath.Dot(edge1, h);

        if (Math.Abs(a) < 1e-10) { t = 0; return false; }

        var f = 1.0 / a;
        var s = GeometryMath.ToVector(tri.V0, origin);
        var u = f * GeometryMath.Dot(s, h);
        if (u is < 0 or > 1) { t = 0; return false; }

        var q = GeometryMath.Cross(s, edge1);
        var v = f * GeometryMath.Dot(dir, q);
        if (v < 0 || u + v > 1) { t = 0; return false; }

        t = f * GeometryMath.Dot(edge2, q);
        return t > 1e-5;
    }

    private static double? ReceiverHitDistance(
        Point3D origin, Vector3D dir, double segLen,
        Point3D receiver, double radius)
    {
        var len = Math.Sqrt(dir.X*dir.X + dir.Y*dir.Y + dir.Z*dir.Z) * segLen;
        if (len < 1e-10) return null;

        var ux = dir.X * segLen / len;
        var uy = dir.Y * segLen / len;
        var uz = dir.Z * segLen / len;

        var ox = receiver.X - origin.X;
        var oy = receiver.Y - origin.Y;
        var oz = receiver.Z - origin.Z;

        var tClose = ox*ux + oy*uy + oz*uz;
        if (tClose < 0 || tClose > len) return null;

        var cx = origin.X + tClose*ux - receiver.X;
        var cy = origin.Y + tClose*uy - receiver.Y;
        var cz = origin.Z + tClose*uz - receiver.Z;

        if (cx*cx + cy*cy + cz*cz > radius*radius) return null;
        return tClose;
    }

    // ─── Tekrar üretilebilir RNG ─────────────────────────────────────────────
    // Seed verildiyse her ışın kendi deterministik tohumuyla bağımsız bir Random
    // alır → koşu sonuçları thread zamanlamasından bağımsız ve tekrar üretilebilir.
    // Seed null ise yüksek performanslı Random.Shared kullanılır (üretim varsayılanı).
    private static Random RayRng(int? seed, int salt) =>
        seed is int s ? new Random(unchecked(s * 1_000_003 + salt)) : Random.Shared;

    // ─── Küre üzerinde rassal yön (Marsaglia) ────────────────────────────────

    private static Vector3D RandomOnSphere(Random rng)
    {
        double x, y, s;
        do {
            x = rng.NextDouble() * 2 - 1;
            y = rng.NextDouble() * 2 - 1;
            s = x*x + y*y;
        } while (s >= 1.0 || s < 1e-10);
        var f = 2 * Math.Sqrt(1 - s);
        return new Vector3D(f * x, f * y, 1 - 2 * s);
    }

    // ─── Yüzey üçgenleme ──────────────────────────────────────────────────────

    // Üçgenleri yüzey Id'sine göre gruplar — ISM görüntü-kaynak geçerlilik testi
    // her yüzeyin üçgenlerine erişir. Koşu başına bir kez kurulur (B4).
    private static Dictionary<int, RayTriangle[]> BuildSurfaceTriangleMap(
        RayTriangle[] triangles,
        CancellationToken ct = default)
    {
        var lists = new Dictionary<int, List<RayTriangle>>();
        for (var i = 0; i < triangles.Length; i++)
        {
            if ((i & 1023) == 0) ct.ThrowIfCancellationRequested();
            var t = triangles[i];
            if (!lists.TryGetValue(t.SurfaceId, out var list))
            {
                list = new List<RayTriangle>();
                lists[t.SurfaceId] = list;
            }
            list.Add(t);
        }
        return lists.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());
    }

    private static RayTriangle[] BuildTriangles(
        IReadOnlyList<ProjectSurface>      surfaces,
        IReadOnlyDictionary<string,string> categoryMaterials,
        IReadOnlyDictionary<string,string>? surfaceMaterials = null,
        CancellationToken                  ct = default)
    {
        var list = new List<RayTriangle>(surfaces.Count * 2);

        foreach (var surf in surfaces)
        {
            ct.ThrowIfCancellationRequested();
            var verts = surf.Vertices;
            if (verts.Count < 3) continue;

            var mat = ResolveMaterial(surf, surfaceMaterials, categoryMaterials);

            foreach (var (p0, p1, p2) in TriangulatePolygon(verts))
            {
                if ((list.Count & 1023) == 0) ct.ThrowIfCancellationRequested();
                var normal = ComputeNormal(p0, p1, p2);
                list.Add(new RayTriangle(
                    surf.Id,
                    p0, p1, p2,
                    normal,
                    1 - mat.Alpha[0], 1 - mat.Alpha[1], 1 - mat.Alpha[2], 1 - mat.Alpha[3],
                    1 - mat.Alpha[4], 1 - mat.Alpha[5], 1 - mat.Alpha[6], 1 - mat.Alpha[7],
                    mat.ScatteringMid));
            }
        }

        return list.ToArray();
    }

    /// Poligonu kulak-kesme (ear clipping) ile üçgenler; konkav poligonları ve NaN
    /// ayraçlı delik döngülerini destekler. Işın izleme dışında DXF ihracatı da kullanır
    /// (naif fan konkav zeminlerde hatalı yüzey üretir).
    public static IEnumerable<(Point3D, Point3D, Point3D)> TriangulatePolygon(IReadOnlyList<Point3D> verts)
    {
        var outer = new List<Point3D>();
        var holes = new List<List<Point3D>>();
        var currentLoop = outer;

        foreach (var v in verts)
        {
            if (double.IsNaN(v.X))
            {
                currentLoop = new List<Point3D>();
                holes.Add(currentLoop);
            }
            else
            {
                currentLoop.Add(v);
            }
        }

        // Boş delikleri temizle
        holes.RemoveAll(h => h.Count < 3);

        if (outer.Count == 3 && holes.Count == 0)
        {
            yield return (outer[0], outer[1], outer[2]);
            yield break;
        }

        var normal = ComputePolygonNormal(outer);
        var axis = DominantAxis(normal);

        var merged = MergeHoles(outer, holes, axis);

        var points = merged.Select(v => ProjectTo2D(v, axis)).ToArray();
        var indices = Enumerable.Range(0, merged.Count).ToList();
        var ccw = SignedArea(points) > 0;

        var guard = 0;
        while (indices.Count > 3 && guard++ < merged.Count * merged.Count)
        {
            var earFound = false;

            for (var i = 0; i < indices.Count; i++)
            {
                var prev = indices[(i - 1 + indices.Count) % indices.Count];
                var curr = indices[i];
                var next = indices[(i + 1) % indices.Count];

                if (!IsConvex(points[prev], points[curr], points[next], ccw)) continue;

                var containsPoint = false;
                foreach (var idx in indices)
                {
                    if (idx == prev || idx == curr || idx == next) continue;
                    if (PointInTriangle(points[idx], points[prev], points[curr], points[next]))
                    {
                        containsPoint = true;
                        break;
                    }
                }

                if (containsPoint) continue;

                yield return ccw 
                    ? (merged[prev], merged[curr], merged[next]) 
                    : (merged[next], merged[curr], merged[prev]);
                indices.RemoveAt(i);
                earFound = true;
                break;
            }

            if (!earFound)
            {
                // Fallback (Fan)
                for (var i = 1; i < merged.Count - 1; i++)
                    yield return (merged[0], merged[i], merged[i + 1]);
                yield break;
            }
        }

        if (indices.Count == 3)
        {
            yield return ccw
                ? (merged[indices[0]], merged[indices[1]], merged[indices[2]])
                : (merged[indices[2]], merged[indices[1]], merged[indices[0]]);
        }
    }

    private static List<Point3D> MergeHoles(List<Point3D> outer, List<List<Point3D>> holes, int axis)
    {
        if (holes.Count == 0) return outer;

        var currentPoly = new List<Point3D>(outer);

        var holeData = holes.Select(h => {
            var pts2D = h.Select(p => ProjectTo2D(p, axis)).ToList();
            var maxIdx = 0;
            for (int i = 1; i < pts2D.Count; i++)
                if (pts2D[i].x > pts2D[maxIdx].x) maxIdx = i;
            return new { Hole3D = h, Pts2D = pts2D, MaxIdx = maxIdx, MaxX = pts2D[maxIdx].x };
        }).OrderByDescending(h => h.MaxX).ToList();

        foreach (var hd in holeData)
        {
            var m3D = hd.Hole3D[hd.MaxIdx];
            var m2D = hd.Pts2D[hd.MaxIdx];
            var cur2D = currentPoly.Select(p => ProjectTo2D(p, axis)).ToList();

            int bestPIdx = -1;
            double minDist = double.MaxValue;

            for (int i = 0; i < cur2D.Count; i++)
            {
                var p2D = cur2D[i];
                if (p2D.x < m2D.x) continue;

                var isVisible = true;
                for (int j = 0; j < cur2D.Count; j++)
                {
                    var e1 = cur2D[j];
                    var e2 = cur2D[(j + 1) % cur2D.Count];
                    if (SegmentsIntersect(m2D, p2D, e1, e2))
                    {
                        isVisible = false;
                        break;
                    }
                }

                if (isVisible)
                {
                    double d = (p2D.x - m2D.x) * (p2D.x - m2D.x) + (p2D.y - m2D.y) * (p2D.y - m2D.y);
                    if (d < minDist)
                    {
                        minDist = d;
                        bestPIdx = i;
                    }
                }
            }

            if (bestPIdx == -1)
            {
                for (int i = 0; i < cur2D.Count; i++)
                {
                    double d = (cur2D[i].x - m2D.x) * (cur2D[i].x - m2D.x) + (cur2D[i].y - m2D.y) * (cur2D[i].y - m2D.y);
                    if (d < minDist)
                    {
                        minDist = d;
                        bestPIdx = i;
                    }
                }
            }

            var newPoly = new List<Point3D>();
            for (int i = 0; i <= bestPIdx; i++) newPoly.Add(currentPoly[i]);
            for (int i = 0; i < hd.Hole3D.Count; i++)
                newPoly.Add(hd.Hole3D[(hd.MaxIdx + i) % hd.Hole3D.Count]);
            newPoly.Add(hd.Hole3D[hd.MaxIdx]);
            newPoly.Add(currentPoly[bestPIdx]);
            for (int i = bestPIdx + 1; i < currentPoly.Count; i++) newPoly.Add(currentPoly[i]);
            currentPoly = newPoly;
        }
        return currentPoly;
    }

    private static bool SegmentsIntersect((double x, double y) a, (double x, double y) b, (double x, double y) c, (double x, double y) d)
    {
        if ((Math.Abs(a.x - c.x) < 1e-9 && Math.Abs(a.y - c.y) < 1e-9) ||
            (Math.Abs(a.x - d.x) < 1e-9 && Math.Abs(a.y - d.y) < 1e-9) ||
            (Math.Abs(b.x - c.x) < 1e-9 && Math.Abs(b.y - c.y) < 1e-9) ||
            (Math.Abs(b.x - d.x) < 1e-9 && Math.Abs(b.y - d.y) < 1e-9))
            return false;

        var d1 = Cross2D(c, d, a);
        var d2 = Cross2D(c, d, b);
        var d3 = Cross2D(a, b, c);
        var d4 = Cross2D(a, b, d);

        return ((d1 > 1e-9 && d2 < -1e-9) || (d1 < -1e-9 && d2 > 1e-9)) &&
               ((d3 > 1e-9 && d4 < -1e-9) || (d3 < -1e-9 && d4 > 1e-9));
    }

    private static Vector3D ComputePolygonNormal(IReadOnlyList<Point3D> verts)
    {
        var nx = 0.0;
        var ny = 0.0;
        var nz = 0.0;

        for (var i = 0; i < verts.Count; i++)
        {
            var current = verts[i];
            var next = verts[(i + 1) % verts.Count];
            nx += (current.Y - next.Y) * (current.Z + next.Z);
            ny += (current.Z - next.Z) * (current.X + next.X);
            nz += (current.X - next.X) * (current.Y + next.Y);
        }

        var normal = new Vector3D(nx, ny, nz);
        return normal.Length < 1e-12 ? ComputeNormal(verts[0], verts[1], verts[2]) : normal.Normalize();
    }

    private static int DominantAxis(Vector3D n)
    {
        var ax = Math.Abs(n.X);
        var ay = Math.Abs(n.Y);
        var az = Math.Abs(n.Z);
        if (ax >= ay && ax >= az) return 0;
        return ay >= az ? 1 : 2;
    }

    private static (double x, double y) ProjectTo2D(Point3D p, int droppedAxis) => droppedAxis switch
    {
        0 => (p.Y, p.Z),
        1 => (p.X, p.Z),
        _ => (p.X, p.Y)
    };

    private static double SignedArea(IReadOnlyList<(double x, double y)> points)
    {
        var area = 0.0;
        for (var i = 0; i < points.Count; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Count];
            area += a.x * b.y - b.x * a.y;
        }
        return area * 0.5;
    }

    private static bool IsConvex((double x, double y) a, (double x, double y) b, (double x, double y) c, bool ccw)
    {
        var cross = Cross2D(a, b, c);
        return ccw ? cross > 1e-10 : cross < -1e-10;
    }

    private static bool PointInTriangle(
        (double x, double y) p,
        (double x, double y) a,
        (double x, double y) b,
        (double x, double y) c)
    {
        var d1 = Cross2D(p, a, b);
        var d2 = Cross2D(p, b, c);
        var d3 = Cross2D(p, c, a);
        var hasNeg = d1 < -1e-10 || d2 < -1e-10 || d3 < -1e-10;
        var hasPos = d1 > 1e-10 || d2 > 1e-10 || d3 > 1e-10;
        return !(hasNeg && hasPos);
    }

    private static double Cross2D((double x, double y) a, (double x, double y) b, (double x, double y) c) =>
        (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

    private static AcousticMaterial ResolveMaterial(
        ProjectSurface surface,
        IReadOnlyDictionary<string,string>? surfaceMaterials,
        IReadOnlyDictionary<string,string>? categoryMaterials)
    {
        var id = MaterialResolver.ResolveMaterialId(surface, surfaceMaterials, categoryMaterials);
        if (id is not null)
        {
            var m = MaterialDatabase.Instance.FindById(id) ?? MaterialLibrary.FindById(id);
            if (m is not null) return m;
        }
        return MaterialLibrary.Default;
    }

    private static Vector3D ComputeNormal(Point3D a, Point3D b, Point3D c)
    {
        var ab = GeometryMath.ToVector(a, b);
        var ac = GeometryMath.ToVector(a, c);
        return GeometryMath.Cross(ab, ac).Normalize();
    }
}

// ─── Yardımcı iç yapı ────────────────────────────────────────────────────────

// Geçiş mertebesine ulaşan bir ışının kuyruk tohumu: geliş zamanı + 8 bant enerjisi.
// EmitReverbTail tüm tohumları ortak histogramda toplar (B1).
internal readonly record struct TailSeed(
    double T0,
    double E0, double E1, double E2, double E3,
    double E4, double E5, double E6, double E7);

internal readonly struct RayTriangle(
    int      surfaceId,
    Point3D  v0, Point3D  v1, Point3D  v2,
    Vector3D normal,
    double r0, double r1, double r2, double r3,
    double r4, double r5, double r6, double r7,
    double scattering)
{
    public int      SurfaceId  { get; } = surfaceId;
    public Point3D  V0         { get; } = v0;
    public Point3D  V1         { get; } = v1;
    public Point3D  V2         { get; } = v2;
    public Vector3D Normal     { get; } = normal;
    // Önceden hesaplanmış kenarlar — Möller–Trumbore her testte yeniden çıkarmasın.
    public Vector3D Edge1      { get; } = GeometryMath.ToVector(v0, v1);
    public Vector3D Edge2      { get; } = GeometryMath.ToVector(v0, v2);
    public double   R0         { get; } = r0;
    public double   R1         { get; } = r1;
    public double   R2         { get; } = r2;
    public double   R3         { get; } = r3;
    public double   R4         { get; } = r4;
    public double   R5         { get; } = r5;
    public double   R6         { get; } = r6;
    public double   R7         { get; } = r7;
    public double   Scattering { get; } = scattering;
}
