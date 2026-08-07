using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using BinaModels = Kst.Acoustics.Core.Bina.Models;

namespace Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

/// Gerçek kod Core/Bina/Services/AciklikYardimci'ya taşındı.
/// Mevcut çağrıcılar için BasitProjeKat ↔ BinaKat dönüştürücü delegasyon.
public static class AciklikYardimci
{
    public static bool IsAciklikYakininda(Point2D nokta, BasitProjeKat kat, double tolM = 0.30)
    {
        foreach (var a in kat.Acikliklar)
        {
            if (a.IptalEdildi) continue;
            if (PoligonYardimci.SegmentMesafesi(nokta, a.Bas, a.Bit) < tolM)
                return true;
        }
        return false;
    }

    public static IEnumerable<(Point2D Bas, Point2D Bit)> AktifAcikliklar(BasitProjeKat kat)
        => kat.Acikliklar.Where(a => !a.IptalEdildi).Select(a => (a.Bas, a.Bit));

    public static void DisindaKalanlariSil(BasitProjeKat kat)
    {
        var binaKat = ToBinaKat(kat);
        Kst.Acoustics.Core.Bina.Services.AciklikYardimci.DisindaKalanlariSil(binaKat);
        // Silinen Id'leri BpcAciklik listesine yansıt
        var kalanIds = new HashSet<string>(binaKat.Acikliklar.Select(a => a.Id));
        kat.Acikliklar.RemoveAll(a => !kalanIds.Contains(a.Id));
    }

    public static int KomsuDuvarlariTamamla(BasitProjeKat kat)
    {
        var binaKat = ToBinaKat(kat);
        int eklenen = Kst.Acoustics.Core.Bina.Services.AciklikYardimci.KomsuDuvarlariTamamla(binaKat);
        // Eklenen yansımaları BpcAciklik listesine aktar
        var mevcutIds = new HashSet<string>(kat.Acikliklar.Select(a => a.Id));
        foreach (var a in binaKat.Acikliklar)
        {
            if (!mevcutIds.Contains(a.Id))
                kat.Acikliklar.Add(AciklikTespitServisi.ToBpc(a));
        }
        return eklenen;
    }

    public static BpcAciklik? KomsuDuvaraYansit(BasitProjeKat kat, BpcAciklik kaynak)
    {
        var binaKat = ToBinaKat(kat);
        var binaKaynak = AciklikTespitServisi.ToBina(kaynak);
        // ReferenceEquals kontrolünün çalışması için listede kaynak ile AYNI referans olmalı.
        // ToBinaKat zaten listeyi kopyalar; burada kaynağın kopyasını tam referansla değiştir.
        var idx = binaKat.Acikliklar.FindIndex(a => a.Id == binaKaynak.Id);
        if (idx >= 0)
            binaKat.Acikliklar[idx] = binaKaynak;
        else
            binaKat.Acikliklar.Add(binaKaynak);

        var yansima = Kst.Acoustics.Core.Bina.Services.AciklikYardimci.KomsuDuvaraYansit(binaKat, binaKaynak);
        if (yansima == null) return null;

        var bpcYansima = AciklikTespitServisi.ToBpc(yansima);
        kat.Acikliklar.Add(bpcYansima);
        return bpcYansima;
    }

    private static BinaModels.BinaKat ToBinaKat(BasitProjeKat kat)
    {
        var binaKat = new BinaModels.BinaKat
        {
            Id = kat.Id, Ad = kat.Ad, KatNo = kat.KatNo,
            KatYuksekligiM = kat.KatYuksekligiM,
            OriginOffset = kat.OriginOffset,
            Acikliklar = kat.Acikliklar.Select(AciklikTespitServisi.ToBina).ToList()
        };
        foreach (var oda in kat.Odalar)
        {
            binaKat.Odalar.Add(new BinaModels.BinaOda
            {
                Id = oda.Id, Ad = oda.Ad,
                AlanM2 = oda.AlanM2,
                Poligon = new List<Kst.Acoustics.Core.Common.Geometry.Point2D>(oda.Poligon)
            });
        }
        return binaKat;
    }
}
