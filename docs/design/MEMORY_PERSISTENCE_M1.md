# M1: Memory persistence boundary

Цель этого слоя — сохранить причинное событие и его MemoryAnchor через обычный save/load и следующий игровой день без повторного выполнения выбора или награды.

## Инварианты

- `MemoryEvent` хранит stable ID, день, место и исходный `ChoiceId`.
- `MemoryAnchor` хранит trace и состояния свидетелей.
- Загрузка восстанавливает snapshot как данные; она не вызывает `ApplyChoice` и не начисляет качества.
- Только независимый `Accepted` witness делает событие устойчивым к Сверке.
- `Declined` / `Unknown` не становятся persisted memory.
- Старый save без ledger загружается с пустой памятью.
- Повреждённая строка не должна ломать загрузку остальных валидных строк.
- При частичном восстановлении или неизвестной будущей версии автоматическая перезапись исходного save блокируется.

## Acceptance scenario

```text
Day 2
  first_memory_lida_mark_meeting
  choice = leave_signal_repair_to_help_lida
  location = tram_plaza
  witness = Accepted
  trace = dispatcher_note

Save → Load → NewDay

Expected:
  exactly one event
  exactly one anchor
  no repeated reward
  original choice/location preserved
  event survives Sverka
```

## Не входит

- networking;
- облачная память;
- AI-generated dialogue;
- полный городской reset;
- доказательство того, что NPC фактически видел событие — это gameplay responsibility.
