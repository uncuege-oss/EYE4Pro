using Kst.Acoustics.Metraj.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Kst.Acoustics.Metraj.Template;

public class TemplateMatchResult
{
    public string TemplateName { get; set; } = string.Empty;
    public string LayerName { get; set; } = string.Empty;
    public int Count { get; set; }
    public MetrajPoint Center { get; set; } = new(0, 0, 0);
}

public class TemplateMatcher
{
    private readonly TemplateLibrary _library;
    private readonly MetrajPointComparer _pointComparer = new MetrajPointComparer(1e-6);

    // Hard limit on recursive calls per island/template pair to prevent hangs
    private const int MaxRecursiveCalls = 500;

    public TemplateMatcher(TemplateLibrary library)
    {
        _library = library;
    }

    public List<TemplateMatchResult> Match(List<MetrajEntity> entities, CancellationToken ct = default, Action<string>? onProgress = null)
    {
        var results = new List<TemplateMatchResult>();
        var segments = ExtractSegments(entities);
        if (segments.Count == 0) return results;

        onProgress?.Invoke($"Geometri analiz ediliyor... ({segments.Count} segment)");
        var islands = GroupIntoIslands(segments);

        var templates = _library.GetSortedTemplates().ToList();
        if (templates.Count == 0) 
        {
            onProgress?.Invoke("UYARI: Şablon kütüphanesi boş. Hiçbir şablon yüklenmemiş.");
            return results;
        }

        // DIAGNOSTICS: report template info
        var segCounts = templates.Select(t => $"{t.Name}={t.SegmentCount}seg").ToList();
        onProgress?.Invoke($"Şablonlar: {string.Join(", ", segCounts.Take(10))}{(segCounts.Count > 10 ? "..." : "")}");

        // Build a lookup: segment count -> templates
        var templatesBySegCount = new Dictionary<int, List<MetrajTemplate>>();
        foreach (var t in templates)
        {
            if (!templatesBySegCount.ContainsKey(t.SegmentCount))
                templatesBySegCount[t.SegmentCount] = new List<MetrajTemplate>();
            templatesBySegCount[t.SegmentCount].Add(t);
        }

        // Island segment count histogram for diagnostics
        var islandSizeHist = new Dictionary<int, int>();
        foreach (var island in islands)
        {
            int sc = island.Count;
            if (!islandSizeHist.ContainsKey(sc)) islandSizeHist[sc] = 0;
            islandSizeHist[sc]++;
        }

        // Report how many islands match template sizes
        int candidateIslands = 0;
        foreach (var kvp in templatesBySegCount)
        {
            if (islandSizeHist.TryGetValue(kvp.Key, out int cnt))
                candidateIslands += cnt;
        }
        onProgress?.Invoke($"{islands.Count} grup, {candidateIslands} tanesi şablon segment sayısıyla eşleşiyor. Taranıyor...");

        int processedIslands = 0;
        int testedPairs = 0;

        foreach (var island in islands)
        {
            ct.ThrowIfCancellationRequested();
            processedIslands++;

            // Skip oversized islands — no armature has 100+ segments.
            // Large islands are building outlines, ductwork, etc.
            if (island.Count > 100) continue;

            // EXACT segment count match only — no fuzzy range
            if (!templatesBySegCount.TryGetValue(island.Count, out var candidates))
                continue;

            // Per-island time budget: 50ms max
            var sw = Stopwatch.StartNew();

            // Build connectivity map only for candidate islands
            var islandMap = BuildConnectivityMap(island, new HashSet<int>());
            var startPoints = islandMap.Keys.ToList();

            foreach (var template in candidates)
            {
                if (sw.ElapsedMilliseconds > 50) break;

                testedPairs++;
                int callCounter = 0;
                bool found = false;

                foreach (var startPoint in startPoints)
                {
                    if (callCounter >= MaxRecursiveCalls) break;
                    if (sw.ElapsedMilliseconds > 50) break;

                    var match = FindMatchRecursive(startPoint, template, islandMap, new HashSet<int>(),
                        new List<int>(), new List<MetrajPoint> { startPoint }, ct, ref callCounter);
                    if (match != null)
                    {
                        string layer = island[match[0]].Layer;

                        double minX = double.MaxValue, maxX = double.MinValue;
                        double minY = double.MaxValue, maxY = double.MinValue;
                        foreach (var segIdx in match)
                        {
                            var s = island[segIdx];
                            minX = Math.Min(minX, Math.Min(s.P1.X, s.P2.X));
                            maxX = Math.Max(maxX, Math.Max(s.P1.X, s.P2.X));
                            minY = Math.Min(minY, Math.Min(s.P1.Y, s.P2.Y));
                            maxY = Math.Max(maxY, Math.Max(s.P1.Y, s.P2.Y));
                        }
                        var center = new MetrajPoint((minX + maxX) / 2, (minY + maxY) / 2, 0);

                        results.Add(new TemplateMatchResult
                        {
                            TemplateName = template.Name,
                            LayerName = layer,
                            Count = 1,
                            Center = center
                        });
                        found = true;
                        break;
                    }
                }
                if (found) break;
            }

            if (processedIslands % 10000 == 0)
                onProgress?.Invoke($"Tarama: {processedIslands}/{islands.Count} grup, {results.Count} tespit, {testedPairs} detaylı karşılaştırma");
        }

        onProgress?.Invoke($"Eşleştirme tamamlandı: {results.Count} tespit ({testedPairs} detaylı karşılaştırma yapıldı)");
        return results;
    }

