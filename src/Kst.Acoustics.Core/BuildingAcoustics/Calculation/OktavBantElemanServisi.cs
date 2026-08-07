using System;
using Kst.Acoustics.Core.BuildingAcoustics.Models;

namespace Kst.Acoustics.Core.BuildingAcoustics.Calculation;

/// Yapı elemanı için 7-bantlı (63–4000 Hz) R(f) ve Ln(f) dizilerini üretir.
/// Gerçek veri varsa doğrudan döner; yoksa fizik modeliyle tahmin eder.
public static class OktavBantElemanServisi
{
    // 63, 125, 250, 500, 1000, 2000, 4000 Hz
    public static readonly int[] Freqler = [63, 125, 250, 500, 1000, 2000, 4000];
    public static readonly int[] UcuncuOktavFreqler =
        [100, 125, 160, 200, 250, 315, 400, 500, 630, 800, 1000, 1250, 1600, 2000, 2500, 3150];

    /// Eski HasRealOktavData bayrağını yeni köken alanına kayıpsız biçimde bağlar.
    /// Belirsiz+dolu legacy veri, ölçüm olduğuna dair kanıt taşımadığından model kabul edilir.
    public static SpektrumVeriKaynagi EtkinSpektrumKaynagi(YapiElemani e) =>
        e.SpektrumKaynagi != SpektrumVeriKaynagi.Belirsiz
            ? e.SpektrumKaynagi
            : e.HasRealOktavData
                ? SpektrumVeriKaynagi.Olcum
                : SpektrumVeriKaynagi.Model;

    public static SpektrumVeriKaynagi EtkinDarbeSpektrumKaynagi(YapiElemani e) =>
        e.DarbeSpektrumKaynagi != SpektrumVeriKaynagi.Belirsiz
            ? e.DarbeSpektrumKaynagi
            : EtkinSpektrumKaynagi(e);

    public static bool TamUcuncuOktavHavaVerisiVar(YapiElemani e) =>
        e.RThirdOctave.Count >= UcuncuOktavFreqler.Length &&
        e.RThirdOctave.Take(UcuncuOktavFreqler.Length).All(GecerliBant);

    public static bool TamOktavHavaVerisiVar(YapiElemani e) =>
        GecerliBant(e.R63) && GecerliBant(e.R125) && GecerliBant(e.R250) &&
        GecerliBant(e.R500) && GecerliBant(e.R1k) && GecerliBant(e.R2k) &&
        GecerliBant(e.R4k);

    public static bool TamOktavDarbeVerisiVar(YapiElemani e) =>
        GecerliBant(e.L63) && GecerliBant(e.L125) && GecerliBant(e.L250) &&
        GecerliBant(e.L500) && GecerliBant(e.L1k) && GecerliBant(e.L2k) &&
        GecerliBant(e.L4k);

    public static bool TamHavaSpektrumuVar(YapiElemani e) =>
        TamUcuncuOktavHavaVerisiVar(e) || TamOktavHavaVerisiVar(e);

    /// Sonuç ve raporların ölçüm/üretici/model/varsayılan ayrımını koruyan etiketi.
    /// Verinin bantları eksikse beyan edilen köken ne olursa olsun model yolu kullanılır.
    public static string HavaSpektrumKaynagiEtiketi(YapiElemani e) =>
        KaynakEtiketi(TamHavaSpektrumuVar(e) ? EtkinSpektrumKaynagi(e) : SpektrumVeriKaynagi.Model);

    public static string DarbeSpektrumKaynagiEtiketi(YapiElemani e) =>
        KaynakEtiketi(TamOktavDarbeVerisiVar(e) ? EtkinDarbeSpektrumKaynagi(e) : SpektrumVeriKaynagi.Model);

    /// Hava sesi yalıtımı R(f) dizisi (7 bant). HasRealOktavData ise ölçüm verisi; aksi halde model.
    /// Sonuçta ApplyLining çağrılır: DeltaRThirdOctave uzunluk eşleşirse per-band, aksi halde uniform DeltaRDb.
    public static double[] GetRDizisi(YapiElemani e) => ApplyLining(GetRDizisiRaw(e), e);

