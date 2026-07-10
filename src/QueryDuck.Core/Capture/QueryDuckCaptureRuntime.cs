using System.Data.Common;
using QueryDuck.Core.Adapters;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("QueryDuck.EntityFrameworkExtensions")]

namespace QueryDuck.Core.Capture;

internal static class QueryDuckCaptureRuntime
{
    internal static QueryCaptureOptions? CurrentOptions { get; set; }

    internal static DatabaseAdapterRegistry? Adapters { get; set; }

    internal static DbConnection? LastConnection { get; set; }

    internal static string? LastProviderName { get; set; }

    internal static QueryCaptureOptions GetCurrentOptions() => CurrentOptions ?? new QueryCaptureOptions();
}
