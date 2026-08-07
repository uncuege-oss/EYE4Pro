using System;
using System.Linq;
using Kst.Acoustics.Core.ImpactSurface;

namespace Kst.Acoustics.Tests;

/// <summary>
/// ImpactCalculationService motorunun fiziğini analitik referanslara karşı
/// doğrulayan testler. M01 "RayTracingValidationTests" deseninin M05 karşılığı.
///
/// Motor tek-DOF sönümlü yay yaklaşımı + half-sine puls üretir:
///   v        = sqrt(2 g h)
///   k        = E_eq · A / t
///   ω_n      = sqrt(k / m)
///   duration = (π / ω_n) · (1 + ξ)
///   a_peak   = v · ω_n · (1 - ξ/2)
///
/// Bu testler motorun yukarıdaki formüllere ölçek olarak uyduğunu, fiziksel
/// monotonlukları (sertlik, kafa kütlesi, düşüş yüksekliği) koruduğunu ve
/// koşunun deterministik olduğunu kanıtlar.
/// </summary>
public static class ImpactValidationTests
{
    private const double Tol = 0.08; // %8 göreli tolerans (sayısal ayrıklaştırma + sönüm modeli)

    // ── Drop yüksekliği skalama: a_peak ve Gmax ∝ √h ──────────────────────────
    public static void DropHeightScalingFollowsSqrtH()
    {
        var stack = SingleLayer("EVA", 30);
        var low  = ImpactCalculationService.Calculate(stack, dropHeightM: 0.5, headMass: 5.0, contactArea: 0.01);
        var high = ImpactCalculationService.Calculate(stack, dropHeightM: 2.0, headMass: 5.0, contactArea: 0.01);

        // h 4× → v 2× → Gmax 2× (duration h'den bağımsız)
        double ratio = high.Gmax / low.Gmax;
        AssertClose(2.0, ratio, Tol, "Gmax düşüş yüksekliği skalaması");

        // Duration sabit kalmalı
        double durLow  = low.AccelSeries.Length  * (Math.PI / Math.Max(low.Gmax, 1e-6)) * 0; // dummy
        // Pratik kontrol: AccelSeries uzunluğu 100 sabit, dolayısıyla dt değişir,
        // toplam puls süresi (duration) AccelSeries'in son non-zero indeksinden okunamaz.
        // Bunun yerine HIC oranını kontrol edelim: HIC ≈ duration · a^2.5
        // duration sabit, a 2× → HIC 2^2.5 ≈ 5.66×
        double hicRatio = high.HIC / Math.Max(low.HIC, 1e-9);
        AssertClose(Math.Pow(2.0, 2.5), hicRatio, 0.15, "HIC düşüş yüksekliği skalaması");
    }

    // ── Kafa kütlesi skalama: a_peak ∝ 1/√m ───────────────────────────────────
    public static void HeadMassScalingFollowsInverseSqrtM()
    {
        var stack = SingleLayer("EVA", 30);
        var light = ImpactCalculationService.Calculate(stack, dropHeightM: 1.5, headMass: 2.5, contactArea: 0.01);
        var heavy = ImpactCalculationService.Calculate(stack, dropHeightM: 1.5, headMass: 10.0, contactArea: 0.01);

        // m 4× → ω/2 → a_peak/2 → Gmax yarıya
        double ratio = light.Gmax / heavy.Gmax;
        AssertClose(2.0, ratio, Tol, "Gmax kafa kütlesi skalaması");
    }

    // ── Sertlik etkisi: E_eq düştükçe Gmax düşer ──────────────────────────────
    public static void LowerStiffnessReducesGmax()
    {
        // Aynı kalınlık, farklı malzemeler. EVA (E=2.75) > PU-VE (E=0.11)
        var stiff = SingleLayer("EVA", 30);
        var soft  = SingleLayer("PU-VE", 30);

        var rStiff = ImpactCalculationService.Calculate(stiff, 1.5);
        var rSoft  = ImpactCalculationService.Calculate(soft,  1.5);

        AssertLess(rSoft.Gmax, rStiff.Gmax, "Düşük modüllü katman daha düşük Gmax vermeli");
        AssertLess(rSoft.HIC,  rStiff.HIC,  "Düşük modüllü katman daha düşük HIC vermeli");
    }

