using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.BuildingAcoustics.Reports;

namespace Kst.Acoustics.Core.BuildingAcoustics.Calculation;

public enum BelirsizlikParametreTuru
{
    DisGurultuDb,
    BolmeRwOfsetiDb,
    AciklikRwOfsetiDb,
    MenfezDneOfsetiDb,
    JunctionKijOverrideOfsetiDb,
    MekanYukseklikOrani,
    MekanEmilimAlaniOfsetiM2,
    DosemeLnwOfsetiDb,
    DosemeDeltaLwOfsetiDb,
}

public enum BelirsizlikDagilimi
{
    Normal,
    Uniform,
}

/// <summary>
/// Belirsizlik parametresinin değeri baz girdiye EKLENEN ofsettir. Oran türlerinde
/// 0.05, baz değerin yüzde 5 artırılması anlamına gelir.
/// </summary>
public sealed record BelirsizlikParametresi(
    string Ad,
    BelirsizlikParametreTuru Tur,
    double StandartSapma,
    Guid? HedefId = null,
    double MinimumOfset = double.NegativeInfinity,
    double MaksimumOfset = double.PositiveInfinity,
    BelirsizlikDagilimi Dagilim = BelirsizlikDagilimi.Normal);

public sealed record DuyarlilikSatiri(
    string Parametre,
    BelirsizlikParametreTuru Tur,
    double StandartSapma,
    double EksiSonuc,
    double ArtiSonuc,
    double BirimBasinaTurev,
    double BirStandartSapmaEtkisi,
    double EtkiBuyuklugu,
    string Yorum);

public sealed record DuyarlilikAnalizSonucu(
    Guid SonucId,
    AkustikRaporSonucTuru SonucTuru,
    double TemelDeger,
    double TemelMarj,
    IReadOnlyList<DuyarlilikSatiri> Parametreler,
    string GirdiParmakIzi);

public sealed record MonteCarloAnalizSonucu(
    Guid SonucId,
    AkustikRaporSonucTuru SonucTuru,
    int Seed,
    int IstenenTekrar,
    int GecerliTekrar,
    int BasarisizTekrar,
    double Ortalama,
    double StandartSapma,
    double P05,
    double P50,
    double P95,
    double UygunlukOlasiligi,
    double OrtalamaMarj,
    IReadOnlyList<double> OrnekSonuclar,
    string GirdiParmakIzi);

/// <summary>
/// Merkezî fark duyarlılığı ve tekrarlanabilir Monte Carlo analizi. Her deneme
/// bağımsız derin kopyada kanonik M08 hesap motorundan geçirilir.
/// </summary>
public sealed class BelirsizlikAnalizServisi
{
    private readonly BinaAkustikDegerlendirmeServisi _degerlendirme = new();

