using System.Collections.Generic;

namespace Probuzhdenie.FreeCity;

public static class FirstDistrictStory
{
    public const string LidaName = "Лида";
    public const string MarkName = "Марк";
    public const string NikaName = "Ника";
    public const string MeetingEventId = "district1.lida-mark.meeting";

    private const string LidaDelayAction = "district1.lida.delay";
    private const string LidaRepairAction = "district1.lida.repair";
    private const string MarkOfferWitnessAction = "district1.mark.offer-witness";
    private const string MarkLeaveAction = "district1.mark.leave";
    private const string MarkInviteAction = "district1.mark.invite";
    private const string MarkDeclineAction = "district1.mark.decline-invitation";

    public static bool CanApplyChoice(NpcCharacter npc, DialogueChoice choice, HeroProgress progress)
    {
        var episode = progress.DistrictEpisode;
        return choice.ActionId switch
        {
            LidaDelayAction or LidaRepairAction => npc.Name == LidaName && progress.Day == 1 && episode.Phase == DistrictPhase.Routine && episode.CurrentTime < 22f,
            MarkInviteAction or MarkDeclineAction => npc.Name == MarkName && progress.Day == 1 && episode.Phase == DistrictPhase.WaitingForMark && episode.CurrentTime < episode.Deadline,
            MarkOfferWitnessAction or MarkLeaveAction => npc.Name == MarkName && progress.Day == 1 && episode.Phase == DistrictPhase.Met &&
                FindMarkWitness(MemoryRuntime.Current.FindAnchor(MeetingEventId), npc.Id)?.UnderstoodEvent == false,
            _ => true,
        };
    }

    public static void ApplyIdentity(NpcCharacter npc)
    {
        int relative = npc.Id - MemoryRuntime.HeroId;
        npc.Name = relative switch
        {
            1 => LidaName,
            2 => MarkName,
            3 => NikaName,
            _ => npc.Name,
        };
    }

    public static bool TryGetDialogue(NpcCharacter npc, HeroProgress progress, out string line, out DialogueChoice[] choices)
    {
        line = "";
        choices = System.Array.Empty<DialogueChoice>();

        if (npc.Name == LidaName)
            return TryGetLidaDialogue(progress, out line, out choices);
        if (npc.Name == MarkName)
            return TryGetMarkDialogue(npc, progress, out line, out choices);
        return false;
    }

    public static void ApplyChoice(NpcCharacter npc, DialogueChoice choice, HeroProgress progress)
    {
        switch (choice.ActionId)
        {
            case LidaDelayAction:
                progress.DistrictEpisode.Plan(true, progress.Day);
                break;
            case LidaRepairAction:
                progress.DistrictEpisode.Plan(false, progress.Day);
                break;
            case MarkInviteAction:
                progress.DistrictEpisode.Invite(npc, progress.Day, progress.DistrictEpisode.CurrentTime);
                break;
            case MarkDeclineAction:
                progress.DistrictEpisode.DeclineInvitation();
                break;
            case MarkOfferWitnessAction:
                RecordMarkDecision(npc);
                break;
            case MarkLeaveAction:
                var witness = FindMarkWitness(MemoryRuntime.Current.FindAnchor(MeetingEventId), npc.Id);
                if (witness != null)
                {
                    witness.UnderstoodEvent = true;
                    witness.Consented = false;
                    witness.ConsentReason = "Участник не вовлечён в сохранение памяти по решению героя.";
                }
                break;
        }
    }

