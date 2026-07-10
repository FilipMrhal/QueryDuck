using QueryDuck.Core.Diagnostics;
using System.Linq.Expressions;

namespace QueryDuck.Core.Rules;

internal sealed class DistinctWithoutProjectionRule : QueryRuleBase
{
    public override string Id => "QD022";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        DistinctVisitor.Run(() => new DistinctVisitor(Id), context.Expression);

    private sealed class DistinctVisitor(string ruleId) : DiagnosticRuleVisitor(ruleId)
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (QueryableExpressionHelpers.IsQueryableMethod(node, "Distinct") &&
                !QueryableExpressionHelpers.HasQueryableAncestor(node, "Select"))
            {
                Info(
                    "Distinct on full entities compares every mapped column — often expensive.",
                    "Project to the columns you need with Select before Distinct.");
            }

            return base.VisitMethodCall(node);
        }
    }
}
