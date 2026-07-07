using System.Linq.Expressions;

namespace QueryDuck.Core.ExpressionTrees;

public sealed record ExpressionTreeNode(
    string Kind,
    string Type,
    string? Name = null,
    string? Value = null,
    IReadOnlyList<ExpressionTreeNode>? Children = null);

public static class ExpressionTreeBuilder
{
    public static ExpressionTreeNode Build(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return Visit(UnwrapQueryExpression(expression));
    }

    private static Expression UnwrapQueryExpression(Expression expression)
    {
        if (expression is MethodCallExpression call)
        {
            foreach (var argument in call.Arguments)
            {
                var unwrapped = UnwrapArgument(argument);
                if (unwrapped is not null)
                {
                    return unwrapped;
                }
            }
        }

        return expression;
    }

    private static Expression? UnwrapArgument(Expression argument) => argument switch
    {
        LambdaExpression => argument,
        UnaryExpression { Operand: LambdaExpression } unary => unary.Operand,
        _ => null,
    };

    private static ExpressionTreeNode Visit(Expression expression) => expression switch
    {
        BinaryExpression binary => new ExpressionTreeNode(
            binary.NodeType.ToString(),
            binary.Type.Name,
            Children:
            [
                Visit(binary.Left),
                Visit(binary.Right),
            ]),
        UnaryExpression unary => new ExpressionTreeNode(
            unary.NodeType.ToString(),
            unary.Type.Name,
            Children: [Visit(unary.Operand)]),
        MethodCallExpression call => new ExpressionTreeNode(
            "Call",
            call.Type.Name,
            Name: $"{call.Method.DeclaringType?.Name}.{call.Method.Name}",
            Children: call.Arguments.Select(Visit).ToArray()),
        MemberExpression member => new ExpressionTreeNode(
            "MemberAccess",
            member.Type.Name,
            Name: member.Member.Name,
            Children: member.Expression is null ? null : [Visit(member.Expression)]),
        ParameterExpression parameter => new ExpressionTreeNode(
            "Parameter",
            parameter.Type.Name,
            Name: parameter.Name ?? "<anonymous>"),
        ConstantExpression constant => new ExpressionTreeNode(
            "Constant",
            constant.Type.Name,
            Value: FormatConstant(constant.Value)),
        LambdaExpression lambda => new ExpressionTreeNode(
            "Lambda",
            lambda.Type.Name,
            Children:
            [
                ..lambda.Parameters.Select(Visit),
                Visit(lambda.Body),
            ]),
        _ => new ExpressionTreeNode(
            expression.NodeType.ToString(),
            expression.Type.Name),
    };

    private static string? FormatConstant(object? value) => value switch
    {
        null => "null",
        string s => $"\"{s}\"",
        _ => value.ToString(),
    };
}
