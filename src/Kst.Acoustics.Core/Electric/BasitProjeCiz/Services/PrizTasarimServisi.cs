using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;

namespace Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

public static class PrizTasarimServisi
{
    public static bool PrizBuati(BasitProjeEleman e) =>
        e.Tip == BpcElemanTipi.Buat && e.PaftaTipi == BpcPaftaTipi.Priz;

    public static bool MahaldeBaglananPriz(BasitProjeEleman e) =>
        BasitProjeCizKabloServisi.ElemanPrizMi(e.Tip) && !BasitProjeCizKabloServisi.ElemanPrizDirektPanoMu(e);

    public static bool DogrudanPriz(BasitProjeEleman e) =>
        BasitProjeCizKabloServisi.ElemanPrizMi(e.Tip) && BasitProjeCizKabloServisi.ElemanPrizDirektPanoMu(e);

    public static List<string> MahalKablolariniCek(BasitProjeKat kat)
    {
        var elemanlar = kat.Elemanlar.ToDictionary(e => e.Id);
        var atanan = new HashSet<string>();
        var adetler = new Dictionary<string, int>();
        foreach (var d in kat.PrizDevreleri.ToList())
        {
            var kaynak = d.BuatId == null ? null : elemanlar.GetValueOrDefault(d.BuatId);
            // Eski dosyalarda doğrudan priz linyelerinin yük kaydı kaynak olarak kendi prizini gösterir.
            if (kaynak != null && DogrudanPriz(kaynak))
            {
                d.PrizElemanIds = [kaynak.Id];
                continue;
            }
            if (kaynak == null || !PrizBuati(kaynak)) d.PrizElemanIds.Clear();
            else
            {
                int adet = adetler.GetValueOrDefault(kaynak.Id);
                var gecerli = new List<string>();
                foreach (var id in d.PrizElemanIds)
                {
                    if (adet >= BasitProjeCizKabloServisi.PrizBuatKapasitesi) break;
                    if (!elemanlar.TryGetValue(id, out var priz) || !MahaldeBaglananPriz(priz) ||
                        kaynak.OdaId == null || priz.OdaId != kaynak.OdaId || !atanan.Add(id)) continue;
                    gecerli.Add(id);
                    adet++;
                }
                d.PrizElemanIds = gecerli;
                adetler[kaynak.Id] = adet;
            }
            if (d.PrizElemanIds.Count == 0) kat.PrizDevreleri.Remove(d);
        }
        BasitProjeCizKabloServisi.KatPrizleriniOtomatikKablola(kat);
        BasitProjeCizKabloServisi.TumPrizRotalariniHesapla(kat);
        return EksikMahalKablolari(kat, BaglanabilirBuatlar(kat));
    }

    // Buattan çıkan linye ancak güncel mahal içi devresine ait prizleri besleyebilir.
    public static HashSet<string> BaglanabilirBuatlar(BasitProjeKat kat)
    {
        var elemanlar = kat.Elemanlar.ToDictionary(e => e.Id);
        var rotalar = kat.Kablolar.Where(k => k.Tip == BpcKabloTipi.PrizKablosu && k.Rota.Count >= 2).ToLookup(k => k.DevreId);
        var uyelikler = kat.PrizDevreleri.SelectMany(d => d.PrizElemanIds).ToLookup(id => id);
        var sonuc = new HashSet<string>();
        foreach (var grup in kat.PrizDevreleri.Where(d => d.BuatId != null && d.PrizElemanIds.Count > 0).GroupBy(d => d.BuatId!))
        {
            if (!elemanlar.TryGetValue(grup.Key, out var buat) || !PrizBuati(buat) || buat.OdaId == null ||
                grup.Sum(d => d.PrizElemanIds.Count) > BasitProjeCizKabloServisi.PrizBuatKapasitesi) continue;
            bool DevreGecerli(BpcPrizDevre d)
            {
                if (rotalar[d.Id].Count() < d.PrizElemanIds.Count) return false;
                var uclar = rotalar[d.Id].SelectMany(k => new[] { k.Rota[0], k.Rota[^1] }).ToList();
                bool UcaDegiyor(BasitProjeEleman e) => uclar.Any(p => Math.Abs(p.X - e.X) < 0.01 && Math.Abs(p.Y - e.Y) < 0.01);
                return UcaDegiyor(buat) && d.PrizElemanIds.All(id => uyelikler[id].Count() == 1 &&
                    elemanlar.TryGetValue(id, out var priz) && MahaldeBaglananPriz(priz) && priz.OdaId == buat.OdaId && UcaDegiyor(priz));
            }
            if (grup.All(DevreGecerli)) sonuc.Add(grup.Key);
        }
        return sonuc;
    }

    private static List<string> EksikMahalKablolari(BasitProjeKat kat, HashSet<string> gecerliBuatlar)
    {
        var bagli = kat.PrizDevreleri.Where(d => d.BuatId != null && gecerliBuatlar.Contains(d.BuatId)).SelectMany(d => d.PrizElemanIds).ToHashSet();
        return kat.Elemanlar.Where(e => MahaldeBaglananPriz(e) && !bagli.Contains(e.Id))
            .GroupBy(e => e.OdaId).Select(g =>
                $"{kat.Odalar.FirstOrDefault(o => o.Id == g.Key)?.Ad ?? "Mahal atanmamış"}: {g.Count()} priz bağlı değil. Mahal ve buat kapasitesini kontrol edin.").ToList();
    }

