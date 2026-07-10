using System.Linq.Expressions;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class ExecuteUpdateDeleteRule : QueryRuleBase
{
    public override string Id => "QD017";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        BulkModificationVisitor.Run(() => new BulkModificationVisitor(Id), context.Expression);

    private sealed class BulkModificationVisitor(string ruleId) : DiagnosticRuleVisitor(ruleId)
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.Name is "ExecuteUpdate" or "ExecuteDelete" or "ExecuteUpdateAsync" or "ExecuteDeleteAsync" &&
                node.Method.DeclaringType?.FullName?.Contains("EntityFrameworkCore", StringComparison.Ordinal) == true &&
                !QueryableExpressionHelpers.HasQueryableAncestor(node, "Where"))
            {
                Warn(
                    $"{node.Method.Name} without a Where filter may affect every row in the table.",
                    "Add a selective Where clause, or document intentional full-table updates/deletes.");
            }

            return base.VisitMethodCall(node);
        }
    }
}
