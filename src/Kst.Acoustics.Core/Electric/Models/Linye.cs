using System.Text.Json.Serialization;

namespace Kst.Acoustics.Core.Electric.Models;

/// <summary>
/// Faz tipi — monofaze veya trifaze.
/// </summary>
public enum FazTipi
{
    Monofaze,
    Trifaze
}

/// <summary>
/// Faz atama — R, S veya T.
/// </summary>
public enum FazAtamasi
{
    R,
    S,
    T
}

/// <summary>
/// Sigorta cinsi — IEC 60898.
/// </summary>
public enum SigortaCinsi
{
    B, // Resistif yükler (aydınlatma, priz)
    C, // Motor, trafo, endüktif yükler
    D  // Büyük motor, ağır endüktif
}

/// <summary>
/// Pano tipi — dağıtım veya ana.
/// </summary>
public enum PanoTipi
{
    TaliPano,            // Dağıtım tablosu (kat panoları)
    AnaPano,             // Ana dağıtım panosu (AG)
    Kompanzasyon,        // Kompanzasyon panosu
    AydinlatmaPanosu,    // Aydınlatma panosu (KAR + sigorta + astronomik zaman rölesi)
    TransferPanosu,      // ATS/MTS şalteri + jeneratör besleme transferi
    UpsPanosu,           // UPS (KGK) ile beslenen kritik dağıtım panosu
    KontrolPanosu,       // Darbe kesicileri buton sinyaliyle tetikleyen kontrol panosu
    YanginPanosu         // Yangın panosu — ana şalter ÖNCESİNDEN beslenir; ana pano yüküne eklenmez
}

/// <summary>
/// Bir üst panodan bu panoya besleme türü.
/// </summary>
public enum BaglantiTipi
{
    Kablo,    // Doğrudan kablo
    Busbar    // Busbar üzerinden çıkış kutusu sigortası ile
}

/// <summary>
/// Bir linye (devre) — pano içindeki her bir çıkışı temsil eder.
/// </summary>
public class Linye
{
    public string Id { get; set; } = System.Guid.NewGuid().ToString();
    public string LinyeNo { get; set; } = "";
    public string YukTipiAd { get; set; } = "Priz";     // Kütüphaneden seçilen yük tipi adı
    public FazTipi FazTipi { get; set; } = FazTipi.Monofaze;
    public int SortiSayisi { get; set; } = 1;
    public double BirimGucW { get; set; } = 300;         // Kütüphaneden varsayılan, kullanıcı değiştirebilir
    public double TalepFaktoru { get; set; } = 1.0;
    public string Aciklama { get; set; } = "";

    // === Hesaplanan Değerler ===
    [JsonIgnore]
    public double ToplamGucW => SortiSayisi * BirimGucW * TalepFaktoru;
    public double AkimA { get; set; }
    public int SigortaAmper { get; set; }
    public int? ManuelSigortaAmper { get; set; }
    public SigortaCinsi SigortaCinsi { get; set; } = SigortaCinsi.B;
    public string KesmeKapasitesi { get; set; } = "6kA";

    /// <summary>Kullanıcı bu linyenin koruma cihazını ≤63A olsa bile TMŞ'ye zorladı mı?
    /// &gt;63A linyeler bu bayraktan bağımsız zaten TMŞ'dir. UI'daki "Cihaz" açılır menüsünden
    /// değiştirilebilir; yalnızca ≤63A linyelerde anlamlıdır.</summary>
    public bool TmsZorla { get; set; }

    /// <summary>Kullanıcı kesme kapasitesini elle seçti mi? true ise hesap motoru bara Ik'sına göre
    /// otomatik değeri YAZMAZ; kullanıcının seçtiği <see cref="KesmeKapasitesi"/> korunur (cihaz tipiyle
    /// uyumsuz kalırsa motor kendini düzeltir).</summary>
    public bool KesmeKapasitesiManuel { get; set; }

    /// <summary>Koruma cihazı tipi gösterimi: anma akımı ≤63A → "Sigorta" (MCB), &gt;63A veya
    /// <see cref="TmsZorla"/> işaretliyse → "TMŞ". Eşik <c>KorumaCihaziServisi.SigortaUstSinirA</c> (63A).</summary>
    [JsonIgnore]
    public string KorumaCihaziTipi => (SigortaAmper > 63 || TmsZorla) ? "TMŞ" : "Sigorta";

    /// <summary>Cihaz tipi (Sigorta/TMŞ) kullanıcı tarafından değiştirilebilir mi? Yalnızca ≤63A
    /// linyelerde true; &gt;63A linyeler zorunlu TMŞ olduğundan açılır menü kilitlenir.</summary>
    [JsonIgnore]
    public bool SigortaDegistirilebilir => SigortaAmper <= 63;

