using System.Globalization;
using QueryDuck.Core.Capture;
using QueryDuck.Serilog;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace QueryDuck.Tests;

public sealed class QueryDuckSerilogTests
{
    [Fact]
    public void Redactor_detects_pii_parameter_names()
    {
        var options = new QueryDuckSensitiveDataLoggingOptions();
        Assert.True(QueryDuckSensitiveDataRedactor.IsPiiParameterName("@email", options));
        Assert.True(QueryDuckSensitiveDataRedactor.IsPiiParameterName(":Password", options));
        Assert.False(QueryDuckSensitiveDataRedactor.IsPiiParameterName("@orderId", options));
    }

    [Fact]
    public void Enricher_redacts_parameter_values_by_default()
    {
        var captureEvent = CreateEvent(parameters: new Dictionary<string, object?>
        {
            ["@email"] = "user@example.com",
            ["@orderId"] = 42,
        });

        var properties = QueryDuckSerilogEnricher.BuildProperties(
            captureEvent,
            CreateContext(captureEvent, isSlow: true),
            new QueryDuckSerilogOptions());

        Assert.DoesNotContain("Parameters", properties.Keys);
        var names = Assert.IsType<string[]>(properties["ParameterNames"]);
        Assert.Equal(["@email", "@orderId"], names);
        Assert.Equal("SELECT * FROM orders", properties["Sql"]);
    }

    [Fact]
    public void Enricher_hashes_sql_when_sql_text_disabled()
    {
        var captureEvent = CreateEvent();
        var options = new QueryDuckSerilogOptions
        {
            SensitiveData = { IncludeSqlText = false },
        };

        var properties = QueryDuckSerilogEnricher.BuildProperties(
            captureEvent,
            CreateContext(captureEvent, isSlow: true),
            options);

        Assert.DoesNotContain("Sql", properties.Keys);
        Assert.StartsWith("sha256:", properties["SqlHash"]?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Enricher_includes_parameter_values_when_sensitive_data_opted_in()
    {
        var captureEvent = CreateEvent(parameters: new Dictionary<string, object?>
        {
            ["@email"] = "user@example.com",
            ["@orderId"] = 42,
        });

        var options = new QueryDuckSerilogOptions
        {
            SensitiveData =
            {
                IncludeSensitiveData = true,
                IncludeParameterValues = true,
                IncludePii = true,
            },
        };

        var properties = QueryDuckSerilogEnricher.BuildProperties(
            captureEvent,
            CreateContext(captureEvent, isSlow: true),
            options);

        var parameters = Assert.IsType<Dictionary<string, object?>>(properties["Parameters"]);
        Assert.Equal("user@example.com", parameters["@email"]);
        Assert.Equal(42, parameters["@orderId"]);
    }

    [Fact]
    public void Enricher_redacts_pii_even_when_sensitive_data_enabled_without_pii_opt_in()
    {
        var captureEvent = CreateEvent(parameters: new Dictionary<string, object?>
        {
            ["@email"] = "user@example.com",
            ["@orderId"] = 42,
        });

        var options = new QueryDuckSerilogOptions
        {
            SensitiveData =
            {
                IncludeSensitiveData = true,
                IncludeParameterValues = true,
                IncludePii = false,
            },
        };

        var properties = QueryDuckSerilogEnricher.BuildProperties(
            captureEvent,
            CreateContext(captureEvent, isSlow: true),
            options);

        var parameters = Assert.IsType<Dictionary<string, object?>>(properties["Parameters"]);
        Assert.Equal(QueryDuckSensitiveDataRedactor.RedactedToken, parameters["@email"]);
        Assert.Equal(42, parameters["@orderId"]);
    }

    [Fact]
    public async Task Publisher_emits_slow_query_log_and_skips_successful_queries_by_default()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        var options = new QueryDuckSerilogOptions();
        var publisher = new QueryDuckSerilogEventPublisher(logger, options);

        var slowEvent = CreateEvent(durationMs: 900);
        await publisher.PublishAsync(slowEvent, CreateContext(slowEvent, isSlow: true));

        var fastEvent = CreateEvent(durationMs: 10);
        await publisher.PublishAsync(fastEvent, CreateContext(fastEvent, isSlow: false));

        Assert.Single(sink.Events);
        Assert.Equal(LogEventLevel.Warning, sink.Events[0].Level);
        Assert.Contains("slow query", sink.Events[0].RenderMessage(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Publisher_emits_failure_log_with_error_details()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        var publisher = new QueryDuckSerilogEventPublisher(logger, new QueryDuckSerilogOptions());

        var failureEvent = CreateEvent(
            succeeded: false,
            errorMessage: "no such table: missing",
            exceptionType: "Microsoft.Data.Sqlite.SqliteException");

        await publisher.PublishAsync(failureEvent, CreateContext(failureEvent, isSlow: false));

        var logEvent = Assert.Single(sink.Events);
        Assert.Equal(LogEventLevel.Error, logEvent.Level);
        Assert.Contains("SQL failure", logEvent.RenderMessage(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddSerilogExporter_registers_publisher_on_options()
    {
        var logger = new LoggerConfiguration().CreateLogger();
        var options = new QueryCaptureOptions();
        options.AddSerilogExporter(logger, o => o.LogSuccessfulQueries = true);

        Assert.Single(options.EventPublishers);
        Assert.IsType<QueryDuckSerilogEventPublisher>(options.EventPublishers[0]);
    }

    private static QueryCaptureEvent CreateEvent(
        double durationMs = 900,
        bool succeeded = true,
        string? errorMessage = null,
        string? exceptionType = null,
        IReadOnlyDictionary<string, object?>? parameters = null) =>
        new()
        {
            EventId = Guid.NewGuid().ToString("N"),
            Timestamp = DateTimeOffset.UtcNow,
            Sql = "SELECT * FROM orders",
            Provider = "Sqlite",
            Duration = TimeSpan.FromMilliseconds(durationMs),
            Parameters = parameters ?? new Dictionary<string, object?>(),
            Succeeded = succeeded,
            ErrorMessage = errorMessage,
            ExceptionType = exceptionType,
        };

    private static QueryCapturePublishContext CreateContext(QueryCaptureEvent captureEvent, bool isSlow) =>
        new()
        {
            CaptureEvent = captureEvent,
            IsSlow = isSlow,
            SlowQueryThresholdMs = 500,
        };

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
