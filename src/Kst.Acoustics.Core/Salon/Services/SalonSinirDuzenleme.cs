using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Salon.Models;

namespace Kst.Acoustics.Core.Salon.Services;

public static class SalonSinirDuzenleme
{
    public static void KenarBol(SalonGeometri s, SalonCizimOgesi o, int edge, double t)
    {
        o.KenarlariHazirla(); t = Math.Clamp(t, .001, .999);
        var p = o.KenarNoktasi(edge, t); double sweep = 4 * Math.Atan(o.KenarKavisleri[edge]);
        string oldId = o.KenarKimlikleri[edge], newId = Guid.NewGuid().ToString("N");
        o.Noktalar.Insert(edge + 1, p); o.KenarKavisleri[edge] = Math.Tan(sweep * t / 4);
        o.KenarKavisleri.Insert(edge + 1, Math.Tan(sweep * (1 - t) / 4)); o.KenarKimlikleri.Insert(edge + 1, newId);
        foreach (var ac in s.Acikliklar.Where(a => a.DuvarId == o.Id && a.KenarId == oldId))
            if (ac.KenarOrani <= t) ac.KenarOrani /= t; else { ac.KenarId = newId; ac.KenarOrani = (ac.KenarOrani - t) / (1 - t); }
        o.Tip = SalonCizimTipi.Polyline; SalonTasarimServisi.BaglariGuncelle(s);
    }
    public static void KoseSil(SalonGeometri s, SalonCizimOgesi o, int index)
    {
        o.KenarlariHazirla(); int prev = (index + o.Noktalar.Count - 1) % o.Noktalar.Count;
        var affected = s.Acikliklar.Where(a => a.DuvarId == o.Id && (a.KenarId == o.KenarKimlikleri[index] || a.KenarId == o.KenarKimlikleri[prev])).ToList();
        o.KenarKavisleri[prev] = 0; o.Noktalar.RemoveAt(index); o.KenarKavisleri.RemoveAt(index); o.KenarKimlikleri.RemoveAt(index); o.Tip = SalonCizimTipi.Polyline;
        foreach (var ac in affected) { ac.DuvarId = null; ac.KenarId = null; SalonTasarimServisi.AcikligiBagla(s, ac); }
    }
    public static void Birlestir(SalonGeometri s, List<SalonCizimOgesi> selection, bool subtract)
    {
        if (selection.Count < 2 || selection.Any(o => o.KapaliPoligon().Count < 3)) throw new InvalidOperationException("En az iki kapalı alan seçin (Shift+tık).");
        var first = selection[0];
        if (selection.Any(o => o.Islev != first.Islev)) throw new InvalidOperationException("Aynı işlevdeki alanları seçin.");
        List<List<Point2D>> Region(SalonCizimOgesi o) => ClipperAdaptor.Fark(new[] { (IReadOnlyList<Point2D>)o.KapaliPoligon(128) }, o.IcBosluklar);
        var combined = ClipperAdaptor.Birlestir(selection.Take(selection.Count - (subtract ? 1 : 0)).SelectMany(Region));
        var result = subtract ? ClipperAdaptor.Fark(combined, Region(selection[^1])) : combined;
        var newObjects = new List<SalonCizimOgesi>();
        foreach (var p in result.Where(p => ClipperAdaptor.IsaretliAlan(p) > 0))
        {
            var o = SalonDonusumServisi.Kopyala(first); o.Tip = SalonCizimTipi.Polyline; o.Noktalar = p; o.Kapali = true; o.KenarKavisleri.Clear(); o.KenarKimlikleri.Clear(); o.KenarlariHazirla();
            o.IcBosluklar = result.Where(h => ClipperAdaptor.IsaretliAlan(h) < 0 && SalonTasarimServisi.Icinde(h[0], p)).ToList();
            newObjects.Add(o);
        }
        foreach (var child in s.CizimOgeleri.Where(o => selection.Any(p => p.Id == o.UstNesneId)))
            child.UstNesneId = newObjects.FirstOrDefault(o => child.Noktalar.Any(p => SalonTasarimServisi.Icinde(p, o.Noktalar)))?.Id;
        foreach (var o in selection) s.CizimOgeleri.Remove(o); foreach (var o in newObjects) s.CizimOgeleri.Add(o);
        foreach (var a in s.Acikliklar.Where(a => selection.Any(o => o.Id == a.DuvarId))) { a.DuvarId = null; a.KenarId = null; SalonTasarimServisi.AcikligiBagla(s, a); }
    }

