using QueryDuck.Core.Adapters;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("QueryDuck.EntityFrameworkExtensions")]

namespace QueryDuck.Core.Capture;

internal static class QueryDuckCaptureRuntime
{
    internal static QueryCaptureOptions? CurrentOptions { get; set; }

    internal static DatabaseAdapterRegistry? Adapters { get; set; }
}
