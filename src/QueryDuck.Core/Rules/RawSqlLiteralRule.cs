using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class RawSqlLiteralRule : QueryRuleBase
{
    public override string Id => "QD016";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        RawSqlVisitor.Analyze(context.Expression);

    private sealed class RawSqlVisitor : ExpressionVisitor
    {
        private readonly List<QueryDiagnostic> _diagnostics = [];

        public static IEnumerable<QueryDiagnostic> Analyze(Expression expression)
        {
            var visitor = new RawSqlVisitor();
            visitor.Visit(expression);
            return visitor._diagnostics;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(RelationalQueryableExtensions) &&
                node.Method.Name is "FromSqlRaw" or "FromSql" &&
                node.Arguments.Count >= 2 &&
                node.Arguments[1] is ConstantExpression { Value: string literal } &&
                !literal.Contains('?', StringComparison.Ordinal) &&
                !literal.Contains('@', StringComparison.Ordinal))
            {
                _diagnostics.Add(new QueryDiagnostic(
                    "QD016",
                    QueryDiagnosticSeverity.Warning,
                    "FromSqlRaw/FromSql uses a string literal without parameters — plan cache churn and injection risk.",
                    "Use parameterized FromSql/FromSqlInterpolated or FormattableString overloads."));
            }

            if (node.Method.DeclaringType == typeof(RelationalDatabaseFacadeExtensions) &&
                node.Method.Name is "ExecuteSqlRaw" or "ExecuteSql" &&
                node.Arguments.Count >= 2 &&
                node.Arguments[1] is ConstantExpression { Value: string execLiteral } &&
                !execLiteral.Contains('?', StringComparison.Ordinal) &&
                !execLiteral.Contains('@', StringComparison.Ordinal))
            {
                _diagnostics.Add(new QueryDiagnostic(
                    "QD016",
                    QueryDiagnosticSeverity.Warning,
                    "ExecuteSqlRaw/ExecuteSql uses a string literal without parameters.",
                    "Prefer ExecuteSqlInterpolated or pass SqlParameter values."));
            }

            return base.VisitMethodCall(node);
        }
    }
}
