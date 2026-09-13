using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

public enum ArchiveOutcome { None, Meeting, Repair, Private, Refused, Unconfirmed }

// Archive permission is separate from consent to remember. Stable ledger events
// preserve each decision without a second save-state machine or replay rewards.
public static class DistrictArchiveStory
{
    public static readonly Vector3 NikaPosition = new(-5.5f, 0.12f, 11.5f);
    public const string RequestId = "district1.archive.request";
    public const string EntryId = "district1.archive.entry";
    private const string Prefix = "district1.archive.";
    private const string Ask = Prefix + "ask";
    private const string Publish = Prefix + "publish";
    private const string Repair = Prefix + "repair";
    private const string Private = Prefix + "private";
    private const string Refused = Prefix + "refused";
    private const string Unconfirmed = Prefix + "unconfirmed";

    public static ArchiveOutcome Outcome => MemoryRuntime.Current.FindEvent(EntryId)?.ChoiceId switch
    {
        Publish => ArchiveOutcome.Meeting,
        Repair => ArchiveOutcome.Repair,
        Private => ArchiveOutcome.Private,
        Refused => ArchiveOutcome.Refused,
        Unconfirmed => ArchiveOutcome.Unconfirmed,
        _ => ArchiveOutcome.None,
    };

    public static bool Requested => MemoryRuntime.Current.FindEvent(RequestId) != null;
    private static string PermissionId(int npcId) => npcId switch
    {
        1 => Prefix + "permission.1",
        2 => Prefix + "permission.2",
        _ => throw new ArgumentOutOfRangeException(nameof(npcId)),
    };
    public static bool? Permission(int npcId) => MemoryRuntime.Current.FindEvent(PermissionId(npcId))?.ChoiceId switch
    {
        "granted" => true,
        "refused" => false,
        _ => null,
    };
    private static bool MeetingRemembered => MemoryRuntime.Current.HasPersisted(FirstDistrictStory.MeetingEventId);
    private static bool HasRefusal => Permission(1) == false || Permission(2) == false;
    private static bool BothGranted => Permission(1) == true && Permission(2) == true;
    private static bool IsNika(NpcCharacter npc) => npc.Id == 3;

    public static bool CanApply(NpcCharacter npc, DialogueChoice choice, HeroProgress progress)
    {
        string? action = choice.ActionId;
        if (action == null || !action.StartsWith(Prefix, StringComparison.Ordinal)) return true;
        if (progress.Day < 2 || Outcome != ArchiveOutcome.None) return false;
        return action switch
        {
            Ask => IsNika(npc) && MeetingRemembered && !Requested,
            Publish => IsNika(npc) && MeetingRemembered && Requested && BothGranted,
            Repair => IsNika(npc) && progress.DistrictEpisode.Phase == DistrictPhase.Repaired,
            Private => IsNika(npc),
            Refused => IsNika(npc) && Requested && HasRefusal,
            Unconfirmed => IsNika(npc) && !MeetingRemembered && progress.DistrictEpisode.Phase != DistrictPhase.Repaired,
            _ => npc.Id is 1 or 2 && action == PermissionId(npc.Id) && Requested && MeetingRemembered && Permission(npc.Id) == null,
        };
    }

    public static void Apply(NpcCharacter npc, DialogueChoice choice, HeroProgress progress)
    {
        if (!CanApply(npc, choice, progress)) return;
        string? action = choice.ActionId;
        if (action == Ask)
            Register(RequestId, "archive_request", Ask, progress.Day, new() { 0, 3 }, "Герой предложил Нике проверить разрешения участников.");
        else if (npc.Id is 1 or 2 && action == PermissionId(npc.Id))
        {
            bool consent = npc.Trust >= 8f && npc.Friendliness >= 15f;
            Register(action, "archive_permission", consent ? "granted" : "refused", progress.Day,
                new() { 0, npc.Id }, consent ? "Участник разрешил Нике записать встречу со своим именем." :
                    "Участник помнит встречу, но не разрешает включать своё имя в архив.");
        }
        else if (IsNika(npc) && action is Publish or Repair or Private or Refused or Unconfirmed)
        {
            bool publicEntry = action is Publish or Repair;
            string consequence = action switch
            {
                Publish => "Ника записала встречу с разрешения Лиды и Марка.",
                Repair => "Ника и герой проверили исправный сигнал и записали результат ремонта без чужих имён.",
                Refused => "Ника отказалась публиковать встречу без разрешения каждого участника.",
                Unconfirmed => "Ника оставила запись без имён: чужая память не подтверждает встречу.",
                _ => "Герой и Ника оставили личную историю вне публичного архива.",
            };
            if (Register(EntryId, "archive_decision", action, progress.Day,
                action == Publish ? new() { 0, 1, 2, 3 } : new() { 0, 3 }, consequence) && publicEntry)
                MemoryRuntime.Current.RegisterAnchor(new MemoryAnchor
                {
                    EventId = EntryId,
                    TraceId = "depot-archive-page",
                    Witnesses = new() { new() { NpcId = 3, UnderstoodEvent = true, Consented = true,
                        ConsentReason = "Ника проверила допустимость записи и согласилась сохранить совместное свидетельство." } },
                });
        }
    }

