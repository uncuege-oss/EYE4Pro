using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Kst.Acoustics.Core.Architecture.Models;
using Kst.Acoustics.Core.Architecture.Rules;

namespace Kst.Acoustics.Core.Architecture.Reports;

/// <summary>
/// M37 mahal listesi tablosunun tek satiri. Kat, mahal no, ad, islev (kullanim sinifi),
/// alan ve hacim bilgisini tasir.
/// </summary>
public sealed class MahalListesiSatiri
{
    public int KatNo { get; init; } = 1;
    public string MahalNo { get; init; } = "";
    public string Ad { get; init; } = "";
    public MahalTipi MahalTipi { get; init; } = MahalTipi.Tanimsiz;
    public KullanimSinifi KullanimSinifi { get; init; } = KullanimSinifi.Tanimsiz;
    public int Adet { get; init; } = 1;
    public double AlanM2 { get; init; }
    public double HacimM3 { get; init; }
}

/// <summary>
/// M37 mahal listesi tablosu: satirlar ve toplam ozetleri.
/// </summary>
public sealed class MahalListesiTablosu
{
    public IReadOnlyList<MahalListesiSatiri> Satirlar { get; init; } = [];

    public int SatirSayisi => Satirlar.Count;
    public int ToplamMahalAdedi => Satirlar.Sum(s => s.Adet);
    public double ToplamAlanM2 => Satirlar.Sum(s => s.AlanM2);
    public double ToplamHacimM3 => Satirlar.Sum(s => s.HacimM3);
    public bool VeriVar => Satirlar.Count > 0;
}

/// <summary>
/// M37 alan tablosunun kullanim sinifi bazli kirilim satiri.
/// </summary>
public sealed class AlanKirilimSatiri
{
    public KullanimSinifi KullanimSinifi { get; init; } = KullanimSinifi.Tanimsiz;
    public int MahalSayisi { get; init; }
    public int ToplamAdet { get; init; }
    public double AlanM2 { get; init; }
    public double HacimM3 { get; init; }
    public double AlanOrani { get; init; }
}

/// <summary>
/// M37 alan tablosu: net/brut/sirkulasyon/teknik alanlar, hedef farki ve kullanim sinifi kirilimi.
/// `AlanOzetiServisi` ciktisini raporlanabilir tabloya cevirir.
/// </summary>
public sealed class AlanTablosu
{
    public double NetAlanM2 { get; init; }
    public double SirkulasyonAlanM2 { get; init; }
    public double TeknikAlanM2 { get; init; }
    public double BrutAlanM2 { get; init; }
    public double HedefToplamAlanM2 { get; init; }
    public double HedefFarkM2 { get; init; }
    public double HedefFarkOrani { get; init; }
    public double NetHacimM3 { get; init; }
    public int MahalSayisi { get; init; }
    public int ToplamMahalAdedi { get; init; }
    public IReadOnlyList<AlanKirilimSatiri> KullanimKirilimi { get; init; } = [];

    public bool VeriVar => NetAlanM2 > 0 || KullanimKirilimi.Count > 0;
}

/// <summary>
/// M37 kapi-pencere cizelgesinin tek satiri (ayni tip + olcu + yapi elemani tipindeki acikliklar gruplanir).
/// </summary>
public sealed class KapiPencereSatiri
{
    public string Poz { get; init; } = "";
    public bool PencereMi { get; init; }
    public double GenislikM { get; init; }
    public double YukseklikM { get; init; }
    public int Adet { get; init; }
    public string YapiElemaniTipiAdi { get; init; } = "";
    public IReadOnlyList<string> MalzemeAdlari { get; init; } = [];
    public double BirimAlanM2 { get; init; }

    public string Tip => PencereMi ? "Pencere" : "Kapı";
    public double ToplamAlanM2 => BirimAlanM2 * Adet;

    /// <summary>Genislik/Yukseklik santimetre cinsinden, "G/Y" biciminde (or. "90/210").</summary>
    public string Olcu => string.Format(
        CultureInfo.InvariantCulture, "{0:0}/{1:0}", GenislikM * 100, YukseklikM * 100);
}

/// <summary>
/// M37 kapi-pencere cizelgesi v1: taslak acikliklardan gruplanmis pozisyon satirlari.
/// </summary>
public sealed class KapiPencereCizelgesi
{
    public IReadOnlyList<KapiPencereSatiri> Satirlar { get; init; } = [];

