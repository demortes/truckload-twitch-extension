<#
.SYNOPSIS
  Builds the Truckload Bridge Windows installer (installer\output\TruckloadSetup-v<Version>.exe).

.DESCRIPTION
  1. Publishes the Bridge as a self-contained single-file win-x64 exe.
  2. Downloads the pinned TruckTel release, verifies its SHA-256, and extracts the plugin.
  3. Compiles installer\TruckloadSetup.iss with Inno Setup 6 (ISCC.exe must be installed).

.EXAMPLE
  .\installer\build.ps1 -Version 1.3.0
#>
param(
    [Parameter(Mandatory)][string]$Version,
    [string]$IngestUrl = ''
)

$ErrorActionPreference = 'Stop'

# Pinned TruckTel release (https://github.com/jvanstraten/TruckTel, MIT). Bump both together.
$TruckTelVersion = 'v0.1.2'
$TruckTelSha256 = '0047d4a525d9f5a2b1f15c432a94430dc5519f442e272e20a06aa524f357664d'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$staging = Join-Path $PSScriptRoot 'staging'
$publish = Join-Path $staging 'bridge-publish'

Remove-Item -Recurse -Force $staging -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $staging | Out-Null

Write-Host "==> Publishing Truckload.Bridge $Version"
dotnet publish (Join-Path $root 'bridge/Truckload.Bridge') -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version=$Version `
    -o $publish
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
Copy-Item (Join-Path $publish 'Truckload.Bridge.exe') $staging

Write-Host "==> Fetching TruckTel $TruckTelVersion"
$zip = Join-Path $staging 'trucktel.zip'
Invoke-WebRequest -UseBasicParsing `
    -Uri "https://github.com/jvanstraten/TruckTel/releases/download/$TruckTelVersion/trucktel.zip" `
    -OutFile $zip
$actual = (Get-FileHash -Algorithm SHA256 $zip).Hash.ToLowerInvariant()
if ($actual -ne $TruckTelSha256) {
    throw "TruckTel checksum mismatch: expected $TruckTelSha256 but got $actual"
}
$extract = Join-Path $staging 'trucktel-extract'
Expand-Archive -Path $zip -DestinationPath $extract
Copy-Item (Join-Path $extract 'trucktel.dll') $staging
New-Item -ItemType Directory -Force -Path (Join-Path $staging 'trucktel') | Out-Null
Copy-Item (Join-Path $extract 'trucktel/LICENSE') (Join-Path $staging 'trucktel')

Write-Host '==> Compiling installer'
$iscc = @(
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source,
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 (ISCC.exe) not found. Install it from https://jrsoftware.org/isinfo.php' }

$isccArgs = @("/DAppVersion=$Version")
if ($IngestUrl) { $isccArgs += "/DDefaultIngestUrl=$IngestUrl" }
$isccArgs += (Join-Path $PSScriptRoot 'TruckloadSetup.iss')
& $iscc @isccArgs
if ($LASTEXITCODE -ne 0) { throw 'ISCC failed' }

Write-Host "==> Done: $(Join-Path $PSScriptRoot "output/TruckloadSetup-v$Version.exe")"
