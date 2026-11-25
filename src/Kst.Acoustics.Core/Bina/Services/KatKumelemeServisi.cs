using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Bina.Services;

/// Tek DXF içinde yan yana çizilmiş birden fazla kat planını (model space'te
/// mekânsal olarak ayrık kümeler) tespit eder — codex50 §9.
public static class KatKumelemeServisi
{
    public sealed class Ayarlar
    {
        /// Doluluk ızgarası hücre boyu (m). Küçüldükçe hassasiyet artar, performans düşer.
        public double HucreBoyuM = 1.0;

        /// Kümeler arası bu mesafeden (m) daha yakın hücreler TEK küme sayılır
        /// (dilate-then-flood ile). Planlar arası tipik boşluktan büyük, bina içi
        /// boşluklardan (koridor/bahçe) küçük olmalı.
        public double KumeBoslukEsigiM = 5.0;

        /// Bir kümenin "plan" sayılması için minimum bbox alanı (m²). Altındaki
        /// kümeler antet/lejant/görünüş gibi plan-dışı öğe sayılır.
        public double MinPlanAlaniM2 = 20.0;

        /// Planın altında/üstünde yazılan başlığın çizim sınırına en fazla uzaklığı.
        /// Eşit uzaklıktaki iki çizime başlık atanmaz; kullanıcı kontrolünde kalır.
        public double DisBaslikMesafesiM = 8.0;

        /// Başlık içeren, yan yana temas eden büyük pafta çerçevelerini ayır.
        public bool PaftaCerceveleriniAyir = true;

        public long MaxHucre = 4_000_000;
    }

    /// Tespit edilen bir mekânsal küme: bbox + kümeye ait ham segment/metin/arc alt kümeleri.
    public sealed record KatKumesi(
        double MinX, double MinY, double MaxX, double MaxY,
        List<(Point2D A, Point2D B)> Segmentler,
        List<(Point2D Pos, string Value)> Metinler,
        List<(Point2D Merkez, double R, double BasDeg, double BitDeg)> Arclar)
    {
        public double GenislikM => MaxX - MinX;
        public double DerinlikM => MaxY - MinY;
        public double BboxAlanM2 => Math.Max(0, GenislikM) * Math.Max(0, DerinlikM);
        public Point2D Merkez => new((MinX + MaxX) / 2.0, (MinY + MaxY) / 2.0);
    }

    /// Ham segmentleri mekânsal kümelere ayırır; metin/arc'lar bbox içine düşenlere
    /// dağıtılır (yalnız bilgilendirme amaçlı — kümeleme yalnız segmentlere göre yapılır).
    /// Tek küme bulunursa (veya segment yoksa) tek elemanlı/boş liste döner.
    public static List<KatKumesi> Kumele(
        IReadOnlyList<(Point2D A, Point2D B)> segmentler,
        IReadOnlyList<(Point2D Pos, string Value)> metinler,
        IReadOnlyList<(Point2D Merkez, double R, double BasDeg, double BitDeg)> arclar,
        Ayarlar? ayar = null)
    {
        ayar ??= new Ayarlar();
        if (segmentler.Count == 0) return new List<KatKumesi>();

        if (ayar.PaftaCerceveleriniAyir && PaftaCercevelerindenAyir(segmentler, metinler, arclar, ayar) is { } paftalar)
            return paftalar;

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var (a, b) in segmentler)
        {
            minX = Math.Min(minX, Math.Min(a.X, b.X));
            minY = Math.Min(minY, Math.Min(a.Y, b.Y));
            maxX = Math.Max(maxX, Math.Max(a.X, b.X));
            maxY = Math.Max(maxY, Math.Max(a.Y, b.Y));
        }
        if (maxX <= minX || maxY <= minY) return new List<KatKumesi>();

        double cs = ayar.HucreBoyuM;
        double margin = ayar.KumeBoslukEsigiM + 3 * cs;
        minX -= margin; minY -= margin; maxX += margin; maxY += margin;

