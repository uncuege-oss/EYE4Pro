using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;

namespace Kst.Acoustics.Core.Electric.BasitProjeCiz.Services;

public class BasitProjeCizKutuphaneServisi
{
    // v7 — Yangın algılama sembolleri kullanıcı referansına göre yenilendi
    //      (üçgen+O/H dedektörler, çift halka+K kombine, koni+dalgalı siren).
    // v8 — Priz/zayıf akım refleyti: Kombine Priz Kutusu 4 prizli (2 trifaze+2 mono),
    //      Data/TV/Telefon Priz priz-tabanlı (U) + ayırt edici simge, Hoparlör
    //      (tavan tipi: daire+H), Anons Amfisi (gerçek amplifikatör: kutu + 2 VU).
    // v9 — Anahtar ailesi (Anahtar/Komütatör/Darbe/Vavien/Dimmer/Deviator/Kom-Vavien/
    //      İki Kutuplu) Türk mimari standardına çevrildi (dikey gövde + üst kontak
    //      dairesi). Sensör → PIR yarım kubbe. Kat Panosu → yamuk (Aydınlatma-Priz Tablosu).
    // v10 — Priz ailesi tamamen yenilendi (kullanıcı standardı): üst cap + stem + uzun
    //       chord + altta aşağı açılan dome (∪). Trifaze → 3 pin. UPS → dome içinde
    //       dolu elmas. Kombine kutusu → kutu + içinde 2 priz. İkili → iki dome ortak cap.
    //       Data/TV/Telefon → basit T-stili (dome yok), Data'da üstte üçgen.
    // v11 — Bug fix: SembolSec Pano kategorisinde BlokAdi'ye bakmıyordu → migration buat'ı
    //       pano-yamuğuyla eziyordu. BlokAdi switch eklendi (E4P_BUAT → BuatSembol).
    //       Buat → küçük dolu nokta. Anahtar ailesi yeniden çizildi (daire ORTADA, kol
    //       yukarı, alt duvar bağlantısı): Anahtar (tek kol), Komütatör (V), Darbe (iç
    //       dolu nokta), Vavien (iki çapraz kol + tick), Deviator (daire+X), Dimmer
    //       (anahtar+üçgen), İki Kutuplu (iki dikey kol), Kom-Vavien (V + sağ-alt çapraz).
    // v12 — E4P_KOMUTATÖR → E4P_KOMUTAT (ASCII-safe; AutoCAD Ö karakterini düşürüyor).
    //       TipBlokAdindan'da her ikisi de kabul edilir (eski proje uyumluluğu).
    // v13 — Global malzeme kütüphanesi temeli ve yeni M12 malzeme kataloğu.
    // v14 — Aydınlatma kataloğu yalnız kullanıcı tanımlı M12 listesine indirildi.
    // v15 — Priz ailesi geometri düzeltmesi: toprak T/pim dome tepesine indirildi
    //       (PrizSembol/Trifaze/İkili/Üçlü/Kombine). OlusturBlok merkezleme eklendi.
    //       Yükleme sırası: gömülü DXF önce, sonra programatik — programatik her zaman kazanır.
    // v16 yalnız alt pano tanımını ekler; kullanıcıların DXF sembolleri korunur.
    private const int GuncelSembolSetVersion = 16;

    /// Uygulamayla gelen gömülü DXF sembol şablonunun (Sablonlar/EYE4PRO_Sembol_Sablonu.dxf)
    /// güncel sürümü. Kullanıcının kayıtlı `GomuluSablonSurum` değeri bundan küçükse açılışta
    /// şablon yeniden uygulanır (yeni şablon yayınlandığında bu sayı artırılır).
    private const int GuncelGomuluSablonSurum = 2;
    /// Gömülü kaynağın manifest adı (csproj'daki LogicalName ile birebir).
    private const string GomuluSablonKaynakAdi = "EYE4PRO_Sembol_Sablonu.dxf";

    private readonly string _filePath;
    /// Bu oturumda uygulanmış gömülü şablon sürümü; `Kaydet()` bunu JSON'a yazar.
    private int _uygulananGomuluSablonSurum;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public List<BpcArmatürTanim> Armatürler { get; private set; } = new();
    public List<BpcArmatürTanim> Anahtarlar { get; private set; } = new();
    public List<BpcArmatürTanim> Panolar    { get; private set; } = new();
    public List<BpcArmatürTanim> Prizler    { get; private set; } = new();
    public List<BpcArmatürTanim> Klimalar   { get; private set; } = new();
    public List<BpcArmatürTanim> Malzemeler { get; private set; } = new();
    private static readonly HashSet<string> IzinliAydinlatmaBloklari = new(StringComparer.OrdinalIgnoreCase)
    {
        "E4P_LED_PANEL_60x60",
        "E4P_LED_PANEL_30x30",
        "E4P_LED_PANEL_60x30",
        "E4P_LED_PANEL_120x30",
        "E4P_YUKSEK_TAVAN_DIK",
        "E4P_YUKSEK_TAVAN_YUV",
        "E4P_PROJEKTOR",
        "E4P_AVIZE",
        "E4P_U_LED",
        "E4P_LINEAR",
        "E4P_DOWNLIGHT",
        "E4P_APLIK",
        "E4P_SPOT",
        "E4P_BANT_ARM",
        "E4P_KANOPI",
        "E4P_ACIL_ARM",
        "E4P_ACIL_YON",
        "E4P_EXPROOF",
        "E4P_OZEL_ARM"
    };
    /// Zayıf akım sembolleri için BlokAdi → çizgiler override haritası.
    /// `ZayifAkimOverrides` static alanına da kopyalanır (anlık tanım üretiminde okunur).
    public Dictionary<string, List<BpcSembolCizgi>> ZayifAkimSemboller { get; private set; } = new();

