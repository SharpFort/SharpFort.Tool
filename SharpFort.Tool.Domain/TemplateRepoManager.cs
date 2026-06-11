using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using Volo.Abp.DependencyInjection;

namespace SharpFort.Tool.Domain;

/// <summary>
/// 模板仓库管理器 — 支持 GitHub API v3
/// 免认证模式: 公开仓库 60次/小时
/// 认证模式: Bearer Token 5000次/小时
/// </summary>
public class TemplateRepoManager : ITransientDependency
{
    private readonly ConfigManager _configManager;
    private readonly IHttpClientFactory _httpClientFactory;

    public TemplateRepoManager(ConfigManager configManager, IHttpClientFactory httpClientFactory)
    {
        _configManager = configManager;
        _httpClientFactory = httpClientFactory;
    }

    private string Host => _configManager.GetConfig().Repo.Host;
    private string Owner => _configManager.GetConfig().Repo.Owner;
    private string RepoName => _configManager.GetConfig().Repo.RepoName;
    private string? AccessToken => _configManager.GetConfig().Repo.AccessToken;

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SharpFort.Tool");
        if (!string.IsNullOrEmpty(AccessToken))
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", AccessToken);
        return client;
    }

    /// <summary>
    /// 检查分支是否存在 (HEAD 请求，轻量)
    /// </summary>
    public async Task<bool> IsExsitBranchAsync(string branch)
    {
        using var client = CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Head,
            $"{Host}/repos/{Owner}/{RepoName}/branches/{branch}");
        var response = await client.SendAsync(request);
        return response.StatusCode != HttpStatusCode.NotFound;
    }

    /// <summary>
    /// 获取所有分支
    /// </summary>
    public async Task<List<string>> GetAllBranchAsync()
    {
        using var client = CreateClient();
        var response = await client.GetAsync(
            $"{Host}/repos/{Owner}/{RepoName}/branches?per_page=100");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync();
        JArray jsonArray = JArray.Parse(result);
        List<string> names = new();
        foreach (JObject obj in jsonArray)
        {
            string? name = obj["name"]?.ToString();
            if (name != null) names.Add(name);
        }
        return names;
    }

    /// <summary>
    /// 下载分支 zip — 返回流 + ETag
    /// </summary>
    public async Task<(Stream stream, string? etag)> DownLoadFileAsync(string branch)
    {
        using var client = CreateClient();
        var response = await client.GetAsync(
            $"{Host}/repos/{Owner}/{RepoName}/zipball/{branch}");
        response.EnsureSuccessStatusCode();
        var etag = response.Headers.ETag?.Tag;
        var stream = await response.Content.ReadAsStreamAsync();
        return (stream, etag);
    }

    /// <summary>
    /// 检查分支是否有更新 (ETag 条件请求)
    /// 返回: true=有更新, false=无变化
    /// </summary>
    public async Task<bool> HasUpdateAsync(string branch, string? etag)
    {
        if (string.IsNullOrEmpty(etag))
            return true; // 无 ETag → 视为有更新

        using var client = CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{Host}/repos/{Owner}/{RepoName}/zipball/{branch}");
        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(etag));
        var response = await client.SendAsync(request);
        return response.StatusCode != HttpStatusCode.NotModified;
    }
}
