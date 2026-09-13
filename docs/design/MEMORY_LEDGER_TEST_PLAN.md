# Memory Ledger verification plan

The M1 memory ledger is accepted only when the following invariants are executable and green:

- valid events are recorded once;
- duplicate event IDs are idempotent;
- an event alone is not persisted;
- the actor cannot witness their own event;
- unknown consent cannot produce a persisted anchor;
- declined consent produces a final rejected decision;
- a trace is required;
- the referenced event must exist;
- only an informed, consenting participant other than the hero can preserve an anchor;
- candidates remain unpersisted until Sverka;
- the same event cannot be anchored twice;
- the trace and accepted witness remain retrievable;
- malformed events are rejected.

Core checks live in `src/FreeCity/MemoryLedgerSelfTest.cs` and
`MemoryAnchorTests.cs`. The functional command also runs Sverka, story,
integration and save/load regressions: both outcomes, 20 mornings, replayed
dialogue rewards, stable NPC roles, no passive awareness, legacy/newer saves.

Available command (also configured in CI):

```bash
dotnet run --configuration Release --no-build -- --memory-test
```

Use `--functional-test` for the complete integrated suite and `--self-test` for
the original save check. These are headless checks, not visual or Unreal QA.
