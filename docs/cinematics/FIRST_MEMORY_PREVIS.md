# First Memory — Blender previs

Статус: production blueprint для проверки первой сцены «Пробуждения» до финального видео и до дорогой генерации.

Этот документ описывает **previs**, а не обещает готовую кат-сцену или реализованный сюжет. Его цель — проверить причинность, географию, камеру и эмоциональный payoff первого memory-anchor эпизода.

## Зачем

Главный принцип:

> Сначала причина и постановка, потом дорогой рендер.

Previs считается полезным только если зритель по серой сцене понимает три вещи:

1. город повторяет состояние;
2. действие героя создало возможность встречи;
3. отношение между двумя людьми пережило Сверку.

Если это приходится объяснять словами, меняем blocking/camera/timing, а не тратим ещё одну генерацию видео.

## Формат

- 28 секунд
- 24 fps
- 672 кадра
- 1920×1080
- 16:9
- Blender metric, Z-up

## Сцена

Компактный район Меры:

- трамвайная остановка в центре;
- маленькая площадь;
- пешеходный сигнал;
- трамвайная линия;
- окружающие малоэтажные здания;
- герой — работник городской службы;
- Лида — диспетчер;
- Марк — садовник.

Условные координаты:

```text
Hero start       ( 4.0,-18.0,0.0 )
Tram stop        ( 0.0,  0.0,0.0 )
Lida             (-2.0,  1.0,0.0 )
Mark entry       (-8.0,  8.0,0.0 )
Signal           ( 2.0, -1.0,0.0 )
Plaza center     (-3.0,  4.0,0.0 )
Tram line Y       3.5
```

## Shot list

### 01 — Routine — frames 1–96

Раннее утро. Герой идёт обычным маршрутом.

Camera `CAM_01_ROUTINE`:

```text
start (7.5,-22.5,2.4)
end   (5.5, -9.5,2.2)
lens  35 mm
```

Мягкий tracking, почти человеческая высота, очень слабое микродвижение камеры. Город спокоен и предсказуем.

### 02 — Anomaly — frames 97–192

Герой приближается к остановке. Сигнал ведёт себя неправильно. Один прохожий повторяет короткий фрагмент шага; дальний трамвай на несколько кадров оказывается в двух чуть смещённых положениях.

Не использовать цифровой glitch.

Герой замедляется и останавливается около `(1.3,-2.2,0)`, но камера ещё немного продолжает движение. Это первый визуальный разрыв между человеком и распорядком.

Camera `CAM_02_ANOMALY`, 40 mm.

### 03 — Recognition — frames 193–288

Medium close-up героя.

Camera `CAM_03_FACE`, 65 mm, очень медленный push-in.

Герой сначала переводит взгляд, затем слегка голову. Эмоция — любопытство и узнавание, не паника.

Лида: «Ты опять пришёл.»

Герой после паузы: «Опять?»

Диалог не обязателен для версии без звука: смысл должен читаться по реакции.

### 04 — Choice — frames 289–408

Герой ремонтирует сигнал. Приближается трамвай. В одном читаемом кадре должны быть связаны:

- герой;
- сигнал;
- Лида;
- направление трамвая;
- площадь.

Герой прекращает ремонт, убирает инструмент и идёт к Лиде.

Никакого UI выбора.

Camera `CAM_04_CHOICE`, примерно 42 mm, почти статичная.

### 05 — Meeting — frames 409–504

Марк входит на площадь. Лида задерживает отправление, и они впервые оказываются рядом.

В начале герой композиционно важен. Затем камера медленно смещается так, чтобы Лида и Марк стали центром сцены, а герой — вторичным.

Camera `CAM_05_MEETING`, 40 mm, restrained lateral move.

Принцип:

> Герой создаёт возможность, но не владеет чужим событием.

### 06 — Sverka — frames 505–576

Ночь. Высокий ракурс.

Город поэтапно возвращается к сохранённому распорядку:

1. трамвай;
2. сигнал;
3. уличные предметы;
4. позиции/распорядок людей;
5. предутренний свет.

Не использовать порталы, взрывы или магическую энергию.

Один небольшой след памяти — например записка на остановке — остаётся.

Camera `CAM_06_RESET`, около `(0,-5,18)`, 32 mm, медленный descent.

### 07 — Memory — frames 577–648

Следующее утро.

Повторить визуальную грамматику Shot 01 почти буквально: тот же объектив, направление и похожую траекторию камеры.

Лида видит Марка и впервые узнаёт его:

> «Доброе утро, Марк.»

Марк останавливается и слегка улыбается.

Герой проходит на заднем плане. Он не останавливается — только едва поворачивает голову.

Camera `CAM_07_MEMORY`: копия/рифма `CAM_01_ROUTINE`, 35 mm.

### 08 — Title — frames 649–672

Чёрный экран.

```text
ПРОБУЖДЕНИЕ

ГОРОД ЗАБЫВАЕТ.
ЛЮДИ МОГУТ ПОМНИТЬ.
```

## Цветовое кодирование gray-box

Это не финальная палитра, а помощь чтению сцены:

```text
Environment  neutral gray
Hero         muted blue-gray
Lida         muted warm brown
Mark         muted green
Memory trace pale cyan
```

Без неона.

## Camera rules

- Routine: плавное предсказуемое движение.
- Anomaly: камера кратко продолжает ритм после остановки героя.
- Recognition: медленный push-in.
- Choice: камера спокойнее, чем герой.
- Meeting: плавное смещение внимания от героя к Лиде и Марку.
- Sverka: почти механическая точность.
- Memory: визуальная рифма с первым утром.

