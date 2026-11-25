using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using netDxf;
using netDxf.Entities;
using netDxf.Units;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Bina.Services;

public partial class DxfKatPlaniParser
{
    public double UnitlessScaleMeters { get; set; } = 0.001;
    public string? UnitWarning { get; private set; }

    private static DxfDocument GeometriBelgesiniOku(string filePath)
    {
        var dxf = DxfDocument.Load(filePath) ?? throw new InvalidOperationException("DXF okunamadı.");
        // Bazı AutoCAD APPID kayıtları kendi kendilerine veya birbirlerine XData
        // ile referans verir. netDxf Insert.Explode -> XData.Clone bu grafiği
        // sınırsız kopyalayıp yakalanamayan StackOverflow ile süreci kapatır.
        // Bina okuyucusu APPID metadata'sını kullanmaz: yalnız bellekteki kayıtların
        // XData'sını kaldır. Nesne XData'sı, geometri, katmanlar ve kaynak dosya korunur.
        foreach (var registry in dxf.ApplicationRegistries)
            registry.XData.Clear();
        return dxf;
    }

    /// Paftayı oda taramasından önce okur. Bütün paftaya flood-fill uygulamak
    /// kesitleri ve anteti oda sayar; otomatik akış önce plan bölgelerini ayırır.
    public BinaKat HamVeriyiOku(string filePath)
    {
        var dxf = GeometriBelgesiniOku(filePath);
        double scale = HamOlcegiOku(dxf);
        var raw = ExtractRawSegments(dxf, scale);
        var detection = ExtractRawSegments(dxf, scale, includeAnnotationLayers: false);
        return new BinaKat {
            DxfFilePath = filePath, UnitWarning = UnitWarning,
            HamSegmentler = raw, HamTespitSegmentler = detection,
            HamMetinler = ExtractRawTexts(dxf, scale),
            DxfArclar = ExtractRawArcs(dxf, scale).Select(a => new BinaDxfArc {
                Merkez = a.Merkez, R = a.R, BasAciDeg = a.BasDeg, BitAciDeg = a.BitDeg }).ToList()
        };
    }

    private double HamOlcegiOku(DxfDocument dxf)
    {
        double scale = GetMetersPerDrawingUnit(dxf.DrawingVariables.InsUnits);
        if (dxf.DrawingVariables.InsUnits == DrawingUnits.Unitless)
        {
            var doorArcs = dxf.Entities.Arcs.Where(a =>
                Regex.IsMatch(a.Layer.Name, "KAPI|DOOR", RegexOptions.IgnoreCase) && a.Radius > 0).ToList();
            if (doorArcs.Count >= 3)
            {
                var candidates = new[] { 0.001, 0.01, 1.0 }.Select(s => (Scale: s,
                    Count: doorArcs.Count(a => a.Radius * s >= 0.55 && a.Radius * s <= 1.8)))
                    .OrderByDescending(x => x.Count).ToList();
                if (candidates[0].Count >= 3 && candidates[0].Count > candidates[1].Count * 2)
                {
                    scale = candidates[0].Scale;
                    UnitWarning = $"Birimsiz çizim: kapı katmanındaki {candidates[0].Count} yaydan ölçek önerildi (1 birim = {scale} m). Bilinen bir ölçüyle kontrol edin.";
                }
            }
        }
        return scale;
    }

