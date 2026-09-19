param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$ArtifactsDirectory = "artifacts",
    [switch]$SkipUiBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$uiDirectory = Join-Path $repoRoot "src/MapStudio.UI"
$desktopProject = Join-Path $repoRoot "src/MapStudio.Desktop/MapStudio.Desktop.csproj"
$publishDirectory = Join-Path $repoRoot "$ArtifactsDirectory/publish/$Runtime"
$outputDirectory = Join-Path $repoRoot $ArtifactsDirectory
$installerScript = Join-Path $PSScriptRoot "MapStudio.iss"

if (-not $SkipUiBuild) {
    Push-Location $uiDirectory
    try {
        npm install --no-audit --no-fund
        npm run build
    }
    finally {
        Pop-Location
    }
}

New-Item -ItemType Directory -Force -Path $publishDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

$publishArgs = @(
    "publish",
    $desktopProject,
    "--configuration", $Configuration,
    "--runtime", $Runtime,
    "--self-contained", "true",
    "--output", $publishDirectory,
    "-p:EnableWindowsTargeting=true",
    "-p:PublishReadyToRun=false",
    "-p:PublishTrimmed=false",
    "-p:DebugType=None",
    "-p:DebugSymbols=false",
    "-p:Version=$Version",
    "-p:InformationalVersion=$Version"
)

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

$exePath = Join-Path $publishDirectory "OMSI Map Studio.exe"
$uiIndex = Join-Path $publishDirectory "ui/index.html"
if (-not (Test-Path $exePath)) { throw "Expected desktop executable not found: $exePath" }
if (-not (Test-Path $uiIndex)) { throw "React UI output not found: $uiIndex" }

$isccCandidates = @(
    $env:ISCC_PATH,
    "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) }

if ($isccCandidates.Count -eq 0) { throw "Inno Setup 6 not found. Install it or set ISCC_PATH." }
$iscc = $isccCandidates[0]

& $iscc "/DAppVersion=$Version" "/DSourceDir=$publishDirectory" "/DOutputDir=$outputDirectory" $installerScript
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed with exit code $LASTEXITCODE" }

$installerPath = Join-Path $outputDirectory "OMSI-Map-Studio-Setup-$Version-win-x64.exe"
if (-not (Test-Path $installerPath)) { throw "Expected installer executable not found: $installerPath" }

$hash = (Get-FileHash $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $(Split-Path -Leaf $installerPath)" | Set-Content "$installerPath.sha256" -Encoding ascii

Write-Host "Installer ready: $installerPath"
Write-Host "SHA256: $hash"