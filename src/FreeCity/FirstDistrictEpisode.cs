using System;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

public enum DistrictPhase { Routine, RepairPlanned, DelayPlanned, WaitingForMark, MarkOnWay, Met, Repaired, Missed }
public enum DistrictInteraction { None, Signal, Trace, Rest }

public sealed class DistrictEpisodeSaveData
{
    public DistrictPhase Phase { get; set; }
    public float Deadline { get; set; }
    public float MarkTravel { get; set; }
    public bool MarkRefused { get; set; }
    public bool InvitationWithdrawn { get; set; }
    public float? DepartureSeconds { get; set; }
}

public sealed class FirstDistrictEpisode
{
    public const float DepartureDuration = 24f;
    public const float DepartureDwell = 2f;
    public static readonly Vector3 Signal = new(11.5f, 0.12f, 11.5f);
    public static readonly Vector3 Stop = new(5.5f, 0.12f, 11.5f);
    public static readonly Vector3 MarkStart = new(34f, 0.12f, 11.5f);
    public static readonly Vector3 MarkMeeting = new(7f, 0.12f, 11.5f);
    public static readonly Vector3 Trace = new(-3f, 0.12f, 11.5f);
    public static readonly Vector3 Rest = new(-10f, 0.12f, 11.5f);
    public DistrictPhase Phase { get; private set; }
    public float Deadline { get; private set; }
    public float MarkTravel { get; private set; }
    public bool MarkRefused { get; private set; }
    public bool InvitationWithdrawn { get; private set; }
    public float DepartureSeconds { get; private set; } = -1f;
    public bool TramBlocked { get; private set; }
    public bool TramGone => DepartureSeconds >= DepartureDuration;
    public bool TramDeparting => DepartureSeconds >= 0f && !TramGone;
    public Vector3 TramOffset => OffsetAt(DepartureSeconds);
    public float CurrentTime { get; internal set; } = 8f;
    public bool Finished => Phase is DistrictPhase.Met or DistrictPhase.Repaired or DistrictPhase.Missed;
    public Vector3 MarkPosition => Vector3.Lerp(MarkStart, MarkMeeting, MarkTravel);

    public bool Plan(bool delay, int day)
    {
        if (day != 1 || Phase != DistrictPhase.Routine) return false;
        Phase = delay ? DistrictPhase.DelayPlanned : DistrictPhase.RepairPlanned;
        return true;
    }

    public bool ActivateSignal(Vector3 player, float time, int day)
    {
        if (day != 1 || !Near(player, Signal) || !float.IsFinite(time) || time >= 22f) return false;
        if (Phase == DistrictPhase.RepairPlanned)
        {
            Phase = DistrictPhase.Repaired;
            DepartureSeconds = 0f;
        }
        else if (Phase == DistrictPhase.DelayPlanned)
        {
            Phase = DistrictPhase.WaitingForMark;
            Deadline = Math.Min(23.5f, time + 1.8f);
        }
        else return false;
        return true;
    }

    public bool Invite(NpcCharacter mark, int day, float time)
    {
        if (day != 1 || Phase != DistrictPhase.WaitingForMark || time >= Deadline) return false;
        MarkRefused = mark.Trust < 8f || mark.Friendliness < 15f;
        Phase = MarkRefused ? DistrictPhase.Missed : DistrictPhase.MarkOnWay;
        if (MarkRefused) DepartureSeconds = 0f;
        return true;
    }

    public void EndDay()
    {
        if (!Finished) Phase = DistrictPhase.Missed;
        DepartureSeconds = DepartureDuration;
        TramBlocked = false;
    }

    public void DeclineInvitation()
    {
        if (Phase != DistrictPhase.WaitingForMark) return;
        InvitationWithdrawn = true;
        Phase = DistrictPhase.Missed;
        DepartureSeconds = 0f;
    }

    // The authored route stays on one unobstructed sidewalk/crossing; the city
    // collision resolver still decides each step. No teleport may create a meeting.
    public void Update(CityRenderer city, float dt)
    {
        dt = float.IsFinite(dt) ? Math.Clamp(dt, 0f, 0.1f) : 0f;
        var lida = city.Npcs[1];
        var mark = city.Npcs[2];
        Idle(lida);
        Idle(mark);
        if (city.Progress.Day != 1)
        {
            Idle(city.Npcs[3]);
            if (city.Player != null && Near(city.Player.Position, city.Npcs[3].Position, 5f))
                Face(city.Npcs[3], city.Player.Position, dt);
            if (MemoryRuntime.Current.HasPersisted(FirstDistrictStory.MeetingEventId))
            {
                Face(lida, mark.Position, dt);
                Face(mark, lida.Position, dt);
            }
            return;
        }
        if (city.TimeOfDay >= 22f && Phase is DistrictPhase.Routine or DistrictPhase.RepairPlanned or DistrictPhase.DelayPlanned)
            Phase = DistrictPhase.Missed;
        if (Phase is DistrictPhase.WaitingForMark or DistrictPhase.MarkOnWay && city.TimeOfDay >= Deadline)
            Phase = DistrictPhase.Missed;
        if (Phase == DistrictPhase.MarkOnWay)
        {
            WalkMark(city, MarkMeeting, dt);
            if (Vector3.Distance(mark.Position, MarkMeeting) <= 0.4f &&
                Near(lida.Position, Stop, 1f) && !city.IsInside &&
                city.Player != null && Near(city.Player.Position, Stop, 4f))
            {
                Phase = DistrictPhase.Met;
                Idle(mark);
                FirstDistrictStory.RegisterMeetingCandidate(lida, city.Progress.Day);
            }
        }
        else if (Phase == DistrictPhase.Missed && MarkTravel > 0f)
            WalkMark(city, MarkStart, dt);

        if (Finished && DepartureSeconds < 0f) DepartureSeconds = 0f;
        UpdateDeparture(city, dt);
        Vector3 tramFront = new Vector3(-1, 1, 17) + TramOffset;
        Face(lida, Phase is DistrictPhase.Met or DistrictPhase.MarkOnWay ? mark.Position :
            TramGone ? Signal : tramFront, dt);
        if (Phase == DistrictPhase.Met)
        {
            Face(mark, lida.Position, dt);
            mark.Greeting = GestureAt(DepartureSeconds);
        }
        lida.Greeting = GestureAt(DepartureSeconds);
    }

