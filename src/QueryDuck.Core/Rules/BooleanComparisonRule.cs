using QueryDuck.Core.Diagnostics;
using System.Linq.Expressions;
using QueryDuck.Core.Providers;

namespace QueryDuck.Core.Rules;

internal sealed class BooleanComparisonRule : QueryRuleBase
{
    public override string Id => "QD008";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        BooleanVisitor.Run(() => new BooleanVisitor(Id, context.Provider), context.Expression);

    private sealed class BooleanVisitor(string ruleId, DatabaseProvider provider) : DiagnosticRuleVisitor(ruleId)
    {
        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual &&
                (IsBoolConstantComparison(node.Left, node.Right) || IsBoolConstantComparison(node.Right, node.Left)))
            {
                Info(
                    "Boolean equality comparison may map differently across providers (bit/boolean/NUMBER(1)).",
                    ProviderFixHints.BooleanComparison(provider));
            }

            return base.VisitBinary(node);
        }

        private static bool IsBoolConstantComparison(Expression side, Expression otherSide) =>
            side.Type == typeof(bool)
            && otherSide is ConstantExpression { Value: bool };
    }
}
