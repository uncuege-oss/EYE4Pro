using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Bina.Services;

/// DXF duvar segmentlerinden flood-fill ile oda poligonlarını tespit eder.
/// Duvarların kapalı poligon olmasını gerektirmez; çift çizgili / etiketsiz planları doğal ele alır.
/// Akış: raster grid → kapı dilation → dış flood → watershed → kontur → eş-doğrultulu birleştir + snap.
public static class BosluktanOdaTespitServisi
{
    public sealed class Ayarlar
    {
        public double HucreBoyuM = 0.10;
        public double KapiKapatmaM = 0.50;
        public double MinOdaAlaniM2 = 2.0;
        public double SnapToleransM = 0.35;
        public double CikintiDerinlikEsigiM = 0.35;
        public double CikintiGenislikEsigiM = 0.80;
        public long MaxHucre = 4_000_000;
    }

    private const int OUTSIDE = -1;

    public static List<List<Point2D>> OdalariBul(
        IReadOnlyList<(Point2D A, Point2D B)> segmentler, Ayarlar? ayar = null,
        IReadOnlyList<(Point2D A, Point2D B)>? ekDuvarlar = null)
    {
        ayar ??= new Ayarlar();
        if (segmentler == null || segmentler.Count == 0) return new();

        if (ekDuvarlar is { Count: > 0 })
        {
            var birlestirilmis = new List<(Point2D A, Point2D B)>(segmentler.Count + ekDuvarlar.Count);
            birlestirilmis.AddRange(segmentler);
            birlestirilmis.AddRange(ekDuvarlar);
            segmentler = birlestirilmis;
        }

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var (a, b) in segmentler)
        {
            if (!IsFinite(a) || !IsFinite(b)) continue;
            minX = Math.Min(minX, Math.Min(a.X, b.X));
            minY = Math.Min(minY, Math.Min(a.Y, b.Y));
            maxX = Math.Max(maxX, Math.Max(a.X, b.X));
            maxY = Math.Max(maxY, Math.Max(a.Y, b.Y));
        }
        if (maxX <= minX || maxY <= minY) return new();

        if (Math.Max(maxX - minX, maxY - minY) > 5000)
        {
            var xs = new List<double>(segmentler.Count * 2);
            var ys = new List<double>(segmentler.Count * 2);
            foreach (var (a, b) in segmentler)
            {
                if (!IsFinite(a) || !IsFinite(b)) continue;
                xs.Add(a.X); xs.Add(b.X); ys.Add(a.Y); ys.Add(b.Y);
            }
            (minX, maxX) = RobustAralik(xs);
            (minY, maxY) = RobustAralik(ys);
            if (maxX <= minX || maxY <= minY) return new();
        }

        double cs = ayar.HucreBoyuM;
        double margin = ayar.KapiKapatmaM + 3 * cs;
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
            return new();

        int w = (int)wD;
        int h = (int)hD;

        int Idx(int x, int y) => y * w + x;
        int CX(double x) => Math.Clamp((int)((x - minX) / cs), 0, w - 1);
        int CY(double y) => Math.Clamp((int)((y - minY) / cs), 0, h - 1);

        var wall = new bool[w * h];
        foreach (var (a, b) in segmentler)
            CizgiCiz(wall, w, h, CX(a.X), CY(a.Y), CX(b.X), CY(b.Y));

        int R = Math.Max(1, (int)Math.Round(ayar.KapiKapatmaM / cs, MidpointRounding.AwayFromZero));
        var muhurlu = Dilate(wall, w, h, R);

        var label = new int[w * h];
        var q = new Queue<int>();
        for (int x = 0; x < w; x++)
        {
            Disari(Idx(x, 0)); Disari(Idx(x, h - 1));
        }
        for (int y = 0; y < h; y++)
        {
            Disari(Idx(0, y)); Disari(Idx(w - 1, y));
        }
        void Disari(int i)
        {
            if (!muhurlu[i] && label[i] == 0) { label[i] = OUTSIDE; q.Enqueue(i); }
        }
        BfsYay(q, muhurlu, label, w, h, OUTSIDE, sadeceMuhursuz: true);

        int odaNo = 0;
        var cekirdekHucreSayisi = new List<int>();
        for (int i = 0; i < label.Length; i++)
        {
            if (muhurlu[i] || label[i] != 0) continue;
            odaNo++;
            var bq = new Queue<int>();
            label[i] = odaNo; bq.Enqueue(i);
            int sayi = BfsYay(bq, muhurlu, label, w, h, odaNo, sadeceMuhursuz: true);
            cekirdekHucreSayisi.Add(sayi);
        }
        if (odaNo == 0) return new();

