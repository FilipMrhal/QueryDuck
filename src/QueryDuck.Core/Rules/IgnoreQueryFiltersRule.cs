using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class IgnoreQueryFiltersRule : QueryRuleBase
{
    public override string Id => "QD024";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        IgnoreFiltersVisitor.Analyze(context.Expression);

    private sealed class IgnoreFiltersVisitor : ExpressionVisitor
    {
        private readonly List<QueryDiagnostic> _diagnostics = [];

        public static IEnumerable<QueryDiagnostic> Analyze(Expression expression)
        {
            var visitor = new IgnoreFiltersVisitor();
            visitor.Visit(expression);
            return visitor._diagnostics;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions) &&
                node.Method.Name == "IgnoreQueryFilters")
            {
                _diagnostics.Add(new QueryDiagnostic(
                    "QD024",
                    QueryDiagnosticSeverity.Warning,
                    "IgnoreQueryFilters bypasses global filters — soft-delete and tenant filters are disabled.",
                    "Document why filters are bypassed, or scope the query with explicit tenant predicates."));
            }

            return base.VisitMethodCall(node);
        }
    }
}
