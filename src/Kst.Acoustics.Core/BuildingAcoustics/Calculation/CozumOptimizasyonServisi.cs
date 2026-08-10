using System.Text.Json;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.BuildingAcoustics.Reports;

namespace Kst.Acoustics.Core.BuildingAcoustics.Calculation;

public enum CozumDegisiklikTuru
{
    BolmeYapiElemani,
    AciklikYapiElemani,
    MekanZeminYapiElemani,
}

public sealed record CozumOptimizasyonIstegi(
    Guid SonucId,
    AkustikRaporSonucTuru? SonucTuru = null,
    int AzamiCozumSayisi = 10,
    int AzamiDegisiklikSayisi = 2,
    int HerHedefIcinAzamiKatalogAdayi = 16,
    decimal? AzamiToplamMaliyet = null,
    double? AzamiToplamKalinlikArtisiMm = null,
    bool YalnizUygunCozumler = false);

public sealed record CozumDegisikligi(
    CozumDegisiklikTuru Tur,
    Guid HedefId,
    Guid? MekanId,
    Guid YeniYapiElemaniId,
    string OncekiYapiElemani,
    string YeniYapiElemani,
    YapiElemani YeniYapiElemaniVerisi,
    double Miktar,
    decimal TahminiTutar,
    string ParaBirimi);

public sealed record CozumOptimizasyonAdayi(
    int Sira,
    IReadOnlyList<CozumDegisikligi> Degisiklikler,
    double SonucDegeri,
    double Limit,
    double Marj,
    bool Uygun,
    decimal ToplamMaliyet,
    string ParaBirimi,
    double ToplamKalinlikArtisiMm,
    double ToplamKutleArtisiKgM2,
    string Ozet);

public sealed record CozumOptimizasyonSonucu(
    RaporSatiri TemelSonuc,
    IReadOnlyList<CozumOptimizasyonAdayi> Cozumler,
    int DegerlendirilenKombinasyonSayisi,
    bool UygunCozumBulundu,
    string GirdiParmakIzi,
    IReadOnlyList<string> Uyarilar);

/// <summary>
/// Sabit dB eklemek yerine gerçek YapiElemani katalog kayıtlarını analiz kopyasına
/// atar, kanonik rapor motoruyla yeniden hesaplar ve maliyet/kalınlık/marj açısından
/// sıralar. Kaynak proje ve katalog nesneleri hiçbir zaman değiştirilmez.
/// </summary>
public sealed class CozumOptimizasyonServisi
{
    private readonly BinaAkustikDegerlendirmeServisi _degerlendirme = new();

