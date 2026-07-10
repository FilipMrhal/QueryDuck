using QueryDuck.Core;
using QueryDuck.Core.Capture;

namespace QueryDuck.Testing.Factories;

public static class QueryCaptureEventFactory
{
    public static QueryCaptureEvent Create(
        string sql = "SELECT 1",
        string provider = "Oracle",
        double durationMs = 0,
        IReadOnlyDictionary<string, object?>? parameters = null,
        bool succeeded = true,
        string? errorMessage = null,
        string? exceptionType = null) =>
        new()
        {
            EventId = Guid.NewGuid().ToString("N"),
            Timestamp = DateTimeOffset.UtcNow,
            Sql = sql,
            Provider = provider,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            Parameters = parameters ?? new Dictionary<string, object?>(),
            SchemaVersion = QueryDuckDefaults.EventSchemaVersion,
            Succeeded = succeeded,
            ErrorMessage = errorMessage,
            ExceptionType = exceptionType,
        };

    public static QueryCaptureEvent CreateMarker(string marker, string provider = "Oracle") =>
        Create(sql: $"SELECT {marker}", provider: provider);
}
