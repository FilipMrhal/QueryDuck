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

    protected static bool IsNullConstant(Expression expression) =>
        expression is ConstantExpression { Value: null };

    protected static bool IsEmptyStringConstant(Expression expression) =>
        expression is ConstantExpression { Value: string s } && s.Length == 0;
}
