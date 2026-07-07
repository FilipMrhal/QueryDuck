using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using QueryDuck.Core.Debugging;
using QueryDuck.Core.Diagnostics;
using QueryDuck.Core.ExpressionTrees;
using QueryDuck.Core.Providers;

namespace QueryDuck.Core.Capture;

public sealed record QueryDuckPendingCapture(
    Expression Expression,
    DbContext? Context,
    DatabaseProvider Provider,
    IModel? Model,
    ExpressionTreeNode ExpressionTree,
    string ExpressionTreeText,
    string ExpressionCSharp,
    IReadOnlyList<QueryDiagnostic> Diagnostics);

public static class QueryDuckCaptureScope
{
    private static readonly AsyncLocal<QueryDuckPendingCapture?> Pending = new();

    public static void SetPending(IQueryable query, DbContext? context)
    {
        ArgumentNullException.ThrowIfNull(query);
        SetPending(query.Expression, context);
    }

    public static void SetPending(Expression expression, DbContext? context)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var provider = QueryDebugExtensions.ResolveProvider(context);
        Pending.Value = new QueryDuckPendingCapture(
            expression,
            context,
            provider,
            context?.Model,
            ExpressionTreeBuilder.Build(expression),
            ExpressionTreeFormatter.Format(expression),
            ExpressionTreeCSharpRenderer.Render(expression),
            new QueryRuleRunner().Analyze(expression, provider, context?.Model).ToArray());
    }

    public static QueryDuckPendingCapture? TakePending()
    {
        var pending = Pending.Value;
        Pending.Value = null;
        return pending;
    }
}

public static class QueryDuckCaptureScopeExtensions
{
    /// <summary>
    /// Manually attach expression tree + diagnostics to the next SQL command on this async flow.
    /// Not required when <see cref="QueryCaptureOptions.AutoCaptureAllQueries"/> is enabled.
    /// </summary>
    public static IQueryable<T> WithQueryDuckScope<T>(this IQueryable<T> query, DbContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        QueryDuckCaptureScope.SetPending(query, context);
        return query;
    }

    /// <summary>
    /// Manually attach expression tree + diagnostics to the next SQL command on this async flow.
    /// Not required when <see cref="QueryCaptureOptions.AutoCaptureAllQueries"/> is enabled.
    /// </summary>
    public static IQueryable WithQueryDuckScope(this IQueryable query, DbContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        QueryDuckCaptureScope.SetPending(query, context);
        return query;
    }
}
