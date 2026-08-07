namespace Kst.Acoustics.Core.Architecture.Models;

/// <summary>
/// Mimari ihtiyac programinda ve konsept yerlesimde kullanilacak temel mahal tipleri.
/// </summary>
public enum MahalTipi
{
    Tanimsiz = 0,

    // Konut
    Salon = 100,
    OturmaOdasi = 101,
    YatakOdasi = 102,
    CocukOdasi = 103,
    Mutfak = 104,
    Banyo = 105,
    Wc = 106,
    Antre = 107,
    Hol = 108,
    Balkon = 109,
    Teras = 110,

    // Ortak ve servis alanlari
    Koridor = 200,
    Depo = 201,
    TeknikOda = 202,
    ElektrikOdasi = 203,
    MekanikOda = 204,
    JeneratorOdasi = 205,
    KazanDairesi = 206,
    Siginak = 207,
    Otopark = 208,

    // Dusey sirkulasyon ve cekirdek
    Merdiven = 300,
    YanginMerdiveni = 301,
    Asansor = 302,
    Rampa = 303,
    Saft = 304,
    Cekirdek = 305,

    // Ofis ve ticari
    Ofis = 400,
    AcikOfis = 401,
    ToplantiOdasi = 402,
    Resepsiyon = 403,
    Arsiv = 404,
    ServerOdasi = 405,
    Magaza = 406,
    TicariAlan = 407,
    Restoran = 408,
    Kafe = 409,

    // Egitim, kultur ve saglik
    Derslik = 500,
    Laboratuvar = 501,
    Atolye = 502,
    OgretmenOdasi = 503,
    Kutuphane = 504,
    KonferansSalonu = 505,
    Fuaye = 506,
    Sahne = 507,
    Kulis = 508,
    MuayeneOdasi = 509,
    BeklemeAlani = 510,
    HastaOdasi = 511,
    PersonelOdasi = 512,
}
