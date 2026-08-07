using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Electric.Models;

namespace Kst.Acoustics.Core.Electric.Calculation;

/// <summary>
/// Pano hesap servisi — toplam güç, giriş sigortası, KAR, modül sayısı.
/// </summary>
public static class PanoHesapServisi
{
    public static readonly int[] StandartSigortaA =
    {
        6, 10, 13, 16, 20, 25, 32, 40, 50, 63, 80, 100, 125, 160, 200, 250,
        315, 400, 500, 630, 800, 1000, 1250, 1600, 2000, 2500, 3200
    };

    /// <summary>
    /// Verilen sigorta değerinin bir üst standart değerini döndürür (selektivite için).
    /// </summary>
    public static int BirUstStandartSigorta(int sigortaA)
    {
        foreach (var s in StandartSigortaA)
        {
            if (s > sigortaA) return s;
        }
        return StandartSigortaA[^1];
    }

    /// <summary>
    /// Bir panodaki tüm hesaplamaları yapar.
    /// </summary>
    public static void PanoHesapla(Pano pano, ElektrikProje proje, ElektrikKutuphaneServisi kutuphane)
    {
        var cosF = NumberHelper.NormalizeCosF(proje.CosF, 0.85);
        var fazV = proje.FazGerilimV;
        var hatV = proje.SistemGerilimV;

        if (pano.PanoTipi == PanoTipi.KontrolPanosu)
        {
            KontrolPanosuHesapla(pano);
            return;
        }

        // 1. Her linye hesapla (akım + sigorta + kablo kesiti/gerilim düşümü/ısı kaybı)
        foreach (var linye in pano.Linyeler)
        {
            LinyeHesapServisi.LinyeHesapla(linye, fazV, hatV, cosF);
            KabloHesapServisi.LinyeKabloHesapla(linye, proje);
        }

        // 2. Alt panolardan gelen güçleri topla (her alt pano kendi eşzamanlılık faktörüyle).
        //    Yangın panosu ana şalter ÖNCESİNDEN beslenir → üst panonun yük toplamına KATILMAZ.
        double altPanoGuc = pano.AltPanolar
            .Where(p => p.PanoTipi != PanoTipi.YanginPanosu)
            .Sum(p => p.ToplamGucW * Clamp01(p.EszamanlilikFaktoru));

        // 3. Toplam güçler
        pano.ToplamGucW = pano.Linyeler.Sum(l => l.ToplamGucW) + altPanoGuc;
        pano.ToplamReaktifVAR = pano.ToplamGucW * Math.Tan(Math.Acos(cosF));
        pano.ToplamGorunurVA = pano.ToplamGucW / cosF;

        // 4. Faz dağılımı
        pano.FazDagilimi = FazDagilimServisi.OtomatikFazAta(pano.Linyeler, fazV, cosF);

        // 5. Giriş akımı (3 faz olarak) — bağlı (connected) akım
        pano.GirisAkimA = pano.ToplamGorunurVA / (Math.Sqrt(3) * hatV);

        // 5b. Talep (demand) akımı = bağlı akım × eşzamanlılık faktörü (ks). Besleme tarafı
        //     boyutlandırması (giriş/çıkış sigortası, kablo, KAR) bu akıma göre yapılır.
        pano.TalepAkimA = pano.GirisAkimA * Clamp01(pano.EszamanlilikFaktoru);

        // 6. Giriş sigortası — talep akımına göre
        pano.GirisSigortaA = GirisSigortaHesapla(pano.TalepAkimA);

        // 6b. Ana pano giriş şalteri (trafo–ana pano arası). Trafo yoksa otomatik: ana pano bağlı
        //     akımına göre. OG/trafo varsa OrtaGerilimHesapServisi bunu trafo AG çıkış akımına göre ezer.
        if (pano.PanoTipi == PanoTipi.AnaPano)
            pano.GirisSalteriA = GirisSigortaHesapla(pano.GirisAkimA);

        // 7. KAR seçimi — talep akımına göre
        if (pano.KacakAkimRolesiAktif)
        {
            pano.KacakAkimRolesi = KacakAkimRolesiSec(pano.PanoTipi, pano.TalepAkimA, kutuphane);
            pano.KacakAkimAkimTrafosu = AkimTrafosuSec(pano.KacakAkimRolesi, pano.TalepAkimA, kutuphane);
        }
        else
        {
            pano.KacakAkimRolesi = null;
            pano.KacakAkimAkimTrafosu = null;
        }

        // 8. Modül sayısı
        pano.ModulSayisi = ModulSayisiHesapla(pano);
        PanoBoyutlandirmaServisi.Hesapla(pano);

        // 9. Sayaç tayini:
        //    - En üst pano (UstPano == null) → her zaman sayaç
        //    - Aydınlatma Panosu → AydinlatmaSayacli alanına göre
        if (pano.UstPano == null)
        {
            pano.SayacEkle = true;
            pano.SayacTipi = pano.ToplamGucW > 5000 ? "Trifaze" : "Monofaze";
        }
        else if (pano.PanoTipi == PanoTipi.AydinlatmaPanosu)
        {
            pano.SayacEkle = pano.AydinlatmaSayacli;
            if (pano.SayacEkle)
                pano.SayacTipi = pano.ToplamGucW > 5000 ? "Trifaze" : "Monofaze";
        }
        else
        {
            pano.SayacEkle = false;
        }

        // 10. Aydınlatma Panosu girişinde otomatik astronomik zaman rölesi
        if (pano.PanoTipi == PanoTipi.AydinlatmaPanosu)
        {
            // Aydınlatma çıkışı sayısı = "Aydınlatma" yük tipindeki linye sayısı
            int aydinlatmaCikisSayisi = pano.Linyeler.Count(l => l.YukTipiAd.Contains("Aydınlatma"));
            if (aydinlatmaCikisSayisi == 0) aydinlatmaCikisSayisi = 1;
            pano.AstronomikZamanRolesi = AzrSec(pano.GirisAkimA, aydinlatmaCikisSayisi, kutuphane);
        }
        else
        {
            pano.AstronomikZamanRolesi = null;
        }

        // 10c. Darbe kesici + Kontaktör ataması (güç linye akımına göre seçilir):
        //   - Darbe kesici: kullanıcı DarbeKesiciMi işaretlediği linyede.
        //   - Kontaktör: (a) aydınlatma panosundaki aydınlatma linyeleri, (b) KontaktorluMu işaretli
        //     her linye, (c) darbe kesicili ve akımı 16A'i aşan linye (impuls rölesi tek başına yetmez).
        foreach (var linye in pano.Linyeler)
        {
            linye.DarbeKesici = linye.DarbeKesiciMi ? DarbeKesiciSec(linye.AkimA, kutuphane) : null;

            bool aydinlatmaOtomatik = pano.PanoTipi == PanoTipi.AydinlatmaPanosu
                && linye.YukTipiAd.Contains("Aydınlatma");
            bool darbeIcinKontaktor = linye.DarbeKesiciMi && linye.AkimA > 16;
            linye.Kontaktor = (aydinlatmaOtomatik || linye.KontaktorluMu || darbeIcinKontaktor)
                ? KontaktorSec(linye.AkimA, kutuphane)
                : null;
        }

        // 10b. Kompanzasyon panosu — otomatik bileşen seçimi (kullanıcı elle ayarlamadıysa)
        if (pano.PanoTipi == PanoTipi.Kompanzasyon && !pano.KompanzasyonElleAyarlandi)
        {
            KompanzasyonOtomatikSec(pano, proje, kutuphane);
        }

        // 11. Tali pano için üst panoda eklenecek çıkış sigortası (selektivite)
        if (pano.UstPano != null)
        {
            int cikisSigortaA = proje.SelektiviteAktif
                ? BirUstStandartSigorta(pano.GirisSigortaA)
                : pano.GirisSigortaA;

            pano.UstPanoCikisSigortaA = cikisSigortaA;
            if (pano.BaglantiTipi == BaglantiTipi.Busbar)
                BusbarCikisSigortasiniGuncelle(pano, proje, cikisSigortaA);
        }
        else
        {
            pano.UstPanoCikisSigortaA = 0;
        }
    }

