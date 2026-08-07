using Kst.Acoustics.Core.Geometry;

namespace Kst.Acoustics.Core.Projects;

public sealed record ProjectSurface(
    int Id,
    string Name,
    string Category,
    IReadOnlyList<Point3D> Vertices,
    string? OwnerId = null);
