using System.Linq.Expressions;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class NonNullableAggregateRule : QueryRuleBase
{
    public override string Id => "QD003";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        AggregateVisitor.Analyze(context.Expression);

    private sealed class AggregateVisitor : ExpressionVisitor
    {
        private readonly List<QueryDiagnostic> _diagnostics = [];

        public static IEnumerable<QueryDiagnostic> Analyze(Expression expression)
        {
            var visitor = new AggregateVisitor();
            visitor.Visit(expression);
            return visitor._diagnostics;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(Queryable) &&
                node.Method.Name is "Sum" or "Max" or "Min" or "Average")
            {
                var returnType = node.Method.ReturnType;
                if (returnType.IsValueType && Nullable.GetUnderlyingType(returnType) is null)
                {
                    _diagnostics.Add(new QueryDiagnostic(
                        "QD003",
                        QueryDiagnosticSeverity.Warning,
                        $"Aggregate '{node.Method.Name}' returns non-nullable {returnType.Name} — SQL returns NULL for empty sets.",
                        $"Cast the selector to a nullable type, e.g. Sum(x => (decimal?)x.Amount) ?? 0."));
                }
            }

            return base.VisitMethodCall(node);
        }
    }
}
