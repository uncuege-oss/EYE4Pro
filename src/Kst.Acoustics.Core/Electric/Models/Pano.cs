using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Kst.Acoustics.Core.Electric.Models;

/// <summary>
/// Faz dağılımı — R/S/T fazlarının yük ve akım toplamları.
/// </summary>
public class FazDagilimi
{
    public double R_W { get; set; }
    public double S_W { get; set; }
    public double T_W { get; set; }

    public double R_A { get; set; }
    public double S_A { get; set; }
    public double T_A { get; set; }

    /// <summary>
    /// Dengesizlik yüzdesi — max-min / ortalama × 100.
    /// </summary>
    public double DengesizlikYuzde
    {
        get
        {
            var toplam = R_W + S_W + T_W;
            if (toplam == 0) return 0;
            var ort = toplam / 3.0;
            var max = System.Math.Max(R_W, System.Math.Max(S_W, T_W));
            var min = System.Math.Min(R_W, System.Math.Min(S_W, T_W));
            return (max - min) / ort * 100.0;
        }
    }
}

/// <summary>
/// Pano — dağıtım tablosu veya ana pano.
/// </summary>
public class KontrolButonuAtama
{
    public string Id { get; set; } = System.Guid.NewGuid().ToString();
    public string KaynakPanoAd { get; set; } = "";
    public string LinyeId { get; set; } = "";
    public string LinyeNo { get; set; } = "";
    public string LinyeAciklama { get; set; } = "";
    public int SigortaAmper { get; set; }

    [JsonIgnore]
    public string Gosterim => string.IsNullOrWhiteSpace(LinyeAciklama)
        ? $"{KaynakPanoAd} / {LinyeNo} / {SigortaAmper}A"
        : $"{KaynakPanoAd} / {LinyeNo} / {SigortaAmper}A / {LinyeAciklama}";
}

public class KontrolSinyalKablosu
{
    public string KaynakPanoAd { get; set; } = "";
    public int DamarSayisi { get; set; }
    public double KesitMm2 { get; set; } = 1.5;
    public double MetrajM { get; set; }
    public string KabloTipi { get; set; } = "Sinyal";

    [JsonIgnore]
    public string Ozet => $"{KaynakPanoAd}: {DamarSayisi}x{KesitMm2:0.##} mm² {KabloTipi}, {MetrajM:N1} m";
}

/// <summary>Pano gövdesinin montaj biçimi.</summary>
public enum PanoGovdeTipi
{
    DuvarTipi,
    DikiliTip
}

public class Pano
{
    public string Ad { get; set; } = "";
    public string Kat { get; set; } = "";
    public PanoTipi PanoTipi { get; set; } = PanoTipi.TaliPano;
    public List<Linye> Linyeler { get; set; } = new();

    /// <summary>Üst panoya bağlantı (null ise en üst pano). JSON'da serialize edilmez — UstPanoAd'dan yeniden bağlanır.</summary>
    [JsonIgnore]
    public Pano? UstPano { get; set; }

    /// <summary>Üst panonun adı (JSON serialize için referans).</summary>
    public string? UstPanoAd { get; set; }

    /// <summary>Bu panodan üst panoya kablo mesafesi (m).</summary>
    public double KabloMesafeM { get; set; } = 10;

    /// <summary>Kullanıcı kablo mesafesini elle ayarladı mı? false ise mesafe hâlâ varsayılan (10 m)
    /// kabul edilir → "Kontrol" modunda pano sarı işaretlenir.</summary>
    public bool KabloMesafeKullaniciAyarladi { get; set; }

    /// <summary>Eşzamanlılık (diversite) faktörü ks — bu panonun üst panoya yansıttığı
    /// talep oranı (0–1). Üst pano toplam gücünde alt pano gücü bu faktörle çarpılır.
    /// Varsayılan 1.0 (tam eşzamanlılık).</summary>
    public double EszamanlilikFaktoru { get; set; } = 1.0;

    /// <summary>Bu panonun girişinde kaçak akım rölesi kullanılsın mı? Kullanıcı pano bazında
    /// Hesap Özeti'nden kapatabilir. Varsayılan true, eski proje dosyalarıyla uyumludur.</summary>
    public bool KacakAkimRolesiAktif { get; set; } = true;

    /// <summary>Alt panolar — bu panoya bağlı tali panolar. JSON'da serialize edilmez — UstPanoAd'dan yeniden kurulur.</summary>
    [JsonIgnore]
    public List<Pano> AltPanolar { get; set; } = new();

    // === Hesaplanan Değerler ===
    public double ToplamGucW { get; set; }
    public double ToplamReaktifVAR { get; set; }
    public double ToplamGorunurVA { get; set; }
    public double GirisAkimA { get; set; }

    /// <summary>Talep (demand) akımı = <see cref="GirisAkimA"/> × eşzamanlılık faktörü (ks).
    /// Besleme tarafı boyutlandırması (giriş sigortası, üst panodaki çıkış sigortası, besleme
    /// kablosu, KAR) bu akıma göre yapılır; bağlı (connected) akım yalnız gösterim/bara içindir.</summary>
    public double TalepAkimA { get; set; }

