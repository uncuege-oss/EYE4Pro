using CommunityToolkit.Mvvm.Input;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

namespace Kst.Acoustics.App.Electric.BasitProjeCiz.ViewModels;

public partial class BasitProjeCizViewModel
{

    [RelayCommand]
    public void PrizMahalKablolariniCek()
    {
        if (!PrizMahalKablosuAdimi || SeciliKat is not { } kat || KilitliyseDur("mahal içi priz kablosu çekmek")) return;
        if (KabloModuAktif) KabloModuKapat();
        ElemanSnapshotAl(kat);
        var uyarilar = PrizTasarimServisi.MahalKablolariniCek(kat);
        GuncelleKabloGorunum();
        DurumMesaji = uyarilar.FirstOrDefault() ?? "Mahallerdeki priz kabloları çizildi.";
        RotaUyarilariniGoster(uyarilar);
    }

    [RelayCommand]
    public void PrizPanoKablolariniCek()
    {
        if (!PrizPanoKablosuAdimi || SeciliKat is not { } kat || KilitliyseDur("priz pano kablosu çekmek")) return;
        if (KabloModuAktif) KabloModuKapat();
        ElemanSnapshotAl(kat);
        var uyarilar = PrizTasarimServisi.PanoKablolariniCek(kat);
        TaliPanolariAnaPanoyaBagla(kat, kat.Elemanlar.Where(e => e.Tip == BpcElemanTipi.KatPanosu).ToList());
        uyarilar.AddRange(EksikPanoBeslemeleri(kat, prizDahil: true));
        GuncelleKabloGorunum();
        DurumMesaji = uyarilar.FirstOrDefault() ?? "Priz buatları ve doğrudan beslenen prizler atanan panolara bağlandı.";
        RotaUyarilariniGoster(uyarilar);
    }

    [RelayCommand]
    public void PrizAsamasiManuelKablo()
    {
        if (!PrizMahalKablosuAdimi && !PrizPanoKablosuAdimi) return;
        if (KabloModuAktif) { KabloModuKapat(); return; }
        if (SeciliKat == null || KilitliyseDur("priz kablosu çekmek")) return;
        KabloModuToggle();
    }

    private BasitProjeEleman? PrizKabloHedefi(BasitProjeKat kat, double x, double y)
    {
        bool Hedef(BasitProjeEleman e) => PrizMahalKablosuAdimi
            ? PrizTasarimServisi.PrizBuati(e) || KabloModuDurumu == 1 && PrizTasarimServisi.MahaldeBaglananPriz(e)
            : KabloModuDurumu == 4 ? e.Tip == BpcElemanTipi.KatPanosu
            : PrizTasarimServisi.PrizBuati(e) || PrizTasarimServisi.DogrudanPriz(e);
        return kat.Elemanlar.Where(Hedef)
            .Select(e => (Eleman: e, D2: (x-e.X)*(x-e.X) + (y-e.Y)*(y-e.Y)))
            .Where(p => p.D2 < 0.45 * 0.45).OrderBy(p => p.D2).Select(p => p.Eleman).FirstOrDefault();
    }

