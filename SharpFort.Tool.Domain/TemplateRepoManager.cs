using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Volo.Abp.DependencyInjection;

namespace SharpFort.Tool.Domain;

/// <summary>
/// 模板仓库管理器 — 支持 GitHub/Gitee 双源自动切换
/// 免认证模式: GitHub 60次/小时, Gitee 60次/小时
/// 认证模式: GitHub Bearer Token 5000次/小时, Gitee access_token 5000次/小时
/// </summary>
public class TemplateRepoManager : ITransientDependency
{
    private readonly ConfigManager _configManager;
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// 当前活跃的源（首次成功请求后记忆，避免反复尝试）
    /// </summary>
    private RepoSource? _activeSource;

    public TemplateRepoManager(ConfigManager configManager, IHttpClientFactory httpClientFactory)
    {
        _configManager = configManager;
        _httpClientFactory = httpClientFactory;
    }

    private RepoConfig RepoConfig => _configManager.GetConfig().Repo;

    /// <summary>
    /// 获取所有可用源（按优先级排列）
    /// </summary>
    private IEnumerable<RepoSource> GetSources()
    {
        var config = RepoConfig;
        if (_activeSource != null)
        {
            // 优先使用上次成功的源
            yield return _activeSource;
            if (_activeSource == config.Primary && IsConfigured(config.Fallback))
                yield return config.Fallback;
            else if (_activeSource == config.Fallback && IsConfigured(config.Primary))
                yield return config.Primary;
        }
        else
        {
            if (IsConfigured(config.Primary)) yield return config.Primary;
            if (IsConfigured(config.Fallback)) yield return config.Fallback;
        }
    }

    private static bool IsConfigured(RepoSource source) =>
        !string.IsNullOrEmpty(source.Host) &&
        !string.IsNullOrEmpty(source.Owner) &&
        !string.IsNullOrEmpty(source.RepoName);

    private HttpClient CreateClient(RepoSource source)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SharpFort.Tool");

        var token = RepoConfig.AccessToken;
        if (!string.IsNullOrEmpty(token))
        {
            if (source.Host.Contains("gitee"))
            {
                // Gitee 使用 query parameter 认证
                // token 会在 URL 拼接时处理
            }
            else
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }
        }
        return client;
    }

    /// <summary>
    /// 拼接 Gitee 的 access_token 参数（仅 Gitee 使用）
    /// </summary>
    private string AppendToken(string url, RepoSource source)
    {
        var token = RepoConfig.AccessToken;
        if (!string.IsNullOrEmpty(token) && source.Host.Contains("gitee"))
        {
            var separator = url.Contains('?') ? '&' : '?';
            return $"{url}{separator}access_token={token}";
        }
        return url;
    }

    /// <summary>
    /// 双源自动切换：尝试执行操作，主源失败自动切换备用源
    /// </summary>
    private async Task<T> ExecuteWithFallbackAsync<T>(
        Func<RepoSource, HttpClient, Task<T>> operation,
        string operationName)
    {
        Exception? lastException = null;

        foreach (var source in GetSources())
        {
            try
            {
                using var client = CreateClient(source);
                var result = await operation(source, client);
                _activeSource = source;
                return result;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                lastException = ex;
                Console.WriteLine($"  [{source.GetSourceName()}] {operationName} 失败: {ex.Message}");

                // 还有下一个源可用
                Console.WriteLine($"  正在切换到备用源...");
            }
        }

        throw new UserFriendlyException(
            $"所有源均不可用，{operationName}失败。请运行 `sharpfort doctor` 进行环境诊断。\n" +
            $"最后错误: {lastException?.Message}");
    }

    // ==================== 公共 API ====================

    /// <summary>
    /// 检查分支是否存在 (HEAD 请求，轻量)
    /// </summary>
    public async Task<bool> BranchExistsAsync(string branch)
    {
        return await ExecuteWithFallbackAsync(async (source, client) =>
        {
            var url = AppendToken($"{source.GetRepoUrl()}/branches/{branch}", source);
            var request = new HttpRequestMessage(HttpMethod.Head, url);
            var response = await client.SendAsync(request);
            return response.StatusCode != HttpStatusCode.NotFound;
        }, "检查分支");
    }

    /// <summary>
    /// 获取所有分支名称列表
    /// </summary>
    public async Task<List<string>> GetAllBranchAsync()
    {
        return await ExecuteWithFallbackAsync(async (source, client) =>
        {
            var url = AppendToken($"{source.GetRepoUrl()}/branches?per_page=100", source);
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(result);
            var names = new List<string>();
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.TryGetProperty("name", out var nameProp))
                {
                    var name = nameProp.GetString();
                    if (name != null) names.Add(name);
                }
            }
            return names;
        }, "获取分支列表");
    }

    /// <summary>
    /// 下载分支 zip — 返回流 + ETag
    /// </summary>
    public async Task<(Stream stream, string? etag)> DownLoadFileAsync(string branch)
    {
        return await ExecuteWithFallbackAsync(async (source, client) =>
        {
            var url = AppendToken($"{source.GetRepoUrl()}/zipball/{branch}", source);
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var etag = response.Headers.ETag?.Tag;
            var stream = await response.Content.ReadAsStreamAsync();
            return (stream, etag);
        }, "下载模板");
    }

    /// <summary>
    /// 检查分支是否有更新 (ETag 条件请求)
    /// 返回: true=有更新, false=无变化
    /// </summary>
    public async Task<bool> HasUpdateAsync(string branch, string? etag)
    {
        if (string.IsNullOrEmpty(etag))
            return true;

        return await ExecuteWithFallbackAsync(async (source, client) =>
        {
            var url = AppendToken($"{source.GetRepoUrl()}/zipball/{branch}", source);
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(etag));
            var response = await client.SendAsync(request);
            return response.StatusCode != HttpStatusCode.NotModified;
        }, "检查更新");
    }

    // ==================== 诊断 API（供 doctor 命令使用）====================

    /// <summary>
    /// 测试指定源的连通性
    /// </summary>
    public async Task<(bool success, string message)> TestSourceAsync(RepoSource source)
    {
        try
        {
            using var client = CreateClient(source);
            client.Timeout = TimeSpan.FromSeconds(10);
            var url = AppendToken(source.GetRepoUrl(), source);
            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
                return (true, $"连接成功 ({(int)response.StatusCode})");
            if (response.StatusCode == HttpStatusCode.NotFound)
                return (false, $"仓库不存在 (404)");
            if (response.StatusCode == HttpStatusCode.Forbidden)
                return (false, $"访问被拒绝 (403)，可能需要配置 Token");

            return (false, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }
        catch (TaskCanceledException)
        {
            return (false, "连接超时 (10s)");
        }
        catch (HttpRequestException ex)
        {
            return (false, $"网络错误: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, $"未知错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 测试指定源的分支可用性
    /// </summary>
    public async Task<(bool success, string message)> TestBranchAsync(RepoSource source, string branch)
    {
        try
        {
            using var client = CreateClient(source);
            client.Timeout = TimeSpan.FromSeconds(10);
            var url = AppendToken($"{source.GetRepoUrl()}/branches/{branch}", source);
            var request = new HttpRequestMessage(HttpMethod.Head, url);
            var response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
                return (true, "分支可用");
            if (response.StatusCode == HttpStatusCode.NotFound)
                return (false, $"分支 [{branch}] 不存在");

            return (false, $"HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
