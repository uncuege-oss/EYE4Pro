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
    public bool BirimOnayiGerekiyor { get; set; }
    public string KaynakBirim { get; set; } = "";
    public double UygulananOlcek { get; set; } = 1;
    public int GeometrikKotSayisi { get; set; }
    public int MetindenKotSayisi { get; set; }
    public int AtlananKotsuzNoktaSayisi { get; set; }
    public List<AraziImportKatmani> Katmanlar { get; set; } = new();
}

public sealed class AraziImportKatmani
{
    public string Ad { get; set; } = "";
    public int ElemanSayisi { get; set; }
    public int NoktaSayisi { get; set; }
    public int KonturSayisi { get; set; }
    public int KotMetniSayisi { get; set; }
    public bool AraziKatmaniOnerisi { get; set; }
}
