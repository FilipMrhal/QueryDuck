using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;
using QueryDuck.Core.Providers;

namespace QueryDuck.Core.Diagnostics;

public enum QueryDiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

public sealed record QueryDiagnostic(
    string RuleId,
    QueryDiagnosticSeverity Severity,
    string Message,
    string? FixHint = null,
    string? NodePath = null);

public sealed class QueryRuleContext
{
    public QueryRuleContext(Expression expression, DatabaseProvider provider, IModel? model = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        Expression = expression;
        Provider = provider;
        Model = model;
    }

    public Expression Expression { get; }

    public DatabaseProvider Provider { get; }

    public IModel? Model { get; }
}

public interface IQueryRule
{
    string Id { get; }

    IReadOnlyCollection<DatabaseProvider> ApplicableProviders { get; }

    IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context);
}
