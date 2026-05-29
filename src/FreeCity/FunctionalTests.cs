using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK.Mathematics;
using Probuzhdenie.Player;

namespace Probuzhdenie.FreeCity;

public static class FunctionalTests
{
    public static bool Run(out string message)
    {
        var failures = new List<string>();

        CheckHeroProgress(failures);
        CheckAwareness(failures);
        CheckNpcStatesAndDialogues(failures);
        CheckCityGeneration(failures);
        CheckCityRules(failures);
        CheckCollisionDetection(failures);
        CheckInterestMarkerDefaults(failures);
        CheckPlayerController(failures);

        if (!SaveSystem.RunSelfTest(out string saveMessage))
            failures.Add(saveMessage);

        if (failures.Count == 0)
        {
            message = "Functional tests passed.";
            return true;
        }

        message = "Functional tests failed: " + string.Join("; ", failures);
        return false;
    }

    private static void CheckHeroProgress(List<string> failures)
    {
        var progress = new HeroProgress();
        Expect(progress.Day == 1, "new hero starts on day 1", failures);

        bool discovered = progress.DiscoverEgg("first_memory", memoryGain: 10f, curiosityGain: 4f);
        Expect(discovered, "first egg discovery succeeds", failures);
        Expect(Nearly(progress.Memory, 10f) && Nearly(progress.Curiosity, 4f), "egg rewards qualities", failures);

        bool duplicate = progress.DiscoverEgg("first_memory", memoryGain: 10f, curiosityGain: 4f);
        Expect(!duplicate, "duplicate egg discovery is ignored", failures);
        Expect(Nearly(progress.Memory, 10f) && Nearly(progress.Curiosity, 4f), "duplicate egg does not reward twice", failures);

        progress.AddQualities(memory: 200f, curiosity: 200f, empathy: 200f, agency: 200f, courage: 200f);
        Expect(Nearly(progress.Memory, 100f) && Nearly(progress.Curiosity, 100f), "qualities clamp to 100", failures);

        progress.Restore(day: -5, memory: -10f, curiosity: 42f, empathy: 120f, agency: 5f, courage: 8f);
        Expect(progress.Day == 1, "restore clamps day to at least 1", failures);
        Expect(Nearly(progress.Memory, 0f) && Nearly(progress.Empathy, 100f), "restore clamps quality range", failures);

        double applied = progress.ApplyOfflineGrowth(24 * 60);
        Expect(Nearly(applied, 12 * 60), "offline growth caps at 12 hours", failures);
    }

    private static void CheckAwareness(List<string> failures)
    {
        var awareness = new AwarenessSystem();
        awareness.Restore(-10f);
        Expect(Nearly(awareness.Level, 0f) && awareness.Stage == 0, "awareness clamps below zero", failures);

        awareness.Restore(99f);
        var player = new NpcCharacter(Vector3.Zero, Vector3.Zero, seed: 1001);
        awareness.Update(player, timeOfDay: 16f, dt: 2f);
        Expect(Nearly(awareness.Level, 100f), "awareness reaches 100", failures);
        Expect(player.State == NpcState.Aware, "awareness wakes the player", failures);
    }

    private static void CheckNpcStatesAndDialogues(List<string> failures)
    {
        var npc = new NpcCharacter(Vector3.Zero, Vector3.Zero, seed: 2002);

        npc.Update(timeOfDay: 6f, dt: 0.1f);
        Expect(npc.State == NpcState.Sleeping, "npc sleeps before wake hour", failures);
        Expect(!string.IsNullOrWhiteSpace(npc.GetDialogue(0f, new HeroProgress())), "sleeping npc has dialogue", failures);

        npc.Update(timeOfDay: 10f, dt: 0.1f);
        Expect(npc.State == NpcState.Working, "npc works during work hours when at work", failures);
        Expect(!string.IsNullOrWhiteSpace(npc.GetDialogue(0f, new HeroProgress())), "working npc has dialogue", failures);

        npc.State = NpcState.Panic;
        Expect(!string.IsNullOrWhiteSpace(npc.GetDialogue(0f, new HeroProgress())), "panic npc has dialogue", failures);

        npc.Reset();
        Expect(npc.State == NpcState.Walking && npc.Position == npc.HomePos, "npc reset returns home", failures);
    }

    private static void CheckCityGeneration(List<string> failures)
    {
        var a = CityGenerator.Generate(12345);
        var b = CityGenerator.Generate(12345);
        int expectedCount = (CityGenerator.CityRadius * 2 + 1) * (CityGenerator.CityRadius * 2 + 1);

        Expect(a.Count == expectedCount, "city generator creates full grid", failures);
        Expect(b.Count == expectedCount, "city generator deterministic count", failures);

        for (int i = 0; i < Math.Min(a.Count, b.Count); i++)
        {
            if (!SameBlock(a[i], b[i]))
            {
                failures.Add($"city generator differs at block {i}");
                break;
            }
        }

        Expect(a.Any(block => block.Type == BuildingType.Bank), "generated city has a bank marker candidate", failures);
        Expect(a.Any(block => block.Type == BuildingType.Cafe), "generated city has a cafe marker candidate", failures);
        Expect(a.Any(block => block.Type == BuildingType.Police), "generated city has a police marker candidate", failures);
    }

