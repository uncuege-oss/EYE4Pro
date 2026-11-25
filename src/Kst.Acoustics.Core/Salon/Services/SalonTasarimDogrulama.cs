using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Salon.Models;

namespace Kst.Acoustics.Core.Salon.Services;

public sealed record SalonSorunu(string Mesaj, string? NesneId = null, bool Hata = true)
{ public override string ToString() => Mesaj; }

public static class SalonTasarimDogrulama
{
    public static List<SalonSorunu> Kontrol(SalonGeometri s)
    {
        var result = new List<SalonSorunu>();
        foreach (var o in s.CizimOgeleri.Where(o => o.CiktiyaDahil && o.Islev != SalonIslevi.Yardimci))
        {
            void Error(string text) => result.Add(new($"{o.Ad}: {text}", o.Id));
            if (o.Noktalar.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y)) || new[] { o.KotM, o.YukseklikM, o.KalinlikM, o.KademeYuksekligiM, o.EgimX, o.EgimY, o.ParapetM, o.YonDerece, o.SiraAraligiM, o.KoltukGenisligiM, o.SiraMerkezi.X, o.SiraMerkezi.Y, o.KotReferansi.X, o.KotReferansi.Y }.Concat(o.KenarKavisleri).Any(v => !double.IsFinite(v))) { Error("Ölçüler sonlu bir sayı olmalı."); continue; }
            if (o.Islev != SalonIslevi.Duvar && o.KapaliPoligon().Count < 3) { Error("Sınır açık; kapatmak için başlangıç noktasına tıklayın."); continue; }
            var poly = o.KapaliPoligon();
            if (poly.Count >= 3 && Math.Abs(ClipperAdaptor.IsaretliAlan(poly)) < .0001) Error("Alan sıfır veya çok küçük.");
            var edges = o.Segmentler().ToList();
            for (int i = 0; i < edges.Count; i++) for (int j = i + 2; j < edges.Count; j++)
            { if (i == 0 && j == edges.Count - 1 && o.Kapali) continue; if (SalonTrimServisi.SegmentKesisim(edges[i].A, edges[i].B, edges[j].A, edges[j].B, out _, out _)) { Error("Sınır kendi kendini kesiyor."); i = edges.Count; break; } }
            if (o.YukseklikM < 0 || o.KalinlikM < 0 || o.KademeYuksekligiM < 0 || o.ParapetM < 0) Error("Yükseklik ve kalınlık negatif olamaz.");
            if (o.Islev == SalonIslevi.Seyirci && (o.KademeSayisi < 1 || o.KademeSayisi > 500 || o.SiraAraligiM < .3 || o.KoltukGenisligiM < .25)) Error("Kademe 1–500, sıra aralığı ≥0,30 m ve koltuk genişliği ≥0,25 m olmalı.");
            if (o.Islev is SalonIslevi.Sahne or SalonIslevi.Seyirci && o.KotM < 0) Error("Zemin altı için orkestra çukuru işlevini kullanın.");
            if (o.Islev == SalonIslevi.Balkon && o.KotM < o.KalinlikM) Error("Balkon alt kotu zeminin altında kalıyor.");
            if (o.UstNesneId != null && !s.CizimOgeleri.Any(b => b.Id != o.Id && b.Id == o.UstNesneId && b.Islev == SalonIslevi.Balkon && b.CiktiyaDahil)) Error("Bağlı balkon bulunamadı.");
        }
        if (result.Any(x => x.Hata)) return result;
        var shell = SalonTasarimServisi.Kabuk(s);
        if (shell.Count == 0) { result.Add(new("Kapalı salon sınırı yok.")); return result; }
        var panels = SalonTasarimServisi.Paneller(s, shell);
        foreach (var id in SalonTasarimMeshServisi.Cakismalar(panels)) result.Add(new("Bölgeler aynı hacimde üst üste geliyor.", id));
        var roofs = SalonTasarimMeshServisi.Tavanlar(s, shell);
        foreach (var p in panels)
        {
            foreach (var r in roofs)
            {
                var cut = ClipperAdaptor.Kesisim(new[] { (IReadOnlyList<Point2D>)p.Plan }, new[] { (IReadOnlyList<Point2D>)r.Plan });
                if (cut.SelectMany(x => x).Any(q => p.Kot(q) + (s.CizimOgeleri.First(o => o.Id == p.NesneId).Islev == SalonIslevi.Balkon ? s.CizimOgeleri.First(o => o.Id == p.NesneId).ParapetM : 0) > r.Kot(q) + 1e-6)) { result.Add(new("Bölge tavanın üzerine çıkıyor.", p.NesneId)); break; }
            }
        }
        foreach (var o in s.CizimOgeleri.Where(o => o.CiktiyaDahil && o.Islev is SalonIslevi.Sahne or SalonIslevi.Seyirci or SalonIslevi.Balkon or SalonIslevi.Cukur))
        {
            var p = o.KapaliPoligon(); double area = Math.Abs(ClipperAdaptor.IsaretliAlan(p));
            if (ClipperAdaptor.NetAlan(SalonTasarimServisi.Kirp(shell, p)) < area * .995) result.Add(new("Bölgenin dışarı taşan kısmı salon sınırına kırpılıyor.", o.Id, false));
        }
        foreach (var a in s.Acikliklar)
        {
            var wall = s.CizimOgeleri.FirstOrDefault(o => o.Id == a.DuvarId);
            if (wall == null || !wall.KenarKimlikleri.Contains(a.KenarId ?? "")) result.Add(new($"{a.Ad}: bağlı duvar kenarı bulunamadı; yeniden yerleştirin.", a.DuvarId));
            if (new[] { a.GenislikM, a.YukseklikM, a.AltKotM, a.KenarOrani }.Any(v => !double.IsFinite(v))) { result.Add(new($"{a.Ad}: açıklık ölçüleri sonlu olmalı.", a.DuvarId)); continue; }
            if (wall != null && wall.KenarKimlikleri.IndexOf(a.KenarId ?? "") is int edge && edge >= 0)
            {
                double length = 0; var prev = wall.KenarNoktasi(edge, 0);
                for (int i = 1; i <= 128; i++) { var next = wall.KenarNoktasi(edge, i / 128d); length += Math.Sqrt(Math.Pow(next.X - prev.X, 2) + Math.Pow(next.Y - prev.Y, 2)); prev = next; }
                if (a.KenarOrani * length < a.GenislikM / 2 - .001 || (1 - a.KenarOrani) * length < a.GenislikM / 2 - .001) result.Add(new($"{a.Ad}: açıklık kenar ucunu aşıyor.", a.DuvarId));
                foreach (var other in s.Acikliklar.Where(b => b != a && b.DuvarId == a.DuvarId && b.KenarId == a.KenarId))
                    if (Math.Abs(other.KenarOrani - a.KenarOrani) * length < (other.GenislikM + a.GenislikM) / 2 - .001 && Math.Min(other.AltKotM + other.YukseklikM, a.AltKotM + a.YukseklikM) > Math.Max(other.AltKotM, a.AltKotM) + .001) result.Add(new($"{a.Ad}: başka bir açıklıkla çakışıyor.", a.DuvarId));
            }
            if (a.GenislikM <= 0 || a.YukseklikM <= 0 || a.AltKotM < 0 || a.AltKotM + a.YukseklikM > SalonTasarimServisi.TavanKotu(s, new(a.MerkezX, a.MerkezY))) result.Add(new($"{a.Ad}: açıklık ölçüsü duvara sığmıyor.", a.DuvarId));
        }
        if (roofs.Any(r => r.Plan.Any(p => r.Kot(p) <= 0))) result.Add(new("Tavan kotu zeminin üstünde olmalı."));
        return result.Distinct().ToList();
    }
}
