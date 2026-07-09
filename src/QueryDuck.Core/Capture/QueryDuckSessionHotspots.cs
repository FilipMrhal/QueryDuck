namespace QueryDuck.Core.Capture;

public sealed record QueryShapeHotspot(
    string ShapeKey,
    string NormalizedSqlPreview,
    int ExecutionCount,
    double TotalDurationMs,
    double MaxDurationMs,
    double AverageDurationMs,
    IReadOnlyList<string> Providers,
    IReadOnlyList<string> Tags);

public sealed record QueryDuckSessionHotspots(
    int TotalEvents,
    int DistinctShapes,
    IReadOnlyList<QueryShapeHotspot> Hotspots);

public static class QueryDuckSessionHotspotsBuilder
{
    public static QueryDuckSessionHotspots Build(IEnumerable<QueryCaptureEvent> events, int top = 20)
    {
        ArgumentNullException.ThrowIfNull(events);
        var materialized = events.ToArray();
        var groups = materialized
            .GroupBy(e => QueryCaptureHeuristics.NormalizeSqlShape(e.Sql))
            .Select(g =>
            {
                var durations = g.Select(e => e.Duration.TotalMilliseconds).ToArray();
                return new QueryShapeHotspot(
                    Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                        System.Text.Encoding.UTF8.GetBytes(g.Key)))[..8],
                    PreviewSql(g.First().Sql),
                    g.Count(),
                    durations.Sum(),
                    durations.Max(),
                    durations.Average(),
                    g.Select(e => e.Provider).Distinct(StringComparer.Ordinal).ToArray(),
                    g.Select(e => e.Tag).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.Ordinal).Cast<string>().ToArray());
            })
            .OrderByDescending(h => h.ExecutionCount)
            .ThenByDescending(h => h.TotalDurationMs)
            .Take(Math.Max(1, top))
            .ToArray();

        return new QueryDuckSessionHotspots(
            materialized.Length,
            materialized.Select(e => QueryCaptureHeuristics.NormalizeSqlShape(e.Sql)).Distinct(StringComparer.Ordinal).Count(),
            groups);
    }

    private static string PreviewSql(string sql)
    {
        var line = sql.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? sql;
        return line.Length <= 96 ? line : line[..93] + "...";
    }
}
