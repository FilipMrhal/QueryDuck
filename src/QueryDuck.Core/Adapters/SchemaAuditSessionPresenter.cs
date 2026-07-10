namespace QueryDuck.Core.Adapters;

public static class SchemaAuditSessionPresenter
{
    public static (SchemaAuditResult Result, int HiddenFindingCount, bool SessionFilterActive) ApplySessionFilter(
        SchemaAuditResult raw,
        IReadOnlyDictionary<string, double> sessionTableRelevance)
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(sessionTableRelevance);

        if (sessionTableRelevance.Count == 0)
        {
            return (raw, 0, false);
        }

        var nullability = FilterAndRank(
            raw.NullabilityMismatches,
            f => f.TableName,
            f => f with { SessionRelevanceScore = ResolveRelevance(sessionTableRelevance, f.TableName) },
            sessionTableRelevance);
        var types = FilterAndRank(
            raw.TypeMismatches,
            f => f.TableName,
            f => f with { SessionRelevanceScore = ResolveRelevance(sessionTableRelevance, f.TableName) },
            sessionTableRelevance);
        var missingColumns = FilterAndRank(
            raw.MissingColumns,
            f => f.TableName,
            f => f with { SessionRelevanceScore = ResolveRelevance(sessionTableRelevance, f.TableName) },
            sessionTableRelevance);
        var missingIndexes = FilterAndRank(
            raw.MissingIndexes,
            f => f.TableName,
            f => f with { SessionRelevanceScore = ResolveRelevance(sessionTableRelevance, f.TableName) },
            sessionTableRelevance);
        var foreignKeys = FilterAndRank(
            raw.ForeignKeyIssues,
            f => f.TableName,
            f => f with
            {
                SessionRelevanceScore = Math.Max(
                    ResolveRelevance(sessionTableRelevance, f.TableName),
                    ResolveRelevance(sessionTableRelevance, f.ReferencedTable)),
            },
            sessionTableRelevance,
            f => MatchesSessionTable(sessionTableRelevance, f.TableName) ||
                 MatchesSessionTable(sessionTableRelevance, f.ReferencedTable));

        var hidden =
            raw.NullabilityMismatches.Count - nullability.Count +
            raw.TypeMismatches.Count - types.Count +
            raw.MissingColumns.Count - missingColumns.Count +
            raw.MissingIndexes.Count - missingIndexes.Count +
            raw.ForeignKeyIssues.Count - foreignKeys.Count;

        return (
            new SchemaAuditResult(nullability, types, missingColumns, missingIndexes, foreignKeys),
            hidden,
            true);
    }

    private static List<T> FilterAndRank<T>(
        IReadOnlyList<T> findings,
        Func<T, string?> tableSelector,
        Func<T, T> enrich,
        IReadOnlyDictionary<string, double> sessionTableRelevance,
        Func<T, bool>? include = null)
    {
        return findings
            .Where(finding =>
            {
                if (include is not null)
                {
                    return include(finding);
                }

                var tableName = tableSelector(finding);
                return MatchesSessionTable(sessionTableRelevance, tableName);
            })
            .Select(enrich)
            .OrderByDescending(finding => ResolveRelevance(sessionTableRelevance, tableSelector(finding)))
            .ToList();
    }

    private static bool MatchesSessionTable(
        IReadOnlyDictionary<string, double> sessionTableRelevance,
        string? tableName) =>
        !string.IsNullOrWhiteSpace(tableName) &&
        sessionTableRelevance.ContainsKey(SqlIdentifierNormalizer.NormalizeTableName(tableName));

    private static double ResolveRelevance(
        IReadOnlyDictionary<string, double> sessionTableRelevance,
        string? tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return 0;
        }

        return sessionTableRelevance.TryGetValue(SqlIdentifierNormalizer.NormalizeTableName(tableName), out var score)
            ? score
            : 0;
    }
}
