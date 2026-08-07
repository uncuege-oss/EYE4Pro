using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using netDxf;
using netDxf.Blocks;
using netDxf.Entities;
using netDxf.Tables;

namespace Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

/// <summary>
/// Sembol kütüphanesi DXF import/export servisi.
/// Şablon dışa aktarımı: tüm sembolleri DXF BLOCK olarak içeren bir katalog dosyası üretir;
/// kullanıcı AutoCAD'de bunları yeniden çizip kaydeder.
/// İçe aktarım: DXF'teki block tanımlarını okuyup, BlokAdi eşleşen kütüphane girdilerinin
/// SembolCizgiler'ini günceller. ±1 birim kutuya otomatik normalize edilir.
/// </summary>
public class BasitProjeCizSembolDxfServisi
{
    public sealed class ImportSonuc
    {
        public int Guncellendi { get; set; }
        /// DXF'te bulunan ama kütüphane block adlarından hiçbiriyle eşleşmeyen block'lar.
        public List<string> Eslesemedi { get; set; } = new();
        /// Kütüphanenin beklediği ama DXF'te hiç bulunmayan block adları
        /// (kullanıcı şablondan silmiş veya farklı adla kaydetmiş).
        public List<string> BeklenenAmaBulunamayan { get; set; } = new();
        /// DXF'te bulunan tüm desteklenen entity üreten block'lar — teşhis için.
        /// (Block adı, entity sayısı, çıkarılan segment sayısı).
        public List<(string BlokAdi, int EntityCount, int SegmentCount)> OkunanBloklar { get; set; } = new();
        /// DXF'te bulunan ama desteklenen entity üretemediği için atlanan block'lar.
        public List<string> BosBloklar { get; set; } = new();
        public List<string> Hatalar { get; set; } = new();

        public override string ToString()
        {
            var parcalar = new List<string> { $"{Guncellendi} sembol güncellendi" };
            if (Eslesemedi.Count > 0)
                parcalar.Add($"Eşleşmeyen ({Eslesemedi.Count}): {Kisalt(Eslesemedi)}");
            if (BeklenenAmaBulunamayan.Count > 0)
                parcalar.Add($"DXF'te yok ({BeklenenAmaBulunamayan.Count}): {Kisalt(BeklenenAmaBulunamayan)}");
            if (BosBloklar.Count > 0)
                parcalar.Add($"Boş ({BosBloklar.Count}): {Kisalt(BosBloklar)}");
            if (Hatalar.Count > 0)
                parcalar.Add($"Hata: {string.Join("; ", Hatalar)}");
            return string.Join(" | ", parcalar) + ".";
        }

        private static string Kisalt(List<string> liste, int max = 6)
        {
            if (liste.Count <= max) return string.Join(", ", liste);
            return string.Join(", ", liste.Take(max)) + $", … +{liste.Count - max}";
        }
    }

    // ── Şablon dışa aktarımı ─────────────────────────────────────────────────

