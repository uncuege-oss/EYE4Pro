using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.Tests;

/// <summary>
/// Kaydetme sırasında dosya sistemi hatası oluşursa (ör. hedef bir dizinle çakışıyor)
/// SaveAsync hata fırlatmalı ve yarım kalan .tmp dosyasını diskte bırakmamalı — aksi
/// halde tekrar kaydetme denemesinde eski .tmp artığı kafa karıştırır (maincodex M01 P1).
/// </summary>
public static class ProjectFileServiceErrorHandlingTests
{
    public static void SaveAsyncCleansUpTempFileWhenTargetPathIsADirectory()
    {
        // Hedef proje dosyası yolunun kendisi bir DİZİN olarak var — File.Move/Replace
        // bunun üzerine yazamaz, bu yüzden SaveAsync hata fırlatmalı.
        var targetPath = Path.Combine(Path.GetTempPath(), $"e4p01-savefail-{Guid.NewGuid():N}.e4p01");
        Directory.CreateDirectory(targetPath);
        try
        {
            var service = new ProjectFileService();
            var project = BuildValidProject();

            // Platforma göre IOException ya da UnauthorizedAccessException fırlatılabilir
            // (macOS bir dizinin üzerine yazma girişimini erişim hatası olarak raporlar);
            // asıl doğrulanan şey hatanın fırlatılması ve .tmp'nin temizlenmesi.
            AssertThrows<Exception>(() => service.Save(targetPath, project),
                "Hedef bir dizinle çakışıyorsa kaydetme hata fırlatmalı.");

            AssertFalse(FindTemporaryFiles(targetPath).Length > 0,
                "Benzersiz .tmp dosyaları hata sonrası temizlenmeli, diskte kalmamalı.");
        }
        finally
        {
            if (Directory.Exists(targetPath)) Directory.Delete(targetPath, recursive: true);
            foreach (var temp in FindTemporaryFiles(targetPath)) File.Delete(temp);
        }
    }

    private static string[] FindTemporaryFiles(string targetPath)
    {
        var directory = Path.GetDirectoryName(targetPath) ?? Path.GetTempPath();
        return Directory.GetFiles(directory, $".{Path.GetFileName(targetPath)}.*.tmp");
    }

    private static AcousticsProject BuildValidProject() => new(
        "SaveErrorTest", null, DateTime.UtcNow,
        SurfaceImportFormat.Dxf, SurfaceImportUnit.Meters,
        [new ProjectSurface(1, "Zemin", "ZEMIN", [new(0,0,0), new(4,0,0), new(4,4,0), new(0,4,0)])],
        RoomVolume: 48.0);

    private static void AssertFalse(bool condition, string message)
    {
        if (condition) throw new InvalidOperationException(message);
    }

    private static void AssertThrows<TException>(Action action, string message) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }
}