    public int SatirSayisi => Satirlar.Count;
    public int ToplamKapiAdedi => Satirlar.Where(s => !s.PencereMi).Sum(s => s.Adet);
    public int ToplamPencereAdedi => Satirlar.Where(s => s.PencereMi).Sum(s => s.Adet);
    public bool VeriVar => Satirlar.Count > 0;
}

/// <summary>
/// M37 finisaj cizelgesinin tek satiri: bir mahalin zemin/duvar/tavan finisaj onerisi.
/// </summary>
public sealed class FinisajCizelgesiSatiri
{
    public int KatNo { get; init; } = 1;
    public string MahalNo { get; init; } = "";
    public string MahalAdi { get; init; } = "";
    public MahalTipi MahalTipi { get; init; } = MahalTipi.Tanimsiz;
    public string ZeminTipiAdi { get; init; } = "";
    public string DuvarTipiAdi { get; init; } = "";
    public string TavanTipiAdi { get; init; } = "";
    public bool IslakHacim { get; init; }
    public string Not { get; init; } = "";

    public bool TamFinisajli =>
        !string.IsNullOrWhiteSpace(ZeminTipiAdi) &&
        !string.IsNullOrWhiteSpace(DuvarTipiAdi) &&
        !string.IsNullOrWhiteSpace(TavanTipiAdi);
}

/// <summary>
/// M37 finisaj cizelgesi: mahal bazinda zemin/duvar/tavan finisaj onerisi.
/// </summary>
public sealed class FinisajCizelgesi
{
    public IReadOnlyList<FinisajCizelgesiSatiri> Satirlar { get; init; } = [];

    public int SatirSayisi => Satirlar.Count;
    public int IslakHacimSayisi => Satirlar.Count(s => s.IslakHacim);
    public int TamFinisajliMahalSayisi => Satirlar.Count(s => s.TamFinisajli);
    public bool VeriVar => Satirlar.Count > 0;
}

/// <summary>
/// M37 on kontrol raporunun tek satiri (M34 veri/plan + M36 finisaj uyarisi).
/// </summary>
public sealed class OnKontrolSatiri
{
    public KontrolSeviyesi Seviye { get; init; } = KontrolSeviyesi.Bilgi;
    public string KuralKodu { get; init; } = "";
    public string KaynakKodu { get; init; } = "";
    public string Baslik { get; init; } = "";
    public string Mesaj { get; init; } = "";
    public string CozumOnerisi { get; init; } = "";
    public string EtkilenenEleman { get; init; } = "";
    public string VeriEksigi { get; init; } = "";

    public bool KritikMi => Seviye == KontrolSeviyesi.Kritik;
}

/// <summary>
/// M37 on kontrol raporu: seviye/kaynak/kural kodlu uyari satirlari ve seviye sayilari.
/// Kesin uygunluk beyani icermez (M34 on kontrol dili).
/// </summary>
public sealed class OnKontrolRaporu
{
    public IReadOnlyList<OnKontrolSatiri> Satirlar { get; init; } = [];

    public int SatirSayisi => Satirlar.Count;
    public int KritikSayisi => Satirlar.Count(s => s.Seviye == KontrolSeviyesi.Kritik);
    public int UyariSayisi => Satirlar.Count(s => s.Seviye == KontrolSeviyesi.Uyari);
    public int KontrolEdilemediSayisi => Satirlar.Count(s => s.Seviye == KontrolSeviyesi.KontrolEdilemedi);
    public int BilgiSayisi => Satirlar.Count(s => s.Seviye == KontrolSeviyesi.Bilgi);
    public bool KritikVar => KritikSayisi > 0;
    public bool VeriVar => Satirlar.Count > 0;
}

/// <summary>
/// M37 tum mimari rapor ciktilarini tek nesnede toplar (HTML/CSV builder'larinin girdisi).
/// </summary>
public sealed class MimariRaporu
{
    public string ProjeAdi { get; init; } = "";
    public MahalListesiTablosu MahalListesi { get; init; } = new();
    public AlanTablosu AlanTablosu { get; init; } = new();
    public KapiPencereCizelgesi KapiPencereCizelgesi { get; init; } = new();
    public FinisajCizelgesi FinisajCizelgesi { get; init; } = new();
    public OnKontrolRaporu OnKontrol { get; init; } = new();
}
