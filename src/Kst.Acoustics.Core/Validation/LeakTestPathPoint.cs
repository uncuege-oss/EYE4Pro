using Kst.Acoustics.Core.Geometry;

namespace Kst.Acoustics.Core.Validation;

public sealed record LeakTestPathPoint(
    int BounceIndex,
    Point3D Position,
    int? SurfaceId);