    /// Hava sesi yalıtımı R(f) dizisi (16 üçüncü-oktav bant, 100–3150 Hz).
    /// Gerçek üçüncü-oktav veri varsa doğrudan döner; yoksa fizik modeli veya 1/1 oktav interpolasyonu kullanır.
    public static double[] GetRUcuncuOktavDizisi(YapiElemani e) => ApplyLining(GetRUcuncuOktavDizisiRaw(e), e);

    private static double[] GetRDizisiRaw(YapiElemani e)
    {
        if (TamOktavHavaVerisiVar(e))
            return [e.R63, e.R125, e.R250, e.R500, e.R1k, e.R2k, e.R4k];

        var model = e.Tip switch
        {
            YapiElemaniTipi.Pencere or YapiElemaniTipi.Kapi => ModelTekCidar(e),
            _ => e.KutleselRezonansHz.HasValue ? ModelCiftCidar(e) : ModelTekCidar(e)
        };
        // Fix #3 — Sentetik eğriyi, ISO 717 derecesi elemanın nominal Rw'sine eşit olacak şekilde kalibre et.
        return KalibreEtRw(model, e.Rw, Iso717Rw7);
    }

    private static double[] GetRUcuncuOktavDizisiRaw(YapiElemani e)
    {
        if (TamUcuncuOktavHavaVerisiVar(e))
            return e.RThirdOctave.Take(16).ToArray();

        if (TamOktavHavaVerisiVar(e))
            return InterpolateOktavToThird(GetRDizisiRaw(e));

        var model = e.KutleselRezonansHz.HasValue
            ? ModelCiftCidar(e, UcuncuOktavFreqler)
            : ModelTekCidar(e, UcuncuOktavFreqler);
        // Fix #3 — Sentetik 1/3-oktav eğriyi nominal Rw'ye kalibre et (yeniden derecelendirme = Rw).
        return KalibreEtRw(model, e.Rw, Iso717Rw16);
    }

    /// Fix #3 — Sentetik R(f) eğrisine sabit ofset ekleyerek ISO 717 tek-sayı derecesini
    /// elemanın nominal Rw'sine eşitler. ISO 717 kaydırma algoritması tüm bantlara eklenen
    /// tam-sayı ofsette dereceyi birebir kaydırdığından, tek adımlı ofset kesin sonuç verir.
    /// hedefRw ≤ 0 ise dokunulmaz.
    private static double[] KalibreEtRw(double[] r, double hedefRw, Func<double[], double> dereceFn)
    {
        if (hedefRw <= 0 || r.Length == 0) return r;
        double mevcut = dereceFn(r);
        if (mevcut <= 0) return r;
        double delta = hedefRw - mevcut;
        if (Math.Abs(delta) < 1e-9) return r;
        for (int i = 0; i < r.Length; i++) r[i] += delta;
        return r;
    }

    /// ISO 717-1 16-bant (100–3150 Hz) Rw derecesi (yalnız kalibrasyon için minimal sürüm).
    private static double Iso717Rw16(double[] r16)
    {
        if (r16.Length < 16) return 0;
        int[] reference = [33, 36, 39, 42, 45, 48, 51, 52, 53, 54, 55, 56, 56, 56, 56, 56];
        int bestShift = -100;
        for (int shift = -100; shift <= 100; shift++)
        {
            double unfav = 0;
            for (int i = 0; i < 16; i++) unfav += Math.Max(0, reference[i] + shift - r16[i]);
            if (unfav <= 32) bestShift = shift;
        }
        return reference[7] + bestShift;
    }

    /// ISO 717-1 7-bant (63–4000 Hz) oktav uyarlaması Rw derecesi (kalibrasyon için).
    private static double Iso717Rw7(double[] r7)
    {
        if (r7.Length < 7) return 0;
        int[] reference = [33, 36, 42, 52, 55, 56, 56];
        int bestShift = -100;
        for (int shift = -100; shift <= 100; shift++)
        {
            double unfav = 0;
            for (int i = 0; i < 7; i++) unfav += Math.Max(0, reference[i] + shift - r7[i]);
            if (unfav <= 14) bestShift = shift;
        }
        return reference[3] + bestShift;
    }

