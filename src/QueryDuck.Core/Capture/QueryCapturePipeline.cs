using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using QueryDuck.Core.Adapters;
using QueryDuck.Core.Learning;
using QueryDuck.Core.Performance;
using QueryDuck.Core.Providers;

namespace QueryDuck.Core.Capture;

public sealed class QueryCapturePipeline
{
    private readonly QueryCaptureOptions _options;
    private readonly IReadOnlyList<IQueryCaptureEventPublisher> _publishers;

    public QueryCapturePipeline(
        QueryCaptureOptions? options = null,
        DatabaseAdapterRegistry? adapters = null)
    {
        _options = options ?? new QueryCaptureOptions();
        _adapters = adapters;
        QueryDuckCapture.SharedBuffer.Configure(_options.BufferCapacity);
        _publishers = BuildPublishers(_options);
    }

    private readonly DatabaseAdapterRegistry? _adapters;

    public Task RecordAsync(
        DbCommand command,
        DbContext? context,
        TimeSpan duration,
        string caller,
        CancellationToken cancellationToken = default) =>
        RecordAsync(
            command,
            context,
            duration,
            caller,
            QueryCaptureSources.EfCore,
            bulkOperation: null,
            cancellationToken);

    public Task RecordAsync(
        DbCommand command,
        DbContext? context,
        TimeSpan duration,
        string caller,
        string source,
        string? bulkOperation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var parameters = command.Parameters.Cast<DbParameter>()
            .ToDictionary(p => p.ParameterName, p => (object?)p.Value, StringComparer.OrdinalIgnoreCase);

        return RecordCoreAsync(
            command.CommandText,
            context,
            duration,
            caller,
            source,
            bulkOperation,
            parameters,
            succeeded: true,
            errorMessage: null,
            exceptionType: null,
            cancellationToken);
    }

    public Task RecordFailureAsync(
        DbCommand command,
        DbContext? context,
        TimeSpan duration,
        string caller,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(exception);
        var parameters = command.Parameters.Cast<DbParameter>()
            .ToDictionary(p => p.ParameterName, p => (object?)p.Value, StringComparer.OrdinalIgnoreCase);

        return RecordCoreAsync(
            command.CommandText,
            context,
            duration,
            caller,
            QueryCaptureSources.EfCore,
            bulkOperation: null,
            parameters,
            succeeded: false,
            errorMessage: exception.Message,
            exceptionType: exception.GetType().FullName ?? exception.GetType().Name,
            cancellationToken);
    }

    public Task RecordSqlAsync(
        string sql,
        DbContext? context,
        TimeSpan duration,
        string caller,
        string source,
        string? bulkOperation,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default) =>
        RecordCoreAsync(
            sql,
            context,
            duration,
            caller,
            source,
            bulkOperation,
            parameters ?? new Dictionary<string, object?>(),
            succeeded: true,
            errorMessage: null,
            exceptionType: null,
            cancellationToken);

    private async Task RecordCoreAsync(
        string sql,
        DbContext? context,
        TimeSpan duration,
        string caller,
        string source,
        string? bulkOperation,
        IReadOnlyDictionary<string, object?> parameters,
        bool succeeded,
        string? errorMessage,
        string? exceptionType,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        var provider = DatabaseProviderNames.FromProviderName(context?.Database.ProviderName);
        var isSlow = _options.SlowQueryThresholdMs > 0 &&
            duration.TotalMilliseconds >= _options.SlowQueryThresholdMs;

        if (succeeded && !isSlow && _options.EnableSampling && !QueryCaptureSampling.ShouldCapture(_options))
        {
            return;
        }

        var shouldCapturePlan = succeeded &&
            (_options.CaptureExecutionPlans || (_options.CapturePlansForSlowQueries && isSlow));

        string? planText = null;
        string? planHash = null;
        IDatabaseAdapter? adapter = null;
        DbConnection? connection = null;

        if (context?.Database.GetDbConnection() is { } runtimeConnection)
        {
            QueryDuckCaptureRuntime.LastConnection = runtimeConnection;
            QueryDuckCaptureRuntime.LastProviderName = context.Database.ProviderName;
        }

        if (shouldCapturePlan &&
            _adapters?.Resolve(provider) is { } resolvedAdapter &&
            context?.Database.GetDbConnection() is { } dbConnection)
        {
            adapter = resolvedAdapter;
            connection = dbConnection;
            try
            {
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                }

                var plan = await adapter.GetExecutionPlanAsync(connection, sql, parameters, cancellationToken)
                    .ConfigureAwait(false);
                planText = plan.PlanText;
                planHash = plan.PlanHash;
            }
            catch (Exception)
            {
                // Plan capture is best-effort during debugging.
            }
        }

