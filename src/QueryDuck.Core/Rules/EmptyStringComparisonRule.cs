using System.Linq.Expressions;
using QueryDuck.Core.Diagnostics;
using QueryDuck.Core.Providers;

namespace QueryDuck.Core.Rules;

internal sealed class EmptyStringComparisonRule : QueryRuleBase
{
    public override string Id => "QD001";

    public override IReadOnlyCollection<DatabaseProvider> ApplicableProviders { get; } =
        [DatabaseProvider.Oracle];

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        EmptyStringComparisonVisitor.Analyze(context.Expression);

    private sealed class EmptyStringComparisonVisitor : ExpressionVisitor
    {
        private readonly List<QueryDiagnostic> _diagnostics = [];

        public static IEnumerable<QueryDiagnostic> Analyze(Expression expression)
        {
            var visitor = new EmptyStringComparisonVisitor();
            visitor.Visit(expression);
            return visitor._diagnostics;
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual)
            {
                if (IsEmptyStringComparison(node.Left) || IsEmptyStringComparison(node.Right))
                {
                    _diagnostics.Add(new QueryDiagnostic(
                        "QD001",
                        QueryDiagnosticSeverity.Warning,
                        "Comparing to empty string ('') on Oracle — Oracle stores empty string as NULL.",
                        "Use explicit NULL checks or make the property nullable and compare to null."));
                }
            }

            return base.VisitBinary(node);
        }

        private static bool IsEmptyStringComparison(Expression expression) =>
            IsEmptyStringConstant(expression) || IsStringEmptyMember(expression);

        private static bool IsStringEmptyMember(Expression expression) =>
            expression is MemberExpression { Member.Name: "Empty", Expression: null };
    }
}
