[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    # Bundle the .NET Desktop Runtime so the result can run on a machine
    # without .NET installed. Produces a larger dist folder.
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$project = Join-Path $root "src\CcDock\CcDock.csproj"
$dist = Join-Path $root "dist\ccDock"
$syncthing = Join-Path $root "vendor\syncthing\syncthing-windows-amd64-v2.1.7\syncthing.exe"

if (-not (Test-Path $project)) {
    throw "Project file not found: $project"
}
if (-not (Test-Path $syncthing)) {
    Write-Host "Syncthing is missing; downloading it first..." -ForegroundColor Yellow
    & (Join-Path $root "scripts\fetch-syncthing.ps1")
    if (-not (Test-Path $syncthing)) {
        throw "Vendored Syncthing not found: $syncthing"
    }
}

if (Test-Path $dist) {
    Remove-Item $dist -Recurse -Force
}

$selfContainedValue = if ($SelfContained) { "true" } else { "false" }
$arguments = @(
    "publish", $project,
    "-c", $Configuration,
    "-r", "win-x64",
    "--self-contained", $selfContainedValue,
    "-o", $dist
)

Write-Host "Publishing ccDock ($Configuration, self-contained=$selfContainedValue)..." -ForegroundColor Cyan
& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$syncthingTarget = Join-Path $dist "syncthing"
New-Item -ItemType Directory -Path $syncthingTarget -Force | Out-Null
Copy-Item $syncthing (Join-Path $syncthingTarget "syncthing.exe") -Force
Copy-Item (Join-Path (Split-Path $syncthing -Parent) "LICENSE.txt") (Join-Path $syncthingTarget "LICENSE.txt") -Force

Write-Host ""
Write-Host "Build complete: $dist" -ForegroundColor Green
Write-Host "Run: $(Join-Path $dist 'ccDock.exe')"
