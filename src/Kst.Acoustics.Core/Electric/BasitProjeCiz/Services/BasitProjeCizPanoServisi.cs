using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Electric.Calculation;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

/// <summary>
/// Bir pano hattının (Pano→Buat = linye) özeti.
/// Linye etiketi, üzerindeki armatür sayısı, gücü ve sigortası.
/// </summary>
public class PanoLinyeOzet
{
    /// <summary>Buat-pano bağlantısının kimliği (= pano kablosunun DevreId'si).</summary>
    public string BaglantiId { get; set; } = "";
    public string? BuatId { get; set; }
    /// "A" = aydınlatma, "P" = priz
    public string LinyePrefix { get; set; } = "A";

    /// <summary>Çizimdeki numara (A1/P1 gibi); henüz numaralanmadıysa null.</summary>
    public int? Sira { get; set; }
    /// Besleme (alt pano → üst pano) gibi özel satırlarda gösterilecek etiket.
    /// Null ise standart "{prefix}{sıra}" üretilir.
    public string? OzelEtiket { get; set; }
    public string Etiket => OzelEtiket ?? (Sira is { } s ? $"{LinyePrefix}{s}" : "—");

    public int ElemanSayisi { get; set; }
    public double GucW { get; set; }
    public int EksikGucTanimSayisi { get; set; }
    public int SigortaA { get; set; }
    public string SigortaCinsi { get; set; } = "B";
    /// Linye fazı. Aydınlatmada >9 armatür, prizde >7 priz olunca trifaze olur.
    public FazTipi Faz { get; set; } = FazTipi.Monofaze;
    public string SigortaText => SigortaA > 0
        ? (Faz == FazTipi.Trifaze ? $"3F {SigortaA}A {SigortaCinsi}" : $"{SigortaA}A {SigortaCinsi}")
        : "—";
    public string UyariText => EksikGucTanimSayisi > 0 ? $"{EksikGucTanimSayisi} güç tanımı eksik" : "";
}

/// <summary>
/// Bir panonun içerik özeti — linyeleri ve toplamları.
/// </summary>
/// Pano içi kaçak akım rölesi (FI/RCD) grubu.
/// Aydınlatma linyelerinin tamamı Grup 1, priz linyelerinin tamamı Grup 2'ye girer.
/// Mekanik (klima) linyelerinde FI atanmaz.
public class PanoFiGrubu
{
    /// "A" = aydınlatma, "P" = priz
    public string LinyePrefix { get; set; } = "A";
    /// Kaçak akım rölesinin eşiği — IEC 62423'e göre 30 mA (tip A)
    public int MaA { get; set; } = 30;
    public string Tip { get; set; } = "Tip A";
    public int SigortaA { get; set; }
    public double ToplamGucW { get; set; }
    public int LinyeSayisi { get; set; }
    public string Etiket => LinyePrefix == "A" ? "FI-1 (Aydınlatma)" : "FI-2 (Priz)";
}

public class PanoOzet
{
    public required BasitProjeEleman Pano { get; init; }
    public string Ad => Pano.Ad;
    public List<PanoLinyeOzet> Linyeler { get; set; } = new();
    public int ElemanToplam { get; set; }
    public double ToplamGucW { get; set; }
    public int EksikGucTanimToplam { get; set; }
    public bool EksikGucVarMi => EksikGucTanimToplam > 0;
    public int GirisSigortaA { get; set; }
    /// Pano içindeki FI/RCD grupları (Aydınlatma Grup1, Priz Grup2).
    public List<PanoFiGrubu> FiGruplari { get; set; } = new();
}

/// <summary>
/// BasitProjeCiz panolarının içerik özetini çıkarır.
/// Linye = pano'dan çıkan her buat hattı; o buata bağlı tüm devrelerin
/// armatürleri o linyeye sayılır. Akım/sigorta IEC 60898'e göre boyutlanır.
/// </summary>
public static class BasitProjeCizPanoServisi
{
    /// <summary>Basit aydınlatma/priz projesi varsayılanı: monofaze 230 V.</summary>
    public const double GerilimV = 230.0;
    /// <summary>Trifaze hat gerilimi: 400 V.</summary>
    public const double HatGerilimV = 400.0;
    /// <summary>Güç katsayısı (cosφ) — aydınlatma ağırlıklı yükler için.</summary>
    public const double CosF = 0.95;

