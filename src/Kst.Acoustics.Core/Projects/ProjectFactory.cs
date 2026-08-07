using Kst.Acoustics.Core.Importing;

namespace Kst.Acoustics.Core.Projects;

public static class ProjectFactory
{
    public static AcousticsProject CreateEmpty(string projectName, RoomType selectedRoomType = RoomType.General)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);

        return new AcousticsProject(
            projectName.Trim(),
            null,
            DateTime.UtcNow,
            SurfaceImportFormat.Dxf,
            SurfaceImportUnit.Meters,
            [],
            SelectedRoomType: selectedRoomType,
            RoomVolume: 0.0,
            VolumeOrigin: null)
        {
            SemaSurum = AcousticsProjectSchema.CurrentVersion
        };
    }

    public static AcousticsProject CreateFromImport(string projectName, string? sourceFilePath, SurfaceImportResult result, RoomType selectedRoomType = RoomType.General)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);

        var document = result.Format == SurfaceImportFormat.Dxf && result.Document.UnitDetected
            ? DxfSurfaceParser.FinalizeUnit(result.Document, result.Document.Unit)
            : result.Document;

        var surfaces = document.Surfaces
            .Select(surface => new ProjectSurface(
                surface.Id,
                surface.Name,
                surface.Category,
                surface.Vertices.ToArray()))
            .ToArray();

        var vertices = surfaces.SelectMany(s => s.Vertices).ToArray();
        if (surfaces.Length == 0 || vertices.Length == 0)
        {
            throw new InvalidDataException("İçe aktarılan modelde desteklenen kapalı yüzey bulunamadı.");
        }

        var validVertices = vertices.Where(v =>
            double.IsFinite(v.X) && double.IsFinite(v.Y) && double.IsFinite(v.Z)).ToArray();
        if (validVertices.Length == 0)
        {
            throw new InvalidDataException("İçe aktarılan modelde desteklenen geçerli yüzey noktası bulunamadı.");
        }
        if (validVertices.Length != vertices.Length)
        {
            throw new InvalidDataException(
                "İçe aktarılan model NaN/Infinity içeren geçersiz yüzey noktaları barındırıyor.");
        }

        // Kabuk kapalıysa (watertight) gerçek imzalı hacim kullanılır; değilse eksen
        // hizalı bounding box'a düşülür (L plan, eğimli tavan, boşluklu geometrilerde
        // bounding box RT60/Schroeder hesabını yanlış etkiler — bkz. maincodex M01 P0/P1).
        var volume = RoomVolumeCalculator.Estimate(surfaces, document.Unit);

        return new AcousticsProject(
            projectName.Trim(),
            sourceFilePath,
            DateTime.UtcNow,
            result.Format,
            document.Unit,
            surfaces,
            SelectedRoomType: selectedRoomType,
            RoomVolume: volume.VolumeM3,
            VolumeOrigin: volume.Origin,
            ImportWarnings: document.Warnings)
        {
            SemaSurum = AcousticsProjectSchema.CurrentVersion
        };
    }
}
