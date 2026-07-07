using System.Text.Json;
using QueryDuck.Client;

namespace QueryDuck.Tests;

public sealed class QueryDuckClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void DeserializeEventDto_roundTripsSchemaV5Fields()
    {
        const string json = """
            {
              "eventId": "evt-1",
              "timestamp": "2026-07-07T20:00:00Z",
              "sql": "SELECT * FROM orders",
              "provider": "PostgreSql",
              "source": "EntityFrameworkExtensions",
              "bulkOperation": "BulkInsert",
              "duration": "00:00:00.9200000",
              "schemaVersion": 6,
              "improvementAnalysis": {
                "eventId": "evt-1",
                "durationMs": 920,
                "originalSql": "SELECT * FROM orders",
                "pgStatStatements": {
                  "calls": 10,
                  "meanExecTimeMs": 850.5,
                  "totalExecTimeMs": 8505,
                  "rows": 1000,
                  "sharedBlocksHitRatio": 0.93
                },
                "primaryPlanDiff": {
                  "originalSteps": [{ "operation": "SEQ SCAN", "objectName": "orders", "cost": 1500 }],
                  "improvedSteps": [{ "operation": "INDEX SCAN", "objectName": "ix_orders", "cost": 120 }],
                  "summaryLines": ["Estimated cost reduction: 92%"],
                  "textDiff": "diff",
                  "originalMermaid": "flowchart TD",
                  "improvedMermaid": "flowchart TD",
                  "sideBySideMermaid": "flowchart LR"
                },
                "recommendations": []
              }
            }
            """;

        var dto = JsonSerializer.Deserialize<QueryCaptureEventDto>(json, JsonOptions);

        Assert.NotNull(dto);
        Assert.Equal(6, dto!.SchemaVersion);
        Assert.Equal("BulkInsert", dto.BulkOperation);
        Assert.NotNull(dto.ImprovementAnalysis?.PgStatStatements);
        Assert.Equal(10, dto.ImprovementAnalysis!.PgStatStatements!.Calls);
        Assert.NotNull(dto.ImprovementAnalysis.PrimaryPlanDiff?.SideBySideMermaid);
    }

    [Fact]
    public void MetaSourceLabel_formats_ef_extensions()
    {
        var dto = new QueryCaptureEventDto
        {
            Source = "EntityFrameworkExtensions",
            BulkOperation = "BulkMerge",
        };

        Assert.Equal("EF Extensions · BulkMerge", dto.MetaSourceLabel());
    }
}