    /// <summary>Bu linyenin cihaz tipine uygun standart kesme kapasitesi seçenekleri (UI açılır menüsü).
    /// Sigorta (MCB): 6/10 kA, TMŞ (kompakt şalter): 36/50/100 kA.</summary>
    [JsonIgnore]
    public string[] KesmeKapasiteSecenekleri => (SigortaAmper > 63 || TmsZorla)
        ? new[] { "36kA", "50kA", "100kA" }
        : new[] { "6kA", "10kA" };

    public FazAtamasi FazAtamasi { get; set; } = FazAtamasi.R;

    /// <summary>Tabloda gösterilecek faz etiketi: trifaze linye üç fazı da kullanır → "RST";
    /// monofaze linye atandığı tek faza göre "R"/"S"/"T".</summary>
    [JsonIgnore]
    public string FazAtamasiGoster => FazTipi == FazTipi.Trifaze ? "RST" : FazAtamasi.ToString();

    // Linye bazında opsiyonel KAÇ (null ise pano KAÇ'ı geçerli)
    public KacakAkimRolesiTanim? BireyselKar { get; set; }

    /// <summary>
    /// Bu linyeye atanan kontaktör. Aydınlatma panosundaki aydınlatma linyelerinde otomatik dolar;
    /// ayrıca <see cref="KontaktorluMu"/> işaretli her linyede (her pano tipinde) akıma göre seçilir.
    /// </summary>
    public KontaktorTanim? Kontaktor { get; set; }

    /// <summary>
    /// Kullanıcı bu linyeye kontaktör eklenmesini istedi mi? true ise hesapta kontaktör, linye
    /// akımına göre otomatik seçilir. (Aydınlatma panosundaki aydınlatma linyeleri bu bayraktan
    /// bağımsız olarak zaten otomatik kontaktör alır. Ayrıca darbe kesicili ve akımı 16A üstü
    /// linyelere kontaktör otomatik eklenir.)
    /// </summary>
    public bool KontaktorluMu { get; set; }

    /// <summary>Bu linyeye atanan darbe kesici (darbe akım anahtarı). <see cref="DarbeKesiciMi"/>
    /// işaretliyse hesapta akıma göre otomatik seçilir.</summary>
    public DarbeKesiciTanim? DarbeKesici { get; set; }

    /// <summary>Kullanıcı bu linyeye darbe kesici eklenmesini istedi mi? true ise hesapta darbe
    /// kesici akıma göre seçilir; akım 16A'i aşıyorsa ayrıca kontaktör de otomatik eklenir.</summary>
    public bool DarbeKesiciMi { get; set; }

    /// <summary>
    /// Linye için hesaplanan kablo kesiti (mm²).
    /// </summary>
    public string KabloKesiti { get; set; } = "";

    /// <summary>Linye güç kablosunun paralel kol sayısı.</summary>
    public int KabloParalelKolSayisi { get; set; } = 1;

    /// <summary>Linye kablosu gerilim düşümü yüzdesi (%) — proje uzunluğu opsiyoneldir.</summary>
    public double KabloGerilimDususYuzde { get; set; }

    /// <summary>Linye kablosu ısı kaybı (W).</summary>
    public double KabloIsiKaybiW { get; set; }

    /// <summary>Linye kablosu uzunluğu (m) — kullanıcı girişi, varsayılan 10m.</summary>
    public double KabloMesafeM { get; set; } = 10;

    /// <summary>Linye kablosu kesiti termal + (segment) gerilim düşümü kriterlerini karşılıyor mu?
    /// false ise en büyük standart kesit × azami paralel kol bile yetmiyor demektir (uyarı verilir).</summary>
    public bool KabloKesitiYeterli { get; set; } = true;

    /// <summary>Kaynaktan bu linyenin ucundaki yüke kadar birikimli gerilim düşümü (%) —
    /// panonun kümülatif ΔU'su + bu linye segmentinin ΔU'su.</summary>
    public double KumulatifGerilimDususYuzde { get; set; }

    /// <summary>Bu linyenin kümülatif gerilim düşümü, izin verilen toplam sınırı (%5) aşıyor mu?</summary>
    public bool GerilimDususuAsiyor { get; set; }

    /// <summary>Tabloda kümülatif ΔU gösterimi: değer + sınırı aşıyorsa "⚠" işareti.</summary>
    [JsonIgnore]
    public string KumulatifGerilimDususGoster =>
        KumulatifGerilimDususYuzde.ToString("N2") + (GerilimDususuAsiyor ? " ⚠" : "");
}
