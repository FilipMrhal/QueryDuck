using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using QueryDuck.Core.Adapters;
using QueryDuck.Core.Providers;

namespace QueryDuck.Core.Adapters;

public static class SchemaAuditHelper
{
    public static SchemaAuditResult Compare(
        IModel model,
        IReadOnlyList<SchemaColumnInfo> databaseColumns,
        string defaultSchema)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(databaseColumns);

        var nullability = new List<NullabilityMismatch>();
        var types = new List<TypeMismatch>();
        var columnLookup = databaseColumns.ToDictionary(
            c => $"{c.TableName}.{c.ColumnName}",
            c => c,
            StringComparer.OrdinalIgnoreCase);

        foreach (var entityType in model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName() ?? entityType.ClrType.Name;
            _ = entityType.GetSchema() ?? defaultSchema;

            foreach (var property in entityType.GetProperties())
            {
                var columnName = property.GetColumnName() ?? property.Name;
                var key = $"{tableName}.{columnName}";
                if (!columnLookup.TryGetValue(key, out var column))
                {
                    continue;
                }

                if (property.IsNullable != column.IsNullable)
                {
                    nullability.Add(new NullabilityMismatch(
                        entityType.ClrType.Name,
                        property.Name,
                        columnName,
                        property.IsNullable,
                        column.IsNullable,
                        $"Property '{property.Name}' nullability ({property.IsNullable}) differs from column ({column.IsNullable})."));
                }

                var storeType = property.GetColumnType() ?? property.ClrType.Name;
                if (!TypesCompatible(storeType, column.DataType))
                {
                    types.Add(new TypeMismatch(
                        entityType.ClrType.Name,
                        property.Name,
                        columnName,
                        storeType,
                        column.DataType,
                        $"Property '{property.Name}' type '{storeType}' may not match database type '{column.DataType}'."));
                }
            }
        }

        return new SchemaAuditResult(nullability, types);
    }

    public static string ComputePlanHash(string planText)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(planText));
        return Convert.ToHexString(bytes)[..16];
    }

    private static bool TypesCompatible(string modelType, string databaseType)
    {
        var normalizedModel = NormalizeType(modelType);
        var normalizedDb = NormalizeType(databaseType);
        return normalizedModel.Contains(normalizedDb, StringComparison.OrdinalIgnoreCase)
            || normalizedDb.Contains(normalizedModel, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeType(string type) =>
        type.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("System.", string.Empty, StringComparison.OrdinalIgnoreCase);
}
