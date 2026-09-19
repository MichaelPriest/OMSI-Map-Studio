#!/usr/bin/env bash
set -euo pipefail

BUILD_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORKTREE="$BUILD_DIR/worktree"
DOTNET_DIR="$BUILD_DIR/dotnet"
NUGET_DIR="$BUILD_DIR/nuget"
DOWNLOAD_DIR="$BUILD_DIR/downloads"
PUBLISH_DIR="$BUILD_DIR/publish/win-x64"
PUBLIC_DIR="$BUILD_DIR/public"

SOURCE_HEAD="b4f18fcf3e132a5780e5be6257539a3bcdc784e1"
RELEASE="v0.1.0-alpha.3-test.1"
SDK_VERSION="10.0.401"
DOTNET_INSTALL_URL="https://dot.net/v1/dotnet-install.sh"

export MAPSTUDIO_SOURCE_HEAD="$SOURCE_HEAD"
export MAPSTUDIO_RELEASE="$RELEASE"
export MAPSTUDIO_DOTNET_SDK="$SDK_VERSION"
export DOTNET_ROOT="$DOTNET_DIR"
export NUGET_PACKAGES="$NUGET_DIR"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export PATH="$DOTNET_DIR:$PATH"

echo "== OMSI Map Studio external alpha build =="
echo "Source HEAD: $SOURCE_HEAD"
echo "Release: $RELEASE"

node "$BUILD_DIR/reconstruct.mjs"

rm -rf "$DOTNET_DIR" "$NUGET_DIR" "$PUBLISH_DIR" "$PUBLIC_DIR"
mkdir -p "$DOTNET_DIR" "$NUGET_DIR" "$DOWNLOAD_DIR" "$PUBLISH_DIR" "$PUBLIC_DIR"

INSTALL_SCRIPT="$DOWNLOAD_DIR/dotnet-install.sh"
curl --fail --location --retry 3 --retry-delay 2 "$DOTNET_INSTALL_URL" --output "$INSTALL_SCRIPT"
bash "$INSTALL_SCRIPT" --version "$SDK_VERSION" --install-dir "$DOTNET_DIR" --no-path

ACTUAL_SDK_VERSION="$(dotnet --version)"
if [[ "$ACTUAL_SDK_VERSION" != "$SDK_VERSION" ]]; then
  echo "Expected .NET SDK $SDK_VERSION, got $ACTUAL_SDK_VERSION." >&2
  exit 1
fi

dotnet --info

cd "$WORKTREE"

echo "== dotnet test =="
dotnet test tests/MapStudio.Core.Tests/MapStudio.Core.Tests.csproj --configuration Release

echo "== React UI install/build =="
cd "$WORKTREE/src/MapStudio.UI"
npm install
npm run build

echo "== Windows x64 self-contained publish =="
cd "$WORKTREE"
dotnet publish src/MapStudio.Desktop/MapStudio.Desktop.csproj \
  --configuration Release \
  --runtime win-x64 \
  --self-contained true \
  --output "$PUBLISH_DIR" \
  -p:EnableWindowsTargeting=true \
  -p:PublishReadyToRun=false \
  -p:PublishTrimmed=false \
  -p:Version=0.1.0-alpha.3-test.1 \
  -p:InformationalVersion=0.1.0-alpha.3-test.1+$SOURCE_HEAD

if [[ ! -f "$PUBLISH_DIR/OMSI Map Studio.exe" ]]; then
  echo "Expected executable was not produced." >&2
  exit 1
fi

if [[ ! -f "$PUBLISH_DIR/ui/index.html" ]]; then
  echo "React UI was not embedded in published output." >&2
  exit 1
fi

echo "== Package ZIP =="
node "$BUILD_DIR/package-artifact.mjs" "$PUBLISH_DIR" "$PUBLIC_DIR"

echo "== Build completed successfully =="
cat "$PUBLIC_DIR/build-info.json"