    private static bool Register(string id, string type, string choice, int day, List<int> participants, string consequence) =>
        MemoryRuntime.Current.RegisterEvent(new MemoryEvent { EventId = id, EventType = type, ChoiceId = choice,
            Day = day, ParticipantIds = participants, LocationId = "tram-depot-square", Consequence = consequence, HadAlternativeChoice = true });

    private static DialogueChoice Action(string text, string action)
    {
        bool decision = action is Publish or Repair or Private or Refused or Unconfirmed;
        return new(text, 0, 0, decision ? 2 : 0, 0, 0, decision ? 1 : 0, 0, ActionId: action);
    }
    private static DialogueChoice Reply(string text, string id) => new(text, 0, 0, 0, 0, 0, 0, 0, RewardId: Prefix + id);

    public static bool TryDialogue(NpcCharacter npc, HeroProgress progress, out string line, out DialogueChoice[] choices)
    {
        line = "";
        choices = Array.Empty<DialogueChoice>();
        if (IsNika(npc))
        {
            if (progress.Day == 1)
            {
                line = "Я Ника, из архива. Вчерашних исправлений снова нет. Утром проверю журнал у остановки.";
                choices = new[] { Reply("Я тоже посмотрю утром.", "introduction") };
            }
            else if (Outcome != ArchiveOutcome.None)
            {
                line = TraceText(progress.Day)!;
                choices = new[] { Reply("Пусть эта история останется их выбором.", "outcome") };
            }
            else if (MeetingRemembered)
            {
                if (!Requested)
                {
                    line = "Двое помнят встречу, но память не разрешение на публикацию. Спросим Лиду и Марка отдельно?";
                    choices = new[] { Action("Я спрошу каждого про запись с именами.", Ask), Action("Оставим встречу личной.", Private) };
                }
                else if (HasRefusal)
                {
                    line = "Один из участников не разрешил запись. Я не стану вписывать его имя. Их встречу это не отменяет.";
                    choices = new[] { Action("Принимаю отказ. Не публикуем.", Refused) };
                }
                else if (BothGranted)
                {
                    line = "Оба разрешили запись с именами. Я сохраню их свидетельство. Проверим страницу следующим утром?";
                    choices = new[] { Action("Запишем встречу с их разрешения.", Publish), Action("Пока оставим её между нами.", Private) };
                }
                else
                {
                    line = Permission(1) == null ? "Сначала спроси Лиду у остановки. Разрешение помнить не заменяет разрешения публиковать." :
                        "Теперь спроси Марка. Одной подписи недостаточно.";
                    choices = new[] { Reply("Вернусь с их ответами.", "pending"), Action("Не будем публиковать личную историю.", Private) };
                }
            }
            else if (progress.DistrictEpisode.Phase == DistrictPhase.Repaired)
            {
                line = "Сигнал исправен, рейс ушёл вовремя. Я вижу результат твоей работы. Сохраним запись о ремонте, без выдуманной встречи?";
                choices = new[] { Action("Да. Запишем то, что можем проверить.", Repair), Action("Оставим только рабочую отметку.", Private) };
            }
            else
            {
                line = "Я не могу подтвердить общую встречу. Не буду заполнять пробелы чужими именами. Твой рассказ я услышала.";
                choices = new[] { Action("Не будем придумывать за других.", Unconfirmed) };
            }
            return true;
        }
        if (progress.Day < 2 || npc.Id is not (1 or 2)) return false;
        if (Requested && Outcome == ArchiveOutcome.None && MeetingRemembered)
        {
            bool? permission = Permission(npc.Id);
            line = permission == true ? "Я разрешил запись с моим именем. Ника может сохранить нашу встречу." :
                permission == false ? "Я помню встречу. Но не хочу видеть своё имя в архиве. Пожалуйста, не спрашивай снова." :
                "Ника собирает свидетельства? Я помню встречу, но о записи с моим именем хочу решить отдельно.";
            if (npc.Id == 1 && permission == true) line = "Я разрешила запись с моим именем. Ника может сохранить нашу встречу.";
            choices = permission == null ? new[] { Action("Разрешишь Нике записать встречу с твоим именем?", PermissionId(npc.Id)), Reply("Не тороплю. Поговорим позже.", "later") } :
                new[] { Reply("Я передам твой ответ без изменений.", "answer") };
            return true;
        }
        var entry = MemoryRuntime.Current.FindEvent(EntryId);
        if (entry == null || progress.Day <= entry.Day) return false;
        line = Outcome switch
        {
            ArchiveOutcome.Meeting when MemoryRuntime.Current.HasPersisted(EntryId) => npc.Id == 1 ?
                "Доброе утро, Марк. Наша страница на месте. Сегодня после рейса загляну в твой двор." :
                "Лида помнит мой двор. И страница у Ники не исчезла. Теперь у этой встречи есть продолжение.",
            ArchiveOutcome.Private or ArchiveOutcome.Refused when MeetingRemembered => npc.Id == 1 ?
                "Доброе утро, Марк. Наших имён в архиве нет. Но я помню, о чём мы говорили." :
                "В архиве нас нет. Спасибо, что не стал решать за нас. Лиду я всё равно помню.",
            ArchiveOutcome.Repair when npc.Id == 1 && MemoryRuntime.Current.HasPersisted(EntryId) => "Запись о ремонте осталась. Ты выполнил обещание, я могу доверить тебе следующий участок.",
            _ => "",
        };
        if (line.Length == 0) return false;
        choices = new[] { Reply("Доброе утро. Продолжим с этого.", "morning") };
        return true;
    }

