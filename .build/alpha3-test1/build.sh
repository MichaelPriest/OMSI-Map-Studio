#!/usr/bin/env bash
set -euo pipefail

BUILD_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$BUILD_DIR/../.." && pwd)"
WORKTREE="$BUILD_DIR/worktree"
DOTNET_DIR="$BUILD_DIR/dotnet"
NUGET_DIR="$BUILD_DIR/nuget"
DOWNLOAD_DIR="$BUILD_DIR/downloads"
PUBLISH_DIR="$BUILD_DIR/publish/win-x64"
PUBLIC_DIR="$BUILD_DIR/public"

SOURCE_HEAD="b4f18fcf3e132a5780e5be6257539a3bcdc784e1"
RELEASE="v0.1.0-alpha.3-test.1"
SDK_VERSION="10.0.401"
SDK_URL="https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.401/dotnet-sdk-10.0.401-linux-x64.tar.gz"
SDK_SHA512="51c8b999af9e8dd9998c9edc5944e19a90788862068acd38694e098889054ce8c23d4f0c5cccfa16bf187d044562359e5ee69a9f8ad0bbe913ba90311fbce25b"

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

SDK_ARCHIVE="$DOWNLOAD_DIR/dotnet-sdk-$SDK_VERSION-linux-x64.tar.gz"
if [[ ! -f "$SDK_ARCHIVE" ]]; then
  curl --fail --location --retry 3 --retry-delay 2 "$SDK_URL" --output "$SDK_ARCHIVE"
fi

node - "$SDK_ARCHIVE" "$SDK_SHA512" <<'NODE'
const fs = require("fs");
const crypto = require("crypto");
const [file, expected] = process.argv.slice(2);
const actual = crypto.createHash("sha512").update(fs.readFileSync(file)).digest("hex");
if (actual !== expected) {
  console.error(`SDK SHA-512 mismatch: expected ${expected}, got ${actual}`);
  process.exit(1);
}
console.log("Verified .NET SDK SHA-512.");
NODE

tar -xzf "$SDK_ARCHIVE" -C "$DOTNET_DIR"
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
