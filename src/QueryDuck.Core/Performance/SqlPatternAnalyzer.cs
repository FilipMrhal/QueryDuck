using System.Text.RegularExpressions;

namespace QueryDuck.Core.Performance;

internal static partial class SqlPatternAnalyzer
{
    public sealed record SqlPatternFindings(
        bool SelectStar,
        bool MissingLimit,
        bool LeadingWildcardLike,
        bool OrAcrossColumns,
        bool CorrelatedSubquery,
        bool FunctionOnFilteredColumn,
        IReadOnlyList<string> JoinTables,
        IReadOnlyList<string> WhereColumns,
        IReadOnlyList<string> OrderByColumns);

    public static SqlPatternFindings Analyze(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return new SqlPatternFindings(false, false, false, false, false, false, [], [], []);
        }

        var normalized = RegexReplaceWhitespace().Replace(sql, " ");
        var upper = normalized.ToUpperInvariant();

        return new SqlPatternFindings(
            SelectStar: SelectStarRegex().IsMatch(normalized),
            MissingLimit: upper.Contains("SELECT", StringComparison.Ordinal) &&
                !LimitRegex().IsMatch(upper),
            LeadingWildcardLike: LeadingLikeRegex().IsMatch(normalized),
            OrAcrossColumns: OrAcrossColumnsRegex().IsMatch(normalized),
            CorrelatedSubquery: upper.Contains("SELECT", StringComparison.Ordinal) &&
                Regex.Count(upper, @"\bSELECT\b") > 1,
            FunctionOnFilteredColumn: FunctionOnColumnRegex().IsMatch(normalized),
            JoinTables: ExtractJoinTables(normalized),
            WhereColumns: ExtractWhereColumns(normalized),
            OrderByColumns: ExtractOrderByColumns(normalized));
    }

    private static IReadOnlyList<string> ExtractJoinTables(string sql)
    {
        var matches = JoinTableRegex().Matches(sql);
        return matches.Select(m => m.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<string> ExtractWhereColumns(string sql)
    {
        var whereMatch = WhereClauseRegex().Match(sql);
        if (!whereMatch.Success)
        {
            return [];
        }

        return ColumnRefRegex().Matches(whereMatch.Groups[1].Value)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ExtractOrderByColumns(string sql)
    {
        var orderMatch = OrderByRegex().Match(sql);
        if (!orderMatch.Success)
        {
            return [];
        }

        return ColumnRefRegex().Matches(orderMatch.Groups[1].Value)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    [GeneratedRegex(@"\bSELECT\s+\*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SelectStarRegex();

    [GeneratedRegex(@"\b(LIMIT|FETCH\s+FIRST|TOP\s+\(\?\)|TOP\s+\d+|OFFSET\s+\d+\s+ROWS)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LimitRegex();

    [GeneratedRegex(@"LIKE\s+'%", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LeadingLikeRegex();

    [GeneratedRegex(@"\bWHERE\b.+?\bOR\b.+?(=|<|>)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OrAcrossColumnsRegex();

    [GeneratedRegex(@"\bWHERE\b.+\b(UPPER|LOWER|SUBSTR|SUBSTRING|YEAR|MONTH|DATE|TRUNC|CAST)\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FunctionOnColumnRegex();

    [GeneratedRegex(@"\b(?:INNER|LEFT|RIGHT|FULL|CROSS)?\s*JOIN\s+([""\[]?\w+""\]?\.?[""\[]?\w+""\]?|[""\[]?\w+""\]?)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JoinTableRegex();

    [GeneratedRegex(@"\bWHERE\b(.+?)(?:\bGROUP\b|\bORDER\b|\bHAVING\b|\bLIMIT\b|\bFETCH\b|\bOFFSET\b|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WhereClauseRegex();

    [GeneratedRegex(@"\bORDER\s+BY\b(.+?)(?:\bLIMIT\b|\bFETCH\b|\bOFFSET\b|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OrderByRegex();

    [GeneratedRegex(@"(?:\b\w+\.)?(\w+)\s*(=|<|>|<=|>=|<>|!=|LIKE|IN\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ColumnRefRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex RegexReplaceWhitespace();
}
