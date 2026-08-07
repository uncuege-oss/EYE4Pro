using System.Linq;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.BuildingAcoustics.Calculation;

public enum UyumDurumu
{
    Gecti,
    Kaldi,
    VeriEksik
}

/// <summary>
/// Yönetmelik (RG 31.05.2017/30082) EK-3 sınır değer kontrolü.
/// Limit seçimi (Madde 11):
///   • Döşeme       → Tablo 3.3 (kaynak derecesi × sınıf, L'nT,w); konutta aynı bağımsız
///                    birim içi döşeme → Tablo 3.5.
///   • Dış cephe    → Tablo 3.1 (Lgag − ofset, A–D'de 30 dB taban; DnT,A,tr).
///   • Ortak duvar  → Tablo 3.2 (kaynak × alıcı × sınıf, DnT,A); konutta aynı bağımsız
///                    birim içi bölme → Tablo 3.4.
/// U5 — Madde 11(3): kapı içeren bölme elemanlarında sınır değerden en fazla 14 dB
/// düşük yalıtıma izin verilir (yalnız iç bölme DnT,A yolunda uygulanır).
/// </summary>
public class YonetmelikUyumServisi
{
    /// Madde 11(3) — kapı içeren elemanlarda izin verilen tolerans (dB).
    public const double KapiToleransiDb = 14.0;

    public UyumDurumu KontrolEt(BinaSinifi sinif, Bolme bolme, double disGurultuLgag = 0)
        => KontrolEt(sinif, AkustikPerformansSinifi.C, bolme, disGurultuLgag);

    public UyumDurumu KontrolEt(BinaSinifi sinif, AkustikPerformansSinifi performansSinifi, Bolme bolme, double disGurultuLgag = 0)
    {
        if (bolme.YapiElemani == null) return UyumDurumu.VeriEksik;

        var tip = BolmeTipiBelirle(bolme);
        var limit = SecLimit(sinif, performansSinifi, tip, bolme, disGurultuLgag);
        if (limit.HedefDeger <= 0)
            return UyumDurumu.VeriEksik;

        if (tip == BolmeTipi.Doseme)
        {
            if (bolme.ApparentLnTw <= 0) return UyumDurumu.VeriEksik;
            // L'nT,w için: mevcut ≤ limit (küçük = daha iyi)
            return bolme.ApparentLnTw <= limit.HedefDeger ? UyumDurumu.Gecti : UyumDurumu.Kaldi;
        }

        double mevcutVal = HesaplaOlcumDegeri(bolme, limit);
        if (mevcutVal <= 0) return UyumDurumu.VeriEksik;
        return mevcutVal >= limit.HedefDeger ? UyumDurumu.Gecti : UyumDurumu.Kaldi;
    }

    /// UX — Limit'e göre dB marjı: pozitif = uygun, negatif = sınırın altında.
    /// • Doseme (L'nT,w küçük iyi): marj = limit − mevcut
    /// • Duvar/cephe (DnT,A büyük iyi): marj = mevcut − limit
    /// Veri eksikse double.NaN döner. UI 5 seviyeli renklendirme bu marjı kullanır.
    public double HesaplaMarjDb(BinaSinifi sinif, Bolme bolme, double disGurultuLgag = 0)
        => HesaplaMarjDb(sinif, AkustikPerformansSinifi.C, bolme, disGurultuLgag);

    public double HesaplaMarjDb(BinaSinifi sinif, AkustikPerformansSinifi performansSinifi, Bolme bolme, double disGurultuLgag = 0)
    {
        if (bolme.YapiElemani == null) return double.NaN;

        var tip = BolmeTipiBelirle(bolme);
        var limit = SecLimit(sinif, performansSinifi, tip, bolme, disGurultuLgag);
        if (limit.HedefDeger <= 0) return double.NaN;

        if (tip == BolmeTipi.Doseme)
        {
            if (bolme.ApparentLnTw <= 0) return double.NaN;
            return limit.HedefDeger - bolme.ApparentLnTw;
        }

        double mevcut = HesaplaOlcumDegeri(bolme, limit);
        if (mevcut <= 0) return double.NaN;
        return mevcut - limit.HedefDeger;
    }

    /// Limit değerini de döndüren overload — rapor ve UI için kullanışlı
    public (UyumDurumu Durum, AkustikLimit Limit) KontrolEtDetayli(BinaSinifi sinif, Bolme bolme, double disGurultuLgag = 0)
        => KontrolEtDetayli(sinif, AkustikPerformansSinifi.C, bolme, disGurultuLgag);

    public (UyumDurumu Durum, AkustikLimit Limit) KontrolEtDetayli(
        BinaSinifi sinif, AkustikPerformansSinifi performansSinifi, Bolme bolme, double disGurultuLgag = 0)
    {
        if (bolme.YapiElemani == null)
            return (UyumDurumu.VeriEksik, new AkustikLimit { Aciklama = "Yapı elemanı atanmamış" });

        var tip = BolmeTipiBelirle(bolme);
        var limit = SecLimit(sinif, performansSinifi, tip, bolme, disGurultuLgag);
        var durum = KontrolEt(sinif, performansSinifi, bolme, disGurultuLgag);
        return (durum, limit);
    }

