using System.Data.Common;

namespace QueryDuck.Core.Adapters;

internal static class StatementCacheDiagnosticsHelper
{
    public static async Task<IReadOnlyList<StatementCacheFinding>> QueryAsync(
        DbConnection connection,
        string commandText,
        Func<DbDataReader, StatementCacheFinding> mapRow,
        Func<Exception, string> unavailableMessage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandText);
        ArgumentNullException.ThrowIfNull(mapRow);
        ArgumentNullException.ThrowIfNull(unavailableMessage);

        var findings = new List<StatementCacheFinding>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = commandText;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                findings.Add(mapRow(reader));
            }
        }
        catch (Exception ex)
        {
            findings.Add(new StatementCacheFinding(
                DiagnosticsLimits.UnsupportedStatementCacheSignature,
                0,
                unavailableMessage(ex)));
        }

        return findings;
    }
}
