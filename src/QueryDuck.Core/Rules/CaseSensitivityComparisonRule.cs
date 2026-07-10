using System.Linq.Expressions;
using QueryDuck.Core.Diagnostics;
using QueryDuck.Core.Providers;

namespace QueryDuck.Core.Rules;

internal sealed class CaseSensitivityComparisonRule : QueryRuleBase
{
    public override string Id => "QD005";

    public override IReadOnlyCollection<DatabaseProvider> ApplicableProviders { get; } =
        [DatabaseProvider.SqlServer, DatabaseProvider.MySql];

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        CaseSensitivityVisitor.Run(() => new CaseSensitivityVisitor(Id), context.Expression);

    private sealed class CaseSensitivityVisitor(string ruleId) : DiagnosticRuleVisitor(ruleId)
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.Name is "Equals" or "Compare" &&
                node.Method.DeclaringType == typeof(string))
            {
                Info(
                    "String comparison on SQL Server/MySQL may use case-insensitive collation by default.",
                    "Verify collation matches PostgreSQL/Oracle case-sensitive expectations if porting queries.");
            }

            return base.VisitMethodCall(node);
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual &&
                node.Left.Type == typeof(string))
            {
                Info(
                    "String equality comparison on SQL Server/MySQL may be case-insensitive.",
                    "Use explicit collation or EF.Functions.Collate when case sensitivity matters.");
            }

            return base.VisitBinary(node);
        }
    }
}