    public DuyarlilikAnalizSonucu DuyarlilikAnalizi(
        BinaProje proje,
        Guid sonucId,
        IReadOnlyList<BelirsizlikParametresi> parametreler,
        AkustikRaporSonucTuru? sonucTuru = null,
        CancellationToken iptal = default,
        IProgress<double>? ilerleme = null)
    {
        ArgumentNullException.ThrowIfNull(proje);
        ArgumentNullException.ThrowIfNull(parametreler);
        var temel = _degerlendirme.Hesapla(proje);
        var temelSatir = temel.SonucBul(sonucId, sonucTuru)
            ?? throw new KeyNotFoundException($"Duyarlılık hedef sonucu bulunamadı: {sonucId}");
        var satirlar = new List<DuyarlilikSatiri>();

        for (var i = 0; i < parametreler.Count; i++)
        {
            iptal.ThrowIfCancellationRequested();
            var p = parametreler[i];
            Dogrula(p);
            var adim = p.StandartSapma;
            var eksiOfset = Math.Clamp(-adim, p.MinimumOfset, p.MaksimumOfset);
            var artiOfset = Math.Clamp(adim, p.MinimumOfset, p.MaksimumOfset);
            if (Math.Abs(artiOfset - eksiOfset) < 1e-12)
                throw new ArgumentException($"{p.Ad}: duyarlılık için artı/eksi aralığı sıfırdır.", nameof(parametreler));

            var eksi = Degerlendir(proje, sonucId, temelSatir.SonucTuru, [(p, eksiOfset)]);
            var arti = Degerlendir(proje, sonucId, temelSatir.SonucTuru, [(p, artiOfset)]);
            var turev = (arti.Deger - eksi.Deger) / (artiOfset - eksiOfset);
            var sigmaEtkisi = turev * p.StandartSapma;
            satirlar.Add(new(p.Ad, p.Tur, p.StandartSapma, eksi.Deger, arti.Deger,
                turev, sigmaEtkisi, Math.Abs(sigmaEtkisi), Yorumla(temelSatir, sigmaEtkisi)));
            ilerleme?.Report((i + 1d) / Math.Max(1, parametreler.Count));
        }

        return new DuyarlilikAnalizSonucu(sonucId, temelSatir.SonucTuru,
            temelSatir.Deger, temelSatir.MarjDb,
            satirlar.OrderByDescending(x => x.EtkiBuyuklugu).ToList(), temel.GirdiParmakIzi);
    }

    public MonteCarloAnalizSonucu MonteCarlo(
        BinaProje proje,
        Guid sonucId,
        IReadOnlyList<BelirsizlikParametresi> parametreler,
        int tekrar = 1000,
        int seed = 2026,
        AkustikRaporSonucTuru? sonucTuru = null,
        CancellationToken iptal = default,
        IProgress<double>? ilerleme = null)
    {
        ArgumentNullException.ThrowIfNull(proje);
        ArgumentNullException.ThrowIfNull(parametreler);
        if (tekrar is < 10 or > 100_000)
            throw new ArgumentOutOfRangeException(nameof(tekrar), "Monte Carlo tekrar sayısı 10–100000 olmalıdır.");
        foreach (var p in parametreler) Dogrula(p);

        var temel = _degerlendirme.Hesapla(proje);
        var temelSatir = temel.SonucBul(sonucId, sonucTuru)
            ?? throw new KeyNotFoundException($"Monte Carlo hedef sonucu bulunamadı: {sonucId}");
        var random = new Random(seed);
        var degerler = new List<double>(tekrar);
        var marjlar = new List<double>(tekrar);
        var uygun = 0;
        var basarisiz = 0;

        for (var i = 0; i < tekrar; i++)
        {
            iptal.ThrowIfCancellationRequested();
            try
            {
                var ofsetler = parametreler.Select(p => (p, Ornekle(random, p))).ToList();
                var satir = Degerlendir(proje, sonucId, temelSatir.SonucTuru, ofsetler);
                if (!double.IsFinite(satir.Deger))
                {
                    basarisiz++;
                    continue;
                }
                degerler.Add(satir.Deger);
                if (!double.IsNaN(satir.MarjDb)) marjlar.Add(satir.MarjDb);
                if (satir.Durum == nameof(UyumDurumu.Gecti) || (!double.IsNaN(satir.MarjDb) && satir.MarjDb >= 0))
                    uygun++;
            }
            catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or ArgumentException)
            {
                basarisiz++;
            }
            if (i % Math.Max(1, tekrar / 100) == 0 || i + 1 == tekrar)
                ilerleme?.Report((i + 1d) / tekrar);
        }

