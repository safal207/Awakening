using System;
using System.Runtime.CompilerServices;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

/// <summary>
/// World-space staging for the first memory chapter. It owns no quest rewards:
/// it binds Lida/Mark/Nika to one city, exposes the broken signal interaction,
/// and restores the meeting-space arrangement at day boundaries.
/// </summary>
public static class FirstMemorySpatial
{
    public static readonly Vector3 SignalPosition = new(
        CityGenerator.BlockSize + 2f,
        0.12f,
        CityGenerator.BlockSize - 2.5f);

    private sealed class State
    {
        public bool Configured;
        public int ObservedDay = 1;
        public bool SignalObserved;
        public NpcCharacter? Lida;
        public NpcCharacter? Mark;
        public NpcCharacter? Nika;
        public Vector3 MarkBaseline;
        public bool HasMarkBaseline;
    }

    private static readonly ConditionalWeakTable<HeroProgress, State> States = new();

    public static void ConfigureCity(CityRenderer city)
    {
        if (city.Player == null || city.Npcs.Count < 4) return;

        State state = GetState(city.Progress);
        NpcCharacter lida = city.Npcs[1];
        NpcCharacter mark = city.Npcs[2];
        NpcCharacter nika = city.Npcs[3];

        lida.AssignNarrativeRole(NarrativeRole.Lida);
        mark.AssignNarrativeRole(NarrativeRole.Mark);
        nika.AssignNarrativeRole(NarrativeRole.Nika);
        nika.Name = "Ника";
        state.Configured = true;
        state.Lida = lida;
        state.Mark = mark;
        state.Nika = nika;

        Vector3 lidaPosition = city.ClampToWalkable(SignalPosition + new Vector3(0.2f, 0f, 1.55f), 0.25f);
        Vector3 markPosition = city.ClampToWalkable(SignalPosition + new Vector3(7.5f, 0f, 1.55f), 0.25f);
        Vector3 nikaPosition = city.ClampToWalkable(SignalPosition + new Vector3(-5.4f, 0f, 5.0f), 0.25f);
        PlaceStationary(lida, lidaPosition);
        PlaceStationary(mark, markPosition);
        PlaceStationary(nika, nikaPosition);
        state.MarkBaseline = markPosition;
        state.HasMarkBaseline = true;
        state.ObservedDay = city.Progress.Day;

        if (IsPersistedFromEarlierDay(city.Progress))
            PlaceMarkNearLida(state);
    }

    public static bool IsConfigured(HeroProgress progress) => GetState(progress).Configured;

    public static NpcCharacter? SignalInteractionTarget(CityRenderer city, Vector3 playerPosition, float range)
    {
        SyncDay(city.Progress);
        State state = GetState(city.Progress);
        if (!state.Configured || state.Lida == null || state.SignalObserved) return null;

        FirstMemoryChapterProgress chapter = FirstMemoryChapterState.For(city.Progress);
        if (chapter.InvestigationCompleted || chapter.Branch != FirstMemoryBranch.None)
            return null;
        if (city.Progress.Ledger.TryGetEvent(FirstMemorySlice.EventId, out _) ||
            city.Progress.Ledger.TryGetEvent(FirstMemorySlice.RepairEventId, out _))
            return null;

        return Vector3.DistanceSquared(playerPosition, SignalPosition) <= range * range ? state.Lida : null;
    }

    public static bool IsSignalObserved(HeroProgress progress)
    {
        State state = GetState(progress);
        // Unit-level memory tests do not construct a spatial city; keep the old
        // direct narrative path valid there and gate only configured game worlds.
        if (!state.Configured) return true;
        return state.ObservedDay == progress.Day && state.SignalObserved;
    }

    public static bool MarkSignalObserved(HeroProgress progress)
    {
        State state = GetState(progress);
        if (state.ObservedDay != progress.Day)
        {
            state.ObservedDay = progress.Day;
            state.SignalObserved = false;
        }

        if (state.SignalObserved) return false;
        state.SignalObserved = true;
        return true;
    }

    internal static bool TryBringMarkToMeeting(HeroProgress progress, NpcCharacter lida)
    {
        State state = GetState(progress);
        if (!state.Configured || state.Mark == null) return false;
        PlaceStationary(state.Mark, MeetingPosition(lida));
        return true;
    }

    internal static void SyncDay(HeroProgress progress)
    {
        State state = GetState(progress);
        if (!state.Configured || state.ObservedDay == progress.Day) return;

        state.ObservedDay = progress.Day;
        state.SignalObserved = false;

        if (state.Mark == null || state.Lida == null) return;
        if (IsPersistedFromEarlierDay(progress))
            PlaceMarkNearLida(state);
        else if (state.HasMarkBaseline)
            PlaceStationary(state.Mark, state.MarkBaseline);
    }

    private static bool IsPersistedFromEarlierDay(HeroProgress progress)
    {
        return progress.Ledger.IsPersisted(FirstMemorySlice.EventId) &&
               progress.Ledger.TryGetEvent(FirstMemorySlice.EventId, out var memoryEvent) &&
               memoryEvent != null && progress.Day > memoryEvent.Day;
    }

    private static State GetState(HeroProgress progress) =>
        States.GetValue(progress, p => new State { ObservedDay = p.Day });

    private static void PlaceMarkNearLida(State state)
    {
        if (state.Lida == null || state.Mark == null) return;
        PlaceStationary(state.Mark, MeetingPosition(state.Lida));
    }

    private static Vector3 MeetingPosition(NpcCharacter lida) =>
        lida.Position + new Vector3(1.8f, 0f, 0.6f);

    private static void PlaceStationary(NpcCharacter npc, Vector3 position)
    {
        npc.Position = position;
        npc.HomePos = position;
        npc.WorkPos = position;
        npc.WakeHour = 0f;
        npc.WorkStart = 0f;
        npc.WorkEnd = 24f;
        npc.SleepHour = 24f;
        npc.State = NpcState.Working;
        npc.Velocity = Vector3.Zero;
    }
}
