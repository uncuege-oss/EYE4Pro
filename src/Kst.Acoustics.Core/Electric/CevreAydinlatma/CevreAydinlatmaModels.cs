using System.Collections.Generic;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Electric.CevreAydinlatma.Fotometri;

namespace Kst.Acoustics.Core.Electric.CevreAydinlatma;

/// <summary>Park/çevre DXF'inde işaretlenen yüzey kategorisi.</summary>
public enum YuzeyKategori
{
    Diger,
    YesilAlan,
    Yol,
    SporAlani,
    Bina
}

/// <summary>DXF arka plan çizgi segmenti (park altlığı görüntüsü).</summary>
public sealed class CizgiSegment
{
    public Point2D A { get; set; }
    public Point2D B { get; set; }
}

/// <summary>Park altlığındaki bir alan: yeşil alan, yol, spor alanı veya bina.</summary>
public sealed class CevreYuzey
{
    public int Id { get; set; }
    public string Ad { get; set; } = "";
    public YuzeyKategori Kategori { get; set; } = YuzeyKategori.Diger;
    public List<Point2D> Poligon { get; set; } = [];

    /// <summary>Otomatik atamada kullanılan kaynak DXF katman adı (varsa).</summary>
    public string KaynakKatman { get; set; } = "";

    public double AlanM2 { get; set; }
}

/// <summary>
/// Aydınlatma armatürü tanımı (kütüphane kaydı). Güç/akı LDT/IES fotometriden veya manuel gelir.
/// Bir parkta birden fazla armatür tipi olabilir.
/// </summary>
public sealed class CevreArmaturTanim
{
    public int Id { get; set; }
    public string Ad { get; set; } = "";
    public string Marka { get; set; } = "";
    public string Model { get; set; } = "";

    /// <summary>Sistem gücü, balast/sürücü dahil (W).</summary>
    public double GucW { get; set; }

    /// <summary>Toplam ışık akısı (lm).</summary>
    public double AkiLm { get; set; }

    public double RenkSicaklikK { get; set; }
    public string Ip { get; set; } = "";

    public FotometriKaynak FotometriKaynak { get; set; } = FotometriKaynak.Manuel;

    /// <summary>LDT/IES dosya yolu (varsa). Nokta-nokta hesabı için dağılım buradan tekrar okunabilir.</summary>
    public string? FotometriDosyaYolu { get; set; }
}

/// <summary>
/// Aydınlatma direği tanımı (kütüphane kaydı). Bir parkta birden fazla direk tipi olabilir.
/// </summary>
public sealed class CevreDirekTanim
{
    public int Id { get; set; }
    public string Ad { get; set; } = "";

    /// <summary>Montaj yüksekliği (m).</summary>
    public double BoyM { get; set; }

    public string Malzeme { get; set; } = "";
    public int KonsolKolSayisi { get; set; } = 1;
    public double KonsolUzunlukM { get; set; }
    public double KonsolAciDeg { get; set; }

    /// <summary>Bu direğe varsayılan armatür (kütüphane Id'si).</summary>
    public int? VarsayilanArmaturId { get; set; }
}

/// <summary>
/// Direk dizilim güzergâhı. Açık (yol kenarı polilinesi) ya da kapalı (yeşil alan çevresi) olabilir.
/// </summary>
public sealed class AydinlatmaGuzergahi
{
    public int Id { get; set; }
    public string Ad { get; set; } = "";

    /// <summary>Güzergâh noktaları (yol kenarı veya yeşil alan kenar referansı).</summary>
    public List<Point2D> Rota { get; set; } = [];

    /// <summary>true: kapalı poligon çevresi; false: açık güzergâh.</summary>
    public bool Kapali { get; set; }

    /// <summary>Direk aralığı (m).</summary>
    public double DirekAralikM { get; set; } = 25;

    /// <summary>Kenardan içe(+)/dışa(−) kaydırma (m); yalnızca kapalı güzergâhta.</summary>
    public double KenarOfsetM { get; set; }

    public int? DirekTanimId { get; set; }
    public int? ArmaturTanimId { get; set; }
}

/// <summary>Yerleşmiş bir aydınlatma direği (güzergâh üzerinde otomatik veya elle konumlanmış).</summary>
public sealed class AydinlatmaDiregi
{
    public int Id { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public int? DirekTanimId { get; set; }
    public int? ArmaturTanimId { get; set; }
    public int GuzergahId { get; set; }

    /// M60 Terrain köprüsü: arazi yüzeyinden okunan zemin (dip) kotu (m).
    /// null = arazi yüklenmemiş veya direk arazi dışında. Salt-okunur tüketim.
    public double? ZeminKotuM { get; set; }
}

public enum TopraklamaSistemi
{
    TT,
    TN_S,
    TN_C_S
}

/// <summary>Topraklama kazığı (düşey elektrot) noktası.</summary>
public sealed class TopraklamaKazigi
{
    public double X { get; set; }
    public double Y { get; set; }
    public double BoyM { get; set; } = 2.4;
    public double CapM { get; set; } = 0.016;
}

/// <summary>
/// Topraklama tasarımı (M15 Faz 7). Şerit (kablo hendeği boyunca tüm direk diplerini bağlayan
/// iletken) + kazık noktaları; TT sisteminde işletme + koruma topraklaması.
/// </summary>
public sealed class TopraklamaTasarimi
{
    public TopraklamaSistemi Sistem { get; set; } = TopraklamaSistemi.TT;
    public List<Point2D> SeritGuzergahi { get; set; } = [];
    public string SeritTipi { get; set; } = "Galvaniz 30×3.5 mm";
    public double SeritGenislikM { get; set; } = 0.03;
    public double GomulmeDerinlikM { get; set; } = 0.7;
    public List<TopraklamaKazigi> Kaziklar { get; set; } = [];
    public bool IsletmeTopraklamasi { get; set; } = true;
    public bool KorumaTopraklamasi { get; set; } = true;
    public double ToprakOzdirencOhmM { get; set; } = 100;
    public double HesaplananDirencOhm { get; set; }
}

/// <summary>Çevre aydınlatma projesi kök modeli (M15). Faz 2: yüzeyler; Faz 3: kütüphaneler; Faz 4: güzergâh + direkler; Faz 7: topraklama.</summary>
public sealed class CevreAydinlatmaProje
{
    public string Ad { get; set; } = "Çevre Aydınlatma Projesi";
    public string? DxfDosyaYolu { get; set; }
    public string? BirimUyarisi { get; set; }
    public List<CizgiSegment> ArkaPlanSegmentleri { get; set; } = [];
    public List<CevreYuzey> Yuzeyler { get; set; } = [];
    public List<CevreArmaturTanim> ArmaturKutuphanesi { get; set; } = [];
    public List<CevreDirekTanim> DirekKutuphanesi { get; set; } = [];
    public List<AydinlatmaGuzergahi> Guzergahlar { get; set; } = [];
    public List<AydinlatmaDiregi> Direkler { get; set; } = [];
    public TopraklamaTasarimi Topraklama { get; set; } = new();
}
