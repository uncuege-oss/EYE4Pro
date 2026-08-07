using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Simulation;

namespace Kst.Acoustics.Core.Projects;

public enum ProjectValidationSeverity
{
    Warning,
    Error
}

public readonly record struct ProjectValidationIssue(string Message, ProjectValidationSeverity Severity);

/// <summary>
/// Yüklenen bir .e4p01 dosyasının kullanılabilir olduğunu doğrular: yinelenen ID'ler,
/// üçten az köşeli (dejenere) yüzeyler, NaN/Infinity koordinatlar. Bunlar dosya bozulması,
/// elle düzenleme veya eski bir bug'dan kalma artıklar olabilir; sessizce kabul edilirse
/// hacim/RT60/ışın izleme hesaplarını NaN'a ya da yanlış sonuca sürükler.
/// </summary>
public static class AcousticsProjectValidator
{
    private const int MinimumRayCount = 100;
    private const int MaximumRayCount = 100_000;
    private const int MinimumReflectionOrder = 10;
    private const int MaximumReflectionOrder = 500;
    private const int MinimumTransitionOrder = 5;
    private const int MaximumTransitionOrder = 150;
    private const int MaximumGridCells = 4_000;
    private const double MinimumReceiverRadiusM = 0.01;
    private const double MaximumReceiverRadiusM = 10.0;

    public static IReadOnlyList<ProjectValidationIssue> Validate(AcousticsProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var issues = new List<ProjectValidationIssue>();

        if (string.IsNullOrWhiteSpace(project.Name))
            issues.Add(new ProjectValidationIssue("Proje adı boş olamaz.", ProjectValidationSeverity.Error));

        if (!Enum.IsDefined(project.ImportFormat))
            issues.Add(new ProjectValidationIssue($"Bilinmeyen içe aktarma biçimi: {project.ImportFormat}.", ProjectValidationSeverity.Error));
        if (!Enum.IsDefined(project.Unit))
            issues.Add(new ProjectValidationIssue($"Bilinmeyen proje birimi: {project.Unit}.", ProjectValidationSeverity.Error));
        if (!Enum.IsDefined(project.SelectedRoomType))
            issues.Add(new ProjectValidationIssue($"Bilinmeyen oda tipi: {project.SelectedRoomType}.", ProjectValidationSeverity.Error));

        if (project.SemaSurum <= 0)
            issues.Add(new ProjectValidationIssue(
                $"Geçersiz proje şema sürümü: {project.SemaSurum}.", ProjectValidationSeverity.Error));

        if (project.SemaSurum > AcousticsProjectSchema.CurrentVersion)
        {
            issues.Add(new ProjectValidationIssue(
                $"Proje dosyası bu uygulamanın desteklediğinden daha yeni bir şema sürümünde " +
                $"(dosya: {project.SemaSurum}, uygulama: {AcousticsProjectSchema.CurrentVersion}). " +
                "Lütfen EYE4PRO'yu güncelleyin.",
                ProjectValidationSeverity.Error));
            return issues; // İleri sürüm güvenle yorumlanamaz — başka kontrol anlamsız.
        }

        var seenSurfaceIds = new HashSet<int>();
        for (var surfaceIndex = 0; surfaceIndex < project.Surfaces.Count; surfaceIndex++)
        {
            var surface = project.Surfaces[surfaceIndex];
            if (surface is null)
            {
                issues.Add(new ProjectValidationIssue(
                    $"Yüzey listesinin {surfaceIndex + 1}. öğesi boş.", ProjectValidationSeverity.Error));
                continue;
            }

            if (!seenSurfaceIds.Add(surface.Id))
                issues.Add(new ProjectValidationIssue(
                    $"Yinelenen yüzey ID'si: {surface.Id} ('{surface.Name}').", ProjectValidationSeverity.Error));

            if (string.IsNullOrWhiteSpace(surface.Name) || string.IsNullOrWhiteSpace(surface.Category))
                issues.Add(new ProjectValidationIssue(
                    $"Yüzey ID {surface.Id} için ad ve kategori boş olamaz.", ProjectValidationSeverity.Error));

            if (surface.Vertices is null || surface.Vertices.Count < 3)
            {
                issues.Add(new ProjectValidationIssue(
                    $"Yüzey '{surface.Name}' (ID {surface.Id}) üçten az köşeye sahip.", ProjectValidationSeverity.Error));
                continue;
            }

            if (surface.Vertices.Any(v => !IsFinite(v)))
                issues.Add(new ProjectValidationIssue(
                    $"Yüzey '{surface.Name}' (ID {surface.Id}) geçersiz (NaN/Infinity) köşe koordinatı içeriyor.",
                    ProjectValidationSeverity.Error));
            else if (!HasUsablePolygonArea(surface.Vertices, project.Unit))
                issues.Add(new ProjectValidationIssue(
                    $"Yüzey '{surface.Name}' (ID {surface.Id}) sıfır alana sahip veya köşeleri eşdoğrusal.",
                    ProjectValidationSeverity.Error));
        }

        ValidatePoints(project.Sources, "Kaynak", AcousticPointType.Source, issues);
        ValidatePoints(project.Receivers, "Alıcı", AcousticPointType.Receiver, issues);

        if (!double.IsFinite(project.RoomVolume) || project.RoomVolume < 0)
            issues.Add(new ProjectValidationIssue(
                $"Oda hacmi geçersiz: {project.RoomVolume}.", ProjectValidationSeverity.Error));

        if (project.VolumeOrigin is { } origin && !Enum.IsDefined(origin))
            issues.Add(new ProjectValidationIssue($"Bilinmeyen hacim kaynağı: {origin}.", ProjectValidationSeverity.Error));

        ValidateMaterialMap(project.CategoryMaterials, "kategori", issues);
        ValidateMaterialMap(project.SurfaceMaterials, "yüzey", issues);
        ValidatePanels(project.PanelKonfigurasyon, project.Unit, issues);
        var expectedUnitScale = Enum.IsDefined(project.Unit)
            ? ProjectScalingService.GetMetersPerUnit(project.Unit)
            : (double?)null;
        ValidateSimulationParameters(
            project.SimulationSettings,
            "Simülasyon ayarları",
            issues,
            expectedUnitScale);
        ValidateSimulationResult(project.LastResult, issues);

        return issues;
    }

