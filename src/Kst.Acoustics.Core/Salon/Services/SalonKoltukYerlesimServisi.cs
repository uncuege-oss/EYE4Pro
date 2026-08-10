using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Salon.Models;

namespace Kst.Acoustics.Core.Salon.Services;

public sealed record SalonKoltuk(
    string Id,
    string SekciyonId,
    int Sira,
    int SiraIciNo,
    int Blok,
    Point3D Konum,
    double BakisAciDerece,
    bool TekerlekliSandalyeYeri = false);

public sealed record SalonKoltukSirasi(
    int Sira,
    double YM,
    double KotM,
    IReadOnlyList<SalonKoltuk> Koltuklar);

public sealed record SalonKoltukYerlesimi(
    IReadOnlyList<SalonKoltukSirasi> Siralar,
    IReadOnlyList<SalonKoltuk> Koltuklar,
    int NormalKoltukSayisi,
    int TekerlekliSandalyeYeriSayisi,
    IReadOnlyList<string> Uyarilar);

/// <summary>
/// Brüt alan tahmini yerine koltuk genişliği, sıra aralığı, yan/orta koridorlar ve
/// tekerlekli sandalye ceplerini dikkate alan gerçek yerleşim üreticisi.
/// </summary>
public static class SalonKoltukYerlesimServisi
{
    private const double MinKoltukGenisligi = 0.40;
    private const double MinSiraAraligi = 0.60;
    private const double SandalyeCepGenisligi = 0.90;

    public static SalonKoltukYerlesimi Uret(SeyirciSekciyonu s, SahneGeometri? sahne = null)
    {
        ArgumentNullException.ThrowIfNull(s);
        var uyarilar = new List<string>();

        if (!Sonlu(s.GenislikM) || !Sonlu(s.DerinlikM) || s.GenislikM <= 0 || s.DerinlikM <= 0 ||
            !Sonlu(s.MerkezX) || !Sonlu(s.MerkezY) || !Sonlu(s.TabanKotuM) ||
            !Sonlu(s.KademeYuksekligiM))
        {
            uyarilar.Add("Seyirci alanının ölçü, merkez ve kot değerleri sonlu; genişlik/derinlik pozitif olmalıdır.");
            return new SalonKoltukYerlesimi([], [], 0, 0, uyarilar);
        }

        double koltukW = Sonlu(s.KoltukGenisligiM)
            ? Math.Max(MinKoltukGenisligi, s.KoltukGenisligiM)
            : MinKoltukGenisligi;
        double siraD = Sonlu(s.SiraAraligiM)
            ? Math.Max(MinSiraAraligi, s.SiraAraligiM)
            : MinSiraAraligi;
        double yan = Sonlu(s.YanKoridorGenisligiM)
            ? Math.Clamp(s.YanKoridorGenisligiM, 0, s.GenislikM / 2)
            : 0;
        int ortaSayisi = Math.Clamp(s.OrtaKoridorSayisi, 0, 8);
        double ortaW = Sonlu(s.OrtaKoridorGenisligiM) ? Math.Max(0, s.OrtaKoridorGenisligiM) : 0;

        if (!Sonlu(s.KoltukGenisligiM) || s.KoltukGenisligiM < MinKoltukGenisligi)
            uyarilar.Add($"Koltuk genişliği {MinKoltukGenisligi:F2} m altına indirilemez; sınır kullanıldı.");
        if (!Sonlu(s.SiraAraligiM) || s.SiraAraligiM < MinSiraAraligi)
            uyarilar.Add($"Sıra aralığı {MinSiraAraligi:F2} m altına indirilemez; sınır kullanıldı.");
        if (!Sonlu(s.YanKoridorGenisligiM) || !Sonlu(s.OrtaKoridorGenisligiM))
            uyarilar.Add("Sonlu olmayan koridor genişliği 0 m kabul edildi.");

        double kullanilabilirW = s.GenislikM - 2 * yan - ortaSayisi * ortaW;
        int blokSayisi = ortaSayisi + 1;
        if (s.DerinlikM < siraD || kullanilabilirW < koltukW)
        {
            uyarilar.Add("Seyirci alanına seçilen koltuk ve koridor ölçüleriyle sıra sığmıyor.");
            return new SalonKoltukYerlesimi([], [], 0, 0, uyarilar);
        }

        int siraSayisi = Math.Max(1, (int)Math.Floor(s.DerinlikM / siraD));
        double onArkaPay = (s.DerinlikM - siraSayisi * siraD) / 2.0;
        double blokW = kullanilabilirW / blokSayisi;
        int blokKoltuk = (int)Math.Floor(blokW / koltukW);
        if (blokKoltuk <= 0)
        {
            uyarilar.Add("Orta koridor sayısı/genişliği nedeniyle koltuk bloğu oluşturulamıyor.");
            return new SalonKoltukYerlesimi([], [], 0, 0, uyarilar);
        }

        double xSol = s.MerkezX - s.GenislikM / 2 + yan;
        var blokAraliklari = Enumerable.Range(0, blokSayisi)
            .Select(blok =>
            {
                double sol = xSol + blok * (blokW + ortaW);
                return (Sol: sol, Sag: sol + blokW);
            }).ToList();
        double yOn = s.TersYon
            ? s.MerkezY + s.DerinlikM / 2
            : s.MerkezY - s.DerinlikM / 2;
        double yon = s.TersYon ? -1 : 1;
        var tumu = new List<SalonKoltuk>();
        var siralar = new List<SalonKoltukSirasi>();

        for (int row = 0; row < siraSayisi; row++)
        {
            double y = yOn + yon * (onArkaPay + (row + 0.5) * siraD);
            int kademe = KademeIndeksi(s, y);
            double z = s.TabanKotuM + kademe * Math.Max(0, s.KademeYuksekligiM);
            var satir = new List<SalonKoltuk>();
            int no = 1;

            for (int blok = 0; blok < blokSayisi; blok++)
            {
                double blokSol = blokAraliklari[blok].Sol;
                double bosluk = (blokW - blokKoltuk * koltukW) / 2.0;
                for (int col = 0; col < blokKoltuk; col++)
                {
                    double x = blokSol + bosluk + (col + 0.5) * koltukW;
                    double aci = sahne is null
                        ? (s.TersYon ? 90.0 : -90.0)
                        : Math.Atan2(sahne.MerkezY - y, sahne.MerkezX - x) * 180.0 / Math.PI;
                    satir.Add(new SalonKoltuk(
                        $"{s.Id}-R{row + 1}-S{no}", s.Id, row + 1, no++, blok + 1,
                        new Point3D(x, y, z), aci));
                }
            }

            siralar.Add(new SalonKoltukSirasi(row + 1, y, z, satir));
            tumu.AddRange(satir);
        }

        // Erişilebilir cepleri en arka sıranın iki yanından başlayarak gerçek 0,90 m
        // genişlikte ayır. Her cep, çakışan normal koltukları kaldırır.
        int istenenCep = Math.Clamp(s.TekerlekliSandalyeYeri, 0, 100);
        int eklenenCep = 0;
        if (istenenCep > 0 && siralar.Count > 0)
        {
            var arka = siralar[^1];
            var duzenli = arka.Koltuklar.ToList();
            var merkezler = ErisilebilirCepMerkezleri(blokAraliklari);
            for (int i = 0; i < istenenCep && i < merkezler.Count; i++)
            {
                double merkez = merkezler[i];

                // Koltuk merkezini değil fiziksel genişliğini karşılaştır; aksi halde
                // cep sınırını kesen yarım koltuklar erişilebilir alanı daraltıyordu.
                duzenli.RemoveAll(k => Math.Abs(k.Konum.X - merkez) <
                    (SandalyeCepGenisligi + koltukW) / 2 - 1e-9);
                double aci = sahne is null
                    ? (s.TersYon ? 90.0 : -90.0)
                    : Math.Atan2(sahne.MerkezY - arka.YM, sahne.MerkezX - merkez) * 180.0 / Math.PI;
                duzenli.Add(new SalonKoltuk(
                    $"{s.Id}-R{arka.Sira}-W{i + 1}", s.Id, arka.Sira, i + 1, 0,
                    new Point3D(merkez, arka.YM, arka.KotM), aci, true));
                eklenenCep++;
            }

            tumu.RemoveAll(k => k.Sira == arka.Sira);
            duzenli.Sort((a, b) => a.Konum.X.CompareTo(b.Konum.X));
            tumu.AddRange(duzenli);
            siralar[^1] = arka with { Koltuklar = duzenli };
            if (eklenenCep < istenenCep)
                uyarilar.Add($"İstenen {istenenCep} erişilebilir yerin yalnız {eklenenCep} adedi sığdı.");
        }

        int normal = tumu.Count(k => !k.TekerlekliSandalyeYeri);
        return new SalonKoltukYerlesimi(siralar, tumu, normal, eklenenCep, uyarilar);
    }

