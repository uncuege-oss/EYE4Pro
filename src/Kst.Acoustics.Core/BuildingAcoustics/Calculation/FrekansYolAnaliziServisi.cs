using Kst.Acoustics.Core.BuildingAcoustics.Models;

namespace Kst.Acoustics.Core.BuildingAcoustics.Calculation;

public enum FrekansYolTuru
{
    OpakDirekt,
    Aciklik,
    Menfez,
    Df,
    Fd,
    Ff,
}

public sealed record FrekansYolBantKatkisi(
    int FrekansHz,
    double RijDb,
    double KijDb,
    double IletimKatsayisi,
    double KatkiYuzde);

public sealed record FrekansIletimYolu(
    string YolId,
    FrekansYolTuru Tur,
    string Ad,
    Guid? HedefId,
    JunctionKenarTipi? Kenar,
    IReadOnlyList<FrekansYolBantKatkisi> Bantlar)
{
    public double OrtalamaKatkiYuzde => Bantlar.Count == 0 ? 0 : Bantlar.Average(x => x.KatkiYuzde);
    public double EnYuksekKatkiYuzde => Bantlar.Count == 0 ? 0 : Bantlar.Max(x => x.KatkiYuzde);
}

public sealed record FrekansYolBandi(
    int FrekansHz,
    double ToplamRDb,
    double ToplamIletimKatsayisi,
    string BaskinYolId,
    string BaskinYol,
    double BaskinYolKatkiYuzde);

/// <summary>
/// EN 12354 enerji toplamını görünür kılan, 100–3150 Hz yol çözümlemesi.
/// Her bantta yol katkılarının toplamı yüzde 100'dür ve ToplamRDb, kanonik
/// hava sesi motorunun R'(f) değeriyle aynıdır.
/// </summary>
public sealed record FrekansYolAnalizSonucu(
    Guid BolmeId,
    string BolmeAdi,
    IReadOnlyList<int> FrekanslarHz,
    IReadOnlyList<FrekansYolBandi> Bantlar,
    IReadOnlyList<FrekansIletimYolu> Yollar,
    double Rw,
    double C,
    double Ctr,
    string SpektrumKaynagi,
    string GirdiParmakIzi)
{
    public FrekansIletimYolu? BaskinYol => Yollar.FirstOrDefault();
}

public sealed class FrekansYolAnaliziServisi
{
    private readonly BinaAkustikDegerlendirmeServisi _degerlendirme = new();

    private sealed record HamKatki(
        string Id,
        FrekansYolTuru Tur,
        string Ad,
        Guid? HedefId,
        JunctionKenarTipi? Kenar,
        int Bant,
        double Rij,
        double Kij,
        double Tau);

    /// <summary>
    /// Projeyi derin kopyada hesaplar; kaynak proje, bölme ve junction koleksiyonları
    /// değiştirilmez.
    /// </summary>
    public FrekansYolAnalizSonucu AnalizEt(BinaProje proje, Guid bolmeId)
    {
        ArgumentNullException.ThrowIfNull(proje);
        var sonuc = _degerlendirme.Hesapla(proje);
        var bolme = TumBolmeler(sonuc.HesaplananProje).FirstOrDefault(b => b.Id == bolmeId)
            ?? throw new KeyNotFoundException($"Frekans analizi için bölme bulunamadı: {bolmeId}");
        return AnalizEtHesaplanmisBolme(bolme, sonuc.GirdiParmakIzi);
    }

