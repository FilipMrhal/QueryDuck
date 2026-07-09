using System.Linq.Expressions;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class DynamicOrderByRule : QueryRuleBase
{
    public override string Id => "QD018";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        DynamicOrderVisitor.Analyze(context.Expression);

    private sealed class DynamicOrderVisitor : ExpressionVisitor
    {
        private readonly List<QueryDiagnostic> _diagnostics = [];

        public static IEnumerable<QueryDiagnostic> Analyze(Expression expression)
        {
            var visitor = new DynamicOrderVisitor();
            visitor.Visit(expression);
            return visitor._diagnostics;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(Queryable) &&
                node.Method.Name is "OrderBy" or "OrderByDescending" &&
                node.Arguments.Count >= 3 &&
                node.Arguments[2] is UnaryExpression { Operand: LambdaExpression lambda } &&
                UsesRuntimeKey(lambda.Body))
            {
                _diagnostics.Add(new QueryDiagnostic(
                    "QD018",
                    QueryDiagnosticSeverity.Warning,
                    "Dynamic OrderBy key may prevent index use and can be unstable across providers.",
                    "Prefer a fixed OrderBy column, or sort in memory after materialization when keys are runtime-driven."));
            }

            return base.VisitMethodCall(node);
        }

        private static bool UsesRuntimeKey(Expression body) =>
            body is ParameterExpression or MemberExpression { Expression: ParameterExpression }
                ? false
                : body.NodeType is ExpressionType.Call or ExpressionType.Invoke or ExpressionType.Conditional
                    or ExpressionType.Switch or ExpressionType.Convert;
    }
}
