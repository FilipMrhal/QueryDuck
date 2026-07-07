using DotNet.Testcontainers.Builders;
using Testcontainers.PostgreSql;

namespace QueryDuck.Testing.Fixtures;

public sealed class PostgreSqlContainerFixture : IAsyncDisposable
{
    private PostgreSqlContainer? _container;

    public string? ConnectionString { get; private set; }

    public static bool IsIntegrationEnabled => OracleContainerFixture.IsIntegrationEnabled;

    public async Task StartAsync()
    {
        if (!IsIntegrationEnabled)
        {
            return;
        }

        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
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
