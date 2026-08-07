using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.BuildingAcoustics.Models;

namespace Kst.Acoustics.Core.BuildingAcoustics.Calculation;

/// <summary>
/// Yönetmelik EK-10 esinli "sağlanan akustik performans sınıfı" motoru.
/// Hedef sınıfa göre geçti/kaldı yerine, hesap sonuçlarından her yapı elemanının
/// GERÇEKTE hangi sınıfı sağladığını türetir: A'dan F'ye sınıf limitleri taranır,
/// elemanın sağladığı en iyi (en üst) sınıf döner.
///
/// DİKKAT — kapsam sınırı: EK-10'un resmi Akustik Performans Belgesi süreci
/// TAMAMLANMIŞ BİNADA ÖLÇÜME dayanır (EK-10 10.1/4). Bu motor hesaplamaya dayalı
/// bir ÖN DEĞERLENDİRME sınıfı üretir; raporda bu ayrım açıkça belirtilmelidir.
/// EK-10/9 kuralı uygulanır: farklı özellikler farklı sınıflar gösteriyorsa
/// en alt performans sınıfı atanır. Değerlendirilemeyen özellik için EK-10 10.2/3
/// gereği "performans belirlenemedi" ifadesi kullanılır.
/// </summary>
public class SinifAtamaServisi
{
    private readonly YonetmelikUyumServisi _uyum = new();

    /// Akustik özellik grupları (EK-10 10.1/5'in hesapla değerlendirilebilen alt kümesi).
    public enum AkustikOzellik
    {
        /// Dış yapı elemanlarının hava doğuşlu ses yalıtımı (DnT,A,tr — Tablo 3.1)
        DisCepheHavaSesi,
        /// Bölme elemanlarının hava doğuşlu ses yalıtımı (DnT,A — Tablo 3.2/3.4)
        BolmeHavaSesi,
        /// Döşemelerin darbe sesi yalıtımı (L'nT,w — Tablo 3.3/3.5)
        DarbeSesi,
        /// Servis ekipmanlarından kaynaklı iç gürültü (EK-5 Tablo 5.1/5.2 — EK-10 10.1/5-ç)
        TesisatGurultusu,
    }

    /// Tek eleman sonucu: sağlanan sınıf, hiçbir sınıfı sağlamıyorsa SinifDisi=true,
    /// hesap verisi yoksa Belirlenemedi=true.
    public sealed record ElemanSinifi(
        AkustikPerformansSinifi? Sinif,
        bool SinifDisi,
        bool Belirlenemedi)
    {
        public string Etiket => Belirlenemedi
            ? "performans belirlenemedi"
            : SinifDisi ? "Sınıf dışı (F altı)" : Sinif!.Value.ToString();
    }

    /// Özellik grubu sonucu: gruptaki en kötü eleman sınıfı (EK-10/9) + eleman sayıları.
    public sealed record OzellikSinifi(
        AkustikOzellik Ozellik,
        ElemanSinifi Sonuc,
        int DegerlendirilenEleman,
        int BelirlenemeyenEleman);

    /// Bina düzeyi ön değerlendirme: özellik başına sınıf + genel (en alt) sınıf.
    public sealed record BinaSinifSonucu(
        IReadOnlyList<OzellikSinifi> Ozellikler,
        ElemanSinifi Genel);

    /// Bir bölmenin sağladığı en iyi sınıfı A→F tarayarak bulur.
    /// Yönetmelik limitleri sınıf parametreli olduğundan her sınıf için KontrolEt çalıştırılır.
    public ElemanSinifi SaglananSinif(BinaSinifi binaSinifi, Bolme bolme, double disGurultuLgag = 0)
    {
        bool herhangiVeriVar = false;
        foreach (var sinif in Enum.GetValues<AkustikPerformansSinifi>())
        {
            var durum = _uyum.KontrolEt(binaSinifi, sinif, bolme, disGurultuLgag);
            if (durum == UyumDurumu.Gecti)
                return new ElemanSinifi(sinif, SinifDisi: false, Belirlenemedi: false);
            if (durum == UyumDurumu.Kaldi)
                herhangiVeriVar = true;
            // VeriEksik: bu sınıf için limit türetilemedi (ör. E/F cephede Lgag'sız) —
            // diğer sınıflara bakmaya devam et.
        }

        return herhangiVeriVar
            ? new ElemanSinifi(null, SinifDisi: true, Belirlenemedi: false)
            : new ElemanSinifi(null, SinifDisi: false, Belirlenemedi: true);
    }

