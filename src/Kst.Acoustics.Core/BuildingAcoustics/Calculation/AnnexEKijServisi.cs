using System;
using Kst.Acoustics.Core.BuildingAcoustics.Models;

namespace Kst.Acoustics.Core.BuildingAcoustics.Calculation;

/// TS EN 12354-1 Annex E — birleşim (junction) tipine, eleman sınıfına ve kütle oranına göre
/// titreşim azaltma indisi Kij. L-birleşimi hariç (minimum -2 dB) tüm K değerleri ≥ 0 dB'ye sıkıştırılır.
///
/// Akış:
///   K_lab = baseline(geom, yol, eleman sınıfı, M) + bağlantı eklemesi
///   K_ij  = K_lab + AbsorptionLengthCorrection(lij, a_i(f), a_j(f))   (in-situ)
///
/// Eleman sınıfı seçimi:
///   Homogen + Homogen → Annex E rijit formüller (E.3/E.4/E.9)
///   En az 1 Hafif      → E.7/E.8 hafif çift-cidar formülleri (tüm-hafif → E.8, karışık → E.7)
///   En az 1 Sandvic    → E.5 esnek ara katman: rijit + Δ₁(f); BaglantiEklemesi BASTIRILIR (çift sayım)
public enum AnnexEYolTipi
{
    /// T-birleşimi sürekli (in-line) yolu: 1↔2 (örn. zemin sürekli, ayırıcı duvar branch).
    InLine,
    /// T veya L-birleşimi branch yolu: branch elemanı içeren yol (ör. D↔f, D↔F).
    Branch,
    /// X-birleşimi karşılıklı (cross) yol: 1↔3 (ör. iki kat zemini arasındaki yol).
    Cross,
    /// X-birleşimi dik yolu: 1↔2, 1↔4 vb. (ör. ayırıcı duvar ↔ zemin).
    Perp,
}

public static class AnnexEKijServisi
{
    public const double FrekansReferansHz = 1000.0;
    public const double SesHiziMs = 343.0;
    public const double VarsayilanTsSn = 0.5;
    public const double VarsayilanFHz = 500.0;

    /// EN 12354-1 / ISO 10140 — laboratuvar referans yapısal RT (T_s,lab, s).
    /// η_lab ≈ 0.067 (T_s,lab ≈ 0.15 s) — ağır eleman lab kenar sönümü tipik değeri;
    /// kütleden tahmin edilen in-situ T_s ile birlikte düzeltmeyi ~±2 dB bandında tutar.
    /// NOT: Nihai proje öncesi laboratuvar kayıp faktörü konvansiyonunuzla doğrulayın.
    public const double LabReferansTsSn = 0.15;

    /// E.7/E.8 hafif eleman frekans referansı f_k (Hz).
    public const double HafifFkHz = 500.0;
    /// E.5 esnek ara katman köşe frekansı f₁ (Hz): E₁/t₁ ≈ 100 MN/m³ için standart tipik değer.
    public const double EsnekKatmanF1Hz = 125.0;

    /// Yola katılan üç elemandan formül tablosunu seçer (en yüksek decoupling potansiyeli önce).
    /// Sandviç seçildiğinde esnek ara katman E.5 formülünde içerildiğinden
    /// <see cref="BaglantiEklemesi"/> ayrıca uygulanmamalıdır (çağıran taraf bastırır).
    public static YapiElemaniHafifligi SinifSec(
        YapiElemaniHafifligi a,
        YapiElemaniHafifligi b,
        YapiElemaniHafifligi c)
    {
        // Hafif > Sandvic > Homogen sırasında ilerle (en yüksek decoupling önce)
        if (a == YapiElemaniHafifligi.Hafif || b == YapiElemaniHafifligi.Hafif || c == YapiElemaniHafifligi.Hafif)
            return YapiElemaniHafifligi.Hafif;
        if (a == YapiElemaniHafifligi.Sandvic || b == YapiElemaniHafifligi.Sandvic || c == YapiElemaniHafifligi.Sandvic)
            return YapiElemaniHafifligi.Sandvic;
        return YapiElemaniHafifligi.Homogen;
    }

