using System.Linq.Expressions;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class InlinedConstantRule : QueryRuleBase
{
    public override string Id => "QD002";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        InlinedConstantVisitor.Run(() => new InlinedConstantVisitor(Id), context.Expression);

    private sealed class InlinedConstantVisitor(string ruleId) : DiagnosticRuleVisitor(ruleId)
    {
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
                Warn(
                    $"Inlined constant '{expression.Type.Name}' in predicate — may cause plan cache misses / hard parses.",
                    "Capture the value in a local variable so EF parameterizes it, or use EF.Parameter() / EF.Constant() explicitly.");
            }
        }

        private static bool IsPredicateComparison(ExpressionType nodeType) =>
            nodeType is ExpressionType.Equal or ExpressionType.NotEqual
                or ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual
                or ExpressionType.LessThan or ExpressionType.LessThanOrEqual;
    }
}
