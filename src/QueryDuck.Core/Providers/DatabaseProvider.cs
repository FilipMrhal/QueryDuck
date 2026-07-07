namespace QueryDuck.Core.Providers;

public enum DatabaseProvider
{
    Unknown,
    Oracle,
    PostgreSql,
    SqlServer,
    MySql,
}

public static class DatabaseProviderNames
{
    public const string Oracle = "Oracle.EntityFrameworkCore";
    public const string PostgreSql = "Npgsql.EntityFrameworkCore.PostgreSQL";
    public const string SqlServer = "Microsoft.EntityFrameworkCore.SqlServer";
    public const string MySql = "Pomelo.EntityFrameworkCore.MySql";

    public static DatabaseProvider FromProviderName(string? providerName) => providerName switch
    {
        Oracle => DatabaseProvider.Oracle,
        PostgreSql => DatabaseProvider.PostgreSql,
        SqlServer => DatabaseProvider.SqlServer,
        MySql => DatabaseProvider.MySql,
        _ => DatabaseProvider.Unknown,
    };
}
