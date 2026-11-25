using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;

namespace Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

public static class AydinlatmaTasarimServisi
{
    public const string Giris = "GIRIS";
    public const string YedekOnEk = "YEDEK-";

    public static void MahalKablolariniCek(BasitProjeKat kat, BpcRotaTipi rotaTipi, string? odaId = null)
    {
        var elemanlar = kat.Elemanlar.ToDictionary(e => e.Id);
        var eskiBuatlar = new Dictionary<string, string>();
        var degisenDevreler = new HashSet<string>();
        foreach (var devre in kat.Devreler)
        {
            if (devre.AnahtarElemanId == null || !elemanlar.TryGetValue(devre.AnahtarElemanId, out var anahtar)) continue;
            bool mahalSecili = odaId == null || anahtar.OdaId == odaId;
            // Taşınan armatür eski mahalin anahtarında kalırsa otomatik dağıtım onu dolu sayar.
            if (devre.ArmaturElemanIds.RemoveAll(id => elemanlar.TryGetValue(id, out var armatur) &&
                    armatur.OdaId != anahtar.OdaId && (mahalSecili || armatur.OdaId == odaId)) > 0)
                degisenDevreler.Add(devre.Id);
            if (devre.BuatId != null && elemanlar.TryGetValue(devre.BuatId, out var eskiBuat) &&
                eskiBuat.Tip == BpcElemanTipi.Buat && eskiBuat.PaftaTipi == BpcPaftaTipi.Aydinlatma &&
                eskiBuat.OdaId == anahtar.OdaId)
                eskiBuatlar.TryAdd(anahtar.Id, eskiBuat.Id);
        }
        BasitProjeCizKabloServisi.KatAydinlatmasiniOtomatikKablola(kat, odaId);
        var yukler = eskiBuatlar.Values.GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
        var slotlar = BasitProjeCizKabloServisi.HesaplaWallSlotlari(kat);
        foreach (var grup in kat.Devreler.Where(d => d.AnahtarElemanId != null).GroupBy(d => d.AnahtarElemanId!))
        {
            if (!elemanlar.TryGetValue(grup.Key, out var anahtar)) continue;
            if (odaId != null && anahtar.OdaId != odaId)
            {
                foreach (var devre in grup.Where(d => degisenDevreler.Contains(d.Id)))
                    BasitProjeCizKabloServisi.RotaHesapla(kat, devre, rotaTipi, slotlar.GetValueOrDefault(devre.Id));
                continue;
            }
            if (!eskiBuatlar.TryGetValue(anahtar.Id, out var buatId))
            {
                var buat = kat.Elemanlar.Where(e => e.Tip == BpcElemanTipi.Buat &&
                    e.PaftaTipi == BpcPaftaTipi.Aydinlatma && e.OdaId == anahtar.OdaId)
                    .OrderBy(e => yukler.GetValueOrDefault(e.Id) >= 4)
                    .ThenBy(e => Math.Pow(e.X - anahtar.X, 2) + Math.Pow(e.Y - anahtar.Y, 2))
                    .FirstOrDefault();
                if (buat != null)
                {
                    buatId = buat.Id;
                    yukler[buatId] = yukler.GetValueOrDefault(buatId) + 1;
                }
            }
            foreach (var devre in grup)
            {
                devre.BuatId = buatId;
                BasitProjeCizKabloServisi.RotaHesapla(kat, devre, rotaTipi, slotlar.GetValueOrDefault(devre.Id));
            }
        }
    }

    public static List<string> PanoKablolariniCek(BasitProjeKat kat)
    {
        var uyarilar = new List<string>();
        var gecis = kat.Odalar.Where(o => o.GecisSecili).Select(o => o.Id).ToList();
        var panolar = kat.Elemanlar.Where(e => e.Tip == BpcElemanTipi.KatPanosu).ToList();
        var kullanilan = kat.Devreler.Where(d => d.ArmaturElemanIds.Count > 0).Select(d => d.BuatId).ToHashSet();
        foreach (var buat in kat.Elemanlar.Where(e => e.Tip == BpcElemanTipi.Buat &&
                     e.PaftaTipi == BpcPaftaTipi.Aydinlatma && kullanilan.Contains(e.Id)))
        {
            var hedef = BasitProjeCizKabloServisi.MahaleAtananPano(buat.OdaId, panolar);
            var baglanti = kat.BuatPanoBaglantilari.FirstOrDefault(b => b.BuatId == buat.Id && b.LinyePrefix == "A");
            if (hedef == null)
            {
                var mahal = kat.Odalar.FirstOrDefault(o => o.Id == buat.OdaId);
                uyarilar.Add($"{mahal?.Ad ?? buat.Ad} için pano atayın.");
                continue;
            }
            if (baglanti?.PanoId != hedef.Id)
                BasitProjeCizKabloServisi.BuatPanoBagla(kat, buat.Id, hedef.Id, gecis);
        }
        uyarilar.AddRange(BasitProjeCizKabloServisi.BuatPanoRotalariniHesapla(kat, "A"));
        return uyarilar.Distinct().ToList();
    }

