using System.Linq.Expressions;
using QueryDuck.Core.Diagnostics;
using QueryDuck.Core.Providers;

namespace QueryDuck.Core.Rules;

internal sealed class BooleanComparisonRule : QueryRuleBase
{
    public override string Id => "QD008";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        BooleanVisitor.Analyze(context.Expression, context.Provider);

    private sealed class BooleanVisitor : ExpressionVisitor
    {
        private readonly List<QueryDiagnostic> _diagnostics = [];
        private readonly DatabaseProvider _provider;

        private BooleanVisitor(DatabaseProvider provider) => _provider = provider;

        public static IEnumerable<QueryDiagnostic> Analyze(Expression expression, DatabaseProvider provider)
        {
            var visitor = new BooleanVisitor(provider);
            visitor.Visit(expression);
            return visitor._diagnostics;
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual &&
                (IsBoolConstantComparison(node.Left, node.Right) || IsBoolConstantComparison(node.Right, node.Left)))
            {
                _diagnostics.Add(new QueryDiagnostic(
                    "QD008",
                    QueryDiagnosticSeverity.Info,
                    "Boolean equality comparison may map differently across providers (bit/boolean/NUMBER(1)).",
                    ProviderHint(_provider)));
            }

            return base.VisitBinary(node);
        }

        private static bool IsBoolConstantComparison(Expression side, Expression otherSide) =>
            side.Type == typeof(bool)
            && otherSide is ConstantExpression { Value: bool };

        private static string ProviderHint(DatabaseProvider provider) => provider switch
        {
            DatabaseProvider.Oracle =>
                "Oracle often stores flags as NUMBER(1); prefer nullable bool? or explicit 0/1 comparisons.",
            DatabaseProvider.PostgreSql =>
                "PostgreSQL uses native boolean; avoid comparing to 0/1 literals.",
            DatabaseProvider.SqlServer =>
                "SQL Server uses bit; NULL comparisons behave like SQL three-valued logic.",
            DatabaseProvider.MySql =>
                "MySQL may map bool to TINYINT(1); verify provider boolean type mapping.",
            _ => "Prefer filtering on the bool property directly instead of comparing to true/false literals.",
        };
    }
}
