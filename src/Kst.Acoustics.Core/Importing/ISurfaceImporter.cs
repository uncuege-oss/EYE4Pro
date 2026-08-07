namespace Kst.Acoustics.Core.Importing;

public interface ISurfaceImporter
{
    SurfaceImportFormat Format { get; }

    bool TryParse(string text, out SurfaceImportDocument? document);
}
