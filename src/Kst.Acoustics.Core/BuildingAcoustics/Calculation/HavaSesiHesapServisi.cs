using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.BuildingAcoustics.Calculation;

public class HavaSesiHesapServisi
{
    /// <summary>
    /// TS EN 12354-1 standardına göre iki mekan arasındaki hava sesi yalıtımını hesaplar.
    /// </summary>
    public double HesaplaApparentRw(Bolme bolme)
    {
        if (bolme.KaynakMekan == null || bolme.AliciMekan == null || bolme.YapiElemani == null)
            return 0;

        if (bolme.Alan <= 0)
            return 0;

        var ucuncu = HesaplaUcuncuOktavApparentR(bolme);
        if (ucuncu.RwHesap <= 0)
            return 0;

        bolme.ApparentRw = ucuncu.RwHesap;
        return ucuncu.RwHesap;
    }

    public static double CalculateCompositeRw(Bolme bolme)
    {
        if (bolme.YapiElemani == null || bolme.Alan <= 0)
            return 0;

        var openingArea = bolme.Acikliklar.Sum(a => Math.Max(0, a.Alan));
        var solidArea   = Math.Max(0, bolme.Alan - openingArea);
        var totalArea   = Math.Max(bolme.Alan, solidArea + openingArea);
        if (totalArea <= 0)
            return 0;

        var rOpak = bolme.YapiElemani.EffectiveRw;
        if (bolme.Acikliklar.Count == 0 && bolme.Menfezler.Count == 0) return rOpak;

        double weightedTau = solidArea * Math.Pow(10, -rOpak / 10.0);
        foreach (var aciklik in bolme.Acikliklar)
        {
            double rAciklik = aciklik.YapiElemani?.EffectiveRw ?? (aciklik.Tip == BolmeAcikligiTipi.Kapi ? 28.0 : 32.0);
            weightedTau += Math.Max(0, aciklik.Alan) * Math.Pow(10, -rAciklik / 10.0);
        }

        foreach (var menfez in bolme.Menfezler)
            weightedTau += MenfezVarsayilan.A0 * Math.Pow(10, -menfez.DneDb / 10.0);

        var tau = weightedTau / totalArea;
        double rComposite = tau > 0 ? -10.0 * Math.Log10(tau) : rOpak;
        
        return rComposite;
    }

    public static IReadOnlyList<FlankingYoluSonucu> GetOnUcYolFlankingYollari(Bolme bolme)
    {
        var ayiriciRw = bolme.YapiElemani?.EffectiveRw ?? 0;
        return ayiriciRw > 0
            ? GetOnUcYolFlankingYollari(bolme, ayiriciRw).ToList()
            : [];
    }

    private static IEnumerable<double> GetOnUcYolFlankingRijleri(Bolme bolme, double ayiriciRw) =>
        GetOnUcYolFlankingYollari(bolme, ayiriciRw).Select(y => y.Rij);

    private static IEnumerable<FlankingYoluSonucu> GetOnUcYolFlankingYollari(Bolme bolme, double ayiriciRw)
    {
        if (bolme.KaynakMekan == null || bolme.AliciMekan == null || bolme.Alan <= 0)
            yield break;

        // Kij artık Annex E rijit formülleri + bağlantı eklemesi üzerinden, yol tipine
        // göre per-path hesaplanır (T'de Df/Fd = branch, Ff = in-line; X'te Df/Fd = perp,
        // Ff = cross). A9: Direkt elemana in-situ T_s düzeltmesi uygulanmaz; 
        // in-situ etki yanal iletim üzerinden ele alınır. Flanking elemanlarına da T_s,situ
        // üzerinden Annex F düzeltmesi uygulanır.
        double ss = bolme.Alan;
        EnsureDefaultJunctions(bolme);
        foreach (var junction in bolme.Junctions.OrderBy(j => j.Kenar))
        {
            var (kaynakFlank, kaynakAlan) = GetFlankingElemani(bolme.KaynakMekan, bolme, junction.Kenar);
            var (aliciFlank, aliciAlan) = GetFlankingElemani(bolme.AliciMekan, bolme, junction.Kenar);
            // ISO 12354-1: flanking eleman R'leri laboratuvar değeridir; in-situ etki Dv,ij içindedir.
            double rKaynak = kaynakFlank?.EffectiveRw ?? 0;
            double rAlici  = aliciFlank?.EffectiveRw  ?? 0;

            if (aliciFlank?.EffectiveRw > 0)
            {
                var k = ComputeKijForPath(junction, "Df", bolme.YapiElemani, kaynakFlank, aliciFlank, ss, kaynakAlan, aliciAlan);
                var rij = (ayiriciRw + rAlici) / 2.0 + k + CouplingTerm(ss, ss, aliciAlan);
                yield return new FlankingYoluSonucu(junction.Kenar, "Df", rij, k, ayiriciRw, rAlici);
            }

            if (kaynakFlank?.EffectiveRw > 0)
            {
                var k = ComputeKijForPath(junction, "Fd", bolme.YapiElemani, kaynakFlank, aliciFlank, ss, kaynakAlan, aliciAlan);
                var rij = (rKaynak + ayiriciRw) / 2.0 + k + CouplingTerm(ss, kaynakAlan, ss);
                yield return new FlankingYoluSonucu(junction.Kenar, "Fd", rij, k, rKaynak, ayiriciRw);
            }

            if (kaynakFlank?.EffectiveRw > 0 && aliciFlank?.EffectiveRw > 0)
            {
                var k = ComputeKijForPath(junction, "Ff", bolme.YapiElemani, kaynakFlank, aliciFlank, ss, kaynakAlan, aliciAlan);
                var rij = (rKaynak + rAlici) / 2.0 + k + CouplingTerm(ss, kaynakAlan, aliciAlan);
                yield return new FlankingYoluSonucu(junction.Kenar, "Ff", rij, k, rKaynak, rAlici);
            }
        }
    }

