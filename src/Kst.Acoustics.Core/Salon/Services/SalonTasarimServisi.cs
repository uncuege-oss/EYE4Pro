using System.Collections.ObjectModel;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Salon.Models;

namespace Kst.Acoustics.Core.Salon.Services;

/// Shared plan model for editing, sections, mesh, quantities and export.
public static class SalonTasarimServisi
{
    public static string Ad(SalonIslevi role) => role switch
    {
        SalonIslevi.Sinir => "Salon sınırı",
        SalonIslevi.Sahne => "Sahne",
        SalonIslevi.Seyirci => "Seyirci",
        SalonIslevi.Balkon => "Balkon",
        SalonIslevi.Cukur => "Orkestra çukuru",
        SalonIslevi.Bosluk => "Boşluk",
        SalonIslevi.Yardimci => "Yardımcı çizgi",
        SalonIslevi.Tavan => "Tavan bölgesi",
        SalonIslevi.Koridor => "Koridor",
        _ => "Duvar"
    };

    public static SalonKatmanTipi KatmanTipi(SalonIslevi role) => role switch
    {
        SalonIslevi.Sinir or SalonIslevi.Bosluk => SalonKatmanTipi.Zemin,
        SalonIslevi.Sahne => SalonKatmanTipi.Sahne,
        SalonIslevi.Seyirci => SalonKatmanTipi.Seyirci,
        SalonIslevi.Balkon => SalonKatmanTipi.Balkon,
        SalonIslevi.Cukur => SalonKatmanTipi.Orkestra,
        SalonIslevi.Tavan => SalonKatmanTipi.Tavan,
        SalonIslevi.Duvar => SalonKatmanTipi.DuvarSeyirci,
        _ => SalonKatmanTipi.Genel
    };

    public static void Hazirla(SalonGeometri salon)
    {
        if (salon.SerbestTasarim) { foreach (var o in salon.CizimOgeleri) o.OrtakSiniraDonustur(); return; }
        foreach (var o in salon.CizimOgeleri)
        {
            var k = salon.Katmanlar.FirstOrDefault(x => x.Id == o.KatmanId);
            o.Islev = o.KapaliPoligon().Count < 3 ? SalonIslevi.Duvar : k?.Tip switch
            {
                null or SalonKatmanTipi.Zemin => SalonIslevi.Sinir,
                SalonKatmanTipi.Tavan => SalonIslevi.Tavan,
                _ => SalonIslevi.Sahne
            };
            o.YukseklikM = 0; o.KotM = o.Islev == SalonIslevi.Tavan ? salon.SalonYuksekligiM : 0;
            o.Ad = k?.Ad ?? Ad(o.Islev); o.OrtakSiniraDonustur();
        }
        SalonCizimOgesi Add(List<Point2D> p, SalonIslevi r, string name)
        {
            var o = new SalonCizimOgesi
            {
                Tip = SalonCizimTipi.Polyline,
                Kapali = true,
                Noktalar = p,
                Islev = r,
                Ad = name,
                KatmanId = salon.Katmanlar.FirstOrDefault(k => k.Tip == KatmanTipi(r))?.Id ?? ""
            };
            o.KenarlariHazirla(); salon.CizimOgeleri.Add(o); return o;
        }
        if (salon.Sahne is { } stage) { var o = Add(stage.TabanPoligonu(), SalonIslevi.Sahne, "Sahne"); o.KotM = stage.TabanKotuM; o.YukseklikM = stage.YukseklikM; }
        foreach (var b in salon.Balkonlar) { var o = Add(SalonBalkonServisi.Footprint(b), SalonIslevi.Balkon, b.Ad); o.Id = b.Id; o.KotM = b.TabanKotuM; o.KalinlikM = b.KalinlikM; o.ParapetM = b.ParapetM; }
        foreach (var s in salon.SeyirciSekciyonlari)
        {
            var o = Add(SalonSeyirciServisi.Footprint(s), SalonIslevi.Seyirci, s.Ad); o.Id = s.Id; o.KotM = s.TabanKotuM; o.KademeSayisi = s.KademeSayisi; o.KademeYuksekligiM = s.KademeYuksekligiM; o.YonDerece = s.TersYon ? -90 : 90;
            o.UstNesneId = salon.CizimOgeleri.FirstOrDefault(b => b.Islev == SalonIslevi.Balkon && Math.Abs(b.KotM - o.KotM) < .01 && Icinde(o.Noktalar[0], b.KapaliPoligon()))?.Id;
        }
        if (salon.OrkestraCukuru is { } c) { var o = Add(c.Footprint(), SalonIslevi.Cukur, "Orkestra çukuru"); o.YukseklikM = c.CukurDerinligiM; }
        salon.Sahne = null; salon.OrkestraCukuru = null; salon.SeyirciSekciyonlari.Clear(); salon.Balkonlar.Clear(); salon.SerbestTasarim = true;
        foreach (var ac in salon.Acikliklar) AcikligiBagla(salon, ac);
    }

