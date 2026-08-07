using System;
using System.Collections.Generic;
using System.Linq;

namespace Kst.Acoustics.Core.Projects;

public static class LoudspeakerDefaults
{
    public static List<AcousticLoudspeaker> GetDefaults()
    {
        return new List<AcousticLoudspeaker>
        {
            CreateGeneric("REF-01", "Reference", "Omni Source", 90, 360, 360),
            CreateGeneric("CEIL-01", "Generic", "Ceiling Speaker 6\"", 88, 120, 120),
            CreateGeneric("WALL-01", "Generic", "Wall Mount 8\"", 92, 90, 60),
            CreateGeneric("ARRAY-01", "Generic", "Line Array Module", 102, 90, 10),
            CreateGeneric("HORN-01", "Generic", "Long Throw Horn", 108, 40, 20),
            CreateGeneric("SUB-01", "Generic", "Active Subwoofer 18\"", 95, 300, 300),
            CreateGeneric("COL-01", "Generic", "Column Speaker", 94, 120, 15)
        };
    }

    private static AcousticLoudspeaker CreateGeneric(string id, string man, string model, double sensBase, double hBeam, double vBeam)
    {
        // 8 Octave bands: 63, 125, 250, 500, 1k, 2k, 4k, 8k
        double[] sensitivity = new double[8];
        DirectivityBalloon[] balloons = new DirectivityBalloon[8];

        for (int i = 0; i < 8; i++)
        {
            // Frekans arttıkça duyarlılık genelde hafif artar, sub hariç
            if (model.Contains("Subwoofer"))
                sensitivity[i] = i < 3 ? sensBase : sensBase - (i - 2) * 15;
            else
                sensitivity[i] = sensBase + (i > 0 ? Math.Min(i, 4) : 0);

            // Frekans arttıkça beamwidth daralır (fizik kuralı)
            // 1kHz'deki beamwidth baz alınır.
            double freqFactor = Math.Pow(2, i - 4); // 1k Hz is index 4
            double currentH = hBeam / Math.Sqrt(freqFactor);
            double currentV = vBeam / Math.Sqrt(freqFactor);
            
                // Limit beamwidth to 360/180
                currentH = Math.Min(currentH, 360);
                currentV = Math.Min(currentV, 180);

            balloons[i] = GenerateBalloon(10, currentH, currentV);
        }

        return new AcousticLoudspeaker(id, man, model, sensitivity, balloons);
    }

    private static DirectivityBalloon GenerateBalloon(int res, double hBeam, double vBeam)
    {
        int rows = 180 / res + 1;
        int cols = 360 / res;
        double[,] data = new double[rows, cols];

        for (int t = 0; t < rows; t++)
        {
            double thetaDeg = t * res; // 0 = hoparlör ileri ekseni, 180 = arka eksen

            for (int p = 0; p < cols; p++)
            {
                double phiDeg = p * res; // 0 -> 360
                if (hBeam >= 360 && vBeam >= 180)
                {
                    data[t, p] = 0;
                    continue;
                }

                // Elips beamwidth hesabı
                // hBeam ve vBeam 'total' açı olduğu için yarıçap / 2 kullanılır.
                double radH = hBeam / 2.0;
                double radV = vBeam / 2.0;

                // Simülasyon tarafındaki LocalAngles ile uyumlu polar eksen:
                // theta=0 ileri eksen, phi ise o eksen çevresindeki yatay/dikey yönü belirtir.
                double phiRad = phiDeg * Math.PI / 180.0;
                double denominator = Math.Sqrt(Math.Pow(radV * Math.Cos(phiRad), 2) + Math.Pow(radH * Math.Sin(phiRad), 2));
                double effectiveBeamRadius = denominator > 0 ? (radH * radV) / denominator : radH;

                double attenuation = 0;
                if (effectiveBeamRadius > 0)
                {
                    // -6dB noktası beamwidth/2'dedir.
                    attenuation = -6.0 * Math.Pow(thetaDeg / effectiveBeamRadius, 2);
                }

                // Minimum -40dB (arkaya sızıntı)
                data[t, p] = Math.Max(attenuation, -40.0);
            }
        }

        return new DirectivityBalloon(res, data);
    }
}
