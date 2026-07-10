using System.Data.Common;

namespace QueryDuck.Core.Adapters;

internal static class ExplainCommandHelper
{
    public static DbCommand CreateCommand(
        DbConnection connection,
        string commandText,
        IReadOnlyDictionary<string, object?>? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandText);

        var command = connection.CreateCommand();
        command.CommandText = commandText;
        BindParameters(command, parameters);
        return command;
    }

    public static void BindParameters(DbCommand command, IReadOnlyDictionary<string, object?>? parameters)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (parameters is null || parameters.Count == 0)
        {
            return;
        }

        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = NormalizeParameterName(name);
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }

    private static string NormalizeParameterName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        if (name[0] is '@' or ':' or '$')
        {
            return name;
        }

        return $"@{name}";
    }
}
