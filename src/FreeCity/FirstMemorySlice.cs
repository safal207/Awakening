using System;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

public enum NarrativeRole
{
    None,
    Lida,
    Mark,
    Nika,
}

/// <summary>
/// First Memory narrative logic. A configured game city uses the three-morning
/// M2 chapter; isolated unit-level calls keep the original single-slice behavior
/// so M1 causal invariants remain independently testable.
/// </summary>
public static class FirstMemorySlice
{
    public const string EventId = "first_memory_lida_mark_meeting";
    public const string RepairEventId = "first_memory_repair_promise_kept";
    public const string LocationId = "tram_plaza";
    public const string InspectSignalActionId = "inspect_broken_crossing_signal";
    public const string RoutineRepairDayOneActionId = "routine_repair_first_morning";
    public const string HelpLidaActionId = "leave_signal_repair_to_help_lida";
    public const string RepairPromiseActionId = "finish_promised_signal_repair";
    public const string AcceptWitnessActionId = "mark_accepts_memory_witness";
    public const string NikaArchiveActionId = "nika_archives_consented_meeting";
    public const string TraceId = "dispatcher_note";
    public const string RepairTraceId = "maintenance_log";

    // Stable semantic actor id for the player in narrative records. NPC ids are non-negative.
    public const int HeroActorId = -1;

    private static NpcCharacter? _lida;
    private static NpcCharacter? _mark;
    private static NpcCharacter? _nika;

    internal static MemoryLedger LedgerFor(HeroProgress progress) => progress.Ledger;

    internal static void RegisterCharacter(NpcCharacter npc)
    {
        if (npc.NarrativeRole == NarrativeRole.Lida) _lida = npc;
        else if (npc.NarrativeRole == NarrativeRole.Mark) _mark = npc;
        else if (npc.NarrativeRole == NarrativeRole.Nika) _nika = npc;
    }

    internal static void ResetRegistryForTests()
    {
        _lida = null;
        _mark = null;
        _nika = null;
    }

    public static bool TryGetDialogue(
        NpcCharacter npc,
        HeroProgress progress,
        out (string npcLine, DialogueChoice[] choices) dialogue)
    {
        if (FirstMemorySpatial.IsConfigured(progress))
            return TryGetChapterDialogue(npc, progress, out dialogue);
        return TryGetLegacyDialogue(npc, progress, out dialogue);
    }

    public static bool ApplyChoice(NpcCharacter npc, DialogueChoice choice, HeroProgress progress)
    {
        if (FirstMemorySpatial.IsConfigured(progress))
            return ApplyChapterChoice(npc, choice, progress);
        return ApplyLegacyChoice(npc, choice, progress);
    }

