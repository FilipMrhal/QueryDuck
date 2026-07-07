# QueryDuck

EF Core 10 debugging toolkit for Oracle, PostgreSQL, SQL Server, and MySQL/MariaDB.

## Features (v1.3.0)

- `query.Debug(context)` — debugger watch window support
- `UseQueryDuckDebugging()` — one-line setup: auto-captures expression trees on every query + local HTTP server for Rider
- **Diagnostic rules QD001–QD009** — provider-aware expression analysis
- **Session insights** — live N+1 detection and slow-query warnings
- **Query duration capture** — per-command timing in events and IDE tool windows
- **Rider plugin** — query table, syntax-colored SQL, expression tree, diagnostics, parameters, plans
- **Visual Studio extension** — same workflow in **View → Other Windows → QueryDuck** (VS 2022)

## Diagnostic rules

| Rule | Scope | Detects |
|------|-------|---------|
| QD001 | Oracle | Empty string comparisons (`''` treated as NULL) |
| QD002 | All | Inlined constants in predicates |
| QD003 | All | Non-nullable aggregate selectors |
| QD004 | All | Nullable captured variable comparisons |
| QD005 | SQL Server, MySQL | Case-insensitive string comparisons |
| QD006 | All | Large captured `Contains` / IN-list filters |
| QD007 | All | `DateTime.Now` / `UtcNow` evaluated by the database |
| QD008 | All | Boolean literal comparisons (`== true/false`) |
| QD009 | All | `First`/`Single`/`Last` without `OrderBy` |

## Session insights

When capture is enabled, QueryDuck aggregates session-level warnings:

- **N+1 detection** — repeated SQL shapes (`DetectNPlusOne`, default threshold 5)
- **Slow queries** — commands slower than `SlowQueryThresholdMs` (default 500 ms)

Warnings appear on `GET /queryduck/health` and in the Rider tool window banner.

## Rider plugin workflow

The plugin reads from the **local event server** started by your app.

**1. Enable debugging in your app:**

```csharp
options.UseQueryDuckDebugging();

await context.Customers.Where(c => c.Code == "").ToListAsync();
```

**2. Run the sample:**

```bash
dotnet run --project samples/QueryDuck.Sample
```

**3. Open View → Tool Windows → QueryDuck** in Rider (plugin v1.3.0+)

## Visual Studio extension workflow

Same local event server, same JSON API — no app changes required beyond `UseQueryDuckDebugging()`.

**1. Build and install the VSIX (Windows + VS 2022):**

```powershell
dotnet build vs-extension/QueryDuck.VisualStudio/QueryDuck.VisualStudio.csproj -c Release
# Install bin/Release/QueryDuck.VisualStudio.vsix
```

**2. Run your app** (sample or your project with QueryDuck enabled).

**3. Open View → Other Windows → QueryDuck** in Visual Studio.

See [vs-extension/README.md](vs-extension/README.md) for F5 experimental-instance debugging.

## API reference
|----------|--------|---------|
| `/queryduck/events` | GET | JSON array for the plugin |
| `/queryduck/health` | GET | Connection status, count, session warnings |
| `/queryduck/session/warnings` | GET | Session-level N+1 / slow-query warnings |
| `/queryduck/events/clear` | POST | Clear captured events |

## Configuration

```csharp
options.UseQueryDuckDebugging(o =>
{
    o.DetectNPlusOne = true;
    o.NPlusOneThreshold = 5;
    o.SlowQueryThresholdMs = 500;
    o.CaptureExecutionPlans = true; // requires provider adapter registry
});
```

## Slow query improvement engine (v1.1.0)

When a query exceeds `SlowQueryThresholdMs`, QueryDuck automatically analyzes SQL and execution plans and attaches `improvementAnalysis` to the capture event.

| Category | Example recommendation |
|----------|------------------------|
| **IndexCreation** | Full table scan detected → `CREATE INDEX ...` DDL for your provider |
| **ManualRewrite** | `SELECT *`, leading `%LIKE`, OR predicates → rewritten SQL + plan diff |
| **UseCte** | Correlated / repeated subqueries → `WITH filtered_... AS (...)` template |
| **SchemaSeparation** | Wide joins / `SELECT *` → split hot vs cold columns |
| **ApplicationChange** | Unbounded result sets → add paging / `LIMIT` |