        SlowQueryImprovementAnalysisDto? improvementAnalysis = null;
        if (succeeded && _options.AnalyzeSlowQueries && isSlow)
        {
            var draftEvent = new QueryCaptureEvent
            {
                EventId = Guid.NewGuid().ToString("N"),
                Timestamp = DateTimeOffset.UtcNow,
                Sql = sql,
                Provider = provider.ToString(),
                Source = source,
                BulkOperation = bulkOperation,
                Duration = duration,
                ExecutionPlan = planText,
                PlanHash = planHash,
            };

            PgStatStatementInsight? pgStat = null;
            QueryHistoricalStatsInsight? historicalStats = null;
            Dictionary<string, IReadOnlyList<ColumnStatistics>> tableStatistics = new(StringComparer.OrdinalIgnoreCase);

            if (adapter is not null && connection is not null)
            {
                try
                {
                    if (connection.State != System.Data.ConnectionState.Open)
                    {
                        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                    }

                    if (_options.EnableHistoricalStatsInsights)
                    {
                        historicalStats = await adapter.TryMatchHistoricalStatsAsync(connection, sql, cancellationToken)
                            .ConfigureAwait(false);
                        if (historicalStats is not null && provider == DatabaseProvider.PostgreSql)
                        {
                            pgStat = new PgStatStatementInsight(
                                historicalStats.Calls,
                                historicalStats.MeanExecTimeMs,
                                historicalStats.TotalExecTimeMs,
                                historicalStats.Rows,
                                historicalStats.CacheHitRatio ?? 0,
                                historicalStats.MatchedQueryText);
                        }
                    }

                    if (_options.EnableStatisticsBasedIndexRecommendations)
                    {
                        tableStatistics = await LoadTableStatisticsAsync(
                            adapter,
                            connection,
                            provider,
                            sql,
                            cancellationToken).ConfigureAwait(false);
                    }

                    if (context?.Model is not null)
                    {
                        await QueryDuckSchemaAuditCache.TryRefreshAsync(
                            adapter,
                            context.Model,
                            connection,
                            cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception)
                {
                    // Optional database insights are best-effort during debugging.
                }
            }

            var improvementContext = new SlowQueryImprovementContext(
                pgStat,
                historicalStats,
                tableStatistics,
                _options.EmitMermaidPlanGraphs);

            var analysis = SlowQueryImprovementEngine.Analyze(draftEvent, improvementContext);
            if (adapter is not null && connection is not null)
            {
                analysis = await SlowQueryImprovementEngine.EnrichWithImprovedPlanAsync(
                    analysis,
                    adapter,
                    connection,
                    parameters,
                    planText,
                    _options.EmitMermaidPlanGraphs,
                    cancellationToken).ConfigureAwait(false);
            }

            if (_options.EnableHeuristicMemory)
            {
                analysis = QueryHeuristicMemory.Apply(analysis, provider.ToString());
            }

            improvementAnalysis = analysis.ToDto();
        }

        if (succeeded && isSlow && _options.EnableHeuristicMemory)
        {
            QueryHeuristicMemory.RecordSlowCapture(provider.ToString(), sql, duration.TotalMilliseconds);
        }

        var captureEvent = QueryCaptureEventFactory.Create(
            sql,
            provider,
            parameters,
            planText,
            planHash,
            duration,
            caller,
            source,
            bulkOperation,
            improvementAnalysis,
            succeeded,
            errorMessage,
            exceptionType,
            _options.CaptureSourceLocations ? SourceLocationCapture.CaptureFromCaller() : null);
        QueryDuckCapture.SharedBuffer.Add(captureEvent);
        QueryDuckTransactionTimeline.RecordQuery(captureEvent);
        QueryDuckSessionTables.Record(sql, duration);
        QueryDuckSession.Refresh(QueryDuckCapture.LastCommands, _options);
        await PublishAsync(captureEvent, isSlow, cancellationToken).ConfigureAwait(false);
    }

    private async Task PublishAsync(
        QueryCaptureEvent captureEvent,
        bool isSlow,
        CancellationToken cancellationToken)
    {
        var context = new QueryCapturePublishContext
        {
            CaptureEvent = captureEvent,
            IsSlow = isSlow,
            SlowQueryThresholdMs = _options.SlowQueryThresholdMs,
        };

        foreach (var publisher in _publishers)
        {
            await publisher.PublishAsync(captureEvent, context, cancellationToken).ConfigureAwait(false);
        }
    }

    private static IReadOnlyList<IQueryCaptureEventPublisher> BuildPublishers(QueryCaptureOptions options)
    {
        var publishers = new List<IQueryCaptureEventPublisher>(options.EventPublishers.Count + 1);
        publishers.AddRange(options.EventPublishers);
        publishers.Add(new JsonLinesEventPublisher(options));
        return publishers;
    }

    private static async Task<Dictionary<string, IReadOnlyList<ColumnStatistics>>> LoadTableStatisticsAsync(
        IDatabaseAdapter adapter,
        DbConnection connection,
        DatabaseProvider provider,
        string sql,
        CancellationToken cancellationToken)
    {
        var patterns = SqlPatternAnalyzer.Analyze(sql);
        var tables = patterns.ReferencedTables
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();

        var statistics = new Dictionary<string, IReadOnlyList<ColumnStatistics>>(StringComparer.OrdinalIgnoreCase);
        foreach (var tableReference in tables)
        {
            var (schema, table) = ProviderSchemaHelper.ResolveTableReference(tableReference, provider);
            if (provider == DatabaseProvider.MySql && string.IsNullOrWhiteSpace(schema))
            {
                schema = connection.Database;
            }

            var columns = await adapter.GetColumnStatisticsAsync(connection, schema, table, cancellationToken)
                .ConfigureAwait(false);
            if (columns.Count > 0)
            {
                statistics[tableReference] = columns;
            }
        }

        return statistics;
    }

    private static string NormalizeTableName(string table) =>
        table.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Last()
            .Trim('"', '[', ']', '`');
}

public static class QueryCaptureSources
{
    public const string EfCore = "EfCore";

    public const string EntityFrameworkExtensions = "EntityFrameworkExtensions";
}