    private void PrizAsamasiNoktaSec(BasitProjeKat kat, double x, double y)
    {
        var hedef = PrizKabloHedefi(kat, x, y);
        if (hedef == null) { KabloTiklamaDetect(x + kat.OriginOffset.X, y + kat.OriginOffset.Y); return; }
        if (PrizPanoKablosuAdimi)
        {
            if (_buatPanoSeciliBuat == null)
            {
                if (PrizTasarimServisi.PrizBuati(hedef) &&
                    !PrizTasarimServisi.BaglanabilirBuatlar(kat).Contains(hedef.Id))
                { KabloModuMesaji = "Önce bu buata mahal içi priz hattını bağlayın."; return; }
                _buatPanoSeciliBuat = hedef;
                KabloModuDurumu = 4;
                KabloModuMesaji = "Şimdi bu mahal için atanmış panoyu seçin.";
                return;
            }
            var kaynak = _buatPanoSeciliBuat;
            if (BasitProjeCizKabloServisi.MahaleAtananPano(kaynak.OdaId, kat.Elemanlar)?.Id != hedef.Id)
            { KabloModuMesaji = "Seçilen pano bu mahale atanmamış."; return; }
            ElemanSnapshotAl(kat);
            var gecis = kat.Odalar.Where(o => o.GecisSecili).Select(o => o.Id);
            var bag = PrizTasarimServisi.PanoBaglantisiniKur(kat, kaynak, hedef, gecis);
            var uyarilar = BasitProjeCizKabloServisi.BuatPanoRotalariniHesapla(kat, "P");
            _buatPanoSeciliBuat = null;
            SeciliDevreId = bag.Id;
            KabloModuDurumu = 3;
            KabloModuMesaji = uyarilar.FirstOrDefault() ?? "Priz hattı bağlandı. Sıradaki buatı veya doğrudan beslenen prizi seçin.";
            GuncelleKabloGorunum();
            RotaUyarilariniGoster(uyarilar);
            return;
        }

        if (PrizTasarimServisi.PrizBuati(hedef))
        {
            _aktifPrizDevre = kat.PrizDevreleri.FirstOrDefault(d => d.BuatId == hedef.Id) ?? new BpcPrizDevre { BuatId = hedef.Id };
            KabloModuDurumu = 1;
            SeciliDevreId = _aktifPrizDevre.Id;
            GuncelleKabloGorunum();
            KabloModuMesaji = "Aynı mahaldeki prizi seçin. Bağlı prize tekrar tıklayarak hattan çıkarabilirsiniz.";
            return;
        }
        if (_aktifPrizDevre == null) return;
        if (_aktifPrizDevre.PrizElemanIds.Contains(hedef.Id))
        {
            ElemanSnapshotAl(kat);
            _aktifPrizDevre.PrizElemanIds.Remove(hedef.Id);
            if (_aktifPrizDevre.PrizElemanIds.Count == 0)
            {
                kat.PrizDevreleri.Remove(_aktifPrizDevre);
                KullanilmayanBuatPanoBaglantisiniSil(kat, _aktifPrizDevre.BuatId, "P");
            }
            AktifPrizRotasiniCiz(kat);
            return;
        }
        PrizleriAktifBuataBagla(kat, [hedef]);
    }

    private void PrizleriAktifBuataBagla(BasitProjeKat kat, IEnumerable<BasitProjeEleman> adaylar)
    {
        var aktif = _aktifPrizDevre;
        var buat = kat.Elemanlar.FirstOrDefault(e => e.Id == aktif?.BuatId);
        if (aktif == null || buat == null) return;
        var bagli = kat.PrizDevreleri.Where(d => d.BuatId == buat.Id).SelectMany(d => d.PrizElemanIds).ToHashSet();
        int bos = BasitProjeCizKabloServisi.PrizBuatKapasitesi - bagli.Count;
        if (bos <= 0) { KabloModuMesaji = "Bu priz buatına en fazla 7 priz bağlanabilir."; return; }
        var yeni = adaylar.Where(p => PrizTasarimServisi.MahaldeBaglananPriz(p) && buat.OdaId != null &&
            p.OdaId == buat.OdaId && !bagli.Contains(p.Id)).Take(bos).Select(e => e.Id).ToHashSet();
        if (yeni.Count == 0) { KabloModuMesaji = "Aynı mahaldeki normal prizleri seçin."; return; }
        ElemanSnapshotAl(kat);
        foreach (var diger in kat.PrizDevreleri.Where(d => d != aktif).ToList())
        {
            if (diger.PrizElemanIds.RemoveAll(yeni.Contains) == 0) continue;
            BasitProjeCizKabloServisi.PrizRotaHesapla(kat, diger);
            if (diger.PrizElemanIds.Count == 0)
            {
                kat.PrizDevreleri.Remove(diger);
                KullanilmayanBuatPanoBaglantisiniSil(kat, diger.BuatId, "P");
            }
        }
        if (!kat.PrizDevreleri.Contains(aktif)) kat.PrizDevreleri.Add(aktif);
        aktif.PrizElemanIds.AddRange(yeni);
        AktifPrizRotasiniCiz(kat);
        KabloModuMesaji = $"{yeni.Count} priz bağlandı. Buatta {bagli.Count + yeni.Count}/7 priz var.";
    }
}
