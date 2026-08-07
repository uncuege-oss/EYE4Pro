using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Architecture.Model3D;
using Kst.Acoustics.Core.Architecture.Models;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Architecture.Exporting;

/// <summary>
/// Mimari modeli (M35 `PlanTaslagi.Model3D`) M50 ortak bina geometrisine (`BinaGeometri`,
/// `.e4bina`) cevirir. Mimari = uretici/tohum: kavramsal dikdortgen bloklar M50 poligon
/// odalarina indirgenir; kullanici detayi M50'de inceltir. Tek yon (export); mimari Id'leri
/// korunarak diger disiplinler (M12/M08/M21) kararli OdaId ile baglanabilir.
/// </summary>
public static class BinaGeometriAdapteri
{
    /// <summary>
    /// Mimari projeden M50 bina geometrisi uretir. 3D model yoksa yalniz bina adi taşıyan boş
    /// geometri doner (kat/oda eklenmez).
    /// </summary>
    public static BinaGeometri Donustur(MimariProje? proje)
    {
        // `BinaGeometriServisi.YeniOlustur` kullanılır ki üretilen dosya güncel
        // `SemaSurum` ile damgalansın (elle `new BinaGeometri()` eski sürümde kalırdı).
        var geometri = BinaGeometriServisi.YeniOlustur(
            string.IsNullOrWhiteSpace(proje?.ProjeAdi) ? "Mimari Bina" : proje!.ProjeAdi.Trim());

        var model = proje?.PlanTaslagi?.Model3D;
        if (model is null)
            return geometri;

        foreach (var kat in model.Katlar.OrderBy(k => k.KatNo))
            geometri.Katlar.Add(KatDonustur(kat));

        return geometri;
    }

    private static BinaKat KatDonustur(Kat3DModel kat)
    {
        var binaKat = new BinaKat
        {
            Id = KatId(kat),
            Ad = string.IsNullOrWhiteSpace(kat.Ad) ? $"Kat {kat.KatNo}" : kat.Ad,
            KatNo = kat.KatNo,
            KatYuksekligiM = kat.KatYuksekligiM > 0 ? kat.KatYuksekligiM : 3.0
        };

        foreach (var mahal in kat.Mahaller.OrderBy(m => m.Ad, StringComparer.CurrentCulture))
            binaKat.Odalar.Add(OdaDonustur(mahal));

        // Duvar doğrularından açıklık Bas/Bit türetebilmek için duvar sözlüğü.
        var duvarlar = kat.Duvarlar.ToDictionary(d => d.Id, d => d, StringComparer.OrdinalIgnoreCase);
        foreach (var aciklik in kat.Acikliklar)
        {
            var binaAciklik = AciklikDonustur(aciklik, duvarlar);
            if (binaAciklik is not null)
                binaKat.Acikliklar.Add(binaAciklik);
        }

        return binaKat;
    }

    private static BinaOda OdaDonustur(MahalHacmi3D mahal)
    {
        var x = mahal.X;
        var y = mahal.Y;
        var g = Math.Max(0, mahal.GenislikM);
        var d = Math.Max(0, mahal.DerinlikM);

        return new BinaOda
        {
            Id = OdaId(mahal),
            Ad = string.IsNullOrWhiteSpace(mahal.Ad) ? "Mahal" : mahal.Ad,
            AlanM2 = mahal.AlanM2,
            Poligon = new List<Point2D>
            {
                new(x, y),
                new(x + g, y),
                new(x + g, y + d),
                new(x, y + d)
            }
        };
    }

    private static BinaAciklik? AciklikDonustur(Aciklik3D aciklik, IReadOnlyDictionary<string, Duvar3D> duvarlar)
    {
        // M50 yalnız Kapı/Pencere taşır; geçiş/şaft boşluğu/ tanımsız dışa aktarılmaz.
        BinaAciklikTipi tip;
        if (aciklik.Tip == Aciklik3DTipi.Pencere)
            tip = BinaAciklikTipi.Pencere;
        else if (aciklik.Tip == Aciklik3DTipi.Kapi)
            tip = BinaAciklikTipi.Kapi;
        else
            return null;

        var genislik = Math.Max(0, aciklik.GenislikM);
        var (bas, bit) = BasBitTuret(aciklik, genislik, duvarlar);

        return new BinaAciklik
        {
            Id = aciklik.Id,
            Tip = tip,
            Bas = bas,
            Bit = bit,
            GenislikM = genislik,
            YukseklikM = Math.Max(0, aciklik.YukseklikM),
            EsikYuksekligiM = Math.Max(0, aciklik.AltKotuM),
            ManuelEklendi = true,
            Kaynak = "mimari"
        };
    }

    /// <summary>
    /// Açıklık merkezini, bağlı duvar doğrusu yönünde ±genişlik/2 ötelenerek Bas/Bit'e çevirir.
    /// Duvar bulunamaz veya sıfır uzunluksa yatay segment varsayar.
    /// </summary>
    private static (Point2D Bas, Point2D Bit) BasBitTuret(
        Aciklik3D aciklik,
        double genislik,
        IReadOnlyDictionary<string, Duvar3D> duvarlar)
    {
        var yari = genislik / 2.0;
        var mx = aciklik.MerkezX;
        var my = aciklik.MerkezY;

        double dirX = 1, dirY = 0;
        if (!string.IsNullOrWhiteSpace(aciklik.DuvarId) &&
            duvarlar.TryGetValue(aciklik.DuvarId, out var duvar))
        {
            var dx = duvar.BitisX - duvar.BaslangicX;
            var dy = duvar.BitisY - duvar.BaslangicY;
            var uzunluk = Math.Sqrt(dx * dx + dy * dy);
            if (uzunluk > 1e-9)
            {
                dirX = dx / uzunluk;
                dirY = dy / uzunluk;
            }
        }

        return (new Point2D(mx - dirX * yari, my - dirY * yari),
                new Point2D(mx + dirX * yari, my + dirY * yari));
    }

    /// <summary>Kat Id'si: kaynak kat taslağı Id'si varsa onu, yoksa kat numarasından kararlı Id.</summary>
    private static string KatId(Kat3DModel kat)
        => string.IsNullOrWhiteSpace(kat.KaynakKatTaslagiId)
            ? $"mimari-kat-{kat.KatNo}"
            : kat.KaynakKatTaslagiId;

    /// <summary>
    /// Oda Id'si: kaynak mahal bloğu Id'si (plan bloğu) varsa onu kullanır — diğer disiplinler
    /// bu kararlı OdaId'ye bağlanır. Yoksa 3D mahal Id'sine düşer.
    /// </summary>
    private static string OdaId(MahalHacmi3D mahal)
        => string.IsNullOrWhiteSpace(mahal.KaynakMahalBloguId) ? mahal.Id : mahal.KaynakMahalBloguId;
}
