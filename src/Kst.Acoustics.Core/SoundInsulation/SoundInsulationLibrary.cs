using System.Collections.Generic;

namespace Kst.Acoustics.Core.SoundInsulation;

public static class SoundInsulationLibrary
{
    public static List<SoundInsulationMaterial> GetBuiltInMaterials()
    {
        var list = new List<SoundInsulationMaterial>
        {
            // --- CONCRETE & MASONRY (ISO 12354-1 Table B.1) ---
            new(id: "CONC_DENSE", name: "Beton (Yoğun/Donatılı)", category: "Masif", subCategory: "Beton", density: 2400, youngModulus: 30e9, poissonRatio: 0.2, lossFactor: 0.01),
            new(id: "CONC_NORMAL", name: "Beton (Normal)", category: "Masif", subCategory: "Beton", density: 2200, youngModulus: 25e9, poissonRatio: 0.2, lossFactor: 0.01),
            new(id: "CONC_LIGHT", name: "Hafif Beton (Pomza/Ekstrüde)", category: "Masif", subCategory: "Beton", density: 1200, youngModulus: 10e9, poissonRatio: 0.2, lossFactor: 0.02),
            new(id: "BRICK_SOLID", name: "Dolu Tuğla (Harçlı)", category: "Masif", subCategory: "Tuğla", density: 1800, youngModulus: 15e9, poissonRatio: 0.2, lossFactor: 0.03),
            new(id: "BRICK_HOLLOW", name: "Delikli Tuğla (Düşey)", category: "Masif", subCategory: "Tuğla", density: 1200, youngModulus: 8e9, poissonRatio: 0.2, lossFactor: 0.04),
            new(id: "SAND_LIME", name: "Kalsiyum Silikat (Kum-Kireç)", category: "Masif", subCategory: "Taş", density: 1900, youngModulus: 18e9, poissonRatio: 0.2, lossFactor: 0.02),
            new(id: "GAS_CONC", name: "Gazbeton (Ytong/Hebel)", category: "Masif", subCategory: "Blok", density: 600, youngModulus: 1.8e9, poissonRatio: 0.2, lossFactor: 0.05),
            new(id: "STONE_NATURAL", name: "Doğal Taş (Kireçtaşı/Mermer)", category: "Masif", subCategory: "Taş", density: 2600, youngModulus: 50e9, poissonRatio: 0.2, lossFactor: 0.005),

            // --- PLASTERS & RENDERS (ISO 12354-1 Table B.1) ---
            new(id: "CEM_PLASTER", name: "Çimento Sıva", category: "Masif", subCategory: "Sıva", density: 1900, youngModulus: 12e9, poissonRatio: 0.2, lossFactor: 0.015),
            new(id: "GYP_PLASTER", name: "Alçı Sıva", category: "Masif", subCategory: "Sıva", density: 1000, youngModulus: 3.5e9, poissonRatio: 0.2, lossFactor: 0.02),
            new(id: "LIME_MORTAR", name: "Kara Sıva (Kireç-Çimento)", category: "Masif", subCategory: "Sıva", density: 1600, youngModulus: 8e9, poissonRatio: 0.2, lossFactor: 0.02),
            new(id: "SATIN_GYP", name: "Saten Alçı (İnce Bitiriş)", category: "Masif", subCategory: "Sıva", density: 800, youngModulus: 2.5e9, poissonRatio: 0.2, lossFactor: 0.03),

            // --- GYPSUM PRODUCTS (EN 520, EN 15283) ---
            new(id: "GYP_STD", name: "Alçı Levha (Tip A)", category: "Levha", subCategory: "Alçı", density: 800, youngModulus: 2.5e9, poissonRatio: 0.3, lossFactor: 0.03, isFavorite: true),
            new(id: "GYP_FIRE", name: "Alçı Levha (Tip F - Yangın)", category: "Levha", subCategory: "Alçı", density: 850, youngModulus: 2.8e9, poissonRatio: 0.3, lossFactor: 0.03),
            new(id: "GYP_WATER", name: "Alçı Levha (Tip H - Suya Dayanıklı)", category: "Levha", subCategory: "Alçı", density: 820, youngModulus: 2.6e9, poissonRatio: 0.3, lossFactor: 0.03),
            new(id: "GYP_SOUND", name: "Akustik Alçı Levha (Yüksek Yoğunluk)", category: "Levha", subCategory: "Alçı", density: 1050, youngModulus: 3.5e9, poissonRatio: 0.3, lossFactor: 0.04, isFavorite: true),
            new(id: "GYP_FIBRE", name: "Alçı Lifli Levha (Fermacell vb.)", category: "Levha", subCategory: "Alçı", density: 1150, youngModulus: 4.5e9, poissonRatio: 0.3, lossFactor: 0.05),
            new(id: "GYP_BLOCK", name: "Alçı Bloğu (Dolu)", category: "Masif", subCategory: "Alçı", density: 900, youngModulus: 4e9, poissonRatio: 0.2, lossFactor: 0.03),

            // --- WOOD BASED PANELS (EN 13986) ---
            new(id: "WOOD_SOFT", name: "Yumuşak Ahşap (Çam/Ladin)", category: "Ahşap", subCategory: "Masif Ahşap", density: 500, youngModulus: 10e9, poissonRatio: 0.3, lossFactor: 0.02),
            new(id: "WOOD_HARD", name: "Sert Ahşap (Meşe/Kayın)", category: "Ahşap", subCategory: "Masif Ahşap", density: 750, youngModulus: 14e9, poissonRatio: 0.3, lossFactor: 0.015),
            new(id: "PLYWOOD", name: "Kontrplak", category: "Ahşap", subCategory: "Panel", density: 600, youngModulus: 7e9, poissonRatio: 0.3, lossFactor: 0.02),
            new(id: "CHIPBOARD", name: "Yonga Levha (Sunta)", category: "Ahşap", subCategory: "Panel", density: 650, youngModulus: 3e9, poissonRatio: 0.3, lossFactor: 0.03),
            new(id: "OSB_BOARD", name: "OSB (Oriented Strand Board)", category: "Ahşap", subCategory: "Panel", density: 650, youngModulus: 4e9, poissonRatio: 0.3, lossFactor: 0.025),
            new(id: "MDF_BOARD", name: "MDF (Medium Density Fibreboard)", category: "Ahşap", subCategory: "Panel", density: 750, youngModulus: 3.8e9, poissonRatio: 0.3, lossFactor: 0.02),
            new(id: "HDF_BOARD", name: "HDF (High Density Fibreboard)", category: "Ahşap", subCategory: "Panel", density: 900, youngModulus: 5e9, poissonRatio: 0.3, lossFactor: 0.015),

            // --- METALS (ISO 12354-1 Table B.1) ---
            new(id: "STEEL", name: "Çelik", category: "Metal", subCategory: "Çelik", density: 7850, youngModulus: 210e9, poissonRatio: 0.3, lossFactor: 0.0001),
            new(id: "ALUMINUM", name: "Alüminyum", category: "Metal", subCategory: "Alüminyum", density: 2700, youngModulus: 70e9, poissonRatio: 0.3, lossFactor: 0.0001),
            new(id: "LEAD", name: "Kurşun (Acoustic Grade)", category: "Metal", subCategory: "Kurşun", density: 11340, youngModulus: 16e9, poissonRatio: 0.4, lossFactor: 0.01),

            // --- GLASS ---
            new(id: "GLASS_FLOAT", name: "Cam (Düz/Float)", category: "Cam", subCategory: "Cam", density: 2500, youngModulus: 70e9, poissonRatio: 0.22, lossFactor: 0.002),
            new(id: "GLASS_LAM", name: "Cam (Lamine/Akustik PVB)", category: "Cam", subCategory: "Cam", density: 2500, youngModulus: 60e9, poissonRatio: 0.22, lossFactor: 0.1),

            // --- INSULATION (POROUS MATERIALS) ---
            new(id: "AIR", name: "Hava Boşluğu", category: "Boşluk", subCategory: "Hava", density: 1.2, youngModulus: 0, poissonRatio: 0, lossFactor: 0, isFavorite: true),
            new(id: "GW_LOW", name: "Cam Yünü (Hafif) 15-20kg", category: "Dolgu", subCategory: "Mineral Yün", density: 18, flowResistivity: 5000, isFavorite: false),
            new(id: "GW_HIGH", name: "Cam Yünü (Yoğun) 30kg+", category: "Dolgu", subCategory: "Mineral Yün", density: 35, flowResistivity: 12000, isFavorite: false),
            new(id: "SW_LIGHT", name: "Taş Yünü (Duvar) 40-50kg", category: "Dolgu", subCategory: "Mineral Yün", density: 50, flowResistivity: 18000, isFavorite: true),
            new(id: "SW_DENSE", name: "Taş Yünü (Zemin) 100kg+", category: "Dolgu", subCategory: "Mineral Yün", density: 110, flowResistivity: 45000, isFavorite: false),
            new(id: "EPS_30", name: "EPS (Genleştirilmiş Polistiren)", category: "Dolgu", subCategory: "Polimer", density: 30, flowResistivity: 2500, isFavorite: false),
            new(id: "XPS_40", name: "XPS (Ekstrüde Polistiren)", category: "Dolgu", subCategory: "Polimer", density: 40, flowResistivity: 6000, isFavorite: false),
            new(id: "WOOL_FIBRE", name: "Doğal Lifler (Koyun Yünü/Kenevir)", category: "Dolgu", subCategory: "Doğal Lif", density: 40, flowResistivity: 10000, isFavorite: false),

            new(id: "RUBBER", name: "Kauçuk (Damping Layer)", category: "Kauçuk", subCategory: "Polimer", density: 1200, youngModulus: 0.1e9, poissonRatio: 0.45, lossFactor: 0.5),
            new(id: "ASPHALT", name: "Asfalt / Bitüm", category: "Masif", subCategory: "Diğer", density: 2100, youngModulus: 2e9, poissonRatio: 0.35, lossFactor: 0.1)
        };

        foreach (var m in list) m.IsBuiltIn = true;
        return list;
    }

