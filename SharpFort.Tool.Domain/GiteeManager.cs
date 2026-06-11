using System.Net;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Volo.Abp.DependencyInjection;

namespace SharpFort.Tool.Domain;

public class GiteeManager : ITransientDependency
{
    private readonly string? _accessToken;
    private readonly IHttpClientFactory _httpClientFactory;
    private const string GiteeHost = "https://gitee.com/api/v5";
    private const string Owner = "sunshang-hl";
    private const string Repo = "yi-template";

    public GiteeManager(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
        // 优先环境变量 → 配置文件
        _accessToken = Environment.GetEnvironmentVariable("SHARPFORT_GITEE_TOKEN")
            ?? configuration.GetValue<string>("GiteeAccession");
    }

    /// <summary>
    /// 构建 Gitee API URL，有令牌时附加 access_token 参数
    /// </summary>
    private string BuildUrl(string path)
    {
        var url = $"{GiteeHost}{path}";
        if (!string.IsNullOrEmpty(_accessToken))
        {
            url += $"{(path.Contains('?') ? "&" : "?")}access_token={_accessToken}";
        }
        return url;
    }

    /// <summary>
    /// 是否存在当前分支
    /// </summary>
    public async Task<bool> IsExsitBranchAsync(string branch)
    {
        using var client = _httpClientFactory.CreateClient();
        var response = await client.GetAsync(
            BuildUrl($"/repos/{Owner}/{Repo}/branches/{branch}"));
        return response.StatusCode != HttpStatusCode.NotFound;
    }

    /// <summary>
    /// 获取所有分支
    /// </summary>
    public async Task<List<string>> GetAllBranchAsync()
    {
        using var client = _httpClientFactory.CreateClient();
        var response = await client.GetAsync(
            BuildUrl($"/repos/{Owner}/{Repo}/branches?sort=name&direction=asc&page=1&per_page=100"));
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync();
        JArray jsonArray = JArray.Parse(result);
        List<string> names = new List<string>();
        foreach (JObject obj in jsonArray)
        {
            string? name = obj["name"]?.ToString();
            if (name != null) names.Add(name);
        }
        return names;
    }

    /// <summary>
    /// 下载仓库分支代码
    /// </summary>
    public async Task<Stream> DownLoadFileAsync(string branch)
    {
        using var client = _httpClientFactory.CreateClient();
        var response = await client.GetAsync(
            BuildUrl($"/repos/{Owner}/{Repo}/zipball?ref={branch}"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync();
    }
}
