using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.BuildingAcoustics.Reports;

namespace Kst.Acoustics.Core.BuildingAcoustics.Calculation;

public sealed class OlcumKarsilastirmaSatiri
{
    public Guid OlcumId { get; set; }
    public string OlcumAdi { get; set; } = "";
    public SahaOlcumTuru OlcumTuru { get; set; }
    public Guid? SonucId { get; set; }
    public double? ModelDegeri { get; set; }
    public double OlcumDegeri { get; set; }
    public double? ModeldenSapma { get; set; }
    public double? LimitDegeri { get; set; }
    public double? OlcumMarji { get; set; }
    public bool? OlcumUygunMu { get; set; }
    public bool RaporlamaIcinOnayli { get; set; }
    public string Birim { get; set; } = "";
    public string EslesmeDurumu { get; set; } = "";
    public string Aciklama { get; set; } = "";
}

/// <summary>Canlı modeli değiştirmeden hesap sonucu ile saha ölçümünü yan yana getirir.</summary>
public sealed class OlcumKarsilastirmaServisi
{
    public IReadOnlyList<OlcumKarsilastirmaSatiri> Karsilastir(
        BinaProje proje,
        bool sadeceRaporlamaIcinOnayli = false)
    {
        ArgumentNullException.ThrowIfNull(proje);
        var olcumler = (proje.SahaOlcumleri ?? new List<SahaOlcumKaydi>())
            .Where(o => !sadeceRaporlamaIcinOnayli ||
                        (o.RaporlamaIcinOnayli && o.Durum == SahaOlcumDurumu.Dogrulandi))
            .OrderByDescending(o => o.OlcumTarihi)
            .ThenBy(o => o.Id)
            .ToList();
        if (olcumler.Count == 0)
            return [];

        var degerlendirme = new BinaAkustikDegerlendirmeServisi().Hesapla(proje);
        var mekanlar = proje.Katlar.SelectMany(k => k.Mekanlar).ToDictionary(m => m.Id);
        var bolmeSahipleri = proje.Katlar.SelectMany(k => k.Mekanlar)
            .SelectMany(m => m.Bolmeler.Select(b => (Bolme: b, Mekan: m)))
            .GroupBy(x => x.Bolme.Id)
            .ToDictionary(g => g.Key, g => g.First().Mekan);

        var sonuc = new List<OlcumKarsilastirmaSatiri>(olcumler.Count);
        foreach (var olcum in olcumler)
        {
            var satir = new OlcumKarsilastirmaSatiri
            {
                OlcumId = olcum.Id,
                OlcumAdi = olcum.Ad,
                OlcumTuru = olcum.Tur,
                OlcumDegeri = olcum.Deger,
                RaporlamaIcinOnayli = olcum.RaporlamaIcinOnayli,
                Birim = olcum.Birim
            };

            if (olcum.Tur == SahaOlcumTuru.DisGurultu)
            {
                satir.ModelDegeri = proje.DisGurultuDb;
                satir.ModeldenSapma = olcum.Deger - proje.DisGurultuDb;
                satir.EslesmeDurumu = "Proje dış gürültü girdisiyle eşleşti";
                satir.Aciklama = "Bu ölçüm bir yönetmelik sonuç satırı değil, proje Lgag/Lden girdisiyle kıyaslandı.";
                sonuc.Add(satir);
                continue;
            }

            var raporSatiri = EslesenSatiriBul(olcum, degerlendirme.Satirlar, mekanlar, bolmeSahipleri);
            if (raporSatiri is null)
            {
                satir.EslesmeDurumu = "Eşleşmedi";
                satir.Aciklama = "Bölme veya mekan kimliğiyle uyumlu bir hesap sonucu bulunamadı.";
                sonuc.Add(satir);
                continue;
            }

            satir.SonucId = raporSatiri.SonucId;
            satir.ModelDegeri = raporSatiri.Deger;
            satir.ModeldenSapma = olcum.Deger - raporSatiri.Deger;
            satir.LimitDegeri = raporSatiri.LimitDeger > 0 ? raporSatiri.LimitDeger : null;
            satir.EslesmeDurumu = "Eşleşti";
            if (satir.LimitDegeri is { } limit)
            {
                satir.OlcumMarji = raporSatiri.DahaDusukDahaIyi
                    ? limit - olcum.Deger
                    : olcum.Deger - limit;
                satir.OlcumUygunMu = satir.OlcumMarji >= 0;
            }
            satir.Aciklama = olcum.Belirsizlik is { } belirsizlik
                ? $"Ölçüm belirsizliği ±{belirsizlik:F1} {olcum.Birim}; uygunluk marjına otomatik eklenmedi."
                : "Ölçüm belirsizliği beyan edilmedi.";
            sonuc.Add(satir);
        }
        return sonuc;
    }

