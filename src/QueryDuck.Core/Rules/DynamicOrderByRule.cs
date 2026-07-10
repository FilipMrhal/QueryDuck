using System.Linq.Expressions;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class DynamicOrderByRule : QueryRuleBase
{
    public override string Id => "QD018";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        DynamicOrderVisitor.Run(() => new DynamicOrderVisitor(Id), context.Expression);

    private sealed class DynamicOrderVisitor(string ruleId) : DiagnosticRuleVisitor(ruleId)
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (TryGetOrderByLambda(node) is { Body: var keySelector } &&
                UsesRuntimeKey(keySelector))
            {
                Warn(
                    "Dynamic OrderBy key may prevent index use and can be unstable across providers.",
                    "Prefer a fixed OrderBy column, or sort in memory after materialization when keys are runtime-driven.");
            }

            return base.VisitMethodCall(node);
        }

        private static LambdaExpression? TryGetOrderByLambda(MethodCallExpression node)
        {
            if (!QueryableExpressionHelpers.IsQueryableMethod(node, "OrderBy", "OrderByDescending"))
            {
                return null;
            }

            foreach (var argument in node.Arguments)
            {
                if (argument is UnaryExpression { Operand: LambdaExpression lambda })
                {
                    return lambda;
                }
            }

            return null;
        }

        private static bool UsesRuntimeKey(Expression body) =>
            body is ParameterExpression or MemberExpression { Expression: ParameterExpression }
                ? false
                : body.NodeType is ExpressionType.Call or ExpressionType.Invoke or ExpressionType.Conditional
                    or ExpressionType.Switch or ExpressionType.Convert;
    }
}
