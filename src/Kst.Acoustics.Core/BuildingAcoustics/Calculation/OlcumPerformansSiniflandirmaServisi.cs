using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.BuildingAcoustics.Reports;

namespace Kst.Acoustics.Core.BuildingAcoustics.Calculation;

public enum OlcumPerformansOzelligi
{
    DisCepheHavaSesi,
    BolmeHavaSesi,
    DarbeSesi,
    IcOrtamArkaPlanGurultusu,
    ServisEkipmaniGurultusu,
    Reverberasyon,
}

public sealed record OlcumPerformansSinifi(
    AkustikPerformansSinifi? Sinif,
    bool SinifDisi,
    bool Belirlenemedi)
{
    public string Etiket => Belirlenemedi ? "performans belirlenemedi" :
        SinifDisi ? "Sınıf dışı (F altı)" : Sinif!.Value.ToString();
}

public sealed record OlcumPerformansSatiri(
    Guid OlcumId,
    string OlcumAdi,
    OlcumPerformansOzelligi Ozellik,
    double Deger,
    string Birim,
    OlcumPerformansSinifi Sonuc,
    string Aciklama);

public sealed record OlcumPerformansOzellikSonucu(
    OlcumPerformansOzelligi Ozellik,
    int OlcumSayisi,
    OlcumPerformansSinifi Sonuc);

public sealed record OlcumPerformansBinaSonucu(
    IReadOnlyList<OlcumPerformansSatiri> Olcumler,
    IReadOnlyList<OlcumPerformansOzellikSonucu> Ozellikler,
    OlcumPerformansSinifi Genel);

/// <summary>
/// Yalnız doğrulanmış ve raporlamaya onaylı ölçümleri A→F sınırlarına karşı tarar.
/// Hesap modeli ölçüm değerinin yerine kullanılmaz; model yalnız sonuç kimliği ve mevzuat
/// kriteri eşlemesi sağlar. EK-10 kapsam/örnekleme yeterliliği ayrıca ön kontrolden geçmelidir.
/// </summary>
public sealed class OlcumPerformansSiniflandirmaServisi
{
    private static readonly OlcumPerformansOzelligi[] TumOzellikler =
        Enum.GetValues<OlcumPerformansOzelligi>();

    public OlcumPerformansBinaSonucu Degerlendir(BinaProje proje, ISet<Guid>? haric = null)
    {
        ArgumentNullException.ThrowIfNull(proje);
        var onayli = proje.SahaOlcumleri.Where(x =>
            x.Durum == SahaOlcumDurumu.Dogrulandi && x.RaporlamaIcinOnayli).ToList();
        var karsilastirmalar = new OlcumKarsilastirmaServisi()
            .Karsilastir(proje, sadeceRaporlamaIcinOnayli: true)
            .ToDictionary(x => x.OlcumId);

        var sinifSatirlari = new Dictionary<AkustikPerformansSinifi, Dictionary<Guid, RaporSatiri>>();
        var hesaplanan = new BinaAkustikDegerlendirmeServisi().Hesapla(proje, haric).HesaplananProje;
        foreach (var sinif in Enum.GetValues<AkustikPerformansSinifi>())
        {
            hesaplanan.AkustikPerformansSinifi = sinif;
            sinifSatirlari[sinif] = new BinaAkustikRaporServisi()
                .TabloVerisiOlustur(hesaplanan, haric)
                .GroupBy(x => x.SonucId)
                .ToDictionary(x => x.Key, x => x.First());
        }

        var mekanlar = proje.Katlar.SelectMany(k => k.Mekanlar).ToDictionary(x => x.Id);
        var satirlar = new List<OlcumPerformansSatiri>();
        foreach (var olcum in onayli)
        {
            if (!Ozellik(olcum.Tur, out var ozellik))
                continue;

            OlcumPerformansSinifi sonuc;
            string aciklama;
            if (ozellik == OlcumPerformansOzelligi.IcOrtamArkaPlanGurultusu)
            {
                var mekanId = olcum.AliciMekanId ?? olcum.KaynakMekanId;
                if (mekanId is not { } id || !mekanlar.TryGetValue(id, out var mekan))
                {
                    sonuc = Belirlenemedi();
                    aciklama = "Ölçüm bir alıcı mekân kimliğiyle eşleştirilemedi.";
                }
                else
                {
                    sonuc = SinifTara(sinif =>
                        EK4IcGurultuServisi.LimitBul(proje.BinaIslevi, sinif, mekan.Ad, mekan.KullanimTipi)?.LimitDb,
                        olcum.Deger, dahaDusukDahaIyi: true);
                    aciklama = "EK-4 Tablo 4.1 iç ortam L_Aeq sınırlarıyla sınıflandırıldı.";
                }
            }
            else if (ozellik == OlcumPerformansOzelligi.Reverberasyon)
            {
                var eslesme = karsilastirmalar.GetValueOrDefault(olcum.Id);
                var limit = eslesme?.LimitDegeri;
                sonuc = limit is null ? Belirlenemedi() :
                    olcum.Deger <= limit.Value
                        ? new(AkustikPerformansSinifi.C, false, false)
                        : new(null, true, false);
                aciklama = limit is null
                    ? "EK-6 sayısal sınırı bulunamadı; uzman kararı gerekir."
                    : "EK-6 değeri C-D için geçerlidir; daha üst sınıf uzman kararı olmadan atanmadı.";
            }
            else
            {
                var eslesme = karsilastirmalar.GetValueOrDefault(olcum.Id);
                if (eslesme?.SonucId is not { } sonucId)
                {
                    sonuc = Belirlenemedi();
                    aciklama = "Ölçüm, kararlı bir yönetmelik sonuç kimliğiyle eşleştirilemedi.";
                }
                else
                {
                    RaporSatiri? Referans(AkustikPerformansSinifi sinif) =>
                        sinifSatirlari[sinif].GetValueOrDefault(sonucId);
                    var ilk = Referans(AkustikPerformansSinifi.C);
                    sonuc = SinifTara(
                        sinif => Referans(sinif) is { LimitDeger: > 0 } r ? r.LimitDeger : null,
                        olcum.Deger,
                        ilk?.DahaDusukDahaIyi ?? false);
                    aciklama = "Ölçüm değeri, eşleşen kriterin A–F yönetmelik sınırlarına karşı tarandı.";
                }
            }
            satirlar.Add(new(olcum.Id, olcum.Ad, ozellik, olcum.Deger, olcum.Birim, sonuc, aciklama));
        }

        var ozellikler = TumOzellikler.Select(ozellik =>
        {
            var grup = satirlar.Where(x => x.Ozellik == ozellik).Select(x => x.Sonuc).ToList();
            return new OlcumPerformansOzellikSonucu(ozellik, grup.Count, EnKotu(grup));
        }).ToList();
        var genel = ozellikler.Any(x => x.Sonuc.Belirlenemedi)
            ? Belirlenemedi()
            : EnKotu(ozellikler.Select(x => x.Sonuc).ToList());
        return new(satirlar, ozellikler, genel);
    }

