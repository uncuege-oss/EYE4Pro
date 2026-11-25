using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

namespace Kst.Acoustics.App.Electric.BasitProjeCiz.ViewModels;

public partial class BasitProjeCizViewModel
{
    private bool MahalKabloAsamasinda => AydinlatmaAkisiAktif && AydinlatmaAsamasi == BpcAydinlatmaAsamasi.MahalKablosu;
    private bool PanoKabloAsamasinda => AydinlatmaAkisiAktif && AydinlatmaAsamasi == BpcAydinlatmaAsamasi.PanoKablosu;

    public void GeçisMahaliniAyarla(BasitProjeOda oda, bool izin)
    {
        if (SeciliKat is not { } kat || !kat.Odalar.Contains(oda) || oda.GecisSecili == izin ||
            KilitliyseDur("geçiş mahalini değiştirmek")) return;
        ElemanSnapshotAl(kat);
        oda.GecisSecili = izin;
        if (!izin)
            foreach (var bag in kat.BuatPanoBaglantilari.Where(b => !PrizAkisiAktif || b.LinyePrefix == "P"))
                bag.GecisOdaIds.RemoveAll(id => id == oda.Id);
        if (kat.BuatPanoBaglantilari.Count > 0) RotalarBayat = true;
        DurumMesaji = izin ? $"{oda.Ad} kablo geçişine açıldı." : $"{oda.Ad} için kablo geçişi kaldırıldı.";
    }

    partial void OnKabloRotaTipiChanged(BpcRotaTipi value)
    {
        if (!MahalKabloAsamasinda || _aktifDevre == null || SeciliKat is not { } kat || ProjeKilitli) return;
        ElemanSnapshotAl(kat);
        _aktifDevre.ManuelRotaTipi = value;
        AktifDevreRotasiniCiz(kat);
    }

    private bool DevreAsamadaDuzenlenebilir(BasitProjeKat kat, string devreId)
    {
        if (PrizAkisiAktif)
        {
            if (PrizPanoKablosuAdimi) return kat.BuatPanoBaglantilari.Any(b => b.Id == devreId && b.LinyePrefix == "P");
            return PrizMahalKablosuAdimi && kat.PrizDevreleri.Any(d => d.Id == devreId &&
                !d.PrizElemanIds.Contains(d.BuatId ?? ""));
        }
        if (MahalKabloAsamasinda) return kat.Devreler.Any(d => d.Id == devreId);
        if (PanoKabloAsamasinda) return kat.BuatPanoBaglantilari.Any(b => b.Id == devreId && b.LinyePrefix == "A");
        return true;
    }

    [RelayCommand]
    public void MahalAydinlatmaKablolariniCek() => MahalAydinlatmaKablolariniCekIc(null);

    private void MahalAydinlatmaKablolariniCekIc(string? odaId)
    {
        if (SeciliKat is not { } kat || KilitliyseDur("mahal kablolarını çekmek")) return;
        ElemanSnapshotAl(kat);
        AydinlatmaTasarimServisi.MahalKablolariniCek(kat, KabloRotaTipi, odaId);
        GuncelleKabloGorunum();
        GuncelleElemanVurgu();
        var eksikler = AydinlatmaTasarimServisi.EksikBaglantilariBul(kat, panoGerekli: false);
        DurumMesaji = KabloModuMesaji = eksikler.FirstOrDefault() ?? "Mahal içi aydınlatma kabloları çizildi.";
    }

    [RelayCommand]
    public void AtanmisPanolaraKabloCek()
    {
        if (SeciliKat is not { } kat || KilitliyseDur("pano kablolarını çekmek")) return;
        ElemanSnapshotAl(kat);
        var uyarilar = AydinlatmaTasarimServisi.PanoKablolariniCek(kat);
        TaliPanolariAnaPanoyaBagla(kat, kat.Elemanlar.Where(e => e.Tip == BpcElemanTipi.KatPanosu).ToList());
        uyarilar.AddRange(EksikPanoBeslemeleri(kat));
        uyarilar.AddRange(AydinlatmaTasarimServisi.EksikBaglantilariBul(kat, panoGerekli: true));
        GuncelleKabloGorunum();
        RotaUyarilariniGoster(uyarilar.Distinct().ToList());
        DurumMesaji = KabloModuMesaji = uyarilar.FirstOrDefault() ?? "Aydınlatma buatları atanan panolara bağlandı.";
    }

}
