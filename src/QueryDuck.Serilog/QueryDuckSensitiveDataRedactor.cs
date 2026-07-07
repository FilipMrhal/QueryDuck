using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace QueryDuck.Serilog;

internal static class QueryDuckSensitiveDataRedactor
{
    internal const string RedactedToken = "[REDACTED]";

    public static bool IsPiiParameterName(string parameterName, QueryDuckSensitiveDataLoggingOptions options)
    {
        if (string.IsNullOrWhiteSpace(parameterName))
        {
            return false;
        }

        var normalized = parameterName.Trim().TrimStart('@', ':').ToUpperInvariant();
        return options.PiiParameterNamePatterns.Any(pattern =>
            normalized.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    public static object? ProtectValue(object? value, QueryDuckSensitiveDataMode mode, bool allowInclude)
    {
        if (value is null or DBNull)
        {
            return null;
        }

        if (allowInclude)
        {
            return mode switch
            {
                QueryDuckSensitiveDataMode.Omit => null,
                QueryDuckSensitiveDataMode.Hash => HashValue(value),
                _ => value,
            };
        }

        return mode switch
        {
            QueryDuckSensitiveDataMode.Omit => null,
            QueryDuckSensitiveDataMode.Hash => HashValue(value),
            QueryDuckSensitiveDataMode.Include => QueryDuckSensitiveDataRedactor.RedactedToken,
            _ => QueryDuckSensitiveDataRedactor.RedactedToken,
        };
    }

    public static string? ProtectText(string? text, QueryDuckSensitiveDataMode mode, bool allowInclude)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        if (allowInclude)
        {
            return mode switch
            {
                QueryDuckSensitiveDataMode.Omit => null,
                QueryDuckSensitiveDataMode.Hash => HashValue(text),
                _ => text,
            };
        }

        return mode switch
        {
            QueryDuckSensitiveDataMode.Omit => null,
            QueryDuckSensitiveDataMode.Hash => HashValue(text),
            QueryDuckSensitiveDataMode.Include => QueryDuckSensitiveDataRedactor.RedactedToken,
            _ => QueryDuckSensitiveDataRedactor.RedactedToken,
        };
    }

    public static string HashValue(object value)
    {
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return $"sha256:{Convert.ToHexString(hash.AsSpan(0, 8))}";
    }
}