    private static bool TryGetChapterDialogue(
        NpcCharacter npc,
        HeroProgress progress,
        out (string npcLine, DialogueChoice[] choices) dialogue)
    {
        dialogue = default;
        MemoryLedger ledger = progress.Ledger;
        FirstMemoryChapterProgress chapter = FirstMemoryChapterState.For(progress);
        int morning = chapter.Morning(progress.Day);

        if (npc.NarrativeRole == NarrativeRole.Lida)
        {
            if (chapter.IsOutcomeMorning(progress.Day))
            {
                if (chapter.Branch == FirstMemoryBranch.Meeting)
                {
                    dialogue = ledger.IsPersisted(EventId)
                        ? (
                            "Доброе утро. Марк уже прошёл мимо, и я назвала его по имени раньше, чем успела подумать. Я помню нашу встречу.",
                            new[]
                            {
                                new DialogueChoice("Значит, вчерашнее не исчезло.", 2, 3, 2, 2, 2, 2, 1),
                            })
                        : (
                            "Вчера я задержала рейс ради какой-то встречи. Помню чувство, но имя человека исчезло. Похоже, одного события было недостаточно.",
                            new[]
                            {
                                new DialogueChoice("Мы не будем записывать память за него.", 1, 2, 1, 1, 3, 1, 1),
                            });
                    return true;
                }

                if (chapter.Branch == FirstMemoryBranch.Repair)
                {
                    dialogue = ledger.IsPersisted(RepairEventId)
                        ? (
                            "Сигнал работает с самого утра. Ты закончил ремонт, как обещал, и журнал это сохранил. Марк для меня всё ещё просто садовник с площади.",
                            new[]
                            {
                                new DialogueChoice("Иногда обещание важнее красивой случайности.", 2, 4, 2, 1, 2, 3, 1),
                            })
                        : (
                            "Я помню, что мы решили закончить ремонт, но в журнале нет подтверждения. Не хочу выдавать ощущение за факт.",
                            new[]
                            {
                                new DialogueChoice("Тогда оставим это неподтверждённым.", 1, 2, 1, 1, 2, 2, 1),
                            });
                    return true;
                }
            }

            if (chapter.Branch == FirstMemoryBranch.Meeting)
            {
                if (ledger.IsPersisted(EventId))
                {
                    dialogue = (
                        "Марк согласился сохранить встречу. Я оставила запись у диспетчера. Теперь остаётся пережить ночь.",
                        new[] { new DialogueChoice("Увидимся утром.", 1, 2, 1, 1, 1, 1, 1) });
                    return true;
                }

                dialogue = (
                    "Марк успел выйти на площадь. Но моя задержка рейса ещё не делает его память нашей. Поговори с ним — решение должно быть его.",
                    new[] { new DialogueChoice("Я спрошу, а не буду решать за него.", 2, 2, 1, 1, 3, 2, 1) });
                return true;
            }

            if (chapter.Branch == FirstMemoryBranch.Repair)
            {
                dialogue = (
                    "Ремонт закончен и записан в журнал. Сегодня мы не свели людей вместе, зато я знаю, что могу доверять твоему обещанию.",
                    new[] { new DialogueChoice("Посмотрим, что останется утром.", 2, 3, 1, 1, 2, 2, 1) });
                return true;
            }

            if (morning == 1)
            {
                if (!FirstMemorySpatial.IsSignalObserved(progress))
                {
                    dialogue = (
                        "Сигнал у перехода снова погас. Можно быстро вернуть питание и идти дальше — или разобраться, почему контроллер каждый раз приходит к одному состоянию.",
                        new[]
                        {
                            new DialogueChoice(
                                "Проверить контакты и журнал ошибки.",
                                1, 2, 2, 3, 0, 1, 0,
                                InspectSignalActionId),
                            new DialogueChoice(
                                "Быстро восстановить сигнал и закончить маршрут.",
                                2, 2, 0, 0, 0, 2, 1,
                                RoutineRepairDayOneActionId),
                        });
                    return true;
                }

                if (chapter.InvestigationCompleted)
                {
                    dialogue = (
                        "Контроллер исправен, но журнал заканчивается на вчерашней эталонной записи. Сегодня не будем ломать расписание. Если утром поломка вернётся — это уже не случайность.",
                        new[] { new DialogueChoice("Вернусь утром и сравню.", 1, 2, 2, 2, 1, 1, 1) });
                    return true;
                }

                dialogue = (
                    "Сигнал работает. Я отметила обычный ремонт. Если завтра он снова погаснет в ту же минуту, придётся признать, что мы лечим не причину.",
                    new[] { new DialogueChoice("Тогда завтра проверим глубже.", 1, 2, 1, 2, 1, 1, 1) });
                return true;
            }

            // Morning 2+: investigation is required before the promise conflict.
            if (!chapter.InvestigationCompleted)
            {
                dialogue = (
                    "Он снова погас в ту же минуту. Вчера мы просто вернули питание. Теперь можно проверить, что именно Сверка возвращает назад.",
                    new[]
                    {
                        new DialogueChoice(
                            "Сравнить контроллер с вчерашним состоянием.",
                            1, 3, 3, 4, 0, 2, 1,
                            InspectSignalActionId),
                    });
                return true;
            }

            dialogue = (
                "Теперь ясно: обход из-за сигнала разводит людей по разным сторонам площади. Я могу задержать рейс, и Марк успеет сюда. Но ты обещал закончить ремонт до последнего трамвая. На оба решения времени нет.",
                new[]
                {
                    new DialogueChoice(
                        "Задержи рейс. Я проверю, что произойдёт, если Марк успеет сюда.",
                        3, 3, 1, 3, 3, 3, 3,
                        HelpLidaActionId),
                    new DialogueChoice(
                        "Я сдержу обещание и закончу ремонт вовремя.",
                        3, 5, 2, 1, 1, 4, 2,
                        RepairPromiseActionId),
                });
            return true;
        }

        if (npc.NarrativeRole == NarrativeRole.Mark &&
            chapter.Branch == FirstMemoryBranch.Meeting &&
            ledger.TryGetEvent(EventId, out var memoryEvent) && memoryEvent != null)
        {
            if (!ledger.IsPersisted(EventId))
            {
                dialogue = (
                    "Я обычно не оказываюсь здесь в это время. Если эту встречу собираются записать, я хочу сам решить, согласен ли помнить её завтра.",
                    new[]
                    {
                        new DialogueChoice(
                            "Если хочешь — согласись быть свидетелем. Без твоего решения записи не будет.",
                            3, 5, 2, 2, 4, 2, 2,
                            AcceptWitnessActionId),
                        new DialogueChoice("Не соглашайся ради нас. Это твоя память.", 1, 3, 0, 0, 4, 1, 2),
                    });
                return true;
            }

            dialogue = (
                "Я согласился. Пусть запись останется, но потому что я этого захотел, а не потому что меня вписали в чей-то план.",
                new[] { new DialogueChoice("Именно так.", 2, 3, 1, 1, 3, 1, 1) });
            return true;
        }

        if (npc.NarrativeRole == NarrativeRole.Nika)
        {
            if (!chapter.IsOutcomeMorning(progress.Day))
            {
                dialogue = (
                    "В архиве есть сегодняшние исправления и нет вчерашних. Я могу хранить факты, но не стану превращать чужую встречу в документ без согласия участников.",
                    new[] { new DialogueChoice("Запись не важнее человека.", 1, 3, 1, 2, 4, 1, 1) });
                return true;
            }

            if (chapter.Branch == FirstMemoryBranch.Meeting)
            {
                if (!ledger.IsPersisted(EventId) || !MeetingHasMarkConsent(ledger))
                {
                    dialogue = (
                        "У меня нет подтверждённого согласия Марка. Я не внесу встречу в архив только потому, что она кажется нам важной.",
                        new[] { new DialogueChoice("Тогда ничего не записывай.", 2, 4, 0, 0, 4, 2, 2) });
                    return true;
                }

                if (!chapter.NikaArchived)
                {
                    dialogue = (
                        "Согласие Марка есть, и диспетчерская запись пережила утро. Теперь я могу сохранить копию как свидетельство — не как чужую биографию.",
                        new[]
                        {
                            new DialogueChoice(
                                "Сохрани только факт встречи и согласие участников.",
                                2, 4, 2, 2, 4, 3, 2,
                                NikaArchiveActionId),
                        });
                    return true;
                }

                dialogue = (
                    "Копия сохранена: место, событие и согласие. Никаких догадок о том, что они должны чувствовать.",
                    new[] { new DialogueChoice("Этого достаточно.", 1, 3, 1, 1, 3, 1, 1) });
                return true;
            }

            if (chapter.Branch == FirstMemoryBranch.Repair)
            {
                dialogue = (
                    "В журнале есть завершённый ремонт и подпись Лиды. Это служебный факт, а не запись о чужой жизни. Его можно хранить без притворства, что встреча произошла.",
                    new[] { new DialogueChoice("Пусть архив различает факт и желание.", 2, 3, 2, 2, 3, 2, 1) });
                return true;
            }
        }

        return false;
    }

