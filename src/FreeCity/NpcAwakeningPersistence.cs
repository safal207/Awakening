using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Probuzhdenie.FreeCity;

/// <summary>
/// Applies persistent NPC awakening only after the deterministic city resident
/// list exists. Modern rows match by PersistentId; legacy rows fall back to the
/// historical positional order and infer awakening only from State=Aware.
/// </summary>
public static class NpcAwakeningPersistence
{
    private sealed class PendingRows
    {
        public required List<SaveSystem.NpcSaveData> Rows;
    }

    private static readonly ConditionalWeakTable<HeroProgress, PendingRows> PendingByProgress = new();

    public static void SetPending(HeroProgress progress, List<SaveSystem.NpcSaveData>? rows)
    {
        PendingByProgress.Remove(progress);
        if (rows != null)
            PendingByProgress.Add(progress, new PendingRows { Rows = rows });
    }

    public static void RegisterCity(CityRenderer city)
    {
        if (!PendingByProgress.TryGetValue(city.Progress, out PendingRows? pending))
            return;

        PendingByProgress.Remove(city.Progress);
        Apply(city.Npcs, pending.Rows);
    }

    public static void Apply(IReadOnlyList<NpcCharacter> residents, IReadOnlyList<SaveSystem.NpcSaveData> rows)
    {
        if (residents.Count == 0 || rows.Count == 0) return;

        var byPersistentId = new Dictionary<int, NpcCharacter>();
        for (int i = 0; i < residents.Count; i++)
        {
            int persistentId = ResidentIdentity.GetPersistentId(residents[i]);
            if (persistentId >= 0)
                byPersistentId[persistentId] = residents[i];
        }

        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            SaveSystem.NpcSaveData row = rows[rowIndex];
            NpcCharacter? npc = null;

            if (row.PersistentId is int persistentId)
                byPersistentId.TryGetValue(persistentId, out npc);
            else if (rowIndex < residents.Count)
                npc = residents[rowIndex];

            if (npc == null) continue;

            NpcState state = Enum.TryParse(row.State, out NpcState parsed)
                ? parsed
                : NpcState.Walking;
            bool awakened = row.IsAwakened ?? state == NpcState.Aware;
            npc.RestorePersistentState(awakened, row.Awareness, state);
        }
    }
}