    /// Projedeki tüm bölmeleri özellik gruplarına ayırıp EK-10/9 en-alt-sınıf kuralıyla
    /// bina ön değerlendirme sınıfını türetir. `haricTutulanBolmeIdleri` rapor filtresidir.
    public BinaSinifSonucu BinaSinifiDegerlendir(BinaProje proje, ISet<Guid>? haricTutulanBolmeIdleri = null)
    {
        var gruplar = new Dictionary<AkustikOzellik, List<ElemanSinifi>>
        {
            [AkustikOzellik.DisCepheHavaSesi] = new(),
            [AkustikOzellik.BolmeHavaSesi] = new(),
            [AkustikOzellik.DarbeSesi] = new(),
            [AkustikOzellik.TesisatGurultusu] = new(),
        };

        // Hava sesi (duvar/cephe): mekan.Bolmeler'deki gerçek bölmeler.
        foreach (var kat in proje.Katlar)
        {
            foreach (var mekan in kat.Mekanlar)
            {
                if (mekan.Sinif == MekanSinifi.HesapDisi)
                    continue;

                // Cephe, oda ölçeğinde enerjetik olarak birleşik tek bir sonuçtur.
                // Aynı DnT,A,tr sonucunu her cephe parçasından ayrı ayrı sınıflandırmak
                // ve malzemesiz parçaları atlamak yerine oda başına bir kez değerlendir.
                var cepheler = mekan.Bolmeler
                    .Where(b => b.IsDisCephe && haricTutulanBolmeIdleri?.Contains(b.Id) != true)
                    .ToList();
                if (cepheler.Count > 0)
                {
                    bool eksikCephe = cepheler.Any(b =>
                        b.YapiElemani == null || b.YapiElemani.EffectiveRw <= 0 || b.Alan <= 0 ||
                        b.Acikliklar.Any(a => a.Alan > 0 &&
                            (a.YapiElemani == null || a.YapiElemani.EffectiveRw <= 0)));

                    var temsilci = cepheler.FirstOrDefault(b => b.DisCepheSpektralSonuc is { DnTATr: > 0 })
                                    ?? cepheler[0];
                    bool birlesikSonucEksik = cepheler.Count > 1 && temsilci.DisCepheSpektralSonuc == null;
                    gruplar[AkustikOzellik.DisCepheHavaSesi].Add(
                        eksikCephe || birlesikSonucEksik
                            ? BelirlenemediSonucu()
                            : SaglananSinif(proje.BinaSinifi, temsilci, proje.DisGurultuDb));
                }

                foreach (var bolme in mekan.Bolmeler)
                {
                    if (haricTutulanBolmeIdleri?.Contains(bolme.Id) == true)
                        continue;
                    if (bolme.IsDisCephe)
                        continue; // oda-birleşik cephe yukarıda bir kez işlendi

                    if (bolme.YapiElemani == null)
                    {
                        gruplar[AkustikOzellik.BolmeHavaSesi].Add(BelirlenemediSonucu());
                        continue;
                    }

                    var ozellik = bolme.YapiElemani.Tip == YapiElemaniTipi.Doseme
                        ? AkustikOzellik.DarbeSesi
                        : AkustikOzellik.BolmeHavaSesi;

                    if (bolme.Acikliklar.Any(a => a.Alan > 0 && a.YapiElemani == null))
                    {
                        gruplar[ozellik].Add(BelirlenemediSonucu());
                        continue;
                    }

                    gruplar[ozellik].Add(SaglananSinif(proje.BinaSinifi, bolme, proje.DisGurultuDb));
                }
            }
        }

        // Darbe sesi: katlar arası darbe yolları geçici bölmelerle değerlendirilir (mekan.
        // Bolmeler'de yaşamazlar) — hesaplanmış sentetik döşeme bölmeleri paylaşılan Core
        // boru hattından üretilir (deterministik Id sayesinde hariç-tutma filtresi çalışır).
        foreach (var dosemeBolmesi in new KatlarArasiDarbeYoluServisi().DegerlendirilmisDarbeBolmeleri(proje))
        {
            if (haricTutulanBolmeIdleri?.Contains(dosemeBolmesi.Id) == true)
                continue;
            gruplar[AkustikOzellik.DarbeSesi].Add(SaglananSinif(proje.BinaSinifi, dosemeBolmesi, proje.DisGurultuDb));
        }

        // Tesisat gürültüsü (EK-10 10.1/5-ç): beyan edilen düzey EK-5 sınırlarına karşı
        // A→F taranır; sürekli düzey Tablo 5.1, kesikli düzey Tablo 5.2. Beyanı olmayan
        // mekanlar bu özelliğe katılmaz.
        foreach (var kat in proje.Katlar)
            foreach (var mekan in kat.Mekanlar)
            {
                if (mekan.Sinif == MekanSinifi.HesapDisi)
                    continue;
                if (mekan.TesisatSurekliDb > 0)
                    gruplar[AkustikOzellik.TesisatGurultusu].Add(TesisatSinifi(
                        s => EK5TesisatServisi.SurekliLimit(proje.BinaIslevi, s, mekan.Ad, mekan.KullanimTipi)?.LimitDb,
                        mekan.TesisatSurekliDb));
                if (mekan.TesisatKesikliDb > 0)
                    gruplar[AkustikOzellik.TesisatGurultusu].Add(TesisatSinifi(
                        s => EK5TesisatServisi.KesikliLimit(s).LimitDb,
                        mekan.TesisatKesikliDb));
            }

        var ozellikler = new List<OzellikSinifi>();
        foreach (var (ozellik, sonuclar) in gruplar)
        {
            ozellikler.Add(new OzellikSinifi(
                ozellik,
                EnKotu(sonuclar),
                DegerlendirilenEleman: sonuclar.Count(s => !s.Belirlenemedi),
                BelirlenemeyenEleman: sonuclar.Count(s => s.Belirlenemedi)));
        }

        // Genel sınıf (EK-10/9): değerlendirilebilen özelliklerin en kötüsü.
        // Hiçbir özellik değerlendirilemiyorsa genel de belirlenemedi.
        var degerlendirilebilen = ozellikler
            .Where(o => !o.Sonuc.Belirlenemedi)
            .Select(o => o.Sonuc)
            .ToList();
        // Uygulanabilir bir özellikte tek bir eksik eleman dahi varsa, birkaç iyi
        // elemandan resmî bina sınıfı türetilmez. Hiç elemanı olmayan özellik grubu
        // ise projeye uygulanmıyor olabilir ve tek başına geneli zehirlemez.
        var genel = ozellikler.Any(o => o.BelirlenemeyenEleman > 0)
            ? BelirlenemediSonucu()
            : degerlendirilebilen.Count == 0
            ? new ElemanSinifi(null, SinifDisi: false, Belirlenemedi: true)
            : EnKotu(degerlendirilebilen);

        return new BinaSinifSonucu(ozellikler, genel);
    }