When a rewrite is safe to EXPLAIN, QueryDuck runs the improved SQL against your live connection (best-effort) and builds a **plan comparison** (`PrimaryPlanDiff`) showing original vs improved steps and estimated cost reduction.

```csharp
options.UseQueryDuckDebugging(o =>
{
    o.AnalyzeSlowQueries = true;
    o.CapturePlansForSlowQueries = true; // EXPLAIN slow queries automatically
    o.SlowQueryThresholdMs = 500;
});
```

Rider plugin: open the **Improvements** tab on a slow query to see recommendations, suggested SQL/DDL, pg_stat_statements (when enabled), side-by-side plan graphs, and the text plan diff.

## Advanced slow-query insights (opt-in, v1.3.0)

These features are **off by default**. Enable only what you need:

```csharp
options.UseQueryDuckDebugging(o =>
{
    o.AnalyzeSlowQueries = true;
    o.CapturePlansForSlowQueries = true;

    // PostgreSQL: match captured SQL against pg_stat_statements history
    o.EnablePgStatStatementsInsights = true;

    // Read pg_stats column selectivity to refine index DDL
    o.EnableStatisticsBasedIndexRecommendations = true;

    // Attach Mermaid flowcharts to plan diffs for Rider side-by-side rendering
    o.EmitMermaidPlanGraphs = true;
}, DatabaseAdapterRegistry.CreateWithAllProviders()); // adapters required
```

| Option | Requires | What it adds |
|--------|----------|--------------|
| `EnablePgStatStatementsInsights` | PostgreSQL + `pg_stat_statements` extension | Historical calls, mean/total time, rows, cache hit ratio on slow queries |
| `EnableStatisticsBasedIndexRecommendations` | PostgreSQL `pg_stats` (or future provider stats) | Index column order + partial-index hints from real selectivity |
| `EmitMermaidPlanGraphs` | Plan diff / EXPLAIN | Mermaid flowcharts in event JSON; Rider renders side-by-side graphs |

**Prerequisites for PostgreSQL insights:**

```sql
CREATE EXTENSION IF NOT EXISTS pg_stat_statements;
```

## Entity Framework Extensions (v1.2.0)

