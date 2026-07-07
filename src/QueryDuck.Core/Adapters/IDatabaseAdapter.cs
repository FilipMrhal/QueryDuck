using System.Data.Common;
using Microsoft.EntityFrameworkCore.Metadata;
using QueryDuck.Core.Providers;

namespace QueryDuck.Core.Adapters;

public sealed record SchemaColumnInfo(
    string TableName,
    string ColumnName,
    string DataType,
    bool IsNullable,
    int? MaxLength,
    int? Precision,
    int? Scale);

public sealed record NullabilityMismatch(
    string EntityType,
    string PropertyName,
    string ColumnName,
    bool ModelIsNullable,
    bool DatabaseIsNullable,
    string Message);

public sealed record TypeMismatch(
    string EntityType,
    string PropertyName,
    string ColumnName,
    string ModelType,
    string DatabaseType,
    string Message);

public sealed record SchemaAuditResult(
    IReadOnlyList<NullabilityMismatch> NullabilityMismatches,
    IReadOnlyList<TypeMismatch> TypeMismatches)
{
    public bool HasIssues => NullabilityMismatches.Count > 0 || TypeMismatches.Count > 0;
}

public sealed record ExecutionPlanResult(
    string PlanText,
    string PlanHash);

public sealed record StatementCacheFinding(
    string Signature,
    int VariantCount,
    string Message);

public interface IDatabaseAdapter
{
    DatabaseProvider Provider { get; }

    Task<SchemaAuditResult> AuditSchemaAsync(IModel model, DbConnection connection, CancellationToken cancellationToken = default);

    Task<ExecutionPlanResult> GetExecutionPlanAsync(
        DbConnection connection,
        string sql,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StatementCacheFinding>> GetStatementCacheDiagnosticsAsync(
        DbConnection connection,
        CancellationToken cancellationToken = default);

    Task<PgStatStatementInsight?> TryMatchPgStatStatementAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<PgStatStatementInsight?>(null);

    Task<IReadOnlyList<ColumnStatistics>> GetColumnStatisticsAsync(
        DbConnection connection,
        string schema,
        string table,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ColumnStatistics>>([]);
}
