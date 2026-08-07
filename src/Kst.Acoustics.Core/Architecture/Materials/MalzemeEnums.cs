namespace Kst.Acoustics.Core.Architecture.Materials;

/// <summary>
/// Mimari malzeme katalogunda kullanilan temel kategori.
/// </summary>
public enum MalzemeKategorisi
{
    Tanimsiz = 0,
    Genel = 1,
    ZeminKaplamasi = 2,
    DuvarKaplamasi = 3,
    TavanKaplamasi = 4,
    Kaplama = 5,
    Yalitim = 6,
    Boya = 7,
    Siva = 8,
    Seramik = 9,
    Cam = 10,
    Metal = 11,
    Ahsap = 12,
    Beton = 13,
    Alci = 14,
    Membran = 15,
    Kapi = 16,
    Pencere = 17,
    Cekirdek = 18
}

/// <summary>
/// M36 ve sonraki fazlarda malzemenin baglanabilecegi yapi elemani sinifi.
/// </summary>
public enum YapiElemaniSinifi
{
    Tanimsiz = 0,
    Duvar = 1,
    Doseme = 2,
    Tavan = 3,
    Kapi = 4,
    Pencere = 5,
    Cati = 6,
    Cekirdek = 7,
    Saft = 8,
    MahalFinisaji = 9
}

/// <summary>
/// Karar veya atama verisinin hangi BIM nesnesine baglandigini belirtir.
/// </summary>
public enum MalzemeAtamaHedefTuru
{
    Tanimsiz = 0,
    Mahal = 1,
    Duvar = 2,
    Doseme = 3,
    Tavan = 4,
    Kapi = 5,
    Pencere = 6,
    Cekirdek = 7,
    Saft = 8
}

/// <summary>
/// Katmanli yapi elemani tiplerinde katmanin teknik rolunu belirtir.
/// </summary>
public enum MalzemeKatmanFonksiyonu
{
    Tanimsiz = 0,
    Tasiyici = 1,
    Kaplama = 2,
    Yalitim = 3,
    HavaBoslugu = 4,
    Boya = 5,
    Siva = 6,
    Bitis = 7,
    Membran = 8,
    Altlik = 9
}

/// <summary>
/// Malzeme verisinin hangi teknik amacla kullanilabilecegini gosterir.
/// </summary>
public enum MalzemeTeknikSinifi
{
    Tanimsiz = 0,
    Standart = 1,
    YanginaDayanikli = 2,
    Akustik = 3,
    IslakHacim = 4,
    IsilYalitim = 5,
    SuYalitimi = 6,
    KaymazYuzey = 7,
    Tasiyici = 8,
    DisKabuk = 9
}

/// <summary>
/// Metraj ve raporlamada kullanilacak varsayilan birim.
/// </summary>
public enum MetrajBirimi
{
    Tanimsiz = 0,
    Adet = 1,
    Metre = 2,
    Metrekare = 3,
    Metrekup = 4,
    Kilogram = 5,
    Litre = 6
}
