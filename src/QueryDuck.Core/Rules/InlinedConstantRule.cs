using System.Linq.Expressions;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class InlinedConstantRule : QueryRuleBase
{
    public override string Id => "QD002";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        InlinedConstantVisitor.Analyze(context.Expression);

    private sealed class InlinedConstantVisitor : ExpressionVisitor
    {
        private readonly List<QueryDiagnostic> _diagnostics = [];

        public static IEnumerable<QueryDiagnostic> Analyze(Expression expression)
        {
            var visitor = new InlinedConstantVisitor();
            visitor.Visit(expression);
            return visitor._diagnostics;
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (IsPredicateComparison(node.NodeType))
            {
                CheckSide(node.Left);
                CheckSide(node.Right);
            }

            return base.VisitBinary(node);
        }

        private void CheckSide(Expression expression)
        {
            if (expression is ConstantExpression { Value: not null })
            {
                _diagnostics.Add(new QueryDiagnostic(
                    "QD002",
                    QueryDiagnosticSeverity.Warning,
                    $"Inlined constant '{expression.Type.Name}' in predicate — may cause plan cache misses / hard parses.",
                    "Capture the value in a local variable so EF parameterizes it, or use EF.Parameter() / EF.Constant() explicitly."));
            }
        }

        private static bool IsPredicateComparison(ExpressionType nodeType) =>
            nodeType is ExpressionType.Equal or ExpressionType.NotEqual
                or ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual
                or ExpressionType.LessThan or ExpressionType.LessThanOrEqual;
    }
}
