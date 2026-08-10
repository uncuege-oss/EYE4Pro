using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.App.ViewModels;

public sealed partial class ProjectWindowViewModel
{
    private readonly ProjectPackageService _projectPackageService = new();

    public bool IsCreatingProjectPackage
    {
        get => _isCreatingProjectPackage;
        private set => SetProperty(ref _isCreatingProjectPackage, value);
    }
    private bool _isCreatingProjectPackage;

    public string PackageStatusText
    {
        get => _packageStatusText;
        private set => SetProperty(ref _packageStatusText, value);
    }
    private string _packageStatusText = "Proje, kullanılan kütüphaneler ve kaynak geometri tek pakette toplanabilir.";

    public async Task<bool> CreateProjectPackageAsync(string path)
    {
        if (IsCreatingProjectPackage)
        {
            StatusMessage = "Bir proje paketi zaten oluşturuluyor.";
            return false;
        }

        IsCreatingProjectPackage = true;
        PackageStatusText = "Paket hazırlanıyor ve bütünlüğü doğrulanıyor...";
        StatusMessage = PackageStatusText;
        try
        {
            if (HasInvalidPoints)
                throw new InvalidDataException(PointValidationMessage);

            var result = await _projectPackageService.CreateAsync(
                path,
                BuildCurrentProject(),
                MaterialDatabase.Instance.All,
                LoudspeakerDatabase.Instance.All);
            PackageStatusText =
                $"Paket hazır: {Path.GetFileName(result.PackagePath)} · " +
                $"{result.IncludedMaterialCount} materyal · " +
                $"{result.IncludedLoudspeakerCount} hoparlör · " +
                $"kaynak geometri: {(result.SourceIncluded ? "eklendi" : "yok")}";
            StatusMessage = PackageStatusText;
            return true;
        }
        catch (Exception ex)
        {
            PackageStatusText = $"Paket oluşturulamadı: {ex.Message}";
            StatusMessage = PackageStatusText;
            return false;
        }
        finally
        {
            IsCreatingProjectPackage = false;
        }
    }
}
