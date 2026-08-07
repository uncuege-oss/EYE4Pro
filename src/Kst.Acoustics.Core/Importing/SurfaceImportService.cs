namespace Kst.Acoustics.Core.Importing;

public sealed class SurfaceImportService
{
    private readonly IReadOnlyList<ISurfaceImporter> _importers;

    public SurfaceImportService()
        : this(
        [
            new DxfSurfaceParser()
        ])
    {
    }

    public SurfaceImportService(IEnumerable<ISurfaceImporter> importers)
    {
        ArgumentNullException.ThrowIfNull(importers);
        _importers = importers.ToArray();
    }

    public IReadOnlyList<ISurfaceImporter> Importers => _importers;

    public SurfaceImportResult Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        foreach (var importer in _importers)
        {
            if (importer.TryParse(text, out var document) && document is not null)
            {
                return new SurfaceImportResult(importer.Format, document);
            }
        }

        throw new InvalidOperationException("Sağlanan metin ile eşleşen veya ayrıştırabilen bir içe aktarıcı bulunamadı.");
    }

    public SurfaceImportResult ParseFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Parse(File.ReadAllText(path));
    }
}
