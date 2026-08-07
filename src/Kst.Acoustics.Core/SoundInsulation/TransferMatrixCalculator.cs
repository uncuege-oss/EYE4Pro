using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Kst.Acoustics.Core.SoundInsulation;

/// <summary>
/// Transfer Matrix Method (Allard-Atalla) tabanlı TL hesabı.
/// - Masif/levha katmanlar için ince plak (thin-plate) plate-impedance boundary.
/// - Hava katmanı için klasik fluid katmanı transfer matrisi.
/// - Poröz katman için Johnson-Champoux-Allard (5 parametre) effective fluid model.
/// - Field-incidence ortalaması: 0–78° trapez integrali (Paris formülü).
/// </summary>
public static class TransferMatrixCalculator
{
    private const double C0 = 343.0;          // m/s, hava ses hızı
    private const double Rho0 = 1.213;        // kg/m³, hava yoğunluğu
    private const double Eta = 1.84e-5;       // Pa·s, hava dinamik viskozitesi
    private const double Gamma = 1.4;         // hava adyabatik
    private const double Pr = 0.71;           // Prandtl sayısı
    private const double P0 = 1.01325e5;      // Pa, atmosferik
    private const double MaxAngleDeg = 78.0;  // limited field-incidence
    private const int AngleSamples = 19;      // trapez örnekleme

    public static Dictionary<int, double> Calculate(WallAssembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return Calculate(assembly, SoundInsulationConstants.ThirdOctaveBands);
    }

    public static Dictionary<int, double> Calculate(WallAssembly assembly, IEnumerable<int> frequencies)
    {
        var result = new Dictionary<int, double>();
        var layers = assembly.Layers;
        if (layers.Count == 0)
        {
            foreach (var f in frequencies) result[f] = 0;
            return result;
        }

        foreach (var freq in frequencies)
        {
            var f = (double)freq;
            var tauField = FieldIncidenceTau(layers, f);
            var tl = -10.0 * Math.Log10(Math.Max(tauField, 1e-12));
            result[freq] = Math.Max(0, tl);
        }
        return result;
    }

    private static double FieldIncidenceTau(IReadOnlyList<SoundInsulationLayer> layers, double f)
    {
        double maxTheta = MaxAngleDeg * Math.PI / 180.0;
        double dTheta = maxTheta / (AngleSamples - 1);

        double sumNum = 0;
        double sumDen = 0;
        for (int i = 0; i < AngleSamples; i++)
        {
            double theta = i * dTheta;
            double w = Math.Sin(theta) * Math.Cos(theta);
            double tau = TransmissionAtAngle(layers, f, theta);
            // trapez ağırlığı
            double weight = (i == 0 || i == AngleSamples - 1) ? 0.5 : 1.0;
            sumNum += weight * tau * w;
            sumDen += weight * w;
        }
        return sumDen > 0 ? sumNum / sumDen : 0;
    }

    private static double TransmissionAtAngle(IReadOnlyList<SoundInsulationLayer> layers, double f, double theta)
    {
        // Yarısonsuz havada karakteristik empedans (oblique)
        double cosTheta = Math.Cos(theta);
        if (cosTheta < 1e-6) cosTheta = 1e-6;
        var Z1 = new Complex(Rho0 * C0 / cosTheta, 0);

        // Zincir T = T_1 · T_2 · ... · T_n  (identity ile başla)
        var t11 = Complex.One;
        var t12 = Complex.Zero;
        var t21 = Complex.Zero;
        var t22 = Complex.One;

        foreach (var layer in layers)
        {
            var (l11, l12, l21, l22) = BuildLayerMatrix(layer, f, theta);
            var n11 = t11 * l11 + t12 * l21;
            var n12 = t11 * l12 + t12 * l22;
            var n21 = t21 * l11 + t22 * l21;
            var n22 = t21 * l12 + t22 * l22;
            t11 = n11; t12 = n12; t21 = n21; t22 = n22;
        }

        // Allard-Atalla §11.4 — sandviçin her iki ucu yarısonsuz havayla sınırlı:
        // τ(θ) = |2 · Z1 / (t11·Z1 + t12 + t21·Z1² + t22·Z1)|²
        var denom = t11 * Z1 + t12 + t21 * Z1 * Z1 + t22 * Z1;
        var num = 2.0 * Z1;
        if (denom.Magnitude < 1e-30) return 0;
        var pRatio = num / denom;
        return pRatio.Magnitude * pRatio.Magnitude;
    }

