using System;
using System.Collections.Generic;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Salon.Models;

/// <summary>Çizimle ilişkili, dosyada kalıcı ölçü türleri.</summary>
public enum SalonOlcuTipi
{
    Dogrusal,
    Yatay,
    Dikey,
    Aci,
    Yaricap
}

/// <summary>
/// İki noktaya veya bir çizim öğesine bağlı gerçek ölçü. Ölçü değeri saklanmaz;
/// geometri değiştiğinde <c>SalonOlculendirmeServisi</c> tarafından yeniden hesaplanır.
/// </summary>
public sealed class SalonOlculendirme
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Ad { get; set; } = "Ölçü";
    public SalonOlcuTipi Tip { get; set; } = SalonOlcuTipi.Dogrusal;
    public Point2D Baslangic { get; set; }
    public Point2D Bitis { get; set; }
    public string? OgeId { get; set; }
    public int BaslangicNoktaIndeksi { get; set; } = -1;
    public int BitisNoktaIndeksi { get; set; } = -1;
    public double OfsetM { get; set; } = 0.35;
    public string SonEk { get; set; } = "m";
    public bool Gorunur { get; set; } = true;
}

/// <summary>Parametrik geometri kısıtı türleri.</summary>
public enum SalonKisitTipi
{
    Yatay,
    Dikey,
    Uzunluk,
    Cakistir,
    Paralel,
    Dik,
    EsitUzunluk,
    Sabit
}

/// <summary>
/// Çizim öğeleri arasında kalıcı geometrik ilişki. Çözücü değişikliği tek bir undo
/// işlemi olarak uygular; çözülemeyen kısıtı modeli bozmak yerine raporlar.
/// </summary>
public sealed class SalonGeometrikKisit
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Ad { get; set; } = "Kısıt";
    public SalonKisitTipi Tip { get; set; }
    public string OgeId { get; set; } = "";
    public string? IkinciOgeId { get; set; }
    public int NoktaIndeksi { get; set; }
    public int IkinciNoktaIndeksi { get; set; } = 1;
    public double Deger { get; set; }
    public bool Etkin { get; set; } = true;
}

public enum SalonAltlikTipi
{
    Dxf,
    Pdf,
    Gorsel
}

/// <summary>DXF/PDF/görsel referans altlığı ve metreye dönüşüm ayarları.</summary>
public sealed class SalonReferansAltlik
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Ad { get; set; } = "Referans";
    public string DosyaYolu { get; set; } = "";
    public SalonAltlikTipi Tip { get; set; }
    public double Olcek { get; set; } = 1.0;
    public double DonusDerece { get; set; }
    public double OfsetX { get; set; }
    public double OfsetY { get; set; }
    public double Saydamlik { get; set; } = 0.45;
    public bool Gorunur { get; set; } = true;
    public bool Kilitli { get; set; } = true;

    /// <summary>
    /// DXF çizgilerinin dosyayla birlikte saklanan önbelleği. Kaynak taşınsa bile altlık
    /// görünmeye devam eder; yeniden bağlama önbelleği günceller.
    /// </summary>
    public List<SalonAltlikSegmenti> Segmentler { get; set; } = new();
}

public sealed class SalonAltlikSegmenti
{
    public Point2D Baslangic { get; set; }
    public Point2D Bitis { get; set; }
    public string Katman { get; set; } = "0";
}

/// <summary>Akustik bulut/reflektör paneli için kalıcı 3B parametrik tanım.</summary>
public sealed class SalonYansiticiPanel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Ad { get; set; } = "Yansıtıcı";
    public double MerkezX { get; set; }
    public double MerkezY { get; set; }
    public double MerkezZ { get; set; } = 6.0;
    public double GenislikM { get; set; } = 2.4;
    public double DerinlikM { get; set; } = 1.2;
    public double XEgimDerece { get; set; }
    public double YEgimDerece { get; set; }
    public string KatmanId { get; set; } = "";
    public string Kategori { get; set; } = "Yansıtıcı";
    public bool Gorunur { get; set; } = true;
}
