using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Measurement.Data;

namespace Kst.Acoustics.Core.Measurement.Processing;

public class EqGenerator
{
    public static List<BiquadFilter> GenerateFilters(FrequencyResponseData fr, TargetCurve target, double fs)
    {
        var filters = new List<BiquadFilter>();
        var error = new double[fr.Frequencies.Length];

        // 1. Mevcut hata payını hesapla
        for (int i = 0; i < fr.Frequencies.Length; i++)
        {
            double targetDb = TargetCurveService.GetTargetLevel(target, fr.Frequencies[i]);
            error[i] = targetDb - fr.Magnitudes[i];
        }

        // 2. En büyük 5 tepeyi bul ve düzelt (Basit yaklaşım)
        // Gerçekte bu iteratif bir süreçtir.
        for (int iteration = 0; iteration < 5; iteration++)
        {
            // En büyük hatayı bul (yalnız 2 kHz altına odaklanılır - oda modları bölgesi)
            double maxErr = 0;
            int maxIdx = -1;
            for (int i = 0; i < fr.Frequencies.Length; i++)
            {
                if (fr.Frequencies[i] > 2000) continue; // 2kHz üstüne EQ yapmayalım (opsiyonel sınır)
                if (Math.Abs(error[i]) > Math.Abs(maxErr))
                {
                    maxErr = error[i];
                    maxIdx = i;
                }
            }

            if (maxIdx == -1 || Math.Abs(maxErr) < 1.0) break;

            double f0 = fr.Frequencies[maxIdx];
            // Kazanç tipik bir PEQ aralığıyla sınırlanır; hatalı/gürültülü bir FR ölçümü
            // (ör. çok düşük seviyeli sweep) aşırı büyük hata üretse bile filtre mantıksız
            // kazanç (ör. +160 dB) üretmemelidir.
            double gain = Math.Clamp(maxErr, -12.0, 12.0);
            double q = 4.0; // Sabit Q (Şimdilik)

            var filter = BiquadFilter.CreatePeak(f0, gain, q, fs);
            filters.Add(filter);

            // Hata dizisini güncelle
            for (int i = 0; i < fr.Frequencies.Length; i++)
            {
                error[i] -= filter.GetResponse(fr.Frequencies[i], fs);
            }
        }

        return filters;
    }
}