    private static bool ApplyChapterChoice(NpcCharacter npc, DialogueChoice choice, HeroProgress progress)
    {
        MemoryLedger ledger = progress.Ledger;
        FirstMemoryChapterProgress chapter = FirstMemoryChapterState.For(progress);

        if (choice.ActionId == InspectSignalActionId && npc.NarrativeRole == NarrativeRole.Lida)
        {
            chapter.MarkInvestigation(progress.Day);
            FirstMemorySpatial.MarkSignalObserved(progress);
            return true;
        }

        if (choice.ActionId == RoutineRepairDayOneActionId && npc.NarrativeRole == NarrativeRole.Lida)
        {
            bool changed = chapter.MarkRoutineRepair(progress.Day);
            FirstMemorySpatial.MarkSignalObserved(progress);
            return changed;
        }

        if (choice.ActionId == HelpLidaActionId && npc.NarrativeRole == NarrativeRole.Lida)
        {
            if (!CanChooseBranch(chapter, progress.Day)) return false;
            var memoryEvent = new MemoryEvent(
                Id: EventId,
                Day: progress.Day,
                Kind: "shared_meeting",
                LocationId: LocationId,
                ActorId: HeroActorId,
                ChoiceId: HelpLidaActionId,
                Description: "Лида задержала трамвай, и Марк успел выйти на площадь.");
            if (!ledger.TryRecordEvent(memoryEvent)) return false;
            if (!chapter.TryChooseBranch(FirstMemoryBranch.Meeting, progress.Day)) return false;
            BringMarkToMeeting(npc, progress);
            return true;
        }

        if (choice.ActionId == RepairPromiseActionId && npc.NarrativeRole == NarrativeRole.Lida)
        {
            if (!CanChooseBranch(chapter, progress.Day)) return false;
            var repairEvent = new MemoryEvent(
                Id: RepairEventId,
                Day: progress.Day,
                Kind: "promise_kept",
                LocationId: LocationId,
                ActorId: HeroActorId,
                ChoiceId: RepairPromiseActionId,
                Description: "Герой закончил обещанный ремонт сигнала до последнего рейса.");
            if (!ledger.TryRecordEvent(repairEvent)) return false;
            if (!chapter.TryChooseBranch(FirstMemoryBranch.Repair, progress.Day)) return false;
            return ledger.TryCreateAnchor(
                RepairEventId,
                RepairTraceId,
                ResidentIdentity.GetPersistentId(npc),
                WitnessConsent.Accepted);
        }

        if (choice.ActionId == AcceptWitnessActionId && npc.NarrativeRole == NarrativeRole.Mark &&
            chapter.Branch == FirstMemoryBranch.Meeting)
        {
            return ledger.TryCreateAnchor(
                EventId,
                TraceId,
                ResidentIdentity.GetPersistentId(npc),
                WitnessConsent.Accepted);
        }

        if (choice.ActionId == NikaArchiveActionId && npc.NarrativeRole == NarrativeRole.Nika &&
            chapter.IsOutcomeMorning(progress.Day) && chapter.Branch == FirstMemoryBranch.Meeting &&
            ledger.IsPersisted(EventId) && MeetingHasMarkConsent(ledger))
        {
            return chapter.MarkNikaArchived();
        }

        return false;
    }

