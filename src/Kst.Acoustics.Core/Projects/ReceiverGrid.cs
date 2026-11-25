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
    public int Cols => checked((int)AxisPointCount(XMin, XMax, CellSize));
    public int Rows => checked((int)AxisPointCount(YMin, YMax, CellSize));
    public int TotalCells => checked(Cols * Rows);

    // Include an exact endpoint despite one ULP of division error, but never
    // round a partial cell up and place a receiver outside the requested area.
    public static double AxisPointCount(double min, double max, double step) =>
        Math.Floor(Math.BitIncrement((max - min) / step)) + 1;
}
