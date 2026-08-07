using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Kst.Acoustics.Core.Bina.Models;

/// M50 bina geometrisinin kök modeli. `.e4bina.json` olarak serileştirilir.
public class BinaGeometri : IBinaGeometri
{
    /// Şema versiyon numarası — migrasyon tetikleyicisi.
    public int SemaSurum { get; set; } = 1;

    /// Her kayıtta artan revizyon sayacı. Modüller yükledikleri değeri projeye
    /// yazar; fark varsa "geometri güncellenmiş" uyarısı gösterilir.
    public int GeometriRevizyon { get; set; } = 1;

    public string BinaAdi { get; set; } = "";
    public string? DosyaYolu { get; set; }

    /// Eski modül projesinden ilk kez dışa aktarılmış geometrilerde kaynak
    /// artifact kimliği. Aynı adlı başka projenin yan dosyasına bağlanmayı önler.
    public string? KaynakModul { get; set; }
    public string? KaynakArtifactKimligi { get; set; }

    public ObservableCollection<BinaKat> Katlar { get; set; } = new();
    public List<BinaDuseyBirlesim> DuseyBirlesimleri { get; set; } = new();

    // IBinaGeometri read-only görünümleri
    IReadOnlyList<IBinaKat> IBinaGeometri.Katlar => Katlar;
    IReadOnlyList<IBinaDuseyBirlesim> IBinaGeometri.DuseyBirlesimleri => DuseyBirlesimleri;
}