    private static (Complex T11, Complex T12, Complex T21, Complex T22) BuildLayerMatrix(
        SoundInsulationLayer layer, double f, double theta)
    {
        var mat = layer.Material;
        double omega = 2.0 * Math.PI * f;
        double cosTheta = Math.Cos(theta);

        // Hava boşluğu (poröz dolgu yoksa)
        if (layer.IsAirGap && !layer.HasPorousFill)
        {
            return AirLayerMatrix(layer.Thickness, omega, theta);
        }

        // Poröz dolgu (JCA)
        if (layer.HasPorousFill || (layer.IsAirGap && mat.FlowResistivity > 0))
        {
            return JcaPorousLayerMatrix(mat, layer.Thickness, omega, theta);
        }

        // Masif/levha katman:
        if (mat.Density > 0 && layer.Thickness > 0)
        {
            // Elastik veri varsa tam katı tabaka (P+S dalga) modeli; yoksa ince plak/kütle limitine düş.
            if (mat.YoungModulus > 0)
                return SolidLayerMatrix(mat, layer.Thickness, omega, theta);
            return PlateLayerMatrix(mat, layer.Thickness, f, theta);
        }

        // Fallback: hava
        return AirLayerMatrix(layer.Thickness, omega, theta);
    }

    /// <summary>İnce plak (Cremer) için boundary impedance: T = [[1, Zp], [0, 1]].</summary>
    private static (Complex, Complex, Complex, Complex) PlateLayerMatrix(
        SoundInsulationMaterial mat, double t, double f, double theta)
    {
        double surfaceMass = mat.Density * t;
        double fc = mat.EstimateCriticalFrequency(t);
        double eta = Math.Clamp(mat.LossFactor, 1e-4, 0.5);
        double sin2 = Math.Sin(theta) * Math.Sin(theta);
        double sin4 = sin2 * sin2;

        // Z_plate(ω, θ) = jωm · [1 - (f/fc)²·sin⁴(θ)] + jωm · η · (f/fc)²·sin⁴(θ)
        // (Cremer & Heckl: plate impedance, sönümlü)
        double ratio = fc > 0 ? (f / fc) * (f / fc) * sin4 : 0;
        var jwm = new Complex(0, 2.0 * Math.PI * f * surfaceMass);
        var Zp = jwm * new Complex(1.0 - ratio, eta * ratio);

        return (Complex.One, Zp, Complex.Zero, Complex.One);
    }

    /// <summary>Hava katmanı transfer matrisi (oblique).</summary>
    private static (Complex, Complex, Complex, Complex) AirLayerMatrix(
        double d, double omega, double theta)
    {
        double cosTheta = Math.Cos(theta);
        double k0 = omega / C0;
        // Eğik geçiş: kx = k·sin(θ), kz = k·cos(θ); fluid layer:
        // T = [[cos(kz·d), j·Z·sin(kz·d)/cos(θ)], [j·cos(θ)·sin(kz·d)/Z, cos(kz·d)]]
        double kzd = k0 * cosTheta * d;
        double Z = Rho0 * C0;
        var cosTerm = new Complex(Math.Cos(kzd), 0);
        var jSin = new Complex(0, Math.Sin(kzd));
        return (
            cosTerm,
            jSin * (Z / cosTheta),
            jSin * (cosTheta / Z),
            cosTerm);
    }