    /// <summary>
    /// Kütüphanedeki tüm sembolleri (+ zayıf akım sabitleri) DXF BLOCK tanımı olarak
    /// dosyaya yazar. Aynı zamanda görsel katalog için her sembolden bir INSERT
    /// örneği + altta isim etiketi yerleştirir.
    /// </summary>
    public void SablonDisariAktar(BasitProjeCizKutuphaneServisi library, string outputPath)
    {
        var dxf = new DxfDocument();
        var refLayer = new Layer("SEMBOLLER") { Color = AciColor.Yellow };

        // Tek kayıt: BlokAdi, görünen ad, sembol çizgileri
        var tumKatalog = new List<(string BlokAdi, string Ad, List<BpcSembolCizgi> Cizgiler)>();

        void Ekle(IEnumerable<BpcArmatürTanim> liste)
        {
            foreach (var t in liste)
            {
                if (string.IsNullOrWhiteSpace(t.BlokAdi)) continue;
                if (tumKatalog.Any(x => string.Equals(x.BlokAdi, t.BlokAdi, StringComparison.OrdinalIgnoreCase)))
                    continue;
                tumKatalog.Add((t.BlokAdi, t.Ad, t.SembolCizgiler));
            }
        }

        Ekle(library.Armatürler);
        Ekle(library.Anahtarlar);
        Ekle(library.Panolar);
        Ekle(library.Prizler);
        Ekle(library.Klimalar);

        // Zayıf akım sabitleri (E4P_DUMAN_DED, E4P_KAMERA, E4P_HOPARLOR, vb.)
        foreach (BpcElemanTipi tip in Enum.GetValues(typeof(BpcElemanTipi)))
        {
            var zt = BasitProjeCizKutuphaneServisi.ZayifAkimTanim(tip);
            if (zt == null || string.IsNullOrWhiteSpace(zt.BlokAdi)) continue;
            if (tumKatalog.Any(x => string.Equals(x.BlokAdi, zt.BlokAdi, StringComparison.OrdinalIgnoreCase)))
                continue;
            tumKatalog.Add((zt.BlokAdi, zt.Ad, zt.SembolCizgiler));
        }

        // BLOCK tanımları + katalog yerleşimi
        const double hucreBoyu = 3.0;        // hücre = sembol bölgesi + etiket alanı
        const double sembolYari = 1.0;       // sembol ±1 kutuda
        const int sutun = 6;                 // grid sütun sayısı

        for (int i = 0; i < tumKatalog.Count; i++)
        {
            var (blokAdi, ad, segs) = tumKatalog[i];

            var block = new Block(blokAdi);
            if (segs.Count == 0)
            {
                // Boş — küçük yer tutucu daire
                block.Entities.Add(new Circle(Vector3.Zero, 0.3));
            }
            else
            {
                foreach (var s in segs)
                {
                    block.Entities.Add(new Line(
                        new Vector3(s.Ax, s.Ay, 0),
                        new Vector3(s.Bx, s.By, 0)) { Layer = refLayer });
                }
            }
            dxf.Blocks.Add(block);

            // Katalog yerleşimi
            int gx = i % sutun;
            int gy = i / sutun;
            double cx = gx * hucreBoyu;
            double cy = -gy * hucreBoyu;

            dxf.Entities.Add(new Insert(block, new Vector3(cx, cy, 0)) { Layer = refLayer });
            // Hücre çerçevesi (görsel ayraç)
            double half = sembolYari + 0.3;
            dxf.Entities.Add(new Line(new Vector3(cx - half, cy - half, 0), new Vector3(cx + half, cy - half, 0)) { Layer = refLayer, Color = AciColor.DarkGray });
            dxf.Entities.Add(new Line(new Vector3(cx + half, cy - half, 0), new Vector3(cx + half, cy + half, 0)) { Layer = refLayer, Color = AciColor.DarkGray });
            dxf.Entities.Add(new Line(new Vector3(cx + half, cy + half, 0), new Vector3(cx - half, cy + half, 0)) { Layer = refLayer, Color = AciColor.DarkGray });
            dxf.Entities.Add(new Line(new Vector3(cx - half, cy + half, 0), new Vector3(cx - half, cy - half, 0)) { Layer = refLayer, Color = AciColor.DarkGray });

            // İsim etiketleri (block adı + okunabilir ad)
            dxf.Entities.Add(new Text(blokAdi, new Vector3(cx - half, cy - half - 0.30, 0), 0.18)
                { Layer = refLayer, Color = AciColor.Cyan });
            dxf.Entities.Add(new Text(ad, new Vector3(cx - half, cy - half - 0.60, 0), 0.16)
                { Layer = refLayer, Color = AciColor.Default });
        }

        // Başlık
        dxf.Entities.Add(new Text(
            "EYE4PRO M12 — Sembol Şablonu (DXF Block'larını düzenleyip kaydet, sonra uygulamadan içe aktar)",
            new Vector3(0, 1.5, 0), 0.30) { Layer = refLayer, Color = AciColor.Yellow });

        dxf.Save(outputPath);
    }

    // ── İçe aktarım ──────────────────────────────────────────────────────────

    /// <summary>
    /// DXF'teki block tanımlarını okuyup, BlokAdi eşleşen kütüphane girdilerini
    /// (Armatürler/Anahtarlar/Panolar/Prizler/Klimalar + ZayifAkimSemboller override)
    /// günceller. Eşleşmeyen block adları sonuçta listelenir.
    /// </summary>
    public ImportSonuc SembolleriYukle(string dxfPath, BasitProjeCizKutuphaneServisi library)
    {
        var sonuc = new ImportSonuc();
        DxfDocument? dxf;
        try { dxf = DxfDocument.Load(dxfPath); }
        catch (Exception ex)
        {
            sonuc.Hatalar.Add($"DXF yüklenemedi: {ex.Message}");
            return sonuc;
        }
        if (dxf == null) { sonuc.Hatalar.Add("DXF okunamadı."); return sonuc; }
        return SembolleriUygula(dxf, library);
    }

