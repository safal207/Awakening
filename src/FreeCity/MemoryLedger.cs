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
/// A shared-memory anchor record. It becomes Sverka-persistent only when the
/// linked event has at least one independent Accepted witness.
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
            if (pair.Key >= 0 && pair.Key != actorId && pair.Value == WitnessConsent.Accepted)
                return true;
        }
        return false;
    }

    internal bool HasIndependentWitnessRecord(int actorId)
    {
        foreach (var pair in _witnesses)
        {
            if (pair.Key >= 0 && pair.Key != actorId &&
                Enum.IsDefined(typeof(WitnessConsent), pair.Value))
                return true;
        }
        return false;
    }
}

/// <summary>
/// Minimal event/anchor store for M1. Stable IDs make event creation idempotent.
/// ApplySverka removes events that never acquired a valid shared-memory anchor.
/// Restore methods rebuild snapshots without dispatching gameplay actions/rewards.
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

    internal bool TryRestoreEvent(MemoryEvent memoryEvent)
    {
        if (!IsValidEvent(memoryEvent)) return false;
        if (_events.TryGetValue(memoryEvent.Id, out var existing))
            return existing == memoryEvent;
        _events.Add(memoryEvent.Id, memoryEvent);
        return true;
    }

    public bool TryCreateAnchor(string eventId, string traceId, int witnessNpcId, WitnessConsent consent)
    {
        if (string.IsNullOrWhiteSpace(eventId) || !_events.TryGetValue(eventId, out var memoryEvent)) return false;
        if (string.IsNullOrWhiteSpace(traceId)) return false;
        if (witnessNpcId < 0 || witnessNpcId == memoryEvent.ActorId) return false;
        if (consent != WitnessConsent.Accepted) return false;
        if (_anchors.ContainsKey(eventId)) return false;

        var anchor = new MemoryAnchor(eventId, traceId, memoryEvent.Day);
        anchor.SetWitnessConsent(witnessNpcId, consent);

        if (!anchor.HasAcceptedWitness(memoryEvent.ActorId)) return false;
        _anchors.Add(eventId, anchor);
        return true;
    }

    internal bool TryRestoreAnchor(MemoryAnchor anchor)
    {
        if (anchor == null || string.IsNullOrWhiteSpace(anchor.EventId) ||
            string.IsNullOrWhiteSpace(anchor.TraceId) ||
            !_events.TryGetValue(anchor.EventId, out var memoryEvent) ||
            anchor.CreatedDay < memoryEvent.Day ||
            !anchor.HasIndependentWitnessRecord(memoryEvent.ActorId))
            return false;

        var copy = new MemoryAnchor(anchor.EventId, anchor.TraceId, anchor.CreatedDay);
        foreach (var pair in anchor.Witnesses)
        {
            if (pair.Key < 0 || pair.Key == memoryEvent.ActorId ||
                !Enum.IsDefined(typeof(WitnessConsent), pair.Value))
                return false;
            copy.SetWitnessConsent(pair.Key, pair.Value);
        }

        if (_anchors.TryGetValue(copy.EventId, out var existing))
            return AnchorsEquivalent(existing, copy);

        _anchors.Add(copy.EventId, copy);
        return true;
    }

    public bool IsPersisted(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId) || !_events.TryGetValue(eventId, out var memoryEvent)) return false;
        return _anchors.TryGetValue(eventId, out var anchor) &&
               anchor.HasAcceptedWitness(memoryEvent.ActorId);
    }

    public bool TryGetEvent(string eventId, out MemoryEvent? memoryEvent)
    {
        memoryEvent = null;
        if (string.IsNullOrWhiteSpace(eventId)) return false;
        bool found = _events.TryGetValue(eventId, out var value);
        memoryEvent = value;
        return found;
    }

    public bool TryGetAnchor(string eventId, out MemoryAnchor? anchor)
    {
        anchor = null;
        if (string.IsNullOrWhiteSpace(eventId)) return false;
        bool found = _anchors.TryGetValue(eventId, out var value);
        anchor = value;
        return found;
    }

    public int ApplySverka()
    {
        var forgotten = new List<string>();
        foreach (string id in _events.Keys)
            if (!IsPersisted(id)) forgotten.Add(id);

        foreach (string id in forgotten)
        {
            _events.Remove(id);
            _anchors.Remove(id);
        }

        return forgotten.Count;
    }

    private static bool AnchorsEquivalent(MemoryAnchor left, MemoryAnchor right)
    {
        if (!string.Equals(left.EventId, right.EventId, StringComparison.Ordinal) ||
            !string.Equals(left.TraceId, right.TraceId, StringComparison.Ordinal) ||
            left.CreatedDay != right.CreatedDay ||
            left.Witnesses.Count != right.Witnesses.Count)
            return false;

        foreach (var pair in left.Witnesses)
            if (!right.Witnesses.TryGetValue(pair.Key, out var consent) || consent != pair.Value)
                return false;
        return true;
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
