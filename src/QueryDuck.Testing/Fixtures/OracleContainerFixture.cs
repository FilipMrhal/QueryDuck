using DotNet.Testcontainers.Builders;
using Testcontainers.Oracle;

namespace QueryDuck.Testing.Fixtures;

/// <summary>
/// Testcontainers fixture for Oracle integration tests.
/// Container startup is skipped unless QUERYDUCK_RUN_INTEGRATION=true.
/// </summary>
public sealed class OracleContainerFixture : IAsyncDisposable
{
    private OracleContainer? _container;

    public string? ConnectionString { get; private set; }

    public static bool IsIntegrationEnabled =>
        string.Equals(
            Environment.GetEnvironmentVariable("QUERYDUCK_RUN_INTEGRATION"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    public async Task StartAsync()
    {
        if (!IsIntegrationEnabled)
        {
            return;
        }

        _container = new OracleBuilder()
            .WithImage("gvenzl/oracle-free:23-slim-faststart")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1521))
            .Build();

        await _container.StartAsync().ConfigureAwait(false);
        ConnectionString = _container.GetConnectionString();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
        }
    }
}
