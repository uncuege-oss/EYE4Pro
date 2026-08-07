using System;
using System.IO;
using Kst.Acoustics.Core.Architecture.Models;
using Kst.Acoustics.Core.Bina.Models;
using Kst.Acoustics.Core.Bina.Services;

namespace Kst.Acoustics.Core.Architecture.Exporting;

/// <summary>
/// Dışa aktarım sonucunun durumu.
/// </summary>
public enum BinaDisaAktarimDurumu
{
    /// Yeni `.e4bina` üretildi.
    Uretildi,
    /// Dosya zaten vardı ve onay olmadığı için korunup yalnız bağlandı (M50'de düzenlenmiş olabilir).
    BaglandiUzerineYazilmadi,
    /// Kullanıcı onayıyla mevcut dosyanın üzerine yazıldı.
    UzerineYazildi
}

/// <summary>
/// Mimari dışa aktarım sonucu: durum, dosya yolu, revizyon ve okunabilir mesaj.
/// </summary>
public sealed class BinaGeometriDisaAktarimSonucu
{
    public BinaDisaAktarimDurumu Durum { get; init; }
    public string DosyaYolu { get; init; } = "";
    public int GeometriRevizyon { get; init; }
    public string Mesaj { get; init; } = "";

    public bool Yazildi => Durum is BinaDisaAktarimDurumu.Uretildi or BinaDisaAktarimDurumu.UzerineYazildi;
}

/// <summary>
/// Mimari projeyi M50 `.e4bina.json` dosyasına dışa aktarır ve `MimariProje.BinaGeometriYolu`'na bağlar.
/// M50'de elle düzenlenmiş bir dosyanın üzerine yazmamak için varsayılan koruma uygular
/// (codex50 deseni: dosya varsa onay olmadan üzerine yazma, yalnız bağla).
/// </summary>
public static class BinaGeometriDisaAktarimServisi
{
    public static BinaGeometriDisaAktarimSonucu DisaAktar(
        MimariProje proje,
        string dosyaYolu,
        bool uzerineYaz = false)
    {
        ArgumentNullException.ThrowIfNull(proje);
        if (string.IsNullOrWhiteSpace(dosyaYolu))
            throw new ArgumentException("Bina geometrisi dosya yolu boş olamaz.", nameof(dosyaYolu));

        var varOlan = File.Exists(dosyaYolu);
        if (varOlan && !uzerineYaz)
        {
            // Üzerine yazma; mevcut (M50'de düzenlenmiş olabilecek) dosyayı koru, yalnız bağla.
            proje.BinaGeometriYolu = dosyaYolu;
            return new BinaGeometriDisaAktarimSonucu
            {
                Durum = BinaDisaAktarimDurumu.BaglandiUzerineYazilmadi,
                DosyaYolu = dosyaYolu,
                Mesaj = "Mevcut .e4bina dosyası korunarak projeye bağlandı; üzerine yazmak için onay gerekir."
            };
        }

        var geometri = BinaGeometriAdapteri.Donustur(proje);

        // Üzerine yazarken revizyon damgasının artmaya devam etmesi için mevcut revizyonu devral
        // (downstream modüller güncellemeyi artan revizyondan anlar).
        if (varOlan)
        {
            try
            {
                geometri.GeometriRevizyon = BinaGeometriServisi.Ac(dosyaYolu).GeometriRevizyon;
            }
            catch
            {
                // Bozuk/okunamayan mevcut dosya: sıfırdan revizyonla devam.
            }
        }

        BinaGeometriServisi.Kaydet(geometri, dosyaYolu); // GeometriRevizyon artar, DosyaYolu set edilir
        proje.BinaGeometriYolu = dosyaYolu;

        return new BinaGeometriDisaAktarimSonucu
        {
            Durum = varOlan ? BinaDisaAktarimDurumu.UzerineYazildi : BinaDisaAktarimDurumu.Uretildi,
            DosyaYolu = dosyaYolu,
            GeometriRevizyon = geometri.GeometriRevizyon,
            Mesaj = varOlan
                ? "Mevcut .e4bina dosyasının üzerine yazıldı."
                : "Mimari modelden yeni .e4bina dosyası üretildi."
        };
    }
}
