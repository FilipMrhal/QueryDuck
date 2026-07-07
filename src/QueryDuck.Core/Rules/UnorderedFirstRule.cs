using System.Linq.Expressions;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class UnorderedFirstRule : QueryRuleBase
{
    public override string Id => "QD009";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        FirstVisitor.Analyze(context.Expression);

    private sealed class OrderByScanner : ExpressionVisitor
    {
        public bool HasOrderBy { get; private set; }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(Queryable) &&
                node.Method.Name is "OrderBy" or "OrderByDescending" or "ThenBy" or "ThenByDescending")
            {
                HasOrderBy = true;
            }

            return base.VisitMethodCall(node);
        }
    }

    private sealed class FirstVisitor : ExpressionVisitor
    {
        private readonly List<QueryDiagnostic> _diagnostics = [];
        private readonly bool _hasOrderBy;

        private FirstVisitor(bool hasOrderBy) => _hasOrderBy = hasOrderBy;

        public static IEnumerable<QueryDiagnostic> Analyze(Expression expression)
        {
            var scanner = new OrderByScanner();
            scanner.Visit(expression);
            var visitor = new FirstVisitor(scanner.HasOrderBy);
            visitor.Visit(expression);
            return visitor._diagnostics;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(Queryable) &&
                node.Method.Name is "First" or "FirstOrDefault" or "Single" or "SingleOrDefault" or "Last" or "LastOrDefault" &&
                !_hasOrderBy)
            {
                _diagnostics.Add(new QueryDiagnostic(
                    "QD009",
                    QueryDiagnosticSeverity.Warning,
                    $"Queryable.{node.Method.Name} without OrderBy — results are non-deterministic across providers and executions.",
                    "Add OrderBy/ThenBy before First/Single/Last, or use Min/Max/Any when order does not matter."));
            }

            return base.VisitMethodCall(node);
        }
    }
}
