using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;

namespace Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

public static class BasitProjeCizKabloServisi
{
    public const int PrizBuatKapasitesi = 7;
    private const double GecisKoprusuMaxM = 1.20;

    // ─── Devre yönetimi ──────────────────────────────────────────────────

    public static int MaxDevreNo(BpcElemanTipi tip) => tip switch
    {
        BpcElemanTipi.UcluKomutator => 3,
        BpcElemanTipi.Komütatör => 2,
        BpcElemanTipi.KomütatörVavien => 2,
        _ => 1
    };

    public static BpcDevre EnsureDevre(BasitProjeKat kat, string anahtarId, int devreNo = 1)
    {
        var mevcut = kat.Devreler.FirstOrDefault(
            d => d.AnahtarElemanId == anahtarId && d.DevreNo == devreNo);
        if (mevcut != null) return mevcut;
        var yeni = new BpcDevre { AnahtarElemanId = anahtarId, DevreNo = devreNo };
        kat.Devreler.Add(yeni);
        return yeni;
    }

    public static void ArmaturAtama(BasitProjeKat kat, BpcDevre hedefDevre, string armaturId)
    {
        foreach (var d in kat.Devreler)
            d.ArmaturElemanIds.Remove(armaturId);
        if (!hedefDevre.ArmaturElemanIds.Contains(armaturId))
            hedefDevre.ArmaturElemanIds.Add(armaturId);
    }

    public static void ArmaturCikar(BasitProjeKat kat, string armaturId)
    {
        foreach (var d in kat.Devreler)
            d.ArmaturElemanIds.Remove(armaturId);
    }

    // ─── Vavien grubu ────────────────────────────────────────────────────

    public static BpcVavienGrup? BulVavienGrup(BasitProjeKat kat, string anahtarId)
        => kat.VavienGruplari.FirstOrDefault(
            g => g.Anahtar1Id == anahtarId || g.Anahtar2Id == anahtarId);

    public static BpcVavienGrup VavienGrupOlustur(BasitProjeKat kat,
        string anahtar1Id, string anahtar2Id)
    {
        var mevcut = kat.VavienGruplari.FirstOrDefault(
            g => (g.Anahtar1Id == anahtar1Id && g.Anahtar2Id == anahtar2Id) ||
                 (g.Anahtar1Id == anahtar2Id && g.Anahtar2Id == anahtar1Id));
        if (mevcut != null) return mevcut;
        var devre = EnsureDevre(kat, anahtar1Id);
        var grup  = new BpcVavienGrup
        {
            Anahtar1Id = anahtar1Id,
            Anahtar2Id = anahtar2Id,
            DevreId    = devre.Id
        };
        kat.VavienGruplari.Add(grup);
        return grup;
    }

    // ─── Duvar snap tespiti ───────────────────────────────────────────────

    /// Anahtar/buat tipinde olanlar duvara snap yapar; armatür/sensör yapmaz.
    public static bool ElemanDuvarSnapMi(BpcElemanTipi tip) => tip switch
    {
        BpcElemanTipi.Anahtar or BpcElemanTipi.Komütatör or
        BpcElemanTipi.DarbeAnahtari or BpcElemanTipi.Vavien or
        BpcElemanTipi.KatPanosu or BpcElemanTipi.Buat or
        BpcElemanTipi.Priz or BpcElemanTipi.KapakliPriz or
        BpcElemanTipi.SivaUstuPriz or BpcElemanTipi.TrifazePriz or
        BpcElemanTipi.KombinePrizKutusu or
        BpcElemanTipi.KlimaIcUnite or BpcElemanTipi.KlimaDisUnite or
        // Zayıf akım: prizler ve merkezler/dağıtım kutuları duvara yapışır
        BpcElemanTipi.DataPriz or BpcElemanTipi.TvPriz or BpcElemanTipi.TelefonPriz or
        BpcElemanTipi.KatDagitimKutusu or BpcElemanTipi.DataRack or
        BpcElemanTipi.TvMerkezi or BpcElemanTipi.TelefonDagitim or
        BpcElemanTipi.YanginSantrali or BpcElemanTipi.CctvKayit or
        BpcElemanTipi.AgAnahtari or
        BpcElemanTipi.AnonsAmfi or BpcElemanTipi.YanginButonu => true,
        _ => false  // TavanPrizi, UsbliMasaPrizi, Armatür, Sensör, kamera, hoparlör, dedektör → serbest
    };

    public static bool ElemanPrizMi(BpcElemanTipi tip) => tip switch
    {
        BpcElemanTipi.Priz or BpcElemanTipi.KapakliPriz or
        BpcElemanTipi.SivaUstuPriz or BpcElemanTipi.TrifazePriz or
        BpcElemanTipi.KombinePrizKutusu or BpcElemanTipi.TavanPrizi or
        BpcElemanTipi.UsbliMasaPrizi or BpcElemanTipi.IkiliPriz or
        BpcElemanTipi.UcluPriz or BpcElemanTipi.UpsPrizi or
        BpcElemanTipi.KapakliUpsPrizi or BpcElemanTipi.SivaUstuUpsPrizi => true,
        _ => false
    };

    /// Müstakil, trifaze, kombine ve UPS prizleri buat almaz; doğrudan panoya gider.
    /// Diğer priz tipleri otomatik priz buatı alır.
    public static bool ElemanPrizDirektPanoMu(BpcElemanTipi tip) => tip switch
    {
        BpcElemanTipi.TrifazePriz or BpcElemanTipi.KombinePrizKutusu or
        BpcElemanTipi.UpsPrizi or BpcElemanTipi.KapakliUpsPrizi or
        BpcElemanTipi.SivaUstuUpsPrizi => true,
        _ => false
    };

    /// Müstakil priz de direk panoya gider (blok adına göre).
    public static bool ElemanPrizDirektPanoMu(BasitProjeEleman el) =>
        ElemanPrizDirektPanoMu(el.Tip) ||
        el.BlokAdi == "E4P_MUSTAKIL_PRIZ";

    public static void ButaAta(BasitProjeKat kat, BpcDevre devre, string buatId)
    {
        devre.BuatId = buatId;

        if (string.IsNullOrEmpty(devre.AnahtarElemanId))
            return;

        foreach (var ayniElemanDevresi in kat.Devreler.Where(d => d.AnahtarElemanId == devre.AnahtarElemanId))
            ayniElemanDevresi.BuatId = buatId;
    }

    public static BpcBuatPanoBaglanti BuatPanoBagla(
        BasitProjeKat kat,
        string buatId,
        string panoId,
        IEnumerable<string> gecisOdaIds,
        string linyePrefix = "A")
    {
        linyePrefix = string.IsNullOrWhiteSpace(linyePrefix) ? "A" : linyePrefix.Trim().ToUpperInvariant();
        var mevcut = kat.BuatPanoBaglantilari.FirstOrDefault(b => b.BuatId == buatId && b.LinyePrefix == linyePrefix);
        if (mevcut == null)
        {
            mevcut = new BpcBuatPanoBaglanti { BuatId = buatId, LinyePrefix = linyePrefix };
            kat.BuatPanoBaglantilari.Add(mevcut);
        }

        mevcut.PanoId = panoId;
        mevcut.LinyePrefix = linyePrefix;
        mevcut.GecisOdaIds = gecisOdaIds.Distinct().ToList();
        return mevcut;
    }

    // ── Pano rolleri (BlokAdi ile ayrışır; hepsi BpcElemanTipi.KatPanosu) ──────
    // E4P_TALI_PANO → tali, E4P_ALT_PANO → alt, E4P_ANA_PANO → ana, diğerleri (E4P_KAT_PANOSU,
    // E4P_OZEL_PANO, boş/eski) → kat panosu.
    public static bool PanoTaliMi(BasitProjeEleman p)
        => string.Equals(p.BlokAdi, "E4P_TALI_PANO", StringComparison.OrdinalIgnoreCase);
    public static bool PanoAnaMi(BasitProjeEleman p)
        => string.Equals(p.BlokAdi, "E4P_ANA_PANO", StringComparison.OrdinalIgnoreCase);
    public static bool PanoAltMi(BasitProjeEleman p)
        => string.Equals(p.BlokAdi, "E4P_ALT_PANO", StringComparison.OrdinalIgnoreCase);
    public static bool PanoKatMi(BasitProjeEleman p)
        => p.Tip == BpcElemanTipi.KatPanosu && !PanoTaliMi(p) && !PanoAnaMi(p) && !PanoAltMi(p);

    /// Aydınlatma tasarımında kullanıcı tarafından mahal için seçilen pano.
    public static BasitProjeEleman? MahaleAtananPano(string? odaId, IEnumerable<BasitProjeEleman> panolar)
        => string.IsNullOrEmpty(odaId) ? null : panolar.FirstOrDefault(p =>
            p.Tip == BpcElemanTipi.KatPanosu && p.BagliOdaIds.Contains(odaId));

    /// Bir buatın bağlanacağı hedef panoyu kullanıcı kuralına göre seçer:
    /// 1) Buatın mahalindeki (odasındaki) TALİ pano varsa → o tali pano,
    /// 2) Mahalde tali pano yoksa → en yakın KAT panosu,
    /// 3) Kat panosu da yoksa → en yakın pano (ana hariç tercih).
    public static BasitProjeEleman? BuatIcinHedefPano(
        BasitProjeEleman buat, IReadOnlyList<BasitProjeEleman> katPanolari)
    {
        if (katPanolari.Count == 0) return null;

        // 1) Buatın mahalindeki tali pano (en yakın)
        if (!string.IsNullOrEmpty(buat.OdaId))
        {
            var odaTali = katPanolari
                .Where(p => PanoTaliMi(p) && p.OdaId == buat.OdaId)
                .OrderBy(p => PanoBuatMesafe2(p, buat))
                .FirstOrDefault();
            if (odaTali != null) return odaTali;

            // Odası bu kat panosuna açıkça atanmışsa onu tercih et
            var atanmis = katPanolari
                .Where(p => PanoKatMi(p) && p.BagliOdaIds.Contains(buat.OdaId))
                .OrderBy(p => PanoBuatMesafe2(p, buat))
                .FirstOrDefault();
            if (atanmis != null) return atanmis;
        }

        // 2) Mahalde tali yok → en yakın kat panosu
        var enYakinKat = katPanolari
            .Where(PanoKatMi)
            .OrderBy(p => PanoBuatMesafe2(p, buat))
            .FirstOrDefault();
        if (enYakinKat != null) return enYakinKat;

        // 3) Kat panosu yok → ana hariç en yakın; o da yoksa herhangi (ana dahil)
        return katPanolari.Where(p => !PanoAnaMi(p)).OrderBy(p => PanoBuatMesafe2(p, buat)).FirstOrDefault()
               ?? katPanolari.OrderBy(p => PanoBuatMesafe2(p, buat)).First();
    }

