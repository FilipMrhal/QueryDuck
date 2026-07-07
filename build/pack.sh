#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIGURATION="${CONFIGURATION:-Release}"
OUTPUT="${OUTPUT:-$ROOT/artifacts/nuget}"
VERSION="${VERSION:-}"

mkdir -p "$OUTPUT"

PACK_ARGS=(
  "$ROOT/QueryDuck.slnx"
  --configuration "$CONFIGURATION"
  --output "$OUTPUT"
  /p:ContinuousIntegrationBuild=true
)

if [[ -n "$VERSION" ]]; then
  PACK_ARGS+=(/p:Version="$VERSION")
fi

dotnet pack "${PACK_ARGS[@]}"

"$ROOT/build/verify-packages.sh" "$OUTPUT"

echo "NuGet packages written to $OUTPUT"
