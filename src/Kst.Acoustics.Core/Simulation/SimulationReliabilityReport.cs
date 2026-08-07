using System.Text.Json.Serialization;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.Core.Simulation;

public sealed class SimulationReliabilityReport
{
    public SimulationReliabilityReport() { }

    public SimulationReliabilityReport(
        double confidenceScore,
        string confidenceLabel,
        string summary,
        IReadOnlyList<string>? warnings = null,
        IReadOnlyList<string>? recommendations = null,
        IReadOnlyDictionary<string, double>? metrics = null)
    {
        ConfidenceScore = Math.Clamp(confidenceScore, 0, 100);
        ConfidenceLabel = confidenceLabel;
        Summary = summary;
        Warnings = warnings ?? [];
        Recommendations = recommendations ?? [];
        Metrics = metrics ?? new Dictionary<string, double>();
    }

    public double ConfidenceScore { get; init; }
    public string ConfidenceLabel { get; init; } = "";
    public string Summary { get; init; } = "";
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Recommendations { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, double> Metrics { get; init; } = new Dictionary<string, double>();
}

public static class SimulationReliabilityAnalyzer
{
    private static readonly int[] OctaveBands = [63, 125, 250, 500, 1000, 2000, 4000, 8000];

    public static SimulationReliabilityReport Analyze(
        AcousticsProject project,
        IReadOnlyDictionary<string, string> categoryMaterials,
        SimulationResult result)
    {
        var warnings = new List<string>();
        var recommendations = new List<string>();
        var metrics = new Dictionary<string, double>();
        var score = 100.0;

        var sourceCount = Math.Max(project.Sources.Count, 0);
        var receiverCount = Math.Max(project.Receivers.Count, 0);
        var pairCount = Math.Max(1, sourceCount * receiverCount);
        var arrivalsPerPair = result.TotalArrivals / (double)pairCount;
        var raysPerPair = result.Parameters.RayCount;

        metrics["Kaynak sayısı"] = sourceCount;
        metrics["Alıcı sayısı"] = receiverCount;
        metrics["Işın / kaynak"] = raysPerPair;
        metrics["Geliş / çift"] = arrivalsPerPair;
        metrics["Toplam geliş"] = result.TotalArrivals;

        if (sourceCount == 0 || receiverCount == 0)
        {
            score -= 45;
            warnings.Add("Kaynak veya alıcı tanımı eksik; sonuç üretilemez ya da temsil gücü çok düşüktür.");
        }

        if (raysPerPair < 10_000)
        {
            score -= 18;
            warnings.Add($"Işın sayısı düşük ({raysPerPair:N0}). C50/C80/STI gibi erken enerji metriklerinde Monte Carlo sapması artar.");
            recommendations.Add("Ön tasarım için en az 20.000, rapor kalitesi için 50.000-100.000 ışın/kaynak kullanın.");
        }
        else if (raysPerPair < 50_000)
        {
            score -= 8;
            recommendations.Add("Yakınsama kontrolü için aynı modeli 50.000+ ışınla tekrar çalıştırın.");
        }

        if (arrivalsPerPair < 200)
        {
            score -= 22;
            warnings.Add($"Alıcı başına geliş sayısı düşük ({arrivalsPerPair:N0}); RT, C80 ve STI sonuçları gürültülü olabilir.");
            recommendations.Add("Alıcı yarıçapını, ışın sayısını veya kaynak/alıcı konumlarını kontrol edin.");
        }
        else if (arrivalsPerPair < 1_000)
        {
            score -= 10;
            recommendations.Add("Geliş sayısı orta düzeyde; kritik kararlar için ışın sayısını artırıp sonucu karşılaştırın.");
        }

        var categories = project.Surfaces
            .Select(s => s.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var assigned = categories.Count(c =>
            categoryMaterials.TryGetValue(c, out var id) &&
            !string.Equals(id, MaterialLibrary.Default.Id, StringComparison.OrdinalIgnoreCase));
        var materialRatio = categories.Length == 0 ? 0 : assigned / (double)categories.Length;
        metrics["Materyal atama oranı"] = materialRatio;

        if (categories.Length == 0)
        {
            score -= 20;
            warnings.Add("Yüzey kategorisi bulunamadı; materyal güvenilirliği değerlendirilemedi.");
        }
        else if (materialRatio < 0.5)
        {
            score -= 18;
            warnings.Add($"Kategori materyallerinin yalnızca {materialRatio:P0} kadarı özelleştirilmiş; varsayılan alfa sonuçları baskılıyor.");
            recommendations.Add("Tüm ana yüzey kategorilerine gerçek oktav bant alfa ve saçılım verisi atayın.");
        }
        else if (materialRatio < 0.9)
        {
            score -= 8;
            recommendations.Add("Kalan varsayılan materyalleri proje gerçeklerine göre güncelleyin.");
        }

        if (project.RoomVolume <= 0)
        {
            score -= 15;
            warnings.Add("Oda hacmi pozitif değil; RT ve Schroeder frekansı güvenilirliği düşer.");
        }

        // ── Alıcı yarıçapı geometriyle uyumlu mu? (birim tuzağı) ──
        // ReceiverRadius proje birimi cinsindendir. Birim mm/cm ise 1.0'lık varsayılan
        // yarıçap odaya göre ya çok küçük (neredeyse hiç isabet → çöp sonuç) ya da çok
        // büyük (örtüşen küre → şişmiş enerji) olabilir. En küçük oda boyutuna göre denetle.
        var (minDim, maxDim) = RoomExtent(project);
        var radius = result.Parameters.ReceiverRadius;
        var radiusMeters = radius * result.Parameters.UnitScale;
        if (minDim > 0)
        {
            metrics["Alıcı yarıçapı / en küçük boyut"] = radius / minDim;
            if (radius < minDim * 0.005)
            {
                score -= 20;
                warnings.Add($"Alıcı yarıçapı ({radiusMeters:G3} m) oda boyutuna göre çok küçük; ışın isabeti yetersiz kalır.");
                recommendations.Add("Alıcı yarıçapını fiziksel metre cinsinden artırın (tipik başlangıç: 0,3–1,0 m).");
            }
            else if (radius > maxDim * 0.30)
            {
                score -= 10;
                warnings.Add($"Alıcı yarıçapı ({radiusMeters:G3} m) odaya göre çok büyük; küreler örtüşüp enerjiyi şişirebilir, erken/geç ayrımı bozulur.");
                recommendations.Add("Alıcı yarıçapını küçültün; tipik değer oda boyutunun küçük bir kesridir.");
            }
        }

        // Çağıran (RayTracingService.Run) parametreleri zaten hesapladıysa onları
        // tekrar üretme; yoksa (örn. doğrudan test çağrısı) burada hesapla (B2).
        AcousticParameters[] acousticParameters =
            result.ComputedParameters is { Count: > 0 } pre
                ? pre.ToArray()
                : result.Echograms
                    .Select(e => AcousticParameterCalculator.Compute(e, result.Parameters))
                    .ToArray();

        var finiteRtMid = acousticParameters.Select(a => a.RT60_Mid).Where(double.IsFinite).ToArray();
        metrics["RT mid hesaplanma oranı"] = acousticParameters.Length == 0 ? 0 : finiteRtMid.Length / (double)acousticParameters.Length;
        if (acousticParameters.Length > 0 && finiteRtMid.Length < acousticParameters.Length)
        {
            score -= 12;
            warnings.Add("Bazı kaynak-alıcı çiftlerinde RT60 mid hesaplanamadı; bozunma eğrisi yeterli dinamik aralık vermiyor.");
            recommendations.Add("Işın sayısını ve maksimum yansıma mertebesini artırın; alıcı konumlarını oda içinde doğrulayın.");
        }

        if (finiteRtMid.Length >= 2)
        {
            var rtMean = finiteRtMid.Average();
            var rtStd = Std(finiteRtMid);
            var rtCv = rtMean > 1e-9 ? rtStd / rtMean : 0;
            metrics["RT mid değişkenlik"] = rtCv;
            if (rtCv > 0.25)
            {
                score -= 8;
                warnings.Add($"Kaynak-alıcı çiftleri arasında RT60 değişkenliği yüksek (CV={rtCv:P0}); oda homojen olmayabilir veya örnekleme yetersiz olabilir.");
            }
        }

        var finiteSti = acousticParameters.Select(a => a.STI).Where(double.IsFinite).ToArray();
        metrics["STI hesaplanma oranı"] = acousticParameters.Length == 0 ? 0 : finiteSti.Length / (double)acousticParameters.Length;
        if (acousticParameters.Length > 0 && finiteSti.Length < acousticParameters.Length)
        {
            score -= 10;
            warnings.Add("Bazı çiftlerde STI hesaplanamadı; ekogram enerjisi veya bant kapsamı yetersiz.");
            recommendations.Add("STI için arka plan gürültüsü, kaynak seviyesi ve hoparlör yönelimi verilerini proje girdisi olarak zenginleştirin.");
        }

        var fsValues = acousticParameters
            .Select(a => a.SchroederFrequency(project.RoomVolume))
            .Where(double.IsFinite)
            .ToArray();
        if (fsValues.Length > 0)
        {
            var fsAvg = fsValues.Average();
            metrics["Schroeder frekansı"] = fsAvg;
            var weakBands = OctaveBands.Where(f => f < fsAvg).ToArray();
            if (weakBands.Length > 0)
            {
                score -= weakBands.Contains(250) ? 12 : 7;
                warnings.Add($"Schroeder frekansı yaklaşık {fsAvg:F0} Hz; {string.Join(", ", weakBands)} Hz bantları geometrik akustikte düşük güvenlidir.");
                recommendations.Add("Schroeder frekansı altındaki bantlar için oda modları/dalga tabanlı kontrol kullanın.");
            }
        }
        else
        {
            score -= 8;
            warnings.Add("Schroeder frekansı hesaplanamadı; hacim veya RT60 mid verisi eksik.");
        }

        // ── Bozunma eğrisi kalitesi: doğrusal uyum (R²) ve dinamik aralık ──
        var midBands = acousticParameters
            .SelectMany(a => a.Bands.Where(b => b.FrequencyHz is 500 or 1000))
            .ToArray();

        var fitR2 = midBands.Select(b => b.DecayFitR2).Where(double.IsFinite).ToArray();
        if (fitR2.Length > 0)
        {
            var meanR2 = fitR2.Average();
            metrics["RT bozunma uyumu (R²)"] = meanR2;
            if (meanR2 < 0.90)
            {
                score -= 12;
                warnings.Add($"Bozunma eğrisi doğrusallıktan uzak (R²={meanR2:F2}); RT60 regresyonu gürültülü/güvenilmez. Daha fazla ışın veya alıcı yarıçapı gerekebilir.");
                recommendations.Add("Işın sayısını artırın; tek yönlü kaçışları azaltmak için kapalı geometriyi ve alıcı konumunu doğrulayın.");
            }
            else if (meanR2 < 0.97)
            {
                score -= 5;
                recommendations.Add($"Bozunma uyumu orta (R²={meanR2:F2}); kritik kararlar öncesi ışın sayısını artırıp eğriyi düzleştirin.");
            }
        }

        var dynRange = midBands.Select(b => b.DynamicRangeDb).Where(double.IsFinite).ToArray();
        if (dynRange.Length > 0)
        {
            var worstRange = dynRange.Max(); // en sığ (0'a en yakın) bozunma
            metrics["Bozunma dinamik aralığı (dB)"] = dynRange.Min();
            if (worstRange > -35.0)
            {
                score -= 8;
                warnings.Add($"Bazı bantlarda bozunma yalnızca {worstRange:F0} dB'e ulaşıyor (<35 dB); T30 ekstrapolasyona dayanıyor, RT60 güvenilirliği düşer.");
                recommendations.Add("Maksimum yansıma mertebesini ve enerji eşiğini artırarak bozunmanın −35 dB altına inmesini sağlayın.");
            }
        }

        if (result.Parameters.MaxOrder < 80)
        {
            score -= 6;
            recommendations.Add("Uzun RT beklenen hacimlerde maksimum yansıma mertebesini 100+ kullanın.");
        }

        var label = LabelFor(score);
        var summary = $"{label} güven ({score:F0}/100) - {result.TotalArrivals:N0} geliş, {raysPerPair:N0} ışın/kaynak, materyal ataması {materialRatio:P0}.";

        if (warnings.Count == 0)
            warnings.Add("Temel kalite kontrollerinde kritik uyarı yok; yine de ölçüm yerine geçmeden önce yakınsama ve saha kalibrasyonu önerilir.");

        recommendations.Add("Rapor tesliminden önce aynı modeli farklı ışın sayılarıyla çalıştırıp RT/C80/STI değişimini karşılaştırın.");
        recommendations.Add("Mümkünse ISO 3382 saha ölçümüyle model alfa/saçılım değerlerini kalibre edin.");

        return new SimulationReliabilityReport(score, label, summary, warnings.Distinct().ToArray(), recommendations.Distinct().ToArray(), metrics);
    }

    private static string LabelFor(double score) => score switch
    {
        >= 85 => "Yüksek",
        >= 70 => "Orta-Yüksek",
        >= 55 => "Orta",
        >= 40 => "Düşük-Orta",
        _ => "Düşük"
    };

    // Tüm yüzey köşelerinin sınır kutusundan (en küçük, en büyük) kenar uzunluğu.
    private static (double min, double max) RoomExtent(AcousticsProject project)
    {
        double xMin = double.MaxValue, yMin = double.MaxValue, zMin = double.MaxValue;
        double xMax = double.MinValue, yMax = double.MinValue, zMax = double.MinValue;
        bool any = false;

        foreach (var s in project.Surfaces)
            foreach (var v in s.Vertices)
            {
                any = true;
                if (v.X < xMin) xMin = v.X; if (v.X > xMax) xMax = v.X;
                if (v.Y < yMin) yMin = v.Y; if (v.Y > yMax) yMax = v.Y;
                if (v.Z < zMin) zMin = v.Z; if (v.Z > zMax) zMax = v.Z;
            }

        if (!any) return (0, 0);

        var dx = xMax - xMin;
        var dy = yMax - yMin;
        var dz = zMax - zMin;
        var dims = new[] { dx, dy, dz }.Where(d => d > 1e-9).ToArray();
        return dims.Length == 0 ? (0, 0) : (dims.Min(), dims.Max());
    }

    private static double Std(IReadOnlyList<double> values)
    {
        if (values.Count <= 1) return 0;
        var mean = values.Average();
        var variance = values.Sum(v => (v - mean) * (v - mean)) / (values.Count - 1);
        return Math.Sqrt(variance);
    }
}