    public static bool HasErrors(IReadOnlyList<ProjectValidationIssue> issues) =>
        issues.Any(i => i.Severity == ProjectValidationSeverity.Error);

    private static void ValidatePoints(
        IReadOnlyList<AcousticPoint> points,
        string label,
        AcousticPointType expectedType,
        List<ProjectValidationIssue> issues)
    {
        var seenIds = new HashSet<int>();
        for (var index = 0; index < points.Count; index++)
        {
            var p = points[index];
            if (p is null)
            {
                issues.Add(new ProjectValidationIssue(
                    $"{label} listesinin {index + 1}. öğesi boş.", ProjectValidationSeverity.Error));
                continue;
            }

            if (!seenIds.Add(p.Id))
                issues.Add(new ProjectValidationIssue(
                    $"Yinelenen {label.ToLowerInvariant()} ID'si: {p.Id} ('{p.Label}').", ProjectValidationSeverity.Error));

            if (!IsFinite(p.Position))
                issues.Add(new ProjectValidationIssue(
                    $"{label} '{p.Label}' (ID {p.Id}) geçersiz (NaN/Infinity) konum içeriyor.",
                    ProjectValidationSeverity.Error));

            if (string.IsNullOrWhiteSpace(p.Label))
                issues.Add(new ProjectValidationIssue(
                    $"{label} ID {p.Id} etiketi boş.", ProjectValidationSeverity.Error));

            if (p.Type != expectedType)
                issues.Add(new ProjectValidationIssue(
                    $"{label} '{p.Label}' yanlış nokta tipine sahip: {p.Type}.", ProjectValidationSeverity.Error));

            if (!Enum.IsDefined(p.Directivity) ||
                !double.IsFinite(p.DirectivityAzimuthDeg) ||
                !double.IsFinite(p.DirectivityElevationDeg) ||
                !double.IsFinite(p.DirectivityRollDeg))
                issues.Add(new ProjectValidationIssue(
                    $"{label} '{p.Label}' geçersiz yönlülük/açı değeri içeriyor.", ProjectValidationSeverity.Error));
        }
    }

