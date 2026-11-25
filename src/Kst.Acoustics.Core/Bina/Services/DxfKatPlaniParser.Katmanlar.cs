using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Common.Geometry;
using netDxf;
using netDxf.Entities;
using System.Text.RegularExpressions;

namespace Kst.Acoustics.Core.Bina.Services;

public partial class DxfKatPlaniParser
{
    public MimariKatmanliCizim KatmanliOku(string filePath)
    {
        var dxf = GeometriBelgesiniOku(filePath);
        double scale = HamOlcegiOku(dxf);
        var layers = dxf.Layers.ToDictionary(l => l.Name, l =>
            new MimariCizimKatmani(l.Name, 0, l.IsVisible && !l.IsFrozen), StringComparer.OrdinalIgnoreCase);
        var ogeler = new List<MimariKatmanliCizim.KatmanliOge>();

        string EffectiveLayer(string? own, string? inherited)
            => string.IsNullOrEmpty(own) || (own == "0" && inherited is not null) ? inherited ?? "0" : own;
        void Count(string layer)
        {
            if (!layers.TryGetValue(layer, out var current)) current = new(layer, 0, true);
            layers[layer] = current with { NesneSayisi = current.NesneSayisi + 1 };
        }
        MimariKatmanliCizim.KatmanliOge NewItem(string layer, IReadOnlyList<string> parents)
        {
            var requirements = parents.Append(layer).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var item = new MimariKatmanliCizim.KatmanliOge {
                GerekliKatmanlar = requirements,
                TespitIcin = !requirements.Any(IsAnnotationLayer)
            };
            ogeler.Add(item); Count(layer); return item;
        }
        Point2D P(double x, double y) => new(x * scale, y * scale);
        void TextItem(string? raw, Point2D pos, string layer, IReadOnlyList<string> parents)
        {
            var value = TemizleMetin(raw);
            if (!string.IsNullOrWhiteSpace(value)) NewItem(layer, parents).Metinler.Add((pos, value));
        }
        void Visit(IEnumerable<EntityObject> entities, string? inherited, IReadOnlyList<string> parents, int depth)
        {
            if (depth > 16) throw new InvalidOperationException("CAD blok iç içe geçme sınırı aşıldı (16). Çizimi sadeleştirin.");
            foreach (var entity in entities)
            {
                // Nesneye özel görünmezlik katman düğmesiyle açılmaz. Katman
                // kapalı/donmuş olsa da içerik tutulur; kullanıcı açabilir.
                if (!entity.IsVisible) continue;
                string layer = EffectiveLayer(entity.Layer?.Name, inherited);
                if (entity is Insert insert)
                {
                    Count(layer);
                    var required = parents.Append(layer).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                    // netDxf Explode, attribute değerlerini dönüşmüş Text olarak
                    // döndürür; ayrıca eklemek aynı oda/kot etiketini iki kez sayar.
                    Visit(insert.Explode(), layer, required, depth + 1);
                    continue;
                }
                if (entity is Text text) { TextItem(text.Value, P(text.Position.X, text.Position.Y), layer, parents); continue; }
                if (entity is MText mtext) { TextItem(mtext.Value, P(mtext.Position.X, mtext.Position.Y), layer, parents); continue; }
                var oge = NewItem(layer, parents);
                void Poly(IEnumerable<Vector2> vertices, bool closed)
                {
                    var points = vertices.Select(v => P(v.X, v.Y)).ToList();
                    for (int i = 1; i < points.Count; i++) oge.Segmentler.Add((points[i - 1], points[i]));
                    if (closed && points.Count > 2) oge.Segmentler.Add((points[^1], points[0]));
                }
                switch (entity)
                {
                    case Line l: oge.Segmentler.Add((P(l.StartPoint.X, l.StartPoint.Y), P(l.EndPoint.X, l.EndPoint.Y))); break;
                    case Polyline2D p: Poly(p.PolygonalVertexes(16), p.IsClosed); break;
                    case Polyline3D p: Poly(p.Vertexes.Select(v => new Vector2(v.X, v.Y)), p.IsClosed); break;
                    case Arc arc:
                        Poly(arc.PolygonalVertexes(16), false);
                        oge.Arclar.Add(new BinaDxfArc { Merkez = P(arc.Center.X, arc.Center.Y), R = arc.Radius * scale,
                            BasAciDeg = arc.StartAngle, BitAciDeg = arc.EndAngle }); break;
                    case Circle c: Poly(c.PolygonalVertexes(16), true); break;
                    case Ellipse e: Poly(e.PolygonalVertexes(16), e.IsFullEllipse); break;
                    case Spline s: Poly(s.PolygonalVertexes(16).Select(v => new Vector2(v.X, v.Y)), s.IsClosed); break;
                }
            }
        }
        Visit(dxf.Entities.All, null, Array.Empty<string>(), 0);
        foreach (var oge in ogeler)
        {
            int olcuSayisi = oge.Metinler.Count(t =>
                Regex.IsMatch(t.Value.Trim(), @"^(?:[+±-]\s*\d|(?:(?:TABAN|DÖŞEME|DOSEME|KAT)\s+)?KOT|KAT\s+Y[ÜU]K|H\s*=)", RegexOptions.IgnoreCase));
            if (olcuSayisi == 0) continue;
            foreach (var ad in oge.GerekliKatmanlar)
                if (layers.TryGetValue(ad, out var layer))
                    layers[ad] = layer with { KotYukseklikEtiketiSayisi = layer.KotYukseklikEtiketiSayisi + olcuSayisi };
        }
        return new(filePath, UnitWarning, layers.Values.OrderBy(l => l.Ad, StringComparer.OrdinalIgnoreCase).ToArray(), ogeler);
    }
}
