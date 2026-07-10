using System.Data.Common;

namespace QueryDuck.Core.Adapters;

internal static class HistoricalStatsQueryHelper
{
    public static async Task<QueryHistoricalStatsInsight?> TryMatchAsync(
        DbConnection connection,
        string sql,
        string commandText,
        Func<DbDataReader, string, QueryHistoricalStatsInsight?> tryMapRow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandText);
        ArgumentNullException.ThrowIfNull(tryMapRow);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = commandText;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var insight = tryMapRow(reader, sql);
                if (insight is not null)
                {
                    return insight;
                }
            }
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }
}
