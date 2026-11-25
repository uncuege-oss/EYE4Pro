using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Salon.Models;
using static Kst.Acoustics.Core.Salon.Services.SalonTasarimServisi;

namespace Kst.Acoustics.Core.Salon.Services;

public static class SalonTasarimMeshServisi
{
    public sealed record Sonuc(List<ProjectSurface> Yuzeyler, double Hacim);
    static List<List<Point2D>> Simple(List<List<Point2D>> p) => SalonPoligonServisi.BasitPoligonlaraAyir(p);
    static List<IReadOnlyList<Point2D>> Set(List<Point2D> p) => new() { p };
    static double Integral(List<Point2D> p, Func<Point2D, double> z)
    {
        double area = 0, mx = 0, my = 0;
        for (int i = 0; i < p.Count; i++) { var a = p[i]; var b = p[(i + 1) % p.Count]; double c = a.X * b.Y - b.X * a.Y; area += c; mx += (a.X + b.X) * c; my += (a.Y + b.Y) * c; }
        if (Math.Abs(area) < 1e-12) return 0;
        return Math.Abs(area / 2) * z(new(mx / (3 * area), my / (3 * area)));
    }

    public static List<Panel> Tavanlar(SalonGeometri s, List<List<Point2D>> shell)
    {
        var result = new List<Panel>(); var remaining = shell;
        foreach (var o in s.CizimOgeleri.Where(o => o.CiktiyaDahil && o.Islev == SalonIslevi.Tavan).Reverse())
        {
            var p = o.KapaliPoligon(); var region = ClipperAdaptor.Fark(Kirp(remaining, p), o.IcBosluklar); foreach (var cut in Simple(region)) result.Add(new(cut, 0, o.KotM, "Tavan", o.Id, o.EgimX, o.EgimY, o.KotReferansi));
            remaining = ClipperAdaptor.Fark(remaining, region);
        }
        if (remaining.Count == 0) return result;
        double minX = remaining.SelectMany(p => p).Min(p => p.X) - 1, maxX = remaining.SelectMany(p => p).Max(p => p.X) + 1;
        var ys = s.TavanProfili.Select(p => p.YM).Concat(new[] { remaining.SelectMany(p => p).Min(p => p.Y) - 1, remaining.SelectMany(p => p).Max(p => p.Y) + 1 }).Distinct().Order().ToList();
        for (int i = 0; i + 1 < ys.Count; i++)
        {
            double a = ys[i], b = ys[i + 1], z = SalonTavanServisi.Kot(s, a), slope = (SalonTavanServisi.Kot(s, b) - z) / (b - a);
            var band = new List<Point2D> { new(minX, a), new(maxX, a), new(maxX, b), new(minX, b) };
            foreach (var cut in Simple(Kirp(remaining, band))) result.Add(new(cut, 0, z, "Tavan", "", 0, slope, new(0, a)));
        }
        return result;
    }

