using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Salon.Models;
using Kst.Acoustics.Core.Validation;

namespace Kst.Acoustics.Core.Salon.Services;

/// M51 salon geometrisi tutarlılık kontrolleri. İki katman:
///   • <see cref="Dogrula"/> — hızlı, senkron geometrik kontroller (Clipper2):
///     kabuk var mı, sahne/seyirci/balkon dış hatta taşıyor mu, gizli katman öğesi var mı.
///   • <see cref="KabukKapalilikRaporu"/> — üretilen 3D kabuğa ışın tabanlı sızıntı testi
///     (<see cref="LeakTestService"/>): M01'e gitmeden önce "kabuk kapalı mı" yanıtı.
/// Uyarılar Türkçe, kullanıcıya doğrudan gösterilebilir.
public static class SalonDogrulamaServisi
{
    /// Footprint'in dış hat içinde sayılması için gereken minimum alan oranı.
    private const double IcOran = 0.995;

    public static List<string> Dogrula(SalonGeometri salon)
    {
        if (salon.SerbestTasarim) return SalonTasarimDogrulama.Kontrol(salon).Select(x=>x.Mesaj).ToList();
        var uyarilar = new List<string>();

        // Kabuk poligonları (görünür Zemin tipli / katmansız kapalı öğeler)
        var kabukPoligonlari = new List<IReadOnlyList<Point2D>>();
        int gizliOge = 0;
        foreach (var oge in salon.CizimOgeleri)
        {
            var katman = salon.Katmanlar.FirstOrDefault(k => k.Id == oge.KatmanId);
            if (katman is { Gorunur: false }) gizliOge++;
            if (katman is not null && katman.Tip != SalonKatmanTipi.Zemin) continue;
            var poly = oge.KapaliPoligon(Salon3DGorunumServisi.Faset(salon));
            if (poly.Count >= 3) kabukPoligonlari.Add(poly);
        }

        if (gizliOge > 0)
            uyarilar.Add($"{gizliOge} öğe görünümde gizli; hesap ve çıktıya dahildir.");

        // Tavan profili tutarlılığı
        if (salon.TavanProfili.Count == 1)
            uyarilar.Add("Tavan profili tek nokta içeriyor — profil için en az 2 kırılım " +
                         "noktası gerekir; şu an düz tavan kullanılıyor.");
        if (salon.TavanProfili.Any(n => n.KotM <= 0))
            uyarilar.Add("Tavan profilinde kot ≤ 0 olan nokta var — tavan zeminin altına iner.");
        {
            double minKot = SalonTavanServisi.ProfilliMi(salon) ? salon.TavanProfili.Min(n => n.KotM) : salon.SalonYuksekligiM;
            double maksPlatform = 0;
            if (salon.Sahne is { } sp) maksPlatform = Math.Max(maksPlatform, sp.TabanKotuM + sp.YukseklikM);
            foreach (var sk in salon.SeyirciSekciyonlari)
                maksPlatform = Math.Max(maksPlatform,
                    sk.TabanKotuM + Math.Max(0, sk.KademeSayisi - 1) * sk.KademeYuksekligiM);
            foreach (var bk in salon.Balkonlar)
                maksPlatform = Math.Max(maksPlatform, bk.TabanKotuM + bk.ParapetM);
            if (maksPlatform > minKot)
                uyarilar.Add($"Platform üst kotu ({maksPlatform:F1} m) en alçak tavan kotunu " +
                             $"({minKot:F1} m) aşıyor — kesişim kontrol edin.");
        }

        if (kabukPoligonlari.Count == 0)
        {
            uyarilar.Add("Kapalı zemin poligonu yok — kabuk (zemin+tavan+duvar) üretilmez; " +
                         "M01 hacmi kaba tahmine düşer. Zemin katmanında kapalı bir dış hat çizin.");
            AciklikUyarilari(salon, null, uyarilar);
            return uyarilar; // taşma kontrolleri kabuk ister
        }

        var kabuk = ClipperAdaptor.Birlestir(kabukPoligonlari);
        AciklikUyarilari(salon, kabuk, uyarilar);

        if (salon.Sahne is { } sahne)
        {
            double oran = IcindeOran(kabuk, sahne.TabanPoligonu(Salon3DGorunumServisi.Faset(salon)));
            if (oran < IcOran)
                uyarilar.Add(sahne.Konum == SahneKonum.SalonOnu
                    ? "Sahne dış hattın dışında — 'salon önü' sahne için dış hatta sahne cebi " +
                      "(proscenium) ekleyin, aksi halde sahne kapalı hacmin dışında kalır."
                    : $"Sahne salon dış hattının dışına taşıyor (içeride kalan alan %{oran * 100:F0}).");
        }

        foreach (var sek in salon.SeyirciSekciyonlari)
        {
            double oran = IcindeOran(kabuk, SalonSeyirciServisi.Footprint(sek));
            if (oran < IcOran)
                uyarilar.Add($"'{sek.Ad}' seyirci alanı dış hattın dışına taşıyor " +
                             $"(içeride kalan alan %{oran * 100:F0}).");
        }

        foreach (var b in salon.Balkonlar)
        {
            double oran = IcindeOran(kabuk, SalonBalkonServisi.Footprint(b));
            if (oran < IcOran)
                uyarilar.Add($"'{b.Ad}' dış hattın dışına taşıyor (içeride kalan alan %{oran * 100:F0}).");
        }

        if (salon.OrkestraCukuru is { } cukur)
        {
            double oran = IcindeOran(kabuk, cukur.Footprint());
            if (oran < IcOran)
                uyarilar.Add($"Orkestra çukuru dış hattın dışına taşıyor (içeride kalan alan %{oran * 100:F0}).");
            if (salon.Sahne is { } sn)
            {
                double cakisan = ClipperAdaptor.NetAlan(ClipperAdaptor.Kesisim(
                    new[] { (IReadOnlyList<Point2D>)cukur.Footprint() },
                    new[] { (IReadOnlyList<Point2D>)sn.TabanPoligonu(Salon3DGorunumServisi.Faset(salon)) }));
                if (cakisan > 0.05)
                    uyarilar.Add($"Orkestra çukuru sahne footprint'iyle çakışıyor ({cakisan:F1} m²) — " +
                                 "çukuru sahnenin önüne alın.");
            }
        }

        var copy=SalonGeometriServisi.KopyadanYukle(SalonGeometriServisi.KopyaJson(salon));
        SalonTasarimServisi.Hazirla(copy);
        var overlapping=SalonTasarimMeshServisi.Cakismalar(SalonTasarimServisi.Paneller(copy,SalonTasarimServisi.Kabuk(copy)));
        if(overlapping.Count>0)uyarilar.Add("Bölgeler aynı hacimde üst üste geliyor; yerleşimi düzeltin.");
        return uyarilar;
    }

