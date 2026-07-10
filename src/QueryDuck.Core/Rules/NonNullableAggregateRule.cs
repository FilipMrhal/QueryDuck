using System.Linq.Expressions;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class NonNullableAggregateRule : QueryRuleBase
{
    public override string Id => "QD003";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        AggregateVisitor.Run(() => new AggregateVisitor(Id), context.Expression);

    private sealed class AggregateVisitor(string ruleId) : DiagnosticRuleVisitor(ruleId)
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (QueryableExpressionHelpers.IsQueryableMethod(node, "Sum", "Max", "Min", "Average"))
            {
                var returnType = node.Method.ReturnType;
                if (returnType.IsValueType && Nullable.GetUnderlyingType(returnType) is null)
                {
                    Warn(
                        $"Aggregate '{node.Method.Name}' returns non-nullable {returnType.Name} — SQL returns NULL for empty sets.",
                        $"Cast the selector to a nullable type, e.g. Sum(x => (decimal?)x.Amount) ?? 0.");
                }
            }

            return base.VisitMethodCall(node);
        }
    }
}
