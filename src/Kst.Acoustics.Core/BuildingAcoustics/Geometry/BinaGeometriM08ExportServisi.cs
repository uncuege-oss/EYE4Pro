using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.Common.Geometry;
// Bu namespace'te (BuildingAcoustics.Geometry) aynı adlı B4 servisi var — M50
// dosya servisi alias ile çağrılır.
using M50GeometriServisi = Kst.Acoustics.Core.Bina.Services.BinaGeometriServisi;

namespace Kst.Acoustics.Core.BuildingAcoustics.Geometry;

/// <summary>
/// M08 → M50 köprüsü (4.3 migrasyonu): eski .e4p08 projelerindeki gömülü geometriyi
/// BinaGeometri'ye çevirir ve .e4bina dosyasına dışa aktarır (M12'deki
/// BinaGeometriExportServisi ile simetrik).
///
/// Id kararlılığı:
/// - Mekan.Id (Guid) → BinaOda.Id = guid string; BinaGeometriMekanAdapteri geri
///   yüklerken Guid.TryParse ile aynı mekana bağlar.
/// - Kat Id'si deterministik "m08-kat-{KatNo}" (M08 Kat modelinde Id yok; adapter
///   geri eşleşmeyi KatNo ile yapar).
/// - Bölme açıklıkları AciklikId'leriyle taşınır; AciklikId'siz (M08'de elle
///   eklenmiş) açıklıklar yeni Id alarak geometriye terfi eder — migrasyondan
///   sonra geometri sahibi M50'dir.
/// </summary>
public static class BinaGeometriM08ExportServisi
{
    public static BinaGeometri GeometriyeCevir(BinaProje proje)
    {
        var geo = M50GeometriServisi.YeniOlustur(proje.ProjeAdi);

        foreach (var kat in proje.Katlar)
        {
            var binaKat = new BinaKat
            {
                Id = $"m08-kat-{kat.KatNo}",
                Ad = kat.Ad,
                KatNo = kat.KatNo,
                KatYuksekligiM = kat.KatYuksekligi,
                DxfFilePath = kat.DxfFilePath,
                OriginOffset = new Point2D(kat.OriginOffset.X, kat.OriginOffset.Y),
                DxfGoster = kat.DxfGoster,
                BloklariGoster = kat.BloklariGoster
            };

            foreach (var mekan in kat.Mekanlar)
            {
                binaKat.Odalar.Add(new BinaOda
                {
                    Id = mekan.Id.ToString(),
                    Ad = mekan.Ad,
                    AlanM2 = mekan.Alan,
                    YukseklikOverrideM = Math.Abs(mekan.Yukseklik - kat.KatYuksekligi) > 1e-9 && mekan.Yukseklik > 0
                        ? mekan.Yukseklik
                        : null,
                    Poligon = mekan.Poligon.Select(p => new Point2D(p.X, p.Y)).ToList()
                });
            }

            foreach (var (a, b) in kat.RawDxfSegments)
                binaKat.DxfSegmentler.Add(new BinaDxfSegment { A = a, B = b });

            binaKat.DxfArclar = kat.RawDxfArcs
                .Select(a => new BinaDxfArc { Merkez = a.Merkez, R = a.R, BasAciDeg = a.BasDeg, BitAciDeg = a.BitDeg })
                .ToList();

            binaKat.Acikliklar = AcikliklariTopla(kat);

            geo.Katlar.Add(binaKat);
        }

        // 4.4 — Patlatma M50 modeline bağlanır: M08 düşey birleşimleri
        // BinaDuseyBirlesim olarak geometriye taşınır (üye bağı MekanId → OdaId).
        foreach (var b in proje.DuseyMekanBirlesimleri)
        {
            geo.DuseyBirlesimleri.Add(new BinaDuseyBirlesim
            {
                Id = b.Id.ToString(),
                AktifMi = b.AktifMi,
                UstKatNo = b.UstKatNo,
                AltKatNo = b.AltKatNo,
                Uyeler = b.Uyeler.Select(u => new BinaBirlesimUyesi
                {
                    KatNo = u.KatNo,
                    OdaId = u.MekanId.ToString(),
                    TavanAcik = u.TavanAcik,
                    DosemeAcik = u.DosemeAcik
                }).ToList()
            });
        }

        return geo;
    }

