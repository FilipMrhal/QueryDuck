using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class MissingAsNoTrackingRule : QueryRuleBase
{
    public override string Id => "QD015";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        TrackingVisitor.Run(() => new TrackingVisitor(Id), context.Expression);

    private sealed class TrackingVisitor(string ruleId) : DiagnosticRuleVisitor(ruleId)
    {
        private bool _hasAsNoTracking;
        private bool _hasAsNoTrackingWithIdentityResolution;

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions))
            {
                switch (node.Method.Name)
                {
                    case "AsNoTracking":
                        _hasAsNoTracking = true;
                        break;
                    case "AsNoTrackingWithIdentityResolution":
                        _hasAsNoTrackingWithIdentityResolution = true;
                        break;
                }
            }

            if (IsReadOnlyProjection(node) &&
                !_hasAsNoTracking &&
                !_hasAsNoTrackingWithIdentityResolution)
            {
                Info(
                    "Read-only projection query without AsNoTracking — EF tracks entities unnecessarily.",
                    "Add .AsNoTracking() (or AsNoTrackingWithIdentityResolution) for read-only queries.");
            }

            return base.VisitMethodCall(node);
        }

        private static bool IsReadOnlyProjection(MethodCallExpression node) =>
            QueryableExpressionHelpers.IsQueryableMethod(
                node,
                "Select",
                "ToList",
                "ToArray",
                "First",
                "FirstOrDefault",
                "Single",
                "SingleOrDefault",
                "Any",
                "Count",
                "LongCount");
    }
}