    /// <summary>Yalnız analiz kopyası üzerinde kullanılmak üzere hesaplanmış bölmeyi çözümler.</summary>
    internal static FrekansYolAnalizSonucu AnalizEtHesaplanmisBolme(Bolme bolme, string parmakIzi = "")
    {
        if (bolme.YapiElemani is null || bolme.Alan <= 0)
            throw new InvalidOperationException("Frekans/yol analizi için yapı elemanı ve pozitif bölme alanı gereklidir.");

        var frekanslar = OktavBantElemanServisi.UcuncuOktavFreqler;
        var solidR = OktavBantElemanServisi.GetRUcuncuOktavDizisi(bolme.YapiElemani);
        var aciklikAlani = bolme.Acikliklar.Sum(a => Math.Max(0, a.Alan));
        var opakAlan = Math.Max(0, bolme.Alan - aciklikAlani);
        var toplamAlan = Math.Max(bolme.Alan, opakAlan + aciklikAlani);
        if (toplamAlan <= 0)
            throw new InvalidOperationException("Frekans/yol analizi için toplam alan sıfır olamaz.");

        var ham = new List<HamKatki>();
        for (var i = 0; i < frekanslar.Length; i++)
        {
            if (opakAlan > 0)
            {
                var tau = opakAlan / toplamAlan * Math.Pow(10, -solidR[i] / 10.0);
                ham.Add(new("opak", FrekansYolTuru.OpakDirekt, "Opak ayırıcı", bolme.Id,
                    null, i, solidR[i], 0, tau));
            }

            foreach (var aciklik in bolme.Acikliklar)
            {
                var r = aciklik.YapiElemani is null
                    ? aciklik.Tip == BolmeAcikligiTipi.Kapi ? 28.0 : 32.0
                    : OktavBantElemanServisi.GetRUcuncuOktavDizisi(aciklik.YapiElemani)[i];
                var tau = Math.Max(0, aciklik.Alan) / toplamAlan * Math.Pow(10, -r / 10.0);
                ham.Add(new($"aciklik:{aciklik.Id:N}", FrekansYolTuru.Aciklik,
                    $"{aciklik.Tip}: {aciklik.YapiElemani?.Ad ?? "varsayılan"}", aciklik.Id,
                    null, i, r, 0, tau));
            }

            foreach (var menfez in bolme.Menfezler)
            {
                var dne = menfez.HasSpectrum ? menfez.DneThirdOctave[i] : menfez.DneDb;
                var tau = MenfezVarsayilan.A0 / toplamAlan * Math.Pow(10, -dne / 10.0);
                ham.Add(new($"menfez:{menfez.Id:N}", FrekansYolTuru.Menfez,
                    $"Menfez: {(string.IsNullOrWhiteSpace(menfez.Ad) ? menfez.Tip : menfez.Ad)}",
                    menfez.Id, null, i, dne, 0, tau));
            }

            if (bolme.KaynakMekan is not null && bolme.AliciMekan is not null)
            {
                foreach (var yol in HavaSesiHesapServisi.GetUcuncuOktavFlankingYollari(bolme, i, solidR[i]))
                {
                    var tur = yol.YolTipi switch
                    {
                        "Df" => FrekansYolTuru.Df,
                        "Fd" => FrekansYolTuru.Fd,
                        _ => FrekansYolTuru.Ff,
                    };
                    ham.Add(new($"yanal:{yol.JunctionId:N}:{yol.YolTipi}", tur,
                        $"{yol.Kenar} {yol.YolTipi} yanal yolu", yol.JunctionId, yol.Kenar,
                        i, yol.Rij, yol.Kij, Math.Pow(10, -yol.Rij / 10.0)));
                }
            }
        }

        var toplamTaular = Enumerable.Range(0, frekanslar.Length)
            .Select(i => ham.Where(x => x.Bant == i).Sum(x => x.Tau))
            .ToArray();
        var bantlar = Enumerable.Range(0, frekanslar.Length).Select(i =>
        {
            var bantYollari = ham.Where(x => x.Bant == i).ToList();
            var toplam = toplamTaular[i];
            var baskin = bantYollari.OrderByDescending(x => x.Tau).FirstOrDefault();
            return new FrekansYolBandi(
                frekanslar[i],
                toplam > 0 ? -10 * Math.Log10(toplam) : 0,
                toplam,
                baskin?.Id ?? "",
                baskin?.Ad ?? "—",
                baskin is null || toplam <= 0 ? 0 : 100 * baskin.Tau / toplam);
        }).ToList();

        var yollar = ham.GroupBy(x => new { x.Id, x.Tur, x.Ad, x.HedefId, x.Kenar })
            .Select(g => new FrekansIletimYolu(
                g.Key.Id, g.Key.Tur, g.Key.Ad, g.Key.HedefId, g.Key.Kenar,
                g.OrderBy(x => x.Bant).Select(x => new FrekansYolBantKatkisi(
                    frekanslar[x.Bant], x.Rij, x.Kij, x.Tau,
                    toplamTaular[x.Bant] <= 0 ? 0 : 100 * x.Tau / toplamTaular[x.Bant])).ToList()))
            .OrderByDescending(x => x.OrtalamaKatkiYuzde)
            .ThenBy(x => x.Ad, StringComparer.CurrentCulture)
            .ToList();

        var toplamR = bantlar.Select(x => x.ToplamRDb).ToArray();
        var (rw, c, ctr, _) = new HavaSesiHesapServisi().CalculateSingleValue(toplamR);
        return new FrekansYolAnalizSonucu(
            bolme.Id, bolme.Ad, frekanslar, bantlar, yollar, rw, c, ctr,
            OktavBantElemanServisi.HavaSpektrumKaynagiEtiketi(bolme.YapiElemani), parmakIzi);
    }

    private static IEnumerable<Bolme> TumBolmeler(BinaProje proje) =>
        proje.Katlar.SelectMany(k => k.Mekanlar).SelectMany(m => m.Bolmeler);
}
