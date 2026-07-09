using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class ExecuteUpdateDeleteRule : QueryRuleBase
{
    public override string Id => "QD017";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        BulkModificationVisitor.Analyze(context.Expression);

    private sealed class BulkModificationVisitor : ExpressionVisitor
    {
        private readonly List<QueryDiagnostic> _diagnostics = [];

        public static IEnumerable<QueryDiagnostic> Analyze(Expression expression)
        {
            var visitor = new BulkModificationVisitor();
            visitor.Visit(expression);
            return visitor._diagnostics;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.Name is "ExecuteUpdate" or "ExecuteDelete" or "ExecuteUpdateAsync" or "ExecuteDeleteAsync" &&
                node.Method.DeclaringType?.FullName?.Contains("EntityFrameworkCore", StringComparison.Ordinal) == true)
            {
                var hasFilter = HasWhereAncestor(node);
                if (!hasFilter)
                {
                    _diagnostics.Add(new QueryDiagnostic(
                        "QD017",
                        QueryDiagnosticSeverity.Warning,
                        $"{node.Method.Name} without a Where filter may affect every row in the table.",
                        "Add a selective Where clause, or document intentional full-table updates/deletes."));
                }
            }

            return base.VisitMethodCall(node);
        }

        private static bool HasWhereAncestor(MethodCallExpression node)
        {
            var current = node.Arguments.FirstOrDefault() as MethodCallExpression;
            while (current is not null)
            {
                if (current.Method.Name == "Where" && current.Method.DeclaringType == typeof(Queryable))
                {
                    return true;
                }

                current = current.Arguments.FirstOrDefault() as MethodCallExpression;
            }

            return false;
        }
    }
}