    public static string? TraceText(int day)
    {
        var entry = MemoryRuntime.Current.FindEvent(EntryId);
        if (entry == null) return null;
        bool survived = day > entry.Day && MemoryRuntime.Current.HasPersisted(EntryId);
        if (day > entry.Day && !survived && Outcome is ArchiveOutcome.Meeting or ArchiveOutcome.Repair)
            return "Страница не пережила Сверку. Ника не станет выдавать исчезнувшую запись за сохранённое свидетельство.";
        return Outcome switch
        {
            ArchiveOutcome.Meeting => survived ? "Страница сохранилась: встреча Лиды и Марка. Рядом их разрешения и подпись Ники." :
                "Ника записала встречу с разрешения обоих участников. Страница ещё не прошла Сверку.",
            ArchiveOutcome.Repair => survived ? "Страница сохранилась: исправный сигнал. Подписи героя и Ники. Чужой встречи здесь нет." :
                "Ника записала проверенный ремонт. Утром станет ясно, останется ли страница.",
            ArchiveOutcome.Private => MeetingRemembered ? "Личная история не опубликована. Пустая страница не отменяет общей памяти." :
                "Ника не добавила отдельное свидетельство в архив. В журнале осталась только рабочая отметка, без чужих имён.",
            ArchiveOutcome.Refused => "Ника не опубликовала встречу: нет разрешения каждого участника. Их память осталась личной.",
            _ => "В архиве нет подтверждённой встречи. Ника не стала придумывать чужие воспоминания.",
        };
    }

    public static (Vector3 position, string name) Objective(int day)
    {
        var entry = MemoryRuntime.Current.FindEvent(EntryId);
        if (entry != null) return day <= entry.Day ? (FirstDistrictEpisode.Rest, "Проверить следующее утро") : (FirstDistrictEpisode.Trace, "Страница Ники");
        if (!Requested || HasRefusal || BothGranted) return (NikaPosition, "Ника у журнала");
        return Permission(1) == null ? (FirstDistrictEpisode.Stop, "Разрешение Лиды") : (FirstDistrictEpisode.MarkMeeting, "Разрешение Марка");
    }
}
