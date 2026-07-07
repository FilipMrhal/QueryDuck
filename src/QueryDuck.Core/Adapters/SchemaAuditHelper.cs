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
        var missingColumns = new List<MissingColumnFinding>();
        var missingIndexes = new List<MissingIndexFinding>();
        var foreignKeys = new List<ForeignKeyFinding>();
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
                    missingColumns.Add(new MissingColumnFinding(
                        entityType.ClrType.Name,
                        tableName,
                        property.Name,
                        columnName,
                        $"Model maps '{property.Name}' to column '{columnName}' on '{tableName}', but the column was not found in the database."));
                    continue;
                }

                if (property.IsNullable != column.IsNullable)
                {
                    nullability.Add(new NullabilityMismatch(
                        entityType.ClrType.Name,
                        tableName,
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
                        tableName,
                        property.Name,
                        columnName,
                        storeType,
                        column.DataType,
                        $"Property '{property.Name}' type '{storeType}' may not match database type '{column.DataType}'."));
                }
            }

            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                var principalTable = foreignKey.PrincipalEntityType.GetTableName() ?? foreignKey.PrincipalEntityType.ClrType.Name;
                foreach (var property in foreignKey.Properties)
                {
                    var columnName = property.GetColumnName() ?? property.Name;
                    foreignKeys.Add(new ForeignKeyFinding(
                        tableName,
                        columnName,
                        principalTable,
                        $"Foreign key '{tableName}.{columnName}' references '{principalTable}' — verify an index exists on the FK column for join performance."));

                    if (!property.IsPrimaryKey())
                    {
                        missingIndexes.Add(new MissingIndexFinding(
                            tableName,
                            columnName,
                            $"Consider an index on foreign key column '{tableName}.{columnName}'."));
                    }
                }
            }
        }

        return new SchemaAuditResult(nullability, types, missingColumns, missingIndexes, foreignKeys);
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
