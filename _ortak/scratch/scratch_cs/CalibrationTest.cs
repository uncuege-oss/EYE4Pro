using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.NoiseBarrier;

namespace Kst.Acoustics.Scratch;

public class CalibrationTest
{
    public static void Run()
    {
        Console.WriteLine("--- Trafik Modeli Kalibrasyon Testi ---");
        
        var project = new NoiseBarrierProject
        {
            Name = "Kalibrasyon Testi",
            Building = new BuildingModel { Width = 20, Height = 10, Depth = 10 },
            Barrier = new BarrierModel { Height = 0.1, Length = 100, PositionY = -5 } // Almost no barrier
        };
        
        // 1000 vph, 50 km/h, 10% heavy
        var source = new NoiseSource
        {
            Name = "Referans Yol",
            Type = SourceType.Traffic,
            X = -250, Y = -20, Z = 0.5,
            XEnd = 250, YEnd = -20,
            TrafficFlowVehiclesPerHour = 1000,
            AverageSpeedKmh = 50,
            HeavyVehiclePercent = 10,
            TrafficModel = TrafficEmissionModel.CnossosEuPreview
        };
        project.Sources.Add(source);
        
        // Receiver at 10m from the road (Road is at Y=-20, Building Facade at Y=0, so receiver is at Y=-0.5? No, FacadeReceiverOffset is 0.5)
        // Let's place a manual receiver at exactly 10m from road center.
        // Road is at Y=-20. 10m from road is Y=-10.
        var receiver = new ReceiverPoint { Id = 1, X = 0, Y = -10, Z = 1.5 };
        project.Receivers.Add(receiver);
        
        var engine = new NoiseBarrierEngine(project, new List<NoiseBarrierMaterial>());
        engine.RunSimulation();
        
        Console.WriteLine($"Kaynak: {source.Name}");
        Console.WriteLine($"Alici Konumu: (0, -10, 1.5) [Yol-Alici Mesafesi: 10m]");
        Console.WriteLine($"Hesaplanan Lp: {receiver.TotalDbANoBarrier:F1} dB(A)");
        
        // Reference check:
        // For 1000 vph at 50km/h, CNOSSOS basic emission is ~68-72 dB(A) per meter? No, Lw' is usually ~65-70 dB(A)/m.
        // At 10m, it should be around 62-66 dB(A).
        
        if (receiver.TotalDbANoBarrier < 55)
            Console.WriteLine("UYARI: Seviye cok dusuk gorunuyor! (< 55 dB)");
        else if (receiver.TotalDbANoBarrier > 75)
            Console.WriteLine("UYARI: Seviye cok yuksek gorunuyor! (> 75 dB)");
        else
            Console.WriteLine("SONUC: Seviye makul aralikta (55-75 dB).");
            
        Console.WriteLine("---------------------------------------");
    }
}
