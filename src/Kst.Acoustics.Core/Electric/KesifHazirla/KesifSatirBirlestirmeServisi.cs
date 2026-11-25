namespace Kst.Acoustics.Core.Electric.KesifHazirla;

/// <summary>Aynı keşif tablosundaki aynı Poz No + Birim satırlarını tek satırda tutar.</summary>
public static class KesifSatirBirlestirmeServisi
{
    private const decimal Epsilon = 0.0000001m;

    public static int ProjeyiBirlestir(KesifProje proje)
        => KesifTabloAgaci.TumTablolar(proje).Where(t => !KesifTabloAgaci.KilitliMi(proje, t)).Sum(TabloyuBirlestir);

    public static int TabloyuBirlestir(KesifTablosu tablo)
    {
        if (tablo.TamamlandiMi) return 0;
        var kaldirilan = 0;
        var hedefler = new Dictionary<string, KesifSatir>(StringComparer.OrdinalIgnoreCase);

        foreach (var satir in tablo.Satirlar.ToList())
        {
            LegacyKatkiyiTamamla(satir);
            var anahtar = AnahtarUret(satir.PozNo, satir.BirimAdi, satir.Birim);
            if (!hedefler.TryGetValue(anahtar, out var hedef))
            {
                hedefler[anahtar] = satir;
                continue;
            }

            var eskiTutar = hedef.SatirToplam + satir.SatirToplam;
            var yeniMiktar = hedef.Miktar + satir.Miktar;
            foreach (var katki in satir.OtomatikKatkilar)
            {
                var mevcut = hedef.OtomatikKatkilar.FirstOrDefault(k => k.KaynakReferansId == katki.KaynakReferansId);
                if (mevcut is null)
                {
                    hedef.OtomatikKatkilar.Add(new KesifSatirOtomatikKatki
                    {
                        KaynakReferansId = katki.KaynakReferansId,
                        KaynakModul = katki.KaynakModul,
                        Miktar = katki.Miktar
                    });
                }
                else
                {
                    mevcut.Miktar += katki.Miktar;
                }
            }

            hedef.Miktar = yeniMiktar;
            if (Math.Abs(yeniMiktar) > Epsilon)
                hedef.BirimFiyat = eskiTutar / yeniMiktar;
            if (hedef.PozId == Guid.Empty) hedef.PozId = satir.PozId;
            if (string.IsNullOrWhiteSpace(hedef.Aciklama)) hedef.Aciklama = satir.Aciklama;

            tablo.Satirlar.Remove(satir);
            KaynakBilgisiniGuncelle(hedef);
            kaldirilan++;
        }

        return kaldirilan;
    }

    public static void ManuelMiktarEkle(KesifSatir satir, decimal miktar)
    {
        LegacyKatkiyiTamamla(satir);
        satir.Miktar += miktar;
        KaynakBilgisiniGuncelle(satir);
    }

    public static void OtomatikKatkiEkle(KesifSatir satir, Guid referansId, string kaynakModul, decimal miktar)
    {
        LegacyKatkiyiTamamla(satir);
        var manuelMiktar = satir.Miktar - satir.OtomatikKatkilar.Sum(k => k.Miktar);
        var katki = satir.OtomatikKatkilar.FirstOrDefault(k => k.KaynakReferansId == referansId);
        if (katki is null)
        {
            katki = new KesifSatirOtomatikKatki
            {
                KaynakReferansId = referansId,
                KaynakModul = kaynakModul
            };
            satir.OtomatikKatkilar.Add(katki);
        }

        katki.Miktar += miktar;
        if (!string.IsNullOrWhiteSpace(kaynakModul))
            katki.KaynakModul = kaynakModul;
        satir.Miktar = manuelMiktar + satir.OtomatikKatkilar.Sum(k => k.Miktar);
        KaynakBilgisiniGuncelle(satir);
    }

    /// <summary>Referans katkısını düşürür; satır tamamen boşaldıysa true döndürür.</summary>
    public static bool ReferansKatkisiniSil(KesifSatir satir, Guid referansId)
    {
        LegacyKatkiyiTamamla(satir);
        var oncekiOtomatik = satir.OtomatikKatkilar.Sum(k => k.Miktar);
        var manuelMiktar = satir.Miktar - oncekiOtomatik;
        var kaldirilacak = satir.OtomatikKatkilar.Where(k => k.KaynakReferansId == referansId).ToList();
        foreach (var katki in kaldirilacak)
            satir.OtomatikKatkilar.Remove(katki);

        satir.Miktar = manuelMiktar + satir.OtomatikKatkilar.Sum(k => k.Miktar);
        KaynakBilgisiniGuncelle(satir);
        return Math.Abs(satir.Miktar) <= Epsilon && satir.OtomatikKatkilar.Count == 0;
    }

    public static string AnahtarUret(string? pozNo, string? birimAdi, string? birim)
    {
        var poz = Normalize(pozNo);
        var birimAnahtari = Normalize(birim);
        return string.IsNullOrWhiteSpace(poz)
            ? $"AD|{Normalize(birimAdi)}|{birimAnahtari}"
            : $"POZ|{poz}|{birimAnahtari}";
    }

    public static void LegacyKatkiyiTamamla(KesifSatir satir)
    {
        satir.OtomatikKatkilar ??= new();
        if (satir.OtomatikKatkilar.Count == 0 &&
            satir.KaynakTipi == KesifKaynakTipi.Otomatik &&
            satir.KaynakReferansId is Guid referansId)
        {
            satir.OtomatikKatkilar.Add(new KesifSatirOtomatikKatki
            {
                KaynakReferansId = referansId,
                KaynakModul = satir.KaynakModul,
                Miktar = satir.Miktar
            });
        }
    }

    private static void KaynakBilgisiniGuncelle(KesifSatir satir)
    {
        var otomatikMiktar = satir.OtomatikKatkilar.Sum(k => k.Miktar);
        var manuelMiktar = satir.Miktar - otomatikMiktar;
        satir.KaynakTipi = satir.OtomatikKatkilar.Count == 0
            ? KesifKaynakTipi.Manuel
            : Math.Abs(manuelMiktar) > Epsilon
                ? KesifKaynakTipi.Karma
                : KesifKaynakTipi.Otomatik;

        var referanslar = satir.OtomatikKatkilar.Select(k => k.KaynakReferansId).Distinct().ToList();
        satir.KaynakReferansId = referanslar.Count == 1 ? referanslar[0] : null;
        satir.KaynakModul = string.Join(" + ", satir.OtomatikKatkilar
            .Select(k => k.KaynakModul)
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string Normalize(string? value) => (value ?? "").Trim().ToUpperInvariant();
}