    /// Açıklıklar (kapı/pencere) bir duvar segmentine bağlanıyor mu? Duvar segmentleri:
    /// görünür açık çizgi öğeleri + kabuk çevre kenarları.
    private static void AciklikUyarilari(
        SalonGeometri salon, List<List<Point2D>>? kabuk, List<string> uyarilar)
    {
        if (salon.Acikliklar.Count == 0) return;

        var segmentler = new List<(Point2D A, Point2D B)>();
        foreach (var oge in salon.CizimOgeleri)
        {
            var katman = salon.Katmanlar.FirstOrDefault(k => k.Id == oge.KatmanId);

            if (oge.KapaliPoligon(Salon3DGorunumServisi.Faset(salon)).Count >= 3) continue; // kapalı → kabukta
            foreach (var s in oge.Segmentler(Salon3DGorunumServisi.Faset(salon)))
                segmentler.Add(s);
        }
        if (kabuk is not null)
            foreach (var halka in kabuk)
                for (int i = 0; i < halka.Count; i++)
                    segmentler.Add((halka[i], halka[(i + 1) % halka.Count]));

        foreach (var ac in salon.Acikliklar)
        {
            bool bagli = segmentler.Any(sg =>
                NoktaSegmentUzakligi(sg.A, sg.B, ac.MerkezX, ac.MerkezY)
                    <= Salon3DGorunumServisi.AciklikToleransM);
            if (!bagli)
                uyarilar.Add($"'{ac.Ad}' hiçbir duvarın üzerinde değil — merkezi bir duvar " +
                             "çizgisinin üzerine taşıyın (3D'de kesilmez).");
        }
    }

    private static double NoktaSegmentUzakligi(Point2D a, Point2D b, double px, double py)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double len2 = dx * dx + dy * dy;
        if (len2 < 1e-12) return Math.Sqrt((px - a.X) * (px - a.X) + (py - a.Y) * (py - a.Y));
        double t = Math.Clamp(((px - a.X) * dx + (py - a.Y) * dy) / len2, 0, 1);
        double cx = a.X + t * dx, cy = a.Y + t * dy;
        return Math.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
    }

    /// Üretilen 3D kabuğa ışın tabanlı sızıntı testi uygular; Türkçe özet döner.
    public static async Task<string> KabukKapalilikRaporu(SalonGeometri salon)
    {
        var project = SalonImporter.Cevir(salon, salon.DosyaYolu);
        if (project.Surfaces.Count == 0)
            return "Sızıntı testi yapılamadı: 3D yüzey üretilemedi (kapalı zemin çizin).";

        var sonuc = await new LeakTestService().RunAsync(project);
        return sonuc.Passed
            ? $"✔ Kabuk kapalı görünüyor ({sonuc.BounceCount} sıçrama, ışın kaçışı yok)."
            : $"⚠ Kabuk sızdırıyor: {sonuc.Message}";
    }

    /// Footprint alanının kabuk içinde kalan oranı (0..1). Footprint dejenere ise 1 döner.
    private static double IcindeOran(List<List<Point2D>> kabuk, IReadOnlyList<Point2D> footprint)
    {
        if (footprint.Count < 3) return 1;
        double alan = Math.Abs(ClipperAdaptor.IsaretliAlan(footprint));
        if (alan < 1e-9) return 1;
        double ic = ClipperAdaptor.NetAlan(ClipperAdaptor.Kesisim(kabuk, new[] { footprint }));
        return ic / alan;
    }
}
