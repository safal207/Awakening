# Memory Anchor M1 — bounded contract

## Why this exists

The current prototype tracks hero qualities and awareness, but the new Awakening loop needs a durable answer to a more important question:

> What happened, why did it happen, who witnessed it voluntarily, and what survived Sverka?

M1 starts with a deliberately small data contract before changing gameplay or save files.

## Event contract

A `MemoryEvent` records:

- stable event ID;
- in-game day;
- event kind;
- location ID;
- actor ID;
- the concrete choice that caused the event;
- a short factual description.

The event is not automatically persistent just because it occurred.

## Anchor contract

A `MemoryAnchor` requires all of the following:

1. the event already exists;
2. the event includes a concrete choice;
3. a trace exists (`TraceId`);
4. a witness is someone other than the actor;
5. that witness explicitly accepted preservation;
6. the same event is not anchored twice.

Declined or unknown consent does not create an anchor.

## Initial invariants

```text
EVENT != ANCHOR

anchor(event) => event exists
anchor(event) => trace exists
anchor(event) => accepted witness exists
accepted witness != event actor
anchor(event) is idempotent
```

This is the first implementation of the design principle from `docs/CONCEPT.md`:

> Event + Witness + Trace + Choice.

## Explicit non-goals

M1 does not yet:

- write anchors into the save format;
- reset the world through Sverka;
- reward qualities;
- resolve conflicting witnesses;
- replicate events over a network;
- generate prose with an LLM;
- replace existing dialogue logic.

Those changes should follow only after the data contract is covered by functional tests.

## First intended vertical-slice event

```text
Id: first_memory_lida_mark_meeting
Kind: shared_meeting
LocationId: tram_plaza
ActorId: hero
ChoiceId: leave_signal_repair_to_help_lida
TraceId: dispatcher_note
Witness: Lida or Mark, explicit Accepted
```

The gameplay payoff is not the anchor record itself. The payoff is the next morning: a character can act differently because the game can point to the persisted cause.