    private static void ValidateMaterialMap(
        IReadOnlyDictionary<string, string> map,
        string label,
        List<ProjectValidationIssue> issues)
    {
        foreach (var (key, value) in map)
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                issues.Add(new ProjectValidationIssue(
                    $"Geçersiz {label} materyal eşleşmesi bulundu.", ProjectValidationSeverity.Error));
    }

    private static void ValidatePanels(
        AcousticPanelKonfigurasyon? config,
        SurfaceImportUnit projectUnit,
        List<ProjectValidationIssue> issues)
    {
        if (config is null) return;
        if (config.PanelEnleriM is null || config.AktifTipler is null || config.Paneller is null)
        {
            issues.Add(new ProjectValidationIssue(
                "Panel konfigürasyonu eksik koleksiyon içeriyor.", ProjectValidationSeverity.Error));
            return;
        }

        var hasPanelDefinition = config.AktifTipler.Length > 0 || config.Paneller.Count > 0;
        if ((hasPanelDefinition && config.OturumId == Guid.Empty) ||
            !Enum.IsDefined(config.PanelEniModu) ||
            !double.IsFinite(config.PanelEniM) || config.PanelEniM < 0 ||
            !double.IsFinite(config.SarkmaCikmaM) || config.SarkmaCikmaM < 0 ||
            config.PanelEnleriM.Any(value => !double.IsFinite(value) || value < 0) ||
            config.AktifTipler.Any(value => !Enum.IsDefined(value)))
            issues.Add(new ProjectValidationIssue(
                "Panel konfigürasyonu geçersiz ölçü veya enum değeri içeriyor.",
                ProjectValidationSeverity.Error));

        if (hasPanelDefinition &&
            (config.PanelEniModu == PanelEniModu.TekTip && config.PanelEniM <= 0 ||
             config.PanelEniModu == PanelEniModu.Sirali &&
             (config.PanelEnleriM.Count == 0 || config.PanelEnleriM.Any(value => value <= 0))))
            issues.Add(new ProjectValidationIssue(
                "Etkin panel konfigürasyonu pozitif bir tek tip veya sıralı panel eni içermeli.",
                ProjectValidationSeverity.Error));

        var ids = new HashSet<Guid>();
        foreach (var panel in config.Paneller)
        {
            if (panel is null)
            {
                issues.Add(new ProjectValidationIssue("Panel listesinde boş öğe var.", ProjectValidationSeverity.Error));
                continue;
            }
            if (panel.Id == Guid.Empty)
                issues.Add(new ProjectValidationIssue(
                    "Panel ID'si boş GUID olamaz.", ProjectValidationSeverity.Error));
            else if (!ids.Add(panel.Id))
                issues.Add(new ProjectValidationIssue($"Yinelenen panel ID'si: {panel.Id}.", ProjectValidationSeverity.Error));

            if (!Enum.IsDefined(panel.Tip) ||
                !double.IsFinite(panel.PanelEniM) || panel.PanelEniM <= 0 ||
                !double.IsFinite(panel.SarkmaCikmaM) || panel.SarkmaCikmaM < 0 ||
                !double.IsFinite(panel.AciDerece) || panel.Vertices is null || panel.Vertices.Count < 3 ||
                panel.Vertices.Any(v => !IsFinite(v)) ||
                panel.Vertices.Count >= 3 && panel.Vertices.All(IsFinite) &&
                !HasUsablePolygonArea(panel.Vertices, projectUnit))
                issues.Add(new ProjectValidationIssue(
                    $"Panel {panel.Id} geçersiz tip, ölçü veya yüzey geometrisi içeriyor.",
                    ProjectValidationSeverity.Error));
        }
    }