    public int GirisSigortaA { get; set; }
    /// Proje tasarımında elle seçilmiş pano girişi ve üst pano çıkışı. Null = otomatik.
    public int? ManuelGirisSigortaA { get; set; }
    public int? ManuelUstPanoCikisSigortaA { get; set; }

    /// <summary>Trafo–ana pano arası giriş şalteri anma akımı (A). Yalnız ana pano için anlamlı;
    /// trafo varsa trafo AG çıkış akımına, yoksa ana pano talep akımına göre seçilir.</summary>
    public int GirisSalteriA { get; set; }
    public KacakAkimRolesiTanim? KacakAkimRolesi { get; set; }
    public AkimTrafosuTanim? KacakAkimAkimTrafosu { get; set; }
    public FazDagilimi FazDagilimi { get; set; } = new();
    public int ModulSayisi { get; set; }

    // === Pano gövdesi ve yerleşim alanı ===
    public PanoGovdeTipi GovdeTipi { get; set; } = PanoGovdeTipi.DuvarTipi;
    public double ModulAlaniM2 { get; set; }
    public int KlemensSayisi { get; set; }
    public double KlemensAlaniM2 { get; set; }
    public double GerekliPanoAlaniM2 { get; set; }
    public bool PanoOlcusuOtomatik { get; set; } = true;
    public double PanoGenislikM { get; set; } = 0.10;
    public double PanoYukseklikM { get; set; } = 0.20;
    public int PanoDerinlikCm { get; set; } = 90;

    [JsonIgnore]
    public double PanoOnYuzAlaniM2 => PanoGenislikM * PanoYukseklikM;

    [JsonIgnore]
    public string PanoOlcusu => $"{PanoGenislikM:0.00} x {PanoYukseklikM:0.00} m";

    // Kablo
    public string KabloKesiti { get; set; } = "";       // mm²
    public int KabloParalelKolSayisi { get; set; } = 1;
    public string KabloTipi { get; set; } = "NHXMH";

    // Sayaç (en üst panoda veya Aydınlatma Panosu sayaçlı versiyonu)
    public bool SayacEkle { get; set; }
    public string SayacTipi { get; set; } = "";          // Monofaze / Trifaze

    // === Aydınlatma Panosu özel alanları ===
    /// <summary>
    /// Aydınlatma panosu girişinde otomatik atanan astronomik zaman rölesi (kütüphaneden).
    /// </summary>
    public AstronomikZamanRolesiTanim? AstronomikZamanRolesi { get; set; }

    /// <summary>
    /// Aydınlatma panosu sayaçlı versiyon mu (true) / sayaçsız mı (false).
    /// PanoTipi == AydinlatmaPanosu olduğunda kullanılır.
    /// </summary>
    public bool AydinlatmaSayacli { get; set; }

    // === Kontrol Panosu özel alanları ===
    /// <summary>
    /// Kontrol panosundaki butonların tetiklediği darbe kesicili linyeler.
    /// </summary>
    public List<KontrolButonuAtama> KontrolButonlari { get; set; } = new();

    /// <summary>
    /// Kontrol panosundan kaynak panolara giden sinyal kabloları; kaynak pano başına tek kayıt.
    /// Aynı kaynak panoda birden fazla darbe kesici varsa damar sayısı artar.
    /// </summary>
    public List<KontrolSinyalKablosu> KontrolSinyalKablolari { get; set; } = new();

    // === Kompanzasyon Panosu özel alanları ===
    /// <summary>
    /// Kompanzasyon panosunda harmonik filtre bulunsun mu?
    /// </summary>
    public bool HarmonikFiltreliMi { get; set; }

    /// <summary>
    /// Kütüphaneden seçilen harmonik filtre tipi (HarmonikFiltreliMi true ise).
    /// </summary>
    public HarmonikFiltreTanim? SeciliHarmonikFiltre { get; set; }

    /// <summary>Kompanzasyon panosu için seçilen kompanzasyon rölesi.</summary>
    public KompanzasyonRolesiTanim? SeciliKompanzasyonRolesi { get; set; }

    /// <summary>SVC destekli ise seçilen şönt reaktör.</summary>
    public ReaktorTanim? SeciliReaktor { get; set; }

    /// <summary>SVC destekli ise seçilen tristör sürücü.</summary>
    public SvcSurucuTanim? SeciliSvcSurucu { get; set; }

    /// <summary>Kompanzasyon panosu girişinde NH bıçaklı sigorta.</summary>
    public BicakliSigortaTanim? GirisBicakliSigorta { get; set; }

    /// <summary>Kullanıcı kompanzasyon bileşenlerini elle değiştirdi mi? (true ise otomatik seçim devre dışı)</summary>
    public bool KompanzasyonElleAyarlandi { get; set; }

    // === Bir üst panoda otomatik eklenen çıkış sigortası (selektivite) ===
    /// <summary>
    /// Bu pano için bir üst panoda otomatik eklenmesi gereken çıkış sigortası (A).
    /// Selektivite varsa giriş sigortasının bir üst standart değeri, yoksa giriş sigortası ile aynı.
    /// </summary>
    public int UstPanoCikisSigortaA { get; set; }

