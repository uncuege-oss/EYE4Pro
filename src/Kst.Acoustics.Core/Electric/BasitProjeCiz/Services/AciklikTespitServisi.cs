using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using BinaServices = Kst.Acoustics.Core.Bina.Services;
using BinaModels = Kst.Acoustics.Core.Bina.Models;

namespace Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

/// Gerçek kod Core/Bina/Services/AciklikTespitServisi'ya taşındı.
/// Mevcut çağrıcılar için BpcAciklik ↔ BinaAciklik dönüştürücü delegasyon.
public static class AciklikTespitServisi
{
    public sealed class Ayarlar
    {
        public double MinKapiR = 0.50;
        public double MaxKapiR = 1.50;
        public double MinKapiSweepDeg = 60;
        public double MaxKapiSweepDeg = 120;
        public double MaxMenteseWallDistM = 0.15;
        public double DuvarOrtTol = 0.12;
        public double DuvarOrthTol = 0.08;
        public double MinDuvarUzunluk = 0.4;
        public double SegmentBirlestirme = 0.05;
        public double MinAciklikGenisM = 0.60;
        public double MaxAciklikGenisM = 2.50;
        public double KenarTamponM = 0.20;
        public double SoveAramaUcM = 0.30;
        public double MinSoveBoyuM = 0.10;
        public double SoveEksenYakinM = 0.20;
        public double MaxKarsiDuvarM = 1.50;
        public double MinKarsiDuvarM = 0.05;
        public double DedupTolM = 0.30;
    }

    public static List<BpcAciklik> Bul(
        IReadOnlyList<(Point2D A, Point2D B)> duvarSegmentleri,
        IReadOnlyList<(Point2D Merkez, double R, double BasDeg, double BitDeg)> arclar,
        IReadOnlyList<BpcAciklik> mevcut,
        Ayarlar? ayar = null)
    {
        var binaAyar = ayar == null ? null : MapAyar(ayar);
        var binaMevcut = mevcut.Select(ToBina).ToList();
        var sonuc = BinaServices.AciklikTespitServisi.Bul(duvarSegmentleri, arclar, binaMevcut, binaAyar);
        return sonuc.Select(ToBpc).ToList();
    }

    private static BinaServices.AciklikTespitServisi.Ayarlar MapAyar(Ayarlar a) => new()
    {
        MinKapiR = a.MinKapiR, MaxKapiR = a.MaxKapiR,
        MinKapiSweepDeg = a.MinKapiSweepDeg, MaxKapiSweepDeg = a.MaxKapiSweepDeg,
        MaxMenteseWallDistM = a.MaxMenteseWallDistM,
        DuvarOrtTol = a.DuvarOrtTol, DuvarOrthTol = a.DuvarOrthTol,
        MinDuvarUzunluk = a.MinDuvarUzunluk, SegmentBirlestirme = a.SegmentBirlestirme,
        MinAciklikGenisM = a.MinAciklikGenisM, MaxAciklikGenisM = a.MaxAciklikGenisM,
        KenarTamponM = a.KenarTamponM, SoveAramaUcM = a.SoveAramaUcM,
        MinSoveBoyuM = a.MinSoveBoyuM, SoveEksenYakinM = a.SoveEksenYakinM,
        MaxKarsiDuvarM = a.MaxKarsiDuvarM, MinKarsiDuvarM = a.MinKarsiDuvarM,
        DedupTolM = a.DedupTolM
    };

    internal static BinaModels.BinaAciklik ToBina(BpcAciklik b) => new()
    {
        Id = b.Id, Bas = b.Bas, Bit = b.Bit,
        Tip = b.Tip == BpcAciklikTipi.Kapı
            ? BinaModels.BinaAciklikTipi.Kapi
            : BinaModels.BinaAciklikTipi.Pencere,
        GenislikM = System.Math.Sqrt(
            (b.Bit.X - b.Bas.X) * (b.Bit.X - b.Bas.X) +
            (b.Bit.Y - b.Bas.Y) * (b.Bit.Y - b.Bas.Y)),
        IptalEdildi = b.IptalEdildi,
        ManuelEklendi = b.ManuelEklendi,
        Kaynak = b.Kaynak
    };

    internal static BpcAciklik ToBpc(BinaModels.BinaAciklik b) => new()
    {
        Id = b.Id, Bas = b.Bas, Bit = b.Bit,
        Tip = b.Tip == BinaModels.BinaAciklikTipi.Kapi
            ? BpcAciklikTipi.Kapı
            : BpcAciklikTipi.Pencere,
        IptalEdildi = b.IptalEdildi,
        ManuelEklendi = b.ManuelEklendi,
        Kaynak = b.Kaynak
    };
}
