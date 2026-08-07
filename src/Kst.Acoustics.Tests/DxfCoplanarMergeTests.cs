using Kst.Acoustics.Core.Importing;

namespace Kst.Acoustics.Tests;

/// <summary>
/// Coplanar yüzey birleştirme: kenar/köşe eşleştirme toleransı artık proje birimine göre
/// ayarlanıyor (önceden sabit 3 ondalık — mm projesinde 1 mikron, gerçekçi kaynaşmaları
/// ayırıyordu). Ayrıca T-junction'da (üç yüzeyin ortak köşesi) sınır grafiği artık
/// adjacency list kullanıyor, tekil hedef bir kenarı sessizce ezmiyor
/// (maincodex M01 incelemesi P2).
/// </summary>
public static class DxfCoplanarMergeTests
{
    public static void MillimeterUnitFloatingNoiseStillMergesCoplanarFaces()
    {
        // İki bitişik dikdörtgen, paylaşılan kenarın köşelerinde ~0.3mm gerçekçi kayma
        // ile (DXF dışa aktarımından kalma yuvarlama gürültüsünü taklit eder).
        // Eski sabit 3-ondalık tolerans (mm biriminde 1 mikron) bunu birleştiremezdi;
        // yeni birim-duyarlı tolerans (~1mm) birleştirmeli → tek 4 köşeli dikdörtgen.
        var importer = new SurfaceImportService();
        var result = importer.Parse(BuildTwoAdjacentMmFaces(edgeOffsetMm: 0.3));

        var surfaces = result.Document.Surfaces;
        AssertEqual(1, surfaces.Count, "İki bitişik yüzey tek yüzeyde birleşmeli.");
        AssertEqual(4, surfaces[0].Vertices.Count,
            "Birleşmiş dikdörtgenin ortak kenarındaki doğrusal köşeler kaldırılıp 4 köşe kalmalı.");
    }

    public static void DiagonallyTouchingSquaresSharingOnlyACornerStayAsTwoSurfaces()
    {
        // İki kare yalnızca TEK bir köşe noktasında temas ediyor (kenar paylaşmıyor) —
        // ör. çapraz döşenmiş kare desenli zemin/oda köşeleri. O ortak köşeden HER İKİ
        // karenin de birer sınır kenarı çıkar. Eski Dictionary<string,string> bunlardan
        // birini sessizce ezip bir halkayı bozardı; adjacency list ikisini de korumalı →
        // sonuç iki ayrı, bozulmamış 4 köşeli kare olmalı.
        var importer = new SurfaceImportService();
        var result = importer.Parse(BuildTwoSquaresTouchingAtOneCorner());

        var surfaces = result.Document.Surfaces;
        AssertEqual(2, surfaces.Count, "Yalnızca köşeden temas eden iki kare birleştirilmemeli, iki ayrı yüzey kalmalı.");
        foreach (var s in surfaces)
        {
            AssertEqual(4, s.Vertices.Count, $"Yüzey '{s.Name}' 4 köşeli kalmalı (bozulmamış).");
        }
    }

    private static string BuildTwoSquaresTouchingAtOneCorner()
    {
        // Kare A: (0,0)-(1,0)-(1,1)-(0,1)   Kare D: (1,1)-(2,1)-(2,2)-(1,2)
        // İkisi yalnızca (1,1) noktasında temas eder, kenar paylaşmazlar.
        return """
0
SECTION
2
ENTITIES
0
3DFACE
10
0
20
0
30
0
11
1
21
0
31
0
12
1
22
1
32
0
13
0
23
1
33
0
0
3DFACE
10
1
20
1
30
0
11
2
21
1
31
0
12
2
22
2
32
0
13
1
23
2
33
0
0
ENDSEC
0
EOF
""";
    }

    private static string BuildTwoAdjacentMmFaces(double edgeOffsetMm)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        string F(double v) => v.ToString(culture);

        // Dikdörtgen 1: (0,0)-(1000,0)-(1000,1000)-(0,1000)
        // Dikdörtgen 2: (1000,0)-(2000,0)-(2000,1000)-(1000,1000) — paylaşılan kenar x≈1000
        // Yüzey 2'nin paylaşılan köşeleri hafifçe kaydırılmış (edgeOffsetMm).
        return $$"""
0
SECTION
2
HEADER
9
$INSUNITS
70
4
0
ENDSEC
0
SECTION
2
ENTITIES
0
3DFACE
10
0
20
0
30
0
11
1000
21
0
31
0
12
1000
22
1000
32
0
13
0
23
1000
33
0
0
3DFACE
10
{{F(1000 + edgeOffsetMm)}}
20
0
30
0
11
2000
21
0
31
0
12
2000
22
1000
32
0
13
{{F(1000 + edgeOffsetMm)}}
23
1000
33
0
0
ENDSEC
0
EOF
""";
    }

    private static void AssertEqual(int expected, int actual, string message)
    {
        if (expected != actual)
            throw new InvalidOperationException($"{message} (beklenen {expected}, gerçek {actual}).");
    }
}