    private static void KontrolPanosuHesapla(Pano pano)
    {
        pano.ToplamGucW = 0;
        pano.ToplamReaktifVAR = 0;
        pano.ToplamGorunurVA = 0;
        pano.GirisAkimA = 0;
        pano.GirisSigortaA = 0;
        pano.KacakAkimRolesi = null;
        pano.KacakAkimAkimTrafosu = null;
        pano.FazDagilimi = new FazDagilimi();
        pano.KabloKesiti = "";
        pano.KabloParalelKolSayisi = 1;
        pano.KabloGerilimDususYuzde = 0;
        pano.KabloIsiKaybiW = 0;
        pano.SayacEkle = false;
        pano.SayacTipi = "";
        pano.AstronomikZamanRolesi = null;
        pano.UstPanoCikisSigortaA = 0;
        pano.CikisKutusuSigortaA = 0;

        int butonModul = pano.KontrolButonlari.Count;
        int yedek = butonModul == 0 ? 0 : (int)Math.Ceiling(butonModul * 0.20);
        pano.ModulSayisi = butonModul + yedek;
        PanoBoyutlandirmaServisi.Hesapla(pano);
    }

    private static void BusbarCikisSigortasiniGuncelle(Pano pano, ElektrikProje proje, int sigortaA)
    {
        pano.CikisKutusuSigortaA = sigortaA;

        if (string.IsNullOrWhiteSpace(pano.BaglandigiBusbarId)) return;
        var busbar = proje.TumBusbarlar.FirstOrDefault(b => b.Id == pano.BaglandigiBusbarId);
        var kutu = busbar?.CikisKutulari.FirstOrDefault(k =>
            string.Equals(k.BagliPanoAd, pano.Ad, StringComparison.OrdinalIgnoreCase));

        if (kutu != null)
            kutu.SigortaA = sigortaA;
    }

