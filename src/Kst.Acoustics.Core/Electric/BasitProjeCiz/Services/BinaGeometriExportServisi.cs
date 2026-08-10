using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Bina.Services;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;

namespace Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

/// <summary>
/// M12 → M50 köprüsü (3.2 migrasyonu): eski .e4p12 projelerindeki gömülü geometriyi
/// BinaGeometri'ye çevirir ve .e4bina dosyasına dışa aktarır.
///
/// Id kararlılığı: kat/oda/açıklık Id'leri M12'den AYNEN taşınır — böylece dışa
/// aktarılan geometri M50'de düzenlenip geri yüklendiğinde eleman `OdaId` bağları
/// kopmaz (BinaGeometriImportServisi ile simetrik).
/// </summary>
public static class BinaGeometriExportServisi
{
    /// M12 projesindeki gömülü geometriyi BinaGeometri'ye çevirir (Id'ler korunur).
    public static BinaGeometri GeometriyeCevir(BasitProjeCizProje proje)
    {
        var geo = BinaGeometriServisi.YeniOlustur(proje.ProjeAdi);

        foreach (var kat in proje.Katlar.OrderBy(k => k.KatNo))
        {
            var binaKat = new BinaKat
            {
                Id = kat.Id,
                Ad = kat.Ad,
                KatNo = kat.KatNo,
                KatYuksekligiM = kat.KatYuksekligiM,
                DxfFilePath = kat.DxfFilePath,
                UnitWarning = kat.UnitWarning,
                OriginOffset = new Point2D(kat.OriginOffset.X, kat.OriginOffset.Y),
                DxfGoster = kat.DxfGoster,
                BloklariGoster = kat.BloklariGoster
            };

            foreach (var oda in kat.Odalar)
            {
                binaKat.Odalar.Add(new BinaOda
                {
                    Id = oda.Id,
                    Ad = oda.Ad,
                    AlanM2 = oda.AlanM2,
                    Poligon = oda.Poligon.Select(p => new Point2D(p.X, p.Y)).ToList()
                });
            }

            foreach (var s in kat.DxfSegmentleri)
                binaKat.DxfSegmentler.Add(new BinaDxfSegment { A = s.A, B = s.B, Layer = s.Layer });

            binaKat.DxfArclar = kat.DxfArclari
                .Select(a => new BinaDxfArc { Merkez = a.Merkez, R = a.R, BasAciDeg = a.BasAciDeg, BitAciDeg = a.BitAciDeg })
                .ToList();

            binaKat.Acikliklar = kat.Acikliklar
                .Select(a =>
                {
                    var tip = a.Tip == BpcAciklikTipi.Pencere ? BinaAciklikTipi.Pencere : BinaAciklikTipi.Kapi;
                    return new BinaAciklik
                    {
                        Id = a.Id,
                        Bas = a.Bas,
                        Bit = a.Bit,
                        Tip = tip,
                        GenislikM = Mesafe(a.Bas, a.Bit),
                        // M12 açıklık boyutu tutmaz (codex50 §3.6) — M50 tip varsayılanı atanır,
                        // kullanıcı M50'de düzeltebilir.
                        YukseklikM = BinaAciklik.VarsayilanYukseklik(tip),
                        IptalEdildi = a.IptalEdildi,
                        ManuelEklendi = a.ManuelEklendi,
                        Kaynak = a.Kaynak
                    };
                })
                .ToList();

            geo.Katlar.Add(binaKat);
        }

        return geo;
    }

