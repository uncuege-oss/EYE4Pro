using Kst.Acoustics.Core.Common.Geometry;

namespace Kst.Acoustics.Core.Salon.Models;

public enum SalonIslevi { Eski, Sinir, Sahne, Seyirci, Balkon, Cukur, Bosluk, Yardimci, Tavan, Koridor, Duvar }
public enum SalonZeminBicimi { Duz, Basamak, Rampa }

public partial class SalonCizimOgesi : System.ComponentModel.INotifyPropertyChanged
{
    public SalonIslevi Islev { get; set; }
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    private string _ad = "";
    public string Ad { get => _ad; set { if (_ad == value) return; _ad = value; PropertyChanged?.Invoke(this, new(nameof(Ad))); } }
    public double KotM { get; set; }
    public double YukseklikM { get; set; } = 1;
    public double KalinlikM { get; set; } = .3;
    public double ParapetM { get; set; }
    public double YonDerece { get; set; } = 90;
    public SalonZeminBicimi ZeminBicimi { get; set; } = SalonZeminBicimi.Basamak;
    public int KademeSayisi { get; set; } = 6;
    public double KademeYuksekligiM { get; set; } = .15;
    public double SiraAraligiM { get; set; } = .9;
    public double KoltukGenisligiM { get; set; } = .55;
    public bool YayliSiralar { get; set; }
    public Point2D SiraMerkezi { get; set; }
    public string? UstNesneId { get; set; }
    public bool CiktiyaDahil { get; set; } = true;
    public double EgimX { get; set; }
    public double EgimY { get; set; }
    public Point2D KotReferansi { get; set; }
    public string? KesitGrubu { get; set; }
    // Bulge = tan(signed sweep / 4). Curves remain exact in the document.
    public List<List<Point2D>> IcBosluklar { get; set; } = new();
    public List<double> KenarKavisleri { get; set; } = new();
    public List<string> KenarKimlikleri { get; set; } = new();

    public void OrtakSiniraDonustur()
    {
        if (Tip == SalonCizimTipi.Daire && Noktalar.Count > 0)
        {
            var c = Noktalar[0]; double r = YariCapM;
            Noktalar = new() { new(c.X + r, c.Y), new(c.X, c.Y + r), new(c.X - r, c.Y), new(c.X, c.Y - r) };
            KenarKavisleri = Enumerable.Repeat(Math.Tan(Math.PI / 8), 4).ToList(); KenarKimlikleri.Clear(); Tip = SalonCizimTipi.Polyline; Kapali = true;
        }
        if (Tip == SalonCizimTipi.Dikdortgen) { Tip = SalonCizimTipi.Polyline; Kapali = true; }
        KenarlariHazirla();
    }

    public void KenarlariHazirla()
    {
        while (KenarKavisleri.Count < Noktalar.Count) KenarKavisleri.Add(0);
        while (KenarKimlikleri.Count < Noktalar.Count) KenarKimlikleri.Add(Guid.NewGuid().ToString("N"));
    }

    public Point2D KenarNoktasi(int i, double t)
    {
        var a = Noktalar[i]; var b = Noktalar[(i + 1) % Noktalar.Count];
        double bulge = i < KenarKavisleri.Count ? KenarKavisleri[i] : 0;
        if (Math.Abs(bulge) < 1e-9) return new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double cx = (a.X + b.X) / 2 - dy * (1 - bulge * bulge) / (4 * bulge);
        double cy = (a.Y + b.Y) / 2 + dx * (1 - bulge * bulge) / (4 * bulge);
        double r = Math.Sqrt((a.X - cx) * (a.X - cx) + (a.Y - cy) * (a.Y - cy));
        double angle = Math.Atan2(a.Y - cy, a.X - cx) + 4 * Math.Atan(bulge) * t;
        return new(cx + r * Math.Cos(angle), cy + r * Math.Sin(angle));
    }

    public List<Point2D> SinirNoktalari(int cozunurluk = 48)
    {
        var result = new List<Point2D>();
        int edges = Kapali ? Noktalar.Count : Noktalar.Count - 1;
        for (int i = 0; i < edges; i++)
        {
            double b = i < KenarKavisleri.Count ? KenarKavisleri[i] : 0;
            int n = Math.Max(1, (int)Math.Ceiling(Math.Abs(4 * Math.Atan(b)) * cozunurluk / (2 * Math.PI)));
            for (int j = 0; j < n; j++) result.Add(KenarNoktasi(i, (double)j / n));
        }
        if (!Kapali && Noktalar.Count > 0) result.Add(Noktalar[^1]);
        return result;
    }
    public override string ToString() => string.IsNullOrWhiteSpace(Ad) ? Islev.ToString() : Ad;
}
