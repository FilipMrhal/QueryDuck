using Testcontainers.MariaDb;

namespace QueryDuck.Testing.Fixtures;

public sealed class MariaDbContainerFixture : IAsyncDisposable
{
    private MariaDbContainer? _container;

    public string? ConnectionString { get; private set; }

    public static bool IsIntegrationEnabled => OracleContainerFixture.IsIntegrationEnabled;

    public async Task StartAsync()
    {
        if (!IsIntegrationEnabled)
        {
            return;
        }

        _container = new MariaDbBuilder()
            .WithImage("mariadb:11")
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
