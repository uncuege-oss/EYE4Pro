using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.BuildingAcoustics.Parsers;

/// <summary>
/// B3 — DXF LINE segmentlerinden kanonik oda poligonu çıkarımı (face detection).
///
/// Planar straight-line graph (PSLG) yöntemi:
///   1. Uçları ε toleransla snapla → ortak köşeler
///   2. Half-edge yapısı (her edge iki yönlü) + her vertex'te açıya göre CCW sıralama
///   3. Leftmost-turn ile face trace: ardışık half-edge'ler aynı yüze ait
///   4. Pozitif yüzeyli (CCW) face'leri oda adayı say; sınırsız yüz (CW) elenir
///   5. Min/max alan filtresi
///
/// Flood-fill (<see cref="Electric.Ortak.BosluktanOdaTespitServisi"/>) alternatifi:
///   • raster quantization yok → mm hassasiyetinde poligon
///   • performans O(N log N), büyük binalarda hızlı
///
/// Sınır: tüm kesişimler explicit köşe olmalı. CAD'de kesişen iki segment
/// kesişim noktasında bölünmemişse face detection eksik olur. Pratikte
/// duvarlar continuous polyline ile çizildiğinden bu varsayım geçerlidir.
/// Kapı/pencere boşluğu kapatılmamışsa dış kontur birden fazla oda yutar;
/// bu senaryoda flood-fill sanal duvarlarla daha tutarlıdır.
/// </summary>
public static class KanonikOdaTespitServisi
{
    public sealed class Ayarlar
    {
        /// Vertex snap toleransı (m). Bu mesafe içindeki uçlar tek noktaya birleştirilir.
        public double SnapToleransM { get; set; } = 0.05;
        /// Minimum oda alanı (m²) — bunun altındaki kapalı yüzler atılır.
        public double MinOdaAlaniM2 { get; set; } = 2.0;
        /// Maksimum oda alanı (m²) — bunun üstündekiler tipik olarak çevre dış sınırıdır.
        public double MaxOdaAlaniM2 { get; set; } = 10000.0;
    }

    public static List<List<Point2D>> OdalariBul(
        IReadOnlyList<(Point2D A, Point2D B)> segmentler,
        Ayarlar? ayar = null)
    {
        ayar ??= new Ayarlar();
        if (segmentler == null || segmentler.Count == 0) return new();

        // ── 1. Vertex snap + dedupe (spatial hash) ───────────────────────────
        var vertices = new List<Point2D>();
        var buckets = new Dictionary<(long, long), List<int>>();
        double tol = Math.Max(ayar.SnapToleransM, 1e-6);
        double cell = Math.Max(tol, 0.01);
        double tolSq = tol * tol;

        int FindOrAdd(Point2D p)
        {
            long cx = (long)Math.Floor(p.X / cell);
            long cy = (long)Math.Floor(p.Y / cell);
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (!buckets.TryGetValue((cx + dx, cy + dy), out var list)) continue;
                foreach (var idx in list)
                {
                    var v = vertices[idx];
                    double dvx = v.X - p.X;
                    double dvy = v.Y - p.Y;
                    if (dvx * dvx + dvy * dvy <= tolSq) return idx;
                }
            }
            int newIdx = vertices.Count;
            vertices.Add(p);
            if (!buckets.TryGetValue((cx, cy), out var bucket))
                buckets[(cx, cy)] = bucket = new List<int>();
            bucket.Add(newIdx);
            return newIdx;
        }

        // ── 2. Half-edge tablosu ─────────────────────────────────────────────
        // edges[2k]   = forward (a→b)
        // edges[2k+1] = reverse (b→a)
        // twin(e) = e XOR 1
        var edges = new List<HalfEdge>();
        var edgeSet = new HashSet<(int, int)>();
        foreach (var (a, b) in segmentler)
        {
            int va = FindOrAdd(a);
            int vb = FindOrAdd(b);
            if (va == vb) continue;
            var key = va < vb ? (va, vb) : (vb, va);
            if (!edgeSet.Add(key)) continue;

            double angleAb = Math.Atan2(vertices[vb].Y - vertices[va].Y, vertices[vb].X - vertices[va].X);
            double angleBa = Math.Atan2(vertices[va].Y - vertices[vb].Y, vertices[va].X - vertices[vb].X);
            edges.Add(new HalfEdge(va, vb, angleAb));
            edges.Add(new HalfEdge(vb, va, angleBa));
        }

        if (edges.Count == 0) return new();

        // ── 3. Outgoing half-edge'leri her vertex'te açıya göre sırala ───────
        var outgoing = new List<int>[vertices.Count];
        for (int i = 0; i < vertices.Count; i++) outgoing[i] = new List<int>();
        for (int e = 0; e < edges.Count; e++)
            outgoing[edges[e].From].Add(e);
        for (int v = 0; v < vertices.Count; v++)
            outgoing[v].Sort((x, y) => edges[x].Angle.CompareTo(edges[y].Angle));

        // Her edge'in outgoing listesindeki konumu (hızlı next-arama için)
        var indexInOutgoing = new int[edges.Count];
        for (int v = 0; v < vertices.Count; v++)
        {
            var list = outgoing[v];
            for (int i = 0; i < list.Count; i++)
                indexInOutgoing[list[i]] = i;
        }

        // ── 4. Face tracing (leftmost-turn) ──────────────────────────────────
        var visited = new bool[edges.Count];
        var faces = new List<List<Point2D>>();

        for (int start = 0; start < edges.Count; start++)
        {
            if (visited[start]) continue;
            var faceVerts = new List<int>();
            int cur = start;
            int safety = 0;
            bool ok = true;
            while (true)
            {
                if (visited[cur]) { ok = false; break; }
                visited[cur] = true;
                faceVerts.Add(edges[cur].From);

                int target = edges[cur].To;
                int twin = cur ^ 1;
                var sorted = outgoing[target];
                int n = sorted.Count;
                if (n < 1) { ok = false; break; }
                int idx = indexInOutgoing[twin];
                int prevIdx = (idx - 1 + n) % n;  // CCW önceki = leftmost turn
                int next = sorted[prevIdx];
                if (next == start) break; // face kapandı
                cur = next;
                if (++safety > 200_000) { ok = false; break; }
            }

            if (!ok || faceVerts.Count < 3) continue;

            var polygon = faceVerts.Select(i => vertices[i]).ToList();
            double signedArea = SignedArea(polygon);
            if (signedArea <= 0) continue; // sınırsız yüz (CW) veya dejenere
            if (signedArea < ayar.MinOdaAlaniM2 || signedArea > ayar.MaxOdaAlaniM2) continue;

            faces.Add(polygon);
        }

        return faces;
    }

    private static double SignedArea(IReadOnlyList<Point2D> poly)
    {
        double sum = 0;
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Count];
            sum += a.X * b.Y - b.X * a.Y;
        }
        return sum * 0.5;
    }

    private readonly record struct HalfEdge(int From, int To, double Angle);
}
