using System;
using System.Collections.Generic;
using Kst.Acoustics.Core.Architecture.Models;

namespace Kst.Acoustics.Core.Architecture.Materials;

/// <summary>
/// Mimari BIM malzeme katalog satiri. Marka urun degil, teknik karar tasiyicisi olarak kullanilir.
/// </summary>
public class MimariMalzeme
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Kod { get; set; } = "";
    public string Ad { get; set; } = "";
    public MalzemeKategorisi Kategori { get; set; } = MalzemeKategorisi.Genel;
    public List<MalzemeTeknikSinifi> TeknikSiniflar { get; set; } = new();
    public List<MahalTipi> UygunMahalTipleri { get; set; } = new();
    public List<KullanimSinifi> UygunKullanimSiniflari { get; set; } = new();
    public MalzemePerformansi Performans { get; set; } = new();
    public MetrajBilgisi Metraj { get; set; } = new();
    public string KaynakKodu { get; set; } = "M36";
    public string Notlar { get; set; } = "";

    public bool KodluMu => !string.IsNullOrWhiteSpace(Kod);
    public bool AdliMi => !string.IsNullOrWhiteSpace(Ad);
    public bool IslakHacimUygun => Performans.SuyaDayanikli || Performans.IslakHacimUygun;
    public bool MetrajlanabilirMi => Metraj.MetrajlanabilirMi;

    public bool MahalIcinUygunMu(MahalTipi mahalTipi, KullanimSinifi kullanimSinifi)
    {
        var mahalUygun = UygunMahalTipleri.Count == 0 || UygunMahalTipleri.Contains(mahalTipi);
        var kullanimUygun = UygunKullanimSiniflari.Count == 0 || UygunKullanimSiniflari.Contains(kullanimSinifi);
        return mahalUygun && kullanimUygun;
    }

    public bool TeknikSinifVar(MalzemeTeknikSinifi teknikSinif)
        => TeknikSiniflar.Contains(teknikSinif);
}