    public static void AcikligiBagla(SalonGeometri s, DuvarAcikligi ac)
    {
        double best = .2;
        foreach (var o in s.CizimOgeleri.Where(o => o.Islev is SalonIslevi.Sinir or SalonIslevi.Duvar))
        {
            o.KenarlariHazirla(); int n = o.Kapali ? o.Noktalar.Count : o.Noktalar.Count - 1;
            for (int i = 0; i < n; i++) for (int j = 0; j < 100; j++)
            {
                var a = o.KenarNoktasi(i, j / 100.0); var b = o.KenarNoktasi(i, (j + 1) / 100.0);
                double dx = b.X - a.X, dy = b.Y - a.Y, len = dx * dx + dy * dy;
                double t = len < 1e-12 ? 0 : Math.Clamp(((ac.MerkezX - a.X) * dx + (ac.MerkezY - a.Y) * dy) / len, 0, 1);
                double x = a.X + t * dx, y = a.Y + t * dy, d = Math.Sqrt(Math.Pow(ac.MerkezX - x, 2) + Math.Pow(ac.MerkezY - y, 2));
                if (d >= best) continue; best = d; ac.DuvarId = o.Id; ac.KenarId = o.KenarKimlikleri[i]; ac.KenarOrani = (j + t) / 100;
            }
        }
        BaglariGuncelle(s);
    }

    public static void BaglariGuncelle(SalonGeometri s)
    {
        foreach (var ac in s.Acikliklar)
        {
            var o = s.CizimOgeleri.FirstOrDefault(o => o.Id == ac.DuvarId); if (o is null) continue;
            int i = o.KenarKimlikleri.IndexOf(ac.KenarId ?? ""); if (i < 0 || i >= o.Noktalar.Count) continue;
            var p = o.KenarNoktasi(i, ac.KenarOrani); ac.MerkezX = p.X; ac.MerkezY = p.Y;
        }
        foreach (var o in s.CizimOgeleri.Where(o => o.UstNesneId != null))
            if (s.CizimOgeleri.FirstOrDefault(b => b.Id == o.UstNesneId) is { } b) o.KotM = b.KotM;
    }

    public static List<List<Point2D>> Kabuk(SalonGeometri s)
    {
        var outer = s.CizimOgeleri.Where(o => o.CiktiyaDahil && o.Islev == SalonIslevi.Sinir).Select(o => (IReadOnlyList<Point2D>)o.KapaliPoligon(Salon3DGorunumServisi.Faset(s))).Where(p => p.Count >= 3);
        var holes = s.CizimOgeleri.Where(o => o.CiktiyaDahil && o.Islev == SalonIslevi.Bosluk).Select(o => (IReadOnlyList<Point2D>)o.KapaliPoligon());
        return ClipperAdaptor.Fark(ClipperAdaptor.Birlestir(outer), holes.Concat(s.CizimOgeleri.Where(o => o.CiktiyaDahil && o.Islev == SalonIslevi.Sinir).SelectMany(o => o.IcBosluklar)));
    }
    public static bool Icinde(Point2D p, IReadOnlyList<Point2D> poly)
    {
        bool inside = false; for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        { var a = poly[i]; var b = poly[j]; if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside; }
        return inside;
    }
    public static List<List<Point2D>> Kirp(List<List<Point2D>> shell, IReadOnlyList<Point2D> p)
        => shell.Count == 0 ? new() : ClipperAdaptor.Kesisim(shell, new[] { p });
    public static double Alan(IEnumerable<IReadOnlyList<Point2D>> polys) => ClipperAdaptor.NetAlan(polys.Select(p => p.ToList()).ToList());

