[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PublishDirectory,
    [Parameter(Mandatory)][string]$PackageDirectory,
    [string]$Version = 'CI test',
    [string]$Commit = '',
    [string]$WorkflowUrl = ''
)
$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $PublishDirectory).Path).TrimEnd([char[]]@('\','/'))
$destination = [IO.Path]::GetFullPath($ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($PackageDirectory)).TrimEnd([char[]]@('\','/'))
$comparison = [StringComparison]::OrdinalIgnoreCase
$separator = [IO.Path]::DirectorySeparatorChar
if ($source.Equals($destination,$comparison) -or $destination.StartsWith($source + $separator,$comparison) -or $source.StartsWith($destination + $separator,$comparison)) {
    throw 'Publish and package directories must be separate.'
}
foreach ($required in @('MapStudio.Native.exe','MapStudio.Native.pri','MapStudio.Native.dll','MapStudio.Native.runtimeconfig.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $required) -PathType Leaf)) { throw "Missing runtime file: $required" }
}
if ((Test-Path -LiteralPath $destination) -and @(Get-ChildItem -LiteralPath $destination -Force).Count -gt 0) { throw 'Package directory must be empty.' }
$app = Join-Path $destination 'app'
$docs = Join-Path $destination 'Documentacao'
New-Item -ItemType Directory -Force -Path $app,$docs | Out-Null
Get-ChildItem -LiteralPath $source -Force | Copy-Item -Destination $app -Recurse -Force
$files = @(Get-ChildItem -LiteralPath $source -File -Recurse -Force)
foreach ($file in $files) {
    $relative = [IO.Path]::GetRelativePath($source,$file.FullName)
    $copied = Join-Path $app $relative
    if (-not (Test-Path -LiteralPath $copied -PathType Leaf)) { throw "Missing copied runtime file: $relative" }
    if ((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $copied -Algorithm SHA256).Hash) { throw "Runtime file changed: $relative" }
}
@'
@echo off
setlocal
if not exist "%~dp0app\MapStudio.Native.exe" (
  echo Extraia o ZIP inteiro antes de executar.
  pause
  exit /b 1
)
start "" /D "%~dp0app" "%~dp0app\MapStudio.Native.exe"
'@ | Set-Content -LiteralPath (Join-Path $destination 'Iniciar OMSI Map Studio.cmd') -Encoding ascii
@'
OMSI MAP STUDIO - WINDOWS X64
1. Extraia todo o ZIP.
2. Execute "Iniciar OMSI Map Studio.cmd".
3. Nao mova arquivos dentro de app/.
4. Consulte os manuais e dados da build em Documentacao/.
5. Use um workspace de teste; preserve seus mapas originais.

EN: Extract the complete ZIP and run "Iniciar OMSI Map Studio.cmd".
Keep the app/ folder intact. Documentation is in Documentacao/.
'@ | Set-Content -LiteralPath (Join-Path $destination 'LEIA-ME.txt') -Encoding utf8
$manualDir = Join-Path $app 'Docs'
if (Test-Path -LiteralPath $manualDir -PathType Container) {
    Get-ChildItem -LiteralPath $manualDir -File | Where-Object { $_.Extension -in '.md','.pdf' } | Copy-Item -Destination $docs -Force
}
@(
    'Product: OMSI Map Studio'
    'Platform: Windows x64'
    "Version: $Version"
    "Commit: $Commit"
    "Workflow: $WorkflowUrl"
    "Built at (UTC): $([DateTime]::UtcNow.ToString('o'))"
    "Verified runtime files: $($files.Count)"
    'CI does not certify manual validation in OMSI 2.'
) | Set-Content -LiteralPath (Join-Path $docs 'BUILD-INFO.txt') -Encoding utf8
$manifest = Join-Path $docs 'SHA256SUMS.txt'
Get-ChildItem -LiteralPath $destination -File -Recurse -Force | Sort-Object FullName | ForEach-Object {
    $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $relative = [IO.Path]::GetRelativePath($destination,$_.FullName).Replace('\','/')
    "$hash  $relative"
} | Set-Content -LiteralPath $manifest -Encoding ascii
if (@(Get-ChildItem -LiteralPath $destination -File -Force).Count -ne 2) { throw 'Unexpected loose files in package root.' }
Write-Host "Portable package created. $($files.Count) runtime files verified."
