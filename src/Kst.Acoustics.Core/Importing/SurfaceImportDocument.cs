namespace Kst.Acoustics.Core.Importing;

public sealed record SurfaceImportDocument(
    SurfaceImportUnit Unit,
    IReadOnlyList<SurfaceDefinition> Surfaces,
    bool UnitDetected = true,
    int InsUnitsCode = 6,
    IReadOnlyList<string>? Warnings = null);
