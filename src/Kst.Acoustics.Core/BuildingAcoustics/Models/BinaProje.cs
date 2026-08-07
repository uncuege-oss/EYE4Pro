using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Kst.Acoustics.Core.BuildingAcoustics.Models;

public class BinaProje
{
    /// <summary>M08 proje belgesi için kalıcı biçim kimliği.</summary>
    public const string CurrentModuleId = "M08";
    public const int CurrentSchemaVersion = 2;

    /// <summary>
    /// Dosyanın hangi modüle ait olduğunu doğrulamak için kullanılır. Başka bir modülün
    /// JSON dosyasının M08 projesi gibi açılıp daha sonra üzerine yazılmasını engeller.
    /// </summary>
    public string ModuleId { get; set; } = CurrentModuleId;

    /// <summary>M08 belge şeması. Daha yeni şemalar eski uygulamada salt biçimde reddedilir.</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Proje kilitli mi (kaydedilir). Patlatma yapılınca otomatik true olur;
    /// patlatma geri alınana kadar manuel olarak çözülemez.
    /// </summary>
    public bool ProjeKilitli { get; set; }

    /// <summary>Aktif düşey patlatma (döşeme/tavan) var mı.</summary>
    [JsonIgnore]
    public bool PatlatilmisDosemeVar => DuseyMekanBirlesimleri.Any(b => b.AktifMi);

    public string ProjeAdi { get; set; } = "";
    public string Adres { get; set; } = "";
    public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;

    // ── Mimari Akustik Raporu kapak bilgileri (Yönetmelik EK-10 10.2/1a) ─────
    /// Müşteri / yapı sahibi adı — rapor kapağında gösterilir.
    public string MusteriAdi { get; set; } = "";
    /// Değerlendirmeden sorumlu kişi ve kuruluş — rapor kapağında imza alanıyla gösterilir.
    public string SorumluKisiKurulus { get; set; } = "";
    
    public BinaSinifi BinaSinifi { get; set; } = BinaSinifi.S1;
    public BinaIslevi BinaIslevi { get; set; } = BinaIslevi.Konut;
    public AkustikPerformansSinifi AkustikPerformansSinifi { get; set; } = AkustikPerformansSinifi.C;
    public double DisGurultuDb { get; set; } = 65.0;
    /// <summary>
    /// Varsayılan 65 dBA değerinin kullanıcı tarafından ölçüm/hesap girdisi olarak
    /// doğrulandığını belirtir. Eski projelerde false kalır ve ön kontrolde varsayım uyarısı verir.
    /// </summary>
    public bool DisGurultuKullaniciTarafindanDogrulandi { get; set; }
    public CepheMaruziyet DisGurultuCepheMaruziyeti { get; set; } = CepheMaruziyet.SerbestAlan;
    /// <summary>
    /// Dış gürültü 1/3 oktav spektrumu.
    /// Bant sırası: 50, 63, 80, 100, 125, 160, 200, 250, 315, 400, 500,
    /// 630, 800, 1000, 1250, 1600, 2000, 2500, 3150 Hz.
    /// Boş bırakılırsa Lgag/Lden değerinden normalize trafik spektrumu üretilir.
    /// </summary>
    public List<double> DisGurultuThirdOctave { get; set; } = new();
    /// Dış gürültü spektrumunun izlenebilir kökeni. Eski projelerde Belirsiz kalır;
    /// bu durum ölçüm kadar güvenilir kabul edilmez.
    public SpektrumVeriKaynagi DisGurultuSpektrumKaynagi { get; set; } = SpektrumVeriKaynagi.Belirsiz;
    
    // ── M50 bina geometrisi bağlantısı (4.3) ──────────────────────────────
    /// Bağlı .e4bina dosyasının yolu (varsa). Dosya taşınmışsa gömülü kopya kullanılır.
    public string? BinaGeometriYolu { get; set; }
    /// Yüklenen geometrinin revizyon damgası — .e4bina'daki değerden küçükse
    /// "geometri güncellenmiş" uyarısı gösterilir.
    public int? BinaGeometriRevizyon { get; set; }
    /// Yüklenen geometrinin gömülü kopyası — proje .e4bina olmadan da açılabilir.
    public Kst.Acoustics.Core.Bina.Models.BinaGeometri? BinaGeometriKopya { get; set; }

    public List<Kat> Katlar { get; set; } = new();
    public List<BagimsizBolum> BagimsizBolumler { get; set; } = new();
    public List<DuseyMekanBirlesimi> DuseyMekanBirlesimleri { get; set; } = new();
    
    // Proje genelindeki tüm yapı elemanları (Kütüphane referansı)
    public List<YapiElemani> YapiElemaniKutuphanesi { get; set; } = new();
    
    // Akustik noktalar (Kaynak ve Alıcılar)
    public List<AkustikNokta> AkustikNoktalar { get; set; } = new();

    // Dış gürültü kaynakları
    public List<DisGurultuKaynagi> DisGurultuKaynaklari { get; set; } = new();
}
