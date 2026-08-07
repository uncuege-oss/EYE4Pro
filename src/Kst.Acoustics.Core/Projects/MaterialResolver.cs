using System.Collections.Generic;
using System.Globalization;

namespace Kst.Acoustics.Core.Projects;

/// <summary>
/// Bir yüzey için kullanılacak materyal ID'sini öncelik sırasıyla çözer:
///   1) SurfaceMaterials[surface.Id] varsa (yüzey-bazlı override)
///   2) CategoryMaterials[surface.Category] varsa (kategori varsayılanı)
///   3) Hiçbiri yoksa null (caller library default'una düşer).
/// </summary>
public static class MaterialResolver
{
    public static string? ResolveMaterialId(
        ProjectSurface surface,
        IReadOnlyDictionary<string, string>? surfaceMaterials,
        IReadOnlyDictionary<string, string>? categoryMaterials)
    {
        if (surfaceMaterials is not null &&
            surfaceMaterials.TryGetValue(surface.Id.ToString(CultureInfo.InvariantCulture), out var perSurfaceId) &&
            !string.IsNullOrEmpty(perSurfaceId))
        {
            return perSurfaceId;
        }

        if (categoryMaterials is not null &&
            categoryMaterials.TryGetValue(surface.Category, out var categoryId) &&
            !string.IsNullOrEmpty(categoryId))
        {
            return categoryId;
        }

        return null;
    }

    public static string? ResolveMaterialId(ProjectSurface surface, AcousticsProject project) =>
        ResolveMaterialId(surface, project.SurfaceMaterials, project.CategoryMaterials);
}
