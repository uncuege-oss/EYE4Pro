namespace Kst.Acoustics.Core.Projects;

public static class MaterialLibrary
{
    // Alpha dizisi sırası: [63, 125, 250, 500, 1k, 2k, 4k, 8k] Hz
    // Scattering dizisi sırası: [63, 125, 250, 500, 1k, 2k, 4k, 8k] Hz
    private static IReadOnlyList<AcousticMaterial> CorePresets { get; } =
    [
        new("concrete_bare",    "Çıplak Beton",
            [0.01, 0.01, 0.01, 0.02, 0.02, 0.03, 0.04, 0.05],
            [0.02, 0.02, 0.03, 0.03, 0.03, 0.04, 0.04, 0.05],
            "Beton / taş"),

        new("concrete_painted", "Boyalı Beton",
            [0.01, 0.01, 0.01, 0.02, 0.02, 0.02, 0.02, 0.02],
            [0.02, 0.02, 0.03, 0.03, 0.03, 0.03, 0.03, 0.04],
            "Beton / taş"),

        new("brick",            "Sıvasız Tuğla",
            [0.02, 0.03, 0.03, 0.03, 0.04, 0.05, 0.07, 0.10],
            [0.02, 0.02, 0.03, 0.03, 0.04, 0.04, 0.05, 0.06],
            "Duvar / tuğla"),

        new("plaster",          "Sıvalı Yüzey",
            [0.02, 0.02, 0.02, 0.03, 0.04, 0.05, 0.05, 0.05],
            [0.02, 0.02, 0.03, 0.03, 0.04, 0.04, 0.05, 0.05],
            "Sıva / alçı"),

        new("glass",            "Cam (Büyük Panel)",
            [0.50, 0.35, 0.25, 0.18, 0.12, 0.07, 0.04, 0.02],
            [0.03, 0.03, 0.04, 0.05, 0.06, 0.07, 0.07, 0.07],
            "Cam"),

        new("wood_parquet",     "Parke / Ahşap Döşeme",
            [0.04, 0.04, 0.04, 0.07, 0.06, 0.06, 0.07, 0.07],
            [0.03, 0.03, 0.04, 0.05, 0.05, 0.06, 0.06, 0.07],
            "Ahşap / döşeme"),

        new("wood_stage",       "Ahşap Sahne Tabanı",
            [0.15, 0.10, 0.07, 0.05, 0.04, 0.04, 0.04, 0.03],
            [0.03, 0.03, 0.04, 0.05, 0.05, 0.06, 0.06, 0.07],
            "Ahşap / döşeme"),

        new("gypsum_board",     "Alçıpan",
            [0.35, 0.29, 0.10, 0.05, 0.04, 0.07, 0.09, 0.10],
            [0.03, 0.03, 0.04, 0.05, 0.05, 0.06, 0.07, 0.08],
            "Alçıpan / panel"),

        new("carpet_thin",      "İnce Halı (5mm)",
            [0.01, 0.02, 0.03, 0.05, 0.10, 0.30, 0.50, 0.60],
            [0.06, 0.07, 0.08, 0.10, 0.12, 0.14, 0.15, 0.18],
            "Halı / tekstil"),

        new("carpet_thick",     "Kalın Halı (10mm)",
            [0.05, 0.08, 0.24, 0.57, 0.69, 0.71, 0.73, 0.75],
            [0.06, 0.07, 0.08, 0.10, 0.12, 0.14, 0.15, 0.18],
            "Halı / tekstil"),

        new("curtain_heavy",    "Ağır Perde / Kumaş",
            [0.07, 0.14, 0.35, 0.55, 0.72, 0.70, 0.65, 0.60],
            [0.08, 0.10, 0.12, 0.14, 0.16, 0.18, 0.20, 0.20],
            "Perde / tekstil"),

        new("acoustic_ceiling", "Akustik Tavan Paneli",
            [0.07, 0.14, 0.30, 0.60, 0.75, 0.79, 0.80, 0.75],
            [0.12, 0.14, 0.16, 0.20, 0.22, 0.25, 0.28, 0.30],
            "Akustik panel"),

        new("foam_50mm",        "Akustik Sünger 50mm",
            [0.05, 0.08, 0.25, 0.60, 0.85, 0.90, 0.85, 0.80],
            [0.18, 0.20, 0.25, 0.30, 0.35, 0.38, 0.40, 0.40],
            "Akustik panel"),

        new("audience_seated",  "Oturmuş Dinleyici Alanı",
            [0.20, 0.39, 0.57, 0.80, 0.94, 0.92, 0.87, 0.82],
            [0.30, 0.35, 0.40, 0.50, 0.55, 0.58, 0.60, 0.60],
            "İnsan / koltuk"),

        new("open_air",         "Açık Alan (referans)",
            [1.00, 1.00, 1.00, 1.00, 1.00, 1.00, 1.00, 1.00],
            [1.00, 1.00, 1.00, 1.00, 1.00, 1.00, 1.00, 1.00],
            "Referans"),
    ];

    public static IReadOnlyList<AcousticMaterial> Presets { get; } =
        [.. CorePresets, .. MasterMaterialLibraryLoader.Load()];

    public static AcousticMaterial Default => CorePresets[0]; // Çıplak Beton

    public static AcousticMaterial? FindById(string id) =>
        Presets.FirstOrDefault(m => m.Id == id);
}