    /// Katın tüm bölme açıklıklarını tekilleştirerek BinaAciklik listesine çevirir.
    /// Tekilleştirme önce AciklikId, yoksa orta nokta yakınlığı (5 cm) iledir.
    private static List<BinaAciklik> AcikliklariTopla(Kat kat)
    {
        var sonuc = new List<BinaAciklik>();
        var gorulenIdler = new HashSet<string>();

        bool YakinVarMi(Point2D orta) => sonuc.Any(s =>
        {
            double ox = (s.Bas.X + s.Bit.X) * 0.5, oy = (s.Bas.Y + s.Bit.Y) * 0.5;
            double dx = ox - orta.X, dy = oy - orta.Y;
            return dx * dx + dy * dy < 0.05 * 0.05;
        });

        foreach (var bolmeAcikligi in kat.Mekanlar
                     .SelectMany(m => m.Bolmeler)
                     .SelectMany(b => b.Acikliklar))
        {
            var orta = new Point2D(
                (bolmeAcikligi.Baslangic.X + bolmeAcikligi.Bitis.X) * 0.5,
                (bolmeAcikligi.Baslangic.Y + bolmeAcikligi.Bitis.Y) * 0.5);

            if (bolmeAcikligi.AciklikId is { } id)
            {
                if (!gorulenIdler.Add(id)) continue;
            }
            else if (YakinVarMi(orta))
            {
                continue;
            }

            sonuc.Add(new BinaAciklik
            {
                Id = bolmeAcikligi.AciklikId ?? Guid.NewGuid().ToString(),
                Bas = bolmeAcikligi.Baslangic,
                Bit = bolmeAcikligi.Bitis,
                Tip = bolmeAcikligi.Tip == BolmeAcikligiTipi.Kapi
                    ? BinaAciklikTipi.Kapi
                    : BinaAciklikTipi.Pencere,
                GenislikM = bolmeAcikligi.Genislik,
                YukseklikM = bolmeAcikligi.Yukseklik,
                EsikYuksekligiM = bolmeAcikligi.EsikYuksekligiM,
                ManuelEklendi = bolmeAcikligi.AciklikId is null,
                Kaynak = bolmeAcikligi.AciklikId is null ? "manuel" : "m08-migrasyon"
            });
        }

        return sonuc;
    }

