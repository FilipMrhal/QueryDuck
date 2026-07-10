using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class IgnoreQueryFiltersRule : QueryRuleBase
{
    public override string Id => "QD024";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        IgnoreFiltersVisitor.Run(() => new IgnoreFiltersVisitor(Id), context.Expression);

    private sealed class IgnoreFiltersVisitor(string ruleId) : DiagnosticRuleVisitor(ruleId)
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions) &&
                node.Method.Name == "IgnoreQueryFilters")
            {
                Warn(
                    "IgnoreQueryFilters bypasses global filters — soft-delete and tenant filters are disabled.",
                    "Document why filters are bypassed, or scope the query with explicit tenant predicates.");
            }

            return base.VisitMethodCall(node);
        }
    }
}
