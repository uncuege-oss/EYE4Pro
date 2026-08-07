using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Kst.Acoustics.Core.Salon.Models;

/// M51 "Salon Tasarla" modülünün kök modeli. `.e4salon.json` olarak serileştirilir.
/// M50 <c>BinaGeometri</c> deseni: <see cref="SemaSurum"/> migrasyon tetikleyicisi,
/// <see cref="GeometriRevizyon"/> tüketici fark kontrolü için artan damga.
public class SalonGeometri : ISalonGeometri
{
    /// Şema versiyon numarası — migrasyon tetikleyicisi.
    public int SemaSurum { get; set; } = 1;

    /// Her kayıtta artan revizyon sayacı. Tüketici modüller (M01) yükledikleri
    /// değeri saklar; fark varsa "salon geometrisi güncellenmiş" uyarısı gösterilir.
    public int GeometriRevizyon { get; set; } = 1;

    public string SalonAdi { get; set; } = "";
    public string? DosyaYolu { get; set; }

    /// Varsayılan duvar/salon yüksekliği (m). 2D→3D extrusion'da kullanılır;
    /// öğe/bölge bazlı override ileride eklenebilir.
    public double SalonYuksekligiM { get; set; } = 8.0;

    /// Daire/oval fasetleme kenar sayısı (akustik hassasiyet ↔ yüzey sayısı dengesi).
    /// Servisler 12–128 aralığına kelepçeler; eski dosyalarda alan yoksa 48 kullanılır.
    public int FasetKenarSayisi { get; set; } = 48;

    /// Çizim/yüzey katmanları (şablonlar otomatik atar, kullanıcı ekler).
    public ObservableCollection<SalonKatman> Katmanlar { get; set; } = new();

    /// Serbest 2D eskiz öğeleri (zemin, sahne konturu, yardımcı çizgiler).
    public ObservableCollection<SalonCizimOgesi> CizimOgeleri { get; set; } = new();

    /// Parametrik sahne (opsiyonel). null = sahne tanımlı değil.
    public SahneGeometri? Sahne { get; set; }

    /// Orkestra çukuru (opsiyonel). null = tanımlı değil.
    public OrkestraCukuru? OrkestraCukuru { get; set; }

    /// Kademeli seyirci alanları (0+).
    public ObservableCollection<SeyirciSekciyonu> SeyirciSekciyonlari { get; set; } = new();

    /// Balkon/loca platformları (0+).
    public ObservableCollection<BalkonGeometri> Balkonlar { get; set; } = new();

    /// Duvar açıklıkları — kapı/pencere (0+). Plan konumuyla duvara bağlanır.
    public ObservableCollection<DuvarAcikligi> Acikliklar { get; set; } = new();

    /// Tavan kesit profili: derinlik (Y) → kot kontrol noktaları. Boş/1 nokta = düz
    /// tavan (`SalonYuksekligiM`). ≥2 nokta = parçalı/eğimli tavan (X yönünde ekstrüde).
    public ObservableCollection<TavanKirilimNoktasi> TavanProfili { get; set; } = new();

    // ISalonGeometri salt-okunur görünümleri
    IReadOnlyList<ISalonKatman> ISalonGeometri.Katmanlar => Katmanlar;
    IReadOnlyList<ISalonCizimOgesi> ISalonGeometri.CizimOgeleri => CizimOgeleri;
    ISahneGeometri? ISalonGeometri.Sahne => Sahne;
    IOrkestraCukuru? ISalonGeometri.OrkestraCukuru => OrkestraCukuru;
    IReadOnlyList<ISeyirciSekciyonu> ISalonGeometri.SeyirciSekciyonlari => SeyirciSekciyonlari;
    IReadOnlyList<IBalkonGeometri> ISalonGeometri.Balkonlar => Balkonlar;
    IReadOnlyList<IDuvarAcikligi> ISalonGeometri.Acikliklar => Acikliklar;
    IReadOnlyList<ITavanKirilimNoktasi> ISalonGeometri.TavanProfili => TavanProfili;
}
