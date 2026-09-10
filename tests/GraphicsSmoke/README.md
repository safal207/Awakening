# Native Graphics Smoke Test

Run from the repository root on Windows with an OpenGL 3.3-capable desktop:

```powershell
dotnet run --project tests/GraphicsSmoke/GraphicsSmoke.csproj -c Release
```

The test opens the real game window, checks scripted menu clicks and portrait
rotation, renders ten scenes, checks portrait pixels and GL errors, then
minimizes/restores the window. Images go to `artifacts/visual-refresh`.
It uses the game's profiling sandbox, so it does not load or write player saves.
The zero-frame `visual-profile.json` is a teardown artifact, NOT a performance result.

The `.cs.txt` extension keeps the harness outside the root project's default C#
glob. This project explicitly compiles it and references the game project.

## Performance

Run separately from screenshot capture and other GPU-heavy tasks:

```powershell
dotnet run --project Probuzhdenie.csproj -c Release -- --runtime-profile --profile-seconds=120 --profile-output=artifacts/visual-refresh/runtime.json
```

For additional window diagnostics with a watchdog:

```powershell
dotnet run --project tests/GraphicsSmoke/GraphicsSmoke.csproj -c Release -- --runtime-smoke --runtime-profile --profile-seconds=120 --profile-output=artifacts/visual-refresh/runtime-diagnostic.json
```

With the running game process ID, a second terminal can sample Windows GPU
process counters. Missing counters are recorded as unavailable, not as zero.

```powershell
./tests/GraphicsSmoke/Measure-Gpu.ps1 -GameProcessId <process-id>
```

The automatic route measures rendering and simulation load. It deliberately
moves through the map and does not prove walkability, narrative progression,
full controller support, long-term memory stability or network readiness.
Screenshots use scripted camera/time values, not a completed story playthrough.
