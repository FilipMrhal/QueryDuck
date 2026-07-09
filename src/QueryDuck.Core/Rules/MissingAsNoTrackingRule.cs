using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class MissingAsNoTrackingRule : QueryRuleBase
{
    public override string Id => "QD015";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        TrackingVisitor.Analyze(context.Expression);

    private sealed class TrackingVisitor : ExpressionVisitor
    {
        private readonly List<QueryDiagnostic> _diagnostics = [];
        private bool _hasAsNoTracking;
        private bool _hasAsNoTrackingWithIdentityResolution;

        public static IEnumerable<QueryDiagnostic> Analyze(Expression expression)
        {
            var visitor = new TrackingVisitor();
            visitor.Visit(expression);
            return visitor._diagnostics;
        }

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
                _diagnostics.Add(new QueryDiagnostic(
                    "QD015",
                    QueryDiagnosticSeverity.Info,
                    "Read-only projection query without AsNoTracking — EF tracks entities unnecessarily.",
                    "Add .AsNoTracking() (or AsNoTrackingWithIdentityResolution) for read-only queries."));
            }

            return base.VisitMethodCall(node);
        }

        private static bool IsReadOnlyProjection(MethodCallExpression node)
        {
            if (node.Method.DeclaringType != typeof(Queryable))
            {
                return false;
            }

            return node.Method.Name is "Select" or "ToList" or "ToArray" or "First" or "FirstOrDefault"
                or "Single" or "SingleOrDefault" or "Any" or "Count" or "LongCount";
        }
    }
}