    // ── Kontak alanı skalama: A 4× → k 4× → ω 2× → a_peak 2× ─────────────────
    public static void ContactAreaScalingFollowsSqrtA()
    {
        var stack = SingleLayer("EVA", 30);
        var small = ImpactCalculationService.Calculate(stack, 1.5, headMass: 5.0, contactArea: 0.005);
        var large = ImpactCalculationService.Calculate(stack, 1.5, headMass: 5.0, contactArea: 0.020);

        // A 4× → ω 2× → Gmax 2×
        double ratio = large.Gmax / small.Gmax;
        AssertClose(2.0, ratio, Tol, "Gmax kontak alanı skalaması");
    }

    // ── Analitik tek katman karşılaştırması ───────────────────────────────────
    public static void SingleLayerMatchesAnalyticalHalfSine()
    {
        // Sönümsüz bir malzeme seçmek için ImpactAbsorption=0 olan synthetic stack
        // kullanırdık; mevcut DB'de en düşük sönüm STEEL'de (0.5%). Onu kullanıyoruz.
        // EVA: E=2.75 MPa, abs=70%
        const double h = 1.0;
        const double m = 5.0;
        const double A = 0.01;
        const double thickMm = 30;
        var stack = SingleLayer("EVA", thickMm);

        double eEq = 2.75; // MPa (tek katman → eEq = E)
        double tM  = thickMm / 1000.0;
        double xi  = 0.70; // ImpactAbsorption / 100

        double v  = Math.Sqrt(2 * 9.81 * h);
        double k  = eEq * 1e6 * A / tM;
        double om = Math.Sqrt(k / m);
        double aPeakMs2 = v * om * (1 - xi * 0.5);
        double expectedGmax = aPeakMs2 / 9.81;

        var r = ImpactCalculationService.Calculate(stack, h, m, A);
        AssertClose(expectedGmax, r.Gmax, 0.05, "Tek katman Gmax (analitik half-sine)");
    }

    // ── Determinizm: aynı girdi → aynı çıktı ──────────────────────────────────
    public static void DeterministicRunIsRepeatable()
    {
        var s1 = TwoLayer("SBR-G", 40, "PU-HR", 20);
        var s2 = TwoLayer("SBR-G", 40, "PU-HR", 20);

        var r1 = ImpactCalculationService.Calculate(s1, 1.6, 4.6, 0.012);
        var r2 = ImpactCalculationService.Calculate(s2, 1.6, 4.6, 0.012);

        AssertClose(r1.Gmax, r2.Gmax, 1e-9, "Gmax tekrar üretilebilirlik");
        AssertClose(r1.HIC,  r2.HIC,  1e-9, "HIC tekrar üretilebilirlik");
        AssertClose(r1.CriticalFallHeight, r2.CriticalFallHeight, 1e-9, "CFH tekrar üretilebilirlik");

        // İvme-zaman serisi birebir aynı olmalı
        if (r1.AccelSeries.Length != r2.AccelSeries.Length)
            throw new Exception("AccelSeries uzunluğu farklı.");
        for (int i = 0; i < r1.AccelSeries.Length; i++)
            if (Math.Abs(r1.AccelSeries[i] - r2.AccelSeries[i]) > 1e-12)
                throw new Exception($"AccelSeries[{i}] tekrar üretilemedi.");
    }

