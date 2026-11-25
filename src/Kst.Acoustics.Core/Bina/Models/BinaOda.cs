using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Bina.Models;

public class BinaOda : IBinaOda, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? n = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public string Id { get; set; } = Guid.NewGuid().ToString();

    private string _ad = "Oda";
    public string Ad
    {
        get => _ad;
        set => Set(ref _ad, value);
    }

    public double AlanM2 { get; set; }

    /// Oda bazlı yükseklik override (m). null = kat yüksekliği kullanılır.
    /// M08 asma kat / düşey birleşim senaryoları için gerekli.
    public double? YukseklikOverrideM { get; set; }

    /// Kat içindeki kot farkları için mutlak proje kotu (m). null = kat tabanı.
    public double? TabanKotuOverrideM { get; set; }

    /// Aynı bağlı hacimde kalan kaynak mahal adları. Ayrı etiket, ayrı duvar kanıtı değildir.
    public List<string> KaynakMahalAdlari { get; set; } = new();
    [JsonIgnore]
    public string KaynakMahalOzeti => string.Join(" · ", KaynakMahalAdlari);
    IReadOnlyList<string> IBinaOda.KaynakMahalAdlari => KaynakMahalAdlari;

    public List<Point2D> Poligon { get; set; } = new();

    public Point2D Merkez
    {
        get
        {
            if (Poligon.Count == 0) return new Point2D();
            return new Point2D(Poligon.Average(p => p.X), Poligon.Average(p => p.Y));
        }
    }

    public override string ToString() => $"{Ad} ({AlanM2:N1} m²)";
}
