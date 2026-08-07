using System.IO;

namespace Kst.Acoustics.Metraj.Rules;

public static class RuleSetStore
{
    public static string GetConfigPath(string dwgPath)
    {
        string dir = Path.GetDirectoryName(dwgPath) ?? string.Empty;
        string fileName = Path.GetFileNameWithoutExtension(dwgPath);
        return Path.Combine(dir, $"{fileName}.metraj-rules.json");
    }

    public static void Save(string dwgPath, RuleSet ruleSet)
    {
        string path = GetConfigPath(dwgPath);
        File.WriteAllText(path, ruleSet.ToJson());
    }

    public static RuleSet Load(string dwgPath)
    {
        // Kayıt stratejisi: <proje-adı>.metraj-rules.json (Sidecar file)
        string path = GetConfigPath(dwgPath);
        if (!File.Exists(path)) return new RuleSet();

        string json = File.ReadAllText(path);
        return RuleSet.FromJson(json) ?? new RuleSet();
    }
}
