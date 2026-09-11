[CmdletBinding()]
param(
    [string]$EngineRoot,
    [switch]$CheckOnly,
    [switch]$BuildScene,
    [switch]$Open
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'Awakening\Awakening.uproject'
$version = (Get-Content -LiteralPath $project -Raw | ConvertFrom-Json).EngineAssociation
if (-not $EngineRoot) {
    $manifest = 'C:\ProgramData\Epic\UnrealEngineLauncher\LauncherInstalled.dat'
    if (Test-Path -LiteralPath $manifest) {
        $install = (Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json).InstallationList |
            Where-Object AppName -EQ "UE_$version" | Select-Object -First 1
        if ($install) { $EngineRoot = $install.InstallLocation }
    }
    if (-not $EngineRoot) { $EngineRoot = "C:\Program Files\Epic Games\UE_$version" }
}
$build = Join-Path $EngineRoot 'Engine\Build\BatchFiles\Build.bat'
$editor = Join-Path $EngineRoot 'Engine\Binaries\Win64\UnrealEditor.exe'
$commandlet = Join-Path $EngineRoot 'Engine\Binaries\Win64\UnrealEditor-Cmd.exe'
if (-not (Test-Path -LiteralPath $build) -or -not (Test-Path -LiteralPath $editor)) {
    throw "Unreal Engine $version was not found at '$EngineRoot'. Install it through Epic Games Launcher or supply -EngineRoot. No installation or download was performed."
}
$engineVersion = Get-Content -LiteralPath (Join-Path $EngineRoot 'Engine\Build\Build.version') -Raw | ConvertFrom-Json
if ("$($engineVersion.MajorVersion).$($engineVersion.MinorVersion)" -ne $version) {
    throw "This scaffold targets UE $version. Engine conversion must be reviewed explicitly; this script will not modify the project association."
}
Write-Output "Engine found: $EngineRoot"
Write-Output "Project: $project"
if ($CheckOnly) { return }

& $build AwakeningEditor Win64 Development "-Project=$project" -WaitMutex -NoHotReloadFromIDE
if ($LASTEXITCODE -ne 0) { throw "Unreal C++ build failed ($LASTEXITCODE). Scene generation was not started." }
if ($BuildScene) {
    $script = Join-Path $PSScriptRoot 'Awakening\Content\Python\build_street.py'
    $report = Join-Path $PSScriptRoot 'Awakening\Saved\AwakeningBootstrap.json'
    $started = [DateTime]::UtcNow
    & $commandlet $project /Engine/Maps/Entry -run=pythonscript "-script=$script" -unattended -nop4 -AllowCommandletRendering
    if ($LASTEXITCODE -ne 0) { throw "Scene generation failed ($LASTEXITCODE). See Awakening/Saved/Logs." }
    if (-not (Test-Path -LiteralPath $report) -or (Get-Item -LiteralPath $report).LastWriteTimeUtc -lt $started) {
        throw 'Scene generation did not produce a fresh completion report. Do not treat this run as successful.'
    }
}
if ($Open) {
    $map = Join-Path $PSScriptRoot 'Awakening\Content\AwakeningPrototype\Maps\MeraStreet.umap'
    if (-not (Test-Path -LiteralPath $map)) { throw 'Generate the scene with -BuildScene before opening it.' }
    & $editor $project /Game/AwakeningPrototype/Maps/MeraStreet
}
