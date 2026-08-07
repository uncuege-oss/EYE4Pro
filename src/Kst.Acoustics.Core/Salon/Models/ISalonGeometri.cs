using System.Collections.Generic;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Salon.Models;

/// Tüketici modüllere (M01 ve ileride elektrik/mekanik) verilen salt-okunur salon
/// geometrisi görünümü. Modüller geometriyi değiştiremez; yalnız okuyup yüzey üretir.
/// Faz 3-5 öğeleri (sahne/seyirci/balkon) ve tavan profili de salt-okunur sunulur —
/// tüketici sözleşmesi tam kapsamlıdır.
public interface ISalonGeometri
{
    int SemaSurum { get; }
    int GeometriRevizyon { get; }
    string SalonAdi { get; }
    /// Varsayılan duvar yüksekliği (m) — 2D→3D extrusion için (düz tavan).
    double SalonYuksekligiM { get; }
    /// Daire/oval fasetleme kenar sayısı (servislerce 12–128'e kelepçelenir).
    int FasetKenarSayisi { get; }
    IReadOnlyList<ISalonKatman> Katmanlar { get; }
    IReadOnlyList<ISalonCizimOgesi> CizimOgeleri { get; }
    /// Parametrik sahne; null = tanımlı değil.
    ISahneGeometri? Sahne { get; }
    /// Orkestra çukuru; null = tanımlı değil.
    IOrkestraCukuru? OrkestraCukuru { get; }
    IReadOnlyList<ISeyirciSekciyonu> SeyirciSekciyonlari { get; }
    IReadOnlyList<IBalkonGeometri> Balkonlar { get; }
    /// Duvar açıklıkları (kapı/pencere) — plan konumuyla duvara bağlanır.
    IReadOnlyList<IDuvarAcikligi> Acikliklar { get; }
    /// Tavan kesit profili (boş = düz tavan `SalonYuksekligiM`'de).
    IReadOnlyList<ITavanKirilimNoktasi> TavanProfili { get; }
}

public interface ISahneGeometri
{
    SahneTipi Tip { get; }
    SahneKonum Konum { get; }
    double GenislikM { get; }
    double DerinlikM { get; }
    double YukseklikM { get; }
    double TabanKotuM { get; }
    double MerkezX { get; }
    double MerkezY { get; }
    List<Point2D> TabanPoligonu(int daireKenar = 48);
}

public interface ISeyirciSekciyonu
{
    string Id { get; }
    string Ad { get; }
    double GenislikM { get; }
    double DerinlikM { get; }
    int KademeSayisi { get; }
    double KademeYuksekligiM { get; }
    double TabanKotuM { get; }
    double MerkezX { get; }
    double MerkezY { get; }
    bool TersYon { get; }
}

public interface IBalkonGeometri
{
    string Id { get; }
    string Ad { get; }
    BalkonTipi Tip { get; }
    double GenislikM { get; }
    double DerinlikM { get; }
    double TabanKotuM { get; }
    double KalinlikM { get; }
    double ParapetM { get; }
    double MerkezX { get; }
    double MerkezY { get; }
}

public interface ITavanKirilimNoktasi
{
    double YM { get; }
    double KotM { get; }
}

public interface IDuvarAcikligi
{
    string Id { get; }
    string Ad { get; }
    AciklikTipi Tip { get; }
    double MerkezX { get; }
    double MerkezY { get; }
    double GenislikM { get; }
    double YukseklikM { get; }
    double AltKotM { get; }
}

public interface IOrkestraCukuru
{
    double GenislikM { get; }
    double DerinlikM { get; }
    double CukurDerinligiM { get; }
    double MerkezX { get; }
    double MerkezY { get; }
    List<Point2D> Footprint();
}

public interface ISalonKatman
{
    string Id { get; }
    string Ad { get; }
    SalonKatmanTipi Tip { get; }
    string RenkHex { get; }
    bool Gorunur { get; }
}

public interface ISalonCizimOgesi
{
    string Id { get; }
    SalonCizimTipi Tip { get; }
    IReadOnlyList<Point2D> Noktalar { get; }
    double YariCapM { get; }
    /// Yay başlangıç/bitiş açıları (derece, CCW). Yalnız Yay için anlamlı.
    double AciBasDeg { get; }
    double AciBitisDeg { get; }
    bool Kapali { get; }
    string KatmanId { get; }
}
