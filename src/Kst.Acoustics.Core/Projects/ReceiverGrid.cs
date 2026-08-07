namespace Kst.Acoustics.Core.Projects;

/// <summary>Yatay bir düzlemde ızgara alıcı haritası tanımı.</summary>
public sealed record ReceiverGrid(
    double PlaneZ,    // yükseklik (proje birimi)
    double XMin,
    double XMax,
    double YMin,
    double YMax,
    double CellSize)  // ızgara adımı (proje birimi)
{
    public int Cols => (int)Math.Max(1, Math.Round((XMax - XMin) / CellSize) + 1);
    public int Rows => (int)Math.Max(1, Math.Round((YMax - YMin) / CellSize) + 1);
    public int TotalCells => Cols * Rows;
}
