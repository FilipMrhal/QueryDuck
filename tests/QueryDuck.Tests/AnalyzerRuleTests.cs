using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using QueryDuck.Analyzers;

namespace QueryDuck.Tests;

public sealed class AnalyzerRuleTests
{
    [Fact]
    public void Analyzer_QD001_ReportsEmptyStringComparison()
    {
        const string source = """
            using System.Linq;
            class Program
            {
                void M()
                {
                    var q = new string[0].AsQueryable().Where(x => x == "");
                }
            }
            """;

        var diagnostics = CompileWithAnalyzer(source);
        Assert.Contains(diagnostics, d => d.Id == QueryDuckDiagnosticAnalyzer.EmptyStringRuleId);
    }

    [Fact]
    public void Analyzer_QD003_ReportsNonNullableAggregate()
    {
        const string source = """
            using System.Linq;
            class Program
            {
                void M()
                {
                    var q = new int[0].AsQueryable().Average(x => x);
                }
            }
            """;

        var diagnostics = CompileWithAnalyzer(source);
        Assert.Contains(diagnostics, d => d.Id == QueryDuckDiagnosticAnalyzer.NonNullableAggregateRuleId);
    }

    [Fact]
    public void Analyzer_QD005_ReportsStringComparison()
    {
        const string source = """
            using System.Linq;
            class Program
            {
                void M()
                {
                    var q = new string[0].AsQueryable().Where(x => x == "open");
                }
            }
            """;

        var diagnostics = CompileWithAnalyzer(source);
        Assert.Contains(diagnostics, d => d.Id == QueryDuckDiagnosticAnalyzer.CaseSensitivityRuleId);
    }

    private static ImmutableArray<Diagnostic> CompileWithAnalyzer(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
            .Cast<MetadataReference>()
            .ToList();

        var compilation = CSharpCompilation.Create(
            "AnalyzerTests",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzer = new QueryDuckDiagnosticAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        return compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
    }
}
