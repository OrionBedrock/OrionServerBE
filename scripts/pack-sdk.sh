#!/usr/bin/env bash
# Pack Orion SDK NuGet packages into artifacts/nuget (local feed for plugin builds).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="${ROOT}/artifacts/nuget"
VER="${1:-0.1.0}"
mkdir -p "$OUT"
for proj in \
  src/PluginContracts/PluginContracts.csproj \
  src/Orion.Api/Orion.Api.csproj \
  src/Orion.Gameplay.Api/Orion.Gameplay.Api.csproj \
  src/Protocol/Protocol.csproj
do
  dotnet pack "$ROOT/$proj" -c Release -o "$OUT" -p:Version="$VER" -p:PackageVersion="$VER" --nologo
done
echo "Packed SDK $VER -> $OUT"
ls -la "$OUT"/*.nupkg