        var wq = new Queue<int>();
        for (int i = 0; i < label.Length; i++)
            if (label[i] != 0 && !wall[i]) wq.Enqueue(i);
        WatershedYay(wq, wall, label, w, h);

        double cellArea = cs * cs;
        var cellCount = new int[odaNo + 1];
        var edgesByLabel = new Dictionary<int, List<int>>[odaNo + 1];

        int CornerId(int cx, int cy) => cx + cy * (w + 1);

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int k = label[x + y * w];
            if (k <= 0) continue;
            cellCount[k]++;

            var dict = edgesByLabel[k] ??= new Dictionary<int, List<int>>();
            void Kenar(int aCx, int aCy, int bCx, int bCy)
            {
                int a = CornerId(aCx, aCy), b = CornerId(bCx, bCy);
                if (!dict!.TryGetValue(a, out var lst)) { lst = new List<int>(); dict[a] = lst; }
                lst.Add(b);
            }
            bool Oda(int nx, int ny) =>
                nx >= 0 && nx < w && ny >= 0 && ny < h && label[nx + ny * w] == k;

            if (!Oda(x, y - 1)) Kenar(x, y, x + 1, y);
            if (!Oda(x + 1, y)) Kenar(x + 1, y, x + 1, y + 1);
            if (!Oda(x, y + 1)) Kenar(x + 1, y + 1, x, y + 1);
            if (!Oda(x - 1, y)) Kenar(x, y + 1, x, y);
        }

        var (vCenters, hCenters) = DuvarKoordinatKumeleri(segmentler);
        double minAlan = ayar.MinOdaAlaniM2;
        double snapTol = ayar.SnapToleransM;

        Point2D CornerPos(int id) => new(minX + (id % (w + 1)) * cs, minY + (id / (w + 1)) * cs);

        var sonuc = new List<List<Point2D>>();
        for (int k = 1; k <= odaNo; k++)
        {
            if (cellCount[k] * cellArea < minAlan * 0.5) continue;
            var dict = edgesByLabel[k];
            if (dict == null) continue;

            var kontur = EnBuyukHalka(dict, CornerPos);
            if (kontur.Count < 4) continue;

            var sade = EsDogrultuBirlestir(kontur);
            sade = SnapDuvarlara(sade, vCenters, hCenters, snapTol);
            sade = EsDogrultuBirlestir(sade);
            sade = KucukCikintilariDuzlestir(sade, ayar.CikintiDerinlikEsigiM, ayar.CikintiGenislikEsigiM);
            sade = EsDogrultuBirlestir(sade);

            if (sade.Count < 3) continue;
            if (PoligonYardimci.Alan(sade) < minAlan) continue;
            sonuc.Add(sade);
        }

        return sonuc;
    }

    public static List<List<Point2D>> OdalariAdaptifBul(
        IReadOnlyList<(Point2D A, Point2D B)> segmentler,
        Ayarlar? ayar = null,
        IReadOnlyList<(Point2D A, Point2D B)>? ekDuvarlar = null,
        double devOranEsigi = 0.25,
        Ayarlar? ikinciGecisAyar = null)
    {
        var ilkSonuc = OdalariBul(segmentler, ayar, ekDuvarlar);
        if (ilkSonuc.Count == 0) return ilkSonuc;

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var poly in ilkSonuc)
        foreach (var p in poly)
        {
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }
        double toplamBbox = (maxX - minX) * (maxY - minY);
        if (toplamBbox <= 0) return ilkSonuc;
        double devEsik = toplamBbox * devOranEsigi;

        ikinciGecisAyar ??= new Ayarlar
        {
            HucreBoyuM = 0.07,
            KapiKapatmaM = 0.80,
            MinOdaAlaniM2 = 4.0,
            SnapToleransM = 0.25,
            CikintiDerinlikEsigiM = ayar?.CikintiDerinlikEsigiM ?? 0.35,
            CikintiGenislikEsigiM = ayar?.CikintiGenislikEsigiM ?? 0.80
        };

        var sonuc = new List<List<Point2D>>(ilkSonuc.Count);
        foreach (var oda in ilkSonuc)
        {
            double alan = PoligonYardimci.Alan(oda);
            if (alan < devEsik) { sonuc.Add(oda); continue; }

            double oMinX = double.MaxValue, oMinY = double.MaxValue;
            double oMaxX = double.MinValue, oMaxY = double.MinValue;
            foreach (var p in oda)
            {
                if (p.X < oMinX) oMinX = p.X;
                if (p.Y < oMinY) oMinY = p.Y;
                if (p.X > oMaxX) oMaxX = p.X;
                if (p.Y > oMaxY) oMaxY = p.Y;
            }
            const double pad = 0.50;
            oMinX -= pad; oMinY -= pad; oMaxX += pad; oMaxY += pad;

            var ikinciSegs = SegBboxFiltre(segmentler, oMinX, oMinY, oMaxX, oMaxY);
            IReadOnlyList<(Point2D A, Point2D B)>? ikinciEkDuvarlar = null;
            if (ekDuvarlar is { Count: > 0 })
                ikinciEkDuvarlar = SegBboxFiltre(ekDuvarlar, oMinX, oMinY, oMaxX, oMaxY);

            if (ikinciSegs.Count == 0) { sonuc.Add(oda); continue; }

            var altOdalar = OdalariBul(ikinciSegs, ikinciGecisAyar, ikinciEkDuvarlar);

            var icAlt = new List<List<Point2D>>();
            foreach (var alt in altOdalar)
            {
                var merkez = PoligonMerkezi(alt);
                if (PoligonYardimci.IsPointInside(merkez, oda)) icAlt.Add(alt);
            }

            if (icAlt.Count >= 2) sonuc.AddRange(icAlt);
            else sonuc.Add(oda);
        }
        return sonuc;
    }

    private static List<(Point2D A, Point2D B)> SegBboxFiltre(
        IReadOnlyList<(Point2D A, Point2D B)> segs,
        double minX, double minY, double maxX, double maxY)
    {
        var sonuc = new List<(Point2D A, Point2D B)>();
        foreach (var (a, b) in segs)
        {
            double sMinX = Math.Min(a.X, b.X), sMaxX = Math.Max(a.X, b.X);
            double sMinY = Math.Min(a.Y, b.Y), sMaxY = Math.Max(a.Y, b.Y);
            if (sMaxX < minX || sMinX > maxX || sMaxY < minY || sMinY > maxY) continue;
            sonuc.Add((a, b));
        }
        return sonuc;
    }

    private static Point2D PoligonMerkezi(List<Point2D> poly)
    {
        double sx = 0, sy = 0;
        foreach (var p in poly) { sx += p.X; sy += p.Y; }
        return new Point2D(sx / poly.Count, sy / poly.Count);
    }

    private static List<Point2D> EnBuyukHalka(Dictionary<int, List<int>> ileri,
        Func<int, Point2D> pos)
    {
        List<Point2D>? enBuyuk = null;
        double enBuyukAlan = 0;

        foreach (var bas in ileri.Keys.ToList())
        {
            while (ileri.TryGetValue(bas, out var cikis) && cikis.Count > 0)
            {
                var halka = new List<int>();
                int cur = bas;
                bool kapandi = false;
                while (true)
                {
                    if (!ileri.TryGetValue(cur, out var lst) || lst.Count == 0) break;
                    int nxt = lst[^1];
                    lst.RemoveAt(lst.Count - 1);
                    halka.Add(cur);
                    cur = nxt;
                    if (cur == bas) { kapandi = true; break; }
                }
                if (!kapandi || halka.Count < 4) continue;

                var poly = halka.Select(pos).ToList();
                double alan = PoligonYardimci.Alan(poly);
                if (alan > enBuyukAlan) { enBuyukAlan = alan; enBuyuk = poly; }
            }
        }
        return enBuyuk ?? new List<Point2D>();
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

    private static bool[] Dilate(bool[] wall, int w, int h, int R)
    {
        var dist = new int[w * h];
        Array.Fill(dist, int.MaxValue);
        var q = new Queue<int>();
        for (int i = 0; i < wall.Length; i++)
            if (wall[i]) { dist[i] = 0; q.Enqueue(i); }

        var muhurlu = (bool[])wall.Clone();
        while (q.Count > 0)
        {
            int c = q.Dequeue();
            int d = dist[c];
            if (d >= R) continue;
            int x = c % w, y = c / w;
            Komsu(x, y, w, h, (ni) =>
            {
                if (dist[ni] > d + 1) { dist[ni] = d + 1; muhurlu[ni] = true; q.Enqueue(ni); }
            });
        }
        return muhurlu;
    }

    private static int BfsYay(Queue<int> q, bool[] muhurlu, int[] label, int w, int h,
        int etiket, bool sadeceMuhursuz)
    {
        int sayi = 0;
        while (q.Count > 0)
        {
            int c = q.Dequeue();
            sayi++;
            int x = c % w, y = c / w;
            Komsu(x, y, w, h, (ni) =>
            {
                if (label[ni] != 0) return;
                if (sadeceMuhursuz && muhurlu[ni]) return;
                label[ni] = etiket;
                q.Enqueue(ni);
            });
        }
        return sayi;
    }

    private static void WatershedYay(Queue<int> q, bool[] wall, int[] label, int w, int h)
    {
        while (q.Count > 0)
        {
            int c = q.Dequeue();
            int et = label[c];
            int x = c % w, y = c / w;
            Komsu(x, y, w, h, (ni) =>
            {
                if (label[ni] != 0 || wall[ni]) return;
                label[ni] = et;
                q.Enqueue(ni);
            });
        }
    }

    private static void Komsu(int x, int y, int w, int h, Action<int> f)
    {
        if (x > 0)     f((x - 1) + y * w);
        if (x < w - 1) f((x + 1) + y * w);
        if (y > 0)     f(x + (y - 1) * w);
        if (y < h - 1) f(x + (y + 1) * w);
    }

    private static List<Point2D> EsDogrultuBirlestir(List<Point2D> pts)
    {
        if (pts.Count < 3) return pts;
        var temiz = new List<Point2D>();
        foreach (var p in pts)
            if (temiz.Count == 0 || Mesafe(temiz[^1], p) > 1e-6) temiz.Add(p);
        if (temiz.Count >= 2 && Mesafe(temiz[0], temiz[^1]) <= 1e-6) temiz.RemoveAt(temiz.Count - 1);
        if (temiz.Count < 3) return temiz;

        var sonuc = new List<Point2D>();
        int n = temiz.Count;
        for (int i = 0; i < n; i++)
        {
            var prev = temiz[(i - 1 + n) % n];
            var cur = temiz[i];
            var next = temiz[(i + 1) % n];
            double ax = cur.X - prev.X, ay = cur.Y - prev.Y;
            double bx = next.X - cur.X, by = next.Y - cur.Y;
            double cross = ax * by - ay * bx;
            double la = Math.Sqrt(ax * ax + ay * ay), lb = Math.Sqrt(bx * bx + by * by);
            if (la < 1e-9 || lb < 1e-9) continue;
            if (Math.Abs(cross) / (la * lb) > 1e-3) sonuc.Add(cur);
        }
        return sonuc.Count >= 3 ? sonuc : temiz;
    }

    private static List<Point2D> KucukCikintilariDuzlestir(
        List<Point2D> pts, double derinlikEsigi, double genislikEsigi)
    {
        if (pts.Count < 5 || derinlikEsigi <= 0 || genislikEsigi <= 0) return pts;

        var cur = pts.ToList();
        bool changed;
        int guard = 0;
        do
        {
            changed = false;
            int n = cur.Count;
            if (n < 5) break;

            for (int i = 0; i < n; i++)
            {
                var p0 = cur[i];
                var p1 = cur[(i + 1) % n];
                var p2 = cur[(i + 2) % n];
                var p3 = cur[(i + 3) % n];
                var p4 = cur[(i + 4) % n];

                if (!KucukDikdortgenCikintiMi(p0, p1, p2, p3, p4, derinlikEsigi, genislikEsigi))
                    continue;

                var sonraki = new List<Point2D>(n - 3);
                for (int j = 0; j < n; j++)
                {
                    int rel = (j - ((i + 1) % n) + n) % n;
                    if (rel is >= 0 and <= 2) continue;
                    sonraki.Add(cur[j]);
                }

                cur = sonraki;
                changed = true;
                break;
            }
        } while (changed && ++guard < 64);

        return cur.Count >= 3 ? cur : pts;
    }

    private static bool KucukDikdortgenCikintiMi(
        Point2D p0, Point2D p1, Point2D p2, Point2D p3, Point2D p4,
        double derinlikEsigi, double genislikEsigi)
    {
        double d12 = Mesafe(p1, p2), d23 = Mesafe(p2, p3), d34 = Mesafe(p3, p4);
        if (d12 <= 1e-6 || d23 <= 1e-6 || d34 <= 1e-6) return false;
        if (Math.Max(d12, d34) > derinlikEsigi) return false;
        if (d23 > genislikEsigi) return false;
        if (NoktaCizgiMesafesi(p4, p0, p1) > Math.Max(0.05, derinlikEsigi * 0.25)) return false;
        if (AciSinusu(p0, p1, p2, p3) > 0.05) return false;
        if (TersYonDot(p1, p2, p3, p4) < 0.95) return false;
        if (Math.Abs(d12 - d34) > Math.Max(0.08, derinlikEsigi * 0.35)) return false;
        return true;
    }

    private static double AciSinusu(Point2D a, Point2D b, Point2D c, Point2D d)
    {
        double ax = b.X - a.X, ay = b.Y - a.Y;
        double bx = d.X - c.X, by = d.Y - c.Y;
        double la = Math.Sqrt(ax * ax + ay * ay), lb = Math.Sqrt(bx * bx + by * by);
        if (la < 1e-9 || lb < 1e-9) return 1;
        return Math.Abs(ax * by - ay * bx) / (la * lb);
    }

    private static double TersYonDot(Point2D a, Point2D b, Point2D c, Point2D d)
    {
        double ax = b.X - a.X, ay = b.Y - a.Y;
        double bx = d.X - c.X, by = d.Y - c.Y;
        double la = Math.Sqrt(ax * ax + ay * ay), lb = Math.Sqrt(bx * bx + by * by);
        if (la < 1e-9 || lb < 1e-9) return -1;
        return -(ax * bx + ay * by) / (la * lb);
    }

    private static double NoktaCizgiMesafesi(Point2D p, Point2D a, Point2D b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-9) return Mesafe(p, a);
        return Math.Abs((p.X - a.X) * dy - (p.Y - a.Y) * dx) / len;
    }

    private static List<Point2D> SnapDuvarlara(List<Point2D> pts, double[] vX, double[] hY, double tol)
    {
        double SnapEksen(double v, double[] kume)
        {
            double best = v, bestD = tol;
            foreach (var c in kume) { double d = Math.Abs(v - c); if (d < bestD) { bestD = d; best = c; } }
            return best;
        }
        return pts.Select(p => new Point2D(SnapEksen(p.X, vX), SnapEksen(p.Y, hY))).ToList();
    }

    private static (double[] vX, double[] hY) DuvarKoordinatKumeleri(
        IReadOnlyList<(Point2D A, Point2D B)> segs)
    {
        const double minLen = 0.4, orthTol = 0.08, clTol = 0.12;
        var vXs = new List<double>(); var hYs = new List<double>();
        foreach (var (a, b) in segs)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < minLen) continue;
            if (Math.Abs(dy) / len < orthTol) hYs.Add((a.Y + b.Y) * 0.5);
            else if (Math.Abs(dx) / len < orthTol) vXs.Add((a.X + b.X) * 0.5);
        }
        return (Kumelendir(vXs, clTol), Kumelendir(hYs, clTol));
    }

    private static double[] Kumelendir(List<double> vals, double tol)
    {
        if (vals.Count == 0) return Array.Empty<double>();
        var sorted = vals.OrderBy(v => v).ToList();
        var centers = new List<double>();
        double sum = sorted[0]; int cnt = 1;
        for (int i = 1; i < sorted.Count; i++)
        {
            if (sorted[i] - sorted[i - 1] <= tol) { sum += sorted[i]; cnt++; }
            else { centers.Add(sum / cnt); sum = sorted[i]; cnt = 1; }
        }
        centers.Add(sum / cnt);
        return centers.ToArray();
    }

    private static double Mesafe(Point2D a, Point2D b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static bool IsFinite(Point2D p)
        => double.IsFinite(p.X) && double.IsFinite(p.Y);

    private static (double, double) RobustAralik(List<double> vals)
    {
        if (vals.Count == 0) return (0, 0);
        double med = Medyan(vals);
        var sapma = new List<double>(vals.Count);
        foreach (var v in vals) sapma.Add(Math.Abs(v - med));
        double mad = Medyan(sapma);
        double r = 20 * mad + 50;
        double lo = med - r, hi = med + r;

        double mn = double.MaxValue, mx = double.MinValue;
        foreach (var v in vals)
            if (v >= lo && v <= hi) { if (v < mn) mn = v; if (v > mx) mx = v; }

        return mn <= mx ? (mn, mx) : (med, med);
    }

    private static double Medyan(List<double> vals)
    {
        vals.Sort();
        int n = vals.Count;
        return n % 2 == 1 ? vals[n / 2] : (vals[n / 2 - 1] + vals[n / 2]) * 0.5;
    }
}
