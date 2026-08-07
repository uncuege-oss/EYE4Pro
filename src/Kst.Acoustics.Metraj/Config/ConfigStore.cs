using System.Text.Json;

namespace Kst.Acoustics.Metraj.Config;

public class AppConfig
{
    public string? OdaConverterPath { get; set; }
}

public static class ConfigStore
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
        ".kst-metraj");
    
    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    public static AppConfig Load()
    {
        if (!File.Exists(ConfigPath)) return new AppConfig();
        try
        {
            string json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
        catch { return new AppConfig(); }
    }

    public static void Save(AppConfig config)
    {
        if (!Directory.Exists(ConfigDir)) Directory.CreateDirectory(ConfigDir);
        string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }
}