    /// <summary>
    /// Eski (M50 bağlantısız) M08 projesini açılışta migrate eder — M12'deki
    /// EskiProjeyiMigrateEt ile aynı sözleşme:
    /// - Gömülü geometri `.e4bina.json` olarak proje yanına dışa aktarılır;
    ///   dosya zaten varsa yalnız aynı kat/oda kimliklerini taşıyorsa bağlanır.
    /// - Yol + revizyon damgası + gömülü kopya projeye yazılır; kalıcılaşması
    ///   için kullanıcının projeyi kaydetmesi yeterlidir.
    /// Veri kaybı yoktur: mekan atamaları/sınıfları olduğu gibi kalır.
    /// </summary>
    /// <returns>Durum notu; migrasyon gerekmiyorsa null.</returns>
    public static string? EskiProjeyiMigrateEt(BinaProje proje, string? e4p08Yolu)
    {
        if (proje.BinaGeometriKopya is not null || !string.IsNullOrWhiteSpace(proje.BinaGeometriYolu))
            return null;
        if (!proje.Katlar.Any(k => k.Mekanlar.Count > 0 || k.RawDxfSegments.Count > 0))
            return null;

        var geo = GeometriyeCevir(proje);

        if (string.IsNullOrWhiteSpace(e4p08Yolu))
        {
            proje.BinaGeometriKopya = geo;
            proje.BinaGeometriRevizyon = geo.GeometriRevizyon;
            return "Gömülü geometri M50 formatına çevrildi (proje kaydedilince kalıcılaşır).";
        }

        string hedef = Path.Combine(
            Path.GetDirectoryName(e4p08Yolu) ?? "",
            Path.GetFileNameWithoutExtension(e4p08Yolu) + ".e4bina.json");

        if (File.Exists(hedef))
        {
            try
            {
                var mevcut = M50GeometriServisi.Ac(hedef);
                if (AyniMekanKimlikleri(mevcut, geo))
                {
                    proje.BinaGeometriYolu = hedef;
                    proje.BinaGeometriRevizyon = mevcut.GeometriRevizyon;
                    // Bağlantı ve gömülü yedek aynı geometriyi göstermeli; aksi halde
                    // dosya kaybolduğunda proje farklı bir bina ile açılabilirdi.
                    proje.BinaGeometriKopya = mevcut;
                    return $"Doğrulanan mevcut bina geometrisi bağlandı: {Path.GetFileName(hedef)} (Rev. {mevcut.GeometriRevizyon}).";
                }
            }
            catch
            {
                // Bozuk/başka biçimdeki yan dosyaya bağlanma; aşağıda güvenli, benzersiz
                // bir hedefe mevcut projenin geometrisi yazılır.
            }

            hedef = BenzersizMigrasyonYolu(e4p08Yolu, proje.Id);
            M50GeometriServisi.Kaydet(geo, hedef);
            proje.BinaGeometriYolu = hedef;
            proje.BinaGeometriRevizyon = geo.GeometriRevizyon;
            proje.BinaGeometriKopya = geo;
            return $"Aynı adlı yan dosya başka geometriye ait; güvenli yeni dosya oluşturuldu: {Path.GetFileName(hedef)}.";
        }

        M50GeometriServisi.Kaydet(geo, hedef);
        proje.BinaGeometriYolu = hedef;
        proje.BinaGeometriRevizyon = geo.GeometriRevizyon;
        proje.BinaGeometriKopya = geo;
        return $"Gömülü geometri dışa aktarıldı: {Path.GetFileName(hedef)} — artık M50'de düzenlenebilir.";
    }

    private static bool AyniMekanKimlikleri(BinaGeometri a, BinaGeometri b)
    {
        var aKimlikleri = a.Katlar
            .SelectMany(k => k.Odalar.Select(o => $"{k.KatNo}:{o.Id}"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var bKimlikleri = b.Katlar
            .SelectMany(k => k.Odalar.Select(o => $"{k.KatNo}:{o.Id}"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return aKimlikleri.Count > 0 && aKimlikleri.SetEquals(bKimlikleri);
    }

    private static string BenzersizMigrasyonYolu(string e4p08Yolu, Guid projeId)
    {
        var dizin = Path.GetDirectoryName(e4p08Yolu) ?? "";
        var kok = Path.GetFileNameWithoutExtension(e4p08Yolu);
        var temel = Path.Combine(dizin, $"{kok}.m08-{projeId:N}.e4bina.json");
        if (!File.Exists(temel)) return temel;

        for (var i = 2; ; i++)
        {
            var aday = Path.Combine(dizin, $"{kok}.m08-{projeId:N}-{i}.e4bina.json");
            if (!File.Exists(aday)) return aday;
        }
    }

    /// Proje açılışında bağlı .e4bina'nın güncelliğini denetler; yeniyse uyarı döner.
    public static string? GuncellikUyarisi(BinaProje proje)
    {
        if (string.IsNullOrWhiteSpace(proje.BinaGeometriYolu) || proje.BinaGeometriRevizyon is null)
            return null;
        try
        {
            if (!File.Exists(proje.BinaGeometriYolu))
                return "Bağlı .e4bina dosyası bulunamadı; gömülü geometri kopyası kullanılıyor.";
            var guncel = M50GeometriServisi.Ac(proje.BinaGeometriYolu);
            if (guncel.GeometriRevizyon > proje.BinaGeometriRevizyon)
                return $"Bina geometrisi güncellenmiş (Rev. {proje.BinaGeometriRevizyon} → {guncel.GeometriRevizyon}). " +
                       "Yeniden yüklemek için 'Bina Geometrisi Yükle'yi kullanın.";
        }
        catch
        {
            // Denetim başarısızsa sessiz geç — proje açılışını engelleme.
        }
        return null;
    }
}
