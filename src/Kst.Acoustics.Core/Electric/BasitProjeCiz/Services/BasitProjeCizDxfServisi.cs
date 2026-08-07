using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Kst.Acoustics.Core.Common.Geometry;
// 4.4 — BuildingAcoustics bağımlılığı koparıldı: parser artık ortak Bina katmanından.
using M50Parser = Kst.Acoustics.Core.Bina.Services.DxfKatPlaniParser;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.Ortak;
using netDxf;
using netDxf.Blocks;
using netDxf.Entities;
using netDxf.Tables;

namespace Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

public class BasitProjeCizDxfServisi
{
    public BasitProjeKat KatYukle(string filePath, int katNo, string katAdi, double katYuksekligiM)
    {
        // M8 DxfKatPlaniParser ile ham segmentleri + TEXT temelli oda isimlerini al.
        // M12'de kat DXF'leri CAD'deki ortak koordinat çerçevesini korumalı; aksi halde
        // her dosyanın kendi min köşesine normalize edilmesi kat hizasını bozar.
        var parser = new M50Parser();
        var binaKat = parser.Parse(filePath, katNo, katYuksekligiM, normalizeOrigin: false);
        var hamSegmentler = binaKat.HamSegmentler;
        var hamArclar = binaKat.DxfArclar
            .Select(a => (a.Merkez, a.R, a.BasAciDeg, a.BitAciDeg))
            .ToList();

        var kat = new BasitProjeKat
        {
            KatNo = katNo,
            Ad = string.IsNullOrWhiteSpace(katAdi) ? $"Kat {katNo}" : katAdi.Trim(),
            KatYuksekligiM = katYuksekligiM,
            DxfFilePath = filePath,
            UnitWarning = parser.UnitWarning
        };

        // Arka plan referansı için segmentleri sadeleştir: büyük mimari DXF'ler
        // (20-40 MB) eğri/yazı/tarama yüzünden kat başına 0.6-1.1 MİLYON segment
        // içerebiliyor; bunların %96-98'i 5 cm altı (bina ölçeğinde görünmez) mikro
        // parçalar. Tümünü saklayıp render etmek belleği/çizimi patlatıp uygulamayı
        // çökertiyordu. Çok kısa olanları ele + makul tavanla sınırla. (Flood-fill
        // oda tespiti zaten yukarıda TAM set üzerinde koştu; bu yalnız görüntü/snap.)
        foreach (var (a, b) in SadelestirSegmentler(hamSegmentler))
        {
            kat.DxfSegmentleri.Add(new BpcDxfSegment
            {
                A = new Point2D(a.X, a.Y),
                B = new Point2D(b.X, b.Y),
                Layer = ""
            });
        }

        // ARC entity'lerini de aktar (kapı yayı tespiti için)
        foreach (var (mer, r, bas, bit) in hamArclar)
            kat.DxfArclari.Add(new BpcArc { Merkez = mer, R = r, BasAciDeg = bas, BitAciDeg = bit });

        // Açıklık (kapı + pencere) tespiti — flood-fill'den ÖNCE çalışır ki sanal duvar
        // olarak kullanılabilsin.
        kat.Acikliklar = AciklikTespitServisi.Bul(
            hamSegmentler,
            hamArclar,
            mevcut: System.Array.Empty<BpcAciklik>());

        var aciklikSanalDuvarlari = kat.Acikliklar
            .Where(a => !a.IptalEdildi)
            .Select(a => (a.Bas, a.Bit))
            .ToList();

        // ── Oda tespiti: önce flood-fill (boşluk tarama) yöntemini dene ──────
        // Duvarları engel sayıp aralarındaki kapalı boşlukları oda olarak bulur;
        // çift çizgili/etiketsiz/kapalı-olmayan duvarlarda klasik tespitten sağlam.
        // Açıklıklar varsa kör dilation'ı azalt (daha temiz koridor/oda ayrımı).
        var ffAyar = aciklikSanalDuvarlari.Count > 0
            ? new Kst.Acoustics.Core.Bina.Services.BosluktanOdaTespitServisi.Ayarlar { KapiKapatmaM = 0.20 }
            : null;
        var ffPoligonlar = Kst.Acoustics.Core.Bina.Services.BosluktanOdaTespitServisi.OdalariAdaptifBul(
            hamSegmentler, ffAyar, aciklikSanalDuvarlari);

        if (ffPoligonlar.Count > 0)
        {
            // İsimleri DXF'in oda-içi TEXT/MTEXT etiketlerinden ata
            OdalariUygula(kat, ffPoligonlar, binaKat.HamMetinler);
            kat.UnitWarning = BirlestirUyari(parser.UnitWarning,
                $"Odalar boşluk tarama ile bulundu ({ffPoligonlar.Count} oda). Sınırları gerekirse elle düzeltin.");
        }
        else
        {
            // Fallback: klasik tespit (TEXT/polyline/döngü) — Bina parser çıktısı
            foreach (var oda in binaKat.Odalar)
            {
                kat.Odalar.Add(new BasitProjeOda
                {
                    Ad = oda.Ad,
                    AlanM2 = oda.AlanM2,
                    Poligon = oda.Poligon.Select(p => new Point2D(p.X, p.Y)).ToList()
                });
            }
        }

        // Oda poligonlarının dışında kalan otomatik açıklıkları temizle.
        AciklikYardimci.DisindaKalanlariSil(kat);
        // Kapı iki odaya hizmet eder: tek duvarda bulunan açıklıkları komşu duvara tamamla.
        AciklikYardimci.KomsuDuvarlariTamamla(kat);

        return kat;
    }

    /// <summary>
    /// Arka plan/görüntü için ham DXF segmentlerini sadeleştirir. Saf mantık Ortak katmana
    /// (<see cref="DxfSegmentYardimci.Sadelestir"/>) taşındı; bu metot uyumluluk için delege eder.
    /// </summary>
    public static IEnumerable<(Point2D A, Point2D B)> SadelestirSegmentler(
        IReadOnlyList<(Point2D A, Point2D B)> segmentler,
        double minUzunlukM = 0.05, int maxSegment = 200_000)
        => Kst.Acoustics.Core.Bina.Services.DxfSegmentYardimci.Sadelestir(segmentler, minUzunlukM, maxSegment);