    /// <summary>
    /// Bir üst panodan bu panoya gelen besleme: doğrudan kablo mu, yoksa busbar üzerinden mi?
    /// </summary>
    public BaglantiTipi BaglantiTipi { get; set; } = BaglantiTipi.Kablo;

    /// <summary>
    /// Bu pano bir busbar çıkış kutusu üzerinden besleniyorsa, ilgili busbar hattı referansı.
    /// </summary>
    public string? BaglandigiBusbarId { get; set; }

    /// <summary>
    /// Busbar üzerindeki çıkış kutusu sigortası (A). Sadece BaglantiTipi == Busbar ise kullanılır.
    /// </summary>
    public int CikisKutusuSigortaA { get; set; }

    // === Bu panoya gelen çıkış (üst panodaki) koruma cihazı — kullanıcı override'ları ===
    /// <summary>Bu panoyu besleyen çıkış cihazını (üst panodaki) ≤63A olsa bile TMŞ'ye zorla.
    /// &gt;63A çıkışlar zaten TMŞ'dir. UI'daki "Çıkış Sigortaları" tablosundan değiştirilebilir.</summary>
    public bool CikisTmsZorla { get; set; }

    /// <summary>Bu panoyu besleyen çıkış cihazının kesme kapasitesi (üst pano barası Ik'sına göre).
    /// Hesap motoru otomatik doldurur; <see cref="CikisKesmeKapasitesiManuel"/> ise kullanıcı seçimi korunur.</summary>
    public string CikisKesmeKapasitesi { get; set; } = "";

    /// <summary>Çıkış cihazı kesme kapasitesini kullanıcı elle seçti mi?</summary>
    public bool CikisKesmeKapasitesiManuel { get; set; }

    /// <summary>Bu panoyu besleyen çıkış cihazının anma akımı (A): busbar ise çıkış kutusu sigortası,
    /// değilse üst panodaki çıkış sigortası.</summary>
    [JsonIgnore]
    public int CikisKorumaAkimA => BaglantiTipi == BaglantiTipi.Busbar ? CikisKutusuSigortaA : UstPanoCikisSigortaA;

    /// <summary>Çıkış cihazı tipi gösterimi: ≤63A → "Sigorta", &gt;63A veya <see cref="CikisTmsZorla"/> → "TMŞ".</summary>
    [JsonIgnore]
    public string CikisKorumaCihaziTipi => (CikisKorumaAkimA > 63 || CikisTmsZorla) ? "TMŞ" : "Sigorta";

    /// <summary>Çıkış cihazı tipi değiştirilebilir mi? (≤63A ise true; &gt;63A zorunlu TMŞ.)</summary>
    [JsonIgnore]
    public bool CikisSigortaDegistirilebilir => CikisKorumaAkimA <= 63;

    /// <summary>Çıkış cihazının tipine uygun standart kesme kapasitesi seçenekleri (UI açılır menüsü).</summary>
    [JsonIgnore]
    public string[] CikisKesmeKapasiteSecenekleri => (CikisKorumaAkimA > 63 || CikisTmsZorla)
        ? new[] { "36kA", "50kA", "100kA" }
        : new[] { "6kA", "10kA" };

    /// <summary>
    /// Bu panodan çıkan busbar hatları (sadece ana pano ya da üst pano çıkışı için).
    /// JSON'da serialize edilmez — TumBusbarlar.UstPanoAd üzerinden yeniden bağlanır.
    /// </summary>
    [JsonIgnore]
    public List<BusbarHatti> CikanBusbarlar { get; set; } = new();

    // === Kablo hesap çıktıları ===
    /// <summary>Hesaplanan gerilim düşümü yüzdesi (%).</summary>
    public double KabloGerilimDususYuzde { get; set; }

    /// <summary>Kablodaki güç kaybı (W) — ısı hesabı.</summary>
    public double KabloIsiKaybiW { get; set; }

    /// <summary>Besleme kablosu kesiti termal + (segment) gerilim düşümü kriterlerini karşılıyor mu?
    /// false ise en büyük standart kesit × azami paralel kol bile yetmiyor demektir (uyarı verilir).</summary>
    public bool KabloKesitiYeterli { get; set; } = true;

    /// <summary>Kaynaktan (trafo/ana pano) bu panonun barasına kadar birikimli gerilim düşümü (%).
    /// Her segmentin kendi ΔU'su zincir boyunca toplanır (busbar segmenti dahil).</summary>
    public double KumulatifGerilimDususYuzde { get; set; }

    /// <summary>Bu panonun kümülatif gerilim düşümü, izin verilen toplam sınırı (%5) aşıyor mu?</summary>
    public bool GerilimDususuAsiyor { get; set; }

    // === Kısa devre ===
    /// <summary>Bu panonun barasındaki üç-faz kısa devre akımı Ik3 (kA) — kaynaktan
    /// kabloların empedansı eklenerek aşağı doğru azalır.</summary>
    public double KisaDevreAkimiKA { get; set; }
}
