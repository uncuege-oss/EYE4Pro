using System.Collections.Generic;

namespace Kst.Acoustics.Core.Electric.Models;

/// <summary>
/// Jeneratör çalışma modu.
/// </summary>
public enum JeneratorMod
{
    /// <summary>Prime Power — sürekli işletme, yük değişkenli.</summary>
    Prime,
    /// <summary>Standby (Emergency) — acil yedek, sınırlı çalışma saati.</summary>
    Standby
}

/// <summary>
/// Jeneratörün besleme kapsamı — tüm tesisi mi yoksa lokal bir bölgeyi mi besler.
/// </summary>
public enum JeneratorBeslemeKapsami
{
    /// <summary>Tesis geneli — jeneratör ana panoyu besler, tüm tesisi yedekler.</summary>
    TesisGeneli,
    /// <summary>Lokal bölge — jeneratör yalnız belirli bir tali panoyu (bölgeyi) besler.</summary>
    LokalBolge
}

/// <summary>
/// Projedeki jeneratör örneği — kütüphaneden seçilen JeneratorTanim'a referans verir.
/// Ana panoya transfer panosu üzerinden bağlanır.
/// </summary>
public class Jenerator
{
    public string Id { get; set; } = System.Guid.NewGuid().ToString();

    /// <summary>Bağlı olduğu ana pano adı.</summary>
    public string AnaPanoAd { get; set; } = "";

    /// <summary>Transfer panosu adı (PanoTipi.TransferPanosu olan pano).</summary>
    public string TransferPanoAd { get; set; } = "";

    /// <summary>Kütüphaneden seçilen jeneratör tipi adı.</summary>
    public string JeneratorTanimAd { get; set; } = "";

    /// <summary>Kütüphaneden seçilen transfer şalteri adı.</summary>
    public string TransferSalterAd { get; set; } = "";

    /// <summary>Jeneratör dış mekânda kuruluyorsa true; Excel metrajına koruma kabini eklenir.</summary>
    public bool DisMekandaMi { get; set; }

    /// <summary>Kullanıcı transfer şalterini elle seçtiyse <c>true</c>; aksi halde hesap motoru
    /// ana pano akımına göre otomatik seçer ve yük değiştikçe günceller (varsayılan otomatik).</summary>
    public bool TransferSalterManuel { get; set; }

    /// <summary>Prime veya Standby modunda işletilecek.</summary>
    public JeneratorMod Mod { get; set; } = JeneratorMod.Standby;

    /// <summary>Tasarımda dikkate alınan emniyet faktörü (örn. 1.25 = %25 emniyet).</summary>
    public double EmniyetFaktoru { get; set; } = 1.25;

    // Hesaplanmış değerler
    public double HesaplananYukKW { get; set; }
    public double HesaplananYukKVA { get; set; }
    public double GerekliKVAMin { get; set; }    // emniyet faktörlü
    public bool YukYeterliMi { get; set; }       // seçilen jeneratör yetişiyor mu
    // Not: Eski ".eye4elec.json" dosyalarındaki "GereklikVAMin" (yazım hatası) alanı,
    // PropertyNameCaseInsensitive=true sayesinde otomatik olarak GerekliKVAMin'e eşlenir;
    // ayrı alias property gerekmez (case-insensitive ayarla çakışıyordu — kaldırıldı).

    /// <summary>Tahmini yakıt tüketimi (L/h) — yük yüzdesine göre interpolasyon.</summary>
    public double YakitTuketimLh { get; set; }

    /// <summary>(Tek-jeneratör/eski şema) jeneratörün bulunduğu kat etiketi. Grup kullanılıyorsa
    /// <see cref="JeneratorGrubu.Kat"/> geçerlidir.</summary>
    public string Kat { get; set; } = "";

    /// <summary>Bu jeneratör ünitesinden senkron panoya giden güç kablosunun mesafesi (m).</summary>
    public double SenkronPanoKabloMesafeM { get; set; } = 10;

    /// <summary>Eski tek-jeneratör şeması için senkron/transfer panosundan hedef panoya kablo
    /// mesafesi (m). Grup modelinde <see cref="JeneratorGrubu.SenkronHedefPanoKabloMesafeM"/>
    /// kullanılır.</summary>
    public double SenkronHedefPanoKabloMesafeM { get; set; } = 10;