    public sealed record Panel(List<Point2D> Plan, double Alt, double Ust, string Kategori, string NesneId, double EgimX = 0, double EgimY = 0, Point2D Referans = default)
    {
        public double Kot(Point2D p) => Ust + EgimX * (p.X - Referans.X) + EgimY * (p.Y - Referans.Y);
    }

    public static List<Panel> Paneller(SalonGeometri s, List<List<Point2D>> shell)
    {
        var result = new List<Panel>();
        var corridors = s.CizimOgeleri.Where(o => o.CiktiyaDahil && o.Islev == SalonIslevi.Koridor).Select(o => (IReadOnlyList<Point2D>)o.KapaliPoligon()).ToList();
        foreach (var o in s.CizimOgeleri.Where(o => o.CiktiyaDahil && o.Islev is SalonIslevi.Sahne or SalonIslevi.Seyirci or SalonIslevi.Balkon or SalonIslevi.Cukur))
        {
            var poly = o.KapaliPoligon(Salon3DGorunumServisi.Faset(s)); if (poly.Count < 3) continue;
            var clipped = ClipperAdaptor.Fark(Kirp(shell, poly), o.IcBosluklar);
            if (o.UstNesneId != null && s.CizimOgeleri.FirstOrDefault(b => b.Id == o.UstNesneId) is { } parent) clipped = ClipperAdaptor.Fark(Kirp(clipped, parent.KapaliPoligon()), parent.IcBosluklar);
            string category = s.Katmanlar.FirstOrDefault(k => k.Id == o.KatmanId)?.Ad ?? Ad(o.Islev);
            if (o.Islev == SalonIslevi.Seyirci) clipped = ClipperAdaptor.Fark(clipped, corridors);
            if (o.Islev != SalonIslevi.Seyirci)
            {
                double top = o.Islev == SalonIslevi.Cukur ? -o.YukseklikM : o.Islev == SalonIslevi.Balkon ? o.KotM : o.KotM + o.YukseklikM;
                double bottom = o.Islev == SalonIslevi.Balkon ? o.KotM - o.KalinlikM : o.Islev == SalonIslevi.Cukur ? top : o.KotM;
                foreach (var p in SalonPoligonServisi.BasitPoligonlaraAyir(clipped)) result.Add(new(p, bottom, top, category, o.Id));
                continue;
            }
            double angle = o.YonDerece * Math.PI / 180, ux = Math.Cos(angle), uy = Math.Sin(angle);
            double lo = poly.Min(p => p.X * ux + p.Y * uy), hi = poly.Max(p => p.X * ux + p.Y * uy);
            double sideLo = poly.Min(p => -p.X * uy + p.Y * ux) - 1, sideHi = poly.Max(p => -p.X * uy + p.Y * ux) + 1;
            int n = o.ZeminBicimi == SalonZeminBicimi.Duz ? 1 : Math.Clamp(o.KademeSayisi, 1, 500);
            for (int i = 0; i < n; i++)
            {
                Point2D P(double x, double y) => new(x * ux - y * uy, x * uy + y * ux);
                var a = lo + (hi - lo) * i / n; var b = lo + (hi - lo) * (i + 1) / n;
                var band = new List<Point2D> { P(a, sideLo), P(b, sideLo), P(b, sideHi), P(a, sideHi) };
                double slope = o.ZeminBicimi == SalonZeminBicimi.Rampa && hi - lo > 1e-9 ? (n - 1) * o.KademeYuksekligiM / (hi - lo) : 0;
                foreach (var p in SalonPoligonServisi.BasitPoligonlaraAyir(ClipperAdaptor.Kesisim(clipped, new[] { (IReadOnlyList<Point2D>)band })))
                    result.Add(new(p, o.KotM, o.ZeminBicimi == SalonZeminBicimi.Rampa ? o.KotM : o.KotM + i * o.KademeYuksekligiM, category, o.Id, slope * ux, slope * uy, P(lo, 0)));
            }
        }
        return result;
    }

