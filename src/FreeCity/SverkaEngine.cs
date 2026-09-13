using System.Collections.Generic;
using System.Linq;

namespace Probuzhdenie.FreeCity;

public sealed record SverkaDecision(string EventId, bool Persisted, string Reason);

public sealed class SverkaReport
{
    public int Day { get; init; }
    public List<SverkaDecision> Decisions { get; init; } = new();
    public int PersistedCount => Decisions.Count(d => d.Persisted);
    public int RejectedCount => Decisions.Count(d => !d.Persisted);
}

public static class SverkaEngine
{
    public static SverkaReport Run(MemoryLedger ledger, int heroId, int day)
    {
        var eventsById = ledger.Events.ToDictionary(e => e.EventId, e => e);
        var report = new SverkaReport { Day = day };

        foreach (var anchor in ledger.Anchors.Where(a => a.Status == MemoryAnchorStatus.Candidate).ToList())
        {
            if (!eventsById.TryGetValue(anchor.EventId, out var memoryEvent)) continue;
            if (memoryEvent.Day > day) continue;

            bool persisted = ledger.ResolveForSverka(anchor.EventId, heroId, out string reason);
            report.Decisions.Add(new SverkaDecision(anchor.EventId, persisted, reason));
        }

        return report;
    }
}
