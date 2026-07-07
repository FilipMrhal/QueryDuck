using System.Diagnostics;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using QueryDuck.Core.Diagnostics;
using QueryDuck.Core.ExpressionTrees;
using QueryDuck.Core.Providers;

namespace QueryDuck.Core.Debugging;

[DebuggerDisplay("{Summary,nq}")]
public sealed class QueryDebugView
{
    private readonly IQueryable _query;
    private readonly DatabaseProvider _provider;
    private readonly IModel? _model;
    private readonly QueryRuleRunner _ruleRunner;

    internal QueryDebugView(
        IQueryable query,
        DatabaseProvider provider,
        IModel? model,
        QueryRuleRunner? ruleRunner = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        _query = query;
        _provider = provider;
        _model = model;
        _ruleRunner = ruleRunner ?? new QueryRuleRunner();
    }

    public string Sql
    {
        get
        {
            try
            {
                return _query.ToQueryString();
            }
            catch (Exception ex)
            {
                return $"[Unable to generate SQL: {ex.Message}]";
            }
        }
    }

    public string ExpressionTree => ExpressionTreeFormatter.Format(_query.Expression);

    public string ExpressionCSharp => ExpressionTreeCSharpRenderer.Render(_query.Expression);

    public string Provider => _provider.ToString();

    public IReadOnlyList<QueryDiagnostic> Warnings => _ruleRunner.Analyze(_query.Expression, _provider, _model);

    public string Summary => $"{Provider} query — {Warnings.Count} warning(s)";

    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public QueryDiagnostic[] WarningsExpanded => Warnings.ToArray();
}

[DebuggerTypeProxy(typeof(QueryDebugViewTypeProxy))]
public static class QueryDebugExtensions
{
    public static QueryDebugView Debug<T>(this IQueryable<T> query, DbContext? context = null) =>
        CreateView(query, context);

    public static QueryDebugView Debug(this IQueryable query, DbContext? context = null) =>
        CreateView(query, context);

    private static QueryDebugView CreateView(IQueryable query, DbContext? context)
    {
        ArgumentNullException.ThrowIfNull(query);
        var provider = ResolveProvider(context);
        return new QueryDebugView(query, provider, context?.Model);
    }

    internal static DatabaseProvider ResolveProvider(DbContext? context)
    {
        if (context is not null)
        {
            return DatabaseProviderNames.FromProviderName(context.Database.ProviderName);
        }

        return DatabaseProvider.Unknown;
    }
}

internal sealed class QueryDebugViewTypeProxy(QueryDebugView view)
{
    public string Sql => view.Sql;

    public string ExpressionTree => view.ExpressionTree;

    public string ExpressionCSharp => view.ExpressionCSharp;

    public string Provider => view.Provider;

    public QueryDiagnostic[] Warnings => view.WarningsExpanded;
}
