@echo off
setlocal
cd /d "%~dp0"

echo Starting Awakening M2 blind playtest...
echo A local-only action trace will be saved next to the game.
echo No trace data is sent anywhere.
echo.

"%~dp0Probuzhdenie.exe" --playtest-trace "%~dp0PLAYTEST_TRACE.jsonl"
set "GAME_EXIT=%ERRORLEVEL%"

echo.
echo Session finished. Keep the newest PLAYTEST_TRACE*.jsonl with the observer notes.
exit /b %GAME_EXIT%
