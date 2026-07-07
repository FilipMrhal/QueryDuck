using System.Globalization;
using System.Text;
using QueryDuck.Core.Adapters;
using QueryDuck.Core.Providers;

namespace QueryDuck.Core.Performance;

public static class StatisticsIndexRecommendationEngine
{
    public static SlowQueryRecommendation? RecommendFromStatistics(
        DatabaseProvider provider,
        string table,
        IReadOnlyList<string> filterColumns,
        IReadOnlyList<string> orderByColumns,
        IReadOnlyList<ColumnStatistics> statistics)
    {
        ArgumentNullException.ThrowIfNull(filterColumns);
        ArgumentNullException.ThrowIfNull(orderByColumns);
        ArgumentNullException.ThrowIfNull(statistics);
        if (statistics.Count == 0)
        {
            return null;
        }

        var statsByColumn = statistics.ToDictionary(
            s => s.ColumnName,
            s => s,
            StringComparer.OrdinalIgnoreCase);

        var indexColumns = BuildIndexColumnOrder(filterColumns, orderByColumns, statsByColumn);
        if (indexColumns.Count == 0)
        {
            return null;
        }

        var rationale = BuildRationale(indexColumns, statsByColumn);
        var indexSql = BuildIndexSql(provider, table, indexColumns, statsByColumn);

        return new SlowQueryRecommendation(
            SlowQueryImprovementCategory.IndexCreation,
            "Index from column statistics",
            rationale,
            SuggestedIndexSql: indexSql);
    }

    private static List<string> BuildIndexColumnOrder(
        IReadOnlyList<string> filterColumns,
        IReadOnlyList<string> orderByColumns,
        IReadOnlyDictionary<string, ColumnStatistics> statsByColumn)
    {
        var ordered = new List<string>();
        foreach (var column in filterColumns)
        {
            if (statsByColumn.ContainsKey(column) && !ordered.Contains(column, StringComparer.OrdinalIgnoreCase))
            {
                ordered.Add(column);
            }
        }

        foreach (var column in orderByColumns)
        {
            if (statsByColumn.ContainsKey(column) && !ordered.Contains(column, StringComparer.OrdinalIgnoreCase))
            {
                ordered.Add(column);
            }
        }

        if (ordered.Count == 0)
        {
            ordered.AddRange(statsByColumn.Keys.Take(3));
        }

        return ordered;
    }

    private static string BuildRationale(
        IReadOnlyList<string> indexColumns,
        IReadOnlyDictionary<string, ColumnStatistics> statsByColumn)
    {
        var builder = new StringBuilder();
        builder.Append("Statistics-backed index recommendation: ");
        builder.Append(string.Join(", ", indexColumns));
        builder.Append(". ");

        foreach (var column in indexColumns.Take(3))
        {
            if (!statsByColumn.TryGetValue(column, out var stats))
            {
                continue;
            }

            if (stats.NullFraction > 0.5)
            {
                builder.Append(CultureInfo.InvariantCulture, $"{column} is sparse ({stats.NullFraction:P0} null); consider a partial index. ");
            }

            if (stats.DistinctFraction is > 0 and < 0.05)
            {
                builder.Append(CultureInfo.InvariantCulture, $"{column} has low cardinality ({stats.DistinctFraction:P1}); equality filters benefit most. ");
            }
            else if (stats.DistinctCount is > 1000 or < -0.9)
            {
                builder.Append(CultureInfo.InvariantCulture, $"{column} is highly selective; strong index candidate. ");
            }

            if (stats.Correlation is >= 0.8 or <= -0.8)
            {
                builder.Append(CultureInfo.InvariantCulture, $"{column} correlation {stats.Correlation:F2} supports range/order scans. ");
            }
        }

        return builder.ToString().Trim();
    }

    private static string BuildIndexSql(
        DatabaseProvider provider,
        string table,
        IReadOnlyList<string> indexColumns,
        IReadOnlyDictionary<string, ColumnStatistics> statsByColumn)
    {
        var indexName = $"ix_{SanitizeIdentifier(table)}_{string.Join('_', indexColumns.Take(2).Select(SanitizeIdentifier))}";
        var columnsSql = string.Join(", ", indexColumns.Select(c => Quote(provider, c)));

        var partial = indexColumns
            .Select(c => statsByColumn.TryGetValue(c, out var stats) ? stats : null)
            .FirstOrDefault(s => s?.NullFraction > 0.5);

        var whereClause = partial is not null
            ? $" WHERE {Quote(provider, partial.ColumnName)} IS NOT NULL"
            : string.Empty;

        return provider switch
        {
            DatabaseProvider.PostgreSql =>
                $"CREATE INDEX CONCURRENTLY IF NOT EXISTS {Quote(provider, indexName)} ON {Quote(provider, table)} ({columnsSql}){whereClause};",
            DatabaseProvider.SqlServer =>
                $"CREATE NONCLUSTERED INDEX {Quote(provider, indexName)} ON {Quote(provider, table)} ({columnsSql});",
            DatabaseProvider.Oracle =>
                $"CREATE INDEX {Quote(provider, indexName)} ON {Quote(provider, table)} ({columnsSql});",
            DatabaseProvider.MySql =>
                $"CREATE INDEX {Quote(provider, indexName)} ON {Quote(provider, table)} ({columnsSql});",
            _ => $"CREATE INDEX {indexName} ON {table} ({columnsSql});",
        };
    }

    private static string Quote(DatabaseProvider provider, string identifier) =>
        provider switch
        {
            DatabaseProvider.PostgreSql => $"\"{identifier.Trim('"')}\"",
            DatabaseProvider.SqlServer => $"[{identifier.Trim('[', ']')}]",
            DatabaseProvider.MySql => $"`{identifier.Trim('`')}`",
            DatabaseProvider.Oracle => identifier.ToUpperInvariant(),
            _ => identifier,
        };

    private static string SanitizeIdentifier(string value) =>
        new string(value.Where(char.IsLetterOrDigit).Take(24).ToArray()).ToUpperInvariant();
}