    private static int LinyeSigortaA(double gucW, double akimA, int minimumA)
        => gucW <= 0 ? 0 : LinyeHesapServisi.SigortaBoyutlandir(akimA, minimumA);

    public static List<PanoOzet> PanoOzetleri(
        BasitProjeKat kat,
        IEnumerable<BpcArmatürTanim> armaturTanimlari,
        BpcPaftaTipi? aktifPafta = null)
    {
        var tanimlar = armaturTanimlari?.ToList() ?? new List<BpcArmatürTanim>();
        var tanimById = new Dictionary<string, BpcArmatürTanim>();
        var tanimByBlok = new Dictionary<string, BpcArmatürTanim>();
        foreach (var t in tanimlar)
        {
            tanimById[t.Id] = t;
            if (!string.IsNullOrWhiteSpace(t.BlokAdi))
                tanimByBlok[t.BlokAdi] = t;
        }

        (double GucW, bool Eksik) GucBul(BasitProjeEleman el)
        {
            static double AdetleCarp(BasitProjeEleman eleman, double guc)
                => eleman.OzelAlanTuru == "Adet" && eleman.CustomAdet is > 1
                    ? guc * eleman.CustomAdet.Value
                    : guc;

            if (el.CustomGucW.HasValue)
                return (AdetleCarp(el, el.CustomGucW.Value), false);
            if (!string.IsNullOrEmpty(el.ArmatürTanimId) &&
                tanimById.TryGetValue(el.ArmatürTanimId, out var t1))
                return (AdetleCarp(el, t1.GucW), false);
            if (!string.IsNullOrWhiteSpace(el.BlokAdi) &&
                tanimByBlok.TryGetValue(el.BlokAdi, out var t2))
                return (AdetleCarp(el, t2.GucW), false);
            return (0, true);
        }

        static FazTipi FazTipiBul(BasitProjeEleman el) =>
            el.Tip == BpcElemanTipi.TrifazePriz ? FazTipi.Trifaze : FazTipi.Monofaze;

        static double AkimBul(double gucW, BasitProjeEleman? el)
        {
            var faz = el == null ? FazTipi.Monofaze : FazTipiBul(el);
            var gerilim = faz == FazTipi.Trifaze ? HatGerilimV : GerilimV;
            return LinyeHesapServisi.AkimHesapla(gucW, gerilim, CosF, faz);
        }

        static bool LinyeAktifPaftadaMi(string prefix, BpcPaftaTipi? pafta) => pafta switch
        {
            null => true,
            BpcPaftaTipi.Aydinlatma => prefix == "A",
            BpcPaftaTipi.Priz => prefix == "P",
            BpcPaftaTipi.Mekanik => prefix == "M",
            BpcPaftaTipi.KabloTavasi => false,
            BpcPaftaTipi.Topraklama => false,
            BpcPaftaTipi.BusbarKablo => false,
            _ => true
        };

        static (double GucW, int Eksik) ToplaGuc(IEnumerable<BasitProjeEleman> elemanlar,
            Func<BasitProjeEleman, (double GucW, bool Eksik)> gucBul)
        {
            double toplam = 0;
            int eksik = 0;
            foreach (var el in elemanlar)
            {
                var g = gucBul(el);
                toplam += g.GucW;
                if (g.Eksik) eksik++;
            }
            return (toplam, eksik);
        }

        static double ToplaAkim(IEnumerable<BasitProjeEleman> elemanlar,
            Func<BasitProjeEleman, (double GucW, bool Eksik)> gucBul)
        {
            double toplam = 0;
            foreach (var el in elemanlar)
                toplam += AkimBul(gucBul(el).GucW, el);
            return toplam;
        }

        var sonuc = new List<PanoOzet>();
        var panolar = kat.Elemanlar.Where(e => e.Tip == BpcElemanTipi.KatPanosu);

        foreach (var pano in panolar)
        {
            var ozet = new PanoOzet { Pano = pano };

            foreach (var baglanti in kat.BuatPanoBaglantilari.Where(b => b.PanoId == pano.Id))
            {
                var kablo = kat.Kablolar.FirstOrDefault(
                    k => k.Tip == BpcKabloTipi.PanoKablosu && k.DevreId == baglanti.Id);

                PanoLinyeOzet linyeOzet;

                if (!LinyeAktifPaftadaMi(baglanti.LinyePrefix, aktifPafta))
                    continue;

                if (baglanti.LinyePrefix == "P")
                {
                    // Priz linyesi
                    var prizler = kat.PrizDevreleri
                        .Where(d => d.BuatId == baglanti.BuatId)
                        .SelectMany(d => d.PrizElemanIds)
                        .Distinct()
                        .Select(id => kat.Elemanlar.FirstOrDefault(e => e.Id == id))
                        .OfType<BasitProjeEleman>()
                        .ToList();

                    var guc = ToplaGuc(prizler, GucBul);
                    // Trifaze priz içeren veya 7'den fazla priz barındıran linye trifaze aktarılır.
                    bool trifaze = prizler.Count > 7 || prizler.Any(p => FazTipiBul(p) == FazTipi.Trifaze);
                    var faz = trifaze ? FazTipi.Trifaze : FazTipi.Monofaze;
                    var akim = trifaze
                        ? LinyeHesapServisi.AkimHesapla(guc.GucW, HatGerilimV, CosF, FazTipi.Trifaze)
                        : ToplaAkim(prizler, GucBul);
                    linyeOzet = new PanoLinyeOzet
                    {
                        BaglantiId = baglanti.Id,
                        BuatId = baglanti.BuatId,
                        LinyePrefix = "P",
                        Sira = kablo?.PanoSira,
                        ElemanSayisi = prizler.Count,
                        GucW = guc.GucW,
                        EksikGucTanimSayisi = guc.Eksik,
                        Faz = faz,
                        SigortaA = LinyeSigortaA(guc.GucW, akim, 16),
                        SigortaCinsi = "B"
                    };
                }
                else
                {
                    // Aydınlatma linyesi
                    var devreler = kat.Devreler.Where(d => d.BuatId == baglanti.BuatId).ToList();
                    var armaturlar = devreler
                        .SelectMany(d => d.ArmaturElemanIds)
                        .Distinct()
                        .Select(id => kat.Elemanlar.FirstOrDefault(e => e.Id == id))
                        .OfType<BasitProjeEleman>()
                        .Where(e => BasitProjeCizKabloServisi.ElemanArmaturMi(e.Tip))
                        .ToList();

                    var guc = ToplaGuc(armaturlar, GucBul);
                    // Bir buatta 9'dan fazla armatür → linye trifaze (panoda trifaze sigorta)
                    bool trifaze = armaturlar.Count > 9;
                    var faz = trifaze ? FazTipi.Trifaze : FazTipi.Monofaze;
                    double gerilim = trifaze ? HatGerilimV : GerilimV;
                    double akim = LinyeHesapServisi.AkimHesapla(guc.GucW, gerilim, CosF, faz);
                    linyeOzet = new PanoLinyeOzet
                    {
                        BaglantiId = baglanti.Id,
                        BuatId = baglanti.BuatId,
                        LinyePrefix = "A",
                        Sira = kablo?.PanoSira,
                        ElemanSayisi = armaturlar.Count,
                        GucW = guc.GucW,
                        EksikGucTanimSayisi = guc.Eksik,
                        Faz = faz,
                        SigortaA = LinyeSigortaA(guc.GucW, akim, 10),
                        SigortaCinsi = "B"
                    };
                }

                ozet.Linyeler.Add(linyeOzet);
            }

            // Mekanik (Klima) linyeleri
            if (LinyeAktifPaftadaMi("M", aktifPafta) && kat.MekanikDevreleri != null)
            {
                foreach (var mBag in kat.MekanikDevreleri.Where(m => m.PanoId == pano.Id))
                {
                    var klima = kat.Elemanlar.FirstOrDefault(e => e.Id == mBag.KlimaElemanId);
                    if (klima == null) continue;

                    var kablo = kat.Kablolar.FirstOrDefault(
                        k => k.Tip == BpcKabloTipi.MekanikKablosu && k.DevreId == mBag.Id);

                    var guc = GucBul(klima);
                    double akim = AkimBul(guc.GucW, klima);

                    ozet.Linyeler.Add(new PanoLinyeOzet
                    {
                        BaglantiId = mBag.Id,
                        BuatId = null,
                        LinyePrefix = "M",
                        Sira = kablo?.PanoSira,
                        ElemanSayisi = 1,
                        GucW = guc.GucW,
                        EksikGucTanimSayisi = guc.Eksik ? 1 : 0,
                        SigortaA = LinyeSigortaA(guc.GucW, akim, 6),
                        SigortaCinsi = "C" // Klima kompresör kalkış akımları için C tipi
                    });
                }
            }

            ozet.Linyeler = ozet.Linyeler
                .OrderBy(l => l.LinyePrefix)
                .ThenBy(l => l.Sira ?? int.MaxValue)
                .ThenBy(l => l.BaglantiId)
                .ToList();
            ozet.ElemanToplam = ozet.Linyeler.Sum(l => l.ElemanSayisi);
            ozet.ToplamGucW = ozet.Linyeler.Sum(l => l.GucW);
            ozet.EksikGucTanimToplam = ozet.Linyeler.Sum(l => l.EksikGucTanimSayisi);

            // Pano girişi: herhangi bir linye trifazeyse pano ana kesicisi de
            // trifazedir (400 V hat gerilimi). Aksi halde monofaze 230 V.
            bool panoTrifaze = ozet.Linyeler.Any(l => l.Faz == FazTipi.Trifaze);
            var girisFaz = panoTrifaze ? FazTipi.Trifaze : FazTipi.Monofaze;
            double girisGerilim = panoTrifaze ? HatGerilimV : GerilimV;
            double girisAkim = LinyeHesapServisi.AkimHesapla(
                ozet.ToplamGucW, girisGerilim, CosF, girisFaz);
            ozet.GirisSigortaA = LinyeHesapServisi.SigortaBoyutlandir(girisAkim);

            // FI/RCD grupları: aydınlatma (A) → Grup1, priz (P) → Grup2, mekanik (M) → FI yok
            foreach (var prefix in new[] { "A", "P" })
            {
                var linyeler = ozet.Linyeler.Where(l => l.LinyePrefix == prefix).ToList();
                if (linyeler.Count == 0) continue;
                double grupGuc = linyeler.Sum(l => l.GucW);
                double grupAkim = LinyeHesapServisi.AkimHesapla(grupGuc, GerilimV, CosF, FazTipi.Monofaze);
                ozet.FiGruplari.Add(new PanoFiGrubu
                {
                    LinyePrefix = prefix,
                    MaA = 30,
                    Tip = "Tip A",
                    SigortaA = LinyeSigortaA(grupGuc, grupAkim, prefix == "P" ? 16 : 10),
                    ToplamGucW = grupGuc,
                    LinyeSayisi = linyeler.Count
                });
            }

            sonuc.Add(ozet);
        }

        // ── Alt/tali pano beslemeleri ────────────────────────────────────────
        // Bir üst panoya gelen besleme hattı (BpcPanoBeslemeHatti), üst panonun
        // içeriğinde "besleme" linyesi olarak listelenir. Yük = alt panonun (kendi
        // yükleri + onun altındakiler) toplam gücüdür. Üst panonun giriş sigortası
        // bu beslemeleri de kapsar. Çizimde hat var ama listede yok hatasını çözer.
        var ozetById   = sonuc.ToDictionary(o => o.Pano.Id);
        var ownGucById = sonuc.ToDictionary(o => o.Pano.Id, o => o.ToplamGucW);
        var beslemeler = kat.PanoBeslemeHatlari
            .Where(h => h.UygulamaTipi == BpcPanoBeslemeUygulamaTipi.Kablo)
            .ToList();

        double AltPanoToplamGuc(string panoId, HashSet<string> ziyaret)
        {
            if (!ziyaret.Add(panoId)) return 0; // cevrim koruması
            double g = ownGucById.TryGetValue(panoId, out var o) ? o : 0;
            foreach (var h in beslemeler.Where(h => h.AnaPanoId == panoId))
                g += AltPanoToplamGuc(h.TaliPanoId, ziyaret);
            return g;
        }

        foreach (var ozet in sonuc)
        {
            var gelen = beslemeler.Where(h => h.AnaPanoId == ozet.Pano.Id).ToList();
            if (gelen.Count == 0) continue;

            foreach (var h in gelen)
            {
                if (!ozetById.TryGetValue(h.TaliPanoId, out var kaynak)) continue;
                double kaynakGuc = AltPanoToplamGuc(h.TaliPanoId, new HashSet<string>());
                bool trifaze = kaynak.Linyeler.Any(l => l.Faz == FazTipi.Trifaze);
                var faz = trifaze ? FazTipi.Trifaze : FazTipi.Monofaze;
                double gerilim = trifaze ? HatGerilimV : GerilimV;
                double akim = LinyeHesapServisi.AkimHesapla(kaynakGuc, gerilim, CosF, faz);
                ozet.Linyeler.Add(new PanoLinyeOzet
                {
                    BaglantiId   = h.Id,
                    LinyePrefix  = "BK",
                    OzelEtiket   = $"← {kaynak.Ad}",
                    ElemanSayisi = 1,
                    GucW         = kaynakGuc,
                    Faz          = faz,
                    SigortaA     = LinyeHesapServisi.SigortaBoyutlandir(akim),
                    SigortaCinsi = "C" // alt pano karma yükü → C tipi
                });
            }

            // Toplam güç ve giriş sigortası beslemeleri de kapsasın
            ozet.ToplamGucW   = AltPanoToplamGuc(ozet.Pano.Id, new HashSet<string>());
            ozet.ElemanToplam = ozet.Linyeler.Sum(l => l.ElemanSayisi);
            bool panoTri = ozet.Linyeler.Any(l => l.Faz == FazTipi.Trifaze);
            var girisFaz2 = panoTri ? FazTipi.Trifaze : FazTipi.Monofaze;
            double girisGer2 = panoTri ? HatGerilimV : GerilimV;
            double girisAkim2 = LinyeHesapServisi.AkimHesapla(ozet.ToplamGucW, girisGer2, CosF, girisFaz2);
            ozet.GirisSigortaA = LinyeHesapServisi.SigortaBoyutlandir(girisAkim2);
        }

        return sonuc;
    }