    public BasitProjeCizKutuphaneServisi()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Eye4Pro", "Electric", "BasitProjeCiz");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "AydinlatmaArmaturKutuphanesi.json");
        Yukle();
    }

    public void Yukle()
    {
        if (!File.Exists(_filePath))
        {
            VarsayilanlariYukle();
            GomuluSembolSablonunuUygula();  // gömülü DXF base layer
            GuncelSembolSetiniUygula();     // programatik geometri üste yazılır — doğru geometri garantili
            _uygulananGomuluSablonSurum = GuncelGomuluSablonSurum;
            TryKaydet();
            return;
        }

        BpcKutuphaneData? data = null;
        List<BpcArmatürTanim>? eskiListe = null;

        try
        {
            var json = File.ReadAllText(_filePath);

            // 1) Yeni format (BpcKutuphaneData) dene
            try { data = JsonSerializer.Deserialize<BpcKutuphaneData>(json, JsonOptions); }
            catch (JsonException) { /* eski format olabilir */ }

            // 2) Yeni format değilse, eski liste formatını dene
            if (data == null)
            {
                try { eskiListe = JsonSerializer.Deserialize<List<BpcArmatürTanim>>(json, JsonOptions); }
                catch (JsonException) { /* tamamen okunamıyor */ }
            }
        }
        catch (Exception ex)
        {
            // Okuma hatası — defaults yükleyip yeni formatta kaydet.
            // Gerçek disk/izin hatasını tamamen yutmamak için tanılama izi bırak.
            System.Diagnostics.Debug.WriteLine($"[Kutuphane] Okuma hatası, varsayılanlar yüklendi: {ex.Message}");
            VarsayilanlariYukle();
            TryKaydet();
            return;
        }

        var sembolSetVersion = data?.SembolSetVersion ?? 0;
        if (data != null)
        {
            Armatürler = data.Armatürler?.Count > 0 ? data.Armatürler : VarsayilanArmatürler();
            Anahtarlar = data.Anahtarlar?.Count > 0 ? data.Anahtarlar : VarsayilanAnahtarlar();
            Panolar    = data.Panolar?.Count    > 0 ? data.Panolar    : VarsayilanPanolar();
            Prizler    = data.Prizler?.Count    > 0 ? data.Prizler    : VarsayilanPrizler();
            Klimalar   = data.Klimalar?.Count   > 0 ? data.Klimalar   : VarsayilanKlimalar();
            Malzemeler  = data.Malzemeler?.Count > 0 ? data.Malzemeler : new();
            ZayifAkimSemboller = data.ZayifAkimSemboller ?? new();
        }
        else
        {
            // Eski format (List<BpcArmatürTanim>) ya da hiç okunamadı
            Armatürler = eskiListe ?? VarsayilanArmatürler();
            Anahtarlar = VarsayilanAnahtarlar();
            Panolar    = VarsayilanPanolar();
            Prizler    = VarsayilanPrizler();
            Klimalar   = VarsayilanKlimalar();
        }

        // Göç ve güncelleme mantığı (Trifaze güç -> 1000W, Kombin -> 3000W, Kombin ad -> Kombine Priz Panosu, sembol -> güncel geometrisi)
        var trifaze = Prizler.FirstOrDefault(p => p.BlokAdi == "E4P_TRIFAZE_PRIZ");
        if (trifaze != null && (trifaze.GucW == 3000 || trifaze.GucW == 0))
        {
            trifaze.GucW = 1000;
        }
        var kombin = Prizler.FirstOrDefault(p => p.BlokAdi == "E4P_KOMBIN_PRIZ");
        if (kombin != null)
        {
            kombin.GucW = 3000;
            kombin.Ad = "Kombine Priz Panosu";
            kombin.SembolCizgiler = KombinePrizKutusuSembol();
        }

        // v12 göçü: E4P_KOMUTATÖR → E4P_KOMUTAT (AutoCAD Ö karakterini blok adından düşürüyor)
        foreach (var a in Anahtarlar)
        {
            if (string.Equals(a.BlokAdi, "E4P_KOMUTATÖR", StringComparison.OrdinalIgnoreCase))
                a.BlokAdi = "E4P_KOMUTAT";
        }

        EksikSembollerDoldur(Armatürler, ElemanKategorisi.Armatür);
        EksikSembollerDoldur(Anahtarlar, ElemanKategorisi.Anahtar);
        EksikSembollerDoldur(Panolar,    ElemanKategorisi.Pano);
        EksikSembollerDoldur(Prizler,    ElemanKategorisi.Priz);
        EksikSembollerDoldur(Klimalar,   ElemanKategorisi.Klima);

        // Gömülü DXF sembol şablonu ÖNCE uygulanır (base layer).
        // GlobalMalzemeleriHazirla'dan önce ama EksikSembollerDoldur'dan sonra.
        var gomuluSurum = data?.GomuluSablonSurum ?? 0;
        if (gomuluSurum < GuncelGomuluSablonSurum)
        {
            GomuluSembolSablonunuUygula();
            _uygulananGomuluSablonSurum = GuncelGomuluSablonSurum;
        }
        else
        {
            _uygulananGomuluSablonSurum = gomuluSurum;
        }

        // Programatik sembol seti SONRA uygulanır → her zaman gömülü DXF'i ezer.
        // Böylece kod değişikliği (versiyon yükseltme) gömülü şablondaki eski
        // geometrinin üzerine yazar; yükleme bozulması önlenir.
        if (sembolSetVersion < 15)
            GuncelSembolSetiniUygula();
        else if (sembolSetVersion < 16)
            VarsayilanEksikleriEkle(Panolar, [AltPanoTanimi()]);

        AydinlatmaKatalogunuKisitla();
        ArmatürYerlesimleriniGridYap();
        GlobalMalzemeleriHazirla();

        // Static override haritasını güncelle (anlık ZayifAkimTanim çağrılarında kullanılır)
        ZayifAkimOverrides = ZayifAkimSemboller;

        // Her durumda yeni formatta + güncel sembol setiyle kaydet (eski format dosyaları taşı).
        TryKaydet();
    }

    private void VarsayilanlariYukle()
    {
        Armatürler = VarsayilanArmatürler();
        Anahtarlar = VarsayilanAnahtarlar();
        Panolar    = VarsayilanPanolar();
        Prizler    = VarsayilanPrizler();
        Klimalar   = VarsayilanKlimalar();
        GlobalMalzemeleriHazirla();
    }

    private void TryKaydet()
    {
        try { Kaydet(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[M12] Kütüphane kaydedilemedi: {ex.Message}"); }
    }

    // ── DXF üzerinden sembol şablonu dışa aktarma + içe aktarma köprüleri ────

    /// <summary>
    /// Mevcut kütüphaneyi DXF şablonu olarak dışa aktarır. AutoCAD'de açıp block'ları
    /// düzenleyip kaydet → sonra <see cref="SembolleriDxftenYukle"/> ile uygulanır.
    /// </summary>
    public void SablonDxfDisariAktar(string outputPath)
        => new BasitProjeCizSembolDxfServisi().SablonDisariAktar(this, outputPath);

    /// <summary>
    /// DXF'teki block tanımlarını okuyup eşleşen kütüphane sembollerini günceller.
    /// İşlem öncesi mevcut JSON otomatik yedeklenir (`*.backup.yyyyMMdd-HHmmss.json`).
    /// Sonuç olarak güncellenen sayı + eşleşmeyen block adları döner.
    /// </summary>
    public BasitProjeCizSembolDxfServisi.ImportSonuc SembolleriDxftenYukle(string dxfPath)
    {
        // Yedek al (import sonrası beğenmezsen el ile geri alabilirsin)
        try
        {
            if (File.Exists(_filePath))
            {
                var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                var backup = Path.Combine(
                    Path.GetDirectoryName(_filePath)!,
                    $"AydinlatmaArmaturKutuphanesi.backup.{stamp}.json");
                File.Copy(_filePath, backup, overwrite: false);
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[M12] Kütüphane yedeği alınamadı: {ex.Message}"); }

        var sonuc = new BasitProjeCizSembolDxfServisi().SembolleriYukle(dxfPath, this);
        if (sonuc.Guncellendi > 0)
        {
            // Static override haritasını tazele (zayıf akım için)
            ZayifAkimOverrides = ZayifAkimSemboller;
            TryKaydet();
        }
        return sonuc;
    }

    /// <summary>
    /// Uygulamayla gelen gömülü DXF sembol şablonunu (manifest kaynağı) okuyup eşleşen
    /// kütüphane sembollerinin geometrisini günceller. Kaynak bulunamazsa veya okunamazsa
    /// sessizce programatik sembollerle devam edilir (uygulama açılışı bloke edilmez).
    /// </summary>
    private void GomuluSembolSablonunuUygula()
    {
        try
        {
            var asm = typeof(BasitProjeCizKutuphaneServisi).Assembly;
            using var stream = asm.GetManifestResourceStream(GomuluSablonKaynakAdi);
            if (stream == null) return; // kaynak gömülmemişse fallback: programatik semboller
            var sonuc = new BasitProjeCizSembolDxfServisi().SembolleriYukle(stream, this);
            if (sonuc.Guncellendi > 0)
                ZayifAkimOverrides = ZayifAkimSemboller;
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[M12] Gömülü sembol şablonu uygulanamadı: {ex.Message}"); }
    }

    private enum ElemanKategorisi { Armatür, Anahtar, Pano, Priz, Klima }

    public void Kaydet()
    {
        ArmatürYerlesimleriniGridYap();

        var data = new BpcKutuphaneData
        {
            SembolSetVersion = GuncelSembolSetVersion,
            GomuluSablonSurum = _uygulananGomuluSablonSurum,
            Malzemeler = GlobalMalzemeleriOlustur(),
            Armatürler = Armatürler,
            Anahtarlar = Anahtarlar,
            Panolar    = Panolar,
            Prizler    = Prizler,
            Klimalar   = Klimalar,
            ZayifAkimSemboller = ZayifAkimSemboller
        };
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(data, JsonOptions));
    }

    private void ArmatürYerlesimleriniGridYap()
    {
        foreach (var a in Armatürler)
            a.YerlesimMantigi = BpcArmaturlestirmeMantigi.GridDizilim;
    }

    private void GlobalMalzemeleriHazirla()
    {
        KategorileriTamamla();
        AydinlatmaKatalogunuKisitla();
        if (Malzemeler.Count == 0)
            Malzemeler = GlobalMalzemeleriOlustur();
        else
        {
            Malzemeler.RemoveAll(m => m.KutuphaneKategorisi == BpcKutuphaneKategorisi.Aydinlatma &&
                                      !IzinliAydinlatmaBloklari.Contains(m.BlokAdi));
            VarsayilanEksikleriEkle(Malzemeler, GlobalMalzemeleriOlustur());
        }
    }

    private void AydinlatmaKatalogunuKisitla()
    {
        Armatürler = AydinlatmaListesiniKisitla(Armatürler);

        if (Malzemeler.Count > 0)
        {
            Malzemeler.RemoveAll(m => m.KutuphaneKategorisi == BpcKutuphaneKategorisi.Aydinlatma);
            Malzemeler.InsertRange(0, Armatürler);
        }
    }

    private static List<BpcArmatürTanim> AydinlatmaListesiniKisitla(List<BpcArmatürTanim> mevcut)
    {
        var mevcutMap = mevcut
            .Where(m => IzinliAydinlatmaBloklari.Contains(m.BlokAdi))
            .GroupBy(m => m.BlokAdi, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var sonuc = new List<BpcArmatürTanim>();
        foreach (var varsayilan in VarsayilanArmatürler())
        {
            if (mevcutMap.TryGetValue(varsayilan.BlokAdi, out var eski))
            {
                varsayilan.Id = eski.Id;
                if (eski.SembolCizgiler.Count > 0)
                    varsayilan.SembolCizgiler = eski.SembolCizgiler;
            }
            sonuc.Add(varsayilan);
        }
        return sonuc;
    }

    private void KategorileriTamamla()
    {
        foreach (var a in Armatürler)
        {
            a.KutuphaneGrubu = BpcKutuphaneGrubu.KuvvetliAkim;
            a.KutuphaneKategorisi = BpcKutuphaneKategorisi.Aydinlatma;
            a.ElemanTipi ??= TipBlokAdindan(a.BlokAdi) ?? BpcElemanTipi.Armatür;
            a.MontajTipi = DuvarMontajliAydinlatmaMi(a.BlokAdi) ? "Duvar" : "Tavan";
        }
        foreach (var a in Anahtarlar)
        {
            a.KutuphaneGrubu = BpcKutuphaneGrubu.KuvvetliAkim;
            a.KutuphaneKategorisi = BpcKutuphaneKategorisi.Anahtar;
            a.ElemanTipi ??= TipBlokAdindan(a.BlokAdi) ?? BpcElemanTipi.Anahtar;
            a.MontajTipi = a.ElemanTipi == BpcElemanTipi.Sensör ? "Tavan" : "Duvar";
        }
        foreach (var a in Panolar)
        {
            a.KutuphaneGrubu = BpcKutuphaneGrubu.KuvvetliAkim;
            a.KutuphaneKategorisi = BpcKutuphaneKategorisi.Pano;
            a.ElemanTipi ??= TipBlokAdindan(a.BlokAdi) ?? BpcElemanTipi.KatPanosu;
            a.MontajTipi = a.ElemanTipi == BpcElemanTipi.Buat ? "Tavan" : "Duvar";
        }
        foreach (var a in Prizler)
        {
            a.KutuphaneGrubu = BpcKutuphaneGrubu.KuvvetliAkim;
            a.KutuphaneKategorisi = BpcKutuphaneKategorisi.Priz;
            a.ElemanTipi ??= TipBlokAdindan(a.BlokAdi) ?? BpcElemanTipi.Priz;
            a.MontajTipi = "Duvar";
            if (a.BlokAdi == "E4P_KOMBIN_PRIZ")
            {
                a.VarsayilanMonofazeAdet = a.VarsayilanMonofazeAdet == 0 ? 2 : a.VarsayilanMonofazeAdet;
                a.VarsayilanTrifazeAdet = a.VarsayilanTrifazeAdet == 0 ? 2 : a.VarsayilanTrifazeAdet;
                a.OzelAlanTuru = "KombinePrizKutusu";
            }
            if (a.BlokAdi == "E4P_USB_MASA")
            {
                a.VarsayilanAdet = Math.Max(1, a.VarsayilanAdet);
                a.OzelAlanTuru = "Adet";
            }
        }
        foreach (var a in Klimalar)
        {
            a.KutuphaneGrubu = BpcKutuphaneGrubu.KuvvetliAkim;
            a.KutuphaneKategorisi = BpcKutuphaneKategorisi.Mekanik;
            a.ElemanTipi ??= TipBlokAdindan(a.BlokAdi) ?? BpcElemanTipi.KlimaIcUnite;
            a.MontajTipi = "Duvar";
        }
    }

    private static bool DuvarMontajliAydinlatmaMi(string blokAdi)
        => blokAdi is "E4P_APLIK" or "E4P_ACIL_ARM";

    private List<BpcArmatürTanim> GlobalMalzemeleriOlustur()
    {
        var sonuc = new List<BpcArmatürTanim>();
        sonuc.AddRange(Armatürler);
        sonuc.AddRange(Prizler);
        sonuc.AddRange(Anahtarlar);
        sonuc.AddRange(Panolar);
        sonuc.AddRange(Klimalar);

        // Zayıf akım malzemeleri her çağrıda programatik sembolle yeniden üretilir;
        // varsa DXF/kullanıcı override geometrisini (ZayifAkimSemboller) burada uygula ki
        // palet ve kaydedilen kütüphane güncel geometriyi taşısın.
        var zayif = VarsayilanZayifAkimMalzemeleri();
        if (ZayifAkimSemboller.Count > 0)
        {
            foreach (var z in zayif)
            {
                if (!string.IsNullOrWhiteSpace(z.BlokAdi) &&
                    ZayifAkimSemboller.TryGetValue(z.BlokAdi, out var segs) && segs.Count > 0)
                    z.SembolCizgiler = segs;
            }
        }
        sonuc.AddRange(zayif);
        return sonuc;
    }

    private void GuncelSembolSetiniUygula()
    {
        VarsayilanEksikleriEkle(Armatürler, VarsayilanArmatürler());
        VarsayilanEksikleriEkle(Anahtarlar, VarsayilanAnahtarlar());
        VarsayilanEksikleriEkle(Panolar,    VarsayilanPanolar());
        VarsayilanEksikleriEkle(Prizler,    VarsayilanPrizler());
        VarsayilanEksikleriEkle(Klimalar,   VarsayilanKlimalar());
        GuncelSembolleriAta(Armatürler, ElemanKategorisi.Armatür);
        GuncelSembolleriAta(Anahtarlar, ElemanKategorisi.Anahtar);
        GuncelSembolleriAta(Panolar,    ElemanKategorisi.Pano);
        GuncelSembolleriAta(Prizler,    ElemanKategorisi.Priz);
        GuncelSembolleriAta(Klimalar,   ElemanKategorisi.Klima);
    }

    private static void VarsayilanEksikleriEkle(List<BpcArmatürTanim> hedef, List<BpcArmatürTanim> varsayilanlar)
    {
        var mevcutBloklar = hedef
            .Where(x => !string.IsNullOrWhiteSpace(x.BlokAdi))
            .Select(x => x.BlokAdi)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var item in varsayilanlar)
        {
            if (string.IsNullOrWhiteSpace(item.BlokAdi) || mevcutBloklar.Contains(item.BlokAdi))
                continue;
            hedef.Add(item);
            mevcutBloklar.Add(item.BlokAdi);
        }
    }

    // ─── Eksik sembol dolumu ──────────────────────────────────────────────

    private static void EksikSembollerDoldur(List<BpcArmatürTanim> liste, ElemanKategorisi kat)
    {
        foreach (var a in liste)
        {
            if (a.SembolCizgiler.Count > 0) continue;
            a.SembolCizgiler.AddRange(SembolSec(a.BlokAdi, kat));
        }
    }

    private static void GuncelSembolleriAta(List<BpcArmatürTanim> liste, ElemanKategorisi kat)
    {
        foreach (var a in liste)
        {
            var sembol = SembolSec(a.BlokAdi, kat).ToList();
            if (sembol.Count > 0)
                a.SembolCizgiler = sembol;
        }
    }

    private static IEnumerable<BpcSembolCizgi> SembolSec(string blokAdi, ElemanKategorisi kat)
    {
        if (kat == ElemanKategorisi.Armatür)
        {
            return blokAdi switch
            {
                "E4P_LED_PANEL_60x60" => LedPanelSembol(),
                "E4P_LED_PANEL_30x30" => LedPanelSembol(),
                "E4P_LED_PANEL_60x30" => LineerSembol(),
                "E4P_LED_PANEL_120x30" => LineerSembol(),
                "E4P_DOWNLIGHT"       => DownlightSembol(),
                "E4P_SPOT"            => DownlightSembol(),
                "E4P_LINEAR"          => LineerSembol(),
                "E4P_U_LED"           => LineerSembol(),
                "E4P_CENTER_LIGHT"    => TekNoktaSembol(),
                "E4P_ETANJ"           => EtanjArmatürSembol(),
                "E4P_APLIK"           => AplikSembol(),
                "E4P_ACIL_YON"        => AcilYonlendirmeSembol(),
                "E4P_ACIL_ARM"        => AcilAydinlatmaSembol(),
                "E4P_PROJEKTOR"       => ProjektorSembol(),
                "E4P_YUKSEK_TAVAN_100W" or
                "E4P_YUKSEK_TAVAN_125W" or
                "E4P_YUKSEK_TAVAN_150W" or
                "E4P_YUKSEK_TAVAN_200W" or
                "E4P_YUKSEK_TAVAN_250W" => YuksekTavanArmaturSembol(),
                "E4P_YUKSEK_TAVAN_DIK" => YuksekTavanArmaturSembol(),
                "E4P_YUKSEK_TAVAN_YUV" => YuksekTavanArmaturSembol(),
                "E4P_SENSORLU_TAVAN"  => SensorluTavanArmatürüSembol(),
                "E4P_SARKIT"          => SarkitArmaturSembol(),
                "E4P_GLOP"            => GlopArmaturSembol(),
                "E4P_CIP_LAMBA"       => CiplakLambaSembol(),
                "E4P_BANT_ARM"        => BantArmaturSembol(),
                "E4P_AVIZE"           => AvizeSembol(),
                "E4P_WALL_WASHER"     => WallWasherSembol(),
                "E4P_FLUORESAN"       => FluoresanSembol(),
                "E4P_KANOPI"          => EtanjArmatürSembol(),
                "E4P_EXPROOF"         => EtanjArmatürSembol(),
                "E4P_OZEL_ARM"        => GenelArmatürSembol(),
                _                     => GenelArmatürSembol()
            };
        }
        if (kat == ElemanKategorisi.Anahtar)
        {
            return blokAdi switch
            {
                "E4P_ANAHTAR"       => AnahtarSembol(),
                "E4P_KOMUTAT"       => KomütatörSembol(),
                "E4P_3LU_KOMUTAT"    => KomütatörSembol(),
                "E4P_DARBE"         => DarbeAnahtarSembol(),
                "E4P_VAVIEN"        => VavienSembol(),
                "E4P_KOM_VAVIEN"    => KomütatörVavienSembol(),
                "E4P_DEVIATOR"      => DeviatorSembol(),
                "E4P_DIMMER"        => DimmerSembol(),
                "E4P_IKIKUTUP"      => IkiKutupluAnahtarSembol(),
                "E4P_SENSOR"        => SensorluTavanArmatürüSembol(),
                _                   => AnahtarSembol()
            };
        }
        if (kat == ElemanKategorisi.Priz)
        {
            return blokAdi switch
            {
                "E4P_KAPAKLI_PRIZ"  => KapakliPrizSembol(),
                "E4P_MUSTAKIL_PRIZ" => PrizSembol(),
                "E4P_SIVAUSTU_PRIZ" => SivaUstuPrizSembol(),
                "E4P_TRIFAZE_PRIZ"  => TrifazePrizSembol(),
                "E4P_KOMBIN_PRIZ"   => KombinePrizKutusuSembol(),
                "E4P_TAVAN_PRIZ"    => TavanPriziSembol(),
                "E4P_USB_MASA"      => UsbliMasaPriziSembol(),
                "E4P_UPS_PRIZ"      => UpsPriziSembol(),
                "E4P_KAPAKLI_UPS"   => KapakliUpsPriziSembol(),
                "E4P_SIVAUSTU_UPS"  => SivaUstuUpsPriziSembol(),
                "E4P_IKILI_PRIZ"    => IkiliPrizSembol(),
                "E4P_UCLU_PRIZ"     => UcluPrizSembol(),
                _                   => PrizSembol()
            };
        }
        if (kat == ElemanKategorisi.Klima)
        {
            return blokAdi switch
            {
                "E4P_KLIMA_DIS" => KlimaDisUniteSembol(),
                _               => KlimaIcUniteSembol()
            };
        }
        // Pano kategorisi — Buat da bu listede; BlokAdi'ye göre ayrıştır.
        return blokAdi switch
        {
            "E4P_BUAT"       => BuatSembol(),
            _                => KatPanosuSembol()
        };
    }

    // ─── Varsayılan kataloglar ────────────────────────────────────────────

    private static List<BpcArmatürTanim> VarsayilanArmatürler() =>
    [
        Aydinlatma("60x60 Panel Led", "E4P_LED_PANEL_60x60", "LP", 36, LedPanelSembol()),
        Aydinlatma("30x30 Panel Led", "E4P_LED_PANEL_30x30", "LP30", 18, LedPanelSembol()),
        Aydinlatma("60x30 Panel Led", "E4P_LED_PANEL_60x30", "LP6030", 24, LineerSembol()),
        Aydinlatma("120x30 Panel Led", "E4P_LED_PANEL_120x30", "LP12030", 40, LineerSembol()),
        Aydinlatma("Dikdörtgen Yüksek Tavan Armatürü", "E4P_YUKSEK_TAVAN_DIK", "YTD", 150, YuksekTavanArmaturSembol(), 8.0, 100),
        Aydinlatma("Yuvarlak Yüksek Tavan Armatürü", "E4P_YUKSEK_TAVAN_YUV", "YTY", 150, YuksekTavanArmaturSembol(), 8.0, 100),
        Aydinlatma("Projektör", "E4P_PROJEKTOR", "PRJ", 50, ProjektorSembol()),
        Aydinlatma("Avize", "E4P_AVIZE", "AV", 60, AvizeSembol()),
        Aydinlatma("U Tipi Led", "E4P_U_LED", "UL", 36, LineerSembol()),
        Aydinlatma("Lineer", "E4P_LINEAR", "LN", 24, LineerSembol(), 4.0, 12),
        Aydinlatma("Downlight", "E4P_DOWNLIGHT", "DL", 12, DownlightSembol(), 3.0, 6),
        Aydinlatma("Aplik", "E4P_APLIK", "AP", 12, AplikSembol(), montajTipi: "Duvar"),
        Aydinlatma("Spot", "E4P_SPOT", "SP", 8, DownlightSembol()),
        Aydinlatma("Bant Armatür", "E4P_BANT_ARM", "BA", 36, BantArmaturSembol(), 4.0, 12),
        Aydinlatma("Kanopi", "E4P_KANOPI", "KN", 100, EtanjArmatürSembol(), 6.0, 40),
        Aydinlatma("Acil Aydınlatma", "E4P_ACIL_ARM", "AA", 5, AcilAydinlatmaSembol(), 10.0, 20, "Duvar"),
        Aydinlatma("Acil Yönlendirme", "E4P_ACIL_YON", "AY", 3, AcilYonlendirmeSembol(), 15.0, 30),
        Aydinlatma("Exproof", "E4P_EXPROOF", "EX", 60, EtanjArmatürSembol(), 4.0, 12),
        Aydinlatma("Özel", "E4P_OZEL_ARM", "OZ", 0, GenelArmatürSembol(), 3.0, 12, "Tavan", "OzelArmatur")
    ];

    private static BpcArmatürTanim Aydinlatma(
        string ad,
        string blokAdi,
        string sembolAdi,
        double gucW,
        List<BpcSembolCizgi> sembol,
        double gridAraligiM = 3.0,
        double alanBasinaAdetM2 = 12.0,
        string montajTipi = "Tavan",
        string ozelAlanTuru = "")
        => new()
        {
            Ad = ad,
            EkranAdi = sembolAdi,
            BlokAdi = blokAdi,
            SembolAdi = sembolAdi,
            MontajTipi = montajTipi,
            GucW = gucW,
            YerlesimMantigi = BpcArmaturlestirmeMantigi.GridDizilim,
            GridAraligiM = gridAraligiM,
            AlanBasinaAdetM2 = alanBasinaAdetM2,
            OzelAlanTuru = ozelAlanTuru,
            SembolCizgiler = sembol
        };

    private static List<BpcArmatürTanim> VarsayilanAnahtarlar() =>
    [
        new() { Ad = "Anahtar",            BlokAdi = "E4P_ANAHTAR",      SembolAdi = "A",
                MontajTipi = "Duvar",       GucW = 0, SembolCizgiler = AnahtarSembol() },
        new() { Ad = "Komütatör",          BlokAdi = "E4P_KOMUTAT",      SembolAdi = "K",
                MontajTipi = "Duvar",       GucW = 0, SembolCizgiler = KomütatörSembol() },
        new() { Ad = "3'lü Komütatör",     BlokAdi = "E4P_3LU_KOMUTAT",  SembolAdi = "3K",
                MontajTipi = "Duvar",       GucW = 0, SembolCizgiler = KomütatörSembol() },
        new() { Ad = "Darbe Anahtarı",     BlokAdi = "E4P_DARBE",        SembolAdi = "D",
                MontajTipi = "Duvar",       GucW = 0, SembolCizgiler = DarbeAnahtarSembol() },
        new() { Ad = "Vavien",             BlokAdi = "E4P_VAVIEN",       SembolAdi = "V",
                MontajTipi = "Duvar",       GucW = 0, SembolCizgiler = VavienSembol() },
        new() { Ad = "Komütatör Vavien",   BlokAdi = "E4P_KOM_VAVIEN",   SembolAdi = "KV",
                MontajTipi = "Duvar",       GucW = 0, SembolCizgiler = KomütatörVavienSembol() },
        new() { Ad = "Deviatör",           BlokAdi = "E4P_DEVIATOR",     SembolAdi = "X",
                MontajTipi = "Duvar",       GucW = 0, SembolCizgiler = DeviatorSembol() },
        new() { Ad = "Dimmer",             BlokAdi = "E4P_DIMMER",       SembolAdi = "DIM",
                MontajTipi = "Duvar",       GucW = 0, SembolCizgiler = DimmerSembol() },
        new() { Ad = "İki Kutuplu Anahtar",BlokAdi = "E4P_IKIKUTUP",     SembolAdi = "2A",
                MontajTipi = "Duvar",       GucW = 0, SembolCizgiler = IkiKutupluAnahtarSembol() },
        new() { Ad = "Sensör",             BlokAdi = "E4P_SENSOR",       SembolAdi = "S",
                MontajTipi = "Tavan",       GucW = 0, SembolCizgiler = SensorluTavanArmatürüSembol() }
    ];

    private static List<BpcArmatürTanim> VarsayilanPanolar() =>
    [
        new() { Ad = "Kat Panosu",         BlokAdi = "E4P_KAT_PANOSU",   SembolAdi = "KP",
                MontajTipi = "Duvar",       GucW = 0, SembolCizgiler = KatPanosuSembol() },
        new() { Ad = "Tali Pano",          BlokAdi = "E4P_TALI_PANO",    SembolAdi = "TP",
                MontajTipi = "Duvar",       GucW = 0, SembolCizgiler = KatPanosuSembol() },
        AltPanoTanimi(),
        new() { Ad = "Ana Pano",           BlokAdi = "E4P_ANA_PANO",     SembolAdi = "AP",
                MontajTipi = "Duvar",       GucW = 0, SembolCizgiler = KatPanosuSembol() },
        new() { Ad = "Özel Pano",          BlokAdi = "E4P_OZEL_PANO",    SembolAdi = "OP",
                MontajTipi = "Duvar",       GucW = 0, OzelAlanTuru = "PanoGucListesi", SembolCizgiler = KatPanosuSembol() },
        new() { Ad = "Buat",               BlokAdi = "E4P_BUAT",          SembolAdi = "B",
                MontajTipi = "Tavan",       GucW = 0, SembolCizgiler = BuatSembol() },
        new() { Ad = "Yer Buatı",          BlokAdi = "E4P_YER_BUATI",    SembolAdi = "YB",
                MontajTipi = "Duvar",       GucW = 0, SembolCizgiler = BuatSembol() }
    ];

    private static BpcArmatürTanim AltPanoTanimi() => new()
    {
        Ad = "Alt Pano", BlokAdi = "E4P_ALT_PANO", SembolAdi = "ALP",
        MontajTipi = "Duvar", SembolCizgiler = KatPanosuSembol()
    };

    private static List<BpcArmatürTanim> VarsayilanPrizler() =>
    [
        new() { Ad = "Topraklı Priz",        BlokAdi = "E4P_PRIZ",          SembolAdi = "P",   MontajTipi = "Duvar", GucW = 300, SembolCizgiler = PrizSembol() },
        new() { Ad = "Kapaklı Priz",         BlokAdi = "E4P_KAPAKLI_PRIZ",  SembolAdi = "KP",  MontajTipi = "Duvar", GucW = 300, SembolCizgiler = KapakliPrizSembol() },
        new() { Ad = "Müstakil Priz",        BlokAdi = "E4P_MUSTAKIL_PRIZ", SembolAdi = "MP",  MontajTipi = "Duvar", GucW = 300, SembolCizgiler = PrizSembol() },
        new() { Ad = "Sıvaüstü Priz",        BlokAdi = "E4P_SIVAUSTU_PRIZ", SembolAdi = "SP",  MontajTipi = "Duvar", GucW = 300, SembolCizgiler = SivaUstuPrizSembol() },
        new() { Ad = "Trifaze Priz",         BlokAdi = "E4P_TRIFAZE_PRIZ",  SembolAdi = "3P",  MontajTipi = "Duvar", GucW = 1000,SembolCizgiler = TrifazePrizSembol() },
        new() { Ad = "Kombine Priz Kutusu",  BlokAdi = "E4P_KOMBIN_PRIZ",   SembolAdi = "KPK", MontajTipi = "Duvar", GucW = 3000,
                VarsayilanMonofazeAdet = 2, VarsayilanTrifazeAdet = 2, OzelAlanTuru = "KombinePrizKutusu",
                SembolCizgiler = KombinePrizKutusuSembol() },
        new() { Ad = "Tavan Prizi",          BlokAdi = "E4P_TAVAN_PRIZ",    SembolAdi = "TP",  MontajTipi = "Tavan", GucW = 300, SembolCizgiler = TavanPriziSembol() },
        new() { Ad = "USB'li Masa Prizi",    BlokAdi = "E4P_USB_MASA",      SembolAdi = "USB", MontajTipi = "Duvar", GucW = 150,
                VarsayilanAdet = 1, OzelAlanTuru = "Adet", SembolCizgiler = UsbliMasaPriziSembol() },
        new() { Ad = "UPS Prizi",            BlokAdi = "E4P_UPS_PRIZ",      SembolAdi = "UPS", MontajTipi = "Duvar", GucW = 300, SembolCizgiler = UpsPriziSembol() },
        new() { Ad = "Kapaklı UPS Prizi",    BlokAdi = "E4P_KAPAKLI_UPS",   SembolAdi = "KUPS",MontajTipi = "Duvar", GucW = 300, SembolCizgiler = KapakliUpsPriziSembol() },
        new() { Ad = "Sıva Üstü UPS Prizi",  BlokAdi = "E4P_SIVAUSTU_UPS",  SembolAdi = "SUPS",MontajTipi = "Duvar", GucW = 300, SembolCizgiler = SivaUstuUpsPriziSembol() },
        new() { Ad = "İkili Priz",           BlokAdi = "E4P_IKILI_PRIZ",    SembolAdi = "2P",  MontajTipi = "Duvar", GucW = 600, SembolCizgiler = IkiliPrizSembol() },
        new() { Ad = "Üçlü Priz",            BlokAdi = "E4P_UCLU_PRIZ",     SembolAdi = "3P",  MontajTipi = "Duvar", GucW = 900, SembolCizgiler = UcluPrizSembol() }
    ];

    private static List<BpcArmatürTanim> VarsayilanKlimalar() =>
    [
        new() { Ad = "Klima İç Ünite",  BlokAdi = "E4P_KLIMA_IC",  SembolAdi = "KİÜ", MontajTipi = "Duvar", GucW = 100,  SembolCizgiler = KlimaIcUniteSembol() },
        new() { Ad = "Klima Dış Ünite", BlokAdi = "E4P_KLIMA_DIS", SembolAdi = "KDÜ", MontajTipi = "Duvar", GucW = 2000, SembolCizgiler = KlimaDisUniteSembol() }
    ];

    private static List<BpcArmatürTanim> VarsayilanZayifAkimMalzemeleri() =>
    [
        ZayifMalzeme("Tekli Data Prizi", "E4P_DATA_PRIZ", "D", BpcKutuphaneKategorisi.Data, BpcElemanTipi.DataPriz, DataPrizSembol(), "Duvar"),
        ZayifMalzeme("Çiftli Data Prizi", "E4P_DATA_PRIZ_2", "2D", BpcKutuphaneKategorisi.Data, BpcElemanTipi.DataPriz, DataPrizSembol(), "Duvar", "Adet"),
        ZayifMalzeme("Rack Kabin", "E4P_DATA_RACK", "RACK", BpcKutuphaneKategorisi.Data, BpcElemanTipi.DataRack, DataRackSembol(), "Duvar"),
        ZayifMalzeme("Ana Merkez", "E4P_DATA_MERKEZ", "DM", BpcKutuphaneKategorisi.Data, BpcElemanTipi.DataRack, DataRackSembol(), "Duvar"),

        ZayifMalzeme("TV Prizi", "E4P_TV_PRIZ", "TV", BpcKutuphaneKategorisi.Tv, BpcElemanTipi.TvPriz, TvPrizSembol(), "Duvar"),
        ZayifMalzeme("Fiber Optik Prizi", "E4P_FIBER_PRIZ", "FO", BpcKutuphaneKategorisi.Tv, BpcElemanTipi.TvPriz, TvPrizSembol(), "Duvar"),
        ZayifMalzeme("Kat Dağıtım Panosu", "E4P_TV_KDP", "KDP", BpcKutuphaneKategorisi.Tv, BpcElemanTipi.KatDagitimKutusu, KatDagitimKutusuSembol(), "Duvar"),
        ZayifMalzeme("TV Santrali", "E4P_TV_MERKEZ", "TVM", BpcKutuphaneKategorisi.Tv, BpcElemanTipi.TvMerkezi, TvMerkeziSembol(), "Duvar"),

        ZayifMalzeme("Tel Prizi", "E4P_TEL_PRIZ", "T", BpcKutuphaneKategorisi.Telefon, BpcElemanTipi.TelefonPriz, TelefonPrizSembol(), "Duvar"),
        ZayifMalzeme("Kat Dağıtım Panosu", "E4P_TEL_KDP", "KDP", BpcKutuphaneKategorisi.Telefon, BpcElemanTipi.TelefonDagitim, TelefonDagitimSembol(), "Duvar"),
        ZayifMalzeme("Tel Santrali", "E4P_TEL_DAG", "MDF", BpcKutuphaneKategorisi.Telefon, BpcElemanTipi.TelefonDagitim, TelefonDagitimSembol(), "Duvar"),

        ZayifMalzeme("Harici Kamera", "E4P_KAMERA_HARICI", "HCAM", BpcKutuphaneKategorisi.Cctv, BpcElemanTipi.Kamera, KameraSembol(), "Duvar", "Megapiksel"),
        ZayifMalzeme("Dahili Kamera", "E4P_KAMERA", "CAM", BpcKutuphaneKategorisi.Cctv, BpcElemanTipi.Kamera, KameraSembol(), "Tavan", "Megapiksel"),
        ZayifMalzeme("Kat Dağıtım Kabini", "E4P_CCTV_KDK", "KDK", BpcKutuphaneKategorisi.Cctv, BpcElemanTipi.AgAnahtari, AgAnahtariSembol(), "Duvar"),
        ZayifMalzeme("Kayıt Cihazı", "E4P_NVR", "NVR", BpcKutuphaneKategorisi.Cctv, BpcElemanTipi.CctvKayit, CctvKayitSembol(), "Duvar"),

        ZayifMalzeme("Tavan Hoparlörü", "E4P_HOPARLOR", "HP", BpcKutuphaneKategorisi.AcilAnons, BpcElemanTipi.Hoparlor, HoparlorSembol(), "Tavan"),
        ZayifMalzeme("Duvar Hoparlörü", "E4P_DUVAR_HOPARLOR", "DHP", BpcKutuphaneKategorisi.AcilAnons, BpcElemanTipi.Hoparlor, HoparlorSembol(), "Duvar"),
        ZayifMalzeme("Kat Dağıtım Kutusu", "E4P_ANONS_KDK", "KDK", BpcKutuphaneKategorisi.AcilAnons, BpcElemanTipi.KatDagitimKutusu, KatDagitimKutusuSembol(), "Duvar"),
        ZayifMalzeme("Anons Merkezi", "E4P_ANONS_AMFI", "AMF", BpcKutuphaneKategorisi.AcilAnons, BpcElemanTipi.AnonsAmfi, AnonsAmfiSembol(), "Duvar"),

        ZayifMalzeme("Kapı Zili", "E4P_KAPI_ZILI", "KZ", BpcKutuphaneKategorisi.Diafon, BpcElemanTipi.KapiZili, KapiZiliSembol(), "Duvar"),
        ZayifMalzeme("Diafon", "E4P_DIAFON", "DF", BpcKutuphaneKategorisi.Diafon, BpcElemanTipi.Diafon, DiafonSembol(), "Duvar"),
        ZayifMalzeme("Diafon Santrali", "E4P_DIAFON_SAN", "DFS", BpcKutuphaneKategorisi.Diafon, BpcElemanTipi.DiafonSantrali, DiafonSantraliSembol(), "Duvar"),

        ZayifMalzeme("Isı Dedektörü", "E4P_ISI_DED", "ID", BpcKutuphaneKategorisi.YanginAlgilama, BpcElemanTipi.IsiDedektor, IsiDedektorSembol(), "Tavan"),
        ZayifMalzeme("Kombine Dedektör", "E4P_KOMBINE_DED", "KD", BpcKutuphaneKategorisi.YanginAlgilama, BpcElemanTipi.KombineDedektor, KombineDedektorSembol(), "Tavan"),
        ZayifMalzeme("Duman Dedektörü", "E4P_DUMAN_DED", "DD", BpcKutuphaneKategorisi.YanginAlgilama, BpcElemanTipi.DumanDedektor, DumanDedektorSembol(), "Tavan"),
        ZayifMalzeme("Asma Tavan Armatürü", "E4P_ASMA_TAV_ARM", "ATA", BpcKutuphaneKategorisi.YanginAlgilama, BpcElemanTipi.AsmaTavanDedektorGrubu, AsmaTavanDedektorGrubuSembol(), "Tavan"),
        ZayifMalzeme("Buton", "E4P_YANGIN_BTN", "YB", BpcKutuphaneKategorisi.YanginAlgilama, BpcElemanTipi.YanginButonu, YanginButonuSembol(), "Duvar"),
        ZayifMalzeme("Siren", "E4P_SIREN", "SR", BpcKutuphaneKategorisi.YanginAlgilama, BpcElemanTipi.Siren, SirenSembol(), "Duvar"),
        ZayifMalzeme("Flaşörlü Siren", "E4P_FLASOR_SIREN", "FS", BpcKutuphaneKategorisi.YanginAlgilama, BpcElemanTipi.Siren, SirenSembol(), "Duvar"),
        ZayifMalzeme("Yangın Santrali", "E4P_YANGIN_SAN", "YSP", BpcKutuphaneKategorisi.YanginAlgilama, BpcElemanTipi.YanginSantrali, YanginSantraliSembol(), "Duvar")
    ];

    private static BpcArmatürTanim ZayifMalzeme(
        string ad,
        string blokAdi,
        string sembolAdi,
        BpcKutuphaneKategorisi kategori,
        BpcElemanTipi tip,
        List<BpcSembolCizgi> sembol,
        string montajTipi,
        string ozelAlanTuru = "")
        => new()
        {
            KutuphaneGrubu = BpcKutuphaneGrubu.ZayifAkim,
            KutuphaneKategorisi = kategori,
            ElemanTipi = tip,
            Ad = ad,
            EkranAdi = sembolAdi,
            BlokAdi = blokAdi,
            SembolAdi = sembolAdi,
            MontajTipi = montajTipi,
            OzelAlanTuru = ozelAlanTuru,
            SembolCizgiler = sembol
        };

    // ─── Sembol geometrileri — Aydınlatma ────────────────────────────────
    // Türkiye'de mimari elektrik planlarında okunabilir, sade blok dili:
    // tavan elemanları daire/dikdörtgen içinde çarpı, duvar elemanları küçük merkez + kol,
    // prizler daire/kutu + tip farkı. Koordinatlar ±1.0 normalize.

    public static List<BpcSembolCizgi> LedPanelSembol()
    {
        // LED Panel: Kare (içi boş veya tek köşegen) - EMO Standart
        var l = Kare(0, 0, 0.65).ToList();
        l.Add(new(-0.65, -0.65, 0.65, 0.65));
        return l;
    }

    public static List<BpcSembolCizgi> DownlightSembol()
    {
        // Downlight/Spot: Daire içinde çarpı (X)
        var l = Cember(0, 0, 0.60, 16).ToList();
        double d = 0.60 * 0.707;
        l.Add(new(-d, -d, d, d));
        l.Add(new(-d, d, d, -d));
        return l;
    }

    public static List<BpcSembolCizgi> LineerSembol()
    {
        var l = Dikdortgen(-0.95, -0.22, 0.95, 0.22).ToList();
        l.Add(new(-0.95, 0, 0.95, 0));
        return l;
    }

    public static List<BpcSembolCizgi> TekNoktaSembol()
    {
        // Standart tavan armatürü / lamba çıkışı: Daire içinde çarpı (X).
        var l = Cember(0, 0, 0.60, 16).ToList();
        double d = 0.60 * 0.707;
        l.Add(new(-d, -d, d, d));
        l.Add(new(-d, d, d, -d));
        return l;
    }

    public static List<BpcSembolCizgi> GenelArmatürSembol()
    {
        return TekNoktaSembol();
    }

    public static List<BpcSembolCizgi> EtanjArmatürSembol()
    {
        // Etanj: Dikdörtgen ve içinden geçen çizgi
        var l = Dikdortgen(-0.92, -0.36, 0.92, 0.36).ToList();
        l.Add(new(-1.10, 0, 1.10, 0));
        return l;
    }

    public static List<BpcSembolCizgi> AplikSembol()
    {
        // Aplik: Daire + sol duvarı temsil eden dikey çizgi
        var l = Cember(0, 0, 0.50, 16).ToList();
        l.Add(new(-0.80, -0.60, -0.80, 0.60));
        double d = 0.50 * 0.707;
        l.Add(new(-d, -d, d, d));
        l.Add(new(-d, d, d, -d));
        return l;
    }

    public static List<BpcSembolCizgi> AcilYonlendirmeSembol()
    {
        // Acil Yönlendirme: Dikdörtgen içinde ok (AY)
        var l = Dikdortgen(-0.90, -0.40, 0.90, 0.40).ToList();
        l.Add(new(-0.60, 0, 0.50, 0));
        l.Add(new(0.50, 0, 0.20, 0.22));
        l.Add(new(0.50, 0, 0.20, -0.22));
        return l;
    }

    public static List<BpcSembolCizgi> AcilAydinlatmaSembol()
    {
        // Acil Aydınlatma: Daire içinde çarpı + dış kesik daire
        var l = Cember(0, 0, 0.55, 16).ToList();
        double d = 0.55 * 0.707;
        l.Add(new(-d, -d, d, d));
        l.Add(new(-d, d, d, -d));
        l.AddRange(Yay(0, 0, 0.80, 200, 340, 8));
        return l;
    }

    public static List<BpcSembolCizgi> ProjektorSembol()
    {
        // Projektör: Daire + ışık konisi üçgeni
        var l = Cember(0, 0, 0.40, 16).ToList();
        l.Add(new(0.40, 0.20, 1.00, 0.50));
        l.Add(new(0.40, -0.20, 1.00, -0.50));
        l.Add(new(1.00, 0.50, 1.00, -0.50));
        return l;
    }

    public static List<BpcSembolCizgi> YuksekTavanArmaturSembol()
    {
        // Yüksek tavan: çift daire + askı/kasa çizgisi + ışık yönü
        var l = Cember(0, 0, 0.58, 20).ToList();
        l.AddRange(Cember(0, 0, 0.34, 16));
        l.Add(new(-0.42, 0.70, 0.42, 0.70));
        l.Add(new(-0.24, 0.58, -0.42, 0.70));
        l.Add(new(0.24, 0.58, 0.42, 0.70));
        l.Add(new(-0.42, -0.72, -0.18, -1.05));
        l.Add(new(0, -0.78, 0, -1.12));
        l.Add(new(0.42, -0.72, 0.18, -1.05));
        return l;
    }

    public static List<BpcSembolCizgi> SensorluTavanArmatürüSembol()
    {
        // Sensörlü Tavan: Daire içinde çarpı + dışta algılama yayları
        var l = TekNoktaSembol();
        l.AddRange(Yay(0, 0, 0.85, 30, 150, 8));
        l.AddRange(Yay(0, 0, 0.85, 210, 330, 8));
        return l;
    }

    public static List<BpcSembolCizgi> SarkitArmaturSembol()
    {
        // Sarkıt: Büyük daire (tavan) + küçük daire (lamba) + bağlantı çizgisi
        var l = Cember(0, 0.70, 0.18, 12).ToList(); // tavan bağlantı noktası
        l.AddRange(Cember(0, 0, 0.40, 16));
        double d = 0.40 * 0.707;
        l.Add(new(-d, -d, d, d));
        l.Add(new(-d, d, d, -d));
        l.Add(new(0, 0.40, 0, 0.52)); // bağlantı kolu
        return l;
    }

    public static List<BpcSembolCizgi> GlopArmaturSembol()
    {
        // Glop: Daire içinde + (artı) işareti (banyo tipi)
        var l = Cember(0, 0, 0.60, 16).ToList();
        l.Add(new(0, -0.60, 0, 0.60));
        l.Add(new(-0.60, 0, 0.60, 0));
        return l;
    }

    public static List<BpcSembolCizgi> CiplakLambaSembol()
    {
        // Çıplak Lamba: Daire ve üst çizgi
        var l = Cember(0, 0, 0.50, 16).ToList();
        l.Add(new(0, 0.50, 0, 0.90));
        return l;
    }

    public static List<BpcSembolCizgi> BantArmaturSembol()
    {
        // Bant: İnce uzun dikdörtgen
        return Dikdortgen(-1.20, -0.18, 1.20, 0.18).ToList();
    }

    public static List<BpcSembolCizgi> AvizeSembol()
    {
        // Avize: Merkez daire + 3 kol (120 derece)
        var l = Cember(0, 0, 0.25, 12).ToList();
        for (int i = 0; i < 3; i++)
        {
            double a = Math.PI / 2 + i * 2 * Math.PI / 3;
            double x1 = 0.25 * Math.Cos(a), y1 = 0.25 * Math.Sin(a);
            double x2 = 0.75 * Math.Cos(a), y2 = 0.75 * Math.Sin(a);
            l.Add(new(x1, y1, x2, y2));
            l.AddRange(Cember(x2, y2, 0.18, 10));
        }
        return l;
    }

    public static List<BpcSembolCizgi> WallWasherSembol()
    {
        // Wall Washer: Dikdörtgen gövde + alt ışık okları
        var l = Dikdortgen(-0.70, -0.20, 0.70, 0.20).ToList();
        for (double x = -0.50; x <= 0.50; x += 0.25)
        {
            l.Add(new(x, -0.20, x, -0.55));
            l.Add(new(x, -0.55, x - 0.12, -0.40));
            l.Add(new(x, -0.55, x + 0.12, -0.40));
        }
        return l;
    }

    public static List<BpcSembolCizgi> FluoresanSembol()
    {
        // Flüoresan: Uzun dikdörtgen + orta dikey ve yatay bölme
        var l = Dikdortgen(-0.95, -0.22, 0.95, 0.22).ToList();
        l.Add(new(-0.95, 0, 0.95, 0));
        l.Add(new(0, -0.22, 0, 0.22));
        return l;
    }

    // ─── Sembol geometrileri — Anahtarlar ────────────────────────────────
    // Mimari plan görünümü (floor plan) elektrik sembol sözleşmelerine göre.

    // Anahtar ailesi (kullanıcı standardı): daire ORTADA, üstte kısa kol (anahtar
    // tipine göre değişir), altta duvar bağlantı çizgisi.

    public static List<BpcSembolCizgi> AnahtarSembol() // Normal
    {
        var l = new List<BpcSembolCizgi>();
        // EMO Standart Anahtar: Daire ve 45 derece kol
        l.AddRange(Cember(0, 0, 0.30, 16));
        l.Add(new(0.21, 0.21, 0.65, 0.65)); // 45 derece kol
        l.Add(new(-0.35, -0.30, 0.35, -0.30)); // Duvar çizgisi
        return l;
    }

    public static List<BpcSembolCizgi> KomütatörSembol()
    {
        var l = new List<BpcSembolCizgi>();
        l.AddRange(Cember(0, 0, 0.30, 16));
        // Merkez üstten V şeklinde iki kol
        l.Add(new(0.15, 0.26, 0.45, 0.65));
        l.Add(new(-0.15, 0.26, -0.45, 0.65));
        l.Add(new(-0.35, -0.30, 0.35, -0.30)); // Duvar çizgisi
        return l;
    }

    public static List<BpcSembolCizgi> DarbeAnahtarSembol()
    {
        var l = AnahtarSembol();
        // Kolun etrafında veya daire içinde küçük buton işareti
        l.AddRange(Cember(0, 0, 0.10, 8)); // İçine küçük daire
        return l;
    }

    public static List<BpcSembolCizgi> VavienSembol()
    {
        var l = new List<BpcSembolCizgi>();
        l.AddRange(Cember(0, 0, 0.30, 16));
        // Çıkan 45 derece kol ve uçlarında kanca
        l.Add(new(0.21, 0.21, 0.65, 0.65)); 
        l.Add(new(0.65, 0.65, 0.85, 0.45));
        l.Add(new(0.65, 0.65, 0.45, 0.85));
        l.Add(new(-0.35, -0.30, 0.35, -0.30)); // Duvar çizgisi
        return l;
    }

    public static List<BpcSembolCizgi> KomütatörVavienSembol()
    {
        var l = new List<BpcSembolCizgi>();
        l.AddRange(Cember(0, 0, 0.30, 16));
        // Bir vavien kolu, bir normal kol
        l.Add(new(0.15, 0.26, 0.45, 0.65)); // Normal
        l.Add(new(-0.15, 0.26, -0.45, 0.65)); // Vavien kolu
        l.Add(new(-0.45, 0.65, -0.65, 0.45));
        l.Add(new(-0.45, 0.65, -0.25, 0.85));
        l.Add(new(-0.35, -0.30, 0.35, -0.30)); 
        return l;
    }

    public static List<BpcSembolCizgi> DeviatorSembol()
    {
        var l = new List<BpcSembolCizgi>();
        l.AddRange(Cember(0, 0, 0.30, 16));
        l.Add(new(-0.45, 0.45, 0.45, -0.45));
        l.Add(new(-0.45, -0.45, 0.45, 0.45));
        l.Add(new(-0.35, -0.30, 0.35, -0.30)); 
        return l;
    }

    public static List<BpcSembolCizgi> DimmerSembol()
    {
        var l = AnahtarSembol();
        l.Add(new(0.40, 0.40, 0.25, 0.55));
        l.Add(new(0.40, 0.40, 0.55, 0.25));
        return l;
    }

    public static List<BpcSembolCizgi> IkiKutupluAnahtarSembol()
    {
        var l = new List<BpcSembolCizgi>();
        l.AddRange(Cember(0, 0, 0.30, 16));
        l.Add(new(0.15, 0.26, 0.45, 0.56));
        l.Add(new(0.0, 0.30, 0.30, 0.60));
        l.Add(new(-0.35, -0.30, 0.35, -0.30)); 
        return l;
    }

    // ─── Sembol geometrileri — Sensör / Buat ─────────────────────────────

    public static List<BpcSembolCizgi> SensörSembol()
    {
        // PIR sensör: yarım kubbe (üst) + altta düz taban çizgisi + iki ayak.
        var l = new List<BpcSembolCizgi>();
        l.AddRange(Yay(0, 0, 0.55, 0, 180, 14));    // üst yarım daire
        l.Add(new(-0.55, 0, 0.55, 0));              // taban
        l.Add(new(-0.30, 0, -0.30, -0.30));         // sol bacak
        l.Add(new( 0.30, 0,  0.30, -0.30));         // sağ bacak
        return l;
    }

    public static List<BpcSembolCizgi> BuatSembol()
    {
        // Buat: küçük dolu nokta (eşmerkezli halkalarla "dolu" görüntüsü).
        var l = new List<BpcSembolCizgi>();
        l.AddRange(Cember(0, 0, 0.12, 10));
        l.AddRange(Cember(0, 0, 0.08, 8));
        l.AddRange(Cember(0, 0, 0.04, 6));
        return l;
    }

    // ─── Sembol geometrileri — Pano ───────────────────────────────────────

    public static List<BpcSembolCizgi> KatPanosuSembol()
    {
        // EMO Kat Dağıtım Tablosu: İçi kısmen dolu dikdörtgen (yarısı çapraz taralı).
        var l = new List<BpcSembolCizgi>();
        l.AddRange(Dikdortgen(-0.80, -0.30, 0.80, 0.50));
        // Ortadan dikey böl
        l.Add(new(0, -0.30, 0, 0.50));
        // Tarama (sağ taraf)
        l.Add(new(0, -0.30, 0.80, 0.50));
        l.Add(new(0, 0.10, 0.40, 0.50));
        l.Add(new(0.40, -0.30, 0.80, 0.10));
        return l;
    }

    // ─── Sembol geometrileri — Priz ──────────────────────────────────────

    // Priz ailesi — EMO/Türk mimari konvansiyonu:
    // Duvar çizgisi altta, dome (∩) yukarı açık, toprak pimi dome tepesine kadar.

    public static List<BpcSembolCizgi> PrizSembol() // Normal/Topraklı Priz
    {
        var l = new List<BpcSembolCizgi>();
        // Duvar çizgisi
        l.Add(new(-0.55, -0.30, 0.55, -0.30));
        // Yarım daire (∩): merkez (0,-0.30), r=0.45 → tepe y=0.15
        l.AddRange(Yay(0, -0.30, 0.45, 0, 180, 14));
        // Toprak T: duvardan dome tepesine (y=0.15), T çubuğu tam tepede
        l.Add(new(0, -0.30, 0, 0.15));
        l.Add(new(-0.15, 0.15, 0.15, 0.15));
        return l;
    }

    public static List<BpcSembolCizgi> KapakliPrizSembol() // Etanj / Kapaklı Priz
    {
        var l = PrizSembol();
        // Dış kapak yayı (r=0.55, aynı merkez)
        l.AddRange(Yay(0, -0.30, 0.55, 0, 180, 16));
        return l;
    }

    public static List<BpcSembolCizgi> SivaUstuPrizSembol()
    {
        var l = PrizSembol();
        // Dışına dikdörtgen kutu (dome tepesi 0.15'in üstünde biraz boşluk)
        l.AddRange(Dikdortgen(-0.65, -0.30, 0.65, 0.30));
        return l;
    }

    public static List<BpcSembolCizgi> TrifazePrizSembol()
    {
        var l = new List<BpcSembolCizgi>();
        l.Add(new(-0.55, -0.30, 0.55, -0.30)); // Duvar
        // Yarım daire: merkez (0,-0.30), r=0.45 → tepe y=0.15
        l.AddRange(Yay(0, -0.30, 0.45, 0, 180, 14));
        // 3 faz pimi: duvardan dome tepesine (y=0.15)
        l.Add(new(-0.20, -0.30, -0.20, 0.15));
        l.Add(new(0,     -0.30, 0,     0.15));
        l.Add(new(0.20,  -0.30, 0.20,  0.15));
        return l;
    }

    public static List<BpcSembolCizgi> KombinePrizKutusuSembol()
    {
        // Dikdörtgen içinde 2 priz; her dome: merkez (cx,-0.40), r=0.35 → tepe y=-0.05
        var l = Dikdortgen(-0.95, -0.45, 0.95, 0.10).ToList();

        void IcPriz(double cx)
        {
            l.AddRange(Yay(cx, -0.40, 0.35, 0, 180, 12));
            l.Add(new(cx, -0.40, cx, -0.05));           // toprak dikey → dome tepesi
            l.Add(new(cx - 0.10, -0.05, cx + 0.10, -0.05)); // T çubuğu
        }
        IcPriz(-0.45);
        IcPriz(0.45);
        return l;
    }

    public static List<BpcSembolCizgi> TavanPriziSembol()
    {
        var l = PrizSembol(); // dome tepe y=0.15
        // Çarpı dome'un hemen üstünde (tavan bağlantısını simgeler)
        l.Add(new(-0.15, 0.25, 0.15, 0.42));
        l.Add(new(-0.15, 0.42, 0.15, 0.25));
        return l;
    }

    public static List<BpcSembolCizgi> UsbliMasaPriziSembol()
    {
        var l = PrizSembol(); // dome tepe y=0.15
        // USB göstergesi: dome'un hemen üstünde küçük dikdörtgen
        l.AddRange(Dikdortgen(-0.15, 0.20, 0.15, 0.40));
        return l;
    }

    public static List<BpcSembolCizgi> UpsPriziSembol()
    {
        var l = PrizSembol(); // dome içi: y ∈ [-0.30, 0.15]
        // UPS üçgeni dome içinde (tepe y≈0.08, taban y≈-0.10)
        l.AddRange(Ucgen(0, -0.02, 0.10));
        return l;
    }

    public static List<BpcSembolCizgi> KapakliUpsPriziSembol()
    {
        var l = UpsPriziSembol();
        l.AddRange(Yay(0, -0.30, 0.55, 0, 180, 16));
        return l;
    }

    public static List<BpcSembolCizgi> SivaUstuUpsPriziSembol()
    {
        var l = UpsPriziSembol(); // dome tepe y=0.15
        l.AddRange(Dikdortgen(-0.65, -0.30, 0.65, 0.30));
        return l;
    }

    public static List<BpcSembolCizgi> IkiliPrizSembol()
    {
        var l = new List<BpcSembolCizgi>();
        l.Add(new(-0.85, -0.30, 0.85, -0.30)); // Ortak duvar
        // Sol dome: merkez (-0.40,-0.30), r=0.35 → tepe y=0.05
        l.AddRange(Yay(-0.40, -0.30, 0.35, 0, 180, 12));
        l.Add(new(-0.40, -0.30, -0.40, 0.05));
        l.Add(new(-0.50, 0.05, -0.30, 0.05));
        // Sağ dome: merkez (0.40,-0.30), r=0.35 → tepe y=0.05
        l.AddRange(Yay(0.40, -0.30, 0.35, 0, 180, 12));
        l.Add(new(0.40, -0.30, 0.40, 0.05));
        l.Add(new(0.30, 0.05, 0.50, 0.05));
        return l;
    }

    public static List<BpcSembolCizgi> UcluPrizSembol()
    {
        var l = new List<BpcSembolCizgi>();
        l.Add(new(-1.0, -0.30, 1.0, -0.30));
        void IcPriz(double cx)
        {
            // Dome: merkez (cx,-0.30), r=0.25 → tepe y=-0.05
            l.AddRange(Yay(cx, -0.30, 0.25, 0, 180, 10));
            l.Add(new(cx, -0.30, cx, -0.05));
            l.Add(new(cx - 0.08, -0.05, cx + 0.08, -0.05));
        }
        IcPriz(-0.60);
        IcPriz(0);
        IcPriz(0.60);
        return l;
    }

    // ─── Yardımcı ─────────────────────────────────────────────────────────

    private static IEnumerable<BpcSembolCizgi> Cember(double cx, double cy, double r, int n)
    {
        for (int i = 0; i < n; i++)
        {
            double a1 = 2 * Math.PI * i / n;
            double a2 = 2 * Math.PI * (i + 1) / n;
            yield return new BpcSembolCizgi(
                cx + r * Math.Cos(a1), cy + r * Math.Sin(a1),
                cx + r * Math.Cos(a2), cy + r * Math.Sin(a2));
        }
    }


    /// Belirli açı aralığında yay (derece). 0°=doğu, CCW pozitif.
    private static IEnumerable<BpcSembolCizgi> Yay(double cx, double cy, double r,
        double basDeg, double bitDeg, int n)
    {
        double a0 = basDeg * Math.PI / 180, a1 = bitDeg * Math.PI / 180;
        for (int i = 0; i < n; i++)
        {
            double t1 = a0 + (a1 - a0) * i / n;
            double t2 = a0 + (a1 - a0) * (i + 1) / n;
            yield return new BpcSembolCizgi(
                cx + r * Math.Cos(t1), cy + r * Math.Sin(t1),
                cx + r * Math.Cos(t2), cy + r * Math.Sin(t2));
        }
    }

    // ─── Zayıf akım sembolleri (Faz 1: Data / TV / Telefon) ──────────────
    // Normalize ±1 koordinat. IEC 60617 esinli özgün geometriler.

    private static IEnumerable<BpcSembolCizgi> Kare(double cx, double cy, double h)
    {
        yield return new(cx - h, cy - h, cx + h, cy - h);
        yield return new(cx + h, cy - h, cx + h, cy + h);
        yield return new(cx + h, cy + h, cx - h, cy + h);
        yield return new(cx - h, cy + h, cx - h, cy - h);
    }

    private static IEnumerable<BpcSembolCizgi> Dikdortgen(double x0, double y0, double x1, double y1)
    {
        yield return new(x0, y0, x1, y0);
        yield return new(x1, y0, x1, y1);
        yield return new(x1, y1, x0, y1);
        yield return new(x0, y1, x0, y0);
    }

    /// Yukarı bakan eşkenar üçgen; r = tabanın yarı genişliği. Geometrik merkez (cx,cy).
    private static IEnumerable<BpcSembolCizgi> Ucgen(double cx, double cy, double r)
    {
        double h = r * 1.732; // sqrt(3)
        double yBase = cy - h * 0.333;
        double yApex = cy + h * 0.667;
        yield return new(cx - r, yBase, cx + r, yBase);
        yield return new(cx + r, yBase, cx,     yApex);
        yield return new(cx,     yApex, cx - r, yBase);
    }

    // Sembol içine yerleştirilen düz harfler (çizgi parçalarıyla). h = harf yüksekliği.
    private static IEnumerable<BpcSembolCizgi> HarfO(double cx, double cy, double h)
        => Cember(cx, cy, h * 0.45, 12);

    private static IEnumerable<BpcSembolCizgi> HarfH(double cx, double cy, double h)
    {
        double w = h * 0.32, hh = h * 0.5;
        yield return new(cx - w, cy - hh, cx - w, cy + hh);
        yield return new(cx + w, cy - hh, cx + w, cy + hh);
        yield return new(cx - w, cy,      cx + w, cy);
    }

    private static IEnumerable<BpcSembolCizgi> HarfK(double cx, double cy, double h)
    {
        double w = h * 0.32, hh = h * 0.5;
        yield return new(cx - w, cy - hh, cx - w, cy + hh); // dikey
        yield return new(cx - w, cy,      cx + w, cy + hh); // çapraz yukarı
        yield return new(cx - w, cy,      cx + w, cy - hh); // çapraz aşağı
    }

    /// Data prizi: EMO standartlarında duvara yaslı üçgen ve dikey çizgi.
    public static List<BpcSembolCizgi> DataPrizSembol()
    {
        var l = new List<BpcSembolCizgi>();
        l.Add(new(-0.55, -0.30, 0.55, -0.30)); // Duvar
        l.Add(new(-0.35, -0.30, 0.35, -0.30)); // Üçgen taban
        l.Add(new(-0.35, -0.30, 0, 0.30));
        l.Add(new(0.35, -0.30, 0, 0.30));
        // İçine dik çizgi
        l.Add(new(0, -0.30, 0, 0.15));
        return l;
    }

    /// TV prizi: EMO standardında kare ve T ibaresi.
    public static List<BpcSembolCizgi> TvPrizSembol()
    {
        var l = new List<BpcSembolCizgi>();
        l.Add(new(-0.55, -0.30, 0.55, -0.30)); // Duvar
        l.AddRange(Dikdortgen(-0.35, -0.30, 0.35, 0.40));
        // İçine T şekli
        l.Add(new(0, -0.10, 0, 0.20));
        l.Add(new(-0.15, 0.20, 0.15, 0.20));
        return l;
    }

    /// Telefon prizi: EMO standart içi boş üçgen.
    public static List<BpcSembolCizgi> TelefonPrizSembol()
    {
        var l = new List<BpcSembolCizgi>();
        l.Add(new(-0.55, -0.30, 0.55, -0.30)); // Duvar
        l.Add(new(-0.35, -0.30, 0.35, -0.30));
        l.Add(new(-0.35, -0.30, 0, 0.30));
        l.Add(new(0.35, -0.30, 0, 0.30));
        return l;
    }

    /// Kat dağıtım kutusu: dikdörtgen + çapraz (ara toplayıcı).
    public static List<BpcSembolCizgi> KatDagitimKutusuSembol()
    {
        var l = Dikdortgen(-0.82, -0.62, 0.82, 0.62).ToList();
        l.Add(new(-0.55, 0.30, 0.55, 0.30));
        l.Add(new(-0.55, 0.00, 0.55, 0.00));
        l.Add(new(-0.55, -0.30, 0.55, -0.30));
        return l;
    }

    /// Data rack/kabinet: dik dikdörtgen + U birimleri (yatay çizgiler).
    public static List<BpcSembolCizgi> DataRackSembol()
    {
        var l = Dikdortgen(-0.6, -0.95, 0.6, 0.95).ToList();
        for (double y = 0.62; y > -0.75; y -= 0.34)
            l.Add(new(-0.46, y, 0.46, y));
        l.Add(new(-0.42, 0.78, 0.42, 0.78));
        return l;
    }

    /// TV merkezi (baş uç): dikdörtgen + üstte anten üçgeni.
    public static List<BpcSembolCizgi> TvMerkeziSembol()
    {
        var l = Dikdortgen(-0.82, -0.55, 0.82, 0.55).ToList();
        l.Add(new(-0.58, 0.18, 0.58, 0.18));
        l.Add(new(-0.58, -0.18, 0.58, -0.18));
        l.Add(new(0, 0.55, 0, 0.92));
        l.Add(new(0, 0.92, -0.26, 0.74));
        l.Add(new(0, 0.92, 0.26, 0.74));
        return l;
    }

    /// Telefon dağıtım (MDF): dikdörtgen + dikey bölme çizgileri.
    public static List<BpcSembolCizgi> TelefonDagitimSembol()
    {
        var l = Dikdortgen(-0.85, -0.6, 0.85, 0.6).ToList();
        l.AddRange(Yay(0, -0.02, 0.36, 205, 335, 10));
        l.Add(new(-0.56, 0.30, 0.56, 0.30));
        l.Add(new(-0.56, -0.30, 0.56, -0.30));
        return l;
    }

    /// Kamera: gövde dikdörtgeni + lens dairesi + görüş alanı üçgeni.
    public static List<BpcSembolCizgi> KameraSembol()
    {
        var l = Cember(-0.16, 0, 0.38, 14).ToList();
        l.Add(new(0.18, 0.16, 0.82, 0.42));
        l.Add(new(0.18, -0.16, 0.82, -0.42));
        l.Add(new(0.82, 0.42, 0.82, -0.42));
        l.Add(new(-0.55, 0, -0.86, 0));
        return l;
    }

    /// Hoparlör: tavan tip — dış daire (gövde) + iç "H" harfi.
    public static List<BpcSembolCizgi> HoparlorSembol()
    {
        var l = Cember(0, 0, 0.78, 20).ToList();
        l.AddRange(HarfH(0, 0, 0.62));
        return l;
    }

    /// NVR/kayıt cihazı: dikdörtgen + REC noktası + birim çizgileri.
    public static List<BpcSembolCizgi> CctvKayitSembol()
    {
        var l = Dikdortgen(-0.85, -0.5, 0.85, 0.5).ToList();
        l.Add(new(-0.65, 0.18, 0.65, 0.18));
        l.Add(new(-0.65, -0.10, 0.30, -0.10));
        l.AddRange(Cember(0.56, -0.18, 0.10, 8));
        return l;
    }

    /// Amplifikatör (anons güç amfisi): dikdörtgen gövde + iki yuvarlak VU göstergesi.
    public static List<BpcSembolCizgi> AnonsAmfiSembol()
    {
        var l = Dikdortgen(-0.92, -0.55, 0.92, 0.55).ToList();
        l.AddRange(Cember(-0.38, 0, 0.32, 14));   // sol VU
        l.AddRange(Cember( 0.38, 0, 0.32, 14));   // sağ VU
        // VU gösterge işaretleri (küçük çapraz çizgi)
        l.Add(new(-0.50, -0.10, -0.26,  0.18));
        l.Add(new( 0.26, -0.10,  0.50,  0.18));
        return l;
    }

    /// Diafon: duvar telefonu/monitör gövdesi + ahize çizgisi.
    public static List<BpcSembolCizgi> DiafonSembol()
    {
        var l = Dikdortgen(-0.55, -0.75, 0.55, 0.75).ToList();
        l.AddRange(Dikdortgen(-0.34, 0.10, 0.34, 0.50));
        l.Add(new(-0.28, -0.40, 0.20, -0.40));
        l.Add(new(-0.22, -0.54, 0.26, -0.54));
        l.AddRange(Yay(0.36, -0.46, 0.22, 115, 245, 8));
        return l;
    }

    /// Kapı zili: zil butonu + çan yayı.
    public static List<BpcSembolCizgi> KapiZiliSembol()
    {
        var l = Cember(0, -0.22, 0.24, 12).ToList();
        l.AddRange(Yay(0, 0.05, 0.52, 20, 160, 10));
        l.Add(new(-0.38, -0.10, 0.38, -0.10));
        l.Add(new(-0.16, -0.54, 0.16, -0.54));
        return l;
    }

    /// Diafon santrali: pano odası merkezi, dikdörtgen + bağlantı klemensleri.
    public static List<BpcSembolCizgi> DiafonSantraliSembol()
    {
        var l = Dikdortgen(-0.86, -0.60, 0.86, 0.60).ToList();
        l.Add(new(-0.86, 0.28, 0.86, 0.28));
        for (double x = -0.54; x <= 0.54; x += 0.36)
            l.AddRange(Cember(x, -0.12, 0.08, 8));
        l.Add(new(-0.58, 0.43, 0.58, 0.43));
        return l;
    }

    /// Ağ switch'i (PoE): dikdörtgen + alt kenarda port çizgileri.
    public static List<BpcSembolCizgi> AgAnahtariSembol()
    {
        var l = Dikdortgen(-0.85, -0.45, 0.85, 0.45).ToList();
        for (double x = -0.55; x <= 0.55; x += 0.28)
            l.AddRange(Dikdortgen(x - 0.08, -0.28, x + 0.08, -0.10));
        l.Add(new(-0.55, 0.18, 0.55, 0.18));
        return l;
    }

    // ─── Yangın algılama sembolleri (IEC 60617 / yaygın yangın projesi sözleşmesi) ──
    // Nokta tipi dedektörler yuvarlak gövde + iç işaret; buton kare; siren çan;
    // flaşör ışık patlaması; santral kontrol paneli.

    /// Optik duman dedektörü: yukarı bakan üçgen gövde + ortada "O" harfi.
    public static List<BpcSembolCizgi> DumanDedektorSembol()
    {
        var l = Ucgen(0, 0.05, 0.88).ToList();
        l.AddRange(HarfO(0, 0.10, 0.50));
        return l;
    }

    /// Isı dedektörü: yukarı bakan üçgen gövde + ortada "H" harfi.
    public static List<BpcSembolCizgi> IsiDedektorSembol()
    {
        var l = Ucgen(0, 0.05, 0.88).ToList();
        l.AddRange(HarfH(0, 0.10, 0.52));
        return l;
    }

    /// Kombine dedektör: çift halka (dış + iç daire) + ortada "K" harfi.
    public static List<BpcSembolCizgi> KombineDedektorSembol()
    {
        var l = Cember(0, 0, 0.88, 20).ToList();
        l.AddRange(Cember(0, 0, 0.70, 18));
        l.AddRange(HarfK(0, 0, 0.58));
        return l;
    }

    /// Yangın butonu (manuel ihbar): yuvarlak gövde + iç artı (+) işareti.
    public static List<BpcSembolCizgi> YanginButonuSembol()
    {
        var l = Cember(0, 0, 0.78, 18).ToList();
        l.Add(new(-0.42, 0, 0.42, 0));   // yatay
        l.Add(new(0, -0.42, 0, 0.42));   // dikey
        return l;
    }

    /// Siren: sağa açılan koni (huni) gövdesi + sağa doğru ses dalgası yayları.
    public static List<BpcSembolCizgi> SirenSembol()
    {
        var l = new List<BpcSembolCizgi>();
        // Trapezoid koni — sağa açık
        l.Add(new(-0.65, -0.38, -0.65, 0.38));   // sol kenar (kapalı uç)
        l.Add(new(-0.65, -0.38, -0.10, -0.55));  // alt kenar (genişleyen)
        l.Add(new(-0.10, -0.55, -0.10, 0.55));   // sağ kenar (açık ağız)
        l.Add(new(-0.10, 0.55,  -0.65, 0.38));   // üst kenar
        // Ses dalgaları (3 kademe yay, koninin sağında)
        l.AddRange(Yay(-0.10, 0, 0.30, -55, 55, 6));
        l.AddRange(Yay(-0.10, 0, 0.55, -55, 55, 8));
        l.AddRange(Yay(-0.10, 0, 0.82, -55, 55, 10));
        return l;
    }

    /// Flaşör (ışıklı uyarı): merkez lens dairesi + 8 yönlü ışık ışını.
    public static List<BpcSembolCizgi> FlasorSembol()
    {
        var l = Cember(0, 0, 0.30, 12).ToList();
        for (int i = 0; i < 8; i++)
        {
            double a = 2 * Math.PI * i / 8;
            l.Add(new(0.42 * Math.Cos(a), 0.42 * Math.Sin(a),
                      0.82 * Math.Cos(a), 0.82 * Math.Sin(a)));
        }
        return l;
    }

    /// Asma tavan dedektör grubu: AT dedektörü (üst) + kesik tavan çizgisi +
    /// normal dedektör (alt-sol) + ikaz lambası elması (alt-sağ) + bağlantı hatları.
    public static List<BpcSembolCizgi> AsmaTavanDedektorGrubuSembol()
    {
        var l = new List<BpcSembolCizgi>();

        // ── Asma tavan içi dedektör (üst) ─────────────────────────────
        l.AddRange(Cember(0, 0.60, 0.30, 12));
        l.Add(new(-0.18, 0.60, 0.18, 0.60));   // iç yatay

        // ── Asma tavan sınır çizgisi (kesik) ──────────────────────────
        for (double x = -0.92; x < 0.92; x += 0.26)
            l.Add(new(x, 0.18, Math.Min(x + 0.16, 0.92), 0.18));

        // ── Normal dedektör (alt-sol) ──────────────────────────────────
        l.AddRange(Cember(-0.38, -0.42, 0.30, 12));
        l.Add(new(-0.56, -0.42, -0.20, -0.42)); // iç yatay

        // ── İkaz lambası — elmas (alt-sağ) ────────────────────────────
        const double lx = 0.60, ly = -0.42, lr = 0.26;
        l.Add(new(lx,      ly + lr, lx + lr, ly));
        l.Add(new(lx + lr, ly,      lx,      ly - lr));
        l.Add(new(lx,      ly - lr, lx - lr, ly));
        l.Add(new(lx - lr, ly,      lx,      ly + lr));

        // ── Bağlantı: AT ded → normal ded ─────────────────────────────
        l.Add(new(0, 0.30, -0.38, -0.12));

        // ── Bağlantı: normal ded → ikaz lambası ───────────────────────
        l.Add(new(-0.08, -0.42, 0.34, -0.42));

        return l;
    }

    /// Yangın santrali (FACP): kontrol paneli — gövde + üst başlık şeridi + gösterge/ekran.
    public static List<BpcSembolCizgi> YanginSantraliSembol()
    {
        var l = Dikdortgen(-0.9, -0.7, 0.9, 0.7).ToList();
        l.Add(new(-0.9, 0.38, 0.9, 0.38));
        l.AddRange(Dikdortgen(-0.62, -0.42, 0.62, 0.16));
        l.AddRange(Cember(-0.56, 0.54, 0.07, 6));
        l.AddRange(Cember(-0.32, 0.54, 0.07, 6));
        l.AddRange(Cember(-0.08, 0.54, 0.07, 6));
        return l;
    }

    /// Zayıf akım sembolleri için kullanıcı override haritası (BlokAdi → çizgiler).
    /// Kütüphane yüklenince doldurulur; `ZayifAkimTanim` çağrısında varsayılan sembol
    /// yerine bu haritadan override edilir.
    public static Dictionary<string, List<BpcSembolCizgi>>? ZayifAkimOverrides { get; set; }

    /// Zayıf akım sabit malzemeleri için kütüphaneye yazılmayan, anlık tanım üretir.
    /// Eğer `ZayifAkimOverrides` haritasında BlokAdi varsa SembolCizgiler override edilir.
    public static BpcArmatürTanim? ZayifAkimTanim(BpcElemanTipi tip)
    {
        var t = ZayifAkimTanimVarsayilan(tip);
        if (t != null && ZayifAkimOverrides != null &&
            ZayifAkimOverrides.TryGetValue(t.BlokAdi, out var ovrSembol) &&
            ovrSembol.Count > 0)
        {
            t.SembolCizgiler = new List<BpcSembolCizgi>(ovrSembol);
        }
        return t;
    }

    private static BpcArmatürTanim? ZayifAkimTanimVarsayilan(BpcElemanTipi tip) => tip switch
    {
        BpcElemanTipi.DataPriz       => new() { Ad = "Data Prizi",      BlokAdi = "E4P_DATA_PRIZ",   SembolAdi = "D",    MontajTipi = "Duvar", SembolCizgiler = DataPrizSembol() },
        BpcElemanTipi.TvPriz         => new() { Ad = "TV Prizi",        BlokAdi = "E4P_TV_PRIZ",     SembolAdi = "TV",   MontajTipi = "Duvar", SembolCizgiler = TvPrizSembol() },
        BpcElemanTipi.TelefonPriz    => new() { Ad = "Telefon Prizi",   BlokAdi = "E4P_TEL_PRIZ",    SembolAdi = "T",    MontajTipi = "Duvar", SembolCizgiler = TelefonPrizSembol() },
        BpcElemanTipi.KatDagitimKutusu => new() { Ad = "Kat Dağıtım Kutusu", BlokAdi = "E4P_KDK",   SembolAdi = "KDK",  MontajTipi = "Duvar", SembolCizgiler = KatDagitimKutusuSembol() },
        BpcElemanTipi.DataRack       => new() { Ad = "Data Rack",       BlokAdi = "E4P_DATA_RACK",   SembolAdi = "RACK", MontajTipi = "Duvar", SembolCizgiler = DataRackSembol() },
        BpcElemanTipi.TvMerkezi      => new() { Ad = "TV Merkezi",      BlokAdi = "E4P_TV_MERKEZ",   SembolAdi = "TVM",  MontajTipi = "Duvar", SembolCizgiler = TvMerkeziSembol() },
        BpcElemanTipi.TelefonDagitim => new() { Ad = "Telefon Dağıtım", BlokAdi = "E4P_TEL_DAG",     SembolAdi = "MDF",  MontajTipi = "Duvar", SembolCizgiler = TelefonDagitimSembol() },
        BpcElemanTipi.Kamera         => new() { Ad = "Kamera",          BlokAdi = "E4P_KAMERA",      SembolAdi = "CAM",  MontajTipi = "Tavan", SembolCizgiler = KameraSembol() },
        BpcElemanTipi.CctvKayit      => new() { Ad = "Kayıt Cihazı (NVR)", BlokAdi = "E4P_NVR",      SembolAdi = "NVR",  MontajTipi = "Duvar", SembolCizgiler = CctvKayitSembol() },
        BpcElemanTipi.AgAnahtari     => new() { Ad = "Ağ Switch'i",     BlokAdi = "E4P_SWITCH",      SembolAdi = "SW",   MontajTipi = "Duvar", SembolCizgiler = AgAnahtariSembol() },
        BpcElemanTipi.Hoparlor       => new() { Ad = "Hoparlör",        BlokAdi = "E4P_HOPARLOR",    SembolAdi = "HP",   MontajTipi = "Tavan", SembolCizgiler = HoparlorSembol() },
        BpcElemanTipi.AnonsAmfi      => new() { Ad = "Anons Amfisi",    BlokAdi = "E4P_ANONS_AMFI",  SembolAdi = "AMF",  MontajTipi = "Duvar", SembolCizgiler = AnonsAmfiSembol() },
        BpcElemanTipi.Diafon         => new() { Ad = "Diafon",          BlokAdi = "E4P_DIAFON",      SembolAdi = "DF",   MontajTipi = "Duvar", SembolCizgiler = DiafonSembol() },
        BpcElemanTipi.KapiZili       => new() { Ad = "Kapı Zili",       BlokAdi = "E4P_KAPI_ZILI",   SembolAdi = "KZ",   MontajTipi = "Duvar", SembolCizgiler = KapiZiliSembol() },
        BpcElemanTipi.DiafonSantrali => new() { Ad = "Diafon Santrali", BlokAdi = "E4P_DIAFON_SAN",  SembolAdi = "DFS",  MontajTipi = "Duvar", SembolCizgiler = DiafonSantraliSembol() },
        BpcElemanTipi.DumanDedektor  => new() { Ad = "Duman Dedektörü", BlokAdi = "E4P_DUMAN_DED",   SembolAdi = "DD",   MontajTipi = "Tavan", SembolCizgiler = DumanDedektorSembol() },
        BpcElemanTipi.IsiDedektor    => new() { Ad = "Isı Dedektörü",   BlokAdi = "E4P_ISI_DED",     SembolAdi = "ID",   MontajTipi = "Tavan", SembolCizgiler = IsiDedektorSembol() },
        BpcElemanTipi.KombineDedektor => new() { Ad = "Kombine Dedektör", BlokAdi = "E4P_KOMBINE_DED", SembolAdi = "KD", MontajTipi = "Tavan", SembolCizgiler = KombineDedektorSembol() },
        BpcElemanTipi.AsmaTavanDedektorGrubu => new() { Ad = "AT Dedektör Grubu", BlokAdi = "E4P_ASMA_TAV_DED", SembolAdi = "AT", MontajTipi = "Tavan", SembolCizgiler = AsmaTavanDedektorGrubuSembol() },
        BpcElemanTipi.YanginButonu   => new() { Ad = "Yangın Butonu",   BlokAdi = "E4P_YANGIN_BTN",  SembolAdi = "YB",   MontajTipi = "Duvar", SembolCizgiler = YanginButonuSembol() },
        BpcElemanTipi.Siren          => new() { Ad = "Siren",           BlokAdi = "E4P_SIREN",       SembolAdi = "SR",   MontajTipi = "Duvar", SembolCizgiler = SirenSembol() },
        BpcElemanTipi.Flasor         => new() { Ad = "Flaşör",          BlokAdi = "E4P_FLASOR",      SembolAdi = "FL",   MontajTipi = "Duvar", SembolCizgiler = FlasorSembol() },
        BpcElemanTipi.YanginSantrali => new() { Ad = "Yangın Santrali", BlokAdi = "E4P_YANGIN_SAN",  SembolAdi = "YSP",  MontajTipi = "Duvar", SembolCizgiler = YanginSantraliSembol() },
        _ => null
    };

    public BpcArmatürTanim? BulManuelTanim(BpcElemanTipi tip)
    {
        // Zayıf akım sabit malzemeleri: kütüphaneye yazılmaz, anlık tanım üretilir
        if (ZayifAkimTanim(tip) is { } zt) return zt;

        // Aydınlatma tipleri için kütüphanedeki ilk eşleşeni bul
        if (tip == BpcElemanTipi.Armatür || tip == BpcElemanTipi.SarkitArmatur || tip == BpcElemanTipi.GlopArmatur ||
            tip == BpcElemanTipi.CiplakLamba || tip == BpcElemanTipi.BantArmatur || tip == BpcElemanTipi.Avize ||
            tip == BpcElemanTipi.WallWasher)
        {
            var aydinlatmaTanim = Armatürler.FirstOrDefault(a => a.ElemanTipi == tip) ?? Armatürler.FirstOrDefault();
            if (aydinlatmaTanim != null) return aydinlatmaTanim;
        }

        var blok = tip switch
        {
            BpcElemanTipi.Anahtar       => "E4P_ANAHTAR",
            BpcElemanTipi.Komütatör     => "E4P_KOMUTAT",
            BpcElemanTipi.UcluKomutator => "E4P_3LU_KOMUTAT",
            BpcElemanTipi.DarbeAnahtari => "E4P_DARBE",
            BpcElemanTipi.Vavien        => "E4P_VAVIEN",
            BpcElemanTipi.KomütatörVavien => "E4P_KOM_VAVIEN",
            BpcElemanTipi.Deviatör      => "E4P_DEVIATOR",
            BpcElemanTipi.Dimmer        => "E4P_DIMMER",
            BpcElemanTipi.IkiKutupluAnahtar => "E4P_IKIKUTUP",
            BpcElemanTipi.KatPanosu     => "E4P_KAT_PANOSU",
            BpcElemanTipi.Buat          => "E4P_BUAT",
            BpcElemanTipi.Priz             => "E4P_PRIZ",
            BpcElemanTipi.KapakliPriz      => "E4P_KAPAKLI_PRIZ",
            BpcElemanTipi.SivaUstuPriz     => "E4P_SIVAUSTU_PRIZ",
            BpcElemanTipi.TrifazePriz      => "E4P_TRIFAZE_PRIZ",
            BpcElemanTipi.KombinePrizKutusu=> "E4P_KOMBIN_PRIZ",
            BpcElemanTipi.TavanPrizi       => "E4P_TAVAN_PRIZ",
            BpcElemanTipi.UsbliMasaPrizi   => "E4P_USB_MASA",
            BpcElemanTipi.UpsPrizi         => "E4P_UPS_PRIZ",
            BpcElemanTipi.KapakliUpsPrizi  => "E4P_KAPAKLI_UPS",
            BpcElemanTipi.SivaUstuUpsPrizi => "E4P_SIVAUSTU_UPS",
            BpcElemanTipi.IkiliPriz        => "E4P_IKILI_PRIZ",
            BpcElemanTipi.UcluPriz         => "E4P_UCLU_PRIZ",
            BpcElemanTipi.KlimaIcUnite     => "E4P_KLIMA_IC",
            BpcElemanTipi.KlimaDisUnite    => "E4P_KLIMA_DIS",
            _                           => null
        };
        if (blok == null) return null;
        var kaynak = tip switch 
        {
            _ when tip == BpcElemanTipi.KatPanosu || tip == BpcElemanTipi.Buat => Panolar,
            _ when BasitProjeCizKabloServisi.ElemanPrizMi(tip) => Prizler,
            _ when tip == BpcElemanTipi.KlimaIcUnite || tip == BpcElemanTipi.KlimaDisUnite => Klimalar,
            _ => Anahtarlar
        };
        return kaynak.Find(a => a.BlokAdi == blok) ?? kaynak.FirstOrDefault();
    }

    public static BpcElemanTipi TanimTipi(BpcArmatürTanim tanim) =>
        TipBlokAdindan(tanim.BlokAdi) ?? tanim.ElemanTipi ?? BpcElemanTipi.Armatür;

    /// <summary>
    /// Blok adından eleman tipini kurtarır. Eski projelerde enum'lar tamsayı olarak
    /// saklandığından, enum'a ortadan değer eklenince <c>Tip</c> kayabilir; ancak
    /// <c>BlokAdi</c> metin olduğundan doğru kalır. Bilinmeyen blok → null (dokunma).
    /// </summary>
    public static BpcElemanTipi? TipBlokAdindan(string? blokAdi) => blokAdi switch
    {
        "E4P_ANAHTAR"        => BpcElemanTipi.Anahtar,
        "E4P_KOMUTAT"        => BpcElemanTipi.Komütatör,
        "E4P_KOMUTATÖR"      => BpcElemanTipi.Komütatör, // eski projeler için geriye dönük uyumluluk
        "E4P_3LU_KOMUTAT"    => BpcElemanTipi.UcluKomutator,
        "E4P_DARBE"          => BpcElemanTipi.DarbeAnahtari,
        "E4P_VAVIEN"         => BpcElemanTipi.Vavien,
        "E4P_KOM_VAVIEN"     => BpcElemanTipi.KomütatörVavien,
        "E4P_DEVIATOR"       => BpcElemanTipi.Deviatör,
        "E4P_DIMMER"         => BpcElemanTipi.Dimmer,
        "E4P_IKIKUTUP"       => BpcElemanTipi.IkiKutupluAnahtar,
        "E4P_SENSOR"         => BpcElemanTipi.Sensör,
        "E4P_KAT_PANOSU"     => BpcElemanTipi.KatPanosu,
        "E4P_TALI_PANO"      => BpcElemanTipi.KatPanosu,
        "E4P_ALT_PANO"       => BpcElemanTipi.KatPanosu,
        "E4P_ANA_PANO"       => BpcElemanTipi.KatPanosu,
        "E4P_OZEL_PANO"      => BpcElemanTipi.KatPanosu,
        "E4P_BUAT"           => BpcElemanTipi.Buat,
        "E4P_YER_BUATI"      => BpcElemanTipi.Buat,
        "E4P_PRIZ"           => BpcElemanTipi.Priz,
        "E4P_KAPAKLI_PRIZ"   => BpcElemanTipi.KapakliPriz,
        "E4P_MUSTAKIL_PRIZ"  => BpcElemanTipi.Priz,
        "E4P_SIVAUSTU_PRIZ"  => BpcElemanTipi.SivaUstuPriz,
        "E4P_TRIFAZE_PRIZ"   => BpcElemanTipi.TrifazePriz,
        "E4P_KOMBIN_PRIZ"    => BpcElemanTipi.KombinePrizKutusu,
        "E4P_TAVAN_PRIZ"     => BpcElemanTipi.TavanPrizi,
        "E4P_USB_MASA"       => BpcElemanTipi.UsbliMasaPrizi,
        "E4P_UPS_PRIZ"       => BpcElemanTipi.UpsPrizi,
        "E4P_KAPAKLI_UPS"    => BpcElemanTipi.KapakliUpsPrizi,
        "E4P_SIVAUSTU_UPS"   => BpcElemanTipi.SivaUstuUpsPrizi,
        "E4P_IKILI_PRIZ"     => BpcElemanTipi.IkiliPriz,
        "E4P_UCLU_PRIZ"      => BpcElemanTipi.UcluPriz,
        "E4P_KLIMA_IC"       => BpcElemanTipi.KlimaIcUnite,
        "E4P_KLIMA_DIS"      => BpcElemanTipi.KlimaDisUnite,
        "E4P_OZEL_ARM"       => BpcElemanTipi.Armatür,
        "E4P_DATA_PRIZ"      => BpcElemanTipi.DataPriz,
        "E4P_DATA_PRIZ_2"    => BpcElemanTipi.DataPriz,
        "E4P_DATA_MERKEZ"    => BpcElemanTipi.DataRack,
        "E4P_TV_PRIZ"        => BpcElemanTipi.TvPriz,
        "E4P_FIBER_PRIZ"     => BpcElemanTipi.TvPriz,
        "E4P_TV_KDP"         => BpcElemanTipi.KatDagitimKutusu,
        "E4P_TEL_PRIZ"       => BpcElemanTipi.TelefonPriz,
        "E4P_TEL_KDP"        => BpcElemanTipi.TelefonDagitim,
        "E4P_KDK"            => BpcElemanTipi.KatDagitimKutusu,
        "E4P_DATA_RACK"      => BpcElemanTipi.DataRack,
        "E4P_TV_MERKEZ"      => BpcElemanTipi.TvMerkezi,
        "E4P_TEL_DAG"        => BpcElemanTipi.TelefonDagitim,
        "E4P_KAMERA"         => BpcElemanTipi.Kamera,
        "E4P_KAMERA_HARICI"  => BpcElemanTipi.Kamera,
        "E4P_CCTV_KDK"       => BpcElemanTipi.AgAnahtari,
        "E4P_NVR"            => BpcElemanTipi.CctvKayit,
        "E4P_SWITCH"         => BpcElemanTipi.AgAnahtari,
        "E4P_HOPARLOR"       => BpcElemanTipi.Hoparlor,
        "E4P_DUVAR_HOPARLOR" => BpcElemanTipi.Hoparlor,
        "E4P_ANONS_KDK"      => BpcElemanTipi.KatDagitimKutusu,
        "E4P_ANONS_AMFI"     => BpcElemanTipi.AnonsAmfi,
        "E4P_DIAFON"         => BpcElemanTipi.Diafon,
        "E4P_KAPI_ZILI"      => BpcElemanTipi.KapiZili,
        "E4P_DIAFON_SAN"     => BpcElemanTipi.DiafonSantrali,
        "E4P_DUMAN_DED"      => BpcElemanTipi.DumanDedektor,
        "E4P_ISI_DED"        => BpcElemanTipi.IsiDedektor,
        "E4P_KOMBINE_DED"    => BpcElemanTipi.KombineDedektor,
        "E4P_ASMA_TAV_DED"   => BpcElemanTipi.AsmaTavanDedektorGrubu,
        "E4P_ASMA_TAV_ARM"   => BpcElemanTipi.AsmaTavanDedektorGrubu,
        "E4P_YANGIN_BTN"     => BpcElemanTipi.YanginButonu,
        "E4P_SIREN"          => BpcElemanTipi.Siren,
        "E4P_FLASOR_SIREN"   => BpcElemanTipi.Siren,
        "E4P_FLASOR"         => BpcElemanTipi.Flasor,
        "E4P_YANGIN_SAN"     => BpcElemanTipi.YanginSantrali,
        _                    => null
    };

    public static List<BpcSembolCizgi> KlimaIcUniteSembol()
    {
        // Klima İç Ünite: Dikdörtgen içinde X
        var l = Dikdortgen(-0.88, -0.28, 0.88, 0.28).ToList();
        l.Add(new(-0.88, -0.28, 0.88, 0.28));
        l.Add(new(-0.88, 0.28, 0.88, -0.28));
        return l;
    }

    public static List<BpcSembolCizgi> KlimaDisUniteSembol()
    {
        // Klima Dış Ünite: Kare gövde + fan dairesi
        var l = Kare(0, 0, 0.60).ToList();
        l.AddRange(Cember(0, 0, 0.45, 16));
        l.Add(new(-0.35, -0.35, 0.35, 0.35));
        l.Add(new(-0.35, 0.35, 0.35, -0.35));
        return l;
    }
}
