using System.Data;
using System.Data.Common;

namespace QueryDuck.Core.Adapters;

internal static class DbConnectionExtensions
{
    public static async Task EnsureOpenAsync(this DbConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
