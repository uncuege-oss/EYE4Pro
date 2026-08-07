namespace Kst.Acoustics.Core.Importing;

internal static class SurfaceImportText
{
    public static List<string> SplitLines(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .ToList();
    }
}
