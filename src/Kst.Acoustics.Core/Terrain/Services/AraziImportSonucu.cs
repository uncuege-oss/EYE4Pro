using System.Collections.Generic;
using Kst.Acoustics.Core.Terrain.Models;

namespace Kst.Acoustics.Core.Terrain.Services;

/// DXF arazi içe aktarma çıktısı: üretilen arazi modeli + kullanıcıya gösterilecek uyarılar.
public class AraziImportSonucu
{
    public AraziModeli Arazi { get; set; } = new();
    public List<string> Uyarilar { get; set; } = new();

    /// Kotu çözülemeyen (KotKaynagi=Bilinmiyor) kontur sayısı — kullanıcı elle girmeli.
    public int KotsuzKonturSayisi { get; set; }
}
