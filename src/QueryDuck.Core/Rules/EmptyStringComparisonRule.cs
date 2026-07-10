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
        EmptyStringComparisonVisitor.Run(() => new EmptyStringComparisonVisitor(Id), context.Expression);

    private sealed class EmptyStringComparisonVisitor(string ruleId) : DiagnosticRuleVisitor(ruleId)
    {
        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual &&
                (IsEmptyStringComparison(node.Left) || IsEmptyStringComparison(node.Right)))
            {
                Warn(
                    "Comparing to empty string ('') on Oracle — Oracle stores empty string as NULL.",
                    "Use explicit NULL checks or make the property nullable and compare to null.");
            }

            return base.VisitBinary(node);
        }

        private static bool IsEmptyStringComparison(Expression expression) =>
            IsEmptyStringConstant(expression) || IsStringEmptyMember(expression);

        private static bool IsStringEmptyMember(Expression expression) =>
            expression is MemberExpression { Member.Name: "Empty", Expression: null };
    }
}
