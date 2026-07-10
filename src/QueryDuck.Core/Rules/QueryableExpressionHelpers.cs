using System.Linq.Expressions;

namespace QueryDuck.Core.Rules;

internal static class QueryableExpressionHelpers
{
    public static bool IsQueryableMethod(MethodCallExpression node, params ReadOnlySpan<string> methodNames)
    {
        if (node.Method.DeclaringType != typeof(Queryable))
        {
            return false;
        }

        foreach (var methodName in methodNames)
        {
            if (node.Method.Name == methodName)
            {
                return true;
            }
        }

        return false;
    }

    public static bool HasQueryableAncestor(MethodCallExpression node, params ReadOnlySpan<string> methodNames) =>
        HasAncestorMethod(node, typeof(Queryable), methodNames);

    public static bool HasEnumerableAncestor(MethodCallExpression node, params ReadOnlySpan<string> methodNames) =>
        HasAncestorMethod(node, typeof(Enumerable), methodNames);

    private static bool HasAncestorMethod(
        MethodCallExpression node,
        Type declaringType,
        ReadOnlySpan<string> methodNames)
    {
        var current = node.Arguments.FirstOrDefault() as MethodCallExpression;
        while (current is not null)
        {
            if (current.Method.DeclaringType == declaringType && MatchesAny(current.Method.Name, methodNames))
            {
                return true;
            }

            current = current.Arguments.FirstOrDefault() as MethodCallExpression;
        }

        return false;
    }

    private static bool MatchesAny(string methodName, ReadOnlySpan<string> methodNames)
    {
        foreach (var candidate in methodNames)
        {
            if (methodName == candidate)
            {
                return true;
            }
        }

        return false;
    }
}