    /// Beyan edilen tesisat düzeyi için sağlanan sınıf: A→F taranır, düzeyi sınırın
    /// altında tutan ilk (en iyi) sınıf döner. Limit türetilemiyorsa (Tablo 5.1 kapsam
    /// dışı işlev/mekan) belirlenemedi; F sınırı bile aşılıyorsa sınıf dışı.
    private static ElemanSinifi TesisatSinifi(Func<AkustikPerformansSinifi, double?> limitSec, double girilenDb)
    {
        bool limitVar = false;
        foreach (var sinif in Enum.GetValues<AkustikPerformansSinifi>())
        {
            if (limitSec(sinif) is not { } limit)
                continue;
            limitVar = true;
            if (girilenDb <= limit)
                return new ElemanSinifi(sinif, SinifDisi: false, Belirlenemedi: false);
        }
        return limitVar
            ? new ElemanSinifi(null, SinifDisi: true, Belirlenemedi: false)
            : new ElemanSinifi(null, SinifDisi: false, Belirlenemedi: true);
    }

    /// Bir grup eleman sonucundan en kötüsünü seçer: Sınıf dışı > F > ... > A.
    /// Boş grup veya tümü belirlenemedi → belirlenemedi.
    private static ElemanSinifi EnKotu(IReadOnlyList<ElemanSinifi> sonuclar)
    {
        if (sonuclar.Count == 0 || sonuclar.Any(s => s.Belirlenemedi))
            return BelirlenemediSonucu();

        var gecerli = sonuclar.Where(s => !s.Belirlenemedi).ToList();

        if (gecerli.Any(s => s.SinifDisi))
            return new ElemanSinifi(null, SinifDisi: true, Belirlenemedi: false);

        // AkustikPerformansSinifi enum'unda A=0 … F=5; sayısal en büyük = en kötü.
        var enKotu = gecerli.Max(s => (int)s.Sinif!.Value);
        return new ElemanSinifi((AkustikPerformansSinifi)enKotu, SinifDisi: false, Belirlenemedi: false);
    }

    private static ElemanSinifi BelirlenemediSonucu() =>
        new(null, SinifDisi: false, Belirlenemedi: true);
}