    private static BolmeTipi BolmeTipiBelirle(Bolme bolme) =>
        bolme.YapiElemani!.Tip == YapiElemaniTipi.Doseme
            ? BolmeTipi.Doseme
            : bolme.IsDisCephe ? BolmeTipi.DisCephe : BolmeTipi.OrtakDuvar;

    /// EK-3 limit seçimi (Madde 11). Konutta (S1) aynı bağımsız birim içi komşuluklar
    /// Tablo 3.4/3.5'ten, diğer tüm komşuluklar Tablo 3.2/3.3'ten okunur (Madde 11/7).
    private static AkustikLimit SecLimit(
        BinaSinifi sinif,
        AkustikPerformansSinifi performansSinifi,
        BolmeTipi tip,
        Bolme bolme,
        double disGurultuLgag)
    {
        bool konutIciBirim = sinif == BinaSinifi.S1 &&
                             bolme.KomsulukTipi == BolmeKomsulukTipi.AyniBagimsizBolumIci;

        var limit = tip switch
        {
            BolmeTipi.Doseme => konutIciBirim
                ? YonetmelikLimitleri.GetTablo35AyniBirimLimit(performansSinifi)
                : YonetmelikLimitleri.GetTablo33Limit(performansSinifi, bolme.KaynakMekan?.KaynakDerecesi ?? ""),

            BolmeTipi.DisCephe => YonetmelikLimitleri.GetTablo31Limit(
                performansSinifi,
                bolme.KaynakMekan?.AliciSinifi ?? "",   // dış cephede iç mekan KaynakMekan'dır
                disGurultuLgag),

            _ => konutIciBirim
                ? YonetmelikLimitleri.GetTablo34AyniBirimLimit(performansSinifi)
                : YonetmelikLimitleri.GetTablo32Limit(
                    performansSinifi,
                    bolme.KaynakMekan?.KaynakDerecesi ?? "",
                    bolme.AliciMekan?.AliciSinifi ?? ""),
        };

        // U5 — Madde 11(3) / Tablo 3.2-3.4 dipnotu: kapı içeren iç bölme elemanlarında
        // sınır değerden en fazla 14 dB düşük yalıtıma izin verilir.
        if (tip == BolmeTipi.OrtakDuvar && limit.HedefDeger > 0 &&
            bolme.Acikliklar.Any(a => a.Tip == BolmeAcikligiTipi.Kapi))
        {
            limit.HedefDeger -= KapiToleransiDb;
            limit.Aciklama += $" — kapılı eleman: −{KapiToleransiDb:F0} dB toleransı (Madde 11/3)";
        }

        return limit;
    }

    /// M3 — Limit birimine göre uygun ölçüm değerini döndür.
    /// DnT,A birimi → DnT,w + (Ctr|C); diğer (eski R'w yolu) → ApparentRw.
    /// Fix #4 — Adaptasyon terimi (C/Ctr), elemanın lab değeri yerine GÖRÜNÜR (apparent)
    /// composite spektrumdan (açıklıklar + flanking dahil) alınır; mevcutsa
    /// <see cref="Bolme.UcuncuOktavHavaSesiSonucu"/>.CHesap/CtrHesap kullanılır.
    public double HesaplaOlcumDegeri(Bolme bolme, AkustikLimit limit)
    {
        bool dntBased = limit.Birim.Contains("DnT", System.StringComparison.OrdinalIgnoreCase);

        if (!dntBased)
            return bolme.IsDisCephe ? bolme.Dntw : bolme.ApparentRw;

        // Dış cephe uygunluğu tek tek duvar parçalarından değil, aynı mekana ait
        // tüm opak/açıklıklı cephelerin enerjetik birleşik DnT,A,tr sonucundan yapılır.
        // DisGurultuServisi aynı sonuç nesnesini odadaki tüm cephe parçalarına bağlar.
        if (bolme.IsDisCephe && bolme.DisCepheSpektralSonuc is { DnTATr: > 0 } cepheSonucu)
            return cepheSonucu.DnTATr;

        if (bolme.Dntw <= 0)
            return 0;

        // Görünür spektrumun C/Ctr'si (varsa) — lab değerinden daha doğru, çünkü açıklık/flanking yansır.
        var spektrum = bolme.UcuncuOktavHavaSesiSonucu;
        double adapt = bolme.IsDisCephe
            ? (spektrum?.CtrHesap ?? bolme.YapiElemani?.Ctr ?? 0)
            : (spektrum?.CHesap  ?? bolme.YapiElemani?.C   ?? 0);
        return bolme.Dntw + adapt;
    }
}
