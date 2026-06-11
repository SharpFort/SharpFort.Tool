using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using SharpFort.Tool.Domain.Shared.Dtos;
using SharpFort.Tool.Domain.Shared.Options;

namespace SharpFort.Tool.Domain;

public class TemplateGenManager : ITransientDependency
{
    private readonly ToolOptions _toolOptions;
    private readonly TemplateRepoManager _repoManager;

    public TemplateGenManager(
        IOptionsMonitor<ToolOptions> toolOptions,
        TemplateRepoManager repoManager)
    {
        _repoManager = repoManager;
        _toolOptions = toolOptions.CurrentValue;
    }

    // ==================== 缓存相关 ====================

    private string GetCacheDir()
    {
        var config = new ConfigManager().GetConfig();
        return config.Tool.CacheDirPath;
    }

    private string GetCachePath(string branch) =>
        Path.Combine(GetCacheDir(), $"{branch}.zip");

    private string GetMetaPath(string branch) =>
        Path.Combine(GetCacheDir(), $"{branch}.meta.json");

    private CacheMeta? ReadCacheMeta(string branch)
    {
        var metaPath = GetMetaPath(branch);
        if (!File.Exists(metaPath)) return null;
        var json = File.ReadAllText(metaPath);
        return JsonSerializer.Deserialize<CacheMeta>(json);
    }

    private void WriteCacheMeta(string branch, string? etag)
    {
        var meta = new CacheMeta
        {
            Branch = branch,
            DownloadedAt = DateTime.UtcNow,
            ETag = etag
        };
        var json = JsonSerializer.Serialize(meta);
        File.WriteAllText(GetMetaPath(branch), json);
    }

    // ==================== 模板获取 ====================

    /// <summary>
    /// 获取模板流 — 优先缓存，支持 ETag 增量更新
    /// </summary>
    private async Task<Stream> GetTemplateStreamAsync(string branch, bool noCache = false)
    {
        var cachePath = GetCachePath(branch);

        if (!noCache && File.Exists(cachePath))
        {
            var meta = ReadCacheMeta(branch);
            if (meta?.ETag != null)
            {
                // ETag 条件检查
                bool hasUpdate = await _repoManager.HasUpdateAsync(branch, meta.ETag);
                if (!hasUpdate)
                {
                    // 无更新，用缓存，只更新时间戳
                    WriteCacheMeta(branch, meta.ETag);
                    return File.OpenRead(cachePath);
                }
            }
        }

        // 下载新版本
        var (stream, etag) = await _repoManager.DownLoadFileAsync(branch);

        // 保存到缓存
        Directory.CreateDirectory(GetCacheDir());
        using (var fs = new FileStream(cachePath, FileMode.Create))
        {
            await stream.CopyToAsync(fs);
        }
        WriteCacheMeta(branch, etag);

        return File.OpenRead(cachePath);
    }

    // ==================== 模板生成 ====================

    public async Task<string> CreateTemplateAsync(TemplateGenCreateDto input)
    {
        if (!await _repoManager.IsExsitBranchAsync(input.GiteeRef))
            throw new UserFriendlyException($"分支未找到 [{input.GiteeRef}]，请检查分支是否存在");

        if (string.IsNullOrEmpty(_toolOptions.TempDirPath))
            throw new UserFriendlyException($"临时目录路径未配置");

        var id = Guid.NewGuid().ToString("N");
        var tempFileDirPath = Path.Combine(_toolOptions.TempDirPath, id);
        Directory.CreateDirectory(tempFileDirPath);

        // 获取模板流 (缓存 + ETag)
        using var gitStream = await GetTemplateStreamAsync(input.GiteeRef, input.NoCache);
        var downloadFilePath = Path.Combine(_toolOptions.TempDirPath, $"{id}.zip");
        using (var fs = new FileStream(downloadFilePath, FileMode.Create))
        {
            await gitStream.CopyToAsync(fs);
        }

        // 解压
        ZipFile.ExtractToDirectory(downloadFilePath, tempFileDirPath, true);
        File.Delete(downloadFilePath);

        // zip 内多一层目录
        var operPath = Directory.GetDirectories(tempFileDirPath)[0];

        // 替换内容
        await ReplaceContentAsync(operPath, input.ReplaceStrData);

        // 重新打包
        var tempFilePath = Path.Combine(_toolOptions.TempDirPath, $"{id}.zip");
        ZipFile.CreateFromDirectory(operPath, tempFilePath);
        Directory.Delete(tempFileDirPath, true);

        return tempFilePath;
    }

    /// <summary>
    /// 获取模板流用于预览（使用缓存）
    /// </summary>
    public async Task<Stream> GetTemplateStreamForPreviewAsync(string branch)
    {
        if (!await _repoManager.IsExsitBranchAsync(branch))
            throw new UserFriendlyException($"分支 [{branch}] 不存在");
        return await GetTemplateStreamAsync(branch);
    }

    public async Task<List<string>> GetAllTemplatesAsync()
    {
        var refs = await _repoManager.GetAllBranchAsync();
        refs.Remove("master");
        refs.Remove("main");
        return refs;
    }

    // ==================== 内容替换 ====================

    private async Task ReplaceContentAsync(string rootDirectory, Dictionary<string, string> dic)
    {
        foreach (var entry in dic)
            await ReplaceInDirectory(rootDirectory, entry.Key, entry.Value);

        static async Task ReplaceInDirectory(string dirPath, string search, string replace)
        {
            var newDirPath = await ReplaceInFiles(dirPath, search, replace);
            foreach (string subDir in Directory.GetDirectories(newDirPath))
                await ReplaceInDirectory(subDir, search, replace);
        }

        static async Task<string> ReplaceInFiles(string dirPath, string search, string replace)
        {
            string dirName = new DirectoryInfo(dirPath).Name;
            string newDirName = dirName.Replace(search, replace);
            if (dirName != newDirName)
            {
                string parent = Path.GetDirectoryName(dirPath)!;
                string newPath = Path.Combine(parent, newDirName);
                Directory.Move(dirPath, newPath);
                dirPath = newPath;
            }

            foreach (string file in Directory.GetFiles(dirPath))
            {
                string newFileName = file.Replace(search, replace);
                if (file != newFileName)
                    File.Move(file, newFileName);
            }

            foreach (string file in Directory.GetFiles(dirPath))
            {
                string content = await File.ReadAllTextAsync(file);
                string newContent = content.Replace(search, replace);
                await File.WriteAllTextAsync(file, newContent);
            }

            return dirPath;
        }
    }
}

/// <summary>
/// 缓存元数据
/// </summary>
public class CacheMeta
{
    public string Branch { get; set; } = "";
    public DateTime DownloadedAt { get; set; }
    public string? ETag { get; set; }
}
