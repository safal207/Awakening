using System.Collections.Generic;

namespace Probuzhdenie.FreeCity;

public sealed class MemoryAnchorSaveData
{
    public string EventId { get; set; } = "";
    public string TraceId { get; set; } = "";
    public List<MemoryWitness> Witnesses { get; set; } = new();
    public MemoryAnchorStatus Status { get; set; }
    public string DecisionReason { get; set; } = "";

    public static MemoryAnchorSaveData From(MemoryAnchor anchor) => new()
    {
        EventId = anchor.EventId,
        TraceId = anchor.TraceId,
        Witnesses = new List<MemoryWitness>(anchor.Witnesses),
        Status = anchor.Status,
        DecisionReason = anchor.DecisionReason,
    };

    public MemoryAnchor ToAnchor()
    {
        var anchor = new MemoryAnchor
        {
            EventId = EventId,
            TraceId = TraceId,
            Witnesses = Witnesses ?? new List<MemoryWitness>(),
        };
        anchor.RestoreDecision(Status, DecisionReason);
        return anchor;
    }
}