    private static bool CanChooseBranch(FirstMemoryChapterProgress chapter, int day) =>
        chapter.Branch == FirstMemoryBranch.None && chapter.InvestigationCompleted && chapter.Morning(day) >= 2;

    private static bool MeetingHasMarkConsent(MemoryLedger ledger)
    {
        if (_mark == null || !ledger.TryGetAnchor(EventId, out var anchor) || anchor == null)
            return false;
        int markId = ResidentIdentity.GetPersistentId(_mark);
        return anchor.Witnesses.TryGetValue(markId, out WitnessConsent consent) &&
               consent == WitnessConsent.Accepted;
    }

    // Legacy single-slice behavior for isolated M1 unit tests without a configured city.
    private static bool TryGetLegacyDialogue(
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
                dialogue = (
                    "Сигнал у остановки снова погас. Если я задержу трамвай, у тебя будет минута разобраться — но маршрут собьётся.",
                    new[]
                    {
                        new DialogueChoice(
                            "Я помогу. Задержи трамвай на минуту.",
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
                    new[] { new DialogueChoice("Пусть это будет нашим следом.", 2, 3, 1, 1, 2, 1, 1) });
                return true;
            }

            dialogue = (
                "Марк успел выйти на площадь. Но одной встречи мало — нужен кто-то, кто сам согласится её запомнить.",
                new[] { new DialogueChoice("Я поговорю с ним.", 2, 2, 1, 1, 2, 2, 1) });
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
                new[] { new DialogueChoice("Увидим утром.", 1, 2, 1, 1, 1, 1, 1) });
            return true;
        }

        return false;
    }

    private static bool ApplyLegacyChoice(NpcCharacter npc, DialogueChoice choice, HeroProgress progress)
    {
        MemoryLedger ledger = progress.Ledger;

        if (choice.ActionId == InspectSignalActionId && npc.NarrativeRole == NarrativeRole.Lida)
            return FirstMemorySpatial.MarkSignalObserved(progress);

        if (choice.ActionId == HelpLidaActionId && npc.NarrativeRole == NarrativeRole.Lida)
        {
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
                ResidentIdentity.GetPersistentId(npc),
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
