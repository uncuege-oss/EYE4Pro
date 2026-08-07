using System;
using System.Collections.Generic;
using System.Linq;

namespace Kst.Acoustics.Core.ImpactSurface
{
    /// <summary>
    /// EN 1177 ve ASTM F1292 standartlarına uygun darbe simülasyonu yapan gelişmiş servis.
    /// </summary>
    public static class ImpactCalculationService
    {
        private const double G = 9.81; // m/s²

        public static ImpactCalculationResult Calculate(ImpactLayerStack stack, double dropHeightM = 1.5, double headMass = 5.0, double contactArea = 0.01)
        {
            ArgumentNullException.ThrowIfNull(stack);

            var result = new ImpactCalculationResult
            {
                TotalThickness = stack.TotalThickness,
                TotalWeight = stack.TotalWeight,
                TotalAlphaW = stack.CalculateCompositeAlphaW(),
                IsValid = true
            };

            if (!IsPositiveFinite(dropHeightM) || !IsPositiveFinite(headMass) || !IsPositiveFinite(contactArea))
            {
                result.IsValid = false;
                result.Errors.Add("Düşüş yüksekliği, kafa kütlesi ve temas alanı pozitif sonlu değerler olmalıdır.");
                result.StatusMessage = "HATA: Geçersiz simülasyon parametresi.";
                return result;
            }

            var activeLayers = stack.Layers.Where(l => l.IsEnabled).ToList();
            if (activeLayers.Count == 0)
            {
                result.Gmax = 5000; // Sert zemin (Beton vb.)
                result.HIC = 9999;
                result.StatusMessage = "UYARI: Aktif katman bulunamadı. Sert zemin varsayılıyor.";
                return result;
            }

            try
            {
                // 1. Seri-Yay Modeli ile Eşdeğer E-Modülü ve Sönümleme
                double totalThicknessM = result.TotalThickness / 1000.0;
                if (!IsPositiveFinite(result.TotalThickness) || activeLayers.Any(l => !IsPositiveFinite(l.ThicknessMm)))
                {
                    result.IsValid = false;
                    result.Errors.Add("Aktif katman kalınlıkları pozitif sonlu değerler olmalıdır.");
                    result.StatusMessage = "HATA: Geçersiz katman kalınlığı.";
                    return result;
                }

                double invEeq = 0;
                double weightedDamping = 0;

                foreach (var layer in activeLayers)
                {
                    var material = layer.Material ?? ImpactMaterialDatabase.GetByCode(layer.MaterialCode);
                    double thicknessRatio = layer.ThicknessMm / result.TotalThickness;
                    
                    if (material != null && IsPositiveFinite(material.ElasticModulus))
                    {
                        invEeq += thicknessRatio * (1.0 / material.ElasticModulus);
                        weightedDamping += thicknessRatio * (Math.Clamp(material.ImpactAbsorption, 0.0, 100.0) / 100.0);
                    }
                    else
                    {
                        invEeq += thicknessRatio * (1.0 / 2000.0); // Beton sertliği
                    }
                }

                double eEq = 1.0 / invEeq; // MPa
                double dampingRatio = Math.Clamp(weightedDamping, 0.0, 0.98); // 0-1 arası sönümleme tahmini

                // 2. Darbe Dinamiği Simülasyonu
                double dropVelocity = Math.Sqrt(2 * G * dropHeightM);
                
                // Karakteristik frekans (f = 1/2pi * sqrt(k/m))
                double stiffness = (eEq * 1000000.0 * contactArea) / Math.Max(totalThicknessM, 0.001);
                double omegaN = Math.Sqrt(stiffness / headMass);
                if (!IsPositiveFinite(omegaN))
                {
                    result.IsValid = false;
                    result.Errors.Add("Katman rijitliği veya kafa kütlesi geçersiz.");
                    result.StatusMessage = "HATA: Dinamik model kurulamadı.";
                    return result;
                }
                
                // Darbe süresi (yarım periyot gibi düşünülürse)
                double duration = Math.PI / omegaN; 
                // Sönümleme ile süreyi biraz uzatalım
                duration *= (1.0 + dampingRatio);

                // 3. İvme Zaman Serisi Üretimi (Half-Sine Pulse)
                int steps = 100;
                double dt = duration / steps;
                double[] accelSeriesG = new double[steps];
                
                double aPeakMs2 = dropVelocity * omegaN; // Basit harmonik hareket tepe ivmesi
                // Sönümleme etkisiyle tepe ivmesini azaltalım
                aPeakMs2 *= (1.0 - dampingRatio * 0.5);

                for (int i = 0; i < steps; i++)
                {
                    double t = i * dt;
                    accelSeriesG[i] = (aPeakMs2 * Math.Sin(Math.PI * t / duration)) / G;
                    if (accelSeriesG[i] < 0) accelSeriesG[i] = 0;
                }

                // 4. Standart HIC Hesabı (Sliding Window Integral)
                result.Gmax = accelSeriesG.Max();
                result.HIC = CalculateStandardHic(accelSeriesG, dt);
                result.Deformation = (dropVelocity / omegaN) * 1000.0; // mm
                result.AccelSeries = accelSeriesG;
                result.PulseDurationMs = duration * 1000.0;

                // 5. Kritik Düşüş Yüksekliği Tahmini
                // Not: Bu bir tahmindir (Estimate)
                double cfhHic = dropHeightM * Math.Pow(1000.0 / Math.Max(result.HIC, 1), 0.667);
                double cfhGmax = dropHeightM * Math.Pow(200.0 / Math.Max(result.Gmax, 1), 2.0);
                result.CriticalFallHeight = Math.Min(cfhHic, cfhGmax);

                // 6. Geri Sıçrama (COR)
                result.CoefficientOfRestitution = Math.Max(0.05, 0.9 - dampingRatio);

                // 7. Durum Kontrolü
                if (!result.IsPassed)
                {
                    if (result.HIC >= 1000) result.Errors.Add("HIC sınırı aşıldı (>1000)");
                    if (result.Gmax >= 200) result.Errors.Add("Gmax sınırı aşıldı (>200g)");
                    result.StatusMessage = "TEHLİKELİ: " + string.Join(", ", result.Errors);
                }
                else
                {
                    result.StatusMessage = "GÜVENLİ: Limitler karşılanıyor.";
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add("Hesaplama hatası: " + ex.Message);
                result.StatusMessage = "HATA: Hesaplama yapılamadı.";
            }

            return result;
        }

        /// <summary>
        /// EN 1177 / ASTM F1292 tipi uyum raporu: birden çok düşüş yüksekliğinde
        /// motoru çalıştırır ve bin aramayla hassas CFH üretir.
        /// </summary>
        public static StandardComplianceReport EvaluateCompliance(
            ImpactLayerStack stack,
            double headMass = 4.6,
            double contactArea = 0.01,
            double[]? testHeights = null)
        {
            ArgumentNullException.ThrowIfNull(stack);

            testHeights ??= new[] { 0.5, 1.0, 1.5, 2.0, 2.5, 3.0 };

            var report = new StandardComplianceReport
            {
                HeadMassKg = headMass,
                ContactAreaM2 = contactArea
            };

            foreach (var h in testHeights)
            {
                var r = Calculate(stack, h, headMass, contactArea);
                if (!r.IsValid)
                {
                    report.IsValid = false;
                    report.Notes.AddRange(r.Errors);
                }

                string verdict;
                if (!r.IsValid) verdict = "Hesap geçersiz";
                else if (r.IsPassed) verdict = "GEÇTİ";
                else if (r.HIC >= 1000 && r.Gmax >= 200) verdict = "HIC + Gmax aştı";
                else if (r.HIC >= 1000) verdict = "HIC aştı";
                else verdict = "Gmax aştı";

                report.Trials.Add(new StandardComplianceTrial
                {
                    DropHeightM = h,
                    GmaxG = r.Gmax,
                    Hic = r.HIC,
                    IsPassed = r.IsPassed,
                    Verdict = verdict
                });
            }

            report.CertifiedCriticalFallHeightM = FindCriticalFallHeight(stack, headMass, contactArea);
            return report;
        }

        /// <summary>
        /// Bin arama ile sınırlar dahilinde geçilen en yüksek düşüş yüksekliğini bulur.
        /// Hassasiyet ≈ 1 cm.
        /// </summary>
        private static double FindCriticalFallHeight(ImpactLayerStack stack, double headMass, double contactArea)
        {
            const double lower = 0.05;
            const double upper = 5.0;

            // Önce alt sınırın bile geçtiğinden emin ol; geçmiyorsa 0 dönüyoruz
            var rLow = Calculate(stack, lower, headMass, contactArea);
            if (!rLow.IsValid || !rLow.IsPassed) return 0.0;

            // Üst sınır geçiyorsa zaten 5m'in üstü → 5m döner
            var rHigh = Calculate(stack, upper, headMass, contactArea);
            if (rHigh.IsValid && rHigh.IsPassed) return upper;

            double lo = lower, hi = upper;
            for (int i = 0; i < 30; i++)
            {
                double mid = 0.5 * (lo + hi);
                var rm = Calculate(stack, mid, headMass, contactArea);
                if (rm.IsValid && rm.IsPassed) lo = mid;
                else hi = mid;
                if (hi - lo < 0.005) break; // 5 mm hassasiyet yeterli
            }
            return Math.Round(lo, 2);
        }

        private static bool IsPositiveFinite(double value) => double.IsFinite(value) && value > 0;

        private static double CalculateStandardHic(double[] accelSeriesG, double dt)
        {
            double maxHic = 0;
            int n = accelSeriesG.Length;

            // Kayar pencere ile HIC(t1, t2) integralini tara
            for (int i = 0; i < n; i++)
            {
                double sum = 0;
                for (int j = i; j < n; j++)
                {
                    sum += accelSeriesG[j];
                    double duration = (j - i + 1) * dt;
                    if (duration > 0.036) continue; // Standart pencere sınırı 36ms veya 15ms

                    double avgA = sum / (j - i + 1);
                    double hic = duration * Math.Pow(avgA, 2.5);
                    if (hic > maxHic) maxHic = hic;
                }
            }
            return maxHic;
        }
    }
}
