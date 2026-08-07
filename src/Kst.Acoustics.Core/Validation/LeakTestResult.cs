using Kst.Acoustics.Core.Geometry;

namespace Kst.Acoustics.Core.Validation;

public sealed record LeakTestResult(
    bool Passed,
    string Message,
    int BounceCount,
    Point3D StartPoint,
    Vector3D StartDirection,
    int? EscapedAfterSurfaceId,
    IReadOnlyList<LeakTestPathPoint> Path);