    public static List<string> EksikBaglantilariBul(BasitProjeKat kat, bool panoGerekli)
    {
        var uyarilar = new HashSet<string>();
        var elemanlar = kat.Elemanlar.ToDictionary(e => e.Id);
        var rotalar = kat.Kablolar.Where(k => k.Rota.Count >= 2).ToLookup(k => k.DevreId);
        var beslemeDevreleri = kat.Devreler.Where(d => d.DevreNo == 1 && d.AnahtarElemanId != null)
            .ToLookup(d => d.AnahtarElemanId!);
        var devreler = kat.Devreler.SelectMany(d => d.ArmaturElemanIds.Select(a => (Id: a, Devre: d)))
            .ToLookup(x => x.Id, x => x.Devre);
        var panolar = kat.Elemanlar.Where(e => e.Tip == BpcElemanTipi.KatPanosu).ToList();
        var odalar = kat.Odalar.ToDictionary(o => o.Id, o => o.Ad);
        foreach (var arm in kat.Elemanlar.Where(e => BasitProjeCizKabloServisi.ElemanArmaturMi(e.Tip)))
        {
            string mahal = $"{kat.Ad} / {odalar.GetValueOrDefault(arm.OdaId ?? "", arm.Ad)}";
            var devre = devreler[arm.Id].FirstOrDefault();
            if (devre?.AnahtarElemanId == null || !elemanlar.TryGetValue(devre.AnahtarElemanId, out var anahtar) ||
                devre.BuatId == null || !elemanlar.TryGetValue(devre.BuatId, out var buat) ||
                arm.OdaId == null || anahtar.OdaId != arm.OdaId || buat.OdaId != arm.OdaId ||
                !BasitProjeCizKabloServisi.ElemanAnahtarMi(anahtar.Tip) || buat.Tip != BpcElemanTipi.Buat ||
                buat.PaftaTipi != BpcPaftaTipi.Aydinlatma ||
                !rotalar[devre.Id].Any(k => RotadaMi(k, anahtar) && RotadaMi(k, arm)) ||
                !beslemeDevreleri[anahtar.Id].Any(d => rotalar[d.Id].Any(k => RotadaMi(k, anahtar) && RotadaMi(k, buat))))
            {
                uyarilar.Add($"{mahal} — anahtar, buat veya mahal içi kablo eksik.");
                continue;
            }
            if (!panoGerekli) continue;
            var hedef = BasitProjeCizKabloServisi.MahaleAtananPano(buat.OdaId, panolar);
            if (hedef == null || !kat.BuatPanoBaglantilari.Any(b => b.BuatId == buat.Id &&
                    b.PanoId == hedef.Id && b.LinyePrefix == "A" &&
                    rotalar[b.Id].Any(k => RotadaMi(k, buat) && RotadaMi(k, hedef))))
                uyarilar.Add($"{mahal} — atanmış panoya giden aydınlatma hattı eksik.");
        }
        return uyarilar.ToList();
    }

    private static bool RotadaMi(BpcKablo kablo, BasitProjeEleman eleman)
        => kablo.Rota.Any(p => Math.Abs(p.X - eleman.X) < 0.01 && Math.Abs(p.Y - eleman.Y) < 0.01);

    public static IEnumerable<(string Id, string Ad, int Amper)> SigortaYerleri(PanoOzet ozet, bool prizDahil = false)
    {
        yield return (Giris, "Pano girişi", ozet.GirisSigortaA);
        foreach (var linye in ozet.Linyeler.Where(l => l.LinyePrefix is "A" or "BK" || (prizDahil && l.LinyePrefix == "P")))
            yield return (linye.BaglantiId, $"Çıkış · {linye.Etiket}", linye.SigortaA);
    }

    public static Dictionary<string, BpcPanoSigortasi> SigortalariHazirla(PanoOzet hesap, bool prizDahil = false)
    {
        var onceki = hesap.Pano.AydinlatmaSigortalari;
        var yeni = new Dictionary<string, BpcPanoSigortasi>();
        foreach (var (id, _, amper) in SigortaYerleri(hesap, prizDahil))
        {
            if (onceki?.GetValueOrDefault(id) is { Manuel: true } elle) yeni[id] = elle;
            else if (amper > 0) yeni[id] = new(amper, false);
        }
        if (onceki != null)
        {
            // Aydınlatma adımını tekrar çalıştırınca geçerli priz çıkışları saklanır.
            if (!prizDahil)
                foreach (var linye in hesap.Linyeler.Where(l => l.LinyePrefix == "P"))
                    if (onceki.TryGetValue(linye.BaglantiId, out var priz)) yeni[linye.BaglantiId] = priz;
            foreach (var kv in onceki.Where(kv => kv.Key.StartsWith(YedekOnEk, StringComparison.Ordinal)))
                yeni[kv.Key] = kv.Value;
        }
        return yeni;
    }
}
