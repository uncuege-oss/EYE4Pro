using System.Threading.Tasks;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.App.ViewModels;

public partial class MainWindowViewModel
{
    public void ClearLeakTest()
    {
        LeakTestPath = [];
        LeakTestSummary = "Sızdırmazlık testi henüz çalıştırılmadı.";
        StatusMessage = "Işın yolu temizlendi.";
    }

    public async Task RunLeakTestAsync()
    {
        if (HasInvalidPoints)
        {
            LeakTestSummary = PointValidationMessage;
            StatusMessage = PointValidationMessage;
            return;
        }

        var project = BuildProjectSnapshot();
        if (project.Surfaces.Count == 0)
        {
            LeakTestSummary = "Test için yüzey bulunamadı.";
            StatusMessage = LeakTestSummary;
            return;
        }

        StatusMessage = "Sızdırmazlık testi çalışıyor...";
        var result = await _leakTestService.RunAsync(project);
        LeakTestSummary = result.Message;
        LeakTestPath = result.Path.Select(x => x.Position).ToArray();
        StatusMessage = result.Message;
    }

    public void ConvertProjectUnits()
    {
        if (_currentProject is null || ProjectSurfaces.Count == 0)
        {
            StatusMessage = "Birim dönüşümü için önce bir proje yükleyin.";
            return;
        }

        if (HasInvalidPoints)
        {
            StatusMessage = PointValidationMessage;
            return;
        }

        if (TargetUnitOption is null)
        {
            StatusMessage = "Hedef birim seçilmedi.";
            return;
        }

        var currentUnit = _currentProject.Unit;
        var targetUnit = TargetUnitOption.Value;
        if (currentUnit == targetUnit)
        {
            StatusMessage = "Proje zaten seçili birimde.";
            return;
        }

        try
        {
            var factor = ProjectScalingService.GetConversionFactor(currentUnit, targetUnit);
            var scaledSurfaces = ProjectScalingService.ConvertUnits(ProjectSurfaces, currentUnit, targetUnit);
            if (!ApplyTransformedSurfaces(scaledSurfaces, targetUnit, factor, scalePhysicalVolume: false))
                return;

            MarkDirty();
            StatusMessage = $"Proje birimi {FormatUnitName(currentUnit)} biriminden {FormatUnitName(targetUnit)} birimine dönüştürüldü.";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or OverflowException)
        {
            StatusMessage = $"Birim dönüşümü uygulanamadı; proje değiştirilmedi. {ex.Message}";
        }
    }

    public void ApplyCustomScale()
    {
        if (_currentProject is null || ProjectSurfaces.Count == 0)
        {
            StatusMessage = "Ölçek uygulamak için önce bir proje yükleyin.";
            return;
        }

        if (HasInvalidPoints)
        {
            StatusMessage = PointValidationMessage;
            return;
        }

        if (!double.TryParse(CustomScaleFactor, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var factor) ||
            !ProjectScalingService.IsSupportedScaleFactor(factor))
        {
            StatusMessage = "Ölçek katsayısı 1e-12 ile 1e12 arasında sonlu ve pozitif bir sayı olmalı.";
            return;
        }

        try
        {
            var scaledSurfaces = ProjectScalingService.ScaleSurfaces(ProjectSurfaces, factor);
            if (!ApplyTransformedSurfaces(scaledSurfaces, _currentProject.Unit, factor, scalePhysicalVolume: true))
                return;

            MarkDirty();
            StatusMessage = $"Projeye {factor.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)} katsayısı uygulandı.";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or OverflowException)
        {
            StatusMessage = $"Ölçek uygulanamadı; proje değiştirilmedi. {ex.Message}";
        }
    }
}
