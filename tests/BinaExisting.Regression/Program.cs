using System.Reflection;
using Kst.Acoustics.Tests;

int passed = 0, failed = 0;
foreach (var type in new[] { typeof(BinaGeometriTests), typeof(CokluKatTests), typeof(LayerPersistenceTests), typeof(RescanMirrorTests) })
{
    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Where(m => m.ReturnType == typeof(void) && m.GetParameters().Length == 0)
        .OrderBy(m => m.MetadataToken);
    foreach (var method in methods)
    {
        try
        {
            method.Invoke(null, null);
            passed++;
            Console.WriteLine($"PASS {type.Name}.{method.Name}");
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"FAIL {type.Name}.{method.Name}: {ex.InnerException ?? ex}");
        }
    }
}
Console.WriteLine($"{passed}/{passed + failed} passed, {failed} failed. Optional fixture tests may report SKIP above.");
return failed == 0 ? 0 : 1;
