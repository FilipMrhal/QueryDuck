using System.Linq.Expressions;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Core.Rules;

internal sealed class GroupByWithoutAggregateRule : QueryRuleBase
{
    public override string Id => "QD023";

    public override IEnumerable<QueryDiagnostic> Analyze(QueryRuleContext context)
    {
        var visitor = new GroupByVisitor(Id);
        visitor.Visit(context.Expression);
        visitor.Complete();
        return visitor.Results;
    }

    private sealed class GroupByVisitor(string ruleId) : DiagnosticRuleVisitor(ruleId)
    {
        private bool _hasGroupBy;
        private bool _hasAggregate;

        internal void Complete()
        {
            if (_hasGroupBy && !_hasAggregate)
            {
                Warn(
                    "GroupBy without an aggregate or projection may return large grouped sequences.",
                    "Project grouped results with Select, or aggregate with Sum/Count/Max/Min.");
            }
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(Queryable))
            {
                if (node.Method.Name == "GroupBy")
                {
                    _hasGroupBy = true;
                }
                else if (QueryableExpressionHelpers.IsQueryableMethod(
                             node,
                             "Sum",
                             "Count",
                             "LongCount",
                             "Average",
                             "Min",
                             "Max"))
                {
                    _hasAggregate = true;
                }
            }

            return base.VisitMethodCall(node);
        }
    }
}
