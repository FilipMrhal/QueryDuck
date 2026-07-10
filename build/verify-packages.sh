#!/usr/bin/env bash
set -euo pipefail

OUTPUT="${1:-artifacts/nuget}"

EXPECTED=(
  QueryDuck.Core
  QueryDuck.EntityFrameworkExtensions
  QueryDuck.Serilog
  QueryDuck.OpenTelemetry
)

shopt -s nullglob
packages=("$OUTPUT"/*.nupkg)
symbols=("$OUTPUT"/*.snupkg)

if (( ${#packages[@]} == 0 )); then
  echo "No .nupkg files found in $OUTPUT" >&2
  exit 1
fi

missing=()
for id in "${EXPECTED[@]}"; do
  if ! compgen -G "$OUTPUT/$id.*.nupkg" > /dev/null; then
    missing+=("$id")
  fi
done

if (( ${#missing[@]} > 0 )); then
  echo "Missing expected NuGet packages:" >&2
  printf '  - %s\n' "${missing[@]}" >&2
  echo "Found packages:" >&2
  ls -1 "$OUTPUT"/*.nupkg >&2 || true
  exit 1
fi

if (( ${#symbols[@]} != ${#packages[@]} )); then
  echo "Expected one .snupkg per .nupkg, found ${#packages[@]} nupkg and ${#symbols[@]} snupkg" >&2
  exit 1
fi

for package in "${packages[@]}"; do
  if ! unzip -t "$package" >/dev/null; then
    echo "Invalid nupkg archive: $package" >&2
    exit 1
  fi
done

# The Core package must bundle the Roslyn analyzers.
core_package=$(compgen -G "$OUTPUT/QueryDuck.Core.*.nupkg" | head -1)
if ! unzip -l "$core_package" | grep -q "analyzers/dotnet/cs/QueryDuck.Analyzers.dll"; then
  echo "QueryDuck.Core package is missing bundled analyzers (analyzers/dotnet/cs/QueryDuck.Analyzers.dll)" >&2
  exit 1
fi

echo "Verified ${#packages[@]} NuGet packages and ${#symbols[@]} symbol packages in $OUTPUT"
