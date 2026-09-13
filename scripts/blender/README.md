# Blender previs scripts

## First Memory

`build_first_memory_previs.py` создаёт воспроизводимый gray-box previs первой сцены «Пробуждения» без внешних ассетов.

### Build only

```powershell
blender --background --python scripts/blender/build_first_memory_previs.py -- `
  --output artifacts/previs/first-memory-previs.blend
```

### Build + render MP4

```powershell
blender --background --python scripts/blender/build_first_memory_previs.py -- `
  --output artifacts/previs/first-memory-previs.blend `
  --render artifacts/previs/first-memory-previs.mp4
```

Ожидаемый результат:

- 24 fps;
- frames `1..672`;
- камеры `CAM_01_ROUTINE` … `CAM_08_TITLE`;
- timeline markers для восьми shots;
- HERO / LIDA / MARK и повторяющий движение NPC;
- tram stop, signal, plaza, tram и persistent note;
- camera binding по shot markers;
- causal metadata в свойствах Scene.

## Назначение

Этот скрипт не создаёт финальную графику и не доказывает, что gameplay-механика якорей памяти уже реализована. Он нужен для проверки:

1. причинной читаемости;
2. blocking;
3. screen direction / eyelines;
4. camera grammar;
5. payoff после Сверки.

Перед дорогой видеогенерацией gray-box должен пройти viewer test из `docs/cinematics/FIRST_MEMORY_PREVIS.md`.