    /// <summary>
    /// Eski .e4p12 dosyalarında biriken ham milyonlarca DXF segmentini transparan
    /// sadeleştirir. 2026-05-24'ten önce kaydedilmiş projeler render/snap'i
    /// ağırlaştırabiliyor — proje yüklemede çağrılır. Yalnız <paramref name="esik"/>
    /// üzeri kat etkilenir; eşik altı katlar dokunulmaz.
    /// </summary>
    /// <returns>(EtkilenenKatSayisi, ToplamÖnce, ToplamSonra) — 0 ise migrasyon
    /// gerekmedi; çağıran kullanıcıya status mesajı gösterir.</returns>
    public static (int EtkilenenKat, int ToplamOnce, int ToplamSonra) EskiSegmentMigrasyonuUygula(
        BasitProjeCizProje proje, int esik = 50_000)
    {
        int toplamOnce = 0, toplamSonra = 0, etkilenenKat = 0;
        foreach (var kat in proje.Katlar)
        {
            if (kat.DxfSegmentleri.Count <= esik) continue;

            int once = kat.DxfSegmentleri.Count;
            var ham = kat.DxfSegmentleri.Select(s => (s.A, s.B)).ToList();
            var sade = Kst.Acoustics.Core.Bina.Services.DxfSegmentYardimci.Sadelestir(ham).ToList();

            kat.DxfSegmentleri.Clear();
            foreach (var s in sade)
                kat.DxfSegmentleri.Add(new BpcDxfSegment { A = s.A, B = s.B });

            toplamOnce += once;
            toplamSonra += sade.Count;
            etkilenenKat++;
        }
        return (etkilenenKat, toplamOnce, toplamSonra);
    }

    /// <summary>
    /// Yüklü katın mevcut DXF segmentlerinden odaları boşluk tarama ile yeniden bulur.
    /// Var olan oda isimlerini centroid eşleşmesiyle korumaya çalışır.
    /// Bulunan oda sayısını döner; bulunamazsa mevcut odalar korunur ve 0 döner.
    /// </summary>
    public static int OdalariYenidenTara(BasitProjeKat kat,
        Kst.Acoustics.Core.Bina.Services.BosluktanOdaTespitServisi.Ayarlar? ayar = null)
    {
        if (kat.DxfSegmentleri.Count == 0) return 0;

        var segs = kat.DxfSegmentleri.Select(s => (s.A, s.B)).ToList();

        // Mevcut açıklıkları sanal duvar olarak ekle (varsa)
        var aciklikSanalDuvarlari = kat.Acikliklar
            .Where(a => !a.IptalEdildi)
            .Select(a => (a.Bas, a.Bit))
            .ToList();
        if (ayar is null && aciklikSanalDuvarlari.Count > 0)
            ayar = new Kst.Acoustics.Core.Bina.Services.BosluktanOdaTespitServisi.Ayarlar { KapiKapatmaM = 0.20 };

        var poligonlar = Kst.Acoustics.Core.Bina.Services.BosluktanOdaTespitServisi.OdalariAdaptifBul(segs, ayar, aciklikSanalDuvarlari);
        if (poligonlar.Count == 0) return 0;

        // Mevcut oda isimlerini Mekan benzeri taşıyıcıya çevirerek isim aktarımı yap
        var eskiAdlar = kat.Odalar
            .Where(o => o.Poligon.Count >= 3)
            .Select(o => (Ad: o.Ad, Merkez: o.Merkez))
            .ToList();

        kat.Odalar.Clear();
        var sayac = 0;
        foreach (var poly in poligonlar)
        {
            sayac++;
            string ad = $"Oda {sayac}";
            foreach (var (eskiAd, merkez) in eskiAdlar)
            {
                if (!string.IsNullOrWhiteSpace(eskiAd) &&
                    PoligonYardimci.IsPointInside(merkez, poly))
                {
                    ad = eskiAd;
                    break;
                }
            }
            kat.Odalar.Add(new BasitProjeOda
            {
                Ad = ad,
                AlanM2 = PoligonYardimci.Alan(poly),
                Poligon = poly
            });
        }

        // Yeni oda poligonlarına göre dışarıda kalan otomatik açıklıkları temizle.
        AciklikYardimci.DisindaKalanlariSil(kat);
        // Kapı iki odaya hizmet eder: tek duvarda bulunan açıklıkları komşu duvara tamamla.
        AciklikYardimci.KomsuDuvarlariTamamla(kat);

        return sayac;
    }

    // Flood-fill poligonlarını odaya çevirir; isimleri DXF'in oda-içi metinlerinden atar.
    private static void OdalariUygula(BasitProjeKat kat, List<List<Point2D>> poligonlar,
        IReadOnlyList<(Point2D Pos, string Value)> metinler)
    {
        int sayac = 0;
        foreach (var poly in poligonlar)
        {
            sayac++;
            kat.Odalar.Add(new BasitProjeOda
            {
                Ad = OdaAdiSec(poly, metinler) ?? $"Oda {sayac}",
                AlanM2 = PoligonYardimci.Alan(poly),
                Poligon = poly
            });
        }
    }

    // Poligon içine düşen metinlerden en uygun oda adını seçer.
    // Alan/ölçü/numara metinleri elenir; kalan isim-benzeri metinlerden merkeze en
    // yakın olan tercih edilir (oda etiketi genelde ortaya yazılır). Yoksa null.
    public static string? OdaAdiSec(IReadOnlyList<Point2D> poly,
        IReadOnlyList<(Point2D Pos, string Value)> metinler)
    {
        if (metinler.Count == 0) return null;
        var merkez = Centroid(poly);

        string? enIyi = null;
        double enYakin = double.MaxValue;
        foreach (var (pos, value) in metinler)
        {
            if (!IsimGibiMi(value)) continue;
            if (!PoligonYardimci.IsPointInside(pos, poly)) continue;
            double dx = pos.X - merkez.X, dy = pos.Y - merkez.Y;
            double d = dx * dx + dy * dy;
            if (d < enYakin) { enYakin = d; enIyi = value.Trim(); }
        }
        return enIyi;
    }