    public static double TavanKotu(SalonGeometri s, Point2D p)
    {
        var o = s.CizimOgeleri.LastOrDefault(o => o.CiktiyaDahil && o.Islev == SalonIslevi.Tavan && Icinde(p, o.KapaliPoligon()));
        return o is null ? SalonTavanServisi.Kot(s, p.Y) : o.KotM + o.EgimX * (p.X - o.KotReferansi.X) + o.EgimY * (p.Y - o.KotReferansi.Y);
    }

    public static List<Point2D> Koltuklar(SalonGeometri s, SalonCizimOgesi o)
    {
        var poly = o.KapaliPoligon(); var seats = new List<Point2D>(); if (poly.Count < 3 || o.Islev != SalonIslevi.Seyirci) return seats;
        var allowed = ClipperAdaptor.Fark(Kirp(Kabuk(s), poly), o.IcBosluklar);
        if (o.UstNesneId != null && s.CizimOgeleri.FirstOrDefault(b => b.Id == o.UstNesneId) is { } parent) allowed = ClipperAdaptor.Fark(Kirp(allowed, parent.KapaliPoligon()), parent.IcBosluklar); allowed = ClipperAdaptor.Fark(allowed, s.CizimOgeleri.Where(x => x.Islev == SalonIslevi.Koridor && x.CiktiyaDahil).Select(x => (IReadOnlyList<Point2D>)x.KapaliPoligon()));
        int attempts = 0;
        double pitch = Math.Max(.25, o.KoltukGenisligiM), row = Math.Max(.3, o.SiraAraligiM), ang = o.YonDerece * Math.PI / 180, ux = Math.Cos(ang), uy = Math.Sin(ang);
        bool Fits(Point2D p) => allowed.Count(h => Icinde(p, h) && ClipperAdaptor.IsaretliAlan(h) > 0) > 0 && !allowed.Any(h => ClipperAdaptor.IsaretliAlan(h) < 0 && Icinde(p, h));
        bool SeatFits(Point2D p, double dx, double dy) { if (++attempts > 2000000) throw new InvalidOperationException("Koltuk yerleşimi çok büyük; bölgeyi küçültün veya sıra merkezini yaklaştırın."); return Fits(p) && new[] { new Point2D(p.X + dx * pitch * .45 + dy * row * .45, p.Y + dy * pitch * .45 - dx * row * .45), new Point2D(p.X - dx * pitch * .45 + dy * row * .45, p.Y - dy * pitch * .45 - dx * row * .45), new Point2D(p.X + dx * pitch * .45 - dy * row * .45, p.Y + dy * pitch * .45 + dx * row * .45), new Point2D(p.X - dx * pitch * .45 - dy * row * .45, p.Y - dy * pitch * .45 + dx * row * .45) }.All(Fits); }
        if (o.YayliSiralar)
        {
            double maxR = poly.Max(p => Math.Sqrt(Math.Pow(p.X - o.SiraMerkezi.X, 2) + Math.Pow(p.Y - o.SiraMerkezi.Y, 2)));
            for (double r = row / 2; r < maxR && seats.Count < 50000; r += row)
                for (double a = 0; a < Math.PI * 2 && seats.Count < 50000; a += pitch / r) { var p = new Point2D(o.SiraMerkezi.X + r * Math.Cos(a), o.SiraMerkezi.Y + r * Math.Sin(a)); if (SeatFits(p, -Math.Sin(a), Math.Cos(a))) seats.Add(p); }
        }
        else
        {
            double lo = poly.Min(p => p.X * ux + p.Y * uy), hi = poly.Max(p => p.X * ux + p.Y * uy), l = poly.Min(p => -p.X * uy + p.Y * ux), h = poly.Max(p => -p.X * uy + p.Y * ux);
            for (double y = lo + row / 2; y < hi && seats.Count < 50000; y += row) for (double x = l + pitch / 2; x < h && seats.Count < 50000; x += pitch)
            { var p = new Point2D(y * ux - x * uy, y * uy + x * ux); if (SeatFits(p, -uy, ux)) seats.Add(p); }
        }
        if (seats.Count >= 50000) throw new InvalidOperationException("Tek seyirci bölgesinde 50.000 koltuk sınırı aşıldı; bölgeyi ayırın.");
        return seats;
    }
}
