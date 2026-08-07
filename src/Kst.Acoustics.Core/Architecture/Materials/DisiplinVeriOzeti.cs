using System;
using System.Collections.Generic;

namespace Kst.Acoustics.Core.Architecture.Materials;

/// <summary>
/// M36 atama verisinden uretilen, tek bir 3D elemana ait disiplinler arasi okunabilir teknik satir.
/// </summary>
public sealed class DisiplinElemanVerisi
{
    public MalzemeAtamaHedefTuru ElemanTuru { get; init; } = MalzemeAtamaHedefTuru.Tanimsiz;
    public string ElemanId { get; init; } = "";
    public string ElemanAdi { get; init; } = "";
    public int KatNo { get; init; } = 1;
    public string TipId { get; init; } = "";
    public string TipAdi { get; init; } = "";
    public double AlanM2 { get; init; }
    public double HacimM3 { get; init; }

    // Akustik
    public double AkustikRw { get; init; }
    public double YutuculukAw { get; init; }
    public string AkustikNot { get; init; } = "";

    // Mekanik / enerji
    public double UDegeriWm2K { get; init; }
    public bool DisKabukMu { get; init; }

    // Yangin (birden cok disiplinde okunabilir)
    public int YanginDayanimDakika { get; init; }
    public string YanginTepkiSinifi { get; init; } = "";

    // Su / islak hacim
    public bool IslakHacimUygun { get; init; }
    public string KaymazlikSinifi { get; init; } = "";

    // Metraj
    public MetrajBirimi MetrajBirimi { get; init; } = MetrajBirimi.Metrekare;
    public int KatmanSayisi { get; init; }
    public double TahminiAgirlikKgM2 { get; init; }
    public IReadOnlyList<string> MalzemeAdlari { get; init; } = [];

    public double ToplamAgirlikKg => TahminiAgirlikKgM2 * Math.Max(0, AlanM2);
    public bool AkustikVeriVar => AkustikRw > 0 || YutuculukAw > 0 || !string.IsNullOrWhiteSpace(AkustikNot);
    public bool EnerjiVeriVar => UDegeriWm2K > 0;
    public bool YanginVeriVar => YanginDayanimDakika > 0 || !string.IsNullOrWhiteSpace(YanginTepkiSinifi);
}

/// <summary>
/// Akustik disiplini icin Rw / yutuculuk / not okunabilir ozeti.
/// </summary>
public sealed class AkustikDisiplinOzeti
{
    public int ElemanSayisi { get; init; }
    public int AkustikVeriliElemanSayisi { get; init; }
    public double EnYuksekRw { get; init; }
    public double EnYuksekYutuculukAw { get; init; }
    public IReadOnlyList<DisiplinElemanVerisi> Satirlar { get; init; } = [];

    public bool VeriVar => AkustikVeriliElemanSayisi > 0;
}

/// <summary>
/// Mekanik / enerji disiplini icin isi (U degeri) ve dis kabuk okunabilir ozeti.
/// </summary>
public sealed class EnerjiDisiplinOzeti
{
    public int DisKabukElemanSayisi { get; init; }
    public int UDegerliElemanSayisi { get; init; }
    public double EnDusukUDegeri { get; init; }
    public double EnYuksekUDegeri { get; init; }
    public IReadOnlyList<DisiplinElemanVerisi> Satirlar { get; init; } = [];

    public bool VeriVar => UDegerliElemanSayisi > 0 || DisKabukElemanSayisi > 0;
}

/// <summary>
/// Elektrik disiplini icin tek mahalin yuzey / kaplama okunabilir ozeti.
/// </summary>
public sealed class ElektrikMahalYuzeyOzeti
{
    public string MahalId { get; init; } = "";
    public string MahalAdi { get; init; } = "";
    public int KatNo { get; init; } = 1;
    public string ZeminTipiAdi { get; init; } = "";
    public string DuvarTipiAdi { get; init; } = "";
    public string TavanTipiAdi { get; init; } = "";
    public bool IslakHacim { get; init; }
    public string KaplamaNotu { get; init; } = "";
}

/// <summary>
/// Elektrik projesinin okuyabilecegi mahal yuzey / kaplama ozetleri.
/// </summary>
public sealed class ElektrikDisiplinOzeti
{
    public int MahalSayisi { get; init; }
    public IReadOnlyList<ElektrikMahalYuzeyOzeti> Mahaller { get; init; } = [];

    public bool VeriVar => MahalSayisi > 0;
}

/// <summary>
/// Metraj icin eleman turune gore toplam adet / alan / hacim / agirlik ozeti.
/// </summary>
public sealed class MetrajTuruOzeti
{
    public MalzemeAtamaHedefTuru ElemanTuru { get; init; } = MalzemeAtamaHedefTuru.Tanimsiz;
    public MetrajBirimi Birim { get; init; } = MetrajBirimi.Metrekare;
    public int ElemanSayisi { get; init; }
    public double ToplamAlanM2 { get; init; }
    public double ToplamHacimM3 { get; init; }
    public double ToplamAgirlikKg { get; init; }
}

/// <summary>
/// Metraj / kesif disiplininin okuyabilecegi birim, alan, hacim ve agirlik ozeti.
/// </summary>
public sealed class MetrajDisiplinOzeti
{
    public int ElemanSayisi { get; init; }
    public double ToplamAlanM2 { get; init; }
    public double ToplamHacimM3 { get; init; }
    public double ToplamAgirlikKg { get; init; }
    public IReadOnlyList<MetrajTuruOzeti> TurOzetleri { get; init; } = [];
    public IReadOnlyList<DisiplinElemanVerisi> Satirlar { get; init; } = [];

    public bool VeriVar => ElemanSayisi > 0;
}

/// <summary>
/// M36 atama verisinden uretilen, disiplinler arasi paylasilan okunabilir teknik ozet kovasi.
/// Akustik, mekanik/enerji, elektrik ve metraj alt ozetlerini tek noktada toplar.
/// </summary>
public sealed class DisiplinVeriOzeti
{
    public IReadOnlyList<DisiplinElemanVerisi> Elemanlar { get; init; } = [];
    public AkustikDisiplinOzeti Akustik { get; init; } = new();
    public EnerjiDisiplinOzeti Enerji { get; init; } = new();
    public ElektrikDisiplinOzeti Elektrik { get; init; } = new();
    public MetrajDisiplinOzeti Metraj { get; init; } = new();

    public int ToplamElemanSayisi => Elemanlar.Count;
}
