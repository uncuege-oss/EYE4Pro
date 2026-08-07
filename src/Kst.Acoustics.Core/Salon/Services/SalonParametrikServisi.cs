using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Salon.Models;

namespace Kst.Acoustics.Core.Salon.Services;

/// Hazır şablondan tam salon üretir: plan dış hattı (Zemin katmanı, kapalı) + parametrik sahne
/// + kademeli seyirci. Katmanlar otomatik atanır. Koordinat: ön kenar y=0, arka +Y; seyirci
/// +Y yönünde yükselir.
/// Yerleşim garantileri:
///   • Seyirci alanı plan dış hattının **içine sığdırılır** (yelpaze ön daralması ve at nalı
///     arka yayı hesaba katılır; kenarlarda ≥1 m koridor payı bırakılır).
///   • "Salon önü" sahnede dış hatta **sahne cebi** açılır: sahne kutusu dış hatta dahil olur,
///     salonla tam yükseklikte açıklıkla (proscenium) bağlanır — sahne kapalı hacmin dışında
///     duvar arkasında kalmaz.
public static class SalonParametrikServisi
{
    /// Seyirci alanı ile duvar arasında bırakılan yan koridor payı (m).
    private const double KenarPayiM = 1.0;

    /// Plan dış hattını çokgen olarak üretir (ön kenar y=0, arka y=Derinlik).
    public static List<Point2D> PlanHatti(SalonSablonu s, int yayKenar = 24)
    {
        double W = Math.Max(2, s.SalonGenislikM);
        double D = Math.Max(2, s.SalonDerinlikM);

        switch (s.SalonTip)
        {
            case SalonTipi.Yelpaze:
            {
                double wf = W * 0.6 / 2.0, wb = W / 2.0;
                return new List<Point2D>
                {
                    new(-wf, 0), new(wf, 0), new(wb, D), new(-wb, D)
                };
            }
            case SalonTipi.AtNali:
            {
                double r = W / 2.0;
                double dd = Math.Max(D, r + 2);     // düz bölüm için yeterli derinlik
                double yc = dd - r;                 // yay merkezi Y
                var pts = new List<Point2D> { new(-r, 0), new(-r, yc) };
                // Sol (π) → üst (π/2) → sağ (0): yay
                for (int i = 0; i <= yayKenar; i++)
                {
                    double a = Math.PI * (1.0 - (double)i / yayKenar); // π → 0
                    pts.Add(new Point2D(r * Math.Cos(a), yc + r * Math.Sin(a)));
                }
                pts.Add(new Point2D(r, 0));
                return pts;
            }
            default: // Dikdortgen
            {
                double w = W / 2.0;
                return new List<Point2D> { new(-w, 0), new(w, 0), new(w, D), new(-w, D) };
            }
        }
    }

    /// Plan yarı genişliği (m) — verilen y derinliğinde dış hattın x sınırı.
    public static double YariGenislik(SalonSablonu s, double y)
    {
        double W = Math.Max(2, s.SalonGenislikM);
        double D = Math.Max(2, s.SalonDerinlikM);

        switch (s.SalonTip)
        {
            case SalonTipi.Yelpaze:
            {
                double wf = W * 0.6 / 2.0, wb = W / 2.0;
                return wf + (wb - wf) * Math.Clamp(y / D, 0, 1);
            }
            case SalonTipi.AtNali:
            {
                double r = W / 2.0;
                double dd = Math.Max(D, r + 2);
                double yc = dd - r;
                if (y <= yc) return r;
                double dy = y - yc;
                return dy >= r ? 0 : Math.Sqrt(r * r - dy * dy);
            }
            default:
                return W / 2.0;
        }
    }