        double spanX = maxX - minX, spanY = maxY - minY;
        double wD = Math.Ceiling(spanX / cs) + 1;
        double hD = Math.Ceiling(spanY / cs) + 1;

        if (wD * hD > ayar.MaxHucre)
        {
            double k = Math.Sqrt(wD * hD / ayar.MaxHucre);
            cs *= k;
            wD = Math.Ceiling(spanX / cs) + 1;
            hD = Math.Ceiling(spanY / cs) + 1;
        }
        if (wD < 2 || hD < 2 || wD * hD > ayar.MaxHucre * 2 || wD > int.MaxValue || hD > int.MaxValue)
            return new List<KatKumesi> { TekKumeFallback(segmentler, metinler, arclar) };

        int w = (int)wD, h = (int)hD;
        int Idx(int x, int y) => y * w + x;
        int CX(double x) => Math.Clamp((int)((x - minX) / cs), 0, w - 1);
        int CY(double y) => Math.Clamp((int)((y - minY) / cs), 0, h - 1);

        var dolu = new bool[w * h];
        foreach (var (a, b) in segmentler)
            CizgiCiz(dolu, w, h, CX(a.X), CY(a.Y), CX(b.X), CY(b.Y));

        // Boşluk eşiği kadar genişlet (dilate) — bu mesafedeki hücreler birleşir.
        int R = Math.Max(1, (int)Math.Round(ayar.KumeBoslukEsigiM / cs, MidpointRounding.AwayFromZero));
        var genisletilmis = Dilate(dolu, w, h, R);

        // Bağlı bileşen etiketleme (8-komşuluk) genişletilmiş ızgarada.
        var label = new int[w * h];
        int kumeNo = 0;
        var q = new Queue<int>();
        for (int i = 0; i < genisletilmis.Length; i++)
        {
            if (!genisletilmis[i] || label[i] != 0) continue;
            kumeNo++;
            label[i] = kumeNo;
            q.Enqueue(i);
            while (q.Count > 0)
            {
                int c = q.Dequeue();
                int x = c % w, y = c / w;
                Komsu8(x, y, w, h, ni =>
                {
                    if (genisletilmis[ni] && label[ni] == 0) { label[ni] = kumeNo; q.Enqueue(ni); }
                });
            }
        }
        if (kumeNo <= 1)
            return new List<KatKumesi> { TekKumeFallback(segmentler, metinler, arclar) };

        // Orijinal (genişletilmemiş) hücrelerin etiketini kullanarak segmentleri kümelere dağıt.
        int LabelOf(Point2D p) => label[Idx(CX(p.X), CY(p.Y))];

        var segByKume = new Dictionary<int, List<(Point2D, Point2D)>>();
        foreach (var s in segmentler)
        {
            int k = LabelOf(s.A);
            if (k == 0) k = LabelOf(s.B);
            if (k == 0) continue;
            if (!segByKume.TryGetValue(k, out var list)) segByKume[k] = list = new List<(Point2D, Point2D)>();
            list.Add(s);
        }

        var sonuc = new List<KatKumesi>();
        foreach (var (k, segs) in segByKume)
        {
            double kMinX = double.MaxValue, kMinY = double.MaxValue;
            double kMaxX = double.MinValue, kMaxY = double.MinValue;
            foreach (var (a, b) in segs)
            {
                kMinX = Math.Min(kMinX, Math.Min(a.X, b.X));
                kMinY = Math.Min(kMinY, Math.Min(a.Y, b.Y));
                kMaxX = Math.Max(kMaxX, Math.Max(a.X, b.X));
                kMaxY = Math.Max(kMaxY, Math.Max(a.Y, b.Y));
            }

            var arclarK = arclar.Where(ar => IcindeMi(ar.Merkez, kMinX, kMinY, kMaxX, kMaxY)).ToList();

            sonuc.Add(new KatKumesi(kMinX, kMinY, kMaxX, kMaxY, segs, [], arclarK));
        }

