using System;
using System.Collections.Generic;
using System.Linq;

namespace Probuzhdenie.FreeCity;

public sealed class MemoryLedger
{
    private readonly Dictionary<string, MemoryEvent> _events = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MemoryAnchor> _anchors = new(StringComparer.Ordinal);

    public IReadOnlyCollection<MemoryEvent> Events => _events.Values;
    public IReadOnlyCollection<MemoryAnchor> Anchors => _anchors.Values;

    public bool RegisterEvent(MemoryEvent memoryEvent)
    {
        if (memoryEvent == null || string.IsNullOrWhiteSpace(memoryEvent.EventId) ||
            memoryEvent.Day < 1 || string.IsNullOrWhiteSpace(memoryEvent.EventType) ||
            string.IsNullOrWhiteSpace(memoryEvent.LocationId) || string.IsNullOrWhiteSpace(memoryEvent.ChoiceId) ||
            memoryEvent.ParticipantIds == null || memoryEvent.ParticipantIds.Count == 0 ||
            memoryEvent.ParticipantIds.Any(id => id < 0)) return false;
        return _events.TryAdd(memoryEvent.EventId, memoryEvent);
    }

    public bool RegisterAnchor(MemoryAnchor anchor)
    {
        if (anchor == null || string.IsNullOrWhiteSpace(anchor.EventId) ||
            !_events.ContainsKey(anchor.EventId) || anchor.Witnesses == null ||
            anchor.Witnesses.Any(w => w == null)) return false;
        return _anchors.TryAdd(anchor.EventId, anchor);
    }

    public MemoryEvent? FindEvent(string eventId) =>
        _events.TryGetValue(eventId, out var memoryEvent) ? memoryEvent : null;

    public MemoryAnchor? FindAnchor(string eventId) =>
        _anchors.TryGetValue(eventId, out var anchor) ? anchor : null;

    public bool ResolveForSverka(string eventId, int heroId, out string reason)
    {
        reason = "missing_event_or_anchor";
        if (!_events.TryGetValue(eventId, out var memoryEvent)) return false;
        if (!_anchors.TryGetValue(eventId, out var anchor)) return false;
        bool persisted = anchor.Evaluate(memoryEvent, heroId);
        reason = anchor.DecisionReason;
        return persisted;
    }

    public bool HasPersisted(string eventId) =>
        _anchors.TryGetValue(eventId, out var anchor) && anchor.PersistedAcrossSverka;

    public void Restore(IEnumerable<MemoryEvent>? events, IEnumerable<MemoryAnchor>? anchors)
    {
        _events.Clear();
        _anchors.Clear();
        if (events != null)
            foreach (var memoryEvent in events) RegisterEvent(memoryEvent);
        if (anchors != null)
            foreach (var anchor in anchors) RegisterAnchor(anchor);
    }
}
