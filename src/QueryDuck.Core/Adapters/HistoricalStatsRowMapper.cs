using System.Data.Common;

namespace QueryDuck.Core.Adapters;

internal static class HistoricalStatsRowMapper
{
    public static QueryHistoricalStatsInsight? TryMap(
        DbDataReader reader,
        string sql,
        Func<DbDataReader, string> readQueryText,
        Func<DbDataReader, string, QueryHistoricalStatsInsight> mapMatched)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentNullException.ThrowIfNull(readQueryText);
        ArgumentNullException.ThrowIfNull(mapMatched);

        var queryText = readQueryText(reader);
        if (!QueryHistoricalStatsSqlMatcher.IsLikelyMatch(sql, queryText))
        {
            return null;
        }

        return mapMatched(reader, queryText);
    }
}
