using QueryDuck.Core.Providers;

namespace QueryDuck.Core.Performance;

public static class MigrationSnippetBuilder
{
    public static string? FromIndexDdl(string? indexSql, DatabaseProvider provider, string? migrationName = null)
    {
        if (string.IsNullOrWhiteSpace(indexSql))
        {
            return null;
        }

        var name = migrationName ?? $"AddIndex_{DateTime.UtcNow:yyyyMMddHHmmss}";
        var escaped = indexSql.Replace("\"", "\\\"", StringComparison.Ordinal);
        var method = provider switch
        {
            DatabaseProvider.PostgreSql => "migrationBuilder.Sql",
            DatabaseProvider.SqlServer => "migrationBuilder.Sql",
            DatabaseProvider.Oracle => "migrationBuilder.Sql",
            DatabaseProvider.MySql => "migrationBuilder.Sql",
            _ => "migrationBuilder.Sql",
        };

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
                    // Add DROP INDEX statement for rollback if needed.
                }
            }
            """;
    }
}