    /// <summary>
    /// <see cref="SembolleriYukle(string, BasitProjeCizKutuphaneServisi)"/> ile aynı; ama DXF'i
    /// bir akıştan okur (gömülü kaynak / single-file yayın senaryosu için).
    /// </summary>
    public ImportSonuc SembolleriYukle(System.IO.Stream dxfStream, BasitProjeCizKutuphaneServisi library)
    {
        var sonuc = new ImportSonuc();
        DxfDocument? dxf;
        try { dxf = DxfDocument.Load(dxfStream); }
        catch (Exception ex)
        {
            sonuc.Hatalar.Add($"DXF akışı yüklenemedi: {ex.Message}");
            return sonuc;
        }
        if (dxf == null) { sonuc.Hatalar.Add("DXF akışı okunamadı."); return sonuc; }
        return SembolleriUygula(dxf, library);
    }

    private ImportSonuc SembolleriUygula(DxfDocument dxf, BasitProjeCizKutuphaneServisi library)
    {
        var sonuc = new ImportSonuc();

        // BlokAdi → SembolCizgiler haritası (case-insensitive)
        var yeniHarita = new Dictionary<string, List<BpcSembolCizgi>>(StringComparer.OrdinalIgnoreCase);

        foreach (var block in dxf.Blocks)
        {
            // Sistem block'larını atla (*Model_Space, *Paper_Space, *D1, ...)
            if (string.IsNullOrWhiteSpace(block.Name) || block.Name.StartsWith("*")) continue;

            int entityCount = block.Entities.Count();

            var segs = new List<(double Ax, double Ay, double Bx, double By)>();
            try { TopluSegmentCikar(block.Entities, segs); }
            catch (Exception ex)
            {
                sonuc.Hatalar.Add($"[{block.Name}] entity okunamadı: {ex.Message}");
                continue;
            }
            if (segs.Count == 0)
            {
                sonuc.BosBloklar.Add(block.Name);
                continue;
            }

            // ±1 kutuya normalize et
            var normalize = Normalize(segs);
            if (normalize.Count == 0)
            {
                sonuc.BosBloklar.Add(block.Name);
                continue;
            }

            yeniHarita[block.Name] = normalize;
            sonuc.OkunanBloklar.Add((block.Name, entityCount, normalize.Count));
        }

        // Kataloğa uygula
        int gncl = 0;
        var eslesenler = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var beklenenler = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Uygula(List<BpcArmatürTanim> liste)
        {
            foreach (var t in liste)
            {
                if (string.IsNullOrWhiteSpace(t.BlokAdi)) continue;
                beklenenler.Add(t.BlokAdi);
                if (yeniHarita.TryGetValue(t.BlokAdi, out var yeniSegs))
                {
                    t.SembolCizgiler = yeniSegs;
                    gncl++;
                    eslesenler.Add(t.BlokAdi);
                }
            }
        }

        Uygula(library.Armatürler);
        Uygula(library.Anahtarlar);
        Uygula(library.Panolar);
        Uygula(library.Prizler);
        Uygula(library.Klimalar);
        // Palet ve yerleştirme global `Malzemeler` listesinden beslenir; o da güncellenmeli
        // (güçlü akım için kategori listeleriyle ref paylaşsa da, JSON'dan yüklenince ayrı
        //  nesneler olur — bu yüzden açıkça uygula). Zayıf akım malzemeleri de burada güncellenir.
        Uygula(library.Malzemeler);

        // Zayıf akım override haritası — kalan eşleşmeleri buraya yaz
        foreach (BpcElemanTipi tip in Enum.GetValues(typeof(BpcElemanTipi)))
        {
            var zt = BasitProjeCizKutuphaneServisi.ZayifAkimTanim(tip);
            if (zt == null || string.IsNullOrWhiteSpace(zt.BlokAdi)) continue;
            beklenenler.Add(zt.BlokAdi);
            if (eslesenler.Contains(zt.BlokAdi)) continue;
            if (yeniHarita.TryGetValue(zt.BlokAdi, out var yeniSegs))
            {
                library.ZayifAkimSemboller[zt.BlokAdi] = yeniSegs;
                gncl++;
                eslesenler.Add(zt.BlokAdi);
            }
        }

        // Eşleşmeyenleri raporla (DXF'te var ama kütüphane bilmiyor)
        foreach (var kv in yeniHarita)
        {
            if (!eslesenler.Contains(kv.Key)) sonuc.Eslesemedi.Add(kv.Key);
        }

        // Beklenen ama bulunamayanlar (kütüphane bekliyor ama DXF'te yok)
        foreach (var bekle in beklenenler)
        {
            if (!eslesenler.Contains(bekle))
                sonuc.BeklenenAmaBulunamayan.Add(bekle);
        }
        sonuc.BeklenenAmaBulunamayan.Sort(StringComparer.OrdinalIgnoreCase);
        sonuc.Eslesemedi.Sort(StringComparer.OrdinalIgnoreCase);
        sonuc.BosBloklar.Sort(StringComparer.OrdinalIgnoreCase);

        // Benzersiz güncellenen blok sayısı (Malzemeler listesi güçlü akımı tekrar
        // ziyaret ettiği için ham `gncl` çift sayar — eşleşen blok adı kümesini kullan).
        _ = gncl;
        sonuc.Guncellendi = eslesenler.Count;
        return sonuc;
    }