    // Sadece alan/ölçü/numara olan metinleri eler; gerçek isimleri kabul eder.
    private static bool IsimGibiMi(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        var t = s.Trim();
        if (t.Length > 40) return false; // muhtemelen not/açıklama, oda adı değil
        // Salt alan/ölçü kalıbı: "12.30", "12,30 m2", "250 m²", "3.00" → isim değil
        if (Regex.IsMatch(t, @"^\s*\d+([.,]\d+)?\s*(m2|m²|M2|mm|cm|m)?\s*$")) return false;
        // En az 2 harf içermeli (rakam/sembol yığını değil)
        int harf = t.Count(char.IsLetter);
        return harf >= 2;
    }

    private static Point2D Centroid(IReadOnlyList<Point2D> poly)
        => new(poly.Average(p => p.X), poly.Average(p => p.Y));

    private static string? BirlestirUyari(string? a, string b)
        => string.IsNullOrWhiteSpace(a) ? b : $"{a} {b}";

    /// <param name="armatürler">
    /// Aktif kütüphane listesi. Verilirse armatürler DXF BLOCK tanımı olarak gömülür
    /// ve INSERT ile yerleştirilir. Null ise basit geometri (circle+line) kullanılır.
    /// </param>
    public void DxfCiktiAl(BasitProjeKat kat, string outputPath,
        IReadOnlyList<BpcArmatürTanim>? armatürler = null)
    {
        var dxf = new DxfDocument();
        var refLayer    = new Layer("E4P-REFERENCE")   { Color = AciColor.DarkGray };
        var lightLayer  = new Layer("E4P-LIGHTING")    { Color = AciColor.Yellow };
        var switchLayer = new Layer("E4P-SWITCH")      { Color = AciColor.Cyan };
        var panelLayer  = new Layer("E4P-PANEL")       { Color = AciColor.Green };
        var sensorLayer = new Layer("E4P-SENSOR")      { Color = new AciColor(50) };  // açık yeşil
        var buatLayer   = new Layer("E4P-BUAT")        { Color = new AciColor(30) };  // turuncu
        var mechLayer   = new Layer("E4P-MECHANICAL")  { Color = new AciColor(150) }; // mavi tonu
        var yanginLayer = new Layer("E4P-YANGIN")      { Color = AciColor.Red };
        var annotLayer  = new Layer("E4P-ANNOTATION")  { Color = AciColor.Default };
        var dataLayer    = new Layer("E4P-DATA")        { Color = new AciColor(4) };
        var tvLayer      = new Layer("E4P-TV")          { Color = new AciColor(6) };
        var telefonLayer = new Layer("E4P-TELEFON")     { Color = new AciColor(30) };
        var cctvLayer    = new Layer("E4P-CCTV")        { Color = new AciColor(200) };
        var anonsLayer   = new Layer("E4P-ANONS")       { Color = new AciColor(94) };
        var diafonLayer  = new Layer("E4P-DIAFON")      { Color = new AciColor(110) };

        // ── Referans (DXF arka plan) ──────────────────────────────────────
        foreach (var seg in kat.DxfSegmentleri)
            dxf.Entities.Add(new Line(ToV3(seg.A), ToV3(seg.B)) { Layer = refLayer });

        // ── Oda poligonları + ad ──────────────────────────────────────────
        foreach (var oda in kat.Odalar.Where(o => o.Poligon.Count > 2))
        {
            var points = oda.Poligon.Select(p => new Polyline2DVertex(p.X, p.Y)).ToList();
            dxf.Entities.Add(new Polyline2D(points, true) { Layer = annotLayer });
            dxf.Entities.Add(new Text(oda.Ad, ToV3(oda.Merkez), 0.22) { Layer = annotLayer });
        }

        // ── Armatür BLOCK tanımları ───────────────────────────────────────
        // BlokAdi → Block nesnesi haritası; her blok yalnızca bir kez tanımlanır.
        var blokHarita = new Dictionary<string, Block>(StringComparer.OrdinalIgnoreCase);

        if (armatürler != null)
        {
            foreach (var tanim in armatürler)
            {
                if (string.IsNullOrWhiteSpace(tanim.BlokAdi)) continue;
                if (blokHarita.ContainsKey(tanim.BlokAdi)) continue;

                var blok = OlusturBlok(tanim, boyut: 0.30);
                blokHarita[tanim.BlokAdi] = blok;
            }
        }

        // ── Zayıf akım / yangın eleman blok tanımları (on-the-fly) ──────
        foreach (var el in kat.Elemanlar.Where(e => !string.IsNullOrWhiteSpace(e.BlokAdi)))
        {
            if (blokHarita.ContainsKey(el.BlokAdi)) continue;
            var tip = BasitProjeCizKutuphaneServisi.TipBlokAdindan(el.BlokAdi);
            if (tip is null) continue;
            var zt = BasitProjeCizKutuphaneServisi.ZayifAkimTanim(tip.Value);
            if (zt is null) continue;
            blokHarita[el.BlokAdi] = OlusturBlok(zt, boyut: 0.30);
        }

        // ── Elemanlar ─────────────────────────────────────────────────────
        // Aynı blok adı (veya aynı tip) + 5 cm içinde konum → yalnız ilkini yaz.
        // Model katmanında oluşan birebir/çok yakın kopyaları DXF'e taşımaz.
        const double CakismaTolM = 0.05;
        var yazilan = new List<(string Anahtar, double X, double Y)>(kat.Elemanlar.Count);

        foreach (var el in kat.Elemanlar)
        {
            var p = new Point2D(el.X, el.Y);
            var anahtar = string.IsNullOrWhiteSpace(el.BlokAdi) ? el.Tip.ToString() : el.BlokAdi;
            bool cakisiyor = false;
            foreach (var k in yazilan)
            {
                if (k.Anahtar == anahtar &&
                    Math.Abs(k.X - el.X) < CakismaTolM &&
                    Math.Abs(k.Y - el.Y) < CakismaTolM)
                { cakisiyor = true; break; }
            }
            if (cakisiyor) continue;
            yazilan.Add((anahtar, el.X, el.Y));

            var layer = ElemanLayer(el, lightLayer, switchLayer, panelLayer, sensorLayer, buatLayer,
                mechLayer, dataLayer, tvLayer, telefonLayer, cctvLayer, anonsLayer, diafonLayer, yanginLayer);

            if (!string.IsNullOrWhiteSpace(el.BlokAdi) && blokHarita.TryGetValue(el.BlokAdi, out var blok))
            {
                // DXF INSERT: önceden tanımlı blok ile yerleştir. Duvara snap edilmiş
                // (duvar montajlı + duvar dibinde) sembolleri tabanı duvara paralel olacak
                // şekilde döndür; sembolün üst yönü oda içine bakar.
                dxf.Entities.Add(new Insert(blok, ToV3(p))
                {
                    Layer = layer,
                    Rotation = DuvarSnapRotasyonDerece(el, kat)
                });
            }
            else
            {
                // Fallback: basit geometriler
                if (BasitProjeCizKabloServisi.ElemanArmaturMi(el.Tip))
                {
                    dxf.Entities.Add(new Circle(ToV3(p), 0.18) { Layer = layer });
                    dxf.Entities.Add(new Line(ToV3(new Point2D(p.X - 0.14, p.Y)), ToV3(new Point2D(p.X + 0.14, p.Y))) { Layer = layer });
                    dxf.Entities.Add(new Line(ToV3(new Point2D(p.X, p.Y - 0.14)), ToV3(new Point2D(p.X, p.Y + 0.14))) { Layer = layer });
                }
                else if (el.Tip == BpcElemanTipi.KatPanosu)
                {
                    dxf.Entities.Add(new Polyline2D(new[]
                    {
                        new Polyline2DVertex(p.X - 0.25, p.Y - 0.18),
                        new Polyline2DVertex(p.X + 0.25, p.Y - 0.18),
                        new Polyline2DVertex(p.X + 0.25, p.Y + 0.18),
                        new Polyline2DVertex(p.X - 0.25, p.Y + 0.18)
                    }, true) { Layer = layer });
                }
                else if (el.Tip == BpcElemanTipi.Sensör)
                {
                    dxf.Entities.Add(new Circle(ToV3(p), 0.12) { Layer = layer });
                    for (double r = 0.20; r <= 0.32; r += 0.10)
                        dxf.Entities.Add(new Arc(ToV3(p), r, 150, 210) { Layer = layer });
                }
                else if (el.Tip == BpcElemanTipi.Buat)
                {
                    dxf.Entities.Add(new Circle(ToV3(p), 0.16) { Layer = layer });
                    double d = 0.10;
                    dxf.Entities.Add(new Line(ToV3(new Point2D(p.X - d, p.Y - d)), ToV3(new Point2D(p.X + d, p.Y + d))) { Layer = layer });
                    dxf.Entities.Add(new Line(ToV3(new Point2D(p.X - d, p.Y + d)), ToV3(new Point2D(p.X + d, p.Y - d))) { Layer = layer });
                }
                else
                {
                    dxf.Entities.Add(new Circle(ToV3(p), 0.12) { Layer = layer });
                }
            }

            dxf.Entities.Add(new Text(LabelFor(el), ToV3(new Point2D(p.X + 0.22, p.Y + 0.10)), 0.16) { Layer = layer });
        }

        // ── Kablo rotaları ────────────────────────────────────────────────
        var kabloGuc      = new Layer("E4P-KABLO-GUC")      { Color = AciColor.Yellow };
        var kabloGuc2     = new Layer("E4P-KABLO-GUC2")     { Color = new AciColor(30) }; // turuncu
        var kabloSeyahat  = new Layer("E4P-KABLO-SEYAHAT")  { Color = AciColor.Cyan };
        var kabloKontrol  = new Layer("E4P-KABLO-KONTROL")  { Color = AciColor.Red };
        var kabloPano     = new Layer("E4P-KABLO-PANO")     { Color = new AciColor(4) }; // cyan
        var kabloPanoPriz = new Layer("E4P-KABLO-PANO-PRIZ") { Color = new AciColor(30) };
        var kabloMekanik  = new Layer("E4P-KABLO-MEKANIK")  { Color = new AciColor(150) }; // mavi tonu
        var kabloTava     = new Layer("E4P-KABLO-TAVASI")   { Color = AciColor.LightGray };
        var beslemeKablo  = new Layer("E4P-KABLO-BESLEME")  { Color = new AciColor(30) };
        var kabloData     = new Layer("E4P-KABLO-DATA")     { Color = new AciColor(4) };
        var kabloTv       = new Layer("E4P-KABLO-TV")       { Color = new AciColor(6) };
        var kabloTelefon  = new Layer("E4P-KABLO-TELEFON")  { Color = new AciColor(30) };
        var kabloCctv     = new Layer("E4P-KABLO-CCTV")     { Color = new AciColor(200) };
        var kabloAnons    = new Layer("E4P-KABLO-ANONS")    { Color = new AciColor(94) };
        var kabloDiafon   = new Layer("E4P-KABLO-DIAFON")   { Color = new AciColor(110) };
        var kabloYangin   = new Layer("E4P-KABLO-YANGIN")   { Color = AciColor.Red };
        var topraklama    = new Layer("E4P-TOPRAKLAMA")     { Color = AciColor.Green };
        var topraklamaBakir = new Layer("E4P-TOPRAKLAMA-BAKIR") { Color = new AciColor(30) };

        var buatPanoById = kat.BuatPanoBaglantilari.ToDictionary(b => b.Id);
        foreach (var kablo in kat.Kablolar)
        {
            if (kablo.Rota.Count < 2) continue;
            var layer = KabloLayer(kablo, buatPanoById, kabloGuc, kabloGuc2, kabloSeyahat, kabloKontrol,
                kabloPano, kabloPanoPriz, kabloMekanik, kabloData, kabloTv, kabloTelefon,
                kabloCctv, kabloAnons, kabloDiafon, kabloYangin);
            for (int i = 0; i < kablo.Rota.Count - 1; i++)
            {
                var a = kablo.Rota[i];
                var b = kablo.Rota[i + 1];
                if (Math.Abs(a.X - b.X) < 1e-9 && Math.Abs(a.Y - b.Y) < 1e-9) continue;
                dxf.Entities.Add(new Line(new Vector3(a.X, a.Y, 0), new Vector3(b.X, b.Y, 0)) { Layer = layer });
            }

            if (kablo.Tip == BpcKabloTipi.PanoKablosu && kablo.PanoSira is not null && kablo.Rota.Count >= 2)
            {
                var p = kablo.Rota[0]; // buat ucu — her kablo farklı konumda, pano ucunda üst üste biner
                var prefix = buatPanoById.TryGetValue(kablo.DevreId, out var baglanti)
                    ? baglanti.LinyePrefix
                    : "A";
                dxf.Entities.Add(new Text($"{prefix}{kablo.PanoSira}", ToV3(new Point2D(p.X + 0.20, p.Y + 0.20)), 0.16) { Layer = layer });
            }
            else if (kablo.Tip == BpcKabloTipi.MekanikKablosu && kablo.PanoSira is not null && kablo.Rota.Count >= 2)
            {
                var p = kablo.Rota[0]; // buat ucu
                dxf.Entities.Add(new Text($"M{kablo.PanoSira}", ToV3(new Point2D(p.X + 0.20, p.Y + 0.20)), 0.16) { Layer = layer });
            }
            else if (kablo.Tip == BpcKabloTipi.ZayifAkimKablosu && kablo.PanoSira is not null && kablo.Rota.Count >= 2)
            {
                var p = kablo.Rota[0]; // buat ucu
                var prefix = kablo.SistemAnahtar is { } sistem ? BpcSistemKatalogu.Get(sistem).LinyePrefix : "Z";
                dxf.Entities.Add(new Text($"{prefix}{kablo.PanoSira}", ToV3(new Point2D(p.X + 0.20, p.Y + 0.20)), 0.16) { Layer = layer });
            }
        }

        // ── Kablo tavaları ───────────────────────────────────────────────
        foreach (var tava in kat.KabloTavalari.Where(t => t.Rota.Count >= 2))
        {
            CizKabloTavasi(dxf, kabloTava, tava);
        }

        foreach (var hat in kat.PanoBeslemeHatlari.Where(h => h.Rota.Count >= 2))
        {
            for (int i = 0; i < hat.Rota.Count - 1; i++)
                dxf.Entities.Add(new Line(ToV3(hat.Rota[i]), ToV3(hat.Rota[i + 1])) { Layer = beslemeKablo });
            var mid = hat.Rota[hat.Rota.Count / 2];
            dxf.Entities.Add(new Text(hat.Etiket, ToV3(new Point2D(mid.X + 0.15, mid.Y + 0.15)), 0.16) { Layer = beslemeKablo });
        }

        CizTopraklama(dxf, topraklama, topraklamaBakir, kat.Topraklama);

        dxf.Save(outputPath);
    }

