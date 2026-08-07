namespace Kst.Acoustics.Core.Landscape.Models;

/// Park sert zemin (hardscape) öğesinin türü. Bitki/ağaç bu listede YOKTUR —
/// yumuşak zemin (softscape) M71 Bitkilendirme modülünde tanımlanır (codex70 §3).
public enum ParkOgeTipi
{
    YuruyusYolu,   // güzergâh + genişlik (çizgi/şerit)
    DosemeAlani,   // meydan / sert döşeme (poligon)
    OturmaAlani,   // oturma bölgesi (poligon)
    Bank,          // tekil bank (nokta + yön)
    SusHavuzu      // süs havuzu / su öğesi (poligon)
}

/// Sert zemin kaplama tipi (alan/oran ve ileride metraj için).
public enum KaplamaTipi
{
    Bilinmiyor,
    BetonPlak,
    DogalTas,
    Granit,
    Ahsap,
    Cakil,
    AsfaltKaucuk,  // oyun alanı / spor zemini
    Su             // süs havuzu su yüzeyi
}

/// `.e4arazi` / `.e4bina` altlıklarının park modeline bağlanma durumu.
/// Park bu geometrileri salt-okunur tüketir; değiştirmez (codex70 §3).
public enum AltlikDurumu
{
    Yok,        // altlık yüklenmedi (düz zemin varsayımı)
    Bagli,      // altlık yüklü ve revizyon güncel
    Eskimis     // altlık güncellenmiş, park yerleşimi doğrulanmalı
}
