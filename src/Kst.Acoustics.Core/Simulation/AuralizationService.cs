using System.IO;
using System.Linq;

namespace Kst.Acoustics.Core.Simulation;

public static class AuralizationService
{
    /// <summary>
    /// Bantbazlı dürtü yanıtı (IR) sentezler: her geliş için 8 oktav banttaki enerji
    /// √E ile amplitude'a çevrilir, ayrı bantlı buffer'lara yazılır, her bant kendi
    /// oktav bandpass filtresinden geçirilir ve toplam IR elde edilir. Bu sayede
    /// materyal frekans-bağlı emilimi auralizasyona yansır.
    /// </summary>
    /// <param name="rng">Tüm bantlar için aynı sign'ı verecek paylaşımlı RNG (faz tutarlılığı için).</param>
    internal static float[] BuildBandedIr(EchogramData echogram, int sampleRate, Random rng)
    {
        var arrivals = echogram.Arrivals;
        if (arrivals.Count == 0) return Array.Empty<float>();

        double maxDelay = arrivals.Max(a => a.TimeSeconds);
        // +0.2 sn süzgeç ringing kuyruğu için minik tampon
        int irSamples = Math.Max(1, (int)((maxDelay + 0.2) * sampleRate));

        const int bandCount = 8; // 63..8k
        var perBand = new float[bandCount][];
        for (int b = 0; b < bandCount; b++)
            perBand[b] = new float[irSamples];

        foreach (var a in arrivals)
        {
            int tIdx = (int)(a.TimeSeconds * sampleRate);
            if ((uint)tIdx >= (uint)irSamples) continue;

            // Tüm bantlarda aynı sign — geliş tek bir akustik olay, faz tutarlı kalmalı
            float sign = rng.NextDouble() > 0.5 ? 1.0f : -1.0f;

            // Her bantta √E amplitude impulse koy
            perBand[0][tIdx] += sign * (float)Math.Sqrt(Math.Max(0, a.E63));
            perBand[1][tIdx] += sign * (float)Math.Sqrt(Math.Max(0, a.E125));
            perBand[2][tIdx] += sign * (float)Math.Sqrt(Math.Max(0, a.E250));
            perBand[3][tIdx] += sign * (float)Math.Sqrt(Math.Max(0, a.E500));
            perBand[4][tIdx] += sign * (float)Math.Sqrt(Math.Max(0, a.E1k));
            perBand[5][tIdx] += sign * (float)Math.Sqrt(Math.Max(0, a.E2k));
            perBand[6][tIdx] += sign * (float)Math.Sqrt(Math.Max(0, a.E4k));
            perBand[7][tIdx] += sign * (float)Math.Sqrt(Math.Max(0, a.E8k));
        }

        // Her bandı kendi oktav bandpass filtresinden zero-phase (filtfilt) geçir:
        // bantlar arası grup gecikmesi dispersion'ı yok olur, faz tutarlı kalır.
        for (int b = 0; b < bandCount; b++)
        {
            var filter = new OctaveBandFilter(OctaveBandFilter.CenterFrequencies[b], sampleRate);
            filter.ProcessZeroPhaseInPlace(perBand[b]);
        }

        // Bant IR'lerini topla
        var ir = new float[irSamples];
        for (int b = 0; b < bandCount; b++)
        {
            var band = perBand[b];
            for (int n = 0; n < irSamples; n++)
                ir[n] += band[n];
        }

        return ir;
    }

    /// <summary>
    /// Simülasyon sonucundaki ekogramı kullanarak Odanın Dürtü Yanıtı (Impulse Response - RIR) WAV dosyasını oluşturur.
    /// </summary>
    public static void GenerateImpulseResponseWav(EchogramData echogram, string outputPath, int sampleRate = 44100)
    {
        var arrivals = echogram.Arrivals;
        if (arrivals.Count == 0) return;

        var rng = new Random(42);
        var ir = BuildBandedIr(echogram, sampleRate, rng);
        if (ir.Length == 0) return;

        // +0.5 saniye sessizlik son yankıdan sonra
        int totalSamples = ir.Length + (int)(0.5 * sampleRate);
        double[] buffer = new double[totalSamples];
        for (int i = 0; i < ir.Length; i++) buffer[i] = ir[i];

        // Normalize işlemi (clipping'i önlemek için)
        double maxAmp = 0;
        for (int i = 0; i < totalSamples; i++)
        {
            if (Math.Abs(buffer[i]) > maxAmp) maxAmp = Math.Abs(buffer[i]);
        }

        if (maxAmp > 0)
        {
            for (int i = 0; i < totalSamples; i++)
            {
                buffer[i] = (buffer[i] / maxAmp) * 0.95; // %95 seviyesine normalize et
            }
        }

        WriteWavFile(outputPath, buffer, sampleRate);
    }

