using Testcontainers.MsSql;

namespace QueryDuck.Testing.Fixtures;

public sealed class SqlServerContainerFixture : IAsyncDisposable
{
    private MsSqlContainer? _container;

    public string? ConnectionString { get; private set; }

    public static bool IsIntegrationEnabled => OracleContainerFixture.IsIntegrationEnabled;

    public async Task StartAsync()
    {
        if (!IsIntegrationEnabled)
        {
            return;
        }

        _container = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
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
