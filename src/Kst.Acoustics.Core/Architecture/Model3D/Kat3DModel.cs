using System;
using System.Collections.Generic;
using System.Linq;

namespace Kst.Acoustics.Core.Architecture.Model3D;

/// <summary>
/// Tek katin 3D hacim ve yapi elemani taslaklarini tasir.
/// </summary>
public class Kat3DModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string KaynakKatTaslagiId { get; set; } = "";
    public int KatNo { get; set; } = 1;
    public string Ad { get; set; } = "Kat 1";
    public double TabanKotuM { get; set; }
    public double KatYuksekligiM { get; set; } = 3.0;
    public double NetYukseklikM { get; set; } = 2.8;
    public List<Doseme3D> Dosemeler { get; set; } = new();
    public List<Tavan3D> Tavanlar { get; set; } = new();
    public List<MahalHacmi3D> Mahaller { get; set; } = new();
    public List<Duvar3D> Duvarlar { get; set; } = new();
    public List<Aciklik3D> Acikliklar { get; set; } = new();
    public List<CekirdekHacmi3D> CekirdekHacimleri { get; set; } = new();
    public List<SaftHacmi3D> SaftHacimleri { get; set; } = new();
    public string Notlar { get; set; } = "";

    public double UstKotuM => TabanKotuM + Math.Max(0, KatYuksekligiM);
    public double KullanilanNetYukseklikM => NetYukseklikM > 0 ? NetYukseklikM : Math.Max(0, KatYuksekligiM);
    public double ToplamMahalAlaniM2 => Mahaller.Sum(m => m.AlanM2);
    public double ToplamMahalHacmiM3 => Mahaller.Sum(m => m.HacimM3);
    public double ToplamDosemeAlaniM2 => Dosemeler.Sum(d => d.AlanM2);
    public double ToplamDuvarAlaniM2 => Duvarlar.Sum(d => d.AlanM2);
    public double ToplamCekirdekHacmiM3 => CekirdekHacimleri.Sum(c => c.HacimM3);
    public double ToplamSaftHacmiM3 => SaftHacimleri.Sum(s => s.HacimM3);

    public MahalHacmi3D? MahalBul(string? kaynakMahalBloguId)
    {
        if (string.IsNullOrWhiteSpace(kaynakMahalBloguId))
            return null;

        return Mahaller.FirstOrDefault(m =>
            string.Equals(m.KaynakMahalBloguId, kaynakMahalBloguId, StringComparison.OrdinalIgnoreCase));
    }
}
