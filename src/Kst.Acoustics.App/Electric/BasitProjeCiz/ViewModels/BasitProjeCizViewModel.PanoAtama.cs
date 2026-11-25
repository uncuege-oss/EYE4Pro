using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

namespace Kst.Acoustics.App.Electric.BasitProjeCiz.ViewModels;

public partial class BasitProjeCizViewModel
{
    public void MahalePanoAta(string odaId, string? panoId)
    {
        if (!ModulKuvvetliMi || KilitliyseDur("mahalin bağlı olduğu panoyu değiştirmek"))
        {
            PanoOdaSecenekleriDoldur();
            return;
        }

        var kat = Katlar.FirstOrDefault(k => k.Odalar.Any(o => o.Id == odaId));
        if (kat == null) return;
        var panoKati = panoId == null ? null : PanoKat(panoId);
        var pano = panoKati?.Elemanlar.FirstOrDefault(p => p.Id == panoId && p.Tip == BpcElemanTipi.KatPanosu);
        if (panoId != null && (pano == null || ((AydinlatmaAkisiAktif || PrizAkisiAktif) && panoKati != kat))) return;
        var eskiKatlar = Katlar.Where(k => k.Elemanlar.Any(p =>
            p.Tip == BpcElemanTipi.KatPanosu && p.BagliOdaIds.Contains(odaId))).ToList();
        var eskiPanolar = eskiKatlar.SelectMany(k => k.Elemanlar).Where(p =>
            p.Tip == BpcElemanTipi.KatPanosu && p.BagliOdaIds.Contains(odaId)).ToList();
        if (pano == null ? eskiPanolar.Count == 0 : eskiPanolar.Count == 1 && eskiPanolar[0] == pano)
            return;

        // Eski dosyalarda atama başka kattaki bir panoda tutulabiliyordu.
        // Değişiklik, önceki sahipleri ve onlara giden aydınlatma kablolarını tek adımda kaldırır.
        eskiKatlar.Add(kat);
        if (panoKati != null) eskiKatlar.Add(panoKati);
        ElemanSnapshotAl(eskiKatlar);
        foreach (var eski in eskiPanolar) eski.BagliOdaIds.RemoveAll(id => id == odaId);
        pano?.BagliOdaIds.Add(odaId);

        var buatIds = kat.Elemanlar.Where(e => e.Tip == BpcElemanTipi.Buat && e.OdaId == odaId)
            .Select(e => e.Id).ToHashSet();
        var prizUclari = kat.Elemanlar.Where(e => e.OdaId == odaId && (PrizTasarimServisi.PrizBuati(e) ||
            PrizTasarimServisi.DogrudanPriz(e))).Select(e => e.Id).ToHashSet();
        var silinenBaglantilar = kat.BuatPanoBaglantilari
            .Where(b => b.PanoId != panoId && (b.LinyePrefix == "A" && buatIds.Contains(b.BuatId) ||
                b.LinyePrefix == "P" && prizUclari.Contains(b.BuatId)))
            .Select(b => b.Id).ToHashSet();
        kat.BuatPanoBaglantilari.RemoveAll(b => silinenBaglantilar.Contains(b.Id));
        kat.Kablolar.RemoveAll(k => silinenBaglantilar.Contains(k.DevreId));
        BasitProjeCizKabloServisi.PanoKablolariniNumaralandir(kat);

        var oda = kat.Odalar.First(o => o.Id == odaId);
        DurumMesaji = pano == null ? $"{oda.Ad} için pano ataması kaldırıldı." : $"{oda.Ad} → {pano.Ad}";
        if (silinenBaglantilar.Count > 0) DurumMesaji += " İlgili paftanın pano kablolarını yeniden çekin.";
        SeciliElemanAyarlariniBildir();
        GuncelleKabloGorunum();
        Guncelle3DGorunum();
    }

