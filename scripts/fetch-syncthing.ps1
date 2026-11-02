[CmdletBinding()]
param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$version = "v2.1.7"
$zipName = "syncthing-windows-amd64-$version.zip"
$url = "https://github.com/syncthing/syncthing/releases/download/$version/$zipName"
$expectedSha256 = "3ce7da770f546a8d02b65b2e7ee31f2a3b8b7c3f990ede045590cca4172ecfe1"

$vendorRoot = Join-Path $root "vendor\syncthing"
$targetDir = Join-Path $vendorRoot "syncthing-windows-amd64-$version"
$targetExe = Join-Path $targetDir "syncthing.exe"

if ((Test-Path $targetExe) -and -not $Force) {
    Write-Host "Syncthing $version is already present: $targetExe"
    return
}

Write-Host "Downloading Syncthing $version..." -ForegroundColor Cyan
$tempZip = Join-Path ([System.IO.Path]::GetTempPath()) ("ccdock-syncthing-" + [Guid]::NewGuid().ToString("N") + ".zip")
try {
    Invoke-WebRequest -Uri $url -OutFile $tempZip -UseBasicParsing

    $actualSha256 = (Get-FileHash $tempZip -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualSha256 -ne $expectedSha256) {
        throw "Syncthing archive checksum mismatch. Expected $expectedSha256, got $actualSha256."
    }

    New-Item -ItemType Directory -Path $vendorRoot -Force | Out-Null
    Expand-Archive -Path $tempZip -DestinationPath $vendorRoot -Force

    if (-not (Test-Path $targetExe)) {
        throw "The Syncthing archive did not contain the expected executable: $targetExe"
    }
}
finally {
    if (Test-Path $tempZip) {
        Remove-Item $tempZip -Force
    }
}

Write-Host "Syncthing $version is ready." -ForegroundColor Green
