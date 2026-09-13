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
            Check(!DistrictScene.TramPresent(episode, 1), "repair releases tram");
            city.Player.Position = FirstDistrictEpisode.Rest;
            Check(city.InteractWithDistrict(DistrictInteraction.Rest) && city.Progress.Day == 2 && city.TimeOfDay == 8f, "rest crosses real midnight");
            Check(!city.InteractWithDistrict(DistrictInteraction.Rest), "rest is not repeatable on day two");
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
                Check(Vector2.Distance(city.ClampToWalkable(center,0.3f).Xz,center.Xz) < 0.02f, "departed tram leaves no invisible collider");
            }
        }
        catch (Exception e) { failures.Add(e.ToString()); }
        finally { MemoryRuntime.Reset(); }
        message = failures.Count == 0 ? "Physical district episode tests passed." : "Physical district episode tests failed: " + string.Join("; ", failures);
        return failures.Count == 0;
    }
}
