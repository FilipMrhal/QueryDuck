using System.Text.RegularExpressions;
using QueryDuck.Core.Providers;

namespace QueryDuck.Core.Performance;

public static partial class MigrationSnippetBuilder
{
    public static string? FromIndexDdl(string? indexSql, DatabaseProvider provider, string? migrationName = null)
    {
        if (string.IsNullOrWhiteSpace(indexSql))
        {
            return null;
        }

        var name = migrationName ?? $"AddIndex_{DateTime.UtcNow:yyyyMMddHHmmss}";
        var escaped = indexSql.Replace("\"", "\\\"", StringComparison.Ordinal);
        var downSql = BuildDropIndexSql(indexSql, provider);
        var downEscaped = downSql?.Replace("\"", "\\\"", StringComparison.Ordinal);
        var method = provider switch
        {
            DatabaseProvider.PostgreSql => "migrationBuilder.Sql",
            DatabaseProvider.SqlServer => "migrationBuilder.Sql",
            DatabaseProvider.Oracle => "migrationBuilder.Sql",
            DatabaseProvider.MySql => "migrationBuilder.Sql",
            _ => "migrationBuilder.Sql",
        };

        var downBody = downEscaped is null
            ? "            // No rollback SQL could be inferred from the index DDL."
            : $"            {method}(\"{downEscaped}\");";

        return $$"""
            // QueryDuck suggested migration snippet
            public partial class {{name}}
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    {{method}}("{{escaped}}");
                }

                protected override void Down(MigrationBuilder migrationBuilder)
                {
            {{downBody}}
                }
            }
            """;
    }

    private static string? BuildDropIndexSql(string indexSql, DatabaseProvider provider)
    {
        var match = CreateIndexRegex().Match(indexSql);
        if (!match.Success)
        {
            return null;
        }

        var indexName = match.Groups["name"].Value.Trim('"', '[', ']', '`');
        var tableName = match.Groups["table"].Value.Trim('"', '[', ']', '`');
        if (string.IsNullOrWhiteSpace(indexName) || string.IsNullOrWhiteSpace(tableName))
        {
            return null;
        }

        return provider switch
        {
            DatabaseProvider.PostgreSql => $"DROP INDEX IF EXISTS {QuoteIdentifier(indexName, '"')};",
            DatabaseProvider.SqlServer => $"DROP INDEX IF EXISTS {QuoteIdentifier(indexName, '[', ']')} ON {QuoteIdentifier(tableName, '[', ']')};",
            DatabaseProvider.Oracle => $"DROP INDEX {QuoteIdentifier(indexName, '"')};",
            DatabaseProvider.MySql => $"DROP INDEX {QuoteIdentifier(indexName, '`')} ON {QuoteIdentifier(tableName, '`')};",
            DatabaseProvider.Sqlite => $"DROP INDEX IF EXISTS {QuoteIdentifier(indexName, '"')};",
            _ => $"DROP INDEX IF EXISTS {indexName};",
        };
    }

    private static string QuoteIdentifier(string identifier, char quote) =>
        $"{quote}{identifier}{quote}";

    private static string QuoteIdentifier(string identifier, char openQuote, char closeQuote) =>
        $"{openQuote}{identifier}{closeQuote}";

    [GeneratedRegex(
        @"CREATE\s+(?:UNIQUE\s+)?INDEX\s+(?<name>(?:""[^""]+""|\[[^\]]+\]|`[^`]+`|\w+))\s+ON\s+(?<table>(?:""[^""]+""|\[[^\]]+\]|`[^`]+`|\w+))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CreateIndexRegex();
}