    /// Per-band ΔR(f) (DeltaRThirdOctave) uzunluğu eşleşirse her banta o değer.
    /// 7-bant (1/1 oktav) yolda 16-bant ΔR(f) varsa oktava indirgenip per-band uygulanır.
    /// Hiçbir per-band veri yoksa uniform DeltaRDb'ye düşer.
    private static double[] ApplyLining(double[] r, YapiElemani e)
    {
        if (e.DeltaRThirdOctave.Count == r.Length)
        {
            for (int i = 0; i < r.Length; i++) r[i] += e.DeltaRThirdOctave[i];
            return r;
        }
        if (r.Length == Freqler.Length && e.DeltaRThirdOctave.Count >= UcuncuOktavFreqler.Length)
        {
            var dr = CollapseThirdToOktav([.. e.DeltaRThirdOctave]);
            for (int i = 0; i < r.Length; i++) r[i] += dr[i];
            return r;
        }
        if (e.DeltaRDb == 0) return r;
        for (int i = 0; i < r.Length; i++) r[i] += e.DeltaRDb;
        return r;
    }

    /// 16-bant (1/3 oktav, 100–3150 Hz) seviye düzeltmesini 7 oktav banda (63–4000 Hz)
    /// indirir. ΔR bir dB ofseti olduğundan oktav içindeki 1/3 oktavların aritmetik
    /// ortalaması alınır. 63 Hz (1/3 oktav verisi yok) en yakın 100 Hz'i, 4000 Hz ise
    /// mevcut tek üye 3150 Hz'i kullanır.
    private static double[] CollapseThirdToOktav(double[] third)
    {
        // Her oktav bandının 16-bant dizisindeki üye indeksleri.
        int[][] gruplar =
        [
            [0],            // 63 Hz  → 100 Hz (en yakın)
            [0, 1, 2],      // 125 Hz → 100,125,160
            [3, 4, 5],      // 250 Hz → 200,250,315
            [6, 7, 8],      // 500 Hz → 400,500,630
            [9, 10, 11],    // 1000 Hz → 800,1000,1250
            [12, 13, 14],   // 2000 Hz → 1600,2000,2500
            [15],           // 4000 Hz → 3150 Hz (mevcut tek üye)
        ];
        var result = new double[Freqler.Length];
        for (int i = 0; i < gruplar.Length; i++)
        {
            double sum = 0;
            foreach (var idx in gruplar[i]) sum += third[idx];
            result[i] = sum / gruplar[i].Length;
        }
        return result;
    }

    /// Darbe sesi düzeyi Ln(f) dizisi (7 bant, yalnızca döşeme).
    public static double[] GetLnDizisi(YapiElemani e)
    {
        if (e.Tip != YapiElemaniTipi.Doseme || e.Lnw <= 0)
            return new double[7];

        if (TamOktavDarbeVerisiVar(e))
        {
            double[] raw = [e.L63, e.L125, e.L250, e.L500, e.L1k, e.L2k, e.L4k];
            // DeltaLw uygula (fr varsa frekans bağımlı, yoksa sabit)
            return e.DeltaLw > 0 ? UygulaYuzerDosemeDuzeltme(raw, e) : raw;
        }

        return ModelDarbeSesi(e);
    }

    // ─── Tek cidarlı model (kütle yasası + çakışma dipi) ────────────────────

    private static double[] ModelTekCidar(YapiElemani e)
        => ModelTekCidar(e, Freqler);

    private static double[] ModelTekCidar(YapiElemani e, IReadOnlyList<int> freqs)
    {
        double mPrime = e.ToplamYuzeyKutlesi > 0 ? e.ToplamYuzeyKutlesi : 200;
        double? fc = e.KritikFrekansHz;
        var result = new double[freqs.Count];

        for (int i = 0; i < freqs.Count; i++)
        {
            double f = freqs[i];
            // Kütle yasası: R(f) = Rw + 20·log10(f/500). DeltaR/per-band ApplyLining ile sonradan eklenir.
            double rMass = e.Rw + 20.0 * Math.Log10(f / 500.0);

            // Çakışma dipi: fc ±1 oktav bölgesinde -6 dB Gauss çukuru
            double coincidenceDip = 0;
            if (fc is { } fcVal && fcVal > 0)
            {
                double ratio = Math.Log2(f / fcVal);
                coincidenceDip = 6.0 * Math.Exp(-ratio * ratio / 0.5);
            }

            result[i] = rMass - coincidenceDip;
        }

        return result;
    }

