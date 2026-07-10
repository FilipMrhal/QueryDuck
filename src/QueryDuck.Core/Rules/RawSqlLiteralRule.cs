using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class RawSqlLiteralRule : QueryRuleBase
{
    public override string Id => "QD016";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        RawSqlVisitor.Run(() => new RawSqlVisitor(Id), context.Expression);

    private sealed class RawSqlVisitor(string ruleId) : DiagnosticRuleVisitor(ruleId)
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(RelationalQueryableExtensions) &&
                node.Method.Name is "FromSqlRaw" or "FromSql" &&
                node.Arguments.Count >= 2 &&
                node.Arguments[1] is ConstantExpression { Value: string literal } &&
                !literal.Contains('?', StringComparison.Ordinal) &&
                !literal.Contains('@', StringComparison.Ordinal))
            {
                Warn(
                    "FromSqlRaw/FromSql uses a string literal without parameters — plan cache churn and injection risk.",
                    "Use parameterized FromSql/FromSqlInterpolated or FormattableString overloads.");
            }

            if (node.Method.DeclaringType == typeof(RelationalDatabaseFacadeExtensions) &&
                node.Method.Name is "ExecuteSqlRaw" or "ExecuteSql" &&
                node.Arguments.Count >= 2 &&
                node.Arguments[1] is ConstantExpression { Value: string execLiteral } &&
                !execLiteral.Contains('?', StringComparison.Ordinal) &&
                !execLiteral.Contains('@', StringComparison.Ordinal))
            {
                Warn(
                    "ExecuteSqlRaw/ExecuteSql uses a string literal without parameters.",
                    "Prefer ExecuteSqlInterpolated or pass SqlParameter values.");
            }

            return base.VisitMethodCall(node);
        }
    }
}
