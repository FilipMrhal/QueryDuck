using System.Globalization;
using System.Text;
using QueryDuck.Core.Providers;

namespace QueryDuck.Core.Performance;

internal static class SqlRewriteSuggestions
{
    public static string? SuggestSelectListRewrite(string sql)
    {
        if (!SqlPatternAnalyzer.Analyze(sql).SelectStar)
        {
            return null;
        }

        return System.Text.RegularExpressions.Regex.Replace(
            sql,
            @"\bSELECT\s+\*",
            "SELECT <explicit_columns>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
    }

    public static string? SuggestUnionInsteadOfOr(string sql)
    {
        if (!SqlPatternAnalyzer.Analyze(sql).OrAcrossColumns)
        {
            return null;
        }

        var whereMatch = System.Text.RegularExpressions.Regex.Match(
            sql,
            @"\bWHERE\b(.+?)(?:\bGROUP\b|\bORDER\b|\bHAVING\b|\bLIMIT\b|\bFETCH\b|\bOFFSET\b|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
        if (!whereMatch.Success)
        {
            return null;
        }

        var parts = whereMatch.Groups[1].Value.Split(" OR ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return null;
        }

        var fromMatch = System.Text.RegularExpressions.Regex.Match(
            sql,
            @"\bSELECT\b(.+?)\bFROM\b(.+?)(?:\bWHERE\b|\bORDER\b|\bGROUP\b|\bJOIN\b|\bINNER\b|\bLEFT\b|\bRIGHT\b|\bLIMIT\b|\bFETCH\b|\bOFFSET\b|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
        if (!fromMatch.Success)
        {
            return null;
        }

        var projection = fromMatch.Groups[1].Value.Trim();
        var fromClause = fromMatch.Groups[2].Value.Trim();
        var branches = parts.Select(part => $"SELECT {projection} FROM {fromClause} WHERE {part}");
        return string.Join("\nUNION ALL\n", branches);
    }

    public static string? SuggestCteForRepeatedScan(string sql, IReadOnlyList<string> joinTables)
    {
        if (joinTables.Count == 0)
        {
            return null;
        }

        var baseTable = joinTables[0];
        var alias = "filtered_" + baseTable.ToUpperInvariant();
        return $"""
            WITH {alias} AS (
                SELECT /* explicit columns */ * FROM {baseTable} WHERE /* selective predicates */
            )
            {sql}
            """.Replace(baseTable, alias, StringComparison.OrdinalIgnoreCase);
    }

    public static string SuggestIndex(DatabaseProvider provider, string table, string column)
    {
        return provider switch
        {
            DatabaseProvider.Oracle =>
                $"CREATE INDEX IX_{table}_{column} ON {table}({column});",
            DatabaseProvider.PostgreSql =>
                string.Create(CultureInfo.InvariantCulture,
                    $"CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_{table.ToUpperInvariant()}_{column.ToUpperInvariant()} ON {table.ToUpperInvariant()} ({column.ToUpperInvariant()});"),
            DatabaseProvider.SqlServer =>
                $"CREATE NONCLUSTERED INDEX IX_{table}_{column} ON dbo.{table} ({column});",
            DatabaseProvider.MySql =>
                $"CREATE INDEX ix_{table}_{column} ON {table} ({column});",
            _ => $"CREATE INDEX IX_{table}_{column} ON {table}({column});",
        };
    }
}
