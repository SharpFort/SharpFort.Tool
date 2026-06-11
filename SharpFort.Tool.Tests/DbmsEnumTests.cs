using Xunit;
using SharpFort.Tool.Domain.Shared.Enums;

namespace SharpFort.Tool.Tests;

public class DbmsEnumTests
{
    [Fact]
    public void DbmsEnum_HasFiveValues()
    {
        var values = Enum.GetValues<DbmsEnum>();
        Assert.Equal(5, values.Length);
    }

    [Fact]
    public void DbmsEnum_ContainsMySql()
    {
        Assert.Contains(DbmsEnum.MySQL, Enum.GetValues<DbmsEnum>());
    }

    [Fact]
    public void DbmsEnum_ContainsPostgreSql()
    {
        Assert.Contains(DbmsEnum.PostgreSQL, Enum.GetValues<DbmsEnum>());
    }
}
