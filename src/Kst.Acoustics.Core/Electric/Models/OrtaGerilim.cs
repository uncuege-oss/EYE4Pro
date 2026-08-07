using System.Collections.Generic;

namespace Kst.Acoustics.Core.Electric.Models;

/// <summary>
/// Trafo soğutma/yalıtım tipi.
/// </summary>
public enum TrafoTipi
{
    /// <summary>Yağlı (ONAN) — genleşme tanklı, serbest soluyan yağ izoleli.</summary>
    Yagli,
    /// <summary>Hermetik — tam dolu, sızdırmaz yağ izoleli.</summary>
    Hermetik,
    /// <summary>Kuru tip — dökme reçine (cast resin) kuru izoleli.</summary>
    KuruTip
}

/// <summary>
/// Trafo + OG hücrelerinin bulunduğu köşk/bina tipi.
/// </summary>
public enum KoskTipi
{
    /// <summary>Bina altı (bodrum/zemin) trafo merkezi.</summary>
    BinaAlti,
    /// <summary>Dış mekan prefabrik beton köşk.</summary>
    DisMekanBetonKosk,
    /// <summary>Harici yapı (beton köşk dışı bir bina/oda).</summary>
    HariciYapi
}

/// <summary>
/// OG hücre işlevi.
/// </summary>
public enum OgHucreTipi
{
    /// <summary>Giriş hücresi (şebeke/besleme).</summary>
    Giris,
    /// <summary>Ölçü hücresi (sayaç + ölçü trafoları).</summary>
    Olcu,
    /// <summary>Bara bağlama (bus coupler) hücresi.</summary>
    BaraBaglama,
    /// <summary>Çıkış (fider) hücresi.</summary>
    Cikis,
    /// <summary>Trafo koruma hücresi.</summary>
    TrafoKoruma
}

/// <summary>
/// OG hücresi koruma/manevra eleman tipi (hücre çeşitleri).
/// </summary>
public enum OgHucreKorumaTipi
{
    /// <summary>Yük ayırıcılı (load-break switch).</summary>
    YukAyirici,
    /// <summary>Yük ayırıcılı + sigortalı (LBS + HRC sigorta — trafo koruma).</summary>
    YukAyiriciSigortali,
    /// <summary>Kesicili (vakum/SF6 kesici + koruma rölesi).</summary>
    Kesicili
}

/// <summary>OG XLPE kablonun iletken malzemesi.</summary>
public enum OgKabloIletkenMalzemesi
{
    Aluminyum,
    Bakir
}

/// <summary>Şebeke, hücre ve trafo arasındaki bir OG XLPE kablo segmenti.</summary>
public class OgXlpeKablo
{
    /// <summary>Alüminyum (NA2XSY) veya bakır (N2XSY) iletken.</summary>
    public OgKabloIletkenMalzemesi IletkenMalzemesi { get; set; } = OgKabloIletkenMalzemesi.Aluminyum;

    /// <summary>Tek damarın faz iletken kesiti (mm²).</summary>
    public double KesitMm2 { get; set; } = 50;

    /// <summary>Kablo güzergâh uzunluğu (m).</summary>
    public double UzunlukM { get; set; } = 10;
}

/// <summary>
/// Trafo AG çıkışı ile ana pano arasındaki bağlantı şekli.
/// </summary>
public enum TrafoCikisBaglantiTipi
{
    /// <summary>Kablo (tek veya paralel kollu).</summary>
    Kablo,
    /// <summary>Bar-24 / busbar kanal (busbar trunking).</summary>
    Bar24,
    /// <summary>AG giriş hücresi/kofresi üzerinden.</summary>
    Hucre
}

/// <summary>
/// Köşk içindeki bir OG hücresi örneği — kütüphane <see cref="OgHucreTanim"/>'a ada göre
/// referans verir. <see cref="OrtaGerilimSistemi.Hucreler"/> listesinde sıralı tutulur.
/// </summary>
public class OgHucre
{
    public string Id { get; set; } = System.Guid.NewGuid().ToString();
    public string Ad { get; set; } = "";

    /// <summary>Kütüphaneden seçilen hücre tipi adı.</summary>
    public string HucreTanimAd { get; set; } = "";

    /// <summary>Hücre işlevi (kütüphane tanımından kopyalanır, gerekirse override).</summary>
    public OgHucreTipi HucreTipi { get; set; } = OgHucreTipi.Cikis;

    // === Hesaplanan değerler ===
    /// <summary>Hücreden geçen anma akımı (A, OG tarafı).</summary>
    public double AkimA { get; set; }

    /// <summary>Hücre anma akımı yükü karşılıyor mu (kütüphane AnmaAkimA ≥ AkimA).</summary>
    public bool AnmaAkimYeterliMi { get; set; } = true;
}

/// <summary>
/// Trafo AG çıkışı → ana pano bağlantısı. Tipi kablo, Bar-24 busbar kanal veya AG hücresi olabilir.
/// </summary>
public class TrafoCikisBaglanti
{
    public TrafoCikisBaglantiTipi Tip { get; set; } = TrafoCikisBaglantiTipi.Kablo;

    /// <summary>Referans ad: kablo tipi (örn. "NYY"), <see cref="BusbarTanim"/> adı veya hücre etiketi.</summary>
    public string ReferansAd { get; set; } = "";

    /// <summary>Bağlantı uzunluğu (m).</summary>
    public double UzunlukM { get; set; } = 10;

    /// <summary>Minimum paralel kol sayısı (kablo için; hesap gerekirse otomatik artırır).</summary>
    public int ParalelKolSayisi { get; set; } = 1;

