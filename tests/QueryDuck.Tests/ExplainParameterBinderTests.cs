using System.Data.Common;
using Microsoft.Data.Sqlite;
using QueryDuck.Core.Adapters;
using QueryDuck.Core.Providers;

namespace QueryDuck.Tests;

public sealed class ExplainParameterBinderTests
{
    [Fact]
    public void BindParameters_AddsNormalizedParameterNames()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT @p0";

        ExplainCommandHelper.BindParameters(command, new Dictionary<string, object?>
        {
            ["p0"] = 42,
        });

        Assert.Single(command.Parameters.Cast<DbParameter>());
        Assert.Equal("@p0", command.Parameters[0]!.ParameterName);
        Assert.Equal(42, Convert.ToInt32(command.Parameters[0]!.Value, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ProviderSchemaHelper_ResolvesQualifiedTableReference()
    {
        var (schema, table) = ProviderSchemaHelper.ResolveTableReference("dbo.Orders", DatabaseProvider.SqlServer);
        Assert.Equal("dbo", schema);
        Assert.Equal("Orders", table);
    }

    [Fact]
    public void ProviderSchemaHelper_UsesProviderDefaultSchema()
    {
        var (schema, table) = ProviderSchemaHelper.ResolveTableReference("orders", DatabaseProvider.PostgreSql);
        Assert.Equal("public", schema);
        Assert.Equal("orders", table);
    }
}
