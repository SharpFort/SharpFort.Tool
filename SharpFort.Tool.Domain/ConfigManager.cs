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

    public static string GetConfigPath() => ConfigPath;
    public static string GetConfigDir() => ConfigDir;

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

/// <summary>
/// SharpFort CLI 全局配置
/// </summary>
public class SharpFortConfig
{
    /// <summary>
    /// 模板仓库配置 (Primary + Fallback 双源)
    /// </summary>
    public RepoConfig Repo { get; set; } = new();

    /// <summary>
    /// 工具目录配置
    /// </summary>
    public ToolConfig Tool { get; set; } = new();

    /// <summary>
    /// 克隆地址配置 (Primary + Fallback 双源)
    /// </summary>
    public CloneConfig Clone { get; set; } = new();

    /// <summary>
    /// 默认模板分支
    /// </summary>
    public string DefaultTemplateBranch { get; set; } = "main";

    public static SharpFortConfig CreateDefault() => new()
    {
        Repo = new RepoConfig
        {
            Primary = new RepoSource
            {
                Host = "https://api.github.com",
                Owner = "SharpFort",
                RepoName = "SharpFort.Template"
            },
            Fallback = new RepoSource
            {
                Host = "https://gitee.com/api/v5",
                Owner = "SharpFort",
                RepoName = "SharpFort.Template"
            },
            AccessToken = ""
        },
        Tool = new ToolConfig
        {
            TempDirPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sharpfort", "temp"),
            CacheDirPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sharpfort", "cache")
        },
        Clone = new CloneConfig
        {
            Primary = "https://github.com/SharpFort/SharpFort.Net",
            Fallback = "https://gitee.com/SharpFort/SharpFort.Net"
        },
        DefaultTemplateBranch = "main"
    };
}

/// <summary>
/// 模板仓库配置 — 主备双源
/// </summary>
public class RepoConfig
{
    /// <summary>
    /// 主源 (默认 GitHub)
    /// </summary>
    public RepoSource Primary { get; set; } = new();

    /// <summary>
    /// 备用源 (默认 Gitee)
    /// </summary>
    public RepoSource Fallback { get; set; } = new();

    /// <summary>
    /// 访问令牌 (可选，GitHub Bearer Token / Gitee access_token)
    /// </summary>
    public string AccessToken { get; set; } = "";
}

/// <summary>
/// 单个仓库源配置
/// </summary>
public class RepoSource
{
    /// <summary>
    /// API 主机地址 (如 https://api.github.com 或 https://gitee.com/api/v5)
    /// </summary>
    public string Host { get; set; } = "";

    /// <summary>
    /// 仓库所有者/组织名
    /// </summary>
    public string Owner { get; set; } = "";

    /// <summary>
    /// 仓库名称
    /// </summary>
    public string RepoName { get; set; } = "";

    /// <summary>
    /// 生成完整的仓库 API 基础 URL
    /// </summary>
    public string GetRepoUrl() => $"{Host}/repos/{Owner}/{RepoName}";

    /// <summary>
    /// 显示友好的源标识
    /// </summary>
    public string GetSourceName()
    {
        if (Host.Contains("github")) return "GitHub";
        if (Host.Contains("gitee")) return "Gitee";
        return Host;
    }
}

/// <summary>
/// 克隆地址配置 — 主备双源
/// </summary>
public class CloneConfig
{
    /// <summary>
    /// 主源克隆地址 (默认 GitHub)
    /// </summary>
    public string Primary { get; set; } = "https://github.com/SharpFort/SharpFort.Net";

    /// <summary>
    /// 备用源克隆地址 (默认 Gitee)
    /// </summary>
    public string Fallback { get; set; } = "https://gitee.com/SharpFort/SharpFort.Net";
}

/// <summary>
/// 工具目录配置
/// </summary>
public class ToolConfig
{
    public string TempDirPath { get; set; } = "";
    public string CacheDirPath { get; set; } = "";
}
