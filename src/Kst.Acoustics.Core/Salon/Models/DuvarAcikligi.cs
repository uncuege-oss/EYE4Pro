using System;

namespace Kst.Acoustics.Core.Salon.Models;

/// Açıklık türü.
public enum AciklikTipi { Kapi, Pencere }

/// Duvar açıklığı: serbest tasarımda kalıcı nesne/kenar kimliği ve kenar oranı ile bağlıdır.
/// Merkez koordinatları bağdan hesaplanır; eski dosyalarda konumsal eşleştirme kullanılır.
/// Açıklık ayrı malzeme kategorisiyle kaplanır; akustik kabuk kapalı kalır.
public class DuvarAcikligi : IDuvarAcikligi
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Ad { get; set; } = "Kapı";
    public string? DuvarId { get; set; }
    public string? KenarId { get; set; }
    public double KenarOrani { get; set; }
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