        // Başlıklar sıklıkla çizimin bbox'ı dışındadır. Bütün metinleri birden fazla
        // kümeye kopyalamak yerine, iç metinleri içerildikleri tek kümeye; dış
        // başlıkları yalnız belirgin biçimde en yakın kümeye ata.
        foreach (var metin in metinler)
        {
            var icindekiler = sonuc.Where(k => IcindeMi(metin.Pos, k.MinX, k.MinY, k.MaxX, k.MaxY)).ToList();
            if (icindekiler.Count == 1)
            {
                icindekiler[0].Metinler.Add(metin);
                continue;
            }
            if (icindekiler.Count > 1 || !KatEtiketServisi.CizimBasligiMi(metin.Value)) continue;
            var yakinlar = sonuc.Select(k => (Kume: k, Mesafe: KutuMesafesi(metin.Pos, k)))
                .Where(x => x.Mesafe <= ayar.DisBaslikMesafesiM)
                .OrderBy(x => x.Mesafe).ToList();
            if (yakinlar.Count == 0) continue;
            if (yakinlar.Count > 1 && yakinlar[1].Mesafe - yakinlar[0].Mesafe < Math.Max(0.5, yakinlar[0].Mesafe * 0.2)) continue;
            yakinlar[0].Kume.Metinler.Add(metin);
        }

