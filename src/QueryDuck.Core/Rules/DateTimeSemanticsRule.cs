using System.Linq.Expressions;
using QueryDuck.Core.Diagnostics;
using QueryDuck.Core.Providers;

namespace QueryDuck.Core.Rules;

internal sealed class DateTimeSemanticsRule : QueryRuleBase
{
    public override string Id => "QD007";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        DateTimeVisitor.Analyze(context.Expression, context.Provider);

    private sealed class DateTimeVisitor : ExpressionVisitor
    {
        private readonly List<QueryDiagnostic> _diagnostics = [];
        private readonly DatabaseProvider _provider;

        private DateTimeVisitor(DatabaseProvider provider) => _provider = provider;

        public static IEnumerable<QueryDiagnostic> Analyze(Expression expression, DatabaseProvider provider)
        {
            var visitor = new DateTimeVisitor(provider);
            visitor.Visit(expression);
            return visitor._diagnostics;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (IsCurrentTimestampMember(node.Method.DeclaringType, node.Method.Name))
            {
                AddDiagnostic(node.Method.DeclaringType?.Name ?? "DateTime", node.Method.Name);
            }

            return base.VisitMethodCall(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (IsCurrentTimestampMember(node.Member.DeclaringType, node.Member.Name))
            {
                AddDiagnostic(node.Member.DeclaringType?.Name ?? "DateTime", node.Member.Name);
            }

            return base.VisitMember(node);
        }

        private void AddDiagnostic(string typeName, string memberName)
        {
            _diagnostics.Add(new QueryDiagnostic(
                "QD007",
                QueryDiagnosticSeverity.Info,
                $"Current timestamp member '{typeName}.{memberName}' is evaluated by the database, not C#.",
                ProviderHint(_provider, memberName)));
        }

        private static bool IsCurrentTimestampMember(Type? declaringType, string name) =>
            declaringType == typeof(DateTime)
                && name is nameof(DateTime.Now) or nameof(DateTime.UtcNow) or nameof(DateTime.Today)
            || declaringType == typeof(DateTimeOffset)
                && name is nameof(DateTimeOffset.Now) or nameof(DateTimeOffset.UtcNow);

        private static string ProviderHint(DatabaseProvider provider, string methodName) => provider switch
        {
            DatabaseProvider.Oracle =>
                $"Oracle maps to SYSDATE/SYSTIMESTAMP (session timezone). Prefer UTC instants from the app for {methodName}.",
            DatabaseProvider.PostgreSql =>
                $"PostgreSQL uses NOW()/CURRENT_TIMESTAMP (session timezone). Consider timestamptz + UTC values for {methodName}.",
            DatabaseProvider.SqlServer =>
                $"SQL Server uses GETDATE()/SYSUTCDATETIME(). Align with datetimeoffset and UTC for {methodName}.",
            DatabaseProvider.MySql =>
                $"MySQL uses NOW()/UTC_TIMESTAMP(). Session time_zone affects results for {methodName}.",
            _ => "Capture the timestamp in a local variable so EF parameterizes it consistently across providers.",
        };
    }
}