    private void WalkMark(CityRenderer city, Vector3 target, float dt)
    {
        var mark = city.Npcs[2];
        Vector3 delta = target - mark.Position;
        float distance = delta.Length;
        if (distance > 0.05f && dt > 0f)
        {
            Vector3 before = mark.Position;
            mark.Position = city.ClampToWalkable(before + delta / distance * Math.Min(distance, dt * 2.1f), 0.3f);
            mark.Velocity = (mark.Position - before) / dt;
            mark.AnimBlend = Math.Clamp(mark.Velocity.Length / 2.1f, 0f, 1f);
            mark.AnimPhase += mark.Velocity.Length * 3.5f * dt;
            if (mark.State != NpcState.Aware) mark.State = NpcState.Walking;
            mark.Rotation = mark.TargetRotation = MathF.Atan2(delta.X, delta.Z);
            MarkTravel = Math.Clamp((MarkStart.X - mark.Position.X) / (MarkStart.X - MarkMeeting.X), 0f, 1f);
        }
    }

    private void UpdateDeparture(CityRenderer city, float dt)
    {
        if (dt <= 0f) return;
        TramBlocked = false;
        if (!TramDeparting) return;
        float next = Math.Min(DepartureDuration, DepartureSeconds + dt);
        Vector3 offset = OffsetAt(next);
        if (offset.X < TramOffset.X)
        {
            // Test the whole swept body, not just the next position: no tunnelling
            // through a person on a slow frame and no pushing them into scenery.
            Box2 current = DistrictScene.TramBounds(this);
            float left = DistrictScene.ParkedTramBounds.Min.X + offset.X;
            for (int i = 0; i < city.Npcs.Count; i++)
            {
                var npc = city.Npcs[i];
                if (city.IsInside && npc == city.Player) continue;
                Vector2 p = npc.Position.Xz;
                if (p.X > left - 0.5f && p.X < current.Max.X + 0.5f &&
                    p.Y > current.Min.Y - 0.5f && p.Y < current.Max.Y + 0.5f)
                {
                    TramBlocked = true;
                    return;
                }
            }
        }
        DepartureSeconds = next;
    }

    private static Vector3 OffsetAt(float seconds)
    {
        float moving = Math.Clamp(seconds - DepartureDwell, 0f, DepartureDuration - DepartureDwell);
        float accelerating = Math.Min(3f, moving);
        float distance = 1.25f * accelerating * accelerating + Math.Max(0f, moving - 3f) * 7.5f;
        return new Vector3(-distance, 0, 0);
    }

    private static float GestureAt(float seconds) => seconds >= 0f && seconds < 4f ? MathF.Sin(seconds * MathF.PI / 4f) : 0f;

    private static void Idle(NpcCharacter npc)
    {
        npc.Velocity = Vector3.Zero;
        npc.AnimBlend = npc.Greeting = 0f;
        if (npc.State != NpcState.Aware) npc.State = NpcState.Relaxing;
    }

    private static void Face(NpcCharacter npc, Vector3 target, float dt)
    {
        Vector3 delta = target - npc.Position;
        if (delta.Xz.LengthSquared < 0.01f) return;
        npc.TargetRotation = MathF.Atan2(delta.X, delta.Z);
        float angle = MathF.Atan2(MathF.Sin(npc.TargetRotation - npc.Rotation), MathF.Cos(npc.TargetRotation - npc.Rotation));
        npc.Rotation += angle * (1f - MathF.Exp(-5f * dt));
    }

    public DistrictInteraction Detect(Vector3 player, int day)
    {
        if (Near(player, Signal, 2f)) return DistrictInteraction.Signal;
        if (Finished && Near(player, Trace, 2f)) return DistrictInteraction.Trace;
        if (Finished && Near(player, Rest, 2f)) return DistrictInteraction.Rest;
        return DistrictInteraction.None;
    }

