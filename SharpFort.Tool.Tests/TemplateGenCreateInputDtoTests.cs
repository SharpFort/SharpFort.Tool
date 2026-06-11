using Xunit;
using SharpFort.Tool.Application.Contracts.Dtos;

namespace SharpFort.Tool.Tests;

public class TemplateGenCreateInputDtoTests
{
    [Fact]
    public void SetNameReplace_AddsSingleKey()
    {
        var dto = new TemplateGenCreateInputDto { Name = "SharpFort.Crm" };
        dto.SetNameReplace();

        Assert.Single(dto.ReplaceStrData);
        Assert.Equal("SharpFort.Crm", dto.ReplaceStrData["SharpFort"]);
    }

    [Fact]
    public void SetNameReplace_PreservesDots()
    {
        var dto = new TemplateGenCreateInputDto { Name = "A.B.C" };
        dto.SetNameReplace();

        Assert.Equal("A.B.C", dto.ReplaceStrData["SharpFort"]);
    }
}
