using System.Linq.Expressions;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class StringContainsPatternRule : QueryRuleBase
{
    public override string Id => "QD013";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        StringMethodVisitor.Analyze(context.Expression);

    private sealed class StringMethodVisitor : ExpressionVisitor
    {
        private readonly List<QueryDiagnostic> _diagnostics = [];
        private int _lambdaDepth;

        public static IEnumerable<QueryDiagnostic> Analyze(Expression expression)
        {
            var visitor = new StringMethodVisitor();
            visitor.Visit(expression);
            return visitor._diagnostics;
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            _lambdaDepth++;
            var result = base.VisitLambda(node);
            _lambdaDepth--;
            return result;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (_lambdaDepth > 0 &&
                node.Method.DeclaringType == typeof(string) &&
                node.Method.Name is "Contains" or "StartsWith" or "EndsWith" &&
                node.Object?.Type == typeof(string))
            {
                _diagnostics.Add(new QueryDiagnostic(
                    "QD013",
                    QueryDiagnosticSeverity.Info,
                    $"string.{node.Method.Name} in a LINQ predicate is often index-unfriendly (especially Contains/leading wildcards).",
                    "Prefer equality/range filters, full-text search, or provider-specific index types for text search."));
            }

            return base.VisitMethodCall(node);
        }
    }
}
