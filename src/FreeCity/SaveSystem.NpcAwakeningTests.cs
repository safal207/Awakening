using System;
using System.Collections.Generic;
using System.IO;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

public static partial class SaveSystem
{
    private static bool RunNpcAwakeningPersistenceSelfTest(out string message)
    {
        var paths = new List<string>();
        try
        {
            CheckExplicitAwakeningThreshold();
            CheckAwakeningSurvivesTwentyResets();
            CheckAwakeningSaveLoadRoundTrip(paths);
            CheckLegacyAwareStateMigrates();
            CheckOrdinaryNpcStillResets();
            message = "NPC awakening persistence self-test passed (5 cases; 20 resets).";
            return true;
        }
        catch (Exception e)
        {
            message = "NPC awakening persistence self-test failed: " + e.Message;
            return false;
        }
        finally
        {
            foreach (string path in paths)
            {
                try { if (File.Exists(path)) File.Delete(path); } catch { }
                try
                {
                    string backup = BackupPathFor(path);
                    if (File.Exists(backup)) File.Delete(backup);
                }
                catch { }
            }
        }
    }

    private static void CheckExplicitAwakeningThreshold()
    {
        var npc = new NpcCharacter(Vector3.Zero, Vector3.Zero, 9101)
        {
            Awareness = 99f,
        };
        var progress = new HeroProgress();
        var meaningful = new DialogueChoice(
            "notice",
            0, 0,
            MemoryGain: 1,
            CuriosityGain: 0,
            EmpathyGain: 0,
            AgencyGain: 0,
            CourageGain: 0);

        npc.ApplyChoice(meaningful, progress);
        RequireNpcAwakening(npc.IsAwakened, "own awareness reaching 100 must explicitly awaken the NPC");
        RequireNpcAwakening(npc.State == NpcState.Aware && Math.Abs(npc.Awareness - 100f) < 0.001f,
            "Awaken must expose compatible Aware state and awareness=100");
    }

    private static void CheckAwakeningSurvivesTwentyResets()
    {
        var npc = new NpcCharacter(new Vector3(4, 0, 7), new Vector3(8, 0, 9), 9102);
        npc.Awaken();

        for (int day = 0; day < 20; day++)
        {
            npc.Position += new Vector3(1f, 0f, -0.5f);
            npc.Reset();
            RequireNpcAwakening(npc.IsAwakened, $"awakening lost on reset {day + 1}");
            RequireNpcAwakening(npc.State == NpcState.Aware && Math.Abs(npc.Awareness - 100f) < 0.001f,
                $"aware compatibility state lost on reset {day + 1}");
            RequireNpcAwakening(npc.Position == npc.HomePos,
                $"daily reset {day + 1} must still reset location even for awakened NPC");
        }

        string line = npc.GetDialogueState(0f, new HeroProgress()).npcLine;
        RequireNpcAwakening(IsAwareDialogue(line), "awakened NPC must keep aware dialogue after 20 resets");
    }

    private static void CheckAwakeningSaveLoadRoundTrip(List<string> paths)
    {
        string path = TempSavePath(paths, "npc-awakening-roundtrip");
        const int seed = 424242;
        var progress = new HeroProgress();
        var city = new CityRenderer(seed, progress);
        ResidentIdentity.BindCity(city.Npcs);
        NpcCharacter target = city.Npcs[7];
        target.Awaken();

        SaveToPath(path, seed, progress, new AwarenessSystem(), 10f, DateTime.UtcNow, city.Npcs);
        var loaded = LoadFromPath(path);
        RequireNpcAwakening(loaded.npcs != null && loaded.npcs.Count == city.Npcs.Count,
            "NPC rows must survive save/load");
        RequireNpcAwakening(loaded.npcs![7].PersistentId == 7 && loaded.npcs[7].IsAwakened == true,
            "modern save row must keep stable identity and explicit awakening flag");

        var restoredCity = new CityRenderer(seed, loaded.progress);
        ResidentIdentity.BindCity(restoredCity.Npcs);
        NpcAwakeningPersistence.RegisterCity(restoredCity);
        NpcCharacter restored = restoredCity.Npcs[7];
        RequireNpcAwakening(restored.IsAwakened && restored.State == NpcState.Aware,
            "Save/Load must restore awakening to the same PersistentId");
        RequireNpcAwakening(Math.Abs(restored.Awareness - 100f) < 0.001f,
            "Save/Load must restore permanent awareness=100");

        restored.Reset();
        RequireNpcAwakening(restored.IsAwakened && restored.State == NpcState.Aware,
            "first Sverka after reload must not erase awakening");
    }

    private static void CheckLegacyAwareStateMigrates()
    {
        var legacy = new NpcSaveData
        {
            PersistentId = null,
            IsAwakened = null,
            Awareness = 100f,
            State = nameof(NpcState.Aware),
        };
        var npc = new NpcCharacter(Vector3.Zero, Vector3.Zero, 9103);

        NpcAwakeningPersistence.Apply(
            new[] { npc },
            new[] { legacy });

        RequireNpcAwakening(npc.IsAwakened && npc.State == NpcState.Aware,
            "legacy State=Aware must migrate to permanent awakening without a new flag");
        npc.Reset();
        RequireNpcAwakening(npc.IsAwakened,
            "migrated legacy awakening must survive subsequent reset");
    }

    private static void CheckOrdinaryNpcStillResets()
    {
        var npc = new NpcCharacter(new Vector3(3, 0, 3), new Vector3(8, 0, 8), 9104)
        {
            Awareness = 73f,
            State = NpcState.Working,
            Position = new Vector3(6, 0, 6),
        };

        npc.Reset();
        RequireNpcAwakening(!npc.IsAwakened, "ordinary NPC must not become awakened because of reset");
        RequireNpcAwakening(npc.State == NpcState.Walking && Math.Abs(npc.Awareness) < 0.001f,
            "ordinary NPC reset behavior must remain unchanged");
        RequireNpcAwakening(npc.Position == npc.HomePos,
            "ordinary NPC must still return home on reset");
    }

    private static bool IsAwareDialogue(string line) =>
        line.Contains("помню", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("петля", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("выбирать", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("Разбуди", StringComparison.OrdinalIgnoreCase);

    private static void RequireNpcAwakening(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