    private static double PanoBuatMesafe2(BasitProjeEleman a, BasitProjeEleman b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    /// Buat→pano rotalarını hesaplar. Rota bulunamayan bağlantılar için BOŞ rotalı
    /// kablo üretilmez; bağlantı korunur ve uyarı listesi döner — çağıran taraf
    /// kullanıcıya gösterir ("bağlantı var ama çizilemedi" sessiz kalmasın).
    public static List<string> BuatPanoRotalariniHesapla(BasitProjeKat kat, string? linyePrefix = null)
    {
        var baglantilar = kat.BuatPanoBaglantilari.Where(b => linyePrefix == null || b.LinyePrefix == linyePrefix).ToList();
        var baglantiIdleri = baglantilar.Select(b => b.Id).ToHashSet();
        kat.Kablolar.RemoveAll(k => k.Tip == BpcKabloTipi.PanoKablosu &&
                                   (linyePrefix == null || baglantiIdleri.Contains(k.DevreId)));
        var uyarilar = new List<string>();

        foreach (var baglanti in baglantilar)
        {
            var buat = kat.Elemanlar.FirstOrDefault(e =>
                e.Id == baglanti.BuatId &&
                (e.Tip == BpcElemanTipi.Buat ||
                 (baglanti.LinyePrefix == "P" && ElemanPrizMi(e.Tip) && ElemanPrizDirektPanoMu(e))));
            var pano = kat.Elemanlar.FirstOrDefault(e => e.Id == baglanti.PanoId && e.Tip == BpcElemanTipi.KatPanosu);
            if (buat == null || pano == null)
            {
                kat.BuatPanoBaglantilari.Remove(baglanti);
                continue;
            }

            var izinliOdalar = IzinliGecisOdalari(kat, buat, pano, baglanti.GecisOdaIds);
            var rota = BuatPanoRota(buat, pano, izinliOdalar, kat.Odalar);
            if (rota.Count < 2)
            {
                var buatOda = kat.Odalar.FirstOrDefault(o => o.Id == buat.OdaId)?.Ad ?? "?";
                uyarilar.Add($"{buat.Ad} ({buatOda}) → {pano.Ad}: izinli geçiş odalarıyla rota bulunamadı — " +
                             "kablo çizilmedi. İlgili odaları \"Geçiş\" olarak işaretleyin.");
                continue;
            }
            kat.Kablolar.Add(new BpcKablo
            {
                DevreId = baglanti.Id,
                Tip = BpcKabloTipi.PanoKablosu,
                Rota = rota
            });
        }

        PanoKablolariniNumaralandir(kat, linyePrefix);
        return uyarilar;
    }

    public static void PanoKablolariniNumaralandir(BasitProjeKat kat, string? linyePrefix = null)
    {
        // Aydınlatma ("A") ve priz ("P") linyelerini ayrı ayrı numaralandır
        var panoCablolar = kat.Kablolar
            .Where(k => k.Tip == BpcKabloTipi.PanoKablosu)
            .ToList();

        var baglantiByKabloId = kat.BuatPanoBaglantilari
            .ToDictionary(b => b.Id, b => b);

        var gruplar = panoCablolar
            .GroupBy(k =>
            {
                if (!baglantiByKabloId.TryGetValue(k.DevreId, out var b))
                    return ("", "A");
                return (b.PanoId, b.LinyePrefix);
            });

        var merkezByDevre = kat.SistemDevreleri.ToDictionary(d => d.Id, d => d.MerkezId ?? "");

        foreach (var grup in gruplar.SelectMany(g => g.GroupBy(k => merkezByDevre.TryGetValue(k.DevreId, out var merkezId) ? merkezId : "")))
        {
            int sira = 1;
            foreach (var k in grup
                         .OrderBy(k => k.Rota.Count > 0 ? k.Rota[^1].Y : 0)
                         .ThenBy(k => k.Rota.Count > 0 ? k.Rota[^1].X : 0)
                         .ThenBy(k => k.DevreId))
            {
                if (linyePrefix != null && (!baglantiByKabloId.TryGetValue(k.DevreId, out var baglanti) ||
                                            baglanti.LinyePrefix != linyePrefix)) continue;
                k.PanoSira = sira++;
            }
        }
    }

    // ─── Priz kablo rotaları ──────────────────────────────────────────────

    // Kat elemanlarını Id→eleman sözlüğüne kurar (bulk rota hesaplarında O(N²)→O(N)).
    // Id'ler benzersiz (GUID); nadir çakışmada son yazan kazanır.
    private static Dictionary<string, BasitProjeEleman> ElemanHaritasi(BasitProjeKat kat)
    {
        var map = new Dictionary<string, BasitProjeEleman>(kat.Elemanlar.Count);
        foreach (var e in kat.Elemanlar) map[e.Id] = e;
        return map;
    }

    // Sözlük verildiyse O(1) arama; verilmediyse eski O(N) tarama (geri uyumluluk).
    private static BasitProjeEleman? ElemanBul(
        BasitProjeKat kat, IReadOnlyDictionary<string, BasitProjeEleman>? map, string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (map != null) return map.TryGetValue(id, out var e) ? e : null;
        return kat.Elemanlar.FirstOrDefault(x => x.Id == id);
    }

    // NOT: Tüm kablo rotaları LOKAL koordinatta saklanır (elemanlarla aynı çerçeve).
    // OriginOffset dönüşümü tek noktada, render/DXF sırasında uygulanır.
    public static void PrizRotaHesapla(BasitProjeKat kat, BpcPrizDevre devre,
        IReadOnlyDictionary<string, BasitProjeEleman>? elemanById = null)
    {
        kat.Kablolar.RemoveAll(k => k.DevreId == devre.Id && k.Tip == BpcKabloTipi.PrizKablosu);
        if (string.IsNullOrEmpty(devre.BuatId)) return;

        var buat = ElemanBul(kat, elemanById, devre.BuatId);
        if (buat == null || ElemanPrizDirektPanoMu(buat)) return;

        var prizler = devre.PrizElemanIds
            .Select(id => ElemanBul(kat, elemanById, id))
            .OfType<BasitProjeEleman>()
            .ToList();

        if (prizler.Count == 0) return;

        // Zincir sırası ekleme sırası değil geometrik yakınlık olsun (codex12 §17):
        // buattan başlayıp en yakın komşuya ilerleyen sıralama daha kısa rota verir.
        // Devre üyeliği (PrizElemanIds) değişmez; yalnız çizim sırası düzenlenir.
        prizler = GeometrikSirala(new Point2D(buat.X, buat.Y), prizler);

        var mahal = kat.Odalar.FirstOrDefault(o => o.Id == buat.OdaId);
        IReadOnlyList<BasitProjeOda> odalar = mahal != null && prizler.All(p => p.OdaId == mahal.Id)
            ? [mahal] : kat.Odalar;

        // Daisy chain: buat → priz1 → priz2 → ...
        BasitProjeEleman? oncekiEl = buat;
        foreach (var priz in prizler)
        {
            List<Point2D> rota;
            bool oncekiDuvarda = ElemanDuvarSnapMi(oncekiEl.Tip);
            bool buDuvarda     = ElemanDuvarSnapMi(priz.Tip);

            if (odalar.Count > 0 && (oncekiDuvarda || buDuvarda))
            {
                rota = DuvarBoyuncaRota(
                    new Point2D(oncekiEl.X, oncekiEl.Y),
                    new Point2D(priz.X, priz.Y),
                    odalar);
            }
            else
            {
                rota = odalar.Count == 0 ? [new(oncekiEl.X, oncekiEl.Y), new(priz.X, priz.Y)] :
                    OdaFarkindaliSegment(new(oncekiEl.X, oncekiEl.Y), new(priz.X, priz.Y), odalar);
            }

            kat.Kablolar.Add(new BpcKablo
            {
                DevreId = devre.Id,
                Tip     = BpcKabloTipi.PrizKablosu,
                Rota    = rota
            });
            oncekiEl = priz;
        }
    }

    public static void TumPrizRotalariniHesapla(BasitProjeKat kat)
    {
        kat.Kablolar.RemoveAll(k => k.Tip == BpcKabloTipi.PrizKablosu);
        var elemanById = ElemanHaritasi(kat);
        foreach (var d in kat.PrizDevreleri)
            PrizRotaHesapla(kat, d, elemanById);
    }

    // ─── Zayıf akım sistem rotaları (Data/TV/Telefon — BuatDaisy) ─────────

    /// Bir zayıf akım devresinin rotasını hesaplar: cihaz1 → cihaz2 → …
    /// → (ara toplayıcı) → merkez. Tek polyline, ZayifAkimKablosu tipinde.
    public static void SistemRotaHesapla(BasitProjeKat kat, BpcSistemDevre devre,
        IReadOnlyDictionary<string, BasitProjeEleman>? elemanById = null)
    {
        kat.Kablolar.RemoveAll(k => k.DevreId == devre.Id && k.Tip == BpcKabloTipi.ZayifAkimKablosu);
        if (string.IsNullOrEmpty(devre.MerkezId)) return;

        var merkez = ElemanBul(kat, elemanById, devre.MerkezId);
        if (merkez == null) return;

        var cihazlar = devre.CihazIds
            .Select(id => ElemanBul(kat, elemanById, id))
            .OfType<BasitProjeEleman>()
            .ToList();
        if (cihazlar.Count == 0) return;

        var topoloji = BpcSistemKatalogu.Get(devre.Sistem).Topoloji;
        var zincir = new List<BasitProjeEleman>();

        if (topoloji == BpcSistemTopoloji.Loop)
        {
            // Kapalı çevrim: merkez → cihaz1 → … → cihazN → merkez
            zincir.Add(merkez);
            zincir.AddRange(cihazlar);
            zincir.Add(merkez);
        }
        else
        {
            // Zincir sırası: cihazlar → (ara toplayıcı) → merkez
            zincir.AddRange(cihazlar);
            if (!string.IsNullOrEmpty(devre.AraToplayiciId))
            {
                var araT = ElemanBul(kat, elemanById, devre.AraToplayiciId);
                if (araT != null) zincir.Add(araT);
            }
            zincir.Add(merkez);
        }

        bool duvarliRota = kat.Odalar.Count > 0;
        var rota = new List<Point2D>();

        for (int i = 0; i < zincir.Count - 1; i++)
        {
            var a = zincir[i];
            var b = zincir[i + 1];
            List<Point2D> parca;
            if (duvarliRota && (ElemanDuvarSnapMi(a.Tip) || ElemanDuvarSnapMi(b.Tip)))
            {
                parca = DuvarBoyuncaRota(new Point2D(a.X, a.Y), new Point2D(b.X, b.Y), kat.Odalar);
            }
            else
            {
                parca = [new(a.X, a.Y), new(b.X, b.Y)];
            }
            // İlk parça hariç baş noktayı tekrarlamadan ekle
            if (rota.Count > 0 && parca.Count > 0) parca.RemoveAt(0);
            rota.AddRange(parca);
        }

        kat.Kablolar.Add(new BpcKablo
        {
            DevreId       = devre.Id,
            Tip           = BpcKabloTipi.ZayifAkimKablosu,
            SistemAnahtar = devre.Sistem,
            Rota          = rota
        });
    }

    public static void TumSistemRotalariniHesapla(BasitProjeKat kat)
    {
        kat.Kablolar.RemoveAll(k => k.Tip == BpcKabloTipi.ZayifAkimKablosu);
        var elemanById = ElemanHaritasi(kat);
        foreach (var d in kat.SistemDevreleri)
            SistemRotaHesapla(kat, d, elemanById);
        SistemKablolariniNumaralandir(kat);
    }

    /// Zayıf akım linyelerini sisteme göre (D1, TV1, T1…) numaralandırır.
    public static void SistemKablolariniNumaralandir(BasitProjeKat kat)
    {
        var gruplar = kat.Kablolar
            .Where(k => k.Tip == BpcKabloTipi.ZayifAkimKablosu)
            .GroupBy(k => k.SistemAnahtar);

        foreach (var grup in gruplar)
        {
            int sira = 1;
            foreach (var k in grup
                         .OrderBy(k => k.Rota.Count > 0 ? k.Rota[^1].Y : 0)
                         .ThenBy(k => k.Rota.Count > 0 ? k.Rota[^1].X : 0)
                         .ThenBy(k => k.DevreId))
            {
                k.PanoSira = sira++;
            }
        }
    }

    public static bool ElemanAnahtarMi(BpcElemanTipi tip) => tip switch
    {
        BpcElemanTipi.Anahtar or BpcElemanTipi.Komütatör or BpcElemanTipi.UcluKomutator or BpcElemanTipi.DarbeAnahtari or
        BpcElemanTipi.Vavien or BpcElemanTipi.KomütatörVavien or BpcElemanTipi.Deviatör or
        BpcElemanTipi.Dimmer or BpcElemanTipi.IkiKutupluAnahtar or BpcElemanTipi.Sensör => true,
        _ => false
    };

    public static bool ElemanArmaturMi(BpcElemanTipi tip) => tip switch
    {
        BpcElemanTipi.Armatür or BpcElemanTipi.SarkitArmatur or BpcElemanTipi.GlopArmatur or
        BpcElemanTipi.CiplakLamba or BpcElemanTipi.BantArmatur or BpcElemanTipi.Avize or
        BpcElemanTipi.WallWasher => true,
        _ => false
    };

    public static List<BasitProjeEleman> GeometrikSiralaPublic(Point2D baslangic, List<BasitProjeEleman> elemanlar)
        => GeometrikSirala(baslangic, elemanlar);

    public static int KapasiteHesaplaPublic(BpcElemanTipi tip) => KapasiteHesapla(tip);

    /// Anahtarın devrelerine halihazırda atanmış armatür sayısı. Kapasite
    /// muhasebesinde yeni atama bu yükün ÜSTÜNE sayılır — farklı armatür grupları
    /// aynı anahtarı doldururken sınır sessizce aşılmaz.
    public static int AnahtarMevcutYuk(BasitProjeKat kat, string anahtarId)
        => kat.Devreler.Where(d => d.AnahtarElemanId == anahtarId)
                       .Sum(d => d.ArmaturElemanIds.Count);

    private static List<BasitProjeEleman> GeometrikSirala(Point2D baslangic, List<BasitProjeEleman> elemanlar)
    {
        var currentPos = baslangic;
        var kalanlar = new List<BasitProjeEleman>(elemanlar);
        var sirali = new List<BasitProjeEleman>();

        while (kalanlar.Count > 0)
        {
            int enYakinIdx = 0;
            double enYakinDist2 = double.MaxValue;
            for (int i = 0; i < kalanlar.Count; i++)
            {
                var dx = Math.Abs(kalanlar[i].X - currentPos.X);
                var dy = Math.Abs(kalanlar[i].Y - currentPos.Y);
                var d2 = dx * dx + dy * dy;
                
                // Grid/Orthogonal hizalama ödülü: Eksenlerde hizalıysa çapraz gitmektense öncelik ver
                if (dx < 0.15 || dy < 0.15) 
                    d2 *= 0.05; 

                if (d2 < enYakinDist2)
                {
                    enYakinDist2 = d2;
                    enYakinIdx = i;
                }
            }
            var enYakin = kalanlar[enYakinIdx];
            sirali.Add(enYakin);
            currentPos = new Point2D(enYakin.X, enYakin.Y);
            kalanlar.RemoveAt(enYakinIdx);
        }
        return sirali;
    }

    private static int KapasiteHesapla(BpcElemanTipi tip) => tip switch {
        BpcElemanTipi.Komütatör => 18,
        BpcElemanTipi.UcluKomutator => 27,
        BpcElemanTipi.Vavien => 9,
        BpcElemanTipi.DarbeAnahtari => int.MaxValue,
        BpcElemanTipi.KomütatörVavien => 18,
        _ => 9
    };

    /// Bir odadaki, verilen konuma en yakın buatı döndürür (yoksa null).
    /// paftaTipi = Aydinlatma → yalnız aydınlatma buatları; Priz → yalnız priz buatları.
    private static BasitProjeEleman? EnYakinBuat(
        BasitProjeKat kat, string? odaId, double x, double y,
        BpcPaftaTipi paftaTipi = BpcPaftaTipi.Aydinlatma)
    {
        BasitProjeEleman? enYakin = null;
        double enIyi = double.MaxValue;
        foreach (var e in kat.Elemanlar)
        {
            if (e.Tip != BpcElemanTipi.Buat || e.OdaId != odaId || e.PaftaTipi != paftaTipi) continue;
            double dx = e.X - x, dy = e.Y - y;
            double d2 = dx * dx + dy * dy;
            if (d2 < enIyi) { enIyi = d2; enYakin = e; }
        }
        return enYakin;
    }

    public static void MahaldekiBosArmaturleriAnahtaraBagla(BasitProjeKat kat, BpcDevre devre, string anahtarId)
    {
        var anahtar = kat.Elemanlar.FirstOrDefault(e => e.Id == anahtarId);
        if (anahtar == null || string.IsNullOrEmpty(anahtar.OdaId)) return;

        var odadakiArmaturler = kat.Elemanlar
            .Where(e => e.OdaId == anahtar.OdaId && BasitProjeCizKabloServisi.ElemanArmaturMi(e.Tip))
            .ToList();

        var atananArmaturIds = kat.Devreler
            .Where(d => d.Id != devre.Id)
            .SelectMany(d => d.ArmaturElemanIds)
            .ToHashSet();

        var bosArmaturler = odadakiArmaturler
            .Where(e => !atananArmaturIds.Contains(e.Id))
            .ToList();

        if (bosArmaturler.Count == 0) return;

        var baslangicPos = new Point2D(anahtar.X, anahtar.Y);
        if (devre.ArmaturElemanIds.Count > 0)
        {
            var sonArmaturId = devre.ArmaturElemanIds[^1];
            var sonArmatur = kat.Elemanlar.FirstOrDefault(e => e.Id == sonArmaturId);
            if (sonArmatur != null)
                baslangicPos = new Point2D(sonArmatur.X, sonArmatur.Y);
        }

        var sirali = GeometrikSirala(baslangicPos, bosArmaturler);

        var ayniOdakiler = kat.Elemanlar
            .Where(e => e.OdaId == anahtar.OdaId && e.Tip == anahtar.Tip)
            .OrderBy(e => e.Id == anahtar.Id ? 0 : 1)
            .ToList();

        int anahtarIdx = 0;
        while (sirali.Count > 0 && anahtarIdx < ayniOdakiler.Count)
        {
            var aktifAnahtar = ayniOdakiler[anahtarIdx];
            // Anahtarın mevcut devre yükü düşülür — dolu anahtara üstüne atanmaz.
            int kapasite = Math.Max(0, KapasiteHesapla(aktifAnahtar.Tip) - AnahtarMevcutYuk(kat, aktifAnahtar.Id));
            if (kapasite == 0) { anahtarIdx++; continue; }
            int devSayisi = MaxDevreNo(aktifAnahtar.Tip);

            int atanacakSayi = Math.Min(sirali.Count, kapasite);
            var atanacaklar = sirali.Take(atanacakSayi).ToList();
            sirali.RemoveRange(0, atanacakSayi);

            int bolumBoyutu = (int)Math.Ceiling(atanacakSayi / (double)devSayisi);
            for (int d = 0; d < devSayisi; d++)
            {
                var devreAtanacaklar = atanacaklar.Skip(d * bolumBoyutu).Take(bolumBoyutu).ToList();
                if (devreAtanacaklar.Count == 0) continue;
                
                var dDevre = EnsureDevre(kat, aktifAnahtar.Id, d + 1);
                foreach (var a in devreAtanacaklar)
                {
                    if (!dDevre.ArmaturElemanIds.Contains(a.Id))
                        dDevre.ArmaturElemanIds.Add(a.Id);
                }

                if (string.IsNullOrEmpty(dDevre.BuatId))
                {
                    var odadakiBuat = EnYakinBuat(kat, aktifAnahtar.OdaId, aktifAnahtar.X, aktifAnahtar.Y);
                    if (odadakiBuat != null) ButaAta(kat, dDevre, odadakiBuat.Id);
                }
            }
            anahtarIdx++;
        }

        if (sirali.Count > 0)
        {
            var sonAnahtar = ayniOdakiler.LastOrDefault() ?? anahtar;
            int devSayisi = MaxDevreNo(sonAnahtar.Tip);
            int bolumBoyutu = (int)Math.Ceiling(sirali.Count / (double)devSayisi);
            for (int d = 0; d < devSayisi; d++)
            {
                var devreAtanacaklar = sirali.Skip(d * bolumBoyutu).Take(bolumBoyutu).ToList();
                if (devreAtanacaklar.Count == 0) continue;
                var dDevre = EnsureDevre(kat, sonAnahtar.Id, d + 1);
                foreach (var a in devreAtanacaklar)
                {
                    if (!dDevre.ArmaturElemanIds.Contains(a.Id))
                        dDevre.ArmaturElemanIds.Add(a.Id);
                }

                if (string.IsNullOrEmpty(dDevre.BuatId))
                {
                    var odadakiBuat = EnYakinBuat(kat, sonAnahtar.OdaId, sonAnahtar.X, sonAnahtar.Y);
                    if (odadakiBuat != null) ButaAta(kat, dDevre, odadakiBuat.Id);
                }
            }
            sirali.Clear();
        }
    }

    /// Sensör kontrollü armatürleri kablolar: her armatür, kendine en yakın sensör
    /// kontrollü sensöre bağlanır (sensör = devre anahtarı) ve devrenin buatı oda
    /// buatıdır. Böylece armatür → sensör → buat zinciri kurulur (duvar anahtarı yok).
    private static void SensorKontrolluArmaturleriKablola(BasitProjeKat kat, string? odaId)
    {
        var atananArmaturIds = kat.Devreler
            .SelectMany(d => d.ArmaturElemanIds)
            .ToHashSet();

        foreach (var oda in kat.Odalar.Where(o => odaId == null || o.Id == odaId))
        {
            var sensorArmaturleri = kat.Elemanlar
                .Where(e => e.OdaId == oda.Id && ElemanArmaturMi(e.Tip) && e.SensorKontrollu
                            && !atananArmaturIds.Contains(e.Id))
                .ToList();
            if (sensorArmaturleri.Count == 0) continue;

            var odaSensorleri = kat.Elemanlar
                .Where(e => e.OdaId == oda.Id && e.Tip == BpcElemanTipi.Sensör && e.SensorKontrollu)
                .ToList();
            if (odaSensorleri.Count == 0) continue;

            // Sensör başına yalnız bir armatür düşsün diye kullanılan sensörleri izle.
            var kullanilanSensorIds = new HashSet<string>();

            foreach (var arm in sensorArmaturleri)
            {
                // Bu armatüre en yakın, henüz kullanılmamış sensör.
                BasitProjeEleman? sensor = odaSensorleri
                    .Where(s => !kullanilanSensorIds.Contains(s.Id))
                    .OrderBy(s => { double dx = s.X - arm.X, dy = s.Y - arm.Y; return dx * dx + dy * dy; })
                    .FirstOrDefault()
                    // Sensör kalmadıysa en yakın sensörü paylaş.
                    ?? odaSensorleri
                        .OrderBy(s => { double dx = s.X - arm.X, dy = s.Y - arm.Y; return dx * dx + dy * dy; })
                        .First();
                kullanilanSensorIds.Add(sensor.Id);

                var devre = EnsureDevre(kat, sensor.Id, 1);
                if (!devre.ArmaturElemanIds.Contains(arm.Id))
                    devre.ArmaturElemanIds.Add(arm.Id);
                atananArmaturIds.Add(arm.Id);

                if (string.IsNullOrEmpty(devre.BuatId))
                {
                    var buat = EnYakinBuat(kat, oda.Id, sensor.X, sensor.Y);
                    if (buat != null) ButaAta(kat, devre, buat.Id);
                }
            }
        }
    }

    public static void KatAydinlatmasiniOtomatikKablola(BasitProjeKat kat, string? odaId = null)
    {
        // Önce sensör kontrollü armatürleri kendi sensörleri üzerinden buata bağla.
        SensorKontrolluArmaturleriKablola(kat, odaId);

        foreach (var oda in kat.Odalar.Where(o => odaId == null || o.Id == odaId))
        {
            // Sensör kontrollü sensörler genel anahtar havuzuna girmez (ön-kablolandılar).
            var odadakiAnahtarlar = kat.Elemanlar
                .Where(e => e.OdaId == oda.Id && ElemanAnahtarMi(e.Tip) && !e.SensorKontrollu)
                .ToList();

            if (odadakiAnahtarlar.Count == 0) continue;

            // Sensör kontrollü armatürler bu genel akışa girmez (kendi devreleri var).
            var odadakiArmaturler = kat.Elemanlar
                .Where(e => e.OdaId == oda.Id && BasitProjeCizKabloServisi.ElemanArmaturMi(e.Tip) && !e.SensorKontrollu)
                .ToList();

            var atananArmaturIds = kat.Devreler
                .SelectMany(d => d.ArmaturElemanIds)
                .ToHashSet();

            var bosArmaturler = odadakiArmaturler
                .Where(e => !atananArmaturIds.Contains(e.Id))
                .ToList();

            if (bosArmaturler.Count == 0) continue;

            // Farklı tipteki armatürleri grupla
            var gruplar = bosArmaturler.GroupBy(a => a.BlokAdi).ToList();
            
            int anahtarIdx = 0;
            foreach (var grup in gruplar)
            {
                var armaturler = grup.ToList();
                if (odadakiAnahtarlar.Count == 0) continue;
                var ilkAnahtar = odadakiAnahtarlar[Math.Min(anahtarIdx, odadakiAnahtarlar.Count - 1)];

                var siraliArmaturler = GeometrikSirala(new Point2D(ilkAnahtar.X, ilkAnahtar.Y), armaturler);

                while (siraliArmaturler.Count > 0 && anahtarIdx < odadakiAnahtarlar.Count)
                {
                    var anahtar = odadakiAnahtarlar[anahtarIdx];
                    // Önceki grubun/çalıştırmanın doldurduğu kapasite düşülür.
                    int bosKapasite = Math.Max(0, KapasiteHesapla(anahtar.Tip) - AnahtarMevcutYuk(kat, anahtar.Id));
                    if (bosKapasite == 0) { anahtarIdx++; continue; }
                    int devSayisi = MaxDevreNo(anahtar.Tip);

                    int atanacakSayi = Math.Min(siraliArmaturler.Count, bosKapasite);
                    var atanacaklar = siraliArmaturler.Take(atanacakSayi).ToList();
                    siraliArmaturler.RemoveRange(0, atanacakSayi);

                    int bolumBoyutu = (int)Math.Ceiling(atanacakSayi / (double)devSayisi);
                    for (int d = 0; d < devSayisi; d++)
                    {
                        var devreAtanacaklar = atanacaklar.Skip(d * bolumBoyutu).Take(bolumBoyutu).ToList();
                        if (devreAtanacaklar.Count == 0) continue;
                        
                        var devre = EnsureDevre(kat, anahtar.Id, d + 1);
                        foreach (var a in devreAtanacaklar)
                        {
                            if (!devre.ArmaturElemanIds.Contains(a.Id))
                                devre.ArmaturElemanIds.Add(a.Id);
                        }

                        if (string.IsNullOrEmpty(devre.BuatId))
                        {
                            var odadakiBuat = EnYakinBuat(kat, oda.Id, anahtar.X, anahtar.Y);
                            if (odadakiBuat != null) ButaAta(kat, devre, odadakiBuat.Id);
                        }
                    }
                    anahtarIdx++;
                }

                if (siraliArmaturler.Count > 0)
                {
                    var sonAnahtar = odadakiAnahtarlar.Last();
                    int devSayisi = MaxDevreNo(sonAnahtar.Tip);
                    int bolumBoyutu = (int)Math.Ceiling(siraliArmaturler.Count / (double)devSayisi);
                    for (int d = 0; d < devSayisi; d++)
                    {
                        var devreAtanacaklar = siraliArmaturler.Skip(d * bolumBoyutu).Take(bolumBoyutu).ToList();
                        if (devreAtanacaklar.Count == 0) continue;
                        var devre = EnsureDevre(kat, sonAnahtar.Id, d + 1);
                        foreach (var a in devreAtanacaklar)
                        {
                            if (!devre.ArmaturElemanIds.Contains(a.Id))
                                devre.ArmaturElemanIds.Add(a.Id);
                        }

                        if (string.IsNullOrEmpty(devre.BuatId))
                        {
                            var odadakiBuat = EnYakinBuat(kat, oda.Id, sonAnahtar.X, sonAnahtar.Y);
                            if (odadakiBuat != null) ButaAta(kat, devre, odadakiBuat.Id);
                        }
                    }
                    siraliArmaturler.Clear();
                }
            }
        }
    }

    /// Katın buat-gerektiren prizlerini en yakın priz buatına (PaftaTipi=Priz) daisy-chain bağlar.
    /// Müstakil/trifaze/kombine/UPS prizler direkt panoya gider — bu metot onlara dokunmaz.
    /// <paramref name="odaId"/> verilirse yalnız o oda işlenir (Priz Mahal Modu).
    public static void KatPrizleriniOtomatikKablola(BasitProjeKat kat, string? odaId = null)
    {
        IEnumerable<BasitProjeOda> hedefOdalar = odaId == null ? kat.Odalar : kat.Odalar.Where(o => o.Id == odaId);

        // Zaten bir priz devresine atanmış prizler yeniden dağıtılmaz — aksi halde
        // buat sayısı/sıralama değişen ikinci çalıştırmada aynı priz iki devreye
        // girer (çift kablo + pano özetinde çift yük). Aydınlatmadaki
        // atananArmaturIds korumasının priz karşılığıdır.
        var atananPrizIds = kat.PrizDevreleri
            .SelectMany(d => d.PrizElemanIds)
            .ToHashSet();

        foreach (var oda in hedefOdalar)
        {
            var buatGerektirenPrizler = kat.Elemanlar
                .Where(e => e.OdaId == oda.Id && ElemanPrizMi(e.Tip) && !ElemanPrizDirektPanoMu(e)
                            && !atananPrizIds.Contains(e.Id))
                .OrderBy(e => e.X).ThenBy(e => e.Y)
                .ToList();
            if (buatGerektirenPrizler.Count == 0) continue;

            var odadakiPrizBuatlari = kat.Elemanlar
                .Where(e => e.OdaId == oda.Id && e.Tip == BpcElemanTipi.Buat && e.PaftaTipi == BpcPaftaTipi.Priz)
                .OrderBy(e => e.X).ThenBy(e => e.Y)
                .ToList();
            if (odadakiPrizBuatlari.Count == 0) continue;

            // Prizleri buatlara dengeli dağıt; ancak hiçbir buat PrizBasinaBuat (7)
            // sınırını aşmasın. Yerleştirme servisi normalde ceil(n/7) buat ürettiği
            // için dengeli dağıtım zaten ≤7 verir; buat manuel eksikse artan prizler
            // bağlanmadan kalır ("Boştakileri Göster"de görünür) — sınır aşılmaz.
            int buatSayisi = odadakiPrizBuatlari.Count;
            var kapasite = odadakiPrizBuatlari.Select(b => Math.Max(0, PrizBuatKapasitesi -
                kat.PrizDevreleri.Where(d => d.BuatId == b.Id).Sum(d => d.PrizElemanIds.Count))).ToArray();
            int kalanKapasite = kapasite.Sum();
            int prizIdx = 0;
            for (int i = 0; i < buatSayisi && prizIdx < buatGerektirenPrizler.Count; i++)
            {
                var buat = odadakiPrizBuatlari[i];
                int kalanPriz = buatGerektirenPrizler.Count - prizIdx;
                int kalanBuat = buatSayisi - i;
                int dengeli = (int)Math.Ceiling(kalanPriz / (double)kalanBuat);
                kalanKapasite -= kapasite[i];
                // Sonraki buatlar elle doldurulmuşsa kullanılabilir kapasiteyi bu buatta değerlendir.
                int adet = Math.Min(kapasite[i], Math.Max(dengeli, kalanPriz - kalanKapasite));
                if (adet == 0) continue;
                var mevcutDevre = kat.PrizDevreleri.FirstOrDefault(d => d.BuatId == buat.Id);
                if (mevcutDevre == null)
                {
                    mevcutDevre = new BpcPrizDevre { BuatId = buat.Id };
                    kat.PrizDevreleri.Add(mevcutDevre);
                }

                for (int j = 0; j < adet && prizIdx < buatGerektirenPrizler.Count; j++)
                {
                    var priz = buatGerektirenPrizler[prizIdx++];
                    if (!mevcutDevre.PrizElemanIds.Contains(priz.Id))
                        mevcutDevre.PrizElemanIds.Add(priz.Id);
                }
            }
        }
    }

    /// İki pano (ör. tali → kat panosu) arası besleme rotasını geçiş odaları +
    /// duvar boyunca hesaplar. Buat→pano ile aynı yönlendirme motorunu kullanır;
    /// böylece tali besleme hatları da çapraz değil duvar kenarından ilerler.
    public static List<Point2D> PanoBeslemeRota(
        BasitProjeKat kat,
        BasitProjeEleman kaynakPano,
        BasitProjeEleman hedefPano,
        IEnumerable<string> gecisOdaIds)
    {
        var izinli = IzinliGecisOdalari(kat, kaynakPano, hedefPano, gecisOdaIds);
        return BuatPanoRota(kaynakPano, hedefPano, izinli, kat.Odalar);
    }

    private static List<BasitProjeOda> IzinliGecisOdalari(
        BasitProjeKat kat,
        BasitProjeEleman buat,
        BasitProjeEleman pano,
        IEnumerable<string> gecisOdaIds)
    {
        var izinli = new HashSet<string>(gecisOdaIds);
        foreach (var oda in kat.Odalar.Where(o => o.GecisSecili))
            izinli.Add(oda.Id);

        if (!string.IsNullOrWhiteSpace(buat.OdaId))
            izinli.Add(buat.OdaId);
        if (!string.IsNullOrWhiteSpace(pano.OdaId))
            izinli.Add(pano.OdaId);

        var buatPos = new Point2D(buat.X, buat.Y);
        var panoPos = new Point2D(pano.X, pano.Y);
        foreach (var oda in kat.Odalar)
        {
            if (NoktaOdayaAitMi(buatPos, oda) ||
                NoktaOdayaAitMi(panoPos, oda))
            {
                izinli.Add(oda.Id);
            }
        }

        return kat.Odalar.Where(o => izinli.Contains(o.Id)).ToList();
    }

    private static bool NoktaOdayaAitMi(Point2D p, BasitProjeOda oda)
    {
        const double wallTolerance = 0.18;
        return PointInPolygon(p, oda.Poligon) ||
               EnYakinDuvarMesafesi(p, [oda]) <= wallTolerance;
    }

    private static List<Point2D> BuatPanoRota(
        BasitProjeEleman buat,
        BasitProjeEleman pano,
        IReadOnlyList<BasitProjeOda> izinliOdalar,
        IReadOnlyList<BasitProjeOda>? tumOdalar = null)
    {
        var start = new Point2D(buat.X, buat.Y);
        var end = new Point2D(pano.X, pano.Y);

        if (izinliOdalar.Count == 0)
        {
            // Oda geometrisi yoksa eski davranış: düz çizgi. Oda geometrisi varsa
            // geçiş izni olmadan tüm odalar üzerinden fallback yapılmaz.
            if (tumOdalar is { Count: > 0 })
                return [];
            return [start, end];
        }

        var wallRoute = DuvarAgindaRota(start, end, izinliOdalar, 0.10);
        if (wallRoute.Count >= 2)
        {
            var rota = RotaTemizle(UzunCaprazlariDikKir(BasUcEkle(start, wallRoute, end), izinliOdalar));
            return rota;
        }

        // Son çare yine yalnız izinli odalarla sınırlıdır. Tüm odalar üzerinden
        // rota aramak, "Geçiş" izni olmayan mahallerden kablo geçmesine yol açar.
        var shortest = IzinliOdaGrafRota(start, end, izinliOdalar);
        if (shortest.Count >= 2)
        {
            var rota = RotaTemizle(UzunCaprazlariDikKir(shortest, izinliOdalar));
            if (RotaIzinliOdalardaMi(rota, izinliOdalar))
                return rota;
        }

        return [];
    }

    private static bool RotaIzinliOdalardaMi(
        IReadOnlyList<Point2D> rota,
        IReadOnlyList<BasitProjeOda> izinliOdalar)
    {
        if (rota.Count < 2) return false;
        for (int i = 0; i < rota.Count - 1; i++)
        {
            if (!SegmentIzinliOdalardaMi(rota[i], rota[i + 1], izinliOdalar))
                return false;
        }
        return true;
    }

    private static List<Point2D> IzinliOdaGrafRota(Point2D start, Point2D end, IReadOnlyList<BasitProjeOda> izinliOdalar)
    {
        var nodes = new List<Point2D> { start, end };
        foreach (var oda in izinliOdalar)
        {
            if (oda.Poligon.Count == 0) continue;
            nodes.Add(oda.Merkez);
            nodes.AddRange(oda.Poligon);
        }

        nodes = nodes
            .Aggregate(new List<Point2D>(), (acc, p) =>
            {
                if (!acc.Any(x => Dist(x, p) < 0.05)) acc.Add(p);
                return acc;
            });

        int n = nodes.Count;
        var edges = Enumerable.Range(0, n).Select(_ => new List<(int To, double W)>()).ToList();
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                if (!SegmentIzinliOdalardaMi(nodes[i], nodes[j], izinliOdalar)) continue;
                var w = Dist(nodes[i], nodes[j]);
                edges[i].Add((j, w));
                edges[j].Add((i, w));
            }
        }

