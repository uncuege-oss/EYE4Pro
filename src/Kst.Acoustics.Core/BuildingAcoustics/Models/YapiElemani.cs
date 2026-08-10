using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Kst.Acoustics.Core.BuildingAcoustics.Models;

public enum YapiElemaniTipi
{
    Duvar,
    Doseme,
    Tavan,
    Pencere,
    Kapi
}

/// EN 12354-1 Annex E sınıflaması: Kij formülleri eleman kategorisine göre farklı tablodan okunur.
/// - Homogen: tek cidarlı, sürekli ağır eleman (beton, tuğla, BA döşeme). Annex E.1 formülleri.
/// - Hafif: çift plakalı karkas (alçıpan/OSB üzeri ahşap/metal C profil + dolgu). Annex E.2 düşük temas tabanı.
/// - Sandvic: ağır + esnek katman + ağır (sandviç panel, prekast giydirme). Karışık tabanlı.
public enum YapiElemaniHafifligi
{
    Homogen,
    Hafif,
    Sandvic,
}

/// <summary>
/// Oktav/üçüncü-oktav verisinin izlenebilir kökeni. <see cref="Belirsiz"/>, eski
/// proje ve kataloglarla geriye dönük uyumluluk içindir; güvenilirlik hesabında
/// ölçüm veya üretici verisi olarak kabul edilmez.
/// </summary>
public enum SpektrumVeriKaynagi
{
    Belirsiz,
    Olcum,
    Uretici,
    Model,
    Varsayilan,
}

public sealed record SpektrumVeriKaynagiSecenegi(SpektrumVeriKaynagi Deger, string Ad)
{
    public override string ToString() => Ad;
}

public class YapiElemani
{
    private static readonly IReadOnlyList<SpektrumVeriKaynagiSecenegi> _spektrumKaynagiSecenekleri =
    [
        new(SpektrumVeriKaynagi.Belirsiz, "Belirsiz"),
        new(SpektrumVeriKaynagi.Olcum, "Ölçüm"),
        new(SpektrumVeriKaynagi.Uretici, "Üretici / laboratuvar"),
        new(SpektrumVeriKaynagi.Model, "Hesap modeli"),
        new(SpektrumVeriKaynagi.Varsayilan, "Varsayılan spektrum")
    ];

    public static IReadOnlyList<SpektrumVeriKaynagiSecenegi> TumSpektrumKaynagiSecenekleri =>
        _spektrumKaynagiSecenekleri;

    /// <summary>
    /// Katalog ve proje arasında kullanılan kararlı ürün kimliği. Eski kayıtlarda
    /// <see cref="Guid.Empty"/> olabilir; yükleme migrasyonu ad+tip anahtarından
    /// deterministik bir kimlik üretir.
    /// </summary>
    public Guid Id { get; set; }

    public string Ad { get; set; } = "";
    public YapiElemaniTipi Tip { get; set; }
    public List<YapiKatmani> Katmanlar { get; set; } = new();

