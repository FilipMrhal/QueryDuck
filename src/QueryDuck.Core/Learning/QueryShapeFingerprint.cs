using System.Security.Cryptography;
using System.Text;
using QueryDuck.Core.Adapters;

namespace QueryDuck.Core.Learning;

internal static class QueryShapeFingerprint
{
    public static string Compute(string sql, string provider)
    {
        var normalized = QueryHistoricalStatsSqlMatcher.NormalizeForMatch(sql);
        var payload = $"{provider}|{normalized}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash, 0, 8);
    }
}
