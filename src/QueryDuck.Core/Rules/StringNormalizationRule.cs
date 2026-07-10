using System.Linq.Expressions;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class StringNormalizationRule : QueryRuleBase
{
    public override string Id => "QD021";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        NormalizationVisitor.Run(() => new NormalizationVisitor(Id), context.Expression);

    private sealed class NormalizationVisitor(string ruleId) : DiagnosticRuleVisitor(ruleId)
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(string) &&
                node.Method.Name is "ToLower" or "ToUpper" or "ToLowerInvariant" or "ToUpperInvariant")
            {
                Info(
                    $"string.{node.Method.Name}() in a predicate prevents index use and may change collation semantics.",
                    "Compare with case-insensitive collation, or normalize values at write time.");
            }

            return base.VisitMethodCall(node);
        }
    }
}
