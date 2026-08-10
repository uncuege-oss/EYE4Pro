using System.Collections.Generic;
using System.Linq;
using System;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;

namespace Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

public sealed record BinaGeometriImportSonucu(
    int YeniKat, int GuncellenenKat, int KorunanEskiKat, List<string> Uyarilar);

/// <summary>
/// M50 → M12 köprüsü: .e4bina geometrisini BasitProjeCizProje'ye uygular.
///
/// Id kararlılığı esastır:
/// - Kat ve oda Id'leri M50'den AYNEN alınır → elemanlar `OdaId` ile bağlanabilir,
///   geometri yeniden yüklendiğinde bağlar kopmaz.
/// - Mevcut katlar Id eşleşmesiyle güncellenir; Elemanlar/Devreler/Kablolar/Mahal
///   listesi gibi M12'ye özgü veriye DOKUNULMAZ.
/// - Odalarda M12'ye özgü bayraklar (GecisSecili, AsmaTavan, MahalListesineDahil)
///   Id eşleşen odalarda korunur.
/// - Geometride artık olmayan eski kat: hiçbir M12 elektrik verisi yoksa silinir;
///   eleman/devre/tava/topraklama/mahal vb. taşıyorsa korunur + uyarı.
/// </summary>
public static class BinaGeometriImportServisi
{
    public static BinaGeometriImportSonucu Uygula(
        BasitProjeCizProje proje, BinaGeometri geo, string? dosyaYolu)
    {
        Dogrula(proje, geo);
        var uyarilar = new List<string>();
        int yeni = 0, guncellenen = 0, korunan = 0;

        // Bağlantı bilgisi: yol + revizyon damgası + gömülü kopya
        proje.BinaGeometriYolu = dosyaYolu;
        proje.BinaGeometriRevizyon = geo.GeometriRevizyon;
        proje.BinaGeometriKopya = geo;

        var geoKatIdleri = new HashSet<string>(geo.Katlar.Select(k => k.Id));
        var tumEskiOdalar = proje.Katlar
            .SelectMany(k => k.Odalar)
            .ToDictionary(o => o.Id, StringComparer.Ordinal);
        var odaHedefKatlari = geo.Katlar
            .SelectMany(k => k.Odalar.Select(o => (OdaId: o.Id, KatId: k.Id)))
            .ToDictionary(x => x.OdaId, x => x.KatId, StringComparer.Ordinal);

        // Aynı kararlı oda Id'si yeni geometride başka kata taşındıysa eski katın
        // koleksiyonundan ayır. M12 bayrakları global lookup üzerinden yeni kata
        // aktarılır; böylece iki katta yinelenen OdaId bırakılmaz.
        foreach (var eskiKat in proje.Katlar)
        {
            for (int i = eskiKat.Odalar.Count - 1; i >= 0; i--)
            {
                var oda = eskiKat.Odalar[i];
                if (odaHedefKatlari.TryGetValue(oda.Id, out var hedefKatId) &&
                    !string.Equals(eskiKat.Id, hedefKatId, StringComparison.Ordinal))
                    eskiKat.Odalar.RemoveAt(i);
            }
        }

        // Geometride olmayan eski katlar: yalnız gerçekten elektrik verisi yoksa kaldır.
        foreach (var eski in proje.Katlar.Where(k => !geoKatIdleri.Contains(k.Id)).ToList())
        {
            var veriSayisi = ElektrikVerisiSayisi(eski);
            if (veriSayisi == 0)
            {
                proje.Katlar.Remove(eski);
            }
            else
            {
                korunan++;
                uyarilar.Add(
                    $"'{eski.Ad}' katı yeni geometride yok; {veriSayisi} M12 elektrik kaydı taşıdığı için korundu.");
            }
        }

        foreach (var geoKat in geo.Katlar)
        {
            var hedef = proje.Katlar.FirstOrDefault(k => k.Id == geoKat.Id);
            if (hedef is null)
            {
                hedef = new BasitProjeKat { Id = geoKat.Id };
                proje.Katlar.Add(hedef);
                yeni++;
            }
            else
            {
                guncellenen++;
            }
            KatiGuncelle(hedef, geoKat, tumEskiOdalar);
        }

        // Kat sırası M50 sol panelindeki görsel sırayı izler (korunan eski katlar sona düşer).
        var geoSira = geo.Katlar.Select((k, i) => (k.Id, Index: i)).ToDictionary(x => x.Id, x => x.Index);
        var sirali = proje.Katlar
            .OrderBy(k => geoKatIdleri.Contains(k.Id) ? 0 : 1)
            .ThenBy(k => geoSira.TryGetValue(k.Id, out var index) ? index : int.MaxValue)
            .ToList();
        proje.Katlar.Clear();
        foreach (var k in sirali) proje.Katlar.Add(k);

        return new BinaGeometriImportSonucu(yeni, guncellenen, korunan, uyarilar);
    }

