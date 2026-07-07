using QueryDuck.Core.Performance;

namespace QueryDuck.Core.Capture;

public sealed record SessionTableRelevance(
    string TableName,
    int HitCount,
    double TotalDurationMs,
    double RelevanceScore);

public static class QueryDuckSessionTables
{
    private static readonly Lock Gate = new();
    private static readonly Dictionary<string, (int HitCount, double TotalDurationMs)> Tables =
        new(StringComparer.OrdinalIgnoreCase);

    public static void Record(string sql, TimeSpan duration)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return;
        }

        var patterns = SqlPatternAnalyzer.Analyze(sql);
        if (patterns.ReferencedTables.Count == 0)
        {
            return;
        }

        var durationMs = Math.Max(0, duration.TotalMilliseconds);
        lock (Gate)
        {
            foreach (var table in patterns.ReferencedTables)
            {
                var normalized = NormalizeTableName(table);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                if (Tables.TryGetValue(normalized, out var existing))
                {
                    Tables[normalized] = (existing.HitCount + 1, existing.TotalDurationMs + durationMs);
                }
                else
                {
                    Tables[normalized] = (1, durationMs);
                }
            }
        }
    }

    public static IReadOnlyList<SessionTableRelevance> GetTables(int top = 50)
    {
        lock (Gate)
        {
            return Tables
                .Select(pair => new SessionTableRelevance(
                    pair.Key,
                    pair.Value.HitCount,
                    pair.Value.TotalDurationMs,
                    ComputeRelevance(pair.Value.HitCount, pair.Value.TotalDurationMs)))
                .OrderByDescending(t => t.RelevanceScore)
                .ThenByDescending(t => t.HitCount)
                .Take(Math.Max(1, top))
                .ToArray();
        }
    }

    public static IReadOnlyDictionary<string, double> GetRelevanceLookup()
    {
        lock (Gate)
        {
            return Tables.ToDictionary(
                pair => pair.Key,
                pair => ComputeRelevance(pair.Value.HitCount, pair.Value.TotalDurationMs),
                StringComparer.OrdinalIgnoreCase);
        }
    }

    public static void Clear()
    {
        lock (Gate)
        {
            Tables.Clear();
        }
    }

    private static double ComputeRelevance(int hitCount, double totalDurationMs) =>
        hitCount + (totalDurationMs / 1000.0);

    private static string NormalizeTableName(string table) =>
        table.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Last()
            .Trim('"', '[', ']', '`');
}
