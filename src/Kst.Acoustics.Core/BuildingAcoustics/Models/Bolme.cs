using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.BuildingAcoustics.Models;

public class Bolme : INotifyPropertyChanged
{
    private YapiElemani? _yapiElemani;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Ad { get; set; } = "";
    /// M4 — Bölmenin toplam (brüt) yüzey alanı (m²): kenar uzunluğu × kat yüksekliği.
    /// Pencere/kapı açıklıkları DAHİL; CompositeRw bu alanı opak + açıklık olarak ayırır.
    /// OdaTespitServisi.DetectSharedPartitions atar.
    public double Alan { get; set; }
    public Point2D Baslangic { get; set; }
    public Point2D Bitis { get; set; }
    public bool HasGeometry { get; set; }
    public List<BolmeAcikligi> Acikliklar { get; set; } = new();
    public List<Menfez> Menfezler { get; set; } = new();
    public List<Junction> Junctions { get; set; } = new();
    public double AciklikAlani => Acikliklar.Sum(a => Math.Max(0, a.Alan));
    public double NetOpakAlan => Math.Max(0, Alan - AciklikAlani);
    
    // Bağlantılı Yapı Elemanı
    public YapiElemani? YapiElemani
    {
        get => _yapiElemani;
        set
        {
            if (ReferenceEquals(_yapiElemani, value))
                return;

            _yapiElemani = value;
            OnPropertyChanged();
        }
    }
    
    // Komşuluk İlişkisi
    public Mekan? KaynakMekan { get; set; }
    public Mekan? AliciMekan { get; set; } // Null ise dış cephedir
    
    public bool IsDisCephe => AliciMekan == null;
    public BolmeKomsulukTipi KomsulukTipi
    {
        get
        {
            if (IsDisCephe) return BolmeKomsulukTipi.DisCephe;
            if (KaynakMekan?.BagimsizBolumId is not { } kaynakId ||
                AliciMekan?.BagimsizBolumId is not { } aliciId)
                return BolmeKomsulukTipi.Bilinmiyor;
            return kaynakId == aliciId
                ? BolmeKomsulukTipi.AyniBagimsizBolumIci
                : BolmeKomsulukTipi.BagimsizBolumlerArasi;
        }
    }

    public string KomsulukTipiEtiketi => KomsulukTipi switch
    {
        BolmeKomsulukTipi.AyniBagimsizBolumIci => "Aynı bağımsız bölüm içi",
        BolmeKomsulukTipi.BagimsizBolumlerArasi => "Bağımsız bölümler arası",
        BolmeKomsulukTipi.DisCephe => "Dış cephe",
        _ => "Bağımsız bölüm atanmamış",
    };
    
    // Hesaplama Sonuçları — tek sayı
    public double ApparentRw { get; set; }   // R'w (dB)
    public double ApparentLnw { get; set; }  // L'n,w (dB)
    public double ApparentLnTw { get; set; } // L'nT,w (dB)
    public double Dntw { get; set; }          // DnT,w (dB)

    // Hesaplama Sonuçları — oktav bant (serileştirilmez; her hesapta yeniden üretilir)
    [JsonIgnore] public OktavBantSonucu? OktavHavaSesiSonucu { get; set; }
    [JsonIgnore] public OktavDarbeSonucu? OktavDarbeSesiSonucu { get; set; }
    [JsonIgnore] public UcuncuOktavHavaSonucu? UcuncuOktavHavaSesiSonucu { get; set; }
    [JsonIgnore] public DisCepheSpektralSonuc? DisCepheSpektralSonuc { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public enum BolmeKomsulukTipi
{
    Bilinmiyor,
    AyniBagimsizBolumIci,
    BagimsizBolumlerArasi,
    DisCephe
}

public enum BolmeAcikligiTipi
{
    Pencere,
    Kapi
}

public class BolmeAcikligi
{
    public BolmeAcikligiTipi Tip { get; set; }
    public YapiElemani? YapiElemani { get; set; }

    /// 4.2 — M50 kaynağı: bağlı BinaAciklik.Id. Dolu ise boyutlar (Genislik/
    /// Yukseklik/Esik) M50 geometrisinden gelir ve M08'de SALT-OKUNUR'dur;
    /// M08 yalnız AciklikId → YapiElemani eşlemesini sahiplenir.
    /// null = M08'de elle eklenmiş açıklık (boyutlar M08'de düzenlenebilir).
    public string? AciklikId { get; set; }

    /// Boyutları M50 geometrisi yönetiyor mu (UI salt-okunur kontrolü).
    [System.Text.Json.Serialization.JsonIgnore]
    public bool GeometridenGelir => !string.IsNullOrEmpty(AciklikId);

    public Point2D Baslangic { get; set; }
    public Point2D Bitis { get; set; }
    public double Genislik { get; set; }
    public double Yukseklik { get; set; }
    /// B4 — Eşik yüksekliği (m): açıklığın alt kenarının döşemeden yüksekliği.
    /// 0 ise tip varsayılanı (Kapı: 0, Pencere: 0.9 m) kullanılır.
    public double EsikYuksekligiM { get; set; } = 0.0;
    public double Alan => Genislik * Yukseklik;

    /// B4 — Etkin eşik yüksekliği: kullanıcı tanımlı varsa onu, yoksa tip varsayılanını döner.
    public double EtkinEsikYuksekligiM => EsikYuksekligiM > 0
        ? EsikYuksekligiM
        : (Tip == BolmeAcikligiTipi.Kapi ? 0.0 : 0.9);
}

public class AciklikOnayar
{
    public string Ad { get; set; } = "";
    public BolmeAcikligiTipi Tip { get; set; }
    public double GenislikCm { get; set; }
    public double YukseklikCm { get; set; }
    public string Ozet => $"{Tip} | {GenislikCm:F0}×{YukseklikCm:F0} cm";
}