    public BinaKat Parse(string filePath, int katNo, double katYuksekligiM, bool normalizeOrigin = true)
    {
        var dxf = GeometriBelgesiniOku(filePath);

        var kat = new BinaKat { KatNo = katNo, KatYuksekligiM = katYuksekligiM, DxfFilePath = filePath };
        var unitScale = GetMetersPerDrawingUnit(dxf.DrawingVariables.InsUnits);
        bool isUnitless = dxf.DrawingVariables.InsUnits == DrawingUnits.Unitless;

        var rawSegments = ExtractRawSegments(dxf, unitScale);
        var detectionSegments = ExtractRawSegments(dxf, unitScale, includeAnnotationLayers: false);
        var rawTexts    = ExtractRawTexts(dxf, unitScale);
        var rawArcs     = ExtractRawArcs(dxf, unitScale);
        // Oda tespiti annotation katmanlarını (YAZI/DIM/ÖLÇ…) duvar saymaz — birincil
        // geçiş de alternatif-ölçek geçişleriyle aynı seti kullanır (tutarlılık).
        var odalar      = DetectRoomsFromSpaces(
            DetectionSegmentsOrRaw(detectionSegments, rawSegments), rawTexts, rawArcs, katYuksekligiM);

        if (IsWeakRoomDetection(odalar, rawTexts))
        {
            var best = new DetectionCandidate(unitScale, rawSegments, detectionSegments, rawTexts, rawArcs, odalar);
            foreach (var altScale in AlternativeRoomScales(unitScale))
            {
                var altRawSegments = ExtractRawSegments(dxf, altScale);
                var altDetectionSegments = ExtractRawSegments(dxf, altScale, includeAnnotationLayers: false);
                if (!HasPlausibleFloorSpan(DetectionSegmentsOrRaw(altDetectionSegments, altRawSegments)))
                    continue;

                var altRawTexts = ExtractRawTexts(dxf, altScale);
                var altRawArcs = ExtractRawArcs(dxf, altScale);
                var altOdalar = DetectRoomsFromSpaces(
                    DetectionSegmentsOrRaw(altDetectionSegments, altRawSegments),
                    altRawTexts,
                    altRawArcs,
                    katYuksekligiM);

                var alt = new DetectionCandidate(altScale, altRawSegments, altDetectionSegments, altRawTexts, altRawArcs, altOdalar);
                if (IsBetterRoomDetection(alt, best))
                    best = alt;
            }

            if (Math.Abs(best.Scale - unitScale) > 1e-12)
            {
                unitScale = best.Scale;
                rawSegments = best.RawSegments;
                detectionSegments = best.DetectionSegments;
                rawTexts = best.RawTexts;
                rawArcs = best.RawArcs;
                odalar = best.Rooms;
                UnitWarning = BirlestirUyari(UnitWarning,
                    $"DXF ölçeği oda taramasına göre otomatik düzeltildi (1 birim = {unitScale * 100:0.##} cm).");
            }
        }

        if (odalar.Count > 0)
        {
            UnitWarning = BirlestirUyari(UnitWarning,
                $"Oda/mekan blokları boşluk tarama ile bulundu ({odalar.Count} oda). Sınırları gerekirse elle düzeltin.");
        }

        if (odalar.Count == 0)
        {
            odalar = DetectRoomsFromPolylines(dxf, katYuksekligiM, unitScale);

            if (odalar.Count == 0)
                odalar = DetectRoomsFromLines(dxf, katYuksekligiM, unitScale);

            if (odalar.Count == 0)
            {
                var textScale = EstimateScaleFromTexts(dxf) ?? unitScale;
                odalar = DetectRoomsFromTextLabels(dxf, katYuksekligiM, textScale);
                if (odalar.Count > 0)
                {
                    unitScale    = textScale;
                rawSegments  = ExtractRawSegments(dxf, unitScale);
                detectionSegments = ExtractRawSegments(dxf, unitScale, includeAnnotationLayers: false);
                rawTexts     = ExtractRawTexts(dxf, unitScale);
                rawArcs      = ExtractRawArcs(dxf, unitScale);
                SnapPolygonsToWallCoords(odalar, rawSegments, snapTol: 0.5);
                    UnitWarning = BirlestirUyari(UnitWarning,
                        "Oda geometrisi TEXT etiketlerinden oluşturuldu (duvarlar kapalı poligon değil). Sınırları manuel düzenleyebilirsiniz.");
                }
            }
        }

        if (odalar.Count == 0 && isUnitless)
        {
            foreach (var altScale in new[] { UnitlessScaleMeters * 10, UnitlessScaleMeters * 100 })
            {
                rawSegments = ExtractRawSegments(dxf, altScale);
                detectionSegments = ExtractRawSegments(dxf, altScale, includeAnnotationLayers: false);
                rawTexts    = ExtractRawTexts(dxf, altScale);
                rawArcs     = ExtractRawArcs(dxf, altScale);
                odalar      = DetectRoomsFromSpaces(rawSegments, rawTexts, rawArcs, katYuksekligiM);
                if (odalar.Count == 0)
                    odalar = DetectRoomsFromPolylines(dxf, katYuksekligiM, altScale);
                if (odalar.Count == 0)
                    odalar = DetectRoomsFromLines(dxf, katYuksekligiM, altScale);
                if (odalar.Count > 0)
                {
                    unitScale = altScale;
                    UnitWarning = $"DXF Unitless: ölçek otomatik algılandı (1 birim = {altScale * 100:0.##} cm). Yanlışsa Kat Yöneticisi'nden düzeltin.";
                    break;
                }
            }
        }

        MapRoomNamesFromTexts(odalar, rawTexts);

        kat.Odalar       = new ObservableCollection<BinaOda>(odalar);
        kat.HamSegmentler = rawSegments is List<(Point2D, Point2D)> l ? l : rawSegments.ToList();
        kat.HamMetinler  = rawTexts is List<(Point2D, string)> lt ? lt : rawTexts.ToList();
        kat.DxfArclar    = rawArcs
            .Select(a => new BinaDxfArc { Merkez = a.Merkez, R = a.R, BasAciDeg = a.BasDeg, BitAciDeg = a.BitDeg })
            .ToList();

        kat.Acikliklar = AciklikTespitServisi.Bul(kat.HamSegmentler, rawArcs, Array.Empty<BinaAciklik>());

        if (normalizeOrigin)
            NormalizeOrigin(kat);

        kat.DxfSegmentler = DxfSegmentYardimci.Sadelestir(kat.HamSegmentler)
            .Select(s => new BinaDxfSegment { A = s.A, B = s.B })
            .ToList();

        kat.UnitWarning = UnitWarning;
        return kat;
    }

    public static void NormalizeOrigin(BinaKat kat)
    {
        var polPts = kat.Odalar.SelectMany(m => m.Poligon).ToList();
        if (polPts.Count == 0) return;

        var minX = polPts.Min(p => p.X);
        var minY = polPts.Min(p => p.Y);
        if (Math.Abs(minX) < 1e-6 && Math.Abs(minY) < 1e-6) return;

        foreach (var oda in kat.Odalar)
        {
            oda.Poligon = oda.Poligon
                .Select(p => new Point2D(p.X - minX, p.Y - minY))
                .ToList();
        }

        foreach (var a in kat.Acikliklar)
        {
            a.Bas = new Point2D(a.Bas.X - minX, a.Bas.Y - minY);
            a.Bit = new Point2D(a.Bit.X - minX, a.Bit.Y - minY);
        }

        foreach (var arc in kat.DxfArclar)
            arc.Merkez = new Point2D(arc.Merkez.X - minX, arc.Merkez.Y - minY);

        kat.HamSegmentler = kat.HamSegmentler
            .Select(s => (new Point2D(s.A.X - minX, s.A.Y - minY),
                          new Point2D(s.B.X - minX, s.B.Y - minY)))
            .ToList();

        kat.HamMetinler = kat.HamMetinler
            .Select(t => (new Point2D(t.Pos.X - minX, t.Pos.Y - minY), t.Value))
            .ToList();
        kat.HamTespitSegmentler = kat.HamTespitSegmentler.Select(s =>
            (new Point2D(s.A.X - minX, s.A.Y - minY), new Point2D(s.B.X - minX, s.B.Y - minY))).ToList();
    }