    /// <summary>
    /// Bir kaynak ses dosyasını (WAV) oda ekogramı ile konvolüsyona sokarak auralizasyon yapar.
    /// Daha doğal bir ses için önce dürtü yanıtını (IR) sentezler, sonra konvolüsyon yapar.
    /// </summary>
    public static void ConvolveSourceWithEchogram(string sourceWavPath, EchogramData echogram, string outputPath)
    {
        var arrivals = echogram.Arrivals;
        if (arrivals.Count == 0)
            throw new InvalidOperationException("Bu kaynak-alıcı çiftinde hiç geliş yok. Simülasyonu çalıştırın veya alıcı yarıçapını/ışın sayısını artırın.");

        // 1. Kaynak dosyayı oku
        var (sourceBuffer, sampleRate) = ReadWavFile(sourceWavPath);
        if (sourceBuffer == null || sourceBuffer.Length == 0)
            throw new InvalidOperationException($"WAV dosyası okunamadı veya boş: {sourceWavPath}");

        // Performans için kaynak sesi ilk 10 saniye ile sınırlayalım
        int maxSourceSamples = 10 * sampleRate;
        if (sourceBuffer.Length > maxSourceSamples)
        {
            var truncated = new float[maxSourceSamples];
            Array.Copy(sourceBuffer, truncated, maxSourceSamples);
            sourceBuffer = truncated;
        }

        // 2. Bantbazlı (8 oktav) IR sentezle — materyalin frekans-bağlı emilimi auralizasyona yansır
        var rng = new Random(42);
        var irBuffer = BuildBandedIr(echogram, sampleRate, rng);
        int irSamples = irBuffer.Length;
        if (irSamples == 0)
            throw new InvalidOperationException("Dürtü yanıtı sentezlenemedi (geçerli geliş yok).");

        // 3. Konvolüsyon (FFT Etki Alanında - Frequency Domain Convolution)
        // O(N*logN) karmaşıklığı ile zaman etki alanındaki O(N*M)'den çok daha hızlıdır.
        int totalSamples = sourceBuffer.Length + irSamples - 1;
        
        // FFT için en yakın 2'nin kuvveti uzunluğunu bulalım (Zero-padding için)
        int fftSize = 1;
        while (fftSize < totalSamples) fftSize <<= 1;

        double[] sourcePadded = new double[fftSize];
        double[] irPadded     = new double[fftSize];
        
        for (int i = 0; i < sourceBuffer.Length; i++) sourcePadded[i] = sourceBuffer[i];
        for (int i = 0; i < irSamples; i++)           irPadded[i]     = irBuffer[i];

        // FFT uygula
        System.Numerics.Complex[] sourceFft = FftSharp.FFT.Forward(sourcePadded);
        System.Numerics.Complex[] irFft     = FftSharp.FFT.Forward(irPadded);

        // Frekans düzleminde çarpma (Konvolüsyon teoremi)
        System.Numerics.Complex[] resultFft = new System.Numerics.Complex[fftSize];
        for (int i = 0; i < fftSize; i++)
        {
            resultFft[i] = sourceFft[i] * irFft[i];
        }

        // Ters FFT ile zaman düzlemine dön.
        // FftSharp.FFT.InverseReal N/2+1 uzunlukta spektrum bekler (rfft); biz tam N uzunlukta
        // komplex spektrum üretiyoruz. Inverse() in-place çalışır; resultFft'i ters dönüştürür.
        FftSharp.FFT.Inverse(resultFft);

        float[] outputBuffer = new float[totalSamples];
        for (int i = 0; i < totalSamples; i++)
        {
            outputBuffer[i] = (float)resultFft[i].Real;
        }

        // 4. RMS-tabanlı normalize (peak guard'lı):
        // Salt-peak normalize, başlangıçtaki tek bir sivri darbe yüzünden tüm sesi sönükleştirir.
        // RMS hedefiyle ses seviyesini eşitler, sonra peak 0.95'i aşıyorsa geri ölçeklenir.
        float maxAmp = 0;
        double sumSq = 0;
        foreach (var val in outputBuffer)
        {
            var a = Math.Abs(val);
            if (a > maxAmp) maxAmp = a;
            sumSq += val * val;
        }

        if (maxAmp > 0)
        {
            double rms = Math.Sqrt(sumSq / totalSamples);
            const double targetRms = 0.18; // ≈ -15 dBFS, tipik müzik seviyesi
            double scale = rms > 0 ? targetRms / rms : 1.0 / maxAmp;
            // Peak guard
            double scaledPeak = maxAmp * scale;
            if (scaledPeak > 0.95) scale *= 0.95 / scaledPeak;
            for (int i = 0; i < totalSamples; i++)
                outputBuffer[i] = (float)(outputBuffer[i] * scale);
        }

        WriteWavFileFromFloat(outputPath, outputBuffer, sampleRate);
    }