    /// <summary>0–1 aralığına kıstırır; geçersiz/sıfır değerleri 1.0 (tam talep) sayar.</summary>
    private static double Clamp01(double ks) => ks <= 0 ? 1.0 : Math.Min(1.0, ks);

    /// <summary>
    /// IEC 60898 giriş sigortası boyutu — hesaplanan akımın üstündeki ilk standart değer.
    /// </summary>
    public static int GirisSigortaHesapla(double girisAkimA)
    {
        foreach (var s in StandartSigortaA)
        {
            if (s >= girisAkimA) return s;
        }
        return StandartSigortaA[^1];
    }

    /// <summary>
    /// KAR seçimi — tali pano: 30mA, ana pano: 300mA.
    /// 100A üstü yüklerde klasik 4P KAR yerine troid röle kullanılır.
    /// </summary>
    public static KacakAkimRolesiTanim? KacakAkimRolesiSec(PanoTipi panoTipi, double girisAkimA,
        ElektrikKutuphaneServisi kutuphane)
    {
        const double troidEsigiA = 100.0;
        int hedefMa = panoTipi == PanoTipi.AnaPano ? 300 : 30;
        int hedefKutup = 4; // Trifaze sistem → 4P

        var klasikler = kutuphane.KacakAkimRoleleri
            .Where(k => k.SensitiviteMa == hedefMa && k.KutupSayisi == hedefKutup && !k.TroidTipiMi)
            .OrderBy(k => k.AnmaAkimA)
            .ToList();

        if (girisAkimA <= troidEsigiA)
            return klasikler.FirstOrDefault(k => k.AnmaAkimA >= girisAkimA)
                   ?? klasikler.LastOrDefault()
                   ?? TroidKacakAkimRolesiSec(kutuphane, hedefMa, girisAkimA);

        return TroidKacakAkimRolesiSec(kutuphane, hedefMa, girisAkimA)
               ?? klasikler.FirstOrDefault(k => k.AnmaAkimA >= girisAkimA)
               ?? klasikler.LastOrDefault();
    }

