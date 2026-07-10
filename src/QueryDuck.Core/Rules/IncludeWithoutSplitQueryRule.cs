using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class IncludeWithoutSplitQueryRule : QueryRuleBase
{
    public override string Id => "QD010";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        IncludeVisitor.Run(() => new IncludeVisitor(Id), context.Expression);

    private sealed class IncludeVisitor(string ruleId) : DiagnosticRuleVisitor(ruleId)
    {
        private int _includeCount;
        private bool _hasAsSplitQuery;

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions))
            {
                if (node.Method.Name is "Include" or "ThenInclude")
                {
                    _includeCount++;
                }
                else if (node.Method.Name == "AsSplitQuery")
                {
                    _hasAsSplitQuery = true;
                }
            }

            if (_includeCount >= 2 && !_hasAsSplitQuery)
            {
                Warn(
                    "Multiple Include/ThenInclude calls without AsSplitQuery — EF may generate a cartesian product join.",
                    "Call .AsSplitQuery() after Include chains, or reduce eager loading depth.");
                _includeCount = 0;
            }

            return base.VisitMethodCall(node);
        }
    }
}
