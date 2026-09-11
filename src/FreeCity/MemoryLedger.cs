using System;
using System.Collections.Generic;

namespace Probuzhdenie.FreeCity;

/// <summary>
/// One concrete event the game can later reason about.
/// This is intentionally small: it records what happened, where, when and which
/// player choice caused it without trying to become a general quest system.
/// </summary>
public sealed record MemoryEvent(
    string Id,
    int Day,
    string Kind,
    string LocationId,
    int ActorId,
    string ChoiceId,
    string Description);

public enum WitnessConsent
{
    Unknown,
    Accepted,
    Declined,
}

/// <summary>
/// A persisted shared-memory anchor. An anchor is valid only when a concrete
/// event, a trace and at least one voluntary witness are present.
/// </summary>
public sealed class MemoryAnchor
{
    private readonly Dictionary<int, WitnessConsent> _witnesses = new();

    public string EventId { get; }
    public string TraceId { get; }
    public int CreatedDay { get; }
    public IReadOnlyDictionary<int, WitnessConsent> Witnesses => _witnesses;

    public MemoryAnchor(string eventId, string traceId, int createdDay)
    {
        EventId = eventId;
        TraceId = traceId;
        CreatedDay = Math.Max(1, createdDay);
    }

    public void SetWitnessConsent(int npcId, WitnessConsent consent)
    {
        _witnesses[npcId] = consent;
    }

    public bool HasAcceptedWitness(int actorId)
    {
        foreach (var pair in _witnesses)
        {
            if (pair.Key != actorId && pair.Value == WitnessConsent.Accepted)
                return true;
        }
        return false;
    }
}

/// <summary>
/// Minimal event/anchor store for M1. It provides stable IDs and idempotency so
/// the same event cannot be rewarded or anchored twice by repeated input/load.
/// It is not yet wired into save/load or Sverka; that comes after the data
/// contract and invariants are proven.
/// </summary>
public sealed class MemoryLedger
{
    private readonly Dictionary<string, MemoryEvent> _events = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MemoryAnchor> _anchors = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, MemoryEvent> Events => _events;
    public IReadOnlyDictionary<string, MemoryAnchor> Anchors => _anchors;

    public bool TryRecordEvent(MemoryEvent memoryEvent)
    {
        if (!IsValidEvent(memoryEvent)) return false;
        return _events.TryAdd(memoryEvent.Id, memoryEvent);
    }

    public bool TryCreateAnchor(string eventId, string traceId, int witnessNpcId, WitnessConsent consent)
    {
        if (!_events.TryGetValue(eventId, out var memoryEvent)) return false;
        if (string.IsNullOrWhiteSpace(traceId)) return false;
        if (witnessNpcId == memoryEvent.ActorId) return false;
        if (consent != WitnessConsent.Accepted) return false;
        if (_anchors.ContainsKey(eventId)) return false;

        var anchor = new MemoryAnchor(eventId, traceId, memoryEvent.Day);
        anchor.SetWitnessConsent(witnessNpcId, consent);

        if (!anchor.HasAcceptedWitness(memoryEvent.ActorId)) return false;
        _anchors.Add(eventId, anchor);
        return true;
    }

    public bool IsPersisted(string eventId)
    {
        if (!_events.TryGetValue(eventId, out var memoryEvent)) return false;
        return _anchors.TryGetValue(eventId, out var anchor) &&
               anchor.HasAcceptedWitness(memoryEvent.ActorId);
    }

    public bool TryGetEvent(string eventId, out MemoryEvent? memoryEvent)
    {
        bool found = _events.TryGetValue(eventId, out var value);
        memoryEvent = value;
        return found;
    }

    public bool TryGetAnchor(string eventId, out MemoryAnchor? anchor)
    {
        bool found = _anchors.TryGetValue(eventId, out var value);
        anchor = value;
        return found;
    }

    private static bool IsValidEvent(MemoryEvent memoryEvent)
    {
        return memoryEvent != null &&
               !string.IsNullOrWhiteSpace(memoryEvent.Id) &&
               memoryEvent.Day >= 1 &&
               !string.IsNullOrWhiteSpace(memoryEvent.Kind) &&
               !string.IsNullOrWhiteSpace(memoryEvent.LocationId) &&
               !string.IsNullOrWhiteSpace(memoryEvent.ChoiceId) &&
               !string.IsNullOrWhiteSpace(memoryEvent.Description);
    }
}
