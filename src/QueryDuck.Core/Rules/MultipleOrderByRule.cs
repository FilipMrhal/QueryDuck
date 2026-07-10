using System.Linq.Expressions;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class MultipleOrderByRule : QueryRuleBase
{
    public override string Id => "QD020";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        MultipleOrderVisitor.Run(() => new MultipleOrderVisitor(Id), context.Expression);

    private sealed class MultipleOrderVisitor(string ruleId) : DiagnosticRuleVisitor(ruleId)
    {
        private int _orderByCount;

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (QueryableExpressionHelpers.IsQueryableMethod(node, "OrderBy", "OrderByDescending"))
            {
                _orderByCount++;
                if (_orderByCount > 1)
                {
                    Warn(
                        "Multiple OrderBy calls — only the last ordering is effective unless ThenBy is used.",
                        "Replace subsequent OrderBy calls with ThenBy / ThenByDescending.");
                }
            }

            return base.VisitMethodCall(node);
        }
    }
}
