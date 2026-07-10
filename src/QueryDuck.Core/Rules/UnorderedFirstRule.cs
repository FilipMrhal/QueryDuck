using QueryDuck.Core.Diagnostics;
using System.Linq.Expressions;

namespace QueryDuck.Core.Rules;

internal sealed class UnorderedFirstRule : QueryRuleBase
{
    public override string Id => "QD009";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        FirstVisitor.Run(
            () => new FirstVisitor(Id, OrderByPresenceScanner.ContainsOrderBy(context.Expression)),
            context.Expression);

    private sealed class FirstVisitor(string ruleId, bool hasOrderBy) : DiagnosticRuleVisitor(ruleId)
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (QueryableExpressionHelpers.IsQueryableMethod(
                    node,
                    "First",
                    "FirstOrDefault",
                    "Single",
                    "SingleOrDefault",
                    "Last",
                    "LastOrDefault") &&
                !hasOrderBy)
            {
                Warn(
                    $"Queryable.{node.Method.Name} without OrderBy — results are non-deterministic across providers and executions.",
                    "Add OrderBy/ThenBy before First/Single/Last, or use Min/Max/Any when order does not matter.");
            }

            return base.VisitMethodCall(node);
        }
    }
}
