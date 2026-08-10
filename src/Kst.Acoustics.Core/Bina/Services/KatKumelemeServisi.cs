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

        /// Plan başlığı gibi bbox'ın hemen dışında duran metinlerin en yakın
        /// kümeye atanabileceği azami mesafe (m).
        public double EtiketAtamaMesafesiM = 3.0;

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

        // Her iki küme de R kadar genişlediğinden toplam birleşme mesafesi yaklaşık
        // 2R'dir. Eşiğin tamamını iki yana uygulamak, 5 m ayarında yaklaşık 10 m
        // uzaktaki ayrı planları yanlışlıkla tek kümeye bağlıyordu.
        int R = Math.Max(0, (int)Math.Floor(ayar.KumeBoslukEsigiM / (2.0 * cs)));
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

            sonuc.Add(new KatKumesi(kMinX, kMinY, kMaxX, kMaxY, segs, new(), arclarK));
        }

        // Metinleri her kümenin yalnız tam bbox içinden toplamak, plan başlığını
        // çoğu CAD çiziminde olduğu gibi çerçevenin hemen dışındaysa kaybediyordu.
        // Her metin en fazla bir kez, tolerans içindeki en yakın bbox'a atanır.
        foreach (var metin in metinler)
        {
            KatKumesi? enYakinKume = null;
            double enYakinMesafe = double.MaxValue;
            foreach (var kume in sonuc)
            {
                double mesafe = DikdortgeneMesafe(
                    metin.Pos, kume.MinX, kume.MinY, kume.MaxX, kume.MaxY);
                if (mesafe >= enYakinMesafe) continue;
                enYakinMesafe = mesafe;
                enYakinKume = kume;
            }

            if (enYakinKume is not null && enYakinMesafe <= ayar.EtiketAtamaMesafesiM)
                enYakinKume.Metinler.Add(metin);
        }

        return sonuc.OrderBy(k => k.MinY).ThenBy(k => k.MinX).ToList();
    }

    /// Kümelerden "plan olmayanları" (küçük bbox — antet/lejant/görünüş) ayıklar.
    public static (List<KatKumesi> Planlar, List<KatKumesi> PlanDegil) PlanFiltrele(
        IReadOnlyList<KatKumesi> kumeler, Ayarlar? ayar = null)
    {
        ayar ??= new Ayarlar();
        var planlar = new List<KatKumesi>();
        var planDegil = new List<KatKumesi>();
        foreach (var k in kumeler)
        {
            if (k.BboxAlanM2 >= ayar.MinPlanAlaniM2) planlar.Add(k);
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

    private static double DikdortgeneMesafe(
        Point2D p, double minX, double minY, double maxX, double maxY)
    {
        double dx = p.X < minX ? minX - p.X : p.X > maxX ? p.X - maxX : 0;
        double dy = p.Y < minY ? minY - p.Y : p.Y > maxY ? p.Y - maxY : 0;
        return Math.Sqrt(dx * dx + dy * dy);
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
