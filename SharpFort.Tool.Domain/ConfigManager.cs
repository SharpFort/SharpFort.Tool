using System.Text.Json;
using Volo.Abp.DependencyInjection;

namespace SharpFort.Tool.Domain;

public class ConfigManager : ISingletonDependency
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sharpfort");
    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    private SharpFortConfig _config;

    public ConfigManager()
    {
        EnsureConfigExists();
        LoadConfig();
    }

    public SharpFortConfig GetConfig() => _config;

    public void SaveConfig(SharpFortConfig config)
    {
        _config = config;
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    private void EnsureConfigExists()
    {
        if (!Directory.Exists(ConfigDir))
            Directory.CreateDirectory(ConfigDir);
        if (!File.Exists(ConfigPath))
        {
            var json = JsonSerializer.Serialize(SharpFortConfig.CreateDefault(),
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
    }

    private void LoadConfig()
    {
        var json = File.ReadAllText(ConfigPath);
        _config = JsonSerializer.Deserialize<SharpFortConfig>(json) ?? SharpFortConfig.CreateDefault();
    }
}

public class SharpFortConfig
{
    public RepoConfig Repo { get; set; } = new();
    public ToolConfig Tool { get; set; } = new();
    public string CloneAddress { get; set; } = "https://github.com/SharpFort/SharpFort.Net";
    public string DefaultTemplateBranch { get; set; } = "main";

    public static SharpFortConfig CreateDefault() => new()
    {
        Repo = new RepoConfig
        {
            Host = "https://api.github.com",
            Owner = "SharpFort",
            RepoName = "SharpFort.Template",
            AccessToken = ""
        },
        Tool = new ToolConfig
        {
            TempDirPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sharpfort", "temp"),
            CacheDirPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sharpfort", "cache")
        },
        CloneAddress = "https://github.com/SharpFort/SharpFort.Net",
        DefaultTemplateBranch = "main"
    };
}

public class RepoConfig
{
    public string Host { get; set; } = "https://api.github.com";
    public string Owner { get; set; } = "";
    public string RepoName { get; set; } = "";
    public string AccessToken { get; set; } = "";
}

public class ToolConfig
{
    public string TempDirPath { get; set; } = "";
    public string CacheDirPath { get; set; } = "";
}