    private static OlcumPerformansSinifi SinifTara(
        Func<AkustikPerformansSinifi, double?> limitBul,
        double deger,
        bool dahaDusukDahaIyi)
    {
        var limitVar = false;
        foreach (var sinif in Enum.GetValues<AkustikPerformansSinifi>())
        {
            if (limitBul(sinif) is not { } limit || limit <= 0) continue;
            limitVar = true;
            if (dahaDusukDahaIyi ? deger <= limit : deger >= limit)
                return new(sinif, false, false);
        }
        return limitVar ? new(null, true, false) : Belirlenemedi();
    }

    private static OlcumPerformansSinifi EnKotu(IReadOnlyList<OlcumPerformansSinifi> sonuclar)
    {
        if (sonuclar.Count == 0 || sonuclar.Any(x => x.Belirlenemedi)) return Belirlenemedi();
        if (sonuclar.Any(x => x.SinifDisi)) return new(null, true, false);
        return new((AkustikPerformansSinifi)sonuclar.Max(x => (int)x.Sinif!.Value), false, false);
    }

    private static OlcumPerformansSinifi Belirlenemedi() => new(null, false, true);

    private static bool Ozellik(SahaOlcumTuru tur, out OlcumPerformansOzelligi ozellik)
    {
        ozellik = tur switch
        {
            SahaOlcumTuru.DisCepheHavaSesi => OlcumPerformansOzelligi.DisCepheHavaSesi,
            SahaOlcumTuru.BolmeHavaSesi => OlcumPerformansOzelligi.BolmeHavaSesi,
            SahaOlcumTuru.DarbeSesi => OlcumPerformansOzelligi.DarbeSesi,
            SahaOlcumTuru.IcOrtamArkaPlanGurultusu => OlcumPerformansOzelligi.IcOrtamArkaPlanGurultusu,
            SahaOlcumTuru.IcGurultuSurekli or SahaOlcumTuru.IcGurultuKesikli or
                SahaOlcumTuru.ServisEkipmaniSurekli or SahaOlcumTuru.ServisEkipmaniKesikli =>
                OlcumPerformansOzelligi.ServisEkipmaniGurultusu,
            SahaOlcumTuru.Reverberasyon => OlcumPerformansOzelligi.Reverberasyon,
            _ => default,
        };
        return tur is not SahaOlcumTuru.DisGurultu;
    }
}
