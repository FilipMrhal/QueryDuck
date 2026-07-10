using System.Data.Common;

namespace QueryDuck.Core.Adapters;

internal static class SchemaColumnReader
{
    public static async Task<List<SchemaColumnInfo>> QueryAsync(
        DbConnection connection,
        string commandText,
        Func<DbDataReader, SchemaColumnInfo> mapRow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandText);
        ArgumentNullException.ThrowIfNull(mapRow);

        var columns = new List<SchemaColumnInfo>();
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            columns.Add(mapRow(reader));
        }

        return columns;
    }

    public static SchemaColumnInfo MapStringNullableRow(DbDataReader reader, string nullableTrueValue = "YES") =>
        new(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3).Equals(nullableTrueValue, StringComparison.OrdinalIgnoreCase),
            ReadNullableInt32(reader, 4),
            ReadNullableInt32(reader, 5),
            ReadNullableInt32(reader, 6));

    public static SchemaColumnInfo MapSqlServerRow(DbDataReader reader) =>
        new(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetBoolean(3),
            ReadNullableInt32(reader, 4),
            ReadNullableInt32(reader, 5),
            ReadNullableInt32(reader, 6));

    public static SchemaColumnInfo MapOracleRow(DbDataReader reader) =>
        new(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3).Equals("Y", StringComparison.OrdinalIgnoreCase),
            reader.IsDBNull(4) ? null : reader.GetInt32(4),
            ReadNullableInt32(reader, 5),
            ReadNullableInt32(reader, 6));

    private static int? ReadNullableInt32(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
}