    public static int OdalariBosluklardanYenidenTara(BinaKat kat)
    {
        var rawArcs = kat.DxfArclar
            .Select(a => (a.Merkez, a.R, a.BasAciDeg, a.BitAciDeg))
            .ToList();
        var odalar = DetectRoomsFromSpaces(kat.KaynakKatmanlari is not null || kat.HamTespitSegmentler.Count > 0 ? kat.HamTespitSegmentler : kat.HamSegmentler, kat.HamMetinler, rawArcs, kat.KatYuksekligiM);
        if (odalar.Count == 0) return 0;
        MapRoomNamesFromTexts(odalar, kat.HamMetinler);
        kat.Odalar = new ObservableCollection<BinaOda>(odalar);
        return odalar.Count;
    }

    public List<(Point2D A, Point2D B)> ExtractRawSegmentsFromFile(string filePath)
    {
        var dxf = GeometriBelgesiniOku(filePath);
        var unitScale = EstimateScaleFromTexts(dxf) ?? GetMetersPerDrawingUnit(dxf.DrawingVariables.InsUnits);
        return ExtractRawSegments(dxf, unitScale);
    }

    private sealed record DetectionCandidate(
        double Scale,
        List<(Point2D A, Point2D B)> RawSegments,
        List<(Point2D A, Point2D B)> DetectionSegments,
        List<(Point2D Pos, string Value)> RawTexts,
        List<(Point2D Merkez, double R, double BasDeg, double BitDeg)> RawArcs,
        List<BinaOda> Rooms);

    private static IReadOnlyList<(Point2D A, Point2D B)> DetectionSegmentsOrRaw(
        List<(Point2D A, Point2D B)> detectionSegments,
        List<(Point2D A, Point2D B)> rawSegments)
        => detectionSegments.Count > 0 ? detectionSegments : rawSegments;

    private static IEnumerable<double> AlternativeRoomScales(double unitScale)
    {
        yield return unitScale * 10.0;
        yield return unitScale * 100.0;
    }

    private static bool IsWeakRoomDetection(
        IReadOnlyList<BinaOda> rooms,
        IReadOnlyList<(Point2D Pos, string Value)> rawTexts)
    {
        if (rawTexts.Count < 3) return false;

        var totalArea = rooms.Sum(r => r.AlanM2 > 0 ? r.AlanM2 : CalculatePolygonArea(r.Poligon));
        return rooms.Count < 4 || totalArea < 50.0;
    }

    private static bool IsBetterRoomDetection(DetectionCandidate candidate, DetectionCandidate current)
    {
        var currentArea = current.Rooms.Sum(r => r.AlanM2 > 0 ? r.AlanM2 : CalculatePolygonArea(r.Poligon));
        var candidateArea = candidate.Rooms.Sum(r => r.AlanM2 > 0 ? r.AlanM2 : CalculatePolygonArea(r.Poligon));

        if (candidate.Rooms.Count >= current.Rooms.Count * 2 && candidateArea >= Math.Max(50.0, currentArea * 5.0))
            return true;

        if (currentArea < 50.0 && candidateArea >= Math.Max(50.0, currentArea * 5.0))
            return true;

        return current.Rooms.Count == 0 && candidate.Rooms.Count > 0;
    }

    private static bool HasPlausibleFloorSpan(IReadOnlyList<(Point2D A, Point2D B)> segments)
    {
        if (segments.Count == 0) return false;

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var (a, b) in segments)
        {
            minX = Math.Min(minX, Math.Min(a.X, b.X));
            minY = Math.Min(minY, Math.Min(a.Y, b.Y));
            maxX = Math.Max(maxX, Math.Max(a.X, b.X));
            maxY = Math.Max(maxY, Math.Max(a.Y, b.Y));
        }