    /// Bir BasitProjeKat'ı kopyalar: aynı DXF segmentleri, odalar ve ofset; yeni Id.
    /// Elemanlar (armatür, anahtar) kopyalanmaz; benzer kat boş başlar.
    public static BasitProjeKat KatiKopyala(BasitProjeKat kaynak, int yeniKatNo, string? yeniAd = null)
    {
        var kopya = new BasitProjeKat
        {
            KatNo = yeniKatNo,
            Ad = yeniAd ?? $"Kat {yeniKatNo}",
            KatYuksekligiM = kaynak.KatYuksekligiM,
            DxfFilePath = kaynak.DxfFilePath,
            DxfGoster = kaynak.DxfGoster,
            BloklariGoster = kaynak.BloklariGoster,
            OriginOffset = new Point2D(kaynak.OriginOffset.X, kaynak.OriginOffset.Y),
            UnitWarning = kaynak.UnitWarning,
            Topraklama = new BpcTopraklama
            {
                SecimNoktalari = kaynak.Topraklama.SecimNoktalari.Select(p => new Point2D(p.X, p.Y)).ToList(),
                Kaziklar = kaynak.Topraklama.Kaziklar.Select(p => new Point2D(p.X, p.Y)).ToList(),
                EkTopraklamaVar = kaynak.Topraklama.EkTopraklamaVar,
                Seritler = kaynak.Topraklama.Seritler.Select(s => new BpcTopraklamaSerit
                {
                    A = new Point2D(s.A.X, s.A.Y),
                    B = new Point2D(s.B.X, s.B.Y),
                    Tip = s.Tip
                }).ToList()
            }
        };

        var odaIdMap = new Dictionary<string, string>();
        foreach (var oda in kaynak.Odalar)
        {
            var odaKopya = new BasitProjeOda
            {
                Ad = oda.Ad,
                AlanM2 = oda.AlanM2,
                GecisSecili = oda.GecisSecili,
                AsmaTavan = oda.AsmaTavan,
                MahalListesineDahil = oda.MahalListesineDahil,
                Poligon = oda.Poligon.Select(p => new Point2D(p.X, p.Y)).ToList()
            };
            odaIdMap[oda.Id] = odaKopya.Id;
            kopya.Odalar.Add(odaKopya);
        }

        foreach (var seg in kaynak.DxfSegmentleri)
        {
            kopya.DxfSegmentleri.Add(new BpcDxfSegment
            {
                A = new Point2D(seg.A.X, seg.A.Y),
                B = new Point2D(seg.B.X, seg.B.Y),
                Layer = seg.Layer
            });
        }

        kopya.DxfArclari = kaynak.DxfArclari.Select(a => new BpcArc
        {
            Merkez = new Point2D(a.Merkez.X, a.Merkez.Y),
            R = a.R,
            BasAciDeg = a.BasAciDeg,
            BitAciDeg = a.BitAciDeg
        }).ToList();

        kopya.Acikliklar = kaynak.Acikliklar.Select(a => new BpcAciklik
        {
            Bas = new Point2D(a.Bas.X, a.Bas.Y),
            Bit = new Point2D(a.Bit.X, a.Bit.Y),
            Tip = a.Tip,
            IptalEdildi = a.IptalEdildi,
            ManuelEklendi = a.ManuelEklendi,
            Kaynak = a.Kaynak
        }).ToList();

        kopya.MahalListesi = kaynak.MahalListesi.Select(s => new BpcMahalSatiri
        {
            Tip = s.Tip,
            ArmatürTanimId = s.ArmatürTanimId,
            ArmatürTanimAd = s.ArmatürTanimAd,
            YerlesimTipi = s.YerlesimTipi,
            MontajTipi = s.MontajTipi,
            GridAraligiM = s.GridAraligiM,
            GridAraligiYM = s.GridAraligiYM,
            DuvarAraligiM = s.DuvarAraligiM,
            Sayilar = s.Sayilar
                .Where(kv => odaIdMap.ContainsKey(kv.Key))
                .ToDictionary(kv => odaIdMap[kv.Key], kv => kv.Value),
            HucreAyarlari = s.HucreAyarlari
                .Where(kv => odaIdMap.ContainsKey(kv.Key))
                .ToDictionary(kv => odaIdMap[kv.Key], kv => KopyalaMahalHucreAyari(kv.Value))
        }).ToList();

        foreach (var tava in kaynak.KabloTavalari)
        {
            kopya.KabloTavalari.Add(new BpcKabloTavasi
            {
                Ad = tava.Ad,
                Tip = tava.Tip,
                Adet = tava.Adet,
                GenislikMm = tava.GenislikMm,
                YukseklikMm = tava.YukseklikMm,
                EtKalinligiMm = tava.EtKalinligiMm,
                AgirHizmet = tava.AgirHizmet,
                KotM = tava.KotM,
                EtiketGoster = tava.EtiketGoster,
                Rota = tava.Rota.Select(p => new Point2D(p.X, p.Y)).ToList(),
                TaliHatlar = tava.TaliHatlar.Select(t => new BpcKabloTavasiTaliHat
                {
                    GenislikMm = t.GenislikMm,
                    Rota = t.Rota.Select(p => new Point2D(p.X, p.Y)).ToList()
                }).ToList()
            });
        }

        foreach (var hat in kaynak.PanoBeslemeHatlari)
        {
            kopya.PanoBeslemeHatlari.Add(new BpcPanoBeslemeHatti
            {
                AnaPanoId = hat.AnaPanoId,
                TaliPanoId = hat.TaliPanoId,
                AnaKatNo = hat.AnaKatNo,
                TaliKatNo = hat.TaliKatNo,
                UygulamaTipi = hat.UygulamaTipi,
                Etiket = hat.Etiket,
                Rota = hat.Rota.Select(p => new Point2D(p.X, p.Y)).ToList()
            });
        }

        return kopya;
    }

