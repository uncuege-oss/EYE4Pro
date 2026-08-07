using System.Collections.Generic;

namespace Kst.Acoustics.Core.Electric.Models;

/// <summary>
/// Elektrik projesi — tüm pano hiyerarşisini ve proje ayarlarını barındırır.
/// </summary>
public class ElektrikProje
{
    public string ProjeAdi { get; set; } = "Yeni Elektrik Projesi";
    public string BinaTipi { get; set; } = "Konut";
    public double SistemGerilimV { get; set; } = 400;      // 3 faz hat gerilimi
    public double FazGerilimV { get; set; } = 230;          // Tek faz
    public double CosF { get; set; } = 0.85;                // Mevcut güç faktörü
    public double HedefCosF { get; set; } = 0.99;           // Hedef güç faktörü
    public double KompanzasyonTetikKW { get; set; } = 15;   // 15kW üstü → kompanzasyon
    public string VarsayilanKabloTipi { get; set; } = "NHXMH";

    /// <summary>Selektivite uygulansın mı? — Aktif ise üst pano çıkış sigortası bir üst standart değere çekilir.</summary>
    public bool SelektiviteAktif { get; set; } = false;

    /// <summary>İzin verilen <b>segment</b> gerilim düşümü yüzdesi (%) — her kablo/busbar segmenti
    /// (pano besleme, linye, busbar çıkışı) bu sınıra göre boyutlandırılır. Son devre için tipik %3.</summary>
    public double IzinVerilenGerilimDususYuzde { get; set; } = 3.0;

    /// <summary>İzin verilen <b>toplam (kümülatif)</b> gerilim düşümü yüzdesi (%) — kaynaktan
    /// (trafo/ana pano) en uçtaki yüke kadar birikimli ΔU bu sınırı aşarsa uyarı verilir.
    /// TS HD 60364-5-52'ye göre aydınlatma için tipik %3, diğer kullanımlar için %5.</summary>
    public double IzinVerilenToplamGerilimDususYuzde { get; set; } = 5.0;

    // === Kısa devre (IEC 60909 basitleştirilmiş) ===
    /// <summary>Besleme trafosunun gücü (kVA). 0 ise trafo yok; ana baradaki Ik için
    /// <see cref="SebekeBaslangicIkKA"/> kullanılır.</summary>
    public double TrafoGucKVA { get; set; } = 0;

    /// <summary>Trafo kısa devre gerilimi uk (%). Trafo empedansını belirler (tipik 4–6).</summary>
    public double TrafoUkYuzde { get; set; } = 4.0;

    /// <summary>Trafo yoksa ana baradaki prospektif üç-faz kısa devre akımı (kA).
    /// Şebeke bağlantısı için varsayılan kaynak empedansını belirler.</summary>
    public double SebekeBaslangicIkKA { get; set; } = 10.0;

    /// <summary>Bina kat konfigürasyonu — şema yerleşiminde kat çizgilerini belirler.</summary>
    public BinaKonfigurasyonu Bina { get; set; } = new();

    /// <summary>Kök pano — en üst seviye (ana pano veya sayaç panosu).</summary>
    public Pano? KokPano { get; set; }

    /// <summary>Tüm panolar düz liste (navigasyon kolaylığı).</summary>
    public List<Pano> TumPanolar { get; set; } = new();

    /// <summary>Tüm busbar hatları düz liste.</summary>
    public List<BusbarHatti> TumBusbarlar { get; set; } = new();

    /// <summary>Kompanzasyon hesap sonucu (sadece ana panoda, >15kW ise).</summary>
    public KompanzasyonSonucu? Kompanzasyon { get; set; }

    /// <summary>Eski tek jeneratör alanı. Yeni projelerde <see cref="JeneratorGruplari"/> kullanılır.</summary>
    public Jenerator? Jenerator { get; set; }

    /// <summary>Projedeki jeneratör grupları — her grup bir senkron panosu üzerinden ana panoya bağlanır.</summary>
    public List<JeneratorGrubu> JeneratorGruplari { get; set; } = new();

    /// <summary>Orta gerilim besleme sistemi — ana panonun üstündeki şebeke girişi, OG hücreleri,
    /// trafo ve köşk. Opsiyonel (null ise OG katmanı çizilmez, kısa devre eski TrafoGucKVA/
    /// SebekeBaslangicIkKA alanlarından türetilir).</summary>
    public OrtaGerilimSistemi? OrtaGerilim { get; set; }

    /// <summary>Projedeki UPS'ler — her biri bir besleyen panonun altındaki kritik panoyu besler.</summary>
    public List<Ups> Upsler { get; set; } = new();

    /// <summary>UPS montaj topolojisi (proje geneli): Lokal (kritik pano önü) veya Merkezi
    /// (ana pano → merkezi UPS → dağıtım → kat kritik panoları). Varsayılan Lokal.</summary>
    public UpsTopolojisi UpsTopolojisi { get; set; } = UpsTopolojisi.Lokal;
}
