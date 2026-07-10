using System.Linq.Expressions;
using QueryDuck.Core.Diagnostics;
using QueryDuck.Core.Providers;

namespace QueryDuck.Core.Rules;

internal sealed class LargeContainsRule : QueryRuleBase
{
    public override string Id => "QD006";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        ContainsVisitor.Run(() => new ContainsVisitor(Id, context.Provider), context.Expression);

    private sealed class ContainsVisitor(string ruleId, DatabaseProvider provider) : DiagnosticRuleVisitor(ruleId)
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.Name == "Contains" && IsCollectionContains(node))
            {
                Warn(
                    "Contains/IN-list filter uses a captured collection — large lists can hurt plans and differ by provider.",
                    ProviderFixHints.LargeContains(provider));
            }

            return base.VisitMethodCall(node);
        }

        private static bool IsCollectionContains(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(Enumerable) ||
                node.Method.DeclaringType == typeof(Queryable))
            {
                return node.Arguments.Count >= 1 && LooksLikeCapturedCollection(node.Arguments[0]);
            }

            return node.Method.Name == "Contains"
                && node.Object is not null
                && LooksLikeCapturedCollection(node.Object);
        }

        private static bool LooksLikeCapturedCollection(Expression expression) =>
            expression is ConstantExpression { Value: System.Collections.IEnumerable and not string }
            || expression is MemberExpression { Expression: ConstantExpression or MemberExpression };
    }
}
