@echo off
setlocal
set "SAVE_DIR=%APPDATA%\Probuzhdenie"

echo M2 blind playtest clean-save reset
echo.
echo This removes ONLY:
echo   "%SAVE_DIR%"
echo.
choice /M "Reset this playtest save"
if errorlevel 2 exit /b 0

if exist "%SAVE_DIR%" (
  rmdir /S /Q "%SAVE_DIR%"
  if exist "%SAVE_DIR%" (
    echo ERROR: could not remove playtest save directory.
    exit /b 1
  )
)

echo Clean playtest state ready.
exit /b 0
