using Kst.Acoustics.Core.Geometry;

namespace Kst.Acoustics.Core.Importing;

public sealed record SurfaceDefinition(
    int Id,
    string Name,
    string Category,
    IReadOnlyList<Point3D> Vertices);
