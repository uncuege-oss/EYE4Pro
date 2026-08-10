using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
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

    private double _alanM2;
    public double AlanM2
    {
        get => _alanM2;
        set => Set(ref _alanM2, value);
    }

    /// Oda bazlı yükseklik override (m). null = kat yüksekliği kullanılır.
    /// M08 asma kat / düşey birleşim senaryoları için gerekli.
    private double? _yukseklikOverrideM;
    public double? YukseklikOverrideM
    {
        get => _yukseklikOverrideM;
        set => Set(ref _yukseklikOverrideM, value);
    }

    private List<Point2D> _poligon = new();
    public List<Point2D> Poligon
    {
        get => _poligon;
        set
        {
            value ??= new List<Point2D>();
            if (ReferenceEquals(_poligon, value)) return;
            _poligon = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Poligon)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Merkez)));
        }
    }

    public Point2D Merkez
    {
        get => PoligonIcNoktaBulucu.Bul(Poligon);
    }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? EkAlanlar { get; set; }

    public override string ToString() => $"{Ad} ({AlanM2:N1} m²)";
}

/// <summary>
/// Basit ve geçerli bir poligon için poligonun içinde kalması garanti edilen
/// temsil noktasını bulur. Alan centroid'i içbükey poligonlarda boşluğa düşebildiği
/// için yatay tarama aralıklarına düşülür. Bu nokta oda kimlik eşleştirme ve
/// düşey birleşim gibi containment kararlarında kullanılır.
/// </summary>
internal static class PoligonIcNoktaBulucu
{
    public static Point2D Bul(IReadOnlyList<Point2D>? poligon)
    {
        if (poligon is null || poligon.Count == 0) return new Point2D();
        if (poligon.Count < 3) return KoseOrtalamasi(poligon);

        var centroid = AlanCentroidi(poligon);
        if (NoktaIcerideVeyaSinirda(centroid, poligon)) return centroid;

        // Ardışık benzersiz Y seviyelerinin ortasında tarama yapmak, tarama
        // doğrusunun bir köşeden geçmesiyle oluşan tek/tekrarlı kesişimleri önler.
        var ys = poligon
            .Select(p => p.Y)
            .Where(double.IsFinite)
            .Distinct()
            .OrderBy(y => y)
            .ToList();

        Point2D? enIyi = null;
        double enGenisAralik = -1;
        for (int yi = 0; yi + 1 < ys.Count; yi++)
        {
            double y0 = ys[yi], y1 = ys[yi + 1];
            if (y1 - y0 <= 1e-10) continue;
            double y = (y0 + y1) * 0.5;

            var kesimler = new List<double>();
            for (int i = 0, j = poligon.Count - 1; i < poligon.Count; j = i++)
            {
                var a = poligon[j];
                var b = poligon[i];
                if (!double.IsFinite(a.X) || !double.IsFinite(a.Y) ||
                    !double.IsFinite(b.X) || !double.IsFinite(b.Y))
                    continue;
                if ((a.Y > y) == (b.Y > y)) continue;
                double x = a.X + (y - a.Y) * (b.X - a.X) / (b.Y - a.Y);
                if (double.IsFinite(x)) kesimler.Add(x);
            }

            kesimler.Sort();
            for (int i = 0; i + 1 < kesimler.Count; i += 2)
            {
                double genislik = kesimler[i + 1] - kesimler[i];
                if (genislik <= enGenisAralik || genislik <= 1e-10) continue;
                var aday = new Point2D((kesimler[i] + kesimler[i + 1]) * 0.5, y);
                if (!NoktaIcerideVeyaSinirda(aday, poligon)) continue;
                enGenisAralik = genislik;
                enIyi = aday;
            }
        }

        return enIyi ?? KoseOrtalamasi(poligon);
    }

    private static Point2D AlanCentroidi(IReadOnlyList<Point2D> poligon)
    {
        double a2 = 0, cx = 0, cy = 0;
        for (int i = 0; i < poligon.Count; i++)
        {
            var a = poligon[i];
            var b = poligon[(i + 1) % poligon.Count];
            double cross = a.X * b.Y - b.X * a.Y;
            a2 += cross;
            cx += (a.X + b.X) * cross;
            cy += (a.Y + b.Y) * cross;
        }

        if (!double.IsFinite(a2) || Math.Abs(a2) < 1e-12)
            return KoseOrtalamasi(poligon);
        var sonuc = new Point2D(cx / (3.0 * a2), cy / (3.0 * a2));
        return double.IsFinite(sonuc.X) && double.IsFinite(sonuc.Y)
            ? sonuc
            : KoseOrtalamasi(poligon);
    }

    private static Point2D KoseOrtalamasi(IReadOnlyList<Point2D> poligon)
    {
        double sx = 0, sy = 0;
        int sayi = 0;
        foreach (var p in poligon)
        {
            if (!double.IsFinite(p.X) || !double.IsFinite(p.Y)) continue;
            sx += p.X;
            sy += p.Y;
            sayi++;
        }
        return sayi == 0 ? new Point2D() : new Point2D(sx / sayi, sy / sayi);
    }

    private static bool NoktaIcerideVeyaSinirda(Point2D p, IReadOnlyList<Point2D> poligon)
    {
        if (!double.IsFinite(p.X) || !double.IsFinite(p.Y)) return false;
        if (PoligonYardimci.IsPointInside(p, poligon)) return true;
        for (int i = 0; i < poligon.Count; i++)
            if (PoligonYardimci.SegmentMesafesi(p, poligon[i], poligon[(i + 1) % poligon.Count]) <= 1e-8)
                return true;
        return false;
    }
}