    public static Sonuc Uret(SalonGeometri s)
    {
        BaglariGuncelle(s);
        var shell = Kabuk(s); var surfaces = new List<ProjectSurface>(); if (shell.Count == 0) return new(surfaces, 0);
        var panels = Paneller(s, shell); var roofs = Tavanlar(s, shell); int id = 1;
        var collisions = Cakismalar(panels);
        if (collisions.Count > 0) throw new InvalidDataException("Üst üste gelen bölgeleri düzeltin: " + string.Join(", ", collisions.Select(x => s.CizimOgeleri.FirstOrDefault(o => o.Id == x)?.Ad ?? x).Distinct()));
        string Cat(SalonKatmanTipi t, string fallback) => s.Katmanlar.FirstOrDefault(k => k.Tip == t)?.Ad ?? fallback;
        void Face(List<Point2D> p, Func<Point2D, double> z, string category, string name = "")
        { if (p.Count >= 3 && Math.Abs(ClipperAdaptor.IsaretliAlan(p)) > 1e-8) surfaces.Add(new(id++, name == "" ? category : name, category, p.Select(q => new Point3D(q.X, q.Y, z(q))).ToList())); }
        var floorCuts = panels.Where(p => p.Alt <= .0001).Select(p => (IReadOnlyList<Point2D>)p.Plan).ToList();
        foreach (var p in Simple(ClipperAdaptor.Fark(shell, floorCuts))) Face(p, _ => 0, Cat(SalonKatmanTipi.Zemin, "Zemin"));
        double volume = 0;
        foreach (var r in roofs) { Face(r.Plan, r.Kot, Cat(SalonKatmanTipi.Tavan, "Tavan")); volume += Integral(r.Plan, r.Kot); }
        foreach (var p in panels)
        {
            var owner = s.CizimOgeleri.First(o => o.Id == p.NesneId);
            var topCuts = panels.Where(q => q.NesneId != p.NesneId && Math.Abs(q.Alt - p.Ust) < 1e-6).Select(q => (IReadOnlyList<Point2D>)q.Plan);
            foreach (var top in Simple(ClipperAdaptor.Fark(Set(p.Plan), topCuts))) Face(top, p.Kot, p.Kategori, owner.Ad);
            if (p.Alt > 0 && p.Plan.Any(q => p.Kot(q) > p.Alt + 1e-7))
                foreach (var bottom in Simple(ClipperAdaptor.Fark(Set(p.Plan), panels.Where(q => q != p && q.Plan.All(v => Math.Abs(q.Kot(v) - p.Alt) < 1e-6)).Select(q => (IReadOnlyList<Point2D>)q.Plan)))) Face(bottom, _ => p.Alt, p.Kategori + "-Alt", owner.Ad + " alt");
            double solid = Integral(p.Plan, q => p.Kot(q) - p.Alt);
            volume -= owner.Islev == SalonIslevi.Cukur ? Integral(p.Plan, p.Kot) : solid;
            for (int i = 0; i < p.Plan.Count; i++)
            {
                var start = p.Plan[i]; var end = p.Plan[(i + 1) % p.Plan.Count];
                var times = Splits(start, end, panels.Select(q => q.Plan).Concat(shell));
                for (int k = 0; k + 1 < times.Count; k++)
                {
                    Point2D At(double t) => new(start.X + (end.X - start.X) * t, start.Y + (end.Y - start.Y) * t);
                    var a = At(times[k]); var b = At(times[k + 1]); double dx = b.X - a.X, dy = b.Y - a.Y, len = Math.Sqrt(dx * dx + dy * dy); if (len < 1e-7) continue;
                    double sign = ClipperAdaptor.IsaretliAlan(p.Plan) > 0 ? 1 : -1;
                    var outside = new Point2D((a.X + b.X) / 2 + sign * dy / len * .00002, (a.Y + b.Y) / 2 - sign * dx / len * .00002);
                    if (!InShell(outside)) continue; // exterior wall owns this face
                    double bottom = owner.Islev == SalonIslevi.Cukur ? p.Ust : p.Alt;
                    double za = owner.Islev == SalonIslevi.Cukur ? 0 : p.Kot(a), zb = owner.Islev == SalonIslevi.Cukur ? 0 : p.Kot(b);
                    var side = new List<Point2D> { new(0, bottom), new(len, bottom), new(len, zb), new(0, za) };
                    var neighbours = panels.Where(q => q != p && Icinde(outside, q.Plan)).ToList();
                    var cuts = neighbours.Select(q => (IReadOnlyList<Point2D>)new List<Point2D> { new(0, q.Alt), new(len, q.Alt), new(len, Pit(q)?0:q.Kot(b)), new(0, Pit(q)?0:q.Kot(a)) }).ToList();
                    foreach (var face in Simple(ClipperAdaptor.Fark(Set(side), cuts)))
                        surfaces.Add(new(id++, owner.Ad + " yan", p.Kategori, face.Select(q => new Point3D(a.X + dx * q.X / len, a.Y + dy * q.X / len, q.Y)).ToList()));
                    if (owner.Islev == SalonIslevi.Balkon && owner.ParapetM > 0 && !neighbours.Any(q => q.NesneId == p.NesneId))
                        surfaces.Add(new(id++, owner.Ad + " parapet", p.Kategori + "-Parapet", new List<Point3D> { new(a.X, a.Y, za), new(b.X, b.Y, zb), new(b.X, b.Y, zb + owner.ParapetM), new(a.X, a.Y, za + owner.ParapetM) }));
                }
            }
        }
        bool InShell(Point2D p) => shell.Count(r => Icinde(p, r)) % 2 == 1;
        // Vertical faces close discontinuities between independently elevated roof patches.
        foreach (var r in roofs) for (int i = 0; i < r.Plan.Count; i++)
        {
            var start = r.Plan[i]; var end = r.Plan[(i + 1) % r.Plan.Count]; var times = Splits(start, end, roofs.Select(q => q.Plan));
            for (int k = 0; k + 1 < times.Count; k++)
            {
                Point2D At(double t) => new(start.X + (end.X - start.X) * t, start.Y + (end.Y - start.Y) * t);
                var a = At(times[k]); var b = At(times[k + 1]); double dx = b.X - a.X, dy = b.Y - a.Y, len = Math.Sqrt(dx * dx + dy * dy); if (len < 1e-8) continue;
                double sign = ClipperAdaptor.IsaretliAlan(r.Plan) > 0 ? 1 : -1;
                var outside = new Point2D((a.X + b.X) / 2 + sign * dy / len * .00002, (a.Y + b.Y) / 2 - sign * dx / len * .00002);
                var other = roofs.FirstOrDefault(q => q != r && Icinde(outside, q.Plan)); if (other == null) continue;
                double da = r.Kot(a) - other.Kot(a), db = r.Kot(b) - other.Kot(b);
                if (da <= 1e-7 && db <= 1e-7) continue;
                if (da * db < 0) { var crossing = At(times[k] + (times[k + 1] - times[k]) * da / (da - db)); if (da < 0) a = crossing; else b = crossing; }
                surfaces.Add(new(id++, "Tavan kırılımı", Cat(SalonKatmanTipi.Tavan, "Tavan"), new List<Point3D> { new(a.X, a.Y, other.Kot(a)), new(b.X, b.Y, other.Kot(b)), new(b.X, b.Y, r.Kot(b)), new(a.X, a.Y, r.Kot(a)) }));
            }
        }
        // Split walls at every roof patch boundary, preserving non-planar roof changes.
        foreach (var ring in shell) for (int i = 0; i < ring.Count; i++) Wall(ring[i], ring[(i + 1) % ring.Count], Cat(SalonKatmanTipi.DuvarSeyirci, "Duvar"));
        foreach (var o in s.CizimOgeleri.Where(o => o.CiktiyaDahil && o.Islev == SalonIslevi.Duvar)) foreach (var (a, b) in o.Segmentler()) Wall(a, b, s.Katmanlar.FirstOrDefault(k => k.Id == o.KatmanId)?.Ad ?? "Duvar");
        void Wall(Point2D a, Point2D b, string category)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y, len = Math.Sqrt(dx * dx + dy * dy); if (len < 1e-8) return;
            var ts = Splits(a, b, roofs.Select(r => r.Plan).Concat(panels.Select(p => p.Plan)));
            for (int j = 0; j + 1 < ts.Count; j++)
            {
                double t0 = ts[j], t1 = ts[j + 1]; var mid = new Point2D(a.X + dx * (t0 + t1) / 2, a.Y + dy * (t0 + t1) / 2);
                // Move a tiny distance into the shell to resolve boundary membership.
                var roof = roofs.FirstOrDefault(r => Icinde(new(mid.X + dy / len * .0002, mid.Y - dx / len * .0002), r.Plan) || Icinde(new(mid.X - dy / len * .0002, mid.Y + dx / len * .0002), r.Plan));
                double z0 = roof?.Kot(new(a.X + dx * t0, a.Y + dy * t0)) ?? TavanKotu(s, mid), z1 = roof?.Kot(new(a.X + dx * t1, a.Y + dy * t1)) ?? z0;
                var near = panels.Where(p => Icinde(new(mid.X + dy / len * .00002, mid.Y - dx / len * .00002), p.Plan) || Icinde(new(mid.X - dy / len * .00002, mid.Y + dx / len * .00002), p.Plan)).ToList();
                double baseZ = Math.Min(0, near.Select(p => p.Alt).DefaultIfEmpty(0).Min());
                var wall = new List<Point2D> { new(t0 * len, baseZ), new(t1 * len, baseZ), new(t1 * len, z1), new(t0 * len, z0) };
                var solidCuts = near.Select(p => (IReadOnlyList<Point2D>)new List<Point2D> { new(t0 * len, p.Alt), new(t1 * len, p.Alt), new(t1 * len, p.Kot(new(a.X + dx * t1, a.Y + dy * t1))), new(t0 * len, p.Kot(new(a.X + dx * t0, a.Y + dy * t0))) }).ToList();
                var visibleWall = ClipperAdaptor.Fark(Set(wall), solidCuts);
                var openings = new List<(List<Point2D> P, DuvarAcikligi A)>();
                foreach (var ac in s.Acikliklar)
                {
                    double t = ((ac.MerkezX - a.X) * dx + (ac.MerkezY - a.Y) * dy) / (len * len);
                    double d = Math.Abs((ac.MerkezX - a.X) * dy - (ac.MerkezY - a.Y) * dx) / len;
                    // A curved host is measured along its arc, so an opening can span several facets.
                    var host = s.CizimOgeleri.FirstOrDefault(o => o.Id == ac.DuvarId);
                    int edge = host?.KenarKimlikleri.IndexOf(ac.KenarId ?? "") ?? -1;
                    if (host != null && edge >= 0)
                    {
                        double best = double.MaxValue, ratio = 0;
                        var m = new Point2D((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                        for (int sample = 0; sample <= 400; sample++) { double u = sample / 400d; var q = host.KenarNoktasi(edge, u); double dist = Math.Pow(q.X - m.X, 2) + Math.Pow(q.Y - m.Y, 2); if (dist < best) { best = dist; ratio = u; } }
                        double bulge = edge < host.KenarKavisleri.Count ? host.KenarKavisleri[edge] : 0;
                        if (Math.Abs(bulge) > 1e-8 && best < .04)
                        {
                            var h0 = host.KenarNoktasi(edge, 0); var h1 = host.KenarNoktasi(edge, 1); double chord = Math.Sqrt(Math.Pow(h1.X - h0.X, 2) + Math.Pow(h1.Y - h0.Y, 2));
                            double arc = chord * (1 + bulge * bulge) / (4 * Math.Abs(bulge)) * Math.Abs(4 * Math.Atan(bulge));
                            var before = host.KenarNoktasi(edge, Math.Max(0, ratio - .001)); var after = host.KenarNoktasi(edge, Math.Min(1, ratio + .001));
                            double orientation = (after.X - before.X) * dx + (after.Y - before.Y) * dy >= 0 ? 1 : -1;
                            t = .5 + orientation * (ac.KenarOrani - ratio) * arc / len; d = 0;
                        }
                    }
                    if (d > .02 || t * len + ac.GenislikM / 2 < 0 || t * len - ac.GenislikM / 2 > len) continue;
                    var cut = new List<Point2D> { new(t * len - ac.GenislikM / 2, ac.AltKotM), new(t * len + ac.GenislikM / 2, ac.AltKotM), new(t * len + ac.GenislikM / 2, ac.AltKotM + ac.YukseklikM), new(t * len - ac.GenislikM / 2, ac.AltKotM + ac.YukseklikM) };
                    openings.Add((cut, ac));
                }
                void WFace(List<Point2D> p, string cat) => surfaces.Add(new(id++, cat, cat, p.Select(q => new Point3D(a.X + dx * q.X / len, a.Y + dy * q.X / len, q.Y)).ToList()));
                foreach (var cut in Simple(ClipperAdaptor.Fark(visibleWall, openings.Select(o => (IReadOnlyList<Point2D>)o.P)))) WFace(cut, category);
                var used = new List<IReadOnlyList<Point2D>>();
                foreach (var op in openings) { foreach (var cut in Simple(ClipperAdaptor.Fark(ClipperAdaptor.Kesisim(visibleWall, Set(op.P)), used))) WFace(cut, Cat(op.A.Tip == AciklikTipi.Kapi ? SalonKatmanTipi.Kapi : SalonKatmanTipi.Pencere, op.A.Ad)); used.Add(op.P); }
            }
        }
        return new(surfaces, Math.Max(0, volume));
    }
    private static List<double> Splits(Point2D a, Point2D b, IEnumerable<List<Point2D>> rings)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y, len2 = dx * dx + dy * dy; var ts = new List<double> { 0, 1 }; if (len2 < 1e-12) return ts;
        foreach (var ring in rings) for (int i = 0; i < ring.Count; i++)
        {
            var p = ring[i]; double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2;
            if (t > 0 && t < 1 && Math.Abs((p.X - a.X) * dy - (p.Y - a.Y) * dx) < 1e-7 * Math.Sqrt(len2)) ts.Add(t);
            if (SalonTrimServisi.SegmentKesisim(a, b, p, ring[(i + 1) % ring.Count], out var q, out _)) ts.Add(Math.Clamp(((q.X - a.X) * dx + (q.Y - a.Y) * dy) / len2, 0, 1));
        }
        return ts.DistinctBy(t => Math.Round(t, 8)).Order().ToList();
    }
    public static HashSet<string> Cakismalar(List<Panel> ps)
    {
        var result = new HashSet<string>(); for (int i = 0; i < ps.Count; i++) for (int j = i + 1; j < ps.Count; j++)
        {
            var a = ps[i]; var b = ps[j]; if (a.NesneId == b.NesneId) continue;
            var cut = ClipperAdaptor.Kesisim(Set(a.Plan), Set(b.Plan)); if (Alan(cut) < .0001) continue;
            if((Pit(a)&&b.Alt<=0)||(Pit(b)&&a.Alt<=0)){result.Add(a.NesneId);result.Add(b.NesneId);continue;}
            if (cut.SelectMany(p => p).Any(p => Math.Min(a.Kot(p), b.Kot(p)) > Math.Max(a.Alt, b.Alt) + 1e-6) || (Math.Abs(a.Alt - b.Alt) < 1e-6 && cut.SelectMany(p => p).All(p => Math.Abs(a.Kot(p) - b.Kot(p)) < 1e-6))) { result.Add(a.NesneId); result.Add(b.NesneId); }
        }
        return result;
    }
    static bool Pit(Panel p)=>p.Alt<0&&p.Plan.All(v=>Math.Abs(p.Kot(v)-p.Alt)<1e-8);
}