    /// Şablondan yeni bir salon geometrisi üretir.
    public static SalonGeometri Olustur(SalonSablonu s)
    {
        var salon = SalonGeometriServisi.YeniOlustur("Şablon Salon");
        salon.SalonYuksekligiM = Math.Max(2, s.SalonYuksekligiM);

        var zeminKat = salon.Katmanlar.First(k => k.Tip == SalonKatmanTipi.Zemin);

        double D = Math.Max(2, s.SalonDerinlikM);
        double sahneDer = Math.Max(1, s.SahneDerinlikM);
        double sahneGen = Math.Max(1, s.SahneGenislikM);

        // 1) Plan dış hattı (kapalı) → Zemin katmanı (kabuk: zemin+tavan+duvar üretir).
        //    Salon önü sahnede dış hatta sahne cebi açılır (proscenium açıklığı).
        var hat = PlanHatti(s);
        if (s.SahneKonum == SahneKonum.SalonOnu)
        {
            double onYari = YariGenislik(s, 0);
            double cepYari = Math.Min((sahneGen + 1.0) / 2.0, onYari - 0.5);
            sahneGen = Math.Max(1, Math.Min(sahneGen, 2 * cepYari - 1.0)); // sahne cebe sığsın
            hat = OnCepAc(hat, cepYari, sahneDer + 1.0);
        }
        salon.CizimOgeleri.Add(new SalonCizimOgesi
        {
            Tip = SalonCizimTipi.Polyline,
            Kapali = true,
            KatmanId = zeminKat.Id,
            Noktalar = hat
        });

        // 2) Sahne — önde (salon içi: ön kenarın hemen içinde; salon önü: cebin içinde)
        double sahneY = s.SahneKonum == SahneKonum.SalonOnu
            ? -(sahneDer / 2 + 0.5)
            : sahneDer / 2 + 0.5;
        salon.Sahne = new SahneGeometri
        {
            Tip = s.SahneTip,
            Konum = s.SahneKonum,
            GenislikM = sahneGen,
            DerinlikM = sahneDer,
            YukseklikM = Math.Max(0, s.SahneYuksekligiM),
            MerkezX = 0,
            MerkezY = sahneY
        };

        // 3) Kademeli seyirci — sahnenin arkasından salon arkasına; plan hattına sığdırılır.
        double seyirciOn = (s.SahneKonum == SahneKonum.SalonOnu ? 0.5 : sahneDer + 1.0) + 0.5;
        double seyirciArka = D - 1.0;
        double seyirciDer = Math.Max(2, seyirciArka - seyirciOn);

        // Yarı genişlik ön→arka boyunca monoton: yelpaze önde, at nalı arkada dardır.
        double siganYari = Math.Min(YariGenislik(s, seyirciOn), YariGenislik(s, seyirciArka)) - KenarPayiM;
        double seyirciGen = Math.Max(2, Math.Min(Math.Max(2, s.SalonGenislikM) * 0.82, 2 * siganYari));

        salon.SeyirciSekciyonlari.Add(new SeyirciSekciyonu
        {
            Ad = "Seyirci 1",
            GenislikM = seyirciGen,
            DerinlikM = seyirciDer,
            KademeSayisi = Math.Max(1, s.SeyirciKademe),
            KademeYuksekligiM = Math.Max(0, s.SeyirciKademeYukM),
            MerkezX = 0,
            MerkezY = (seyirciOn + seyirciArka) / 2,
            TersYon = false
        });

        return salon;
    }

    /// Ön kenara (y=0) sahne cebi ekler: dış hat, sahne kutusunu (−cepDer..0) kapsayacak
    /// şekilde genişletilir. Kenar bulunamazsa hat değiştirilmez.
    private static List<Point2D> OnCepAc(List<Point2D> hat, double cepYari, double cepDer)
    {
        if (cepYari <= 0.5 || cepDer <= 0) return hat;

        int n = hat.Count;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            if (Math.Abs(hat[i].Y) > 1e-9 || Math.Abs(hat[j].Y) > 1e-9) continue;
            if (Math.Min(hat[i].X, hat[j].X) > -cepYari + 1e-9) continue;
            if (Math.Max(hat[i].X, hat[j].X) < cepYari - 1e-9) continue;

            // Cep köşeleri kenarın gezinme yönüne göre sıralanır (poligon yönelimi korunur).
            double dir = Math.Sign(hat[j].X - hat[i].X);
            var cep = new List<Point2D>
            {
                new(-dir * cepYari, 0),
                new(-dir * cepYari, -cepDer),
                new(dir * cepYari, -cepDer),
                new(dir * cepYari, 0)
            };
            var yeni = new List<Point2D>(hat);
            yeni.InsertRange(i + 1, cep);
            return yeni;
        }
        return hat;
    }
}