        var maxSpan = Math.Max(maxX - minX, maxY - minY);
        return maxSpan is >= 20.0 and <= 300.0;
    }

    // ── Ham veri çıkarıcılar ──────────────────────────────────────────────────

    private static List<(Point2D Merkez, double R, double BasDeg, double BitDeg)> ExtractRawArcs(
        DxfDocument dxf, double unitScale)
    {
        var list = new List<(Point2D, double, double, double)>();

        void Ekle(double cx, double cy, double r, double bas, double bit)
            => list.Add((new Point2D(cx * unitScale, cy * unitScale), r * unitScale, bas, bit));

        foreach (var a in dxf.Entities.Arcs)
            Ekle(a.Center.X, a.Center.Y, a.Radius, a.StartAngle, a.EndAngle);

        void Visit(Insert ins, int depth)
        {
            if (depth > 16) return;
            foreach (var e in ins.Explode())
                if (e is Arc aa) Ekle(aa.Center.X, aa.Center.Y, aa.Radius, aa.StartAngle, aa.EndAngle);
                else if (e is Insert nested) Visit(nested, depth + 1);
        }
        foreach (var ins in dxf.Entities.Inserts) Visit(ins, 0);

        return list;
    }

    private static List<(Point2D Pos, string Value)> ExtractRawTexts(DxfDocument dxf, double unitScale)
    {
        var list = new List<(Point2D, string)>();

        void Ekle(double x, double y, string? raw)
        {
            var v = TemizleMetin(raw);
            if (!string.IsNullOrWhiteSpace(v))
                list.Add((new Point2D(x * unitScale, y * unitScale), v));
        }

        foreach (var t in dxf.Entities.Texts)  Ekle(t.Position.X, t.Position.Y, t.Value);
        foreach (var t in dxf.Entities.MTexts) Ekle(t.Position.X, t.Position.Y, t.Value);

        void Visit(Insert ins, int depth)
        {
            if (depth > 16) return;
            foreach (var attribute in ins.Attributes)
                Ekle(attribute.Position.X, attribute.Position.Y, attribute.Value);
            foreach (var e in ins.Explode())
            {
                if (e is Text tt) Ekle(tt.Position.X, tt.Position.Y, tt.Value);
                else if (e is MText mt) Ekle(mt.Position.X, mt.Position.Y, mt.Value);
                else if (e is Insert nested) Visit(nested, depth + 1);
            }
        }
        foreach (var ins in dxf.Entities.Inserts) Visit(ins, 0);

        return list;
    }

    private static string TemizleMetin(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var v = s;
        v = v.Replace("\\P", " ");
        v = Regex.Replace(v, @"\\[A-Za-z][^;\\]*;", "");
        v = Regex.Replace(v, @"\\[A-Za-z]", "");
        v = v.Replace("{", "").Replace("}", "").Replace("\\", "");
        v = Regex.Replace(v, @"\s+", " ").Trim();
        return v;
    }

    private static List<(Point2D A, Point2D B)> ExtractRawSegments(
        DxfDocument dxf,
        double unitScale,
        bool includeAnnotationLayers = true)
    {
        var segs = new List<(Point2D, Point2D)>();

        void AddPolyPointsV2(IReadOnlyList<Vector2> pts, bool isClosed)
        {
            if (pts == null || pts.Count < 2) return;
            for (int i = 0; i < pts.Count - 1; i++)
                segs.Add((new Point2D(pts[i].X * unitScale, pts[i].Y * unitScale),
                          new Point2D(pts[i + 1].X * unitScale, pts[i + 1].Y * unitScale)));
            if (isClosed && pts.Count > 2)
                segs.Add((new Point2D(pts[^1].X * unitScale, pts[^1].Y * unitScale),
                          new Point2D(pts[0].X * unitScale, pts[0].Y * unitScale)));
        }

        void AddPolyPointsV3(IReadOnlyList<Vector3> pts, bool isClosed)
        {
            if (pts == null || pts.Count < 2) return;
            for (int i = 0; i < pts.Count - 1; i++)
                segs.Add((new Point2D(pts[i].X * unitScale, pts[i].Y * unitScale),
                          new Point2D(pts[i + 1].X * unitScale, pts[i + 1].Y * unitScale)));
            if (isClosed && pts.Count > 2)
                segs.Add((new Point2D(pts[^1].X * unitScale, pts[^1].Y * unitScale),
                          new Point2D(pts[0].X * unitScale, pts[0].Y * unitScale)));
        }

        void ProcessEntities(IEnumerable<EntityObject> entities, int depth = 0)
        {
            if (depth > 16) return;
            foreach (var ent in entities)
            {
                if (!includeAnnotationLayers && IsAnnotationLayer(ent.Layer?.Name))
                    continue;

                if (ent is Line l)
                {
                    segs.Add((
                        new Point2D(l.StartPoint.X * unitScale, l.StartPoint.Y * unitScale),
                        new Point2D(l.EndPoint.X   * unitScale, l.EndPoint.Y   * unitScale)));
                }
                else if (ent is Polyline2D p)
                {
                    var verts = p.Vertexes.Select(v => new Point2D(v.Position.X * unitScale, v.Position.Y * unitScale)).ToList();
                    for (int i = 0; i < verts.Count - 1; i++)
                        segs.Add((verts[i], verts[i + 1]));
                    if (p.IsClosed && verts.Count > 2)
                        segs.Add((verts[^1], verts[0]));
                }
                else if (ent is Arc a)    AddPolyPointsV2(a.PolygonalVertexes(16), false);
                else if (ent is Circle c) AddPolyPointsV2(c.PolygonalVertexes(16), true);
                else if (ent is Ellipse e) AddPolyPointsV2(e.PolygonalVertexes(16), true);
                else if (ent is Spline sp) AddPolyPointsV3(sp.PolygonalVertexes(16), false);
                else if (ent is Insert i)
                {
                    ProcessEntities(i.Explode(), depth + 1);
                }
            }
        }

        ProcessEntities(dxf.Entities.All);
        return segs;
    }

    // ── Oda tespiti ───────────────────────────────────────────────────────────

    private static List<BinaOda> DetectRoomsFromSpaces(
        IReadOnlyList<(Point2D A, Point2D B)> rawSegments,
        IReadOnlyList<(Point2D Pos, string Value)> rawTexts,
        IReadOnlyList<(Point2D Merkez, double R, double BasDeg, double BitDeg)> rawArcs,
        double katYuksekligiM)
    {
        if (rawSegments.Count == 0) return new();

        var acikliklar = AciklikTespitServisi.Bul(rawSegments, rawArcs, Array.Empty<BinaAciklik>());
        var sanalDuvarlar = acikliklar
            .Where(a => !a.IptalEdildi)
            .Select(a => (a.Bas, a.Bit))
            .ToList();

        var ayar = sanalDuvarlar.Count > 0
            ? new BosluktanOdaTespitServisi.Ayarlar { KapiKapatmaM = 0.20 }
            : null;

        var poligonlar = BosluktanOdaTespitServisi.OdalariBul(rawSegments, ayar, sanalDuvarlar);
        return OdalarıPoligonlardanOlustur(poligonlar, rawTexts);
    }

    private static List<BinaOda> OdalarıPoligonlardanOlustur(
        IReadOnlyList<List<Point2D>> poligonlar,
        IReadOnlyList<(Point2D Pos, string Value)> rawTexts)
    {
        var odalar = new List<BinaOda>();
        foreach (var poly in poligonlar)
        {
            if (poly.Count < 3) continue;
            var alan = CalculatePolygonArea(poly);
            if (alan < 0.5) continue;

            var ad  = OdaAdiSec(poly, rawTexts) ?? $"Oda {odalar.Count + 1}";
            var oda = new BinaOda
            {
                Ad     = ad,
                AlanM2 = alan,
                Poligon = poly.Select(p => new Point2D(p.X, p.Y)).ToList()
            };
            odalar.Add(oda);
        }
        return odalar;
    }

    private static void MapRoomNamesFromTexts(
        IReadOnlyList<BinaOda> odalar,
        IReadOnlyList<(Point2D Pos, string Value)> rawTexts)
    {
        foreach (var oda in odalar)
        {
            var ad = OdaAdiSec(oda.Poligon, rawTexts);
            if (!string.IsNullOrWhiteSpace(ad))
                oda.Ad = ad.Trim();
        }
    }

    // ── TEXT etiketleri son fallback ──────────────────────────────────────────

    private static readonly Regex AreaRegex =
        new(@"(\d+[.,]\d+)\s*m2", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static double? EstimateScaleFromTexts(DxfDocument dxf)
    {
        // Oda etiketleri TEXT veya MTEXT olabilir — yalnız TEXT okunursa MTEXT
        // etiketli dosyalarda ölçek tahmini hiç çalışmazdı.
        var rawPositions = dxf.Entities.Texts
            .Select(t => (t.Position.X, t.Position.Y, Value: t.Value ?? ""))
            .Concat(dxf.Entities.MTexts
                .Select(t => (t.Position.X, t.Position.Y, Value: t.Value ?? "")))
            .Where(t => MimariOdaEtiketServisi.ContainsRoomName(t.Value))
            .Select(t => (t.X, t.Y))
            .ToList();

        if (rawPositions.Count < 2) return null;

        double rawW    = rawPositions.Max(p => p.X) - rawPositions.Min(p => p.X);
        double rawH    = rawPositions.Max(p => p.Y) - rawPositions.Min(p => p.Y);
        double rawSpan = Math.Max(rawW, rawH);

        if (rawSpan <= 0) return null;

        double scale = 30.0 / rawSpan;
        double[] candidates = { 0.0001, 0.001, 0.01, 0.1, 1.0 };
        return candidates.MinBy(c => Math.Abs(Math.Log10(c) - Math.Log10(scale)));
    }

    private List<BinaOda> DetectRoomsFromTextLabels(
        DxfDocument dxf,
        double katYuksekligiM,
        double unitScale)
    {
        var allTexts = dxf.Entities.Texts
            .Select(t => (X: t.Position.X * unitScale, Y: t.Position.Y * unitScale, Val: t.Value ?? ""))
            .Concat(dxf.Entities.MTexts
                .Select(t => (X: t.Position.X * unitScale, Y: t.Position.Y * unitScale, Val: t.Value ?? "")))
            .ToList();

        var roomTexts = allTexts
            .Where(t => MimariOdaEtiketServisi.ContainsRoomName(t.Val))
            .ToList();

        var areaTexts = allTexts
            .Select(t => (t.X, t.Y, Match: AreaRegex.Match(t.Val)))
            .Where(t => t.Match.Success)
            .Select(t => (t.X, t.Y, Area: double.Parse(
                t.Match.Groups[1].Value.Replace(',', '.'),
                System.Globalization.CultureInfo.InvariantCulture)))
            .ToList();

        var odalar = new List<BinaOda>();

        foreach (var (rx, ry, rName) in roomTexts)
        {
            double area = 10.0;
            double minDist = 5.0;
            foreach (var (ax, ay, a) in areaTexts)
            {
                double d = Math.Sqrt((rx - ax) * (rx - ax) + (ry - ay) * (ry - ay));
                if (d < minDist) { minDist = d; area = a; }
            }

            var polygon = BuildEstimatedRoomRectangle(rx, ry, area, roomTexts);
            odalar.Add(new BinaOda { Ad = rName.Trim(), AlanM2 = area, Poligon = polygon });
        }

        if (odalar.Count > 0)
        {
            double snapTol = 1.0;
            var xs = odalar.SelectMany(m => m.Poligon.Select(p => p.X)).OrderBy(x => x).ToList();
            var ys = odalar.SelectMany(m => m.Poligon.Select(p => p.Y)).OrderBy(y => y).ToList();

            double[] GridCenters(List<double> sorted)
            {
                var centers = new List<double>();
                double sum = sorted[0]; int cnt = 1;
                for (int i = 1; i < sorted.Count; i++)
                {
                    if (sorted[i] - sorted[i - 1] <= snapTol) { sum += sorted[i]; cnt++; }
                    else { centers.Add(sum / cnt); sum = sorted[i]; cnt = 1; }
                }
                centers.Add(sum / cnt);
                return [.. centers];
            }

            var xGrid = GridCenters(xs);
            var yGrid = GridCenters(ys);

            double SnapTo(double v, double[] grid)
            {
                double best = v, bestD = snapTol;
                foreach (var c in grid) { double d = Math.Abs(v - c); if (d < bestD) { bestD = d; best = c; } }
                return best;
            }

            foreach (var oda in odalar)
            {
                var snapped = oda.Poligon
                    .Select(p => new Point2D(SnapTo(p.X, xGrid), SnapTo(p.Y, yGrid)))
                    .ToList();
                if (CalculatePolygonArea(snapped) > 1.0)
                {
                    oda.Poligon = snapped;
                    oda.AlanM2  = CalculatePolygonArea(snapped);
                }
            }
        }

        foreach (var oda in odalar)
        {
            if (CalculatePolygonArea(oda.Poligon) > 0.5)
                oda.AlanM2 = CalculatePolygonArea(oda.Poligon);
        }

        return odalar;
    }

    private static List<Point2D> BuildEstimatedRoomRectangle(
        double rx, double ry, double area,
        List<(double X, double Y, string Val)> roomTexts)
    {
        double right = double.MaxValue, left = double.MaxValue,
               up    = double.MaxValue, down = double.MaxValue;
        foreach (var (ox, oy, _) in roomTexts)
        {
            double dx = ox - rx, dy = oy - ry;
            if (Math.Abs(dx) < 0.01 && Math.Abs(dy) < 0.01) continue;
            if (Math.Abs(dx) > Math.Abs(dy))
            {
                if (dx > 0) right = Math.Min(right, dx);
                else        left  = Math.Min(left, -dx);
            }
            else
            {
                if (dy > 0) up   = Math.Min(up,   dy);
                else        down = Math.Min(down, -dy);
            }
        }

        double targetHalf = Math.Sqrt(area) / 2.0;
        double hRight = Math.Min(targetHalf, double.IsInfinity(right) ? targetHalf : right * 0.5);
        double hLeft  = Math.Min(targetHalf, double.IsInfinity(left)  ? targetHalf : left  * 0.5);
        double hUp    = Math.Min(targetHalf, double.IsInfinity(up)    ? targetHalf : up    * 0.5);
        double hDown  = Math.Min(targetHalf, double.IsInfinity(down)  ? targetHalf : down  * 0.5);

        return
        [
            new(rx - hLeft,  ry - hDown),
            new(rx + hRight, ry - hDown),
            new(rx + hRight, ry + hUp),
            new(rx - hLeft,  ry + hUp)
        ];
    }

    // ── Polyline2D tabanlı oda tespiti ────────────────────────────────────────

    private List<BinaOda> DetectRoomsFromPolylines(DxfDocument dxf, double katYuksekligiM, double unitScale)
    {
        var odalar = new List<BinaOda>();
        var closedPolylines = dxf.Entities.Polylines2D.Where(p => p.IsClosed).ToList();
        var hasRoomLayer = closedPolylines.Any(p => IsRoomLayer(p.Layer.Name));

        foreach (var poly in closedPolylines.Where(p => !hasRoomLayer || IsRoomLayer(p.Layer.Name)))
        {
            var polygon = GetPolygon(poly, unitScale);
            var area    = CalculatePolygonArea(polygon);
            if (area < 0.5) continue;

            double minX = polygon.Min(p => p.X), maxX = polygon.Max(p => p.X);
            double minY = polygon.Min(p => p.Y), maxY = polygon.Max(p => p.Y);
            if (Math.Min(maxX - minX, maxY - minY) < 0.6) continue;

            odalar.Add(new BinaOda
            {
                Ad      = $"Oda {odalar.Count + 1}",
                AlanM2  = area,
                Poligon = polygon
            });
        }
        return odalar;
    }

    // ── LINE+ARC segmentlerinden kapalı poligon tespiti ───────────────────────

    private List<BinaOda> DetectRoomsFromLines(DxfDocument dxf, double katYuksekligiM, double unitScale)
    {
        const double snapTol  = 0.05;
        const double minLen   = 0.10;
        const int    maxSegs  = 6000;

        bool hasWallLayer = dxf.Entities.Lines.Any(l => IsWallLayer(l.Layer.Name))
                         || dxf.Entities.Arcs.Any(a  => IsWallLayer(a.Layer.Name));

        var segments = dxf.Entities.Lines
            .Where(l => !hasWallLayer || IsWallLayer(l.Layer.Name))
            .Select(l => (
                A: new Point2D(l.StartPoint.X * unitScale, l.StartPoint.Y * unitScale),
                B: new Point2D(l.EndPoint.X   * unitScale, l.EndPoint.Y   * unitScale)
            ))
            .Where(s => Distance(s.A, s.B) >= minLen)
            .ToList();

        var arcSegs = dxf.Entities.Arcs
            .Where(a => !hasWallLayer || IsWallLayer(a.Layer.Name))
            .Select(a =>
            {
                double sr = a.StartAngle * Math.PI / 180.0;
                double er = a.EndAngle   * Math.PI / 180.0;
                var aStart = new Point2D(
                    (a.Center.X + a.Radius * Math.Cos(sr)) * unitScale,
                    (a.Center.Y + a.Radius * Math.Sin(sr)) * unitScale);
                var aEnd = new Point2D(
                    (a.Center.X + a.Radius * Math.Cos(er)) * unitScale,
                    (a.Center.Y + a.Radius * Math.Sin(er)) * unitScale);
                return (A: aStart, B: aEnd);
            })
            .Where(s => Distance(s.A, s.B) >= minLen)
            .ToList();

        var allSegs = segments.Concat(arcSegs).Take(maxSegs).ToList();
        if (allSegs.Count == 0) return new List<BinaOda>();

        var nodes    = new List<Point2D>();
        var nodeGrid = new Dictionary<(int, int), List<int>>();
        var edges    = new List<(int A, int B)>();
        double cellSize = snapTol * 2.0;

        (int cx, int cy) CellOf(Point2D p) =>
            ((int)Math.Floor(p.X / cellSize), (int)Math.Floor(p.Y / cellSize));

        int GetOrAddNode(Point2D p)
        {
            var (cx, cy) = CellOf(p);
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (nodeGrid.TryGetValue((cx + dx, cy + dy), out var bucket))
                    foreach (int idx in bucket)
                        if (Distance(nodes[idx], p) <= snapTol) return idx;
            }
            int newIdx = nodes.Count;
            nodes.Add(p);
            var cell = CellOf(p);
            if (!nodeGrid.ContainsKey(cell)) nodeGrid[cell] = new List<int>();
            nodeGrid[cell].Add(newIdx);
            return newIdx;
        }

        foreach (var (a, b) in allSegs)
        {
            int ia = GetOrAddNode(a);
            int ib = GetOrAddNode(b);
            if (ia != ib) edges.Add((ia, ib));
        }

        var adj = new Dictionary<int, List<int>>();
        foreach (var (a, b) in edges)
        {
            if (!adj.ContainsKey(a)) adj[a] = new List<int>();
            if (!adj.ContainsKey(b)) adj[b] = new List<int>();
            adj[a].Add(b);
            adj[b].Add(a);
        }

        var visited = new HashSet<(int, int)>();
        var odalar  = new List<BinaOda>();

        foreach (var startNode in adj.Keys)
        {
            foreach (var nextNode in adj[startNode])
            {
                var edgeKey = (Math.Min(startNode, nextNode), Math.Max(startNode, nextNode));
                if (visited.Contains(edgeKey)) continue;

                var cycle = FindCycle(startNode, nextNode, adj, visited);
                if (cycle == null || cycle.Count < 3) continue;

                var polygon = cycle.Select(n => nodes[n]).ToList();
                var area    = CalculatePolygonArea(polygon);
                if (area < 0.5) continue;

                odalar.Add(new BinaOda
                {
                    Ad      = $"Oda {odalar.Count + 1}",
                    AlanM2  = area,
                    Poligon = polygon
                });
            }
        }
        return odalar;
    }

    private static List<int>? FindCycle(int start, int second, Dictionary<int, List<int>> adj,
        HashSet<(int, int)> visitedEdges)
    {
        var prev  = new Dictionary<int, int> { [start] = -1, [second] = start };
        var queue = new Queue<int>();
        queue.Enqueue(second);

        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            foreach (int next in adj.GetValueOrDefault(cur) ?? new List<int>())
            {
                if (next == start && cur != second)
                {
                    var cycle = new List<int> { start };
                    int n = cur;
                    while (n != start) { cycle.Add(n); n = prev[n]; }
                    cycle.Reverse();

                    for (int i = 0; i < cycle.Count; i++)
                    {
                        int a = cycle[i], b = cycle[(i + 1) % cycle.Count];
                        visitedEdges.Add((Math.Min(a, b), Math.Max(a, b)));
                    }
                    return cycle;
                }
                if (!prev.ContainsKey(next))
                {
                    prev[next] = cur;
                    queue.Enqueue(next);
                }
            }
        }
        return null;
    }

    // ── Yardımcılar ───────────────────────────────────────────────────────────

    private static void SnapPolygonsToWallCoords(
        List<BinaOda> odalar,
        List<(Point2D A, Point2D B)> rawSegs,
        double snapTol = 0.5)
    {
        var (hCenters, vCenters) = ExtractWallCoordClusters(rawSegs);

        double Nearest(double v, double[] centers, double tol)
        {
            double best = v, bestD = tol;
            foreach (var c in centers) { double d = Math.Abs(v - c); if (d < bestD) { bestD = d; best = c; } }
            return best;
        }

        foreach (var oda in odalar)
        {
            var snapped = oda.Poligon
                .Select(p => new Point2D(
                    Nearest(p.X, vCenters, snapTol),
                    Nearest(p.Y, hCenters, snapTol)))
                .ToList();

            if (CalculatePolygonArea(snapped) > 0.5)
            {
                oda.Poligon = snapped;
                oda.AlanM2  = CalculatePolygonArea(snapped);
            }
        }
    }

    private static (double[] HorizontalYs, double[] VerticalXs) ExtractWallCoordClusters(
        List<(Point2D A, Point2D B)> rawSegs)
    {
        const double minLen  = 0.4;
        const double orthTol = 0.08;
        const double clTol   = 0.15;

        var hYs = new List<double>();
        var vXs = new List<double>();
        foreach (var (a, b) in rawSegs)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < minLen) continue;
            if (Math.Abs(dy) / len < orthTol)
                hYs.Add((a.Y + b.Y) * 0.5);
            else if (Math.Abs(dx) / len < orthTol)
                vXs.Add((a.X + b.X) * 0.5);
        }

        static double[] Cluster(List<double> vals, double tol)
        {
            if (vals.Count == 0) return [];
            var sorted = vals.OrderBy(v => v).ToList();
            var centers = new List<double>();
            double sum = sorted[0]; int cnt = 1;
            for (int i = 1; i < sorted.Count; i++)
            {
                if (sorted[i] - sorted[i - 1] <= tol) { sum += sorted[i]; cnt++; }
                else { centers.Add(sum / cnt); sum = sorted[i]; cnt = 1; }
            }
            centers.Add(sum / cnt);
            return [.. centers];
        }

        return (Cluster(hYs, clTol), Cluster(vXs, clTol));
    }

    private static string? OdaAdiSec(
        IReadOnlyList<Point2D> poly,
        IReadOnlyList<(Point2D Pos, string Value)> metinler)
    {
        if (metinler.Count == 0) return null;
        var merkez = new Point2D(poly.Average(p => p.X), poly.Average(p => p.Y));

        string? enIyi = null;
        double enYakin = double.MaxValue;
        int enIyiPuan = -1;
        foreach (var (pos, value) in metinler)
        {
            var etiket = MimariOdaEtiketServisi.Degerlendir(value);
            if (etiket is null) continue;
            if (!PoligonYardimci.IsPointInside(pos, poly)) continue;
            double dx = pos.X - merkez.X, dy = pos.Y - merkez.Y;
            double d = dx * dx + dy * dy;
            if (etiket.Puan > enIyiPuan || (etiket.Puan == enIyiPuan && d < enYakin))
            { enYakin = d; enIyiPuan = etiket.Puan; enIyi = etiket.Ad; }
        }
        return enIyi;
    }

    private static bool IsimGibiMi(string s)
        => MimariOdaEtiketServisi.TryName(s, out _);

    private List<Point2D> GetPolygon(Polyline2D poly, double unitScale) =>
        poly.Vertexes.Select(v => new Point2D(v.Position.X * unitScale, v.Position.Y * unitScale)).ToList();

    private static double CalculatePolygonArea(List<Point2D> pts)
    {
        double area = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            var p1 = pts[i]; var p2 = pts[(i + 1) % pts.Count];
            area += p1.X * p2.Y - p2.X * p1.Y;
        }
        return Math.Abs(area) / 2.0;
    }

    private static double Distance(Point2D a, Point2D b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static bool IsWallLayer(string? name)
    {
        var s = (name ?? "").ToUpperInvariant().Replace('ı', 'I').Replace('İ', 'I').Replace('Ş', 'S').Replace('Ö', 'O').Replace('Ü', 'U').Replace('Ç', 'C').Replace('Ğ', 'G');
        return s.Contains("DUVAR") || s.Contains("WALL") || s.Contains("A-WALL");
    }

    private static bool IsAnnotationLayer(string? name)
    {
        var s = (name ?? "").ToUpperInvariant().Replace('ı', 'I').Replace('İ', 'I').Replace('Ş', 'S').Replace('Ö', 'O').Replace('Ü', 'U').Replace('Ç', 'C').Replace('Ğ', 'G');
        if (string.IsNullOrWhiteSpace(s)) return false;

        return s.Contains("TEFR") || s.Contains("TARAMA") || s.Contains("HATCH") || s.Contains("FURN") || s.Contains("ESYA") || s.Contains("AKS") || s == "KOT" || s.Contains("MERDIVEN") || s.Contains("YAZI")
            || s.Contains("TEXT")
            || s.Contains("MTEXT")
            || s.Contains("DIM")
            || s.Contains("ÖLÇ")
            || s.Contains("OLC")
            || s.Contains("NOT")
            || s.Contains("GORUNUS")
            || s.Contains("GÖRÜNÜŞ");
    }

    private static bool IsRoomLayer(string? name)
    {
        var s = (name ?? "").ToUpperInvariant().Replace('ı', 'I').Replace('İ', 'I').Replace('Ş', 'S').Replace('Ö', 'O').Replace('Ü', 'U').Replace('Ç', 'C').Replace('Ğ', 'G');
        return s.Contains("ODA") || s.Contains("ROOM") || s.Contains("A-ROOM");
    }

    private static string? BirlestirUyari(string? a, string b)
        => string.IsNullOrWhiteSpace(a) ? b : $"{a} {b}";

    private double GetMetersPerDrawingUnit(DrawingUnits units)
    {
        if (units == DrawingUnits.Unitless)
        {
            UnitWarning = "DXF InsUnits=Unitless. Varsayılan ölçek: 1 birim = 1 mm. Gerekirse OriginOffset ile hizalayın.";
            return UnitlessScaleMeters;
        }
        UnitWarning = null;
        return units switch
        {
            DrawingUnits.Millimeters  => 0.001,
            DrawingUnits.Centimeters  => 0.01,
            DrawingUnits.Decimeters   => 0.1,
            DrawingUnits.Meters       => 1.0,
            DrawingUnits.Kilometers   => 1000.0,
            DrawingUnits.Inches       => 0.0254,
            DrawingUnits.Feet         => 0.3048,
            DrawingUnits.Yards        => 0.9144,
            DrawingUnits.Miles        => 1609.344,
            DrawingUnits.USSurveyFeet => 1200.0 / 3937.0,
            _ => UnitlessScaleMeters
        };
    }
}