    private List<List<(MetrajPoint P1, MetrajPoint P2, string Layer)>> GroupIntoIslands(
        List<(MetrajPoint P1, MetrajPoint P2, string Layer)> segments)
    {
        var islands = new List<List<(MetrajPoint P1, MetrajPoint P2, string Layer)>>();
        var used = new bool[segments.Count];
        var connectivityComparer = new MetrajPointComparer(0.01);
        var map = new Dictionary<MetrajPoint, List<int>>(connectivityComparer);

        for (int i = 0; i < segments.Count; i++)
        {
            if (!map.ContainsKey(segments[i].P1)) map[segments[i].P1] = new List<int>();
            if (!map.ContainsKey(segments[i].P2)) map[segments[i].P2] = new List<int>();
            map[segments[i].P1].Add(i);
            map[segments[i].P2].Add(i);
        }

        for (int i = 0; i < segments.Count; i++)
        {
            if (used[i]) continue;

            var currentIsland = new List<(MetrajPoint, MetrajPoint, string)>();
            var queue = new Queue<int>();
            queue.Enqueue(i);
            used[i] = true;

            while (queue.Count > 0)
            {
                var idx = queue.Dequeue();
                currentIsland.Add(segments[idx]);

                foreach (var pt in new[] { segments[idx].P1, segments[idx].P2 })
                {
                    if (map.TryGetValue(pt, out var neighbors))
                    {
                        foreach (var nIdx in neighbors)
                        {
                            if (!used[nIdx])
                            {
                                used[nIdx] = true;
                                queue.Enqueue(nIdx);
                            }
                        }
                    }
                }
            }
            islands.Add(currentIsland);
        }
        return islands;
    }

    private List<int>? FindMatchRecursive(
        MetrajPoint currentPoint, MetrajTemplate template,
        Dictionary<MetrajPoint, List<(int Index, MetrajPoint Other)>> map,
        HashSet<int> usedSegments, List<int> currentPath, List<MetrajPoint> pathPoints,
        CancellationToken ct, ref int callCounter)
    {
        callCounter++;
        if (callCounter > MaxRecursiveCalls) return null;

        if (currentPath.Count == template.SegmentCount)
        {
            var signature = new GeometricSignature(pathPoints);
            if (GeometricSignature.IsMatch(template.Signature, signature, template.LengthTolPercent, template.AngleTolDeg))
                return new List<int>(currentPath);
            return null;
        }

        if (!map.TryGetValue(currentPoint, out var neighbors)) return null;

        foreach (var neighbor in neighbors)
        {
            if (callCounter > MaxRecursiveCalls) return null;
            if (usedSegments.Contains(neighbor.Index) || currentPath.Contains(neighbor.Index)) continue;

            currentPath.Add(neighbor.Index);
            pathPoints.Add(neighbor.Other);

            var result = FindMatchRecursive(neighbor.Other, template, map, usedSegments, currentPath, pathPoints, ct, ref callCounter);
            if (result != null) return result;

            currentPath.RemoveAt(currentPath.Count - 1);
            pathPoints.RemoveAt(pathPoints.Count - 1);
        }

        return null;
    }

