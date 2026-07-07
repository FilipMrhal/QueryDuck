using System.Text;
using System.Text.RegularExpressions;

namespace QueryDuck.Core.Capture;

public static class EntityFrameworkExtensionsLogParser
{
    private static readonly Regex SqlStartRegex = new(
        @"^\s*(SELECT|INSERT|UPDATE|DELETE|MERGE|WITH|CREATE|ALTER|DROP|TRUNCATE)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static IReadOnlyList<string> ExtractSqlStatements(string? logText)
    {
        if (string.IsNullOrWhiteSpace(logText))
        {
            return [];
        }

        var statements = new List<string>();
        var current = new StringBuilder();
        foreach (var rawLine in logText.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (SqlStartRegex.IsMatch(line) && current.Length > 0)
            {
                statements.Add(current.ToString().Trim());
                current.Clear();
            }

            if (SqlStartRegex.IsMatch(line) || current.Length > 0)
            {
                if (current.Length > 0)
                {
                    current.AppendLine();
                }

                current.Append(line);
            }
        }

        if (current.Length > 0)
        {
            statements.Add(current.ToString().Trim());
        }

        return statements;
    }

    public static string? ResolvePrimarySql(string? logText, string? fallbackSql = null)
    {
        var statements = ExtractSqlStatements(logText);
        if (statements.Count > 0)
        {
            return statements[^1];
        }

        return string.IsNullOrWhiteSpace(fallbackSql) ? null : fallbackSql.Trim();
    }

    public static string ResolveBulkOperationName(int inserted, int updated, int deleted, string? destinationTable)
    {
        if (inserted > 0 && updated == 0 && deleted == 0)
        {
            return "BulkInsert";
        }

        if (updated > 0 && inserted == 0 && deleted == 0)
        {
            return "BulkUpdate";
        }

        if (deleted > 0 && inserted == 0 && updated == 0)
        {
            return "BulkDelete";
        }

        if (inserted > 0 || updated > 0 || deleted > 0)
        {
            return "BulkMerge";
        }

        return string.IsNullOrWhiteSpace(destinationTable) ? "BulkOperation" : $"BulkOperation:{destinationTable}";
    }
}
