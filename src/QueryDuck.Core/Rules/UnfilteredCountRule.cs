using QueryDuck.Core.Diagnostics;
using System.Linq.Expressions;

namespace QueryDuck.Core.Rules;

internal sealed class UnfilteredCountRule : QueryRuleBase
{
    public override string Id => "QD019";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        UnfilteredCountVisitor.Run(() => new UnfilteredCountVisitor(Id), context.Expression);

    private sealed class UnfilteredCountVisitor(string ruleId) : DiagnosticRuleVisitor(ruleId)
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (QueryableExpressionHelpers.IsQueryableMethod(node, "Count", "LongCount", "Any") &&
                !QueryableExpressionHelpers.HasQueryableAncestor(node, "Where"))
            {
                Info(
                    $"{node.Method.Name} without a Where filter may scan the entire table.",
                    "Add a selective Where clause, or cache counts for large tables.");
            }

            return base.VisitMethodCall(node);
        }
    }
}
