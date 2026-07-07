using System.Linq.Expressions;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class DistinctWithoutProjectionRule : QueryRuleBase
{
    public override string Id => "QD022";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        DistinctVisitor.Analyze(context.Expression);

    private sealed class DistinctVisitor : ExpressionVisitor
    {
        private readonly List<QueryDiagnostic> _diagnostics = [];

        public static IEnumerable<QueryDiagnostic> Analyze(Expression expression)
        {
            var visitor = new DistinctVisitor();
            visitor.Visit(expression);
            return visitor._diagnostics;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(Queryable) &&
                node.Method.Name == "Distinct" &&
                !HasSelectAncestor(node))
            {
                _diagnostics.Add(new QueryDiagnostic(
                    "QD022",
                    QueryDiagnosticSeverity.Info,
                    "Distinct on full entities compares every mapped column — often expensive.",
                    "Project to the columns you need with Select before Distinct."));
            }

            return base.VisitMethodCall(node);
        }

        private static bool HasSelectAncestor(MethodCallExpression node)
        {
            var current = node.Arguments.FirstOrDefault() as MethodCallExpression;
            while (current is not null)
            {
                if (current.Method.Name == "Select" && current.Method.DeclaringType == typeof(Queryable))
                {
                    return true;
                }

                current = current.Arguments.FirstOrDefault() as MethodCallExpression;
            }

            return false;
        }
    }
}
