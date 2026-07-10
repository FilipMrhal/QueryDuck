using System.Linq.Expressions;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class TakeSkipWithoutOrderByRule : QueryRuleBase
{
    public override string Id => "QD011";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        TakeSkipVisitor.Run(
            () => new TakeSkipVisitor(Id, OrderByPresenceScanner.ContainsOrderBy(context.Expression)),
            context.Expression);

    private sealed class TakeSkipVisitor(string ruleId, bool hasOrderBy) : DiagnosticRuleVisitor(ruleId)
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (QueryableExpressionHelpers.IsQueryableMethod(node, "Take", "Skip") && !hasOrderBy)
            {
                Warn(
                    $"Queryable.{node.Method.Name} without OrderBy — paging/skip results are non-deterministic.",
                    "Add OrderBy/ThenBy before Take/Skip to stabilize row selection.");
            }

            return base.VisitMethodCall(node);
        }
    }
}
