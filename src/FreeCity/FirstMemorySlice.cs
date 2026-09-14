using System;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

public enum NarrativeRole
{
    None,
    Lida,
    Mark,
}

/// <summary>
/// The smallest playable causal chain for the first memory:
/// player observation -> choice -> concrete event -> voluntary witness -> memory anchor -> next-day consequence.
/// This deliberately stays narrow instead of becoming a quest framework.
/// </summary>
public static class FirstMemorySlice
{
    public const string EventId = "first_memory_lida_mark_meeting";
    public const string LocationId = "tram_plaza";
    public const string InspectSignalActionId = "inspect_broken_crossing_signal";
    public const string HelpLidaActionId = "leave_signal_repair_to_help_lida";
    public const string AcceptWitnessActionId = "mark_accepts_memory_witness";
    public const string TraceId = "dispatcher_note";

    // Stable semantic actor id for the player in narrative records. NPC ids are non-negative.
    public const int HeroActorId = -1;

    private static NpcCharacter? _lida;
    private static NpcCharacter? _mark;

    internal static MemoryLedger LedgerFor(HeroProgress progress) => progress.Ledger;

    internal static void RegisterCharacter(NpcCharacter npc)
    {
        if (npc.NarrativeRole == NarrativeRole.Lida) _lida = npc;
        else if (npc.NarrativeRole == NarrativeRole.Mark) _mark = npc;
    }

    internal static void ResetRegistryForTests()
    {
        _lida = null;
        _mark = null;
    }

    public static bool TryGetDialogue(
        NpcCharacter npc,
        HeroProgress progress,
        out (string npcLine, DialogueChoice[] choices) dialogue)
    {
        dialogue = default;
        MemoryLedger ledger = progress.Ledger;

        if (npc.NarrativeRole == NarrativeRole.Lida)
        {
            if (ledger.IsPersisted(EventId) &&
                ledger.TryGetEvent(EventId, out var persistedEvent) &&
                persistedEvent != null && progress.Day > persistedEvent.Day)
            {
                dialogue = (
                    "Я уже поздоровалась с Марком по имени. Не понимаю почему, но я помню нашу вчерашнюю встречу.",
                    new[]
                    {
                        new DialogueChoice("Ты правда его помнишь?", 2, 3, 2, 1, 2, 1, 1),
                        new DialogueChoice("Значит, записка пережила утро.", 1, 3, 3, 2, 1, 2, 1),
                    });
                return true;
            }

            if (!ledger.TryGetEvent(EventId, out _))
            {
                if (!FirstMemorySpatial.IsSignalObserved(progress))
                {
                    dialogue = (
                        "Сигнал у перехода погас. Корпус цел, но индикаторы молчат. Сначала нужно понять, что именно сбилось.",
                        new[]
                        {
                            new DialogueChoice(
                                "Осмотреть контакты и журнал ошибки.",
                                1, 2, 2, 3, 0, 1, 0,
                                InspectSignalActionId),
                            new DialogueChoice("Не сейчас.", 0, 0, 0, 0, 0, 0, 0),
                        });
                    return true;
                }

                dialogue = (
                    "Питание есть, но контроллер снова вернулся в исходное состояние. Если я задержу трамвай, у тебя будет минута проверить, кого эта поломка разводит по разным маршрутам.",
                    new[]
                    {
                        new DialogueChoice(
                            "Я проверю. Задержи трамвай на минуту.",
                            4, 4, 1, 2, 2, 3, 2,
                            HelpLidaActionId),
                        new DialogueChoice("Сначала закончу обычный маршрут.", 0, -1, 0, 0, 0, 1, 0),
                    });
                return true;
            }

            if (ledger.IsPersisted(EventId))
            {
                dialogue = (
                    "Я оставила запись о встрече в журнале диспетчера. Посмотрим, будет ли она здесь утром.",
                    new[]
                    {
                        new DialogueChoice("Пусть это будет нашим следом.", 2, 3, 1, 1, 2, 1, 1),
                    });
                return true;
            }

            dialogue = (
                "Марк успел выйти на площадь. Но одной встречи мало — нужен кто-то, кто сам согласится её запомнить.",
                new[]
                {
                    new DialogueChoice("Я поговорю с ним.", 2, 2, 1, 1, 2, 2, 1),
                });
            return true;
        }

        if (npc.NarrativeRole == NarrativeRole.Mark &&
            ledger.TryGetEvent(EventId, out var memoryEvent) && memoryEvent != null)
        {
            if (!ledger.IsPersisted(EventId))
            {
                dialogue = (
                    "Странно. Я обычно не оказываюсь на этой площади в это время. Лида попросила оставить запись о встрече — только если я сам согласен.",
                    new[]
                    {
                        new DialogueChoice(
                            "Если хочешь — запомни это утро. Оставим запись у диспетчера.",
                            3, 5, 2, 2, 4, 2, 2,
                            AcceptWitnessActionId),
                        new DialogueChoice("Нет. Это должно остаться твоим выбором.", 1, 2, 0, 0, 3, 1, 1),
                    });
                return true;
            }

            dialogue = (
                "Запись сделана. Но я не знаю, останется ли от неё что-нибудь после ночи.",
                new[]
                {
                    new DialogueChoice("Увидим утром.", 1, 2, 1, 1, 1, 1, 1),
                });
            return true;
        }

        return false;
    }

    public static bool ApplyChoice(NpcCharacter npc, DialogueChoice choice, HeroProgress progress)
    {
        MemoryLedger ledger = progress.Ledger;

        if (choice.ActionId == InspectSignalActionId && npc.NarrativeRole == NarrativeRole.Lida)
            return FirstMemorySpatial.MarkSignalObserved(progress);

        if (choice.ActionId == HelpLidaActionId && npc.NarrativeRole == NarrativeRole.Lida)
        {
            if (!FirstMemorySpatial.IsSignalObserved(progress)) return false;

            var memoryEvent = new MemoryEvent(
                Id: EventId,
                Day: progress.Day,
                Kind: "shared_meeting",
                LocationId: LocationId,
                ActorId: HeroActorId,
                ChoiceId: HelpLidaActionId,
                Description: "Лида задержала трамвай, и Марк успел выйти на площадь.");

            if (!ledger.TryRecordEvent(memoryEvent)) return false;
            BringMarkToMeeting(npc, progress);
            return true;
        }

        if (choice.ActionId == AcceptWitnessActionId && npc.NarrativeRole == NarrativeRole.Mark)
        {
            return ledger.TryCreateAnchor(
                EventId,
                TraceId,
                npc.Id,
                WitnessConsent.Accepted);
        }

        return false;
    }

    private static void BringMarkToMeeting(NpcCharacter lida, HeroProgress progress)
    {
        if (FirstMemorySpatial.TryBringMarkToMeeting(progress, lida)) return;
        if (_mark == null) return;

        Vector3 meeting = lida.Position + new Vector3(1.8f, 0f, 0.6f);
        meeting.Y = lida.Position.Y;
        _mark.Position = meeting;
        _mark.HomePos = meeting;
        _mark.WorkPos = meeting;
        _mark.WakeHour = 0f;
        _mark.WorkStart = 0f;
        _mark.WorkEnd = 24f;
        _mark.SleepHour = 24f;
        _mark.State = NpcState.Relaxing;
        _mark.Velocity = Vector3.Zero;
    }
}
