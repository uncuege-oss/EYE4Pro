using System;
using System.Collections.Generic;
using System.Linq;

namespace Kst.Acoustics.Core.SoundInsulation;

public static class SoundInsulationValidation
{
    public static void RunSampleCalculations()
    {
        var assemblies = SoundInsulationLibrary.GetSampleAssemblies();
        
        foreach (var assembly in assemblies)
        {
            Console.WriteLine($"--- System: {assembly.Name} ---");
            var result = SoundInsulationCalculator.Calculate(assembly);
            
            Console.WriteLine($"Total Mass: {assembly.TotalSurfaceMass:F2} kg/m2");
            if (result.MassAirMassResonance > 0)
                Console.WriteLine($"MAM Resonance: {result.MassAirMassResonance:F1} Hz");
            
            if (result.CriticalFrequencies is not null)
            {
                foreach (var cf in result.CriticalFrequencies)
                    Console.WriteLine($"Critical Frequency ({cf.Key}): {cf.Value:F1} Hz");
            }

            Console.WriteLine("TL Values (dB):");
            foreach (var band in SoundInsulationConstants.OctaveBands)
            {
                if (result.TransmissionLossValues.TryGetValue(band, out double val))
                {
                    Console.WriteLine($"  {band} Hz: {val:F1} dB");
                }
            }
            
            if (result.RwRating != null)
            {
                var r = result.RwRating;
                Console.WriteLine($"Result: Rw ({r.C}; {r.Ctr}) = {r.Rw} ({r.C}; {r.Ctr}) dB");
                Console.WriteLine($"Limiting Frequencies: {string.Join(", ", r.LimitingFrequencies)} Hz");
            }

            foreach (var warning in result.Warnings)
                Console.WriteLine($"[Warning] {warning}");

            Console.WriteLine();
        }
    }

    /// <summary>
    /// Aynı şablonu Empirical/TMM/Hybrid yöntemleriyle çalıştırıp Rw kıyasını verir.
    /// </summary>
    public static void RunMethodComparison()
    {
        var assemblies = SoundInsulationLibrary.GetSampleAssemblies();
        var methods = new[] { CalculationMethod.Empirical, CalculationMethod.TMM, CalculationMethod.Hybrid };

        foreach (var sample in assemblies)
        {
            Console.WriteLine($"--- {sample.Name} ---");
            foreach (var method in methods)
            {
                var result = SoundInsulationCalculator.Calculate(sample with { Method = method });
                var rw = result.RwRating?.Rw.ToString() ?? "-";
                var c  = result.RwRating?.C.ToString() ?? "-";
                var ctr = result.RwRating?.Ctr.ToString() ?? "-";
                Console.WriteLine($"  {method,-10}  Rw = {rw} ({c}; {ctr}) dB  ·  ReliabilityScore = {result.ReliabilityScore:F0}");
            }
            Console.WriteLine();
        }
    }
}
