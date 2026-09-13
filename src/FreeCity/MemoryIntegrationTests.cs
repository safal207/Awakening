using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

public static class MemoryIntegrationTests
{
    public static bool Run(out string message)
    {
        var failures = new List<string>();
        void Check(bool ok, string name) { if (!ok) failures.Add(name); }
        MemoryRuntime.Reset();
        try
        {
            for (int world = 0; world < 3; world++)
            {
                var city = new CityRenderer(424242);
                Check(city.Npcs.Select(n => n.Id).SequenceEqual(Enumerable.Range(0, 50)), "stable world IDs");
                Check(city.Npcs[1].Name == FirstDistrictStory.LidaName && city.Npcs[2].Name == FirstDistrictStory.MarkName &&
                    city.Npcs[3].Name == FirstDistrictStory.NikaName, "roles after world recreation");
            }

            foreach (float startingAwareness in new[] { 0f, 20f, 60f, 99f })
            {
                var awareness = new AwarenessSystem();
                awareness.Restore(startingAwareness);
                var player = new NpcCharacter(Vector3.Zero, Vector3.Zero, 123, id: 0);
                for (int second = 0; second < 1800; second++) awareness.Update(player, 13f, 1f);
                Check(awareness.Level == startingAwareness && player.State != NpcState.Aware, "waiting never grants awareness");
            }

            var progress = new HeroProgress();
            var story = new CityRenderer(424242, progress);
            var lida = story.Npcs[1];
            FirstDistrictStory.TryGetDialogue(lida, progress, out _, out var choices);
            Check(lida.ApplyChoice(choices[0], progress), "first story action applies");
            FirstDistrictStory.TryGetDialogue(lida, progress, out _, out choices);
            var reply = choices[0];
            Check(lida.ApplyChoice(reply, progress), "first response applies");
            var before = (progress.Empathy, progress.Curiosity, lida.Trust, lida.Awareness, lida.TimesTalked);
            Check(!lida.ApplyChoice(reply, progress), "repeated response rejected");
            Check(before == (progress.Empathy, progress.Curiosity, lida.Trust, lida.Awareness, lida.TimesTalked), "no repeated rewards or relationship change");
            Check(!lida.ApplyChoice(reply with { Text = "Translated reply" }, progress), "explicit reward ID survives translation");
            Check(MemoryRuntime.Current.Anchors.Count == 1, "single anchor after repeated dialogue");

            var awakened = story.Npcs[4];
            awakened.State = NpcState.Aware;
            awakened.Awareness = 100f;
            story.TimeOfDay = 24f;
            story.AdvanceDayClock(0.1f);
            Check(progress.Day == 2 && awakened.State == NpcState.Aware && awakened.Awareness == 100f,
                "real midnight preserves NPC awakening");
            var qualities = (progress.Memory, progress.Curiosity, progress.Empathy, progress.Agency, progress.Courage);
            for (int day = 0; day < 20; day++)
            {
                progress.NewDay();
                awakened.Reset();
                Check(!lida.ApplyChoice(reply, progress), "repeat remains rejected across mornings");
            }
            Check(qualities == (progress.Memory, progress.Curiosity, progress.Empathy, progress.Agency, progress.Courage), "qualities survive 20 mornings");
            Check(awakened.State == NpcState.Aware && awakened.Awareness == 100f, "awakening survives 20 resets");
        }
        catch (Exception e)
        {
            failures.Add(e.Message);
        }
        finally
        {
            MemoryRuntime.Reset();
        }
        message = failures.Count == 0 ? "Memory integration tests passed." : "Memory integration tests failed: " + string.Join("; ", failures);
        return failures.Count == 0;
    }
}
