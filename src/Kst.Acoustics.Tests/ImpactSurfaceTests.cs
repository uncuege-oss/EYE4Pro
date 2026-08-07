using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.ImpactSurface;

namespace Kst.Acoustics.Tests
{
    public static class ImpactSurfaceTests
    {
        public static void RunAll()
        {
            Console.WriteLine("Running Impact Surface Tests...");
            
            RunTest("Calculation results are finite", CalculationResultsAreFinite);
            RunTest("Critical Fall Height is logical", CriticalFallHeightIsLogical);
            RunTest("Total thickness is correct", TotalThicknessIsCorrect);
            RunTest("Empty stack handles gracefully", EmptyStackHandlesGracefully);
            RunTest("Templates load correctly", TemplatesLoadCorrectly);
            RunTest("Extreme thickness handles gracefully", ExtremeThicknessHandlesGracefully);
            RunTest("HIC Calculation matches physics (a_peak conversion)", HicCalculationIsCorrectScale);
            RunTest("Invalid inputs report validation errors", InvalidInputsReportValidationErrors);
            RunTest("Project serialization preserves parameters and materials", ProjectSerializationPreservesParametersAndMaterials);
        }

        private static void RunTest(string name, Action test)
        {
            try
            {
                test();
                Console.WriteLine($"  [PASS] {name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [FAIL] {name}: {ex.Message}");
                throw; // Rethrow to stop the runner if needed
            }
        }

        public static void CalculationResultsAreFinite()
        {
            var stack = CreateTestStack();
            var result = ImpactCalculationService.Calculate(stack, 1.5);

            AssertFinite(result.Gmax, "Gmax");
            AssertFinite(result.HIC, "HIC");
            AssertFinite(result.CriticalFallHeight, "CFH");
        }

        public static void CriticalFallHeightIsLogical()
        {
            var stackThin = new ImpactLayerStack();
            stackThin.Layers.Add(new ImpactLayer { MaterialCode = "EVA", ThicknessMm = 10 });
            var resultThin = ImpactCalculationService.Calculate(stackThin, 1.0);

            var stackThick = new ImpactLayerStack();
            stackThick.Layers.Add(new ImpactLayer { MaterialCode = "EVA", ThicknessMm = 50 });
            var resultThick = ImpactCalculationService.Calculate(stackThick, 1.0);

            AssertGreater(resultThick.CriticalFallHeight, resultThin.CriticalFallHeight, "Kalın katman daha yüksek CFH sağlamalıdır.");
        }

        public static void TotalThicknessIsCorrect()
        {
            var stack = CreateTestStack(); 
            var result = ImpactCalculationService.Calculate(stack, 1.5);
            AssertClose(60.0, result.TotalThickness, 0.1, "Toplam kalınlık");
        }

        public static void EmptyStackHandlesGracefully()
        {
            var stack = new ImpactLayerStack();
            var result = ImpactCalculationService.Calculate(stack, 1.5);

            AssertGreater(result.Gmax, 1000, "Boş yığın çok yüksek Gmax vermeli");
            AssertClose(0, result.TotalThickness, 0.1, "Boş yığın kalınlığı 0 olmalı");
        }

        public static void TemplatesLoadCorrectly()
        {
            var templates = ImpactSurfaceTemplate.GetDefaultTemplates();
            AssertGreater(templates.Count, 3, "Şablonlar yüklenemedi");
        }

        public static void ExtremeThicknessHandlesGracefully()
        {
            var stack = new ImpactLayerStack();
            stack.Layers.Add(new ImpactLayer { MaterialCode = "GEL", ThicknessMm = 500 }); 
            var result = ImpactCalculationService.Calculate(stack, 2.0);

            AssertFinite(result.Gmax, "Aşırı kalınlık Gmax");
            AssertGreater(result.CriticalFallHeight, 0.2, "Aşırı kalınlık CFH"); // Birim düzeltmesi sonrası eşik güncellendi
        }

        public static void HicCalculationIsCorrectScale()
        {
            // [K1] Düzeltmesi sonrası HIC değerlerinin gerçekçi olup olmadığını kontrol et
            var stack = new ImpactLayerStack();
            stack.Layers.Add(new ImpactLayer { MaterialCode = "EVA", ThicknessMm = 20 });
            var result = ImpactCalculationService.Calculate(stack, 1.0);
            
            // 20mm EVA, 1m yükseklik -> HIC yüzlerce/binlerce olmalı, 0.5 falan olmamalı
            AssertGreater(result.HIC, 100, $"HIC değeri gerçekçi değil ({result.HIC:F1}). Birim dönüşüm hatası olabilir.");
        }

        public static void InvalidInputsReportValidationErrors()
        {
            var stack = CreateTestStack();
            var result = ImpactCalculationService.Calculate(stack, dropHeightM: -1.0, headMass: 0, contactArea: 0.01);

            if (result.IsValid)
                throw new Exception("Geçersiz parametreler IsValid=false üretmeli.");
            if (string.IsNullOrWhiteSpace(result.StatusMessage))
                throw new Exception("Geçersiz parametreler kullanıcıya durum mesajı üretmeli.");
        }

        public static void ProjectSerializationPreservesParametersAndMaterials()
        {
            var project = new ImpactSurfaceProject
            {
                Name = "Test",
                DropHeight = 2.4,
                HeadMass = 4.6,
                ContactArea = 0.018,
                TemplateName = "Özel",
                Layers = new List<ImpactLayer>
                {
                    new() { Index = 1, MaterialCode = "CUSTOM_PAD", ThicknessMm = 35, FunctionTag = "Impact" }
                },
                CustomMaterials = new List<ImpactMaterial>
                {
                    new() { Code = "CUSTOM_PAD", Name = "Özel Pad", Density = 500, ElasticModulus = 3.2, ImpactAbsorption = 72, AlphaW = 0.34, Category = "Impact", Source = "Kullanıcı Tanımlı" }
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(project);
            var restored = System.Text.Json.JsonSerializer.Deserialize<ImpactSurfaceProject>(json)
                           ?? throw new Exception("Proje deserialize edilemedi.");

            AssertClose(2.4, restored.DropHeight, 0.001, "Düşüş yüksekliği");
            AssertClose(4.6, restored.HeadMass, 0.001, "Kafa kütlesi");
            AssertClose(0.018, restored.ContactArea, 0.001, "Temas alanı");
            AssertClose(35, restored.Layers.Single().ThicknessMm, 0.001, "Katman kalınlığı");
            AssertClose(72, restored.CustomMaterials.Single().ImpactAbsorption, 0.001, "Özel malzeme darbe absorpsiyonu");
        }

        // --- Helpers ---

        private static ImpactLayerStack CreateTestStack()
        {
            var stack = new ImpactLayerStack();
            stack.Layers.Add(new ImpactLayer { MaterialCode = "SBR-G", ThicknessMm = 40 });
            stack.Layers.Add(new ImpactLayer { MaterialCode = "PU-HR", ThicknessMm = 20 });
            return stack;
        }

        private static void AssertClose(double expected, double actual, double tolerance, string label)
        {
            if (Math.Abs(expected - actual) > tolerance)
                throw new Exception($"{label}: expected {expected:F2}, actual {actual:F2}.");
        }

        private static void AssertGreater(double actual, double threshold, string message)
        {
            if (actual <= threshold)
                throw new Exception($"{message} Actual {actual:F2}, threshold {threshold:F2}.");
        }

        private static void AssertFinite(double value, string label)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new Exception($"{label} is not finite.");
        }
    }
}