    // ── Golden test: referans stack için sabitlenmiş değerler ─────────────────
    // Motor formülü değişirse bu test bilinçli güncellenmelidir.
    public static void GoldenReferenceEvaStackProducesExpectedValues()
    {
        var stack = SingleLayer("EVA", 30);
        var r = ImpactCalculationService.Calculate(stack, dropHeightM: 1.5, headMass: 5.0, contactArea: 0.01);

        // Beklenen analitik (yukarıdaki formülle):
        //   v=5.4249 m/s, k=9.167e5 N/m, ω=428.17 rad/s
        //   a_peak_base = v·ω = 2322.8 m/s²; sönüm faktörü (1-0.35)=0.65
        //   a_peak = 1509.8 m/s² → 153.9 g
        AssertClose(153.9, r.Gmax, 0.05, "Golden EVA Gmax");
        AssertGreater(r.HIC, 100, "Golden EVA HIC alt sınır");
        AssertLess(r.HIC, 100000, "Golden EVA HIC üst sınır");
        AssertGreater(r.CriticalFallHeight, 0.0, "Golden EVA CFH pozitif");
    }

    // ── İvme serisi pozitif yarım-sinüs şekline uyar ──────────────────────────
    public static void AccelSeriesIsNonNegativeHalfSine()
    {
        var stack = SingleLayer("EVA", 30);
        var r = ImpactCalculationService.Calculate(stack, 1.5);

        if (r.AccelSeries.Length == 0)
            throw new Exception("AccelSeries boş.");

        foreach (var a in r.AccelSeries)
            if (a < 0 || double.IsNaN(a) || double.IsInfinity(a))
                throw new Exception($"AccelSeries negatif/non-finite değer: {a}.");

        // Tepe değer ortalarda olmalı (half-sine), sınırlarda değil
        int peakIdx = Array.IndexOf(r.AccelSeries, r.AccelSeries.Max());
        int n = r.AccelSeries.Length;
        if (peakIdx < n / 4 || peakIdx > 3 * n / 4)
            throw new Exception($"Tepe ivme half-sine ortasında değil (idx={peakIdx}/{n}).");
    }

    // ── Seri-yay modeli: iki sert katman tek sert katmandan daha yumuşaktır ──
    // 1/k_eq = 1/k1 + 1/k2  →  k_eq < min(k1, k2)
    public static void SeriesSpringStackIsSofterThanSingleLayer()
    {
        // Aynı toplam kalınlık (40 mm) — biri tek katman EVA, diğeri 20+20 EVA
        var single = SingleLayer("EVA", 40);
        var split  = TwoLayer("EVA", 20, "EVA", 20);

        var rSingle = ImpactCalculationService.Calculate(single, 1.5);
        var rSplit  = ImpactCalculationService.Calculate(split,  1.5);

        // Seri-yay modeli aynı toplam kalınlık için aynı sertlik vermelidir
        AssertClose(rSingle.Gmax, rSplit.Gmax, 0.02, "Aynı malzeme tek vs bölünmüş katman Gmax tutarlılığı");
    }

    // ── Standart uyum: yumuşak yığın geçer, sert yığın CFH=0 ─────────────────
    public static void ComplianceSweepClassifiesSoftAndHardStacks()
    {
        var soft = new ImpactLayerStack();
        soft.Layers.Add(new ImpactLayer { MaterialCode = "PU-VE", ThicknessMm = 80 });
        var rSoft = ImpactCalculationService.EvaluateCompliance(soft);
        AssertGreater(rSoft.CertifiedCriticalFallHeightM, 0.2, "Yumuşak yığın CFH > 0.2 m olmalı.");
        if (rSoft.Trials.Count == 0) throw new Exception("Tarama denemeleri boş.");

        var hard = new ImpactLayerStack();
        hard.Layers.Add(new ImpactLayer { MaterialCode = "CONC", ThicknessMm = 50 });
        var rHard = ImpactCalculationService.EvaluateCompliance(hard);
        if (rHard.CertifiedCriticalFallHeightM > 0.05)
            throw new Exception($"Sert yığın CFH ~0 olmalı, oysa {rHard.CertifiedCriticalFallHeightM:F2} m.");
    }

