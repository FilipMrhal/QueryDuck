using System.Linq.Expressions;
using QueryDuck.Core.Diagnostics;
using QueryDuck.Core.Providers;

namespace QueryDuck.Core.Rules;

internal abstract class QueryRuleBase : IQueryRule
{
    public abstract string Id { get; }

    public virtual IReadOnlyCollection<DatabaseProvider> ApplicableProviders { get; } =
        [DatabaseProvider.Unknown, DatabaseProvider.Oracle, DatabaseProvider.PostgreSql, DatabaseProvider.SqlServer, DatabaseProvider.MySql, DatabaseProvider.Sqlite];

    public abstract IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context);

    protected QueryDiagnostic Warning(string message, string? fixHint = null) =>
        CreateWarning(Id, message, fixHint);

    protected QueryDiagnostic Info(string message, string? fixHint = null) =>
        CreateInfo(Id, message, fixHint);

    protected QueryDiagnostic Error(string message, string? fixHint = null) =>
        CreateError(Id, message, fixHint);

    internal static QueryDiagnostic CreateWarning(string ruleId, string message, string? fixHint = null) =>
        new(ruleId, QueryDiagnosticSeverity.Warning, message, fixHint);

    internal static QueryDiagnostic CreateInfo(string ruleId, string message, string? fixHint = null) =>
        new(ruleId, QueryDiagnosticSeverity.Info, message, fixHint);

    internal static QueryDiagnostic CreateError(string ruleId, string message, string? fixHint = null) =>
        new(ruleId, QueryDiagnosticSeverity.Error, message, fixHint);

    protected static bool IsNullConstant(Expression expression) =>
        expression is ConstantExpression { Value: null };

    protected static bool IsEmptyStringConstant(Expression expression) =>
        expression is ConstantExpression { Value: string s } && s.Length == 0;
}
