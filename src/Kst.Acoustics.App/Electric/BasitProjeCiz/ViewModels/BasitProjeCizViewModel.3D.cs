using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.App.Controls;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.App.Electric.BasitProjeCiz.ViewModels;

public partial class BasitProjeCizViewModel
{
    public void GuncelleBinaGeometrisi()
    {
        // A4: 2D modda + Sadece2DSeciliKat aktifken yalnız seçili katı render et.
        // 3D'de her zaman tüm katlar (üst-alt görselleştirme için gerekli).
        var katList = (Sadece2DSeciliKat
                       && Projection == GeometryProjectionMode.Top2D
                       && SeciliKat != null)
            ? new List<BasitProjeKat> { SeciliKat }
            : Katlar.ToList();

        System.Threading.Tasks.Task.Run(() =>
        {
            var surfaces = _geometri.BinaModelineCevir(katList);
            var bg = _geometri.ArkaPlanSegmentleri(katList);

            // Açıklıkları dünya koordinatına çevir (OriginOffset + kat Z'si uygulanır).
            var ack = new List<(Point3D Bas, Point3D Bit, bool Iptal, BpcAciklikTipi Tip)>();
            double tabanZ = 0;
            foreach (var kat in katList.AsEnumerable().Reverse())
            {
                double ox = kat.OriginOffset.X, oy = kat.OriginOffset.Y;
                foreach (var a in kat.Acikliklar)
                {
                    ack.Add((
                        new Point3D(a.Bas.X + ox, a.Bas.Y + oy, tabanZ),
                        new Point3D(a.Bit.X + ox, a.Bit.Y + oy, tabanZ),
                        a.IptalEdildi,
                        a.Tip));
                }
                tabanZ += kat.KatYuksekligiM;
            }

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                RenderSurfaces = surfaces;
                RenderBackgroundSegments = bg.Count > 0 ? bg : null;
                RenderAcikliklar = ack.Count > 0 ? ack : null;
            });
        });
    }

    public void GuncelleElemanVeKablolar()
    {
        var katList = (Sadece2DSeciliKat
                       && Projection == GeometryProjectionMode.Top2D
                       && SeciliKat != null)
            ? new List<BasitProjeKat> { SeciliKat }
            : Katlar.ToList();
        foreach (var kat in katList)
            NormalizeElemanTipleri(kat);

        var armatürPts = new List<AcousticPoint>();
        var manuelPts  = new List<AcousticPoint>();
        int pointId = 1;
        double tabanZ = 0;

        foreach (var kat in katList.AsEnumerable().Reverse())
        {
            double ox = kat.OriginOffset.X, oy = kat.OriginOffset.Y;
            foreach (var el in kat.Elemanlar.Where(ElemanAktifPaftadaMi))
            {
                var pos = new Point3D(el.X + ox, el.Y + oy, tabanZ);
                var label = ElemanEtiket(el);
                var pt = new AcousticPoint(pointId++, label, AcousticPointType.Source, pos);
                if (BasitProjeCizKabloServisi.ElemanArmaturMi(el.Tip))
                    armatürPts.Add(pt);
                else
                    manuelPts.Add(pt);
            }
            tabanZ += kat.KatYuksekligiM;
        }

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            RenderArmatürNoktalar = armatürPts.Count > 0 ? armatürPts : null;
            RenderManuelNoktalar  = manuelPts.Count  > 0 ? manuelPts  : null;
            RenderKablolar();
        });
    }

    public void Guncelle3DGorunum()
    {
        GuncelleBinaGeometrisi();
        GuncelleElemanVeKablolar();
    }

    private bool ElemanAktifPaftadaMi(BasitProjeEleman el)
    {
        if (el.Tip == BpcElemanTipi.KatPanosu)
            return true;

        if (el.Tip is BpcElemanTipi.KlimaIcUnite or BpcElemanTipi.KlimaDisUnite)
            return AktifPafta == BpcPaftaTipi.Mekanik;

        // Priz tipleri yalnız Priz paftasında görünür
        if (BasitProjeCizKabloServisi.ElemanPrizMi(el.Tip))
            return AktifPafta == BpcPaftaTipi.Priz;

        if (el.Tip == BpcElemanTipi.Buat)
            return AktifPafta == el.PaftaTipi;

        if (AktifPafta == BpcPaftaTipi.KabloTavasi)
            return false;

        var tanim = BpcSistemKatalogu.Get(AktifPafta);
        return tanim.CihazTipleri.Contains(el.Tip)
               || tanim.AraToplayiciTipleri.Contains(el.Tip)
               || tanim.MerkezMi(el.Tip);
    }

    private static void NormalizeElemanTipleri(BasitProjeKat kat)
    {
        foreach (var el in kat.Elemanlar)
        {
            var dogruTip = BasitProjeCizKutuphaneServisi.TipBlokAdindan(el.BlokAdi);
            if (dogruTip is { } t && el.Tip != t)
            {
                el.Tip = t;
                el.PaftaTipi = PaftaForTip(t);
            }
        }
    }

    private static BpcPaftaTipi PaftaForTip(BpcElemanTipi tip)
    {
        if (tip == BpcElemanTipi.KatPanosu) return BpcPaftaTipi.Aydinlatma;
        if (tip is BpcElemanTipi.KlimaIcUnite or BpcElemanTipi.KlimaDisUnite)
            return BpcPaftaTipi.Mekanik;
        // Priz tipleri Priz paftasına aittir; aydınlatma görünümünde karışmamalı.
        if (BasitProjeCizKabloServisi.ElemanPrizMi(tip)) return BpcPaftaTipi.Priz;
        foreach (var s in BpcSistemKatalogu.Tumu)
            if (s.CihazTipleri.Contains(tip) || s.AraToplayiciTipleri.Contains(tip) || s.MerkezMi(tip))
                return s.Sistem;
        return BpcPaftaTipi.Aydinlatma;
    }

    private static string ElemanEtiket(BasitProjeEleman el)
    {
        if (IsArmaturTip(el.Tip))
        {
            var ad = string.IsNullOrWhiteSpace(el.EkranAdi) ? el.Ad : el.EkranAdi;
            if (string.IsNullOrWhiteSpace(ad)) return "L";
            return ad.Length <= 6 ? ad : ad[..6];
        }

        return el.Tip switch
        {
            BpcElemanTipi.Anahtar or BpcElemanTipi.Dimmer or BpcElemanTipi.IkiKutupluAnahtar => "A",
            BpcElemanTipi.Komütatör        => "K",
            BpcElemanTipi.DarbeAnahtari    => "D",
            BpcElemanTipi.Vavien or BpcElemanTipi.KomütatörVavien or BpcElemanTipi.Deviatör => "V",
            BpcElemanTipi.KatPanosu        => PanoEtiket(el),
            BpcElemanTipi.Sensör           => "S",
            BpcElemanTipi.Buat             => "B",
            BpcElemanTipi.Priz or BpcElemanTipi.UpsPrizi => "P",
            BpcElemanTipi.KapakliPriz or BpcElemanTipi.KapakliUpsPrizi => "KP",
            BpcElemanTipi.SivaUstuPriz or BpcElemanTipi.SivaUstuUpsPrizi => "SP",
            BpcElemanTipi.TrifazePriz      => "3P",
            BpcElemanTipi.KombinePrizKutusu=> "KPK",
            BpcElemanTipi.TavanPrizi       => "TP",
            BpcElemanTipi.UsbliMasaPrizi   => "USB",
            BpcElemanTipi.IkiliPriz        => "2P",
            BpcElemanTipi.UcluPriz         => "3P",
            BpcElemanTipi.KlimaIcUnite     => "KİÜ",
            BpcElemanTipi.KlimaDisUnite    => "KDÜ",
            // Zayıf akım
            BpcElemanTipi.DataPriz         => "D",
            BpcElemanTipi.DataRack         => "RACK",
            BpcElemanTipi.KatDagitimKutusu => "KDK",
            BpcElemanTipi.TvPriz           => "TV",
            BpcElemanTipi.TvMerkezi        => "TVM",
            BpcElemanTipi.TelefonPriz      => "T",
            BpcElemanTipi.TelefonDagitim   => "MDF",
            BpcElemanTipi.Kamera           => "CAM",
            BpcElemanTipi.CctvKayit        => "NVR",
            BpcElemanTipi.AgAnahtari       => "SW",
            BpcElemanTipi.Hoparlor         => "HP",
            BpcElemanTipi.AnonsAmfi        => "AMF",
            BpcElemanTipi.Diafon           => "DF",
            BpcElemanTipi.KapiZili         => "KZ",
            BpcElemanTipi.DiafonSantrali   => "DFS",
            BpcElemanTipi.DumanDedektor    => "DD",
            BpcElemanTipi.IsiDedektor      => "ID",
            BpcElemanTipi.KombineDedektor  => "KD",
            BpcElemanTipi.AsmaTavanDedektorGrubu => "AT",
            BpcElemanTipi.YanginButonu     => "YB",
            BpcElemanTipi.Siren            => "SR",
            BpcElemanTipi.Flasor           => "FL",
            BpcElemanTipi.YanginSantrali   => "YSP",
            _                              => "?"
        };
    }

    private static string PanoEtiket(BasitProjeEleman el)
    {
        var ad = string.IsNullOrWhiteSpace(el.EkranAdi) ? el.Ad : el.EkranAdi;
        if (string.IsNullOrWhiteSpace(ad) || ad == "Kat Panosu") return "KP";
        return ad.Length <= 6 ? ad : ad[..6];
    }
}