    public static void Bagla(SalonGeometri s, List<SalonCizimOgesi> selection)
    {
        if (selection.Count < 2) throw new InvalidOperationException("Birleşecek çizgi ve yayları seçin.");
        var edges = new List<(Point2D A, Point2D B, double K)>();
        foreach (var o in selection)
        {
            if (o.Tip == SalonCizimTipi.Yay && o.YayUclari() is { } ends) edges.Add((ends.Bas, ends.Bitis, Math.Tan((o.AciBitisDeg - o.AciBasDeg) * Math.PI / 720)));
            else if (o.Tip is SalonCizimTipi.Cizgi or SalonCizimTipi.Polyline) for (int i = 0; i < o.Noktalar.Count - 1; i++) edges.Add((o.Noktalar[i], o.Noktalar[i + 1], i < o.KenarKavisleri.Count ? o.KenarKavisleri[i] : 0));
            else throw new InvalidOperationException("Yalnız açık çizgi, kırık çizgi ve yay seçin.");
        }
        double D(Point2D a, Point2D b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
        var path = new List<Point2D>(); var bulges = new List<double>(); var e = edges[0]; edges.RemoveAt(0); path.Add(e.A); bulges.Add(e.K); var last = e.B;
        while (edges.Count > 0)
        {
            int i = edges.FindIndex(x => D(x.A, last) < .005 || D(x.B, last) < .005);
            if (i < 0) throw new InvalidOperationException("Uçlar birleşmiyor; açık uçları 5 mm içinde hizalayın.");
            e = edges[i]; edges.RemoveAt(i); path.Add(last);
            bool reverse = D(e.B, last) < .005; bulges.Add(reverse ? -e.K : e.K); last = reverse ? e.A : e.B;
        }
        if (D(path[0], last) > .005) throw new InvalidOperationException("Sınır açık; son uç başlangıçla birleşmiyor.");
        var boundary = new SalonCizimOgesi { Islev = SalonIslevi.Sinir, Ad = "Birleştirilmiş salon sınırı", Tip = SalonCizimTipi.Polyline, Noktalar = path, KenarKavisleri = bulges, Kapali = true, KatmanId = s.Katmanlar.FirstOrDefault(k => k.Tip == SalonKatmanTipi.Zemin)?.Id ?? "" }; boundary.KenarlariHazirla();
        foreach (var o in selection) s.CizimOgeleri.Remove(o); s.CizimOgeleri.Add(boundary);
        foreach (var ac in s.Acikliklar.Where(ac => selection.Any(o => o.Id == ac.DuvarId))) { ac.DuvarId = null; ac.KenarId = null; SalonTasarimServisi.AcikligiBagla(s, ac); }
    }

    public static void Kose(SalonCizimOgesi o, int index, double distance, bool round)
    {
        if (!o.Kapali || o.Noktalar.Count < 3) throw new InvalidOperationException("Kapalı bir sınır seçin.");
        o.KenarlariHazirla(); int prev = (index + o.Noktalar.Count - 1) % o.Noktalar.Count, next = (index + 1) % o.Noktalar.Count;
        if (Math.Abs(o.KenarKavisleri[prev]) + Math.Abs(o.KenarKavisleri[index]) > 1e-8) throw new InvalidOperationException("Bu işlem için köşenin iki kenarı düz olmalı.");
        var a = o.Noktalar[prev]; var b = o.Noktalar[index]; var c = o.Noktalar[next]; double l1 = Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2)), l2 = Math.Sqrt(Math.Pow(c.X - b.X, 2) + Math.Pow(c.Y - b.Y, 2));
        if (distance <= 0 || distance >= Math.Min(l1, l2) / 2) throw new InvalidOperationException("Mesafe, kısa kenarın yarısından küçük olmalı.");
        var p = new Point2D(b.X + (a.X - b.X) * distance / l1, b.Y + (a.Y - b.Y) * distance / l1); var q = new Point2D(b.X + (c.X - b.X) * distance / l2, b.Y + (c.Y - b.Y) * distance / l2);
        double turn = Math.Atan2((b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X), (b.X - a.X) * (c.X - b.X) + (b.Y - a.Y) * (c.Y - b.Y));
        o.Tip = SalonCizimTipi.Polyline; o.Noktalar[index] = p; o.Noktalar.Insert(index + 1, q); o.KenarKavisleri[index] = round ? Math.Tan(turn / 4) : 0; o.KenarKavisleri.Insert(index + 1, 0); o.KenarKimlikleri.Insert(index, Guid.NewGuid().ToString("N"));
    }
}
