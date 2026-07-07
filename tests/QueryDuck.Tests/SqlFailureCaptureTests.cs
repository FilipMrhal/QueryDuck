using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QueryDuck.Core;
using QueryDuck.Core.Capture;
using QueryDuck.Sample;

namespace QueryDuck.Tests;

[Collection("QueryDuckCapture")]
public sealed class SqlFailureCaptureTests : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        QueryDuckCapture.Clear();
        await QueryDuckEventServerHost.StopAsync();
    }

    public Task DisposeAsync() => InitializeAsync();

    [Fact]
    public async Task CommandFailedAsync_captures_failure_event_with_error_metadata()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseSqlite(connection)
            .UseQueryDuckCapture(o =>
            {
                o.StartLocalEventServer = false;
                o.PublishEvents = false;
                o.AutoCaptureAllQueries = false;
            })
            .Options;

        await using var context = new SampleDbContext(options);
        await context.Database.EnsureCreatedAsync();
        QueryDuckCapture.Clear();

        await Assert.ThrowsAnyAsync<Exception>(() =>
            context.Database.ExecuteSqlRawAsync("SELECT * FROM definitely_missing_table"));

        var captured = Assert.Single(QueryDuckCapture.LastCommands);
        Assert.False(captured.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(captured.ErrorMessage));
        Assert.False(string.IsNullOrWhiteSpace(captured.ExceptionType));
        Assert.Contains("definitely_missing_table", captured.Sql, StringComparison.OrdinalIgnoreCase);
    }
}