Камера должна становиться **спокойнее** по мере того, как герой понимает происходящее.

## Scene Builder prompt

```text
Build a complete Blender cinematic gray-box previs for a 28-second narrative game teaser called AWAKENING.

Scene settings: 24 fps, frames 1 through 672, 1920x1080, metric scale, Z-up.

The environment is one compact modern tram district called Mera with a tram stop, small public plaza, pedestrian crossing, traffic signal, tram line and surrounding low-rise urban buildings.

Create three named characters: HERO, LIDA and MARK.
HERO is an ordinary male municipal worker in muted blue-gray work clothing. Do not portray him as a superhero.
LIDA is a tram dispatcher.
MARK is a gardener.

Create named cameras:
CAM_01_ROUTINE
CAM_02_ANOMALY
CAM_03_FACE
CAM_04_CHOICE
CAM_05_MEETING
CAM_06_RESET
CAM_07_MEMORY
CAM_08_TITLE

SHOT 1 frames 1-96: Hero walks from approximately (4,-18,0) toward the tram stop. Camera tracks behind and slightly right from approximately (7.5,-22.5,2.4) to (5.5,-9.5,2.2), 35mm lens. Quiet early morning, routine and predictability.

SHOT 2 frames 97-192: Hero slows and stops near the tram stop while the camera continues briefly. A pedestrian repeats part of a step and a distant tram briefly occupies two subtly offset positions. Do not use digital glitch graphics.

SHOT 3 frames 193-288: 65mm medium close-up. Hero looks toward Lida and the failed signal. Very slow push-in. Recognition and curiosity, not fear.

SHOT 4 frames 289-408: Hero attempts to repair the signal. A tram approaches. Frame Hero, Lida, signal, tram direction and plaza in one readable composition. Hero stops repairing, puts away his tool and walks toward Lida. No game UI.

SHOT 5 frames 409-504: Mark enters the plaza. Lida delays departure and meets Mark. Start with Hero compositionally central, then move laterally so Lida and Mark become the primary composition while Hero becomes secondary.

SHOT 6 frames 505-576: High-angle night view. The district gradually resets to its stored routine state. Tram, signal and street objects return to original positions. No portals, explosions or supernatural energy. One small paper note at the stop remains unchanged.

SHOT 7 frames 577-648: Repeat almost exactly the visual language and camera motion of Shot 1. It is the next morning. Lida recognizes Mark. Hero walks in the background and subtly turns his head after noticing their recognition.

SHOT 8 frames 649-672: black title-card placeholder.

Camera behavior must be restrained, cinematic, grounded and human scale. Avoid dramatic action-camera movement. The camera should progressively become calmer as Hero understands the situation.

Use basic gray materials with only light color coding: Hero muted blue, Lida muted warm brown, Mark muted green, environment neutral gray, memory trace pale cyan.

Prioritize blocking, timing, eyelines, screen direction, spatial clarity and continuity over asset detail.

Do not create final detailed assets.
```

## AI self-review

После первой сборки выполнить отдельный проход только по постановке:

```text
Review the completed Blender previs as a cinematographer, film editor and gameplay narrative designer.

Do not add new story events.

Check:
1. Can the viewer understand the geography of the tram stop?
2. Is Hero's decision readable without UI?
3. Does Hero cause the opportunity for Lida and Mark to meet?
4. Does Shot 7 clearly visually echo Shot 1?
5. Is the city reset understandable without magical visual effects?
6. Does Lida recognizing Mark feel like the payoff?
7. Is Hero visually secondary during their meeting?
8. Are eyelines and screen direction consistent?
9. Are camera movements restrained?
10. Can the story be understood without dialogue?

Fix only blocking, framing, camera curves and timing. Do not increase visual detail.
```

## QA / go-no-go

До финальной видео-генерации:

- [ ] остановка и площадь читаются пространственно;
- [ ] Shot 07 очевидно рифмуется с Shot 01;
- [ ] решение героя понятно без UI;
- [ ] встреча Лиды и Марка причинно связана с действием героя;
- [ ] герой не выглядит супергероем;
- [ ] Сверка читается как reset, а не магия;
- [ ] persistent trace заметен, но не кричит;
- [ ] персонажи не телепортируются между планами;
- [ ] экранное направление не ломается;
- [ ] финальный payoff понятен без пояснения автора.

Полевой критерий:

Показать gray-box трём людям без объяснений. PASS, если минимум двое понимают примерно следующее:

> Город повторился, герой изменил ситуацию, а связь между двумя людьми почему-то сохранилась.

## Связь с gameplay vertical slice

Cinematic beat должен иметь игровой эквивалент:

```text
NOTICE
  ↓
INVESTIGATE
  ↓
CHOOSE
  ↓
ACT
  ↓
WITNESS
  ↓
ANCHOR
  ↓
SVERKA
  ↓
CONSEQUENCE
```

Previs не должен обещать механику, которую мы принципиально не собираемся реализовывать в первом playable fragment.

## Production pipeline

```text
CAUSE GRAPH
    ↓
SCENE BLOCKING
    ↓
BLENDER PREVIS
    ↓
CAMERA QA
    ↓
STORY QA
    ↓
HERO / WORLD REFERENCES
    ↓
FINAL VIDEO GENERATION
    ↓
EDIT
```

Цель этого документа — сделать дорогую генерацию **последним**, а не первым шагом.