    private static KacakAkimRolesiTanim? TroidKacakAkimRolesiSec(
        ElektrikKutuphaneServisi kutuphane, int hedefMa, double girisAkimA)
    {
        var troidler = kutuphane.KacakAkimRoleleri
            .Where(k => k.SensitiviteMa == hedefMa && k.TroidTipiMi)
            .OrderBy(k => k.AnmaAkimA)
            .ToList();

        return troidler.FirstOrDefault(k => k.AnmaAkimA >= girisAkimA)
               ?? troidler.LastOrDefault();
    }

    /// <summary>
    /// Troid kaçak akım rölesi için rölenin sekonder ve pencere ihtiyacına göre akım trafosu seçer.
    /// Klasik KAR seçildiyse akım trafosu gerekmez.
    /// </summary>
    public static AkimTrafosuTanim? AkimTrafosuSec(KacakAkimRolesiTanim? kar,
        double girisAkimA, ElektrikKutuphaneServisi kutuphane)
    {
        if (kar?.TroidTipiMi != true) return null;

        int sekonderA = kar.AkimTrafosuSekonderA > 0 ? kar.AkimTrafosuSekonderA : 5;
        int minCapMm = Math.Max(0, kar.MinAkimTrafosuIcCapMm);

        var uygunlar = kutuphane.AkimTrafolari
            .Where(a => a.SekonderA == sekonderA
                        && a.IcCapMm >= minCapMm
                        && a.Tip.Contains("Toroid", StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.PrimerA)
            .ThenBy(a => a.IcCapMm)
            .ToList();

        return uygunlar.FirstOrDefault(a => a.PrimerA >= girisAkimA)
               ?? uygunlar.LastOrDefault();
    }

    /// <summary>
    /// Kompanzasyon panosu için otomatik bileşen seçimi:
    /// Röle (kademe sayısına ve SVC durumuna göre), reaktör + SVC sürücü (SVC ise),
    /// NH bıçaklı giriş sigortası. Kullanıcı manuel ayarladıysa çağrılmaz.
    /// </summary>
    public static void KompanzasyonOtomatikSec(Pano pano, ElektrikProje proje, ElektrikKutuphaneServisi kutuphane)
    {
        // Hedef kademe sayısı: kondansatör kademe sayısı (6/12/18)
        int hedefKademe = proje.Kompanzasyon?.KondansatorKademeSayisi ?? 6;
        if (hedefKademe < 6) hedefKademe = 6;

        // SVC/reaktör desteği proje kompanzasyon sonucundaki ReaktorAktif bayrağına bağlıdır.
        // (Harmonik filtre ayrı bir bileşendir; SVC gereğini belirlemez.)
        bool svcGerek = proje.Kompanzasyon?.ReaktorAktif == true;

        pano.SeciliKompanzasyonRolesi = kutuphane.KompanzasyonRoleleri
            .Where(r => r.KademeSayisi >= hedefKademe && r.SvcDestekli == svcGerek)
            .OrderBy(r => r.KademeSayisi)
            .FirstOrDefault()
            ?? kutuphane.KompanzasyonRoleleri.OrderBy(r => r.KademeSayisi).FirstOrDefault();

        // SVC destekli ise reaktör + sürücü seç (proje seviyesi kompanzasyonun seçtiklerini yansıt)
        if (svcGerek)
        {
            // Reaktör: ilk reaktör kademesinden ya da kütüphanede en küçükten
            var ilkReaktorKademesi = proje.Kompanzasyon?.ReaktorKademeleri.FirstOrDefault();
            pano.SeciliReaktor = ilkReaktorKademesi != null
                ? kutuphane.Reaktorler.FirstOrDefault(r => r.Ad == ilkReaktorKademesi.ReaktorAd)
                  ?? kutuphane.Reaktorler.FirstOrDefault()
                : kutuphane.Reaktorler.FirstOrDefault();

            // SVC sürücü: hesabın seçtiği yük sürücü (toplam reaktör kVAr'dan büyük)
            pano.SeciliSvcSurucu = proje.Kompanzasyon?.SecilenYukSurucu
                ?? kutuphane.SvcSuruculer.FirstOrDefault();
        }
        else
        {
            pano.SeciliReaktor = null;
            pano.SeciliSvcSurucu = null;
        }

        // Harmonik filtre seçili değilse ama HarmonikFiltreliMi true ise, ilk filtreyi al
        if (pano.HarmonikFiltreliMi && pano.SeciliHarmonikFiltre == null && kutuphane.HarmonikFiltreler.Count > 0)
            pano.SeciliHarmonikFiltre = kutuphane.HarmonikFiltreler[0];

        // NH bıçaklı giriş sigortası — kondansatör bankı akımına göre (boyutlandırma yapıldıysa);
        // henüz yapılmadıysa ana pano akımına düşer.
        double kompAkim = pano.GirisAkimA > 0 ? pano.GirisAkimA : (proje.KokPano?.GirisAkimA ?? 0);
        pano.GirisBicakliSigorta = kutuphane.BicakliSigortalar
            .Where(b => b.AnmaAkimA >= kompAkim && b.Karakteristik == "gG")
            .OrderBy(b => b.AnmaAkimA)
            .FirstOrDefault()
            ?? kutuphane.BicakliSigortalar.LastOrDefault();
    }

    /// <summary>Kompanzasyon kondansatör bankı için harmonik + tolerans akım katsayısı.
    /// IEC 60831: kondansatörler sürekli 1.3×In akımı ve 1.1×Un gerilimi taşıyabilmeli
    /// (1.3 × 1.1 ≈ 1.43). Bank kablosu, ana sigorta ve kademe sigortaları bu akıma göre seçilir.</summary>
    public const double KondansatorAkimFaktoru = 1.43;

    /// <summary>
    /// Kompanzasyon panosuna yapısı gereği yük bağlanmadığından akım/sigorta/kablo aksi halde
    /// minimum (0) çıkar. Bu metot kondansatör bankının kVAr'ından gerçek bank akımını türetir
    /// (× <see cref="KondansatorAkimFaktoru"/>) ve panonun görünür güç / akım / giriş sigortası /
    /// üst pano çıkış sigortasını buna göre günceller. Aktif güç (ToplamGucW) ≈ 0 bırakılır;
    /// üst panonun aktif yük toplamı şişmez. Kablo bu metottan SONRA yeniden hesaplanmalıdır.
    /// </summary>
    public static void KompanzasyonPanoBoyutlandir(Pano pano, ElektrikProje proje,
        ElektrikKutuphaneServisi kutuphane)
    {
        if (pano.PanoTipi != PanoTipi.Kompanzasyon) return;

        double hatV = proje.SistemGerilimV;
        double qKVar = proje.Kompanzasyon?.ToplamKondansatorKVar ?? 0;
        if (qKVar <= 0)
            qKVar = proje.Kompanzasyon?.KondansatorKademeleri.Sum(k => k.KVar) ?? 0;
        if (qKVar <= 0 || hatV <= 0) return;

        double qVA = qKVar * 1000.0;
        pano.ToplamReaktifVAR = qVA;
        pano.ToplamGorunurVA = qVA;          // kondansatör bankında S ≈ Q (P ≈ 0)

        double bankAkim = qVA / (Math.Sqrt(3) * hatV);
        pano.GirisAkimA = bankAkim * KondansatorAkimFaktoru;
        pano.TalepAkimA = pano.GirisAkimA * Clamp01(pano.EszamanlilikFaktoru);
        pano.GirisSigortaA = GirisSigortaHesapla(pano.TalepAkimA);

        if (pano.UstPano != null)
        {
            int cikisSigortaA = proje.SelektiviteAktif
                ? BirUstStandartSigorta(pano.GirisSigortaA)
                : pano.GirisSigortaA;
            pano.UstPanoCikisSigortaA = cikisSigortaA;
            if (pano.BaglantiTipi == BaglantiTipi.Busbar)
                BusbarCikisSigortasiniGuncelle(pano, proje, cikisSigortaA);
        }

        if (pano.KacakAkimRolesiAktif)
        {
            pano.KacakAkimRolesi = KacakAkimRolesiSec(pano.PanoTipi, pano.GirisAkimA, kutuphane);
            pano.KacakAkimAkimTrafosu = AkimTrafosuSec(pano.KacakAkimRolesi, pano.GirisAkimA, kutuphane);
        }
    }

    /// <summary>Tek bir kondansatör kademesinin sigorta anma akımı (A) — kademe kVAr'ından
    /// türetilir, harmonik/tolerans katsayısıyla çarpılır, üstündeki ilk standart değere yuvarlanır.</summary>
    public static int KondansatorKademeSigortaA(double kademeKVar, double hatV)
    {
        if (kademeKVar <= 0 || hatV <= 0) return 0;
        double kademeAkim = (kademeKVar * 1000.0) / (Math.Sqrt(3) * hatV) * KondansatorAkimFaktoru;
        return GirisSigortaHesapla(kademeAkim);
    }

    /// <summary>
    /// Astronomik zaman rölesi seçimi — kütüphaneden uygun anma akımı ve çıkış sayısına sahip olanı bulur.
    /// </summary>
    public static AstronomikZamanRolesiTanim? AzrSec(double girisAkimA, int aydinlatmaCikisSayisi,
        ElektrikKutuphaneServisi kutuphane)
    {
        return kutuphane.AstronomikZamanRoleleri
            .Where(a => a.AnmaAkimA >= girisAkimA && a.CikisSayisi >= aydinlatmaCikisSayisi)
            .OrderBy(a => a.AnmaAkimA).ThenBy(a => a.CikisSayisi)
            .FirstOrDefault()
            ?? kutuphane.AstronomikZamanRoleleri.OrderByDescending(a => a.AnmaAkimA).FirstOrDefault();
    }

    /// <summary>
    /// Kontaktör seçimi — linye akımının üstünde anma akıma sahip olanı bulur.
    /// </summary>
    public static KontaktorTanim? KontaktorSec(double linyeAkimA, ElektrikKutuphaneServisi kutuphane)
    {
        return kutuphane.Kontaktorler
            .Where(k => k.AnmaAkimA >= linyeAkimA)
            .OrderBy(k => k.AnmaAkimA)
            .FirstOrDefault()
            ?? kutuphane.Kontaktorler.OrderByDescending(k => k.AnmaAkimA).FirstOrDefault();
    }

    /// <summary>
    /// Darbe kesici seçimi — linye akımının üstünde anma akıma sahip en küçük darbe kesiciyi bulur.
    /// </summary>
    public static DarbeKesiciTanim? DarbeKesiciSec(double linyeAkimA, ElektrikKutuphaneServisi kutuphane)
    {
        return kutuphane.DarbeKesiciler
            .Where(d => d.AnmaAkimA >= linyeAkimA)
            .OrderBy(d => d.AnmaAkimA)
            .FirstOrDefault()
            ?? kutuphane.DarbeKesiciler.OrderByDescending(d => d.AnmaAkimA).FirstOrDefault();
    }

    /// <summary>
    /// Modül sayısı hesabı — linyeler + KAR + giriş sigortası + %20 yedek.
    /// </summary>
    public static int ModulSayisiHesapla(Pano pano)
    {
        int linyeModul = pano.Linyeler.Count; // Her linye 1 modul
        int karModul = pano.KacakAkimRolesi == null
            ? 0
            : pano.KacakAkimRolesi.TroidTipiMi ? 3 : 4;
        int girisModul = 3; // 3 fazlı giriş otomatiği
        int altPanoModul = pano.AltPanolar.Count * 3; // Her alt pano bağlantısı

        int toplam = linyeModul + karModul + girisModul + altPanoModul;
        int yedek = (int)Math.Ceiling(toplam * 0.20);

        return toplam + yedek;
    }
}
