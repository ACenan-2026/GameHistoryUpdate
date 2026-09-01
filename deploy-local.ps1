# deploy-local.ps1
# ---------------------------------------------------------------------------
# Builds the GameHistory web app (Debug) and publishes it into the local IIS
# test environment at C:\inetpub\wwwroot\GameHistory, using the Local_Debug
# FileSystem publish profile.
#
# This is the same thing the Visual Studio "Publish > Local_Debug" button does,
# but from the command line so it can be automated (git hook, scheduled task,
# or just run after a pull).
#
# Usage (from the repo root):
#     .\deploy-local.ps1
#     .\deploy-local.ps1 -Configuration Release   # optional override
# ---------------------------------------------------------------------------

[CmdletBinding()]
param(
    [string]$Configuration = 'Debug',
    [string]$Profile = 'Local_Debug',
    # Where to deploy. If omitted, falls back to the GameHistoryDeployPath env
    # var, then to the default in the publish profile (standard IIS root).
    [string]$PublishUrl
)

$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'GameHistory\GameHistory\GameHistory.csproj'
if (-not (Test-Path $project)) { throw "Project not found: $project" }

# Locate MSBuild.exe via vswhere (ships with Visual Studio 2017+ / Build Tools).
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) {
    throw "vswhere.exe not found. Install Visual Studio 2017+ or the Build Tools for Visual Studio."
}

$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild `
    -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw "MSBuild.exe not found via vswhere." }

# Resolve the deploy target for logging (param > env var > profile default).
$target = if ($PublishUrl) { $PublishUrl }
          elseif ($env:GameHistoryDeployPath) { $env:GameHistoryDeployPath }
          else { 'C:\inetpub\wwwroot\GameHistory (profile default)' }

$msbuildArgs = @(
    $project,
    '/p:DeployOnBuild=true',
    "/p:PublishProfile=$Profile",
    "/p:Configuration=$Configuration",
    '/p:Platform=AnyCPU',
    '/verbosity:minimal',
    '/nologo'
)
if ($PublishUrl) { $msbuildArgs += "/p:publishUrl=$PublishUrl" }

Write-Host "Publishing GameHistory ($Configuration) -> $target" -ForegroundColor Cyan

& $msbuild @msbuildArgs

if ($LASTEXITCODE -ne 0) { throw "Publish failed (MSBuild exit code $LASTEXITCODE)." }

Write-Host "Done. Local test site is up to date." -ForegroundColor Green
