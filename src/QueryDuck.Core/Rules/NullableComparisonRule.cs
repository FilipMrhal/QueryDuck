using System.Linq.Expressions;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class NullableComparisonRule : QueryRuleBase
{
    public override string Id => "QD004";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        NullableComparisonVisitor.Analyze(context.Expression);

    private sealed class NullableComparisonVisitor : ExpressionVisitor
    {
        private readonly List<QueryDiagnostic> _diagnostics = [];

        public static IEnumerable<QueryDiagnostic> Analyze(Expression expression)
        {
            var visitor = new NullableComparisonVisitor();
            visitor.Visit(expression);
            return visitor._diagnostics;
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual)
            {
                if (MightBeNullCaptured(node.Left) || MightBeNullCaptured(node.Right))
                {
                    _diagnostics.Add(new QueryDiagnostic(
                        "QD004",
                        QueryDiagnosticSeverity.Info,
                        "Comparison involves a possibly-null captured variable — verify NULL semantics match your intent.",
                        "With UseRelationalNulls(true), NULL comparisons behave like SQL three-valued logic."));
                }
            }

            return base.VisitBinary(node);
        }

        private static bool MightBeNullCaptured(Expression expression) =>
            expression is MemberExpression { Expression: MemberExpression { Expression: ConstantExpression } member }
            && !member.Type.IsValueType;
    }
}
