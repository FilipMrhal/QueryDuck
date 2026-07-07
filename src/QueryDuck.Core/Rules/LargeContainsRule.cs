using System.Linq.Expressions;
using QueryDuck.Core.Diagnostics;
using QueryDuck.Core.Providers;

namespace QueryDuck.Core.Rules;

internal sealed class LargeContainsRule : QueryRuleBase
{
    public override string Id => "QD006";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        ContainsVisitor.Analyze(context.Expression, context.Provider);

    private sealed class ContainsVisitor : ExpressionVisitor
    {
        private readonly List<QueryDiagnostic> _diagnostics = [];
        private readonly DatabaseProvider _provider;

        private ContainsVisitor(DatabaseProvider provider) => _provider = provider;

        public static IEnumerable<QueryDiagnostic> Analyze(Expression expression, DatabaseProvider provider)
        {
            var visitor = new ContainsVisitor(provider);
            visitor.Visit(expression);
            return visitor._diagnostics;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.Name == "Contains" && IsCollectionContains(node))
            {
                _diagnostics.Add(new QueryDiagnostic(
                    "QD006",
                    QueryDiagnosticSeverity.Warning,
                    "Contains/IN-list filter uses a captured collection — large lists can hurt plans and differ by provider.",
                    ProviderHint(_provider)));
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

        private static string ProviderHint(DatabaseProvider provider) => provider switch
        {
            DatabaseProvider.Oracle =>
                "Prefer temp-table joins or batching; Oracle may expand IN lists into many OR predicates.",
            DatabaseProvider.PostgreSql =>
                "PostgreSQL maps to ANY(@p); keep arrays small or join to a staging table.",
            DatabaseProvider.SqlServer =>
                "Large IN lists may become OPENJSON/table-valued parameters; batch IDs instead.",
            DatabaseProvider.MySql =>
                "MySQL IN lists can exceed optimizer limits; batch IDs or use a temp table join.",
            _ => "Batch large ID lists or join to a staging table instead of sending huge IN clauses.",
        };
    }
}
