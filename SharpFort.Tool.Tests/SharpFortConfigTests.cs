using Xunit;
using SharpFort.Tool.Domain;

namespace SharpFort.Tool.Tests;

public class SharpFortConfigTests
{
    [Fact]
    public void CreateDefault_PrimaryHostIsGitHub()
    {
        var config = SharpFortConfig.CreateDefault();
        Assert.Equal("https://api.github.com", config.Repo.Primary.Host);
    }

    [Fact]
    public void CreateDefault_FallbackHostIsGitee()
    {
        var config = SharpFortConfig.CreateDefault();
        Assert.Equal("https://gitee.com/api/v5", config.Repo.Fallback.Host);
    }

    [Fact]
    public void CreateDefault_HasDefaultBranch()
    {
        var config = SharpFortConfig.CreateDefault();
        Assert.Equal("main", config.DefaultTemplateBranch);
    }

    [Fact]
    public void CreateDefault_ClonePrimaryIsGitHub()
    {
        var config = SharpFortConfig.CreateDefault();
        Assert.Contains("github.com/SharpFort", config.Clone.Primary);
    }

    [Fact]
    public void CreateDefault_CloneFallbackIsGitee()
    {
        var config = SharpFortConfig.CreateDefault();
        Assert.Contains("gitee.com/SharpFort", config.Clone.Fallback);
    }

    [Fact]
    public void CreateDefault_PrimaryOwnerIsSharpFort()
    {
        var config = SharpFortConfig.CreateDefault();
        Assert.Equal("SharpFort", config.Repo.Primary.Owner);
        Assert.Equal("SharpFort", config.Repo.Fallback.Owner);
    }

    [Fact]
    public void CreateDefault_PrimaryRepoNameIsTemplate()
    {
        var config = SharpFortConfig.CreateDefault();
        Assert.Equal("SharpFort.Template", config.Repo.Primary.RepoName);
        Assert.Equal("SharpFort.Template", config.Repo.Fallback.RepoName);
    }

    [Fact]
    public void RepoSource_GetSourceName_GitHub()
    {
        var source = new RepoSource { Host = "https://api.github.com" };
        Assert.Equal("GitHub", source.GetSourceName());
    }

    [Fact]
    public void RepoSource_GetSourceName_Gitee()
    {
        var source = new RepoSource { Host = "https://gitee.com/api/v5" };
        Assert.Equal("Gitee", source.GetSourceName());
    }

    [Fact]
    public void RepoSource_GetRepoUrl()
    {
        var source = new RepoSource
        {
            Host = "https://api.github.com",
            Owner = "SharpFort",
            RepoName = "SharpFort.Template"
        };
        Assert.Equal("https://api.github.com/repos/SharpFort/SharpFort.Template", source.GetRepoUrl());
    }
}
