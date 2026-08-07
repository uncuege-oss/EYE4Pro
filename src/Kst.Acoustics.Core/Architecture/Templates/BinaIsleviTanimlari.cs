using Kst.Acoustics.Core.Architecture.Models;

namespace Kst.Acoustics.Core.Architecture.Templates;

/// <summary>
/// M31 icin varsayilan bina islevi tanim katalogu.
/// </summary>
public static class BinaIsleviTanimlari
{
    private static readonly BinaIsleviTanim[] Tanimlar =
    [
        new()
        {
            Islev = BinaIslevi.Konut,
            Ad = "Konut",
            Aciklama = "Apartman, toplu konut veya benzeri sürekli yaşam yapısı.",
            VarsayilanSirkulasyonOrani = 0.16,
            VarsayilanTeknikAlanOrani = 0.04,
            VarsayilanKatYuksekligiM = 3.0,
            KapasiteEtiketi = "Daire sayısı"
        },
        new()
        {
            Islev = BinaIslevi.Villa,
            Ad = "Villa / Müstakil Yapı",
            Aciklama = "Tek aile kullanımı için düşük yoğunluklu konut yapısı.",
            VarsayilanSirkulasyonOrani = 0.14,
            VarsayilanTeknikAlanOrani = 0.04,
            VarsayilanKatYuksekligiM = 3.0,
            KapasiteEtiketi = "Kullanıcı sayısı"
        },
        new()
        {
            Islev = BinaIslevi.Ofis,
            Ad = "Ofis",
            Aciklama = "Açık ofis, yönetici odaları, toplantı ve destek alanları içeren çalışma yapısı.",
            VarsayilanSirkulasyonOrani = 0.22,
            VarsayilanTeknikAlanOrani = 0.07,
            VarsayilanKatYuksekligiM = 3.2,
            KapasiteEtiketi = "Çalışan sayısı"
        },
        new()
        {
            Islev = BinaIslevi.Egitim,
            Ad = "Eğitim",
            Aciklama = "Derslik, laboratuvar, atölye ve ortak eğitim alanları içeren yapı.",
            VarsayilanSirkulasyonOrani = 0.24,
            VarsayilanTeknikAlanOrani = 0.06,
            VarsayilanKatYuksekligiM = 3.4,
            KapasiteEtiketi = "Öğrenci sayısı"
        },
        new()
        {
            Islev = BinaIslevi.Saglik,
            Ad = "Sağlık",
            Aciklama = "Muayene, hasta, bekleme ve personel alanları içeren sağlık yapısı.",
            VarsayilanSirkulasyonOrani = 0.28,
            VarsayilanTeknikAlanOrani = 0.10,
            VarsayilanKatYuksekligiM = 3.6,
            KapasiteEtiketi = "Hasta / kullanıcı sayısı"
        },
        new()
        {
            Islev = BinaIslevi.Konaklama,
            Ad = "Konaklama",
            Aciklama = "Otel, apart otel, pansiyon veya benzeri geçici konaklama yapısı.",
            VarsayilanSirkulasyonOrani = 0.22,
            VarsayilanTeknikAlanOrani = 0.08,
            VarsayilanKatYuksekligiM = 3.1,
            KapasiteEtiketi = "Oda sayısı"
        },
        new()
        {
            Islev = BinaIslevi.Kulturel,
            Ad = "Kültürel",
            Aciklama = "Kütüphane, sergi, müze, tiyatro veya kültür merkezi kullanımı.",
            VarsayilanSirkulasyonOrani = 0.26,
            VarsayilanTeknikAlanOrani = 0.09,
            VarsayilanKatYuksekligiM = 4.0,
            KapasiteEtiketi = "Ziyaretçi sayısı"
        },
        new()
        {
            Islev = BinaIslevi.KonferansVeToplanti,
            Ad = "Konferans / Toplantı",
            Aciklama = "Konferans salonu, fuaye, toplantı ve sahne destek alanları içeren yapı.",
            VarsayilanSirkulasyonOrani = 0.30,
            VarsayilanTeknikAlanOrani = 0.10,
            VarsayilanKatYuksekligiM = 4.5,
            KapasiteEtiketi = "Koltuk / kişi sayısı"
        },
        new()
        {
            Islev = BinaIslevi.Ticari,
            Ad = "Ticari",
            Aciklama = "Mağaza, satış alanı, ortak dolaşım ve servis alanları içeren ticari yapı.",
            VarsayilanSirkulasyonOrani = 0.24,
            VarsayilanTeknikAlanOrani = 0.08,
            VarsayilanKatYuksekligiM = 3.8,
            KapasiteEtiketi = "Ziyaretçi sayısı"
        },
        new()
        {
            Islev = BinaIslevi.RestoranVeYemeIcme,
            Ad = "Restoran / Yeme İçme",
            Aciklama = "Yeme içme salonu, mutfak, servis ve depo alanları içeren yapı.",
            VarsayilanSirkulasyonOrani = 0.20,
            VarsayilanTeknikAlanOrani = 0.08,
            VarsayilanKatYuksekligiM = 3.6,
            KapasiteEtiketi = "Oturma kapasitesi"
        },
        new()
        {
            Islev = BinaIslevi.Endustri,
            Ad = "Endüstri",
            Aciklama = "Üretim, atölye, teknik hacim ve lojistik destek alanları içeren yapı.",
            VarsayilanSirkulasyonOrani = 0.18,
            VarsayilanTeknikAlanOrani = 0.12,
            VarsayilanKatYuksekligiM = 5.0,
            KapasiteEtiketi = "Çalışan sayısı"
        },
        new()
        {
            Islev = BinaIslevi.DepoVeLojistik,
            Ad = "Depo / Lojistik",
            Aciklama = "Depolama, yükleme, sevkiyat ve destek alanları içeren yapı.",
            VarsayilanSirkulasyonOrani = 0.16,
            VarsayilanTeknikAlanOrani = 0.06,
            VarsayilanKatYuksekligiM = 6.0,
            KapasiteEtiketi = "Depo kapasitesi"
        },
        new()
        {
            Islev = BinaIslevi.KamuIdari,
            Ad = "Kamu / İdari",
            Aciklama = "Kamu hizmeti, idari ofis, bekleme ve başvuru alanları içeren yapı.",
            VarsayilanSirkulasyonOrani = 0.24,
            VarsayilanTeknikAlanOrani = 0.07,
            VarsayilanKatYuksekligiM = 3.3,
            KapasiteEtiketi = "Kullanıcı sayısı"
        },
        new()
        {
            Islev = BinaIslevi.Spor,
            Ad = "Spor",
            Aciklama = "Spor salonu, soyunma, seyirci ve servis alanları içeren yapı.",
            VarsayilanSirkulasyonOrani = 0.24,
            VarsayilanTeknikAlanOrani = 0.09,
            VarsayilanKatYuksekligiM = 5.5,
            KapasiteEtiketi = "Sporcu / seyirci sayısı"
        },
        new()
        {
            Islev = BinaIslevi.Dini,
            Ad = "Dini",
            Aciklama = "İbadet, cemaat, hazırlık ve servis alanları içeren yapı.",
            VarsayilanSirkulasyonOrani = 0.18,
            VarsayilanTeknikAlanOrani = 0.05,
            VarsayilanKatYuksekligiM = 4.5,
            KapasiteEtiketi = "Cemaat kapasitesi"
        },
        new()
        {
            Islev = BinaIslevi.KarmaKullanim,
            Ad = "Karma Kullanım",
            Aciklama = "Birden fazla bina işlevini aynı projede birleştiren yapı.",
            VarsayilanSirkulasyonOrani = 0.24,
            VarsayilanTeknikAlanOrani = 0.08,
            VarsayilanKatYuksekligiM = 3.5,
            KapasiteEtiketi = "Kullanıcı sayısı"
        },
    ];

    public static IReadOnlyList<BinaIsleviTanim> Tum => Tanimlar;

    public static BinaIsleviTanim? Bul(BinaIslevi islev)
    {
        foreach (var tanim in Tanimlar)
        {
            if (tanim.Islev == islev)
                return tanim;
        }

        return null;
    }

    public static BinaIsleviTanim Varsayilan => Bul(BinaIslevi.Konut) ?? Tanimlar[0];
}
