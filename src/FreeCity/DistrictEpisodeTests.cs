using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using Probuzhdenie.Player;

namespace Probuzhdenie.FreeCity;

public static class DistrictEpisodeTests
{
    public static bool Run(out string message)
    {
        var failures = new List<string>();
        void Check(bool ok, string name) { if (!ok) failures.Add(name); }
        CityRenderer Fresh()
        {
            MemoryRuntime.Reset();
            return new CityRenderer(424242);
        }
        try
        {
            var ui = new UiRenderer();
            foreach (var size in new[] { (960,540), (960,900), (1280,720), (1920,1080) })
            {
                ui.Begin(size.Item1, size.Item2);
                foreach (string line in ui.WrapText("Лида удерживает рейс. Но Марку нужно самому решить, хочет ли он идти. ОченьДлинноеСловоБезПробелов",0.0045f,0.79f))
                    Check(ui.MeasureText(line,0.0045f) <= 0.7901f, "wrapped dialogue stays in bounds");
            }
            var city = Fresh();
            var episode = city.Progress.DistrictEpisode;
            FirstDistrictStory.TryGetDialogue(city.Npcs[1], city.Progress, out _, out var choices);
            var delay = choices[0];
            var repair = choices[1];
            Check(city.Npcs[1].ApplyChoice(repair, city.Progress), "plan repair");
            Check(!city.Npcs[1].ApplyChoice(delay, city.Progress), "mutually exclusive plans");
            Check(MemoryRuntime.Current.Events.Count == 0, "plan is not an event");
            city.Player!.Position = FirstDistrictEpisode.MarkStart;
            Check(!city.InteractWithDistrict(DistrictInteraction.Signal), "reject remote signal action");
            city.Player.Position = FirstDistrictEpisode.Signal;
            Check(new InteractionDetector(city).Detect(city.Player.Position).DistrictAction == DistrictInteraction.Signal, "detect physical signal");
            Check(city.InteractWithDistrict(DistrictInteraction.Signal) && episode.Phase == DistrictPhase.Repaired, "physical repair");
            float agency = city.Progress.Agency;
            city.InteractWithDistrict(DistrictInteraction.Signal);
            Check(city.Progress.Agency == agency, "no repeated signal reward");
            Check(DistrictScene.TramPresent(episode, 1) && episode.TramDeparting && episode.TramOffset == Vector3.Zero, "repair starts departure without popping out");
            city.Player.Position = FirstDistrictEpisode.Rest;
            Check(city.InteractWithDistrict(DistrictInteraction.Rest) && city.Progress.Day == 2 && city.TimeOfDay == 8f, "rest crosses real midnight");
            Check(city.InteractWithDistrict(DistrictInteraction.Rest) && city.Progress.Day == 3, "rest remains available for third morning");
            Check(MemoryRuntime.Current.Events.Count == 0 && episode.TraceText(2).Contains("Встречи не было"), "repair never invents meeting memory");

            city = Fresh(); episode = city.Progress.DistrictEpisode;
            episode.Plan(true, 1);
            city.Player!.Position = FirstDistrictEpisode.Signal;
            city.InteractWithDistrict(DistrictInteraction.Signal);
            city.TimeOfDay = episode.Deadline;
            city.UpdateDistrict(0);
            Check(episode.Phase == DistrictPhase.Missed && MemoryRuntime.Current.Events.Count == 0, "deadline without Mark");
            Check(!episode.Invite(city.Npcs[2], 1, city.TimeOfDay), "late invitation rejected");

            city = Fresh(); episode = city.Progress.DistrictEpisode;
            episode.Plan(true, 1);
            episode.ActivateSignal(FirstDistrictEpisode.Signal, 8f, 1);
            city.Npcs[2].Trust = 0;
            FirstDistrictStory.TryGetDialogue(city.Npcs[2], city.Progress, out _, out choices);
            Check(city.Npcs[2].ApplyChoice(choices[0], city.Progress) && episode.MarkRefused && episode.Phase == DistrictPhase.Missed, "voluntary refusal to travel");
            Check(MemoryRuntime.Current.Events.Count == 0, "refusal creates no fictitious meeting");

            city = Fresh(); episode = city.Progress.DistrictEpisode;
            episode.Plan(true, 1); episode.ActivateSignal(FirstDistrictEpisode.Signal, 8f, 1);
            FirstDistrictStory.TryGetDialogue(city.Npcs[2], city.Progress, out _, out choices);
            city.Npcs[2].ApplyChoice(choices[1], city.Progress);
            Check(episode.InvitationWithdrawn && !episode.MarkRefused && episode.TraceText(1).Contains("герой отозвал"),
                "hero withdrawal is not attributed to Mark");
            var restoredEpisode = new FirstDistrictEpisode();
            restoredEpisode.Restore(episode.Snapshot(), MemoryRuntime.Current);
            Check(restoredEpisode.InvitationWithdrawn && !restoredEpisode.MarkRefused, "withdrawal reason round trip");

            city = Fresh(); episode = city.Progress.DistrictEpisode;
            episode.Plan(true, 1); episode.ActivateSignal(FirstDistrictEpisode.Signal, 8f, 1);
            city.Npcs[2].Trust = 10; city.Npcs[2].Friendliness = 20;
            episode.Invite(city.Npcs[2], 1, 8f);
            city.Player!.Position = FirstDistrictEpisode.MarkStart;
            for (int i = 0; i < 160; i++) { city.AdvanceDayClock(0.1f); city.UpdateDistrict(0.1f); }
            Check(episode.Phase == DistrictPhase.MarkOnWay && episode.MarkTravel > 0.98f && MemoryRuntime.Current.Events.Count == 0,
                "walking without player at meeting creates no memory");
            city.Player.Position = FirstDistrictEpisode.Stop + new Vector3(0,0,-2);
            city.Npcs[1].Position = FirstDistrictEpisode.MarkStart;
            city.UpdateDistrict(0);
            Check(MemoryRuntime.Current.Events.Count == 0, "Lida must also be present");
            city.Npcs[1].Position = FirstDistrictEpisode.Stop;
            city.UpdateDistrict(0);
            Check(episode.Phase == DistrictPhase.Met && MemoryRuntime.Current.Anchors.Count == 1, "three people share a real place");
            city.UpdateDistrict(1);
            Check(MemoryRuntime.Current.Events.Count == 1, "single physical event");
            Check(episode.TramDeparting && city.Npcs[1].Greeting > 0 && city.Npcs[2].Greeting > 0,
                "meeting releases tram and both participants greet");
            FirstDistrictStory.TryGetDialogue(city.Npcs[2], city.Progress, out _, out choices);
            city.Npcs[2].ApplyChoice(choices[0], city.Progress);
            city.Player.Position = FirstDistrictEpisode.Rest;
            city.InteractWithDistrict(DistrictInteraction.Rest);
            Check(MemoryRuntime.Current.HasPersisted(FirstDistrictStory.MeetingEventId) && episode.TraceText(2).Contains("две подписи"), "visible persisted morning trace");

            foreach (int seed in new[] { 1, 42, 424242, -10 })
            {
                MemoryRuntime.Reset();
                city = new CityRenderer(seed);
                for (int x = 7; x <= 34; x++)
                    Check(Vector2.Distance(city.ClampToWalkable(new Vector3(x,0.12f,11.5f),0.3f).Xz,new Vector2(x,11.5f)) < 0.02f, "authored route stays walkable");
                var center = new Vector3(4,0.12f,17);
                Check(Vector3.Distance(city.ClampToWalkable(center,0.3f),center) > 1f, "tram has collision");
                city.Progress.DistrictEpisode.Plan(false, 1);
                city.Progress.DistrictEpisode.ActivateSignal(FirstDistrictEpisode.Signal,8,1);
                for (int i = 3; i < city.Npcs.Count; i++) city.Npcs[i].Position = new Vector3(i,0.12f,40);
                for (int i = 0; i < 250; i++) city.UpdateDistrict(0.1f);
                Check(city.Progress.DistrictEpisode.TramGone, "tram departs along authored street");
                Check(Vector2.Distance(city.ClampToWalkable(center,0.3f).Xz,center.Xz) < 0.02f, "departed tram leaves no invisible collider");
                for (int x = -155; x <= 9; x++)
                    Check(Vector2.Distance(city.ClampToWalkable(new Vector3(x,0.12f,17),1.2f).Xz,new Vector2(x,17)) < 0.02f, "tram corridor clear of static obstacles");
            }

            city = Fresh(); episode = city.Progress.DistrictEpisode;
            for (int i = 3; i < city.Npcs.Count; i++) city.Npcs[i].Position = new Vector3(i,0.12f,40);
            episode.Plan(false,1); episode.ActivateSignal(FirstDistrictEpisode.Signal,8,1);
            for (int i = 0; i < 10; i++) city.UpdateDistrict(0.1f);
            Check(episode.TramOffset == Vector3.Zero && city.Npcs[1].Greeting > 0.5f, "dispatch gesture before motion");
            float paused = episode.DepartureSeconds;
            city.UpdateDistrict(0);
            Check(episode.DepartureSeconds == paused, "zero delta cannot advance departure");
            for (int i = 0; i < 45; i++) city.UpdateDistrict(0.1f);
            var shifted = DistrictScene.TramBounds(episode);
            Check(shifted.Min.X < -10 && Vector2.Distance(city.ClampToWalkable(new Vector3(shifted.Center.X,0.12f,17),0.3f).Xz,shifted.Center) > 1,
                "tram collider follows visible body");
            Check(Vector2.Distance(city.ClampToWalkable(new Vector3(4,0.12f,17),0.3f).Xz,new Vector2(4,17)) < 0.02f, "old stop collider released during motion");
            city.Player!.Position = new Vector3(shifted.Min.X-0.6f,0.12f,17);
            Vector3 beforeTram = episode.TramOffset, beforePlayer = city.Player.Position;
            city.UpdateDistrict(0.1f);
            Check(episode.TramBlocked && episode.TramOffset == beforeTram && city.Player.Position == beforePlayer, "brake for player without pushing or tunnelling");
            city.UpdateDistrict(0);
            Check(episode.TramBlocked && episode.TramOffset == beforeTram, "zero delta preserves blocked status");
            city.Player.Position = FirstDistrictEpisode.Signal;
            city.Npcs[3].Position = new Vector3(shifted.Min.X-0.6f,0.12f,17);
            city.UpdateDistrict(0.1f);
            Check(episode.TramBlocked && episode.TramOffset == beforeTram, "brake for non-player pedestrian");
            city.Npcs[3].Position = new Vector3(0,0.12f,40);
            city.UpdateDistrict(0.1f);
            Check(!episode.TramBlocked && episode.TramOffset.X < beforeTram.X, "resume once tracks clear");
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 50; i++) city.UpdateDistrict(0.1f);
            Check(GC.GetAllocatedBytesForCurrentThread() == allocated, "steady departure update allocates no managed memory");

            city = Fresh(); episode = city.Progress.DistrictEpisode;
            episode.Plan(true,1); episode.ActivateSignal(FirstDistrictEpisode.Signal,8,1);
            city.Npcs[2].Trust = 10; city.Npcs[2].Friendliness = 20;
            episode.Invite(city.Npcs[2],1,8);
            for (int i = 0; i < 30; i++) city.UpdateDistrict(0.1f);
            float outward = episode.MarkTravel;
            city.TimeOfDay = episode.Deadline;
            for (int i = 0; i < 45; i++) city.UpdateDistrict(0.1f);
            Check(outward > 0.1f && episode.MarkTravel < 0.01f && MemoryRuntime.Current.Events.Count == 0,
                "missed Mark walks back without a fabricated meeting");

            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, -2f, -0.5f, FirstDistrictEpisode.DepartureDuration+1 })
            {
                bool rejected = false;
                try { new FirstDistrictEpisode().Restore(new DistrictEpisodeSaveData { Phase=DistrictPhase.Repaired, DepartureSeconds=invalid },new MemoryLedger()); }
                catch (System.IO.InvalidDataException) { rejected = true; }
                Check(rejected, "invalid departure time rejected");
            }
            var wave = CharacterPose.Create(0,0,1).RightArm;
            Check(Math.Abs((wave.Joint-wave.Root).Length-0.163f)<0.0001f && Math.Abs((wave.Tip-wave.Joint).Length-0.148f)<0.0001f && wave.Tip.Y>0.95f,
                "greeting raises hand without stretching bones");
        }
        catch (Exception e) { failures.Add(e.ToString()); }
        finally { MemoryRuntime.Reset(); }
        message = failures.Count == 0 ? "Physical district episode tests passed." : "Physical district episode tests failed: " + string.Join("; ", failures);
        return failures.Count == 0;
    }
}
