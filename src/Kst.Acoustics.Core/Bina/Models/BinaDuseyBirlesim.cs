using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kst.Acoustics.Core.Bina.Models;

/// Döşeme/tavan patlatma — iki oda arasındaki düşey mekan birleşimi.
/// M08'deki DuseyMekanBirlesimi modeli M50'ye taşındı; her iki modül ortak kullanır.
public class BinaDuseyBirlesim : IBinaDuseyBirlesim
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public bool AktifMi { get; set; } = true;
    public List<BinaBirlesimUyesi> Uyeler { get; set; } = new();

    /// Patlatmanın etkilediği üst kat numarası.
    public int UstKatNo { get; set; }
    /// Patlatmanın etkilediği alt kat numarası.
    public int AltKatNo { get; set; }

    /// Patlat öncesi üst katın oda listesi (derin kopya). Geri-al için.
    /// Oda verileri yalnız Id, Ad, Poligon, AlanM2 içerir.
    public List<BinaOda>? GeriAlmaUstKatOdalari { get; set; }
    /// Patlat öncesi alt katın oda listesi (derin kopya). Geri-al için.
    public List<BinaOda>? GeriAlmaAltKatOdalari { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? EkAlanlar { get; set; }

    // IBinaDuseyBirlesim
    IReadOnlyList<IBinaBirlesimUyesi> IBinaDuseyBirlesim.Uyeler => Uyeler;
}

public class BinaBirlesimUyesi : IBinaBirlesimUyesi
{
    /// Üyenin bulunduğu katın KARARLI kimliği (BinaKat.Id). Kat sıralama/silme ile
    /// KatNo değişse bile birleşim doğru katı bulur. Eski dosyalarda boş → KatNo'dan
    /// migrasyonla doldurulur.
    public string KatId { get; set; } = "";

    /// Üyenin bulunduğu katın güncel numarası (görüntü sırası + M08 köprüsü için).
    /// KatId üzerinden tazelenir; tek başına kimlik olarak GÜVENİLMEZ.
    public int KatNo { get; set; }
    public string OdaId { get; set; } = "";
    /// Bu odanın tavanı birleşim gereği açık mı (üstteki odaya açılır).
    public bool TavanAcik { get; set; } = true;
    /// Bu odanın döşemesi birleşim gereği açık mı (alttaki odaya açılır).
    public bool DosemeAcik { get; set; } = true;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? EkAlanlar { get; set; }
}
