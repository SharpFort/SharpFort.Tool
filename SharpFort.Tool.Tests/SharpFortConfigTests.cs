using Xunit;
using SharpFort.Tool.Domain;

namespace SharpFort.Tool.Tests;

public class SharpFortConfigTests
{
    [Fact]
    public void CreateDefault_HasGitHubHost()
    {
        var config = SharpFortConfig.CreateDefault();
        Assert.Equal("https://api.github.com", config.Repo.Host);
    }

    [Fact]
    public void CreateDefault_HasDefaultBranch()
    {
        var config = SharpFortConfig.CreateDefault();
        Assert.Equal("default", config.DefaultTemplateBranch);
    }

    [Fact]
    public void CreateDefault_CloneAddressIsGitHub()
    {
        var config = SharpFortConfig.CreateDefault();
        Assert.Contains("github.com/SharpFort", config.CloneAddress);
    }
}
