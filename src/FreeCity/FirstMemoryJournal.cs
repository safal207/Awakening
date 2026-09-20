namespace Probuzhdenie.FreeCity;

public static class FirstMemoryJournal
{
    public const string SignalFactId = "first_memory.journal.signal_fact";
    public const string SignalQuestionId = "first_memory.journal.signal_question";
    public const string RepairPromiseId = "first_memory.journal.repair_promise";
    public const string RoutineRepairFactId = "first_memory.journal.routine_repair";
    public const string MeetingId = "first_memory.journal.lida_mark_meeting";
    public const string WitnessFactId = "first_memory.journal.mark_consent";
    public const string RepairFactId = "first_memory.journal.repair_completed";
    public const string ArchiveFactId = "first_memory.journal.nika_archive";

    public static void RecordInvestigation(HeroProgress progress)
    {
        ObservationJournal journal = ObservationJournalState.For(progress);
        journal.TryAdd(
            SignalFactId,
            ObservationKind.Fact,
            "Контроллер сигнала исправен, но журнал каждый раз возвращается к эталонной записи.",
            progress.Day);
        journal.TryAdd(
            SignalQuestionId,
            ObservationKind.Question,
            "Почему Сверка возвращает сигнал к одной и той же неисправности?",
            progress.Day);
        journal.TryAdd(
            RepairPromiseId,
            ObservationKind.Promise,
            "Закончить ремонт до последнего трамвая, если сбой повторится.",
            progress.Day);
    }

    public static void RecordRoutineRepair(HeroProgress progress)
    {
        ObservationJournal journal = ObservationJournalState.For(progress);
        journal.TryAdd(
            RoutineRepairFactId,
            ObservationKind.Fact,
            "Сигнал восстановлен обычным способом. Нужно проверить, повторится ли сбой утром.",
            progress.Day,
            ObservationStatus.Completed);
        journal.TryAdd(
            SignalQuestionId,
            ObservationKind.Question,
            "Повторится ли та же поломка после Сверки?",
            progress.Day);
    }

    public static void RecordMeetingBranch(HeroProgress progress)
    {
        ObservationJournal journal = ObservationJournalState.For(progress);
        journal.TrySetStatus(
            RepairPromiseId,
            ObservationStatus.NotCompleted,
            "Обещанный ремонт сегодня не закончен: время ушло на окно встречи Лиды и Марка.");
        journal.TryAdd(
            MeetingId,
            ObservationKind.OpenMeeting,
            "Лида и Марк встретились на площади. Пока неизвестно, переживёт ли встреча Сверку.",
            progress.Day);
    }

    public static void RecordRepairBranch(HeroProgress progress)
    {
        ObservationJournal journal = ObservationJournalState.For(progress);
        journal.TrySetStatus(
            RepairPromiseId,
            ObservationStatus.Completed,
            "Обещание выполнено: сигнал отремонтирован до последнего трамвая.");
        journal.TryAdd(
            RepairFactId,
            ObservationKind.Fact,
            "Лида подтвердила завершённый ремонт; встречи с Марком сегодня не было.",
            progress.Day,
            ObservationStatus.Completed);
    }

    public static void RecordWitnessAccepted(HeroProgress progress)
    {
        ObservationJournal journal = ObservationJournalState.For(progress);
        journal.TrySetStatus(
            MeetingId,
            ObservationStatus.Completed,
            "Марк добровольно согласился помнить встречу; диспетчерская запись стала общим следом.");
        journal.TryAdd(
            WitnessFactId,
            ObservationKind.Fact,
            "Согласие Марка получено явно. Встречу можно считать совместным воспоминанием.",
            progress.Day,
            ObservationStatus.Completed);
    }

    public static void RecordWitnessDeclined(HeroProgress progress)
    {
        ObservationJournal journal = ObservationJournalState.For(progress);
        journal.TrySetStatus(
            MeetingId,
            ObservationStatus.NotCompleted,
            "Марк не дал согласия закреплять встречу. Это решение остаётся его границей.");
        journal.TryAdd(
            WitnessFactId,
            ObservationKind.Fact,
            "Согласие Марка на общий якорь не получено.",
            progress.Day,
            ObservationStatus.NotCompleted);
    }

    public static void RecordNikaArchive(HeroProgress progress)
    {
        ObservationJournalState.For(progress).TryAdd(
            ArchiveFactId,
            ObservationKind.Fact,
            "Ника сохранила только факт встречи, место и подтверждённое согласие — без догадок о чувствах участников.",
            progress.Day,
            ObservationStatus.Completed);
    }
}
