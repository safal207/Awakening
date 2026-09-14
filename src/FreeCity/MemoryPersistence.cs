using System;
using System.Collections.Generic;

namespace Probuzhdenie.FreeCity;

internal sealed class MemoryPersistenceSnapshot
{
    public List<MemoryEventSaveData> Events { get; set; } = new();
    public List<MemoryAnchorSaveData> Anchors { get; set; } = new();
}

internal sealed class MemoryEventSaveData
{
    public string Id { get; set; } = "";
    public int Day { get; set; }
    public string Kind { get; set; } = "";
    public string LocationId { get; set; } = "";
    public int ActorId { get; set; }
    public string ChoiceId { get; set; } = "";
    public string Description { get; set; } = "";
}

internal sealed class MemoryAnchorSaveData
{
    public string EventId { get; set; } = "";
    public string TraceId { get; set; } = "";
    public int CreatedDay { get; set; }
    public List<MemoryWitnessSaveData> Witnesses { get; set; } = new();
}

internal sealed class MemoryWitnessSaveData
{
    public int NpcId { get; set; }
    public string Consent { get; set; } = "";
}

internal static class MemoryPersistence
{
    public static MemoryPersistenceSnapshot Capture(MemoryLedger ledger)
    {
        var snapshot = new MemoryPersistenceSnapshot();

        foreach (var memoryEvent in ledger.Events.Values)
        {
            snapshot.Events.Add(new MemoryEventSaveData
            {
                Id = memoryEvent.Id,
                Day = memoryEvent.Day,
                Kind = memoryEvent.Kind,
                LocationId = memoryEvent.LocationId,
                ActorId = memoryEvent.ActorId,
                ChoiceId = memoryEvent.ChoiceId,
                Description = memoryEvent.Description,
            });
        }

        foreach (var anchor in ledger.Anchors.Values)
        {
            var row = new MemoryAnchorSaveData
            {
                EventId = anchor.EventId,
                TraceId = anchor.TraceId,
                CreatedDay = anchor.CreatedDay,
            };
            foreach (var witness in anchor.Witnesses)
            {
                row.Witnesses.Add(new MemoryWitnessSaveData
                {
                    NpcId = witness.Key,
                    Consent = witness.Value.ToString(),
                });
            }
            snapshot.Anchors.Add(row);
        }

        return snapshot;
    }

    /// <summary>
    /// Restores a snapshot without executing choices or rewards. Valid rows are
    /// retained even if another row is corrupt; false means the source should
    /// not be overwritten automatically until the player can recover it.
    /// </summary>
    public static bool TryRestore(MemoryPersistenceSnapshot? snapshot, out MemoryLedger ledger)
    {
        ledger = new MemoryLedger();
        if (snapshot == null) return true;

        bool clean = true;
        foreach (var row in snapshot.Events ?? new())
        {
            if (row == null)
            {
                clean = false;
                continue;
            }

            var memoryEvent = new MemoryEvent(
                row.Id ?? "",
                row.Day,
                row.Kind ?? "",
                row.LocationId ?? "",
                row.ActorId,
                row.ChoiceId ?? "",
                row.Description ?? "");

            if (!ledger.TryRestoreEvent(memoryEvent)) clean = false;
        }

        foreach (var row in snapshot.Anchors ?? new())
        {
            if (row == null || string.IsNullOrWhiteSpace(row.EventId) ||
                string.IsNullOrWhiteSpace(row.TraceId))
            {
                clean = false;
                continue;
            }

            var anchor = new MemoryAnchor(row.EventId, row.TraceId, row.CreatedDay);
            bool validWitnesses = true;
            foreach (var witness in row.Witnesses ?? new())
            {
                if (witness == null || witness.NpcId < 0 ||
                    !Enum.TryParse<WitnessConsent>(witness.Consent, ignoreCase: true, out var consent) ||
                    !Enum.IsDefined(typeof(WitnessConsent), consent))
                {
                    validWitnesses = false;
                    break;
                }
                anchor.SetWitnessConsent(witness.NpcId, consent);
            }

            if (!validWitnesses || !ledger.TryRestoreAnchor(anchor)) clean = false;
        }

        return clean;
    }
}
