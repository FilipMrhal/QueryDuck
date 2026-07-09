using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;
using QueryDuck.Core.Diagnostics;
using QueryDuck.Core.Providers;
using QueryDuck.Core.Rules;

namespace QueryDuck.Core.Diagnostics;

public sealed class QueryRuleRunner
{
    private static readonly IQueryRule[] DefaultRules =
    [
        new EmptyStringComparisonRule(),
        new InlinedConstantRule(),
        new NonNullableAggregateRule(),
        new NullableComparisonRule(),
        new CaseSensitivityComparisonRule(),
        new LargeContainsRule(),
        new DateTimeSemanticsRule(),
        new BooleanComparisonRule(),
        new UnorderedFirstRule(),
        new IncludeWithoutSplitQueryRule(),
        new TakeSkipWithoutOrderByRule(),
        new NonTranslatablePatternRule(),
        new StringContainsPatternRule(),
        new MissingAsNoTrackingRule(),
        new RawSqlLiteralRule(),
        new ExecuteUpdateDeleteRule(),
        new DynamicOrderByRule(),
        new UnfilteredCountRule(),
        new MultipleOrderByRule(),
        new StringNormalizationRule(),
        new DistinctWithoutProjectionRule(),
        new GroupByWithoutAggregateRule(),
        new IgnoreQueryFiltersRule(),
    ];

    private readonly IReadOnlyList<IQueryRule> _rules;

    public QueryRuleRunner(IEnumerable<IQueryRule>? rules = null) =>
        _rules = (rules ?? DefaultRules).ToList();

    public IReadOnlyList<QueryDiagnostic> Analyze(Expression expression, DatabaseProvider provider, IModel? model = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var context = new QueryRuleContext(expression, provider, model);
        var diagnostics = new List<QueryDiagnostic>();

        foreach (var rule in _rules)
        {
            if (rule.ApplicableProviders.Count > 0 && !rule.ApplicableProviders.Contains(provider))
            {
                continue;
            }

            diagnostics.AddRange(rule.Analyze(context));
        }

        return diagnostics;
    }
}