    public CozumOptimizasyonSonucu OptimizeEt(
        BinaProje proje,
        IReadOnlyList<YapiElemani> katalog,
        CozumOptimizasyonIstegi istek,
        CancellationToken iptal = default,
        IProgress<double>? ilerleme = null)
    {
        ArgumentNullException.ThrowIfNull(proje);
        ArgumentNullException.ThrowIfNull(katalog);
        ArgumentNullException.ThrowIfNull(istek);

        var temel = _degerlendirme.Hesapla(proje);
        var temelSatir = temel.SonucBul(istek.SonucId, istek.SonucTuru)
            ?? throw new KeyNotFoundException($"Optimize edilecek M08 sonucu bulunamadı: {istek.SonucId}");
        if (temelSatir.SonucTuru is AkustikRaporSonucTuru.TesisatSurekli or
            AkustikRaporSonucTuru.TesisatKesikli or AkustikRaporSonucTuru.Reverberasyon)
            return BosSonuc(temelSatir, temel.GirdiParmakIzi,
                "Bu sonuç türünde yapı elemanı kataloğuyla değiştirilebilen bir iletim yolu yoktur.");

        var atomikler = AtomikDegisiklikleriOlustur(
            temel.HesaplananProje, katalog, temelSatir, Math.Max(1, istek.HerHedefIcinAzamiKatalogAdayi));
        if (atomikler.Count == 0)
            return BosSonuc(temelSatir, temel.GirdiParmakIzi,
                "Hedefe uygun ve mevcut elemandan farklı katalog adayı bulunamadı.");

        var kombinasyonlar = new List<IReadOnlyList<CozumDegisikligi>>();
        kombinasyonlar.AddRange(atomikler.Select(x => (IReadOnlyList<CozumDegisikligi>)[x]));
        if (istek.AzamiDegisiklikSayisi >= 2)
        {
            // Kombinasyon patlamasını sınırlamak için her fiziksel hedefin akustik açıdan
            // en güçlü ve en ekonomik ilk adaylarını bir beam havuzunda tut.
            var beam = atomikler
                .GroupBy(HedefAnahtari)
                .SelectMany(g => g.OrderBy(AkustikOncelik).ThenBy(x => x.TahminiTutar).Take(4))
                .Take(24)
                .ToList();
            for (var i = 0; i < beam.Count; i++)
            for (var j = i + 1; j < beam.Count; j++)
            {
                if (HedefAnahtari(beam[i]) == HedefAnahtari(beam[j])) continue;
                kombinasyonlar.Add([beam[i], beam[j]]);
            }
        }

        var degerlendirilen = new List<CozumOptimizasyonAdayi>();
        for (var i = 0; i < kombinasyonlar.Count; i++)
        {
            iptal.ThrowIfCancellationRequested();
            var degisiklikler = kombinasyonlar[i];
            var maliyet = degisiklikler.Sum(x => x.TahminiTutar);
            if (istek.AzamiToplamMaliyet is { } maxMaliyet && maliyet > maxMaliyet) continue;

            var analizKopyasi = BinaAkustikDegerlendirmeServisi.DerinKlonla(proje);
            foreach (var degisiklik in degisiklikler)
                DegisikligiUygula(analizKopyasi, degisiklik);

            var adaySonucu = _degerlendirme.Hesapla(analizKopyasi);
            var satir = adaySonucu.SonucBul(istek.SonucId, temelSatir.SonucTuru);
            if (satir is null) continue;

            var kalinlik = KalinlikArtisi(temel.HesaplananProje, degisiklikler);
            if (istek.AzamiToplamKalinlikArtisiMm is { } maxKalinlik && kalinlik > maxKalinlik) continue;
            var kutle = KutleArtisi(temel.HesaplananProje, degisiklikler);
            var uygun = satir.Durum == nameof(UyumDurumu.Gecti) || (!double.IsNaN(satir.MarjDb) && satir.MarjDb >= 0);
            degerlendirilen.Add(new(0, degisiklikler, satir.Deger, satir.LimitDeger,
                satir.MarjDb, uygun, maliyet, ParaBirimi(degisiklikler), kalinlik, kutle,
                Ozetle(degisiklikler, satir)));
            ilerleme?.Report((i + 1d) / kombinasyonlar.Count);
        }

        IEnumerable<CozumOptimizasyonAdayi> sirali = degerlendirilen;
        if (istek.YalnizUygunCozumler)
            sirali = sirali.Where(x => x.Uygun);
        sirali = sirali
            .OrderByDescending(x => x.Uygun)
            .ThenByDescending(x => double.IsNaN(x.Marj) ? double.NegativeInfinity : x.Marj)
            .ThenBy(x => x.ToplamMaliyet)
            .ThenBy(x => x.ToplamKalinlikArtisiMm)
            .ThenBy(x => x.Degisiklikler.Count);

        var secilen = sirali.Take(Math.Max(1, istek.AzamiCozumSayisi))
            .Select((x, index) => x with { Sira = index + 1 })
            .ToList();
        var uyarilar = new List<string>();
        if (!degerlendirilen.Any(x => x.Uygun))
            uyarilar.Add("Verilen katalog ve kısıtlarla yönetmelik limitini sağlayan çözüm bulunamadı; en iyi marjlı adaylar gösteriliyor.");
        if (katalog.Any(x => x.BirimFiyat <= 0))
            uyarilar.Add("Bazı katalog kayıtlarında birim fiyat bulunmadığından maliyet sıralaması eksik olabilir.");

        return new CozumOptimizasyonSonucu(temelSatir, secilen, degerlendirilen.Count,
            degerlendirilen.Any(x => x.Uygun), temel.GirdiParmakIzi, uyarilar);
    }

    /// <summary>Seçilen çözümü canlı projeye değil, yeni bir derin kopyaya uygular.</summary>
    public BinaProje KlonUzerindeUygula(BinaProje proje, CozumOptimizasyonAdayi cozum)
    {
        ArgumentNullException.ThrowIfNull(proje);
        ArgumentNullException.ThrowIfNull(cozum);
        var clone = BinaAkustikDegerlendirmeServisi.DerinKlonla(proje);
        foreach (var degisiklik in cozum.Degisiklikler)
            DegisikligiUygula(clone, degisiklik);
        return clone;
    }

    private static CozumOptimizasyonSonucu BosSonuc(RaporSatiri temel, string parmakIzi, string uyari) =>
        new(temel, [], 0, false, parmakIzi, [uyari]);

