# Code Quality Metrics

QueryDuck enforces quality at build time, in CI, and through documented thresholds. All metrics below are active from Phase 0.

## Build-time gates

| Metric | Target | Enforcement |
|--------|--------|-------------|
| Compiler warnings | 0 | `TreatWarningsAsErrors=true` in `Directory.Build.props` |
| Nullable reference types | 100% enabled | `<Nullable>enable</Nullable>` on all projects |
| Static analysis | Latest rules | `AnalysisLevel=latest-all`, `Microsoft.CodeAnalysis.NetAnalyzers` |
| Code style | Enforced in build | `EnforceCodeStyleInBuild=true`, `.editorconfig` |
| NuGet audit | Enabled (low+) | `NuGetAudit=true`; transitive advisories reported as warnings (not build-breaking until direct deps are affected) |

## Test coverage

| Metric | Target | Enforcement |
|--------|--------|-------------|
| Line coverage (solution total) | ≥ 85% | Coverlet `Threshold=85` in `Directory.Build.props` |
| Coverage format | Cobertura | `CoverletOutputFormat=cobertura` → `artifacts/coverage/` |
| Exclusions | Generated/obsolete code | `ExcludeByAttribute` on analyzer-generated and excluded members |

Coverage thresholds apply once meaningful tests exist (Phase 1+). Phase 0 tests are structural stubs and may report low coverage until Phase 1 ships.

## CI pipeline (`.github/workflows/ci.yml`)

Each push and pull request must pass:

1. **Restore & build** — `dotnet build --configuration Release`
2. **Format check** — `dotnet format --verify-no-changes`
3. **Tests with coverage** — `dotnet test --configuration Release --collect:"XPlat Code Coverage"`
4. **Coverage gate** — Coverlet threshold enforced via MSBuild properties

## Analyzer rule policy

Rules are configured in `.editorconfig`:

- **Errors** — nullability (`CS86xx`), argument validation (`CA1062`), culture-sensitive operations (`CA1305`–`CA1310`), simplified null checks (`CA1510`)
- **Warnings** — unsealed internal types (`CA1515`), missing braces (`csharp_prefer_braces`)
- **Disabled** — `ConfigureAwait` in library code (`CA2007`), uninstantiated internal classes in DI-heavy code (`CA1812`)

Provider packages and the Roslyn analyzer project may add package-specific rules in later phases; new rules must be documented here.

## Incremental quality expectations by phase

| Phase | Minimum expectation |
|-------|---------------------|
| 0 | Solution builds; CI green; metrics documented |
| 1+ | Core library ≥ 85% line coverage on new code |
| 4+ | Each provider adapter has integration tests (Testcontainers) |
| 7 | Analyzer package has dedicated rule tests (`Microsoft.CodeAnalysis.CSharp.Analyzer.Testing`) |

## Local commands

```bash
# Build with all analyzers
dotnet build

# Verify formatting
dotnet format --verify-no-changes

# Run tests with coverage report
dotnet test --configuration Release --collect:"XPlat Code Coverage"
```

Coverage reports are written to `artifacts/coverage/`.