    private static void CheckCityRules(List<string> failures)
    {
        var progress = new HeroProgress();
        var city = new CityRenderer(seed: 4242, progress);

        Expect(city.NpcCount == 50, "city spawns 50 NPCs", failures);
        Expect(city.Player != null, "city has player", failures);
        Expect(city.InterestMarkers.Any(marker => marker.Id == "center"), "city has center marker", failures);
        Expect(city.InterestMarkers.Count >= 2, "city has multiple interest markers", failures);

        city.RegisterTalk();
        city.RegisterTalk();
        Expect(Nearly(progress.Empathy, 0f), "talk egg waits for threshold", failures);

        city.RegisterTalk();
        Expect(progress.DiscoveredEggs.Contains("talk_thrice"), "talk threshold discovers empathy egg", failures);
        Expect(progress.Empathy > 0f, "talk threshold rewards empathy", failures);
        float empathyAfterFirstAward = progress.Empathy;

        city.RegisterTalk();
        city.RegisterTalk();
        city.RegisterTalk();
        Expect(Nearly(progress.Empathy, empathyAfterFirstAward), "talk egg cannot be farmed repeatedly", failures);
    }

    private static void CheckCollisionDetection(List<string> failures)
    {
        var progress = new HeroProgress();
        var city = new CityRenderer(seed: 4242, progress);

        // Find center of a building (not tree/lamp) for collision test
        Vector3 inBuilding = Vector3.Zero;
        bool foundBuilding = false;
        foreach (var block in city.Blocks)
        {
            if (block.Type == BuildingType.Tree || block.Type == BuildingType.Lamp) continue;
            inBuilding = new Vector3(block.X + block.Width * 0.5f, 0f, block.Z + block.Depth * 0.5f);
            foundBuilding = true;
            break;
        }

        if (foundBuilding)
        {
            bool walkable = city.IsPositionWalkable(inBuilding, 0f);
            Expect(!walkable, "center of a building is not walkable", failures);

            Vector3 clamped = city.ClampToWalkable(inBuilding, 0.3f);
            bool clampedWalkable = city.IsPositionWalkable(clamped, 0f);
            Expect(clampedWalkable, "ClampToWalkable pushes position out of building", failures);
        }

        Vector3 onRoad = new(-1f, 0f, 5f);
        bool roadWalkable = city.IsPositionWalkable(onRoad, 0f);
        Expect(roadWalkable, "position on road is walkable", failures);
        Vector3 inRoadBuilding = city.ClampToWalkable(onRoad, 0f);
        Expect(inRoadBuilding == onRoad, "ClampToWalkable does not move road position", failures);
    }

    private static void CheckPlayerController(List<string> failures)
    {
        // Full movement tests need Input (requires GameWindow) — only
        // possible in the graphical environment. Here we test ResetMotion
        // on the real player and verify no crash with null Input.
        var progress = new HeroProgress();
        var city = new CityRenderer(seed: 4242, progress);
        var cam = new Camera();
        cam.Front = -Vector3.UnitZ;
        cam.Right = Vector3.UnitX;

        if (city.Player == null) return;
        var player = city.Player;
        var controller = new PlayerController(city, null!, cam);

        // With null Input, all keys read as false → no movement → idle
        controller.Update(0.016f);
        Expect(controller.CurrentState == PlayerState.Idle, "PlayerController idle with no input state", failures);
        Expect(controller.CurrentSpeed < 0.001f, "PlayerController speed zero with no input", failures);

        // ResetMotion clears everything
        player.Velocity = new Vector3(5f, 0, 3f);
        player.State = NpcState.Walking;
        controller.ResetMotion();
        Expect(controller.CurrentSpeed < 0.001f, "ResetMotion clears speed", failures);
        Expect(player.Velocity == Vector3.Zero, "ResetMotion zeroes velocity", failures);
        Expect(player.State == NpcState.Relaxing, "ResetMotion sets NpcState.Relaxing", failures);
        Expect(controller.CurrentState == PlayerState.Idle, "ResetMotion sets PlayerState.Idle", failures);
    }

    private static void CheckInterestMarkerDefaults(List<string> failures)
    {
        var marker = new InterestMarker("x", "name", "desc", Vector3.One);
        Expect(marker.Id == "x" && marker.Position == Vector3.One, "interest marker stores identity and position", failures);
        Expect(Nearly(marker.Radius, 5f), "interest marker default radius", failures);
        Expect(marker.Color.X > 0f && marker.Color.Y > 0f && marker.Color.Z > 0f, "interest marker default color", failures);
    }

    private static bool SameBlock(CityBlock left, CityBlock right)
    {
        return left.X == right.X &&
            left.Z == right.Z &&
            left.Width == right.Width &&
            left.Depth == right.Depth &&
            left.Type == right.Type &&
            left.Height == right.Height &&
            left.Color == right.Color &&
            left.Accent == right.Accent;
    }

    private static void Expect(bool condition, string description, List<string> failures)
    {
        if (!condition)
            failures.Add(description);
    }

    private static bool Nearly(float actual, float expected, float tolerance = 0.001f)
    {
        return Math.Abs(actual - expected) <= tolerance;
    }

    private static bool Nearly(double actual, double expected, double tolerance = 0.001)
    {
        return Math.Abs(actual - expected) <= tolerance;
    }
}
