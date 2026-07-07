# QueryDuck Visual Studio Extension

Visual Studio **2022** (17.x) extension with feature parity to the Rider plugin.

## Open the tool window

**View → Other Windows → QueryDuck**

## Prerequisites

- Visual Studio 2022 with **.NET desktop development** workload
- A running app with `UseQueryDuckDebugging()` (starts the event server on `http://127.0.0.1:17654`)

## Build (Windows)

The VSIX project targets **.NET Framework 4.7.2** and requires the **Visual Studio SDK**. Build on Windows:

```powershell
dotnet build vs-extension/QueryDuck.VisualStudio/QueryDuck.VisualStudio.csproj -c Release
```

Output: `vs-extension/QueryDuck.VisualStudio/bin/Release/QueryDuck.VisualStudio.vsix`

Install by double-clicking the `.vsix` or via **Extensions → Manage Extensions → Install**.

## Debug in Experimental Instance

Open `vs-extension/QueryDuck.VisualStudio/QueryDuck.VisualStudio.csproj` in Visual Studio and press **F5**. This launches a VS instance with the `[QueryDuck]` suffix.

## Architecture

| Component | Role |
|-----------|------|
| `QueryDuck.Client` | Shared HTTP client + JSON DTOs (also used by tests) |
| `QueryDuck.VisualStudio` | VSIX package, WPF tool window, plan graph rendering |

The extension polls the same local HTTP API as Rider:

- `GET /queryduck/events`
- `GET /queryduck/health`
- `POST /queryduck/events/clear`

## Feature parity with Rider

- Captured query table (time, provider, tag, warnings, duration, SQL preview)
- Session warnings banner (N+1, slow queries)
- Detail tabs: SQL, Expression Tree, C# Expression, Diagnostics, Parameters, Plan, Improvements
- Improvements: recommendations, suggested SQL/DDL, side-by-side plan graphs, text diff, pg_stat_statements panel
- Auto-refresh, follow latest, provider/tag filters

## CI note

The VSIX is **not built on Linux CI** (requires Windows + VS SDK). `QueryDuck.Client` is built and tested in the main solution.