    /// <summary>Johnson-Champoux-Allard etkin akışkan modeli + transfer matris.</summary>
    private static (Complex, Complex, Complex, Complex) JcaPorousLayerMatrix(
        SoundInsulationMaterial mat, double d, double omega, double theta)
    {
        double sigma = Math.Max(mat.FlowResistivity, 1000.0);
        double phi   = Math.Clamp(mat.Porosity, 0.01, 1.0);
        double alpha = Math.Max(mat.Tortuosity, 1.0);
        double Lambda = mat.GetViscousLengthMeters();
        double LambdaP = mat.GetThermalLengthMeters();
        if (Lambda <= 0)
        {
            // sigma/phi'den son çare türetim
            Lambda = Math.Sqrt(8.0 * alpha * Eta / (sigma * phi));
            LambdaP = 2.0 * Lambda;
        }

        // ρ_e(ω) — JCA effective density
        var jw = new Complex(0, omega);
        var inner1 = Complex.Sqrt(Complex.One + (4.0 * alpha * alpha * Eta * Rho0 * omega) /
                                                 (sigma * sigma * Lambda * Lambda * phi * phi)
                                                 * Complex.ImaginaryOne);
        var rhoE = (Rho0 * alpha / phi) * (Complex.One + (sigma * phi / (jw * Rho0 * alpha)) * inner1);

        // K_e(ω) — JCA effective bulk modulus
        var jwPr = new Complex(0, omega * Pr);
        var inner2 = Complex.Sqrt(Complex.One + (jwPr * Rho0 * LambdaP * LambdaP) / (16.0 * Eta));
        var denomK = Gamma - (Gamma - 1.0) /
                     (Complex.One + (8.0 * Eta / (jwPr * LambdaP * LambdaP * Rho0)) * inner2);
        var Ke = (Gamma * P0 / phi) / denomK;

        // k_p ve Z_p. rhoE ve Ke zaten makroskopik efektif büyüklükler (her ikisi de /φ içerir),
        // dolayısıyla karakteristik empedans Z_c = √(ρ_eff·K_eff) makroskopik formdadır ve
        // porozite çarpanını zaten içerir. ESKİ HATA: ayrıca /φ → 1/φ² (Allard & Atalla
        // konvansiyonuna göre fazladan bir 1/φ). φ≈1'de ihmal edilebilir, düşük porozitede önemli.
        var kp = omega * Complex.Sqrt(rhoE / Ke);
        var Zp = Complex.Sqrt(rhoE * Ke);

        // Eğik geçiş için kx = k0·sin(θ); kz_p = sqrt(kp² - kx²)
        double k0 = omega / C0;
        double kx = k0 * Math.Sin(theta);
        var kzp = Complex.Sqrt(kp * kp - kx * kx);
        var kzpd = kzp * d;

        var cosTerm = Complex.Cos(kzpd);
        var sinTerm = Complex.Sin(kzpd);
        var ZpEff = Zp * kp / kzp; // effective acoustic impedance for oblique

        return (
            cosTerm,
            Complex.ImaginaryOne * sinTerm * ZpEff,
            Complex.ImaginaryOne * sinTerm / ZpEff,
            cosTerm);
    }

    /// <summary>
    /// Elastik katı tabaka (Allard &amp; Atalla 2009, §11.3) — boyuna (P) + kayma (S) dalgalı
    /// tam 4×4 transfer matrisi, her iki yüzde σ₁₃=0 (akışkan/poröz sınır) koşuluyla 2×2 [p,v]
    /// forma yoğunlaştırılmış. İnce plak (Cremer) limitine ve düşük frekansta kütle kanununa
    /// indirgenir; kalın betonarme ve elastomerik katmanlarda kayma deformasyonu, dönme ataleti
    /// ve kalınlık (thickness) rezonanslarını yakalar.
    /// Durum vektörü: V = [v₁, v₃, σ₃₃, σ₁₃]; transfer ilişkisi V(0) = T·V(d).
    /// </summary>
    private static (Complex, Complex, Complex, Complex) SolidLayerMatrix(
        SoundInsulationMaterial mat, double d, double omega, double theta)
    {
        double rho = mat.Density;
        double nu = Math.Clamp(mat.PoissonRatio, 0.0, 0.49);
        double eta = Math.Clamp(mat.LossFactor, 0.0, 0.5);

        // Yapısal sönümlü kompleks elastik modüller (E → E(1+iη))
        var E = new Complex(mat.YoungModulus, mat.YoungModulus * eta);
        var mu = E / (2.0 * (1.0 + nu));
        var lambda = E * nu / ((1.0 + nu) * (1.0 - 2.0 * nu));

        // İz dalga sayısı (reel) ve düşey dalga sayıları (P, S)
        double k0 = omega / C0;
        double s = k0 * Math.Sin(theta);
        var delta1Sq = (omega * omega * rho) / (lambda + 2.0 * mu); // δ₁² (P-dalgası)
        var delta3Sq = (omega * omega * rho) / mu;                  // δ₃² (S-dalgası)
        var l = Complex.Sqrt(delta1Sq - s * s);
        var m = Complex.Sqrt(delta3Sq - s * s);

        // Γ(0) katsayı sabitleri
        var c33L = 2.0 * mu * (s * s) - (omega * omega * rho); // σ₃₃ ↔ P
        var twoMuS = 2.0 * mu * s;                             // shear coupling
        var c13S = mu * (delta3Sq - 2.0 * (s * s));            // σ₁₃ ↔ S

        // Γ(0): sütunlar [A₁(P↓), A₂(P↑), A₃(S↓), A₄(S↑)], satırlar [v₁, v₃, σ₃₃, σ₁₃]
        var g0 = new Complex[4, 4]
        {
            { omega * s,  omega * s,  -omega * m, omega * m },
            { omega * l, -omega * l,   omega * s, omega * s },
            { c33L,       c33L,       -twoMuS * m, twoMuS * m },
            { -twoMuS * l, twoMuS * l, c13S,       c13S },
        };

        // Γ(d): her sütunu kendi üs terimiyle ölçekle (e^{∓jld}, e^{∓jmd})
        var j = Complex.ImaginaryOne;
        var eLm = Complex.Exp(-j * l * d);
        var eLp = Complex.Exp(j * l * d);
        var eMm = Complex.Exp(-j * m * d);
        var eMp = Complex.Exp(j * m * d);
        var gd = new Complex[4, 4];
        for (int r = 0; r < 4; r++)
        {
            gd[r, 0] = g0[r, 0] * eLm;
            gd[r, 1] = g0[r, 1] * eLp;
            gd[r, 2] = g0[r, 2] * eMm;
            gd[r, 3] = g0[r, 3] * eMp;
        }

        // T = Γ(0)·Γ(d)⁻¹  (basis seçimi/şube işaretinden bağımsız fiziksel transfer matrisi)
        var t = MatMul4(g0, Invert4(gd));

        // σ₁₃(0)=σ₁₃(d)=0 ile v₁ iç serbestliğini ele → [v₃, σ₃₃] cinsinden 2×2
        Complex a, b, cc, dd;
        if (t[3, 0].Magnitude > 1e-12)
        {
            // v₁(d) = -(T42·v₃(d) + T43·σ₃₃(d)) / T41
            var f42 = t[3, 1] / t[3, 0];
            var f43 = t[3, 2] / t[3, 0];
            a = t[1, 1] - t[1, 0] * f42; // v₃(0) ← v₃(d)
            b = t[1, 2] - t[1, 0] * f43; // v₃(0) ← σ₃₃(d)
            cc = t[2, 1] - t[2, 0] * f42; // σ₃₃(0) ← v₃(d)
            dd = t[2, 2] - t[2, 0] * f43; // σ₃₃(0) ← σ₃₃(d)
        }
        else
        {
            // Kayma decoupled (örn. normal geliş s≈0): yalnız P-bloku
            a = t[1, 1]; b = t[1, 2]; cc = t[2, 1]; dd = t[2, 2];
        }

        // [v₃, σ₃₃] → [p, v] (p = -σ₃₃, v = v₃): T_pv = [[dd, -cc], [-b, a]]
        return (dd, -cc, -b, a);
    }

