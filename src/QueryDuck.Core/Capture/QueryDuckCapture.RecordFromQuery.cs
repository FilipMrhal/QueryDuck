using Microsoft.EntityFrameworkCore;
using QueryDuck.Core.Debugging;
using QueryDuck.Core.Diagnostics;
using QueryDuck.Core.ExpressionTrees;

namespace QueryDuck.Core.Capture;

public static partial class QueryDuckCapture
{
    public static QueryCaptureEvent RecordFromQuery(IQueryable query, DbContext? context = null, string? sqlOverride = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        var provider = QueryDebugExtensions.ResolveProvider(context);
        var diagnostics = new QueryRuleRunner().Analyze(query.Expression, provider, context?.Model).ToArray();
        var dto = diagnostics
            .Select(d => new QueryDiagnosticDto(d.RuleId, d.Severity.ToString(), d.Message, d.FixHint))
            .ToArray();

        string sql;
        try
        {
            sql = sqlOverride ?? query.ToQueryString();
        }
        catch (Exception ex)
        {
            sql = $"[Unable to generate SQL: {ex.Message}]";
        }

        var captureEvent = new QueryCaptureEvent
        {
            EventId = Guid.NewGuid().ToString("N"),
            Timestamp = DateTimeOffset.UtcNow,
            Sql = sql,
            Provider = provider.ToString(),
            Diagnostics = dto,
            Warnings = dto.Select(d => $"[{d.RuleId}] {d.Message}").ToArray(),
            ExpressionTree = ExpressionTreeBuilder.Build(query.Expression),
            ExpressionTreeText = ExpressionTreeFormatter.Format(query.Expression),
            ExpressionCSharp = ExpressionTreeCSharpRenderer.Render(query.Expression),
        };

        Record(captureEvent);
        QueryDuckSession.Refresh(LastCommands, new QueryCaptureOptions());
        return captureEvent;
    }
}