    public static List<WallAssembly> GetSampleAssemblies()
    {
        var materials = GetBuiltInMaterials();
        var gypStd = materials.Find(m => m.Id == "GYP_STD")!;
        var gypSound = materials.Find(m => m.Id == "GYP_SOUND")!;
        var air = materials.Find(m => m.Id == "AIR")!;
        var swLight = materials.Find(m => m.Id == "SW_LIGHT")!;
        var conc = materials.Find(m => m.Id == "CONC_NORMAL")!;
        var cemp = materials.Find(m => m.Id == "CEM_PLASTER")!;
        var gypp = materials.Find(m => m.Id == "GYP_PLASTER")!;
        var brick = materials.Find(m => m.Id == "BRICK_HOLLOW")!;

        return new List<WallAssembly>
        {
            new("ISO_01", "20cm Beton Duvar (Çift Yüz Sıvalı)", new List<SoundInsulationLayer>
            {
                new(cemp, 0.020),
                new(conc, 0.20),
                new(gypp, 0.015)
            }, "Literatür tabanlı donatılı beton perde örneği."),

            new("ISO_02", "Delikli Tuğla Duvar (13.5cm + Sıva)", new List<SoundInsulationLayer>
            {
                new(gypp, 0.020),
                new(brick, 0.135),
                new(gypp, 0.020)
            }, "Geleneksel bölme duvar (Tuğla + Kireç/Alçı Sıva)."),

            new("ISO_03", "Hafif Bölme (Akustik Alçıpan 75mm)", new List<SoundInsulationLayer>
            {
                new(gypSound, 0.0125),
                new(swLight, 0.050, IsAirGap: true, HasPorousFill: true),
                new(gypSound, 0.0125)
            }, "Taş yünü dolgulu yüksek performanslı alçıpan sistem."),

            new("ISO_04", "Çift İskeletli Stüdyo Duvarı", new List<SoundInsulationLayer>
            {
                new(gypSound, 0.015),
                new(gypSound, 0.015),
                new(swLight, 0.100, IsAirGap: true, HasPorousFill: true),
                new(gypSound, 0.015),
                new(gypSound, 0.015)
            }, "Maksimum yalıtım için tasarlanmış bağımsız çift karkaslı duvar."),

            new("TMM_01", "Çift Cam (IGU 4-12-4)", new List<SoundInsulationLayer>
            {
                new(materials.Find(m => m.Id == "GLASS_FLOAT")!, 0.004),
                new(air, 0.012, IsAirGap: true, HasPorousFill: false),
                new(materials.Find(m => m.Id == "GLASS_FLOAT")!, 0.004)
            }, "TMM doğrulama: standart yalıtım camı, hava boşluğu kayıpsız.")
            { Method = CalculationMethod.TMM },

            new("TMM_02", "Çift Alçıpan + 50mm Taş Yünü", new List<SoundInsulationLayer>
            {
                new(gypStd, 0.0125),
                new(gypStd, 0.0125),
                new(swLight, 0.050, IsAirGap: true, HasPorousFill: true),
                new(gypStd, 0.0125),
                new(gypStd, 0.0125)
            }, "TMM doğrulama: lamine alçıpan + JCA poröz dolgu çift cidar.")
            { Method = CalculationMethod.TMM },

            new("TMM_03", "Beton + 50mm Taş Yünü + Alçıpan Kaplama", new List<SoundInsulationLayer>
            {
                new(conc, 0.200),
                new(swLight, 0.050, IsAirGap: true, HasPorousFill: true),
                new(gypSound, 0.0125)
            }, "TMM doğrulama: yüksek kütleli ana yüzey + ince giydirme; rezonans hassasiyeti.")
            { Method = CalculationMethod.TMM },

            new("TMM_04", "25cm Betonarme Duvar", new List<SoundInsulationLayer>
            {
                new(materials.Find(m => m.Id == "CONC_DENSE")!, 0.250)
            }, "TMM doğrulama: kalın betonarme; katı tabaka (P+S dalga) modeliyle kayma/kalınlık etkisi.")
            { Method = CalculationMethod.TMM }
        };
    }
}
