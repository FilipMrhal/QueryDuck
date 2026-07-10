using QueryDuck.Core.Providers;

namespace QueryDuck.Core.Rules;

internal static class ProviderFixHints
{
    public static string BooleanComparison(DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.Oracle =>
            "Oracle often stores flags as NUMBER(1); prefer nullable bool? or explicit 0/1 comparisons.",
        DatabaseProvider.PostgreSql =>
            "PostgreSQL uses native boolean; avoid comparing to 0/1 literals.",
        DatabaseProvider.SqlServer =>
            "SQL Server uses bit; NULL comparisons behave like SQL three-valued logic.",
        DatabaseProvider.MySql =>
            "MySQL may map bool to TINYINT(1); verify provider boolean type mapping.",
        _ => "Prefer filtering on the bool property directly instead of comparing to true/false literals.",
    };

    public static string LargeContains(DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.Oracle =>
            "Prefer temp-table joins or batching; Oracle may expand IN lists into many OR predicates.",
        DatabaseProvider.PostgreSql =>
            "PostgreSQL maps to ANY(@p); keep arrays small or join to a staging table.",
        DatabaseProvider.SqlServer =>
            "Large IN lists may become OPENJSON/table-valued parameters; batch IDs instead.",
        DatabaseProvider.MySql =>
            "MySQL IN lists can exceed optimizer limits; batch IDs or use a temp table join.",
        _ => "Batch large ID lists or join to a staging table instead of sending huge IN clauses.",
    };

    public static string DateTimeSemantics(DatabaseProvider provider, string methodName) => provider switch
    {
        DatabaseProvider.Oracle =>
            $"Oracle maps to SYSDATE/SYSTIMESTAMP (session timezone). Prefer UTC instants from the app for {methodName}.",
        DatabaseProvider.PostgreSql =>
            $"PostgreSQL uses NOW()/CURRENT_TIMESTAMP (session timezone). Consider timestamptz + UTC values for {methodName}.",
        DatabaseProvider.SqlServer =>
            $"SQL Server uses GETDATE()/SYSUTCDATETIME(). Align with datetimeoffset and UTC for {methodName}.",
        DatabaseProvider.MySql =>
            $"MySQL uses NOW()/UTC_TIMESTAMP(). Session time_zone affects results for {methodName}.",
        _ => "Capture the timestamp in a local variable so EF parameterizes it consistently across providers.",
    };
}