    /// <summary>4×4 kompleks matris çarpımı.</summary>
    private static Complex[,] MatMul4(Complex[,] x, Complex[,] y)
    {
        var z = new Complex[4, 4];
        for (int i = 0; i < 4; i++)
            for (int k = 0; k < 4; k++)
            {
                var sum = Complex.Zero;
                for (int p = 0; p < 4; p++) sum += x[i, p] * y[p, k];
                z[i, k] = sum;
            }
        return z;
    }

    /// <summary>4×4 kompleks matris tersi (kısmi pivotlu Gauss-Jordan).</summary>
    private static Complex[,] Invert4(Complex[,] src)
    {
        var a = new Complex[4, 8];
        for (int i = 0; i < 4; i++)
        {
            for (int k = 0; k < 4; k++) a[i, k] = src[i, k];
            a[i, 4 + i] = Complex.One;
        }

        for (int col = 0; col < 4; col++)
        {
            // Kısmi pivot: en büyük büyüklüklü satırı seç
            int piv = col;
            double best = a[col, col].Magnitude;
            for (int r = col + 1; r < 4; r++)
            {
                double mag = a[r, col].Magnitude;
                if (mag > best) { best = mag; piv = r; }
            }
            if (piv != col)
                for (int k = 0; k < 8; k++) (a[col, k], a[piv, k]) = (a[piv, k], a[col, k]);

            var diag = a[col, col];
            if (diag.Magnitude < 1e-30) diag = new Complex(1e-30, 0);
            for (int k = 0; k < 8; k++) a[col, k] /= diag;

            for (int r = 0; r < 4; r++)
            {
                if (r == col) continue;
                var factor = a[r, col];
                if (factor == Complex.Zero) continue;
                for (int k = 0; k < 8; k++) a[r, k] -= factor * a[col, k];
            }
        }

        var inv = new Complex[4, 4];
        for (int i = 0; i < 4; i++)
            for (int k = 0; k < 4; k++) inv[i, k] = a[i, 4 + k];
        return inv;
    }
}
