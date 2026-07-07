using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace QueryDuck.Core.Capture;

public sealed record SourceLocation(
    string FilePath,
    int Line,
    string? MethodName = null);

public static class SourceLocationCapture
{
    private static readonly string[] IgnoredPrefixes =
    [
        "System.",
        "Microsoft.",
        "QueryDuck.",
        "Z.EntityFramework.",
    ];

    public static SourceLocation? Capture(int skipFrames = 2)
    {
        var stack = new StackTrace(skipFrames, fNeedFileInfo: true);
        for (var i = 0; i < stack.FrameCount; i++)
        {
            var frame = stack.GetFrame(i);
            var method = frame?.GetMethod();
            if (method is null)
            {
                continue;
            }

            var declaringType = method.DeclaringType;
            if (declaringType is null || ShouldIgnore(declaringType.FullName))
            {
                continue;
            }

            var file = frame!.GetFileName();
            var line = frame.GetFileLineNumber();
            if (string.IsNullOrWhiteSpace(file) || line <= 0)
            {
                continue;
            }

            return new SourceLocation(file, line, method.Name);
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static SourceLocation? CaptureFromCaller() => Capture(skipFrames: 3);

    private static bool ShouldIgnore(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return true;
        }

        foreach (var prefix in IgnoredPrefixes)
        {
            if (typeName.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return typeName.Contains("EntityFrameworkCore", StringComparison.Ordinal);
    }
}
