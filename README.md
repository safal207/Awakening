# Awakening — Voxel PixelArt Engine

![Real unmodified game screenshot recorded under Xvfb/Mesa with OpenGL](https://raw.githubusercontent.com/safal207/test-it-python/main/dist/ai-path/assets/awakening.webp)

[Открыть реальный игровой кадр](https://safal207.github.io/test-it-python/ai-path/assets/awakening.webp) · [Происхождение снимка и протокол](https://github.com/safal207/test-it-python/blob/main/dist/ai-path/assets/sources.json) · [Путь ученика: C# и 3D](docs/LEARNING_PATH.md)

> **Реальная игра, не AI-макет.** Кадр записан 17 октября 2026 года из запущенного десктопного движка по коммиту `a92bd1560215833a232362ad78de88335e51ac81`. Копия хранится в публичном учебном репозитории AI PATH; это отдельный источник, а не сгенерированная иллюстрация.

A voxel world engine written in C# (.NET) with OpenGL 3.3 Core rendering.
Each block face is an 8×8 pixel grid (64 colored pixels per face) — no textures, pure geometry with color.

> Inspired by Minecraft-style voxel worlds with a unique pixel-art aesthetic.

## Features

- **Voxel world** with procedural generation via Perlin noise
- **PixelArt faces** — each block face is an 8×8 RGB pixel grid
- **Chunk system** — 16×64×16 blocks per chunk, dirty-flag mesh rebuild
- **Block interaction** — place (RMB) and destroy (LMB) blocks, raycast up to 8 blocks
- **Directional lighting** + ambient, day/night cycle
- **FreeCity module** — city generator with NPC characters, hero progress, save system
- **Runtime profiler** — built-in performance profiling (`RuntimeProfile.cs`)
- **Functional tests** — `FunctionalTests.cs` inside `src/FreeCity/`

## Block Types

`Air`, `Grass`, `Dirt`, `Stone`, `Wood`, `Leaves`, `Sand`, `Planks`, `Bricks`, `Water`, `Glass`

## Controls

| Key | Action |
|-----|--------|
| `WASD` | Move |
| `Space` | Up |
| `Ctrl` | Down |
| `Shift` | Sprint |
| `Mouse` | Look around |
| `LMB` | Destroy block |
| `RMB` | Place block |

## Project Structure

```
src/
├── Game.cs              — main loop, render
├── Camera.cs            — camera
├── Input.cs             — input handler
├── UiRenderer.cs        — UI rendering
├── RuntimeProfile.cs    — performance profiling
├── Player/              — player logic
└── FreeCity/
    ├── CityGenerator.cs   — procedural city generation
    ├── CityRenderer.cs    — city rendering
    ├── NpcCharacter.cs    — NPC AI and behavior
    ├── HeroProgress.cs    — hero progression system
    ├── HeroStyle.cs       — hero appearance
    ├── SaveSystem.cs      — save/load
    ├── Awareness.cs       — awareness system
    ├── InterestMarker.cs  — points of interest
    └── FunctionalTests.cs — functional tests
```

## Requirements

- [.NET 8+](https://dotnet.microsoft.com/download)
- OpenGL 3.3 compatible GPU
- Windows (build scripts: `build.bat`, `build_and_run.ps1`)

## Build & Run

### Windows (PowerShell)
```powershell
.\build_and_run.ps1
```

### Windows (CMD)
```bat
build.bat
```

### Manual
```bash
dotnet build Probuzhdenie.csproj
dotnet run
```

## Проверка и путь для начинающих

Для локального инженерного контроля без открытия окна игры:

```bash
dotnet build --configuration Release
dotnet run --configuration Release -- --functional-test
dotnet run --configuration Release -- --self-test
```

[CI workflow](.github/workflows/ci.yml) проверяет сборку и два набора тестов. Графическое поведение и скриншот требуют отдельного реального запуска. Это **десктопный проект**, не браузерная демоигра.

В [учебном маршруте](docs/LEARNING_PATH.md) есть 4 небольшие задачи для первого вклада — от карты управления и работы с блоками до теста на повторное начисление награды и демонстрации FPS.

## License

MIT — see [LICENSE](LICENSE) for details.
