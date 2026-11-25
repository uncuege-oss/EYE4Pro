using System.Collections.Generic;

namespace Kst.Acoustics.Core.Terrain.Models;

/// M60 arazi geometrisinin kök modeli. Hafriyat sonrası `.e4arazi.json` olarak
/// serileştirilir ve tüketici modüllere `IAraziGeometri` ile salt-okunur verilir.
/// (TIN yüzeyi, tasarım yüzeyi ve hafriyat sonucu sonraki fazlarda eklenir.)
public class AraziModeli : IAraziGeometri
{
    /// Şema versiyon numarası — migrasyon tetikleyicisi (M12/M50 deseni).
    public int SemaSurum { get; set; } = 1;

    /// Her kayıtta artan revizyon damgası. Tüketici yüklediği değeri saklar;
    /// fark varsa "arazi güncellenmiş" uyarısı gösterilir.
    public int GeometriRevizyon { get; set; } = 1;

    public string AraziAdi { get; set; } = "";
    public string? DosyaYolu { get; set; }
    /// İçe aktarılan özgün CAD ve seçimler; proje kaydı DosyaYolu'nu değiştirirken korunur.
    public AraziKaynakBilgisi? Kaynak { get; set; }

    public List<AraziNoktasi> Noktalar { get; set; } = new();
    /// Özgün CAD yüzlerinin Noktalar listesindeki köşe indisleri.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<AraziUcgen>? KaynakUcgenleri { get; set; }
    public List<AraziKonturu> Konturlar { get; set; } = new();

    /// Hafriyat tasarım bölgeleri (hedef kotlar). Türetilen TIN ve hafriyat sonucu
    /// serileştirilmez; yüklemede `AraziYuzeyServisi`/`HafriyatHesabiServisi` ile yeniden üretilir.
    public TasarimYuzeyi Tasarim { get; set; } = new();

    // IAraziGeometri salt-okunur görünümleri
    IReadOnlyList<IAraziNokta> IAraziGeometri.Noktalar => Noktalar;
    IReadOnlyList<IAraziKontur> IAraziGeometri.Konturlar => Konturlar;
}