    /// <summary>
    /// Eski (M50 bağlantısız) projeyi açılışta migrate eder:
    /// - Gömülü geometri BinaGeometri'ye çevrilir ve proje yanına `.e4bina.json`
    ///   olarak dışa aktarılır. Aynı adlı dosyanın kaynak kimliği eşleşirse önceki
    ///   dışa aktarım bağlanır; uyuşmazsa üzerine yazılmaz ve benzersiz güvenli bir
    ///   dosya oluşturulur.
    /// - Yol + revizyon damgası + gömülü kopya projeye yazılır (kalıcılaşması
    ///   için kullanıcının projeyi kaydetmesi yeterlidir; SemaSurum kayıtta artar).
    /// Veri kaybı yoktur: M12 katları/elemanları/devreleri olduğu gibi kalır.
    /// </summary>
    /// <returns>Durum çubuğu notu; migrasyon gerekmiyorsa null.</returns>
    public static string? EskiProjeyiMigrateEt(BasitProjeCizProje proje, string? e4p12Yolu)
    {
        // Zaten bağlıysa migrasyon yok.
        if (proje.BinaGeometriKopya is not null || !string.IsNullOrWhiteSpace(proje.BinaGeometriYolu))
            return null;
        // Aktarılacak geometri yoksa (boş proje) migrasyon yok.
        if (!proje.Katlar.Any(k => k.Odalar.Count > 0 || k.DxfSegmentleri.Count > 0))
            return null;

        var geo = GeometriyeCevir(proje);
        geo.KaynakModul = "M12";
        geo.KaynakArtifactKimligi = KaynakArtifactKimligi(geo, e4p12Yolu);

        if (string.IsNullOrWhiteSpace(e4p12Yolu))
        {
            // Kaydedilmemiş proje: yalnız gömülü kopya (dosya yazacak yer yok).
            proje.BinaGeometriKopya = geo;
            proje.BinaGeometriRevizyon = geo.GeometriRevizyon;
            return "Gömülü geometri M50 formatına çevrildi (proje kaydedilince kalıcılaşır).";
        }

        string hedef = Path.Combine(
            Path.GetDirectoryName(e4p12Yolu) ?? "",
            Path.GetFileNameWithoutExtension(e4p12Yolu) + ".e4bina.json");

        if (File.Exists(hedef))
        {
            try
            {
                var mevcut = BinaGeometriServisi.Ac(hedef);
                if (mevcut.KaynakModul == "M12" &&
                    !string.IsNullOrWhiteSpace(mevcut.KaynakArtifactKimligi) &&
                    mevcut.KaynakArtifactKimligi == geo.KaynakArtifactKimligi)
                {
                    proje.BinaGeometriYolu = hedef;
                    proje.BinaGeometriRevizyon = mevcut.GeometriRevizyon;
                    // Yol/revizyon hangi dosyaya aitse gömülü kopya da o dosyanın
                    // içeriği olmalı; M50'de yapılmış güncellemeler korunur.
                    proje.BinaGeometriKopya = mevcut;
                    return $"Mevcut bina geometrisi bağlandı: {Path.GetFileName(hedef)} (Rev. {mevcut.GeometriRevizyon}).";
                }
            }
            catch
            {
                // Bozuk/yarım sidecar mevcut projeye bağlanmaz ve üzerine
                // yazılmaz; aşağıdaki benzersiz hedef kurtarma yolu olur.
            }

            // Kaynak kimliği uyuşmuyorsa başka projeye ait yan dosyayı koru.
            var guvenliHedef = BenzersizHedefYolu(hedef);
            BinaGeometriServisi.Kaydet(geo, guvenliHedef);
            proje.BinaGeometriYolu = guvenliHedef;
            proje.BinaGeometriRevizyon = geo.GeometriRevizyon;
            proje.BinaGeometriKopya = geo;
            return $"Aynı adlı bina geometrisi başka bir projeye ait olabilir; güvenli yeni dosya oluşturuldu: {Path.GetFileName(guvenliHedef)}.";
        }

        BinaGeometriServisi.Kaydet(geo, hedef);
        proje.BinaGeometriYolu = hedef;
        proje.BinaGeometriRevizyon = geo.GeometriRevizyon;
        proje.BinaGeometriKopya = geo;
        return $"Gömülü geometri dışa aktarıldı: {Path.GetFileName(hedef)} — artık M50'de düzenlenebilir.";
    }

    private static double Mesafe(Point2D a, Point2D b)
        => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));

    private static string BenzersizHedefYolu(string hedef)
    {
        var dizin = Path.GetDirectoryName(hedef) ?? "";
        var dosyaAdi = Path.GetFileName(hedef);
        const string sonEk = ".e4bina.json";
        var kok = dosyaAdi.EndsWith(sonEk, StringComparison.OrdinalIgnoreCase)
            ? dosyaAdi[..^sonEk.Length]
            : Path.GetFileNameWithoutExtension(dosyaAdi);

        for (int sira = 2; ; sira++)
        {
            var aday = Path.Combine(dizin, $"{kok}-{sira}{sonEk}");
            if (!File.Exists(aday)) return aday;
        }
    }

    private static string KaynakArtifactKimligi(BinaGeometri geo, string? e4p12Yolu)
    {
        if (!string.IsNullOrWhiteSpace(e4p12Yolu) && File.Exists(e4p12Yolu))
        {
            using var stream = File.OpenRead(e4p12Yolu);
            return "dosya:" + Convert.ToHexString(SHA256.HashData(stream));
        }

        // Test/kurtarma akışında fiziksel .e4p12 henüz yoksa yalnız M12'den
        // dönüştürülen kararlı geometri alanlarından deterministik kimlik üret.
        var kimlikVerisi = new
        {
            geo.BinaAdi,
            Katlar = geo.Katlar.Select(k => new
            {
                k.Id, k.Ad, k.KatNo, k.KatYuksekligiM,
                Odalar = k.Odalar.Select(o => new { o.Id, o.Ad, o.AlanM2, o.Poligon }),
                Acikliklar = k.Acikliklar.Select(a => new
                    { a.Id, a.Bas, a.Bit, a.Tip, a.IptalEdildi, a.ManuelEklendi })
            })
        };
        var json = JsonSerializer.Serialize(kimlikVerisi);
        return "geometri:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}