    /// <summary>
    /// Mutasyondan önce tüm kimlik varsayımlarını doğrular. Böylece bozuk bir
    /// sidecar yarım uygulanıp projeyi karma durumda bırakamaz.
    /// </summary>
    private static void Dogrula(BasitProjeCizProje proje, BinaGeometri geo)
    {
        if (geo.Katlar.Any(k => string.IsNullOrWhiteSpace(k.Id)))
            throw new InvalidOperationException("Bina geometrisinde kimliği boş kat var.");
        if (geo.Katlar.GroupBy(k => k.Id, StringComparer.Ordinal).Any(g => g.Count() > 1))
            throw new InvalidOperationException("Bina geometrisinde yinelenen kat kimliği var.");
        if (geo.Katlar.SelectMany(k => k.Odalar).Any(o => string.IsNullOrWhiteSpace(o.Id)))
            throw new InvalidOperationException("Bina geometrisinde kimliği boş oda var.");
        if (geo.Katlar.SelectMany(k => k.Odalar)
            .GroupBy(o => o.Id, StringComparer.Ordinal).Any(g => g.Count() > 1))
            throw new InvalidOperationException("Bina geometrisinde katlar arasında yinelenen oda kimliği var.");
        if (proje.Katlar.SelectMany(k => k.Odalar)
            .GroupBy(o => o.Id, StringComparer.Ordinal).Any(g => g.Count() > 1))
            throw new InvalidOperationException("Mevcut M12 projesinde katlar arasında yinelenen oda kimliği var.");
    }

    private static int ElektrikVerisiSayisi(BasitProjeKat kat)
    {
        int toplam = kat.Elemanlar?.Count ?? 0;
        toplam += kat.Devreler?.Count ?? 0;
        toplam += kat.VavienGruplari?.Count ?? 0;
        toplam += kat.Kablolar?.Count ?? 0;
        toplam += kat.BuatPanoBaglantilari?.Count ?? 0;
        toplam += kat.PrizDevreleri?.Count ?? 0;
        toplam += kat.MekanikDevreleri?.Count ?? 0;
        toplam += kat.KabloTavalari?.Count ?? 0;
        toplam += kat.PanoBeslemeHatlari?.Count ?? 0;
        toplam += kat.SistemDevreleri?.Count ?? 0;
        toplam += kat.MahalListesi?.Count ?? 0;

        var topraklama = kat.Topraklama;
        if (topraklama is not null)
        {
            toplam += topraklama.SecimNoktalari?.Count ?? 0;
            toplam += topraklama.Seritler?.Count ?? 0;
            toplam += topraklama.Kaziklar?.Count ?? 0;
            if (topraklama.EkTopraklamaVar) toplam++;
        }

        return toplam;
    }

    private static void KatiGuncelle(
        BasitProjeKat hedef,
        BinaKat kaynak,
        IReadOnlyDictionary<string, BasitProjeOda> tumEskiOdalar)
    {
        hedef.Ad = kaynak.Ad;
        hedef.KatNo = kaynak.KatNo;
        hedef.KatYuksekligiM = kaynak.KatYuksekligiM;
        hedef.DxfFilePath = kaynak.DxfFilePath;
        hedef.UnitWarning = kaynak.UnitWarning;
        hedef.OriginOffset = new Point2D(kaynak.OriginOffset.X, kaynak.OriginOffset.Y);
        hedef.DxfGoster = kaynak.DxfGoster;
        hedef.BloklariGoster = kaynak.BloklariGoster;

        // Odalar: Id eşleşmesiyle birleştir — M12 bayrakları korunur.
        hedef.Odalar.Clear();
        foreach (var geoOda in kaynak.Odalar)
        {
            tumEskiOdalar.TryGetValue(geoOda.Id, out var eski);
            hedef.Odalar.Add(new BasitProjeOda
            {
                Id = geoOda.Id,
                Ad = geoOda.Ad,
                AlanM2 = geoOda.AlanM2,
                Poligon = geoOda.Poligon.Select(p => new Point2D(p.X, p.Y)).ToList(),
                GecisSecili = eski?.GecisSecili ?? false,
                AsmaTavan = eski?.AsmaTavan ?? false,
                MahalListesineDahil = eski?.MahalListesineDahil ?? true
            });
        }

        // DXF arka planı + arclar + açıklıklar geometriden birebir alınır.
        hedef.DxfSegmentleri.Clear();
        foreach (var s in kaynak.DxfSegmentler)
            hedef.DxfSegmentleri.Add(new BpcDxfSegment { A = s.A, B = s.B, Layer = s.Layer });

        hedef.DxfArclari = kaynak.DxfArclar
            .Select(a => new BpcArc { Merkez = a.Merkez, R = a.R, BasAciDeg = a.BasAciDeg, BitAciDeg = a.BitAciDeg })
            .ToList();

        hedef.Acikliklar = kaynak.Acikliklar
            .Select(a => new BpcAciklik
            {
                Id = a.Id,
                Bas = a.Bas,
                Bit = a.Bit,
                Tip = a.Tip == BinaAciklikTipi.Pencere ? BpcAciklikTipi.Pencere : BpcAciklikTipi.Kapı,
                IptalEdildi = a.IptalEdildi,
                ManuelEklendi = a.ManuelEklendi,
                Kaynak = a.Kaynak
            })
            .ToList();
    }
}