    // ── Ürün/katalog ve metraj bilgileri ─────────────────────────────────
    public string Uretici { get; set; } = "";
    public string UrunKodu { get; set; } = "";
    public string Kategori { get; set; } = "";
    public string PozNo { get; set; } = "";
    public AkustikMetrajBirimi MetrajBirimi { get; set; } = AkustikMetrajBirimi.Metrekare;
    public decimal BirimFiyat { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
    public decimal FireYuzdesi { get; set; }
    public DateTime? BirimFiyatTarihi { get; set; }
    public List<UrunBelgesi> Belgeler { get; set; } = new();

    public double ToplamYuzeyKutlesi => Katmanlar.Sum(k => k.YuzeyKutlesi); // m' (kg/m2)

    /// Ek2 — Toplam yapısal kalınlık (mm). Hava boşlukları hariç. Katman yoksa 0.
    public double ToplamKalinlikMm => Katmanlar
        .Where(k => k.Yogunluk > 50)
        .Sum(k => k.Kalinlik);

    /// Ek2 — EN 12354-1 ince eleman ayrımı: yapısal kalınlık < 100 mm.
    /// İnce elemanlar bending wave teorisinde plaka olarak modellenir; junction'larda
    /// daha az flanking iletim gösterir. Lab Rw değerleri bunu zaten içerdiği için
    /// bu bayrak şu an yalnızca UI etiketi ve güvenilirlik kontrolü için bilgilendiricidir.
    public bool IsInce => ToplamKalinlikMm > 0 && ToplamKalinlikMm < 100;

    // Akustik Performans Verileri (Laboratuvar Değerleri)
    public double Rw { get; set; } // Ses Azaltma İndisi (dB)
    public double C { get; set; }  // Pembe Gürültü Adaptasyon Terimi
    public double Ctr { get; set; } // Trafik Gürültüsü Adaptasyon Terimi

    /// EN 12354-1 Annex D — kaplama/giydirme/yüzer döşeme tipi katmanın yapısal elemanın
    /// üzerine eklediği iyileştirme ΔR_w (dB). 0 ise kaplama yok / catalog Rw zaten içeriyor.
    /// Etkin R: EffectiveRw = Rw + DeltaRDb (hem direkt hem flanking yolda kullanılır).
    public double DeltaRDb { get; set; } = 0.0;

    /// Opsiyonel 16-band 1/3 oktav ΔR(f), 100–3150 Hz. Dolu ise band bazlı uygulanır,
    /// boş ise uniform DeltaRDb düşer. Catalog ΔR(f) verisi bu listeye yazılır.
    public List<double> DeltaRThirdOctave { get; set; } = new();

    /// Kaplama dahil etkin tek sayı Rw. Hesap motorları bu değeri kullanır.
    public double EffectiveRw => Rw + DeltaRDb;
    /// A6 — Eleman bağlantı tipi: Kij hesabı için kullanılır (12354-1 Annex E).
    public BaglantiTipi BaglantiTipi { get; set; } = BaglantiTipi.Rijit;
    /// Eski kataloglarda kalan tek-sayı flanking notu. Hesap motorunda otomatik Kij override
    /// olarak kullanılmaz; kullanıcı override'ı Junction.KijOverrideDb alanındadır.
    public double FlankingDuzeltmesiDb { get; set; } = 0.0;

    // Darbe Sesi (Sadece döşemeler için)
    public double Lnw { get; set; } // Normalize Darbe Sesi Seviyesi (dB)
    public double DeltaLw { get; set; } // Darbe Sesi Azaltma (Yüzer döşeme vb. için)

    // Yüzer döşeme: elastik katman dinamik rijitliği (MN/m³). 0 → fr hesaplanmaz.
    public double DinamikRijitlikMNm3 { get; set; } = 0.0;

    /// EN 12354-1 Annex C / ISO 10848 in-situ yapısal RT (T_s, s). Eşdeğer soğurma uzunluğu
    /// a_i = 2.2π²·S/(c₀·T_s)·√(f_ref/f) hesabı için elemana özgü değer; 0 ise önce
    /// <see cref="TahminiYapisalRtSn"/> tahmini, o da yoksa varsayılan 0.5 s kullanılır.
    public double YapisalRtSn { get; set; } = 0.0;

    /// EN 12354-1 Annex C — kütleden in-situ yapısal RT (T_s, s) tahmini.
    /// In-situ ölçüm (YapisalRtSn) yoksa fallback olarak kullanılır.
    ///   η_total = η_int + m'/(485·√f)      (Annex C, Eq. C.1)
    ///   T_s     = 2.2 / (f · η_total)
    /// η_int ağır homojen yapı için ≈ 0,01 kabul edilir (Annex C tipik değer); f = 500 Hz
    /// (Kij eşdeğer soğurma uzunluğu referansı). m' ≤ 0 ise tahmin yapılamaz → 0 döner.
    public double TahminiYapisalRtSn(double icKayipFaktoru = 0.01, double f = 500.0)
    {
        double mPrime = ToplamYuzeyKutlesi;
        if (mPrime <= 0 || f <= 0) return 0;
        double etaTotal = icKayipFaktoru + mPrime / (485.0 * Math.Sqrt(f));
        return etaTotal > 0 ? 2.2 / (f * etaTotal) : 0;
    }

    /// EN 12354-1 Annex E sınıfı (Homogen/Hafif/Sandvic). Kij formül tablosunu seçer.
    /// Varsayılan Homogen (ağır beton/tuğla). Alçıpan-karkas duvar için Hafif seçilmeli;
    /// hafif elemanlar junction'da daha yüksek decoupling üretir.
    public YapiElemaniHafifligi Hafiflik { get; set; } = YapiElemaniHafifligi.Homogen;

    // 1/1 oktav bant açık verileri (hava sesi için TL/R değerleri, dB)
    public double R63 { get; set; }
    public double R125 { get; set; }
    public double R250 { get; set; }
    public double R500 { get; set; }
    public double R1k { get; set; }
    public double R2k { get; set; }
    public double R4k { get; set; }
    public double R8k { get; set; }

    // ISO 717-1 ana hesabı için 1/3 oktav R değerleri, 100–3150 Hz (16 bant).
    // Sıra: 100, 125, 160, 200, 250, 315, 400, 500, 630, 800, 1000, 1250, 1600, 2000, 2500, 3150 Hz.
    public List<double> RThirdOctave { get; set; } = new();

    // Darbe sesi oktav bant verileri — Ln(f) laboratuvar değerleri (dB), 63–4000 Hz
    public double L63 { get; set; }
    public double L125 { get; set; }
    public double L250 { get; set; }
    public double L500 { get; set; }
    public double L1k { get; set; }
    public double L2k { get; set; }
    public double L4k { get; set; }

    /// Gerçek ölçüm verisi varsa true; false ise R(f)/Ln(f) model tahmindir.
    /// Geriye dönük uyumluluk alanıdır. Yeni kayıtlarda <see cref="SpektrumKaynagi"/>
    /// kullanılmalıdır; true + Belirsiz, etkin olarak Ölçüm kabul edilir.
    public bool HasRealOktavData { get; set; } = false;

    /// Ölçüm/üretici/model/varsayılan ayrımını kayıplı bir bool yerine korur.
    /// Belirsiz + HasRealOktavData=false olan dolu eski spektrumlar güvenli tarafta
    /// kalınarak model/belirsiz kabul edilir.
    public SpektrumVeriKaynagi SpektrumKaynagi { get; set; } = SpektrumVeriKaynagi.Belirsiz;

    /// Döşemenin Ln(f) verisi, hava sesi R(f) verisinden farklı bir kaynaktan
    /// gelebilir. Belirsiz ise SpektrumKaynagi/legacy bool'a geri düşer.
    public SpektrumVeriKaynagi DarbeSpektrumKaynagi { get; set; } = SpektrumVeriKaynagi.Belirsiz;

    /// Kütüphane düzenleyicisindeki hava ve darbe spektrumu kaynak seçimleri.
    /// Hesap verisi değildir; JSON kataloğuna yazılmaz.
    [JsonIgnore]
    public IReadOnlyList<SpektrumVeriKaynagiSecenegi> SpektrumKaynagiSecenekleri =>
        _spektrumKaynagiSecenekleri;

    [JsonIgnore]
    public string SpektrumKaynagiEtiketi => KaynakEtiketi(SpektrumKaynagi);

    [JsonIgnore]
    public string DarbeSpektrumKaynagiEtiketi => KaynakEtiketi(DarbeSpektrumKaynagi);

    // ── Rezonans Frekansları ─────────────────────────────────────────────────

    /// Kritik frekans (fc) — tek cidarlı elemanlarda çakışma dip noktası.
    /// fc = c₀² / (1.8 · h · cL)  [Hz]
    /// Çift cidarlı (hava boşluklu) elemanlarda null döner.
    public double? KritikFrekansHz
    {
        get
        {
            if (Katmanlar.Count == 0) return null;
            if (Katmanlar.Any(HavaBoşluguMu)) return null; // çift cidar → fc geçersiz
            var structural = Katmanlar.Where(k => !HavaBoşluguMu(k) && k.Yogunluk > 50).ToList();
            if (!structural.Any()) return null;
            double h = structural.Sum(k => k.Kalinlik) / 1000.0; // m
            if (h < 0.001) return null;
            double avgDensity = structural.Sum(k => k.Kalinlik * k.Yogunluk)
                              / structural.Sum(k => k.Kalinlik);
            double cL = LongitudinalHiz(avgDensity);
            return Math.Round(343.0 * 343.0 / (1.8 * h * cL));
        }
    }

    /// Kütlesel rezonans (f0) — çift cidarlı duvarda iki yaprak + hava boşluğu.
    /// f0 = (c₀/2π) · √(ρ_air · (1/m₁ + 1/m₂) / d)  [Hz]
    /// Hava boşluğu bulunmayan elemanlarda null döner.
    public double? KutleselRezonansHz
    {
        get
        {
            if (Katmanlar.Count == 0) return null;
            int airIdx = Katmanlar.FindIndex(HavaBoşluguMu);
            if (airIdx < 0) return null;
            double d = Katmanlar[airIdx].Kalinlik / 1000.0; // m
            if (d < 0.001) return null;
            double m1 = Katmanlar.Take(airIdx)
                                 .Where(k => !HavaBoşluguMu(k))
                                 .Sum(k => k.YuzeyKutlesi);
            double m2 = Katmanlar.Skip(airIdx + 1)
                                 .Where(k => !HavaBoşluguMu(k))
                                 .Sum(k => k.YuzeyKutlesi);
            if (m1 <= 0 || m2 <= 0) return null;
            // f0 = (c₀ / 2π) · √(ρ_air · (1/m₁ + 1/m₂) / d)
            const double c0 = 343.0, rhoAir = 1.21;
            double f0 = (c0 / (2 * Math.PI)) * Math.Sqrt(rhoAir * (1.0 / m1 + 1.0 / m2) / d);
            return Math.Round(f0);
        }
    }

    /// Yüzer döşeme rezonansı (fr) — screed + elastik katman kütlesel-yay sistemi.
    /// fr = (1/2π) · √(s' · 10⁶ / m'')  [Hz]
    /// DinamikRijitlikMNm3 > 0 olan Doseme tipinde hesaplanır.
    public double? YuzerDosemeRezonansHz
    {
        get
        {
            if (Tip != YapiElemaniTipi.Doseme) return null;
            if (DinamikRijitlikMNm3 <= 0) return null;
            double mpp = Katmanlar.Any()
                ? Katmanlar.Where(k => !HavaBoşluguMu(k) && k.Yogunluk > 100).Sum(k => k.YuzeyKutlesi)
                : ToplamYuzeyKutlesi;
            if (mpp <= 0) mpp = ToplamYuzeyKutlesi;
            if (mpp <= 0) return null;
            double sPrime = DinamikRijitlikMNm3 * 1e6; // N/m³
            double fr = (1.0 / (2 * Math.PI)) * Math.Sqrt(sPrime / mpp);
            return Math.Round(fr);
        }
    }

    /// Elemana uygun rezonans açıklaması (tek satır, sonuç tablosunda gösterilir).
    public string RezonansAciklama
    {
        get
        {
            if (YuzerDosemeRezonansHz is { } fr) return $"fr={fr:F0} Hz";
            if (KutleselRezonansHz is { } f0)    return $"f₀={f0:F0} Hz";
            if (KritikFrekansHz is { } fc)        return $"fc={fc:F0} Hz";
            return "";
        }
    }

    public string Ozet => $"{Tip} | {ToplamYuzeyKutlesi:F0} kg/m²";
    public string RwEtiketi => Tip == YapiElemaniTipi.Doseme ? $"Ln,w {Lnw:F0}" : $"Rw {Rw:F0}";

    public override string ToString() => Ad;

    // ── Yardımcı ─────────────────────────────────────────────────────────────

    private static string KaynakEtiketi(SpektrumVeriKaynagi kaynak) => kaynak switch
    {
        SpektrumVeriKaynagi.Olcum => "Ölçüm",
        SpektrumVeriKaynagi.Uretici => "Üretici / laboratuvar",
        SpektrumVeriKaynagi.Model => "Hesap modeli",
        SpektrumVeriKaynagi.Varsayilan => "Varsayılan spektrum",
        _ => "Belirsiz"
    };

    private static bool HavaBoşluguMu(YapiKatmani k)
    {
        if (k.Yogunluk < 5) return true;
        if (string.IsNullOrWhiteSpace(k.MalzemeAdi)) return false;
        var ad = k.MalzemeAdi.ToLowerInvariant().Trim();
        return ad == "hava" || ad == "hava boşluğu" || ad == "air" || ad == "air gap" || ad == "air cavity";
    }

    /// Ortalama yoğunluktan yaklaşık boyuna ses hızı (m/s).
    private static double LongitudinalHiz(double density)
    {
        if (density > 2000) return 3800; // beton, taş
        if (density > 1200) return 2800; // tuğla, yoğun harç
        if (density > 700)  return 2100; // alçı, alçıpan, hafif YTong
        if (density > 400)  return 3200; // ahşap
        return 2000;
    }
}