    private static BpcMahalHucreAyar KopyalaMahalHucreAyari(BpcMahalHucreAyar ayar) => new()
    {
        Aktif = ayar.Aktif,
        GenelAyarKullan = ayar.GenelAyarKullan,
        YerlesimTipi = ayar.YerlesimTipi,
        MontajTipi = ayar.MontajTipi,
        GridAraligiM = ayar.GridAraligiM,
        GridAraligiYM = ayar.GridAraligiYM,
        DuvarAraligiM = ayar.DuvarAraligiM,
        Adet = ayar.Adet,
        OzelAd = ayar.OzelAd,
        OzelGucW = ayar.OzelGucW,
        OzelMonofazeAdet = ayar.OzelMonofazeAdet,
        OzelTrifazeAdet = ayar.OzelTrifazeAdet,
        OzelMegapiksel = ayar.OzelMegapiksel,
        OzelCihazAdet = ayar.OzelCihazAdet
    };

    private static void CizKabloTavasi(
        DxfDocument dxf,
        Layer layer,
        BpcKabloTavasi tava)
    {
        var tavaAdedi = Math.Clamp(tava.Adet, 1, 2);
        var genislikM = Math.Clamp(tava.GenislikMm / 1000.0, 0.07, 0.70);
        var half = genislikM / 2.0;
        var aralik = Math.Clamp(genislikM * 0.20, 0.05, 0.15);
        var merkezAraligi = genislikM + aralik;
        for (int i = 0; i < tava.Rota.Count - 1; i++)
        {
            CizKabloTavasiSegmenti(dxf, layer, tava.Rota[i], tava.Rota[i + 1], half, merkezAraligi, tavaAdedi);
        }

        foreach (var tali in tava.TaliHatlar.Where(t => t.Rota.Count >= 2))
        {
            var taliGenislikM = Math.Clamp(tali.GenislikMm / 1000.0, 0.04, 0.70);
            var taliHalf = taliGenislikM / 2.0;
            var taliAralik = Math.Clamp(taliGenislikM * 0.20, 0.04, 0.15);
            var taliMerkezAraligi = taliGenislikM + taliAralik;
            for (int i = 0; i < tali.Rota.Count - 1; i++)
                CizKabloTavasiSegmenti(dxf, layer, tali.Rota[i], tali.Rota[i + 1], taliHalf, taliMerkezAraligi, tavaAdedi);
        }

        if (tava.EtiketGoster)
        {
            var midIndex = (tava.Rota.Count - 1) / 2;
            var mid = new Point2D(
                (tava.Rota[midIndex].X + tava.Rota[midIndex + 1].X) / 2.0,
                (tava.Rota[midIndex].Y + tava.Rota[midIndex + 1].Y) / 2.0);
            dxf.Entities.Add(new Text(KabloTavasiEtiketi(tava), ToV3(new Point2D(mid.X + 0.15, mid.Y + 0.15)), 0.16)
            {
                Layer = layer
            });
        }
    }