    private static List<CozumDegisikligi> AtomikDegisiklikleriOlustur(
        BinaProje proje,
        IReadOnlyList<YapiElemani> katalog,
        RaporSatiri satir,
        int limit)
    {
        var result = new List<CozumDegisikligi>();
        if (satir.SonucTuru == AkustikRaporSonucTuru.DarbeSesi)
        {
            var kaynak = DarbeKaynaginiBul(proje, satir.SonucId);
            if (kaynak?.ZeminYapiElemani is { } mevcut)
                result.AddRange(Adaylar(katalog, mevcut, YapiElemaniTipi.Doseme, false, limit)
                    .Select(x => Degisiklik(CozumDegisiklikTuru.MekanZeminYapiElemani,
                        kaynak.Id, kaynak.Id, mevcut, x, kaynak.Alan, proje)));
            return result;
        }

        var hedefBolmeler = satir.SonucTuru == AkustikRaporSonucTuru.DisCephe
            ? proje.Katlar.SelectMany(k => k.Mekanlar)
                .Where(m => RaporSonucIdleri.DisCephe(m.Id) == satir.SonucId)
                .SelectMany(m => m.Bolmeler.Where(b => b.IsDisCephe)).ToList()
            : TumBolmeler(proje).Where(b => b.Id == satir.SonucId).ToList();

        foreach (var bolme in hedefBolmeler)
        {
            if (bolme.YapiElemani is { } mevcut)
            {
                result.AddRange(Adaylar(katalog, mevcut, mevcut.Tip, true, limit)
                    .Select(x => Degisiklik(CozumDegisiklikTuru.BolmeYapiElemani,
                        bolme.Id, bolme.KaynakMekan?.Id, mevcut, x,
                        Math.Max(0, bolme.NetOpakAlan), proje)));
            }
            foreach (var aciklik in bolme.Acikliklar.Where(a => a.YapiElemani is not null))
            {
                var mevcutAciklik = aciklik.YapiElemani!;
                var tip = aciklik.Tip == BolmeAcikligiTipi.Kapi ? YapiElemaniTipi.Kapi : YapiElemaniTipi.Pencere;
                result.AddRange(Adaylar(katalog, mevcutAciklik, tip, true, limit)
                    .Select(x => Degisiklik(CozumDegisiklikTuru.AciklikYapiElemani,
                        aciklik.Id, bolme.KaynakMekan?.Id, mevcutAciklik, x,
                        Math.Max(0, aciklik.Alan), proje)));
            }
        }
        return result;
    }

    private static IEnumerable<YapiElemani> Adaylar(
        IReadOnlyList<YapiElemani> katalog,
        YapiElemani mevcut,
        YapiElemaniTipi tip,
        bool yuksekDahaIyi,
        int limit)
    {
        var mevcutSkor = yuksekDahaIyi ? mevcut.EffectiveRw : mevcut.Lnw - mevcut.DeltaLw;
        return katalog.Where(x => x is not null && x.Tip == tip && !AyniEleman(x, mevcut))
            .Where(x => yuksekDahaIyi
                ? x.EffectiveRw > mevcutSkor + 0.01
                : x.Lnw - x.DeltaLw < mevcutSkor - 0.01)
            .OrderBy(x => yuksekDahaIyi ? -x.EffectiveRw : x.Lnw - x.DeltaLw)
            .ThenBy(x => x.BirimFiyat <= 0 ? decimal.MaxValue : x.BirimFiyat)
            .Take(limit);
    }

    private static CozumDegisikligi Degisiklik(
        CozumDegisiklikTuru tur,
        Guid hedef,
        Guid? mekan,
        YapiElemani mevcut,
        YapiElemani yeni,
        double miktar,
        BinaProje proje)
    {
        var fire = yeni.FireYuzdesi > 0 ? yeni.FireYuzdesi : proje.MetrajAyarlari.VarsayilanFireYuzdesi;
        var tutar = yeni.BirimFiyat * (decimal)miktar * (1 + fire / 100m);
        return new(tur, hedef, mekan, EtkinId(yeni), mevcut.Ad, yeni.Ad, Klonla(yeni),
            miktar, decimal.Round(tutar, 2), string.IsNullOrWhiteSpace(yeni.ParaBirimi)
                ? proje.MetrajAyarlari.ParaBirimi : yeni.ParaBirimi);
    }

