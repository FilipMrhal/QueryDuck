using System.Data.Common;
using QueryDuck.Core.Adapters;
using QueryDuck.Core.Providers;

namespace QueryDuck.Core.Capture;

public sealed record QueryDuckStatementCacheDiagnostics(
    string Provider,
    bool ConnectionAvailable,
    IReadOnlyList<StatementCacheFinding> Findings);

public static class QueryDuckStatementCacheDiagnosticsBuilder
{
    public static async Task<QueryDuckStatementCacheDiagnostics> BuildAsync(
        DatabaseAdapterRegistry? adapters,
        DbConnection? connection,
        string? providerName,
        CancellationToken cancellationToken = default)
    {
        var provider = DatabaseProviderNames.FromProviderName(providerName);
        if (adapters?.Resolve(provider) is not { } adapter || connection is null)
        {
            return new QueryDuckStatementCacheDiagnostics(
                provider.ToString(),
                ConnectionAvailable: false,
                []);
        }

        try
        {
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var findings = await adapter.GetStatementCacheDiagnosticsAsync(connection, cancellationToken)
                .ConfigureAwait(false);
            return new QueryDuckStatementCacheDiagnostics(provider.ToString(), true, findings);
        }
        catch (Exception ex)
        {
            return new QueryDuckStatementCacheDiagnostics(
                provider.ToString(),
                true,
                [new StatementCacheFinding("error", 0, $"Statement cache diagnostics failed: {ex.Message}")]);
        }
    }
}
