using Kst.Acoustics.App.ViewModels;

namespace Kst.Acoustics.Tests;

/// <summary>
/// DXF parser'ın ürettiği uyarılar (ör. bulge/yay düz çizgiye dönüştürülmesi) önceden
/// SurfaceImportDocument.Warnings'te toplanıp hiçbir yerde gösterilmiyordu; kullanıcı
/// geometrinin sadeleştirildiğini öğrenmiyordu. Artık MainWindowViewModel bunları
/// ImportWarnings/ImportWarningsSummary üzerinden kalıcı bir panelde sunuyor
/// (maincodex M01 incelemesi P2).
/// </summary>
public static class MainWindowViewModelImportWarningsTests
{
    public static void ImportingDxfWithBulgeArcPopulatesImportWarnings()
    {
        var path = Path.Combine(Path.GetTempPath(), $"import-warnings-test-{Guid.NewGuid():N}.dxf");
        File.WriteAllText(path, BuildLwpolylineWithBulgeDxf());
        try
        {
            var vm = new MainWindowViewModel();
            vm.ImportAsync(path).GetAwaiter().GetResult();

            if (vm.ImportWarnings.Count == 0)
                throw new InvalidOperationException("Bulge içeren LWPOLYLINE import uyarısı üretmeli.");
            if (string.IsNullOrEmpty(vm.ImportWarningsSummary))
                throw new InvalidOperationException("ImportWarningsSummary boş olmamalı.");
            if (!vm.ImportWarningsSummary.Contains("bulge", StringComparison.OrdinalIgnoreCase) &&
                !vm.ImportWarningsSummary.Contains("yay", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Özet bulge/yay uyarısını içermeli: '{vm.ImportWarningsSummary}'");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public static void ImportingCleanDxfLeavesImportWarningsEmpty()
    {
        var path = Path.Combine(Path.GetTempPath(), $"import-warnings-clean-test-{Guid.NewGuid():N}.dxf");
        File.WriteAllText(path, BuildCleanQuadDxf());
        try
        {
            var vm = new MainWindowViewModel();
            vm.ImportAsync(path).GetAwaiter().GetResult();

            if (vm.ImportWarnings.Count != 0)
                throw new InvalidOperationException(
                    $"Temiz bir DXF için ImportWarnings boş olmalı (gerçek: {vm.ImportWarnings.Count}).");
            if (!string.IsNullOrEmpty(vm.ImportWarningsSummary))
                throw new InvalidOperationException("Temiz bir DXF için ImportWarningsSummary boş olmalı.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static string BuildLwpolylineWithBulgeDxf() => """
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
LWPOLYLINE
90
4
70
1
10
0
20
0
42
0.5
10
100
20
0
10
100
20
100
10
0
20
100
0
ENDSEC
0
EOF
""";

    private static string BuildCleanQuadDxf() => """
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
ENDSEC
0
EOF
""";
}
