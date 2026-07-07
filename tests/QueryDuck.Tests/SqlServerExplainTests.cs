using System.Data;
using System.Data.Common;
using QueryDuck.SqlServer;

namespace QueryDuck.Tests;

public sealed class SqlServerExplainTests
{
    [Fact]
    public void ExecuteExplainPlanXml_ReturnsReaderOutput()
    {
        using var connection = new ScriptDbConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";

        var plan = command.ExecuteExplainPlanXml();

        Assert.Equal("<ShowPlanXML/>", plan);
    }

    [Fact]
    public async Task SqlServerAdapter_GetExecutionPlan_UsesExplainExtension()
    {
        var adapter = new SqlServerDatabaseAdapter();
        using var connection = new ScriptDbConnection();

        var plan = await adapter.GetExecutionPlanAsync(connection, "SELECT 1");

        Assert.Equal("<ShowPlanXML/>", plan.PlanText);
        Assert.False(string.IsNullOrWhiteSpace(plan.PlanHash));
    }

    [Fact]
    public async Task SqlServerAdapter_GetStatementCacheDiagnostics_ReturnsFindingWhenDmvUnavailable()
    {
        var adapter = new SqlServerDatabaseAdapter();
        using var connection = new ScriptDbConnection();

        var findings = await adapter.GetStatementCacheDiagnosticsAsync(connection);

        Assert.NotEmpty(findings);
        Assert.Contains(findings, f => f.Message.Contains("DMV diagnostics unavailable", StringComparison.Ordinal));
    }

    private sealed class ScriptDbConnection : DbConnection
    {
#pragma warning disable CS8764, CS8765
        public override string ConnectionString { get; set; } = string.Empty;

        public override string Database => "test";

        public override string DataSource => "test";
#pragma warning restore CS8764, CS8765

        public override string ServerVersion => "1";

        public override ConnectionState State => ConnectionState.Open;

        public override void ChangeDatabase(string databaseName)
        {
        }

        public override void Close()
        {
        }

        public override void Open()
        {
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            throw new NotSupportedException();

        protected override DbCommand CreateDbCommand() => new ScriptDbCommand(this);
    }

    private sealed class ScriptDbCommand(ScriptDbConnection connection) : DbCommand
    {
#pragma warning disable CS8764, CS8765
        public override string CommandText { get; set; } = string.Empty;
#pragma warning restore CS8764, CS8765

        public override int CommandTimeout { get; set; }

        public override CommandType CommandType { get; set; }

        public override bool DesignTimeVisible { get; set; }

        public override UpdateRowSource UpdatedRowSource { get; set; }

        protected override DbConnection? DbConnection { get; set; } = connection;

        protected override DbParameterCollection DbParameterCollection => throw new NotSupportedException();

        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel()
        {
        }

        public override int ExecuteNonQuery() => 0;

        public override object? ExecuteScalar() => null;

        public override void Prepare()
        {
        }

        protected override DbParameter CreateDbParameter() => throw new NotSupportedException();

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            if (CommandText.Contains("sys.dm_exec_query_stats", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("DMV unavailable in test connection.");
            }

            var table = new DataTable();
            table.Columns.Add("plan", typeof(string));
            table.Rows.Add("<ShowPlanXML/>");
            return table.CreateDataReader();
        }
    }
}
