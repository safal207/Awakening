# Native Graphics Smoke Test

Run from the repository root on Windows with an OpenGL 3.3-capable desktop:

```powershell
dotnet run --project tests/GraphicsSmoke/GraphicsSmoke.csproj -c Release
```

The test opens the real game window, checks scripted menu clicks and portrait
rotation, renders eleven scenes, checks portrait pixels and GL errors, compares
shadow-enabled/disabled pixels, then minimizes/restores the window.
Images go to `artifacts/manhattan-refresh`.
Pass `--hero-qa` to write the same scenes, including the turnaround and face
close-up, to `artifacts/hero-refinement` without overwriting earlier captures.
The close-up and turnaround are actual game geometry, not edited concept art.
It uses the game's profiling sandbox, so it does not load or write player saves.
The zero-frame `visual-profile.json` is a teardown artifact, NOT a performance result.

The `.cs.txt` extension keeps the harness outside the root project's default C#
glob. This project explicitly compiles it and references the game project.

## Performance

Run separately from screenshot capture and other GPU-heavy tasks:

Keep the test window restored while the profile runs. A minimized window pauses
the game's renderer and is not an active graphics workload. Use the diagnostic
runner below to record window size/state; its `Renders` counter counts callbacks,
while the JSON `FrameCount` counts frames actually submitted by the game.
Check `Interrupted`, `DurationSeconds` and `WallClockSeconds` before accepting
a run. A process that stayed alive for ten minutes is not necessarily a
ten-minute rendering test.

```powershell
dotnet run --project Probuzhdenie.csproj -c Release -- --runtime-profile --profile-seconds=600 --profile-output=artifacts/manhattan-refresh/runtime.json
```

For additional window diagnostics with a watchdog:

```powershell
dotnet run --project tests/GraphicsSmoke/GraphicsSmoke.csproj -c Release -- --runtime-smoke --runtime-profile --profile-seconds=120 --profile-output=artifacts/visual-refresh/runtime-diagnostic.json
```

With the running game process ID, a second terminal can sample Windows GPU
process counters. Missing counters are recorded as unavailable, not as zero.

```powershell
./tests/GraphicsSmoke/Measure-Gpu.ps1 -GameProcessId <process-id> -OutputPath artifacts/manhattan-refresh/gpu-memory.csv -MaxSeconds 640
```

The automatic route measures rendering and simulation load. It deliberately
moves through the map and does not prove walkability, narrative progression,
full controller support, long-term memory stability or network readiness.
Screenshots use scripted camera/time values, not a completed story playthrough.