    private static void CizTopraklama(
        DxfDocument dxf,
        Layer seritLayer,
        Layer bakirLayer,
        BpcTopraklama topraklama)
    {
        if (topraklama.Seritler.Count == 0 && topraklama.Kaziklar.Count == 0) return;

        foreach (var s in topraklama.Seritler)
        {
            var layer = s.Tip == BpcTopraklamaSeritTipi.OrguBakir ? bakirLayer : seritLayer;
            dxf.Entities.Add(new Line(ToV3(s.A), ToV3(s.B)) { Layer = layer });
        }

        foreach (var p in topraklama.Kaziklar)
        {
            dxf.Entities.Add(new Circle(ToV3(p), 0.16) { Layer = seritLayer });
            dxf.Entities.Add(new Line(ToV3(new Point2D(p.X - 0.14, p.Y)), ToV3(new Point2D(p.X + 0.14, p.Y))) { Layer = seritLayer });
            dxf.Entities.Add(new Line(ToV3(new Point2D(p.X, p.Y - 0.14)), ToV3(new Point2D(p.X, p.Y + 0.14))) { Layer = seritLayer });
        }

        var first = topraklama.Seritler.FirstOrDefault();
        if (first != null)
        {
            dxf.Entities.Add(new Text("30x3.5 Galv. Topraklama Seridi", ToV3(new Point2D(first.A.X + 0.25, first.A.Y + 0.25)), 0.16)
            {
                Layer = seritLayer
            });
            if (topraklama.EkTopraklamaVar)
            {
                dxf.Entities.Add(new Text("35 mm Orgulu Bakir Ek Topraklama", ToV3(new Point2D(first.A.X + 0.25, first.A.Y + 0.55)), 0.16)
                {
                    Layer = bakirLayer
                });
            }
        }
    }

