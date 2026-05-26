# Probuzhdenie - Build & Run Script
# Скачивает .NET SDK (если нет), восстанавливает пакеты и запускает игру

$DotnetDir = "$env:UserProfile\.dotnet"
$DotnetExe = "$DotnetDir\dotnet.exe"

if (-not (Test-Path $DotnetExe)) {
    Write-Host "Downloading .NET SDK 8.0..." -ForegroundColor Yellow
    $url = "https://dot.net/v1/dotnet-install.ps1"
    $script = "$env:TEMP\dotnet-install.ps1"
    Invoke-WebRequest -Uri $url -OutFile $script -UseBasicParsing
    & $script -Channel 8.0 -InstallDir $DotnetDir
}

$env:PATH = "$DotnetDir;$env:PATH"

Write-Host "Restoring packages..." -ForegroundColor Green
& $DotnetExe restore

if ($LASTEXITCODE -eq 0) {
    Write-Host "Building & Running Probuzhdenie..." -ForegroundColor Green
    & $DotnetExe run --configuration Release
}
