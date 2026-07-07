using System.Linq.Expressions;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class StringNormalizationRule : QueryRuleBase
{
    public override string Id => "QD021";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        NormalizationVisitor.Analyze(context.Expression);

    private sealed class NormalizationVisitor : ExpressionVisitor
    {
        private readonly List<QueryDiagnostic> _diagnostics = [];

        public static IEnumerable<QueryDiagnostic> Analyze(Expression expression)
        {
            var visitor = new NormalizationVisitor();
            visitor.Visit(expression);
            return visitor._diagnostics;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(string) &&
                node.Method.Name is "ToLower" or "ToUpper" or "ToLowerInvariant" or "ToUpperInvariant")
            {
                _diagnostics.Add(new QueryDiagnostic(
                    "QD021",
                    QueryDiagnosticSeverity.Info,
                    $"string.{node.Method.Name}() in a predicate prevents index use and may change collation semantics.",
                    "Compare with case-insensitive collation, or normalize values at write time."));
            }

            return base.VisitMethodCall(node);
        }
    }
}
