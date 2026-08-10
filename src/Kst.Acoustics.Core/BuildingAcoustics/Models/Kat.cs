using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.BuildingAcoustics.Models;

public class Kat : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    private int _katNo;
    public int KatNo
    {
        get => _katNo;
        set { if (_katNo == value) return; _katNo = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// M50 <c>BinaKat.Id</c> değerinin M08 tarafındaki kararlı karşılığı.
    /// KatNo yalnız görüntü sırasıdır; katlar yeniden sıralandığında akustik
    /// atamaların başka kata kaymaması için eşleştirme bu kimlikle yapılır.
    /// Eski M08 belgelerinde boş olabilir ve ilk M50 içe aktarımında doldurulur.
    /// </summary>
    public string BinaGeometriKatId { get; set; } = "";

    private string _ad = "";
    public string Ad                              // kullanıcı tanımlı kat adı
    {
        get => _ad;
        set { if (_ad == value) return; _ad = value; OnPropertyChanged(); }
    }

    public double KatYuksekligi { get; set; } // m
    public string DxfFilePath { get; set; } = "";
    public bool DxfGoster { get; set; } = true;
    public bool BloklariGoster { get; set; } = true;

    /// B5 — DXF koordinat orijinini bina koordinat sistemine taşıyan ofset.
    /// Farklı katlardaki DXF'ler aynı orijinden çizilmemişse bu değer hizalamayı sağlar.
    public Point2D OriginOffset { get; set; } = new(0, 0);

    public ObservableCollection<Mekan> Mekanlar { get; set; } = new();

    /// DXF'ten okunan ham çizgi segmentleri (LINE + LWPOLYLINE kenarları).
    /// Viewport'ta odaların altında referans katmanı olarak çizilir; kullanıcının
    /// otomatik üretilen oda dikdörtgenlerini gerçek duvar konumlarına oturtmasını sağlar.
    [JsonIgnore]
    public List<(Point2D A, Point2D B)> RawDxfSegments { get; set; } = new();

    /// JSON uyumlu DXF arka plan segmentleri. ValueTuple JSON'da güvenilir
    /// dönmediği için RawDxfSegments bu DTO üzerinden saklanır.
    public List<DxfSegmentDto> RawDxfSegmentData
    {
        get => RawDxfSegments
            .Select(s => new DxfSegmentDto { A = s.A, B = s.B })
            .ToList();
        set => RawDxfSegments = value?
            .Select(s => (s.A, s.B))
            .ToList() ?? new();
    }

    /// DXF'ten okunan tüm TEXT/MTEXT etiketleri (konum + değer), RawDxfSegments ile
    /// AYNI koordinat çerçevesinde (ölçek + origin normalizasyonu uygulanmış). Oda
    /// poligonu içine düşen metni odaya isim olarak atamak için kullanılır.
    [JsonIgnore]
    public List<(Point2D Pos, string Value)> RawDxfTexts { get; set; } = new();

    /// DXF'teki ARC entity'leri (merkez, R, açılar — derece). Kapı yayı tespiti için.
    /// RawDxfSegments ile aynı koordinat çerçevesinde.
    [JsonIgnore]
    public List<(Point2D Merkez, double R, double BasDeg, double BitDeg)> RawDxfArcs { get; set; } = new();
}

public class DxfSegmentDto
{
    public Point2D A { get; set; }
    public Point2D B { get; set; }
}
