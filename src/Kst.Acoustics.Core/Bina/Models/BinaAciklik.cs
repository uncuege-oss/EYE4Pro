using System;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Bina.Models;

public enum BinaAciklikTipi { Kapi, Pencere }

public class BinaAciklik : IBinaAciklik
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// Duvar segmenti üzerindeki başlangıç noktası (kat koordinat çerçevesi, m).
    public Point2D Bas { get; set; }
    /// Duvar segmenti üzerindeki bitiş noktası (kat koordinat çerçevesi, m).
    public Point2D Bit { get; set; }

    public BinaAciklikTipi Tip { get; set; } = BinaAciklikTipi.Kapi;

    /// Bas-Bit segmentinden hesaplanan yatay genişlik. Kayıtta saklanır, yükleme
    /// sırasında Bas/Bit'ten yeniden hesaplanabilir.
    public double GenislikM { get; set; }

    /// Kullanıcının M50'de girdiği açıklık yüksekliği (m). 0 = girilmemiş.
    public double YukseklikM { get; set; }

    /// Açıklık alt kenarının döşemeden yüksekliği (m). EsikYuksekligiBelirtildi
    /// false ise eski dosyalarla uyumlu olarak 0 = tip varsayılanı.
    /// Tip varsayılanları: Kapı → 0.0 m, Pencere → 0.9 m.
    public double EsikYuksekligiM { get; set; }

    /// Çizimden okunan veya kullanıcı tarafından girilen eşik. Sıfır değerli
    /// boydan camı, eşik bilgisi içermeyen eski kayıtlardan ayırır.
    public bool EsikYuksekligiBelirtildi { get; set; }

    /// Kullanıcı "açıklık değil" derse true. Yeniden taramada korunur.
    public bool IptalEdildi { get; set; }

    /// Kullanıcı elle ekledi mi? true ise yeniden tarama silmez.
    public bool ManuelEklendi { get; set; }

    /// Tespit kaynağı: "arc" (kapı yayı), "gap" (duvar boşluğu), "manuel".
    public string? Kaynak { get; set; }

    /// Etkin eşik yüksekliği — kullanıcı tanımlı varsa onu, yoksa tip varsayılanını döner.
    public double EtkinEsikYuksekligiM =>
        EsikYuksekligiBelirtildi || EsikYuksekligiM > 0 ? EsikYuksekligiM
        : (Tip == BinaAciklikTipi.Kapi ? 0.0 : 0.9);

    /// Tip varsayılan açıklık yüksekliği (m): Kapı 2.1, Pencere 1.5.
    /// Otomatik tespit ve manuel ekleme aynı değeri kullanır (M08 ile tutarlı).
    public static double VarsayilanYukseklik(BinaAciklikTipi tip)
        => tip == BinaAciklikTipi.Kapi ? 2.1 : 1.5;
}