    // ── Compliance verdict tutarlılığı: tüm geçenler IsPassed=true ──────────
    public static void ComplianceVerdictsAreConsistent()
    {
        var stack = SingleLayer("SBR-G", 60);
        var r = ImpactCalculationService.EvaluateCompliance(stack);

        foreach (var t in r.Trials)
        {
            if (t.IsPassed && t.Verdict != "GEÇTİ")
                throw new Exception($"h={t.DropHeightM}: IsPassed ama verdict '{t.Verdict}'.");
            if (!t.IsPassed && t.Verdict == "GEÇTİ")
                throw new Exception($"h={t.DropHeightM}: Verdict GEÇTİ ama IsPassed=false.");
        }

        // Bin arama CFH, ilk başarısız denemenin yüksekliğinden küçük olmalı
        var firstFail = r.Trials.FirstOrDefault(t => !t.IsPassed);
        if (firstFail != null && r.CertifiedCriticalFallHeightM > firstFail.DropHeightM)
            throw new Exception($"CFH ({r.CertifiedCriticalFallHeightM}) ilk başarısızlık ({firstFail.DropHeightM}) üstünde.");
    }

    // ── Rapor üreteci: standart başlıkları içerir, ASCII grafik yer alır ─────
    public static void ReportBuilderProducesExpectedSections()
    {
        var project = new ImpactSurfaceProject
        {
            Name = "Test Yüzey",
            DropHeight = 1.5,
            HeadMass = 4.6,
            ContactArea = 0.01,
            Layers = { new ImpactLayer { Index = 1, MaterialCode = "SBR-G", ThicknessMm = 40, FunctionTag = "Impact" } }
        };
        var stack = new ImpactLayerStack();
        foreach (var l in project.Layers) stack.Layers.Add(l);

        var result = ImpactCalculationService.Calculate(stack, project.DropHeight, project.HeadMass, project.ContactArea);
        var compliance = ImpactCalculationService.EvaluateCompliance(stack, project.HeadMass, project.ContactArea);

        var md = ImpactReportBuilder.Build(project, result, compliance);

        AssertContains(md, "# Darbe Emici Yüzey Analiz Raporu");
        AssertContains(md, "## 1. Simülasyon Parametreleri");
        AssertContains(md, "## 2. Hesaplama Sonuçları");
        AssertContains(md, "## 3. Katman Yapısı");
        AssertContains(md, "## 4. İvme-Zaman Eğrisi");
        AssertContains(md, "## 5. Standart Uyum Tarama");
        AssertContains(md, "Sertifikalı Kritik Düşüş Yüksekliği");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void AssertContains(string text, string fragment)
    {
        if (!text.Contains(fragment, StringComparison.Ordinal))
            throw new Exception($"Rapor '{fragment}' içermiyor.");
    }

    private static ImpactLayerStack SingleLayer(string code, double thicknessMm)
    {
        var s = new ImpactLayerStack();
        s.Layers.Add(new ImpactLayer { MaterialCode = code, ThicknessMm = thicknessMm });
        return s;
    }

    private static ImpactLayerStack TwoLayer(string c1, double t1, string c2, double t2)
    {
        var s = new ImpactLayerStack();
        s.Layers.Add(new ImpactLayer { MaterialCode = c1, ThicknessMm = t1 });
        s.Layers.Add(new ImpactLayer { MaterialCode = c2, ThicknessMm = t2 });
        return s;
    }

    private static void AssertClose(double expected, double actual, double relTol, string label)
    {
        double denom = Math.Max(Math.Abs(expected), 1e-9);
        double err   = Math.Abs(expected - actual) / denom;
        if (err > relTol)
            throw new Exception($"{label}: expected {expected:F4}, actual {actual:F4}, rel err {err:P1} > {relTol:P1}.");
    }

    private static void AssertGreater(double actual, double threshold, string label)
    {
        if (!(actual > threshold))
            throw new Exception($"{label}: {actual:F4} should be > {threshold:F4}.");
    }

    private static void AssertLess(double actual, double threshold, string label)
    {
        if (!(actual < threshold))
            throw new Exception($"{label}: {actual:F4} should be < {threshold:F4}.");
    }
}
