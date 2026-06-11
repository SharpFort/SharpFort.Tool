using System.Text.Json;
using Volo.Abp.DependencyInjection;

namespace SharpFort.Tool.Domain;

/// <summary>
/// 统一配置管理器
/// 配置文件路径: ~/.sharpfort/config.json
/// </summary>
public class ConfigManager : ISingletonDependency
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".sharpfort");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    private SharpFortConfig _config;

    public ConfigManager()
    {
        EnsureConfigExists();
        LoadConfig();
    }

    /// <summary>
    /// 获取当前配置
    /// </summary>
    public SharpFortConfig GetConfig() => _config;

    /// <summary>
    /// 保存配置到文件
    /// </summary>
    public void SaveConfig(SharpFortConfig config)
    {
        _config = config;
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    /// <summary>
    /// 确保配置目录和文件存在
    /// </summary>
    private void EnsureConfigExists()
    {
        if (!Directory.Exists(ConfigDir))
            Directory.CreateDirectory(ConfigDir);

        if (!File.Exists(ConfigPath))
        {
            var defaultConfig = SharpFortConfig.CreateDefault();
            var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
    }

    /// <summary>
    /// 从文件加载配置
    /// </summary>
    private void LoadConfig()
    {
        var json = File.ReadAllText(ConfigPath);
        _config = JsonSerializer.Deserialize<SharpFortConfig>(json) ?? SharpFortConfig.CreateDefault();
    }
}

/// <summary>
/// SharpFort 全局配置
/// </summary>
public class SharpFortConfig
{
    public GiteeConfig Gitee { get; set; } = new();
    public ToolConfig Tool { get; set; } = new();
    public string CloneAddress { get; set; } = "https://github.com/SharpFort/SharpFort.Tool";
    public string DefaultTemplateBranch { get; set; } = "default";

    public static SharpFortConfig CreateDefault() => new()
    {
        Gitee = new GiteeConfig
        {
            Host = "https://gitee.com/api/v5",
            Owner = "sunshang-hl",
            Repo = "yi-template",
            AccessToken = ""
        },
        Tool = new ToolConfig
        {
            TempDirPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".sharpfort", "temp"),
            CacheDirPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".sharpfort", "cache")
        },
        CloneAddress = "https://github.com/SharpFort/SharpFort.Tool",
        DefaultTemplateBranch = "default"
    };
}

public class GiteeConfig
{
    public string Host { get; set; } = "https://gitee.com/api/v5";
    public string Owner { get; set; } = "";
    public string Repo { get; set; } = "";
    public string AccessToken { get; set; } = "";
}

public class ToolConfig
{
    public string TempDirPath { get; set; } = "";
    public string CacheDirPath { get; set; } = "";
}
