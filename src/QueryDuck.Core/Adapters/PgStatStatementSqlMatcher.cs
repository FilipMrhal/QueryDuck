using System.Text.RegularExpressions;

namespace QueryDuck.Core.Adapters;

public static partial class PgStatStatementSqlMatcher
{
    public static string NormalizeForMatch(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return string.Empty;
        }

        var normalized = WhitespaceRegex().Replace(sql, " ").Trim();
        normalized = ParameterRegex().Replace(normalized, "?");
        normalized = LiteralRegex().Replace(normalized, "?");
        return normalized.ToUpperInvariant();
    }

    public static bool IsLikelyMatch(string capturedSql, string pgStatQueryText)
    {
        var left = NormalizeForMatch(capturedSql);
        var right = NormalizeForMatch(pgStatQueryText);
        if (left.Length == 0 || right.Length == 0)
        {
            return false;
        }

        if (left.Equals(right, StringComparison.Ordinal))
        {
            return true;
        }

        if (left.Length >= 24 && right.Contains(left, StringComparison.Ordinal))
        {
            return true;
        }

        if (right.Length >= 24 && left.Contains(right, StringComparison.Ordinal))
        {
            return true;
        }

        var leftTokens = TokenRegex().Matches(left).Select(m => m.Value).Where(t => t.Length > 2).Take(12).ToArray();
        if (leftTokens.Length == 0)
        {
            return false;
        }

        var matched = leftTokens.Count(token => right.Contains(token, StringComparison.Ordinal));
        return matched >= Math.Max(3, leftTokens.Length * 2 / 3);
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@":\w+|@\w+", RegexOptions.CultureInvariant)]
    private static partial Regex ParameterRegex();

    [GeneratedRegex(@"'(?:''|[^'])*'", RegexOptions.CultureInvariant)]
    private static partial Regex LiteralRegex();

    [GeneratedRegex(@"\b[A-Z_][A-Z0-9_]*\b|\b[A-Z][A-Z0-9_]+\b", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();
}