    /// <summary>Hesaplanan anma akımı (A) — kVA × 1000 / (√3 × V).</summary>
    public double AnmaAkimA { get; set; }

    /// <summary>Bu ünite → senkron pano güç kablosunun hesaplanan kesiti (örn. "50 mm²").</summary>
    public string SenkronKabloKesiti { get; set; } = "";

    /// <summary>Bu ünite → senkron pano güç kablosunun paralel kol sayısı.</summary>
    public int SenkronKabloParalelKolSayisi { get; set; } = 1;

    /// <summary>(Eski tek-jeneratör) senkron → hedef pano kablosunun hesaplanan kesiti.</summary>
    public string SenkronHedefKabloKesiti { get; set; } = "";

    /// <summary>(Eski tek-jeneratör) senkron → hedef pano kablosunun paralel kol sayısı.</summary>
    public int SenkronHedefKabloParalelKolSayisi { get; set; } = 1;
}

/// <summary>
/// Birden fazla jeneratörün senkron panosu üzerinden ana panoya bağlandığı grup.
/// Üniteler farklı güçlerde olabilir; mod ve emniyet faktörü grup seviyesinde ortak uygulanır.
/// </summary>
public class JeneratorGrubu
{
    public string Id { get; set; } = System.Guid.NewGuid().ToString();

    /// <summary>Grubun beslediği (hedef) pano adı. Tesis genelinde ana pano,
    /// lokal bölgede beslenen tali panodur. (Alan adı geriye dönük uyumluluk için korunur.)</summary>
    public string AnaPanoAd { get; set; } = "";

    /// <summary>Besleme kapsamı — tesis geneli (ana pano) veya lokal bölge (tali pano).</summary>
    public JeneratorBeslemeKapsami BeslemeKapsami { get; set; } = JeneratorBeslemeKapsami.TesisGeneli;

    /// <summary>Senkron/transfer panosu adı (PanoTipi.TransferPanosu).</summary>
    public string SenkronPanoAd { get; set; } = "";

    /// <summary>Senkron pano çıkışındaki transfer/senkron şalteri.</summary>
    public string TransferSalterAd { get; set; } = "";

    /// <summary>Kullanıcı transfer şalterini elle seçtiyse <c>true</c>; aksi halde hesap motoru
    /// ana pano akımına göre otomatik seçer ve yük değiştikçe günceller (varsayılan otomatik).</summary>
    public bool TransferSalterManuel { get; set; }

    /// <summary>Prime veya Standby modunda işletilecek.</summary>
    public JeneratorMod Mod { get; set; } = JeneratorMod.Standby;

    /// <summary>Tasarımda dikkate alınan emniyet faktörü.</summary>
    public double EmniyetFaktoru { get; set; } = 1.25;

    /// <summary>Jeneratör grubunun bulunduğu kat etiketi (örn. "Bodrum 2", "Çatı"). Boş ise
    /// hedef (ana/tali) panonun katına çizilir. Şemada bu kat bandına yerleştirilir.</summary>
    public string Kat { get; set; } = "";

    /// <summary>Senkron panodan grubun beslediği hedef/transfer/ara panoya giden ortak güç
    /// kablosunun mesafesi (m).</summary>
    public double SenkronHedefPanoKabloMesafeM { get; set; } = 10;

    /// <summary>Senkron panoya bağlı jeneratör üniteleri.</summary>
    public List<Jenerator> Jeneratorler { get; set; } = new();

    // Hesaplanmış grup değerleri
    public double HesaplananYukKW { get; set; }
    public double HesaplananYukKVA { get; set; }
    public double GerekliKVAMin { get; set; }
    public double ToplamGucKW { get; set; }
    public double ToplamGucKVA { get; set; }
    public bool YukYeterliMi { get; set; }
    public double YakitTuketimLh { get; set; }

    /// <summary>Grup toplam anma akımı (A) — ünitelerin anma akımları toplamı.</summary>
    public double ToplamAnmaAkimA { get; set; }

    /// <summary>Senkron pano → hedef (ana/tali) pano ortak güç kablosunun hesaplanan kesiti
    /// (örn. "95 mm²"). Grup toplam akımına göre boyutlandırılır.</summary>
    public string SenkronHedefKabloKesiti { get; set; } = "";

    /// <summary>Senkron pano → hedef pano ortak güç kablosunun paralel kol sayısı.</summary>
    public int SenkronHedefKabloParalelKolSayisi { get; set; } = 1;
}