    private static bool TryGetLidaDialogue(HeroProgress progress, out string line, out DialogueChoice[] choices)
    {
        var episode = progress.DistrictEpisode;
        if (episode.Phase is DistrictPhase.Repaired or DistrictPhase.Missed)
        {
            line = episode.Phase == DistrictPhase.Repaired ? "Сигнал исправен, рейс ушёл вовремя. Сегодня мы с Марком не встретились." :
                episode.InvitationWithdrawn ? "Ты решил не приглашать Марка. Тогда я отпускаю рейс." :
                episode.MarkRefused ? "Марк не захотел прийти. Я уважаю его решение." : "Время вышло. Я отпустила рейс. Марк так и не пришёл.";
            choices = new[] { new DialogueChoice("Понимаю.", 0, 0, 0, 0, 0, 0, 0, RewardId: "district1.lida.outcome") };
            return true;
        }
        if (progress.Day == 1 && episode.Phase != DistrictPhase.Routine && episode.Phase != DistrictPhase.Met)
        {
            line = episode.Phase is DistrictPhase.RepairPlanned or DistrictPhase.DelayPlanned
                ? "Пульт у перехода. Пока ты не переключишь сигнал, ничего не изменится."
                : "Я держу рейс, но не бесконечно. Марк дальше по этому тротуару. Приведи его сюда.";
            choices = new[] { new DialogueChoice("Я вернусь.", 0, 0, 0, 0, 0, 0, 0, RewardId: "district1.lida.plan") };
            return true;
        }
        MemoryAnchor? anchor = MemoryRuntime.Current.FindAnchor(MeetingEventId);
        bool persisted = MemoryRuntime.Current.HasPersisted(MeetingEventId);

        if (progress.Day >= 2 && persisted)
        {
            line = "Марк уже пришёл? Вчера мы говорили у депо. Я помню его имя.";
            choices = new[]
            {
                new DialogueChoice("Ты действительно помнишь вчера?", 2, 2, 1, 1, 2, 1, 0, RewardId: "district1.lida.morning.remember"),
                new DialogueChoice("Хорошо. Не будем торопиться.", 2, 1, 0, 0, 2, 1, 0, RewardId: "district1.lida.morning.slow"),
            };
            return true;
        }

        if (progress.Day >= 2 && anchor?.Status == MemoryAnchorStatus.Rejected)
        {
            line = "Марк? Не припоминаю. Вчера рейс ушёл как обычно, разве нет?";
            choices = new[]
            {
                new DialogueChoice("Похоже, не всё переживает утро.", 0, 1, 2, 2, 0, 1, 1, RewardId: "district1.lida.rejected.reflect"),
                new DialogueChoice("Неважно. Продолжим работу.", 0, 0, 0, 0, 0, 1, 0, RewardId: "district1.lida.rejected.work"),
            };
            return true;
        }

        if (progress.Day == 1 && anchor != null)
        {
            line = "Марк пришёл. Мы впервые поговорили не по расписанию. Захочет ли он это запомнить?";
            choices = new[]
            {
                new DialogueChoice("Спасибо. Я поговорю с ним.", 2, 2, 0, 1, 2, 1, 0, RewardId: "district1.lida.delay.thanks"),
                new DialogueChoice("Если передумаешь — скажи.", 1, 1, 0, 0, 2, 0, 0, RewardId: "district1.lida.delay.respect"),
            };
            return true;
        }

        if (progress.Day == 1)
        {
            line = "Сигнал перехода опять мигает. Последний рейс должен уйти точно по расписанию.";
            choices = new[]
            {
                new DialogueChoice("Задержи рейс на минуту. Я найду Марка.", 2, 3, 1, 2, 2, 3, 2, LidaDelayAction),
                new DialogueChoice("Сначала я закончу ремонт. Расписание важнее.", 1, 2, 2, 0, 1, 2, 1, LidaRepairAction),
            };
            return true;
        }

        line = "Сегодня всё идёт по расписанию.";
        choices = new[] { new DialogueChoice("Понял.", 0, 0, 0, 0, 0, 0, 0, RewardId: "district1.lida.routine") };
        return true;
    }