        return sonuc.OrderBy(k => k.MinY).ThenBy(k => k.MinX).ToList();
    }

    /// Küçük kümeleri ve açık kesit/görünüş/vaziyet başlığı taşıyan çizimleri ayıklar.
    /// Etiketsiz büyük kümeler eski davranışla aday kalır; bunlar kesin kat sayılmaz.
    public static (List<KatKumesi> Planlar, List<KatKumesi> PlanDegil) PlanFiltrele(
        IReadOnlyList<KatKumesi> kumeler, Ayarlar? ayar = null)
    {
        ayar ??= new Ayarlar();
        var planlar = new List<KatKumesi>();
        var planDegil = new List<KatKumesi>();
        // Birden fazla çizimde ölçekli başlık varsa, bunlar paftanın tutarlı
        // başlık düzenidir. Yalnız antette kalan ölçek içermeyen kopya isimler
        // (ör. her paftada "Zemin Kat Planı") bu belgede kat kanıtı değildir.
        bool olcekliBaslikDuzeni = kumeler.Count(k => k.Metinler.Any(m => KatEtiketServisi.OlcekliBaslikMi(m.Value))) >= 2;
        foreach (var k in kumeler)
        {
            bool planDisiBaslik = k.Metinler.Any(m => KatEtiketServisi.PlanDisiBasligiMi(m.Value));
            bool planKaniti = !olcekliBaslikDuzeni || k.Metinler.Any(m =>
                KatEtiketServisi.AcikPlanBasligiMi(m.Value) && KatEtiketServisi.OlcekliBaslikMi(m.Value));
            if (k.BboxAlanM2 >= ayar.MinPlanAlaniM2 && !planDisiBaslik && planKaniti) planlar.Add(k);
            else planDegil.Add(k);
        }
        return (planlar, planDegil);
    }

    private static KatKumesi TekKumeFallback(
        IReadOnlyList<(Point2D A, Point2D B)> segmentler,
        IReadOnlyList<(Point2D Pos, string Value)> metinler,
        IReadOnlyList<(Point2D Merkez, double R, double BasDeg, double BitDeg)> arclar)
    {
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var (a, b) in segmentler)
        {
            minX = Math.Min(minX, Math.Min(a.X, b.X));
            minY = Math.Min(minY, Math.Min(a.Y, b.Y));
            maxX = Math.Max(maxX, Math.Max(a.X, b.X));
            maxY = Math.Max(maxY, Math.Max(a.Y, b.Y));
        }
        return new KatKumesi(minX, minY, maxX, maxY, segmentler.ToList(), metinler.ToList(), arclar.ToList());
    }

    private static bool IcindeMi(Point2D p, double minX, double minY, double maxX, double maxY)
        => p.X >= minX && p.X <= maxX && p.Y >= minY && p.Y <= maxY;

    private static double KutuMesafesi(Point2D p, KatKumesi k)
    {
        double dx = Math.Max(0, Math.Max(k.MinX - p.X, p.X - k.MaxX));
        double dy = Math.Max(0, Math.Max(k.MinY - p.Y, p.Y - k.MaxY));
        return Math.Sqrt(dx * dx + dy * dy);
    }

    // Çerçeveler duvar katmanında da çizilebilir. Yalnız boşluk kümelemesi bu
    // durumda bütün paftayı tek bina sayar. Üst kenarı ve iki düşey yanı bağlı,
    // içinde çizim başlığı bulunan büyük çerçeveleri önce ayrı değerlendirmek
    // bu birleşmeyi önler. Küçük oda dikdörtgenleri bu kurala girmez.
    private static List<KatKumesi>? PaftaCercevelerindenAyir(
        IReadOnlyList<(Point2D A, Point2D B)> segmentler,
        IReadOnlyList<(Point2D Pos, string Value)> metinler,
        IReadOnlyList<(Point2D Merkez, double R, double BasDeg, double BitDeg)> arclar,
        Ayarlar ayar)
    {
        const double tol = 0.08;
        var basliklar = metinler.Where(t => KatEtiketServisi.CizimBasligiMi(t.Value)).ToList();
        if (basliklar.Count < 2) return null;
        var dusey = segmentler.Where(s => Math.Abs(s.A.X - s.B.X) < tol && Math.Abs(s.A.Y - s.B.Y) >= 10)
            .Select(s => (X: (s.A.X + s.B.X) / 2, Alt: Math.Min(s.A.Y, s.B.Y), Ust: Math.Max(s.A.Y, s.B.Y))).ToList();
        var cerceveler = new List<(double MinX, double MinY, double MaxX, double MaxY)>();
        foreach (var s in segmentler.Where(s => Math.Abs(s.A.Y - s.B.Y) < tol && Math.Abs(s.A.X - s.B.X) >= 10))
        {
            double x0 = Math.Min(s.A.X, s.B.X), x1 = Math.Max(s.A.X, s.B.X), y = (s.A.Y + s.B.Y) / 2;
            var sol = dusey.Where(v => Math.Abs(v.X - x0) <= tol && Math.Abs(v.Ust - y) <= tol).ToList();
            var sag = dusey.Where(v => Math.Abs(v.X - x1) <= tol && Math.Abs(v.Ust - y) <= tol).ToList();
            if (sol.Count == 0 || sag.Count == 0) continue;
            double alt = Math.Min(sol.Min(v => v.Alt), sag.Min(v => v.Alt));
            if (!basliklar.Any(t => IcindeMi(t.Pos, x0, alt, x1, y) && t.Pos.Y < (alt + y) / 2)) continue;
            if (cerceveler.Any(c => Math.Abs(c.MinX - x0) < 0.2 && Math.Abs(c.MaxX - x1) < 0.2 && Math.Abs(c.MinY - alt) < 0.2 && Math.Abs(c.MaxY - y) < 0.2)) continue;
            cerceveler.Add((x0, alt, x1, y));
        }
        if (cerceveler.Count < 2) return null;
        // Üst üste/çakışan çerçeve adaylarından yorum yapma; yalnız bağımsız
        // paftalar otomatik ayrılabilir (ortak sınır çizgisi kabul edilir).
        var bagimsiz = cerceveler.Where(c => !cerceveler.Any(d => d != c &&
            Math.Min(c.MaxX, d.MaxX) - Math.Max(c.MinX, d.MinX) > 0.2 &&
            Math.Min(c.MaxY, d.MaxY) - Math.Max(c.MinY, d.MinY) > 0.2)).ToList();
        if (bagimsiz.Count < 2) return null;

        var sonuc = new List<KatKumesi>();
        var kullanilan = new HashSet<int>();
        foreach (var c in bagimsiz)
        {
            bool Icinde(Point2D p) => IcindeMi(p, c.MinX - tol, c.MinY - tol, c.MaxX + tol, c.MaxY + tol);
            bool CerceveKenari((Point2D A, Point2D B) s) =>
                Math.Abs(s.A.X - c.MinX) <= tol && Math.Abs(s.B.X - c.MinX) <= tol ||
                Math.Abs(s.A.X - c.MaxX) <= tol && Math.Abs(s.B.X - c.MaxX) <= tol ||
                Math.Abs(s.A.Y - c.MinY) <= tol && Math.Abs(s.B.Y - c.MinY) <= tol ||
                Math.Abs(s.A.Y - c.MaxY) <= tol && Math.Abs(s.B.Y - c.MaxY) <= tol;
            var segs = new List<(Point2D A, Point2D B)>();
            for (int i = 0; i < segmentler.Count; i++)
            {
                var s = segmentler[i];
                if (!Icinde(s.A) || !Icinde(s.B)) continue;
                kullanilan.Add(i);
                if (!CerceveKenari(s)) segs.Add(s);
            }
            if (segs.Count < 4) continue;
            sonuc.Add(new KatKumesi(c.MinX, c.MinY, c.MaxX, c.MaxY, segs,
                metinler.Where(t => Icinde(t.Pos)).ToList(), arclar.Where(a => Icinde(a.Merkez)).ToList()));
        }
        if (sonuc.Count < 2) return null;
        // Çerçeve dışında kalan bağımsız çizimleri kaybetme; normal mekânsal
        // kümelemeyle geri ekle. Tekrar çerçeve araması yapılmaz.
        bool Paftada(Point2D p) => bagimsiz.Any(c => IcindeMi(p, c.MinX - tol, c.MinY - tol, c.MaxX + tol, c.MaxY + tol));
        var kalan = segmentler.Where((_, i) => !kullanilan.Contains(i)).ToList();
        if (kalan.Count > 0)
            sonuc.AddRange(Kumele(kalan, metinler.Where(t => !Paftada(t.Pos)).ToList(), arclar.Where(a => !Paftada(a.Merkez)).ToList(),
                new Ayarlar { HucreBoyuM = ayar.HucreBoyuM, KumeBoslukEsigiM = ayar.KumeBoslukEsigiM, MinPlanAlaniM2 = ayar.MinPlanAlaniM2,
                    MaxHucre = ayar.MaxHucre, DisBaslikMesafesiM = ayar.DisBaslikMesafesiM, PaftaCerceveleriniAyir = false }));
        return sonuc.OrderBy(k => k.MinY).ThenBy(k => k.MinX).ToList();
    }

    private static void CizgiCiz(bool[] grid, int w, int h, int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        while (true)
        {
            if (x0 >= 0 && x0 < w && y0 >= 0 && y0 < h) grid[y0 * w + x0] = true;
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    private static bool[] Dilate(bool[] dolu, int w, int h, int R)
    {
        var dist = new int[w * h];
        Array.Fill(dist, int.MaxValue);
        var q = new Queue<int>();
        for (int i = 0; i < dolu.Length; i++)
            if (dolu[i]) { dist[i] = 0; q.Enqueue(i); }

        var genis = (bool[])dolu.Clone();
        while (q.Count > 0)
        {
            int c = q.Dequeue();
            int d = dist[c];
            if (d >= R) continue;
            int x = c % w, y = c / w;
            Komsu8(x, y, w, h, ni =>
            {
                if (dist[ni] > d + 1) { dist[ni] = d + 1; genis[ni] = true; q.Enqueue(ni); }
            });
        }
        return genis;
    }

    private static void Komsu8(int x, int y, int w, int h, Action<int> f)
    {
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            if (dx == 0 && dy == 0) continue;
            int nx = x + dx, ny = y + dy;
            if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
            f(nx + ny * w);
        }
    }
}