    /// EN 12354-1 Annex E baseline Kij (henüz bağlantı eklemesi ve a_i düzeltmesi yok).
    /// Eleman kategorisi yola katılan üç eleman üzerinden en hafif olana göre seçilir.
    /// L birleşiminde InLine yolu fiziksel olarak yoktur → sınıra yakın 0 döner (yol toplamına etkisiz).
    public static double BaselineKij(
        JunctionGeometriTipi geom,
        AnnexEYolTipi yol,
        double m,
        YapiElemaniHafifligi perp,
        YapiElemaniHafifligi pathA,
        YapiElemaniHafifligi pathB,
        double f = VarsayilanFHz)
    {
        // L-birleşiminde 3. eleman fiziksel olarak yoktur → Cross/InLine yolları geçerli değil.
        // Yine de yol toplamına ses katmaması için yüksek izolasyon döndürürüz.
        if (geom == JunctionGeometriTipi.L && (yol == AnnexEYolTipi.InLine || yol == AnnexEYolTipi.Cross))
            return 99.0; // pratik üst sınır — bu yolun enerjisi neredeyse 0 olur

        var sinif = SinifSec(perp, pathA, pathB);
        bool hepsiHafif = perp == YapiElemaniHafifligi.Hafif &&
                          pathA == YapiElemaniHafifligi.Hafif &&
                          pathB == YapiElemaniHafifligi.Hafif;
        return sinif switch
        {
            YapiElemaniHafifligi.Hafif   => LightweightKij(geom, yol, m, f, hepsiHafif),
            YapiElemaniHafifligi.Sandvic => SandvicKij(geom, yol, m, f),
            _                            => RigidKij(geom, yol, m),
        };
    }

    /// EN 12354-1 Annex E.1 — ağır homojen birleşimler için Kij (rijit baseline).
    /// M = lg(m'⊥ / m'path); K ≥ 0.
    public static double RigidKij(JunctionGeometriTipi geom, AnnexEYolTipi yol, double m)
    {
        var m2 = m * m;
        double k = geom switch
        {
            // ISO 12354-1 Annex E.1 — T-birleşimi: düz-geçiş (in-line, K₁₃) lineer 14.1·M
            // terimini taşır; branch (K₁₂=K₂₃) taşımaz. (X-birleşimiyle tutarlı.)
            JunctionGeometriTipi.T when yol == AnnexEYolTipi.InLine    => 5.7 + 14.1 * m + 5.7 * m2,
            JunctionGeometriTipi.T                                      => 5.7 + 5.7 * m2,
            JunctionGeometriTipi.L                                      => 15.0 * Math.Abs(m) - 3.0,
            JunctionGeometriTipi.X    when yol == AnnexEYolTipi.Cross  => 8.7 + 17.1 * m + 5.7 * m2,
            JunctionGeometriTipi.X                                      => 8.7 + 5.7 * m2,
            JunctionGeometriTipi.Arti when yol == AnnexEYolTipi.Cross  => 8.7 + 17.1 * m + 5.7 * m2,
            JunctionGeometriTipi.Arti                                   => 8.7 + 5.7 * m2,
            _                                                           => 5.7 + 5.7 * m2,
        };
        return geom == JunctionGeometriTipi.L ? Math.Max(-2.0, k) : Math.Max(0, k);
    }

    /// EN 12354-1 Fig. E.7/E.8 — hafif çift-cidar duvar birleşimleri (standart formüller):
    ///   K₁₃ = 10 + 20·M − 3.3·lg(f/f_k)   ve minimum 10 dB   (in-line / cross)
    ///   K₁₂ = 10 + 10·|M| ± 3.3·lg(f/f_k)                    (branch / perp)
    /// f_k = 500 Hz. İşaret: E.7 (hafif + homojen karışım) → +3.3·lg; E.8 (tüm elemanlar
    /// hafif, coupled double leaf) → −3.3·lg. L köşede branch için K₁₂ formülü kullanılır.
    public static double LightweightKij(
        JunctionGeometriTipi geom,
        AnnexEYolTipi yol,
        double m,
        double f = VarsayilanFHz,
        bool hepsiHafif = false)
    {
        _ = geom; // E.7/E.8 formülleri T ve X figürlerinde aynıdır; L branch de K₁₂ ile temsil edilir.
        double lgF = f > 0 ? Math.Log10(f / HafifFkHz) : 0;
        double k = yol is AnnexEYolTipi.InLine or AnnexEYolTipi.Cross
            ? Math.Max(10.0, 10.0 + 20.0 * m - 3.3 * lgF)
            : 10.0 + 10.0 * Math.Abs(m) + (hepsiHafif ? -3.3 : 3.3) * lgF;
        return Math.Max(0, k);
    }