    public static void EnsureDefaultJunctions(Bolme bolme)
    {
        var len = BolmeUzunlugu(bolme);
        if (len <= 0) len = Math.Sqrt(Math.Max(0.01, bolme.Alan));
        var height = bolme.KaynakMekan?.Yukseklik > 0 ? bolme.KaynakMekan.Yukseklik : Math.Sqrt(Math.Max(0.01, bolme.Alan));

        var yapiElemani = bolme.YapiElemani;
        var beklenenBaglanti = yapiElemani?.BaglantiTipi ?? BaglantiTipi.Rijit;

        foreach (var kenar in Enum.GetValues<JunctionKenarTipi>())
        {
            var varOlan = bolme.Junctions.FirstOrDefault(j => j.Kenar == kenar);
            if (varOlan != null)
            {
                // Kullanıcı junction ayarı hesap sırasında korunur; malzeme değişimi UI atama akışında uygulanır.
                continue;
            }

            bolme.Junctions.Add(new Junction
            {
                Kenar = kenar,
                GeometriTipi = kenar is JunctionKenarTipi.Alt or JunctionKenarTipi.Ust
                    ? JunctionGeometriTipi.T
                    : JunctionGeometriTipi.L,
                BaglantiTipi = beklenenBaglanti,
                Uzunluk = kenar is JunctionKenarTipi.Alt or JunctionKenarTipi.Ust ? len : height,
                KijOverrideDb = 0
            });
        }
    }

    /// Flanking eleman ve onun iletim alanı (m²). Zemin/tavan için oda alanı,
    /// yan duvar için komşu bölme alanı; EN 12354-1 alan terimi √(Si·Sj) için gerekir.
    public static (YapiElemani? Eleman, double Alan) GetFlankingElemani(Mekan mekan, Bolme ayiriciBolme, JunctionKenarTipi kenar) =>
        kenar switch
        {
            JunctionKenarTipi.Alt => (mekan.ZeminYapiElemani, mekan.Alan),
            JunctionKenarTipi.Ust => (mekan.TavanYapiElemani, mekan.Alan),
            JunctionKenarTipi.Sol => FindAdjacentWall(mekan, ayiriciBolme.Baslangic, ayiriciBolme),
            JunctionKenarTipi.Sag => FindAdjacentWall(mekan, ayiriciBolme.Bitis, ayiriciBolme),
            _ => (null, 0)
        };

    private static (YapiElemani? Eleman, double Alan) FindAdjacentWall(Mekan mekan, Point2D joint, Bolme ayiriciBolme)
    {
        const double tol = 0.25;
        var hit = mekan.Bolmeler
            .Where(b => !ReferenceEquals(b, ayiriciBolme) && b.YapiElemani?.Rw > 0)
            .Select(b => new
            {
                Bolme = b,
                Dist = Math.Min(Distance(joint, b.Baslangic), Distance(joint, b.Bitis))
            })
            .Where(x => x.Dist <= tol)
            .OrderBy(x => x.Dist)
            .FirstOrDefault();
        return hit == null ? (null, 0) : (hit.Bolme.YapiElemani, hit.Bolme.Alan);
    }

