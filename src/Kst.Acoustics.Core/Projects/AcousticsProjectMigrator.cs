namespace Kst.Acoustics.Core.Projects;

/// <summary>Uygulamanın bildiği en güncel M01 proje şema sürümü.</summary>
public static class AcousticsProjectSchema
{
    public const int CurrentVersion = 3;
}

/// <summary>
/// Dosya uygulamanın bildiğinden daha yeni olduğunda oluşur. Bu durum bozulma
/// değildir; eski bir .bak sürümüne sessizce düşmek kullanıcının yeni verisini
/// gizleyebileceği için backup recovery özellikle yapılmaz.
/// </summary>
public sealed class UnsupportedProjectSchemaException : NotSupportedException
{
    public UnsupportedProjectSchemaException(int fileVersion, int supportedVersion)
        : base($"Proje şema sürümü desteklenmiyor (dosya: {fileVersion}, uygulama: {supportedVersion}).")
    {
        FileVersion = fileVersion;
        SupportedVersion = supportedVersion;
    }

    public int FileVersion { get; }
    public int SupportedVersion { get; }
}

/// <summary>
/// Diskten okunan bir AcousticsProject'i güncel şemaya taşır ve doğrular. Dönüşümler
/// burada ardışık uygulanır; proje dosyası okuma tek bir yerden geçtiği için tüm
/// çağıranlar aynı migrasyon ve doğrulama zincirini kullanır.
/// </summary>
public static class AcousticsProjectMigrator
{
    /// <exception cref="InvalidDataException">Dosya ileri bir şema sürümünde ya da doğrulama hatası içeriyorsa.</exception>
    public static AcousticsProject MigrateAndValidate(AcousticsProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (project.SemaSurum <= 0)
            throw new InvalidDataException(
                $"Proje şema sürümü geçersiz: {project.SemaSurum}.");

        if (project.SemaSurum > AcousticsProjectSchema.CurrentVersion)
            throw new UnsupportedProjectSchemaException(
                project.SemaSurum,
                AcousticsProjectSchema.CurrentVersion);

        var migrated = Migrate(project);

        var issues = AcousticsProjectValidator.Validate(migrated);
        if (AcousticsProjectValidator.HasErrors(issues))
        {
            var messages = issues
                .Where(i => i.Severity == ProjectValidationSeverity.Error)
                .Select(i => i.Message);
            throw new InvalidDataException(
                "Proje dosyası geçersiz:\n" + string.Join("\n", messages));
        }

        var warnings = issues
            .Where(i => i.Severity == ProjectValidationSeverity.Warning)
            .Select(i => i.Message)
            .ToArray();
        return warnings.Length == 0
            ? migrated
            : migrated with
            {
                ImportWarnings = migrated.ImportWarnings
                    .Concat(warnings)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
            };
    }

    private static AcousticsProject Migrate(AcousticsProject project)
    {
        var panel = NormalizePanelConfiguration(project.PanelKonfigurasyon);
        var simulationSettings = project.SimulationSettings;
        var scenarios = project.Scenarios?.ToArray() ?? [];

        // v1 dosyalarında UnitScale yoktu veya varsayılan 1 olarak kalabiliyordu.
        // ReceiverRadius zaten proje birimindedir; yalnız proje birimi→metre damgasını
        // tamamla. Tarihsel LastResult.Parameters değiştirilmez: birim dönüşümüyle
        // stale olmuş eski bir sonucun hangi girdilerle hesaplandığı korunmalıdır.
        if (project.SemaSurum < 2 && simulationSettings is not null)
            simulationSettings = simulationSettings with
            {
                UnitScale = ProjectScalingService.GetMetersPerUnit(project.Unit)
            };

        // v1 → v2: hacim kaynağı, import uyarıları ve simülasyon ayarları opsiyonel
        // alanlardır. Eksik olduklarında güvenli varsayılan/null korunur.
        return project with
        {
            SemaSurum = AcousticsProjectSchema.CurrentVersion,
            PanelKonfigurasyon = panel,
            ImportWarnings = project.ImportWarnings?.ToArray() ?? [],
            SimulationSettings = simulationSettings,
            Scenarios = scenarios,
            ActiveScenarioId = project.SemaSurum < 3 ? null : project.ActiveScenarioId
        };
    }

    private static AcousticPanelKonfigurasyon? NormalizePanelConfiguration(AcousticPanelKonfigurasyon? source)
    {
        if (source is null) return null;
        source.PanelEnleriM ??= [];
        source.AktifTipler ??= [];
        source.Paneller ??= [];
        foreach (var panel in source.Paneller.Where(p => p is not null))
            panel.Vertices ??= [];
        return source;
    }
}
