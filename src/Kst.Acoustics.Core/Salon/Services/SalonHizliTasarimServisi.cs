using Kst.Acoustics.Core.Salon.Models;

namespace Kst.Acoustics.Core.Salon.Services;

public sealed record SalonHizliTasarimIstegi(
    int HedefKoltuk,
    SalonKullanimTipi Kullanim = SalonKullanimTipi.CokAmacli,
    SalonTipi PlanTipi = SalonTipi.Yelpaze,
    SahneTipi SahneTipi = SahneTipi.Dikdortgen,
    bool BalkonEkle = false,
    bool YansiticiEkle = true);

public sealed record SalonHizliTasarimSonucu(
    SalonGeometri Salon,
    int UretilenKoltuk,
    SalonGorusRaporu? GorusRaporu,
    SalonAkustikOnKontrolSonucu AkustikOnKontrol,
    IReadOnlyList<string> Notlar);

/// <summary>
/// Koltuk hedefi ve kullanım tipinden tek tıkla ölçülendirilmiş salon, gerçek koltuk
/// yerleşimi, görüş hattı ve başlangıç reflektör dizisi üretir.
/// </summary>
public static class SalonHizliTasarimServisi
{
    public static SalonHizliTasarimSonucu Olustur(SalonHizliTasarimIstegi istek)
    {
        ArgumentNullException.ThrowIfNull(istek);
        int hedef = Math.Clamp(istek.HedefKoltuk, 20, 5000);
        double kisiAlan = istek.Kullanim switch
        {
            SalonKullanimTipi.Muzik => 1.15,
            SalonKullanimTipi.Sinema => 0.90,
            SalonKullanimTipi.Konusma => 0.85,
            _ => 1.0
        };
        double seyirciAlan = hedef * kisiAlan;
        double genislik = Math.Clamp(Math.Sqrt(seyirciAlan * 0.85), 10, 55);
        double derinlik = Math.Clamp(seyirciAlan / genislik + 10, 14, 70);
        double hacimKisi = istek.Kullanim == SalonKullanimTipi.Muzik ? 9.0 : 6.5;
        double yukseklik = Math.Clamp(hedef * hacimKisi / (genislik * derinlik), 6, 22);
        double sahneW = Math.Clamp(genislik * 0.55, 8, 24);
        double sahneD = istek.Kullanim == SalonKullanimTipi.Muzik ? 10 : 7;
        var sablon = new SalonSablonu
        {
            SalonTip = istek.PlanTipi,
            SalonGenislikM = genislik,
            SalonDerinlikM = derinlik,
            SalonYuksekligiM = yukseklik,
            SahneTip = istek.SahneTipi,
            SahneKonum = SahneKonum.SalonIci,
            SahneGenislikM = sahneW,
            SahneDerinlikM = sahneD,
            SahneYuksekligiM = 0.9,
            SeyirciKademe = Math.Clamp((int)Math.Round((derinlik - sahneD - 2) / 0.9), 4, 50),
            SeyirciKademeYukM = 0.30
        };

        SalonGeometri salon = SalonParametrikServisi.Olustur(sablon);
        salon.SalonAdi = $"{KullanimAdi(istek.Kullanim)} — {hedef} koltuk";
        var notlar = new List<string>();
        var ana = salon.SeyirciSekciyonlari[0];
        void KoltukAyari(SeyirciSekciyonu s)
        {
            s.KoltukGenisligiM = istek.Kullanim == SalonKullanimTipi.Sinema ? 0.60 : 0.55;
            s.SiraAraligiM = istek.Kullanim == SalonKullanimTipi.Sinema ? 1.05 : 0.90;
            s.YanKoridorGenisligiM = 1.20;
            s.OrtaKoridorSayisi = sablon.SalonGenislikM > 22 ? 2 : 1;
            s.OrtaKoridorGenisligiM = 1.20;
            s.TekerlekliSandalyeYeri = Math.Max(2, (int)Math.Ceiling(hedef * 0.01));
        }
        KoltukAyari(ana);

        // Gerçek yerleşim hedefin altındaysa planı dengeli biçimde büyüt.
        SalonKoltukYerlesimi yerlesim = SalonKoltukYerlesimServisi.Uret(ana, salon.Sahne);
        for (int i = 0; i < 8 && yerlesim.Koltuklar.Count < hedef; i++)
        {
            double oran = Math.Sqrt((double)hedef / Math.Max(1, yerlesim.Koltuklar.Count));
            sablon.SalonGenislikM *= Math.Min(1.15, oran);
            sablon.SalonDerinlikM *= Math.Min(1.10, oran);
            sablon.SalonYuksekligiM = Math.Clamp(
                hedef * hacimKisi / (sablon.SalonGenislikM * sablon.SalonDerinlikM), 6, 22);
            salon = SalonParametrikServisi.Olustur(sablon);
            salon.SalonAdi = $"{KullanimAdi(istek.Kullanim)} — {hedef} koltuk";
            ana = salon.SeyirciSekciyonlari[0];
            KoltukAyari(ana);
            yerlesim = SalonKoltukYerlesimServisi.Uret(ana, salon.Sahne);
        }

        // Görüş kalitesi kabul edilebilir olana dek kademeyi kontrollü artır.
        SalonGorusRaporu? gorus = null;
        for (int i = 0; i < 10; i++)
        {
            gorus = SalonGorusHattiServisi.AnalizEt(salon, ana);
            if (gorus.IyiGorusYuzdesi >= 90 || ana.KademeYuksekligiM >= 0.48) break;
            ana.KademeYuksekligiM += 0.02;
        }
        if (gorus is { IyiGorusYuzdesi: < 90 })
            notlar.Add("Görüş hattı hedefi için kesit editöründe kademe/sıra ayarı incelemesi önerilir.");

        if (istek.BalkonEkle && hedef >= 400)
        {
            salon.Balkonlar.Add(new BalkonGeometri
            {
                Ad = "Arka Balkon",
                GenislikM = Math.Min(sablon.SalonGenislikM * 0.75, ana.GenislikM),
                DerinlikM = Math.Min(7, ana.DerinlikM * 0.25),
                MerkezX = 0,
                MerkezY = sablon.SalonDerinlikM - 4.5,
                TabanKotuM = Math.Min(salon.SalonYuksekligiM - 3, 5.5),
                KalinlikM = 0.40,
                ParapetM = 1.0
            });
            notlar.Add("Balkon geometrisi eklendi; balkon koltuk bölümü kesit görünümünde ayrıca tanımlanmalıdır.");
        }

        if (istek.YansiticiEkle && istek.Kullanim is SalonKullanimTipi.Muzik or SalonKullanimTipi.CokAmacli)
        {
            string katmanId = salon.Katmanlar.First(k => k.Tip == SalonKatmanTipi.Yansitici).Id;
            foreach (var p in SalonYansiticiServisi.OtomatikDizi(salon))
            {
                p.KatmanId = katmanId;
                salon.YansiticiPaneller.Add(p);
            }
        }

        var akustik = SalonAkustikOnKontrolServisi.Hesapla(salon, istek.Kullanim);
        if (yerlesim.Koltuklar.Count != hedef)
            notlar.Add($"Hedef {hedef}, geometrik olarak yerleşen {yerlesim.Koltuklar.Count} koltuk; koridor ve erişilebilir alanlar dahildir.");
        notlar.AddRange(akustik.Bulgular);
        return new SalonHizliTasarimSonucu(salon, yerlesim.Koltuklar.Count, gorus, akustik, notlar.Distinct().ToList());
    }

    public static IReadOnlyList<(string Ad, SalonHizliTasarimIstegi Istek)> HazirOnAyarlar() =>
    [
        ("Konferans 120", new SalonHizliTasarimIstegi(120, SalonKullanimTipi.Konusma, SalonTipi.Yelpaze)),
        ("Çok Amaçlı 300", new SalonHizliTasarimIstegi(300, SalonKullanimTipi.CokAmacli, SalonTipi.Yelpaze)),
        ("Konser 600", new SalonHizliTasarimIstegi(600, SalonKullanimTipi.Muzik, SalonTipi.AtNali, BalkonEkle: true)),
        ("Sinema 180", new SalonHizliTasarimIstegi(180, SalonKullanimTipi.Sinema, SalonTipi.Dikdortgen, YansiticiEkle: false))
    ];

    private static string KullanimAdi(SalonKullanimTipi t) => t switch
    {
        SalonKullanimTipi.Konusma => "Konferans salonu",
        SalonKullanimTipi.Muzik => "Konser salonu",
        SalonKullanimTipi.Sinema => "Sinema",
        _ => "Çok amaçlı salon"
    };
}
