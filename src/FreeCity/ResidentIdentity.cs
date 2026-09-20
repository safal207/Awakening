using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Probuzhdenie.FreeCity;

/// <summary>
/// Separates process-local object ids from persistent resident identity.
/// A save already owns one world seed, so a resident's deterministic city slot
/// is sufficient as its stable id inside that world.
/// </summary>
public static class ResidentIdentity
{
    private sealed class Box
    {
        public int Value;
    }

    private static readonly ConditionalWeakTable<NpcCharacter, Box> PersistentIds = new();

    public static void BindCity(IReadOnlyList<NpcCharacter> residents)
    {
        if (residents == null) throw new ArgumentNullException(nameof(residents));

        for (int i = 0; i < residents.Count; i++)
        {
            NpcCharacter resident = residents[i];
            PersistentIds.Remove(resident);
            PersistentIds.Add(resident, new Box { Value = i });
        }
    }

    public static int GetPersistentId(NpcCharacter resident)
    {
        if (resident == null) throw new ArgumentNullException(nameof(resident));
        return PersistentIds.TryGetValue(resident, out Box? box) ? box.Value : resident.Id;
    }

    public static bool TryGetPersistentId(NpcCharacter resident, out int persistentId)
    {
        if (resident != null && PersistentIds.TryGetValue(resident, out Box? box))
        {
            persistentId = box.Value;
            return true;
        }

        persistentId = -1;
        return false;
    }
}