    private static void DegisikligiUygula(BinaProje proje, CozumDegisikligi d)
    {
        var yeni = Klonla(d.YeniYapiElemaniVerisi);
        switch (d.Tur)
        {
            case CozumDegisiklikTuru.BolmeYapiElemani:
                (TumBolmeler(proje).FirstOrDefault(x => x.Id == d.HedefId)
                    ?? throw new KeyNotFoundException($"Bölme hedefi bulunamadı: {d.HedefId}"))
                    .YapiElemani = yeni;
                break;
            case CozumDegisiklikTuru.AciklikYapiElemani:
                (TumBolmeler(proje).SelectMany(x => x.Acikliklar).FirstOrDefault(x => x.Id == d.HedefId)
                    ?? throw new KeyNotFoundException($"Açıklık hedefi bulunamadı: {d.HedefId}"))
                    .YapiElemani = yeni;
                break;
            case CozumDegisiklikTuru.MekanZeminYapiElemani:
                (proje.Katlar.SelectMany(k => k.Mekanlar).FirstOrDefault(x => x.Id == d.HedefId)
                    ?? throw new KeyNotFoundException($"Mekan hedefi bulunamadı: {d.HedefId}"))
                    .ZeminYapiElemani = yeni;
                break;
        }
    }

    private static Mekan? DarbeKaynaginiBul(BinaProje proje, Guid sonucId)
    {
        foreach (var kat in proje.Katlar)
        foreach (var mekan in kat.Mekanlar)
        foreach (var yol in new KatlarArasiDarbeYoluServisi().BulAltMekanlar(proje.Katlar, kat, mekan))
            if (KatlarArasiDarbeYoluServisi.DarbeYoluId(yol.KaynakMekan.Id, yol.AliciMekan.Id) == sonucId)
                return yol.KaynakMekan;
        return TumBolmeler(proje).FirstOrDefault(b => b.Id == sonucId)?.KaynakMekan;
    }

    private static string HedefAnahtari(CozumDegisikligi x) => $"{x.Tur}:{x.HedefId:N}";
    private static double AkustikOncelik(CozumDegisikligi x) => x.Tur == CozumDegisiklikTuru.MekanZeminYapiElemani
        ? x.YeniYapiElemaniVerisi.Lnw - x.YeniYapiElemaniVerisi.DeltaLw
        : -x.YeniYapiElemaniVerisi.EffectiveRw;

    private static double KalinlikArtisi(BinaProje temel, IReadOnlyList<CozumDegisikligi> ds) =>
        ds.Sum(d => Math.Max(0, d.YeniYapiElemaniVerisi.ToplamKalinlikMm - MevcutEleman(temel, d)?.ToplamKalinlikMm ?? 0));

    private static double KutleArtisi(BinaProje temel, IReadOnlyList<CozumDegisikligi> ds) =>
        ds.Sum(d => Math.Max(0, d.YeniYapiElemaniVerisi.ToplamYuzeyKutlesi - MevcutEleman(temel, d)?.ToplamYuzeyKutlesi ?? 0));

    private static YapiElemani? MevcutEleman(BinaProje proje, CozumDegisikligi d) => d.Tur switch
    {
        CozumDegisiklikTuru.BolmeYapiElemani => TumBolmeler(proje).FirstOrDefault(x => x.Id == d.HedefId)?.YapiElemani,
        CozumDegisiklikTuru.AciklikYapiElemani => TumBolmeler(proje).SelectMany(x => x.Acikliklar).FirstOrDefault(x => x.Id == d.HedefId)?.YapiElemani,
        _ => proje.Katlar.SelectMany(k => k.Mekanlar).FirstOrDefault(x => x.Id == d.HedefId)?.ZeminYapiElemani,
    };

    private static string Ozetle(IReadOnlyList<CozumDegisikligi> ds, RaporSatiri sonuc) =>
        $"{string.Join(" + ", ds.Select(x => x.YeniYapiElemani))}: {sonuc.Deger:F1} {sonuc.Birim}, marj {(double.IsNaN(sonuc.MarjDb) ? "—" : sonuc.MarjDb.ToString("+0.0;-0.0;0.0"))}";

    private static string ParaBirimi(IReadOnlyList<CozumDegisikligi> ds) =>
        ds.Select(x => x.ParaBirimi).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1
            ? ds[0].ParaBirimi : "Karma";

    private static Guid EtkinId(YapiElemani e) => e.Id == Guid.Empty ? M08KararliKimlik.YapiElemani(e) : e.Id;
    private static bool AyniEleman(YapiElemani a, YapiElemani b) =>
        EtkinId(a) == EtkinId(b) || (a.Tip == b.Tip && string.Equals(a.Ad, b.Ad, StringComparison.OrdinalIgnoreCase));

    private static YapiElemani Klonla(YapiElemani e) =>
        JsonSerializer.Deserialize<YapiElemani>(JsonSerializer.Serialize(e))
        ?? throw new InvalidOperationException("Katalog elemanı kopyalanamadı.");

    private static IEnumerable<Bolme> TumBolmeler(BinaProje proje) =>
        proje.Katlar.SelectMany(k => k.Mekanlar).SelectMany(m => m.Bolmeler);
}