    /// EN 12354-1 Fig. E.5 — esnek ara katmanlı (sandviç) birleşim:
    ///   K₁₃ = rijit_in-line + 2·Δ₁ ;  K₁₂ = rijit_branch + Δ₁
    ///   Δ₁ = 10·lg(f/f₁) (f > f₁), f₁ = 125 Hz (E₁/t₁ ≈ 100 MN/m³).
    /// Esnek katmanın etkisi formülde içerildiğinden BaglantiEklemesi ayrıca uygulanmaz.
    public static double SandvicKij(JunctionGeometriTipi geom, AnnexEYolTipi yol, double m, double f = VarsayilanFHz)
    {
        double delta1 = f > EsnekKatmanF1Hz ? 10.0 * Math.Log10(f / EsnekKatmanF1Hz) : 0;
        double carpan = yol is AnnexEYolTipi.InLine or AnnexEYolTipi.Cross ? 2.0 : 1.0;
        return Math.Max(0, RigidKij(geom, yol, m) + carpan * delta1);
    }

    /// Bağlantı tipinin Annex E baseline üzerine eklediği ek decoupling (dB).
    /// Rijit=0, YariElastik=+5, Elastik=+10, Kesintili=+15.
    public static double BaglantiEklemesi(BaglantiTipi tip) => tip switch
    {
        BaglantiTipi.Rijit       => 0.0,
        BaglantiTipi.YariElastik => 5.0,
        BaglantiTipi.Elastik     => 10.0,
        BaglantiTipi.Kesintili   => 15.0,
        _                        => 0.0,
    };

    /// Ek2 — Junction'daki strüktürel güçlendirmenin Kij'e eklediği ek decoupling (dB).
    /// Yok=0; Kiris=+3 (kiriş süreklilik kırıyor); PerdeDuvar=+5 (shear wall yüksek süreklilik).
    /// Annex E rijit baseline + BaglantiEklemesi + StrukturelEklemesi katmanlı uygulanır.
    public static double StrukturelEklemesi(JunctionStrukturelTipi tip) => tip switch
    {
        JunctionStrukturelTipi.Kiris      => 3.0,
        JunctionStrukturelTipi.PerdeDuvar => 5.0,
        _                                 => 0.0,
    };

    /// M = lg(m'⊥ / m_flank); H10 uyarınca ortalama yerine ilgili flank kütlesi alınır.
    /// Eksik kütle (≤0) varsa M=0 döner — geometri baseline kullanılır.
    public static double KutleOraniM(double mPerp, double mFlank)
    {
        if (mPerp <= 0 || mFlank <= 0) return 0;
        return Math.Log10(mPerp / mFlank);
    }

    /// EN 12354-1 / ISO 10848 — eşdeğer soğurma uzunluğu a_i (m).
    ///   a = 2.2·π²·S / (c₀·T_s) · √(f_ref/f)
    /// f_ref = 1000 Hz, c₀ = 343 m/s. Varsayılan T_s = 0.5 s (ağır homojen yapı in-situ),
    /// varsayılan f = 500 Hz (tek-sayı için Rw 500 Hz çıpasıyla hizalı).
    public static double EquivalentAbsorptionLength(double s, double tStruct = VarsayilanTsSn, double f = VarsayilanFHz)
    {
        if (s <= 0 || tStruct <= 0 || f <= 0) return 0;
        return 2.2 * Math.PI * Math.PI * s / (SesHiziMs * tStruct) * Math.Sqrt(FrekansReferansHz / f);
    }

    /// K_ij ↔ D̄_v,ij dönüşümü: D̄_v,ij = K_ij − 10·log10(lij/√(ai·aj)).
    /// Rij formülü D̄_v,ij ile çalıştığı için, in-situ kullanım için bu düzeltmeyi K_ij'den çıkar.
    /// Eksik girdi varsa 0 döner (düzeltme uygulanmaz).
    public static double AbsorptionLengthCorrection(double lij, double aI, double aJ)
    {
        if (lij <= 0 || aI <= 0 || aJ <= 0) return 0;
        return -10.0 * Math.Log10(lij / Math.Sqrt(aI * aJ));
    }

}
