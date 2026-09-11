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
        CheckCameraCollision(failures);
        var projectionCamera = new Camera();
        foreach (float aspect in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            Expect(projectionCamera.Proj(aspect) == projectionCamera.Proj(1),
                "invalid window aspect uses finite fallback projection", failures);
        CheckGameSettings(failures);
        CheckInterestMarkerDefaults(failures);
        CheckPlayerController(failures);
        CheckCharacterMesh(failures);
        CheckWallRelease(failures);
        CheckSceneGeometry(failures);
        CheckSceneLighting(failures);
        CheckStreetLayout(failures);
        CheckPackedVertex(failures);

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

    private static void CheckPackedVertex(List<string> failures)
    {
        Expect(System.Runtime.CompilerServices.Unsafe.SizeOf<PackedSceneVertex>() == PackedSceneVertex.Stride,
            "packed vertex stride matches the GPU layout", failures);
        Expect(System.Runtime.InteropServices.Marshal.OffsetOf<PackedSceneVertex>(nameof(PackedSceneVertex.R)).ToInt32() == 12 &&
            System.Runtime.InteropServices.Marshal.OffsetOf<PackedSceneVertex>(nameof(PackedSceneVertex.Nx)).ToInt32() == 20,
            "packed color and normal offsets match GPU attributes", failures);
        var vertex = new PackedSceneVertex(new float[] { -300.125f, 0.12f, 281.75f, 1.15f, 0.003f, 0.8f, -1, 0.7f, 0 });
        Expect(vertex.Position == new Vector3(-300.125f, 0.12f, 281.75f), "packing preserves world coordinates", failures);
        Expect(Math.Abs((float)vertex.R - 1.15f) < 0.001f && Math.Abs((float)vertex.G - 0.003f) < 0.00001f &&
            Math.Abs((float)vertex.B - 0.8f) < 0.001f, "half colors preserve material tints including values above one", failures);
        Expect(vertex.Nx == -short.MaxValue && vertex.Nz == 0 && Math.Abs(vertex.Ny / (float)short.MaxValue - 0.7f) < 0.00004f,
            "packed signed normals retain direction", failures);
    }

    private static void CheckSceneGeometry(List<string> failures)
    {
        var vertices = new List<float>();
        Vector3 center = new(2, 3, 4);
        SceneGeometry.Box(vertices, center - Vector3.One, Vector3.One * 2, Vector3.One);
        Expect(vertices.Count == 36 * 9, "box has six complete faces", failures);
        CheckOutward(vertices, center, "box", failures);

        vertices.Clear();
        SceneGeometry.Foliage(vertices, center, new Vector3(2, 3, 1), Vector3.One);
        Expect(vertices.Count > 0, "foliage is not empty", failures);
        CheckOutward(vertices, center, "foliage", failures);

        vertices.Clear();
        SceneGeometry.Cylinder(vertices,center-Vector3.UnitY,center+Vector3.UnitY,0.4f,Vector3.One);
        CheckOutward(vertices,center,"cylinder",failures);

        foreach (var ground in new[] { CityStreets.BuildRoads() })
            for (int i = 0; i < ground.Count; i += 9)
                if (!Nearly(ground[i + 7], 1f))
                {
                    failures.Add("street and paving normals must face upward");
                    break;
                }
    }

    private static void CheckStreetLayout(List<string> failures)
    {
        Expect(CityGenerator.CarriagewayWidth == 12 && CityGenerator.SidewalkW == 3,
            "street has twelve metre carriageway and three metre sidewalks",failures);
        Expect(Nearly(CityGenerator.GroundHeight(12,5),0.12f),"sidewalk is raised",failures);
        Expect(Nearly(CityGenerator.GroundHeight(19,5),0f),"road is at grade",failures);
        Expect(Nearly(CityGenerator.GroundHeight(-1,5),0.12f),"negative sidewalk coordinates wrap correctly",failures);
        Expect(Nearly(CityGenerator.GroundHeight(-9,5),0f),"negative road coordinates wrap correctly",failures);
        var city = new CityRenderer(424242);
        Expect(city.IsPositionWalkable(city.Player!.Position,0.3f),"hero spawns outside buildings and parked cars",failures);
        var building = city.Blocks.First(b => b.Type is not (BuildingType.Tree or BuildingType.Lamp));
        var car = CityStreetProps.ParkedCarBounds(building);
        Expect(!city.IsPositionWalkable(new Vector3(car.Center.X,0,car.Center.Y),0),"parked car blocks movement",failures);
        Expect(CityGenerator.WorldToBlock(-0.1f)==-1,"grid indexing handles negative coordinates",failures);
        var view = Matrix4.Identity;
        var projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(70),16f/9f,0.1f,300);
        Expect(new SceneRange(0,3,new(0,0,-5),1).Visible(view,projection),"visible geometry is retained",failures);
        Expect(!new SceneRange(0,3,new(0,0,5),1).Visible(view,projection),"geometry behind camera is culled",failures);
        Expect(!new SceneRange(0,3,new(100,0,-5),1).Visible(view,projection),"offscreen geometry is culled",failures);
        Expect(new SceneRange(0,3,new(0,0,0),1).Visible(view,projection),"near-plane intersections are retained",failures);
        Expect(!new SceneRange(0,3,new(0,0,-250),1).Visible(view,projection),"distant geometry is culled",failures);
    }

    private static void CheckOutward(List<float> vertices, Vector3 center, string name, List<string> failures)
    {
        for (int i = 0; i < vertices.Count; i += 27)
        {
            Vector3 a = new(vertices[i], vertices[i + 1], vertices[i + 2]);
            Vector3 b = new(vertices[i + 9], vertices[i + 10], vertices[i + 11]);
            Vector3 c = new(vertices[i + 18], vertices[i + 19], vertices[i + 20]);
            Vector3 normal = new(vertices[i + 6], vertices[i + 7], vertices[i + 8]);
            if (!float.IsFinite(normal.LengthSquared) || !Nearly(normal.LengthSquared, 1f) ||
                Vector3.Dot(normal, (a + b + c) / 3 - center) <= 0 ||
                Vector3.Dot(Vector3.Cross(b - a, c - a), normal) <= 0)
            {
                failures.Add(name + " must have finite, outward normals and matching winding");
                break;
            }
        }
    }

    private static void CheckSceneLighting(List<string> failures)
    {
        var noon = SceneLighting.At(12);
        var midnight = SceneLighting.At(0);
        Expect(noon == SceneLighting.At(36), "lighting repeats every 24 hours", failures);
        Expect(noon == SceneLighting.At(float.NaN), "invalid time uses finite noon lighting", failures);
        Expect(noon.Ambient.LengthSquared > midnight.Ambient.LengthSquared, "day is brighter than night", failures);
        Expect(noon.WindowGlow == 0 && midnight.WindowGlow == 1, "windows glow only after daylight fades", failures);
        for (int i = 0; i <= 96; i++)
        {
            var light = SceneLighting.At(i / 4f);
            Expect(float.IsFinite(light.Sun.LengthSquared) && light.Sun.LengthSquared > 0,
                "sun direction stays finite and nonzero", failures);
            Expect(light.WindowGlow >= 0 && light.WindowGlow <= 1, "window glow stays bounded", failures);
        }
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

        Vector3 onRoad = new(-1f, 0.12f, 5f);
        bool roadWalkable = city.IsPositionWalkable(onRoad, 0f);
        Expect(roadWalkable, "position on road is walkable", failures);
        Vector3 inRoadBuilding = city.ClampToWalkable(onRoad, 0f);
        Expect(inRoadBuilding == onRoad, "ClampToWalkable does not move road position", failures);
    }

    private static void CheckPlayerController(List<string> failures)
    {
        // Input-free idle/reset checks complement the movement-intent regression tests.
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

    private static void CheckCameraCollision(List<string> failures)
    {
        var city = new CityRenderer(seed: 4242);
        CityBlock building = city.Blocks.First(block =>
            block.Type != BuildingType.Tree && block.Type != BuildingType.Lamp);

        Vector3 focus = new(building.X - 2f, 1.6f, building.Z + building.Depth * 0.5f);
        Vector3 desired = new(building.X + building.Width * 0.5f, 1.6f, building.Z + building.Depth * 0.5f);
        Vector3 resolved = city.ResolveCameraPosition(focus, desired);

        bool insideBuilding =
            resolved.X >= building.X + 0.2f &&
            resolved.X <= building.X + building.Width - 0.2f &&
            resolved.Z >= building.Z + 0.2f &&
            resolved.Z <= building.Z + building.Depth - 0.2f;
        Expect(!insideBuilding, "camera stops before building geometry", failures);
        Expect(Vector3.DistanceSquared(resolved, desired) > 0.01f, "blocked camera position is shortened", failures);

        Vector3 highFocus = new(0f, 45f, 0f);
        Vector3 highDesired = new(8f, 45f, 8f);
        Vector3 unobstructed = city.ResolveCameraPosition(highFocus, highDesired);
        Expect(Vector3.DistanceSquared(unobstructed, highDesired) < 0.0001f, "unblocked camera keeps desired position", failures);
    }

    private static void CheckGameSettings(List<string> failures)
    {
        var settings = new GameSettings
        {
            Width = 100,
            Height = 10000,
            MouseSensitivity = 5f,
        };

        GameSettings.Normalize(settings);
        Expect(settings.Width == 960, "settings enforce minimum width", failures);
        Expect(settings.Height == 2160, "settings enforce maximum height", failures);
        Expect(Nearly(settings.MouseSensitivity, 2f), "settings clamp mouse sensitivity", failures);
        var legacy = System.Text.Json.JsonSerializer.Deserialize<GameSettings>("{\"Width\":1280}")!;
        Expect(legacy.Shadows, "old settings enable shadows by default", failures);
        settings.Shadows = false;
        var restored = System.Text.Json.JsonSerializer.Deserialize<GameSettings>(System.Text.Json.JsonSerializer.Serialize(settings))!;
        Expect(!restored.Shadows, "disabled shadows survive settings serialization", failures);
    }

    private static void CheckWallRelease(List<string> failures)
    {
        var city = new CityRenderer(seed: 4242);
        var player = city.Player!;
        var block = city.Blocks.First(b => b.Type != BuildingType.Tree && b.Type != BuildingType.Lamp);
        player.Position = new Vector3(block.X - 1, 0, block.Z + block.Depth * 0.5f);
        var controller = new PlayerController(city, null!, new Camera());
        for (int i = 0; i < 90; i++) controller.UpdateMovement(1f / 60, Vector3.UnitX, false);
        Vector3 stopped = player.Position;
        Expect(player.Velocity.LengthSquared < 0.001f, "wall stops the hero", failures);
        Expect(player.AnimBlend < 0.001f, "blocked hero stops walking animation", failures);
        controller.UpdateMovement(1f / 60, Vector3.Zero, false);
        Expect(float.IsFinite(player.Position.X) && float.IsFinite(player.Position.Z),
            "releasing movement against a wall keeps finite coordinates", failures);
        Expect(Vector3.DistanceSquared(stopped, player.Position) < 0.001f, "wall release does not teleport", failures);
        Expect(controller.CurrentState == PlayerState.Idle, "wall release returns to idle", failures);
    }

    private static void CheckCharacterMesh(List<string> failures)
    {
        foreach (float blend in new[] { 0f, .3f, 1f })
            for (int i = 0; i < 64; i++)
            {
                CharacterPose pose = HeroPose.Create(i * MathHelper.TwoPi / 64, blend);
                foreach (LimbPose leg in new[] { pose.LeftLeg, pose.RightLeg })
                {
                    Expect(Nearly((leg.Joint - leg.Root).Length, HeroPose.LegLength), "hero thigh length stays constant", failures);
                    Expect(Nearly((leg.Tip - leg.Joint).Length, HeroPose.LegLength), "hero shin length stays constant", failures);
                    Expect(leg.Tip.Y >= HeroPose.AnkleHeight, "hero feet never penetrate the ground", failures);
                }
                Expect(Nearly(Math.Min(pose.LeftLeg.Tip.Y, pose.RightLeg.Tip.Y), HeroPose.AnkleHeight),
                    "hero gait always has a grounded support foot", failures);
            }
        for (int i = 0; i < 32; i++)
        {
            CharacterPose pose = CharacterPose.Create(i * MathHelper.TwoPi / 32, 1);
            foreach (var leg in new[] { pose.LeftLeg, pose.RightLeg })
            {
                Expect(Nearly((leg.Joint - leg.Root).Length, 0.235f), "thigh length is stable", failures);
                Expect(Nearly((leg.Tip - leg.Joint).Length, 0.235f), "shin length is stable", failures);
            }
            foreach (var arm in new[] { pose.LeftArm, pose.RightArm })
            {
                Expect(Nearly((arm.Joint - arm.Root).Length, 0.163f), "upper arm length is stable", failures);
                Expect(Nearly((arm.Tip - arm.Joint).Length, 0.148f), "forearm length is stable", failures);
            }
        }

        var hero = new NpcCharacter(Vector3.Zero, Vector3.Zero, seed: 123);
        HeroStyle.ApplyTo(hero);
        var mesh = new CharacterMesh();
        mesh.Append(hero, 0, Vector3.Zero, 0, 0);
        Expect(mesh.VertexCount > 1000 && mesh.VertexCount < 15000, "character has bounded detailed geometry", failures);
        float[] baseline = mesh.Data.Take(mesh.FloatCount).ToArray();
        Vector3 Read(float[] data, int index) => new(data[index], data[index + 1], data[index + 2]);
        float minY = float.MaxValue, maxY = float.MinValue, minX = float.MaxValue, maxX = float.MinValue;
        bool finite = true, winding = true;
        for (int i = 0; i < baseline.Length; i++) finite &= float.IsFinite(baseline[i]);
        for (int i = 0; i < baseline.Length; i += 9)
        {
            Vector3 p = Read(baseline, i);
            minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
            minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
        }
        for (int i = 0; i < baseline.Length; i += 27)
        {
            Vector3 cross = Vector3.Cross(Read(baseline, i + 9) - Read(baseline, i),
                Read(baseline, i + 18) - Read(baseline, i));
            winding &= cross.LengthSquared > 1e-14f &&
                Vector3.Dot(cross, Read(baseline, i + 6) + Read(baseline, i + 15) + Read(baseline, i + 24)) > 0;
        }
        Expect(finite && winding, "mesh triangles are finite, outward and nondegenerate", failures);
        Expect(Math.Abs(minY) < 0.01f && Nearly(maxY, hero.Height, 0.01f), "character stands on ground at intended height", failures);
        Expect((maxX - minX) / hero.Height < 0.38f, "character has human shoulder proportions", failures);
        mesh.Clear();
        Vector3 origin = new(4, 2, -3);
        mesh.Append(hero, 0, origin, MathHelper.PiOver2, 0);
        bool rotated = mesh.FloatCount == baseline.Length;
        for (int i = 0; rotated && i < baseline.Length; i += 9)
        {
            Vector3 p = Read(baseline, i), n = Read(baseline, i + 6);
            rotated &= Vector3.DistanceSquared(Read(mesh.Data, i), origin + new Vector3(p.Z, p.Y, -p.X)) < 1e-8f;
            rotated &= Vector3.DistanceSquared(Read(mesh.Data, i + 6), new Vector3(n.Z, n.Y, -n.X)) < 1e-8f;
        }
        Expect(rotated, "every body part and normal follows whole-character yaw and translation", failures);
        float[] buffer = mesh.Data;
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 32; i++) { mesh.Clear(); mesh.Append(hero, i * 0.016f); }
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Expect(ReferenceEquals(buffer, mesh.Data) && allocated == 0, "warmed character mesh reuses memory without per-frame allocations", failures);
        mesh.Clear();
        mesh.Append(hero, 0, new Vector3(float.NaN, 0, 0));
        Expect(mesh.VertexCount == 0, "invalid character origin cannot corrupt the render buffer", failures);
        int previousCount = baseline.Length / 9;
        foreach (var detail in new[] { CharacterDetail.Reduced, CharacterDetail.Silhouette })
        {
            mesh.Clear();
            mesh.Append(hero, 0, Vector3.Zero, 0, 0, detail);
            Expect(mesh.VertexCount > 0 && mesh.VertexCount < previousCount, "distance detail reduces vertex count", failures);
            previousCount = mesh.VertexCount;
            bool valid = true;
            for (int i = 0; i < mesh.FloatCount; i++) valid &= float.IsFinite(mesh.Data[i]);
            Expect(valid, "distance detail keeps finite geometry", failures);
        }
        Expect(CharacterMesh.DetailForDistance(0) == CharacterDetail.Full &&
            CharacterMesh.DetailForDistance(400) == CharacterDetail.Reduced &&
            CharacterMesh.DetailForDistance(1600) == CharacterDetail.Silhouette,
            "distance detail selects near, middle and far models", failures);

        bool heroGeometryValid = true;
        foreach (var detail in new[] { CharacterDetail.Full, CharacterDetail.Reduced, CharacterDetail.Silhouette })
            for (int frame = 0; frame < 16; frame++)
            {
                hero.AnimPhase = frame * MathHelper.TwoPi / 16;
                mesh.Clear();
                mesh.Append(hero, 3.6f + frame * .01f, Vector3.Zero, frame * .4f, 1, detail);
                for (int i = 0; i < mesh.FloatCount; i++) heroGeometryValid &= float.IsFinite(mesh.Data[i]);
                for (int i = 0; i < mesh.FloatCount; i += 27)
                {
                    Vector3 cross = Vector3.Cross(Read(mesh.Data, i + 9) - Read(mesh.Data, i),
                        Read(mesh.Data, i + 18) - Read(mesh.Data, i));
                    heroGeometryValid &= cross.LengthSquared > 1e-14f &&
                        Vector3.Dot(cross, Read(mesh.Data, i + 6) + Read(mesh.Data, i + 15) + Read(mesh.Data, i + 24)) > 0;
                }
            }
        Expect(heroGeometryValid, "hero walking and blinking geometry stays finite and outward at every detail", failures);
        var bystander = new NpcCharacter(Vector3.Zero, Vector3.Zero, seed: 456) { Id = 91, Name = "Bystander" };
        mesh.Clear();
        mesh.Append(bystander, 0);
        float[] npcBaseline = mesh.Data.Take(mesh.FloatCount).ToArray();
        mesh.Clear();
        mesh.Append(hero, 0, detail: CharacterDetail.Silhouette);
        mesh.Clear();
        mesh.Append(bystander, 0);
        Expect(mesh.FloatCount == npcBaseline.Length && mesh.Data.AsSpan(0, mesh.FloatCount).SequenceEqual(npcBaseline),
            "hero detail does not leak into subsequent NPC geometry", failures);
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