    private static RaporSatiri? EslesenSatiriBul(
        SahaOlcumKaydi olcum,
        IReadOnlyList<RaporSatiri> satirlar,
        IReadOnlyDictionary<Guid, Mekan> mekanlar,
        IReadOnlyDictionary<Guid, Mekan> bolmeSahipleri)
    {
        var tur = RaporTuru(olcum.Tur);
        if (tur is null) return null;

        Guid? kesinId = null;
        if (olcum.BolmeId is { } bolmeId)
        {
            if (tur == AkustikRaporSonucTuru.DisCephe && bolmeSahipleri.TryGetValue(bolmeId, out var sahip))
                kesinId = RaporSonucIdleri.DisCephe(sahip.Id);
            else
                kesinId = bolmeId;
        }

        Guid? mekanId = olcum.KaynakMekanId ?? olcum.AliciMekanId;
        if (kesinId is null && mekanId is { } mid)
        {
            kesinId = tur switch
            {
                AkustikRaporSonucTuru.DisCephe => RaporSonucIdleri.DisCephe(mid),
                AkustikRaporSonucTuru.Reverberasyon => RaporSonucIdleri.Reverberasyon(mid),
                AkustikRaporSonucTuru.TesisatSurekli => RaporSonucIdleri.Tesisat(mid, false),
                AkustikRaporSonucTuru.TesisatKesikli => RaporSonucIdleri.Tesisat(mid, true),
                _ => null
            };
        }

        if (kesinId is { } id)
        {
            var tam = satirlar.FirstOrDefault(s => s.SonucId == id && s.SonucTuru == tur);
            if (tam is not null) return tam;
        }

        string kaynakAd = olcum.KaynakMekanId is { } kaynakId && mekanlar.TryGetValue(kaynakId, out var kaynak)
            ? kaynak.Ad : "";
        string aliciAd = olcum.AliciMekanId is { } aliciId && mekanlar.TryGetValue(aliciId, out var alici)
            ? alici.Ad : "";
        return satirlar.FirstOrDefault(s => s.SonucTuru == tur &&
            (string.IsNullOrWhiteSpace(kaynakAd) ||
             string.Equals(s.KaynakMekan, kaynakAd, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(s.Mekan, kaynakAd, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(aliciAd) ||
             string.Equals(s.AliciMekan, aliciAd, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(s.Mekan, aliciAd, StringComparison.OrdinalIgnoreCase)));
    }

    private static AkustikRaporSonucTuru? RaporTuru(SahaOlcumTuru tur) => tur switch
    {
        SahaOlcumTuru.BolmeHavaSesi => AkustikRaporSonucTuru.HavaSesi,
        SahaOlcumTuru.DisCepheHavaSesi => AkustikRaporSonucTuru.DisCephe,
        SahaOlcumTuru.DarbeSesi => AkustikRaporSonucTuru.DarbeSesi,
        SahaOlcumTuru.IcGurultuSurekli => AkustikRaporSonucTuru.TesisatSurekli,
        SahaOlcumTuru.IcGurultuKesikli => AkustikRaporSonucTuru.TesisatKesikli,
        SahaOlcumTuru.ServisEkipmaniSurekli => AkustikRaporSonucTuru.TesisatSurekli,
        SahaOlcumTuru.ServisEkipmaniKesikli => AkustikRaporSonucTuru.TesisatKesikli,
        SahaOlcumTuru.Reverberasyon => AkustikRaporSonucTuru.Reverberasyon,
        _ => null
    };
}