    /// Tamamlama öncesinde mevcut hatları denetler. Çizim ve devre üyeliklerini değiştirmez.
    public static List<string> EksikBaglantilariBul(BasitProjeKat kat)
    {
        var kullanilanBuatlar = BaglanabilirBuatlar(kat);
        var uyarilar = EksikMahalKablolari(kat, kullanilanBuatlar);
        var panolar = kat.Elemanlar.Where(e => e.Tip == BpcElemanTipi.KatPanosu).ToList();
        var kaynaklar = kat.Elemanlar.Where(e => DogrudanPriz(e) || kullanilanBuatlar.Contains(e.Id)).ToList();
        var baglantilar = kat.BuatPanoBaglantilari.Where(b => b.LinyePrefix == "P").ToLookup(b => b.BuatId);
        var rotalar = kat.Kablolar.Where(k => k.Tip == BpcKabloTipi.PanoKablosu && k.Rota.Count >= 2).ToLookup(k => k.DevreId);
        var yukler = kat.PrizDevreleri.ToLookup(d => d.BuatId);
        var mahalAdlari = kat.Odalar.ToDictionary(o => o.Id, o => o.Ad);
        foreach (var kaynak in kaynaklar)
        {
            var mahal = $"{kat.Ad} / {mahalAdlari.GetValueOrDefault(kaynak.OdaId ?? "", kaynak.Ad)}";
            var pano = BasitProjeCizKabloServisi.MahaleAtananPano(kaynak.OdaId, panolar);
            if (pano == null || !mahalAdlari.ContainsKey(kaynak.OdaId!))
            {
                uyarilar.Add($"{mahal} — mahal panosunu atayın.");
                continue;
            }
            var hatlar = baglantilar[kaynak.Id].ToList();
            if (hatlar.Count != 1 || hatlar[0].PanoId != pano.Id ||
                !rotalar[hatlar[0].Id].Any(k => (Ucta(k.Rota[0], kaynak) && Ucta(k.Rota[^1], pano)) ||
                                                  (Ucta(k.Rota[0], pano) && Ucta(k.Rota[^1], kaynak))))
                uyarilar.Add($"{mahal} — {kaynak.Ad} için {pano.Ad} panosuna giden priz hattını 4. adımda çekin.");

            if (DogrudanPriz(kaynak))
            {
                var devreler = yukler[kaynak.Id].ToList();
                if (devreler.Count != 1 || !devreler[0].PrizElemanIds.SequenceEqual([kaynak.Id]))
                    uyarilar.Add($"{mahal} — doğrudan beslenen {kaynak.Ad} için 4. adımda linyeyi güncelleyin.");
            }
        }
        var kaynakIds = kaynaklar.Select(e => e.Id).ToHashSet();
        if (baglantilar.Any(g => !kaynakIds.Contains(g.Key)))
            uyarilar.Add($"{kat.Ad} — geçersiz priz çıkışlarını düzeltmek için mahal ve pano kablolarını güncelleyin.");
        return uyarilar.Distinct().ToList();

        static bool Ucta(Kst.Acoustics.Core.Common.Geometry.Point2D p, BasitProjeEleman e)
            => Math.Abs(p.X - e.X) < 0.01 && Math.Abs(p.Y - e.Y) < 0.01;
    }

    public static List<string> PanoKablolariniCek(BasitProjeKat kat)
    {
        var kullanilanBuatlar = BaglanabilirBuatlar(kat);
        var uyari = EksikMahalKablolari(kat, kullanilanBuatlar);
        var panolar = kat.Elemanlar.Where(e => e.Tip == BpcElemanTipi.KatPanosu).ToList();
        var gecisler = kat.Odalar.Where(o => o.GecisSecili).Select(o => o.Id).ToList();
        var kaynaklar = kat.Elemanlar.Where(e => DogrudanPriz(e) || PrizBuati(e) && kullanilanBuatlar.Contains(e.Id)).ToList();
        var kaynakIds = kaynaklar.Select(e => e.Id).ToHashSet();
        var silinecek = kat.BuatPanoBaglantilari.Where(b => b.LinyePrefix == "P" && !kaynakIds.Contains(b.BuatId))
            .Select(b => b.Id).ToHashSet();
        foreach (var kaynak in kaynaklar)
        {
            var pano = BasitProjeCizKabloServisi.MahaleAtananPano(kaynak.OdaId, panolar);
            if (pano == null)
            {
                var mahal = kat.Odalar.FirstOrDefault(o => o.Id == kaynak.OdaId);
                uyari.Add($"{mahal?.Ad ?? kaynak.Ad}: panoya bağlamak için önce mahal panosunu atayın.");
                foreach (var b in kat.BuatPanoBaglantilari.Where(b => b.LinyePrefix == "P" && b.BuatId == kaynak.Id))
                    silinecek.Add(b.Id);
                continue;
            }
            PanoBaglantisiniKur(kat, kaynak, pano, gecisler);
        }
        kat.BuatPanoBaglantilari.RemoveAll(b => silinecek.Contains(b.Id));
        kat.Kablolar.RemoveAll(k => silinecek.Contains(k.DevreId));
        uyari.AddRange(BasitProjeCizKabloServisi.BuatPanoRotalariniHesapla(kat, "P"));
        return uyari.Distinct().ToList();
    }

    public static BpcBuatPanoBaglanti PanoBaglantisiniKur(BasitProjeKat kat, BasitProjeEleman kaynak,
        BasitProjeEleman pano, IEnumerable<string> gecisler)
    {
        if (DogrudanPriz(kaynak))
        {
            var yuk = kat.PrizDevreleri.FirstOrDefault(d => d.BuatId == kaynak.Id);
            if (yuk == null)
            {
                yuk = new BpcPrizDevre { BuatId = kaynak.Id };
                kat.PrizDevreleri.Add(yuk);
            }
            yuk.PrizElemanIds = [kaynak.Id];
        }
        return BasitProjeCizKabloServisi.BuatPanoBagla(kat, kaynak.Id, pano.Id, gecisler, "P");
    }
}
