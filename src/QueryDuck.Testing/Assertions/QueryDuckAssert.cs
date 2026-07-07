using System.Linq;
using QueryDuck.Core.Debugging;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Testing.Assertions;

public static class QueryDuckAssert
{
    public static void ShouldHaveNoWarnings(this QueryDebugView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var warnings = view.Warnings.Where(w => w.Severity >= QueryDiagnosticSeverity.Warning).ToArray();
        if (warnings.Length > 0)
        {
            var messages = string.Join(Environment.NewLine, warnings.Select(w => $"{w.RuleId}: {w.Message}"));
            throw new InvalidOperationException($"Expected no warnings, but found:{Environment.NewLine}{messages}");
        }
    }

    public static void ShouldContainRule(this QueryDebugView view, string ruleId)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (!view.Warnings.Any(w => w.RuleId == ruleId))
        {
            throw new InvalidOperationException($"Expected diagnostic rule '{ruleId}' was not found.");
        }
    }
}

public static class QueryDuckQueryableAssert
{
    public static QueryDebugView Should(this IQueryable query) => query.Debug();
}