        if (degerler.Count == 0)
            throw new InvalidOperationException("Monte Carlo analizinde geçerli sonuç üretilemedi.");
        degerler.Sort();
        var ortalama = degerler.Average();
        var varyans = degerler.Count > 1
            ? degerler.Sum(x => Math.Pow(x - ortalama, 2)) / (degerler.Count - 1)
            : 0;
        return new MonteCarloAnalizSonucu(
            sonucId, temelSatir.SonucTuru, seed, tekrar, degerler.Count, basarisiz,
            ortalama, Math.Sqrt(varyans), Quantile(degerler, 0.05), Quantile(degerler, 0.50),
            Quantile(degerler, 0.95), uygun / (double)degerler.Count,
            marjlar.Count == 0 ? double.NaN : marjlar.Average(), degerler, temel.GirdiParmakIzi);
    }

    private RaporSatiri Degerlendir(
        BinaProje kaynak,
        Guid sonucId,
        AkustikRaporSonucTuru tur,
        IReadOnlyList<(BelirsizlikParametresi Parametre, double Ofset)> ofsetler)
    {
        var clone = BinaAkustikDegerlendirmeServisi.DerinKlonla(kaynak);
        foreach (var (parametre, ofset) in ofsetler)
            Uygula(clone, parametre, ofset);
        return _degerlendirme.Hesapla(clone).SonucBul(sonucId, tur)
            ?? throw new KeyNotFoundException($"Belirsizlik denemesinde hedef sonuç kayboldu: {sonucId}");
    }

    private static void Uygula(BinaProje proje, BelirsizlikParametresi p, double ofset)
    {
        switch (p.Tur)
        {
            case BelirsizlikParametreTuru.DisGurultuDb:
                proje.DisGurultuDb = Math.Max(0, proje.DisGurultuDb + ofset);
                break;
            case BelirsizlikParametreTuru.BolmeRwOfsetiDb:
                ElemanBul(TumBolmeler(proje).FirstOrDefault(x => x.Id == Hedef(p))?.YapiElemani, p).DeltaRDb += ofset;
                break;
            case BelirsizlikParametreTuru.AciklikRwOfsetiDb:
                ElemanBul(TumBolmeler(proje).SelectMany(x => x.Acikliklar)
                    .FirstOrDefault(x => x.Id == Hedef(p))?.YapiElemani, p).DeltaRDb += ofset;
                break;
            case BelirsizlikParametreTuru.MenfezDneOfsetiDb:
            {
                var menfez = TumBolmeler(proje).SelectMany(x => x.Menfezler).FirstOrDefault(x => x.Id == Hedef(p))
                    ?? throw HedefHatasi(p);
                menfez.DneDb = Math.Max(0, menfez.DneDb + ofset);
                if (menfez.HasSpectrum)
                    for (var i = 0; i < menfez.DneThirdOctave.Count; i++)
                        menfez.DneThirdOctave[i] = Math.Max(0, menfez.DneThirdOctave[i] + ofset);
                break;
            }
            case BelirsizlikParametreTuru.JunctionKijOverrideOfsetiDb:
            {
                var junction = TumBolmeler(proje).SelectMany(x => x.Junctions).FirstOrDefault(x => x.Id == Hedef(p))
                    ?? throw HedefHatasi(p);
                if (junction.KijOverrideDb <= 0)
                    throw new InvalidOperationException($"{p.Ad}: otomatik Kij için ofset uygulanamaz; önce ölçülmüş/özel Kij override girilmelidir.");
                junction.KijOverrideDb = Math.Max(0.01, junction.KijOverrideDb + ofset);
                break;
            }
            case BelirsizlikParametreTuru.MekanYukseklikOrani:
            {
                var mekan = MekanBul(proje, p);
                mekan.Yukseklik = Math.Max(0.1, mekan.Yukseklik * (1 + ofset));
                break;
            }
            case BelirsizlikParametreTuru.MekanEmilimAlaniOfsetiM2:
            {
                var mekan = MekanBul(proje, p);
                mekan.EmilimAlani = Math.Max(0, mekan.EmilimAlani + ofset);
                break;
            }
            case BelirsizlikParametreTuru.DosemeLnwOfsetiDb:
                DarbeElemaniBul(proje, p).Lnw += ofset;
                break;
            case BelirsizlikParametreTuru.DosemeDeltaLwOfsetiDb:
                DarbeElemaniBul(proje, p).DeltaLw += ofset;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(p.Tur));
        }
    }

    private static YapiElemani DarbeElemaniBul(BinaProje proje, BelirsizlikParametresi p)
    {
        var id = Hedef(p);
        var mekan = proje.Katlar.SelectMany(k => k.Mekanlar).FirstOrDefault(x => x.Id == id);
        var eleman = mekan?.ZeminYapiElemani ?? TumBolmeler(proje).FirstOrDefault(x => x.Id == id)?.YapiElemani;
        return ElemanBul(eleman, p);
    }

    private static Mekan MekanBul(BinaProje proje, BelirsizlikParametresi p) =>
        proje.Katlar.SelectMany(k => k.Mekanlar).FirstOrDefault(x => x.Id == Hedef(p)) ?? throw HedefHatasi(p);

    private static YapiElemani ElemanBul(YapiElemani? eleman, BelirsizlikParametresi p) => eleman ?? throw HedefHatasi(p);
    private static Guid Hedef(BelirsizlikParametresi p) => p.HedefId ?? throw HedefHatasi(p);
    private static KeyNotFoundException HedefHatasi(BelirsizlikParametresi p) =>
        new($"{p.Ad}: belirsizlik parametresi hedefi bulunamadı.");

    private static double Ornekle(Random random, BelirsizlikParametresi p)
    {
        double x;
        if (p.Dagilim == BelirsizlikDagilimi.Uniform)
        {
            var yariAralik = Math.Sqrt(3) * p.StandartSapma;
            x = (2 * random.NextDouble() - 1) * yariAralik;
        }
        else
        {
            var u1 = Math.Max(double.Epsilon, random.NextDouble());
            var u2 = random.NextDouble();
            x = Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2) * p.StandartSapma;
        }
        return Math.Clamp(x, p.MinimumOfset, p.MaksimumOfset);
    }

    private static void Dogrula(BelirsizlikParametresi p)
    {
        if (string.IsNullOrWhiteSpace(p.Ad)) throw new ArgumentException("Belirsizlik parametresi adı boş olamaz.");
        if (!double.IsFinite(p.StandartSapma) || p.StandartSapma <= 0)
            throw new ArgumentOutOfRangeException(nameof(p.StandartSapma), $"{p.Ad}: standart sapma pozitif olmalıdır.");
        if (p.MinimumOfset > p.MaksimumOfset)
            throw new ArgumentException($"{p.Ad}: minimum ofset maksimumdan büyük olamaz.");
    }

    private static double Quantile(IReadOnlyList<double> sorted, double q)
    {
        if (sorted.Count == 1) return sorted[0];
        var pos = Math.Clamp(q, 0, 1) * (sorted.Count - 1);
        var alt = (int)Math.Floor(pos);
        var ust = (int)Math.Ceiling(pos);
        if (alt == ust) return sorted[alt];
        return sorted[alt] + (sorted[ust] - sorted[alt]) * (pos - alt);
    }

    private static string Yorumla(RaporSatiri satir, double sigmaEtkisi)
    {
        if (Math.Abs(sigmaEtkisi) < 0.05) return "Sonuca etkisi ihmal edilebilir.";
        var sonucuIyilestirir = satir.DahaDusukDahaIyi ? sigmaEtkisi < 0 : sigmaEtkisi > 0;
        return sonucuIyilestirir
            ? $"Parametrede +1σ değişim sonucu yaklaşık {Math.Abs(sigmaEtkisi):F2} {satir.Birim} iyileştirir."
            : $"Parametrede +1σ değişim sonucu yaklaşık {Math.Abs(sigmaEtkisi):F2} {satir.Birim} kötüleştirir.";
    }

    private static IEnumerable<Bolme> TumBolmeler(BinaProje proje) =>
        proje.Katlar.SelectMany(k => k.Mekanlar).SelectMany(m => m.Bolmeler);
}
