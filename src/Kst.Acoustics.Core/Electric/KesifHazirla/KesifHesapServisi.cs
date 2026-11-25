namespace Kst.Acoustics.Core.Electric.KesifHazirla;

public static class KesifHesapServisi
{
    public static IReadOnlyList<KesifIcmalSatiri> IcmalOlustur(KesifProje proje)
    {
        ArgumentNullException.ThrowIfNull(proje);

        // Alt tablolar dahil tüm tabloları dolaş; satırlar yalnız yaprak tablolarda bulunur.
        var rows = KesifTabloAgaci.TumTablolar(proje)
            .SelectMany(t => t.Satirlar.Select(s => new { Tablo = t, Satir = s }))
            .Where(x => !string.IsNullOrWhiteSpace(x.Satir.PozNo) || !string.IsNullOrWhiteSpace(x.Satir.BirimAdi))
            .GroupBy(x => new
            {
                Poz = NormalizePozKey(x.Satir),
                Birim = NormalizeKey(x.Satir.Birim)
            })
            .Select(g =>
            {
                var sample = g.First().Satir;
                var totalAmount = g.Sum(x => x.Satir.Miktar);
                var totalCost = g.Sum(x => x.Satir.SatirToplam);
                var weightedUnitPrice = totalAmount == 0 ? 0 : totalCost / totalAmount;
                var unitPrices = g.Select(x => x.Satir.BirimFiyat).Distinct().Count();

                return new KesifIcmalSatiri
                {
                    PozNo = sample.PozNo,
                    BirimAdi = sample.BirimAdi,
                    Birim = sample.Birim,
                    ToplamMiktar = totalAmount,
                    OrtalamaBirimFiyat = weightedUnitPrice,
                    ToplamTutar = totalCost,
                    BirimFiyatFarkiVar = unitPrices > 1,
                    Kirilimlar = new(g.GroupBy(x => x.Tablo.Id)
                        .OrderBy(x => x.First().Tablo.Ad, StringComparer.CurrentCultureIgnoreCase)
                        .Select(tg => new KesifIcmalKirilim
                        {
                            TabloId = tg.Key,
                            TabloAdi = tg.First().Tablo.Ad,
                            Miktar = tg.Sum(x => x.Satir.Miktar),
                            Tutar = tg.Sum(x => x.Satir.SatirToplam)
                        }))
                };
            })
            .OrderBy(x => x.PozNo, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.Birim, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return rows;
    }

    private static string NormalizeKey(string? value)
        => (value ?? "").Trim().ToUpperInvariant();

    private static string NormalizePozKey(KesifSatir satir)
    {
        var pozNo = NormalizeKey(satir.PozNo);
        return !string.IsNullOrWhiteSpace(pozNo)
            ? $"POZ:{pozNo}"
            : $"AD:{NormalizeKey(satir.BirimAdi)}";
    }
}
