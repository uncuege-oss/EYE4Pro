using System.IO;
using System.Threading.Tasks;
using Kst.Acoustics.Core.Importing;

namespace Kst.Acoustics.Core.Measurement.Spatial;

public static class DxfRoomLoader
{
    public static async Task<RoomGeometry> LoadAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
            throw new FileNotFoundException("DXF dosyası bulunamadı.", path);

        var text = await File.ReadAllTextAsync(path);
        var parser = new DxfSurfaceParser();
        if (!parser.TryParse(text, out var document) || document is null)
            throw new InvalidDataException("DXF dosyası okunamadı veya geometri bulunamadı.");

        return RoomGeometry.FromSurfaces(document, Path.GetFileName(path));
    }
}
