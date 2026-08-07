using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Architecture.Circulation;

namespace Kst.Acoustics.Core.Architecture.Layout;

/// <summary>
/// Konsept plan icindeki tek katin blok, koridor ve taslak sinir verisini tasir.
/// </summary>
public class KatTaslagi
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int KatNo { get; set; } = 1;
    public string Ad { get; set; } = "Kat 1";
    public double HedefAlanM2 { get; set; }
    public List<MahalBlogu> Bloklar { get; set; } = new();
    public List<KoridorTaslagi> Koridorlar { get; set; } = new();
    public List<CekirdekElemani> Cekirdekler { get; set; } = new();
    public List<CekirdekGrubu> CekirdekGruplari { get; set; } = new();
    public List<SaftTaslagi> Saftlar { get; set; } = new();
    public PlanSiniri Sinir { get; set; } = new();
    public string Notlar { get; set; } = "";

    public int BlokSayisi => Bloklar.Count;
    public int CekirdekSayisi => Cekirdekler.Count;
    public int SaftSayisi => Saftlar.Count;
    public double BlokHedefAlanM2 => Bloklar.Sum(b => Math.Max(0, b.HedefAlanM2));
    public double BlokYerlesimAlanM2 => Bloklar.Sum(b => b.AlanM2);
    public double KoridorAlanM2 => Koridorlar.Sum(k => k.AlanM2);
    public double CekirdekAlanM2 => Cekirdekler.Sum(c => c.AlanM2);
    public double SaftAlanM2 => Saftlar.Sum(s => s.AlanM2);
    public double DuseySirkulasyonAlanM2 => CekirdekAlanM2 + SaftAlanM2;
    public double ToplamTaslakAlanM2 => BlokYerlesimAlanM2 + KoridorAlanM2 + DuseySirkulasyonAlanM2;

    public MahalBlogu? BlokBul(string? blokId)
    {
        if (string.IsNullOrWhiteSpace(blokId))
            return null;

        return Bloklar.FirstOrDefault(b => string.Equals(b.Id, blokId, StringComparison.OrdinalIgnoreCase));
    }

    public CekirdekElemani? CekirdekBul(string? cekirdekId)
    {
        if (string.IsNullOrWhiteSpace(cekirdekId))
            return null;

        return Cekirdekler.FirstOrDefault(c => string.Equals(c.Id, cekirdekId, StringComparison.OrdinalIgnoreCase));
    }

    public CekirdekGrubu? CekirdekGrubuBul(string? grupId)
    {
        if (string.IsNullOrWhiteSpace(grupId))
            return null;

        return CekirdekGruplari.FirstOrDefault(g => string.Equals(g.Id, grupId, StringComparison.OrdinalIgnoreCase));
    }

    public SaftTaslagi? SaftBul(string? saftId)
    {
        if (string.IsNullOrWhiteSpace(saftId))
            return null;

        return Saftlar.FirstOrDefault(s => string.Equals(s.Id, saftId, StringComparison.OrdinalIgnoreCase));
    }
}

public class PlanSiniri
{
    public double X { get; set; }
    public double Y { get; set; }
    public double GenislikM { get; set; }
    public double DerinlikM { get; set; }

    public double AlanM2 => Math.Max(0, GenislikM) * Math.Max(0, DerinlikM);
}
