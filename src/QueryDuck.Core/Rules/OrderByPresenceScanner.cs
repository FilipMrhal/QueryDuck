using System.Linq.Expressions;

namespace QueryDuck.Core.Rules;

internal sealed class OrderByPresenceScanner : ExpressionVisitor
{
    public bool HasOrderBy { get; private set; }

    public static bool ContainsOrderBy(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var scanner = new OrderByPresenceScanner();
        scanner.Visit(expression);
        return scanner.HasOrderBy;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (QueryableExpressionHelpers.IsQueryableMethod(
                node,
                "OrderBy",
                "OrderByDescending",
                "ThenBy",
                "ThenByDescending"))
        {
            HasOrderBy = true;
        }

        return base.VisitMethodCall(node);
    }
}