    /// EN 12354-1 Eq. 21 birleşim alanı terimi: 10·log10(Ss / √(Si·Sj)).
    /// Si, Sj iletim yolundaki kaynak/alıcı eleman alanları (m²).
    private static double CouplingTerm(double ss, double si, double sj)
    {
        var area = Math.Max(ss, 0.01);
        var denom = Math.Sqrt(Math.Max(si, 0.01) * Math.Max(sj, 0.01));
        return 10.0 * Math.Log10(area / denom);
    }

    /// EN 12354-1 Annex E rijit baseline + bağlantı eklemesi + in-situ soğurma-uzunluğu düzeltmesi.
    /// Sonuç D̄_v,ij olarak Rij formülünde kullanılır: D̄_v,ij = K_ij − 10·log10(lij/√(ai·aj)).
    /// Kullanıcı override (Junction.KijOverrideDb > 0) önceliklidir.
    /// Yol → Annex E tipi: T'de Df/Fd = Branch, Ff = InLine; X/+ 'da Df/Fd = Perp, Ff = Cross; L → Branch.
    /// f: değerlendirme frekansı (Hz, varsayılan 500 — tek sayı yolu); 1/3 oktav için bant merkezi.
    /// Eleman.YapisalRtSn > 0 ise o değer kullanılır, aksi halde 0.5 s varsayılan.
    private static double ComputeKijForPath(
        Junction junction,
        string yolTipi,
        YapiElemani? d,
        YapiElemani? fSrc,
        YapiElemani? fRcv,
        double ssArea,
        double fSrcArea,
        double fRcvArea,
        double f = 500.0)
    {
        if (junction.KijOverrideDb > 0) return junction.KijOverrideDb;

        var pathTipi = MapPathToAnnexETip(junction.GeometriTipi, yolTipi);
        double mD = d?.ToplamYuzeyKutlesi ?? 0;
        double mF = fSrc?.ToplamYuzeyKutlesi ?? 0;
        double mf = fRcv?.ToplamYuzeyKutlesi ?? 0;
        var hD = d?.Hafiflik     ?? YapiElemaniHafifligi.Homogen;
        var hF = fSrc?.Hafiflik  ?? YapiElemaniHafifligi.Homogen;
        var hf = fRcv?.Hafiflik  ?? YapiElemaniHafifligi.Homogen;

        // EN 12354-1 s.43: M = lg(m'⊥,i / m'i); m'i yol elemanı i, m'⊥,i junction'da ona dik
        // DİĞER eleman. Flank (F ve f) aynı sürekli elemanın iki yüzüdür → kütle ortalaması
        // alınır; oran her yolda AYIRICI (D) ↔ FLANK arasındadır.
        // Ff (in-line/cross, lineer M terimi): i = flank, ⊥ = D → M = lg(mD/mFlank).
        // Df/Fd (branch/perp, yalnız M²):      i = D,     ⊥ = flank → M = lg(mFlank/mD).
        double mFlank = mF > 0 && mf > 0 ? 0.5 * (mF + mf) : Math.Max(mF, mf);
        double m;
        double si, sj, tsI, tsJ;
        YapiElemaniHafifligi perpH, pathAH, pathBH;
        switch (yolTipi)
        {
            case "Df":
                m = AnnexEKijServisi.KutleOraniM(mFlank, mD);
                si = ssArea; sj = fRcvArea;                   // path: D ↔ f
                tsI = ElemanTs(d); tsJ = ElemanTs(fRcv);
                perpH = hF; pathAH = hD; pathBH = hf;
                break;
            case "Fd":
                m = AnnexEKijServisi.KutleOraniM(mFlank, mD);
                si = fSrcArea; sj = ssArea;                   // path: F ↔ D
                tsI = ElemanTs(fSrc); tsJ = ElemanTs(d);
                perpH = hf; pathAH = hF; pathBH = hD;
                break;
            case "Ff":
                m = AnnexEKijServisi.KutleOraniM(mD, mFlank);
                si = fSrcArea; sj = fRcvArea;                 // path: F ↔ f
                tsI = ElemanTs(fSrc); tsJ = ElemanTs(fRcv);
                perpH = hD; pathAH = hF; pathBH = hf;
                break;
            default:
                m = 0; si = sj = 0; tsI = tsJ = AnnexEKijServisi.VarsayilanTsSn;
                perpH = pathAH = pathBH = YapiElemaniHafifligi.Homogen;
                break;
        }

        // A2 — Sandviç sınıfında esnek ara katman E.5 formülünde içerilir; BaglantiEklemesi
        // ayrıca uygulanmaz (çift sayım önlenir).
        var sinif = AnnexEKijServisi.SinifSec(perpH, pathAH, pathBH);
        var baglantiEk = sinif == YapiElemaniHafifligi.Sandvic
            ? 0
            : AnnexEKijServisi.BaglantiEklemesi(junction.BaglantiTipi);
        var kAnnex = AnnexEKijServisi.BaselineKij(junction.GeometriTipi, pathTipi, m, perpH, pathAH, pathBH, f)
                   + baglantiEk
                   + AnnexEKijServisi.StrukturelEklemesi(junction.StrukturelTipi);

        // In-situ dönüşüm: D̄_v,ij = K_ij − 10·log10(lij/√(ai·aj)).
        // A4 — EN 12354-1 E.3 Limiting values: seçilen Kij, D̄_v,ij,situ ≥ 0 dB vermelidir.
        var aI = AnnexEKijServisi.EquivalentAbsorptionLength(si, tsI, f);
        var aJ = AnnexEKijServisi.EquivalentAbsorptionLength(sj, tsJ, f);
        return Math.Max(0, kAnnex + AnnexEKijServisi.AbsorptionLengthCorrection(junction.Uzunluk, aI, aJ));
    }

