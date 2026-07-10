using System.Data.Common;

namespace QueryDuck.Core.Adapters;

internal static class DbParameterExtractor
{
    public static IReadOnlyDictionary<string, object?> ToDictionary(DbCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.Parameters.Cast<DbParameter>()
            .ToDictionary(p => p.ParameterName, p => (object?)p.Value, StringComparer.OrdinalIgnoreCase);
    }
}
