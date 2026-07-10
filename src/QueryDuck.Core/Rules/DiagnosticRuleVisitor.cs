using System.Linq.Expressions;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal abstract class DiagnosticRuleVisitor(string ruleId) : ExpressionVisitor
{
    private readonly List<QueryDiagnostic> _diagnostics = [];

    protected void Warn(string message, string? fixHint = null) =>
        _diagnostics.Add(QueryRuleBase.CreateWarning(ruleId, message, fixHint));

    protected void Info(string message, string? fixHint = null) =>
        _diagnostics.Add(QueryRuleBase.CreateInfo(ruleId, message, fixHint));

    protected void Error(string message, string? fixHint = null) =>
        _diagnostics.Add(QueryRuleBase.CreateError(ruleId, message, fixHint));

    internal IReadOnlyList<QueryDiagnostic> Results => _diagnostics;

    internal static IEnumerable<QueryDiagnostic> Run<TVisitor>(
        Func<TVisitor> createVisitor,
        Expression expression)
        where TVisitor : DiagnosticRuleVisitor
    {
        var visitor = createVisitor();
        visitor.Visit(expression);
        return visitor._diagnostics;
    }
}
