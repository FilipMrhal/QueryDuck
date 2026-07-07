using System.Linq.Expressions;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class MultipleOrderByRule : QueryRuleBase
{
    public override string Id => "QD020";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        MultipleOrderVisitor.Analyze(context.Expression);

    private sealed class MultipleOrderVisitor : ExpressionVisitor
    {
        private readonly List<QueryDiagnostic> _diagnostics = [];
        private int _orderByCount;

        public static IEnumerable<QueryDiagnostic> Analyze(Expression expression)
        {
            var visitor = new MultipleOrderVisitor();
            visitor.Visit(expression);
            return visitor._diagnostics;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(Queryable) &&
                node.Method.Name is "OrderBy" or "OrderByDescending")
            {
                _orderByCount++;
                if (_orderByCount > 1)
                {
                    _diagnostics.Add(new QueryDiagnostic(
                        "QD020",
                        QueryDiagnosticSeverity.Warning,
                        "Multiple OrderBy calls — only the last ordering is effective unless ThenBy is used.",
                        "Replace subsequent OrderBy calls with ThenBy / ThenByDescending."));
                }
            }

            return base.VisitMethodCall(node);
        }
    }
}
