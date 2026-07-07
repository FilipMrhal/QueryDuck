using System.Linq.Expressions;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class TakeSkipWithoutOrderByRule : QueryRuleBase
{
    public override string Id => "QD011";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        TakeSkipVisitor.Analyze(context.Expression);

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

    private sealed class TakeSkipVisitor : ExpressionVisitor
    {
        private readonly List<QueryDiagnostic> _diagnostics = [];
        private readonly bool _hasOrderBy;

        private TakeSkipVisitor(bool hasOrderBy) => _hasOrderBy = hasOrderBy;

        public static IEnumerable<QueryDiagnostic> Analyze(Expression expression)
        {
            var scanner = new OrderByScanner();
            scanner.Visit(expression);
            var visitor = new TakeSkipVisitor(scanner.HasOrderBy);
            visitor.Visit(expression);
            return visitor._diagnostics;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(Queryable) &&
                node.Method.Name is "Take" or "Skip" &&
                !_hasOrderBy)
            {
                _diagnostics.Add(new QueryDiagnostic(
                    "QD011",
                    QueryDiagnosticSeverity.Warning,
                    $"Queryable.{node.Method.Name} without OrderBy — paging/skip results are non-deterministic.",
                    "Add OrderBy/ThenBy before Take/Skip to stabilize row selection."));
            }

            return base.VisitMethodCall(node);
        }
    }
}
