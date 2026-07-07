using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace QueryDuck.Core.Capture;

/// <summary>
/// Automatically attaches expression tree + diagnostics before every query compiles and executes.
/// Registered when <see cref="QueryCaptureOptions.AutoCaptureAllQueries"/> is enabled.
/// </summary>
public sealed class QueryDuckAutoCaptureInterceptor : IQueryExpressionInterceptor
{
    public Expression QueryCompilationStarting(
        Expression queryExpression,
        QueryExpressionEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(queryExpression);
        ArgumentNullException.ThrowIfNull(eventData);
        QueryDuckCaptureScope.SetPending(queryExpression, eventData.Context);
        return queryExpression;
    }
}
