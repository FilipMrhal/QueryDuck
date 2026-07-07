using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using QueryDuck.Core.Adapters;

namespace QueryDuck.Core.Capture;

public sealed class QueryCaptureInterceptor : DbCommandInterceptor
{
    private readonly QueryCapturePipeline _pipeline;

    public QueryCaptureInterceptor(
        QueryCaptureOptions? options = null,
        DatabaseAdapterRegistry? adapters = null)
    {
        var resolvedOptions = options ?? new QueryCaptureOptions();
        _pipeline = new QueryCapturePipeline(resolvedOptions, adapters);
        QueryDuckCaptureRuntime.CurrentOptions = resolvedOptions;
        QueryDuckCaptureRuntime.Adapters = adapters;
    }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        await RecordAsync(command, eventData, eventData.Duration, cancellationToken).ConfigureAwait(false);
        return await base.ReaderExecutedAsync(command, eventData, result, cancellationToken).ConfigureAwait(false);
    }

    public override async ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        await RecordAsync(command, eventData, eventData.Duration, cancellationToken).ConfigureAwait(false);
        return await base.NonQueryExecutedAsync(command, eventData, result, cancellationToken).ConfigureAwait(false);
    }

    public override async ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        await RecordAsync(command, eventData, eventData.Duration, cancellationToken).ConfigureAwait(false);
        return await base.ScalarExecutedAsync(command, eventData, result, cancellationToken).ConfigureAwait(false);
    }

    public override async Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(eventData);
        await _pipeline.RecordFailureAsync(
            command,
            eventData.Context,
            eventData.Duration,
            eventData.CommandSource.ToString(),
            eventData.Exception,
            cancellationToken).ConfigureAwait(false);
        await base.CommandFailedAsync(command, eventData, cancellationToken).ConfigureAwait(false);
    }

    private Task RecordAsync(
        DbCommand command,
        CommandEventData eventData,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(eventData);
        return _pipeline.RecordAsync(
            command,
            eventData.Context,
            duration,
            eventData.CommandSource.ToString(),
            cancellationToken);
    }
}
