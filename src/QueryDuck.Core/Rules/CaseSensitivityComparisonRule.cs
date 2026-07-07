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
        CaseSensitivityVisitor.Analyze(context.Expression);

    private sealed class CaseSensitivityVisitor : ExpressionVisitor
    {
        private readonly List<QueryDiagnostic> _diagnostics = [];

        public static IEnumerable<QueryDiagnostic> Analyze(Expression expression)
        {
            var visitor = new CaseSensitivityVisitor();
            visitor.Visit(expression);
            return visitor._diagnostics;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.Name is "Equals" or "Compare" &&
                node.Method.DeclaringType == typeof(string))
            {
                _diagnostics.Add(new QueryDiagnostic(
                    "QD005",
                    QueryDiagnosticSeverity.Info,
                    "String comparison on SQL Server/MySQL may use case-insensitive collation by default.",
                    "Verify collation matches PostgreSQL/Oracle case-sensitive expectations if porting queries."));
            }

            return base.VisitMethodCall(node);
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual &&
                node.Left.Type == typeof(string))
            {
                _diagnostics.Add(new QueryDiagnostic(
                    "QD005",
                    QueryDiagnosticSeverity.Info,
                    "String equality comparison on SQL Server/MySQL may be case-insensitive.",
                    "Use explicit collation or EF.Functions.Collate when case sensitivity matters."));
            }

            return base.VisitBinary(node);
        }
    }
}