    /// <summary>
    /// Zayıf akım merkezlerinin (rack/splitter/MDF…) içerik özeti. Her merkez bir
    /// "pano" gibi listelenir; linyeleri o merkeze bağlı sistem devreleridir.
    /// Güç/sigorta hesabı yapılmaz (zayıf akım); cihaz sayısı gösterilir.
    /// </summary>
    public static List<PanoOzet> MerkezOzetleri(BasitProjeKat kat, BpcPaftaTipi sistem)
    {
        var tanim = BpcSistemKatalogu.Get(sistem);
        var sonuc = new List<PanoOzet>();

        // Ana merkez + ek merkezler (ör. CCTV'de NVR ve ağ switch'i)
        var merkezler = kat.Elemanlar.Where(e => tanim.MerkezMi(e.Tip));
        foreach (var merkez in merkezler)
        {
            var ozet = new PanoOzet { Pano = merkez };

            foreach (var devre in kat.SistemDevreleri
                         .Where(d => d.Sistem == sistem && d.MerkezId == merkez.Id))
            {
                var kablo = kat.Kablolar.FirstOrDefault(
                    k => k.Tip == BpcKabloTipi.ZayifAkimKablosu && k.DevreId == devre.Id);

                ozet.Linyeler.Add(new PanoLinyeOzet
                {
                    BaglantiId   = devre.Id,
                    LinyePrefix  = tanim.LinyePrefix,
                    Sira         = kablo?.PanoSira,
                    ElemanSayisi = devre.CihazIds.Count,
                    GucW         = 0,
                    SigortaA     = 0
                });
            }

            ozet.Linyeler = ozet.Linyeler
                .OrderBy(l => l.Sira ?? int.MaxValue)
                .ThenBy(l => l.BaglantiId)
                .ToList();
            ozet.ElemanToplam = ozet.Linyeler.Sum(l => l.ElemanSayisi);
            sonuc.Add(ozet);
        }

        return sonuc;
    }
}