    /// In-situ yapısal RT seçimi: önce kullanıcı/ölçüm değeri (YapisalRtSn > 0),
    /// yoksa EN 12354-1 Annex C kütle tahmini, ne ölçüm ne kütle varsa lab referansı
    /// (LabReferansTsSn) kullanılır.
    public static double ElemanTs(YapiElemani? e)
    {
        if (e is null) return AnnexEKijServisi.LabReferansTsSn;
        if (e.YapisalRtSn > 0) return e.YapisalRtSn;
        var tahmin = e.TahminiYapisalRtSn();
        return tahmin > 0 ? tahmin : AnnexEKijServisi.LabReferansTsSn;
    }

    private static AnnexEYolTipi MapPathToAnnexETip(JunctionGeometriTipi geom, string yolTipi)
    {
        bool isFf = yolTipi == "Ff";
        return geom switch
        {
            JunctionGeometriTipi.T    => isFf ? AnnexEYolTipi.InLine : AnnexEYolTipi.Branch,
            // L köşede sürekli 3. eleman yoktur; Ff (in-line) yolu fiziksel olarak mevcut değildir.
            // BaselineKij L+InLine için 99 dB döner → enerji toplamında yok sayılır.
            JunctionGeometriTipi.L    => isFf ? AnnexEYolTipi.InLine : AnnexEYolTipi.Branch,
            JunctionGeometriTipi.X    => isFf ? AnnexEYolTipi.Cross  : AnnexEYolTipi.Perp,
            JunctionGeometriTipi.Arti => isFf ? AnnexEYolTipi.Cross  : AnnexEYolTipi.Perp,
            _                          => AnnexEYolTipi.Branch,
        };
    }

    private static double BolmeUzunlugu(Bolme bolme) =>
        Distance(bolme.Baslangic, bolme.Bitis);

