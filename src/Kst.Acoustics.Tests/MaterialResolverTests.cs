using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.Tests;

/// <summary>
/// MaterialResolver öncelik kuralını doğrular: yüzey override → kategori varsayılanı → null.
/// Bu, simülasyonun yüzey-bazlı materyal atamalarını doğru aldığının regresyon kanıtıdır.
/// </summary>
public static class MaterialResolverTests
{
    private static ProjectSurface MakeSurface(int id, string category) =>
        new(id, $"S{id}", category, [new Point3D(0, 0, 0), new Point3D(1, 0, 0), new Point3D(0, 1, 0)]);

    public static void SurfaceOverrideBeatsCategoryDefault()
    {
        var surface = MakeSurface(42, "DUVAR");

        var surfaceOverrides = new Dictionary<string, string> { ["42"] = "MAT_CUSTOM" };
        var categoryDefaults = new Dictionary<string, string> { ["DUVAR"] = "MAT_DEFAULT" };

        var id = MaterialResolver.ResolveMaterialId(surface, surfaceOverrides, categoryDefaults);
        if (id != "MAT_CUSTOM")
            throw new Exception($"Yüzey override öncelikli olmalı: beklenen MAT_CUSTOM, alınan {id}");
    }

    public static void CategoryDefaultUsedWhenNoOverride()
    {
        var surface = MakeSurface(10, "TAVAN");

        var surfaceOverrides = new Dictionary<string, string>(); // boş
        var categoryDefaults = new Dictionary<string, string> { ["TAVAN"] = "MAT_TAVAN" };

        var id = MaterialResolver.ResolveMaterialId(surface, surfaceOverrides, categoryDefaults);
        if (id != "MAT_TAVAN")
            throw new Exception($"Kategori varsayılanı kullanılmalı: beklenen MAT_TAVAN, alınan {id}");
    }

    public static void NullWhenNeitherDefined()
    {
        var surface = MakeSurface(7, "DUVAR");

        var id = MaterialResolver.ResolveMaterialId(surface, null, null);
        if (id is not null)
            throw new Exception($"Hiçbir atama yoksa null dönmeli: alınan {id}");
    }

    public static void EmptyStringOverrideTreatedAsAbsent()
    {
        var surface = MakeSurface(5, "DUVAR");

        // Override boş string ise yok sayılmalı (UI'da silinen ama anahtar kalan durum)
        var surfaceOverrides = new Dictionary<string, string> { ["5"] = "" };
        var categoryDefaults = new Dictionary<string, string> { ["DUVAR"] = "MAT_DEFAULT" };

        var id = MaterialResolver.ResolveMaterialId(surface, surfaceOverrides, categoryDefaults);
        if (id != "MAT_DEFAULT")
            throw new Exception($"Boş override yok sayılıp kategori kullanılmalı: alınan {id}");
    }
}
