using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class IncludeWithoutSplitQueryRule : QueryRuleBase
{
    public override string Id => "QD010";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        IncludeVisitor.Analyze(context.Expression);

    private sealed class IncludeVisitor : ExpressionVisitor
    {
        private readonly List<QueryDiagnostic> _diagnostics = [];
        private int _includeCount;
        private bool _hasAsSplitQuery;

        public static IEnumerable<QueryDiagnostic> Analyze(Expression expression)
        {
            var visitor = new IncludeVisitor();
            visitor.Visit(expression);
            return visitor._diagnostics;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions))
            {
                if (node.Method.Name == "Include" || node.Method.Name == "ThenInclude")
                {
                    _includeCount++;
                }
                else if (node.Method.Name == "AsSplitQuery")
                {
                    _hasAsSplitQuery = true;
                }
            }

            if (_includeCount >= 2 && !_hasAsSplitQuery)
            {
                _diagnostics.Add(new QueryDiagnostic(
                    "QD010",
                    QueryDiagnosticSeverity.Warning,
                    "Multiple Include/ThenInclude calls without AsSplitQuery — EF may generate a cartesian product join.",
                    "Call .AsSplitQuery() after Include chains, or reduce eager loading depth."));
                _includeCount = 0;
            }

            return base.VisitMethodCall(node);
        }
    }
}