        var dist = Enumerable.Repeat(double.MaxValue, n).ToArray();
        var prev = Enumerable.Repeat(-1, n).ToArray();
        var used = new bool[n];
        dist[0] = 0;

        for (int iter = 0; iter < n; iter++)
        {
            int u = -1;
            double best = double.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (!used[i] && dist[i] < best)
                {
                    best = dist[i];
                    u = i;
                }
            }
            if (u < 0 || u == 1) break;
            used[u] = true;

            foreach (var (to, w) in edges[u])
            {
                if (used[to]) continue;
                var nd = dist[u] + w;
                if (nd < dist[to])
                {
                    dist[to] = nd;
                    prev[to] = u;
                }
            }
        }

        if (double.IsInfinity(dist[1]) || dist[1] == double.MaxValue)
            return [];

        var path = new List<Point2D>();
        for (int at = 1; at >= 0; at = prev[at])
        {
            path.Add(nodes[at]);
            if (at == 0) break;
            if (prev[at] < 0) return [];
        }
        path.Reverse();
        return path;
    }

    private static bool AyniOdaIcindeMi(Point2D a, Point2D b, IReadOnlyList<BasitProjeOda> odalar)
        => odalar.Any(o => PointInPolygon(a, o.Poligon) && PointInPolygon(b, o.Poligon));

    private static bool SegmentIzinliOdalardaMi(Point2D a, Point2D b, IReadOnlyList<BasitProjeOda> odalar)
    {
        if (KisaAciklikKoprusuMu(a, b, odalar))
            return true;

        int samples = Math.Max(12, (int)Math.Ceiling(Dist(a, b) / 0.15));
        for (int i = 0; i <= samples; i++)
        {
            double t = i / (double)samples;
            var p = new Point2D(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
            if (!NoktaOdaIcindeMi(p, odalar) && EnYakinDuvarMesafesi(p, odalar) > 0.12)
                return false;
        }
        return true;
    }

    private static bool KisaAciklikKoprusuMu(Point2D a, Point2D b, IReadOnlyList<BasitProjeOda> odalar)
    {
        if (Dist(a, b) > GecisKoprusuMaxM + 0.25) return false;

        bool aBagli = NoktaOdaIcindeMi(a, odalar) || EnYakinDuvarMesafesi(a, odalar) <= 0.25;
        bool bBagli = NoktaOdaIcindeMi(b, odalar) || EnYakinDuvarMesafesi(b, odalar) <= 0.25;
        if (!aBagli || !bBagli) return false;

        var mid = new Point2D((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);
        return EnYakinDuvarMesafesi(mid, odalar) <= (GecisKoprusuMaxM / 2.0 + 0.20);
    }

    // ─── Rota hesaplama ──────────────────────────────────────────────────

    /// Devrenin kablo rotalarını hesaplayıp kat.Kablolar'a yazar.
    /// wallSlot: aynı duvardaki kaçıncı kablo (staggering için).
    public static void RotaHesapla(BasitProjeKat kat, BpcDevre devre, BpcRotaTipi rotaTipi,
        int wallSlot = 0)
    {
        rotaTipi = devre.ManuelRotaTipi ?? rotaTipi;
        kat.Kablolar.RemoveAll(k => k.DevreId == devre.Id);

        if (string.IsNullOrEmpty(devre.AnahtarElemanId)) return;

        var anahtar = kat.Elemanlar.FirstOrDefault(e => e.Id == devre.AnahtarElemanId);
        if (anahtar == null) return;

        // ── Anahtar → Armatürler ──────────────────────────────────────
        if (devre.ArmaturElemanIds.Count > 0)
        {
            var armatürler = devre.ArmaturElemanIds
                .Select(id => kat.Elemanlar.FirstOrDefault(e => e.Id == id))
                .OfType<BasitProjeEleman>()
                .ToList();

            if (armatürler.Count > 0)
            {
                var kabloTip = devre.DevreNo == 2
                    ? BpcKabloTipi.GucKablosu2
                    : AnahtarTipineGoreKabloTipi(anahtar.Tip);

                List<Point2D> rota;
                if (kat.Odalar.Count > 0)
                    rota = ArmaturOdaFarkindaliRota(anahtar, armatürler, kat.Odalar, rotaTipi);
                else
                    rota = rotaTipi == BpcRotaTipi.DaisyChain
                        ? DaisyChainRota(anahtar, armatürler)
                        : DogruCizgiRota(anahtar, armatürler);

                kat.Kablolar.Add(new BpcKablo { DevreId = devre.Id, Tip = kabloTip, Rota = rota });
            }
        }

        // ── Vavien seyahat kablosu ────────────────────────────────────
        var vavienGrup = kat.VavienGruplari.FirstOrDefault(g => g.DevreId == devre.Id);
        if (vavienGrup != null)
        {
            var a2 = kat.Elemanlar.FirstOrDefault(e => e.Id == vavienGrup.Anahtar2Id);
            if (a2 != null)
            {
                List<Point2D> seyahatRota;
                if (kat.Odalar.Count > 0)
                {
                    seyahatRota = DuvarBoyuncaRota(
                        new Point2D(anahtar.X, anahtar.Y),
                        new Point2D(a2.X, a2.Y),
                        kat.Odalar,
                        wallSlot);
                }
                else
                {
                    seyahatRota = [new(anahtar.X, anahtar.Y), new(a2.X, a2.Y)];
                }
                kat.Kablolar.Add(new BpcKablo
                {
                    DevreId = devre.Id,
                    Tip     = BpcKabloTipi.SeyahatKablosu,
                    Rota    = seyahatRota
                });
            }
        }

        // ── Anahtar → Buat (duvar boyunca veya düz) ──────────────────
        if (devre.DevreNo == 1 && !string.IsNullOrEmpty(devre.BuatId))
        {
            var buat = kat.Elemanlar.FirstOrDefault(e => e.Id == devre.BuatId);
            if (buat != null)
            {
                List<Point2D> buatRota;
                if (ElemanDuvarSnapMi(anahtar.Tip) && kat.Odalar.Count > 0)
                {
                    buatRota = DuvarBoyuncaRota(
                        new Point2D(anahtar.X, anahtar.Y),
                        new Point2D(buat.X, buat.Y),
                        kat.Odalar,
                        wallSlot);
                }
                else
                {
                    buatRota = [new(anahtar.X, anahtar.Y), new(buat.X, buat.Y)];
                }

                kat.Kablolar.Add(new BpcKablo
                {
                    DevreId = devre.Id,
                    Tip     = BpcKabloTipi.GucKablosu,
                    Rota    = buatRota
                });

                if (anahtar.Tip == BpcElemanTipi.Vavien)
                    VavienIkinciAnahtardanButaKabloEkle(kat, devre, anahtar, buat, wallSlot);
            }
        }
    }

    private static void VavienIkinciAnahtardanButaKabloEkle(
        BasitProjeKat kat,
        BpcDevre devre,
        BasitProjeEleman anahtar1,
        BasitProjeEleman buat,
        int wallSlot)
    {
        var grup = kat.VavienGruplari.FirstOrDefault(g => g.DevreId == devre.Id);
        if (grup == null) return;

        var anahtar2Id = grup.Anahtar1Id == anahtar1.Id ? grup.Anahtar2Id : grup.Anahtar1Id;
        var anahtar2 = kat.Elemanlar.FirstOrDefault(e => e.Id == anahtar2Id);
        if (anahtar2 == null) return;

        List<Point2D> rota;
        if (kat.Odalar.Count > 0)
        {
            rota = DuvarBoyuncaRota(
                new Point2D(anahtar2.X, anahtar2.Y),
                new Point2D(buat.X, buat.Y),
                kat.Odalar,
                wallSlot + 1);
        }
        else
        {
            rota = [new(anahtar2.X, anahtar2.Y), new(buat.X, buat.Y)];
        }

        kat.Kablolar.Add(new BpcKablo
        {
            DevreId = devre.Id,
            Tip = BpcKabloTipi.GucKablosu,
            Rota = rota
        });
    }

    // ─── Duvar boyunca rota ───────────────────────────────────────────────

    /// Duvar snap'li bir noktadan (anahtarPos) hedefe (buatPos veya ikinci anahtar)
    /// duvar boyunca gider. İki nokta aynı odadaysa o odanın çevresini, farklı
    /// odadaysa her iki odanın duvarlarını birleştirip kısa bir köprü ile bağlar.
    /// wallSlot=0 → 2 cm içeride, her +1 slot için +4 cm daha içeride.
    public static List<Point2D> DuvarBoyuncaRota(
        Point2D anahtarPos, Point2D buatPos,
        IEnumerable<BasitProjeOda> odalar,
        int wallSlot = 0,
        bool directToTargetInLastRoom = false)
    {
        const double baseOffset = 0.10;
        const double slotStep   = 0.04;
        double offset = baseOffset + wallSlot * slotStep;

        var odaList = odalar.ToList();
        var (poly1, startEdge1) = EnYakinKenar(anahtarPos, odaList);
        var (poly2, endEdge2)   = EnYakinKenar(buatPos,    odaList);

        if (directToTargetInLastRoom)
        {
            if (ReferenceEquals(poly1, poly2) || poly2.Count < 3)
            {
                return [anahtarPos, buatPos];
            }

            var normalRota = DuvarBoyuncaRota(anahtarPos, buatPos, odaList, wallSlot, directToTargetInLastRoom: false);
            int firstInTargetIdx = -1;
            for (int i = 1; i < normalRota.Count; i++)
            {
                if (PointInPolygon(normalRota[i], poly2))
                {
                    firstInTargetIdx = i;
                    break;
                }
            }

            if (firstInTargetIdx != -1)
            {
                var directRota = new List<Point2D>();
                for (int i = 0; i <= firstInTargetIdx; i++)
                {
                    directRota.Add(normalRota[i]);
                }
                directRota.Add(buatPos);
                return RotaTemizle(UzunCaprazlariDikKir(directRota, odaList));
            }
        }

        var duzRota = KisaDuzDuvarRota(anahtarPos, buatPos, odaList);
        if (duzRota.Count >= 2)
            return duzRota;

        var agRota = DuvarAgindaRota(anahtarPos, buatPos, odaList, offset);
        if (agRota.Count >= 2)
            return RotaTemizle(UzunCaprazlariDikKir(BasUcEkle(anahtarPos, agRota, buatPos), odaList));

        if (poly1.Count < 3)
            return [anahtarPos, buatPos];

        // Aynı oda → tek poligon üzerinden yürü
        if (ReferenceEquals(poly1, poly2) || poly2.Count < 3)
        {
            var endEdge = ReferenceEquals(poly1, poly2) ? endEdge2 : EnYakinKenarIndex(buatPos, poly1);
            var walk = TekPoligonYuru(poly1, startEdge1, endEdge, anahtarPos, buatPos, offset);
            return RotaTemizle(UzunCaprazlariDikKir(BasUcEkle(anahtarPos, walk, buatPos), odaList));
        }

        // Farklı oda → poly1 ve poly2 arasındaki en yakın kenar çiftini bul
        var (bridgeEdge1, bridgeEdge2) = EnYakinKenarCifti(poly1, poly2);

        // poly1: startEdge1 → bridgeEdge1
        var bridgeMid1 = KenarOrta(poly1[bridgeEdge1], poly1[(bridgeEdge1 + 1) % poly1.Count]);
        var walk1 = TekPoligonYuru(poly1, startEdge1, bridgeEdge1, anahtarPos, bridgeMid1, offset);

        // poly2: bridgeEdge2 → endEdge2
        var bridgeMid2 = KenarOrta(poly2[bridgeEdge2], poly2[(bridgeEdge2 + 1) % poly2.Count]);
        var walk2 = TekPoligonYuru(poly2, bridgeEdge2, endEdge2, bridgeMid2, buatPos, offset);

        var combined = new List<Point2D> { anahtarPos };
        combined.AddRange(walk1);
        if (walk1.Count > 0 && walk2.Count > 0)
            combined.AddRange(DikAciliKopru(walk1[^1], walk2[0], odaList));
        combined.AddRange(walk2);
        combined.Add(buatPos);
        return RotaTemizle(UzunCaprazlariDikKir(combined, odaList));
    }

    private static List<Point2D> KisaDuzDuvarRota(
        Point2D start,
        Point2D end,
        IReadOnlyList<BasitProjeOda> odalar)
    {
        const double sameLineTol = 0.12;
        const double wallTol = 0.18;

        if (Math.Abs(start.X - end.X) <= sameLineTol &&
            SegmentDuvaraYakinMi(start, end, odalar, wallTol))
        {
            return [start, new Point2D(start.X, end.Y), end];
        }

        if (Math.Abs(start.Y - end.Y) <= sameLineTol &&
            SegmentDuvaraYakinMi(start, end, odalar, wallTol))
        {
            return [start, new Point2D(end.X, start.Y), end];
        }

        var corner1 = new Point2D(start.X, end.Y);
        var corner2 = new Point2D(end.X, start.Y);
        bool corner1Ok = SegmentDuvaraYakinMi(start, corner1, odalar, wallTol) &&
                         SegmentDuvaraYakinMi(corner1, end, odalar, wallTol);
        bool corner2Ok = SegmentDuvaraYakinMi(start, corner2, odalar, wallTol) &&
                         SegmentDuvaraYakinMi(corner2, end, odalar, wallTol);

        if (!corner1Ok && !corner2Ok)
            return [];

        if (corner1Ok && !corner2Ok)
            return [start, corner1, end];

        if (corner2Ok && !corner1Ok)
            return [start, corner2, end];

        return KopruSkoru(start, corner1, end, odalar) <= KopruSkoru(start, corner2, end, odalar)
            ? [start, corner1, end]
            : [start, corner2, end];
    }

    private static bool SegmentDuvaraYakinMi(
        Point2D a,
        Point2D b,
        IReadOnlyList<BasitProjeOda> odalar,
        double tolerance)
    {
        const int samples = 8;
        for (int i = 0; i <= samples; i++)
        {
            double t = i / (double)samples;
            var p = new Point2D(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
            if (EnYakinDuvarMesafesi(p, odalar) > tolerance)
                return false;
        }
        return true;
    }

    private readonly record struct WallEdge(int OdaIndex, int EdgeIndex, Point2D A, Point2D B);

    // ── Duvar graf önbelleği (K12, 2026-07-19) ──────────────────────────────
    // Taban graf (duvar kenarları + yakın düğüm birleştirme + O(E²) kısa açıklık
    // köprüleri) her rota sorgusunda yeniden kurulmak yerine oda-kümesi anahtarıyla
    // önbelleğe alınır. Sorgu başına yalnız start/end düğümleri kopya graf üzerine
    // eklenir. M12'de oda geometrisi M50'den salt-okunur geldiği için taban graf
    // oturum boyunca kararlıdır; anahtar koordinat hash'i içerdiğinden geometri
    // değişirse eski girdi doğal olarak ıskalanır (kapasite aşımında temizlenir).

    private sealed class DuvarGraf
    {
        public List<WallEdge> Edges = new();
        public List<Point2D> Nodes = new();
        public Dictionary<(long X, long Y), int> NodeKeys = new();
        public Dictionary<int, List<(int To, double W)>> Adj = new();
    }

    private static readonly Dictionary<string, DuvarGraf> _grafCache = new();
    private const int GrafCacheKapasite = 8;

    private static string GrafAnahtari(IReadOnlyList<BasitProjeOda> odalar)
    {
        var sb = new System.Text.StringBuilder(odalar.Count * 24);
        foreach (var o in odalar)
        {
            double h = 0;
            foreach (var p in o.Poligon) h += p.X * 31.17 + p.Y * 17.31;
            sb.Append(o.Id).Append(':').Append(o.Poligon.Count)
              .Append(':').Append(Math.Round(h, 4)).Append(';');
        }
        return sb.ToString();
    }

    private static DuvarGraf DuvarGrafiniAlVeyaKur(IReadOnlyList<BasitProjeOda> odalar)
    {
        var anahtar = GrafAnahtari(odalar);
        if (_grafCache.TryGetValue(anahtar, out var mevcut)) return mevcut;

        var graf = new DuvarGraf { Edges = TumDuvarKenarlariniAl(odalar) };
        var nodes = graf.Nodes;
        var nodeKeys = graf.NodeKeys;
        var adj = graf.Adj;

        int AddNode(Point2D p)
        {
            var key = PointKey(p);
            if (nodeKeys.TryGetValue(key, out var existing))
                return existing;
            int id = nodes.Count;
            nodeKeys[key] = id;
            nodes.Add(p);
            adj[id] = new List<(int To, double W)>();
            return id;
        }

        void Connect(int a, int b)
        {
            if (a == b) return;
            var w = Dist(nodes[a], nodes[b]);
            if (w < 1e-9) return;
            adj[a].Add((b, w));
            adj[b].Add((a, w));
        }

        foreach (var edge in graf.Edges)
        {
            var a = AddNode(edge.A);
            var b = AddNode(edge.B);
            Connect(a, b);
        }

        YakinDugumleriBagla(nodes, adj);
        KisaAciklikKopruleriniBagla(graf.Edges, nodes, adj, AddNode, Connect);

        if (_grafCache.Count >= GrafCacheKapasite) _grafCache.Clear();
        _grafCache[anahtar] = graf;
        return graf;
    }

    private static List<Point2D> DuvarAgindaRota(
        Point2D start, Point2D end,
        IReadOnlyList<BasitProjeOda> odalar,
        double offset)
    {
        var taban = DuvarGrafiniAlVeyaKur(odalar);
        var edges = taban.Edges;
        if (edges.Count == 0) return [];

        // Sorgu kopyası: taban graf paylaşımlıdır, mutasyona uğramamalı.
        var nodes = new List<Point2D>(taban.Nodes);
        var nodeKeys = new Dictionary<(long X, long Y), int>(taban.NodeKeys);
        var adj = new Dictionary<int, List<(int To, double W)>>(taban.Adj.Count + 8);
        foreach (var kv in taban.Adj) adj[kv.Key] = new List<(int To, double W)>(kv.Value);

        int AddNode(Point2D p)
        {
            var key = PointKey(p);
            if (nodeKeys.TryGetValue(key, out var existing))
                return existing;
            int id = nodes.Count;
            nodeKeys[key] = id;
            nodes.Add(p);
            adj[id] = new List<(int To, double W)>();
            return id;
        }

        void Connect(int a, int b)
        {
            if (a == b) return;
            var w = Dist(nodes[a], nodes[b]);
            if (w < 1e-9) return;
            adj[a].Add((b, w));
            adj[b].Add((a, w));
        }

        int source = AddNode(start);
        int target = AddNode(end);
        KenarAdaylariniBagla(start, source, edges, nodes, adj, AddNode, Connect);
        KenarAdaylariniBagla(end, target, edges, nodes, adj, AddNode, Connect);

        var rawPath = EnKisaYol(source, target, nodes, adj);
        if (rawPath.Count < 4)
            return [];

        var wallPath = rawPath
            .Skip(1)
            .Take(rawPath.Count - 2)
            .Select(i => nodes[i]);

        return RotaTemizle(wallPath
            .Select(p => DuvarIcineOfsetle(p, edges, odalar, offset)));
    }

    private static List<WallEdge> TumDuvarKenarlariniAl(IReadOnlyList<BasitProjeOda> odalar)
    {
        var result = new List<WallEdge>();
        for (int oi = 0; oi < odalar.Count; oi++)
        {
            var poly = odalar[oi].Poligon;
            if (poly.Count < 3) continue;
            for (int ei = 0; ei < poly.Count; ei++)
                result.Add(new WallEdge(oi, ei, poly[ei], poly[(ei + 1) % poly.Count]));
        }
        return result;
    }

    private static void KenarAdaylariniBagla(
        Point2D p, int pointNode,
        IReadOnlyList<WallEdge> edges,
        IReadOnlyList<Point2D> nodes,
        Dictionary<int, List<(int To, double W)>> adj,
        Func<Point2D, int> addNode,
        Action<int, int> connect)
    {
        var candidates = EnYakinKenarAdaylari(p, edges);
        foreach (var edge in candidates)
        {
            var proj = KenarProje(p, edge.A, edge.B);
            int projNode = addNode(proj);
            int aNode = addNode(edge.A);
            int bNode = addNode(edge.B);

            AddWeightedEdge(pointNode, projNode, Dist(p, proj), nodes, adj);
            connect(projNode, aNode);
            connect(projNode, bNode);
        }
    }

    private static List<WallEdge> EnYakinKenarAdaylari(Point2D p, IReadOnlyList<WallEdge> edges)
    {
        const double cornerTolerance = 0.08;
        double minD = double.MaxValue;
        var distances = new List<(WallEdge Edge, double D)>();
        foreach (var edge in edges)
        {
            var proj = KenarProje(p, edge.A, edge.B);
            var d = Dist(p, proj);
            distances.Add((edge, d));
            if (d < minD) minD = d;
        }

        return distances
            .Where(x => x.D <= minD + cornerTolerance)
            .Select(x => x.Edge)
            .ToList();
    }

    private static void AddWeightedEdge(
        int a, int b, double w,
        IReadOnlyList<Point2D> nodes,
        Dictionary<int, List<(int To, double W)>> adj)
    {
        if (a == b) return;
        if (w < 1e-9) w = Dist(nodes[a], nodes[b]);
        adj[a].Add((b, w));
        adj[b].Add((a, w));
    }

    private static void YakinDugumleriBagla(
        IReadOnlyList<Point2D> nodes,
        Dictionary<int, List<(int To, double W)>> adj)
    {
        const double tol = 0.03;
        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                var d = Dist(nodes[i], nodes[j]);
                if (d > tol || d < 1e-9) continue;
                adj[i].Add((j, d));
                adj[j].Add((i, d));
            }
        }
    }

    private static void KisaAciklikKopruleriniBagla(
        IReadOnlyList<WallEdge> edges,
        IReadOnlyList<Point2D> nodes,
        Dictionary<int, List<(int To, double W)>> adj,
        Func<Point2D, int> addNode,
        Action<int, int> connect)
    {
        const double minGap = 0.04;
        for (int i = 0; i < edges.Count; i++)
        {
            for (int j = i + 1; j < edges.Count; j++)
            {
                if (edges[i].OdaIndex == edges[j].OdaIndex) continue;
                var (a, b, d) = EnYakinSegmentNoktalari(edges[i].A, edges[i].B, edges[j].A, edges[j].B);
                if (d < minGap || d > GecisKoprusuMaxM) continue;

                var an = addNode(a);
                var bn = addNode(b);
                connect(an, addNode(edges[i].A));
                connect(an, addNode(edges[i].B));
                connect(bn, addNode(edges[j].A));
                connect(bn, addNode(edges[j].B));
                connect(an, bn);
            }
        }
    }

    private static (Point2D A, Point2D B, double D) EnYakinSegmentNoktalari(
        Point2D a1, Point2D a2,
        Point2D b1, Point2D b2)
    {
        var adaylar = new List<(Point2D A, Point2D B)>
        {
            (a1, KenarProje(a1, b1, b2)),
            (a2, KenarProje(a2, b1, b2)),
            (KenarProje(b1, a1, a2), b1),
            (KenarProje(b2, a1, a2), b2)
        };

        return adaylar
            .Select(x => (x.A, x.B, D: Dist(x.A, x.B)))
            .OrderBy(x => x.D)
            .First();
    }

    private static List<int> EnKisaYol(
        int source, int target,
        IReadOnlyList<Point2D> nodes,
        IReadOnlyDictionary<int, List<(int To, double W)>> adj)
    {
        var dist = Enumerable.Repeat(double.PositiveInfinity, nodes.Count).ToArray();
        var prev = Enumerable.Repeat(-1, nodes.Count).ToArray();
        var pq = new PriorityQueue<int, double>();

        dist[source] = 0;
        pq.Enqueue(source, 0);

        while (pq.Count > 0)
        {
            pq.TryDequeue(out int u, out double du);
            if (du > dist[u] + 1e-9) continue;
            if (u == target) break;
            if (!adj.TryGetValue(u, out var list)) continue;

            foreach (var (v, w) in list)
            {
                var nd = du + w;
                if (nd >= dist[v] - 1e-9) continue;
                dist[v] = nd;
                prev[v] = u;
                pq.Enqueue(v, nd);
            }
        }

        if (double.IsPositiveInfinity(dist[target]))
            return [];

        var path = new List<int>();
        for (int at = target; at >= 0; at = prev[at])
        {
            path.Add(at);
            if (at == source) break;
        }
        path.Reverse();
        return path.Count > 0 && path[0] == source ? path : [];
    }

    private static Point2D DuvarIcineOfsetle(
        Point2D p,
        IReadOnlyList<WallEdge> edges,
        IReadOnlyList<BasitProjeOda> odalar,
        double offset)
    {
        var edge = edges
            .OrderBy(e => Dist(p, KenarProje(p, e.A, e.B)))
            .First();
        var centroid = PolyCentroid(odalar[edge.OdaIndex].Poligon);
        var inward = IcNormal(edge.A, edge.B, centroid);
        return new Point2D(p.X + inward.X * offset, p.Y + inward.Y * offset);
    }

    private static List<Point2D> RotaTemizle(IEnumerable<Point2D> points)
    {
        const double eps = 1e-6;
        var result = new List<Point2D>();
        foreach (var p in points)
        {
            if (result.Count > 0 && Dist(result[^1], p) < eps)
                continue;
            result.Add(p);
        }

        for (int i = result.Count - 2; i > 0; i--)
        {
            var a = result[i - 1];
            var b = result[i];
            var c = result[i + 1];
            var abx = b.X - a.X;
            var aby = b.Y - a.Y;
            var bcx = c.X - b.X;
            var bcy = c.Y - b.Y;
            var cross = Math.Abs(abx * bcy - aby * bcx);
            if (cross < 1e-8)
                result.RemoveAt(i);
        }

        return result;
    }

    private static List<Point2D> UzunCaprazlariDikKir(
        IReadOnlyList<Point2D> rota,
        IReadOnlyList<BasitProjeOda> odalar)
    {
        const double eps = 1e-6;
        const double minDiagonal = 0.15;
        if (rota.Count < 2) return rota.ToList();

        var result = new List<Point2D> { rota[0] };
        for (int i = 0; i < rota.Count - 1; i++)
        {
            var a = rota[i];
            var b = rota[i + 1];
            if (Dist(a, b) > minDiagonal &&
                Math.Abs(a.X - b.X) > eps &&
                Math.Abs(a.Y - b.Y) > eps)
            {
                result.AddRange(DikAciliKopru(a, b, odalar).Where(p => Dist(result[^1], p) > eps));
            }
            else if (Dist(result[^1], b) > eps)
            {
                result.Add(b);
            }
        }

        return result;
    }

    private static List<Point2D> DikAciliKopru(
        Point2D from,
        Point2D to,
        IReadOnlyList<BasitProjeOda> odalar)
    {
        const double eps = 1e-6;
        if (Dist(from, to) < eps)
            return [];
        if (Math.Abs(from.X - to.X) < eps || Math.Abs(from.Y - to.Y) < eps)
            return [to];

        var aday1 = new Point2D(from.X, to.Y);
        var aday2 = new Point2D(to.X, from.Y);
        var secilen = KopruSkoru(from, aday1, to, odalar) <= KopruSkoru(from, aday2, to, odalar)
            ? aday1
            : aday2;

        return [secilen, to];
    }

    private static double KopruSkoru(
        Point2D a,
        Point2D corner,
        Point2D b,
        IReadOnlyList<BasitProjeOda> odalar)
    {
        return SegmentDuvarUzaklikSkoru(a, corner, odalar)
             + SegmentDuvarUzaklikSkoru(corner, b, odalar)
             + (NoktaOdaIcindeMi(corner, odalar) ? 1000.0 : 0.0);
    }

    private static double SegmentDuvarUzaklikSkoru(
        Point2D a,
        Point2D b,
        IReadOnlyList<BasitProjeOda> odalar)
    {
        const int samples = 5;
        double score = 0;
        for (int i = 1; i < samples; i++)
        {
            double t = i / (double)samples;
            var p = new Point2D(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
            score += EnYakinDuvarMesafesi(p, odalar);
            if (NoktaOdaIcindeMi(p, odalar))
                score += 100.0;
        }
        return score;
    }

    private static double EnYakinDuvarMesafesi(Point2D p, IReadOnlyList<BasitProjeOda> odalar)
    {
        double minD = double.MaxValue;
        foreach (var oda in odalar)
        {
            var poly = oda.Poligon;
            for (int i = 0; i < poly.Count; i++)
            {
                var proj = KenarProje(p, poly[i], poly[(i + 1) % poly.Count]);
                minD = Math.Min(minD, Dist(p, proj));
            }
        }
        return minD;
    }

    private static bool NoktaOdaIcindeMi(Point2D p, IReadOnlyList<BasitProjeOda> odalar)
        => odalar.Any(o => PointInPolygon(p, o.Poligon));

    private static bool PointInPolygon(Point2D p, IReadOnlyList<Point2D> poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var pi = poly[i];
            var pj = poly[j];
            if (((pi.Y > p.Y) != (pj.Y > p.Y)) &&
                (p.X < (pj.X - pi.X) * (p.Y - pi.Y) / ((pj.Y - pi.Y) + 1e-12) + pi.X))
            {
                inside = !inside;
            }
        }
        return inside;
    }

    private static (long X, long Y) PointKey(Point2D p)
    {
        const double q = 0.005;
        return ((long)Math.Round(p.X / q), (long)Math.Round(p.Y / q));
    }

    private static List<Point2D> BasUcEkle(Point2D bas, List<Point2D> orta, Point2D uc)
    {
        var r = new List<Point2D>(orta.Count + 2) { bas };
        r.AddRange(orta);
        r.Add(uc);
        return r;
    }

    private static Point2D KenarOrta(Point2D a, Point2D b)
        => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);

    /// İki poligon arasındaki en yakın kenar çiftini döner.
    private static (int e1, int e2) EnYakinKenarCifti(
        IReadOnlyList<Point2D> poly1, IReadOnlyList<Point2D> poly2)
    {
        int best1 = 0, best2 = 0;
        double minD = double.MaxValue;
        int n1 = poly1.Count, n2 = poly2.Count;
        for (int i = 0; i < n1; i++)
        {
            var m1 = KenarOrta(poly1[i], poly1[(i + 1) % n1]);
            for (int j = 0; j < n2; j++)
            {
                var m2 = KenarOrta(poly2[j], poly2[(j + 1) % n2]);
                double d = Dist(m1, m2);
                if (d < minD) { minD = d; best1 = i; best2 = j; }
            }
        }
        return (best1, best2);
    }

    /// Tek poligon üzerinde startEdge'den endEdge'e kadar duvar yürür,
    /// içe ofset uygulanmış ara noktaları döner (uç noktalar hariç).
    private static List<Point2D> TekPoligonYuru(
        IReadOnlyList<Point2D> poly, int startEdge, int endEdge,
        Point2D startWorld, Point2D endWorld, double offset)
    {
        int n = poly.Count;
        var wallStart = KenarProje(startWorld, poly[startEdge], poly[(startEdge + 1) % n]);
        var wallEnd   = KenarProje(endWorld,   poly[endEdge],   poly[(endEdge   + 1) % n]);

        var fwPts = SinirNoktalar(wallStart, startEdge, wallEnd, endEdge, poly, true);
        var bwPts = SinirNoktalar(wallStart, startEdge, wallEnd, endEdge, poly, false);
        var pts   = RotaUzunluk(fwPts) <= RotaUzunluk(bwPts) ? fwPts : bwPts;

        var centroid = PolyCentroid(poly);
        return pts.Select(p =>
        {
            int e = EnYakinKenarIndex(p, poly);
            var inward = IcNormal(poly[e], poly[(e + 1) % n], centroid);
            return new Point2D(p.X + inward.X * offset, p.Y + inward.Y * offset);
        }).ToList();
    }

    private static List<Point2D> SinirNoktalar(
        Point2D wallStart, int startEdge,
        Point2D wallEnd,   int endEdge,
        IReadOnlyList<Point2D> poly, bool ileri)
    {
        int n = poly.Count;
        var pts = new List<Point2D> { wallStart };

        if (startEdge == endEdge)
        {
            pts.Add(wallEnd);
            return pts;
        }

        int i     = ileri ? (startEdge + 1) % n : startEdge;
        int stop  = ileri ? endEdge              : (endEdge + 1) % n;
        int guard = n + 2;

        while (i != stop && guard-- > 0)
        {
            pts.Add(poly[i]);
            i = ileri ? (i + 1) % n : (i - 1 + n) % n;
        }

        pts.Add(wallEnd);
        return pts;
    }

    // ─── Wall-slot (staggering) hesabı ───────────────────────────────────

    /// Her devrenin aynı polygon kenarındaki kaçıncı kablo olduğunu hesaplar.
    public static Dictionary<string, int> HesaplaWallSlotlari(BasitProjeKat kat)
    {
        var slots       = new Dictionary<string, int>();
        var edgeCounters = new Dictionary<(int oda, int edge), int>();
        var odaList     = kat.Odalar.ToList();

        foreach (var d in kat.Devreler)
        {
            if (string.IsNullOrEmpty(d.AnahtarElemanId) || string.IsNullOrEmpty(d.BuatId))
                continue;
            var anahtar = kat.Elemanlar.FirstOrDefault(e => e.Id == d.AnahtarElemanId);
            if (anahtar == null || !ElemanDuvarSnapMi(anahtar.Tip)) continue;

            var p = new Point2D(anahtar.X, anahtar.Y);
            int bestOda = 0, bestEdge = 0;
            double minD = double.MaxValue;
            for (int oi = 0; oi < odaList.Count; oi++)
            {
                var poly = odaList[oi].Poligon;
                for (int ei = 0; ei < poly.Count; ei++)
                {
                    var proj = KenarProje(p, poly[ei], poly[(ei + 1) % poly.Count]);
                    double dd = Dist(p, proj);
                    if (dd < minD) { minD = dd; bestOda = oi; bestEdge = ei; }
                }
            }

            var key = (bestOda, bestEdge);
            if (!edgeCounters.ContainsKey(key)) edgeCounters[key] = 0;
            slots[d.Id] = edgeCounters[key]++;
        }
        return slots;
    }

    // ─── Rota algoritmaları ──────────────────────────────────────────────

    // Tavan armatürleri: aynı oda içinde düz çizgi (tavan kablosu), farklı oda → duvar ağı.
    private static List<Point2D> ArmaturOdaFarkindaliRota(
        BasitProjeEleman anahtar,
        List<BasitProjeEleman> armatürler,
        IReadOnlyList<BasitProjeOda> odalar,
        BpcRotaTipi rotaTipi)
    {
        if (rotaTipi == BpcRotaTipi.DogruCizgi)
        {
            // Yıldız topolojisi: merkez noktası aynı oda içinde ise düz çizgiler yeterli
            double cx = armatürler.Average(a => a.X);
            double cy = armatürler.Average(a => a.Y);
            var center = new Point2D(cx, cy);
            var anahtarPos = new Point2D(anahtar.X, anahtar.Y);
            var rota = new List<Point2D>();
            rota.Add(anahtarPos);

            var toCenter = OdaFarkindaliSegment(anahtarPos, center, odalar);
            if (toCenter.Count > 1) rota.AddRange(toCenter.Skip(1));
            else rota.Add(center);

            foreach (var arm in armatürler)
            {
                var armPos = new Point2D(arm.X, arm.Y);
                rota.Add(center);
                rota.Add(armPos);
            }
            return rota;
        }

        // Daisy chain: sıralı bağlantı — her segment oda kontrolünden geçer
        {
            var rota = new List<Point2D>();
            Point2D prev = new(anahtar.X, anahtar.Y);
            rota.Add(prev);
            foreach (var arm in armatürler)
            {
                var armPos = new Point2D(arm.X, arm.Y);
                var seg = OdaFarkindaliSegment(prev, armPos, odalar);
                if (seg.Count > 1)
                    rota.AddRange(seg.Skip(1));
                else
                    rota.Add(armPos);
                prev = armPos;
            }
            return rota;
        }
    }

    // Aynı oda içinde düz çizgi; farklı oda veya duvar kesişimi varsa duvar ağı.
    private static List<Point2D> OdaFarkindaliSegment(
        Point2D from, Point2D to,
        IReadOnlyList<BasitProjeOda> odalar)
    {
        if (AyniOdaIcindeMi(from, to, odalar) &&
            SegmentIzinliOdalardaMi(from, to, odalar))
            return [from, to];

        var agRota = DuvarAgindaRota(from, to, odalar, 0.10);
        if (agRota.Count >= 2)
            return RotaTemizle(BasUcEkle(from, agRota, to));

        return [from, to];
    }

    private static List<Point2D> DaisyChainRota(BasitProjeEleman anahtar,
        List<BasitProjeEleman> armatürler)
    {
        var rota = new List<Point2D>();
        if (armatürler.Count == 0) return rota;

        var anahtarPos = new Point2D(anahtar.X, anahtar.Y);
        rota.Add(anahtarPos);

        // Anahtardan ilk armatüre çapraz gitmek yerine L (dirsek) şeklinde ortogonal git
        var ilk = armatürler.First();
        var ilkPos = new Point2D(ilk.X, ilk.Y);

        // Eğer anahtar dikey veya yatay olarak armatürle zaten çok yakın değilse bir dirsek atalım
        if (Math.Abs(anahtarPos.X - ilkPos.X) > 0.15 && Math.Abs(anahtarPos.Y - ilkPos.Y) > 0.15)
        {
            // Duvara paralel (X veya Y) ilerleme dirseği (Burada anahtarın X'inde kalıp Y'sine gitmeyi tercih edelim)
            rota.Add(new Point2D(anahtarPos.X, ilkPos.Y));
        }

        foreach (var arm in armatürler)
        {
            rota.Add(new Point2D(arm.X, arm.Y));
        }

        return rota;
    }

    private static List<Point2D> DogruCizgiRota(BasitProjeEleman anahtar,
        List<BasitProjeEleman> armatürler)
    {
        double cx = armatürler.Average(a => a.X);
        double cy = armatürler.Average(a => a.Y);
        var rota = new List<Point2D>
        {
            new(anahtar.X, anahtar.Y),
            new(cx, cy)
        };
        foreach (var a in armatürler)
        {
            rota.Add(new Point2D(cx, cy));
            rota.Add(new Point2D(a.X, a.Y));
        }
        return rota;
    }

    private static double Mesafe(Point2D p, BasitProjeEleman e)
    {
        double dx = p.X - e.X, dy = p.Y - e.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static BpcKabloTipi AnahtarTipineGoreKabloTipi(BpcElemanTipi tip) => tip switch
    {
        BpcElemanTipi.DarbeAnahtari => BpcKabloTipi.KontrolKablosu,
        BpcElemanTipi.Sensör        => BpcKabloTipi.KontrolKablosu,
        BpcElemanTipi.Vavien        => BpcKabloTipi.GucKablosu,
        _                           => BpcKabloTipi.GucKablosu
    };

    // ─── Geometri yardımcıları ───────────────────────────────────────────

    private static (IReadOnlyList<Point2D> poly, int edge) EnYakinKenar(
        Point2D p, IEnumerable<BasitProjeOda> odalar)
    {
        IReadOnlyList<Point2D> bestPoly = Array.Empty<Point2D>();
        int bestEdge = 0;
        double minD  = double.MaxValue;
        foreach (var oda in odalar)
        {
            var poly = oda.Poligon;
            for (int i = 0; i < poly.Count; i++)
            {
                var proj = KenarProje(p, poly[i], poly[(i + 1) % poly.Count]);
                double d = Dist(p, proj);
                if (d < minD) { minD = d; bestPoly = poly; bestEdge = i; }
            }
        }
        return (bestPoly, bestEdge);
    }

    private static int EnYakinKenarIndex(Point2D p, IReadOnlyList<Point2D> poly)
    {
        int best  = 0;
        double minD = double.MaxValue;
        for (int i = 0; i < poly.Count; i++)
        {
            var proj = KenarProje(p, poly[i], poly[(i + 1) % poly.Count]);
            double d = Dist(p, proj);
            if (d < minD) { minD = d; best = i; }
        }
        return best;
    }

    private static Point2D KenarProje(Point2D p, Point2D a, Point2D b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double lenSq = dx * dx + dy * dy;
        if (lenSq < 1e-12) return a;
        double t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq, 0.0, 1.0);
        return new Point2D(a.X + t * dx, a.Y + t * dy);
    }

    private static double Dist(Point2D a, Point2D b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double RotaUzunluk(IEnumerable<Point2D> pts)
    {
        double d = 0;
        Point2D? prev = null;
        foreach (var p in pts)
        {
            if (prev.HasValue) d += Dist(prev.Value, p);
            prev = p;
        }
        return d;
    }

    // Alan-ağırlıklı centroid; boş poligonda (0,0) döner (bölme koruması). İç normal
    // yönü için kullanılır — ortak PoligonYardimci.Centroid tek kaynak.
    private static Point2D PolyCentroid(IReadOnlyList<Point2D> poly)
        => PoligonYardimci.Centroid(poly);

    /// Kenarın odaya bakan (içe) birim normalini döner.
    private static Point2D IcNormal(Point2D a, Point2D b, Point2D centroid)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-9) return new Point2D(0, 1);
        // İki aday normal
        var n1 = new Point2D(-dy / len,  dx / len);
        var n2 = new Point2D( dy / len, -dx / len);
        // Kenar orta noktasından centroid'e olan yönü seç
        var mid = new Point2D((a.X + b.X) / 2, (a.Y + b.Y) / 2);
        double dot = (centroid.X - mid.X) * n1.X + (centroid.Y - mid.Y) * n1.Y;
        return dot >= 0 ? n1 : n2;
    }

    // ─── Görsel segmentlere dönüştür ─────────────────────────────────────

    public static List<(Point2D A, Point2D B, string Renk)> KabloSegmentleri(
        IEnumerable<BpcKablo> kablolar)
    {
        var result = new List<(Point2D, Point2D, string)>();
        var devreSlots = new Dictionary<string, int>();
        int nextSlot = 0;
        foreach (var k in kablolar)
        {
            if (k.Rota.Count < 2) continue;
            var renk = KabloRengi(k);
            if (!devreSlots.TryGetValue(k.DevreId, out var slot))
                devreSlots[k.DevreId] = slot = nextSlot++;

            for (int i = 0; i < k.Rota.Count - 1; i++)
            {
                var a = k.Rota[i];
                var b = k.Rota[i + 1];
                if (SegmentDuvaraParalelMi(a, b))
                    (a, b) = SegmentiParalelKaydir(a, b, slot);
                result.Add((a, b, renk));
            }
        }
        return result;
    }

    private static bool SegmentDuvaraParalelMi(Point2D a, Point2D b)
    {
        const double tol = 0.03;
        return Dist(a, b) >= 0.10 &&
               (Math.Abs(a.X - b.X) <= tol || Math.Abs(a.Y - b.Y) <= tol);
    }

    private static (Point2D A, Point2D B) SegmentiParalelKaydir(Point2D a, Point2D b, int slot)
    {
        const double step = 0.04;
        double offset = slot switch
        {
            0 => 0,
            _ when slot % 2 == 1 => ((slot + 1) / 2) * step,
            _ => -(slot / 2) * step
        };

        if (Math.Abs(offset) < 1e-9)
            return (a, b);

        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-9)
            return (a, b);

        double nx = -dy / len;
        double ny = dx / len;
        return (new Point2D(a.X + nx * offset, a.Y + ny * offset),
                new Point2D(b.X + nx * offset, b.Y + ny * offset));
    }

    /// Kablonun rengini belirler. Zayıf akım kablolarında renk, kablonun ait olduğu
    /// sistemden (SistemAnahtar) katalog üzerinden okunur.
    public static string KabloRengi(BpcKablo kablo)
        => kablo.Tip == BpcKabloTipi.ZayifAkimKablosu && kablo.SistemAnahtar is { } s
            ? BpcSistemKatalogu.Get(s).KabloRengi
            : KabloRengi(kablo.Tip);

    public static string KabloRengi(BpcKabloTipi tip) => tip switch
    {
        BpcKabloTipi.GucKablosu2    => "#E0954E",
        BpcKabloTipi.SeyahatKablosu => "#4E8CE0",
        BpcKabloTipi.KontrolKablosu => "#E04E4E",
        BpcKabloTipi.PanoKablosu    => "#00D0B8",
        BpcKabloTipi.PrizKablosu    => "#E07840", // turuncu-kırmızı (priz)
        BpcKabloTipi.MekanikKablosu => "#80D8FF", // açık mavi (mekanik)
        _                           => "#E0C84E"
    };

    public static Dictionary<string, int> HesaplaMekanikWallSlotlari(BasitProjeKat kat)
    {
        var slots       = new Dictionary<string, int>();
        var edgeCounters = new Dictionary<(int oda, int edge), int>();
        var odaList     = kat.Odalar.ToList();

        if (kat.MekanikDevreleri == null) return slots;

        foreach (var d in kat.MekanikDevreleri)
        {
            var klima = kat.Elemanlar.FirstOrDefault(e => e.Id == d.KlimaElemanId);
            if (klima == null) continue;

            var p = new Point2D(klima.X, klima.Y);
            int bestOda = 0, bestEdge = 0;
            double minD = double.MaxValue;
            for (int oi = 0; oi < odaList.Count; oi++)
            {
                var poly = odaList[oi].Poligon;
                for (int ei = 0; ei < poly.Count; ei++)
                {
                    var proj = KenarProje(p, poly[ei], poly[(ei + 1) % poly.Count]);
                    double dd = Dist(p, proj);
                    if (dd < minD) { minD = dd; bestOda = oi; bestEdge = ei; }
                }
            }

            var key = (bestOda, bestEdge);
            if (!edgeCounters.ContainsKey(key)) edgeCounters[key] = 0;
            slots[d.Id] = edgeCounters[key]++;
        }
        return slots;
    }

    public static void MekanikRotalariniHesapla(BasitProjeKat kat)
    {
        kat.Kablolar.RemoveAll(k => k.Tip == BpcKabloTipi.MekanikKablosu);
        if (kat.MekanikDevreleri == null) return;

        var mekSlots = HesaplaMekanikWallSlotlari(kat);

        foreach (var devre in kat.MekanikDevreleri.ToList())
        {
            var klima = kat.Elemanlar.FirstOrDefault(e => e.Id == devre.KlimaElemanId && (e.Tip == BpcElemanTipi.KlimaIcUnite || e.Tip == BpcElemanTipi.KlimaDisUnite));
            var pano = kat.Elemanlar.FirstOrDefault(e => e.Id == devre.PanoId && e.Tip == BpcElemanTipi.KatPanosu);
            if (klima == null || pano == null)
            {
                kat.MekanikDevreleri.Remove(devre);
                continue;
            }

            var gecisOdaIds = new List<string>();
            var izinliOdalar = IzinliGecisOdalari(kat, klima, pano, gecisOdaIds);

            int slot = mekSlots.TryGetValue(devre.Id, out var s) ? s : 0;
            var startPos = new Point2D(klima.X, klima.Y);
            var endPos = new Point2D(pano.X, pano.Y);
            var rota = DuvarBoyuncaRota(startPos, endPos, izinliOdalar, wallSlot: slot);

            kat.Kablolar.Add(new BpcKablo
            {
                DevreId = devre.Id,
                Tip = BpcKabloTipi.MekanikKablosu,
                Rota = rota
            });
        }

        MekanikKablolariniNumaralandir(kat);
    }

    public static void MekanikKablolariniNumaralandir(BasitProjeKat kat)
    {
        if (kat.MekanikDevreleri == null) return;
        var mekKablolar = kat.Kablolar
            .Where(k => k.Tip == BpcKabloTipi.MekanikKablosu)
            .ToList();

        var panoByDevre = kat.MekanikDevreleri.ToDictionary(d => d.Id, d => d.PanoId);
        foreach (var grup in mekKablolar.GroupBy(k => panoByDevre.TryGetValue(k.DevreId, out var panoId) ? panoId : ""))
        {
            int sira = 1;
            foreach (var k in grup
                         .OrderBy(k => k.Rota.Count > 0 ? k.Rota[^1].Y : 0)
                         .ThenBy(k => k.Rota.Count > 0 ? k.Rota[^1].X : 0)
                         .ThenBy(k => k.DevreId))
            {
                k.PanoSira = sira++;
            }
        }
    }
}
