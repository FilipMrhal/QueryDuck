using QueryDuck.Core.Capture;
using QueryDuck.Serilog;
using Serilog;

namespace QueryDuck.Tests;

public sealed class ProductionPresetTests
{
    [Fact]
    public void ProductionDefaults_enable_serilog_and_disable_http_server()
    {
        var options = new QueryCaptureOptions();

        QueryDuckSerilogOptionsBuilderExtensions.ConfigureProductionDefaults(
            options,
            new LoggerConfiguration().CreateLogger(),
            configureSerilog: null,
            configure: null);

        Assert.False(options.StartLocalEventServer);
        Assert.False(options.PublishEvents);
        Assert.Single(options.EventPublishers.OfType<QueryDuckSerilogEventPublisher>());
    }

    [Fact]
    public void ProductionDefaults_can_be_overridden_to_reenable_server()
    {
        var options = new QueryCaptureOptions();

        QueryDuckSerilogOptionsBuilderExtensions.ConfigureProductionDefaults(
            options,
            new LoggerConfiguration().CreateLogger(),
            configureSerilog: serilog => serilog.LogSuccessfulQueries = true,
            configure: o => o.StartLocalEventServer = true);

        Assert.True(options.StartLocalEventServer);
        Assert.Single(options.EventPublishers.OfType<QueryDuckSerilogEventPublisher>());
    }
}
