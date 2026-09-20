# Local playtest trace

The trace is **opt-in and local-only**. It is enabled only by
`--playtest-trace <path>` (the packaged `START_PLAYTEST.bat` supplies this flag).

Each line is one JSON object with only:

- `elapsed_ms` — milliseconds since trace start;
- `event_type` — bounded event ID;
- `day` — current game day when available;
- `action_id` — predefined dialogue action ID when available;
- `value` — predefined finding/role/status value when needed.

Recorded gameplay event IDs:
`session_start`, `journal_open`, `journal_close`, `finding_discovered`,
`dialogue_choice`, `end_day_requested`, `morning_started`, `session_end`.

Process/test markers such as `trace_started`, `trace_closed`,
`functional_test_exit` and `self_test_exit` may also appear.

Not recorded:
- player name, email or account identity;
- OS username, hostname, IP or device identifiers;
- raw dialogue text or free-form player text;
- keyboard/mouse event stream;
- player/world coordinates;
- screenshots, audio or video.

The game never uploads the file. The observer manually keeps it with the
corresponding P1…P5 session result.