    /// <summary>Güncel trafo akımı ve mesafeye göre hesaplanan gerçek paralel kol sayısı.</summary>
    public int HesaplananParalelKolSayisi { get; set; } = 1;

    // === Hesaplanan değerler ===
    /// <summary>Bağlantıdan geçen akım (A, AG tarafı — trafo tam yük akımı).</summary>
    public double AkimA { get; set; }

    /// <summary>Kablo seçildiyse ortak formatlı kablo etiketi (örn. "6x(4x240) mm²").</summary>
    public string KesitMm2 { get; set; } = "";

    /// <summary>Gerilim düşümü yüzdesi (%).</summary>
    public double GerilimDususYuzde { get; set; }

    /// <summary>Bağlantıdaki ısı kaybı (W).</summary>
    public double IsiKaybiW { get; set; }
}

/// <summary>
/// Orta gerilim besleme sistemi — ana panonun (kök/ana pano) üstündeki şebeke girişi,
/// OG hücreleri, trafo ve AG çıkış bağlantısını modeller. Tüm ekipman bir köşkte yer alır.
/// <para>
/// Projede tek bir <see cref="OrtaGerilimSistemi"/> bulunur (tek trafo → tek ana pano).
/// Kütüphane <see cref="TrafoTanim"/> ve <see cref="OgHucreTanim"/>'a ada göre referans verir;
/// <see cref="Jenerator"/> / <see cref="Ups"/> deseniyle aynıdır.
/// </para>
/// <para>
/// GELİŞTİRME NOTU (çoklu trafo): İleride çift trafolu / çift baralı tesisler için bu sınıf
/// <c>List&lt;TrafoTanimAd&gt;</c> ve bara bağlama (bus coupler) ile birden fazla AG seksiyonu /
/// ana pano besleyecek şekilde genişletilebilir. Şu an tek trafo + tek ana pano modellenir;
/// bara bağlama hücresi kütüphanede tanımlıdır ancak tek seksiyon çizilir.
/// </para>
/// </summary>
public class OrtaGerilimSistemi
{
    public string Id { get; set; } = System.Guid.NewGuid().ToString();

    /// <summary>Beslediği ana (kök) pano adı.</summary>
    public string AnaPanoAd { get; set; } = "";

    /// <summary>Köşk/trafo merkezi tipi.</summary>
    public KoskTipi KoskTipi { get; set; } = KoskTipi.DisMekanBetonKosk;

    // === Köşk fiziksel ölçüleri (köşk yerleşim çizimi için; 0 ise dizilimden otomatik) ===
    public double KoskGenislikMm { get; set; }
    public double KoskDerinlikMm { get; set; }
    public double KoskYukseklikMm { get; set; }

    // === Şebeke (besleme) parametreleri ===
    /// <summary>OG primer gerilim (kV) — örn. 34.5, 33, 15.8.</summary>
    public double OgGerilimKV { get; set; } = 34.5;

    /// <summary>Şebeke kısa devre gücü Skq (MVA). 0 ise <see cref="SebekeIkKA"/> kullanılır.</summary>
    public double SebekeKisaDevreMVA { get; set; } = 350;

    /// <summary>Şebeke kısa devre akımı (kA, OG tarafı) — Skq verilmezse kullanılır.</summary>
    public double SebekeIkKA { get; set; } = 12.5;

    /// <summary>Şebeke → giriş hücresi OG XLPE kablosu.</summary>
    public OgXlpeKablo SebekeHucreXlpeKablosu { get; set; } = new();

    /// <summary>Trafo koruma hücresi → trafo OG XLPE kablosu.</summary>
    public OgXlpeKablo HucreTrafoXlpeKablosu { get; set; } = new();

    /// <summary>Kütüphaneden seçilen trafo tipi adı (tek trafo).</summary>
    public string TrafoTanimAd { get; set; } = "";

    /// <summary>Trafo/OG sisteminin bulunduğu kat etiketi (örn. "Bodrum 2", "Çatı"). Boş ise OG bloğu
    /// şemanın en üstündeki ayrılmış banda (geleneksel OG girişi) çizilir; doluysa o katın üstündeki
    /// ayrılmış banda yerleştirilir (trafo o kat seviyesinde görünür).</summary>
    public string TrafoKat { get; set; } = "";

    /// <summary>Trafo boyutlandırma emniyet/büyüme faktörü (örn. 1.0–1.25).</summary>
    public double EmniyetFaktoru { get; set; } = 1.0;

    /// <summary>Köşk içindeki OG hücreleri (sıralı dizilim — soldan sağa).</summary>
    public List<OgHucre> Hucreler { get; set; } = new();

    /// <summary>Trafo AG çıkışı → ana pano bağlantısı.</summary>
    public TrafoCikisBaglanti CikisBaglanti { get; set; } = new();

    // === Hesaplanan değerler ===
    /// <summary>Ana panodan gelen görünür yük (kVA).</summary>
    public double HesaplananYukKVA { get; set; }

    /// <summary>Emniyet faktörlü gerekli trafo gücü (kVA min.).</summary>
    public double GerekliKVAMin { get; set; }

    /// <summary>Seçilen trafo yükü karşılıyor mu.</summary>
    public bool TrafoYeterliMi { get; set; }

    /// <summary>Trafo AG (sekonder) tam yük akımı (A).</summary>
    public double TrafoAgAkimA { get; set; }

    /// <summary>Trafo OG (primer) tam yük akımı (A).</summary>
    public double TrafoOgAkimA { get; set; }

    /// <summary>Trafo AG barasındaki üç-faz kısa devre akımı Ik3 (kA).</summary>
    public double AgBaraIkKA { get; set; }
}