    private static bool TryGetMarkDialogue(NpcCharacter mark, HeroProgress progress, out string line, out DialogueChoice[] choices)
    {
        var episode = progress.DistrictEpisode;
        if (progress.Day == 1 && episode.Phase == DistrictPhase.WaitingForMark)
        {
            line = "Лида удерживает рейс? Я могу дойти до остановки. Но мне нужно самому решить, хочу ли я идти.";
            choices = new[]
            {
                new DialogueChoice("Она ждёт у перехода. Приходи, если хочешь.", 0, 0, 0, 0, 0, 0, 0, MarkInviteAction),
                new DialogueChoice("Не буду отвлекать тебя. Оставим всё как есть.", 0, 0, 0, 0, 0, 0, 0, MarkDeclineAction),
            };
            return true;
        }
        if (progress.Day == 1 && episode.Phase == DistrictPhase.MarkOnWay)
        {
            line = "Я иду к остановке. Поговорим там, вместе с Лидой.";
            choices = new[] { new DialogueChoice("До встречи там.", 0, 0, 0, 0, 0, 0, 0, RewardId: "district1.mark.on-way") };
            return true;
        }
        if (episode.Phase is DistrictPhase.Repaired or DistrictPhase.Missed)
        {
            line = episode.InvitationWithdrawn ? "Ты решил оставить всё как есть. Я не успел ответить на приглашение." :
                episode.MarkRefused ? "Я решил остаться. Не хочу, чтобы за меня решали." : "Рейс ушёл. Мы с Лидой сегодня не встретились.";
            choices = new[] { new DialogueChoice("Я услышал тебя.", 0, 0, 0, 0, 0, 0, 0, RewardId: "district1.mark.no-meeting") };
            return true;
        }
        MemoryAnchor? anchor = MemoryRuntime.Current.FindAnchor(MeetingEventId);
        MemoryWitness? witness = FindMarkWitness(anchor, mark.Id);
        bool persisted = MemoryRuntime.Current.HasPersisted(MeetingEventId);

        if (progress.Day >= 2 && persisted)
        {
            line = "Лида вчера назвала меня по имени. Странно... я точно помню эту встречу.";
            choices = new[]
            {
                new DialogueChoice("Значит, это осталось.", 2, 3, 2, 2, 2, 2, 1, RewardId: "district1.mark.morning.remember"),
                new DialogueChoice("Это твоя память. Не моя собственность.", 3, 4, 1, 1, 4, 1, 1, RewardId: "district1.mark.morning.respect"),
            };
            return true;
        }

        if (progress.Day >= 2 && anchor?.Status == MemoryAnchorStatus.Rejected)
        {
            line = "У меня ощущение, что я вчера куда-то собирался. Но дальше — пусто.";
            choices = new[]
            {
                new DialogueChoice("Я не буду придумывать за тебя.", 2, 3, 1, 1, 3, 1, 0, RewardId: "district1.mark.rejected.respect"),
                new DialogueChoice("Попробуем разобраться в другой раз.", 1, 2, 1, 1, 2, 1, 1, RewardId: "district1.mark.rejected.reflect"),
            };
            return true;
        }

        if (progress.Day == 1 && anchor != null && witness != null)
        {
            if (witness.UnderstoodEvent)
            {
                line = witness.Consented
                    ? "Я решил запомнить эту встречу. Если утром всё исчезнет — хотя бы проверим."
                    : "Я понял, что произошло, но не хочу это сохранять. Оставь мой выбор мне.";
                choices = new[] { new DialogueChoice("Я принимаю твой выбор.", 2, 3, 0, 0, 4, 1, 1, RewardId: "district1.mark.witness.respect") };
                return true;
            }

            line = "Лида правда задерживает рейс? Ты сделал это, чтобы мы встретились?";
            choices = new[]
            {
                new DialogueChoice("Да. Я объясню, что произошло, а решать — тебе.", 1, 0, 1, 1, 3, 1, 1, MarkOfferWitnessAction),
                new DialogueChoice("Ты ничего не обязан. Я не буду втягивать тебя.", 1, 0, 0, 0, 3, 1, 0, MarkLeaveAction),
            };
            return true;
        }

        if (progress.Day == 1)
        {
            line = "Я каждый день ухаживаю за двором за депо. Почти никто туда не заходит.";
            choices = new[]
            {
                new DialogueChoice("Расскажи про этот двор.", 3, 2, 1, 2, 2, 0, 0, RewardId: "district1.mark.courtyard"),
                new DialogueChoice("Не до этого сейчас.", -5, -4, 0, 0, 0, 1, 0, RewardId: "district1.mark.leave"),
            };
            return true;
        }

        line = "Доброе утро.";
        choices = new[] { new DialogueChoice("Доброе.", 1, 0, 0, 0, 1, 0, 0, RewardId: "district1.mark.routine") };
        return true;
    }

    internal static void RegisterMeetingCandidate(NpcCharacter lida, int day)
    {
        var ledger = MemoryRuntime.Current;
        int heroId = MemoryRuntime.HeroId;
        int markId = heroId + 2;

        var memoryEvent = new MemoryEvent
        {
            EventId = MeetingEventId,
            EventType = "shared_meeting",
            ParticipantIds = new List<int> { heroId, lida.Id, markId },
            LocationId = "tram-depot-square",
            ChoiceId = LidaDelayAction,
            Day = day,
            Consequence = "Лида задержала рейс, и Марк успел на встречу у депо.",
            HadAlternativeChoice = true,
        };

        if (ledger.RegisterEvent(memoryEvent))
        {
            ledger.RegisterAnchor(new MemoryAnchor
            {
                EventId = MeetingEventId,
                TraceId = "dispatcher-delay-note",
                Witnesses = new List<MemoryWitness>
                {
                    new() { NpcId = markId, UnderstoodEvent = false, Consented = false },
                },
            });
        }
    }

    private static void RecordMarkDecision(NpcCharacter mark)
    {
        MemoryAnchor? anchor = MemoryRuntime.Current.FindAnchor(MeetingEventId);
        MemoryWitness? witness = FindMarkWitness(anchor, mark.Id);
        if (witness == null || witness.UnderstoodEvent) return;

        witness.UnderstoodEvent = true;
        witness.Consented = mark.Trust >= 8f && mark.Friendliness >= 15f;
        witness.ConsentReason = witness.Consented
            ? "Марк понял событие и сам согласился проверить, переживёт ли память Сверку."
            : "Марк понял событие, но его текущее доверие недостаточно для согласия.";
    }

    private static MemoryWitness? FindMarkWitness(MemoryAnchor? anchor, int markId)
    {
        if (anchor == null) return null;
        foreach (var witness in anchor.Witnesses)
            if (witness.NpcId == markId) return witness;
        return null;
    }
}
