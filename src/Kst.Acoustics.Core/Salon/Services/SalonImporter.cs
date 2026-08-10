using System;
using System.Collections.Generic;
using System.Linq;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Salon.Models;
using Kst.Acoustics.Core.Simulation;

namespace Kst.Acoustics.Core.Salon.Services;

/// M51 → M01 köprüsü: SalonGeometri'yi M01'in tükettiği <see cref="AcousticsProject"/>'e çevirir.
/// Yüzeyler <see cref="Salon3DGorunumServisi"/>'den, hacim <see cref="SalonHacimServisi"/>'den
/// (gerçek plan geometrisi; bbox değil); kategoriler (Zemin/Duvar/Tavan/Sahne/Seyirci…)
/// M01'de malzeme atamasını sürer. `GeometriRevizyon` projeye damgalanır — M01 projeyi
/// sonradan açarken kaynak `.e4salon.json` güncellenmişse uyarı verebilir.
public static class SalonImporter
{
    public static AcousticsProject Cevir(SalonGeometri salon, string? kaynakDosya)
    {
        ArgumentNullException.ThrowIfNull(salon);
        SalonGeometriServisi.Normalize(salon);

        // M01 oda hesabı kapalı bir zemin kabuğu ister. Yalnız açık duvarlardan
        // üretilen yüzey sayısı ve bbox hacmi pozitif olabildiği için bunları geçerli
        // salon saymak, zeminsiz/tavansız sızdıran modeli içe aktarıyordu.
        var kabukPoligonlari = new List<IReadOnlyList<Point2D>>();
        foreach (var oge in salon.CizimOgeleri)
        {
            var katman = salon.Katmanlar.FirstOrDefault(k => k.Id == oge.KatmanId);
            if (katman is { Gorunur: false }) continue;
            if (katman is not null && katman.Tip != SalonKatmanTipi.Zemin) continue;
            var poly = oge.KapaliPoligon(Salon3DGorunumServisi.Faset(salon));
            if (poly.Count >= 3) kabukPoligonlari.Add(poly);
        }
        var kabuk = ClipperAdaptor.Birlestir(kabukPoligonlari);
        if (ClipperAdaptor.NetAlan(kabuk) <= 1e-9)
        {
            throw new InvalidDataException(
                "Salon geometrisi M01'e aktarılabilecek kapalı bir zemin kabuğu içermiyor.");
        }

        var surfaces = Salon3DGorunumServisi.YuzeyleriOlustur(salon);
        if (surfaces.Count == 0)
        {
            throw new InvalidDataException(
                "Salon geometrisi M01'e aktarılabilecek kapalı yüzey içermiyor.");
        }

        var volumeM3 = SalonHacimServisi.Hesapla(salon);
        if (!double.IsFinite(volumeM3) || volumeM3 <= 0)
        {
            throw new InvalidDataException(
                "Salon geometrisinden pozitif ve sonlu bir oda hacmi hesaplanamadı.");
        }

        return new AcousticsProject(
            Name: string.IsNullOrWhiteSpace(salon.SalonAdi) ? "Salon" : salon.SalonAdi,
            SourceFilePath: NormalizeSourcePath(kaynakDosya, salon.DosyaYolu),
            ImportedAtUtc: DateTime.UtcNow,
            ImportFormat: SurfaceImportFormat.Salon,
            Unit: SurfaceImportUnit.Meters,
            Surfaces: surfaces,
            RoomVolume: volumeM3,
            VolumeOrigin: RoomVolumeOrigin.SourceModel)
        {
            KaynakGeometriRevizyon = salon.GeometriRevizyon,
            SemaSurum = AcousticsProjectSchema.CurrentVersion
        };
    }

