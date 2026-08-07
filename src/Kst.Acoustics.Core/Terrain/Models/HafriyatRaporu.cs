namespace Kst.Acoustics.Core.Terrain.Models;

/// Kabarma (kazıda gevşeme) ve sıkışma (dolguda sıkıştırma) katsayıları (yüzde).
/// Örn. Kabarma=25 → kazılan zemin %25 kabarır (nakil/depo hacmi).
///      Sıkışma=10 → sıkıştırılmış 1 m³ dolgu için %10 fazla yerinde (bank) malzeme gerekir.
public class HafriyatKatsayilari
{
    public double KabarmaYuzdesi { get; set; }
    public double SikismaYuzdesi { get; set; }

    public static HafriyatKatsayilari Yok => new();
}

/// Hafriyat özet raporu — ham hacimler + katsayılı hacimler + bank denge.
public class HafriyatRaporu
{
    /// Yerinde (bank) kazı hacmi — hesaptan gelen ham yarma.
    public double YarmaBankHacimM3 { get; set; }

    /// Sıkıştırılmış yerinde dolgu hacmi — hesaptan gelen ham dolgu.
    public double DolguSikismisHacimM3 { get; set; }

    /// Kazılan malzemenin kabarmış (gevşek) hacmi — kamyon/depo için.
    public double KazilanGevsekHacimM3 { get; set; }

    /// Sıkıştırılmış dolguyu üretmek için gereken yerinde (bank) malzeme hacmi.
    public double DolguIcinGerekliBankHacimM3 { get; set; }

    /// Bank denge = yarma(bank) − dolgu için gereken bank.
    /// Pozitif: fazla malzeme (depoya nakil). Negatif: dış ödünç (borrow) ihtiyacı.
    public double DengeBankHacimM3 => YarmaBankHacimM3 - DolguIcinGerekliBankHacimM3;

    public bool FazlaMalzemeVar => DengeBankHacimM3 > 0;

    public string Ozet { get; set; } = "";
}
