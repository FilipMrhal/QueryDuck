using System.Linq.Expressions;
using System.Text;

namespace QueryDuck.Core.ExpressionTrees;

public static class ExpressionTreeFormatter
{
    public static string Format(Expression expression) =>
        new FormattingVisitor().Format(expression);

    private sealed class FormattingVisitor : ExpressionVisitor
    {
        private readonly StringBuilder _builder = new();
        private int _depth;

        public string Format(Expression expression)
        {
            Visit(expression);
            return _builder.ToString().TrimEnd();
        }

        public override Expression? Visit(Expression? node)
        {
            if (node is null)
            {
                return null;
            }

            WriteLine($"{node.NodeType}: {node.Type.Name}");
            _depth++;
            var result = base.Visit(node);
            _depth--;
            return result;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            WriteLine($"  Value = {FormatConstant(node.Value)}");
            return node;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            WriteLine($"  Name = {node.Name ?? "<anonymous>"}");
            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            WriteLine($"  Member = {node.Member.DeclaringType?.Name}.{node.Member.Name}");
            return base.VisitMember(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            WriteLine($"  Method = {node.Method.DeclaringType?.Name}.{node.Method.Name}()");
            return base.VisitMethodCall(node);
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            WriteLine($"  Operator = {node.NodeType}");
            return base.VisitBinary(node);
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            WriteLine($"  Unary = {node.NodeType}");
            return base.VisitUnary(node);
        }

        private void WriteLine(string text)
        {
            _builder.Append(new string(' ', _depth * 2));
            _builder.AppendLine(text);
        }

        private static string FormatConstant(object? value) => value switch
        {
            null => "null",
            string s => $"\"{s}\"",
            _ => value.ToString() ?? "<null>",
        };
    }
}

public static class ExpressionTreeCSharpRenderer
{
    public static string Render(Expression expression) =>
        new CSharpVisitor().Render(expression);

    private sealed class CSharpVisitor : ExpressionVisitor
    {
        private readonly StringBuilder _builder = new();

        public string Render(Expression expression)
        {
            Visit(expression);
            return _builder.ToString();
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            _builder.Append('(');
            if (node.Parameters.Count == 1)
            {
                _builder.Append(node.Parameters[0].Name);
            }

            _builder.Append(" => ");
            Visit(node.Body);
            _builder.Append(')');
            return node;
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            _builder.Append('(');
            Visit(node.Left);
            _builder.Append(' ');
            _builder.Append(node.NodeType switch
            {
                ExpressionType.Equal => "==",
                ExpressionType.NotEqual => "!=",
                ExpressionType.AndAlso => "&&",
                ExpressionType.OrElse => "||",
                ExpressionType.GreaterThan => ">",
                ExpressionType.GreaterThanOrEqual => ">=",
                ExpressionType.LessThan => "<",
                ExpressionType.LessThanOrEqual => "<=",
                _ => node.NodeType.ToString(),
            });
            _builder.Append(' ');
            Visit(node.Right);
            _builder.Append(')');
            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is not null)
            {
                Visit(node.Expression);
                _builder.Append('.');
            }

            _builder.Append(node.Member.Name);
            return node;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            _builder.Append(node.Name);
            return node;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            _builder.Append(node.Value switch
            {
                null => "null",
                string s => $"\"{s}\"",
                bool b => b ? "true" : "false",
                _ => node.Value?.ToString() ?? "null",
            });
            return node;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Object is not null)
            {
                Visit(node.Object);
                _builder.Append('.');
            }
            else if (node.Method.DeclaringType is not null)
            {
                _builder.Append(node.Method.DeclaringType.Name);
                _builder.Append('.');
            }

            _builder.Append(node.Method.Name);
            _builder.Append('(');
            for (var i = 0; i < node.Arguments.Count; i++)
            {
                if (i > 0)
                {
                    _builder.Append(", ");
                }

                Visit(node.Arguments[i]);
            }

            _builder.Append(')');
            return node;
        }
    }
}
