# Native Graphics Smoke Test

Run from the repository root on Windows with an OpenGL 3.3-capable desktop:

```powershell
dotnet run --project tests/GraphicsSmoke/GraphicsSmoke.csproj -c Release
```

The test opens the real game window, checks scripted menu clicks and portrait
rotation, renders twenty-six scenes, checks portrait pixels and GL errors, compares
shadow-enabled/disabled pixels, then minimizes/restores the window.
Images go to `artifacts/manhattan-refresh`.
Pass `--hero-qa` to write the same scenes, including the turnaround and face
close-up, to `artifacts/hero-refinement` without overwriting earlier captures.
The close-up and turnaround are actual game geometry, not edited concept art.
It uses the game's profiling sandbox, so it does not load or write player saves.
The zero-frame `visual-profile.json` is a teardown artifact, NOT a performance result.

Six departure captures cover the parked tram, Lida's dispatch gesture, movement,
braking for the player, resuming and the cleared stop. Pixel checks compare the
tram with/without drawing at its projected world position. The probe also checks
that its VBO and total allocated geometry bytes stay stable during departure.
Two more captures cover failed loading without a fake player preview and a
successful recovery notice in a tall window.

The `.cs.txt` extension keeps the harness outside the root project's default C#
glob. This project explicitly compiles it and references the game project.

## Gameplay And Save Recovery

Run the gameplay probe separately from the visual probe:

```powershell
dotnet run --project tests/GraphicsSmoke/GraphicsSmoke.csproj -c Release -- --playthrough
```

The probe supplies scripted keyboard state to the normal game update, player
controller, interaction detector and menu/dialogue handlers. It walks instead
of teleporting and does not directly invoke story choices. Both repair and delay
routes close and recreate the window, reload progress, read the journal and
advance to the second morning. The repair route pauses and reloads during the
animated tram departure. It also checks new-cycle cancellation, a backup
of the previous cycle, failed writes, the failed-close guard, autosave recovery
after resuming play and deliberate exit without saving.
The eight routes also cover outdoor position/orientation, an interior saved and
reopened with a real mesh, subsequent movement/exit, backup recovery, and retry
after an initially unrecoverable load. Closing the failed-load screen must leave
both invalid files unchanged. No new world may be created on that error path.

Saves and the result report are isolated under `artifacts/playthrough-<UTC time>`.
The player's normal save is not read or written. IO errors in the `newcycle`
and `savefailure` fixtures and parsing errors in `loadfailure` are intentional;
the final result must still be PASS.
The probe has a watchdog and exits nonzero if an assertion fails or the window
is closed before completion. This verifies scripted gameplay, not native key
timing, human comprehension or a performance budget.

For a manual session using a separate first-morning save:

```powershell
dotnet run --project tests/GraphicsSmoke/GraphicsSmoke.csproj -c Release -- --interactive-playtest
```

The interactive mode prints its save path and leaves control to the player. It
uses the normal game settings; no automated pass result is produced.

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
