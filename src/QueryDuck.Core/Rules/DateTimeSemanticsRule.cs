using QueryDuck.Core.Diagnostics;
using System.Linq.Expressions;
using QueryDuck.Core.Providers;

namespace QueryDuck.Core.Rules;

internal sealed class DateTimeSemanticsRule : QueryRuleBase
{
    public override string Id => "QD007";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        DateTimeVisitor.Run(() => new DateTimeVisitor(Id, context.Provider), context.Expression);

    private sealed class DateTimeVisitor(string ruleId, DatabaseProvider provider) : DiagnosticRuleVisitor(ruleId)
    {
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

        private void AddDiagnostic(string typeName, string memberName) =>
            Info(
                $"Current timestamp member '{typeName}.{memberName}' is evaluated by the database, not C#.",
                ProviderFixHints.DateTimeSemantics(provider, memberName));

        private static bool IsCurrentTimestampMember(Type? declaringType, string name) =>
            declaringType == typeof(DateTime)
                && name is nameof(DateTime.Now) or nameof(DateTime.UtcNow) or nameof(DateTime.Today)
            || declaringType == typeof(DateTimeOffset)
                && name is nameof(DateTimeOffset.Now) or nameof(DateTimeOffset.UtcNow);
    }
}