QueryDuck captures standard EF Core LINQ queries via `DbCommandInterceptor`. **[Entity Framework Extensions](https://entityframework-extensions.net/)** (`Z.EntityFramework.Extensions.EFCore`) bulk/batch operations bypass that pipeline, so they are **not captured by default**.

Install the optional bridge package when your team uses licensed EF Extensions:

```bash
dotnet add package QueryDuck.EntityFrameworkExtensions
dotnet add package Z.EntityFramework.Extensions.EFCore   # licensed — required in your app
```

```csharp
using QueryDuck.EntityFrameworkExtensions;

options.UseQueryDuckDebugging(o => { ... }, adapters)
       .UseQueryDuckEntityFrameworkExtensions(adapters);

// Or call once at startup:
QueryDuckEntityFrameworkExtensionsIntegration.Enable(adapters);
```

Captured bulk events include:

| Field | Value |
|-------|-------|
| `source` | `EntityFrameworkExtensions` |
| `bulkOperation` | `BulkInsert`, `BulkUpdate`, `BulkMerge`, `DeleteFromQuery`, `UpdateFromQuery`, … |
| SQL | Parsed from EF Extensions operation logs |
| Duration | From EF Extensions stopwatch (when available) |

**Limitations:** no LINQ expression tree for bulk ops (there is no expression to capture). Slow-query improvement analysis still runs on captured bulk SQL. `UpdateFromQuery` / `DeleteFromQuery` batch commands may show zero duration because EF Extensions exposes them via pre-execute hooks only.

## Serilog exporter (v1.4.0)

Export SQL **failures** and **slow queries** to your existing Serilog pipeline with structured `QueryDuck` properties. Sensitive data and PII are **excluded by default**.

```bash
dotnet add package QueryDuck.Serilog
dotnet add package Serilog.AspNetCore   # or your preferred Serilog host package
```

```csharp
using QueryDuck.Serilog;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

options.UseQueryDuckCapture(o =>
{
    o.StartLocalEventServer = false; // optional in production
    o.SlowQueryThresholdMs = 500;
    o.AddSerilogExporter(Log.Logger, serilog =>
    {
        serilog.LogSlowQueries = true;
        serilog.LogSqlFailures = true;
        serilog.LogSuccessfulQueries = false;

        // Safe defaults — opt in explicitly when you need full payloads
        serilog.SensitiveData.IncludeSensitiveData = false;
        serilog.SensitiveData.IncludeParameterValues = false;
        serilog.SensitiveData.IncludePii = false;
        serilog.SensitiveData.IncludeSqlText = true;
        serilog.SensitiveData.IncludeRecommendationSummaries = true;
    });
}, adapters);
```

| Option | Default | Purpose |
|--------|---------|---------|
| `LogSqlFailures` | `true` | Emit `Error` logs when EF Core command execution fails |
| `LogSlowQueries` | `true` | Emit `Warning` logs when duration ≥ `SlowQueryThresholdMs` |
| `LogSuccessfulQueries` | `false` | Skip fast, successful queries |
| `SensitiveData.IncludeSensitiveData` | `false` | Master switch for parameter values, plans, rewrite SQL |
| `SensitiveData.IncludePii` | `false` | Allow PII-like parameter names (`email`, `password`, …) |
| `SensitiveData.IncludeParameterValues` | `false` | Export parameter values (respects PII rules) |
| `SensitiveData.DefaultMode` / `PiiMode` | `Redact` | `Omit`, `Redact`, `Hash`, or `Include` |

Each log event includes a destructured `QueryDuck` object with provider, duration, diagnostics, slow-query recommendation summaries, and (when opted in) redacted or hashed sensitive fields. SQL failures are captured via `CommandFailedAsync` and include `ErrorMessage` / `ExceptionType` (schema v6).

## Build

```bash
dotnet build QueryDuck.slnx --configuration Release
dotnet test QueryDuck.slnx --settings coverlet.runsettings
./build/pack.sh
```

### CI artifacts

GitHub Actions produces:

| Artifact | Contents |
|----------|----------|
| `nuget-packages` | 3 `.nupkg` + matching `.snupkg` symbol packages |
| `queryduck-rider-plugin` | JetBrains Rider plugin `.zip` |
| `queryduck-vsix` | Visual Studio 2022 extension |
| `coverage` | Cobertura coverage + test results |

Tag a release as `v1.4.0` to attach all artifacts to a GitHub Release. Configure the `NUGET_API_KEY` repository secret to publish packages to NuGet.org on tag push.

### NuGet packages

| Package | Contents |
|---------|----------|
| `QueryDuck.Core` | Capture pipeline, diagnostics, slow-query analysis, event server, **all four provider adapters** (Oracle, PostgreSQL, SQL Server, MySQL), and **bundled Roslyn analyzers** |
| `QueryDuck.Serilog` | Serilog exporter for SQL failures / slow queries (PII-safe defaults) |
| `QueryDuck.EntityFrameworkExtensions` | Bridge for licensed Z.EntityFramework.Extensions bulk/batch capture |

Most apps only need `QueryDuck.Core`. Provider adapters are pure ADO.NET (no provider-specific dependencies), so bundling them adds no dependency weight. Register them via `DatabaseAdapterRegistry.CreateWithAllProviders()` or individually with `.AddOracle()`, `.AddPostgreSql()`, `.AddSqlServer()`, `.AddMySql()`.

`QueryDuck.Client` (IDE HTTP client) and `QueryDuck.Testing` (Testcontainers fixtures) are internal projects and are not published.

Version is centralized in `Version.props` (currently **1.4.0**).

See [docs/CODE_QUALITY.md](docs/CODE_QUALITY.md) for quality gates.