    private static bool IsFinite(Point3D p) =>
        double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z);

    /// <summary>
    /// Fan üçgenlerinin alan vektörünü fiziksel metreye dönüştürerek ölçer.
    /// Mutlak tolerans yalnız sayısal sıfırı yakalar; göreli tolerans ise büyük
    /// koordinatlardaki neredeyse-eşdoğrusal poligonları reddeder. Böylece mm/cm
    /// projelerinde aynı fiziksel yüzey farklı sonuç vermez.
    /// </summary>
    private static bool HasUsablePolygonArea(
        IReadOnlyList<Point3D> vertices,
        SurfaceImportUnit unit)
    {
        if (vertices.Count < 3) return false;

        var metersPerUnit = ProjectScalingService.GetMetersPerUnit(unit);
        var origin = vertices[0];
        var offsets = new (double X, double Y, double Z)[vertices.Count];
        double maximumRadiusM = 0;

        for (var i = 1; i < vertices.Count; i++)
        {
            var x = (vertices[i].X - origin.X) * metersPerUnit;
            var y = (vertices[i].Y - origin.Y) * metersPerUnit;
            var z = (vertices[i].Z - origin.Z) * metersPerUnit;
            if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z)) return false;

            offsets[i] = (x, y, z);
            var radius = StableLength(x, y, z);
            if (!double.IsFinite(radius)) return false;
            maximumRadiusM = Math.Max(maximumRadiusM, radius);
        }

        double nx = 0, ny = 0, nz = 0;
        for (var i = 1; i < offsets.Length - 1; i++)
        {
            var a = offsets[i];
            var b = offsets[i + 1];
            nx += a.Y * b.Z - a.Z * b.Y;
            ny += a.Z * b.X - a.X * b.Z;
            nz += a.X * b.Y - a.Y * b.X;
        }

        var twiceAreaM2 = StableLength(nx, ny, nz);
        var relativeToleranceM2 = maximumRadiusM * maximumRadiusM * 1e-12;
        return double.IsFinite(twiceAreaM2) && double.IsFinite(relativeToleranceM2) &&
               twiceAreaM2 > Math.Max(1e-24, relativeToleranceM2);
    }

    private static double StableLength(double x, double y, double z)
    {
        var scale = Math.Max(Math.Abs(x), Math.Max(Math.Abs(y), Math.Abs(z)));
        if (scale == 0) return 0;
        if (!double.IsFinite(scale)) return double.PositiveInfinity;
        x /= scale;
        y /= scale;
        z /= scale;
        return scale * Math.Sqrt(x * x + y * y + z * z);
    }

    private static void ValidateSimulationResult(
        SimulationResult? result,
        List<ProjectValidationIssue> issues)
    {
        if (result is null) return;

        if (result.Parameters is null)
            issues.Add(new ProjectValidationIssue(
                "Kayıtlı simülasyon sonucu parametre içermiyor.", ProjectValidationSeverity.Error));
        else
            ValidateSimulationParameters(result.Parameters, "Kayıtlı simülasyon parametreleri", issues);

        if (result.Echograms is null || result.Echograms.Count == 0)
        {
            issues.Add(new ProjectValidationIssue(
                "Kayıtlı simülasyon sonucu kaynak-alıcı çifti içermiyor.", ProjectValidationSeverity.Error));
            return;
        }

        for (var i = 0; i < result.Echograms.Count; i++)
        {
            var echogram = result.Echograms[i];
            if (echogram is null)
            {
                issues.Add(new ProjectValidationIssue(
                    $"Kayıtlı simülasyonun {i + 1}. ekogramı boş.", ProjectValidationSeverity.Error));
                continue;
            }

            if (echogram.StoredArrivalCount < 0 ||
                echogram.SourceLabel is null || echogram.ReceiverLabel is null)
                issues.Add(new ProjectValidationIssue(
                    $"Kayıtlı simülasyonun {i + 1}. ekogramı geçersiz metadata içeriyor.",
                    ProjectValidationSeverity.Error));
        }

        if (result.ComputedParameters is { } computed)
        {
            if (computed.Count > 0 && computed.Count != result.Echograms.Count)
                issues.Add(new ProjectValidationIssue(
                    "Kayıtlı akustik parametre sayısı kaynak-alıcı çifti sayısıyla uyuşmuyor.",
                    ProjectValidationSeverity.Error));

            for (var i = 0; i < computed.Count; i++)
            {
                ValidateAcousticParameters(computed[i], i, issues);

                if (i >= result.Echograms.Count || computed[i] is not { } parameters ||
                    result.Echograms[i] is not { } echogram)
                    continue;

                if (parameters.SourceId != echogram.SourceId ||
                    parameters.ReceiverId != echogram.ReceiverId)
                    issues.Add(new ProjectValidationIssue(
                        $"Kayıtlı akustik parametrelerin {i + 1}. öğesi ekogram kaynak-alıcı kimlikleriyle uyuşmuyor.",
                        ProjectValidationSeverity.Error));
            }
        }

        ValidateGridMap(result.GridMap, issues);

        if (result.Reliability is { } reliability &&
            (reliability.Warnings is null || reliability.Recommendations is null ||
             reliability.Metrics is null || !double.IsFinite(reliability.ConfidenceScore) ||
             reliability.ConfidenceScore is < 0 or > 100))
            issues.Add(new ProjectValidationIssue(
                "Kayıtlı simülasyon güvenilirlik raporu geçersiz.", ProjectValidationSeverity.Error));
    }

    private static void ValidateAcousticParameters(
        AcousticParameters? parameters,
        int index,
        List<ProjectValidationIssue> issues)
    {
        int[] expectedFrequencies = [63, 125, 250, 500, 1000, 2000, 4000, 8000];
        if (parameters is null || parameters.Bands is null || parameters.Bands.Count != expectedFrequencies.Length)
        {
            issues.Add(new ProjectValidationIssue(
                $"Kayıtlı akustik parametrelerin {index + 1}. öğesi sekiz oktav bandı içermiyor.",
                ProjectValidationSeverity.Error));
            return;
        }

        for (var bandIndex = 0; bandIndex < expectedFrequencies.Length; bandIndex++)
        {
            var band = parameters.Bands[bandIndex];
            if (band is null || band.FrequencyHz != expectedFrequencies[bandIndex] || band.SchroederCurve is null)
            {
                issues.Add(new ProjectValidationIssue(
                    $"Kayıtlı akustik parametrelerin {index + 1}. öğesinde oktav bandı sırası/verisi geçersiz.",
                    ProjectValidationSeverity.Error));
                return;
            }

            if (HasInfiniteMetric(
                    band.RT60, band.T20, band.T30, band.EDT, band.Ts, band.G,
                    band.C50, band.C80, band.D50, band.LF, band.LFC,
                    band.STearly, band.STlate, band.DecayFitR2, band.DynamicRangeDb))
            {
                issues.Add(new ProjectValidationIssue(
                    $"Kayıtlı akustik parametrelerin {index + 1}. öğesinde sonsuz metrik değeri var.",
                    ProjectValidationSeverity.Error));
                return;
            }

            for (var curveIndex = 0; curveIndex < band.SchroederCurve.Length; curveIndex++)
            {
                var point = band.SchroederCurve[curveIndex];
                var outOfOrder = curveIndex > 0 &&
                    (point.Time < band.SchroederCurve[curveIndex - 1].Time ||
                     point.Db > band.SchroederCurve[curveIndex - 1].Db + 1e-6);
                if (!double.IsFinite(point.Time) || point.Time < 0 ||
                    !double.IsFinite(point.Db) || outOfOrder)
                {
                    issues.Add(new ProjectValidationIssue(
                        $"Kayıtlı akustik parametrelerin {index + 1}. öğesinde Schroeder eğrisi sonlu ve sıralı değil.",
                        ProjectValidationSeverity.Error));
                    return;
                }
            }
        }

        if (double.IsInfinity(parameters.IACC) || double.IsInfinity(parameters.STI))
            issues.Add(new ProjectValidationIssue(
                $"Kayıtlı akustik parametrelerin {index + 1}. öğesinde sonsuz özet metrik değeri var.",
                ProjectValidationSeverity.Error));
    }

    // NaN, hesaplanamayan akustik metriklerin bilinçli sentinel değeridir ve JSON'da
    // null olarak saklanır. Infinity ise hesaplama/bozulma hatasıdır ve kabul edilmez.
    private static bool HasInfiniteMetric(params double[] values) =>
        values.Any(double.IsInfinity);

    private static void ValidateGridMap(
        GridMapResult? gridMap,
        List<ProjectValidationIssue> issues)
    {
        if (gridMap is null) return;
        if (gridMap.Grid is null || gridMap.Rows <= 0 || gridMap.Cols <= 0 ||
            !IsValidReceiverGrid(gridMap.Grid) ||
            !GridDefinitionMatches(gridMap.Grid, gridMap.Rows, gridMap.Cols) ||
            !HasGridShape(gridMap.Rt60, gridMap.Rows, gridMap.Cols) ||
            !HasGridShape(gridMap.C80, gridMap.Rows, gridMap.Cols) ||
            !HasGridShape(gridMap.C50, gridMap.Rows, gridMap.Cols) ||
            !HasGridShape(gridMap.Sti, gridMap.Rows, gridMap.Cols))
            issues.Add(new ProjectValidationIssue(
                "Kayıtlı simülasyon ızgara haritasının boyutları/verisi geçersiz.",
                ProjectValidationSeverity.Error));
    }

    private static bool GridDefinitionMatches(ReceiverGrid grid, int rows, int cols)
    {
        var expectedCols = Math.Round((grid.XMax - grid.XMin) / grid.CellSize) + 1;
        var expectedRows = Math.Round((grid.YMax - grid.YMin) / grid.CellSize) + 1;
        return double.IsFinite(expectedCols) && double.IsFinite(expectedRows) &&
               expectedCols == cols && expectedRows == rows;
    }

    private static bool HasGridShape(double[][]? values, int rows, int cols) =>
        values is not null && values.Length == rows &&
        values.All(row => row is not null && row.Length == cols && row.All(v => !double.IsInfinity(v)));

    private static void ValidateSimulationParameters(
        SimulationParameters? parameters,
        string label,
        List<ProjectValidationIssue> issues,
        double? expectedUnitScale = null)
    {
        if (parameters is null) return;
        var receiverRadiusM = parameters.ReceiverRadius * parameters.UnitScale;
        var unitScaleMismatch = expectedUnitScale is { } expected &&
            !NearlyEqual(parameters.UnitScale, expected);
        if (parameters.RayCount is < MinimumRayCount or > MaximumRayCount ||
            parameters.MaxOrder is < MinimumReflectionOrder or > MaximumReflectionOrder ||
            parameters.IsmOrder is < 0 or > 2 ||
            parameters.TransitionOrder is < MinimumTransitionOrder or > MaximumTransitionOrder ||
            parameters.ConvergenceModeIndex is < 0 or > 2 ||
            !double.IsFinite(parameters.ReceiverRadius) ||
            !double.IsFinite(receiverRadiusM) ||
            receiverRadiusM is < MinimumReceiverRadiusM or > MaximumReceiverRadiusM ||
            !double.IsFinite(parameters.EnergyThreshold) || parameters.EnergyThreshold <= 0 ||
            !double.IsFinite(parameters.AirTemperatureCelsius) ||
            parameters.AirTemperatureCelsius is < -20.0 or > 50.0 ||
            !double.IsFinite(parameters.RelativeHumidity) ||
            parameters.RelativeHumidity is < 10.0 or > 100.0 ||
            !double.IsFinite(parameters.UnitScale) || parameters.UnitScale <= 0 ||
            unitScaleMismatch ||
            parameters.ReceiverGrid is { } grid &&
            (!IsValidReceiverGrid(grid) || !HasAcceptableGridCellCount(grid)))
            issues.Add(new ProjectValidationIssue(
                $"{label} geçersiz veya sonlu olmayan değer içeriyor.", ProjectValidationSeverity.Error));
    }

    private static bool IsValidReceiverGrid(ReceiverGrid grid) =>
        double.IsFinite(grid.PlaneZ) &&
        double.IsFinite(grid.XMin) && double.IsFinite(grid.XMax) && grid.XMax > grid.XMin &&
        double.IsFinite(grid.YMin) && double.IsFinite(grid.YMax) && grid.YMax > grid.YMin &&
        double.IsFinite(grid.CellSize) && grid.CellSize > 0 &&
        TryGetGridDimensions(grid, out _, out _);

    private static bool HasAcceptableGridCellCount(ReceiverGrid grid) =>
        TryGetGridDimensions(grid, out var cols, out var rows) &&
        cols * rows <= MaximumGridCells;

    private static bool TryGetGridDimensions(ReceiverGrid grid, out double cols, out double rows)
    {
        cols = Math.Round((grid.XMax - grid.XMin) / grid.CellSize) + 1;
        rows = Math.Round((grid.YMax - grid.YMin) / grid.CellSize) + 1;
        return double.IsFinite(cols) && double.IsFinite(rows) &&
               cols is >= 1 and <= int.MaxValue && rows is >= 1 and <= int.MaxValue &&
               double.IsFinite(cols * rows) && cols * rows <= int.MaxValue;
    }

    private static bool NearlyEqual(double left, double right)
    {
        if (!double.IsFinite(left) || !double.IsFinite(right)) return false;
        return Math.Abs(left - right) <= Math.Max(1e-12, Math.Abs(right) * 1e-9);
    }
}
