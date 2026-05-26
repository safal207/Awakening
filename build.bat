@echo off
echo == Probuzhdenie - Game Builder ==
echo.

where dotnet >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo [INFO] .NET SDK not found. Installing...
    curl -L -o "%TEMP%\dotnet-install.ps1" "https://dot.net/v1/dotnet-install.ps1"
    powershell -NoProfile -ExecutionPolicy Bypass -Command "& '%TEMP%\dotnet-install.ps1' -Channel 8.0"
    set "PATH=%PATH%;%USERPROFILE%\.dotnet"
)

echo [BUILD] Restoring packages...
dotnet restore

echo [BUILD] Compiling...
dotnet build -c Release --no-restore

echo.
echo [DONE] Build complete!
echo.
echo To run: dotnet run --project Probuzhdenie.csproj
echo Controls: WASD=move, Shift=sprint, Mouse=orbit camera,
echo           J/K=zoom, C=look at yourself, Esc=menu