    // Önce en yakın üst kademe, aynı kademede ise mahale atanmış pano ve mesafe.
    // Kat panosu içeren eski projeler tali → kat → ana sırasını korur.
    private static IEnumerable<BasitProjeEleman> UstPanoSecenekleri(
        BasitProjeEleman kaynak, IEnumerable<BasitProjeEleman> elemanlar)
    {
        static int Kademe(BasitProjeEleman p)
        {
            if (BasitProjeCizKabloServisi.PanoAnaMi(p)) return 0;
            if (BasitProjeCizKabloServisi.PanoAltMi(p)) return 3;
            return BasitProjeCizKabloServisi.PanoTaliMi(p) ? 2 : 1;
        }

        var kademe = Kademe(kaynak);
        return elemanlar.Where(p => p.Tip == BpcElemanTipi.KatPanosu && Kademe(p) < kademe)
            .OrderByDescending(p => Kademe(p))
            .ThenByDescending(p => kaynak.OdaId != null && p.BagliOdaIds.Contains(kaynak.OdaId))
            .ThenBy(p => (p.X - kaynak.X) * (p.X - kaynak.X) + (p.Y - kaynak.Y) * (p.Y - kaynak.Y));
    }

    private IEnumerable<string> EksikPanoBeslemeleri(BasitProjeKat kat, bool prizDahil = false)
    {
        var panolar = kat.Elemanlar.Where(p => p.Tip == BpcElemanTipi.KatPanosu).ToList();
        var tumPanolar = Katlar.SelectMany(k => k.Elemanlar).Where(p => p.Tip == BpcElemanTipi.KatPanosu).ToDictionary(p => p.Id);
        var hatlar = Katlar.SelectMany(k => k.PanoBeslemeHatlari)
            .Where(h => h.UygulamaTipi == BpcPanoBeslemeUygulamaTipi.Kablo).ToList();
        var yukluPanolar = kat.BuatPanoBaglantilari.Where(b => b.LinyePrefix == "A" || (prizDahil && b.LinyePrefix == "P"))
            .Select(b => b.PanoId).Concat(hatlar.Select(h => h.AnaPanoId)).ToHashSet();
        foreach (var pano in panolar.Where(p => yukluPanolar.Contains(p.Id)))
        {
            var besleme = hatlar.Where(h => h.TaliPanoId == pano.Id).ToList();
            var ustPanoIds = besleme.Select(h => h.AnaPanoId).Distinct().ToList();
            if (ustPanoIds.Count > 1)
            {
                yield return $"{kat.Ad} / {pano.Ad} — bu panonun birden fazla üst pano beslemesi var.";
                continue;
            }
            // Elle tanımlanmış bir besleme, özellikle katlar arasında, en yakın kat içi panodan önceliklidir.
            var ustId = pano.TaliPanoHedefPanoId;
            if (string.IsNullOrEmpty(ustId))
                ustId = besleme.Any(h => !h.OtomatikKatIciBesleme) ? ustPanoIds[0] : UstPanoSecenekleri(pano, panolar).FirstOrDefault()?.Id;
            if (string.IsNullOrEmpty(ustId) && besleme.Count == 0) continue; // Bağımsız ana pano.
            if (string.IsNullOrEmpty(ustId) || !tumPanolar.TryGetValue(ustId, out var ust) || ust == pano ||
                ustPanoIds.Count != 1 || ustPanoIds[0] != ustId || !BeslemeUcaDegiyor(besleme, ust) || !BeslemeUcaDegiyor(besleme, pano))
                yield return $"{kat.Ad} / {pano.Ad} — atanmış üst panoya besleme güzergâhını güncelleyin.";
        }
    }

    // Katlar arası besleme her katta ayrı parça, eski projelerde ise tek rota olarak saklanabilir.
    private static bool BeslemeUcaDegiyor(List<BpcPanoBeslemeHatti> parcalar, BasitProjeEleman pano)
    {
        return parcalar.Where(h => h.Rota.Count >= 2).Any(h =>
            Mesafe(h.Rota[0], new(pano.X, pano.Y)) <= 0.01 || Mesafe(h.Rota[^1], new(pano.X, pano.Y)) <= 0.01);
    }
}
