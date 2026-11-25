using System;
using System.Collections.Generic;

namespace Kst.Acoustics.Core.Electric.Models;

/// <summary>
/// Busbar tipi tanımı — kütüphaneden yüklenir.
/// </summary>
public class BusbarTanim
{
    public string Ad { get; set; } = "";
    public int NominalAkimA { get; set; } = 160;
    /// <summary>İletken malzemesi: "Bakır" veya "Alüminyum".</summary>
    public string Malzeme { get; set; } = "Bakır";
    public string KesitMm2 { get; set; } = "";
    /// <summary>Birim direnç (mΩ/m) — gerilim düşümü hesabı için.</summary>
    public double DirencMOhmPerM { get; set; } = 0.18;
    /// <summary>Birim reaktans (mΩ/m).</summary>
    public double ReaktansMOhmPerM { get; set; } = 0.10;
    /// <summary>Açıklama (üretici, tip vb.).</summary>
    public string Aciklama { get; set; } = "";

    public override string ToString() => $"{Ad} ({NominalAkimA}A • {Malzeme})";
}

/// <summary>
/// Busbar üzerinde bir çıkış kutusu — bir sigorta üzerinden bir tali panoya besleme verir.
/// </summary>
public class BusbarCikisKutusu
{
    public string Ad { get; set; } = "";
    /// <summary>Busbar üzerindeki konum (m, ana panodan itibaren).</summary>
    public double PozisyonM { get; set; }
    /// <summary>Çıkış kutusunun bağlı panonun bulunduğu kattaki busbar başlangıcından konumu (m).
    /// Kat yerleşimi tanımlıysa <see cref="PozisyonM"/> bu değerden otomatik türetilir.</summary>
    public double? KatPozisyonM { get; set; }
    /// <summary>Çıkış kutusu sigortası (A).</summary>
    public int SigortaA { get; set; }
    /// <summary>Çıkış kutusundan tali panoya kablo uzunluğu (m).</summary>
    public double KabloMesafeM { get; set; } = 2;
    /// <summary>Bağlı tali pano adı (referans).</summary>
    public string BagliPanoAd { get; set; } = "";

    /// <summary>Busbar başlangıcından (üst pano çıkışı) bu çıkış kutusunun pozisyonuna kadar
    /// birikimli gerilim düşümü (%). Kümülatif ΔU zincirinde busbar segmenti olarak kullanılır.</summary>
    public double GerilimDususYuzde { get; set; }
}

/// <summary>Busbar hattının bir kattaki fiziksel uzunluğu.</summary>
public class BusbarKatBolumu
{
    public string Kat { get; set; } = "Z";
    public double UzunlukM { get; set; }
}

/// <summary>
/// Busbar hattı — bir üst panonun çıkışından başlar, çıkış kutuları üzerinden tali panoları besler.
/// </summary>
public class BusbarHatti
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Ad { get; set; } = "Busbar-1";
    /// <summary>Kütüphane busbar tipi adı.</summary>
    public string BusbarTipiAd { get; set; } = "";
    /// <summary>Hesaplanan toplam akıma göre busbar tipini otomatik seç.</summary>
    public bool OtomatikTipSec { get; set; } = true;
    /// <summary>Toplam busbar hattı uzunluğu (m).</summary>
    public double ToplamUzunlukM { get; set; } = 20;
    /// <summary>Eski proje dosyalarıyla uyumluluk için tutulur; yeni akışta kutu pozisyonları elle girilir.</summary>
    public double KutuAraligiM { get; set; } = 5;
    /// <summary>Hangi panonun çıkışından başlar (üst pano adı).</summary>
    public string UstPanoAd { get; set; } = "";
    /// <summary>Çıkış kutuları.</summary>
    public List<BusbarCikisKutusu> CikisKutulari { get; set; } = new();
    /// <summary>Kat bazında busbar uzunlukları. Boşsa eski toplam uzunluk/pozisyon modeli kullanılır.</summary>
    public List<BusbarKatBolumu> KatBolumleri { get; set; } = new();

    // === Hesaplanan değerler ===
    /// <summary>Busbar üzerinden geçen toplam akım (A).</summary>
    public double ToplamAkimA { get; set; }
    /// <summary>Hesaplanan gerilim düşümü yüzdesi (%).</summary>
    public double GerilimDususYuzde { get; set; }
    /// <summary>Busbar üzerindeki ısı kaybı (W).</summary>
    public double IsiKaybiW { get; set; }
}
