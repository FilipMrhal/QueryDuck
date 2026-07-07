using System.Linq.Expressions;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class NonTranslatablePatternRule : QueryRuleBase
{
    public override string Id => "QD012";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context) =>
        TranslatabilityVisitor.Analyze(context.Expression);

    private sealed class TranslatabilityVisitor : ExpressionVisitor
    {
        private readonly List<QueryDiagnostic> _diagnostics = [];
        private int _lambdaDepth;

        public static IEnumerable<QueryDiagnostic> Analyze(Expression expression)
        {
            var visitor = new TranslatabilityVisitor();
            visitor.Visit(expression);
            return visitor._diagnostics;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.Name == "AsEnumerable" &&
                node.Method.DeclaringType == typeof(Enumerable))
            {
                _diagnostics.Add(new QueryDiagnostic(
                    "QD012",
                    QueryDiagnosticSeverity.Error,
                    "AsEnumerable() forces client evaluation — remaining predicates may not translate to SQL.",
                    "Keep filtering in IQueryable until after ToListAsync(), or project in SQL first."));
            }

            if (node.Method.Name == "Compile" && node.Method.DeclaringType == typeof(Expression))
            {
                _diagnostics.Add(new QueryDiagnostic(
                    "QD012",
                    QueryDiagnosticSeverity.Error,
                    "Expression.Compile() inside a query cannot be translated to SQL.",
                    "Evaluate compiled delegates outside the LINQ expression tree."));
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
                _diagnostics.Add(new QueryDiagnostic(
                    "QD012",
                    QueryDiagnosticSeverity.Warning,
                    "Invoking a local function/delegate inside a LINQ predicate may not translate to SQL.",
                    "Inline the logic or filter in memory after materializing the query."));
            }

            return base.VisitInvocation(node);
        }
    }
}
