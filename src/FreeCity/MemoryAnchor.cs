using System;
using System.Collections.Generic;
using System.Linq;

namespace Probuzhdenie.FreeCity;

public enum MemoryAnchorStatus
{
    Candidate,
    Persisted,
    Rejected,
}

public sealed class MemoryAnchor
{
    public string EventId { get; set; } = "";
    public string TraceId { get; set; } = "";
    public List<MemoryWitness> Witnesses { get; set; } = new();
    public MemoryAnchorStatus Status { get; private set; } = MemoryAnchorStatus.Candidate;
    public string DecisionReason { get; private set; } = "not_evaluated";
    public bool PersistedAcrossSverka => Status == MemoryAnchorStatus.Persisted;

    public bool Evaluate(MemoryEvent memoryEvent, int heroId)
    {
        if (!string.Equals(EventId, memoryEvent.EventId, StringComparison.Ordinal)) return Reject("event_mismatch");
        if (string.IsNullOrWhiteSpace(memoryEvent.Consequence)) return Reject("event_has_no_consequence");
        if (!memoryEvent.HadAlternativeChoice) return Reject("no_meaningful_choice");
        if (string.IsNullOrWhiteSpace(TraceId)) return Reject("missing_trace");

        bool validWitness = Witnesses.Any(w =>
            w.NpcId != heroId && memoryEvent.ParticipantIds.Contains(w.NpcId) && w.UnderstoodEvent && w.Consented);
        if (!validWitness) return Reject("missing_voluntary_participant_witness");

        Status = MemoryAnchorStatus.Persisted;
        DecisionReason = "event+participant+witness+understanding+consent+trace+choice";
        return true;
    }

    private bool Reject(string reason)
    {
        Status = MemoryAnchorStatus.Rejected;
        DecisionReason = reason;
        return false;
    }
}
