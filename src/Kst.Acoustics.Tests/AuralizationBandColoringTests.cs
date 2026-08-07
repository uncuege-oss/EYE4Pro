using Kst.Acoustics.Core.Simulation;

namespace Kst.Acoustics.Tests;

/// <summary>
/// Auralizasyon IR sentezinin frekansa bağlı renklendirmesini doğrular.
/// Bantbazlı IR sistemine geçtikten sonra: yalnız bir oktav bantta enerji taşıyan
/// arrival → çıkan IR'nin spektrumu o bantta pik göstermeli, diğer bantlarda
/// belirgin düşük olmalı. Eski (broadband-aritmetik-ortalama) sistemde bu
/// kontrol başarısız olurdu.
/// </summary>
public static class AuralizationBandColoringTests
{
    public static void BandedIrConcentratesEnergyInActiveBand()
    {
        const int sampleRate = 44100;
        // İki uç bandı test ediyoruz: düşük (63 Hz) ve yüksek (8 kHz).
        // Her testte spektral merkez beklenen bandın bir oktav çevresinde olmalı.
        AssertBandConcentration(bandIndex: 0, centerHz: 63,   sampleRate);
        AssertBandConcentration(bandIndex: 3, centerHz: 500,  sampleRate);
        AssertBandConcentration(bandIndex: 7, centerHz: 8000, sampleRate);
    }

    public static void DifferentBandsProduceDistinguishableIrs()
    {
        const int sampleRate = 44100;
        var lowIr  = BuildSingleBandIr(bandIndex: 0, sampleRate); // 63 Hz
        var highIr = BuildSingleBandIr(bandIndex: 7, sampleRate); // 8 kHz

        // Düşük frekans IR'si yüksekten belirgin biçimde daha uzun (envelope) olur:
        // 63 Hz bandpass ringing süresi ~Q/ω0 ≈ 1.41/(2π·63) ≈ 3.6 ms,
        // 8 kHz bandpass için ~28 µs. Energy-time decay analitik olarak çok farklı.
        // RMS centroid zamanı (Ts benzeri) kullanarak ayrıştırılabilirliği test ediyoruz.
        double tsLow  = EnergyCentroidSeconds(lowIr,  sampleRate);
        double tsHigh = EnergyCentroidSeconds(highIr, sampleRate);

        if (tsLow <= tsHigh)
            throw new Exception(
                $"Düşük bandın enerji merkezi (Ts={tsLow * 1000:F2} ms) yüksek bandın merkezinden " +
                $"(Ts={tsHigh * 1000:F2} ms) büyük olmalıydı — bantbazlı filtreleme yapılmıyor olabilir.");
    }

    // ─── Yardımcılar ──────────────────────────────────────────────────────────

    private static void AssertBandConcentration(int bandIndex, double centerHz, int sampleRate)
    {
        var ir = BuildSingleBandIr(bandIndex, sampleRate);

        // FFT magnitude spektrumunu üret
        int fftSize = 1;
        while (fftSize < ir.Length) fftSize <<= 1;
        var padded = new double[fftSize];
        for (int i = 0; i < ir.Length; i++) padded[i] = ir[i];
        var spec = FftSharp.FFT.Forward(padded);

        // Tepe enerjili bin'i bul (DC hariç, Nyquist üstüne kadar)
        int half = fftSize / 2;
        double peakE = 0;
        int peakBin = 1;
        double totalE = 0;
        for (int k = 1; k < half; k++)
        {
            double mag = spec[k].Magnitude;
            double e = mag * mag;
            totalE += e;
            if (e > peakE)
            {
                peakE = e;
                peakBin = k;
            }
        }

        if (totalE <= 0)
            throw new Exception($"Bant {bandIndex} ({centerHz} Hz) IR'sinin enerjisi sıfır — filtreleme tamamen söndürmüş.");

        double peakFreq = (double)peakBin * sampleRate / fftSize;

        // Tepe frekansı bandın yarım-oktav çevresinde (cf/√2, cf·√2) olmalı
        const double sqrt2 = 1.41421356237;
        if (peakFreq < centerHz / sqrt2 || peakFreq > centerHz * sqrt2)
            throw new Exception(
                $"Bant {bandIndex}: beklenen merkez ≈ {centerHz} Hz, ölçülen tepe frekansı {peakFreq:F0} Hz " +
                $"(yarım-oktav toleransı dışında). Bantbazlı renklendirme kırılmış.");

        // Bant içi/dışı enerji oranı: oktav-genişliği (cf/√2..cf·√2) aralığında toplam enerjinin
        // en az %30'u bulunmalı — RBJ Q=√2 bandpass için bu eşik konservatiftir.
        double f1 = centerHz / sqrt2;
        double f2 = centerHz * sqrt2;
        double inBand = 0;
        for (int k = 1; k < half; k++)
        {
            double freq = (double)k * sampleRate / fftSize;
            if (freq >= f1 && freq <= f2)
            {
                double mag = spec[k].Magnitude;
                inBand += mag * mag;
            }
        }
        double inBandFrac = inBand / totalE;
        if (inBandFrac < 0.30)
            throw new Exception(
                $"Bant {bandIndex} ({centerHz} Hz): oktav aralığında enerjinin sadece %{inBandFrac * 100:F0}'i kalıyor " +
                $"(en az %30 beklendi). Filtre yeterince selektif değil.");
    }

    private static float[] BuildSingleBandIr(int bandIndex, int sampleRate)
    {
        // 100 ms civarında 1 adet arrival, sadece istenen bant E=1.0
        double t = 0.05; // 50 ms
        double[] e = new double[8];
        e[bandIndex] = 1.0;
        var arrival = new EchogramArrival(t,
            e[0], e[1], e[2], e[3], e[4], e[5], e[6], e[7],
            1, 0, 0);

        var echo = new EchogramData(1, 2, "S", "R");
        echo.GetType()
            .GetMethod("AddArrival",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(echo, new object[] { arrival });

        var rng = new Random(1234);
        var method = typeof(AuralizationService).GetMethod("BuildBandedIr",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (float[])method.Invoke(null, new object[] { echo, sampleRate, rng })!;
    }

    private static double EnergyCentroidSeconds(float[] ir, int sampleRate)
    {
        // Geliş anından sonraki kuyruğun enerji ağırlıklı merkezi
        double sumE = 0, sumTE = 0;
        for (int n = 0; n < ir.Length; n++)
        {
            double e = ir[n] * (double)ir[n];
            sumE  += e;
            sumTE += n * e;
        }
        if (sumE <= 0) return 0;
        return (sumTE / sumE) / sampleRate;
    }
}