    // ─── Çift cidarlı model (f0 dipi + arttırılmış eğim) ───────────────────

    private static double[] ModelCiftCidar(YapiElemani e)
        => ModelCiftCidar(e, Freqler);

    private static double[] ModelCiftCidar(YapiElemani e, IReadOnlyList<int> freqs)
    {
        double? f0 = e.KutleselRezonansHz;
        var result = new double[freqs.Count];

        for (int i = 0; i < freqs.Count; i++)
        {
            double f = freqs[i];
            // DeltaR/per-band ApplyLining ile sonradan eklenir.
            double rBase = e.Rw + 20.0 * Math.Log10(f / 500.0);

            // f0 altında her iki yaprak bağımsız → Rw ~ tek cidar
            // f0 dipi: ±1 oktav bölgesinde dip
            if (f0 is { } f0Val && f0Val > 0 && f < f0Val * 1.5)
            {
                double ratio = Math.Log2(f / f0Val);
                double dip = 10.0 * Math.Exp(-ratio * ratio / 0.8);
                rBase -= dip;
            }

            result[i] = rBase;
        }

        return result;
    }

    // ─── Darbe sesi modeli (ISO 717-2 referans eğrisine dayalı) ─────────────

    private static double[] ModelDarbeSesi(YapiElemani e)
    {
        // ISO 717-2 referans eğrisi 63–4000 Hz indeksleri (100–3150 Hz dizisinden seçim):
        // 63Hz≈+4, 125≈+3, 250≈+2, 500≈0, 1k≈-1, 2k≈-4, 4k≈-7 (yaklaşık Ln şekli)
        double[] offsets = [4, 3, 2, 0, -1, -4, -7];
        double lnBase = e.Lnw - e.DeltaLw;
        var result = new double[7];

        for (int i = 0; i < 7; i++)
            result[i] = lnBase + offsets[i];

        return result;
    }

    // ─── Yüzer döşeme düzeltmesi ─────────────────────────────────────────────

    private static double[] UygulaYuzerDosemeDuzeltme(double[] ln, YapiElemani e)
    {
        double? fr = e.YuzerDosemeRezonansHz;
        var result = new double[7];

        for (int i = 0; i < 7; i++)
        {
            double f = Freqler[i];
            double delta;

            if (fr is { } frVal && frVal > 0 && f > frVal)
                // fr üzerinde: 40 log(f/fr) eğimi ile azalan Ln (ISO 15643 modeli)
                delta = Math.Min(e.DeltaLw, 40.0 * Math.Log10(f / frVal));
            else
                // fr altında: azaltma yok (kötü yalıtım bölgesi)
                delta = 0;

            result[i] = ln[i] - delta;
        }

        return result;
    }

    // ─── Yardımcı ────────────────────────────────────────────────────────────

    private static bool GecerliBant(double deger) => double.IsFinite(deger) && deger > 0;

    private static string KaynakEtiketi(SpektrumVeriKaynagi kaynak) => kaynak switch
    {
        SpektrumVeriKaynagi.Olcum => "Olcum",
        SpektrumVeriKaynagi.Uretici => "Uretici",
        SpektrumVeriKaynagi.Varsayilan => "Varsayilan",
        _ => "ModelTahmin",
    };

    private static double[] InterpolateOktavToThird(double[] oktav)
    {
        var result = new double[UcuncuOktavFreqler.Length];
        for (int i = 0; i < result.Length; i++)
        {
            var f = UcuncuOktavFreqler[i];
            result[i] = InterpolateLogFrequency(Freqler, oktav, f);
        }
        return result;
    }

    private static double InterpolateLogFrequency(IReadOnlyList<int> freqs, IReadOnlyList<double> values, double f)
    {
        if (values.Count == 0) return 0;
        if (f <= freqs[0]) return values[0];
        if (f >= freqs[^1]) return values[Math.Min(values.Count - 1, freqs.Count - 1)];

        for (int i = 0; i < freqs.Count - 1 && i < values.Count - 1; i++)
        {
            if (f < freqs[i] || f > freqs[i + 1]) continue;
            var t = Math.Log(f / freqs[i]) / Math.Log((double)freqs[i + 1] / freqs[i]);
            return values[i] + (values[i + 1] - values[i]) * t;
        }
        return values[^1];
    }
}
