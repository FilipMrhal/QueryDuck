using System.Linq.Expressions;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class UnfilteredCountRule : QueryRuleBase
{
    public override string Id => "QD019";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        UnfilteredCountVisitor.Analyze(context.Expression);

    private sealed class UnfilteredCountVisitor : ExpressionVisitor
    {
        private readonly List<QueryDiagnostic> _diagnostics = [];

        public static IEnumerable<QueryDiagnostic> Analyze(Expression expression)
        {
            var visitor = new UnfilteredCountVisitor();
            visitor.Visit(expression);
            return visitor._diagnostics;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(Queryable) &&
                node.Method.Name is "Count" or "LongCount" or "Any" &&
                !HasWhereAncestor(node))
            {
                _diagnostics.Add(new QueryDiagnostic(
                    "QD019",
                    QueryDiagnosticSeverity.Info,
                    $"{node.Method.Name} without a Where filter may scan the entire table.",
                    "Add a selective Where clause, or cache counts for large tables."));
            }

            return base.VisitMethodCall(node);
        }

        private static bool HasWhereAncestor(MethodCallExpression node)
        {
            var current = node.Arguments.FirstOrDefault() as MethodCallExpression;
            while (current is not null)
            {
                if (current.Method.Name == "Where" && current.Method.DeclaringType == typeof(Queryable))
                {
                    return true;
                }

                current = current.Arguments.FirstOrDefault() as MethodCallExpression;
            }

            return false;
        }
    }
}
