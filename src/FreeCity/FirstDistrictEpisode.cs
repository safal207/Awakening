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
}

public sealed class FirstDistrictEpisode
{
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
        if (Phase == DistrictPhase.RepairPlanned) Phase = DistrictPhase.Repaired;
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
        return true;
    }

    public void EndDay()
    {
        if (!Finished) Phase = DistrictPhase.Missed;
    }

    public void DeclineInvitation()
    {
        if (Phase != DistrictPhase.WaitingForMark) return;
        InvitationWithdrawn = true;
        Phase = DistrictPhase.Missed;
    }

    // The authored route stays on one unobstructed sidewalk/crossing; the city
    // collision resolver still decides each step. No teleport may create a meeting.
    public void Update(CityRenderer city, float dt)
    {
        if (city.Progress.Day != 1) return;
        if (city.TimeOfDay >= 22f && Phase is DistrictPhase.Routine or DistrictPhase.RepairPlanned or DistrictPhase.DelayPlanned)
            Phase = DistrictPhase.Missed;
        if (Phase is DistrictPhase.WaitingForMark or DistrictPhase.MarkOnWay && city.TimeOfDay >= Deadline)
            Phase = DistrictPhase.Missed;
        var mark = city.Npcs[2];
        if (Phase != DistrictPhase.MarkOnWay) return;
        Vector3 delta = MarkMeeting - mark.Position;
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
        if (Vector3.Distance(mark.Position, MarkMeeting) > 0.4f ||
            !Near(city.Npcs[1].Position, Stop, 1f) || city.IsInside ||
            city.Player == null || !Near(city.Player.Position, Stop, 4f)) return;
        Phase = DistrictPhase.Met;
        mark.Velocity = Vector3.Zero;
        mark.AnimBlend = 0;
        FirstDistrictStory.RegisterMeetingCandidate(city.Npcs[1], city.Progress.Day);
    }

    public DistrictInteraction Detect(Vector3 player, int day)
    {
        if (Near(player, Signal, 2f)) return DistrictInteraction.Signal;
        if (Finished && Near(player, Trace, 2f)) return DistrictInteraction.Trace;
        if (day == 1 && Finished && Near(player, Rest, 2f)) return DistrictInteraction.Rest;
        return DistrictInteraction.None;
    }

    public string SignalText(int day) => day > 1 ? "Сигнал работает. Новый рейс по расписанию." : Phase switch
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

    public (Vector3 position, string name) Objective(int day) => day > 1 ? (Trace, "Журнал у остановки") : Phase switch
    {
        DistrictPhase.Routine => (Stop, "Лида"),
        DistrictPhase.RepairPlanned or DistrictPhase.DelayPlanned => (Signal, "Сигнал перехода"),
        DistrictPhase.WaitingForMark => (MarkStart, $"Марк: {Math.Max(0,(int)((Deadline-CurrentTime)/0.03f))} с"),
        DistrictPhase.MarkOnWay => (Stop, $"Остановка: {Math.Max(0,(int)((Deadline-CurrentTime)/0.03f))} с"),
        DistrictPhase.Met when MemoryRuntime.Current.FindAnchor(FirstDistrictStory.MeetingEventId)?.Witnesses.Exists(w => w.UnderstoodEvent) != true => (MarkMeeting, "Решение Марка"),
        _ => (Rest, "Дождаться утра"),
    };

    public DistrictEpisodeSaveData Snapshot() => new() { Phase = Phase, Deadline = Deadline, MarkTravel = MarkTravel, MarkRefused = MarkRefused, InvitationWithdrawn = InvitationWithdrawn };

    public void Restore(DistrictEpisodeSaveData? data, MemoryLedger ledger)
    {
        if (data == null)
        {
            Phase = ledger.FindEvent(FirstDistrictStory.MeetingEventId) != null ? DistrictPhase.Met : DistrictPhase.Routine;
            MarkTravel = Phase == DistrictPhase.Met ? 1f : 0f;
            Deadline = 0f;
            MarkRefused = InvitationWithdrawn = false;
            return;
        }
        if (!Enum.IsDefined(data.Phase) || !float.IsFinite(data.Deadline) || !float.IsFinite(data.MarkTravel) ||
            data.Deadline < 0f || data.Deadline > 24f || data.MarkTravel < 0f || data.MarkTravel > 1f)
            throw new System.IO.InvalidDataException("Invalid first district episode state.");
        Phase = data.Phase;
        Deadline = data.Deadline;
        MarkTravel = data.MarkTravel;
        MarkRefused = data.MarkRefused;
        InvitationWithdrawn = data.InvitationWithdrawn;
    }

    internal static bool Near(Vector3 a, Vector3 b, float radius = 2.5f) => Vector3.DistanceSquared(a, b) <= radius * radius;
}
