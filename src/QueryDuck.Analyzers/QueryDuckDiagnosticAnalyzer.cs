using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace QueryDuck.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class QueryDuckDiagnosticAnalyzer : DiagnosticAnalyzer
{
    public const string EmptyStringRuleId = "QD001";
    public const string NonNullableAggregateRuleId = "QD003";
    public const string CaseSensitivityRuleId = "QD005";

    private static readonly DiagnosticDescriptor EmptyStringRule = new(
        EmptyStringRuleId,
        "Empty string comparison on Oracle",
        "Comparing to empty string ('') is problematic on Oracle because '' is stored as NULL",
        "QueryDuck",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NonNullableAggregateRule = new(
        NonNullableAggregateRuleId,
        "Non-nullable aggregate selector",
        "Aggregate '{0}' uses a non-nullable selector; SQL returns NULL for empty sets",
        "QueryDuck",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor CaseSensitivityRule = new(
        CaseSensitivityRuleId,
        "Case-sensitive string comparison",
        "String comparison may behave differently across database collations",
        "QueryDuck",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [EmptyStringRule, NonNullableAggregateRule, CaseSensitivityRule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeBinary, SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeBinary(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not BinaryExpressionSyntax binary)
        {
            return;
        }

        if (IsEmptyStringLiteral(binary.Left) || IsEmptyStringLiteral(binary.Right))
        {
            context.ReportDiagnostic(Diagnostic.Create(EmptyStringRule, binary.GetLocation()));
        }

        if (IsStringTyped(binary.Left, context.SemanticModel) || IsStringTyped(binary.Right, context.SemanticModel))
        {
            context.ReportDiagnostic(Diagnostic.Create(CaseSensitivityRule, binary.GetLocation()));
        }
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        if (symbol is null)
        {
            return;
        }

        if (symbol.ContainingType?.Name == "Queryable" &&
            symbol.Name is "Sum" or "Max" or "Min" or "Average" &&
            symbol.ReturnType.IsValueType &&
            symbol.ReturnType.NullableAnnotation != NullableAnnotation.Annotated)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NonNullableAggregateRule,
                invocation.GetLocation(),
                symbol.Name));
            return;
        }

        var reduced = symbol.ReducedFrom ?? symbol;
        if (reduced.ContainingType?.Name == "Queryable" &&
            reduced.Name is "Sum" or "Max" or "Min" or "Average" &&
            reduced.ReturnType.IsValueType &&
            reduced.ReturnType.NullableAnnotation != NullableAnnotation.Annotated)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NonNullableAggregateRule,
                invocation.GetLocation(),
                reduced.Name));
        }
    }

    private static bool IsEmptyStringLiteral(ExpressionSyntax expression) =>
        expression is LiteralExpressionSyntax { Token.RawKind: (int)SyntaxKind.StringLiteralToken } literal
        && literal.Token.ValueText.Length == 0;

    private static bool IsStringTyped(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        var type = semanticModel.GetTypeInfo(expression).Type;
        return type?.SpecialType == SpecialType.System_String;
    }
}
