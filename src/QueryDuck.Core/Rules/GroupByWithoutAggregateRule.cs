using System.Linq.Expressions;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class GroupByWithoutAggregateRule : QueryRuleBase
{
    public override string Id => "QD023";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        GroupByVisitor.Analyze(context.Expression);

    private sealed class GroupByVisitor : ExpressionVisitor
    {
        private readonly List<QueryDiagnostic> _diagnostics = [];
        private bool _hasGroupBy;
        private bool _hasAggregate;

        public static IEnumerable<QueryDiagnostic> Analyze(Expression expression)
        {
            var visitor = new GroupByVisitor();
            visitor.Visit(expression);
            if (visitor._hasGroupBy && !visitor._hasAggregate)
            {
                visitor._diagnostics.Add(new QueryDiagnostic(
                    "QD023",
                    QueryDiagnosticSeverity.Warning,
                    "GroupBy without an aggregate or projection may return large grouped sequences.",
                    "Project grouped results with Select, or aggregate with Sum/Count/Max/Min."));
            }

            return visitor._diagnostics;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(Queryable))
            {
                if (node.Method.Name == "GroupBy")
                {
                    _hasGroupBy = true;
                }
                else if (node.Method.Name is "Sum" or "Count" or "LongCount" or "Average" or "Min" or "Max")
                {
                    _hasAggregate = true;
                }
            }

            return base.VisitMethodCall(node);
        }
    }
}
