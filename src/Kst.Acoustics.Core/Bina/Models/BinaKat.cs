using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Bina.Models;

public class BinaKat : IBinaKat, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? n = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public string Id { get; set; } = Guid.NewGuid().ToString();

    private string _ad = "";
    public string Ad
    {
        get => _ad;
        set => Set(ref _ad, value);
    }

    private int _katNo;
    public int KatNo
    {
        get => _katNo;
        set => Set(ref _katNo, value);
    }

    private double _katYuksekligiM = 3.0;
    public double KatYuksekligiM
    {
        get => _katYuksekligiM;
        set => Set(ref _katYuksekligiM, value);
    }

    public string DxfFilePath { get; set; } = "";
    public string? UnitWarning { get; set; }

    /// DXF içe aktarımında kesinleşen metre/çizim-birimi ölçeği. 0, eski bir
    /// kayıtta ölçek bilgisinin bulunmadığını gösterir.
    public double DxfBirimOlcegiM { get; set; }

    /// Dünya koordinatlarından katın yerel koordinatlarına geçerken çıkarılan
    /// metre cinsinden ofset. Kayıt yeniden açılıp ham DXF okunduğunda aynı
    /// dönüşümün uygulanmasını sağlar.
    public Point2D DxfNormalizeOfsetiM { get; set; } = new(0, 0);

    /// Tek DXF içinden seçilmiş çoklu-kat planının dünya-koordinatı sınırları.
    /// null ise tüm dosya bu kata aittir.
    public Point2D? DxfKaynakBolgesiMinM { get; set; }
    public Point2D? DxfKaynakBolgesiMaxM { get; set; }

    /// Farklı DXF orijinleri için manuel hizalama ofseti (m).
    public Point2D OriginOffset { get; set; } = new(0, 0);

    private bool _dxfGoster = true;
    private bool _bloklariGoster = true;

    public bool DxfGoster
    {
        get => _dxfGoster;
        set => Set(ref _dxfGoster, value);
    }

    public bool BloklariGoster
    {
        get => _bloklariGoster;
        set => Set(ref _bloklariGoster, value);
    }

    public ObservableCollection<BinaOda> Odalar { get; set; } = new();
    public List<BinaAciklik> Acikliklar { get; set; } = new();

    public int OdaSayisi => Odalar.Count;
    public int AciklikSayisi => Acikliklar.Count;

    public void SayilariYenile()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OdaSayisi)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AciklikSayisi)));
    }

    /// Arka plan referansı için sadeleştirilmiş DXF segmentleri (5 cm altı elenmiş).
    public List<BinaDxfSegment> DxfSegmentler { get; set; } = new();

    /// Kapı yayı tespiti için DXF ARC entity'leri.
    public List<BinaDxfArc> DxfArclar { get; set; } = new();

    /// Flood-fill + oda tespiti için tam ham segment seti. JSON'a yazılmaz;
    /// yalnız yükleme sırasında parser tarafından doldurulur.
    [JsonIgnore]
    public List<(Point2D A, Point2D B)> HamSegmentler { get; set; } = new();

    /// Oda tespiti ve çoklu-kat kümelemesi için annotation katmanları ayıklanmış
    /// tam segment seti. JSON'a yazılmaz; boşsa HamSegmentler kullanılır.
    [JsonIgnore]
    public List<(Point2D A, Point2D B)> TespitSegmentler { get; set; } = new();

    /// Ham TEXT/MTEXT etiketleri — oda ismi ataması için. JSON'a yazılmaz.
    [JsonIgnore]
    public List<(Point2D Pos, string Value)> HamMetinler { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? EkAlanlar { get; set; }

    // IBinaKat read-only görünümleri
    IReadOnlyList<IBinaOda> IBinaKat.Odalar => Odalar;
    IReadOnlyList<IBinaAciklik> IBinaKat.Acikliklar => Acikliklar;

    public override string ToString() => $"{Ad} ({KatYuksekligiM:N2} m)";
}