    // ── Yardımcılar ──────────────────────────────────────────────────────────

    // Block entity'lerini line segment listesine indirger (LINE, LWPOLYLINE,
    // POLYLINE2D, ARC, CIRCLE, ELLIPSE, SPLINE). TEXT/HATCH/INSERT atlanır.
    private static void TopluSegmentCikar(
        IEnumerable<EntityObject> entities,
        List<(double Ax, double Ay, double Bx, double By)> segs)
    {
        void EklePolyV2(IReadOnlyList<Vector2> pts, bool kapali)
        {
            if (pts == null || pts.Count < 2) return;
            for (int i = 0; i < pts.Count - 1; i++)
                segs.Add((pts[i].X, pts[i].Y, pts[i + 1].X, pts[i + 1].Y));
            if (kapali && pts.Count > 2)
                segs.Add((pts[^1].X, pts[^1].Y, pts[0].X, pts[0].Y));
        }
        void EklePolyV3(IReadOnlyList<Vector3> pts, bool kapali)
        {
            if (pts == null || pts.Count < 2) return;
            for (int i = 0; i < pts.Count - 1; i++)
                segs.Add((pts[i].X, pts[i].Y, pts[i + 1].X, pts[i + 1].Y));
            if (kapali && pts.Count > 2)
                segs.Add((pts[^1].X, pts[^1].Y, pts[0].X, pts[0].Y));
        }

        foreach (var ent in entities)
        {
            if (ent is Line l)
                segs.Add((l.StartPoint.X, l.StartPoint.Y, l.EndPoint.X, l.EndPoint.Y));
            else if (ent is Polyline2D p)
            {
                var v = p.Vertexes.Select(x => new Vector2(x.Position.X, x.Position.Y)).ToList();
                EklePolyV2(v, p.IsClosed);
            }
            else if (ent is Arc a)     EklePolyV2(a.PolygonalVertexes(24), false);
            else if (ent is Circle c)  EklePolyV2(c.PolygonalVertexes(24), true);
            else if (ent is Ellipse e) EklePolyV2(e.PolygonalVertexes(24), true);
            else if (ent is Spline sp) EklePolyV3(sp.PolygonalVertexes(32), false);
            else if (ent is Insert ins)
            {
                // Block içinde nested INSERT olabilir — patlatıp segment çıkar
                try { TopluSegmentCikar(ins.Explode(), segs); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[M12] INSERT patlatma atlandı: {ex.Message}"); }
            }
            // TEXT/MTEXT/HATCH/SOLID: atlanır
        }
    }

    // Bounding box → ±1 kutuya merkezleyerek ölçekler (en uzun kenar 2.0).
    private static List<BpcSembolCizgi> Normalize(
        List<(double Ax, double Ay, double Bx, double By)> segs)
    {
        if (segs.Count == 0) return new();

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var s in segs)
        {
            minX = Math.Min(minX, Math.Min(s.Ax, s.Bx));
            maxX = Math.Max(maxX, Math.Max(s.Ax, s.Bx));
            minY = Math.Min(minY, Math.Min(s.Ay, s.By));
            maxY = Math.Max(maxY, Math.Max(s.Ay, s.By));
        }
        double w = maxX - minX, h = maxY - minY;
        double cx = (minX + maxX) * 0.5, cy = (minY + maxY) * 0.5;
        double scale = Math.Max(w, h);
        if (scale <= 1e-9) return new();
        double k = 2.0 / scale;  // en uzun kenar → 2.0 (±1)

        var sonuc = new List<BpcSembolCizgi>(segs.Count);
        foreach (var s in segs)
        {
            sonuc.Add(new BpcSembolCizgi(
                (s.Ax - cx) * k, (s.Ay - cy) * k,
                (s.Bx - cx) * k, (s.By - cy) * k));
        }
        return sonuc;
    }
}