    private List<(MetrajPoint P1, MetrajPoint P2, string Layer)> ExtractSegments(List<MetrajEntity> entities)
    {
        var segments = new List<(MetrajPoint P1, MetrajPoint P2, string Layer)>();
        foreach (var entity in entities)
        {
            if (entity is MetrajLine line)
            {
                segments.Add((line.Start, line.End, line.Layer));
            }
            else if (entity is MetrajPolyline poly)
            {
                for (int i = 0; i < poly.Vertices.Count - 1; i++)
                {
                    segments.Add((poly.Vertices[i], poly.Vertices[i + 1], poly.Layer));
                }
                if (poly.IsClosed && poly.Vertices.Count > 1)
                {
                    segments.Add((poly.Vertices.Last(), poly.Vertices.First(), poly.Layer));
                }
            }
            else if (entity is MetrajCircle circle)
            {
                int steps = 16;
                var pts = new List<MetrajPoint>();
                for (int i = 0; i <= steps; i++)
                {
                    double ang = i * 2 * Math.PI / steps;
                    pts.Add(new MetrajPoint(circle.Center.X + circle.Radius * Math.Cos(ang), circle.Center.Y + circle.Radius * Math.Sin(ang), 0));
                }
                for (int i = 0; i < steps; i++) segments.Add((pts[i], pts[i+1], circle.Layer));
            }
            else if (entity is MetrajArc arc)
            {
                double sweep = arc.EndAngle - arc.StartAngle;
                if (sweep < 0) sweep += 360;
                int steps = (int)Math.Max(4, Math.Ceiling(sweep / 22.5));
                var pts = new List<MetrajPoint>();
                for (int i = 0; i <= steps; i++)
                {
                    double ang = (arc.StartAngle + sweep * i / steps) * Math.PI / 180.0;
                    pts.Add(new MetrajPoint(arc.Center.X + arc.Radius * Math.Cos(ang), arc.Center.Y + arc.Radius * Math.Sin(ang), 0));
                }
                for (int i = 0; i < steps; i++) segments.Add((pts[i], pts[i+1], arc.Layer));
            }
            else if (entity is MetrajEllipse ell)
            {
                int steps = 16;
                var pts = new List<MetrajPoint>();
                for (int i = 0; i <= steps; i++)
                {
                    double t = i * 2 * Math.PI / steps;
                    double x = ell.Center.X + ell.MajorAxisX * Math.Cos(t) - (ell.MajorAxisY * ell.Ratio) * Math.Sin(t);
                    double y = ell.Center.Y + ell.MajorAxisY * Math.Cos(t) + (ell.MajorAxisX * ell.Ratio) * Math.Sin(t);
                    pts.Add(new MetrajPoint(x, y, 0));
                }
                for (int i = 0; i < steps; i++) segments.Add((pts[i], pts[i+1], ell.Layer));
            }
        }
        return segments;
    }

    private Dictionary<MetrajPoint, List<(int Index, MetrajPoint Other)>> BuildConnectivityMap(
        List<(MetrajPoint P1, MetrajPoint P2, string Layer)> islandSegments, HashSet<int> usedSegments)
    {
        var map = new Dictionary<MetrajPoint, List<(int Index, MetrajPoint Other)>>(_pointComparer);
        for (int i = 0; i < islandSegments.Count; i++)
        {
            if (usedSegments.Contains(i)) continue;

            var s = islandSegments[i];
            if (!map.ContainsKey(s.P1)) map[s.P1] = new List<(int, MetrajPoint)>();
            if (!map.ContainsKey(s.P2)) map[s.P2] = new List<(int, MetrajPoint)>();

            map[s.P1].Add((i, s.P2));
            map[s.P2].Add((i, s.P1));
        }
        return map;
    }
}
