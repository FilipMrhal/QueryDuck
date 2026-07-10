using QueryDuck.Core.Adapters;
using QueryDuck.Core.Learning;

namespace QueryDuck.Core.Capture;

public sealed record QueryDuckSchemaAuditSnapshot(
    DateTimeOffset CapturedAt,
    string Provider,
    SchemaAuditResult Result,
    bool HasIssues);

public sealed record QueryDuckSchemaAuditPresentation(
    DateTimeOffset CapturedAt,
    string Provider,
    SchemaAuditResult Result,
    bool HasIssues,
    bool SessionFilterActive,
    int HiddenFindingCount,
    IReadOnlyList<SessionTableRelevance> SessionTables);

public static class QueryDuckSchemaAuditCache
{
    private static readonly Lock Gate = new();
    private static QueryDuckSchemaAuditSnapshot? _snapshot;
    private static DateTimeOffset _lastRefresh = DateTimeOffset.MinValue;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);

    public static QueryDuckSchemaAuditPresentation? GetPresentation()
    {
        QueryDuckSchemaAuditSnapshot? snapshot;
        lock (Gate)
        {
            snapshot = _snapshot;
        }

        if (snapshot is null)
        {
            return null;
        }

        var sessionTables = QueryDuckSessionTables.GetTables();
        var sessionLookup = QueryDuckSessionTables.GetRelevanceLookup();
        var (filtered, hiddenCount, filterActive) = SchemaAuditSessionPresenter.ApplySessionFilter(
            snapshot.Result,
            sessionLookup);
        var presented = QueryHeuristicMemory.ApplyToSchemaAudit(filtered, snapshot.Provider);

        return new QueryDuckSchemaAuditPresentation(
            snapshot.CapturedAt,
            snapshot.Provider,
            presented,
            presented.HasIssues,
            filterActive,
            hiddenCount,
            sessionTables);
    }

    public static bool TryRefresh(
        IDatabaseAdapter adapter,
        Microsoft.EntityFrameworkCore.Metadata.IModel model,
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(connection);

        lock (Gate)
        {
            if (DateTimeOffset.UtcNow - _lastRefresh < RefreshInterval)
            {
                return false;
            }

            _lastRefresh = DateTimeOffset.UtcNow;
        }

        return RefreshCoreAsync(adapter, model, connection, cancellationToken).GetAwaiter().GetResult();
    }

    public static async Task<bool> TryRefreshAsync(
        IDatabaseAdapter adapter,
        Microsoft.EntityFrameworkCore.Metadata.IModel model,
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(connection);

        lock (Gate)
        {
            if (DateTimeOffset.UtcNow - _lastRefresh < RefreshInterval)
            {
                return false;
            }

            _lastRefresh = DateTimeOffset.UtcNow;
        }

        return await RefreshCoreAsync(adapter, model, connection, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> RefreshCoreAsync(
        IDatabaseAdapter adapter,
        Microsoft.EntityFrameworkCore.Metadata.IModel model,
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            await connection.EnsureOpenAsync(cancellationToken).ConfigureAwait(false);

            var result = await adapter.AuditSchemaAsync(model, connection, cancellationToken).ConfigureAwait(false);
            var snapshot = new QueryDuckSchemaAuditSnapshot(
                DateTimeOffset.UtcNow,
                adapter.Provider.ToString(),
                result,
                result.HasIssues);

            lock (Gate)
            {
                _snapshot = snapshot;
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static void Clear()
    {
        lock (Gate)
        {
            _snapshot = null;
            _lastRefresh = DateTimeOffset.MinValue;
        }
    }
}
