#!/usr/bin/env bash
# Sync Plugins-Orion/orion:* into a colon-free mirror so csc can compile ProjectReferences.
set -euo pipefail
SRC="${1:?plugins root}"
MIRROR="${2:?mirror dir}"
BE="${3:?OrionServerBE root}"
mkdir -p "$MIRROR"
for d in containers inventory; do
  mkdir -p "$MIRROR/orion-$d"
  rsync -a --delete \
    --exclude bin --exclude obj --exclude .msbuild --exclude .git \
    "$SRC/orion:$d/" "$MIRROR/orion-$d/"
  sed -i 's|orion:containers|orion-containers|g; s|orion:inventory|orion-inventory|g' \
    "$MIRROR/orion-$d"/*.csproj
  # Nearest Directory.Build.props wins; pin monorepo roots for the mirror tree.
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
    <OrionServerBERoot>$BE</OrionServerBERoot>
    <PluginsSiblingRoot>$MIRROR</PluginsSiblingRoot>
  </PropertyGroup>
</Project>
EOF
done