    /// <summary>
    /// Yeniden üretilen M51 geometrisini açık M01 projesine kontrollü biçimde uygular.
    /// Geometri ve kaynak damgası yenilenir; M01'de yapılan akustik çalışmalar korunur.
    /// Geometriye bağlı yüzey override'ları yalnız aynı semantik yüzey güvenle
    /// bulunabiliyorsa taşınır. Üretilmiş panel yerleşimleri ve simülasyon sonucu yeni
    /// geometri için geçerli sayılamayacağından sırasıyla yeniden üretilecek/stale olur.
    /// </summary>
    public static AcousticsProject GeometriyiGuncelle(
        AcousticsProject mevcutProje,
        AcousticsProject yeniGeometri)
    {
        ArgumentNullException.ThrowIfNull(mevcutProje);
        ArgumentNullException.ThrowIfNull(yeniGeometri);

        if (yeniGeometri.ImportFormat != SurfaceImportFormat.Salon)
            throw new ArgumentException(
                "Güncellenecek geometri M51 Salon biçiminde olmalıdır.",
                nameof(yeniGeometri));

        var coordinateFactor = ProjectScalingService.GetConversionFactor(
            mevcutProje.Unit,
            yeniGeometri.Unit);
        var sources = ProjectScalingService.ScalePoints(mevcutProje.Sources, coordinateFactor);
        var receivers = ProjectScalingService.ScalePoints(mevcutProje.Receivers, coordinateFactor);
        var surfaceMaterials = MatchSurfaceMaterials(
            mevcutProje.Surfaces,
            yeniGeometri.Surfaces,
            mevcutProje.SurfaceMaterials,
            out var droppedSurfaceOverrides);

        var warnings = yeniGeometri.ImportWarnings.ToList();
        if (droppedSurfaceOverrides > 0)
        {
            warnings.Add(
                $"Geometri güncellendi; güvenle eşleştirilemeyen {droppedSurfaceOverrides} yüzey malzemesi override'ı kaldırıldı. Kategori malzemeleri korundu.");
        }

        var panelConfig = mevcutProje.PanelKonfigurasyon;
        var hasGeometryBoundPanelState = panelConfig is not null &&
            ((panelConfig.Paneller?.Count ?? 0) > 0 ||
             !string.IsNullOrWhiteSpace(panelConfig.SahneOnuYuzeyId) ||
             !string.IsNullOrWhiteSpace(panelConfig.TavanYuzeyId) ||
             !string.IsNullOrWhiteSpace(panelConfig.SolDuvarYuzeyId) ||
             !string.IsNullOrWhiteSpace(panelConfig.SagDuvarYuzeyId));
        if (hasGeometryBoundPanelState)
        {
            warnings.Add(
                "Geometri güncellendi; mevcut akustik panel yerleşimleri temizlendi. Panel ölçüleri korunarak yerleşim yeniden üretilmelidir.");
        }

        if (mevcutProje.ActiveScenarioId is not null)
        {
            warnings.Add(
                "Geometri güncellendi; akustik senaryolar tarihsel snapshot olarak korundu ve ana model etkinleştirildi. Senaryoyu yeniden uygulayın ve gerekirse yeniden hesaplayın.");
        }

        return yeniGeometri with
        {
            Name = mevcutProje.Name,
            Sources = sources,
            Receivers = receivers,
            CategoryMaterials = new Dictionary<string, string>(
                mevcutProje.CategoryMaterials,
                StringComparer.OrdinalIgnoreCase),
            SurfaceMaterials = surfaceMaterials,
            SelectedRoomType = mevcutProje.SelectedRoomType,
            SimulationSettings = ConvertSimulationSettings(
                mevcutProje.SimulationSettings,
                coordinateFactor,
                yeniGeometri.Unit),
            PanelKonfigurasyon = PreservePanelSettings(mevcutProje.PanelKonfigurasyon),
            LastResult = MarkResultStale(mevcutProje.LastResult),
            Scenarios = mevcutProje.Scenarios.Select(AcousticScenarioCloner.Clone).ToArray(),
            ActiveScenarioId = null,
            ImportWarnings = warnings.Distinct(StringComparer.Ordinal).ToArray(),
            SemaSurum = AcousticsProjectSchema.CurrentVersion
        };
    }

    /// `.e4salon.json` dosyasını yükleyip M01 projesine çevirir.
    public static AcousticsProject Yukle(string dosyaYolu)
        => Cevir(SalonGeometriServisi.Ac(dosyaYolu), dosyaYolu);

