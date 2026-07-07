using QueryDuck.Core.Debugging;
using VerifyTests;

namespace QueryDuck.Testing.Snapshots;

public static class SqlSnapshot
{
    public static Task VerifyQueryString(this IQueryable query, string? snapshotName = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        var debug = query.Debug();
        var name = snapshotName ?? query.Expression.Type.Name;
        return Verifier.Verify(debug.Sql)
            .UseFileName(name)
            .UseDirectory("Snapshots");
    }
}