    private static double Distance(Point2D a, Point2D b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// TS EN 12354-1 Eq. 1 — Standartize ses düzeyi farkı (DnT,w).
    /// DnT,w = R' + 10·log10(0.16·V / (T₀·S))   [T₀ = 0.5 s]
    /// Eşdeğer: DnT,w = R' + 10·log10(0.32·V / S)
    /// (Türetme: DnT = R' + 10·log10(0.16·V/(T₀·S)); Sabine A·T = 0.16·V.)
    /// </summary>
    public double HesaplaDntw(Bolme bolme)
    {
        if (bolme.AliciMekan == null || bolme.Alan <= 0)
            return 0;

        double v = bolme.AliciMekan.Hacim;
        double s = bolme.Alan;

        if (v <= 0 || s <= 0)
            return 0;

        double rPrime = bolme.ApparentRw > 0 ? bolme.ApparentRw : HesaplaApparentRw(bolme);
        if (rPrime <= 0)
            return 0;

        // T₀ = 0.5 s → 0.16 / T₀ = 0.32 (eşdeğer 0.32·V/S)
        const double t0 = 0.5;
        double dntw = rPrime + 10 * Math.Log10(0.16 * v / (t0 * s));

        bolme.Dntw = dntw;
        return dntw;
    }

    /// <summary>
    /// TS EN ISO 717-1 — Tek sayı değeri (Rw) ve adaptasyon terimleri (C, Ctr).
    /// Giriş: 16 üçüncü-oktav bant değeri, 100–3150 Hz.
    /// Algoritma: referans eğriyi 1 dB adımlarla kaydır, toplam olumsuz sapma ≤ 32 dB olan
    /// en yüksek pozisyonda Rw = referans_500Hz + kaydırma.
    /// A3: Giriş bandı sayısı 16'dan azsa hesap geçersiz sayılır (ISO 717-1 Madde 5.1).
    /// </summary>
    public (double Rw, double C, double Ctr, bool BantSayisiYetersiz) CalculateSingleValue(double[] thirdOctaveRw)
    {
        if (thirdOctaveRw.Length == 0)
            return (0, 0, 0, true);

        // A3 — ISO 717-1 Madde 5.1: en az 100–3150 Hz (16 bant) zorunlu
        if (thirdOctaveRw.Length < 16)
            return (0, 0, 0, true);

        var values = thirdOctaveRw.Take(16).ToArray();
        // ISO 717-1 referans eğrisi, 100–3150 Hz (16 bant)
        var reference = new[] { 33, 36, 39, 42, 45, 48, 51, 52, 53, 54, 55, 56, 56, 56, 56, 56 };

        int bestShift = -50;
        for (int shift = -50; shift <= 50; shift++)
        {
            double unfavorable = 0;
            for (int i = 0; i < values.Length && i < reference.Length; i++)
                unfavorable += Math.Max(0, reference[i] + shift - values[i]);

            if (unfavorable <= 32)
                bestShift = shift;
        }

        // Rw = referans eğrisinin 500 Hz değeri (index 7 = 52 dB) + kaydırma
        double rw = reference[7] + bestShift;

        // ISO 717-1 Annex B — adaptasyon terimleri
        // Pembe gürültü spektrumu L1 (100–3150 Hz, normalize toplam = 0 dB)
        var pinkNoise = new double[] { -29, -26, -23, -21, -19, -17, -15, -13, -12, -11, -10, -9, -9, -9, -9, -9 };
        // Kentsel trafik gürültüsü spektrumu L2
        var traffic   = new double[] { -20, -20, -18, -16, -15, -14, -13, -12, -11,  -9,  -8, -9, -10, -11, -13, -15 };

        double sumPink = 0, sumTraffic = 0;
        for (int i = 0; i < values.Length && i < pinkNoise.Length; i++)
        {
            sumPink    += Math.Pow(10, (pinkNoise[i] - values[i]) / 10.0);
            sumTraffic += Math.Pow(10, (traffic[i]   - values[i]) / 10.0);
        }

        double c   = sumPink    > 0 ? -10 * Math.Log10(sumPink)    - rw : 0;
        double ctr = sumTraffic > 0 ? -10 * Math.Log10(sumTraffic) - rw : 0;

        return (Math.Round(rw, 1), Math.Round(c), Math.Round(ctr), false);
    }

    // ─── Oktav bant hesabı ────────────────────────────────────────────────────

    public UcuncuOktavHavaSonucu HesaplaUcuncuOktavApparentR(Bolme bolme)
    {
        if (bolme.YapiElemani == null || bolme.Alan <= 0)
        {
            var empty = new double[16];
            return new UcuncuOktavHavaSonucu(empty, 0, 0, 0, "ModelTahmin");
        }

        var solidR = OktavBantElemanServisi.GetRUcuncuOktavDizisi(bolme.YapiElemani);
        var rDizi = CalculateCompositeRThirdOctave(bolme, solidR);

        if (bolme.KaynakMekan != null && bolme.AliciMekan != null)
        {
            EnsureDefaultJunctions(bolme);
            for (int i = 0; i < rDizi.Length; i++)
            {
                double tauTotal = Math.Pow(10, -rDizi[i] / 10.0);
                foreach (var rij in GetUcuncuOktavFlankingRijleri(bolme, i, solidR[i]))
                    tauTotal += Math.Pow(10, -rij / 10.0);
                rDizi[i] = tauTotal > 0 ? -10.0 * Math.Log10(tauTotal) : rDizi[i];
            }
        }

        var (rw, c, ctr, _) = CalculateSingleValue(rDizi);
        var kaynak = OktavBantElemanServisi.HavaSpektrumKaynagiEtiketi(bolme.YapiElemani);
        var sonuc = new UcuncuOktavHavaSonucu(rDizi, rw, c, ctr, kaynak);
        bolme.UcuncuOktavHavaSesiSonucu = sonuc;
        bolme.ApparentRw = rw;
        return sonuc;
    }

    /// Bölme için oktav bantlı R'(f) dizisi ve tek sayı türetimi.
    /// Bant sayısı yeterliyse ISO 717-1 uyarlaması; değilse mevcut ApparentRw'den sabit tahmin.
    public OktavBantSonucu HesaplaOktavApparentR(Bolme bolme)
    {
        if (bolme.YapiElemani == null || bolme.Alan <= 0)
        {
            var empty = new double[7];
            return new OktavBantSonucu(empty, 0, 0, 0, "TekSayi");
        }

        if (bolme.UcuncuOktavHavaSesiSonucu is { } third)
        {
            // Fix #5 — 1/3-oktav sonucu ZATEN composite (açıklık) + flanking içerir; yalnız oktava indir.
            // Fix #6 — Tek-sayı (Rw/C/Ctr) STANDART ISO 717-1 16-bant sonucundan alınır;
            //          7-bant oktav bantları yalnız görsel bardır, standart-dışı oktav uyarlaması kullanılmaz.
            var rThird = ThirdToOktavDisplay(third.R_f);
            var sonucThird = new OktavBantSonucu(rThird, third.RwHesap, third.CHesap, third.CtrHesap, third.Kaynak);
            bolme.OktavHavaSesiSonucu = sonucThird;
            return sonucThird;
        }

        // 1/3-oktav sonucu yoksa (yalnız oktav verili eleman, nadir): ham R(f) + composite + flanking,
        // ardından ISO 717-1'in oktav-bant YAKLAŞIMI (yalnız bu fallback'te) tek-sayı türetir.
        var rDizi = OktavBantElemanServisi.GetRDizisi(bolme.YapiElemani);
        var kaynak = OktavBantElemanServisi.HavaSpektrumKaynagiEtiketi(bolme.YapiElemani);

        rDizi = CalculateCompositeROktav(bolme, rDizi);

        double compositeRw = CalculateCompositeRw(bolme);
        double apparentRw  = bolme.ApparentRw > 0 ? bolme.ApparentRw : HesaplaApparentRw(bolme);
        double flankingDelta = apparentRw - compositeRw; // ≤ 0
        for (int i = 0; i < rDizi.Length; i++)
            rDizi[i] = Math.Max(0, rDizi[i] + flankingDelta);

        var (rw, c, ctr, _) = CalculateSingleValueOktav(rDizi);
        var sonuc = new OktavBantSonucu(rDizi, rw, c, ctr, kaynak);
        bolme.OktavHavaSesiSonucu = sonuc;
        return sonuc;
    }

    private static IEnumerable<double> GetUcuncuOktavFlankingRijleri(Bolme bolme, int bandIndex, double ayiriciR)
    {
        if (bolme.KaynakMekan == null || bolme.AliciMekan == null)
            yield break;

        double ss = bolme.Alan;
        // 1/3 oktav yolda Kij band merkezi frekansı ile değerlendirilir (a(f) ∝ √(f_ref/f)).
        var freq = bandIndex >= 0 && bandIndex < OktavBantElemanServisi.UcuncuOktavFreqler.Length
            ? OktavBantElemanServisi.UcuncuOktavFreqler[bandIndex]
            : AnnexEKijServisi.VarsayilanFHz;

        // A9 — Direkt elemana in-situ T_s düzeltmesi uygulanmaz; in-situ etki yanal
        // iletim üzerinden ele alınır. Flanking elemanlarına da T_s,situ üzerinden 
        // Annex F düzeltmesi uygulanır.
        foreach (var junction in bolme.Junctions.OrderBy(j => j.Kenar))
        {
            var (kaynakFlank, kaynakAlan) = GetFlankingElemani(bolme.KaynakMekan, bolme, junction.Kenar);
            var (aliciFlank, aliciAlan) = GetFlankingElemani(bolme.AliciMekan, bolme, junction.Kenar);
            // ISO 12354-1: flanking eleman R(f)'leri laboratuvar değeridir; in-situ etki Dv,ij içindedir.

            if (aliciFlank != null)
            {
                var aliciR = GetRThirdBand(aliciFlank, bandIndex);
                var k = ComputeKijForPath(junction, "Df", bolme.YapiElemani, kaynakFlank, aliciFlank, ss, kaynakAlan, aliciAlan, freq);
                yield return (ayiriciR + aliciR) / 2.0 + k + CouplingTerm(ss, ss, aliciAlan);
            }

            if (kaynakFlank != null)
            {
                var kaynakR = GetRThirdBand(kaynakFlank, bandIndex);
                var k = ComputeKijForPath(junction, "Fd", bolme.YapiElemani, kaynakFlank, aliciFlank, ss, kaynakAlan, aliciAlan, freq);
                yield return (kaynakR + ayiriciR) / 2.0 + k + CouplingTerm(ss, kaynakAlan, ss);
            }

            if (kaynakFlank != null && aliciFlank != null)
            {
                var kaynakR = GetRThirdBand(kaynakFlank, bandIndex);
                var aliciR  = GetRThirdBand(aliciFlank, bandIndex);
                var k = ComputeKijForPath(junction, "Ff", bolme.YapiElemani, kaynakFlank, aliciFlank, ss, kaynakAlan, aliciAlan, freq);
                yield return (kaynakR + aliciR) / 2.0 + k + CouplingTerm(ss, kaynakAlan, aliciAlan);
            }
        }
    }

    private static double GetRThirdBand(YapiElemani eleman, int bandIndex)
    {
        var r = OktavBantElemanServisi.GetRUcuncuOktavDizisi(eleman);
        return bandIndex >= 0 && bandIndex < r.Length ? r[bandIndex] : eleman.EffectiveRw;
    }

    public static double[] CalculateCompositeRThirdOctave(Bolme bolme, double[] solidR)
    {
        // ISO 12354-1: direkt (ayırıcı) eleman ve açıklıkları laboratuvar R'siyle kullanılır;
        // in-situ kayıp-faktörü etkisi yalnız flanking yollarında (Dv,ij) hesaba katılır.
        var openingArea = bolme.Acikliklar.Sum(a => Math.Max(0, a.Alan));
        var solidArea = Math.Max(0, bolme.Alan - openingArea);
        var totalArea = Math.Max(bolme.Alan, solidArea + openingArea);
        if (totalArea <= 0 || (bolme.Acikliklar.Count == 0 && bolme.Menfezler.Count == 0))
        {
            // Açıklık ve menfez yok; ayırıcı eleman lab R(f) doğrudan döner.
            return (double[])solidR.Clone();
        }

        var result = new double[16];
        for (int i = 0; i < result.Length; i++)
        {
            double weightedTau = solidArea * Math.Pow(10, -solidR[i] / 10.0);
            foreach (var aciklik in bolme.Acikliklar)
            {
                double openR = aciklik.YapiElemani != null
                    ? GetRThirdBand(aciklik.YapiElemani, i)
                    : (aciklik.Tip == BolmeAcikligiTipi.Kapi ? 28.0 : 32.0);
                weightedTau += Math.Max(0, aciklik.Alan) * Math.Pow(10, -openR / 10.0);
            }
            // A8 — Menfez Dn,e(f) bant bazlı katkısı: A_0·10^(-Dne(f)/10).
            foreach (var menfez in bolme.Menfezler)
            {
                double dne = menfez.HasSpectrum ? menfez.DneThirdOctave[i] : menfez.DneDb;
                weightedTau += MenfezVarsayilan.A0 * Math.Pow(10, -dne / 10.0);
            }
            double tau = weightedTau / totalArea;
            result[i] = tau > 0 ? -10.0 * Math.Log10(tau) : solidR[i];
        }
        return result;
    }

    private static double[] ThirdToOktavDisplay(double[] third)
    {
        if (third.Length < 16)
            return new double[7];

        return
        [
            third[0],
            Average(third, 0, 3),
            Average(third, 3, 6),
            Average(third, 6, 9),
            Average(third, 9, 12),
            Average(third, 12, 15),
            third[15],
        ];
    }

    private static double Average(double[] values, int start, int endExclusive)
    {
        double sum = 0;
        int count = 0;
        for (int i = start; i < endExclusive && i < values.Length; i++)
        {
            sum += values[i];
            count++;
        }
        return count > 0 ? sum / count : 0;
    }

    private static double[] CalculateCompositeROktav(Bolme bolme, double[] solidR)
    {
        var openingArea = bolme.Acikliklar.Sum(a => Math.Max(0, a.Alan));
        var solidArea   = Math.Max(0, bolme.Alan - openingArea);
        var totalArea   = Math.Max(bolme.Alan, solidArea + openingArea);
        if (totalArea <= 0 || bolme.Acikliklar.Count == 0)
            return solidR;

        var result = new double[7];
        for (int i = 0; i < 7; i++)
        {
            double weightedTau = solidArea * Math.Pow(10, -solidR[i] / 10.0);
            foreach (var aciklik in bolme.Acikliklar)
            {
                double openR = aciklik.YapiElemani != null
                    ? OktavBantElemanServisi.GetRDizisi(aciklik.YapiElemani)[i]
                    : (aciklik.Tip == BolmeAcikligiTipi.Kapi ? 28.0 : 32.0);
                weightedTau += Math.Max(0, aciklik.Alan) * Math.Pow(10, -openR / 10.0);
            }
            foreach (var menfez in bolme.Menfezler)
            {
                double dne = menfez.DneDb;
                if (menfez.HasSpectrum && menfez.DneThirdOctave.Count >= 16)
                {
                    var d = menfez.DneThirdOctave.ToArray();
                    dne = i switch {
                        0 => d[0],
                        1 => Average(d, 0, 3),
                        2 => Average(d, 3, 6),
                        3 => Average(d, 6, 9),
                        4 => Average(d, 9, 12),
                        5 => Average(d, 12, 15),
                        6 => d[15],
                        _ => menfez.DneDb
                    };
                }
                weightedTau += MenfezVarsayilan.A0 * Math.Pow(10, -dne / 10.0);
            }
            double tau = weightedTau / totalArea;
            result[i] = tau > 0 ? -10.0 * Math.Log10(tau) : solidR[i];
        }
        return result;
    }

    /// ISO 717-1 uyarlaması — 7 oktav bant (63–4000 Hz) için Rw, C, Ctr türetimi.
    /// Referans eğrisi 63–4000 Hz (7 bant), 500 Hz referans 52 dB olarak korunur.
    public (double Rw, double C, double Ctr, bool BantSayisiYetersiz) CalculateSingleValueOktav(double[] oktavR)
    {
        if (oktavR.Length < 7)
            return (0, 0, 0, true);

        var values = oktavR[..7];
        // ISO 717-1 eğrisinin 63–4000 Hz oktav bant interpolasyonu
        // (63=33, 125=36, 250=42, 500=52, 1k=55, 2k=56, 4k=56) — EN 717-1 Tablo 1 oktav seçimi
        var reference = new[] { 33, 36, 42, 52, 55, 56, 56 };

        int bestShift = -50;
        for (int shift = -50; shift <= 50; shift++)
        {
            double unfavorable = 0;
            for (int i = 0; i < values.Length; i++)
                unfavorable += Math.Max(0, reference[i] + shift - values[i]);

            // 7 bantlı ölçek için eşik ≈ 32 × (7/16) ≈ 14 dB
            if (unfavorable <= 14)
                bestShift = shift;
        }

        double rw = reference[3] + bestShift; // 500 Hz referans = 52

        // Pembe gürültü ve trafik spektrumu — 63–4000 Hz oktav bantları
        var pinkNoise = new double[] { -29, -23, -17, -13, -10, -9, -9 };
        var traffic   = new double[] { -20, -18, -14, -12, -11, -9, -10 };

        double sumPink = 0, sumTraffic = 0;
        for (int i = 0; i < values.Length; i++)
        {
            sumPink    += Math.Pow(10, (pinkNoise[i] - values[i]) / 10.0);
            sumTraffic += Math.Pow(10, (traffic[i]   - values[i]) / 10.0);
        }

        double c   = sumPink    > 0 ? -10 * Math.Log10(sumPink)    - rw : 0;
        double ctr = sumTraffic > 0 ? -10 * Math.Log10(sumTraffic) - rw : 0;

        return (Math.Round(rw, 1), Math.Round(c), Math.Round(ctr), false);
    }

    // ─── Tahmin belirsizliği ───────────────────────────────────────────────────
    // TS EN 12354-1 Table B.1: Tek yerden iletim için standart sapma σ ≈ 2 dB
    public const double HesapBelirsizligi = 2.0;
}

public record FlankingYoluSonucu(
    JunctionKenarTipi Kenar,
    string YolTipi,
    double Rij,
    double Kij,
    double KaynakRw,
    double AliciRw);
