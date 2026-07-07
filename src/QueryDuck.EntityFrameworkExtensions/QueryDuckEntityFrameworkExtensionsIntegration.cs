using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QueryDuck.Core.Adapters;
using QueryDuck.Core.Capture;
using QueryDuck.Core.Learning;
using Z.BulkOperations;
using Z.EntityFramework.Extensions;
using BatchDelete = Z.EntityFramework.Extensions.BatchDelete;
using BatchUpdate = Z.EntityFramework.Extensions.BatchUpdate;

namespace QueryDuck.EntityFrameworkExtensions;

/// <summary>
/// Bridges Z.EntityFramework.Extensions bulk/batch operations into QueryDuck capture.
/// Requires a licensed <c>Z.EntityFramework.Extensions.EFCore</c> package in the consuming app.
/// </summary>
public static class QueryDuckEntityFrameworkExtensionsIntegration
{
    private static readonly ConcurrentDictionary<int, StringBuilder> OperationLogs = new();
    private static int _enabled;
    private static QueryCapturePipeline? _pipeline;
    private static Action<BulkOperation>? _previousBulkOperationBuilder;
    private static Action<BatchDelete>? _previousBatchDeleteBuilder;
    private static Action<BatchUpdate>? _previousBatchUpdateBuilder;

    /// <summary>
    /// Registers global Entity Framework Extensions hooks so bulk/batch SQL appears in QueryDuck.
    /// Call once at startup, before bulk operations run. Safe to call multiple times.
    /// </summary>
    public static void Enable(QueryCaptureOptions? options = null, DatabaseAdapterRegistry? adapters = null)
    {
        var resolvedOptions = options ?? QueryDuckCaptureRuntime.CurrentOptions ?? new QueryCaptureOptions();
        QueryDuckCaptureRuntime.CurrentOptions = resolvedOptions;
        QueryDuckCaptureRuntime.Adapters = adapters ?? QueryDuckCaptureRuntime.Adapters;
        QueryHeuristicMemory.Configure(resolvedOptions);
        _pipeline = new QueryCapturePipeline(resolvedOptions, QueryDuckCaptureRuntime.Adapters);

        if (Interlocked.CompareExchange(ref _enabled, 1, 0) == 1)
        {
            return;
        }

        WireBulkOperationBuilder();
        WireBatchDeleteBuilder();
        WireBatchUpdateBuilder();
    }

    private static void WireBulkOperationBuilder()
    {
        _previousBulkOperationBuilder = EntityFrameworkManager.BulkOperationBuilder;
        EntityFrameworkManager.BulkOperationBuilder = builder =>
        {
            _previousBulkOperationBuilder?.Invoke(builder);
            ConfigureBulkOperation(builder);
        };
    }

    private static void WireBatchDeleteBuilder()
    {
        _previousBatchDeleteBuilder = BatchDeleteManager.BatchDeleteBuilder;
        BatchDeleteManager.BatchDeleteBuilder = builder =>
        {
            _previousBatchDeleteBuilder?.Invoke(builder);
            ConfigureBatchDelete(builder);
        };
    }

    private static void WireBatchUpdateBuilder()
    {
        _previousBatchUpdateBuilder = BatchUpdateManager.BatchUpdateBuilder;
        BatchUpdateManager.BatchUpdateBuilder = builder =>
        {
            _previousBatchUpdateBuilder?.Invoke(builder);
            ConfigureBatchUpdate(builder);
        };
    }

    private static void ConfigureBulkOperation(BulkOperation builder)
    {
        builder.UseStopwatchForSqlExecutingTime = true;
        builder.UseLogDump = true;
        if (builder.LogDump is null)
        {
            builder.LogDump = new StringBuilder();
        }

        var previousLog = builder.Log;
        builder.Log = message =>
        {
            previousLog?.Invoke(message);
            AppendOperationLog(builder, message);
        };

        var previousExecuting = builder.BulkOperationExecuting;
        builder.BulkOperationExecuting = operation =>
        {
            previousExecuting?.Invoke(operation);
            OperationLogs.TryRemove(operation.GetHashCode(), out _);
            OperationLogs[operation.GetHashCode()] = new StringBuilder();
        };

        var previousExecuted = builder.BulkOperationExecuted;
        builder.BulkOperationExecuted = operation =>
        {
            previousExecuted?.Invoke(operation);
            RecordBulkOperation(operation);
        };

        var previousDeleteFromQuery = builder.DeleteFromQueryExecuted;
        builder.DeleteFromQueryExecuted = operation =>
        {
            previousDeleteFromQuery?.Invoke(operation);
            RecordBulkOperation(operation, "DeleteFromQuery");
        };
    }

    private static void ConfigureBatchDelete(BatchDelete builder)
    {
        var previous = builder.Executing;
        builder.Executing = command =>
        {
            previous?.Invoke(command);
            RecordBatchCommand(command, "DeleteFromQuery");
        };
    }

    private static void ConfigureBatchUpdate(BatchUpdate builder)
    {
        var previous = builder.Executing;
        builder.Executing = command =>
        {
            previous?.Invoke(command);
            RecordBatchCommand(command, "UpdateFromQuery");
        };
    }

    private static void AppendOperationLog(BulkOperation operation, string message)
    {
        if (OperationLogs.TryGetValue(operation.GetHashCode(), out var buffer))
        {
            buffer.AppendLine(message);
        }
    }

    private static void RecordBulkOperation(BulkOperation operation, string? operationOverride = null)
    {
        var pipeline = _pipeline;
        if (pipeline is null)
        {
            return;
        }

        var logText = operation.LogDump?.ToString();
        if (OperationLogs.TryRemove(operation.GetHashCode(), out var scopedLog) && scopedLog.Length > 0)
        {
            logText = scopedLog.ToString();
        }

        var result = operation.ResultInfo;
        var operationName = operationOverride ??
            EntityFrameworkExtensionsLogParser.ResolveBulkOperationName(
                result?.RowsAffectedInserted ?? 0,
                result?.RowsAffectedUpdated ?? 0,
                result?.RowsAffectedDeleted ?? 0,
                operation.DestinationTableName);

        var duration = operation.StopwatchForSqlExecutingTime?.Elapsed ?? TimeSpan.Zero;
        var context = operation.OriginalContext;
        var statements = EntityFrameworkExtensionsLogParser.ExtractSqlStatements(logText);
        if (statements.Count == 0)
        {
            var fallback = EntityFrameworkExtensionsLogParser.ResolvePrimarySql(logText);
            if (!string.IsNullOrWhiteSpace(fallback))
            {
                statements = [fallback];
            }
        }

        if (statements.Count == 0)
        {
            return;
        }

        foreach (var sql in statements)
        {
            _ = pipeline.RecordSqlAsync(
                sql,
                context,
                duration,
                operationName,
                QueryCaptureSources.EntityFrameworkExtensions,
                operationName);
        }
    }

    private static void RecordBatchCommand(System.Data.Common.DbCommand command, string operationName)
    {
        var pipeline = _pipeline;
        if (pipeline is null || string.IsNullOrWhiteSpace(command.CommandText))
        {
            return;
        }

        var parameters = command.Parameters.Cast<System.Data.Common.DbParameter>()
            .ToDictionary(p => p.ParameterName, p => (object?)p.Value, StringComparer.OrdinalIgnoreCase);

        _ = pipeline.RecordSqlAsync(
            command.CommandText,
            context: null,
            duration: TimeSpan.Zero,
            caller: operationName,
            source: QueryCaptureSources.EntityFrameworkExtensions,
            bulkOperation: operationName,
            parameters: parameters);
    }
}
