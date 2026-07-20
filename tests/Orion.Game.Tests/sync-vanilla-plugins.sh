#!/usr/bin/env bash
# Sync Plugins-Orion/orion:* into a colon-free mirror so csc can compile sibling ProjectReferences.
set -euo pipefail
SRC="${1:?plugins root}"
MIRROR="${2:?mirror dir}"
BE="${3:?OrionServerBE root}"
mkdir -p "$MIRROR"
# NuGet feed for Orion.Api / Protocol / etc.
if [ -f "$SRC/nuget.config" ]; then
  cp "$SRC/nuget.config" "$MIRROR/nuget.config"
else
  cat > "$MIRROR/nuget.config" <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
EOF
fi
for d in containers inventory; do
  SRC_DIR=""
  if [ -d "$SRC/orion:$d" ]; then
    SRC_DIR="$SRC/orion:$d"
  elif [ -d "$SRC/orion-$d" ]; then
    SRC_DIR="$SRC/orion-$d"
  else
    echo "Plugin source not found for orion:$d under $SRC" >&2
    exit 1
  fi
  mkdir -p "$MIRROR/orion-$d"
  rsync -a --delete \
    --exclude bin --exclude obj --exclude .msbuild --exclude .git \
    "$SRC_DIR/" "$MIRROR/orion-$d/"
  sed -i 's|orion:containers|orion-containers|g; s|orion:inventory|orion-inventory|g' \
    "$MIRROR/orion-$d"/*.csproj
  cat > "$MIRROR/orion-$d/Directory.Build.props" <<EOF
<Project>
  <PropertyGroup>
    <_PluginBuildRoot>\$(HOME)/.cache/orion-plugin-build/\$(MSBuildProjectName)/</_PluginBuildRoot>
    <BaseIntermediateOutputPath>\$(_PluginBuildRoot)obj/</BaseIntermediateOutputPath>
    <IntermediateOutputPath>\$(BaseIntermediateOutputPath)\$(Configuration)/</IntermediateOutputPath>
    <MSBuildProjectExtensionsPath>\$(BaseIntermediateOutputPath)</MSBuildProjectExtensionsPath>
    <BaseOutputPath>\$(_PluginBuildRoot)bin/</BaseOutputPath>
    <OutputPath>\$(BaseOutputPath)</OutputPath>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <UseSharedCompilation>false</UseSharedCompilation>
    <PluginsSiblingRoot>$MIRROR</PluginsSiblingRoot>
  </PropertyGroup>
</Project>
EOF
done
