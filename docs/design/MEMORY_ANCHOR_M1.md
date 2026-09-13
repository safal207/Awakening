# Memory Anchor M1 — bounded contract

## Why this exists

The current prototype tracks hero qualities and awareness, but the new Awakening loop needs a durable answer to a more important question:

> What happened, why did it happen, who witnessed it voluntarily, and what survived Sverka?

The integration branch uses the single contract from PRs #18-#22, with input
validation carried over from PR #15. Memory is connected to save/load and the
day transition; the first Lida/Mark episode is currently dialogue-level.

## Event contract

A `MemoryEvent` records:

- stable event ID;
- in-game day;
- event kind;
- location ID;
- participant IDs (stable within the generated world);
- the concrete choice that caused the event;
- a concrete consequence and whether an alternative choice existed.

The event is not automatically persistent just because it occurred.

## Anchor contract

A candidate `MemoryAnchor` references an existing event and is registered once.
Sverka preserves it only when all of the following are true:

1. the event already exists;
2. the event includes a concrete choice;
3. a trace exists (`TraceId`);
4. a witness is a participant other than the hero;
5. that witness understood the event and voluntarily accepted preservation;
6. the same event is not anchored twice.

Declined or unknown consent leaves a candidate that Sverka rejects with an
explicit reason. Persisted and rejected decisions are final and are not replayed.

## Initial invariants

```text
EVENT != ANCHOR

anchor(event) => event exists
persisted(event) => trace exists
persisted(event) => informed voluntary participant witness exists
accepted witness != hero
anchor(event) is idempotent
```

This is the first implementation of the design principle from `docs/CONCEPT.md`:

> Event + Witness + Trace + Choice.

## Explicit non-goals

Still outside the current slice:

- physically stage the tram delay, repair and meeting;
- resolve conflicting witnesses;
- replicate events over a network;
- generate prose with an LLM;
- provide a general quest system.

Save schema 2 carries events, anchor decisions and claimed dialogue reward IDs.
Story replies use explicit IDs; legacy generic replies use a hash of their text
scoped to a stable NPC ID. Editing a legacy reply's text creates a new reward
identity, so new content should supply an explicit RewardId. Legacy saves load
with an empty reward history; rewards from before schema 2 cannot be reconstructed.

## First intended vertical-slice event

```text
EventId: district1.lida-mark.meeting
EventType: shared_meeting
LocationId: tram-depot-square
ParticipantIds: hero, Lida, Mark
ChoiceId: district1.lida.delay
TraceId: dispatcher-delay-note
Witness: Mark, UnderstoodEvent + Consented
```

The gameplay payoff is not the anchor record itself. The payoff is the next morning: a character can act differently because the game can point to the persisted cause.
