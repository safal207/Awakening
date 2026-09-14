# Memory Ledger verification plan

The M1 memory ledger is accepted only when the following invariants are executable and green:

- valid events are recorded once;
- duplicate event IDs are idempotent;
- an event alone is not persisted;
- the actor cannot witness their own event;
- unknown consent cannot create an anchor;
- declined consent cannot create an anchor;
- a trace is required;
- the referenced event must exist;
- an accepted independent witness can create an anchor;
- an anchored event is persisted;
- the same event cannot be anchored twice;
- the trace and accepted witness remain retrievable;
- malformed events are rejected.

The executable checks live in `src/FreeCity/MemoryLedgerSelfTest.cs`.

Target command once wired into `Program.cs`:

```bash
dotnet run --configuration Release --no-build -- --memory-test
```

Until that command is wired and executed in CI, the M1 memory ledger should be treated as **implemented but unverified**.
