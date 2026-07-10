using QueryDuck.Core;
using QueryDuck.Core.Capture;

namespace QueryDuck.Testing.Fixtures;

public sealed class QueryDuckCaptureReset : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        QueryDuckCapture.Clear();
        await QueryDuckEventServerHost.StopAsync().ConfigureAwait(false);
    }

    public Task DisposeAsync() => InitializeAsync();
}
