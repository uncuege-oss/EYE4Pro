using System.Text.Json.Serialization;
using Kst.Acoustics.Core.Geometry;

namespace Kst.Acoustics.Core.Projects;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PanelTipi 
{ 
    Tavan, 
    DuvarSol, 
    DuvarSag 
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PanelEniModu
{
    TekTip,
    Sirali
}

public class AcousticPanel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public PanelTipi Tip { get; set; }
    public int DilimNo { get; set; }
    public double PanelEniM { get; set; }
    public double SarkmaCikmaM { get; set; }
    public double AciDerece { get; set; }
    public string? MaterialId { get; set; }
    public List<Point3D> Vertices { get; set; } = new();
}

public class AcousticPanelKonfigurasyon
{
    public Guid OturumId { get; set; } = Guid.NewGuid();
    public string? SahneOnuYuzeyId { get; set; }
    public double PanelEniM { get; set; }
    public PanelEniModu PanelEniModu { get; set; } = PanelEniModu.TekTip;
    public List<double> PanelEnleriM { get; set; } = new();
    public double SarkmaCikmaM { get; set; }
    public PanelTipi[] AktifTipler { get; set; } = Array.Empty<PanelTipi>();
    public List<AcousticPanel> Paneller { get; set; } = new();

    // Hedef yüzey ID/isim override'ları. null/boş = otomatik tespit.
    public string? TavanYuzeyId { get; set; }
    public string? SolDuvarYuzeyId { get; set; }
    public string? SagDuvarYuzeyId { get; set; }
}
