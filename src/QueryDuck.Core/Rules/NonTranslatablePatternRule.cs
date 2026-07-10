using System.Linq.Expressions;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class NonTranslatablePatternRule : QueryRuleBase
{
    public override string Id => "QD012";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        TranslatabilityVisitor.Run(() => new TranslatabilityVisitor(Id), context.Expression);

    private sealed class TranslatabilityVisitor(string ruleId) : DiagnosticRuleVisitor(ruleId)
    {
        private int _lambdaDepth;

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.Name == "AsEnumerable" &&
                node.Method.DeclaringType == typeof(Enumerable))
            {
                Error(
                    "AsEnumerable() forces client evaluation — remaining predicates may not translate to SQL.",
                    "Keep filtering in IQueryable until after ToListAsync(), or project in SQL first.");
            }

            if (node.Method.Name == "Compile" && node.Method.DeclaringType == typeof(Expression))
            {
                Error(
                    "Expression.Compile() inside a query cannot be translated to SQL.",
                    "Evaluate compiled delegates outside the LINQ expression tree.");
            }

            return base.VisitMethodCall(node);
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            _lambdaDepth++;
            var result = base.VisitLambda(node);
            _lambdaDepth--;
            return result;
        }

        protected override Expression VisitInvocation(InvocationExpression node)
        {
            if (_lambdaDepth > 0)
            {
                Warn(
                    "Invoking a local function/delegate inside a LINQ predicate may not translate to SQL.",
                    "Inline the logic or filter in memory after materializing the query.");
            }

            return base.VisitInvocation(node);
        }
    }
}
