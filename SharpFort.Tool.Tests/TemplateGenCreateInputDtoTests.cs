using Xunit;
using SharpFort.Tool.Application.Contracts.Dtos;

namespace SharpFort.Tool.Tests;

public class TemplateGenCreateInputDtoTests
{
    [Fact]
    public void SetNameReplace_AddsExpectedKeys()
    {
        var dto = new TemplateGenCreateInputDto { Name = "SharpFort.Crm" };
        dto.SetNameReplace();

        Assert.Equal(2, dto.ReplaceStrData.Count);
        Assert.Equal("SharpFort.Crm", dto.ReplaceStrData["Yi.Abp"]);
        Assert.Equal("SharpFortCrm", dto.ReplaceStrData["YiAbp"]);
    }

    [Fact]
    public void SetNameReplace_RemovesDotsInSecondKey()
    {
        var dto = new TemplateGenCreateInputDto { Name = "A.B.C" };
        dto.SetNameReplace();

        Assert.Equal("ABC", dto.ReplaceStrData["YiAbp"]);
    }
}
