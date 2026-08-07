using System;

namespace Kst.Acoustics.Core.Salon.Models;

/// Açıklık türü.
public enum AciklikTipi { Kapi, Pencere }

/// Duvar açıklığı (kapı/pencere). Bir duvara Id ile değil, **plan konumuyla** bağlanır:
/// 3D üretiminde merkez noktası hangi duvar segmentinin üzerindeyse (tolerans içinde)
/// o duvardan delik açılır ve delik, açıklığın kendi kategorisiyle ("Kapı"/"Pencere")
/// yüzey olarak doldurulur — kabuk sızdırmaz kalır, M01'de ayrı malzeme atanır.
/// Konum-tabanlı bağ, trim/yeniden çizim sonrasında da çalışır (M12 yerleşim deseni).
public class DuvarAcikligi : IDuvarAcikligi
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Ad { get; set; } = "Kapı";
    public AciklikTipi Tip { get; set; } = AciklikTipi.Kapi;

    /// Açıklık merkezinin plan konumu (duvar çizgisi üzerinde, m).
    public double MerkezX { get; set; }
    public double MerkezY { get; set; }

    /// Duvar boyunca genişlik (m).
    public double GenislikM { get; set; } = 1.0;
    /// Düşey yükseklik (m).
    public double YukseklikM { get; set; } = 2.1;
    /// Alt kot — zeminden açıklık altına (m). Kapı 0, pencere için parapet yüksekliği.
    public double AltKotM { get; set; }

    public override string ToString()
        => $"{Ad} ({GenislikM:F1}×{YukseklikM:F1} m @ +{AltKotM:F1} m)";
}
