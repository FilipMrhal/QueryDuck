namespace QueryDuck.Core.Adapters;

internal static class DiagnosticsLimits
{
    public const int HistoricalStatsSampleSize = 200;

    public const int StatementCacheVariantThreshold = 5;

    public const int StatementCacheResultLimit = 20;

    public const string UnsupportedStatementCacheSignature = "unsupported";
}
