using QueryDuck.Core.Capture;

namespace QueryDuck.Tests;

public class EntityFrameworkExtensionsLogParserTests
{
    [Fact]
    public void ExtractSqlStatements_splits_multiple_commands()
    {
        const string log = """
            -- Batch 1
            INSERT INTO "Customers" ("Name") VALUES (@p0);
            -- Batch 2
            UPDATE "Customers" SET "Name" = @p0 WHERE "Id" = @p1;
            """;

        var statements = EntityFrameworkExtensionsLogParser.ExtractSqlStatements(log);

        Assert.Equal(2, statements.Count);
        Assert.StartsWith("INSERT INTO", statements[0], StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("UPDATE", statements[1], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveBulkOperationName_maps_insert_only()
    {
        var name = EntityFrameworkExtensionsLogParser.ResolveBulkOperationName(100, 0, 0, "Customers");

        Assert.Equal("BulkInsert", name);
    }

    [Fact]
    public void ResolvePrimarySql_returns_last_statement()
    {
        const string log = """
            Timing: 12ms
            SELECT COUNT(*) FROM "Customers";
            MERGE INTO "Customers" AS T USING ...
            """;

        var sql = EntityFrameworkExtensionsLogParser.ResolvePrimarySql(log);

        Assert.StartsWith("MERGE INTO", sql, StringComparison.OrdinalIgnoreCase);
    }
}