    private static void CizKabloTavasiSegmenti(
        DxfDocument dxf,
        Layer layer,
        Point2D a,
        Point2D b,
        double half,
        double merkezAraligi,
        int tavaAdedi)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-6) return;
        var ux = -dy / len;
        var uy = dx / len;

        for (int tavaNo = 0; tavaNo < tavaAdedi; tavaNo++)
        {
            var merkezOfset = tavaAdedi == 1 ? 0.0 : (tavaNo == 0 ? -merkezAraligi / 2.0 : merkezAraligi / 2.0);
            var cox = ux * merkezOfset;
            var coy = uy * merkezOfset;
            var nx = ux * half;
            var ny = uy * half;

            var a1 = new Point2D(a.X + cox + nx, a.Y + coy + ny);
            var b1 = new Point2D(b.X + cox + nx, b.Y + coy + ny);
            var a2 = new Point2D(a.X + cox - nx, a.Y + coy - ny);
            var b2 = new Point2D(b.X + cox - nx, b.Y + coy - ny);

            dxf.Entities.Add(new Line(ToV3(a1), ToV3(b1)) { Layer = layer });
            dxf.Entities.Add(new Line(ToV3(a2), ToV3(b2)) { Layer = layer });
            for (double dist = 0.0; dist <= len + 1e-6; dist += 0.8)
            {
                var t = dist / len;
                var cx = a.X + dx * t + cox;
                var cy = a.Y + dy * t + coy;
                dxf.Entities.Add(new Line(
                    ToV3(new Point2D(cx + nx, cy + ny)),
                    ToV3(new Point2D(cx - nx, cy - ny))) { Layer = layer });
            }
        }
    }

    private static string KabloTavasiEtiketi(BpcKabloTavasi tava)
    {
        var adet = Math.Clamp(tava.Adet, 1, 2);
        var genislik = $"{tava.GenislikMm:N0}";
        var yukseklik = $"{tava.YukseklikMm:N0}";
        var et = tava.EtKalinligiMm.ToString("0.0", CultureInfo.InvariantCulture);
        var olcu = adet == 1
            ? $"{genislik}x{yukseklik}x{et}"
            : $"{adet}x{genislik}x{yukseklik}x{et}";
        return tava.AgirHizmet ? $"{tava.Ad}-{olcu} Ağır Hizmet" : $"{tava.Ad}-{olcu}";
    }

    // ─── DXF BLOCK oluşturma ──────────────────────────────────────────────

    /// SembolCizgiler listesinden bir DXF Block oluşturur. boyut: blok genişliği (metre).
    private static Block OlusturBlok(BpcArmatürTanim tanim, double boyut = 0.30)
    {
        var block = new Block(tanim.BlokAdi);
        var segs = tanim.SembolCizgiler;

        if (segs.Count == 0)
        {
            block.Entities.Add(new Circle(Vector3.Zero, boyut * 0.5));
            return block;
        }

        // Bounding box → hedef boyuta ölçekle; merkezi orijine sabitle (0,0 = görsel merkez)
        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxY = double.MinValue;
        foreach (var s in segs)
        {
            minX = Math.Min(minX, Math.Min(s.Ax, s.Bx));
            maxX = Math.Max(maxX, Math.Max(s.Ax, s.Bx));
            minY = Math.Min(minY, Math.Min(s.Ay, s.By));
            maxY = Math.Max(maxY, Math.Max(s.Ay, s.By));
        }
        double symMax = Math.Max(maxX - minX, maxY - minY);
        double scale = symMax > 1e-6 ? boyut / symMax : boyut;
        double ocx = (minX + maxX) * 0.5;
        double ocy = (minY + maxY) * 0.5;

        foreach (var s in segs)
        {
            block.Entities.Add(new Line(
                new Vector3((s.Ax - ocx) * scale, (s.Ay - ocy) * scale, 0),
                new Vector3((s.Bx - ocx) * scale, (s.By - ocy) * scale, 0)));
        }

        return block;
    }

    // ─── Yardımcılar ─────────────────────────────────────────────────────

    private static string LabelFor(BasitProjeEleman el)
    {
        if (el.OzelAlanTuru == "Adet" && el.CustomAdet is > 1)
            return $"{BaseLabelFor(el)} {el.CustomAdet.Value} adet";
        if (el.OzelAlanTuru == "KombinePrizKutusu" && (el.MonofazeAdet is not null || el.TrifazeAdet is not null))
            return $"{BaseLabelFor(el)} {el.MonofazeAdet ?? 0}M/{el.TrifazeAdet ?? 0}T";
        if (el.OzelAlanTuru == "Megapiksel" && el.Megapiksel is > 0)
            return $"{BaseLabelFor(el)} {el.Megapiksel:0.#}MP";
        return BaseLabelFor(el);
    }

    private static string BaseLabelFor(BasitProjeEleman el)
    {
        if (!string.IsNullOrWhiteSpace(el.EkranAdi)) return el.EkranAdi;
        return el.Tip switch
        {
            BpcElemanTipi.Anahtar       => "A",
            BpcElemanTipi.Komütatör     => "K",
            BpcElemanTipi.UcluKomutator => "3K",
            BpcElemanTipi.DarbeAnahtari => "D",
            BpcElemanTipi.Vavien        => "V",
            BpcElemanTipi.KatPanosu     => "KP",
            BpcElemanTipi.Sensör        => "S",
            BpcElemanTipi.Buat          => "B",
            BpcElemanTipi.KlimaIcUnite  => "KİÜ",
            BpcElemanTipi.KlimaDisUnite => "KDÜ",
            _                           => string.IsNullOrWhiteSpace(el.Ad) ? "L" : el.Ad
        };
    }

    private static Layer ElemanLayer(
        BasitProjeEleman el,
        Layer lightLayer,
        Layer switchLayer,
        Layer panelLayer,
        Layer sensorLayer,
        Layer buatLayer,
        Layer mechLayer,
        Layer dataLayer,
        Layer tvLayer,
        Layer telefonLayer,
        Layer cctvLayer,
        Layer anonsLayer,
        Layer diafonLayer,
        Layer yanginLayer)
        => el.Tip switch
        {
            BpcElemanTipi.Armatür => lightLayer,
            BpcElemanTipi.KatPanosu => panelLayer,
            BpcElemanTipi.Sensör => sensorLayer,
            BpcElemanTipi.Buat => buatLayer,
            BpcElemanTipi.KlimaIcUnite or BpcElemanTipi.KlimaDisUnite => mechLayer,
            BpcElemanTipi.DataPriz or BpcElemanTipi.DataRack or BpcElemanTipi.KatDagitimKutusu => dataLayer,
            BpcElemanTipi.TvPriz or BpcElemanTipi.TvMerkezi => tvLayer,
            BpcElemanTipi.TelefonPriz or BpcElemanTipi.TelefonDagitim => telefonLayer,
            BpcElemanTipi.Kamera or BpcElemanTipi.CctvKayit or BpcElemanTipi.AgAnahtari => cctvLayer,
            BpcElemanTipi.Hoparlor or BpcElemanTipi.AnonsAmfi => anonsLayer,
            BpcElemanTipi.Diafon or BpcElemanTipi.KapiZili or BpcElemanTipi.DiafonSantrali => diafonLayer,
            BpcElemanTipi.DumanDedektor or BpcElemanTipi.IsiDedektor
                or BpcElemanTipi.KombineDedektor or BpcElemanTipi.AsmaTavanDedektorGrubu
                or BpcElemanTipi.YanginButonu or BpcElemanTipi.Siren
                or BpcElemanTipi.Flasor or BpcElemanTipi.YanginSantrali => yanginLayer,
            _ => switchLayer
        };

    private static Layer KabloLayer(
        BpcKablo kablo,
        IReadOnlyDictionary<string, BpcBuatPanoBaglanti> buatPanoById,
        Layer kabloGuc,
        Layer kabloGuc2,
        Layer kabloSeyahat,
        Layer kabloKontrol,
        Layer kabloPano,
        Layer kabloPanoPriz,
        Layer kabloMekanik,
        Layer kabloData,
        Layer kabloTv,
        Layer kabloTelefon,
        Layer kabloCctv,
        Layer kabloAnons,
        Layer kabloDiafon,
        Layer kabloYangin)
        => kablo.Tip switch
        {
            BpcKabloTipi.GucKablosu2 => kabloGuc2,
            BpcKabloTipi.SeyahatKablosu => kabloSeyahat,
            BpcKabloTipi.KontrolKablosu => kabloKontrol,
            BpcKabloTipi.PanoKablosu => buatPanoById.TryGetValue(kablo.DevreId, out var b) && b.LinyePrefix == "P"
                ? kabloPanoPriz
                : kabloPano,
            BpcKabloTipi.MekanikKablosu => kabloMekanik,
            BpcKabloTipi.ZayifAkimKablosu => kablo.SistemAnahtar switch
            {
                BpcPaftaTipi.Data => kabloData,
                BpcPaftaTipi.Tv => kabloTv,
                BpcPaftaTipi.Telefon => kabloTelefon,
                BpcPaftaTipi.Cctv => kabloCctv,
                BpcPaftaTipi.Anons => kabloAnons,
                BpcPaftaTipi.Diafon => kabloDiafon,
                BpcPaftaTipi.Yangin => kabloYangin,
                _ => kabloData
            },
            _ => kabloGuc
        };

    private static Vector3 ToV3(Point2D p) => new(p.X, p.Y, 0);

    // Duvar montajlı bir sembol duvar dibindeyse, tabanı duvara paralel olacak ve üst yönü
    // oda içine bakacak şekilde döndürme açısını (derece, CCW) verir. Tavan/zemin montajlı
    // veya duvardan uzak elemanlar döndürülmez (0°).
    private const double DuvarSnapEsigiM = 1.0;
    private static double DuvarSnapRotasyonDerece(BasitProjeEleman el, BasitProjeKat kat)
    {
        if (!string.Equals(el.MontajTipi, "Duvar", StringComparison.OrdinalIgnoreCase))
            return 0;

        // Elemanın mahali: önce OdaId, yoksa konumu içeren oda.
        var oda = kat.Odalar.FirstOrDefault(o => o.Id == el.OdaId && o.Poligon.Count >= 3)
                  ?? kat.Odalar.FirstOrDefault(o => o.Poligon.Count >= 3 &&
                         PoligonYardimci.IsPointInside(new Point2D(el.X, el.Y), o.Poligon));
        if (oda == null || oda.Poligon.Count < 3) return 0;

        var (_, _, _, _, nx, ny, mesafe) = YerlesimGeometri.EnYakinDuvar(oda.Poligon, el.X, el.Y);
        if (mesafe > DuvarSnapEsigiM) return 0; // duvar dibinde değil → döndürme

        // Sembollerin varsayılan oryantasyonu: taban -Y'de (duvar çizgisi), üst yön +Y (içeri).
        // Sembolün +Y ekseni iç normale (nx,ny) denk gelmeli → θ = atan2(-nx, ny).
        return Math.Atan2(-nx, ny) * 180.0 / Math.PI;
    }

    /// Noktayı en yakın polygon kenarına projekte eder (duvar snap).
    public static Point2D SnapToNearestWall(Point2D p, IEnumerable<BasitProjeOda> odalar)
    {
        Point2D best = p;
        double minD = double.MaxValue;
        foreach (var oda in odalar)
        {
            var poly = oda.Poligon;
            for (int i = 0; i < poly.Count; i++)
            {
                var proj = PoligonYardimci.SegmentProje(p, poly[i], poly[(i + 1) % poly.Count]);
                double d = SnapDist(p, proj);
                if (d < minD) { minD = d; best = proj; }
            }
        }
        return best;
    }

    private static double SnapDist(Point2D a, Point2D b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

}