    private static string? NormalizeSourcePath(string? requestedPath, string? modelPath)
    {
        var path = !string.IsNullOrWhiteSpace(requestedPath) ? requestedPath : modelPath;
        return string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
    }

    private static IReadOnlyDictionary<string, string> MatchSurfaceMaterials(
        IReadOnlyList<ProjectSurface> oldSurfaces,
        IReadOnlyList<ProjectSurface> newSurfaces,
        IReadOnlyDictionary<string, string> oldMaterials,
        out int droppedCount)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var oldById = oldSurfaces.ToDictionary(surface => surface.Id);
        var newById = newSurfaces.ToDictionary(surface => surface.Id);
        droppedCount = 0;

        foreach (var (surfaceId, materialId) in oldMaterials)
        {
            if (!int.TryParse(surfaceId, out var id) || !oldById.TryGetValue(id, out var oldSurface))
            {
                droppedCount++;
                continue;
            }

            ProjectSurface? match = null;
            if (newById.TryGetValue(id, out var sameId) && SameSemanticSurface(oldSurface, sameId))
            {
                match = sameId;
            }
            else
            {
                var oldSemanticCount = oldSurfaces.Count(candidate =>
                    SameSemanticSurface(oldSurface, candidate));
                var candidates = newSurfaces.Where(candidate =>
                    SameSemanticSurface(oldSurface, candidate)).ToArray();
                if (oldSemanticCount == 1 && candidates.Length == 1)
                    match = candidates[0];
            }

            if (match is null)
            {
                droppedCount++;
                continue;
            }

            result[match.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)] = materialId;
        }

        return result;
    }

    private static bool SameSemanticSurface(ProjectSurface left, ProjectSurface right) =>
        string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.Category, right.Category, StringComparison.OrdinalIgnoreCase);

    private static SimulationParameters? ConvertSimulationSettings(
        SimulationParameters? settings,
        double coordinateFactor,
        SurfaceImportUnit targetUnit)
    {
        if (settings is null) return null;

        var grid = settings.ReceiverGrid;
        var scaledGrid = grid is null
            ? null
            : new ReceiverGrid(
                grid.PlaneZ * coordinateFactor,
                grid.XMin * coordinateFactor,
                grid.XMax * coordinateFactor,
                grid.YMin * coordinateFactor,
                grid.YMax * coordinateFactor,
                grid.CellSize * coordinateFactor);

        return settings with
        {
            ReceiverRadius = settings.ReceiverRadius * coordinateFactor,
            ReceiverGrid = scaledGrid,
            UnitScale = ProjectScalingService.GetMetersPerUnit(targetUnit),
            InputFingerprint = null
        };
    }

    private static AcousticPanelKonfigurasyon? PreservePanelSettings(
        AcousticPanelKonfigurasyon? source)
    {
        if (source is null) return null;

        return new AcousticPanelKonfigurasyon
        {
            OturumId = Guid.NewGuid(),
            PanelEniM = source.PanelEniM,
            PanelEniModu = source.PanelEniModu,
            PanelEnleriM = source.PanelEnleriM?.ToList() ?? [],
            SarkmaCikmaM = source.SarkmaCikmaM,
            AktifTipler = source.AktifTipler?.ToArray() ?? [],
            Paneller = [],
            SahneOnuYuzeyId = null,
            TavanYuzeyId = null,
            SolDuvarYuzeyId = null,
            SagDuvarYuzeyId = null
        };
    }

    private static SimulationResult? MarkResultStale(SimulationResult? source)
    {
        if (source is null) return null;

        return new SimulationResult
        {
            Echograms = source.Echograms,
            Parameters = source.Parameters,
            Elapsed = source.Elapsed,
            ComputedAt = source.ComputedAt,
            GridMap = source.GridMap,
            Reliability = source.Reliability,
            ComputedParameters = source.ComputedParameters,
            InputFingerprint = source.InputFingerprint,
            IsStale = true,
            StaleReason = "M51 salon geometrisi güncellendi."
        };
    }
}
