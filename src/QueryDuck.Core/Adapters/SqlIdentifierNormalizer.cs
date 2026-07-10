namespace QueryDuck.Core.Adapters;

internal static class SqlIdentifierNormalizer
{
    public static string NormalizeTableName(string table) =>
        table.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Last()
            .Trim('"', '[', ']', '`');
}