    public string SignalText(int day) => day > 1 ? "Сигнал работает. Новый рейс по расписанию." : TramBlocked ?
        "Рейс ждёт, пока путь освободится." : TramDeparting ? "Рейс отправляется. Не выходи на пути." : Phase switch
    {
        DistrictPhase.RepairPlanned => "Починить сигнал",
        DistrictPhase.DelayPlanned => "Включить удержание рейса",
        DistrictPhase.Repaired => "Сигнал исправен. Рейс ушёл без задержки.",
        DistrictPhase.WaitingForMark or DistrictPhase.MarkOnWay => "Рейс удерживается. Лида ждёт у остановки.",
        DistrictPhase.Met => "Лида и Марк встретились у остановки.",
        DistrictPhase.Missed => "Рейс ушёл. Встреча не состоялась.",
        _ => "Сигнал неисправен. Лида ждёт у остановки.",
    };

    public string TraceText(int day)
    {
        if (day >= 2 && DistrictArchiveStory.TraceText(day) is string archive) return archive;
        if (Phase == DistrictPhase.Repaired) return "Журнал: сигнал исправлен, рейс отправлен вовремя. Встречи не было.";
        if (Phase == DistrictPhase.Missed && InvitationWithdrawn) return "Журнал: герой отозвал приглашение. Марк не принимал решения о встрече.";
        if (Phase == DistrictPhase.Missed) return MarkRefused
            ? "Журнал: Марк отказался идти. Лида отпустила рейс."
            : "Журнал: время вышло. Встреча не состоялась.";
        if (day == 1) return "Журнал: рейс задержан. Лида и Марк встретились. Решение о памяти ещё впереди.";
        return MemoryRuntime.Current.HasPersisted(FirstDistrictStory.MeetingEventId)
            ? "На полях две подписи: Лида и Марк. Они помнят вчерашнюю встречу."
            : "В журнале осталась задержка, но имена стёрты. Память не пережила утро.";
    }

    public (Vector3 position, string name) Objective(int day) => day > 1 ? DistrictArchiveStory.Objective(day) : Phase switch
    {
        DistrictPhase.Routine => (Stop, "Лида"),
        DistrictPhase.RepairPlanned or DistrictPhase.DelayPlanned => (Signal, "Сигнал перехода"),
        DistrictPhase.WaitingForMark => (MarkStart, $"Марк: {Math.Max(0,(int)((Deadline-CurrentTime)/0.03f))} с"),
        DistrictPhase.MarkOnWay => (Stop, $"Остановка: {Math.Max(0,(int)((Deadline-CurrentTime)/0.03f))} с"),
        DistrictPhase.Met when MemoryRuntime.Current.FindAnchor(FirstDistrictStory.MeetingEventId)?.Witnesses.Exists(w => w.UnderstoodEvent) != true => (MarkMeeting, "Решение Марка"),
        _ => (Rest, "Дождаться утра"),
    };

    public DistrictEpisodeSaveData Snapshot() => new() { Phase = Phase, Deadline = Deadline, MarkTravel = MarkTravel, MarkRefused = MarkRefused, InvitationWithdrawn = InvitationWithdrawn, DepartureSeconds = DepartureSeconds };

    public void Restore(DistrictEpisodeSaveData? data, MemoryLedger ledger)
    {
        if (data == null)
        {
            Phase = ledger.FindEvent(FirstDistrictStory.MeetingEventId) != null ? DistrictPhase.Met : DistrictPhase.Routine;
            MarkTravel = Phase == DistrictPhase.Met ? 1f : 0f;
            Deadline = 0f;
            MarkRefused = InvitationWithdrawn = false;
            DepartureSeconds = Phase == DistrictPhase.Met ? 0f : -1f;
            TramBlocked = false;
            return;
        }
        if (!Enum.IsDefined(data.Phase) || !float.IsFinite(data.Deadline) || !float.IsFinite(data.MarkTravel) ||
            data.Deadline < 0f || data.Deadline > 24f || data.MarkTravel < 0f || data.MarkTravel > 1f ||
            data.DepartureSeconds is float departure && (!float.IsFinite(departure) || departure < -1f || departure > DepartureDuration ||
                departure < 0f && departure != -1f || departure >= 0f && data.Phase is not (DistrictPhase.Met or DistrictPhase.Repaired or DistrictPhase.Missed)))
            throw new System.IO.InvalidDataException("Invalid first district episode state.");
        Phase = data.Phase;
        Deadline = data.Deadline;
        MarkTravel = data.MarkTravel;
        MarkRefused = data.MarkRefused;
        InvitationWithdrawn = data.InvitationWithdrawn;
        // Older saves already removed a repaired/missed tram. Do not resurrect it.
        DepartureSeconds = data.DepartureSeconds ?? (Phase is DistrictPhase.Repaired or DistrictPhase.Missed ? DepartureDuration :
            Phase == DistrictPhase.Met ? 0f : -1f);
        TramBlocked = false;
    }

    internal static bool Near(Vector3 a, Vector3 b, float radius = 2.5f) => Vector3.DistanceSquared(a, b) <= radius * radius;
}