    /// Her cep bütünüyle tek bir oturma bloğunda kalır; dolayısıyla orta koridorun
    /// üzerine erişilebilir yer konamaz. Dış kenarlar sırayla tercih edilir.
    private static List<double> ErisilebilirCepMerkezleri(
        IReadOnlyList<(double Sol, double Sag)> bloklar)
    {
        var tum = new List<double>();
        foreach (var (sol, sag) in bloklar)
        {
            int adet = Math.Max(0, (int)Math.Floor((sag - sol) / SandalyeCepGenisligi));
            for (int i = 0; i < adet; i++)
                tum.Add(sol + (i + 0.5) * SandalyeCepGenisligi);
        }
        tum.Sort();
        var sirali = new List<double>(tum.Count);
        int a = 0, b = tum.Count - 1;
        while (a <= b)
        {
            sirali.Add(tum[a++]);
            if (a <= b) sirali.Add(tum[b--]);
        }
        return sirali;
    }

    private static int KademeIndeksi(SeyirciSekciyonu s, double y)
    {
        int n = Math.Max(1, s.KademeSayisi);
        double yOn = s.TersYon ? s.MerkezY + s.DerinlikM / 2 : s.MerkezY - s.DerinlikM / 2;
        double mesafe = s.TersYon ? yOn - y : y - yOn;
        return Math.Clamp((int)Math.Floor(mesafe / Math.Max(1e-9, s.DerinlikM) * n), 0, n - 1);
    }

    private static bool Sonlu(double v) => double.IsFinite(v);
}
