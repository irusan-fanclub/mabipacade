#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Builds release artifacts for Mabipacade.
.DESCRIPTION
  Produces:
    artifacts/cli/mabipacade.exe          (Cli, framework-dependent single-file)
    artifacts/server/mabipacade-server.exe (Server, framework-dependent single-file)
    artifacts/debugui/Mabipacade.DebugUi.exe (WPF, framework-dependent single-file)
    artifacts/nupkg/Mabipacade.Core.<v>.nupkg
    artifacts/nupkg/Mabipacade.Decoders.<v>.nupkg
    artifacts/nupkg/*.snupkg  (symbol packages)
#>
[CmdletBinding()]
param(
  [switch]$SkipTests
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot/..

$artifacts = Join-Path (Get-Location) 'artifacts'
if (Test-Path $artifacts) { Remove-Item -Recurse -Force $artifacts }
New-Item -ItemType Directory -Path $artifacts | Out-Null

Write-Host '== Restore ==' -ForegroundColor Cyan
dotnet restore

if (-not $SkipTests) {
  Write-Host '== Test ==' -ForegroundColor Cyan
  dotnet test --configuration Release --no-restore
  if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}

function Publish-SingleFile {
  param([string]$Project, [string]$OutSubdir, [switch]$IncludeAllContent)
  $out = Join-Path $artifacts $OutSubdir
  Write-Host "== Publish $Project -> $OutSubdir ==" -ForegroundColor Cyan
  $extraArgs = @()
  if ($IncludeAllContent) {
    $extraArgs += '-p:IncludeAllContentForSelfExtract=true'
  }
  dotnet publish $Project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=embedded `
    --output $out `
    @extraArgs
  if ($LASTEXITCODE -ne 0) { throw "Publish failed for $Project." }
  # Drop pdbs from the artifact directory (DebugType=embedded already inlines them).
  Get-ChildItem $out -Filter *.pdb -ErrorAction SilentlyContinue | Remove-Item -Force
}

Publish-SingleFile 'src/Mabipacade.Cli/Mabipacade.Cli.csproj'         'cli'
Publish-SingleFile 'src/Mabipacade.Server/Mabipacade.Server.csproj'   'server'

# WPF single-file publish: try without IncludeAllContent first; if it fails, retry with it.
try {
  Publish-SingleFile 'src/Mabipacade.DebugUi/Mabipacade.DebugUi.csproj' 'debugui'
} catch {
  Write-Warning "DebugUi single-file publish failed ($($_.Exception.Message)); retrying with -p:IncludeAllContentForSelfExtract=true"
  $debuguiOut = Join-Path $artifacts 'debugui'
  if (Test-Path $debuguiOut) { Remove-Item -Recurse -Force $debuguiOut }
  Publish-SingleFile 'src/Mabipacade.DebugUi/Mabipacade.DebugUi.csproj' 'debugui' -IncludeAllContent
}

# Build the library projects normally so PDBs exist for NuGet symbol packages.
# (dotnet publish with DebugType=embedded embeds PDBs into the exe but does not
# leave a standalone .pdb on disk — dotnet pack --no-build would then fail with NU5026.)
Write-Host '== Build Release (libraries, for pack) ==' -ForegroundColor Cyan
dotnet build src/Mabipacade.Core/Mabipacade.Core.csproj --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Build failed for Mabipacade.Core.' }
dotnet build src/Mabipacade.Decoders/Mabipacade.Decoders.csproj --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Build failed for Mabipacade.Decoders.' }

Write-Host '== Pack (Core + Decoders) ==' -ForegroundColor Cyan
$nupkgOut = Join-Path $artifacts 'nupkg'
New-Item -ItemType Directory -Path $nupkgOut | Out-Null
dotnet pack --configuration Release --no-build --output $nupkgOut `
  src/Mabipacade.Core/Mabipacade.Core.csproj
if ($LASTEXITCODE -ne 0) { throw 'Pack failed for Mabipacade.Core.' }
dotnet pack --configuration Release --no-build --output $nupkgOut `
  src/Mabipacade.Decoders/Mabipacade.Decoders.csproj
if ($LASTEXITCODE -ne 0) { throw 'Pack failed for Mabipacade.Decoders.' }

Write-Host ''
Write-Host '== Done ==' -ForegroundColor Green
Get-ChildItem $artifacts -Recurse -File | ForEach-Object {
  $rel = $_.FullName.Substring($artifacts.Length + 1)
  $size = [math]::Round($_.Length / 1KB, 1)
  Write-Host ("  {0,-50} {1,10:N1} KB" -f $rel, $size)
}