    private static (float[]? Data, int SampleRate) ReadWavFile(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            string chunkId = new string(br.ReadChars(4));
            if (chunkId != "RIFF") return (null, 0);
            br.ReadInt32(); // chunkSize
            string format = new string(br.ReadChars(4));
            if (format != "WAVE") return (null, 0);

            int sampleRate = 44100;
            short numChannels = 1;
            short bitsPerSample = 16;
            float[]? data = null;

            while (fs.Position < fs.Length - 8)
            {
                string subChunkId = new string(br.ReadChars(4));
                int subChunkSize = br.ReadInt32();

                if (subChunkId == "fmt ")
                {
                    short audioFormat = br.ReadInt16();
                    numChannels = br.ReadInt16();
                    sampleRate = br.ReadInt32();
                    br.ReadInt32(); // byteRate
                    br.ReadInt16(); // blockAlign
                    bitsPerSample = br.ReadInt16();
                    if (subChunkSize > 16) br.ReadBytes(subChunkSize - 16);

                    // Sadece PCM (1) destekliyoruz
                    if (audioFormat != 1) return (null, 0);
                }
                else if (subChunkId == "data")
                {
                    int bytesPerSample = bitsPerSample / 8;
                    int totalSamplesInData = subChunkSize / (bytesPerSample * numChannels);
                    data = new float[totalSamplesInData];

                    for (int i = 0; i < totalSamplesInData; i++)
                    {
                        float sum = 0;
                        for (int c = 0; c < numChannels; c++)
                        {
                            if (bitsPerSample == 16)
                                sum += br.ReadInt16() / 32768.0f;
                            else if (bitsPerSample == 8)
                                sum += (br.ReadByte() - 128) / 128.0f;
                            else if (bitsPerSample == 24)
                            {
                                byte b1 = br.ReadByte();
                                byte b2 = br.ReadByte();
                                byte b3 = br.ReadByte();
                                int val = (b3 << 16) | (b2 << 8) | b1;
                                if ((val & 0x800000) != 0) val |= unchecked((int)0xFF000000);
                                sum += val / 8388608.0f;
                            }
                            else
                            {
                                // Desteklenmeyen bit derinliği, atla
                                br.ReadBytes(bytesPerSample);
                            }
                        }
                        data[i] = sum / numChannels; // Mono'ya indirge
                    }
                    break;
                }
                else
                {
                    br.ReadBytes(subChunkSize);
                }
            }

            return (data, sampleRate);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WAV Parse Error: {ex.Message}");
            return (null, 0);
        }
    }

    private static void WriteWavFileFromFloat(string path, float[] buffer, int sampleRate)
    {
        double[] doubleBuffer = new double[buffer.Length];
        for (int i = 0; i < buffer.Length; i++) doubleBuffer[i] = buffer[i];
        WriteWavFile(path, doubleBuffer, sampleRate);
    }

    private static void WriteWavFile(string path, double[] buffer, int sampleRate)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        int numSamples = buffer.Length;
        int numChannels = 1;
        int bitsPerSample = 16;
        int byteRate = sampleRate * numChannels * bitsPerSample / 8;
        int blockAlign = numChannels * bitsPerSample / 8;
        int dataSize = numSamples * numChannels * bitsPerSample / 8;
        int chunkSize = 36 + dataSize;

        // RIFF header
        bw.Write("RIFF".ToCharArray());
        bw.Write(chunkSize);
        bw.Write("WAVE".ToCharArray());

        // fmt subchunk
        bw.Write("fmt ".ToCharArray());
        bw.Write(16); // Subchunk1Size (16 for PCM)
        bw.Write((short)1); // AudioFormat (1 for PCM)
        bw.Write((short)numChannels);
        bw.Write(sampleRate);
        bw.Write(byteRate);
        bw.Write((short)blockAlign);
        bw.Write((short)bitsPerSample);

        // data subchunk
        bw.Write("data".ToCharArray());
        bw.Write(dataSize);

        // Data
        foreach (var sample in buffer)
        {
            // Convert float [-1.0, 1.0] to short [-32768, 32767]
            short s = (short)Math.Clamp(Math.Round(sample * 32767.0), short.MinValue, short.MaxValue);
            bw.Write(s);
        }
    }
}
