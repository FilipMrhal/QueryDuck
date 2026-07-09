using QueryDuck.Core.Adapters;

namespace QueryDuck.Core.Capture;

public sealed record QueryDuckSchemaAuditSnapshot(
    DateTimeOffset CapturedAt,
    string Provider,
    SchemaAuditResult Result,
    bool HasIssues);

public static class QueryDuckSchemaAuditCache
{
    private static readonly Lock Gate = new();
    private static QueryDuckSchemaAuditSnapshot? _snapshot;
    private static DateTimeOffset _lastRefresh = DateTimeOffset.MinValue;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);

    public static QueryDuckSchemaAuditSnapshot? Current
    {
        get
        {
            lock (Gate)
            {
                return _snapshot;
            }
        }
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
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

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
