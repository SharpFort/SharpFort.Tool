using System.Net;
using Newtonsoft.Json.Linq;
using Volo.Abp.DependencyInjection;

namespace SharpFort.Tool.Domain;

public class GiteeManager : ITransientDependency
{
    private readonly ConfigManager _configManager;
    private readonly IHttpClientFactory _httpClientFactory;

    public GiteeManager(ConfigManager configManager, IHttpClientFactory httpClientFactory)
    {
        _configManager = configManager;
        _httpClientFactory = httpClientFactory;
    }

    private string Host => _configManager.GetConfig().Gitee.Host;
    private string Owner => _configManager.GetConfig().Gitee.Owner;
    private string Repo => _configManager.GetConfig().Gitee.Repo;
    private string? AccessToken => _configManager.GetConfig().Gitee.AccessToken;

    private string BuildUrl(string path)
    {
        var url = $"{Host}{path}";
        if (!string.IsNullOrEmpty(AccessToken))
            url += $"{(path.Contains('?') ? "&" : "?")}access_token={AccessToken}";
        return url;
    }

    public async Task<bool> IsExsitBranchAsync(string branch)
    {
        using var client = _httpClientFactory.CreateClient();
        var response = await client.GetAsync(
            BuildUrl($"/repos/{Owner}/{Repo}/branches/{branch}"));
        return response.StatusCode != HttpStatusCode.NotFound;
    }

    public async Task<List<string>> GetAllBranchAsync()
    {
        using var client = _httpClientFactory.CreateClient();
        var response = await client.GetAsync(
            BuildUrl($"/repos/{Owner}/{Repo}/branches?sort=name&direction=asc&page=1&per_page=100"));
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

    public async Task<Stream> DownLoadFileAsync(string branch)
    {
        using var client = _httpClientFactory.CreateClient();
        var response = await client.GetAsync(
            BuildUrl($"/repos/{Owner}/{Repo}/zipball?ref={branch}"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync();
    }
}
